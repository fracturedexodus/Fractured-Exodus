using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap : Node2D
{
	private const string DefaultMissionId = "black_site_relay";
	private const float DefaultZoom = 0.52f;
	private const float MinZoom = 0.75f;
	private const float MaxZoom = 2.00f;
	private const float ZoomStep = 0.08f;
	private const float CameraPanSpeed = 720f;
	private const float CameraEdgePanMargin = 30f;
	private const float CameraKeyboardFollowEdgeMargin = 8f;
	private const int FogRevealRadius = 4;
	private const int ActorSortBias = 2;
	private const int RuntimePropSortBias = 4;
	private const int WallBaseSortBias = 1;
	private const int WallOccluderSortBias = 7;
	private const int FloorGridSortBias = 1;
	private const int MovementCursorSortBias = 5;
	private const int CombatEffectSortBias = 12;
	private const int CoverGhostZIndex = 160;
	private const float AmbientFloorDiamondWidthFactor = 0.42f;
	private const float AmbientFloorDiamondHeightFactor = 0.24f;
	private const string MedicalBedHealPropId = "medical_bed_heal";
	private const string EnemyLootDropPropId = "enemy_loot_drop";
	private const string EnemyLootDropScenePath = "res://Scenes/Missions/Props/LootCrateProp.tscn";
	private const string EnemyLootDropSpritePath = "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/props/crate_medium.png";
	private const string MissionMusicPath = "res://Sounds/Strike_the_Shield.mp3";
	private const string MissionHitSoundPath = "res://Sounds/795468__aulix24__grunt-3.ogg";
	private const string MissionPlayerShieldHitSoundPath = "res://Sounds/465541__steaq__sci-fi-shield-hit-ogg.ogg";
	private const string MissionOfficerLaserFireSoundPath = "res://Sounds/459781__metzik__laser-gun.wav";
	private const string MissionEnemyLaserFireSoundPath = "res://Sounds/169775__andromadax24__pulse-rifle.wav";
	private const float HostileRoamIntervalSeconds = 1.35f;
	private const float EscortFollowIntervalSeconds = 0.2f;
	private const string RelaySurvivorPodsPropId = "relay_survivor_pods";
	private const string MissionNpcPawnScenePath = "res://Scenes/Missions/Entities/MissionNpcPawn.tscn";
	private static readonly string[] RelaySurvivorDefinitionPaths =
	{
		"res://Data/Missions/Entities/Npcs/relay_survivor_01.tres",
		"res://Data/Missions/Entities/Npcs/relay_survivor_02.tres",
		"res://Data/Missions/Entities/Npcs/relay_survivor_03.tres"
	};

	private sealed class MissionCombatTurnEntry
	{
		public string CombatantId { get; init; } = string.Empty;
		public bool IsOfficer { get; init; }
		public int InitiativeScore { get; init; }
		public OfficerPawn Officer { get; init; }
		public MissionNpcPawn Enemy { get; init; }
	}

	private enum MissionInteractionMenuTargetKind
	{
		Door,
		Prop,
		Npc,
		StaticInteraction
	}

	private enum MissionPlayerCombatActionMode
	{
		Attack,
		Move
	}

	private sealed class MissionInteractionMenuTarget
	{
		public MissionInteractionMenuTargetKind Kind { get; init; }
		public string TargetKey { get; init; } = string.Empty;
		public string Title { get; init; } = string.Empty;
		public string Body { get; init; } = string.Empty;
		public Vector2 ScreenPosition { get; init; } = Vector2.Zero;
		public Vector2I BuildCell { get; init; } = Vector2I.Zero;
		public string DoorId { get; init; } = string.Empty;
		public MissionProp Prop { get; init; }
		public MissionNpcPawn Npc { get; init; }
		public MissionRoomBuilder.MarkerPlacement Interaction { get; init; }
	}

	private GlobalData _globalData;
	private MissionService _missionService;
	private MissionRuntimeState _missionState;
	private MissionTemplate _missionTemplate;
	private MissionUI _missionUi;
	private DialogueUI _dialogueUi;
	private AudioPlaybackService _audioPlaybackService;
	private AudioStreamPlayer _bgmPlayer;
	private AudioStreamPlayer _hitSfxPlayer;
	private AudioStreamPlayer _playerShieldHitSfxPlayer;
	private AudioStreamPlayer _officerLaserFireSfxPlayer;
	private AudioStreamPlayer _enemyLaserFireSfxPlayer;
	private AudioStream _missionHitSound;
	private AudioStream _missionPlayerShieldHitSound;
	private AudioStream _missionOfficerLaserFireSound;
	private AudioStream _missionEnemyLaserFireSound;
	private Node2D _isoWorld;
	private Node2D _characterLayer;
	private Node2D _wallLayer;
	private Node2D _staticPropLayer;
	private Camera2D _camera;
	private MissionRoomBuilder _roomBuilder;
	private TextureRect _backgroundBackdrop;
	private Sprite2D _backgroundFeatureSprite;
	private Node2D _ambientFloorLayer;
	private Node2D _movementGridLayer;
	private Node2D _movementCursorLayer;
	private Node2D _evacZoneLayer;
	private Node2D _combatEffectLayer;
	private SelectionBox _selectionBox;
	private Polygon2D _movementCursorFill;
	private Node2D _movementCursorOutlineRoot;
	private readonly List<Polygon2D> _movementCursorOutlineSegments = new List<Polygon2D>();
	private readonly List<OfficerPawn> _officerPawns = new List<OfficerPawn>();
	private readonly List<MissionNpcPawn> _missionNpcs = new List<MissionNpcPawn>();
	private readonly Dictionary<Vector2I, MissionNpcPawn> _missionNpcsByCell = new Dictionary<Vector2I, MissionNpcPawn>();
	private readonly Dictionary<Vector2I, MissionProp> _missionPropsByCell = new Dictionary<Vector2I, MissionProp>();
	private readonly Dictionary<string, Texture2D> _portraitTextureCache = new Dictionary<string, Texture2D>();
	private readonly HashSet<Vector2I> _blockedStaticPropCells = new HashSet<Vector2I>();
	private readonly Dictionary<string, MissionRoomBuilder.MarkerPlacement> _propPlacementsByInstanceId = new Dictionary<string, MissionRoomBuilder.MarkerPlacement>();
	private readonly MissionSpawner _missionSpawner = new MissionSpawner();
	private readonly HashSet<string> _consumedTriggerKeys = new HashSet<string>();
	private readonly HashSet<string> _engagedEnemyIds = new HashSet<string>();
	private readonly HashSet<string> _escortSurvivorIds = new HashSet<string>();
	private readonly HashSet<string> _extractedSurvivorIds = new HashSet<string>();
	private readonly HashSet<Vector2I> _exploredCells = new HashSet<Vector2I>();
	private readonly HashSet<Vector2I> _visibleCells = new HashSet<Vector2I>();
	private readonly HashSet<Vector2I> _exploredBuildCells = new HashSet<Vector2I>();
	private readonly HashSet<Vector2I> _visibleBuildCells = new HashSet<Vector2I>();
	private readonly HashSet<Vector2I> _visibleOccludableBuildCells = new HashSet<Vector2I>();
	private readonly List<Node2D> _evacZoneVisualRoots = new List<Node2D>();
	private readonly List<Polygon2D> _evacZoneHighlightPolygons = new List<Polygon2D>();
	private readonly List<Line2D> _evacZoneHighlightOutlines = new List<Line2D>();
	private readonly List<Line2D> _movementGridOutlines = new List<Line2D>();
	private readonly List<Polygon2D> _ambientFloorPolygons = new List<Polygon2D>();
	private readonly HashSet<string> _selectedOfficerIds = new HashSet<string>();
	private readonly HashSet<string> _selectedEscortSurvivorIds = new HashSet<string>();
	private string _selectedEscortSurvivorId = string.Empty;
	private int _selectedOfficerIndex;
	private bool _explorationPartyMovementEnabled;
	private bool _isPanning;
	private bool _isSelectionDragging;
	private Vector2 _lastMouseScreenPosition;
	private Vector2 _selectionDragStartWorldPosition;
	private string _pendingInteractionKey = string.Empty;
	private string _pendingInteractionOfficerId = string.Empty;
	private string _pendingDoorId = string.Empty;
	private string _pendingPropInstanceId = string.Empty;
	private string _pendingNpcId = string.Empty;
	private float _evacPulseClock;
	private readonly RandomNumberGenerator _combatRng = new RandomNumberGenerator();
	private readonly List<MissionCombatTurnEntry> _combatQueue = new List<MissionCombatTurnEntry>();
	private bool _combatActive;
	private int _combatRound = 1;
	private int _combatActiveIndex = -1;
	private string _pendingCombatMoveOfficerId = string.Empty;
	private string _pendingCombatMoveEscortSurvivorId = string.Empty;
	private string _pendingCombatAttackEnemyId = string.Empty;
	private int _pendingCombatMoveCost;
	private MissionNpcPawn _focusedEnemy;
	private bool _enemyTurnInProgress;
	private bool _missionGameOver;
	private Node2D _hoveredCombatActor;
	private float _hostileRoamClock;
	private float _escortFollowClock;
	private MissionProp _pendingStoryProp;
	private PropInteractionResult _pendingStoryResult;
	private PropInteractionContext _pendingStoryContext;
	private MissionProp _pendingMedicalBedProp;
	private string _pendingMedicalBedOfficerId = string.Empty;
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
	private MissionInteractionMenuTarget _activeInteractionMenuTarget;
	private string _activeInteractionMenuOfficerId = string.Empty;
	private MissionPlayerCombatActionMode _selectedCombatActionMode = MissionPlayerCombatActionMode.Attack;
	private bool _missionDepthSortingDirty = true;
	private bool _missionDepthSortingRefreshQueued;

	private int CombatAttackActionCost => MissionGridRules.StandardActionCost;
	private int CombatInteractionActionCost => MissionGridRules.StandardActionCost;

	public override void _Ready()
	{
		_combatRng.Randomize();
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		_missionService = new MissionService(_globalData);
		_audioPlaybackService = new AudioPlaybackService();
		_missionState = _missionService.GetCurrentMissionState();
		_isoWorld = GetNode<Node2D>("IsoWorld");
		_characterLayer = GetNode<Node2D>("IsoWorld/CharacterLayer");
		_wallLayer = GetNodeOrNull<Node2D>("IsoWorld/WallLayer");
		_staticPropLayer = GetNodeOrNull<Node2D>("IsoWorld/PropLayer");
		_camera = GetNode<Camera2D>("Camera2D");
		_roomBuilder = GetNode<MissionRoomBuilder>("IsoWorld/RoomBuilder");
		_missionUi = GetNode<MissionUI>("MissionUI");
		_dialogueUi = GetNode<DialogueUI>("DialogueUI");
		EnsureBackgroundNodes();
		BuildPauseMenuUI();
		BuildLoadGameMenuUI();

		if (_missionState == null || string.IsNullOrEmpty(_missionState.MissionID))
		{
			_missionState = _missionService.PrepareMission(DefaultMissionId, "res://exploration_battle.tscn", "Black Site Relay Beacon");
		}

		_missionTemplate = _missionService.GetTemplate(GetActiveMissionId());
		ApplyMissionTemplateToRoomBuilder();

		if (!_roomBuilder.BuildRoom())
		{
			GD.PushError($"Mission '{GetActiveMissionId()}' could not start because its layout failed strict validation.");
			ProcessMode = ProcessModeEnum.Disabled;
			return;
		}
		SpawnMissionProps();
		ApplyMissionBackground();
		BuildAmbientFloorShading();
		BuildMovementGridOverlay();
		BuildMovementCursorHighlight();
		EnsureSelectionBox();
		BuildEvacZoneHighlights();
		EnsureCombatEffectLayer();
		ConfigureMissionView();
		SetupMissionAudio();
		SpawnMissionNpcs();
		SpawnMissionOfficers();
		RestoreSavedMissionStateIfAvailable();
		FocusCameraOnAwayTeam();
		UpdateFogOfWar();
		WireUi();
		WireDialogue();
		UpdateSelectedOfficerDisplay();
		RefreshCombatHud();
		RefreshMissionDepthSortingIfDirty();
	}

	public override void _ExitTree()
	{
		_bgmPlayer?.Stop();
		_portraitTextureCache.Clear();

		if (_missionUi != null)
		{
			_missionUi.ExtractionOutcomeChosen -= OnExtractionOutcomeChosen;
			_missionUi.CombatEndTurnPressed -= OnCombatEndTurnPressed;
			_missionUi.CombatActionChosen -= OnCombatActionChosen;
			_missionUi.CombatWeaponSwapRequested -= OnCombatWeaponSwapRequested;
			_missionUi.ExplorationControlModeChosen -= OnExplorationControlModeChosen;
			_missionUi.ExplorationOfficerChosen -= OnExplorationOfficerChosen;
			_missionUi.ExplorationOfficerInventoryRequested -= OnExplorationOfficerInventoryRequested;
			_missionUi.ExplorationUnitFocusRequested -= OnExplorationUnitFocusRequested;
			_missionUi.OfficerInventoryWeaponEquipRequested -= OnOfficerInventoryWeaponEquipRequested;
			_missionUi.OfficerInventoryShieldEquipRequested -= OnOfficerInventoryShieldEquipRequested;
			_missionUi.MissionSaveConfirmed -= OnMissionSaveConfirmed;
			_missionUi.StoryEventConfirmed -= OnStoryEventConfirmed;
			_missionUi.ConfirmationAccepted -= OnConfirmationAccepted;
			_missionUi.ConfirmationCancelled -= OnConfirmationCancelled;
			_missionUi.InteractionMenuOptionChosen -= OnInteractionMenuOptionChosen;
			if (_missionUi.GameOverReturnButton != null)
			{
				_missionUi.GameOverReturnButton.Pressed -= ReturnToMainMenu;
			}
		}

		if (_dialogueUi != null)
		{
			_dialogueUi.ConversationEnded -= OnMissionConversationEnded;
			_dialogueUi.DialogueStateChanged -= OnMissionDialogueStateChanged;
		}
	}

	public override void _Process(double delta)
	{
		UpdateHeldOfficerMovement();
		UpdateCameraPan((float)delta);
		UpdateKeyboardMovementCameraFollow();
		UpdateSelectionDrag();
		UpdateMovementCursorHighlight();
		UpdateEvacZoneHighlightVisuals((float)delta);
		UpdateHostileRoaming((float)delta);
		UpdateEscortSurvivorBehavior((float)delta);
		UpdateCombatHoverSummary();
		UpdateMissionInteractionMenu();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_missionGameOver)
		{
			return;
		}

		if (_missionUi?.IsOfficerInventoryVisible ?? false)
		{
			if (@event is InputEventKey inventoryEscapeEvent
				&& inventoryEscapeEvent.Pressed
				&& !inventoryEscapeEvent.Echo
				&& inventoryEscapeEvent.Keycode == Key.Escape)
			{
				_missionUi.HideOfficerInventory();
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

		if ((_pauseMenuWrapper?.Visible ?? false)
			|| (_loadMenuWrapper?.Visible ?? false)
			|| (_missionUi?.IsMissionSavePromptVisible ?? false))
		{
			return;
		}

		if ((_dialogueUi != null && _dialogueUi.IsConversationOpen)
			|| (_missionUi?.IsStoryEventVisible ?? false)
			|| (_missionUi?.IsConfirmationVisible ?? false))
		{
			return;
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.F5)
			{
				QuickSaveMission();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.F6)
			{
				QuickLoadMission();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.Tab && !_combatActive)
			{
				CycleOfficerSelection();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.Key1 && !_combatActive)
			{
				SelectOfficer(0);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.Key2 && !_combatActive)
			{
				SelectOfficer(1);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (TryHandleOfficerMoveKey(keyEvent.Keycode))
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.O)
			{
				OfficerPawn activeOfficer = GetSelectedEscortSurvivor() == null ? GetSelectedOfficer() : null;
				if (activeOfficer != null && TryHandleDoorKeyAction(activeOfficer))
				{
					GetViewport().SetInputAsHandled();
					return;
				}
			}

			if (keyEvent.Keycode == Key.Equal || keyEvent.Keycode == Key.KpAdd)
			{
				AdjustZoom(-ZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.Minus || keyEvent.Keycode == Key.KpSubtract)
			{
				AdjustZoom(ZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseButton zoomButton && zoomButton.Pressed)
		{
			if (zoomButton.ButtonIndex == MouseButton.Middle)
			{
				_isPanning = true;
				_lastMouseScreenPosition = zoomButton.Position;
				GetViewport().SetInputAsHandled();
				return;
			}

			if (zoomButton.ButtonIndex == MouseButton.WheelUp)
			{
				AdjustZoom(-ZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (zoomButton.ButtonIndex == MouseButton.WheelDown)
			{
				AdjustZoom(ZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseButton rightMouseButton
			&& rightMouseButton.Pressed
			&& rightMouseButton.ButtonIndex == MouseButton.Right)
		{
			if (_missionUi?.IsMouseOverInteractionMenu() == true)
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			if (TryShowInteractionMenuAtMouse())
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			ClearInteractionMenu();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				if (_combatActive && !IsPlayerTurnActive())
				{
					GetViewport().SetInputAsHandled();
					return;
				}

				_isSelectionDragging = true;
				_selectionDragStartWorldPosition = GetGlobalMousePosition();
				if (_selectionBox != null)
				{
					_selectionBox.StartPos = _selectionDragStartWorldPosition;
					_selectionBox.EndPos = _selectionDragStartWorldPosition;
					_selectionBox.IsDragging = true;
					_selectionBox.QueueRedraw();
				}

				GetViewport().SetInputAsHandled();
				return;
			}

			if (_isSelectionDragging)
			{
				FinalizeLeftMouseAction();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseButton middleRelease && !middleRelease.Pressed && middleRelease.ButtonIndex == MouseButton.Middle)
		{
			_isPanning = false;
		}

		if (@event is InputEventMouseMotion motion && _isPanning && _camera != null)
		{
			Vector2 deltaScreen = motion.Position - _lastMouseScreenPosition;
			_camera.Position -= deltaScreen * _camera.Zoom;
			_lastMouseScreenPosition = motion.Position;
			GetViewport().SetInputAsHandled();
		}
	}

	private void WireUi()
	{
		if (_missionUi == null)
		{
			return;
		}

		_missionUi.SetMissionText(
			(_missionState?.MissionTitle ?? "Away Mission").ToUpper(),
			GetMissionObjectiveText(),
			GetMissionPromptText());
		_missionUi.ClearActionLog();
		_missionUi.ExtractionOutcomeChosen += OnExtractionOutcomeChosen;
		_missionUi.CombatEndTurnPressed += OnCombatEndTurnPressed;
		_missionUi.CombatActionChosen += OnCombatActionChosen;
		_missionUi.CombatWeaponSwapRequested += OnCombatWeaponSwapRequested;
		_missionUi.ExplorationControlModeChosen += OnExplorationControlModeChosen;
		_missionUi.ExplorationOfficerChosen += OnExplorationOfficerChosen;
		_missionUi.ExplorationOfficerInventoryRequested += OnExplorationOfficerInventoryRequested;
		_missionUi.ExplorationUnitFocusRequested += OnExplorationUnitFocusRequested;
		_missionUi.OfficerInventoryWeaponEquipRequested += OnOfficerInventoryWeaponEquipRequested;
		_missionUi.OfficerInventoryShieldEquipRequested += OnOfficerInventoryShieldEquipRequested;
		_missionUi.MissionSaveConfirmed += OnMissionSaveConfirmed;
		_missionUi.StoryEventConfirmed += OnStoryEventConfirmed;
		_missionUi.ConfirmationAccepted += OnConfirmationAccepted;
		_missionUi.ConfirmationCancelled += OnConfirmationCancelled;
		_missionUi.InteractionMenuOptionChosen += OnInteractionMenuOptionChosen;
		if (_missionUi.GameOverReturnButton != null)
		{
			_missionUi.GameOverReturnButton.Pressed += ReturnToMainMenu;
		}
		UpdateMissionCompletionActions();
	}

	private void WireDialogue()
	{
		if (_dialogueUi == null)
		{
			return;
		}

		_dialogueUi.ConversationEnded += OnMissionConversationEnded;
		_dialogueUi.DialogueStateChanged += OnMissionDialogueStateChanged;
	}

	private void HandleMissionGameOver()
	{
		if (_missionGameOver)
		{
			return;
		}

		_missionGameOver = true;
		_combatActive = false;
		_enemyTurnInProgress = false;
		AppendCombatLog("All deployed officers have fallen. The mission is lost.");
		_missionUi?.HideExtractionPrompt();
		_missionUi?.ShowMissionGameOver();
		RefreshCombatHud();
	}

	private void ReturnToMainMenu()
	{
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene("res://main_menu.tscn");
			return;
		}

		GetTree().ChangeSceneToFile("res://main_menu.tscn");
	}

	private void ReturnToMainMenuFromPause()
	{
		HidePauseMenus();
		ReturnToMainMenu();
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
		if (save.IsRecovered)
		{
			label += " [RECOVERED]";
		}

		return label;
	}

}
