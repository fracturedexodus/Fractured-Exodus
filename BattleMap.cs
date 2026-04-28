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
	private ShipContextService _shipContextService;
	private ExplorationActionService _explorationActionService;
	private ExplorationTurnService _explorationTurnService;
	private DistressSignalService _distressSignalService;
	private JumpService _jumpService;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (_globalData != null) _inventoryService = new FleetInventoryService(_globalData);
		if (_globalData != null) _shipContextService = new ShipContextService(_globalData);
		if (_globalData != null) _explorationActionService = new ExplorationActionService(_globalData);
		if (_globalData != null) _distressSignalService = new DistressSignalService(_globalData);
		if (_globalData != null) _jumpService = new JumpService(_globalData);
		_explorationTurnService = new ExplorationTurnService();
		if (_globalData != null && _globalData.CurrentTurn > 0) CurrentTurn = _globalData.CurrentTurn;
		
		Texture2D cursorTex = GD.Load<Texture2D>("res://cursor.png");
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
		
		_btnTrade = new Button();
		_btnTrade.Text = "ACCESS OUTPOST EXCHANGE"; 
		_btnTrade.Visible = false;
		_btnTrade.CustomMinimumSize = new Vector2(0, 40);
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
			UI.CombatLogPanel.Visible = false;
			UI.AttackButton.Visible = false;
			UI.JumpButton.Visible = false;
			UI.ShipMenuPanel.Position = new Vector2(GetViewportRect().Size.X + 50, UI.ShipMenuPanel.Position.Y);

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
		if (_inventoryService == null) return;

		foreach (Node child in _shopItemList.GetChildren()) child.QueueFree();

		foreach (EquipmentData item in _inventoryService.GetShopItems())
		{
			HBoxContainer row = new HBoxContainer();

			RichTextLabel info = new RichTextLabel();
			info.BbcodeEnabled = true;
			info.Text = $"[b][color=yellow]{item.Name}[/color][/b] ({item.Category})\n{item.Description}\n[color=cyan]Cost: {item.CostTech} Tech, {item.CostRaw} Raw[/color]";
			info.CustomMinimumSize = new Vector2(380, 75);
			info.FitContent = true;
			row.AddChild(info);

			Button buyBtn = new Button();
			buyBtn.Text = "BUY";
			buyBtn.CustomMinimumSize = new Vector2(90, 40);
			buyBtn.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

			if (!_inventoryService.CanAfford(item.ItemID))
			{
				buyBtn.Disabled = true;
				buyBtn.Text = "FUNDS";
			}

			buyBtn.Pressed += () => BuyItem(item.ItemID);
			row.AddChild(buyBtn);

			_shopItemList.AddChild(row);
		}

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
			OnSaveGamePressed(); // Auto-save after a purchase
			
			if (UI != null) UI.CombatLogPanel.Visible = true;
			LogCombatMessage($"\n[color=green]--- TRANSACTION APPROVED ---[/color]");
			LogCombatMessage($"Purchased: [color=yellow]{item.Name}[/color] (-{item.CostTech} Tech, -{item.CostRaw} Raw)");
			
			if (SfxPlayer != null)
			{
				AudioStream buySfx = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
				if (buySfx != null) { SfxPlayer.Stream = buySfx; SfxPlayer.PitchScale = 1.2f; SfxPlayer.Play(); }
			}

			OpenShop(); 
		}
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
		if (_inventoryService == null || CurrentlyViewedShip == null) return;
		string shipName = CurrentlyViewedShip.Name;

		ShipLoadout loadout = _inventoryService.GetOrCreateLoadout(shipName);
		if (loadout == null) return;

		foreach (Node child in _equipItemList.GetChildren()) child.QueueFree();

		// --- Display Current Loadout ---
		RichTextLabel currentLoadoutText = new RichTextLabel();
		currentLoadoutText.BbcodeEnabled = true;
		currentLoadoutText.FitContent = true;
		
		string wpn = _inventoryService.GetEquippedItemName(loadout.WeaponID);
		string shld = _inventoryService.GetEquippedItemName(loadout.ShieldID);
		string armr = _inventoryService.GetEquippedItemName(loadout.ArmorID);
		
		currentLoadoutText.Text = $"[color=cyan]--- {shipName.ToUpper()}'s CURRENT LOADOUT ---[/color]\nWeapon: {wpn}\nShield: {shld}\nArmor: {armr}\n\n[color=yellow]--- CARGO HOLD (AVAILABLE INVENTORY) ---[/color]";
		_equipItemList.AddChild(currentLoadoutText);

		List<InventoryStack> inventoryStacks = _inventoryService.GetGroupedInventory();
		if (inventoryStacks.Count == 0)
		{
			Label emptyLabel = new Label { Text = "No unequipped items available in cargo." };
			_equipItemList.AddChild(emptyLabel);
		}
		else
		{
			foreach (InventoryStack stack in inventoryStacks)
			{
				HBoxContainer row = new HBoxContainer();
				RichTextLabel info = new RichTextLabel();
				info.BbcodeEnabled = true;
				info.Text = $"[b]{stack.Item.Name}[/b] (x{stack.Count})\n{stack.Item.Description}";
				info.CustomMinimumSize = new Vector2(380, 50);
				info.FitContent = true;
				row.AddChild(info);

				Button equipBtn = new Button();
				equipBtn.Text = "EQUIP";
				equipBtn.CustomMinimumSize = new Vector2(90, 40);
				equipBtn.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
				equipBtn.Pressed += () => EquipItem(shipName, stack.ItemID);
				row.AddChild(equipBtn);
				
				_equipItemList.AddChild(row);
			}
		}
		
		_equipMenuWrapper.Visible = true;
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

		OnSaveGamePressed(); // Save state
		
		if (SfxPlayer != null)
		{
			AudioStream equipSfx = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (equipSfx != null) { SfxPlayer.Stream = equipSfx; SfxPlayer.PitchScale = 0.8f; SfxPlayer.Play(); }
		}

		if (UI != null) UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=green]--- LOADOUT UPDATED ---[/color]");
		LogCombatMessage($"{shipName} equipped [color=yellow]{itemToEquip.Name}[/color].");

		OpenEquipMenu(); // Refresh the UI to reflect the swap
	}

	// ==========================================
	// STRANDED FLEET PROTOCOL UI
	// ==========================================
	private void BuildStrandedMenu()
	{
		CanvasLayer menuLayer = new CanvasLayer { Layer = 150 };
		AddChild(menuLayer);

		_strandedMenuWrapper = new CenterContainer();
		_strandedMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect); 
		_strandedMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop; 
		_strandedMenuWrapper.Visible = false;
		menuLayer.AddChild(_strandedMenuWrapper);

		PanelContainer strandedPanel = new PanelContainer();
		
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
		style.BorderWidthTop = 2; style.BorderWidthBottom = 2; style.BorderWidthLeft = 2; style.BorderWidthRight = 2;
		style.BorderColor = new Color(1f, 0f, 0f, 0.8f); 
		style.ContentMarginLeft = 20; style.ContentMarginRight = 20; style.ContentMarginTop = 20; style.ContentMarginBottom = 20;
		strandedPanel.AddThemeStyleboxOverride("panel", style);
		
		_strandedMenuWrapper.AddChild(strandedPanel);

		VBoxContainer container = new VBoxContainer();
		container.AddThemeConstantOverride("separation", 15);
		strandedPanel.AddChild(container);

		Label title = new Label();
		title.Text = "=== WARNING: CRITICAL FUEL DEPLETION ===";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeColorOverride("font_color", new Color(1f, 0f, 0f));
		title.AddThemeFontSizeOverride("font_size", 18);
		container.AddChild(title);

		Label body = new Label();
		body.Text = "Your fleet has run out of Raw Materials.\nMain engines are offline. Life support is failing.\n\nWhat are your orders, Commander?";
		body.HorizontalAlignment = HorizontalAlignment.Center;
		body.AddThemeFontSizeOverride("font_size", 14);
		container.AddChild(body);

		Button btnDistress = new Button();
		btnDistress.Text = "SEND FTL DISTRESS SIGNAL (Gamble)";
		btnDistress.CustomMinimumSize = new Vector2(0, 40);
		btnDistress.AddThemeColorOverride("font_color", new Color(1f, 1f, 0f));
		btnDistress.Pressed += OnDistressSignalPressed;
		container.AddChild(btnDistress);

		Button btnAbandon = new Button();
		btnAbandon.Text = "ABANDON FLEET (Return to Menu)";
		btnAbandon.CustomMinimumSize = new Vector2(0, 40);
		btnAbandon.Pressed += () => {
			if (_globalData != null) _globalData.ResetForNewGame();
			OnMainMenuPressed();
		};
		container.AddChild(btnAbandon);
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

	private void OnDistressSignalPressed()
	{
		if (_distressSignalService == null) return;
		_strandedMenuWrapper.Visible = false;
		_isWaitingForDistressSignal = true; 
		
		LogCombatMessage("\n[color=yellow]--- BROADCASTING WIDE-BAND DISTRESS SIGNAL ---[/color]");
		LogCombatMessage("Awaiting response...");
		
		GetTree().CreateTimer(1.5f).Timeout += () => 
		{
			DistressSignalResult result = _distressSignalService.ResolveDistressSignal();
			if (result.RescueArrived)
			{
				UpdateResourceUI();
				
				LogCombatMessage($"[color=green]SIGNAL RECEIVED![/color] A passing smuggler vessel dropped emergency supplies.");
				LogCombatMessage($"[color=cyan]+{result.FuelSalvaged} Raw Materials Acquired.[/color]");
				_isWaitingForDistressSignal = false;
			}
			else if (result.TriggerAmbush)
			{
				LogCombatMessage($"[color=red]WARNING: SLIPSPACE SIGNATURES DETECTED![/color]");
				LogCombatMessage($"[color=red]Hostile forces intercepted the signal. Prepare for combat![/color]");
				
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
		AudioStream bgmStream = GD.Load<AudioStream>("res://battle_theme.mp3"); 
		if (bgmStream != null) { _bgmPlayer.Stream = bgmStream; _bgmPlayer.VolumeDb = -15.0f; _bgmPlayer.Play(); }

		SfxPlayer = new AudioStreamPlayer();
		AddChild(SfxPlayer);
		SfxPlayer.VolumeDb = -5.0f; 

		LaserPlayer = new AudioStreamPlayer();
		AddChild(LaserPlayer);
		LaserPlayer.VolumeDb = -3.0f;
		AudioStream laserStream = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
		if (laserStream != null) LaserPlayer.Stream = laserStream;

		ExplosionPlayer = new AudioStreamPlayer();
		AddChild(ExplosionPlayer);
		ExplosionPlayer.VolumeDb = 0.0f; 
		AudioStream boomStream = GD.Load<AudioStream>("res://Sounds/explosion.wav"); 
		if (boomStream != null) ExplosionPlayer.Stream = boomStream;
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
				}
				else if (targetEvent.ButtonIndex == MouseButton.Right)
				{
					IsTargetingLongRange = false; // Cancel scan
					LogCombatMessage("[color=yellow]Long Range Scan Cancelled.[/color]");
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
			if (SelectedHexes.Count > 0 && HexGrid.ContainsKey(targetHex))
			{
				float totalFuelNeeded = 0f;
				bool containsPlayerFleet = false;

				foreach (Vector2I sHex in SelectedHexes)
				{
					if (HexContents.ContainsKey(sHex) && HexContents[sHex].Type == GameConstants.EntityTypes.PlayerFleet)
					{
						totalFuelNeeded += HexMath.HexDistance(sHex, targetHex) * 0.25f;
						containsPlayerFleet = true;
					}
				}

				if (!containsPlayerFleet) return;
				if (totalFuelNeeded == 0f && !Combat.InCombat) return;

				if (!Combat.InCombat)
				{
					float currentFuel = _globalData != null ? _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle() : 0f;

					if (currentFuel < 0.25f)
					{
						ShowStrandedMenu();
						return; 
					}

					if (currentFuel < totalFuelNeeded)
					{
						if (UI != null) UI.CombatLogPanel.Visible = true;
						LogCombatMessage($"\n[color=red]*** MOVEMENT ABORTED: INSUFFICIENT FUEL ({totalFuelNeeded} {GameConstants.ResourceKeys.RawMaterials} Req) ***[/color]");
						return; 
					}
				}

				bool playerActuallyMoved = false;

				if (SelectedHexes.Count == 1)
				{
					Vector2I shipHex = SelectedHexes[0];
					if (HexContents.ContainsKey(shipHex) && HexContents[shipHex].Type == GameConstants.EntityTypes.PlayerFleet)
					{
						MapEntity ship = HexContents[shipHex];
						
						if (!Combat.InCombat)
						{
							if (IsHexWalkable(targetHex))
							{
								MoveShip(shipHex, targetHex, 0);
								SelectedHexes[0] = targetHex;
								UpdateHighlights();
								playerActuallyMoved = true;
							}
						}
						else
						{
							Dictionary<Vector2I, int> reachable = GetReachableHexes(shipHex, ship.CurrentActions);
							if (reachable.ContainsKey(targetHex))
							{
								int cost = reachable[targetHex];
								MoveShip(shipHex, targetHex, cost);
								SelectedHexes[0] = targetHex;
								UpdateHighlights();
							}
						}
					}
				}
				else if (!Combat.InCombat) 
				{
					MoveGroup(SelectedHexes, targetHex);
					playerActuallyMoved = true;
				}

				if (playerActuallyMoved && !Combat.InCombat)
				{
					RunAfterMovementCompletes(AdvanceExplorationTurn);
				}
			}
		}
	}

	private void ExecuteLongRangeScan(Vector2I targetHex)
	{
		IsTargetingLongRange = false;
		
		if (_globalData == null || _globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() < 5f)
		{
			LogCombatMessage("\n[color=red]*** SCAN FAILED: INSUFFICIENT ENERGY CORES (5.0 Req) ***[/color]");
			return;
		}

		_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores] = _globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() - 5f;
		UpdateResourceUI();

		SystemData sys = _globalData.ExploredSystems[_globalData.SavedSystem];
		
		for (int q = -10; q <= 10; q++)
		{
			int r1 = Mathf.Max(-10, -q - 10);
			int r2 = Mathf.Min(10, -q + 10);
			for (int r = r1; r <= r2; r++)
			{
				Vector2I h = new Vector2I(targetHex.X + q, targetHex.Y + r);
				if (!sys.RadarRevealedHexes.Contains(h)) sys.RadarRevealedHexes.Add(h);
			}
		}

		Fog.UpdateVisibility(); 
		
		UI.CombatLogPanel.Visible = true;
		LogCombatMessage("\n[color=#00ffff]*** DEEP SPACE TELEMETRY UPDATED ***[/color]");
		LogCombatMessage($"[color=yellow]-5.0 {GameConstants.ResourceKeys.EnergyCores}[/color]");

		if (SfxPlayer != null)
		{
			AudioStream scanSfx = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (scanSfx != null) { SfxPlayer.Stream = scanSfx; SfxPlayer.PitchScale = 0.4f; SfxPlayer.Play(); }
		}
		
		ToggleShipMenu(false);
	}

	private void OnLongRangePressed()
	{
		if (IsFleetMoving) return;
		IsTargetingLongRange = !IsTargetingLongRange;
		if (UI != null) UI.CombatLogPanel.Visible = true;
		if (IsTargetingLongRange) LogCombatMessage("\n[color=yellow]Awaiting Deep Scan Coordinates... (Left-Click to Scan, Right-Click to Cancel)[/color]");
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
		
		if (IsTargetingLongRange)
		{
			_radarHighlight.Visible = true;
			_radarHighlight.Position = HexMath.HexToPixel(_currentHoveredHex, HexSize);
			_hoverHighlight.Visible = false;
		}
		else
		{
			_radarHighlight.Visible = false;
			if (HexGrid.ContainsKey(_currentHoveredHex))
			{
				_hoverHighlight.Visible = true;
				_hoverHighlight.Position = HexMath.HexToPixel(_currentHoveredHex, HexSize);

				if (HexContents.ContainsKey(_currentHoveredHex))
				{
					MapEntity hoveredEntity = HexContents[_currentHoveredHex];
					bool isEnemy = hoveredEntity.Type == GameConstants.EntityTypes.EnemyFleet;
					bool isPlayer = hoveredEntity.Type == GameConstants.EntityTypes.PlayerFleet;

					if ((isEnemy || isPlayer) && GodotObject.IsInstanceValid(hoveredEntity.VisualSprite) && hoveredEntity.VisualSprite.Visible)
					{
						if (isEnemy) _hoverHighlight.Color = new Color(1f, 0f, 0f, 0.4f); 
						else _hoverHighlight.Color = new Color(0f, 1f, 0f, 0.4f); 
						
						_hoverTooltip.Text = $"=== {hoveredEntity.Name.ToUpper()} ===\nHP: {hoveredEntity.CurrentHP} / {hoveredEntity.MaxHP}\nShields: {hoveredEntity.CurrentShields} / {hoveredEntity.MaxShields}\nAttack: {hoveredEntity.AttackDamage} DMG\nRange: {hoveredEntity.AttackRange} Hexes";
						
						Vector2 screenPos = _hoverHighlight.GetGlobalTransformWithCanvas().Origin;
						float currentZoom = GetViewportTransform().Scale.X;
						
						_hoverTooltip.Position = screenPos + new Vector2((HexSize * currentZoom) + 15, -60);
						_hoverTooltip.Visible = true;
					}
					else
					{
						_hoverHighlight.Color = new Color(0f, 1f, 1f, 0.4f); 
						_hoverTooltip.Visible = false;
					}
				}
				else
				{
					_hoverHighlight.Color = new Color(0f, 1f, 1f, 0.4f); 
					_hoverTooltip.Visible = false;
				}
			}
			else
			{
				_hoverHighlight.Visible = false;
				_hoverTooltip.Visible = false; 
			}
		}

		foreach (Vector2I hex in SelectedHexes)
		{
			if (HexContents.ContainsKey(hex))
			{
				MapEntity selectedShip = HexContents[hex];
				if (GodotObject.IsInstanceValid(selectedShip.VisualSprite))
				{
					float targetAngle = selectedShip.VisualSprite.GlobalPosition.AngleToPoint(globalMousePos) + selectedShip.BaseRotationOffset;
					selectedShip.VisualSprite.Rotation = Mathf.LerpAngle(selectedShip.VisualSprite.Rotation, targetAngle, 0.15f);
				}
			}
		}

		EnvironmentLayer.Rotation -= 0.05f * (float)delta; 
		foreach (Node child in EnvironmentLayer.GetChildren())
		{
			if (child is Polygon2D rock)
			{
				float spin = rock.GetMeta("spin_speed").AsSingle();
				rock.Rotation += spin * (float)delta;
			}
		}
		
		Hazards.ProcessHazards(delta);
		UpdateJumpButton();
		UpdateAttackButton();
	}

	private void UpdateJumpButton()
	{
		if (_jumpService == null) return;

		JumpButtonState state = _jumpService.BuildJumpButtonState(Combat.InCombat, SelectedHexes, HexContents);
		UI.JumpButton.Text = state.ButtonText;
		UI.JumpButton.Visible = state.IsVisible;
	}

	private void UpdateAttackButton()
	{
		if (SelectedHexes.Count == 1 && HexContents.ContainsKey(SelectedHexes[0]))
		{
			MapEntity singleShip = HexContents[SelectedHexes[0]];
			if (singleShip.Type == GameConstants.EntityTypes.PlayerFleet && singleShip.CurrentActions > 0 && (!Combat.InCombat || singleShip == Combat.ActiveShip))
			{
				UI.AttackButton.Visible = true;
			}
			else
			{
				UI.AttackButton.Visible = false;
				Combat.IsTargeting = false;
				UI.AttackButton.Text = "ATTACK";
			}
		}
		else
		{
			UI.AttackButton.Visible = false;
			Combat.IsTargeting = false;
			UI.AttackButton.Text = "ATTACK";
		}
	}

	internal void ToggleShipMenu(bool expand, MapEntity ship = null)
	{
		if (UI == null) return;
		Tween tween = CreateTween();
		Vector2 screenSize = GetViewportRect().Size;
		float targetX = expand ? screenSize.X - 320 : screenSize.X + 50; 
		tween.TweenProperty(UI.ShipMenuPanel, "position:x", targetX, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		
		if (UI.BtnLongRange != null) UI.BtnLongRange.Visible = false;
		
		if (_btnTrade != null) _btnTrade.Visible = false;
		if (_btnEquip != null) _btnEquip.Visible = false; // --- NEW: Hide equip by default ---

		if (expand && ship != null)
		{
			CurrentlyViewedShip = ship;
			ShipMenuState menuState = _shipContextService?.BuildMenuState(ship, Combat.InCombat, HexContents);
			if (menuState == null) return;
			UI.ShipMenuTitle.Text = menuState.Title;

			string imagePath = menuState.ImagePath;
			if (!string.IsNullOrEmpty(imagePath))
			{
				Texture2D tex = GD.Load<Texture2D>(imagePath);
				if (tex != null) UI.ShipImageDisplay.Texture = tex;
			}
			
			float hpPercent = menuState.HpPercent;
			UI.ShipImageDisplay.Modulate = new Color(1f, hpPercent, hpPercent); 

			UI.HpBar.MaxValue = ship.MaxHP;
			UI.HpBar.Value = ship.CurrentHP;
			UI.HpLabel.Text = $"HULL INTEGRITY: {ship.CurrentHP}/{ship.MaxHP}";
			UI.ShieldBar.MaxValue = ship.MaxShields;
			UI.ShieldBar.Value = ship.CurrentShields;
			UI.ShieldLabel.Text = $"SHIELD CAPACITORS: {ship.CurrentShields}/{ship.MaxShields}";

			UI.ShipMenuDetails.Text = menuState.DetailsText;
				
			bool isPlayer = menuState.IsPlayerShip;
			UI.BtnWeapons.Visible = isPlayer;
			UI.BtnShields.Visible = isPlayer;
			UI.BtnRepair.Visible = isPlayer;
			UI.BtnRepair.Disabled = !menuState.CanRepair;
			UI.CodexButton.Visible = isPlayer;
			UI.BtnScan.Visible = false;
			UI.BtnSalvage.Visible = false;

			if (menuState.ShowEquip && _btnEquip != null)
			{
				_btnEquip.Visible = true;
			}

			if (menuState.ShowLongRange && UI.BtnLongRange != null)
			{
				UI.BtnLongRange.Visible = true;
				UI.BtnLongRange.Disabled = menuState.DisableLongRange;
			}

			if (menuState.ShowScan)
			{
				UI.BtnScan.Visible = true;
				UI.BtnScan.Disabled = menuState.DisableScan;
				UI.BtnScan.Text = menuState.ScanText;
			}

			if (menuState.ShowSalvage)
			{
				UI.BtnSalvage.Visible = true;
				UI.BtnSalvage.Disabled = menuState.DisableSalvage;
				UI.BtnSalvage.Text = menuState.SalvageText;
			}

			if (menuState.ShowTrade && _btnTrade != null)
			{
				_btnTrade.Visible = true;
			}

			if (isPlayer && !Combat.InCombat)
			{
				if (!menuState.ShowLongRange && UI.BtnLongRange != null)
				{
					UI.BtnLongRange.Visible = false;
				}
			}
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
		UI.CombatLogPanel.Visible = true;
		LogCombatMessage("\n[color=yellow]--- FLEET INVENTORY ---[/color]");
		if (_globalData != null)
		{
			foreach(var kvp in _globalData.FleetResources)
			{
				LogCombatMessage($"- {kvp.Key}: {kvp.Value.AsSingle():0.##}");
			}
			
			int weaponCount = _globalData.UnequippedInventory.Count(id => id.StartsWith(GameConstants.ItemPrefixes.Weapon));
			int shieldCount = _globalData.UnequippedInventory.Count(id => id.StartsWith(GameConstants.ItemPrefixes.Shield));
			int armorCount = _globalData.UnequippedInventory.Count(id => id.StartsWith(GameConstants.ItemPrefixes.Armor));
			
			LogCombatMessage($"\n[color=cyan]--- UNEQUIPPED UPGRADES ---[/color]");
			LogCombatMessage($"- Weapons: {weaponCount}");
			LogCombatMessage($"- Shields: {shieldCount}");
			LogCombatMessage($"- Armor: {armorCount}");
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
			AudioStream repairSound = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (repairSound != null) { SfxPlayer.Stream = repairSound; SfxPlayer.PitchScale = 1.5f; SfxPlayer.Play(); }
		}
		
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
			AudioStream jumpSound = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (jumpSound != null) { SfxPlayer.Stream = jumpSound; SfxPlayer.PitchScale = 0.5f; SfxPlayer.Play(); }
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
		if (_globalData != null)
		{
			_globalData.CurrentTurn = CurrentTurn;
			_globalData.InCombat = Combat.InCombat;
			_globalData.CurrentQueueIndex = Combat.GetCurrentQueueIndex();

			if (!string.IsNullOrEmpty(_globalData.SavedSystem) && _globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
			{
				SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
				currentSystem.AsteroidHexes = AsteroidHexes.ToList();
				currentSystem.RadiationHexes = RadiationHexes.ToList();
				if (Fog != null) currentSystem.ExploredHexes = Fog.GetExploredHexes().ToList();
			}
			
			var playerState = new Godot.Collections.Array();
			var enemyState = new Godot.Collections.Array(); 
			
			foreach (var kvp in HexContents)
			{
				if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet || kvp.Value.Type == GameConstants.EntityTypes.EnemyFleet)
				{
					var shipDict = new Godot.Collections.Dictionary<string, Variant>();
					shipDict["Name"] = kvp.Value.Name;
					shipDict["Q"] = kvp.Key.X; shipDict["R"] = kvp.Key.Y; 
					shipDict["CurrentHP"] = kvp.Value.CurrentHP; shipDict["MaxHP"] = kvp.Value.MaxHP;
					shipDict["CurrentShields"] = kvp.Value.CurrentShields; shipDict["MaxShields"] = kvp.Value.MaxShields;
					shipDict["MaxActions"] = kvp.Value.MaxActions;
					shipDict["CurrentActions"] = kvp.Value.CurrentActions;
					shipDict["CurrentInitiativeRoll"] = kvp.Value.CurrentInitiativeRoll; 
					
					if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet) playerState.Add(shipDict);
					if (kvp.Value.Type == GameConstants.EntityTypes.EnemyFleet) enemyState.Add(shipDict);
				}
			}
			
			_globalData.SavedFleetState = playerState;
			
			if (!string.IsNullOrEmpty(_globalData.SavedSystem) && _globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
			{
				_globalData.ExploredSystems[_globalData.SavedSystem].EnemyFleets = enemyState;
			}

			if (_globalData.HasMethod("SaveGame")) _globalData.Call("SaveGame");

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
	}

	private void OnMainMenuPressed() 
	{
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://main_menu.tscn");
		else GetTree().ChangeSceneToFile("res://main_menu.tscn");
	}

	internal void MoveShip(Vector2I fromHex, Vector2I toHex, int cost)
	{
		MapEntity ship = HexContents[fromHex];
		HexContents.Remove(fromHex);
		HexContents[toHex] = ship;
		ship.CurrentActions -= cost;
		if (SelectedHexes.Count == 1 && SelectedHexes[0] == fromHex) ToggleShipMenu(true, ship);
		
		string sfxPath = Database.GetShipMovementSoundPath(ship.Name);
		if (!string.IsNullOrEmpty(sfxPath) && SfxPlayer != null && ship.VisualSprite.Visible)
		{
			AudioStream sfx = GD.Load<AudioStream>(sfxPath);
			if (sfx != null) { SfxPlayer.Stream = sfx; SfxPlayer.Play(); }
		}

		if (ship.Type == GameConstants.EntityTypes.PlayerFleet && _globalData != null && !Combat.InCombat)
		{
			int distance = HexMath.HexDistance(fromHex, toHex);
			float fuelCost = distance * 0.25f;
			float currentFuel = _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle();
			_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] = Mathf.Max(0f, currentFuel - fuelCost);
			UpdateResourceUI(); 
		}

		ActiveMovementTweens++;

		Tween tween = CreateTween();
		Vector2 targetPixelPos = HexMath.HexToPixel(toHex, HexSize);
		float distancePixels = ship.VisualSprite.Position.DistanceTo(targetPixelPos);
		float duration = Mathf.Max(0.3f, distancePixels / 500f); 
		tween.TweenProperty(ship.VisualSprite, "position", targetPixelPos, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		
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
		foreach (Node child in _highlightLayer.GetChildren()) child.QueueFree();

		foreach (Vector2I hex in SelectedHexes) CreateHighlightPolygon(hex, new Color(1f, 0.8f, 0f, 0.6f)); 

		if (Combat.InCombat && SelectedHexes.Count == 1 && Combat.ActiveShip != null && HexContents.ContainsKey(SelectedHexes[0]) && HexContents[SelectedHexes[0]] == Combat.ActiveShip)
		{
			Dictionary<Vector2I, int> reachable = GetReachableHexes(SelectedHexes[0], Combat.ActiveShip.CurrentActions);
			foreach (Vector2I hex in reachable.Keys)
			{
				if (hex == SelectedHexes[0]) continue; 
				CreateHighlightPolygon(hex, new Color(0f, 1f, 0.3f, 0.4f)); 
			}
		}
	}

	private void CreateHighlightPolygon(Vector2I hexCoord, Color color)
	{
		Polygon2D poly = new Polygon2D();
		Vector2[] points = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float angle_deg = 60 * i - 30;
			float angle_rad = Mathf.DegToRad(angle_deg);
			points[i] = new Vector2(HexSize * Mathf.Cos(angle_rad), HexSize * Mathf.Sin(angle_rad));
		}
		poly.Polygon = points; poly.Color = color; poly.Position = HexMath.HexToPixel(hexCoord, HexSize);
		_highlightLayer.AddChild(poly);
	}

	internal void LogCombatMessage(string message) { if (UI != null && UI.CombatLogText != null) UI.CombatLogText.Text += message + "\n"; }

	internal void CheckGameOver()
	{
		if (!HexContents.Values.Any(s => s.Type == GameConstants.EntityTypes.PlayerFleet)) UI.GameOverPanel.Visible = true;
	}

	private void OnAttackPressed()
	{
		if (IsFleetMoving) return; 
		Combat.IsTargeting = !Combat.IsTargeting;
		if (UI != null) UI.AttackButton.Text = Combat.IsTargeting ? "CANCEL TARGET" : "ATTACK";
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
