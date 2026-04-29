using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattleMap : Node2D
{
	[Export] public float HexSize = 65f; 
	[Export] public BattleUI UI;
	[Export] public DialogueUI ConversationUI; 

	internal int ScanningRange = 3; 
	internal int CurrentTurn = 1; 

	internal GlobalData _globalData;
	internal Dictionary<Vector2I, Node2D> HexGrid = new Dictionary<Vector2I, Node2D>();
	internal Dictionary<Vector2I, MapEntity> HexContents = new Dictionary<Vector2I, MapEntity>();
	internal List<Vector2I> SelectedHexes = new List<Vector2I>(); 
	
	internal TacticalCamera MapCamera;
	
	internal Node2D EntityLayer = new Node2D(); 
	internal Node2D EnvironmentLayer = new Node2D(); 
	internal Node2D RadiationLayer = new Node2D(); 
	internal Node2D FogLayer = new Node2D(); 

	internal AudioStreamPlayer SfxPlayer; 
	internal AudioStreamPlayer LaserPlayer;
	internal AudioStreamPlayer ExplosionPlayer;

	internal HashSet<Vector2I> AsteroidHexes = new HashSet<Vector2I>(); 
	internal HashSet<Vector2I> RadiationHexes = new HashSet<Vector2I>(); 

	public CombatManager Combat { get; private set; }
	public HazardManager Hazards { get; private set; }
	public FogOfWarManager Fog { get; private set; }

	public int ActiveMovementTweens = 0;
	public bool IsFleetMoving => ActiveMovementTweens > 0;
	private readonly List<Action> _pendingMovementCallbacks = new List<Action>();

	private int _maxMapRadius = 35; 
	private PackedScene _hexScene = GD.Load<PackedScene>("res://hex_tile.tscn");
	
	private CanvasLayer _bgLayer = new CanvasLayer { Layer = -1 }; 
	private Node2D _gridLayer = new Node2D();  
	private Node2D _highlightLayer = new Node2D(); 
	
	private Polygon2D _hoverHighlight;
	private Vector2I _currentHoveredHex;
	
	// --- Hover Tooltip UI ---
	private Label _hoverTooltip;
	
	// --- Radar variables ---
	private Polygon2D _radarHighlight; 
	public bool IsTargetingLongRange = false;

	internal SelectionBox SelectionBoxUI; 
	private AudioStreamPlayer _bgmPlayer;

	internal bool IsJumping = false; 
	internal MapEntity CurrentlyViewedShip = null;
	internal bool IsFleetTravelMode { get; private set; } = false;
	private readonly Dictionary<string, int> _playerShipHotkeys = new Dictionary<string, int>();

	// --- STRANDED FLEET PROTOCOL UI ---
	private CenterContainer _strandedMenuWrapper;
	private bool _distressSignalAmbush = false; 
	private bool _isWaitingForDistressSignal = false; 
	
	// --- DYNAMIC TRADE BUTTON & SHOP UI ---
	private Button _btnTrade;
	private CenterContainer _shopMenuWrapper;
	private VBoxContainer _shopItemList;

	// --- NEW: EQUIP GEAR UI ---
	private Button _btnEquip;
	private CenterContainer _equipMenuWrapper;
	private VBoxContainer _equipItemList;
	private FleetInventoryService _inventoryService;
	private OfficerService _officerService;
	private ShipContextService _shipContextService;
	private ExplorationActionService _explorationActionService;
	private ExplorationTurnService _explorationTurnService;
	private DistressSignalService _distressSignalService;
	private JumpService _jumpService;
	private ShipMenuPresenterService _shipMenuPresenterService;
	private TerminalMenuPresenterService _terminalMenuPresenterService;
	private StrandedMenuPresenterService _strandedMenuPresenterService;
	private FleetCommandService _fleetCommandService;
	private HighlightPresenterService _highlightPresenterService;
	private LongRangeScanService _longRangeScanService;
	private BattleMapSaveSnapshotService _battleMapSaveSnapshotService;
	private BattleActionButtonPresenterService _battleActionButtonPresenterService;
	private HoverPresentationService _hoverPresentationService;
	private BattleMapAnimationService _battleMapAnimationService;
	private AudioPlaybackService _audioPlaybackService;
	private MovementExecutionService _movementExecutionService;
	private OfficerPanelPresenterService _officerPanelPresenterService;
	private PanelContainer _officerMenuPanel;
	private Label _officerMenuTitle;
	private TextureRect _officerPortraitDisplay;
	private Label _officerDetailsLabel;
	private const string ExplorationMusicPath = "res://Sounds/battle_theme.mp3";
	private const string CombatMusicPath = "res://Sounds/fractured_combat_theme.wav";

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (_globalData != null) _inventoryService = new FleetInventoryService(_globalData);
		if (_globalData != null) _officerService = new OfficerService(_globalData);
		if (_globalData != null) _shipContextService = new ShipContextService(_globalData);
		if (_globalData != null) _explorationActionService = new ExplorationActionService(_globalData);
		if (_globalData != null) _distressSignalService = new DistressSignalService(_globalData);
		if (_globalData != null) _jumpService = new JumpService(_globalData);
		if (_globalData != null) _longRangeScanService = new LongRangeScanService(_globalData);
		_explorationTurnService = new ExplorationTurnService();
		_shipMenuPresenterService = new ShipMenuPresenterService();
		_terminalMenuPresenterService = new TerminalMenuPresenterService();
		_strandedMenuPresenterService = new StrandedMenuPresenterService();
		_fleetCommandService = new FleetCommandService();
		_highlightPresenterService = new HighlightPresenterService();
		_battleMapSaveSnapshotService = new BattleMapSaveSnapshotService();
		_battleActionButtonPresenterService = new BattleActionButtonPresenterService();
		_hoverPresentationService = new HoverPresentationService();
		_battleMapAnimationService = new BattleMapAnimationService();
		_audioPlaybackService = new AudioPlaybackService();
		_movementExecutionService = new MovementExecutionService();
		_officerPanelPresenterService = new OfficerPanelPresenterService();
		if (_globalData != null && _globalData.CurrentTurn > 0) CurrentTurn = _globalData.CurrentTurn;
		
		Texture2D cursorTex = GD.Load<Texture2D>("res://Assets/UI/Cursor.png");
		if (cursorTex != null)
		{
			Input.SetCustomMouseCursor(cursorTex, Input.CursorShape.Arrow, Vector2.Zero);
		}

		Combat = new CombatManager(this);
		Hazards = new HazardManager(this);
		Fog = new FogOfWarManager(this);

		AddChild(_bgLayer);
		AddChild(_gridLayer);
		AddChild(RadiationLayer); 
		AddChild(EnvironmentLayer); 
		AddChild(FogLayer); 
		AddChild(_highlightLayer);
		AddChild(EntityLayer);

		SelectionBoxUI = new SelectionBox();
		AddChild(SelectionBoxUI);

		_hoverHighlight = new Polygon2D();
		Vector2[] points = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float angle_deg = 60 * i - 30;
			float angle_rad = Mathf.DegToRad(angle_deg);
			points[i] = new Vector2(HexSize * Mathf.Cos(angle_rad), HexSize * Mathf.Sin(angle_rad));
		}
		_hoverHighlight.Polygon = points;
		_hoverHighlight.Color = new Color(0f, 1f, 1f, 0.4f); 
		_hoverHighlight.Visible = false; 
		AddChild(_hoverHighlight);

		_hoverTooltip = new Label();
		_hoverTooltip.Visible = false;
		
		StyleBoxFlat tooltipBox = new StyleBoxFlat();
		tooltipBox.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f);
		tooltipBox.BorderWidthBottom = 1; tooltipBox.BorderWidthTop = 1; tooltipBox.BorderWidthLeft = 1; tooltipBox.BorderWidthRight = 1;
		tooltipBox.BorderColor = new Color(1f, 0f, 0f, 0.6f);
		tooltipBox.ContentMarginLeft = 10; tooltipBox.ContentMarginRight = 10; tooltipBox.ContentMarginTop = 10; tooltipBox.ContentMarginBottom = 10;
		
		_hoverTooltip.AddThemeStyleboxOverride("normal", tooltipBox);
		_hoverTooltip.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
		_hoverTooltip.AddThemeFontSizeOverride("font_size", 14);

		CanvasLayer tooltipLayer = new CanvasLayer { Layer = 100 };
		AddChild(tooltipLayer);
		tooltipLayer.AddChild(_hoverTooltip); 

		_radarHighlight = new Polygon2D();
		int circlePoints = 32;
		Vector2[] radarPoints = new Vector2[circlePoints];
		float radiusPixels = 10f * HexSize * Mathf.Sqrt(3); 
		for(int i = 0; i < circlePoints; i++) {
			float angle = (Mathf.Pi * 2 / circlePoints) * i;
			radarPoints[i] = new Vector2(Mathf.Cos(angle) * radiusPixels, Mathf.Sin(angle) * radiusPixels);
		}
		_radarHighlight.Polygon = radarPoints;
		_radarHighlight.Color = new Color(0f, 1f, 0.3f, 0.2f); 
		_radarHighlight.Visible = false;
		AddChild(_radarHighlight);

		SetupCamera();
		SetupAudio(); 
		ConnectUIButtons(); 
		BuildStrandedMenu(); 
		BuildShopUI(); 
		BuildEquipUI(); // --- NEW: BUILD THE EQUIP UI ---
		BuildOfficerPanel();
		
		_btnTrade = new Button();
		_btnTrade.Text = "ACCESS OUTPOST EXCHANGE"; 
		_btnTrade.Visible = false;
		_btnTrade.CustomMinimumSize = new Vector2(0, 40);
		_btnTrade.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_btnTrade.AddThemeColorOverride("font_color", new Color(0f, 1f, 0.8f));
		_btnTrade.Pressed += OpenShop; 
		
		if (UI != null && UI.BtnSalvage != null)
		{
			UI.BtnSalvage.GetParent().AddChild(_btnTrade);
		}
		else
		{
			AddChild(_btnTrade); 
		}

		// --- NEW: THE FLEET LOADOUT BUTTON ---
		_btnEquip = new Button();
		_btnEquip.Text = "FLEET LOADOUT"; 
		_btnEquip.Visible = false;
		_btnEquip.CustomMinimumSize = new Vector2(0, 40);
		_btnEquip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_btnEquip.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0f));
		_btnEquip.Pressed += OpenEquipMenu; 

		if (UI != null && UI.BtnRepair != null)
		{
			UI.BtnRepair.GetParent().AddChild(_btnEquip);
		}
		else
		{
			AddChild(_btnEquip); 
		}
		
		MapSpawner.SetupSpaceBackground(_bgLayer, GetViewportRect().Size);
		MapSpawner.GenerateGrid(_maxMapRadius, HexSize, _hexScene, _gridLayer, HexGrid);
		
		Fog.GenerateFog(FogLayer, HexGrid.Keys, HexSize);

		if (_globalData != null && !string.IsNullOrEmpty(_globalData.SavedSystem) && _globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			Fog.SetExploredHexes(_globalData.ExploredSystems[_globalData.SavedSystem].ExploredHexes);
		}

		MapSpawner.PopulateMapFromMemory(_globalData, _maxMapRadius, HexSize, HexGrid, HexContents, EntityLayer, EnvironmentLayer, RadiationLayer, AsteroidHexes, RadiationHexes);
		
		if (_globalData != null && !string.IsNullOrEmpty(_globalData.SavedSystem) && _globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			SystemData currentSys = _globalData.ExploredSystems[_globalData.SavedSystem];
			if (currentSys.Outposts != null)
			{
				foreach (var outpost in currentSys.Outposts)
				{
					MapEntity outpostEntity = new MapEntity {
						Name = outpost.Name,
						Type = GameConstants.EntityTypes.Outpost,
						Details = "Trading Hub",
						MaxHP = 1500, CurrentHP = 1500,
						MaxShields = 500, CurrentShields = 500
					};
					
					string safeSpritePath = outpost.SpritePath.Replace(".jpg", ".png");
					MapSpawner.SpawnEntityAtHex(outpost.HexPosition, safeSpritePath, outpostEntity, 0.35f, HexSize, HexGrid, HexContents, EntityLayer);
				}
			}
		}

		foreach (var kvp in HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.StarGate && GodotObject.IsInstanceValid(kvp.Value.VisualSprite))
			{
				CpuParticles2D vortex = new CpuParticles2D();
				vortex.Name = "VortexParticles";
				vortex.Amount = 250; 
				vortex.Lifetime = 1.5f;
				vortex.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
				vortex.EmissionSphereRadius = 35f; 
				vortex.Gravity = Vector2.Zero;
				vortex.Emitting = true; 
				
				vortex.RadialAccelMin = -30f; 
				vortex.RadialAccelMax = -50f; 
				
				vortex.TangentialAccelMin = 100f; 
				vortex.TangentialAccelMax = 180f; 
				
				vortex.ScaleAmountMin = 1.5f; 
				vortex.ScaleAmountMax = 3.0f; 
				
				vortex.Color = new Color(0.2f, 0.8f, 1.0f, 0.8f); 
				vortex.ZIndex = 1; 
				
				kvp.Value.VisualSprite.AddChild(vortex);
			}
		}
		
		Fog.UpdateVisibility();
		CenterCameraOnFleet(); 
		
		if (_globalData != null && _globalData.InCombat) Combat.RestoreCombatState(_globalData.CurrentQueueIndex);
		else Combat.CheckForCombatTrigger();

		if (UI != null)
		{
			UI.EndTurnButton.Visible = Combat.InCombat; 
			UI.RepairFleetButton.Visible = !Combat.InCombat; 
			UI.InventoryButton.Visible = !Combat.InCombat;
			UI.GameOverPanel.Visible = false;
			UI.InfoPanel.Visible = false;
			UI.CombatLogPanel.Visible = true;
			if (UI.CombatLogText != null && string.IsNullOrWhiteSpace(UI.CombatLogText.Text))
			{
				UI.CombatLogText.Text = "[color=gray]--- ACTION LOG READY ---[/color]\n";
			}
			UI.AttackButton.Visible = false;
			UI.JumpButton.Visible = false;
			UI.ShipMenuPanel.Position = new Vector2(GetCollapsedShipMenuX(), UI.ShipMenuPanel.Position.Y);

			UpdateResourceUI();
		}
	}

	internal bool IsHexWalkable(Vector2I hex)
	{
		if (!HexGrid.ContainsKey(hex)) return false;
		
		if (HexContents.ContainsKey(hex))
		{
			string type = HexContents[hex].Type;
			if (type == GameConstants.EntityTypes.Planet ||
				type == GameConstants.EntityTypes.BasePlanetPlayerStart ||
				type == GameConstants.EntityTypes.CelestialBody ||
				type == GameConstants.EntityTypes.PlayerFleet ||
				type == GameConstants.EntityTypes.EnemyFleet ||
				type == GameConstants.EntityTypes.StarGate ||
				type == GameConstants.EntityTypes.Outpost)
			{
				return false; 
			}
		}
		return true;
	}

	internal void UpdateResourceUI()
	{
		if (UI == null || UI.InventoryDisplay == null || _globalData == null) return;
		
		float raw = _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle();
		float energy = _globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle();
		float tech = _globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle();

		UI.InventoryDisplay.Text = $"{GameConstants.ResourceKeys.RawMaterials}: {raw:0.##}\n{GameConstants.ResourceKeys.EnergyCores}: {energy:0.##}\n{GameConstants.ResourceKeys.AncientTech}: {tech:0.##}";
	}

	private void ConnectUIButtons()
	{
		if (UI == null) return;
		UI.EndTurnButton.Pressed += OnEndTurnPressed;
		UI.SaveGameButton.Pressed += OnSaveGamePressed;
		UI.RepairFleetButton.Pressed += OnRepairFleetPressed;
		UI.InventoryButton.Pressed += OnInventoryPressed;
		UI.MainMenuButton.Pressed += OnMainMenuPressed;
		UI.JumpButton.Pressed += OnJumpPressed;
		UI.AttackButton.Pressed += OnAttackPressed;
		if (UI.MissileButton != null) UI.MissileButton.Pressed += OnMissilePressed;
		UI.GameOverReturnButton.Pressed += OnMainMenuPressed;
		UI.BtnRepair.Pressed += OnRepairPressed;
		UI.BtnScan.Pressed += OnScanPressed;
		UI.BtnSalvage.Pressed += OnSalvagePressed;
		UI.CodexButton.Pressed += OnCodexPressed;
		UI.CloseMenuButton.Pressed += () => ToggleShipMenu(false);
		UI.TurnLabel.Text = $"TURN {CurrentTurn}";
		
		if (UI.BtnLongRange != null) UI.BtnLongRange.Pressed += OnLongRangePressed;
	}

	// ==========================================
	// OUTPOST SHOP UI
	// ==========================================

	private void BuildShopUI()
	{
		CanvasLayer shopLayer = new CanvasLayer { Layer = 160 };
		AddChild(shopLayer);

		_shopMenuWrapper = new CenterContainer();
		_shopMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_shopMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_shopMenuWrapper.Visible = false;
		shopLayer.AddChild(_shopMenuWrapper);

		PanelContainer shopPanel = new PanelContainer();
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
		style.BorderWidthTop = 2; style.BorderWidthBottom = 2; style.BorderWidthLeft = 2; style.BorderWidthRight = 2;
		style.BorderColor = new Color(0f, 1f, 0.8f, 0.8f);
		style.ContentMarginLeft = 25; style.ContentMarginRight = 25; style.ContentMarginTop = 20; style.ContentMarginBottom = 20;
		shopPanel.AddThemeStyleboxOverride("panel", style);
		_shopMenuWrapper.AddChild(shopPanel);

		VBoxContainer mainVBox = new VBoxContainer();
		mainVBox.AddThemeConstantOverride("separation", 15);
		shopPanel.AddChild(mainVBox);

		Label title = new Label();
		title.Text = "=== BLACK MARKET EXCHANGE ===";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeColorOverride("font_color", new Color(0f, 1f, 0.8f));
		title.AddThemeFontSizeOverride("font_size", 18);
		mainVBox.AddChild(title);

		ScrollContainer scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(500, 350);
		mainVBox.AddChild(scroll);

		_shopItemList = new VBoxContainer();
		_shopItemList.AddThemeConstantOverride("separation", 15);
		scroll.AddChild(_shopItemList);

		Button closeBtn = new Button();
		closeBtn.Text = "CLOSE TERMINAL";
		closeBtn.CustomMinimumSize = new Vector2(0, 40);
		closeBtn.Pressed += () => _shopMenuWrapper.Visible = false;
		mainVBox.AddChild(closeBtn);
	}

	private void OpenShop()
	{
		if (_inventoryService == null || _terminalMenuPresenterService == null) return;
		string outpostName = GetCurrentTradeOutpostName();
		_terminalMenuPresenterService.PopulateShopMenu(
			_shopItemList,
			_inventoryService.GetShopItems(),
			_inventoryService.CanAfford,
			BuyItem,
			_inventoryService.GetAncientTechUnitCount(),
			SellAncientTech,
			_inventoryService.GetSellableInventoryEntries(outpostName),
			outpostName,
			SellInventoryItem);

		_shopMenuWrapper.Visible = true;
	}

	private void BuyItem(string itemID)
	{
		if (_inventoryService == null) return;
		EquipmentData item = _inventoryService.GetEquipment(itemID);
		if (item == null) return;

		if (_inventoryService.BuyItem(itemID))
		{
			UpdateResourceUI();
			
			if (UI != null) UI.CombatLogPanel.Visible = true;
			LogCombatMessage($"\n[color=green]--- TRANSACTION APPROVED ---[/color]");
			LogCombatMessage($"Purchased: [color=yellow]{item.Name}[/color] (-{item.CostTech} Tech, -{item.CostRaw} Raw)");
			ApplyOfficerApprovalEvent(
				OfficerApprovalEventType.PurchaseEquipment,
				new OfficerApprovalContext
				{
					ItemName = item.Name,
					ItemCategory = item.Category
				});
			OnSaveGamePressed(); // Auto-save after a purchase
			
		if (SfxPlayer != null)
		{
			_audioPlaybackService?.TryPlay(SfxPlayer, "res://Sounds/laser.mp3", 1.2f);
		}

			OpenShop(); 
		}
	}

	private void SellAncientTech()
	{
		if (_inventoryService == null || !_inventoryService.SellAncientTech()) return;

		UpdateResourceUI();

		if (UI != null) UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=green]--- EXCHANGE COMPLETED ---[/color]");
		LogCombatMessage($"Sold 1 [color=yellow]{GameConstants.ResourceKeys.AncientTech}[/color] for [color=cyan]{GameConstants.StandardEquipment.AncientTechSaleRaw} {GameConstants.ResourceKeys.RawMaterials}[/color].");
		ApplyOfficerApprovalEvent(OfficerApprovalEventType.SellAncientTech);
		OnSaveGamePressed();

		OpenShop();
	}

	private void SellInventoryItem(string itemID, string itemName, int rawValue)
	{
		if (_inventoryService == null || !_inventoryService.SellInventoryItem(itemID, rawValue)) return;

		UpdateResourceUI();
		OnSaveGamePressed();

		if (UI != null) UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=green]--- EXCHANGE COMPLETED ---[/color]");
		LogCombatMessage($"Sold [color=yellow]{itemName}[/color] for [color=cyan]{rawValue} {GameConstants.ResourceKeys.RawMaterials}[/color].");

		OpenShop();
	}

	// ==========================================
	// FLEET LOADOUT UI (NEW)
	// ==========================================

	private void BuildEquipUI()
	{
		CanvasLayer equipLayer = new CanvasLayer { Layer = 165 };
		AddChild(equipLayer);

		_equipMenuWrapper = new CenterContainer();
		_equipMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_equipMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_equipMenuWrapper.Visible = false;
		equipLayer.AddChild(_equipMenuWrapper);

		PanelContainer equipPanel = new PanelContainer();
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
		style.BorderWidthTop = 2; style.BorderWidthBottom = 2; style.BorderWidthLeft = 2; style.BorderWidthRight = 2;
		style.BorderColor = new Color(1f, 0.6f, 0f, 0.8f);
		style.ContentMarginLeft = 25; style.ContentMarginRight = 25; style.ContentMarginTop = 20; style.ContentMarginBottom = 20;
		equipPanel.AddThemeStyleboxOverride("panel", style);
		_equipMenuWrapper.AddChild(equipPanel);

		VBoxContainer mainVBox = new VBoxContainer();
		mainVBox.AddThemeConstantOverride("separation", 15);
		equipPanel.AddChild(mainVBox);

		Label title = new Label();
		title.Text = "=== FLEET UPGRADES & LOADOUT ===";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0f));
		title.AddThemeFontSizeOverride("font_size", 18);
		mainVBox.AddChild(title);

		ScrollContainer scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(500, 350);
		mainVBox.AddChild(scroll);

		_equipItemList = new VBoxContainer();
		_equipItemList.AddThemeConstantOverride("separation", 15);
		scroll.AddChild(_equipItemList);

		Button closeBtn = new Button();
		closeBtn.Text = "CLOSE TERMINAL";
		closeBtn.CustomMinimumSize = new Vector2(0, 40);
		closeBtn.Pressed += () => _equipMenuWrapper.Visible = false;
		mainVBox.AddChild(closeBtn);
	}

	private void OpenEquipMenu()
	{
		if (_inventoryService == null || _terminalMenuPresenterService == null || CurrentlyViewedShip == null) return;
		string shipName = CurrentlyViewedShip.Name;

		ShipLoadout loadout = _inventoryService.GetOrCreateLoadout(shipName);
		if (loadout == null) return;
		
		string wpn = _inventoryService.GetEquippedItemName(loadout.WeaponID);
		string shld = _inventoryService.GetEquippedItemName(loadout.ShieldID);
		string armr = _inventoryService.GetEquippedItemName(loadout.ArmorID);
		string missile = _inventoryService.GetActiveMissileName(shipName);
		_terminalMenuPresenterService.PopulateEquipMenu(
			_equipItemList,
			shipName,
			wpn,
			shld,
			armr,
			missile,
			_inventoryService.GetGroupedEquippableInventory(),
			itemId => EquipItem(shipName, itemId));
		
		_equipMenuWrapper.Visible = true;
	}

	private string GetCurrentTradeOutpostName()
	{
		MapEntity outpost = CurrentlyViewedShip == null ? null : _shipContextService?.GetAdjacentOutpost(CurrentlyViewedShip, HexContents);
		return string.IsNullOrEmpty(outpost?.Name) ? "Nearest Outpost" : outpost.Name;
	}

	private void EquipItem(string shipName, string itemID)
	{
		if (_inventoryService == null) return;
		EquipmentData itemToEquip = _inventoryService.GetEquipment(itemID);
		if (itemToEquip == null || !_inventoryService.EquipItem(shipName, itemID)) return;
		if (CurrentlyViewedShip != null && CurrentlyViewedShip.Name == shipName)
		{
			_inventoryService.ApplyLoadoutStats(CurrentlyViewedShip);
		}
		
		if (SfxPlayer != null)
		{
			_audioPlaybackService?.TryPlay(SfxPlayer, "res://Sounds/laser.mp3", 0.8f);
		}

		if (UI != null) UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=green]--- LOADOUT UPDATED ---[/color]");
		LogCombatMessage($"{shipName} equipped [color=yellow]{itemToEquip.Name}[/color].");
		ApplyOfficerApprovalEvent(
			OfficerApprovalEventType.EquipItem,
			new OfficerApprovalContext
			{
				ActingShipName = shipName,
				ItemName = itemToEquip.Name,
				ItemCategory = itemToEquip.Category
			});
		OnSaveGamePressed(); // Save state

		OpenEquipMenu(); // Refresh the UI to reflect the swap
	}

	// ==========================================
	// STRANDED FLEET PROTOCOL UI
	// ==========================================
	private void BuildStrandedMenu()
	{
		if (_strandedMenuPresenterService == null) return;
		_strandedMenuWrapper = _strandedMenuPresenterService.BuildMenu(this, OnDistressSignalPressed, OnAbandonFleetPressed);
	}

	private void ShowStrandedMenu()
	{
		if (_strandedMenuWrapper != null && !_strandedMenuWrapper.Visible)
		{
			_strandedMenuWrapper.Visible = true;
			if (UI != null) UI.CombatLogPanel.Visible = true;
			LogCombatMessage("\n[color=red]*** FLEET STRANDED: OUT OF FUEL ***[/color]");
		}
	}

	private void OnAbandonFleetPressed()
	{
		if (_globalData != null) _globalData.ResetForNewGame();
		OnMainMenuPressed();
	}

	private void OnDistressSignalPressed()
	{
		if (_distressSignalService == null) return;
		_strandedMenuWrapper.Visible = false;
		_isWaitingForDistressSignal = true; 
		
		LogCombatMessage("\n[color=yellow]--- BROADCASTING WIDE-BAND DISTRESS SIGNAL ---[/color]");
		LogCombatMessage("Awaiting response...");
		ApplyOfficerApprovalEvent(OfficerApprovalEventType.DistressSignalBroadcast);
		
		GetTree().CreateTimer(1.5f).Timeout += () => 
		{
			DistressSignalResult result = _distressSignalService.ResolveDistressSignal();
			if (result.RescueArrived)
			{
				UpdateResourceUI();
				
				LogCombatMessage($"[color=green]SIGNAL RECEIVED![/color] A passing smuggler vessel dropped emergency supplies.");
				LogCombatMessage($"[color=cyan]+{result.FuelSalvaged} Raw Materials Acquired.[/color]");
				ApplyOfficerApprovalEvent(OfficerApprovalEventType.DistressSignalRescue);
				_isWaitingForDistressSignal = false;
			}
			else if (result.TriggerAmbush)
			{
				LogCombatMessage($"[color=red]WARNING: SLIPSPACE SIGNATURES DETECTED![/color]");
				LogCombatMessage($"[color=red]Hostile forces intercepted the signal. Prepare for combat![/color]");
				ApplyOfficerApprovalEvent(OfficerApprovalEventType.DistressSignalAmbush);
				
				_distressSignalAmbush = true; 
				_isWaitingForDistressSignal = false;
				SpawnAmbushFleet();
			}
		};
	}

	private void SpawnAmbushFleet()
	{
		if (_distressSignalService == null) return;
		List<AmbushSpawnSpec> spawnSpecs = _distressSignalService.PlanAmbushFleet(HexContents, HexGrid.Keys);
		foreach (AmbushSpawnSpec spec in spawnSpecs)
		{
			int shipBaseActionPoints = Database.GetShipBaseActions(spec.EnemyName);
			(int hp, int shields) = Database.GetShipCombatStats(spec.EnemyName);
			(int range, int dmg) = Database.GetShipWeaponStats(spec.EnemyName);

			MapEntity shipData = new MapEntity {
				Name = spec.EnemyName, Type = GameConstants.EntityTypes.EnemyFleet, Details = "Status: Hostile Ambush",
				MaxActions = shipBaseActionPoints, CurrentActions = shipBaseActionPoints,
				AttackRange = range, AttackDamage = dmg,
				MaxHP = hp, CurrentHP = hp, MaxShields = shields, CurrentShields = shields,
				InitiativeBonus = Database.GetShipInitiativeBonus(spec.EnemyName),
				BaseRotationOffset = Database.GetShipRotationOffset(spec.EnemyName)
			};

			MapSpawner.SpawnEntityAtHex(spec.SpawnPos, Database.GetShipTexturePath(spec.EnemyName), shipData, 0.2f, HexSize, HexGrid, HexContents, EntityLayer);
		}

		GetTree().CreateTimer(0.5f).Timeout += () => 
		{
			Combat.CheckForCombatTrigger();
		};
	}

	// ==========================================

	private void CenterCameraOnFleet()
	{
		foreach (var kvp in HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet)
			{
				MapCamera.Position = HexMath.HexToPixel(kvp.Key, HexSize);
				break; 
			}
		}
	}

	private void SetupAudio() 
	{ 
		_bgmPlayer = new AudioStreamPlayer();
		AddChild(_bgmPlayer);
		_bgmPlayer.VolumeDb = -15.0f;
		PlayExplorationMusic();

		SfxPlayer = new AudioStreamPlayer();
		AddChild(SfxPlayer);
		SfxPlayer.VolumeDb = -5.0f; 

		LaserPlayer = new AudioStreamPlayer();
		AddChild(LaserPlayer);
		LaserPlayer.VolumeDb = -3.0f;
		if (_audioPlaybackService != null) LaserPlayer.Stream = _audioPlaybackService.GetStream("res://Sounds/laser.mp3");

		ExplosionPlayer = new AudioStreamPlayer();
		AddChild(ExplosionPlayer);
		ExplosionPlayer.VolumeDb = 0.0f; 
		if (_audioPlaybackService != null) ExplosionPlayer.Stream = _audioPlaybackService.GetStream("res://Sounds/explosion.wav");
	}

	internal void PlayExplorationMusic()
	{
		_audioPlaybackService?.TryPlay(_bgmPlayer, ExplorationMusicPath);
	}

	internal void PlayCombatMusic()
	{
		_audioPlaybackService?.TryPlay(_bgmPlayer, CombatMusicPath);
	}

	private void SetupCamera() 
	{ 
		MapCamera = new TacticalCamera(); 
		AddChild(MapCamera); 
		float w = _maxMapRadius * HexSize * Mathf.Sqrt(3);
		float h = _maxMapRadius * HexSize * 1.5f;
		int padding = 300; 
		MapCamera.Initialize(this, SelectionBoxUI, -w - padding, w + padding, -h - padding, h + padding);
	}

	public override void _Input(InputEvent @event)
	{
		if (_strandedMenuWrapper != null && _strandedMenuWrapper.Visible) return;
		if (_shopMenuWrapper != null && _shopMenuWrapper.Visible) return; 
		if (_equipMenuWrapper != null && _equipMenuWrapper.Visible) return; // Prevent movement while equipping

		if (IsTargetingLongRange)
		{
			if (@event is InputEventMouseButton targetEvent && targetEvent.Pressed)
			{
				if (targetEvent.ButtonIndex == MouseButton.Left)
				{
					ExecuteLongRangeScan(HexMath.PixelToHex(GetGlobalMousePosition(), HexSize));
					GetViewport().SetInputAsHandled();
				}
				else if (targetEvent.ButtonIndex == MouseButton.Right)
				{
					ApplyLongRangeTargetingResult(_longRangeScanService?.CancelTargeting(), true);
					GetViewport().SetInputAsHandled();
				}
			}
			return; 
		}

		bool isMoveCommand = false;
		Vector2I targetHex = _currentHoveredHex;

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.M)
		{
			isMoveCommand = true;
		}
		else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Right)
		{
			isMoveCommand = true;
			targetHex = HexMath.PixelToHex(GetGlobalMousePosition(), HexSize); 
		}

		if (isMoveCommand && !IsFleetMoving)
		{
			HandlePlayerMoveCommand(targetHex);
		}
	}

	private void HandlePlayerMoveCommand(Vector2I targetHex)
	{
		if (_fleetCommandService == null) return;

		float currentFuel = _globalData != null ? _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle() : 0f;
		FleetMovePlan movePlan = _fleetCommandService.BuildMovePlan(
			SelectedHexes,
			targetHex,
			Combat.InCombat,
			currentFuel,
			HexGrid,
			HexContents,
			IsHexWalkable,
			GetReachableHexes);

		if (movePlan.ShowStrandedMenu)
		{
			ShowStrandedMenu();
			return;
		}

		if (!string.IsNullOrEmpty(movePlan.FailureMessage))
		{
			if (UI != null) UI.CombatLogPanel.Visible = true;
			LogCombatMessage($"\n[color=red]{movePlan.FailureMessage}[/color]");
			return;
		}

		if (!movePlan.Allowed) return;

		if (movePlan.IsGroupMove)
		{
			MoveGroup(SelectedHexes, targetHex);
		}
		else
		{
			MoveShip(movePlan.FromHex, movePlan.ToHex, movePlan.MovementCost);
			SelectedHexes = movePlan.UpdatedSelection;
			UpdateHighlights();
		}

		if (movePlan.ShouldAdvanceTurnAfterMovement && !Combat.InCombat)
		{
			RunAfterMovementCompletes(AdvanceExplorationTurn);
		}
	}

	private void ExecuteLongRangeScan(Vector2I targetHex)
	{
		IsTargetingLongRange = false;
		if (_longRangeScanService == null) return;

		LongRangeScanExecutionResult result = _longRangeScanService.ExecuteScan(targetHex);
		if (!result.Allowed)
		{
			if (!string.IsNullOrEmpty(result.FailureMessage))
			{
				if (UI != null) UI.CombatLogPanel.Visible = true;
				LogCombatMessage($"\n[color=red]{result.FailureMessage}[/color]");
			}
			return;
		}

		UpdateResourceUI();
		Fog.UpdateVisibility(); 
		
		UI.CombatLogPanel.Visible = true;
		LogCombatMessage("\n[color=#00ffff]*** DEEP SPACE TELEMETRY UPDATED ***[/color]");
		LogCombatMessage($"[color=yellow]-{result.EnergyCost:0.#} {GameConstants.ResourceKeys.EnergyCores}[/color]");

		if (SfxPlayer != null)
		{
			_audioPlaybackService?.TryPlay(SfxPlayer, "res://Sounds/laser.mp3", 0.4f);
		}
		
		ToggleShipMenu(false);
	}

	private void OnLongRangePressed()
	{
		ApplyLongRangeTargetingResult(_longRangeScanService?.ToggleTargeting(IsFleetMoving, IsTargetingLongRange), false);
	}

	private void ApplyLongRangeTargetingResult(LongRangeScanTargetingResult result, bool useInlineMessage)
	{
		if (result == null) return;

		IsTargetingLongRange = result.IsTargeting;
		if (string.IsNullOrEmpty(result.Message)) return;

		if (UI != null) UI.CombatLogPanel.Visible = true;
		string prefix = useInlineMessage ? string.Empty : "\n";
		LogCombatMessage($"{prefix}[color=yellow]{result.Message}[/color]");
	}

	private void RefreshPlayerShipHotkeys()
	{
		_fleetCommandService?.RefreshPlayerShipHotkeys(_playerShipHotkeys, HexContents);
	}

	private List<Vector2I> GetOrderedPlayerShipHexes()
	{
		return _fleetCommandService != null
			? _fleetCommandService.GetOrderedPlayerShipHexes(HexContents, _playerShipHotkeys)
			: new List<Vector2I>();
	}

	internal void ActivateFleetTravelMode()
	{
		FleetSelectionState state = _fleetCommandService?.BuildFleetTravelSelection(Combat.InCombat, IsFleetMoving, HexContents, _playerShipHotkeys);
		ApplySelectionState(state);
	}

	internal bool TrySelectPlayerShipByHotkey(int slotNumber)
	{
		Vector2I? matchingShipHex = _fleetCommandService?.TryFindPlayerShipHexByHotkey(slotNumber, Combat.InCombat, IsFleetMoving, HexContents, _playerShipHotkeys);
		if (!matchingShipHex.HasValue) return false;

		SelectSinglePlayerShip(matchingShipHex.Value, true);
		return true;
	}

	internal void SelectSinglePlayerShip(Vector2I shipHex, bool openMenu)
	{
		FleetSelectionState state = _fleetCommandService?.BuildSinglePlayerSelection(shipHex, Combat.InCombat, Combat.ActiveShip, HexContents, openMenu);
		ApplySelectionState(state);
	}

	internal void SetManualPlayerSelection(List<Vector2I> selectedShipHexes)
	{
		FleetSelectionState state = _fleetCommandService?.BuildManualPlayerSelection(selectedShipHexes, HexContents);
		ApplySelectionState(state);
	}

	internal void ClearSelectionState(bool deactivateFleetMode = true)
	{
		FleetSelectionState state = _fleetCommandService?.BuildClearedSelection(SelectedHexes, IsFleetTravelMode, deactivateFleetMode);
		ApplySelectionState(state);
	}

	private void ApplySelectionState(FleetSelectionState state)
	{
		if (state == null) return;

		IsFleetTravelMode = state.IsFleetTravelMode;
		SelectedHexes = state.SelectedHexes ?? new List<Vector2I>();

		if (state.ExpandShipMenu && state.ShipToOpen != null)
		{
			ToggleShipMenu(true, state.ShipToOpen);
		}
		else
		{
			ToggleShipMenu(false);
		}

		UpdateHighlights();
	}

	internal void ProcessRoamingEnemies()
	{
		if (Combat.InCombat || _explorationTurnService == null) return;

		List<EnemyRoamingMove> plannedMoves = _explorationTurnService.PlanRoamingEnemyMoves(HexContents, HexGrid.Keys, ScanningRange);
		if (plannedMoves.Count == 0)
		{
			if (Combat != null && !Combat.InCombat)
			{
				Combat.CheckForCombatTrigger();
			}
			return;
		}

		foreach (EnemyRoamingMove move in plannedMoves)
		{
			MoveShip(move.FromHex, move.ToHex, 0);
		}

		RunAfterMovementCompletes(() =>
		{
			if (Combat != null && !Combat.InCombat)
			{
				Combat.CheckForCombatTrigger();
			}
		});
	}

	public override void _Process(double delta)
	{
		if (IsJumping || UI == null) return;

		if (_globalData != null && _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle() < 0.25f)
		{
			if (!Combat.InCombat && !IsFleetMoving && _strandedMenuWrapper != null && !_strandedMenuWrapper.Visible && !_distressSignalAmbush && !_isWaitingForDistressSignal)
			{
				ShowStrandedMenu();
			}
		}
		
		if (IsFleetMoving) Fog.UpdateVisibility();

		Vector2 globalMousePos = GetGlobalMousePosition();
		_currentHoveredHex = HexMath.PixelToHex(globalMousePos, HexSize);
		UpdateHoverPresentation();
		UpdateHoverInfoPanel();
		UpdateShipFacing(globalMousePos);
		UpdateEnvironmentAnimation((float)delta);
		
		Hazards.ProcessHazards(delta);
		UpdateJumpButton();
		UpdateAttackButton();
		UpdateMissileButton();
	}

	private void UpdateHoverPresentation()
	{
		if (_hoverPresentationService == null) return;

		HoverPresentationState state = _hoverPresentationService.BuildState(
			IsTargetingLongRange,
			_currentHoveredHex,
			HexSize,
			HexGrid,
			HexContents);
		_hoverPresentationService.ApplyState(_radarHighlight, _hoverHighlight, _hoverTooltip, state);
	}

	private void UpdateEnvironmentAnimation(float delta)
	{
		_battleMapAnimationService?.AnimateEnvironment(EnvironmentLayer, delta, !Combat.InCombat);
	}

	private void UpdateHoverInfoPanel()
	{
		if (UI?.InfoPanel == null || UI.InfoLabel == null)
		{
			return;
		}

		if (!HexContents.ContainsKey(_currentHoveredHex))
		{
			UI.InfoPanel.Visible = false;
			return;
		}

		MapEntity entity = HexContents[_currentHoveredHex];
		if (entity == null)
		{
			UI.InfoPanel.Visible = false;
			return;
		}

		if (entity.Type == GameConstants.EntityTypes.EnemyFleet &&
			(!GodotObject.IsInstanceValid(entity.VisualSprite) || !entity.VisualSprite.Visible))
		{
			UI.InfoPanel.Visible = false;
			return;
		}

		string dynamicStats = string.Empty;
		if (entity.Type == GameConstants.EntityTypes.PlayerFleet || entity.Type == GameConstants.EntityTypes.EnemyFleet)
		{
			string initText = Combat.InCombat ? $" | INIT: {entity.CurrentInitiativeRoll}" : string.Empty;
			dynamicStats = $"HP: {entity.CurrentHP}/{entity.MaxHP} | SHIELD: {entity.CurrentShields}/{entity.MaxShields}\n" +
						   $"ACTIONS: {entity.CurrentActions}/{entity.MaxActions}{initText}\n" +
						   $"RANGE: {entity.AttackRange} | DMG: 0-{entity.AttackDamage}\n";
		}

		UI.InfoLabel.Text = $"[ {entity.Name.ToUpper()} ]\nType: {entity.Type}\n{dynamicStats}Data: {entity.Details}";
		if (GodotObject.IsInstanceValid(entity.VisualSprite))
		{
			Vector2 spriteScreenPosition = entity.VisualSprite.GetGlobalTransformWithCanvas().Origin;
			UI.InfoPanel.AnchorLeft = 0f;
			UI.InfoPanel.AnchorRight = 0f;
			UI.InfoPanel.AnchorTop = 0f;
			UI.InfoPanel.AnchorBottom = 0f;
			UI.InfoPanel.Position = spriteScreenPosition + new Vector2((HexSize * 0.9f) + 18f, -72f);
		}
		UI.InfoPanel.Visible = true;
	}

	private void UpdateShipFacing(Vector2 mousePosition)
	{
		_battleMapAnimationService?.UpdateSelectedShipFacing(SelectedHexes, HexContents, mousePosition);
	}

	private void UpdateJumpButton()
	{
		if (_jumpService == null || _battleActionButtonPresenterService == null) return;

		JumpButtonState state = _jumpService.BuildJumpButtonState(Combat.InCombat, SelectedHexes, HexContents);
		_battleActionButtonPresenterService.ApplyJumpButtonState(UI, state);
	}

	private void UpdateAttackButton()
	{
		if (_battleActionButtonPresenterService == null) return;

		AttackButtonState state = _battleActionButtonPresenterService.BuildAttackButtonState(
			SelectedHexes,
			HexContents,
			Combat.InCombat,
			Combat.ActiveShip);
		_battleActionButtonPresenterService.ApplyAttackButtonState(UI, Combat, state);
	}

	private void UpdateMissileButton()
	{
		if (_battleActionButtonPresenterService == null || UI?.MissileButton == null) return;

		bool hasMissileEquipped = false;
		bool hasMissileEnergy = _inventoryService?.HasMissileEnergy() ?? false;
		if (SelectedHexes.Count == 1 && HexContents.ContainsKey(SelectedHexes[0]))
		{
			MapEntity ship = HexContents[SelectedHexes[0]];
			hasMissileEquipped = _inventoryService?.GetActiveMissile(ship.Name) != null;
		}

		MissileButtonState state = _battleActionButtonPresenterService.BuildMissileButtonState(
			SelectedHexes,
			HexContents,
			Combat.InCombat,
			Combat.ActiveShip,
			hasMissileEquipped,
			Combat.IsTargetingMissile,
			hasMissileEnergy);
		_battleActionButtonPresenterService.ApplyMissileButtonState(UI, Combat, state);
	}

	internal void ToggleShipMenu(bool expand, MapEntity ship = null)
	{
		if (UI == null) return;
		Tween tween = CreateTween();
		float targetX = expand ? GetExpandedShipMenuX() : GetCollapsedShipMenuX();
		tween.TweenProperty(UI.ShipMenuPanel, "position:x", targetX, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		ToggleOfficerPanel(expand, ship);
		
		_shipMenuPresenterService?.ResetMenu(UI);
		
		if (_btnTrade != null) _btnTrade.Visible = false;
		if (_btnEquip != null) _btnEquip.Visible = false; // --- NEW: Hide equip by default ---

		if (expand && ship != null)
		{
			CurrentlyViewedShip = ship;
			ShipMenuState menuState = _shipContextService?.BuildMenuState(ship, Combat.InCombat, HexContents);
			if (menuState == null) return;

			string weaponName = _inventoryService?.GetActiveWeaponName(ship.Name) ?? FleetInventoryService.DefaultWeaponName;
			string hullName = _inventoryService?.GetActiveHullName(ship.Name) ?? FleetInventoryService.DefaultHullName;
			string shieldName = _inventoryService?.GetActiveShieldName(ship.Name) ?? FleetInventoryService.DefaultShieldName;
			string missileName = _inventoryService?.GetActiveMissileName(ship.Name) ?? FleetInventoryService.DefaultMissileName;
			_shipMenuPresenterService?.ApplyMenuState(UI, ship, menuState, weaponName, hullName, shieldName, missileName);

			if (menuState.ShowEquip && _btnEquip != null)
			{
				_btnEquip.Visible = true;
			}

			if (menuState.ShowTrade && _btnTrade != null)
			{
				_btnTrade.Visible = true;
			}
		}
	}

	private float GetExpandedShipMenuX()
	{
		if (UI?.ShipMenuPanel == null) return GetViewportRect().Size.X;
		float panelWidth = UI.ShipMenuPanel.Size.X > 0f ? UI.ShipMenuPanel.Size.X : UI.ShipMenuPanel.CustomMinimumSize.X;
		return GetViewportRect().Size.X - panelWidth;
	}

	private float GetCollapsedShipMenuX()
	{
		if (UI?.ShipMenuPanel == null) return GetViewportRect().Size.X + 24f;
		return GetViewportRect().Size.X + 24f;
	}

	private void BuildOfficerPanel()
	{
		if (UI == null) return;

		Control uiRoot = UI.GetNodeOrNull<Control>("UIRoot");
		if (uiRoot == null) return;

		_officerMenuPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(360, 560),
			Visible = true
		};
		_officerMenuPanel.AnchorTop = 0.22f;
		_officerMenuPanel.AnchorBottom = 0.22f;
		_officerMenuPanel.OffsetTop = -280f;
		_officerMenuPanel.OffsetBottom = 280f;
		_officerMenuPanel.Position = new Vector2(GetCollapsedOfficerPanelX(), 0f);
		uiRoot.AddChild(_officerMenuPanel);

		VBoxContainer layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		_officerMenuPanel.AddChild(layout);

		_officerMenuTitle = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		layout.AddChild(_officerMenuTitle);

		_officerPortraitDisplay = new TextureRect
		{
			CustomMinimumSize = new Vector2(150, 220),
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		layout.AddChild(_officerPortraitDisplay);

		_officerDetailsLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		layout.AddChild(_officerDetailsLabel);
	}

	private void ToggleOfficerPanel(bool expand, MapEntity ship = null)
	{
		if (_officerMenuPanel == null || _officerPanelPresenterService == null) return;

		_officerPanelPresenterService.Reset(_officerMenuTitle, _officerPortraitDisplay, _officerDetailsLabel);
		float targetX = GetCollapsedOfficerPanelX();

		if (expand && ship != null)
		{
			bool hasOfficer = _officerPanelPresenterService.Apply(_globalData, ship, _officerMenuTitle, _officerPortraitDisplay, _officerDetailsLabel);
			if (hasOfficer)
			{
				targetX = GetExpandedOfficerPanelX();
			}
		}

		Tween tween = CreateTween();
		tween.TweenProperty(_officerMenuPanel, "position:x", targetX, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	private float GetExpandedOfficerPanelX()
	{
		return 0f;
	}

	private float GetCollapsedOfficerPanelX()
	{
		if (_officerMenuPanel == null) return -384f;
		float panelWidth = _officerMenuPanel.Size.X > 0f ? _officerMenuPanel.Size.X : _officerMenuPanel.CustomMinimumSize.X;
		return -panelWidth - 24f;
	}

	private void ApplyOfficerApprovalEvent(OfficerApprovalEventType eventType, OfficerApprovalContext context = null)
	{
		if (_officerService == null || UI == null)
		{
			return;
		}

		List<OfficerApprovalChange> changes = _officerService.ApplyApprovalEvent(eventType, context);
		if (changes.Count == 0)
		{
			return;
		}

		UI.CombatLogPanel.Visible = true;
		foreach (OfficerApprovalChange change in changes)
		{
			string color = change.Delta > 0 ? "#7CFF6B" : "#FF8A8A";
			string disposition = change.Delta > 0 ? "approves of" : "disapproves of";
			LogCombatMessage($"[color={color}]{change.Officer.DisplayName} {disposition} {change.Reason} ({(change.Delta > 0 ? "+" : string.Empty)}{change.Delta})[/color]");

			if (!string.IsNullOrEmpty(change.QueuedDowntimeEventId))
			{
				LogCombatMessage($"[color=orange]{change.Officer.DisplayName} now has something important to discuss.[/color]");
			}
		}

		bool shipMenuExpanded = UI.ShipMenuPanel != null && UI.ShipMenuPanel.Position.X > GetCollapsedShipMenuX() + 1f;
		if (shipMenuExpanded && CurrentlyViewedShip != null)
		{
			ToggleShipMenu(true, CurrentlyViewedShip);
		}
	}

	private void OnScanPressed()
	{
		if (IsFleetMoving) return; 
		if (CurrentlyViewedShip == null || CurrentlyViewedShip.CurrentActions < 1) return;
		if (_explorationActionService == null) return;
		MapEntity planet = _shipContextService?.GetAdjacentPlanet(CurrentlyViewedShip, HexContents);
		if (planet == null) return;

		PlanetData pData = _shipContextService?.GetAdjacentPlanetData(CurrentlyViewedShip, HexContents);
		if (pData != null && pData.HasBeenScanned) return; 

		ScanActionResult result = _explorationActionService.PerformScan(CurrentlyViewedShip, planet, pData, ConversationUI != null);
		if (!result.Allowed)
		{
			if (!string.IsNullOrEmpty(result.FailureMessage))
			{
				UI.CombatLogPanel.Visible = true;
				LogCombatMessage($"\n[color=red]{result.FailureMessage}[/color]");
			}
			return;
		}

		UpdateResourceUI();

		if (result.TriggerConversation && ConversationUI != null)
		{
			UI.CombatLogPanel.Visible = true;
			LogCombatMessage($"\n[color=yellow]*** INCOMING TRANSMISSION FROM SURFACE ***[/color]");
			ApplyOfficerApprovalEvent(
				OfficerApprovalEventType.ScanPlanet,
				new OfficerApprovalContext
				{
					ActingShipName = CurrentlyViewedShip.Name
				});
			ConversationUI.StartConversation(result.ConversationId);
			ToggleShipMenu(true, CurrentlyViewedShip);
			return;
		}

		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=#00ffff]--- SENSOR SWEEP COMPLETED (-{result.EnergyCost:0.#} Energy) ---[/color]");
		LogCombatMessage($"Target: {planet.Name}");
		LogCombatMessage($"Size Class: {result.SizeClass}");
		LogCombatMessage($"Projected Salvage Operation: {result.ProjectedSalvageTurns} Turns");
		LogCombatMessage($"Caution: extended operations carry risk of hostile detection.");
		ApplyOfficerApprovalEvent(
			OfficerApprovalEventType.ScanPlanet,
			new OfficerApprovalContext
			{
				ActingShipName = CurrentlyViewedShip.Name
			});

		ToggleShipMenu(true, CurrentlyViewedShip); 
	}

	private void OnSalvagePressed()
	{
		if (IsFleetMoving) return; 
		if (CurrentlyViewedShip == null || CurrentlyViewedShip.CurrentActions < 1) return;
		if (_explorationActionService == null) return;
		MapEntity planet = _shipContextService?.GetAdjacentPlanet(CurrentlyViewedShip, HexContents);
		if (planet == null) return;

		PlanetData pData = _shipContextService?.GetAdjacentPlanetData(CurrentlyViewedShip, HexContents);
		if (pData != null && pData.HasBeenSalvaged) return; 

		SalvageActionResult result = _explorationActionService.PerformSalvage(
			CurrentlyViewedShip,
			planet,
			pData,
			HexContents,
			ScanningRange,
			AdvanceExplorationTurn);

		if (!result.Allowed)
		{
			if (!string.IsNullOrEmpty(result.FailureMessage))
			{
				UI.CombatLogPanel.Visible = true;
				LogCombatMessage($"\n[color=red]{result.FailureMessage}[/color]");
			}
			return;
		}

		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=yellow]--- SALVAGE OPERATION COMMENCED (-{result.RawCost:0.#} Raw Materials) ---[/color]");
		UpdateResourceUI();
		ApplyOfficerApprovalEvent(
			OfficerApprovalEventType.SalvagePlanet,
			new OfficerApprovalContext
			{
				ActingShipName = CurrentlyViewedShip.Name
			});

		if (result.Ambushed)
		{
			LogCombatMessage($"[color=red]*** HOSTILES DETECTED! SALVAGE ABORTED! ***[/color]");
			Combat.CheckForCombatTrigger();
			ToggleShipMenu(true, CurrentlyViewedShip);
			return;
		}

		UpdateResourceUI();
		LogCombatMessage($"[color=green]Operation Successful. Acquired:[/color]");
		LogCombatMessage($"[color=green]- {result.RawYield} Raw Materials[/color]");
		LogCombatMessage($"[color=green]- {result.EnergyYield} Energy Cores[/color]");
		LogCombatMessage($"[color=green]- {result.TechYield} Ancient Tech[/color]");
		ToggleShipMenu(true, CurrentlyViewedShip); 
	}

	private void OnCodexPressed() 
	{ 
		if (IsFleetMoving) return; 
		OnSaveGamePressed(); 
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://codex.tscn");
		else GetTree().ChangeSceneToFile("res://codex.tscn");
	}

	private void OnInventoryPressed()
	{
		if (_inventoryService == null || UI == null) return;

		UI.CombatLogPanel.Visible = true;
		InventoryReport report = _inventoryService.BuildInventoryReport();
		if (report == null || report.Lines.Count == 0) return;

		bool isFirstLine = true;
		foreach (string line in report.Lines)
		{
			if (isFirstLine)
			{
				LogCombatMessage($"\n{line}");
				isFirstLine = false;
			}
			else
			{
				LogCombatMessage(line);
			}
		}
	}

	internal void OnRepairPressed()
	{
		if (IsFleetMoving) return; 
		if (_explorationActionService == null) return;
		ShipRepairResult result = _explorationActionService.PerformShipRepair(CurrentlyViewedShip);
		if (!result.Allowed) return;
		
		if (SfxPlayer != null)
		{
			_audioPlaybackService?.TryPlay(SfxPlayer, "res://Sounds/laser.mp3", 1.5f);
		}

		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=cyan]--- FIELD REPAIRS COMPLETED (+{result.HealedHull} Hull, +{result.HealedShields} Shields) ---[/color]");
		ApplyOfficerApprovalEvent(
			OfficerApprovalEventType.RepairShip,
			new OfficerApprovalContext
			{
				ActingShipName = CurrentlyViewedShip?.Name ?? string.Empty
			});
		
		ToggleShipMenu(true, CurrentlyViewedShip);
	}

	private void OnRepairFleetPressed()
	{
		if (IsFleetMoving || Combat.InCombat) return; 
		if (_explorationActionService == null) return;

		FleetRepairResult result = _explorationActionService.PerformFleetRepair(HexContents, ScanningRange, AdvanceExplorationTurn);
		if (!result.Allowed) return;

		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=cyan]--- REPAIRING FLEET (Estimated {result.TurnsNeeded} Turns) ---[/color]");
		ApplyOfficerApprovalEvent(OfficerApprovalEventType.RepairFleet);

		if (result.Ambushed)
		{
			LogCombatMessage($"[color=red]*** REPAIRS INTERRUPTED BY ENEMY FLEET! ***[/color]");
			Combat.CheckForCombatTrigger();
			return;
		}

		if (result.FullyRepaired) LogCombatMessage($"[color=green]Fleet repairs complete.[/color]");
	}

	internal void OnEndTurnPressed()
	{
		if (IsFleetMoving) return; 

		if (!Combat.InCombat)
		{
			AdvanceExplorationTurn();
			
			SelectedHexes.Clear();
			ToggleShipMenu(false);
			UpdateHighlights();
			Fog.UpdateVisibility();
		}
		else if (Combat.ActiveShip != null && Combat.ActiveShip.Type == GameConstants.EntityTypes.PlayerFleet) Combat.EndActiveTurn();
	}

	private void AdvanceExplorationTurn()
	{
		CurrentTurn = _explorationTurnService != null
			? _explorationTurnService.AdvanceTurn(CurrentTurn, HexContents)
			: CurrentTurn + 1;
		if (UI != null) UI.TurnLabel.Text = $"TURN {CurrentTurn}";
		
		ProcessRoamingEnemies(); 
	}

	private void OnJumpPressed()
	{
		if (IsJumping || IsFleetMoving || _jumpService == null) return;
		IsJumping = true;

		if (UI != null)
		{
			UI.JumpButton.Visible = false;
			UI.AttackButton.Visible = false;
			if (UI.MissileButton != null) UI.MissileButton.Visible = false;
			UI.InfoPanel.Visible = false;
		}
		ToggleShipMenu(false);

		JumpPlan jumpPlan = _jumpService.BuildJumpPlan(Combat.InCombat, SelectedHexes, HexContents);
		if (!jumpPlan.Allowed)
		{
			IsJumping = false;
			return;
		}

		_jumpService.PrepareForJump();

		Vector2 gatePixelPos = HexMath.HexToPixel(jumpPlan.GateHex, HexSize);

		Tween warpTween = CreateTween();
		warpTween.SetParallel(true);

		if (HexContents.ContainsKey(jumpPlan.GateHex) && GodotObject.IsInstanceValid(HexContents[jumpPlan.GateHex].VisualSprite))
		{
			Sprite2D gateSprite = HexContents[jumpPlan.GateHex].VisualSprite;
			
			CpuParticles2D vortex = gateSprite.GetNodeOrNull<CpuParticles2D>("VortexParticles");
			if (vortex != null)
			{
				vortex.Amount = 500; 
				vortex.ScaleAmountMin = 1.0f; 
				vortex.ScaleAmountMax = 2.5f;
				vortex.RadialAccelMin = -120f; 
				vortex.TangentialAccelMin = 250f; 
				vortex.TangentialAccelMax = 400f; 
				vortex.Color = new Color(1f, 1f, 1f, 1f); 
			}
		}

		foreach(var kvp in HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet)
			{
				Sprite2D shipSprite = kvp.Value.VisualSprite;
				if (GodotObject.IsInstanceValid(shipSprite))
				{
					warpTween.TweenProperty(shipSprite, "position", gatePixelPos, 1.5f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
					warpTween.TweenProperty(shipSprite, "scale", new Vector2(0.001f, 0.001f), 1.5f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
					warpTween.TweenProperty(shipSprite, "rotation", shipSprite.Rotation + Mathf.Pi * 8, 1.5f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
					warpTween.TweenProperty(shipSprite, "modulate", new Color(2f, 2f, 3f, 0f), 1.5f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In); 
				}
			}
		}

		if (SfxPlayer != null)
		{
			_audioPlaybackService?.TryPlay(SfxPlayer, "res://Sounds/laser.mp3", 0.5f);
		}

		warpTween.Chain().TweenCallback(Callable.From(() => 
		{
			OnSaveGamePressed(); 

			SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
			string nextScene = _jumpService.FinalizeJump(jumpPlan.IsEmergencyJump);
			
			if (transitioner != null) transitioner.ChangeScene(nextScene);
			else GetTree().ChangeSceneToFile(nextScene);
		}));
	}

	private void OnSaveGamePressed()
	{
		if (_globalData == null || _battleMapSaveSnapshotService == null) return;

		_battleMapSaveSnapshotService.CaptureSnapshot(
			_globalData,
			CurrentTurn,
			Combat.InCombat,
			Combat.GetCurrentQueueIndex(),
			AsteroidHexes,
			RadiationHexes,
			Fog != null ? Fog.GetExploredHexes() : Enumerable.Empty<Vector2I>(),
			HexContents);

		_globalData.SaveGame();

		if (UI != null)
		{
			UI.SaveGameButton.Text = "GAME SAVED!";
			UI.SaveGameButton.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
			GetTree().CreateTimer(2.0f).Timeout += () =>
			{
				UI.SaveGameButton.Text = "SAVE GAME";
				UI.SaveGameButton.RemoveThemeColorOverride("font_color");
			};
		}
	}

	private void OnMainMenuPressed() 
	{
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://main_menu.tscn");
		else GetTree().ChangeSceneToFile("res://main_menu.tscn");
	}

	internal void MoveShip(Vector2I fromHex, Vector2I toHex, int cost)
	{
		if (_movementExecutionService == null) return;

		MoveExecutionResult result = _movementExecutionService.ExecuteMove(
			HexContents,
			SelectedHexes,
			fromHex,
			toHex,
			cost,
			HexSize,
			Combat.InCombat,
			_globalData);
		if (!result.Allowed || result.Ship == null) return;

		MapEntity ship = result.Ship;
		if (result.ReopenShipMenu)
		{
			ToggleShipMenu(true, ship);
		}

		if (SfxPlayer != null && ship.VisualSprite.Visible)
		{
			_audioPlaybackService?.TryPlay(SfxPlayer, result.SoundPath);
		}

		if (result.RefreshResourceUi)
		{
			UpdateResourceUI();
		}

		ActiveMovementTweens++;

		Tween tween = CreateTween();
		tween.TweenProperty(ship.VisualSprite, "position", result.TargetPixelPosition, result.Duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		
		tween.TweenCallback(Callable.From(() => 
		{
			ActiveMovementTweens = Math.Max(0, ActiveMovementTweens - 1);
			if (ActiveMovementTweens <= 0) 
			{
				Fog.UpdateVisibility(); 
				FlushMovementCallbacks();
			}
		}));
	}

	internal void RunAfterMovementCompletes(Action callback)
	{
		if (callback == null) return;
		if (!IsFleetMoving)
		{
			callback();
			return;
		}

		_pendingMovementCallbacks.Add(callback);
	}

	private void FlushMovementCallbacks()
	{
		if (IsFleetMoving || _pendingMovementCallbacks.Count == 0) return;

		List<Action> callbacksToRun = _pendingMovementCallbacks.ToList();
		_pendingMovementCallbacks.Clear();

		foreach (Action callback in callbacksToRun)
		{
			callback();
		}
	}

	internal void MoveGroup(List<Vector2I> shipsToMove, Vector2I targetHex)
	{
		if (IsFleetMoving) return; 

		shipsToMove = shipsToMove.Where(h => HexContents.ContainsKey(h)).ToList();
		if (shipsToMove.Count == 0) return;

		Vector2I anchorHex = shipsToMove[0];
		List<Vector2I> newSelection = new List<Vector2I>();

		shipsToMove.Sort((a, b) => HexMath.HexDistance(a, targetHex).CompareTo(HexMath.HexDistance(b, targetHex)));

		foreach (Vector2I shipHex in shipsToMove)
		{
			Vector2I offset = new Vector2I(shipHex.X - anchorHex.X, shipHex.Y - anchorHex.Y);
			Vector2I desiredTarget = new Vector2I(targetHex.X + offset.X, targetHex.Y + offset.Y);
			Vector2I finalHex = FindNearestEmptyHex(desiredTarget);

			if (finalHex != shipHex)
			{
				MoveShip(shipHex, finalHex, 0); 
				newSelection.Add(finalHex);
			}
			else newSelection.Add(shipHex);
		}
		
		SelectedHexes = newSelection;
		UpdateHighlights();
	}

	internal Dictionary<Vector2I, int> GetReachableHexes(Vector2I startHex, int movementRange)
	{
		Dictionary<Vector2I, int> costSoFar = new Dictionary<Vector2I, int>();
		costSoFar[startHex] = 0;
		Queue<Vector2I> frontier = new Queue<Vector2I>();
		frontier.Enqueue(startHex);

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (Vector2I dir in HexMath.Directions)
			{
				Vector2I next = current + dir;
				if (!HexGrid.ContainsKey(next)) continue;

				if (!IsHexWalkable(next)) continue;

				int newCost = costSoFar[current] + 1; 
				if (newCost <= movementRange)
				{
					if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
					{
						costSoFar[next] = newCost;
						frontier.Enqueue(next);
					}
				}
			}
		}
		return costSoFar;
	}

	internal void UpdateHighlights()
	{
		if (_highlightPresenterService == null) return;

		Vector2I? activeSelectionHex = null;
		IEnumerable<Vector2I> reachableHexes = Array.Empty<Vector2I>();

		if (Combat.InCombat && SelectedHexes.Count == 1 && Combat.ActiveShip != null && HexContents.ContainsKey(SelectedHexes[0]) && HexContents[SelectedHexes[0]] == Combat.ActiveShip)
		{
			activeSelectionHex = SelectedHexes[0];
			reachableHexes = GetReachableHexes(SelectedHexes[0], Combat.ActiveShip.CurrentActions).Keys;
		}

		_highlightPresenterService.RenderHighlights(
			_highlightLayer,
			SelectedHexes,
			Combat.InCombat,
			activeSelectionHex,
			reachableHexes,
			HexSize);
	}

	internal void LogCombatMessage(string message) { if (UI != null && UI.CombatLogText != null) UI.CombatLogText.Text += message + "\n"; }

	internal void CheckGameOver()
	{
		if (!HexContents.Values.Any(s => s.Type == GameConstants.EntityTypes.PlayerFleet)) UI.GameOverPanel.Visible = true;
	}

	private void OnAttackPressed()
	{
		if (IsFleetMoving) return; 
		Combat.IsTargetingMissile = false;
		if (UI?.MissileButton != null) UI.MissileButton.Text = "MISSILE";
		Combat.IsTargeting = !Combat.IsTargeting;
		if (UI != null) UI.AttackButton.Text = Combat.IsTargeting ? "CANCEL TARGET" : "ATTACK";
	}

	internal void OnMissilePressed()
	{
		if (IsFleetMoving || !Combat.InCombat || UI?.MissileButton == null) return;
		if (SelectedHexes.Count != 1 || !HexContents.ContainsKey(SelectedHexes[0])) return;
		MapEntity ship = HexContents[SelectedHexes[0]];
		if (_inventoryService?.GetActiveMissile(ship.Name) == null) return;
		if (!(_inventoryService?.HasMissileEnergy() ?? false)) return;

		Combat.IsTargeting = false;
		if (UI.AttackButton != null) UI.AttackButton.Text = "ATTACK";
		Combat.IsTargetingMissile = !Combat.IsTargetingMissile;
		UI.MissileButton.Text = Combat.IsTargetingMissile ? "CANCEL MISSILE" : "MISSILE";
	}

	internal void ResetCombatTargetingUi()
	{
		Combat.IsTargeting = false;
		Combat.IsTargetingMissile = false;
		if (UI?.AttackButton != null) UI.AttackButton.Text = "ATTACK";
		if (UI?.MissileButton != null) UI.MissileButton.Text = "MISSILE";
	}

	internal bool TryHandleTargetedCombatClick(Vector2I clickedHex)
	{
		if (!Combat.InCombat || SelectedHexes.Count != 1 || !HexContents.ContainsKey(SelectedHexes[0])) return false;

		if (Combat.IsTargetingMissile)
		{
			MapEntity attacker = HexContents[SelectedHexes[0]];
			EquipmentData missile = _inventoryService?.GetActiveMissile(attacker.Name);
			if (_inventoryService == null || missile == null)
			{
				ResetCombatTargetingUi();
				return true;
			}
			if (!_inventoryService.SpendMissileEnergy())
			{
				if (UI != null) UI.CombatLogPanel.Visible = true;
				LogCombatMessage($"\n[color=red]*** MISSILE LAUNCH FAILED: INSUFFICIENT {GameConstants.ResourceKeys.EnergyCores.ToUpper()} (1.0 Req) ***[/color]");
				ResetCombatTargetingUi();
				UpdateResourceUI();
				return true;
			}

			if (HexContents.ContainsKey(clickedHex) && HexContents[clickedHex].Type == GameConstants.EntityTypes.EnemyFleet)
			{
				int dist = HexMath.HexDistance(SelectedHexes[0], clickedHex);
				if (dist <= missile.MissileRange)
				{
					Combat.PerformMissileAttack(SelectedHexes[0], clickedHex);
					ResetCombatTargetingUi();
					UpdateResourceUI();
					Combat.CheckForCombatTrigger();
				}
				else
				{
					_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores] =
						_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() + 1f;
					UpdateResourceUI();
					GD.Print("Missile target out of range!");
				}
			}
			else
			{
				_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores] =
					_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() + 1f;
				UpdateResourceUI();
				ResetCombatTargetingUi();
			}

			return true;
		}

		if (Combat.IsTargeting)
		{
			if (HexContents.ContainsKey(clickedHex) && HexContents[clickedHex].Type == GameConstants.EntityTypes.EnemyFleet)
			{
				int dist = HexMath.HexDistance(SelectedHexes[0], clickedHex);
				MapEntity attacker = HexContents[SelectedHexes[0]];
				if (dist <= attacker.AttackRange)
				{
					Combat.PerformAttack(SelectedHexes[0], clickedHex);
					ResetCombatTargetingUi();
					Combat.CheckForCombatTrigger();
				}
				else
				{
					GD.Print("Target out of range!");
				}
			}
			else
			{
				ResetCombatTargetingUi();
			}

			return true;
		}

		return false;
	}

	private Vector2I FindNearestEmptyHex(Vector2I target)
	{
		if (IsHexWalkable(target)) return target;
		int radius = 1;
		while (radius < 15) 
		{
			Vector2I current = target + HexMath.Directions[4] * radius;
			for (int i = 0; i < 6; i++)
			{
				for (int j = 0; j < radius; j++)
				{
					if (IsHexWalkable(current)) return current;
					current += HexMath.Directions[i];
				}
			}
			radius++;
		}
		return target; 
	}
	
	internal void AwardEnemyKillSalvage(string enemyName)
	{
		if (_globalData == null || UI == null) return;

		Random rng = new Random();
		float rawYield;
		float energyYield;
		float techYield;

		if (_distressSignalAmbush)
		{
			rawYield = rng.Next(100, 301); 
			energyYield = rng.Next(3, 8); 
			techYield = 1f; 
			_distressSignalAmbush = false; 
		}
		else
		{
			rawYield = rng.Next(15, 41); 
			energyYield = rng.Next(1, 4); 
			techYield = rng.Next(0, 100) < 25 ? 1f : 0f; 
		}

		_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] = _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle() + rawYield;
		_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores] = _globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() + energyYield;
		_globalData.FleetResources[GameConstants.ResourceKeys.AncientTech] = _globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle() + techYield;

		UpdateResourceUI();

		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=#00ff00]*** {enemyName.ToUpper()} DESTROYED ***[/color]");
		LogCombatMessage($"[color=cyan]Combat Salvage:[/color] {rawYield} {GameConstants.ResourceKeys.RawMaterials}, {energyYield} {GameConstants.ResourceKeys.EnergyCores}{(techYield > 0 ? $", 1 {GameConstants.ResourceKeys.AncientTech}" : "")}");
	}
}
