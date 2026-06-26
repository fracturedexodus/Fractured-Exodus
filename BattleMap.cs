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
	private TextureRect _equipShipPortrait;
	private Label _equipShipNameLabel;
	private Label _equipShipRoleLabel;
	private Label _equipShipStatsLabel;
	private Label _equipSummaryLabel;
	private Label _equipFooterLabel;
	private VBoxContainer _equipActiveLoadoutStack;
	private VBoxContainer _equipWeaponStack;
	private VBoxContainer _equipShieldStack;
	private VBoxContainer _equipArmorStack;
	private VBoxContainer _equipMissileStack;
	private VBoxContainer _equipItemList;
	private Button _btnMission;
	private Button _btnOfficerSwap;
	private CenterContainer _savePromptWrapper;
	private LineEdit _saveNameLineEdit;
	private Label _savePromptStatusLabel;
	private CenterContainer _pauseMenuWrapper;
	private CenterContainer _loadMenuWrapper;
	private ItemList _loadSaveList;
	private Label _loadSaveDetailsLabel;
	private Label _loadSaveStatusLabel;
	private Button _loadSelectedSaveButton;
	private Button _deleteSelectedSaveButton;
	private CenterContainer _deleteSaveConfirmWrapper;
	private Label _deleteSaveConfirmLabel;
	private string _pendingDeleteSaveSlotId = string.Empty;
	private string _pendingDeleteSaveDisplayName = string.Empty;
	private readonly List<SaveGameSlotInfo> _availableSaveGames = new List<SaveGameSlotInfo>();
	private CenterContainer _missionPromptWrapper;
	private Label _missionPromptTitle;
	private RichTextLabel _missionPromptDescription;
	private Label _missionOfficerSelectionLabel;
	private ScrollContainer _missionOfficerSelectionScroll;
	private VBoxContainer _missionOfficerSelectionList;
	private Label _missionOfficerSelectionStatus;
	private Button _missionLaunchButton;
	private MapEntity _pendingMissionShip;
	private MissionInteractionContext _pendingMissionContext;
	private readonly Dictionary<string, CheckBox> _missionOfficerCheckboxes = new Dictionary<string, CheckBox>();
	private int _pendingMissionRecommendedOfficerCount = 1;
	private bool _isUpdatingMissionOfficerSelection;
	private CenterContainer _replacementPromptWrapper;
	private Label _replacementPromptTitleLabel;
	private RichTextLabel _replacementPromptBodyLabel;
	private Label _replacementPromptStatusLabel;
	private string _activeReplacementShipName = string.Empty;
	private CenterContainer _officerSwapMenuWrapper;
	private Label _officerSwapTitleLabel;
	private RichTextLabel _officerSwapBodyLabel;
	private VBoxContainer _officerSwapOptionList;
	private Label _officerSwapStatusLabel;
	private string _activeOfficerSwapShipName = string.Empty;
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
	private MissionService _missionService;
	private PanelContainer _officerMenuPanel;
	private Label _officerMenuTitle;
	private TextureRect _officerPortraitDisplay;
	private Label _officerDetailsLabel;
	private ColorRect _officerInventoryOverlay;
	private PanelContainer _officerInventoryPanel;
	private TextureRect _officerInventoryPortrait;
	private Label _officerInventoryNameLabel;
	private Label _officerInventoryRoleLabel;
	private Label _officerInventoryStatsLabel;
	private Label _officerInventorySummaryLabel;
	private Label _officerInventoryFooterLabel;
	private VBoxContainer _officerInventoryLoadoutStack;
	private VBoxContainer _officerInventoryWeaponStack;
	private VBoxContainer _officerInventoryShieldStack;
	private VBoxContainer _officerInventoryItemStack;
	private Button _officerInventoryCloseButton;
	private string _activeOfficerInventoryShipName = string.Empty;
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
		if (_globalData != null) _missionService = new MissionService(_globalData);
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
		BuildSavePromptUI();
		BuildPauseMenuUI();
		BuildLoadGameMenuUI();
		BuildMissionPromptUI();
		BuildOfficerReplacementPromptUI();
		BuildOfficerSwapMenuUI();
		BuildOfficerPanel();
		BuildOfficerInventoryPanel();
		
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

		_btnMission = new Button();
		_btnMission.Text = "BLACK SITE MISSION";
		_btnMission.Visible = false;
		_btnMission.CustomMinimumSize = new Vector2(0, 40);
		_btnMission.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_btnMission.AddThemeColorOverride("font_color", new Color(0.4f, 0.95f, 1f));
		_btnMission.Pressed += OnMissionPressed;

		if (UI != null && UI.BtnSalvage != null)
		{
			UI.BtnSalvage.GetParent().AddChild(_btnMission);
		}
		else
		{
			AddChild(_btnMission);
		}

		_btnOfficerSwap = new Button();
		_btnOfficerSwap.Text = "SWAP OFFICER";
		_btnOfficerSwap.Visible = false;
		_btnOfficerSwap.CustomMinimumSize = new Vector2(0, 40);
		_btnOfficerSwap.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_btnOfficerSwap.AddThemeColorOverride("font_color", new Color(1f, 0.82f, 0.45f));
		_btnOfficerSwap.Pressed += OpenOfficerSwapMenu;

		if (UI != null && UI.BtnRepair != null)
		{
			UI.BtnRepair.GetParent().AddChild(_btnOfficerSwap);
		}
		else
		{
			AddChild(_btnOfficerSwap);
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
						MaxShields = 500, CurrentShields = 500,
						MissionInteractionKey = outpost.MissionInteractionKey ?? string.Empty
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

		CallDeferred(nameof(ShowPendingOfficerReplacementPromptIfNeeded));
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
		float population = _globalData.FleetResources.ContainsKey(GameConstants.ResourceKeys.Population)
			? _globalData.FleetResources[GameConstants.ResourceKeys.Population].AsSingle()
			: 0f;

		UI.InventoryDisplay.Text = $"{GameConstants.ResourceKeys.RawMaterials}: {raw:0.##}\n{GameConstants.ResourceKeys.EnergyCores}: {energy:0.##}\n{GameConstants.ResourceKeys.AncientTech}: {tech:0.##}\n{CampaignText.RemnantsLabel}: {population:0}";
	}

	private void ConnectUIButtons()
	{
		if (UI == null) return;
		UI.EndTurnButton.Pressed += OnEndTurnPressed;
		UI.RepairFleetButton.Pressed += OnRepairFleetPressed;
		UI.InventoryButton.Pressed += OnInventoryPressed;
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

	private void BuildSavePromptUI()
	{
		CanvasLayer saveLayer = new CanvasLayer { Layer = 175 };
		AddChild(saveLayer);

		_savePromptWrapper = new CenterContainer();
		_savePromptWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_savePromptWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_savePromptWrapper.Visible = false;
		saveLayer.AddChild(_savePromptWrapper);

		PanelContainer savePanel = new PanelContainer();
		StyleBoxFlat style = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.11f, 0.96f),
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderColor = new Color(0.3f, 0.95f, 1f, 0.85f),
			ContentMarginLeft = 24,
			ContentMarginRight = 24,
			ContentMarginTop = 20,
			ContentMarginBottom = 20
		};
		savePanel.AddThemeStyleboxOverride("panel", style);
		savePanel.CustomMinimumSize = new Vector2(520f, 220f);
		_savePromptWrapper.AddChild(savePanel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		savePanel.AddChild(content);

		Label title = new Label
		{
			Text = "NAME SAVE FILE",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		title.AddThemeColorOverride("font_color", new Color(0.88f, 0.98f, 1f));
		content.AddChild(title);

		Label body = new Label
		{
			Text = "Enter a name for this campaign save.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(body);

		_saveNameLineEdit = new LineEdit
		{
			PlaceholderText = "Black Site - Turn 3",
			CustomMinimumSize = new Vector2(0f, 42f)
		};
		_saveNameLineEdit.TextSubmitted += _ => ConfirmManualSavePrompt();
		content.AddChild(_saveNameLineEdit);

		_savePromptStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_savePromptStatusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.55f));
		content.AddChild(_savePromptStatusLabel);

		HBoxContainer buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttonRow);

		Button cancelButton = new Button
		{
			Text = "CANCEL",
			CustomMinimumSize = new Vector2(180f, 42f)
		};
		cancelButton.Pressed += HideSavePrompt;
		buttonRow.AddChild(cancelButton);

		Button saveButton = new Button
		{
			Text = "SAVE",
			CustomMinimumSize = new Vector2(180f, 42f)
		};
		saveButton.Pressed += ConfirmManualSavePrompt;
		buttonRow.AddChild(saveButton);
	}

	private void BuildPauseMenuUI()
	{
		CanvasLayer pauseLayer = new CanvasLayer { Layer = 176 };
		AddChild(pauseLayer);

		_pauseMenuWrapper = new CenterContainer();
		_pauseMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_pauseMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_pauseMenuWrapper.Visible = false;
		pauseLayer.AddChild(_pauseMenuWrapper);

		PanelContainer pausePanel = new PanelContainer();
		pausePanel.CustomMinimumSize = new Vector2(440f, 320f);
		pausePanel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
		_pauseMenuWrapper.AddChild(pausePanel);

		VBoxContainer content = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		content.AddThemeConstantOverride("separation", 12);
		pausePanel.AddChild(content);

		Label title = new Label
		{
			Text = "GAME MENU",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 26);
		content.AddChild(title);

		content.AddChild(BuildPauseMenuButton("SAVE GAME", OpenPauseSavePrompt));
		content.AddChild(BuildPauseMenuButton("LOAD GAME", ShowLoadGameMenu));
		content.AddChild(BuildPauseMenuButton("RETURN TO GAME", HidePauseMenus));
		content.AddChild(BuildPauseMenuButton("RETURN TO MAIN MENU", ReturnToMainMenuFromPause));
	}

	private void BuildLoadGameMenuUI()
	{
		CanvasLayer loadLayer = new CanvasLayer { Layer = 177 };
		AddChild(loadLayer);

		_loadMenuWrapper = new CenterContainer();
		_loadMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_loadMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_loadMenuWrapper.Visible = false;
		loadLayer.AddChild(_loadMenuWrapper);

		PanelContainer loadPanel = new PanelContainer();
		loadPanel.CustomMinimumSize = new Vector2(720f, 560f);
		loadPanel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
		_loadMenuWrapper.AddChild(loadPanel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		loadPanel.AddChild(content);

		Label title = new Label
		{
			Text = "LOAD GAME",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		content.AddChild(title);

		_loadSaveList = new ItemList
		{
			CustomMinimumSize = new Vector2(0f, 250f),
			SelectMode = ItemList.SelectModeEnum.Single
		};
		_loadSaveList.ItemSelected += index => UpdateLoadGameSelection((int)index);
		_loadSaveList.ItemActivated += index =>
		{
			UpdateLoadGameSelection((int)index);
			LoadSelectedPauseSave();
		};
		content.AddChild(_loadSaveList);

		_loadSaveDetailsLabel = new Label
		{
			CustomMinimumSize = new Vector2(0f, 108f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_loadSaveDetailsLabel);

		_loadSaveStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_loadSaveStatusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f));
		content.AddChild(_loadSaveStatusLabel);

		HBoxContainer buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttonRow);

		buttonRow.AddChild(BuildPauseMenuButton("BACK", ShowPauseMenu, 180f));

		_deleteSelectedSaveButton = BuildPauseMenuButton("DELETE", PromptDeleteSelectedPauseSave, 180f);
		_deleteSelectedSaveButton.Disabled = true;
		buttonRow.AddChild(_deleteSelectedSaveButton);

		_loadSelectedSaveButton = BuildPauseMenuButton("LOAD SELECTED", LoadSelectedPauseSave, 220f);
		_loadSelectedSaveButton.Disabled = true;
		buttonRow.AddChild(_loadSelectedSaveButton);

		BuildDeleteSaveConfirmationUI(loadLayer);
	}

	private void ShowSavePrompt()
	{
		if (_savePromptWrapper == null || _saveNameLineEdit == null)
		{
			return;
		}

		_savePromptStatusLabel.Text = string.Empty;
		_saveNameLineEdit.Text = BuildDefaultSaveName();
		_savePromptWrapper.Visible = true;
		_saveNameLineEdit.GrabFocus();
		_saveNameLineEdit.SelectAll();
	}

	private void HideSavePrompt()
	{
		if (_savePromptWrapper != null)
		{
			_savePromptWrapper.Visible = false;
		}
	}

	private void ShowPauseMenu()
	{
		HideDeleteSaveConfirmation();
		if (_pauseMenuWrapper == null)
		{
			return;
		}

		_pauseMenuWrapper.Visible = true;
		if (_loadMenuWrapper != null)
		{
			_loadMenuWrapper.Visible = false;
		}
	}

	private void HidePauseMenus()
	{
		HideDeleteSaveConfirmation();
		if (_pauseMenuWrapper != null)
		{
			_pauseMenuWrapper.Visible = false;
		}

		if (_loadMenuWrapper != null)
		{
			_loadMenuWrapper.Visible = false;
		}
	}

	private void TogglePauseMenu()
	{
		if (_deleteSaveConfirmWrapper?.Visible == true)
		{
			HideDeleteSaveConfirmation();
			return;
		}

		if (_loadMenuWrapper?.Visible == true)
		{
			ShowPauseMenu();
			return;
		}

		if (_savePromptWrapper?.Visible == true)
		{
			HideSavePrompt();
			return;
		}

		if (_pauseMenuWrapper == null)
		{
			return;
		}

		_pauseMenuWrapper.Visible = !_pauseMenuWrapper.Visible;
	}

	private void OpenPauseSavePrompt()
	{
		HidePauseMenus();
		ShowSavePrompt();
	}

	private void ShowLoadGameMenu()
	{
		if (_globalData == null || _loadMenuWrapper == null || _loadSaveList == null)
		{
			return;
		}

		_availableSaveGames.Clear();
		_availableSaveGames.AddRange(_globalData.GetAvailableSaveGames());
		_loadSaveList.Clear();
		_loadSaveDetailsLabel.Text = string.Empty;
		_loadSaveStatusLabel.Text = string.Empty;
		_loadSelectedSaveButton.Disabled = _availableSaveGames.Count == 0;
		_deleteSelectedSaveButton.Disabled = _availableSaveGames.Count == 0;

		for (int i = 0; i < _availableSaveGames.Count; i++)
		{
			_loadSaveList.AddItem(BuildSaveListLabel(_availableSaveGames[i]));
		}

		_pauseMenuWrapper.Visible = false;
		_loadMenuWrapper.Visible = true;
		if (_availableSaveGames.Count > 0)
		{
			_loadSaveList.Select(0);
			UpdateLoadGameSelection(0);
		}
		else
		{
			_loadSaveStatusLabel.Text = "No save files found.";
		}
	}

	private void UpdateLoadGameSelection(int index)
	{
		if (index < 0 || index >= _availableSaveGames.Count)
		{
			_loadSaveDetailsLabel.Text = string.Empty;
			_loadSelectedSaveButton.Disabled = true;
			_deleteSelectedSaveButton.Disabled = true;
			return;
		}

		SaveGameSlotInfo save = _availableSaveGames[index];
		string locationText = !string.IsNullOrWhiteSpace(save.CurrentMissionTitle)
			? $"Mission: {save.CurrentMissionTitle}"
			: !string.IsNullOrWhiteSpace(save.SavedSystem)
				? $"System: {save.SavedSystem}{(string.IsNullOrWhiteSpace(save.SavedPlanet) ? string.Empty : $" | Planet: {save.SavedPlanet}")}"
				: "Location: Unknown";
		_loadSaveDetailsLabel.Text = $"{locationText}\nTurn: {save.CurrentTurn}\nSaved: {FormatSaveTimestamp(save.SavedAtUtc)}";
		_loadSelectedSaveButton.Disabled = false;
		_deleteSelectedSaveButton.Disabled = false;
	}

	private void LoadSelectedPauseSave()
	{
		if (_globalData == null || _loadSaveList == null)
		{
			return;
		}

		int[] selectedItems = _loadSaveList.GetSelectedItems();
		if (selectedItems.Length == 0)
		{
			_loadSaveStatusLabel.Text = "Select a save first.";
			return;
		}

		int selectedIndex = selectedItems[0];
		if (selectedIndex < 0 || selectedIndex >= _availableSaveGames.Count)
		{
			_loadSaveStatusLabel.Text = "That save could not be found.";
			return;
		}

		SaveGameSlotInfo selectedSave = _availableSaveGames[selectedIndex];
		if (!_globalData.LoadGame(selectedSave.SlotId))
		{
			_loadSaveStatusLabel.Text = "Unable to load that save.";
			return;
		}

		string scenePath = ResolveLoadedScenePath(_globalData, selectedSave);
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(scenePath);
			return;
		}

		GetTree().ChangeSceneToFile(scenePath);
	}

	private void BuildDeleteSaveConfirmationUI(CanvasLayer loadLayer)
	{
		_deleteSaveConfirmWrapper = new CenterContainer();
		_deleteSaveConfirmWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_deleteSaveConfirmWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_deleteSaveConfirmWrapper.Visible = false;
		loadLayer.AddChild(_deleteSaveConfirmWrapper);

		PanelContainer confirmPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(520f, 220f)
		};
		confirmPanel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
		_deleteSaveConfirmWrapper.AddChild(confirmPanel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		confirmPanel.AddChild(content);

		Label title = new Label
		{
			Text = "DELETE SAVE",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		content.AddChild(title);

		_deleteSaveConfirmLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		content.AddChild(_deleteSaveConfirmLabel);

		HBoxContainer buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttonRow);

		buttonRow.AddChild(BuildPauseMenuButton("CANCEL", HideDeleteSaveConfirmation, 180f));
		buttonRow.AddChild(BuildPauseMenuButton("DELETE SAVE", ConfirmDeleteSelectedPauseSave, 200f));
	}

	private void PromptDeleteSelectedPauseSave()
	{
		if (_loadSaveList == null || _deleteSaveConfirmWrapper == null || _deleteSaveConfirmLabel == null)
		{
			return;
		}

		int[] selectedItems = _loadSaveList.GetSelectedItems();
		if (selectedItems.Length == 0)
		{
			_loadSaveStatusLabel.Text = "Select a save first.";
			return;
		}

		int selectedIndex = selectedItems[0];
		if (selectedIndex < 0 || selectedIndex >= _availableSaveGames.Count)
		{
			_loadSaveStatusLabel.Text = "That save could not be found.";
			return;
		}

		SaveGameSlotInfo selectedSave = _availableSaveGames[selectedIndex];
		_pendingDeleteSaveSlotId = selectedSave.SlotId;
		_pendingDeleteSaveDisplayName = BuildSaveListLabel(selectedSave);
		_deleteSaveConfirmLabel.Text = $"Delete {_pendingDeleteSaveDisplayName}?\nThis cannot be undone.";
		_deleteSaveConfirmWrapper.Visible = true;
	}

	private void HideDeleteSaveConfirmation()
	{
		_pendingDeleteSaveSlotId = string.Empty;
		_pendingDeleteSaveDisplayName = string.Empty;
		if (_deleteSaveConfirmWrapper != null)
		{
			_deleteSaveConfirmWrapper.Visible = false;
		}
	}

	private void ConfirmDeleteSelectedPauseSave()
	{
		if (_globalData == null || string.IsNullOrWhiteSpace(_pendingDeleteSaveSlotId))
		{
			HideDeleteSaveConfirmation();
			return;
		}

		string deletedSaveName = _pendingDeleteSaveDisplayName;
		bool deleted = _globalData.DeleteSaveGame(_pendingDeleteSaveSlotId);
		HideDeleteSaveConfirmation();
		if (!deleted)
		{
			_loadSaveStatusLabel.Text = "Unable to delete that save.";
			return;
		}

		ShowLoadGameMenu();
		_loadSaveStatusLabel.Text = $"Deleted {deletedSaveName}.";
	}

	private void ConfirmManualSavePrompt()
	{
		string saveName = _saveNameLineEdit?.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(saveName))
		{
			if (_savePromptStatusLabel != null)
			{
				_savePromptStatusLabel.Text = "Enter a name before saving.";
			}
			_saveNameLineEdit?.GrabFocus();
			return;
		}

		SaveCampaign(false, saveName);
		HideSavePrompt();
	}

	private string BuildDefaultSaveName()
	{
		string systemName = !string.IsNullOrWhiteSpace(_globalData?.SavedSystem)
			? _globalData.SavedSystem.ToUpperInvariant()
			: "CAMPAIGN";
		return $"{systemName} TURN {CurrentTurn}";
	}

	private void BuildMissionPromptUI()
	{
		CanvasLayer missionLayer = new CanvasLayer { Layer = 170 };
		AddChild(missionLayer);

		_missionPromptWrapper = new CenterContainer();
		_missionPromptWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_missionPromptWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_missionPromptWrapper.Visible = false;
		missionLayer.AddChild(_missionPromptWrapper);

		PanelContainer missionPanel = new PanelContainer();
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = new Color(0.04f, 0.06f, 0.1f, 0.96f);
		style.BorderWidthTop = 2;
		style.BorderWidthBottom = 2;
		style.BorderWidthLeft = 2;
		style.BorderWidthRight = 2;
		style.BorderColor = new Color(0.3f, 0.95f, 1f, 0.85f);
		style.ContentMarginLeft = 24;
		style.ContentMarginRight = 24;
		style.ContentMarginTop = 20;
		style.ContentMarginBottom = 20;
		missionPanel.AddThemeStyleboxOverride("panel", style);
		_missionPromptWrapper.AddChild(missionPanel);

		VBoxContainer content = new VBoxContainer();
		content.CustomMinimumSize = new Vector2(760, 0);
		content.AddThemeConstantOverride("separation", 14);
		missionPanel.AddChild(content);

		_missionPromptTitle = new Label();
		_missionPromptTitle.Text = "BLACK SITE RELAY";
		_missionPromptTitle.HorizontalAlignment = HorizontalAlignment.Center;
		_missionPromptTitle.AddThemeColorOverride("font_color", new Color(0.4f, 0.95f, 1f));
		_missionPromptTitle.AddThemeFontSizeOverride("font_size", 22);
		content.AddChild(_missionPromptTitle);

		_missionPromptDescription = new RichTextLabel();
		_missionPromptDescription.CustomMinimumSize = new Vector2(760, 180);
		_missionPromptDescription.BbcodeEnabled = true;
		_missionPromptDescription.FitContent = true;
		_missionPromptDescription.ScrollActive = false;
		content.AddChild(_missionPromptDescription);

		_missionOfficerSelectionLabel = new Label
		{
			Text = "AWAY TEAM",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_missionOfficerSelectionLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.92f, 1f));
		_missionOfficerSelectionLabel.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(_missionOfficerSelectionLabel);

		_missionOfficerSelectionScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(760, 260),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		content.AddChild(_missionOfficerSelectionScroll);

		_missionOfficerSelectionList = new VBoxContainer();
		_missionOfficerSelectionList.AddThemeConstantOverride("separation", 8);
		_missionOfficerSelectionScroll.AddChild(_missionOfficerSelectionList);

		_missionOfficerSelectionStatus = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_missionOfficerSelectionStatus.AddThemeColorOverride("font_color", new Color(0.74f, 0.86f, 0.98f));
		content.AddChild(_missionOfficerSelectionStatus);

		HBoxContainer buttonRow = new HBoxContainer();
		buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
		buttonRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttonRow);

		_missionLaunchButton = new Button();
		_missionLaunchButton.Text = "DEPLOY AWAY TEAM";
		_missionLaunchButton.CustomMinimumSize = new Vector2(220, 42);
		_missionLaunchButton.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.75f));
		_missionLaunchButton.Pressed += LaunchPendingMission;
		buttonRow.AddChild(_missionLaunchButton);

		Button declineButton = new Button();
		declineButton.Text = "NOT YET";
		declineButton.CustomMinimumSize = new Vector2(160, 42);
		declineButton.Pressed += HideMissionPrompt;
		buttonRow.AddChild(declineButton);
	}

	private void BuildOfficerReplacementPromptUI()
	{
		CanvasLayer replacementLayer = new CanvasLayer { Layer = 190 };
		AddChild(replacementLayer);

		_replacementPromptWrapper = new CenterContainer();
		_replacementPromptWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_replacementPromptWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_replacementPromptWrapper.Visible = false;
		replacementLayer.AddChild(_replacementPromptWrapper);

		PanelContainer panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(620f, 320f);
		panel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
		_replacementPromptWrapper.AddChild(panel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		panel.AddChild(content);

		_replacementPromptTitleLabel = new Label
		{
			Text = "OFFICER DOWN",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_replacementPromptTitleLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.98f, 1f));
		_replacementPromptTitleLabel.AddThemeFontSizeOverride("font_size", 26);
		content.AddChild(_replacementPromptTitleLabel);

		_replacementPromptBodyLabel = new RichTextLabel
		{
			CustomMinimumSize = new Vector2(0f, 150f),
			BbcodeEnabled = true,
			ScrollActive = false,
			FitContent = true
		};
		content.AddChild(_replacementPromptBodyLabel);

		_replacementPromptStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_replacementPromptStatusLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.88f, 0.98f));
		content.AddChild(_replacementPromptStatusLabel);

		HBoxContainer buttonRow = new HBoxContainer();
		buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
		buttonRow.AddThemeConstantOverride("separation", 18);
		content.AddChild(buttonRow);

		buttonRow.AddChild(BuildPauseMenuButton("RECRUIT RANDOM OFFICER", RecruitReplacementOfficer, 260f));
		buttonRow.AddChild(BuildPauseMenuButton("DECIDE LATER", HideOfficerReplacementPrompt, 180f));
	}

	private void BuildOfficerSwapMenuUI()
	{
		CanvasLayer swapLayer = new CanvasLayer { Layer = 188 };
		AddChild(swapLayer);

		_officerSwapMenuWrapper = new CenterContainer();
		_officerSwapMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_officerSwapMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_officerSwapMenuWrapper.Visible = false;
		swapLayer.AddChild(_officerSwapMenuWrapper);

		PanelContainer panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(760f, 560f);
		panel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
		_officerSwapMenuWrapper.AddChild(panel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		panel.AddChild(content);

		_officerSwapTitleLabel = new Label
		{
			Text = "OFFICER TRANSFER",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_officerSwapTitleLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.98f, 1f));
		_officerSwapTitleLabel.AddThemeFontSizeOverride("font_size", 26);
		content.AddChild(_officerSwapTitleLabel);

		_officerSwapBodyLabel = new RichTextLabel
		{
			CustomMinimumSize = new Vector2(0f, 120f),
			BbcodeEnabled = true,
			ScrollActive = false,
			FitContent = true
		};
		content.AddChild(_officerSwapBodyLabel);

		ScrollContainer scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0f, 240f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		content.AddChild(scroll);

		_officerSwapOptionList = new VBoxContainer();
		_officerSwapOptionList.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_officerSwapOptionList);

		_officerSwapStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_officerSwapStatusLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.88f, 0.98f));
		content.AddChild(_officerSwapStatusLabel);

		HBoxContainer buttonRow = new HBoxContainer();
		buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
		buttonRow.AddThemeConstantOverride("separation", 18);
		content.AddChild(buttonRow);

		buttonRow.AddChild(BuildPauseMenuButton("CLOSE", HideOfficerSwapMenu, 180f));
	}

	private void PopulateMissionOfficerSelection(MissionInteractionContext missionContext)
	{
		if (_missionOfficerSelectionList == null || _globalData == null)
		{
			return;
		}

		foreach (Node child in _missionOfficerSelectionList.GetChildren())
		{
			child.QueueFree();
		}
		_missionOfficerCheckboxes.Clear();

		List<string> availableShips = (_globalData.SelectedPlayerFleet ?? new List<string>())
			.Where(shipName => !string.IsNullOrWhiteSpace(shipName))
			.Where(shipName => _officerService?.GetOfficerForShip(shipName) != null)
			.ToList();
		_pendingMissionRecommendedOfficerCount = Mathf.Max(1, missionContext?.Definition?.RecommendedOfficerCount ?? 1);
		int requiredSelections = Mathf.Min(_pendingMissionRecommendedOfficerCount, availableShips.Count);

		if (availableShips.Count == 0)
		{
			Label emptyLabel = new Label
			{
				Text = "No assigned officers are available in the current fleet. Assign officers before launching a mission.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			_missionOfficerSelectionList.AddChild(emptyLabel);
			UpdateMissionOfficerSelectionStatus();
			return;
		}

		HashSet<string> preferredSelection = (_globalData.SelectedMissionOfficerShipNames ?? new List<string>())
			.Where(shipName => !string.IsNullOrWhiteSpace(shipName))
			.ToHashSet();
		if (preferredSelection.Count == 0)
		{
			preferredSelection = availableShips.Take(requiredSelections).ToHashSet();
		}

		_isUpdatingMissionOfficerSelection = true;
		foreach (string shipName in availableShips)
		{
			OfficerState officer = _officerService?.GetOfficerForShip(shipName);
			OfficerMissionLoadoutService.EnsureOfficerLoadout(officer);

			PanelContainer rowPanel = new PanelContainer();
			StyleBoxFlat rowStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.07f, 0.1f, 0.15f, 0.88f),
				BorderWidthLeft = 1,
				BorderWidthTop = 1,
				BorderWidthRight = 1,
				BorderWidthBottom = 1,
				BorderColor = new Color(0.22f, 0.5f, 0.68f, 0.7f),
				ContentMarginLeft = 12,
				ContentMarginTop = 10,
				ContentMarginRight = 12,
				ContentMarginBottom = 10
			};
			rowPanel.AddThemeStyleboxOverride("panel", rowStyle);
			_missionOfficerSelectionList.AddChild(rowPanel);

			VBoxContainer rowContent = new VBoxContainer();
			rowContent.AddThemeConstantOverride("separation", 8);
			rowPanel.AddChild(rowContent);

			CheckBox box = new CheckBox
			{
				Text = $"{officer?.DisplayName ?? shipName}  |  {officer?.Specialty ?? "Unassigned Specialty"}  |  {shipName}",
				TooltipText = $"{officer?.DisplayName ?? shipName}\nShip: {shipName}\nSpecialty: {officer?.Specialty ?? "Unknown"}\nIdeology: {officer?.Ideology ?? "Unknown"}\nArchetype: {officer?.Archetype ?? "Unknown"}"
			};
			box.ButtonPressed = preferredSelection.Contains(shipName);
			string shipNameLocal = shipName;
			box.Toggled += pressed => OnMissionOfficerToggled(shipNameLocal, pressed);
			rowContent.AddChild(box);
			_missionOfficerCheckboxes[shipName] = box;

			Label loadoutLabel = new Label
			{
				Text = "TACTICAL LOADOUT",
				HorizontalAlignment = HorizontalAlignment.Left
			};
			loadoutLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.86f, 0.98f));
			loadoutLabel.AddThemeFontSizeOverride("font_size", 13);
			rowContent.AddChild(loadoutLabel);

			HBoxContainer loadoutRow = new HBoxContainer();
			loadoutRow.AddThemeConstantOverride("separation", 12);
			rowContent.AddChild(loadoutRow);

			VBoxContainer weaponColumn = new VBoxContainer();
			weaponColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			weaponColumn.AddThemeConstantOverride("separation", 4);
			loadoutRow.AddChild(weaponColumn);

			Label weaponLabel = new Label { Text = "Weapon" };
			weaponLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.96f, 1f));
			weaponColumn.AddChild(weaponLabel);

			OptionButton weaponDropdown = BuildMissionWeaponDropdown(officer, shipName);
			weaponColumn.AddChild(weaponDropdown);

			VBoxContainer shieldColumn = new VBoxContainer();
			shieldColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			shieldColumn.AddThemeConstantOverride("separation", 4);
			loadoutRow.AddChild(shieldColumn);

			Label shieldLabel = new Label { Text = "Shield" };
			shieldLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.96f, 1f));
			shieldColumn.AddChild(shieldLabel);

			OptionButton shieldDropdown = BuildMissionShieldDropdown(officer, shipName);
			shieldColumn.AddChild(shieldDropdown);
		}
		_isUpdatingMissionOfficerSelection = false;

		TrimMissionOfficerSelectionToLimit(requiredSelections);
		UpdateMissionOfficerSelectionStatus();
	}

	private void OnMissionOfficerToggled(string shipName, bool pressed)
	{
		if (_isUpdatingMissionOfficerSelection)
		{
			return;
		}

		List<string> availableShips = _missionOfficerCheckboxes.Keys.ToList();
		int selectionLimit = Mathf.Min(_pendingMissionRecommendedOfficerCount, availableShips.Count);
		if (pressed && GetSelectedMissionOfficerShips().Count > selectionLimit && _missionOfficerCheckboxes.TryGetValue(shipName, out CheckBox box))
		{
			_isUpdatingMissionOfficerSelection = true;
			box.ButtonPressed = false;
			_isUpdatingMissionOfficerSelection = false;
		}

		UpdateMissionOfficerSelectionStatus();
	}

	private void TrimMissionOfficerSelectionToLimit(int selectionLimit)
	{
		List<string> selectedShips = GetSelectedMissionOfficerShips();
		if (selectedShips.Count <= selectionLimit)
		{
			return;
		}

		_isUpdatingMissionOfficerSelection = true;
		foreach (string shipName in selectedShips.Skip(selectionLimit))
		{
			if (_missionOfficerCheckboxes.TryGetValue(shipName, out CheckBox box))
			{
				box.ButtonPressed = false;
			}
		}
		_isUpdatingMissionOfficerSelection = false;
	}

	private List<string> GetSelectedMissionOfficerShips()
	{
		return _missionOfficerCheckboxes
			.Where(pair => pair.Value != null && pair.Value.ButtonPressed)
			.Select(pair => pair.Key)
			.ToList();
	}

	private bool CanLaunchPendingMission(List<string> selectedShips = null)
	{
		List<string> availableShips = _missionOfficerCheckboxes.Keys.ToList();
		List<string> resolvedSelection = selectedShips ?? GetSelectedMissionOfficerShips();
		if (availableShips.Count == 0)
		{
			return false;
		}

		int requiredSelections = Mathf.Min(_pendingMissionRecommendedOfficerCount, availableShips.Count);
		return resolvedSelection.Count == requiredSelections;
	}

	private void UpdateMissionOfficerSelectionStatus()
	{
		if (_missionOfficerSelectionStatus == null)
		{
			return;
		}

		List<string> availableShips = _missionOfficerCheckboxes.Keys.ToList();
		List<string> selectedShips = GetSelectedMissionOfficerShips();
		int requiredSelections = Mathf.Min(_pendingMissionRecommendedOfficerCount, availableShips.Count);

		if (availableShips.Count == 0)
		{
			_missionOfficerSelectionStatus.Text = "No mission-ready officers are currently assigned to the fleet.";
			if (_missionLaunchButton != null)
			{
				_missionLaunchButton.Disabled = true;
			}
			return;
		}

		_missionOfficerSelectionStatus.Text = $"Select {requiredSelections} officer{(requiredSelections == 1 ? string.Empty : "s")} for this away mission and confirm their tactical loadouts. Current selection: {selectedShips.Count}/{requiredSelections}.";
		if (_missionLaunchButton != null)
		{
			_missionLaunchButton.Disabled = !CanLaunchPendingMission(selectedShips);
		}
	}

	private OptionButton BuildMissionWeaponDropdown(OfficerState officer, string shipName)
	{
		OptionButton dropdown = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};

		List<string> ownedWeaponIds = OfficerMissionLoadoutService.GetOwnedWeaponIds(officer).ToList();

		int selectedIndex = 0;
		for (int i = 0; i < ownedWeaponIds.Count; i++)
		{
			MissionWeaponDefinition weapon = MissionEquipmentRegistry.GetWeapon(ownedWeaponIds[i]);
			if (weapon == null)
			{
				continue;
			}

			dropdown.AddItem($"{weapon.DisplayName}  [{weapon.MinDamage}-{weapon.MaxDamage} | R{weapon.AttackRange}]");
			int itemIndex = dropdown.ItemCount - 1;
			dropdown.SetItemMetadata(itemIndex, weapon.WeaponId);
			dropdown.SetItemTooltip(itemIndex, $"{weapon.DisplayName}\n{weapon.Description}\nRange: {weapon.AttackRange}\nDamage: {weapon.MinDamage}-{weapon.MaxDamage}\nShield Break: +{weapon.BonusShieldDamage}\nPiercing: +{weapon.ShieldPiercingDamage}");
			if (weapon.WeaponId == officer?.EquippedMissionWeaponId)
			{
				selectedIndex = itemIndex;
			}
		}

		if (dropdown.ItemCount > 0)
		{
			dropdown.Select(selectedIndex);
		}

		string shipNameLocal = shipName;
		dropdown.ItemSelected += index => OnMissionWeaponSelected(shipNameLocal, dropdown, index);
		return dropdown;
	}

	private OptionButton BuildMissionShieldDropdown(OfficerState officer, string shipName)
	{
		OptionButton dropdown = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};

		List<string> ownedShieldIds = OfficerMissionLoadoutService.GetOwnedShieldIds(officer).ToList();

		int selectedIndex = 0;
		for (int i = 0; i < ownedShieldIds.Count; i++)
		{
			MissionShieldDefinition shield = MissionEquipmentRegistry.GetShield(ownedShieldIds[i]);
			if (shield == null)
			{
				continue;
			}

			dropdown.AddItem($"{shield.DisplayName}  [+{shield.CapacityBonus} SHD | +{shield.RechargePerTurn}/turn]");
			int itemIndex = dropdown.ItemCount - 1;
			dropdown.SetItemMetadata(itemIndex, shield.ShieldId);
			dropdown.SetItemTooltip(itemIndex, $"{shield.DisplayName}\n{shield.Description}\nCapacity Bonus: +{shield.CapacityBonus}\nRecharge: +{shield.RechargePerTurn}/turn");
			if (shield.ShieldId == officer?.EquippedMissionShieldId)
			{
				selectedIndex = itemIndex;
			}
		}

		if (dropdown.ItemCount > 0)
		{
			dropdown.Select(selectedIndex);
		}

		string shipNameLocal = shipName;
		dropdown.ItemSelected += index => OnMissionShieldSelected(shipNameLocal, dropdown, index);
		return dropdown;
	}

	private void OnMissionWeaponSelected(string shipName, OptionButton dropdown, long index)
	{
		OfficerState officer = _officerService?.GetOfficerForShip(shipName);
		if (officer == null || dropdown == null || index < 0 || index >= dropdown.ItemCount)
		{
			return;
		}

		string weaponId = dropdown.GetItemMetadata((int)index).AsString();
		if (!OfficerMissionLoadoutService.EquipWeapon(officer, weaponId))
		{
			return;
		}

		ApplyOfficerApprovalEvent(
			OfficerApprovalEventType.EquipItem,
			new OfficerApprovalContext
			{
				ActingShipName = shipName,
				ItemName = MissionEquipmentRegistry.GetWeapon(weaponId)?.DisplayName ?? weaponId,
				ItemCategory = GameConstants.EquipmentCategories.Weapon
			});
	}

	private void OnMissionShieldSelected(string shipName, OptionButton dropdown, long index)
	{
		OfficerState officer = _officerService?.GetOfficerForShip(shipName);
		if (officer == null || dropdown == null || index < 0 || index >= dropdown.ItemCount)
		{
			return;
		}

		string shieldId = dropdown.GetItemMetadata((int)index).AsString();
		if (!OfficerMissionLoadoutService.EquipShield(officer, shieldId))
		{
			return;
		}

		ApplyOfficerApprovalEvent(
			OfficerApprovalEventType.EquipItem,
			new OfficerApprovalContext
			{
				ActingShipName = shipName,
				ItemName = MissionEquipmentRegistry.GetShield(shieldId)?.DisplayName ?? shieldId,
				ItemCategory = GameConstants.EquipmentCategories.Shield
			});
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
			SaveCampaign(true); // Auto-save after a purchase
			
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
		SaveCampaign(true);

		OpenShop();
	}

	private void SellInventoryItem(string itemID, string itemName, int rawValue)
	{
		if (_inventoryService == null || !_inventoryService.SellInventoryItem(itemID, rawValue)) return;

		UpdateResourceUI();
		SaveCampaign(true);

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

		PanelContainer equipPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(1240f, 780f)
		};
		equipPanel.AddThemeStyleboxOverride("panel", CreateInventoryWindowStyle());
		_equipMenuWrapper.AddChild(equipPanel);

		MarginContainer outerMargin = new MarginContainer();
		outerMargin.AddThemeConstantOverride("margin_left", 20);
		outerMargin.AddThemeConstantOverride("margin_top", 20);
		outerMargin.AddThemeConstantOverride("margin_right", 20);
		outerMargin.AddThemeConstantOverride("margin_bottom", 20);
		equipPanel.AddChild(outerMargin);

		VBoxContainer layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 16);
		outerMargin.AddChild(layout);

		HBoxContainer headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 14);
		layout.AddChild(headerRow);

		VBoxContainer rail = new VBoxContainer();
		rail.CustomMinimumSize = new Vector2(76f, 0f);
		rail.AddThemeConstantOverride("separation", 10);
		headerRow.AddChild(rail);
		rail.AddChild(BuildInventoryRailTag("LOADOUT", true));
		rail.AddChild(BuildInventoryRailTag("BATTLE", false));
		rail.AddChild(BuildInventoryRailTag("CARGO", false));

		VBoxContainer mainColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		mainColumn.AddThemeConstantOverride("separation", 14);
		headerRow.AddChild(mainColumn);

		HBoxContainer titleRow = new HBoxContainer();
		titleRow.Alignment = BoxContainer.AlignmentMode.Center;
		titleRow.AddThemeConstantOverride("separation", 12);
		mainColumn.AddChild(titleRow);

		Control leftSpacer = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		titleRow.AddChild(leftSpacer);

		PanelContainer titlePlaque = new PanelContainer
		{
			CustomMinimumSize = new Vector2(520f, 74f)
		};
		titlePlaque.AddThemeStyleboxOverride("panel", CreateInventoryBannerStyle());
		titleRow.AddChild(titlePlaque);

		Label titleLabel = new Label
		{
			Text = "FLEET LOADOUT",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		titleLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		titleLabel.AddThemeFontSizeOverride("font_size", 34);
		titleLabel.AddThemeColorOverride("font_color", new Color(0.93f, 0.90f, 0.84f));
		titlePlaque.AddChild(titleLabel);

		Control rightSpacer = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		titleRow.AddChild(rightSpacer);

		Button closeBtn = new Button
		{
			Text = "CLOSE",
			CustomMinimumSize = new Vector2(120f, 44f)
		};
		closeBtn.AddThemeStyleboxOverride("normal", CreateInventoryButtonStyle());
		closeBtn.AddThemeStyleboxOverride("hover", CreateInventoryButtonStyle(true));
		closeBtn.AddThemeStyleboxOverride("pressed", CreateInventoryButtonStyle(true));
		closeBtn.Pressed += () => _equipMenuWrapper.Visible = false;
		titleRow.AddChild(closeBtn);

		PanelContainer identityPanel = new PanelContainer();
		identityPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle(true));
		mainColumn.AddChild(identityPanel);

		MarginContainer identityMargin = new MarginContainer();
		identityMargin.AddThemeConstantOverride("margin_left", 18);
		identityMargin.AddThemeConstantOverride("margin_top", 14);
		identityMargin.AddThemeConstantOverride("margin_right", 18);
		identityMargin.AddThemeConstantOverride("margin_bottom", 14);
		identityPanel.AddChild(identityMargin);

		VBoxContainer identityContent = new VBoxContainer();
		identityContent.AddThemeConstantOverride("separation", 6);
		identityMargin.AddChild(identityContent);

		_equipShipNameLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_equipShipNameLabel.AddThemeFontSizeOverride("font_size", 30);
		identityContent.AddChild(_equipShipNameLabel);

		_equipShipRoleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_equipShipRoleLabel.AddThemeFontSizeOverride("font_size", 16);
		_equipShipRoleLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.78f, 0.62f));
		identityContent.AddChild(_equipShipRoleLabel);

		HBoxContainer bodyRow = new HBoxContainer();
		bodyRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		bodyRow.AddThemeConstantOverride("separation", 14);
		mainColumn.AddChild(bodyRow);

		PanelContainer portraitPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(280f, 0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		portraitPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		bodyRow.AddChild(portraitPanel);

		MarginContainer portraitMargin = new MarginContainer();
		portraitMargin.AddThemeConstantOverride("margin_left", 14);
		portraitMargin.AddThemeConstantOverride("margin_top", 14);
		portraitMargin.AddThemeConstantOverride("margin_right", 14);
		portraitMargin.AddThemeConstantOverride("margin_bottom", 14);
		portraitPanel.AddChild(portraitMargin);

		VBoxContainer portraitContent = new VBoxContainer();
		portraitContent.AddThemeConstantOverride("separation", 12);
		portraitMargin.AddChild(portraitContent);

		_equipShipPortrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(0f, 240f),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
		};
		portraitContent.AddChild(_equipShipPortrait);

		_equipShipStatsLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_equipShipStatsLabel.AddThemeFontSizeOverride("font_size", 15);
		portraitContent.AddChild(_equipShipStatsLabel);

		VBoxContainer centerColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		centerColumn.AddThemeConstantOverride("separation", 14);
		bodyRow.AddChild(centerColumn);

		PanelContainer summaryPanel = new PanelContainer();
		summaryPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		centerColumn.AddChild(summaryPanel);

		MarginContainer summaryMargin = new MarginContainer();
		summaryMargin.AddThemeConstantOverride("margin_left", 14);
		summaryMargin.AddThemeConstantOverride("margin_top", 14);
		summaryMargin.AddThemeConstantOverride("margin_right", 14);
		summaryMargin.AddThemeConstantOverride("margin_bottom", 14);
		summaryPanel.AddChild(summaryMargin);

		VBoxContainer summaryContent = new VBoxContainer();
		summaryContent.AddThemeConstantOverride("separation", 10);
		summaryMargin.AddChild(summaryContent);
		summaryContent.AddChild(BuildInventorySectionHeader("TACTICAL DOSSIER"));

		_equipSummaryLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_equipSummaryLabel.AddThemeFontSizeOverride("font_size", 15);
		summaryContent.AddChild(_equipSummaryLabel);

		PanelContainer loadoutPanel = BuildInventorySectionCard("ACTIVE GEAR", out _equipActiveLoadoutStack);
		loadoutPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		centerColumn.AddChild(loadoutPanel);

		VBoxContainer rightColumn = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(360f, 0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		rightColumn.AddThemeConstantOverride("separation", 14);
		bodyRow.AddChild(rightColumn);

		rightColumn.AddChild(BuildInventorySectionCard("WEAPON LOCKER", out _equipWeaponStack));
		rightColumn.AddChild(BuildInventorySectionCard("SHIELD ARRAY", out _equipShieldStack));
		rightColumn.AddChild(BuildInventorySectionCard("ARMOR BAY", out _equipArmorStack));
		rightColumn.AddChild(BuildInventorySectionCard("MISSILE HOLD", out _equipMissileStack));

		PanelContainer cargoPanel = new PanelContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		cargoPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		rightColumn.AddChild(cargoPanel);

		MarginContainer cargoMargin = new MarginContainer();
		cargoMargin.AddThemeConstantOverride("margin_left", 14);
		cargoMargin.AddThemeConstantOverride("margin_top", 14);
		cargoMargin.AddThemeConstantOverride("margin_right", 14);
		cargoMargin.AddThemeConstantOverride("margin_bottom", 14);
		cargoPanel.AddChild(cargoMargin);

		VBoxContainer cargoContent = new VBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		cargoContent.AddThemeConstantOverride("separation", 10);
		cargoMargin.AddChild(cargoContent);
		cargoContent.AddChild(BuildInventorySectionHeader("CARGO HOLD"));

		_equipItemList = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		_equipItemList.AddThemeConstantOverride("separation", 8);
		cargoContent.AddChild(_equipItemList);

		_equipFooterLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_equipFooterLabel.AddThemeFontSizeOverride("font_size", 14);
		_equipFooterLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.76f, 0.65f));
		mainColumn.AddChild(_equipFooterLabel);
	}

	private void OpenEquipMenu()
	{
		if (_inventoryService == null || CurrentlyViewedShip == null) return;
		string shipName = CurrentlyViewedShip.Name;

		ShipLoadout loadout = _inventoryService.GetOrCreateLoadout(shipName);
		if (loadout == null) return;
		PopulateFleetLoadoutPanel(shipName, loadout, _inventoryService.GetGroupedEquippableInventory());
		_equipMenuWrapper.Visible = true;
	}

	private string GetCurrentTradeOutpostName()
	{
		MapEntity outpost = CurrentlyViewedShip == null ? null : _shipContextService?.GetAdjacentOutpost(CurrentlyViewedShip, HexContents);
		return string.IsNullOrEmpty(outpost?.Name) ? "Nearest Outpost" : outpost.Name;
	}

	private void PopulateFleetLoadoutPanel(string shipName, ShipLoadout loadout, IReadOnlyList<InventoryStack> inventoryStacks)
	{
		if (_equipShipNameLabel == null
			|| _equipShipRoleLabel == null
			|| _equipShipStatsLabel == null
			|| _equipSummaryLabel == null
			|| _equipFooterLabel == null)
		{
			return;
		}

		MapEntity ship = CurrentlyViewedShip;
		OfficerState assignedOfficer = ResolveOfficerStateForShip(shipName);
		string weaponName = _inventoryService?.GetActiveWeaponName(shipName) ?? FleetInventoryService.DefaultWeaponName;
		string shieldName = _inventoryService?.GetActiveShieldName(shipName) ?? FleetInventoryService.DefaultShieldName;
		string armorName = _inventoryService?.GetActiveHullName(shipName) ?? FleetInventoryService.DefaultHullName;
		string missileName = _inventoryService?.GetActiveMissileName(shipName) ?? FleetInventoryService.DefaultMissileName;

		_equipShipNameLabel.Text = shipName.ToUpperInvariant();
		_equipShipRoleLabel.Text = $"{(assignedOfficer?.DisplayName ?? "UNASSIGNED OFFICER").ToUpperInvariant()}  |  {(assignedOfficer?.Specialty ?? "NO SPECIALTY").ToUpperInvariant()}";
		_equipShipPortrait.Texture = LoadPortraitTexture(Database.GetShipTexturePath(shipName));
		_equipShipStatsLabel.Text = BuildFleetLoadoutStatsText(ship, assignedOfficer);
		_equipSummaryLabel.Text = BuildFleetLoadoutSummaryText(shipName, assignedOfficer, ship);

		List<MissionInventoryEntry> activeEntries = new List<MissionInventoryEntry>
		{
			BuildFleetActiveLoadoutEntry(GameConstants.EquipmentCategories.Weapon, weaponName, loadout?.WeaponID),
			BuildFleetActiveLoadoutEntry(GameConstants.EquipmentCategories.Shield, shieldName, loadout?.ShieldID),
			BuildFleetActiveLoadoutEntry(GameConstants.EquipmentCategories.Armor, armorName, loadout?.ArmorID),
			BuildFleetActiveLoadoutEntry(GameConstants.EquipmentCategories.Missile, missileName, loadout?.MissileID)
		};

		List<InventoryStack> stacks = (inventoryStacks ?? Array.Empty<InventoryStack>()).ToList();
		List<MissionInventoryEntry> weaponEntries = BuildFleetLockerEntries(stacks, GameConstants.EquipmentCategories.Weapon, loadout?.WeaponID, FleetInventoryService.DefaultWeaponName);
		List<MissionInventoryEntry> shieldEntries = BuildFleetLockerEntries(stacks, GameConstants.EquipmentCategories.Shield, loadout?.ShieldID, FleetInventoryService.DefaultShieldName);
		List<MissionInventoryEntry> armorEntries = BuildFleetLockerEntries(stacks, GameConstants.EquipmentCategories.Armor, loadout?.ArmorID, FleetInventoryService.DefaultHullName);
		List<MissionInventoryEntry> missileEntries = BuildFleetLockerEntries(stacks, GameConstants.EquipmentCategories.Missile, loadout?.MissileID, FleetInventoryService.DefaultMissileName);
		List<MissionInventoryEntry> cargoEntries = BuildFleetCargoEntries(stacks);

		PopulateInventoryEntryStack(_equipActiveLoadoutStack, activeEntries, "No active fleet gear.");
		PopulateInteractiveInventoryEntryStack(_equipWeaponStack, weaponEntries, "No reserve weapons in cargo.", itemId => EquipItem(shipName, itemId));
		PopulateInteractiveInventoryEntryStack(_equipShieldStack, shieldEntries, "No reserve shields in cargo.", itemId => EquipItem(shipName, itemId));
		PopulateInteractiveInventoryEntryStack(_equipArmorStack, armorEntries, "No spare armor plating in cargo.", itemId => EquipItem(shipName, itemId));
		PopulateInteractiveInventoryEntryStack(_equipMissileStack, missileEntries, "No missile payloads in cargo.", itemId => EquipItem(shipName, itemId));
		PopulateInventoryItemStack(_equipItemList, cargoEntries, "No unequipped fleet gear in cargo.");

		int cargoCount = stacks.Sum(stack => Mathf.Max(0, stack.Count));
		_equipFooterLabel.Text = cargoCount == 0
			? "Cargo hold is clear. Acquire more upgrades from salvage or trade."
			: $"Cargo hold contains {cargoCount} unequipped upgrade{(cargoCount == 1 ? string.Empty : "s")}. Click a locker entry to refit this ship.";
	}

	private static string BuildFleetLoadoutStatsText(MapEntity ship, OfficerState officer)
	{
		if (ship == null)
		{
			return "No tactical data available.";
		}

		List<string> lines = new List<string>
		{
			$"HULL      {ship.CurrentHP}/{ship.MaxHP}",
			$"SHIELDS   {ship.CurrentShields}/{ship.MaxShields}",
			$"ACTIONS   {ship.CurrentActions}/{ship.MaxActions}",
			$"RANGE     {ship.AttackRange}",
			$"DAMAGE    0-{ship.AttackDamage}",
			$"OFFICER   {(string.IsNullOrWhiteSpace(officer?.DisplayName) ? "None" : officer.DisplayName)}"
		};

		if (!string.IsNullOrWhiteSpace(officer?.Specialty))
		{
			lines.Add($"SPECIALTY {officer.Specialty}");
		}

		return string.Join("\n", lines);
	}

	private static string BuildFleetLoadoutSummaryText(string shipName, OfficerState officer, MapEntity ship)
	{
		List<string> notes = new List<string>
		{
			$"{shipName} is ready for refit. Assign cargo upgrades into its primary combat slots to reshape survivability and strike profile."
		};

		if (officer != null)
		{
			notes.Add($"{officer.DisplayName} currently commands this hull as a {officer.Specialty} specialist.");
			if (!string.IsNullOrWhiteSpace(officer.Biography))
			{
				notes.Add(officer.Biography.Trim());
			}
		}
		else
		{
			notes.Add("No officer is currently assigned to this ship.");
		}

		if (ship != null)
		{
			notes.Add($"Current tactical envelope: {ship.AttackRange} hex range with up to {ship.AttackDamage} damage output.");
		}

		return string.Join("\n\n", notes.Where(note => !string.IsNullOrWhiteSpace(note)));
	}

	private MissionInventoryEntry BuildFleetActiveLoadoutEntry(string category, string displayName, string equippedItemId)
	{
		EquipmentData equipment = _inventoryService?.GetEquipment(equippedItemId);
		return new MissionInventoryEntry
		{
			Title = string.IsNullOrWhiteSpace(displayName) ? category : displayName,
			Detail = BuildFleetEquipmentDetailText(category, equipment, true),
			Highlighted = true
		};
	}

	private List<MissionInventoryEntry> BuildFleetLockerEntries(
		IReadOnlyList<InventoryStack> inventoryStacks,
		string category,
		string equippedItemId,
		string defaultName)
	{
		List<MissionInventoryEntry> entries = new List<MissionInventoryEntry>();
		EquipmentData equippedItem = _inventoryService?.GetEquipment(equippedItemId);
		entries.Add(new MissionInventoryEntry
		{
			Title = !string.IsNullOrWhiteSpace(equippedItem?.Name) ? equippedItem.Name : defaultName,
			Detail = BuildFleetEquipmentDetailText(category, equippedItem, true),
			Highlighted = true,
			CanActivate = false,
			ActionText = "EQUIPPED"
		});

		foreach (InventoryStack stack in (inventoryStacks ?? Array.Empty<InventoryStack>())
			.Where(stack => stack?.Item != null && string.Equals(stack.Item.Category, category, StringComparison.Ordinal))
			.OrderBy(stack => stack.Item.Name, StringComparer.OrdinalIgnoreCase))
		{
			entries.Add(new MissionInventoryEntry
			{
				EntryId = stack.ItemID,
				Title = stack.Item.Name,
				Detail = BuildFleetEquipmentDetailText(category, stack.Item),
				QuantityText = stack.Count > 1 ? $"x{stack.Count}" : string.Empty,
				CanActivate = true,
				ActionText = "EQUIP"
			});
		}

		return entries;
	}

	private List<MissionInventoryEntry> BuildFleetCargoEntries(IReadOnlyList<InventoryStack> inventoryStacks)
	{
		return (inventoryStacks ?? Array.Empty<InventoryStack>())
			.Where(stack => stack?.Item != null)
			.OrderBy(stack => stack.Item.Category, StringComparer.OrdinalIgnoreCase)
			.ThenBy(stack => stack.Item.Name, StringComparer.OrdinalIgnoreCase)
			.Select(stack => new MissionInventoryEntry
			{
				Title = stack.Item.Name,
				Detail = $"{stack.Item.Category} | {BuildFleetEquipmentDetailText(stack.Item.Category, stack.Item)}",
				QuantityText = stack.Count > 1 ? $"x{stack.Count}" : string.Empty
			})
			.ToList();
	}

	private void PopulateInventoryItemStack(VBoxContainer container, IReadOnlyList<MissionInventoryEntry> entries, string emptyText)
	{
		if (container == null)
		{
			return;
		}

		ClearContainerChildren(container);
		int visibleCount = 0;
		foreach (MissionInventoryEntry entry in entries ?? Array.Empty<MissionInventoryEntry>())
		{
			if (entry == null)
			{
				continue;
			}

			container.AddChild(BuildInventoryItemCell(entry));
			visibleCount++;
		}

		if (visibleCount == 0)
		{
			container.AddChild(BuildInventoryPlaceholder(emptyText));
			return;
		}

		for (int slotIndex = visibleCount; slotIndex < 4; slotIndex++)
		{
			container.AddChild(BuildInventoryEmptyCell());
		}
	}

	private static string BuildFleetEquipmentDetailText(string category, EquipmentData equipment, bool activeSlot = false)
	{
		if (equipment == null)
		{
			return category switch
			{
				GameConstants.EquipmentCategories.Weapon => activeSlot ? "Standard issue emitter. No bonus damage package installed." : "Standard issue emitter.",
				GameConstants.EquipmentCategories.Shield => activeSlot ? "Standard shield lattice. No bonus capacity installed." : "Standard shield lattice.",
				GameConstants.EquipmentCategories.Armor => activeSlot ? "Standard hull plating. No armor bonus installed." : "Standard hull plating.",
				GameConstants.EquipmentCategories.Missile => "No missile payload equipped.",
				_ => "No equipment telemetry available."
			};
		}

		List<string> parts = new List<string>();
		switch (category)
		{
			case GameConstants.EquipmentCategories.Weapon:
				parts.Add($"+{equipment.BonusStat} dmg");
				break;
			case GameConstants.EquipmentCategories.Shield:
				parts.Add($"+{equipment.BonusStat} shields");
				break;
			case GameConstants.EquipmentCategories.Armor:
				parts.Add($"+{equipment.BonusStat} hull");
				break;
			case GameConstants.EquipmentCategories.Missile:
				parts.Add($"DMG {equipment.MissileDamage}");
				parts.Add($"Range {equipment.MissileRange}");
				if (!string.IsNullOrWhiteSpace(equipment.MissileAbility))
				{
					parts.Add(equipment.MissileAbility.Replace('_', ' '));
				}
				break;
		}

		if (!string.IsNullOrWhiteSpace(equipment.Description))
		{
			parts.Add(equipment.Description);
		}

		return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
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
		SaveCampaign(true); // Save state

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
		if (_replacementPromptWrapper != null && _replacementPromptWrapper.Visible)
		{
			if (@event is InputEventKey blockedKey && blockedKey.Pressed && !blockedKey.Echo && blockedKey.Keycode == Key.Escape)
			{
				HideOfficerReplacementPrompt();
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (_officerSwapMenuWrapper != null && _officerSwapMenuWrapper.Visible)
		{
			if (@event is InputEventKey swapMenuEscape && swapMenuEscape.Pressed && !swapMenuEscape.Echo && swapMenuEscape.Keycode == Key.Escape)
			{
				HideOfficerSwapMenu();
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (_officerInventoryOverlay != null && _officerInventoryOverlay.Visible)
		{
			if (@event is InputEventKey inventoryEscape && inventoryEscape.Pressed && !inventoryEscape.Echo && inventoryEscape.Keycode == Key.Escape)
			{
				HideOfficerInventory();
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (_equipMenuWrapper != null && _equipMenuWrapper.Visible)
		{
			if (@event is InputEventKey equipEscape && equipEscape.Pressed && !equipEscape.Echo && equipEscape.Keycode == Key.Escape)
			{
				_equipMenuWrapper.Visible = false;
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (@event is InputEventKey escapeEvent && escapeEvent.Pressed && !escapeEvent.Echo && escapeEvent.Keycode == Key.Escape)
		{
			TogglePauseMenu();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_pauseMenuWrapper != null && _pauseMenuWrapper.Visible) return;
		if (_loadMenuWrapper != null && _loadMenuWrapper.Visible) return;
		if (_savePromptWrapper != null && _savePromptWrapper.Visible) return;
		if (_replacementPromptWrapper != null && _replacementPromptWrapper.Visible) return;
		if (_officerSwapMenuWrapper != null && _officerSwapMenuWrapper.Visible) return;
		if (_strandedMenuWrapper != null && _strandedMenuWrapper.Visible) return;
		if (_shopMenuWrapper != null && _shopMenuWrapper.Visible) return; 
		if (_equipMenuWrapper != null && _equipMenuWrapper.Visible) return; // Prevent movement while equipping

		if (@event is InputEventKey saveKeyEvent && saveKeyEvent.Pressed && !saveKeyEvent.Echo)
		{
			if (saveKeyEvent.Keycode == Key.F5)
			{
				QuickSaveCampaign();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (saveKeyEvent.Keycode == Key.F6)
			{
				QuickLoadCampaign();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

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

		if (@event is InputEventMouseButton officerPortraitMouseEvent
			&& officerPortraitMouseEvent.Pressed
			&& _officerPortraitDisplay != null
			&& _officerPortraitDisplay.Visible
			&& _officerPortraitDisplay.GetGlobalRect().HasPoint(GetViewport().GetMousePosition()))
		{
			if (officerPortraitMouseEvent.ButtonIndex == MouseButton.Right)
			{
				OpenViewedShipOfficerInventory();
			}

			GetViewport().SetInputAsHandled();
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
		if (!expand)
		{
			HideOfficerInventory();
			HideOfficerSwapMenu();
			if (_equipMenuWrapper != null)
			{
				_equipMenuWrapper.Visible = false;
			}
		}
		Tween tween = CreateTween();
		float targetX = expand ? GetExpandedShipMenuX() : GetCollapsedShipMenuX();
		tween.TweenProperty(UI.ShipMenuPanel, "position:x", targetX, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		ToggleOfficerPanel(expand, ship);
		
		_shipMenuPresenterService?.ResetMenu(UI);
		
		if (_btnTrade != null) _btnTrade.Visible = false;
		if (_btnEquip != null) _btnEquip.Visible = false; // --- NEW: Hide equip by default ---
		if (_btnMission != null) _btnMission.Visible = false;
		if (_btnOfficerSwap != null) _btnOfficerSwap.Visible = false;

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

			if (menuState.ShowMission && _btnMission != null)
			{
				_btnMission.Text = string.IsNullOrEmpty(menuState.MissionText) ? "BLACK SITE MISSION" : menuState.MissionText;
				_btnMission.Visible = true;
			}

			if (ship.Type == GameConstants.EntityTypes.PlayerFleet && _btnOfficerSwap != null)
			{
				_btnOfficerSwap.Visible = true;
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
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Stop,
			TooltipText = "Right-click to inspect the captain's inventory."
		};
		_officerPortraitDisplay.GuiInput += OnOfficerPortraitGuiInput;
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

	private sealed class OfficerInventoryCombatProfile
	{
		public int MaxHp { get; set; } = 14;
		public int MaxShields { get; set; } = 5;
		public int MaxActions { get; set; } = 2;
		public int AttackMinDamage { get; set; } = 2;
		public int AttackRange { get; set; } = 3;
		public int AttackDamage { get; set; } = 4;
		public int InitiativeBonus { get; set; } = 1;
		public int BonusShieldDamage { get; set; }
		public int ShieldPiercingDamage { get; set; }
		public int ShieldRechargePerTurn { get; set; } = 1;
		public bool UsesMeleeWeapon { get; set; }
		public string WeaponName { get; set; } = "Sidearm";
		public string ShieldName { get; set; } = "Field Aegis";
		public string CombatAbilityId { get; set; } = string.Empty;
		public string WeaponStatusEffectId { get; set; } = string.Empty;
		public float WeaponStatusEffectChance { get; set; }
	}

	private void BuildOfficerInventoryPanel()
	{
		if (UI == null)
		{
			return;
		}

		Control uiRoot = UI.GetNodeOrNull<Control>("UIRoot");
		if (uiRoot == null)
		{
			return;
		}

		_officerInventoryOverlay = new ColorRect
		{
			Visible = false,
			Color = new Color(0.04f, 0.02f, 0.01f, 0.80f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_officerInventoryOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_officerInventoryOverlay.GuiInput += OnOfficerInventoryOverlayGuiInput;
		uiRoot.AddChild(_officerInventoryOverlay);

		_officerInventoryPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(1180f, 760f)
		};
		_officerInventoryPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_officerInventoryPanel.OffsetLeft = -590f;
		_officerInventoryPanel.OffsetTop = -380f;
		_officerInventoryPanel.OffsetRight = 590f;
		_officerInventoryPanel.OffsetBottom = 380f;
		_officerInventoryPanel.AddThemeStyleboxOverride("panel", CreateInventoryWindowStyle());
		_officerInventoryOverlay.AddChild(_officerInventoryPanel);

		MarginContainer outerMargin = new MarginContainer();
		outerMargin.AddThemeConstantOverride("margin_left", 20);
		outerMargin.AddThemeConstantOverride("margin_top", 20);
		outerMargin.AddThemeConstantOverride("margin_right", 20);
		outerMargin.AddThemeConstantOverride("margin_bottom", 20);
		_officerInventoryPanel.AddChild(outerMargin);

		VBoxContainer layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 16);
		outerMargin.AddChild(layout);

		HBoxContainer headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 14);
		layout.AddChild(headerRow);

		VBoxContainer rail = new VBoxContainer();
		rail.CustomMinimumSize = new Vector2(76f, 0f);
		rail.AddThemeConstantOverride("separation", 10);
		headerRow.AddChild(rail);
		rail.AddChild(BuildInventoryRailTag("LOADOUT", true));
		rail.AddChild(BuildInventoryRailTag("STATUS", false));
		rail.AddChild(BuildInventoryRailTag("PACK", false));

		VBoxContainer mainColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		mainColumn.AddThemeConstantOverride("separation", 14);
		headerRow.AddChild(mainColumn);

		HBoxContainer titleRow = new HBoxContainer();
		titleRow.Alignment = BoxContainer.AlignmentMode.Center;
		titleRow.AddThemeConstantOverride("separation", 12);
		mainColumn.AddChild(titleRow);

		Control leftSpacer = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		titleRow.AddChild(leftSpacer);

		PanelContainer titlePlaque = new PanelContainer
		{
			CustomMinimumSize = new Vector2(460f, 74f)
		};
		titlePlaque.AddThemeStyleboxOverride("panel", CreateInventoryBannerStyle());
		titleRow.AddChild(titlePlaque);

		Label titleLabel = new Label
		{
			Text = "INVENTORY",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		titleLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		titleLabel.AddThemeFontSizeOverride("font_size", 34);
		titleLabel.AddThemeColorOverride("font_color", new Color(0.93f, 0.90f, 0.84f));
		titlePlaque.AddChild(titleLabel);

		Control rightSpacer = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		titleRow.AddChild(rightSpacer);

		_officerInventoryCloseButton = new Button
		{
			Text = "CLOSE",
			CustomMinimumSize = new Vector2(120f, 44f)
		};
		_officerInventoryCloseButton.AddThemeStyleboxOverride("normal", CreateInventoryButtonStyle());
		_officerInventoryCloseButton.AddThemeStyleboxOverride("hover", CreateInventoryButtonStyle(true));
		_officerInventoryCloseButton.AddThemeStyleboxOverride("pressed", CreateInventoryButtonStyle(true));
		_officerInventoryCloseButton.Pressed += HideOfficerInventory;
		titleRow.AddChild(_officerInventoryCloseButton);

		PanelContainer identityPanel = new PanelContainer();
		identityPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle(true));
		mainColumn.AddChild(identityPanel);

		MarginContainer identityMargin = new MarginContainer();
		identityMargin.AddThemeConstantOverride("margin_left", 18);
		identityMargin.AddThemeConstantOverride("margin_top", 14);
		identityMargin.AddThemeConstantOverride("margin_right", 18);
		identityMargin.AddThemeConstantOverride("margin_bottom", 14);
		identityPanel.AddChild(identityMargin);

		VBoxContainer identityContent = new VBoxContainer();
		identityContent.AddThemeConstantOverride("separation", 6);
		identityMargin.AddChild(identityContent);

		_officerInventoryNameLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_officerInventoryNameLabel.AddThemeFontSizeOverride("font_size", 30);
		identityContent.AddChild(_officerInventoryNameLabel);

		_officerInventoryRoleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_officerInventoryRoleLabel.AddThemeFontSizeOverride("font_size", 16);
		_officerInventoryRoleLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.78f, 0.62f));
		identityContent.AddChild(_officerInventoryRoleLabel);

		HBoxContainer bodyRow = new HBoxContainer();
		bodyRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		bodyRow.AddThemeConstantOverride("separation", 14);
		mainColumn.AddChild(bodyRow);

		PanelContainer portraitPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(270f, 0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		portraitPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		bodyRow.AddChild(portraitPanel);

		MarginContainer portraitMargin = new MarginContainer();
		portraitMargin.AddThemeConstantOverride("margin_left", 14);
		portraitMargin.AddThemeConstantOverride("margin_top", 14);
		portraitMargin.AddThemeConstantOverride("margin_right", 14);
		portraitMargin.AddThemeConstantOverride("margin_bottom", 14);
		portraitPanel.AddChild(portraitMargin);

		VBoxContainer portraitContent = new VBoxContainer();
		portraitContent.AddThemeConstantOverride("separation", 12);
		portraitMargin.AddChild(portraitContent);

		_officerInventoryPortrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(0f, 250f),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
		};
		portraitContent.AddChild(_officerInventoryPortrait);

		_officerInventoryStatsLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_officerInventoryStatsLabel.AddThemeFontSizeOverride("font_size", 15);
		portraitContent.AddChild(_officerInventoryStatsLabel);

		VBoxContainer centerColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		centerColumn.AddThemeConstantOverride("separation", 14);
		bodyRow.AddChild(centerColumn);

		PanelContainer summaryPanel = new PanelContainer();
		summaryPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		centerColumn.AddChild(summaryPanel);

		MarginContainer summaryMargin = new MarginContainer();
		summaryMargin.AddThemeConstantOverride("margin_left", 14);
		summaryMargin.AddThemeConstantOverride("margin_top", 14);
		summaryMargin.AddThemeConstantOverride("margin_right", 14);
		summaryMargin.AddThemeConstantOverride("margin_bottom", 14);
		summaryPanel.AddChild(summaryMargin);

		VBoxContainer summaryContent = new VBoxContainer();
		summaryContent.AddThemeConstantOverride("separation", 10);
		summaryMargin.AddChild(summaryContent);
		summaryContent.AddChild(BuildInventorySectionHeader("OPERATIVE DOSSIER"));

		_officerInventorySummaryLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_officerInventorySummaryLabel.AddThemeFontSizeOverride("font_size", 15);
		summaryContent.AddChild(_officerInventorySummaryLabel);

		PanelContainer loadoutPanel = BuildInventorySectionCard("ACTIVE GEAR", out _officerInventoryLoadoutStack);
		loadoutPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		centerColumn.AddChild(loadoutPanel);

		VBoxContainer rightColumn = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(360f, 0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		rightColumn.AddThemeConstantOverride("separation", 14);
		bodyRow.AddChild(rightColumn);

		rightColumn.AddChild(BuildInventorySectionCard("WEAPONS LOCKER", out _officerInventoryWeaponStack));
		rightColumn.AddChild(BuildInventorySectionCard("SHIELD RACK", out _officerInventoryShieldStack));

		PanelContainer itemPanel = new PanelContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		itemPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		rightColumn.AddChild(itemPanel);

		MarginContainer itemMargin = new MarginContainer();
		itemMargin.AddThemeConstantOverride("margin_left", 14);
		itemMargin.AddThemeConstantOverride("margin_top", 14);
		itemMargin.AddThemeConstantOverride("margin_right", 14);
		itemMargin.AddThemeConstantOverride("margin_bottom", 14);
		itemPanel.AddChild(itemMargin);

		VBoxContainer itemContent = new VBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		itemContent.AddThemeConstantOverride("separation", 10);
		itemMargin.AddChild(itemContent);
		itemContent.AddChild(BuildInventorySectionHeader("FIELD PACK"));

		_officerInventoryItemStack = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		_officerInventoryItemStack.AddThemeConstantOverride("separation", 8);
		itemContent.AddChild(_officerInventoryItemStack);

		_officerInventoryFooterLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_officerInventoryFooterLabel.AddThemeFontSizeOverride("font_size", 14);
		_officerInventoryFooterLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.76f, 0.65f));
		mainColumn.AddChild(_officerInventoryFooterLabel);
	}

	private void OnOfficerPortraitGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
		{
			return;
		}

		_officerPortraitDisplay?.AcceptEvent();
		GetViewport().SetInputAsHandled();
	}

	private void OpenViewedShipOfficerInventory()
	{
		if (CurrentlyViewedShip == null
			|| CurrentlyViewedShip.Type != GameConstants.EntityTypes.PlayerFleet)
		{
			return;
		}

		OfficerState officerState = ResolveOfficerStateForShip(CurrentlyViewedShip.Name);
		MissionOfficerInventoryPanelData data = BuildOfficerInventoryPanelData(CurrentlyViewedShip.Name, officerState);
		if (data != null)
		{
			ShowOfficerInventory(data);
		}
	}

	private OfficerState ResolveOfficerStateForShip(string shipName)
	{
		if (_globalData?.ShipOfficers == null || string.IsNullOrWhiteSpace(shipName))
		{
			return null;
		}

		return _globalData.ShipOfficers.TryGetValue(shipName, out OfficerState officerState) ? officerState : null;
	}

	private void ShowOfficerInventory(MissionOfficerInventoryPanelData data)
	{
		if (_officerInventoryOverlay == null
			|| _officerInventoryPortrait == null
			|| _officerInventoryNameLabel == null
			|| _officerInventoryRoleLabel == null
			|| _officerInventoryStatsLabel == null
			|| _officerInventorySummaryLabel == null
			|| _officerInventoryFooterLabel == null
			|| data == null)
		{
			return;
		}

		_officerInventoryPortrait.Texture = data.Portrait;
		_activeOfficerInventoryShipName = data.OfficerId ?? string.Empty;
		_officerInventoryNameLabel.Text = string.IsNullOrWhiteSpace(data.DisplayName)
			? "FLEET OFFICER"
			: data.DisplayName.ToUpperInvariant();
		_officerInventoryRoleLabel.Text = $"{(data.Specialty ?? string.Empty).ToUpperInvariant()}  |  {(data.ShipName ?? string.Empty).ToUpperInvariant()}";
		_officerInventoryStatsLabel.Text = data.VitalStatsText ?? string.Empty;
		_officerInventorySummaryLabel.Text = data.SummaryText ?? string.Empty;
		_officerInventoryFooterLabel.Text = data.FooterText ?? "Right-click another captain portrait to inspect a different officer.";

		PopulateInventoryEntryStack(_officerInventoryLoadoutStack, data.LoadoutEntries, "No active loadout data.");
		PopulateInteractiveInventoryEntryStack(
			_officerInventoryWeaponStack,
			data.WeaponEntries,
			"No owned weapons.",
			weaponId => OnOfficerInventoryWeaponEquipRequested(_activeOfficerInventoryShipName, weaponId));
		PopulateInteractiveInventoryEntryStack(
			_officerInventoryShieldStack,
			data.ShieldEntries,
			"No owned shields.",
			shieldId => OnOfficerInventoryShieldEquipRequested(_activeOfficerInventoryShipName, shieldId));
		PopulateInventoryItemStack(data.ItemEntries);
		_officerInventoryOverlay.Visible = true;
	}

	private void HideOfficerInventory()
	{
		if (_officerInventoryOverlay != null)
		{
			_officerInventoryOverlay.Visible = false;
		}

		_activeOfficerInventoryShipName = string.Empty;
	}

	private void OnOfficerInventoryWeaponEquipRequested(string shipName, string weaponId)
	{
		OfficerState officerState = ResolveOfficerStateForShip(shipName);
		if (officerState == null || !OfficerMissionLoadoutService.EquipWeapon(officerState, weaponId))
		{
			return;
		}

		MissionWeaponDefinition weapon = MissionEquipmentRegistry.GetWeapon(weaponId);
		if (UI?.CombatLogPanel != null)
		{
			UI.CombatLogPanel.Visible = true;
			LogCombatMessage($"[color=#d9b572]{officerState.DisplayName} equips {weapon?.DisplayName ?? weaponId}.[/color]");
		}

		ShowOfficerInventory(BuildOfficerInventoryPanelData(shipName, officerState));
	}

	private void OnOfficerInventoryShieldEquipRequested(string shipName, string shieldId)
	{
		OfficerState officerState = ResolveOfficerStateForShip(shipName);
		if (officerState == null || !OfficerMissionLoadoutService.EquipShield(officerState, shieldId))
		{
			return;
		}

		MissionShieldDefinition shield = MissionEquipmentRegistry.GetShield(shieldId);
		if (UI?.CombatLogPanel != null)
		{
			UI.CombatLogPanel.Visible = true;
			LogCombatMessage($"[color=#d9b572]{officerState.DisplayName} equips {shield?.DisplayName ?? shieldId}.[/color]");
		}

		ShowOfficerInventory(BuildOfficerInventoryPanelData(shipName, officerState));
	}

	private MissionOfficerInventoryPanelData BuildOfficerInventoryPanelData(string shipName, OfficerState officerState)
	{
		if (officerState == null || string.IsNullOrWhiteSpace(shipName))
		{
			return null;
		}

		OfficerMissionLoadoutService.EnsureOfficerLoadout(officerState);
		OfficerInventoryCombatProfile profile = BuildOfficerInventoryCombatProfile(officerState);
		List<MissionInventoryEntry> loadoutEntries = new List<MissionInventoryEntry>
		{
			new MissionInventoryEntry
			{
				Title = profile.WeaponName,
				Detail = BuildWeaponDetailText(OfficerMissionLoadoutService.GetEquippedWeapon(officerState), profile),
				Highlighted = true
			},
			new MissionInventoryEntry
			{
				Title = profile.ShieldName,
				Detail = BuildShieldDetailText(OfficerMissionLoadoutService.GetEquippedShield(officerState), profile),
				Highlighted = true
			},
			new MissionInventoryEntry
			{
				Title = string.IsNullOrWhiteSpace(profile.CombatAbilityId) ? "Combat Discipline" : profile.CombatAbilityId,
				Detail = string.IsNullOrWhiteSpace(profile.WeaponStatusEffectId)
					? "Status stable. No active impairments."
					: $"Loadout effect: {profile.WeaponStatusEffectId.Replace('_', ' ')}."
			}
		};

		List<MissionInventoryEntry> weaponEntries = BuildWeaponInventoryEntries(officerState, profile);
		List<MissionInventoryEntry> shieldEntries = BuildShieldInventoryEntries(officerState, profile);
		List<MissionInventoryEntry> itemEntries = BuildItemInventoryEntries(officerState);
		int carriedItems = (officerState.PersonalInventoryItemIDs ?? new List<string>()).Count;
		string footerText = "Click a locker entry to equip it. Right-click another captain portrait to switch dossiers.";
		if (weaponEntries.Count <= 1 && shieldEntries.Count <= 1)
		{
			footerText = carriedItems == 0
				? "No extra field gear is stowed on this officer yet."
				: $"Field pack holds {carriedItems} item{(carriedItems == 1 ? string.Empty : "s")}.";
		}

		return new MissionOfficerInventoryPanelData
		{
			OfficerId = shipName,
			DisplayName = officerState.DisplayName,
			ShipName = shipName,
			Specialty = officerState.Specialty,
			Portrait = LoadPortraitTexture(officerState.PortraitPath),
			SummaryText = BuildOfficerInventorySummaryText(officerState, profile),
			VitalStatsText = BuildOfficerVitalStatsText(officerState, profile),
			FooterText = footerText,
			LoadoutEntries = loadoutEntries,
			WeaponEntries = weaponEntries,
			ShieldEntries = shieldEntries,
			ItemEntries = itemEntries
		};
	}

	private static string BuildOfficerInventorySummaryText(OfficerState officerState, OfficerInventoryCombatProfile profile)
	{
		List<string> notes = new List<string>();
		if (!string.IsNullOrWhiteSpace(officerState?.Biography))
		{
			notes.Add(officerState.Biography.Trim());
		}
		else
		{
			notes.Add("No formal dossier has been written for this officer yet.");
		}

		if (!string.IsNullOrWhiteSpace(officerState?.Archetype) || !string.IsNullOrWhiteSpace(officerState?.Ideology))
		{
			notes.Add($"Alignment: {officerState.Archetype} with {officerState.Ideology} leanings.");
		}

		if (!string.IsNullOrWhiteSpace(officerState?.Flaw))
		{
			notes.Add($"Watchpoint: {officerState.Flaw}.");
		}

		if (!string.IsNullOrWhiteSpace(profile?.CombatAbilityId))
		{
			notes.Add($"Combat discipline: {profile.CombatAbilityId}.");
		}

		return string.Join("\n\n", notes.Where(note => !string.IsNullOrWhiteSpace(note)));
	}

	private static string BuildOfficerVitalStatsText(OfficerState officerState, OfficerInventoryCombatProfile profile)
	{
		if (profile == null)
		{
			return string.Empty;
		}

		List<string> lines = new List<string>
		{
			$"HP        {profile.MaxHp}/{profile.MaxHp}",
			$"SHIELDS   {profile.MaxShields}/{profile.MaxShields}",
			$"ACTIONS   {profile.MaxActions}/{profile.MaxActions}",
			$"RANGE     {profile.AttackRange}",
			$"DAMAGE    {profile.AttackMinDamage}-{profile.AttackDamage}",
			$"INIT      +{profile.InitiativeBonus}",
			$"APPROVAL  {officerState?.Approval ?? 0}",
			$"STRESS    {officerState?.Stress ?? 0}"
		};

		if (profile.ShieldRechargePerTurn > 0)
		{
			lines.Add($"RECHARGE  +{profile.ShieldRechargePerTurn}/turn");
		}

		return string.Join("\n", lines);
	}

	private static List<MissionInventoryEntry> BuildWeaponInventoryEntries(OfficerState officerState, OfficerInventoryCombatProfile profile)
	{
		List<MissionInventoryEntry> entries = OfficerMissionLoadoutService.GetOwnedWeaponIds(officerState)
			.Where(weaponId => !string.IsNullOrWhiteSpace(weaponId))
			.Distinct(StringComparer.Ordinal)
			.Select(weaponId =>
			{
				MissionWeaponDefinition definition = MissionEquipmentRegistry.GetWeapon(weaponId);
				bool isEquipped = string.Equals(weaponId, officerState.EquippedMissionWeaponId, StringComparison.Ordinal);
				return new MissionInventoryEntry
				{
					EntryId = weaponId,
					Title = definition?.DisplayName ?? weaponId,
					Detail = BuildWeaponDetailText(definition, profile),
					Highlighted = isEquipped,
					CanActivate = true,
					ActionText = isEquipped ? "EQUIPPED" : "EQUIP"
				};
			})
			.OrderByDescending(entry => entry.Highlighted)
			.ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (entries.Count == 0 && !string.IsNullOrWhiteSpace(profile?.WeaponName))
		{
			entries.Add(new MissionInventoryEntry
			{
				Title = profile.WeaponName,
				Detail = BuildWeaponDetailText(null, profile),
				Highlighted = true,
				CanActivate = true,
				ActionText = "EQUIPPED"
			});
		}

		return entries;
	}

	private static List<MissionInventoryEntry> BuildShieldInventoryEntries(OfficerState officerState, OfficerInventoryCombatProfile profile)
	{
		List<MissionInventoryEntry> entries = OfficerMissionLoadoutService.GetOwnedShieldIds(officerState)
			.Where(shieldId => !string.IsNullOrWhiteSpace(shieldId))
			.Distinct(StringComparer.Ordinal)
			.Select(shieldId =>
			{
				MissionShieldDefinition definition = MissionEquipmentRegistry.GetShield(shieldId);
				bool isEquipped = string.Equals(shieldId, officerState.EquippedMissionShieldId, StringComparison.Ordinal);
				return new MissionInventoryEntry
				{
					EntryId = shieldId,
					Title = definition?.DisplayName ?? shieldId,
					Detail = BuildShieldDetailText(definition, profile),
					Highlighted = isEquipped,
					CanActivate = true,
					ActionText = isEquipped ? "EQUIPPED" : "EQUIP"
				};
			})
			.OrderByDescending(entry => entry.Highlighted)
			.ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (entries.Count == 0 && !string.IsNullOrWhiteSpace(profile?.ShieldName))
		{
			entries.Add(new MissionInventoryEntry
			{
				Title = profile.ShieldName,
				Detail = BuildShieldDetailText(null, profile),
				Highlighted = true,
				CanActivate = true,
				ActionText = "EQUIPPED"
			});
		}

		return entries;
	}

	private static List<MissionInventoryEntry> BuildItemInventoryEntries(OfficerState officerState)
	{
		return (officerState?.PersonalInventoryItemIDs ?? new List<string>())
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.GroupBy(itemId => itemId, StringComparer.Ordinal)
			.Select(group =>
			{
				CampaignItemDefinition definition = CampaignItemRegistry.GetItem(group.Key);
				return new MissionInventoryEntry
				{
					Title = definition?.DisplayName ?? group.Key,
					Detail = BuildItemDetailText(definition),
					QuantityText = group.Count() > 1 ? $"x{group.Count()}" : string.Empty
				};
			})
			.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string BuildWeaponDetailText(MissionWeaponDefinition definition, OfficerInventoryCombatProfile profile = null)
	{
		List<string> parts = new List<string>();
		if (definition != null)
		{
			parts.Add(definition.IsMelee ? "Melee" : $"Range {definition.AttackRange}");
			parts.Add($"DMG {definition.MinDamage}-{definition.MaxDamage}");
			if (definition.BonusShieldDamage > 0)
			{
				parts.Add($"+{definition.BonusShieldDamage} vs shields");
			}

			if (definition.ShieldPiercingDamage > 0)
			{
				parts.Add($"{definition.ShieldPiercingDamage} pierce");
			}

			if (!string.IsNullOrWhiteSpace(definition.StatusEffectId))
			{
				int chancePercent = Mathf.RoundToInt(definition.StatusEffectChance * 100f);
				parts.Add($"{definition.StatusEffectId.Replace('_', ' ')} {chancePercent}%");
			}

			if (!string.IsNullOrWhiteSpace(definition.Description))
			{
				parts.Add(definition.Description);
			}

			return string.Join(" | ", parts);
		}

		if (profile != null)
		{
			parts.Add(profile.UsesMeleeWeapon ? "Melee" : $"Range {profile.AttackRange}");
			parts.Add($"DMG {profile.AttackMinDamage}-{profile.AttackDamage}");
			if (profile.BonusShieldDamage > 0)
			{
				parts.Add($"+{profile.BonusShieldDamage} vs shields");
			}

			if (profile.ShieldPiercingDamage > 0)
			{
				parts.Add($"{profile.ShieldPiercingDamage} pierce");
			}
		}

		return string.Join(" | ", parts);
	}

	private static string BuildShieldDetailText(MissionShieldDefinition definition, OfficerInventoryCombatProfile profile = null)
	{
		List<string> parts = new List<string>();
		if (definition != null)
		{
			parts.Add($"+{definition.CapacityBonus} capacity");
			parts.Add($"+{definition.RechargePerTurn}/turn");
			if (!string.IsNullOrWhiteSpace(definition.Description))
			{
				parts.Add(definition.Description);
			}

			return string.Join(" | ", parts);
		}

		if (profile != null)
		{
			parts.Add($"Capacity {profile.MaxShields}");
			parts.Add($"+{profile.ShieldRechargePerTurn}/turn");
		}

		return string.Join(" | ", parts);
	}

	private static string BuildItemDetailText(CampaignItemDefinition definition)
	{
		if (definition == null)
		{
			return "Recovered field salvage.";
		}

		List<string> parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(definition.Category))
		{
			parts.Add(definition.Category);
		}

		if (!string.IsNullOrWhiteSpace(definition.Description))
		{
			parts.Add(definition.Description);
		}

		return parts.Count == 0 ? "Recovered field salvage." : string.Join(" | ", parts);
	}

	private static OfficerInventoryCombatProfile BuildOfficerInventoryCombatProfile(OfficerState officerState)
	{
		OfficerInventoryCombatProfile profile = CreateBaseOfficerInventoryCombatProfile(officerState?.Specialty);
		profile.CombatAbilityId = officerState?.CombatAbilityID ?? string.Empty;

		int baseMaxShields = profile.MaxShields;
		int baseShieldRecharge = profile.ShieldRechargePerTurn;

		MissionWeaponDefinition weapon = OfficerMissionLoadoutService.GetEquippedWeapon(officerState);
		if (weapon != null)
		{
			profile.WeaponName = string.IsNullOrWhiteSpace(weapon.DisplayName) ? profile.WeaponName : weapon.DisplayName;
			profile.UsesMeleeWeapon = weapon.IsMelee;
			profile.AttackRange = Mathf.Max(1, weapon.AttackRange);
			profile.AttackMinDamage = Mathf.Max(1, weapon.MinDamage);
			profile.AttackDamage = Mathf.Max(profile.AttackMinDamage, weapon.MaxDamage);
			profile.BonusShieldDamage = Mathf.Max(0, weapon.BonusShieldDamage);
			profile.ShieldPiercingDamage = Mathf.Max(0, weapon.ShieldPiercingDamage);
			profile.WeaponStatusEffectId = weapon.StatusEffectId ?? string.Empty;
			profile.WeaponStatusEffectChance = Mathf.Max(0f, weapon.StatusEffectChance);
		}

		MissionShieldDefinition shield = OfficerMissionLoadoutService.GetEquippedShield(officerState);
		if (shield != null)
		{
			profile.ShieldName = string.IsNullOrWhiteSpace(shield.DisplayName) ? profile.ShieldName : shield.DisplayName;
			profile.MaxShields = Mathf.Max(0, baseMaxShields + shield.CapacityBonus);
			profile.ShieldRechargePerTurn = Mathf.Max(0, baseShieldRecharge + shield.RechargePerTurn);
		}

		return profile;
	}

	private static OfficerInventoryCombatProfile CreateBaseOfficerInventoryCombatProfile(string specialty)
	{
		OfficerInventoryCombatProfile profile = new OfficerInventoryCombatProfile();
		switch (specialty)
		{
			case "Medical Triage":
			case "Morale Support":
				profile.MaxHp = 16;
				profile.MaxShields = 4;
				profile.MaxActions = 2;
				profile.AttackMinDamage = 2;
				profile.AttackRange = 1;
				profile.AttackDamage = 3;
				profile.InitiativeBonus = 0;
				profile.WeaponName = "Shock Baton";
				break;
			case "Salvage Efficiency":
			case "Engine Routing":
				profile.MaxHp = 15;
				profile.MaxShields = 5;
				profile.MaxActions = 2;
				profile.AttackMinDamage = 2;
				profile.AttackRange = 1;
				profile.AttackDamage = 4;
				profile.InitiativeBonus = 1;
				profile.WeaponName = "Cutting Rig";
				break;
			case "Missile Control":
				profile.MaxHp = 13;
				profile.MaxShields = 6;
				profile.MaxActions = 2;
				profile.AttackMinDamage = 3;
				profile.AttackRange = 4;
				profile.AttackDamage = 5;
				profile.InitiativeBonus = 1;
				profile.WeaponName = "Heavy Sidearm";
				break;
			case "Tactical Command":
				profile.MaxHp = 14;
				profile.MaxShields = 6;
				profile.MaxActions = 2;
				profile.AttackMinDamage = 3;
				profile.AttackRange = 4;
				profile.AttackDamage = 5;
				profile.InitiativeBonus = 2;
				profile.WeaponName = "Pulse Carbine";
				break;
			case "Shield Tuning":
				profile.MaxHp = 17;
				profile.MaxShields = 8;
				profile.MaxActions = 2;
				profile.AttackMinDamage = 2;
				profile.AttackRange = 2;
				profile.AttackDamage = 4;
				profile.InitiativeBonus = 0;
				profile.WeaponName = "Defense Pistol";
				profile.ShieldRechargePerTurn = 2;
				break;
			default:
				profile.MaxHp = 14;
				profile.MaxShields = 5;
				profile.MaxActions = 2;
				profile.AttackMinDamage = 2;
				profile.AttackRange = 3;
				profile.AttackDamage = 4;
				profile.InitiativeBonus = 1;
				profile.WeaponName = "Sidearm";
				break;
		}

		return profile;
	}

	private PanelContainer BuildInventoryRailTag(string text, bool active)
	{
		PanelContainer tag = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 70f)
		};
		tag.AddThemeStyleboxOverride("panel", CreateInventoryRailStyle(active));

		Label label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", active ? new Color(0.96f, 0.90f, 0.80f) : new Color(0.72f, 0.68f, 0.62f));
		tag.AddChild(label);
		return tag;
	}

	private PanelContainer BuildInventorySectionCard(string title, out VBoxContainer body)
	{
		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);
		content.AddChild(BuildInventorySectionHeader(title));

		body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 8);
		content.AddChild(body);
		return panel;
	}

	private Label BuildInventorySectionHeader(string title)
	{
		Label header = new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		header.AddThemeFontSizeOverride("font_size", 17);
		header.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.74f));
		return header;
	}

	private void PopulateInventoryEntryStack(VBoxContainer container, IReadOnlyList<MissionInventoryEntry> entries, string emptyText)
	{
		if (container == null)
		{
			return;
		}

		ClearContainerChildren(container);
		if (entries == null || entries.Count == 0)
		{
			container.AddChild(BuildInventoryPlaceholder(emptyText));
			return;
		}

		foreach (MissionInventoryEntry entry in entries)
		{
			if (entry == null)
			{
				continue;
			}

			container.AddChild(BuildInventoryEntryCard(entry));
		}
	}

	private void PopulateInteractiveInventoryEntryStack(
		VBoxContainer container,
		IReadOnlyList<MissionInventoryEntry> entries,
		string emptyText,
		Action<string> onActivate)
	{
		if (container == null)
		{
			return;
		}

		ClearContainerChildren(container);
		if (entries == null || entries.Count == 0)
		{
			container.AddChild(BuildInventoryPlaceholder(emptyText));
			return;
		}

		foreach (MissionInventoryEntry entry in entries)
		{
			if (entry == null)
			{
				continue;
			}

			container.AddChild(BuildInventoryEntryCard(entry, onActivate));
		}
	}

	private void PopulateInventoryItemStack(IReadOnlyList<MissionInventoryEntry> entries)
	{
		if (_officerInventoryItemStack == null)
		{
			return;
		}

		ClearContainerChildren(_officerInventoryItemStack);
		int visibleCount = 0;
		foreach (MissionInventoryEntry entry in entries ?? Array.Empty<MissionInventoryEntry>())
		{
			if (entry == null)
			{
				continue;
			}

			_officerInventoryItemStack.AddChild(BuildInventoryItemCell(entry));
			visibleCount++;
		}

		if (visibleCount == 0)
		{
			_officerInventoryItemStack.AddChild(BuildInventoryPlaceholder("No field items are stowed here."));
			return;
		}

		for (int slotIndex = visibleCount; slotIndex < 4; slotIndex++)
		{
			_officerInventoryItemStack.AddChild(BuildInventoryEmptyCell());
		}
	}

	private Control BuildInventoryEntryCard(MissionInventoryEntry entry, Action<string> onActivate = null)
	{
		PanelContainer card = new PanelContainer
		{
			TooltipText = entry.Detail,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 76f)
		};
		card.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(entry.Highlighted, false));

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		card.AddChild(margin);

		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		margin.AddChild(row);

		VBoxContainer textColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		textColumn.AddThemeConstantOverride("separation", 2);
		row.AddChild(textColumn);

		Label title = new Label
		{
			Text = entry.Title
		};
		title.AddThemeFontSizeOverride("font_size", 15);
		title.AddThemeColorOverride("font_color", entry.Highlighted ? new Color(0.98f, 0.92f, 0.82f) : Colors.White);
		textColumn.AddChild(title);

		if (!string.IsNullOrWhiteSpace(entry.Detail))
		{
			Label detail = new Label
			{
				Text = entry.Detail,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			detail.AddThemeFontSizeOverride("font_size", 12);
			detail.AddThemeColorOverride("font_color", new Color(0.83f, 0.79f, 0.72f));
			textColumn.AddChild(detail);
		}

		if (!string.IsNullOrWhiteSpace(entry.QuantityText))
		{
			Label quantity = new Label
			{
				Text = entry.QuantityText,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center
			};
			quantity.CustomMinimumSize = new Vector2(40f, 0f);
			quantity.AddThemeFontSizeOverride("font_size", 14);
			quantity.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.56f));
			row.AddChild(quantity);
		}

		if (entry.CanActivate && !string.IsNullOrWhiteSpace(entry.EntryId))
		{
			Button actionButton = new Button
			{
				Text = string.IsNullOrWhiteSpace(entry.ActionText) ? "EQUIP" : entry.ActionText,
				Disabled = entry.Highlighted,
				CustomMinimumSize = new Vector2(88f, 34f)
			};
			actionButton.AddThemeStyleboxOverride("normal", CreateInventoryButtonStyle());
			actionButton.AddThemeStyleboxOverride("hover", CreateInventoryButtonStyle(true));
			actionButton.AddThemeStyleboxOverride("pressed", CreateInventoryButtonStyle(true));
			actionButton.AddThemeStyleboxOverride("disabled", CreateInventoryButtonStyle());
			actionButton.AddThemeFontSizeOverride("font_size", 12);
			string entryId = entry.EntryId;
			actionButton.Pressed += () => onActivate?.Invoke(entryId);
			row.AddChild(actionButton);
		}

		return card;
	}

	private Control BuildInventoryPlaceholder(string text)
	{
		PanelContainer card = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		card.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(false, true));

		Label label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", new Color(0.68f, 0.64f, 0.60f));
		card.CustomMinimumSize = new Vector2(0f, 64f);
		card.AddChild(label);
		return card;
	}

	private Control BuildInventoryItemCell(MissionInventoryEntry entry)
	{
		PanelContainer cell = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 92f),
			TooltipText = entry.Detail,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		cell.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(entry.Highlighted, false));

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		cell.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 4);
		margin.AddChild(content);

		Label title = new Label
		{
			Text = entry.Title,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		title.AddThemeFontSizeOverride("font_size", 14);
		title.AddThemeColorOverride("font_color", Colors.White);
		content.AddChild(title);

		Label detail = new Label
		{
			Text = string.IsNullOrWhiteSpace(entry.Detail) ? "Field gear slot." : entry.Detail,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		detail.AddThemeFontSizeOverride("font_size", 11);
		detail.AddThemeColorOverride("font_color", new Color(0.82f, 0.77f, 0.70f));
		content.AddChild(detail);

		if (!string.IsNullOrWhiteSpace(entry.QuantityText))
		{
			Label quantity = new Label
			{
				Text = entry.QuantityText,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			quantity.AddThemeFontSizeOverride("font_size", 13);
			quantity.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.52f));
			content.AddChild(quantity);
		}

		return cell;
	}

	private Control BuildInventoryEmptyCell()
	{
		PanelContainer cell = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 56f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		cell.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(false, true));
		return cell;
	}

	private static void ClearContainerChildren(Node container)
	{
		if (container == null)
		{
			return;
		}

		foreach (Node child in container.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void OnOfficerInventoryOverlayGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton
			|| !mouseButton.Pressed
			|| mouseButton.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		HideOfficerInventory();
		_officerInventoryOverlay?.AcceptEvent();
	}

	private static Texture2D LoadPortraitTexture(string resourcePath)
	{
		if (string.IsNullOrWhiteSpace(resourcePath) || !ResourceLoader.Exists(resourcePath))
		{
			return null;
		}

		return ResourceLoader.Load<Texture2D>(resourcePath);
	}

	private static StyleBoxFlat CreateInventoryWindowStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.17f, 0.13f, 0.11f, 0.98f),
			BorderColor = new Color(0.55f, 0.50f, 0.44f, 1f),
			BorderWidthLeft = 5,
			BorderWidthTop = 5,
			BorderWidthRight = 5,
			BorderWidthBottom = 5,
			CornerRadiusTopLeft = 16,
			CornerRadiusTopRight = 16,
			CornerRadiusBottomRight = 16,
			CornerRadiusBottomLeft = 16,
			ShadowColor = new Color(0f, 0f, 0f, 0.42f),
			ShadowSize = 12,
			ContentMarginLeft = 4f,
			ContentMarginTop = 4f,
			ContentMarginRight = 4f,
			ContentMarginBottom = 4f
		};
	}

	private static StyleBoxFlat CreateInventoryBannerStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.31f, 0.24f, 0.18f, 0.98f),
			BorderColor = new Color(0.68f, 0.60f, 0.45f, 1f),
			BorderWidthLeft = 4,
			BorderWidthTop = 4,
			BorderWidthRight = 4,
			BorderWidthBottom = 4,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12
		};
	}

	private static StyleBoxFlat CreateInventorySectionStyle(bool highlighted = false)
	{
		return new StyleBoxFlat
		{
			BgColor = highlighted ? new Color(0.25f, 0.20f, 0.17f, 0.96f) : new Color(0.14f, 0.11f, 0.09f, 0.96f),
			BorderColor = highlighted ? new Color(0.72f, 0.60f, 0.42f, 1f) : new Color(0.44f, 0.40f, 0.36f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
	}

	private static StyleBoxFlat CreateInventorySlotStyle(bool highlighted, bool empty)
	{
		return new StyleBoxFlat
		{
			BgColor = empty
				? new Color(0.09f, 0.08f, 0.07f, 0.82f)
				: highlighted
					? new Color(0.34f, 0.23f, 0.14f, 0.94f)
					: new Color(0.18f, 0.15f, 0.12f, 0.94f),
			BorderColor = empty
				? new Color(0.24f, 0.22f, 0.20f, 0.82f)
				: highlighted
					? new Color(0.86f, 0.70f, 0.42f, 1f)
					: new Color(0.46f, 0.40f, 0.34f, 1f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8
		};
	}

	private static StyleBoxFlat CreateInventoryRailStyle(bool active)
	{
		return new StyleBoxFlat
		{
			BgColor = active ? new Color(0.30f, 0.22f, 0.14f, 0.96f) : new Color(0.11f, 0.09f, 0.08f, 0.92f),
			BorderColor = active ? new Color(0.82f, 0.68f, 0.40f, 1f) : new Color(0.36f, 0.33f, 0.30f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
	}

	private static StyleBoxFlat CreateInventoryButtonStyle(bool hover = false)
	{
		return new StyleBoxFlat
		{
			BgColor = hover ? new Color(0.39f, 0.26f, 0.16f, 0.98f) : new Color(0.24f, 0.18f, 0.12f, 0.96f),
			BorderColor = hover ? new Color(0.90f, 0.74f, 0.44f, 1f) : new Color(0.62f, 0.50f, 0.30f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
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

	private void OnMissionPressed()
	{
		if (IsFleetMoving || Combat.InCombat || CurrentlyViewedShip == null || _shipContextService == null)
		{
			return;
		}

		MissionInteractionContext missionContext = _shipContextService.GetAdjacentMissionContext(CurrentlyViewedShip, HexContents);
		if (missionContext == null)
		{
			return;
		}

		ShowMissionPrompt(CurrentlyViewedShip, missionContext, false);
	}

	private void ShowMissionPrompt(MapEntity ship, MissionInteractionContext missionContext, bool reopenShipMenu)
	{
		if (_missionPromptWrapper == null || _shipContextService == null || _missionService == null || ship == null || missionContext == null)
		{
			return;
		}

		if (missionContext.Definition == null || string.IsNullOrWhiteSpace(missionContext.InteractionKey))
		{
			return;
		}

		_pendingMissionShip = ship;
		_pendingMissionContext = missionContext;
		_missionPromptTitle.Text = missionContext.Definition.Title.ToUpper();
		_missionPromptDescription.Text = BuildMissionPromptDescription(missionContext);
		PopulateMissionOfficerSelection(missionContext);
		_missionPromptWrapper.Visible = true;

		if (reopenShipMenu)
		{
			ToggleShipMenu(true, ship);
		}
	}

	private string BuildMissionPromptDescription(MissionInteractionContext missionContext)
	{
		if (missionContext.InteractionKey == "planet:black_site_relay")
		{
			return "[color=#66f0ff]A concealed relay signature is bleeding through the crust of "
				+ missionContext.SourceNodeID
				+ ".[/color]\n\n"
				+ missionContext.Definition.Description
				+ "\n\nChoose your away team below, then dispatch the mission.";
		}

		string sourceLabel = string.IsNullOrWhiteSpace(missionContext.SourceNodeID)
			? missionContext.SourceNodeType
			: missionContext.SourceNodeID;
		return "[color=#66f0ff]An actionable signal is available at "
			+ sourceLabel
			+ ".[/color]\n\n"
			+ missionContext.Definition.Description
			+ "\n\nChoose your away team below, then dispatch the mission.";
	}

	private void HideMissionPrompt()
	{
		if (_missionPromptWrapper != null)
		{
			_missionPromptWrapper.Visible = false;
		}

		_pendingMissionShip = null;
		_pendingMissionContext = null;
		_missionOfficerCheckboxes.Clear();
	}

	private void LaunchPendingMission()
	{
		if (_missionService == null || _pendingMissionContext == null)
		{
			return;
		}

		List<string> selectedMissionShips = GetSelectedMissionOfficerShips();
		if (!CanLaunchPendingMission(selectedMissionShips))
		{
			UpdateMissionOfficerSelectionStatus();
			return;
		}

		_globalData.SelectedMissionOfficerShipNames = new List<string>(selectedMissionShips);
		_globalData.SelectedMissionOfficerIDs = selectedMissionShips
			.Select(shipName => _officerService?.GetOfficerForShip(shipName)?.OfficerID ?? string.Empty)
			.Where(officerId => !string.IsNullOrWhiteSpace(officerId))
			.ToList();

		MissionRuntimeState state = _missionService.PrepareMissionFromInteraction(
			_pendingMissionContext.InteractionKey,
			"res://exploration_battle.tscn",
			_pendingMissionContext.SourceEncounterName,
			_pendingMissionContext.SourceNodeType,
			_pendingMissionContext.SourceNodeID);
		if (state == null || string.IsNullOrEmpty(state.ScenePath))
		{
			return;
		}

		SaveCampaign(true);
		HideMissionPrompt();

		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(state.ScenePath);
		}
		else
		{
			GetTree().ChangeSceneToFile(state.ScenePath);
		}
	}

	private void TryPromptMissionForShip(MapEntity ship)
	{
		if (_shipContextService == null || Combat.InCombat || IsFleetMoving)
		{
			return;
		}

		if (_missionPromptWrapper != null && _missionPromptWrapper.Visible)
		{
			return;
		}

		if (ship?.Type != GameConstants.EntityTypes.PlayerFleet)
		{
			return;
		}

		MissionInteractionContext missionContext = _shipContextService.GetAdjacentMissionContext(ship, HexContents);
		if (missionContext != null)
		{
			ShowMissionPrompt(ship, missionContext, true);
		}
	}

	private PlanetData GetPlanetDataByName(string planetName)
	{
		if (_globalData == null
			|| string.IsNullOrEmpty(_globalData.SavedSystem)
			|| !_globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			return null;
		}

		SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
		return currentSystem.Planets.FirstOrDefault(planet => planet != null && planet.Name == planetName);
	}

	private void OnCodexPressed() 
	{ 
		if (IsFleetMoving) return; 
		SaveCampaign(true); 
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
			SaveCampaign(true); 

			SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
			string nextScene = _jumpService.FinalizeJump(jumpPlan.IsEmergencyJump);
			
			if (transitioner != null) transitioner.ChangeScene(nextScene);
			else GetTree().ChangeSceneToFile(nextScene);
		}));
	}

	private void OnManualSaveGamePressed()
	{
		ShowSavePrompt();
	}

	private void SaveCampaign(bool autoSave, string saveName = "")
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

		if (autoSave)
		{
			_globalData.SaveGame(true, "res://exploration_battle.tscn");
		}
		else if (string.IsNullOrWhiteSpace(saveName))
		{
			_globalData.SaveGame(false, "res://exploration_battle.tscn");
		}
		else
		{
			_globalData.SaveNamedGame(saveName, "res://exploration_battle.tscn");
		}

		if (!autoSave)
		{
			if (UI?.CombatLogPanel != null)
			{
				UI.CombatLogPanel.Visible = true;
			}
			string saveLabel = string.IsNullOrWhiteSpace(saveName) ? "QUICKSAVE" : saveName.ToUpperInvariant();
			LogCombatMessage($"\n[color=green]--- GAME SAVED: {saveLabel} ---[/color]");
		}
	}

	private void QuickSaveCampaign()
	{
		SaveCampaign(autoSave: false);
	}

	private void QuickLoadCampaign()
	{
		if (_globalData == null)
		{
			return;
		}

		SaveGameSlotInfo quicksave = _globalData
			.GetAvailableSaveGames()
			.FirstOrDefault(save => save != null && save.IsLegacySave);
		if (quicksave == null)
		{
			if (UI?.CombatLogPanel != null)
			{
				UI.CombatLogPanel.Visible = true;
			}
			LogCombatMessage("\n[color=yellow]--- NO QUICKSAVE FOUND ---[/color]");
			return;
		}

		if (!_globalData.LoadGame(quicksave.SlotId))
		{
			if (UI?.CombatLogPanel != null)
			{
				UI.CombatLogPanel.Visible = true;
			}
			LogCombatMessage("\n[color=red]--- QUICKLOAD FAILED ---[/color]");
			return;
		}

		HidePauseMenus();
		string scenePath = ResolveLoadedScenePath(_globalData, quicksave);
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(scenePath);
			return;
		}

		GetTree().ChangeSceneToFile(scenePath);
	}

	private void OnMainMenuPressed() 
	{
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://main_menu.tscn");
		else GetTree().ChangeSceneToFile("res://main_menu.tscn");
	}

	private void ReturnToMainMenuFromPause()
	{
		HidePauseMenus();
		OnMainMenuPressed();
	}

	private void ShowPendingOfficerReplacementPromptIfNeeded()
	{
		if (_replacementPromptWrapper == null
			|| _globalData?.PendingOfficerReplacementShipNames == null
			|| _globalData.PendingOfficerReplacementShipNames.Count == 0)
		{
			return;
		}

		string shipName = _globalData.PendingOfficerReplacementShipNames
			.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && (_officerService?.GetOfficerForShip(candidate) == null));
		if (string.IsNullOrWhiteSpace(shipName))
		{
			_globalData.PendingOfficerReplacementShipNames.RemoveAll(candidate => string.IsNullOrWhiteSpace(candidate) || _officerService?.GetOfficerForShip(candidate) != null);
			return;
		}

		_activeReplacementShipName = shipName;
		float population = _globalData.FleetResources.ContainsKey(GameConstants.ResourceKeys.Population)
			? _globalData.FleetResources[GameConstants.ResourceKeys.Population].AsSingle()
			: 0f;
		_replacementPromptTitleLabel.Text = "OFFICER DOWN";
		_replacementPromptBodyLabel.Text =
			$"[center]{shipName} returned from the relay mission without an assigned officer.[/center]\n\n" +
			$"[center]Rescued {CampaignText.RemnantsLabel.ToLowerInvariant()} available: [color=cyan]{Mathf.FloorToInt(population)}[/color][/center]\n\n" +
			"[center]Recruit a random new officer from the civilians your fleet has saved, or wait until later.[/center]";
		_replacementPromptStatusLabel.Text = population > 0f
			? string.Empty
			: $"No saved {CampaignText.RemnantsLabel.ToLowerInvariant()} are currently available for officer replacement.";
		_replacementPromptWrapper.Visible = true;
	}

	private void HideOfficerReplacementPrompt()
	{
		if (_replacementPromptWrapper != null)
		{
			_replacementPromptWrapper.Visible = false;
		}
	}

	private void RecruitReplacementOfficer()
	{
		if (_globalData == null || string.IsNullOrWhiteSpace(_activeReplacementShipName))
		{
			return;
		}

		float population = _globalData.FleetResources.ContainsKey(GameConstants.ResourceKeys.Population)
			? _globalData.FleetResources[GameConstants.ResourceKeys.Population].AsSingle()
			: 0f;
		if (population < 1f)
		{
			if (_replacementPromptStatusLabel != null)
			{
				_replacementPromptStatusLabel.Text = $"No saved {CampaignText.RemnantsLabel.ToLowerInvariant()} are available for recruitment yet.";
			}
			return;
		}

		OfficerState replacementOfficer = _officerService?.CreateRandomPopulationOfficer(_activeReplacementShipName);
		if (replacementOfficer == null)
		{
			if (_replacementPromptStatusLabel != null)
			{
				_replacementPromptStatusLabel.Text = "Unable to assemble a replacement officer right now.";
			}
			return;
		}

		_officerService.AssignOfficerToShip(_activeReplacementShipName, replacementOfficer);
		_globalData.FleetResources[GameConstants.ResourceKeys.Population] = Mathf.Max(0f, population - 1f);
		_globalData.PendingOfficerReplacementShipNames.Remove(_activeReplacementShipName);
		LogCombatMessage($"[color=cyan]{replacementOfficer.DisplayName} joins {_activeReplacementShipName} as a replacement officer.[/color]");
		UpdateResourceUI();
		HideOfficerReplacementPrompt();
		_activeReplacementShipName = string.Empty;
		ShowPendingOfficerReplacementPromptIfNeeded();
	}

	private void OpenOfficerSwapMenu()
	{
		if (_globalData == null || CurrentlyViewedShip == null || string.IsNullOrWhiteSpace(CurrentlyViewedShip.Name))
		{
			return;
		}

		HideOfficerInventory();
		_activeOfficerSwapShipName = CurrentlyViewedShip.Name;
		PopulateOfficerSwapMenu(_activeOfficerSwapShipName);
		if (_officerSwapMenuWrapper != null)
		{
			_officerSwapMenuWrapper.Visible = true;
		}
	}

	private void HideOfficerSwapMenu()
	{
		if (_officerSwapMenuWrapper != null)
		{
			_officerSwapMenuWrapper.Visible = false;
		}

		_activeOfficerSwapShipName = string.Empty;
	}

	private void PopulateOfficerSwapMenu(string shipName)
	{
		if (_globalData == null
			|| _officerSwapTitleLabel == null
			|| _officerSwapBodyLabel == null
			|| _officerSwapOptionList == null
			|| _officerSwapStatusLabel == null)
		{
			return;
		}

		ClearContainerChildren(_officerSwapOptionList);
		OfficerState currentOfficer = ResolveOfficerStateForShip(shipName);
		string currentOfficerName = currentOfficer?.DisplayName ?? "No assigned officer";
		string currentOfficerSpecialty = currentOfficer?.Specialty ?? "Unassigned";

		_officerSwapTitleLabel.Text = $"OFFICER TRANSFER: {shipName.ToUpperInvariant()}";
		_officerSwapBodyLabel.Text =
			$"[center]Current officer: [color=cyan]{currentOfficerName}[/color][/center]\n" +
			$"[center]{currentOfficerSpecialty}[/center]\n\n" +
			$"Select a saved {CampaignText.RemnantsLabel.TrimEnd('s').ToLowerInvariant()} to assign to this ship. " +
			$"If an officer is already posted here, they will move into the reserve roster and can be reassigned later.";

		List<RemnantRecord> candidates = (_globalData.RescuedRemnants ?? new List<RemnantRecord>())
			.Where(remnant => remnant != null && !string.IsNullOrWhiteSpace(remnant.RecordId))
			.OrderByDescending(remnant => remnant.StoredOfficerState != null)
			.ThenBy(remnant => remnant.DisplayName, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (candidates.Count == 0)
		{
			_officerSwapStatusLabel.Text = $"No saved {CampaignText.RemnantsLabel.ToLowerInvariant()} are currently available for reassignment.";
			_officerSwapOptionList.AddChild(BuildInventoryPlaceholder($"No saved {CampaignText.RemnantsLabel.ToLowerInvariant()} available."));
			return;
		}

		foreach (RemnantRecord remnant in candidates)
		{
			_officerSwapOptionList.AddChild(BuildOfficerSwapOptionCard(shipName, remnant));
		}

		_officerSwapStatusLabel.Text = $"Available reserve roster: {candidates.Count} {CampaignText.RemnantsLabel.ToLowerInvariant()}.";
	}

	private Control BuildOfficerSwapOptionCard(string shipName, RemnantRecord remnant)
	{
		PanelContainer card = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		card.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		card.AddChild(margin);

		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		VBoxContainer textColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		textColumn.AddThemeConstantOverride("separation", 6);
		row.AddChild(textColumn);

		Label nameLabel = new Label
		{
			Text = string.IsNullOrWhiteSpace(remnant.DisplayName) ? "UNNAMED REMNANT" : remnant.DisplayName.ToUpperInvariant()
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameLabel.AddThemeColorOverride("font_color", Colors.White);
		textColumn.AddChild(nameLabel);

		string reserveTag = remnant.StoredOfficerState != null ? "Reserve Officer" : CampaignText.RemnantsLabel.TrimEnd('s');
		string subtitle = remnant.StoredOfficerState != null
			? $"{reserveTag} | {remnant.StoredOfficerState.Specialty} | Approval {remnant.StoredOfficerState.Approval}"
			: $"{reserveTag} | {BuildRemnantCommissionSummary(remnant)}";
		Label subtitleLabel = new Label
		{
			Text = subtitle,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		subtitleLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.92f, 1f));
		textColumn.AddChild(subtitleLabel);

		Label bodyLabel = new Label
		{
			Text = BuildRemnantSwapDescription(remnant),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		bodyLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.86f, 0.92f));
		textColumn.AddChild(bodyLabel);

		Button assignButton = new Button
		{
			Text = "ASSIGN",
			CustomMinimumSize = new Vector2(140f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		string recordId = remnant.RecordId;
		assignButton.Pressed += () => AssignRemnantToShip(shipName, recordId);
		row.AddChild(assignButton);

		return card;
	}

	private void AssignRemnantToShip(string shipName, string remnantRecordId)
	{
		if (_globalData == null || string.IsNullOrWhiteSpace(shipName) || string.IsNullOrWhiteSpace(remnantRecordId))
		{
			return;
		}

		List<RemnantRecord> roster = _globalData.RescuedRemnants ??= new List<RemnantRecord>();
		RemnantRecord selectedRemnant = roster.FirstOrDefault(remnant => remnant != null && remnant.RecordId == remnantRecordId);
		if (selectedRemnant == null)
		{
			if (_officerSwapStatusLabel != null)
			{
				_officerSwapStatusLabel.Text = "That reserve assignment is no longer available.";
			}
			return;
		}

		OfficerState incomingOfficer = CreateOfficerFromRemnant(selectedRemnant, shipName);
		if (incomingOfficer == null)
		{
			if (_officerSwapStatusLabel != null)
			{
				_officerSwapStatusLabel.Text = "Unable to commission that reserve into officer duty right now.";
			}
			return;
		}

		OfficerState outgoingOfficer = ResolveOfficerStateForShip(shipName);
		roster.Remove(selectedRemnant);
		if (outgoingOfficer != null)
		{
			roster.Add(CreateRemnantFromOfficer(outgoingOfficer));
		}

		_officerService?.AssignOfficerToShip(shipName, incomingOfficer);
		_globalData.PendingOfficerReplacementShipNames?.Remove(shipName);

		if (UI?.CombatLogPanel != null)
		{
			UI.CombatLogPanel.Visible = true;
			LogCombatMessage(outgoingOfficer == null
				? $"[color=cyan]{incomingOfficer.DisplayName} is assigned to {shipName} from the reserve roster.[/color]"
				: $"[color=cyan]{incomingOfficer.DisplayName} relieves {outgoingOfficer.DisplayName} aboard {shipName}.[/color]");
		}

		SaveCampaign(true);
		HideOfficerSwapMenu();
		if (CurrentlyViewedShip != null && CurrentlyViewedShip.Name == shipName)
		{
			ToggleShipMenu(true, CurrentlyViewedShip);
		}
	}

	private OfficerState CreateOfficerFromRemnant(RemnantRecord remnant, string shipName)
	{
		if (remnant?.StoredOfficerState != null)
		{
			OfficerState restoredOfficer = remnant.StoredOfficerState.ToRuntime();
			restoredOfficer.ShipName = shipName;
			OfficerMissionLoadoutService.EnsureOfficerLoadout(restoredOfficer);
			return restoredOfficer;
		}

		if (remnant == null || _officerService == null)
		{
			return null;
		}

		string seedKey = string.IsNullOrWhiteSpace(remnant.RecordId) ? remnant.DisplayName : remnant.RecordId;
		OfficerState commissionedOfficer = _officerService.CreateCustomOfficer(new CustomOfficerRequest
		{
			ShipName = shipName,
			DisplayName = string.IsNullOrWhiteSpace(remnant.DisplayName) ? $"Officer {shipName}" : remnant.DisplayName,
			PortraitPath = remnant.PortraitPath,
			Archetype = SelectBySeed(OfficerService.Archetypes, seedKey, 0),
			Ideology = SelectBySeed(OfficerService.Ideologies, seedKey, 1),
			Specialty = SelectBySeed(OfficerService.Specialties, seedKey, 2),
			Flaw = SelectBySeed(OfficerService.Flaws, seedKey, 3),
			BiographySeed = SelectBySeed(OfficerService.BiographySeeds, seedKey, 4)
		});
		if (commissionedOfficer == null)
		{
			return null;
		}

		commissionedOfficer.OfficerID = $"remnant_{SanitizeIdentifier(seedKey)}";
		commissionedOfficer.Biography = BuildOfficerBiographyFromRemnant(remnant);
		commissionedOfficer.PersonalInventoryItemIDs = (remnant.PersonalInventoryItemIDs ?? new List<string>()).ToList();
		OfficerMissionLoadoutService.EnsureOfficerLoadout(commissionedOfficer);
		return commissionedOfficer;
	}

	private static RemnantRecord CreateRemnantFromOfficer(OfficerState officer)
	{
		return new RemnantRecord
		{
			RecordId = $"reserve_officer_{officer?.OfficerID ?? Guid.NewGuid().ToString("N")}",
			DisplayName = officer?.DisplayName ?? "Reserve Officer",
			Description = officer == null
				? "Former fleet officer awaiting reassignment."
				: string.IsNullOrWhiteSpace(officer.Biography)
					? "Former fleet officer awaiting reassignment."
					: officer.Biography,
			Notes = officer == null
				? string.Empty
				: $"Formerly assigned to {officer.ShipName}. Specialty: {officer.Specialty}.",
			MissionId = string.Empty,
			MissionTitle = "Fleet Reserve",
			RescuedOnTurn = 0,
			PortraitPath = officer?.PortraitPath ?? string.Empty,
			DefinitionPath = string.Empty,
			PersonalInventoryItemIDs = (officer?.PersonalInventoryItemIDs ?? new List<string>()).ToList(),
			StoredOfficerState = OfficerStateSaveData.FromRuntime(officer)
		};
	}

	private static string BuildRemnantSwapDescription(RemnantRecord remnant)
	{
		if (remnant?.StoredOfficerState != null)
		{
			List<string> parts = new List<string>();
			if (!string.IsNullOrWhiteSpace(remnant.StoredOfficerState.Biography))
			{
				parts.Add(remnant.StoredOfficerState.Biography.Trim());
			}

			if (!string.IsNullOrWhiteSpace(remnant.StoredOfficerState.Flaw))
			{
				parts.Add($"Watchpoint: {remnant.StoredOfficerState.Flaw}.");
			}

			return string.Join("\n", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
		}

		return BuildOfficerBiographyFromRemnant(remnant);
	}

	private static string BuildRemnantCommissionSummary(RemnantRecord remnant)
	{
		List<string> parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(remnant?.MissionTitle))
		{
			parts.Add(remnant.MissionTitle);
		}

		int carriedItems = remnant?.PersonalInventoryItemIDs?.Count ?? 0;
		if (carriedItems > 0)
		{
			parts.Add($"{carriedItems} carried item{(carriedItems == 1 ? string.Empty : "s")}");
		}

		return parts.Count == 0 ? "Ready for field commission" : string.Join(" | ", parts);
	}

	private static string BuildOfficerBiographyFromRemnant(RemnantRecord remnant)
	{
		List<string> parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(remnant?.Description))
		{
			parts.Add(remnant.Description.Trim());
		}

		if (!string.IsNullOrWhiteSpace(remnant?.Notes))
		{
			parts.Add(remnant.Notes.Trim());
		}

		if (!string.IsNullOrWhiteSpace(remnant?.MissionTitle))
		{
			parts.Add($"Recovered during {remnant.MissionTitle}.");
		}

		return parts.Count == 0
			? "A rescued remnant now stepping forward into fleet service."
			: string.Join(" ", parts);
	}

	private static string SelectBySeed(IReadOnlyList<string> options, string seedKey, int salt)
	{
		if (options == null || options.Count == 0)
		{
			return string.Empty;
		}

		int index = Mathf.Abs(ComputeSeedHash(seedKey, salt)) % options.Count;
		return options[index];
	}

	private static int ComputeSeedHash(string seedKey, int salt)
	{
		unchecked
		{
			int hash = 17 + salt;
			foreach (char character in seedKey ?? string.Empty)
			{
				hash = (hash * 31) + character;
			}

			return hash;
		}
	}

	private static string SanitizeIdentifier(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "reserve";
		}

		char[] characters = value.Trim().ToLowerInvariant().ToCharArray();
		for (int index = 0; index < characters.Length; index++)
		{
			if (!char.IsLetterOrDigit(characters[index]))
			{
				characters[index] = '_';
			}
		}

		return new string(characters);
	}

	private Button BuildPauseMenuButton(string text, Action onPressed, float width = 260f)
	{
		Button button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(width, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		button.Pressed += () => onPressed?.Invoke();
		return button;
	}

	private static StyleBoxFlat CreateOverlayPanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.11f, 0.96f),
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderColor = new Color(0.3f, 0.95f, 1f, 0.85f),
			ContentMarginLeft = 24,
			ContentMarginRight = 24,
			ContentMarginTop = 20,
			ContentMarginBottom = 20,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8
		};
	}

	private static string ResolveLoadedScenePath(GlobalData globalData, SaveGameSlotInfo selectedSave)
	{
		if (!string.IsNullOrWhiteSpace(selectedSave?.LastSavedScenePath))
		{
			return selectedSave.LastSavedScenePath;
		}

		if (!string.IsNullOrWhiteSpace(globalData?.LastSavedScenePath))
		{
			return globalData.LastSavedScenePath;
		}

		if (!string.IsNullOrWhiteSpace(globalData?.CurrentMissionScenePath))
		{
			return globalData.CurrentMissionScenePath;
		}

		if (globalData?.CurrentSectorStars?.Count > 0)
		{
			return "res://galactic_map.tscn";
		}

		return "res://exploration_battle.tscn";
	}

	private static string FormatSaveTimestamp(string savedAtUtc)
	{
		if (DateTime.TryParse(savedAtUtc, out DateTime parsed))
		{
			return parsed.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
		}

		return "Unknown";
	}

	private static string BuildSaveListLabel(SaveGameSlotInfo save)
	{
		if (save == null)
		{
			return string.Empty;
		}

		string label = save.DisplayName;
		if (save.IsAutoSave)
		{
			label += " [AUTOSAVE]";
		}
		else if (save.IsLegacySave)
		{
			label += " [QUICKSAVE]";
		}

		return label;
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
				TryPromptMissionForShip(ship);
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
