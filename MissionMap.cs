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
	private const int CombatAttackActionCost = 1;
	private const int CombatInteractionActionCost = 1;
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
	private Line2D _movementCursorOutline;
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
	private readonly List<Node2D> _evacZoneVisualRoots = new List<Node2D>();
	private readonly List<Polygon2D> _evacZoneHighlightPolygons = new List<Polygon2D>();
	private readonly List<Line2D> _evacZoneHighlightOutlines = new List<Line2D>();
	private readonly List<Line2D> _movementGridOutlines = new List<Line2D>();
	private readonly List<Polygon2D> _ambientFloorPolygons = new List<Polygon2D>();
	private readonly HashSet<string> _selectedOfficerIds = new HashSet<string>();
	private readonly HashSet<string> _selectedEscortSurvivorIds = new HashSet<string>();
	private string _selectedEscortSurvivorId = string.Empty;
	private int _selectedOfficerIndex;
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

	public override void _Ready()
	{
		_combatRng.Randomize();
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		_missionService = new MissionService(_globalData);
		_audioPlaybackService = new AudioPlaybackService();
		_missionState = _missionService.GetCurrentMissionState();
		_isoWorld = GetNode<Node2D>("IsoWorld");
		_characterLayer = GetNode<Node2D>("IsoWorld/CharacterLayer");
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

		_roomBuilder?.BuildRoom();
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
		UpdateFogOfWar();
		WireUi();
		WireDialogue();
		UpdateSelectedOfficerDisplay();
		RefreshCombatHud();
	}

	public override void _ExitTree()
	{
		_bgmPlayer?.Stop();
		_portraitTextureCache.Clear();

		if (_missionUi != null)
		{
			_missionUi.ExtractionOutcomeChosen -= OnExtractionOutcomeChosen;
			_missionUi.CombatEndTurnPressed -= OnCombatEndTurnPressed;
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
		UpdateMissionDepthSorting();
	}

	private void SetupMissionAudio()
	{
		_bgmPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionMusic");
		if (_bgmPlayer == null)
		{
			_bgmPlayer = new AudioStreamPlayer
			{
				Name = "MissionMusic",
				VolumeDb = -14.0f
			};
			AddChild(_bgmPlayer);
		}
		else
		{
			_bgmPlayer.VolumeDb = -14.0f;
		}

		AudioStream missionMusic = _audioPlaybackService?.GetStream(MissionMusicPath)
			?? _audioPlaybackService?.GetMp3StreamFromFile(MissionMusicPath, true);
		if (missionMusic == null)
		{
			GD.PrintErr($"Mission music not found at {MissionMusicPath}.");
			return;
		}

		if (missionMusic is AudioStreamMP3 mp3Stream)
		{
			mp3Stream.Loop = true;
		}

		_audioPlaybackService?.TryPlayLoaded(_bgmPlayer, missionMusic);

		_hitSfxPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionHitSfx");
		if (_hitSfxPlayer == null)
		{
			_hitSfxPlayer = new AudioStreamPlayer
			{
				Name = "MissionHitSfx",
				VolumeDb = -9.0f
			};
			AddChild(_hitSfxPlayer);
		}
		else
		{
			_hitSfxPlayer.VolumeDb = -9.0f;
		}

		_missionHitSound = _audioPlaybackService?.GetStream(MissionHitSoundPath)
			?? _audioPlaybackService?.GetOggStreamFromFile(MissionHitSoundPath, false);
		if (_missionHitSound == null)
		{
			GD.PrintErr($"Mission hit sound not found at {MissionHitSoundPath}.");
		}

		_playerShieldHitSfxPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionPlayerShieldHitSfx");
		if (_playerShieldHitSfxPlayer == null)
		{
			_playerShieldHitSfxPlayer = new AudioStreamPlayer
			{
				Name = "MissionPlayerShieldHitSfx",
				VolumeDb = -8.0f
			};
			AddChild(_playerShieldHitSfxPlayer);
		}
		else
		{
			_playerShieldHitSfxPlayer.VolumeDb = -8.0f;
		}

		_missionPlayerShieldHitSound = _audioPlaybackService?.GetStream(MissionPlayerShieldHitSoundPath)
			?? _audioPlaybackService?.GetOggStreamFromFile(MissionPlayerShieldHitSoundPath, false);
		if (_missionPlayerShieldHitSound == null)
		{
			GD.PrintErr($"Mission player shield hit sound not found at {MissionPlayerShieldHitSoundPath}.");
		}

		_officerLaserFireSfxPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionOfficerLaserFireSfx");
		if (_officerLaserFireSfxPlayer == null)
		{
			_officerLaserFireSfxPlayer = new AudioStreamPlayer
			{
				Name = "MissionOfficerLaserFireSfx",
				VolumeDb = -7.0f
			};
			AddChild(_officerLaserFireSfxPlayer);
		}
		else
		{
			_officerLaserFireSfxPlayer.VolumeDb = -7.0f;
		}

		_missionOfficerLaserFireSound = _audioPlaybackService?.GetStream(MissionOfficerLaserFireSoundPath)
			?? _audioPlaybackService?.GetWavStreamFromFile(MissionOfficerLaserFireSoundPath, false);
		if (_missionOfficerLaserFireSound == null)
		{
			GD.PrintErr($"Mission officer laser fire sound not found at {MissionOfficerLaserFireSoundPath}.");
		}

		_enemyLaserFireSfxPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionEnemyLaserFireSfx");
		if (_enemyLaserFireSfxPlayer == null)
		{
			_enemyLaserFireSfxPlayer = new AudioStreamPlayer
			{
				Name = "MissionEnemyLaserFireSfx",
				VolumeDb = -7.5f
			};
			AddChild(_enemyLaserFireSfxPlayer);
		}
		else
		{
			_enemyLaserFireSfxPlayer.VolumeDb = -7.5f;
		}

		_missionEnemyLaserFireSound = _audioPlaybackService?.GetStream(MissionEnemyLaserFireSoundPath)
			?? _audioPlaybackService?.GetWavStreamFromFile(MissionEnemyLaserFireSoundPath, false);
		if (_missionEnemyLaserFireSound == null)
		{
			GD.PrintErr($"Mission enemy laser fire sound not found at {MissionEnemyLaserFireSoundPath}.");
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_missionGameOver)
		{
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

	private void ConfigureMissionView()
	{
		if (_camera != null)
		{
			Vector2 roomCenter = _roomBuilder != null
				? _isoWorld.ToGlobal(_roomBuilder.GetRoomCenterWorldPosition())
				: Vector2.Zero;
			_camera.Position = roomCenter;
			ApplyZoom(DefaultZoom);
		}
	}

	private void EnsureBackgroundNodes()
	{
		CanvasLayer backdropCanvas = GetNode<CanvasLayer>("BackdropCanvas");
		_backgroundBackdrop = backdropCanvas.GetNodeOrNull<TextureRect>("BackgroundTexture");
		if (_backgroundBackdrop == null)
		{
			_backgroundBackdrop = new TextureRect
			{
				Name = "BackgroundTexture",
				AnchorsPreset = (int)Control.LayoutPreset.FullRect,
				AnchorRight = 1f,
				AnchorBottom = 1f,
				GrowHorizontal = Control.GrowDirection.Both,
				GrowVertical = Control.GrowDirection.Both,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				Visible = false,
				Modulate = new Color(0.52f, 0.60f, 0.66f, 0.18f)
			};
			backdropCanvas.AddChild(_backgroundBackdrop);
			backdropCanvas.MoveChild(_backgroundBackdrop, 1);
		}

		Node2D backgroundLayer = _isoWorld.GetNodeOrNull<Node2D>("BackgroundArtLayer");
		if (backgroundLayer == null)
		{
			backgroundLayer = new Node2D
			{
				Name = "BackgroundArtLayer",
				ZIndex = -10
			};
			_isoWorld.AddChild(backgroundLayer);
			_isoWorld.MoveChild(backgroundLayer, 0);
		}

		_backgroundFeatureSprite = backgroundLayer.GetNodeOrNull<Sprite2D>("BackgroundFeature");
		if (_backgroundFeatureSprite == null)
		{
			_backgroundFeatureSprite = new Sprite2D
			{
				Name = "BackgroundFeature",
				Centered = true,
				Visible = false
			};
			backgroundLayer.AddChild(_backgroundFeatureSprite);
		}
	}

	private void ApplyMissionBackground()
	{
		MissionBackgroundDefinition definition = MissionBackgroundCatalog.GetById(_roomBuilder?.GetSelectedBackgroundId());

		if (_backgroundBackdrop != null)
		{
			_backgroundBackdrop.Texture = string.IsNullOrEmpty(definition.BackdropTexturePath)
				? null
				: GD.Load<Texture2D>(definition.BackdropTexturePath);
			_backgroundBackdrop.Visible = _backgroundBackdrop.Texture != null;
		}

		if (_backgroundFeatureSprite == null)
		{
			return;
		}

		_backgroundFeatureSprite.Texture = string.IsNullOrEmpty(definition.FeatureTexturePath)
			? null
			: GD.Load<Texture2D>(definition.FeatureTexturePath);
		_backgroundFeatureSprite.Visible = _backgroundFeatureSprite.Texture != null;
		if (!_backgroundFeatureSprite.Visible || _roomBuilder == null)
		{
			return;
		}

		_backgroundFeatureSprite.Scale = new Vector2(definition.FeatureScale, definition.FeatureScale);
		_backgroundFeatureSprite.Modulate = BlendColor(
			definition.FeatureModulate,
			new Color(0.74f, 0.84f, 0.88f, definition.FeatureModulate.A),
			0.34f);
		_backgroundFeatureSprite.Position = _roomBuilder.GetRoomCenterWorldPosition() + definition.FeatureOffset;
	}

	private void BuildAmbientFloorShading()
	{
		EnsureAmbientFloorLayer();
		if (_ambientFloorLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in _ambientFloorLayer.GetChildren())
		{
			_ambientFloorLayer.RemoveChild(child);
			child.QueueFree();
		}

		_ambientFloorPolygons.Clear();

		Vector2 tileStep = _roomBuilder.TileStep;
		Vector2[] diamondPoints =
		{
			new Vector2(0f, -tileStep.Y * AmbientFloorDiamondHeightFactor),
			new Vector2(tileStep.X * AmbientFloorDiamondWidthFactor, 0f),
			new Vector2(0f, tileStep.Y * AmbientFloorDiamondHeightFactor),
			new Vector2(-tileStep.X * AmbientFloorDiamondWidthFactor, 0f)
		};

		foreach (Vector2I buildCell in _roomBuilder.GetFloorCells())
		{
			Polygon2D polygon = new Polygon2D
			{
				Name = $"AmbientFloor_{buildCell.X}_{buildCell.Y}",
				Polygon = diamondPoints,
				Position = _roomBuilder.GetCellWorldPosition(buildCell.X, buildCell.Y, new Vector2(0f, tileStep.Y * 0.02f)),
				Color = Colors.Transparent
			};
			polygon.SetMeta("column", buildCell.X);
			polygon.SetMeta("row", buildCell.Y);
			_ambientFloorLayer.AddChild(polygon);
			_ambientFloorPolygons.Add(polygon);
		}
	}

	private void EnsureAmbientFloorLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_ambientFloorLayer = _isoWorld.GetNodeOrNull<Node2D>("AmbientFloorLayer");
		if (_ambientFloorLayer == null)
		{
			_ambientFloorLayer = new Node2D
			{
				Name = "AmbientFloorLayer",
				ZIndex = 0
			};
			_isoWorld.AddChild(_ambientFloorLayer);
		}

		Node floorLayer = _isoWorld.GetNodeOrNull<Node>("FloorLayer");
		int insertIndex = floorLayer != null ? floorLayer.GetIndex() + 1 : 1;
		_isoWorld.MoveChild(_ambientFloorLayer, insertIndex);
	}

	private void BuildEvacZoneHighlights()
	{
		EnsureEvacZoneLayer();
		if (_evacZoneLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in _evacZoneLayer.GetChildren())
		{
			_evacZoneLayer.RemoveChild(child);
			child.QueueFree();
		}

		_evacZoneVisualRoots.Clear();
		_evacZoneHighlightPolygons.Clear();
		_evacZoneHighlightOutlines.Clear();

		List<MissionRoomBuilder.MarkerPlacement> evacMarkers = _roomBuilder.GetMarkerPlacements()
			.Where(marker => marker.MarkerId == "evac_zone")
			.ToList();
		if (evacMarkers.Count == 0)
		{
			return;
		}

		Vector2 tileStep = _roomBuilder.TileStep;
		Vector2[] diamondPoints =
		{
			new Vector2(0f, -tileStep.Y * 0.28f),
			new Vector2(tileStep.X * 0.28f, 0f),
			new Vector2(0f, tileStep.Y * 0.28f),
			new Vector2(-tileStep.X * 0.28f, 0f)
		};
		Vector2[] innerDiamondPoints =
		{
			new Vector2(0f, -tileStep.Y * 0.16f),
			new Vector2(tileStep.X * 0.16f, 0f),
			new Vector2(0f, tileStep.Y * 0.16f),
			new Vector2(-tileStep.X * 0.16f, 0f)
		};

		foreach (MissionRoomBuilder.MarkerPlacement evacMarker in evacMarkers)
		{
			Node2D root = new Node2D
			{
				Name = $"EvacZone_{evacMarker.Cell.X}_{evacMarker.Cell.Y}",
				Position = _roomBuilder.GetCellWorldPosition(evacMarker.Cell.X, evacMarker.Cell.Y)
			};
			_evacZoneLayer.AddChild(root);
			_evacZoneVisualRoots.Add(root);

			Polygon2D outerFill = new Polygon2D
			{
				Polygon = diamondPoints,
				Color = new Color(0.18f, 0.92f, 0.56f, 0.18f)
			};
			root.AddChild(outerFill);
			_evacZoneHighlightPolygons.Add(outerFill);

			Line2D outline = new Line2D
			{
				Points = diamondPoints,
				Closed = true,
				Width = 4f,
				DefaultColor = new Color(0.48f, 1.00f, 0.72f, 0.82f)
			};
			root.AddChild(outline);
			_evacZoneHighlightOutlines.Add(outline);

			Polygon2D innerFill = new Polygon2D
			{
				Polygon = innerDiamondPoints,
				Color = new Color(0.62f, 1.00f, 0.82f, 0.22f)
			};
			root.AddChild(innerFill);
			_evacZoneHighlightPolygons.Add(innerFill);

			Label label = new Label
			{
				Text = "EVAC",
				Position = new Vector2(-70f, -tileStep.Y * 0.62f),
				Size = new Vector2(140f, 28f),
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			label.AddThemeFontSizeOverride("font_size", 18);
			label.AddThemeColorOverride("font_color", new Color(0.90f, 1.00f, 0.95f, 0.96f));
			label.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.12f, 0.08f, 0.92f));
			label.AddThemeConstantOverride("outline_size", 4);
			root.AddChild(label);
		}

		UpdateEvacZoneHighlightVisuals(0f);
	}

	private void BuildMovementGridOverlay()
	{
		EnsureMovementGridLayer();
		if (_movementGridLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in _movementGridLayer.GetChildren())
		{
			_movementGridLayer.RemoveChild(child);
			child.QueueFree();
		}

		_movementGridOutlines.Clear();

		Vector2 movementTileStep = _roomBuilder.TileStep / _roomBuilder.MovementSubdivision;
		Vector2[] diamondPoints =
		{
			new Vector2(0f, -movementTileStep.Y * 0.5f),
			new Vector2(movementTileStep.X * 0.5f, 0f),
			new Vector2(0f, movementTileStep.Y * 0.5f),
			new Vector2(-movementTileStep.X * 0.5f, 0f)
		};

		foreach (Vector2I buildCell in _roomBuilder.GetFloorCells())
		{
			foreach (Vector2I movementCell in _roomBuilder.GetMovementCellsForBuildCell(buildCell))
			{
				Line2D outline = new Line2D
				{
					Name = $"MovementGrid_{movementCell.X}_{movementCell.Y}",
					Points = diamondPoints,
					Closed = true,
					Width = 1.15f,
					DefaultColor = new Color(0.24f, 0.72f, 1.00f, 0.24f),
					Position = _roomBuilder.GetMovementCellWorldPosition(movementCell.X, movementCell.Y),
					ZIndex = _roomBuilder.GetCanvasSortOrderForMovementCell(movementCell, FloorGridSortBias),
					Visible = false
				};
				outline.ZAsRelative = false;
				outline.SetMeta("movement_cell_x", movementCell.X);
				outline.SetMeta("movement_cell_y", movementCell.Y);
				_movementGridLayer.AddChild(outline);
				_movementGridOutlines.Add(outline);
			}
		}

		UpdateMovementGridVisibility();
	}

	private void EnsureMovementGridLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_movementGridLayer = _isoWorld.GetNodeOrNull<Node2D>("MovementGridLayer");
		if (_movementGridLayer != null)
		{
			return;
		}

		_movementGridLayer = new Node2D
		{
			Name = "MovementGridLayer",
			ZIndex = 0
		};
		_isoWorld.AddChild(_movementGridLayer);

		Node ambientFloorLayer = _isoWorld.GetNodeOrNull<Node>("AmbientFloorLayer");
		Node floorLayer = _isoWorld.GetNodeOrNull<Node>("FloorLayer");
		int insertIndex = ambientFloorLayer != null
			? ambientFloorLayer.GetIndex() + 1
			: floorLayer != null ? floorLayer.GetIndex() + 1 : 1;
		_isoWorld.MoveChild(_movementGridLayer, insertIndex);
	}

	private void BuildMovementCursorHighlight()
	{
		EnsureMovementCursorLayer();
		if (_movementCursorLayer == null || _roomBuilder == null)
		{
			return;
		}

		Vector2 movementTileStep = _roomBuilder.TileStep / _roomBuilder.MovementSubdivision;
		Vector2[] diamondPoints =
		{
			new Vector2(0f, -movementTileStep.Y * 0.5f),
			new Vector2(movementTileStep.X * 0.5f, 0f),
			new Vector2(0f, movementTileStep.Y * 0.5f),
			new Vector2(-movementTileStep.X * 0.5f, 0f)
		};

		if (_movementCursorFill == null)
		{
			_movementCursorFill = new Polygon2D
			{
				Name = "MovementCursorFill",
				Color = new Color(0.26f, 0.78f, 1.00f, 0.12f),
				Visible = false
			};
			_movementCursorLayer.AddChild(_movementCursorFill);
		}
		_movementCursorFill.Polygon = diamondPoints;

		if (_movementCursorOutline == null)
		{
			_movementCursorOutline = new Line2D
			{
				Name = "MovementCursorOutline",
				Closed = true,
				Width = 3.1f,
				DefaultColor = new Color(0.56f, 0.92f, 1.00f, 0.88f),
				Visible = false
			};
			_movementCursorLayer.AddChild(_movementCursorOutline);
		}
		_movementCursorOutline.Points = diamondPoints;
	}

	private void EnsureMovementCursorLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_movementCursorLayer = _isoWorld.GetNodeOrNull<Node2D>("MovementCursorLayer");
		if (_movementCursorLayer != null)
		{
			return;
		}

		_movementCursorLayer = new Node2D
		{
			Name = "MovementCursorLayer",
			ZIndex = 2
		};
		_isoWorld.AddChild(_movementCursorLayer);

		Node movementGridLayer = _isoWorld.GetNodeOrNull<Node>("MovementGridLayer");
		int insertIndex = movementGridLayer != null ? movementGridLayer.GetIndex() + 1 : 2;
		_isoWorld.MoveChild(_movementCursorLayer, insertIndex);
	}

	private void UpdateMovementCursorHighlight()
	{
		if (_movementCursorFill == null || _movementCursorOutline == null || _roomBuilder == null || _isoWorld == null)
		{
			return;
		}

		if (_missionGameOver || (_dialogueUi?.IsConversationOpen ?? false) || (_missionUi?.IsStoryEventVisible ?? false))
		{
			_movementCursorFill.Visible = false;
			_movementCursorOutline.Visible = false;
			return;
		}

		Vector2 mousePosition = GetViewport().GetMousePosition();
		Vector2 screenSize = GetViewportRect().Size;
		if (mousePosition.X < 0f || mousePosition.Y < 0f || mousePosition.X > screenSize.X || mousePosition.Y > screenSize.Y)
		{
			_movementCursorFill.Visible = false;
			_movementCursorOutline.Visible = false;
			return;
		}

		if (_combatActive)
		{
			_movementCursorFill.Visible = false;
			_movementCursorOutline.Visible = false;
			return;
		}

		Vector2I hoveredCell = ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition())));
		if (!_roomBuilder.IsWalkableMovementCell(hoveredCell))
		{
			_movementCursorFill.Visible = false;
			_movementCursorOutline.Visible = false;
			return;
		}

		Vector2 hoverPosition = _roomBuilder.GetMovementCellWorldPosition(hoveredCell.X, hoveredCell.Y);
		_movementCursorFill.Position = hoverPosition;
		_movementCursorOutline.Position = hoverPosition;
		int cursorZIndex = _roomBuilder.GetCanvasSortOrderForMovementCell(hoveredCell, MovementCursorSortBias);
		_movementCursorFill.ZAsRelative = false;
		_movementCursorFill.ZIndex = cursorZIndex;
		_movementCursorOutline.ZAsRelative = false;
		_movementCursorOutline.ZIndex = cursorZIndex;
		_movementCursorFill.Visible = true;
		_movementCursorOutline.Visible = true;
	}

	private void UpdateMovementGridVisibility()
	{
		if (_movementGridLayer == null)
		{
			return;
		}

		_movementGridLayer.Visible = _combatActive;
		if (_combatActive)
		{
			ApplyFogToMovementGrid();
		}
		else
		{
			foreach (Line2D outline in _movementGridOutlines)
			{
				if (outline != null)
				{
					outline.Visible = false;
				}
			}
		}
	}

	private void EnsureEvacZoneLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_evacZoneLayer = _isoWorld.GetNodeOrNull<Node2D>("EvacZoneLayer");
		if (_evacZoneLayer != null)
		{
			return;
		}

		_evacZoneLayer = new Node2D
		{
			Name = "EvacZoneLayer",
			ZIndex = 3
		};
		_isoWorld.AddChild(_evacZoneLayer);
		_isoWorld.MoveChild(_evacZoneLayer, 1);
	}

	private void EnsureCombatEffectLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_combatEffectLayer = _isoWorld.GetNodeOrNull<Node2D>("CombatEffectLayer");
		if (_combatEffectLayer != null)
		{
			return;
		}

		_combatEffectLayer = new Node2D
		{
			Name = "CombatEffectLayer",
			ZIndex = 30
		};
		_isoWorld.AddChild(_combatEffectLayer);
	}

	private void UpdateEvacZoneHighlightVisuals(float delta)
	{
		if (_evacZoneHighlightPolygons.Count == 0 && _evacZoneHighlightOutlines.Count == 0)
		{
			return;
		}

		_evacPulseClock += delta;
		bool extractionAvailable = GetAvailableExtractionOptions().Count > 0;
		bool allOfficersOnEvac = AreAllOfficersOnEvacZone();
		float pulse = 0.5f + 0.5f * Mathf.Sin(_evacPulseClock * 2.4f);
		float emphasis = allOfficersOnEvac && extractionAvailable ? 1f : extractionAvailable ? 0.55f : 0.2f;
		float fillAlpha = 0.16f + (pulse * 0.16f * emphasis);
		float innerAlpha = 0.12f + (pulse * 0.22f * emphasis);
		float outlineAlpha = 0.48f + (pulse * 0.34f * emphasis);
		float scaleBoost = allOfficersOnEvac && extractionAvailable ? 1.05f + pulse * 0.05f : 1f + pulse * 0.025f;

		foreach (Node2D root in _evacZoneVisualRoots)
		{
			if (root != null)
			{
				root.Scale = new Vector2(scaleBoost, scaleBoost);
			}
		}

		for (int i = 0; i < _evacZoneHighlightPolygons.Count; i++)
		{
			Polygon2D polygon = _evacZoneHighlightPolygons[i];
			if (polygon == null)
			{
				continue;
			}

			bool isInner = (i % 2) == 1;
			polygon.Color = isInner
				? new Color(0.70f, 1.00f, 0.86f, innerAlpha)
				: new Color(0.22f, 0.96f, 0.60f, fillAlpha);
		}

		foreach (Line2D outline in _evacZoneHighlightOutlines)
		{
			if (outline != null)
			{
				outline.DefaultColor = new Color(0.56f, 1.00f, 0.76f, outlineAlpha);
				outline.Width = allOfficersOnEvac && extractionAvailable ? 5f : 4f;
			}
		}
	}

	private void AdjustZoom(float delta)
	{
		if (_camera == null)
		{
			return;
		}

		ApplyZoom(_camera.Zoom.X + delta);
	}

	private void ApplyZoom(float zoomValue)
	{
		if (_camera == null)
		{
			return;
		}

		float clampedZoom = Mathf.Clamp(zoomValue, MinZoom, MaxZoom);
		_camera.Zoom = new Vector2(clampedZoom, clampedZoom);
	}

	private void SpawnMissionOfficers()
	{
		_officerPawns.Clear();

		MissionSpawnContext spawnContext = new MissionSpawnContext
		{
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			RoomBuilder = _roomBuilder
		};
		_officerPawns.AddRange(_missionSpawner.SpawnPlayerOfficers(
			spawnContext,
			_characterLayer,
			GetMovementCellGlobalPosition,
			pawn =>
			{
				pawn.EnteredCell += OnOfficerEnteredCell;
				pawn.ReachedCell += OnOfficerReachedCell;
				pawn.CombatStateChanged += OnOfficerCombatStateChanged;
				pawn.Died += OnOfficerDied;
			}));

		SelectOfficer(0);
	}

	private void SpawnMissionNpcs()
	{
		_missionNpcs.Clear();
		_missionNpcsByCell.Clear();
		_escortSurvivorIds.Clear();
		_extractedSurvivorIds.Clear();

		MissionSpawnContext spawnContext = new MissionSpawnContext
		{
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			RoomBuilder = _roomBuilder
		};
		_missionNpcs.AddRange(_missionSpawner.SpawnMissionNpcs(
			spawnContext,
			_characterLayer,
			GetMovementCellGlobalPosition));
		foreach (MissionNpcPawn npc in _missionNpcs.Where(npc => npc != null))
		{
			npc.EnteredCell += OnMissionNpcEnteredCell;
			npc.ReachedCell += OnMissionNpcReachedCell;
			npc.CombatStateChanged += OnMissionNpcCombatStateChanged;
			npc.Died += OnMissionNpcDied;
			_missionNpcsByCell[npc.CurrentCell] = npc;
		}
	}

	private MissionNpcPawn SpawnRuntimeMissionNpc(MissionNpcDefinition definition, Vector2I movementCell)
	{
		if (definition == null || _characterLayer == null || _roomBuilder == null)
		{
			return null;
		}

		string scenePath = !string.IsNullOrWhiteSpace(definition.ScenePath)
			? definition.ScenePath
			: MissionNpcPawnScenePath;
		PackedScene npcScene = GD.Load<PackedScene>(scenePath);
		if (npcScene == null)
		{
			return null;
		}

		MissionNpcPawn npc = npcScene.Instantiate<MissionNpcPawn>();
		_characterLayer.AddChild(npc);
		npc.ApplyDefinition(definition);
		npc.SetGridCell(movementCell, GetMovementCellGlobalPosition(movementCell));
		npc.EnteredCell += OnMissionNpcEnteredCell;
		npc.ReachedCell += OnMissionNpcReachedCell;
		npc.CombatStateChanged += OnMissionNpcCombatStateChanged;
		npc.Died += OnMissionNpcDied;
		_missionNpcs.Add(npc);
		ReindexMissionNpcCells();
		return npc;
	}

	private void SpawnRelaySurvivors(Vector2I anchorBuildCell)
	{
		List<Vector2I> spawnCells = FindRelaySurvivorSpawnCells(anchorBuildCell, RelaySurvivorDefinitionPaths.Length);
		int spawnedCount = 0;
		for (int i = 0; i < RelaySurvivorDefinitionPaths.Length && i < spawnCells.Count; i++)
		{
			MissionNpcDefinition definition = GD.Load<MissionNpcDefinition>(RelaySurvivorDefinitionPaths[i]);
			if (definition == null)
			{
				continue;
			}

			MissionNpcPawn survivor = SpawnRuntimeMissionNpc(definition, spawnCells[i]);
			if (survivor == null || string.IsNullOrWhiteSpace(survivor.NpcId))
			{
				continue;
			}

			_escortSurvivorIds.Add(survivor.NpcId);
			spawnedCount++;
		}

		if (spawnedCount > 0)
		{
			AppendActionLog($"{spawnedCount} relay survivor{(spawnedCount == 1 ? string.Empty : "s")} join the away team and can now be guided to extraction.");
			UpdateMissionCompletionActions();
			UpdateFogOfWar();
		}
	}

	private List<Vector2I> FindRelaySurvivorSpawnCells(Vector2I anchorBuildCell, int count)
	{
		List<Vector2I> cells = new List<Vector2I>();
		if (_roomBuilder == null || count <= 0)
		{
			return cells;
		}

		Vector2I anchorMovementCell = GetMovementCell(anchorBuildCell);
		List<Vector2I> nearbyCells = _roomBuilder.GetReachableMovementCells(anchorMovementCell, 2)
			.Where(candidate => candidate != anchorMovementCell)
			.Where(candidate => _roomBuilder.IsWalkableMovementCell(candidate))
			.Where(candidate => !IsMovementCellBlockedByProp(candidate))
			.Where(candidate => !IsCellOccupiedByLivingActor(candidate))
			.OrderBy(candidate => GetTileDistance(anchorMovementCell, candidate))
			.ToList();

		foreach (Vector2I candidate in nearbyCells)
		{
			cells.Add(candidate);
			if (cells.Count >= count)
			{
				return cells;
			}
		}

		if (_roomBuilder.IsWalkableMovementCell(anchorMovementCell) && !IsMovementCellBlockedByProp(anchorMovementCell) && !IsCellOccupiedByLivingActor(anchorMovementCell))
		{
			cells.Add(anchorMovementCell);
		}

		return cells;
	}

	private void UpdateFogOfWar()
	{
		if (_roomBuilder == null)
		{
			return;
		}

		_visibleCells.Clear();
		_visibleBuildCells.Clear();
		_exploredBuildCells.Clear();
		foreach (OfficerPawn pawn in _officerPawns)
		{
			if (pawn == null)
			{
				continue;
			}

			foreach (Vector2I cell in _roomBuilder.GetReachableMovementCells(pawn.CurrentCell, FogRevealRadius))
			{
				_visibleCells.Add(cell);
				_exploredCells.Add(cell);
				Vector2I buildCell = GetBuildCell(cell);
				_visibleBuildCells.Add(buildCell);
				_exploredBuildCells.Add(buildCell);
			}
		}

		foreach (Vector2I exploredCell in _exploredCells)
		{
			_exploredBuildCells.Add(GetBuildCell(exploredCell));
		}

		RevealAdjacentDoorCells();

		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/FloorLayer"));
		ApplyFogToAmbientFloor();
		ApplyFogToMovementGrid();
		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/WallLayer"));
		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/PropLayer"));
		ApplyFogToMissionProps();
		ApplyFogToMissionNpcs();
		UpdateMissionDepthSorting();
		if (!IsAnyCombatActorMoving())
		{
			EvaluateCombatState();
		}
	}

	private void ApplyFogToLayer(Node2D layer)
	{
		if (layer == null)
		{
			return;
		}

		foreach (Node child in layer.GetChildren())
		{
			if (child is MissionDoor2D door)
			{
				Vector2I doorCell = door.Cell;
				door.SetVisualModulate(GetStructureFogColor(doorCell));
				continue;
			}

			if (child is not Sprite2D sprite)
			{
				continue;
			}

			Vector2I cell = new Vector2I(
				sprite.GetMeta("column", int.MinValue).AsInt32(),
				sprite.GetMeta("row", int.MinValue).AsInt32());
			if (cell.X == int.MinValue || cell.Y == int.MinValue)
			{
				continue;
			}

			Color baseColor = sprite.HasMeta("fog_base_modulate")
				? sprite.GetMeta("fog_base_modulate").AsColor()
				: sprite.Modulate;
			if (!sprite.HasMeta("fog_base_modulate"))
			{
				sprite.SetMeta("fog_base_modulate", baseColor);
			}

			sprite.Modulate = GetFogAdjustedColor(baseColor, cell);
		}
	}

	private void ApplyFogToAmbientFloor()
	{
		foreach (Polygon2D polygon in _ambientFloorPolygons)
		{
			if (polygon == null)
			{
				continue;
			}

			Vector2I cell = new Vector2I(
				polygon.GetMeta("column", int.MinValue).AsInt32(),
				polygon.GetMeta("row", int.MinValue).AsInt32());
			if (cell.X == int.MinValue || cell.Y == int.MinValue)
			{
				continue;
			}

			if (_visibleBuildCells.Contains(cell))
			{
				polygon.Visible = true;
				polygon.Color = new Color(0.04f, 0.10f, 0.11f, 0.18f);
			}
			else if (_exploredBuildCells.Contains(cell))
			{
				polygon.Visible = true;
				polygon.Color = new Color(0.01f, 0.03f, 0.04f, 0.10f);
			}
			else
			{
				polygon.Visible = false;
			}
		}
	}

	private void RevealAdjacentDoorCells()
	{
		if (_roomBuilder == null || _visibleBuildCells.Count == 0)
		{
			return;
		}

		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		HashSet<Vector2I> additionalVisibleCells = new HashSet<Vector2I>();
		foreach (Vector2I cell in _visibleBuildCells)
		{
			foreach (Vector2I direction in directions)
			{
				Vector2I candidate = cell + direction;
				if (_roomBuilder.TryGetDoorIdAtCell(candidate, out string doorId) && !string.IsNullOrWhiteSpace(doorId))
				{
					additionalVisibleCells.Add(candidate);
				}
			}
		}

		foreach (Vector2I cell in additionalVisibleCells)
		{
			_visibleBuildCells.Add(cell);
			_exploredBuildCells.Add(cell);
		}
	}

	private bool IsStructureCellVisible(Vector2I cell)
	{
		return _visibleBuildCells.Contains(cell);
	}

	private bool IsStructureCellExplored(Vector2I cell)
	{
		return _exploredBuildCells.Contains(cell);
	}

	private Color GetStructureFogColor(Vector2I cell)
	{
		return GetFogAdjustedColor(Colors.White, cell);
	}

	private Color GetFogAdjustedSpriteColor(Sprite2D sprite, Vector2I cell)
	{
		Color baseColor = sprite.HasMeta("fog_base_modulate")
			? sprite.GetMeta("fog_base_modulate").AsColor()
			: sprite.Modulate;
		if (!sprite.HasMeta("fog_base_modulate"))
		{
			sprite.SetMeta("fog_base_modulate", baseColor);
		}

		return GetFogAdjustedColor(baseColor, cell);
	}

	private Color GetFogAdjustedColor(Color baseColor, Vector2I cell)
	{
		if (_visibleBuildCells.Contains(cell))
		{
			return baseColor;
		}

		if (_exploredBuildCells.Contains(cell))
		{
			return MultiplyColor(baseColor, 0.38f);
		}

		return MultiplyColor(baseColor, 0.08f);
	}

	private static Color MultiplyColor(Color color, float factor)
	{
		return new Color(
			Mathf.Clamp(color.R * factor, 0f, 1f),
			Mathf.Clamp(color.G * factor, 0f, 1f),
			Mathf.Clamp(color.B * factor, 0f, 1f),
			color.A);
	}

	private static Color BlendColor(Color from, Color to, float weight)
	{
		return new Color(
			Mathf.Lerp(from.R, to.R, weight),
			Mathf.Lerp(from.G, to.G, weight),
			Mathf.Lerp(from.B, to.B, weight),
			Mathf.Lerp(from.A, to.A, weight));
	}

	private static Color GetOccludedStructureColor(Color baseColor)
	{
		Color cooledColor = BlendColor(baseColor, new Color(0.70f, 0.84f, 0.88f, baseColor.A), 0.28f);
		return new Color(
			cooledColor.R,
			cooledColor.G,
			cooledColor.B,
			Mathf.Clamp(baseColor.A * 0.24f, 0.10f, 0.48f));
	}

	private void ApplyFogToMissionNpcs()
	{
		foreach (MissionNpcPawn npc in _missionNpcs)
		{
			if (npc == null)
			{
				continue;
			}

			npc.SetFogVisibility(_visibleCells.Contains(npc.CurrentCell));
		}
	}

	private void ApplyFogToMissionProps()
	{
		foreach (KeyValuePair<Vector2I, MissionProp> kvp in _missionPropsByCell)
		{
			if (kvp.Value == null)
			{
				continue;
			}

			bool isVisible = _visibleBuildCells.Contains(kvp.Key);
			kvp.Value.SetFogVisibility(isVisible);
			kvp.Value.Modulate = isVisible ? Colors.White : new Color(1f, 1f, 1f, 0f);
		}
	}

	private void UpdateMissionDepthSorting()
	{
		if (_roomBuilder == null)
		{
			return;
		}

		foreach (OfficerPawn officer in _officerPawns.Where(officer => officer != null && !officer.IsDead))
		{
			officer.ZAsRelative = false;
			officer.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(GetBuildCell(officer.CurrentCell), ActorSortBias);
			officer.SetCoverOccluded(false, CoverGhostZIndex);
		}

		foreach (MissionNpcPawn npc in _missionNpcs.Where(npc => npc != null && !npc.IsDead && !npc.IsExtracted))
		{
			npc.ZAsRelative = false;
			npc.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(GetBuildCell(npc.CurrentCell), ActorSortBias);
			npc.SetCoverOccluded(false, CoverGhostZIndex);
		}

		foreach ((Vector2I cell, MissionProp prop) in _missionPropsByCell)
		{
			if (prop == null)
			{
				continue;
			}

			prop.ZAsRelative = false;
			prop.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(cell, RuntimePropSortBias);
			prop.SetCoverOccluded(false);
		}

		ApplyWallDepthSorting(GetNodeOrNull<Node2D>("IsoWorld/WallLayer"));
		ApplyDoorAndStaticPropDepthSorting(GetNodeOrNull<Node2D>("IsoWorld/PropLayer"));
	}

	private void ApplyWallDepthSorting(Node2D wallLayer)
	{
		if (wallLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in wallLayer.GetChildren())
		{
			if (child is not Sprite2D sprite)
			{
				continue;
			}

			string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
			Vector2I wallCell = new Vector2I(
				sprite.GetMeta("column", int.MinValue).AsInt32(),
				sprite.GetMeta("row", int.MinValue).AsInt32());
			if (string.IsNullOrWhiteSpace(tileId) || wallCell.X == int.MinValue || wallCell.Y == int.MinValue)
			{
				continue;
			}

			sprite.ZAsRelative = false;
			sprite.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(wallCell, WallBaseSortBias);
			sprite.Modulate = GetFogAdjustedSpriteColor(sprite, wallCell);

			string orientationSuffix = sprite.GetMeta("orientation_suffix", string.Empty).AsString();
			List<Vector2I> occludedCells = GetWallOccludedBuildCells(orientationSuffix, tileId, wallCell)
				.Where(IsAnyVisibleOccludableEntityInBuildCell)
				.Distinct()
				.ToList();
			if (occludedCells.Count == 0)
			{
				continue;
			}

			sprite.Modulate = GetOccludedStructureColor(sprite.Modulate);
			sprite.ZIndex = occludedCells
				.Select(cell => _roomBuilder.GetCanvasSortOrderForBuildCell(cell, WallOccluderSortBias))
				.Max();
			foreach (Vector2I occludedCell in occludedCells)
			{
				SetPropsInBuildCellOccluded(occludedCell);
			}
		}
	}

	private void ApplyDoorAndStaticPropDepthSorting(Node2D propLayer)
	{
		if (propLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in propLayer.GetChildren())
		{
			switch (child)
			{
				case MissionDoor2D door:
				{
					Vector2I doorCell = door.Cell;
					door.ZAsRelative = false;
					door.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(doorCell, WallBaseSortBias);
					Color doorColor = GetStructureFogColor(doorCell);
					List<Vector2I> occludedDoorCells = GetOccludedBuildCellsForOrientation(door.OrientationSuffix, doorCell)
						.Where(IsAnyVisibleOccludableEntityInBuildCell)
						.Distinct()
						.ToList();
					if (occludedDoorCells.Count > 0)
					{
						doorColor = GetOccludedStructureColor(doorColor);
						door.ZIndex = occludedDoorCells
							.Select(cell => _roomBuilder.GetCanvasSortOrderForBuildCell(cell, WallOccluderSortBias))
							.Max();
						foreach (Vector2I occludedDoorCell in occludedDoorCells)
						{
							SetPropsInBuildCellOccluded(occludedDoorCell);
						}
					}
					door.SetVisualModulate(doorColor);
					break;
				}
				case Sprite2D sprite:
				{
					string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
					Vector2I propCell = new Vector2I(
						sprite.GetMeta("column", int.MinValue).AsInt32(),
						sprite.GetMeta("row", int.MinValue).AsInt32());
					if (propCell.X == int.MinValue || propCell.Y == int.MinValue)
					{
						continue;
					}

					sprite.ZAsRelative = false;
					sprite.Modulate = GetFogAdjustedSpriteColor(sprite, propCell);
					string orientationSuffix = sprite.GetMeta("orientation_suffix", string.Empty).AsString();
					List<Vector2I> occludedPropCells = GetWallOccludedBuildCells(orientationSuffix, tileId, propCell)
						.Where(IsAnyVisibleOccludableEntityInBuildCell)
						.Distinct()
						.ToList();
					if (occludedPropCells.Count > 0)
					{
						sprite.Modulate = GetOccludedStructureColor(sprite.Modulate);
						sprite.ZIndex = occludedPropCells
							.Select(cell => _roomBuilder.GetCanvasSortOrderForBuildCell(cell, WallOccluderSortBias))
							.Max();
						foreach (Vector2I occludedPropCell in occludedPropCells)
						{
							SetPropsInBuildCellOccluded(occludedPropCell);
						}
					}
					else
					{
						sprite.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(propCell, RuntimePropSortBias);
					}
					break;
				}
			}
		}
	}

	private bool IsAnyVisibleOccludableEntityInBuildCell(Vector2I buildCell)
	{
		return _officerPawns.Any(officer => officer != null && !officer.IsDead && officer.Visible && GetBuildCell(officer.CurrentCell) == buildCell)
			|| _missionNpcs.Any(npc => npc != null && !npc.IsDead && npc.Visible && GetBuildCell(npc.CurrentCell) == buildCell)
			|| _missionPropsByCell.Any(entry => entry.Value != null && !entry.Value.IsConsumed && entry.Value.Visible && entry.Key == buildCell);
	}

	private void SetPropsInBuildCellOccluded(Vector2I buildCell)
	{
		foreach ((Vector2I cell, MissionProp prop) in _missionPropsByCell.Where(entry => entry.Value != null && entry.Key == buildCell))
		{
			prop.SetCoverOccluded(true);
		}
	}

	private IEnumerable<Vector2I> GetWallOccludedBuildCells(string orientationSuffix, string tileId, Vector2I wallCell)
	{
		if (string.IsNullOrWhiteSpace(orientationSuffix) && !TryGetWallOrientationFromTileId(tileId, out orientationSuffix))
		{
			yield break;
		}

		foreach (Vector2I occludedCell in GetOccludedBuildCellsForOrientation(orientationSuffix, wallCell))
		{
			yield return occludedCell;
		}
	}

	private static bool TryGetWallOrientationFromTileId(string tileId, out string orientationSuffix)
	{
		orientationSuffix = string.Empty;
		if (string.IsNullOrWhiteSpace(tileId))
		{
			return false;
		}

		if (tileId.Contains("_nw_", StringComparison.OrdinalIgnoreCase) || tileId.StartsWith("wall_nw", StringComparison.OrdinalIgnoreCase))
		{
			orientationSuffix = "nw";
			return true;
		}

		if (tileId.Contains("_ne_", StringComparison.OrdinalIgnoreCase) || tileId.StartsWith("wall_ne", StringComparison.OrdinalIgnoreCase))
		{
			orientationSuffix = "ne";
			return true;
		}

		if (tileId.Contains("_se_", StringComparison.OrdinalIgnoreCase) || tileId.StartsWith("wall_se", StringComparison.OrdinalIgnoreCase))
		{
			orientationSuffix = "se";
			return true;
		}

		if (tileId.Contains("_sw_", StringComparison.OrdinalIgnoreCase) || tileId.StartsWith("wall_sw", StringComparison.OrdinalIgnoreCase))
		{
			orientationSuffix = "sw";
			return true;
		}

		return false;
	}

	private static IEnumerable<Vector2I> GetOccludedBuildCellsForOrientation(string orientationSuffix, Vector2I wallCell)
	{
		switch (orientationSuffix)
		{
			case "se":
				yield return wallCell;
				yield return wallCell + new Vector2I(1, 0);
				yield return wallCell + new Vector2I(1, 1);
				yield break;
			case "sw":
				yield return wallCell;
				yield return wallCell + new Vector2I(0, 1);
				yield return wallCell + new Vector2I(1, 1);
				yield break;
		}
	}

	private void ApplyFogToMovementGrid()
	{
		if (!_combatActive)
		{
			foreach (Line2D outline in _movementGridOutlines)
			{
				if (outline != null)
				{
					outline.Visible = false;
				}
			}
			return;
		}

		foreach (Line2D outline in _movementGridOutlines)
		{
			if (outline == null)
			{
				continue;
			}

			Vector2I movementCell = new Vector2I(
				outline.GetMeta("movement_cell_x", int.MinValue).AsInt32(),
				outline.GetMeta("movement_cell_y", int.MinValue).AsInt32());
			if (movementCell.X == int.MinValue || movementCell.Y == int.MinValue)
			{
				continue;
			}

			if (_visibleCells.Contains(movementCell))
			{
				outline.Visible = true;
				outline.DefaultColor = new Color(0.30f, 0.76f, 1.00f, 0.30f);
			}
			else if (_exploredCells.Contains(movementCell))
			{
				outline.Visible = true;
				outline.DefaultColor = new Color(0.14f, 0.34f, 0.56f, 0.17f);
			}
			else
			{
				outline.Visible = false;
			}
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

	private void BuildPauseMenuUI()
	{
		CanvasLayer pauseLayer = new CanvasLayer { Layer = 176 };
		AddChild(pauseLayer);

		_pauseMenuWrapper = new CenterContainer();
		_pauseMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_pauseMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_pauseMenuWrapper.Visible = false;
		pauseLayer.AddChild(_pauseMenuWrapper);

		PanelContainer pausePanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(440f, 320f)
		};
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

		PanelContainer loadPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(720f, 560f)
		};
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

	private void OnMissionSaveConfirmed(string saveName)
	{
		if (_globalData == null)
		{
			return;
		}

		_globalData.CurrentMissionSaveState = BuildCurrentMissionSaveState();
		_globalData.SaveNamedGame(saveName, ResolveMissionScenePath());
		AppendActionLog($"Mission saved as {saveName}.");
	}

	private void QuickSaveMission()
	{
		if (_globalData == null)
		{
			return;
		}

		_globalData.CurrentMissionSaveState = BuildCurrentMissionSaveState();
		_globalData.SaveGame(false, ResolveMissionScenePath());
		AppendActionLog("Mission quicksaved.");
	}

	private void QuickLoadMission()
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
			AppendActionLog("No quicksave found.");
			return;
		}

		if (!_globalData.LoadGame(quicksave.SlotId))
		{
			AppendActionLog("Quickload failed.");
			return;
		}

		HidePauseMenus();
		_missionUi?.HideMissionSavePrompt();
		string scenePath = ResolveLoadedScenePath(_globalData, quicksave);
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(scenePath);
			return;
		}

		GetTree().ChangeSceneToFile(scenePath);
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

		if (_missionUi?.IsMissionSavePromptVisible == true)
		{
			_missionUi.HideMissionSavePrompt();
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
		_missionUi?.ShowMissionSavePrompt($"{(_missionState?.MissionTitle ?? "Mission").Trim()} Save");
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

	private MissionRuntimeSaveData BuildCurrentMissionSaveState()
	{
		return new MissionRuntimeSaveData
		{
			MissionId = GetActiveMissionId(),
			ScenePath = ResolveMissionScenePath(),
			CombatRound = _combatRound,
			CombatActive = _combatActive,
			CombatActiveIndex = _combatActiveIndex,
			FocusedEnemyId = _focusedEnemy?.NpcId ?? string.Empty,
			SelectedOfficerIndex = _selectedOfficerIndex,
			SelectedOfficerIds = _selectedOfficerIds.ToList(),
			SelectedEscortSurvivorPrimaryId = _selectedEscortSurvivorId,
			SelectedEscortSurvivorIds = _selectedEscortSurvivorIds.ToList(),
			ConsumedTriggerKeys = _consumedTriggerKeys.ToList(),
			EngagedEnemyIds = _engagedEnemyIds.ToList(),
			ExploredCells = _exploredCells.Select(Vector2ISaveData.FromVector2I).ToList(),
			Officers = _officerPawns
				.Where(pawn => pawn != null && !string.IsNullOrWhiteSpace(pawn.OfficerID))
				.Select(pawn => new MissionActorSaveData
				{
					ActorId = pawn.OfficerID,
					Cell = Vector2ISaveData.FromVector2I(pawn.CurrentCell),
					CurrentHP = pawn.CurrentHP,
					CurrentShields = pawn.CurrentShields,
					CurrentActions = pawn.CurrentActions,
					ActiveStatusEffectId = pawn.ActiveStatusEffectId,
					IsDead = pawn.IsDead
				})
				.ToList(),
			Npcs = _missionNpcs
				.Where(npc => npc != null && !string.IsNullOrWhiteSpace(npc.NpcId))
				.Select(npc => new MissionActorSaveData
				{
					ActorId = npc.NpcId,
					DefinitionPath = npc.DefinitionResourcePath,
					Cell = Vector2ISaveData.FromVector2I(npc.CurrentCell),
					CurrentHP = npc.CurrentHP,
					CurrentShields = npc.CurrentShields,
					CurrentActions = npc.CurrentActions,
					ActiveStatusEffectId = npc.ActiveStatusEffectId,
					IsDead = npc.IsDead,
					IsConsumed = npc.IsConsumed,
					IsExtracted = npc.IsExtracted
				})
				.ToList(),
			Props = _missionPropsByCell.Values
				.Where(prop => prop != null && !string.IsNullOrWhiteSpace(prop.PropInstanceId))
				.Distinct()
				.Select(prop => new MissionPropSaveData
				{
					PropInstanceId = prop.PropInstanceId,
					IsConsumed = prop.IsConsumed
				})
				.ToList(),
			Doors = (_roomBuilder?.GetDoorIds() ?? Enumerable.Empty<string>())
				.Where(doorId => !string.IsNullOrWhiteSpace(doorId))
				.Select(doorId => new MissionDoorSaveData
				{
					DoorId = doorId,
					IsOpen = _roomBuilder.IsDoorOpen(doorId)
				})
				.ToList(),
			CombatQueue = _combatQueue
				.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.CombatantId))
				.Select(entry => new MissionCombatTurnSaveData
				{
					CombatantId = entry.CombatantId,
					IsOfficer = entry.IsOfficer
				})
				.ToList()
		};
	}

	private void RestoreSavedMissionStateIfAvailable()
	{
		MissionRuntimeSaveData saveState = _globalData?.CurrentMissionSaveState;
		if (saveState == null || !IsCompatibleMissionSave(saveState))
		{
			return;
		}

		RestoreDoorStates(saveState);
		RestorePropStates(saveState);
		RestoreOfficerStates(saveState);
		EnsureSavedMissionNpcsExist(saveState);
		RestoreNpcStates(saveState);
		RestoreMissionCollections(saveState);
		RestoreMissionSelection(saveState);
		RestoreMissionCombatState(saveState);
		_pendingInteractionKey = string.Empty;
		_pendingInteractionOfficerId = string.Empty;
		_pendingDoorId = string.Empty;
		_pendingPropInstanceId = string.Empty;
		_pendingNpcId = string.Empty;
		_pendingCombatMoveOfficerId = string.Empty;
		_pendingCombatAttackEnemyId = string.Empty;
		_pendingCombatMoveCost = 0;
		_enemyTurnInProgress = false;
		_pendingStoryProp = null;
		_pendingStoryResult = null;
		_pendingStoryContext = null;
		_pendingMedicalBedProp = null;
		_pendingMedicalBedOfficerId = string.Empty;
		UpdateMovementGridVisibility();
	}

	private bool IsCompatibleMissionSave(MissionRuntimeSaveData saveState)
	{
		return saveState != null
			&& !string.IsNullOrWhiteSpace(saveState.MissionId)
			&& string.Equals(saveState.MissionId, GetActiveMissionId(), StringComparison.Ordinal);
	}

	private void RestoreDoorStates(MissionRuntimeSaveData saveState)
	{
		if (_roomBuilder == null)
		{
			return;
		}

		foreach (MissionDoorSaveData doorState in saveState.Doors ?? new List<MissionDoorSaveData>())
		{
			if (doorState == null || string.IsNullOrWhiteSpace(doorState.DoorId))
			{
				continue;
			}

			_roomBuilder.TrySetDoorOpen(doorState.DoorId, doorState.IsOpen, false);
		}
	}

	private void RestorePropStates(MissionRuntimeSaveData saveState)
	{
		foreach (MissionPropSaveData propState in saveState.Props ?? new List<MissionPropSaveData>())
		{
			if (propState == null || string.IsNullOrWhiteSpace(propState.PropInstanceId))
			{
				continue;
			}

			MissionProp prop = _missionPropsByCell.Values.FirstOrDefault(candidate => candidate != null && candidate.PropInstanceId == propState.PropInstanceId);
			prop?.ApplySavedConsumptionState(propState.IsConsumed);
		}
	}

	private void RestoreOfficerStates(MissionRuntimeSaveData saveState)
	{
		foreach (MissionActorSaveData officerState in saveState.Officers ?? new List<MissionActorSaveData>())
		{
			if (officerState == null || string.IsNullOrWhiteSpace(officerState.ActorId) || officerState.Cell == null)
			{
				continue;
			}

			OfficerPawn pawn = _officerPawns.FirstOrDefault(candidate => candidate != null && candidate.OfficerID == officerState.ActorId);
			if (pawn == null)
			{
				continue;
			}

			Vector2I cell = officerState.Cell.ToVector2I();
			pawn.SetGridCell(cell, GetMovementCellGlobalPosition(cell));
			pawn.ApplySavedRuntimeState(
				officerState.CurrentHP,
				officerState.CurrentShields,
				officerState.CurrentActions,
				officerState.ActiveStatusEffectId,
				officerState.IsDead);
		}
	}

	private void RestoreNpcStates(MissionRuntimeSaveData saveState)
	{
		foreach (MissionActorSaveData npcState in saveState.Npcs ?? new List<MissionActorSaveData>())
		{
			if (npcState == null || string.IsNullOrWhiteSpace(npcState.ActorId) || npcState.Cell == null)
			{
				continue;
			}

			MissionNpcPawn npc = _missionNpcs.FirstOrDefault(candidate => candidate != null && candidate.NpcId == npcState.ActorId);
			if (npc == null)
			{
				continue;
			}

			Vector2I cell = npcState.Cell.ToVector2I();
			npc.SetGridCell(cell, GetMovementCellGlobalPosition(cell));
			npc.ApplySavedRuntimeState(
				npcState.CurrentHP,
				npcState.CurrentShields,
				npcState.CurrentActions,
				npcState.ActiveStatusEffectId,
				npcState.IsDead,
				npcState.IsConsumed,
				npcState.IsExtracted);
		}

		ReindexMissionNpcCells();
	}

	private void EnsureSavedMissionNpcsExist(MissionRuntimeSaveData saveState)
	{
		foreach (MissionActorSaveData npcState in saveState?.Npcs ?? new List<MissionActorSaveData>())
		{
			if (npcState == null
				|| string.IsNullOrWhiteSpace(npcState.ActorId)
				|| string.IsNullOrWhiteSpace(npcState.DefinitionPath)
				|| _missionNpcs.Any(candidate => candidate != null && candidate.NpcId == npcState.ActorId))
			{
				continue;
			}

			MissionNpcDefinition definition = GD.Load<MissionNpcDefinition>(npcState.DefinitionPath);
			if (definition == null)
			{
				continue;
			}

			MissionNpcPawn spawnedNpc = SpawnRuntimeMissionNpc(definition, npcState.Cell?.ToVector2I() ?? Vector2I.Zero);
			if (spawnedNpc == null)
			{
				continue;
			}

			if (IsEscortSurvivorId(spawnedNpc.NpcId))
			{
				_escortSurvivorIds.Add(spawnedNpc.NpcId);
				if (npcState.IsExtracted)
				{
					_extractedSurvivorIds.Add(spawnedNpc.NpcId);
				}
			}
		}
	}

	private void RestoreMissionCollections(MissionRuntimeSaveData saveState)
	{
		_escortSurvivorIds.Clear();
		_extractedSurvivorIds.Clear();
		foreach (MissionActorSaveData npcState in saveState.Npcs ?? new List<MissionActorSaveData>())
		{
			if (npcState == null || !IsEscortSurvivorId(npcState.ActorId))
			{
				continue;
			}

			_escortSurvivorIds.Add(npcState.ActorId);
			if (npcState.IsExtracted)
			{
				_extractedSurvivorIds.Add(npcState.ActorId);
			}
		}

		_consumedTriggerKeys.Clear();
		foreach (string key in saveState.ConsumedTriggerKeys ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(key))
			{
				_consumedTriggerKeys.Add(key);
			}
		}

		_engagedEnemyIds.Clear();
		foreach (string enemyId in saveState.EngagedEnemyIds ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(enemyId))
			{
				_engagedEnemyIds.Add(enemyId);
			}
		}

		_exploredCells.Clear();
		foreach (Vector2ISaveData exploredCell in saveState.ExploredCells ?? new List<Vector2ISaveData>())
		{
			if (exploredCell != null)
			{
				_exploredCells.Add(exploredCell.ToVector2I());
			}
		}
	}

	private void RestoreMissionSelection(MissionRuntimeSaveData saveState)
	{
		_selectedOfficerIndex = Mathf.Clamp(saveState.SelectedOfficerIndex, 0, Mathf.Max(0, _officerPawns.Count - 1));
		_selectedOfficerIds.Clear();
		_selectedEscortSurvivorIds.Clear();
		foreach (string officerId in saveState.SelectedOfficerIds ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(officerId)
				&& _officerPawns.Any(pawn => pawn != null && !pawn.IsDead && pawn.OfficerID == officerId))
			{
				_selectedOfficerIds.Add(officerId);
			}
		}

		foreach (string survivorId in saveState.SelectedEscortSurvivorIds ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(survivorId)
				&& GetAliveEscortSurvivors().Any(survivor => survivor != null && survivor.NpcId == survivorId))
			{
				_selectedEscortSurvivorIds.Add(survivorId);
			}
		}

		_selectedEscortSurvivorId = !string.IsNullOrWhiteSpace(saveState.SelectedEscortSurvivorPrimaryId)
			&& _selectedEscortSurvivorIds.Contains(saveState.SelectedEscortSurvivorPrimaryId)
			? saveState.SelectedEscortSurvivorPrimaryId
			: string.Empty;
		NormalizeFriendlySelectionState();
		UpdateOfficerSelectionVisuals();
	}

	private void RestoreMissionCombatState(MissionRuntimeSaveData saveState)
	{
		_combatRound = Mathf.Max(1, saveState.CombatRound);
		_combatActive = saveState.CombatActive && _engagedEnemyIds.Count > 0 && GetAliveOfficers().Any();
		_focusedEnemy = _missionNpcs.FirstOrDefault(npc => npc != null && npc.NpcId == saveState.FocusedEnemyId && !npc.IsDead);
		_combatQueue.Clear();
		foreach (MissionCombatTurnSaveData turnState in saveState.CombatQueue ?? new List<MissionCombatTurnSaveData>())
		{
			if (turnState == null || string.IsNullOrWhiteSpace(turnState.CombatantId))
			{
				continue;
			}

			if (turnState.IsOfficer)
			{
				OfficerPawn officer = _officerPawns.FirstOrDefault(candidate => candidate != null && candidate.OfficerID == turnState.CombatantId && !candidate.IsDead);
				if (officer != null)
				{
					_combatQueue.Add(new MissionCombatTurnEntry
					{
						CombatantId = officer.OfficerID,
						IsOfficer = true,
						InitiativeScore = 0,
						Officer = officer
					});
				}
				continue;
			}

			MissionNpcPawn enemy = _missionNpcs.FirstOrDefault(candidate => candidate != null && candidate.NpcId == turnState.CombatantId && !candidate.IsDead && !candidate.IsExtracted);
			if (enemy != null)
			{
				_combatQueue.Add(new MissionCombatTurnEntry
				{
					CombatantId = enemy.NpcId,
					IsOfficer = false,
					InitiativeScore = 0,
					Enemy = enemy
				});
			}
		}

		if (_combatActive && _combatQueue.Count == 0)
		{
			RebuildCombatQueue();
		}

		_combatActiveIndex = _combatActive
			? Mathf.Clamp(saveState.CombatActiveIndex, -1, _combatQueue.Count - 1)
			: -1;
		if (!_combatActive)
		{
			_combatQueue.Clear();
		}
	}

	private string ResolveMissionScenePath()
	{
		if (!string.IsNullOrWhiteSpace(_missionState?.ScenePath))
		{
			return _missionState.ScenePath;
		}

		if (GetTree()?.CurrentScene != null && !string.IsNullOrWhiteSpace(GetTree().CurrentScene.SceneFilePath))
		{
			return GetTree().CurrentScene.SceneFilePath;
		}

		return "res://black_site_relay.tscn";
	}

	private void EnsureSelectionBox()
	{
		_selectionBox = GetNodeOrNull<SelectionBox>("SelectionBox");
		if (_selectionBox != null)
		{
			return;
		}

		_selectionBox = new SelectionBox
		{
			Name = "SelectionBox",
			ZIndex = 200
		};
		AddChild(_selectionBox);
	}

	private void SelectOfficer(int index)
	{
		if (_officerPawns.Count == 0)
		{
			return;
		}

		_selectedOfficerIndex = Mathf.Clamp(index, 0, _officerPawns.Count - 1);
		if (_officerPawns[_selectedOfficerIndex]?.IsDead == true)
		{
			int livingIndex = _officerPawns.FindIndex(pawn => pawn != null && !pawn.IsDead);
			if (livingIndex >= 0)
			{
				_selectedOfficerIndex = livingIndex;
			}
		}

		OfficerPawn selectedOfficer = _officerPawns[_selectedOfficerIndex];
		if (selectedOfficer == null || selectedOfficer.IsDead || string.IsNullOrWhiteSpace(selectedOfficer.OfficerID))
		{
			return;
		}

		SetSelectedFriendlyUnits(new[] { selectedOfficer }, null, selectedOfficer);
	}

	private void SetSelectedOfficers(IEnumerable<OfficerPawn> officers, OfficerPawn primaryOfficer = null)
	{
		SetSelectedFriendlyUnits(officers, null, primaryOfficer);
	}

	private void SetSelectedFriendlyUnits(IEnumerable<OfficerPawn> officers, IEnumerable<MissionNpcPawn> survivors, object primaryUnit = null)
	{
		List<OfficerPawn> selectedOfficers = officers?
			.Where(officer => officer != null && !officer.IsDead && !string.IsNullOrWhiteSpace(officer.OfficerID))
			.Distinct()
			.ToList() ?? new List<OfficerPawn>();
		List<MissionNpcPawn> selectedSurvivors = survivors?
			.Where(survivor => IsActiveEscortSurvivor(survivor) && !string.IsNullOrWhiteSpace(survivor.NpcId))
			.Distinct()
			.ToList() ?? new List<MissionNpcPawn>();
		if (selectedOfficers.Count == 0 && selectedSurvivors.Count == 0)
		{
			return;
		}

		_selectedOfficerIds.Clear();
		_selectedEscortSurvivorIds.Clear();
		foreach (OfficerPawn officer in selectedOfficers)
		{
			_selectedOfficerIds.Add(officer.OfficerID);
		}

		foreach (MissionNpcPawn survivor in selectedSurvivors)
		{
			_selectedEscortSurvivorIds.Add(survivor.NpcId);
		}

		_selectedEscortSurvivorId = string.Empty;
		switch (primaryUnit)
		{
			case OfficerPawn primaryOfficer when _selectedOfficerIds.Contains(primaryOfficer.OfficerID):
			{
				int primaryIndex = _officerPawns.IndexOf(primaryOfficer);
				if (primaryIndex >= 0)
				{
					_selectedOfficerIndex = primaryIndex;
				}

				break;
			}
			case MissionNpcPawn primarySurvivor when _selectedEscortSurvivorIds.Contains(primarySurvivor.NpcId):
				_selectedEscortSurvivorId = primarySurvivor.NpcId;
				break;
		}

		NormalizeFriendlySelectionState();
		UpdateOfficerSelectionVisuals();
		UpdateSelectedOfficerDisplay();
		RefreshCombatHud();
	}

	private void NormalizeFriendlySelectionState()
	{
		_selectedOfficerIds.RemoveWhere(officerId => string.IsNullOrWhiteSpace(officerId)
			|| !_officerPawns.Any(pawn => pawn != null && !pawn.IsDead && pawn.OfficerID == officerId));
		_selectedEscortSurvivorIds.RemoveWhere(survivorId => string.IsNullOrWhiteSpace(survivorId)
			|| !GetAliveEscortSurvivors().Any(survivor => survivor != null && survivor.NpcId == survivorId));

		if (!string.IsNullOrWhiteSpace(_selectedEscortSurvivorId) && !_selectedEscortSurvivorIds.Contains(_selectedEscortSurvivorId))
		{
			_selectedEscortSurvivorId = string.Empty;
		}

		OfficerPawn indexedOfficer = GetSelectedOfficer();
		if (_selectedOfficerIds.Count == 0 && _selectedEscortSurvivorIds.Count == 0)
		{
			OfficerPawn fallbackOfficer = indexedOfficer ?? _officerPawns.FirstOrDefault(pawn => pawn != null && !pawn.IsDead);
			if (fallbackOfficer != null)
			{
				_selectedOfficerIndex = _officerPawns.IndexOf(fallbackOfficer);
				_selectedOfficerIds.Add(fallbackOfficer.OfficerID);
				return;
			}

			MissionNpcPawn fallbackSurvivor = GetAliveEscortSurvivors().FirstOrDefault();
			if (fallbackSurvivor != null)
			{
				_selectedEscortSurvivorIds.Add(fallbackSurvivor.NpcId);
				_selectedEscortSurvivorId = fallbackSurvivor.NpcId;
			}

			return;
		}

		if (!string.IsNullOrWhiteSpace(_selectedEscortSurvivorId))
		{
			return;
		}

		OfficerPawn selectedOfficer = indexedOfficer;
		if (selectedOfficer != null && _selectedOfficerIds.Contains(selectedOfficer.OfficerID))
		{
			return;
		}

		OfficerPawn firstSelectedOfficer = _officerPawns.FirstOrDefault(pawn => pawn != null && !pawn.IsDead && _selectedOfficerIds.Contains(pawn.OfficerID));
		if (firstSelectedOfficer != null)
		{
			_selectedOfficerIndex = _officerPawns.IndexOf(firstSelectedOfficer);
			return;
		}

		MissionNpcPawn firstSelectedSurvivor = GetAliveEscortSurvivors()
			.FirstOrDefault(survivor => !string.IsNullOrWhiteSpace(survivor.NpcId) && _selectedEscortSurvivorIds.Contains(survivor.NpcId));
		if (firstSelectedSurvivor != null)
		{
			_selectedEscortSurvivorId = firstSelectedSurvivor.NpcId;
		}
	}

	private void UpdateOfficerSelectionVisuals()
	{
		for (int i = 0; i < _officerPawns.Count; i++)
		{
			OfficerPawn officer = _officerPawns[i];
			if (officer != null)
			{
				bool isSelected = !officer.IsDead
					&& !string.IsNullOrWhiteSpace(officer.OfficerID)
					&& _selectedOfficerIds.Contains(officer.OfficerID);
				officer.SetSelected(isSelected);
			}
		}

		foreach (MissionNpcPawn npc in _missionNpcs.Where(candidate => candidate != null))
		{
			bool isSelected = IsActiveEscortSurvivor(npc)
				&& !string.IsNullOrWhiteSpace(npc.NpcId)
				&& _selectedEscortSurvivorIds.Contains(npc.NpcId);
			npc.SetSelected(isSelected);
		}
	}

	private void CycleOfficerSelection()
	{
		List<object> controllableUnits = GetControllableUnitsForSelection();
		if (controllableUnits.Count == 0)
		{
			return;
		}

		int currentIndex = GetCurrentSelectionIndex(controllableUnits);
		int nextIndex = (currentIndex + 1 + controllableUnits.Count) % controllableUnits.Count;
		SelectFriendlyUnit(controllableUnits[nextIndex]);
	}

	private OfficerPawn GetSelectedOfficer()
	{
		if (_selectedOfficerIndex < 0 || _selectedOfficerIndex >= _officerPawns.Count)
		{
			return null;
		}

		OfficerPawn pawn = _officerPawns[_selectedOfficerIndex];
		return pawn != null && !pawn.IsDead ? pawn : null;
	}

	private List<OfficerPawn> GetSelectedOfficers()
	{
		List<OfficerPawn> selected = _officerPawns
			.Where(pawn => pawn != null && !pawn.IsDead && !string.IsNullOrWhiteSpace(pawn.OfficerID) && _selectedOfficerIds.Contains(pawn.OfficerID))
			.ToList();
		if (selected.Count > 0)
		{
			return selected;
		}

		OfficerPawn activeOfficer = GetSelectedOfficer();
		if (activeOfficer == null || _selectedEscortSurvivorIds.Count > 0)
		{
			return new List<OfficerPawn>();
		}

		return new List<OfficerPawn> { activeOfficer };
	}

	private void SelectEscortSurvivor(MissionNpcPawn survivor)
	{
		if (!IsActiveEscortSurvivor(survivor) || string.IsNullOrWhiteSpace(survivor.NpcId))
		{
			return;
		}

		SetSelectedFriendlyUnits(null, new[] { survivor }, survivor);
	}

	private MissionNpcPawn GetSelectedEscortSurvivor()
	{
		if (string.IsNullOrWhiteSpace(_selectedEscortSurvivorId))
		{
			return null;
		}

		MissionNpcPawn survivor = _missionNpcs.FirstOrDefault(candidate => candidate != null && candidate.NpcId == _selectedEscortSurvivorId);
		return IsActiveEscortSurvivor(survivor) ? survivor : null;
	}

	private List<MissionNpcPawn> GetSelectedEscortSurvivors()
	{
		List<MissionNpcPawn> selected = GetAliveEscortSurvivors()
			.Where(survivor => !string.IsNullOrWhiteSpace(survivor.NpcId) && _selectedEscortSurvivorIds.Contains(survivor.NpcId))
			.ToList();
		if (selected.Count > 0)
		{
			return selected;
		}

		MissionNpcPawn primarySurvivor = GetSelectedEscortSurvivor();
		return primarySurvivor != null ? new List<MissionNpcPawn> { primarySurvivor } : new List<MissionNpcPawn>();
	}

	private object GetPrimarySelectedFriendlyUnit()
	{
		MissionNpcPawn selectedSurvivor = GetSelectedEscortSurvivor();
		if (selectedSurvivor != null)
		{
			return selectedSurvivor;
		}

		return GetSelectedOfficer();
	}

	private List<object> GetSelectedFriendlyUnits()
	{
		List<object> selectedUnits = GetControllableUnitsForSelection()
			.Where(unit => unit switch
			{
				OfficerPawn officer => !string.IsNullOrWhiteSpace(officer.OfficerID) && _selectedOfficerIds.Contains(officer.OfficerID),
				MissionNpcPawn survivor => !string.IsNullOrWhiteSpace(survivor.NpcId) && _selectedEscortSurvivorIds.Contains(survivor.NpcId),
				_ => false
			})
			.ToList();
		object primaryUnit = GetPrimarySelectedFriendlyUnit();
		if (primaryUnit != null && selectedUnits.Remove(primaryUnit))
		{
			selectedUnits.Insert(0, primaryUnit);
		}

		if (selectedUnits.Count > 0)
		{
			return selectedUnits;
		}

		return primaryUnit != null ? new List<object> { primaryUnit } : new List<object>();
	}

	private List<MissionCombatantSummary> GetSelectedFriendlySummaries()
	{
		return GetSelectedFriendlyUnits()
			.Select(unit => unit switch
			{
				OfficerPawn officer => BuildOfficerSummary(officer),
				MissionNpcPawn survivor => BuildEnemySummary(survivor),
				_ => null
			})
			.Where(summary => summary != null)
			.ToList();
	}

	private List<object> GetControllableUnitsForSelection()
	{
		List<object> units = new List<object>();
		units.AddRange(GetAliveOfficers().Cast<object>());
		units.AddRange(GetAliveEscortSurvivors().OrderBy(npc => npc.DisplayName).Cast<object>());
		return units;
	}

	private int GetCurrentSelectionIndex(List<object> units)
	{
		object selectedUnit = GetPrimarySelectedFriendlyUnit();
		return selectedUnit != null ? units.IndexOf(selectedUnit) : -1;
	}

	private void SelectFriendlyUnit(object unit)
	{
		switch (unit)
		{
			case OfficerPawn officer:
			{
				int officerIndex = _officerPawns.IndexOf(officer);
				if (officerIndex >= 0)
				{
					SelectOfficer(officerIndex);
				}

				break;
			}
			case MissionNpcPawn survivor:
				SelectEscortSurvivor(survivor);
				break;
		}
	}

	private void UpdateSelectedOfficerDisplay()
	{
		if (_missionUi == null)
		{
			return;
		}

		List<object> selectedUnits = GetSelectedFriendlyUnits();
		_missionUi.SetExplorationSelectionInfo(GetSelectedFriendlySummaries(), !_combatActive && !_missionGameOver);
		if (selectedUnits.Count == 0)
		{
			_missionUi.SetExplorationSelectionInfo(Array.Empty<MissionCombatantSummary>(), false);
			return;
		}

		object primaryUnit = selectedUnits[0];
		if (selectedUnits.Count > 1)
		{
			switch (primaryUnit)
			{
				case OfficerPawn officer:
					_missionUi.SetSelectedOfficer(
						$"{officer.OfficerName} (+{selectedUnits.Count - 1})",
						$"{selectedUnits.Count} UNITS SELECTED",
						officer.Specialty);
					return;
				case MissionNpcPawn survivor:
					_missionUi.SetSelectedOfficer(
						$"{survivor.DisplayName} (+{selectedUnits.Count - 1})",
						$"{selectedUnits.Count} UNITS SELECTED",
						"GUIDE TO EVAC");
					return;
			}
		}

		switch (primaryUnit)
		{
			case MissionNpcPawn survivor:
				_missionUi.SetSelectedOfficer(survivor.DisplayName, $"ESCORT {CampaignText.RemnantsLabel.ToUpperInvariant()}", "GUIDE TO EVAC");
				return;
			case OfficerPawn officer:
				_missionUi.SetSelectedOfficer(officer.OfficerName, officer.ShipName, officer.Specialty);
				return;
		}

		_missionUi.SetExplorationSelectionInfo(Array.Empty<MissionCombatantSummary>(), false);
	}

	private MissionOutcome BuildOutcome(string outcomeId)
	{
		string missionId = GetActiveMissionId();
		MissionOutcome outcome = new MissionOutcome
		{
			MissionID = missionId,
			OutcomeID = outcomeId,
			IsSuccess = true
		};
		outcome.FallenOfficerShipNames = _officerPawns
			.Where(pawn => pawn != null && pawn.IsDead)
			.Select(pawn => pawn.ShipName)
			.Where(shipName => !string.IsNullOrWhiteSpace(shipName))
			.Distinct()
			.ToList();

		List<OfficerState> officers = (_missionState?.ParticipatingShipNames ?? new List<string>())
			.Select(shipName => _globalData?.ShipOfficers != null && _globalData.ShipOfficers.TryGetValue(shipName, out OfficerState officer) ? officer : null)
			.Where(officer => officer != null)
			.ToList();

		if (missionId == "outpost_smuggler_exchange")
		{
			if (outcomeId == "deal_cut")
			{
				outcome.Reward.RawMaterials = 35;
				outcome.Reward.EnergyCores = 1;
				outcome.Reward.AncientTech = 2;
				outcome.FlagsToSet.Add("smuggler_exchange_deal_cut");
				outcome.Reward.CodexEntryIds.Add("broker_contract_terms");

				foreach (OfficerState officer in officers)
				{
					int delta = 0;
					if (officer.Ideology == "TechnoReclamation") delta += 1;
					if (officer.Archetype == "Pragmatist") delta += 1;
					if (officer.Archetype == "Scholar") delta += 1;
					if (officer.Ideology == "Humanitarian") delta -= 1;
					if (delta != 0) outcome.ApprovalChanges[officer.OfficerID] = delta;
				}
			}
			else
			{
				outcome.Reward.RawMaterials = 90;
				outcome.Reward.EnergyCores = 2;
				outcome.FlagsToSet.Add("smuggler_exchange_contraband_seized");
				outcome.Reward.CodexEntryIds.Add("smuggler_seizure_report");

				foreach (OfficerState officer in officers)
				{
					int delta = 0;
					if (officer.Archetype == "Pragmatist") delta += 1;
					if (officer.Specialty == "Security") delta += 1;
					if (officer.Ideology == "Humanitarian") delta -= 1;
					if (officer.Archetype == "Idealist") delta -= 1;
					if (delta != 0) outcome.ApprovalChanges[officer.OfficerID] = delta;
				}
			}

			return outcome;
		}

		if (outcomeId == "survivors_saved")
		{
			outcome.Reward.RawMaterials = 70;
			outcome.Reward.EnergyCores = 1;
			outcome.PopulationSaved = GetExtractedEscortSurvivorCount();
			outcome.RescuedRemnants = BuildRescuedRemnantRecords();
			outcome.FlagsToSet.Add("relay_survivors_saved");
			outcome.Reward.CodexEntryIds.Add("relay_survivor_registry");

			foreach (OfficerState officer in officers)
			{
				int delta = 0;
				if (officer.Ideology == "Humanitarian") delta += 2;
				if (officer.Archetype == "Idealist") delta += 1;
				if (officer.Specialty == "Medical Triage") delta += 1;
				if (officer.Ideology == "TechnoReclamation") delta -= 2;
				if (officer.Archetype == "Scholar") delta -= 1;
				if (delta != 0) outcome.ApprovalChanges[officer.OfficerID] = delta;
			}
		}
		else
		{
			outcome.Reward.EnergyCores = 2;
			outcome.Reward.AncientTech = 2;
			outcome.FlagsToSet.Add("relay_archive_secured");
			outcome.Reward.FleetItemIds.Add("custodian_archive_shard");
			outcome.Reward.CodexEntryIds.Add("custodian_archive_shard");

			foreach (OfficerState officer in officers)
			{
				int delta = 0;
				if (officer.Ideology == "TechnoReclamation") delta += 2;
				if (officer.Archetype == "Scholar") delta += 1;
				if (officer.Archetype == "Pragmatist") delta += 1;
				if (officer.Ideology == "Humanitarian") delta -= 2;
				if (officer.Archetype == "Idealist") delta -= 1;
				if (delta != 0) outcome.ApprovalChanges[officer.OfficerID] = delta;
			}
		}

		return outcome;
	}

	private List<RemnantRecord> BuildRescuedRemnantRecords()
	{
		string missionId = GetActiveMissionId();
		string missionTitle = !string.IsNullOrWhiteSpace(_missionTemplate?.Title)
			? _missionTemplate.Title
			: _missionState?.MissionTitle ?? missionId;
		return _missionNpcs
			.Where(npc => npc != null
				&& IsEscortSurvivor(npc)
				&& !string.IsNullOrWhiteSpace(npc.NpcId)
				&& _extractedSurvivorIds.Contains(npc.NpcId))
			.OrderBy(npc => npc.DisplayName)
			.Select(npc => new RemnantRecord
			{
				RecordId = $"{missionId}:{npc.NpcId}",
				DisplayName = npc.DisplayName,
				Description = string.IsNullOrWhiteSpace(npc.Description) ? "Recovered civilian from a completed away mission." : npc.Description,
				Notes = npc.Notes,
				MissionId = missionId,
				MissionTitle = missionTitle,
				RescuedOnTurn = Mathf.Max(1, _globalData?.CurrentTurn ?? 1),
				PortraitPath = npc.PortraitPath,
				DefinitionPath = npc.DefinitionResourcePath
			})
			.ToList();
	}

	private string GetActiveMissionId()
	{
		return string.IsNullOrEmpty(_missionState?.MissionID) ? DefaultMissionId : _missionState.MissionID;
	}

	private void ApplyMissionTemplateToRoomBuilder()
	{
		if (_roomBuilder == null || _missionTemplate == null)
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(_missionTemplate.LayoutResourcePath))
		{
			_roomBuilder.LayoutResourcePath = _missionTemplate.LayoutResourcePath;
		}
	}

	private string GetPrimaryOutcomeId()
	{
		return string.IsNullOrWhiteSpace(_missionTemplate?.PrimaryOutcomeId) ? "survivors_saved" : _missionTemplate.PrimaryOutcomeId;
	}

	private string GetSecondaryOutcomeId()
	{
		return string.IsNullOrWhiteSpace(_missionTemplate?.SecondaryOutcomeId) ? "archive_secured" : _missionTemplate.SecondaryOutcomeId;
	}

	private string GetMissionObjectiveText()
	{
		return string.IsNullOrWhiteSpace(_missionTemplate?.ObjectiveText)
			? "OBJECTIVE: Investigate the relay, assess the survivors, and decide what to save."
			: _missionTemplate.ObjectiveText;
	}

	private string GetBaseMissionPromptText()
	{
		string prompt = string.IsNullOrWhiteSpace(_missionTemplate?.PromptText)
			? "Controls: left click an officer to select, left click a floor tile to move, WASD to step the selected officer, TAB or 1-2 to switch officers, middle mouse drag or screen-edge hover to pan, mouse wheel or +/- to zoom."
			: _missionTemplate.PromptText;
		if (GetAliveEscortSurvivors().Any())
		{
			prompt += $" Rescued {CampaignText.RemnantsLabel.ToLowerInvariant()} can be selected and moved toward evac just like the away team.";
		}

		return prompt;
	}

	private string GetMissionPromptText()
	{
		string basePrompt = GetBaseMissionPromptText();
		if (_combatActive)
		{
			return $"{basePrompt} Combat is active: click a visible hostile to attack with officers, click the ground to reposition your active unit, and use END TURN when that unit is done.";
		}

		List<MissionExtractionOption> extractionOptions = GetAvailableExtractionOptions();
		bool allOfficersOnEvac = AreAllOfficersOnEvacZone();
		if (!allOfficersOnEvac)
		{
			return $"{basePrompt} Complete a valid mission path, then rally every surviving officer on the evac zone to extract.";
		}

		if (extractionOptions.Count == 0)
		{
			return $"{basePrompt} Your officers are assembled at evac, but no mission outcome is ready yet.";
		}

		if (extractionOptions.Count == 1)
		{
			return $"{basePrompt} All officers are on the evac zone. Extraction is ready for {extractionOptions[0].DisplayText.ToUpper()}.";
		}

		return $"{basePrompt} All officers are on the evac zone. Multiple extraction outcomes are available; choose how the mission resolves.";
	}

	private void RefreshMissionPrompt()
	{
		if (_missionUi?.PromptLabel != null)
		{
			_missionUi.PromptLabel.Text = GetMissionPromptText();
		}
	}

	private bool IsOutcomeReady(string outcomeId)
	{
		if (outcomeId == GetPrimaryOutcomeId())
		{
			return AreRequiredFlagsSatisfied(_missionTemplate?.PrimaryOutcomeRequiredFlags)
				&& GetExtractedEscortSurvivorCount() > 0
				&& !GetAliveEscortSurvivors().Any()
				&& AreBlockedFlagsClear(_missionTemplate?.PrimaryOutcomeBlockedFlags);
		}

		if (outcomeId == GetSecondaryOutcomeId())
		{
			return AreRequiredFlagsSatisfied(_missionTemplate?.SecondaryOutcomeRequiredFlags)
				&& AreBlockedFlagsClear(_missionTemplate?.SecondaryOutcomeBlockedFlags);
		}

		return false;
	}

	private string GetOutcomeDisplayName(string outcomeId)
	{
		if (outcomeId == GetPrimaryOutcomeId())
		{
			return string.IsNullOrWhiteSpace(_missionTemplate?.PrimaryActionText) ? "SAVE SURVIVORS" : _missionTemplate.PrimaryActionText;
		}

		if (outcomeId == GetSecondaryOutcomeId())
		{
			return string.IsNullOrWhiteSpace(_missionTemplate?.SecondaryActionText) ? "SECURE ARCHIVE" : _missionTemplate.SecondaryActionText;
		}

		return outcomeId;
	}

	private void OnExtractionOutcomeChosen(string outcomeId)
	{
		if (string.IsNullOrWhiteSpace(outcomeId) || !IsOutcomeReady(outcomeId) || !AreAllOfficersOnEvacZone())
		{
			return;
		}

		CompleteMission(BuildOutcome(outcomeId));
	}

	private void CompleteMission(MissionOutcome outcome)
	{
		_missionService?.ApplyOutcome(outcome);
		_missionService?.ReturnToMissionSource(this);
	}

	private void ReturnWithoutOutcome()
	{
		_globalData?.ClearCurrentMissionState();
		_missionService?.ReturnToMissionSource(this);
	}

	private void UpdateCameraPan(float delta)
	{
		if (_camera == null || _isPanning || _isSelectionDragging)
		{
			return;
		}

		Vector2 input = Vector2.Zero;
		Vector2 mousePosition = GetViewport().GetMousePosition();
		Vector2 screenSize = GetViewportRect().Size;

		if (mousePosition.X >= 0f && mousePosition.X <= screenSize.X && mousePosition.Y >= 0f && mousePosition.Y <= screenSize.Y)
		{
			if (mousePosition.X < CameraEdgePanMargin)
			{
				input.X -= 1f;
			}
			else if (mousePosition.X > screenSize.X - CameraEdgePanMargin)
			{
				input.X += 1f;
			}

			if (mousePosition.Y < CameraEdgePanMargin)
			{
				input.Y -= 1f;
			}
			else if (mousePosition.Y > screenSize.Y - CameraEdgePanMargin)
			{
				input.Y += 1f;
			}
		}

		if (input == Vector2.Zero)
		{
			return;
		}

		_camera.Position += input.Normalized() * CameraPanSpeed * delta * (1.0f / _camera.Zoom.X);
	}

	private void UpdateKeyboardMovementCameraFollow()
	{
		if (_camera == null || _isPanning || _isSelectionDragging || GetHeldOfficerMoveDirection() == Vector2I.Zero)
		{
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		List<Node2D> movingSelectedUnits = GetSelectedFriendlyUnits()
			.Select(unit => unit as Node2D)
			.Where(unit => unit != null && unit switch
			{
				OfficerPawn officer => officer.IsMoving,
				MissionNpcPawn survivor => survivor.IsMoving,
				_ => false
			})
			.ToList();
		if (movingSelectedUnits.Count == 0)
		{
			return;
		}

		bool reachedScreenEdge = movingSelectedUnits.Any(unit =>
		{
			Vector2 screenPosition = GetViewport().GetCanvasTransform() * unit.GlobalPosition;
			return screenPosition.X <= CameraKeyboardFollowEdgeMargin
				|| screenPosition.X >= viewportSize.X - CameraKeyboardFollowEdgeMargin
				|| screenPosition.Y <= CameraKeyboardFollowEdgeMargin
				|| screenPosition.Y >= viewportSize.Y - CameraKeyboardFollowEdgeMargin;
		});
		if (!reachedScreenEdge)
		{
			return;
		}

		Vector2 focusPosition = Vector2.Zero;
		foreach (Node2D unit in movingSelectedUnits)
		{
			focusPosition += unit.GlobalPosition;
		}

		_camera.Position = focusPosition / movingSelectedUnits.Count;
	}

	private void UpdateSelectionDrag()
	{
		if (!_isSelectionDragging || _selectionBox == null)
		{
			return;
		}

		_selectionBox.EndPos = GetGlobalMousePosition();
		_selectionBox.QueueRedraw();
	}

	private void FinalizeLeftMouseAction()
	{
		Rect2 selectionRect = new Rect2(_selectionDragStartWorldPosition, GetGlobalMousePosition() - _selectionDragStartWorldPosition).Abs();
		_isSelectionDragging = false;
		if (_selectionBox != null)
		{
			_selectionBox.IsDragging = false;
			_selectionBox.QueueRedraw();
		}

		ClearInteractionMenu();

		if (!_combatActive && selectionRect.Area >= 100f)
		{
			SelectControllableUnitsInRect(selectionRect);
			return;
		}

		ProcessLeftClickAction();
	}

	private void ProcessLeftClickAction()
	{
		if (_combatActive && !IsPlayerTurnActive())
		{
			return;
		}

		if (TrySelectControllableUnitAtMouse())
		{
			return;
		}

		List<object> selectedUnits = GetSelectedFriendlyUnits();
		if (!_combatActive && selectedUnits.Count > 1 && TryMoveSelectedFriendlyGroup())
		{
			return;
		}

		OfficerPawn activeOfficer = GetSelectedOfficer();
		MissionNpcPawn activeSurvivor = GetSelectedEscortSurvivor();
		if (activeOfficer == null && activeSurvivor == null)
		{
			return;
		}

		if (activeSurvivor != null)
		{
			TryMoveSelectedEscortSurvivor();
			return;
		}

		if (TryHandleInteractionClick(activeOfficer))
		{
			return;
		}

		if (_combatActive && TryHandleCombatAttackClick(activeOfficer))
		{
			return;
		}

		TryMoveSelectedOfficer();
	}

	private void SelectControllableUnitsInRect(Rect2 selectionRect)
	{
		if (_combatActive)
		{
			return;
		}

		List<OfficerPawn> officersInRect = _officerPawns
			.Where(officer => officer != null && !officer.IsDead && selectionRect.HasPoint(officer.GlobalPosition))
			.ToList();
		List<MissionNpcPawn> survivorsInRect = GetAliveEscortSurvivors()
			.Where(survivor => selectionRect.HasPoint(survivor.GlobalPosition))
			.ToList();
		if (officersInRect.Count == 0 && survivorsInRect.Count == 0)
		{
			return;
		}

		object primaryUnit = officersInRect.Cast<object>().Concat(survivorsInRect).FirstOrDefault();
		SetSelectedFriendlyUnits(officersInRect, survivorsInRect, primaryUnit);
	}

	private bool TryHandleOfficerMoveKey(Key keycode)
	{
		Vector2I direction = keycode switch
		{
			Key.W => new Vector2I(0, -1),
			Key.S => new Vector2I(0, 1),
			Key.A => new Vector2I(-1, 0),
			Key.D => new Vector2I(1, 0),
			_ => Vector2I.Zero
		};

		if (direction == Vector2I.Zero)
		{
			return false;
		}

		if (!_combatActive && GetSelectedFriendlyUnits().Count > 1)
		{
			return TryMoveSelectedFriendlyGroupByDirection(direction);
		}

		if (GetSelectedEscortSurvivor() != null)
		{
			return TryMoveSelectedEscortSurvivorByDirection(direction);
		}

		return TryMoveSelectedOfficerByDirection(direction);
	}

	private void UpdateHeldOfficerMovement()
	{
		if (_missionGameOver
			|| _isPanning
			|| _isSelectionDragging
			|| (_dialogueUi?.IsConversationOpen ?? false)
			|| (_missionUi?.IsStoryEventVisible ?? false))
		{
			return;
		}

		object primaryUnit = GetPrimarySelectedFriendlyUnit();
		if (primaryUnit == null)
		{
			return;
		}

		Vector2I direction = GetHeldOfficerMoveDirection();
		if (direction == Vector2I.Zero)
		{
			return;
		}

		if (!_combatActive && GetSelectedFriendlyUnits().Count > 1)
		{
			TryMoveSelectedFriendlyGroupByDirection(direction);
			return;
		}

		switch (primaryUnit)
		{
			case MissionNpcPawn survivor when !survivor.IsMoving:
				TryMoveSelectedEscortSurvivorByDirection(direction);
				break;
			case OfficerPawn officer when !officer.IsMoving:
				TryMoveSelectedOfficerByDirection(direction);
				break;
		}
	}

	private Vector2I GetHeldOfficerMoveDirection()
	{
		if (Input.IsKeyPressed(Key.W))
		{
			return new Vector2I(0, -1);
		}

		if (Input.IsKeyPressed(Key.S))
		{
			return new Vector2I(0, 1);
		}

		if (Input.IsKeyPressed(Key.A))
		{
			return new Vector2I(-1, 0);
		}

		if (Input.IsKeyPressed(Key.D))
		{
			return new Vector2I(1, 0);
		}

		return Vector2I.Zero;
	}

	private void TryMoveSelectedOfficer()
	{
		OfficerPawn activeOfficer = GetSelectedOfficer();
		if (activeOfficer == null || _roomBuilder == null || _isoWorld == null)
		{
			return;
		}

		Vector2 localMousePosition = _isoWorld.ToLocal(GetGlobalMousePosition());
		Vector2I targetCell = ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(localMousePosition));
		TryMoveOfficerToCell(activeOfficer, targetCell);
	}

	private void TryMoveSelectedEscortSurvivor()
	{
		MissionNpcPawn survivor = GetSelectedEscortSurvivor();
		if (survivor == null || _roomBuilder == null || _isoWorld == null)
		{
			return;
		}

		Vector2 localMousePosition = _isoWorld.ToLocal(GetGlobalMousePosition());
		Vector2I targetCell = ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(localMousePosition));
		TryMoveEscortSurvivorToCell(survivor, targetCell);
	}

	private bool TryMoveSelectedFriendlyGroup()
	{
		if (_roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		List<object> selectedUnits = GetSelectedFriendlyUnits()
			.Where(unit => !IsFriendlyUnitMoving(unit))
			.ToList();
		if (selectedUnits.Count <= 1)
		{
			return false;
		}

		Vector2I targetCell = ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition())));
		List<Vector2I> candidateCells = GetGroupDestinationCandidates(targetCell, selectedUnits.Count * 6);
		if (candidateCells.Count == 0)
		{
			return false;
		}

		HashSet<string> selectedOfficerIds = GetSelectedOfficers()
			.Where(officer => !string.IsNullOrWhiteSpace(officer.OfficerID))
			.Select(officer => officer.OfficerID)
			.ToHashSet();
		HashSet<string> selectedSurvivorIds = GetSelectedEscortSurvivors()
			.Where(survivor => !string.IsNullOrWhiteSpace(survivor.NpcId))
			.Select(survivor => survivor.NpcId)
			.ToHashSet();
		HashSet<Vector2I> reservedDestinations = new HashSet<Vector2I>();
		bool movedAnyUnit = false;

		foreach (object unit in OrderFriendlyUnitsForGroupMove(selectedUnits, targetCell))
		{
			Vector2I unitCell = GetFriendlyUnitCurrentCell(unit);
			Vector2I? destination = candidateCells
				.Where(candidate => !reservedDestinations.Contains(candidate))
				.Where(candidate => !IsCellOccupiedByLivingActor(candidate, unit as OfficerPawn, unit as MissionNpcPawn, selectedOfficerIds, selectedSurvivorIds))
				.Where(candidate => TryGetFriendlyUnitMovementPath(unit, candidate, selectedOfficerIds, selectedSurvivorIds, out List<Vector2I> _))
				.OrderBy(candidate => GetMovementStepDistance(candidate, targetCell))
				.ThenBy(candidate => GetMovementStepDistance(unitCell, candidate))
				.Cast<Vector2I?>()
				.FirstOrDefault();
			if (!destination.HasValue)
			{
				continue;
			}

			if (TryMoveFriendlyUnitToCell(unit, destination.Value, selectedOfficerIds, selectedSurvivorIds))
			{
				reservedDestinations.Add(destination.Value);
				movedAnyUnit = true;
			}
		}

		return movedAnyUnit;
	}

	private bool TryMoveSelectedFriendlyGroupByDirection(Vector2I direction)
	{
		if (_combatActive || _roomBuilder == null)
		{
			return false;
		}

		List<object> selectedUnits = GetSelectedFriendlyUnits()
			.Where(unit => !IsFriendlyUnitMoving(unit))
			.ToList();
		if (selectedUnits.Count <= 1)
		{
			return false;
		}

		HashSet<string> selectedOfficerIds = GetSelectedOfficers()
			.Where(officer => !string.IsNullOrWhiteSpace(officer.OfficerID))
			.Select(officer => officer.OfficerID)
			.ToHashSet();
		HashSet<string> selectedSurvivorIds = GetSelectedEscortSurvivors()
			.Where(survivor => !string.IsNullOrWhiteSpace(survivor.NpcId))
			.Select(survivor => survivor.NpcId)
			.ToHashSet();
		HashSet<Vector2I> movingOrigins = selectedUnits
			.Select(GetFriendlyUnitCurrentCell)
			.ToHashSet();
		HashSet<Vector2I> reservedDestinations = new HashSet<Vector2I>();
		List<(object Unit, Vector2I Origin, Vector2I Destination)> movePlans = new List<(object, Vector2I, Vector2I)>();

		foreach (object unit in OrderFriendlyUnitsForDirectionalGroupMove(selectedUnits, direction))
		{
			Vector2I origin = GetFriendlyUnitCurrentCell(unit);
			Vector2I destination = ResolveMovementTargetCell(origin + direction);
			if (!_roomBuilder.IsWalkableMovementCell(destination))
			{
				continue;
			}

			if (reservedDestinations.Contains(destination))
			{
				continue;
			}

			if (IsCellOccupiedByLivingActor(destination, unit as OfficerPawn, unit as MissionNpcPawn, selectedOfficerIds, selectedSurvivorIds)
				|| IsCellOccupiedBySelectedUnitNotMoving(destination, movingOrigins, movePlans))
			{
				continue;
			}

			if (!TryGetFriendlyUnitMovementPath(unit, destination, selectedOfficerIds, selectedSurvivorIds, out List<Vector2I> pathCells)
				|| pathCells.Count <= 1)
			{
				continue;
			}

			movePlans.Add((unit, origin, destination));
			reservedDestinations.Add(destination);
		}

		bool movedAnyUnit = false;
		foreach ((object unit, Vector2I _, Vector2I destination) in movePlans)
		{
			if (TryMoveFriendlyUnitToCell(unit, destination, selectedOfficerIds, selectedSurvivorIds))
			{
				movedAnyUnit = true;
			}
		}

		return movedAnyUnit;
	}

	private bool IsFriendlyUnitMoving(object unit)
	{
		return unit switch
		{
			OfficerPawn officer => officer.IsMoving,
			MissionNpcPawn survivor => survivor.IsMoving,
			_ => false
		};
	}

	private Vector2I GetFriendlyUnitCurrentCell(object unit)
	{
		return unit switch
		{
			OfficerPawn officer => officer.CurrentCell,
			MissionNpcPawn survivor => survivor.CurrentCell,
			_ => Vector2I.Zero
		};
	}

	private bool TryGetFriendlyUnitMovementPath(
		object unit,
		Vector2I destination,
		IReadOnlyCollection<string> ignoredOfficerIds,
		IReadOnlyCollection<string> ignoredSurvivorIds,
		out List<Vector2I> pathCells)
	{
		switch (unit)
		{
			case OfficerPawn officer:
				return TryGetTraversableMovementPath(officer.CurrentCell, destination, officer, null, ignoredOfficerIds, ignoredSurvivorIds, out pathCells);
			case MissionNpcPawn survivor:
				return TryGetTraversableMovementPath(survivor.CurrentCell, destination, null, survivor, ignoredOfficerIds, ignoredSurvivorIds, out pathCells);
			default:
				pathCells = new List<Vector2I>();
				return false;
		}
	}

	private bool TryMoveFriendlyUnitToCell(
		object unit,
		Vector2I destination,
		IReadOnlyCollection<string> ignoredOfficerIds,
		IReadOnlyCollection<string> ignoredSurvivorIds)
	{
		switch (unit)
		{
			case OfficerPawn officer:
				return TryMoveOfficerToCell(officer, destination, ignoredOfficerIds, true, ignoredSurvivorIds);
			case MissionNpcPawn survivor:
				return TryMoveEscortSurvivorToCell(survivor, destination, false, ignoredOfficerIds, ignoredSurvivorIds);
			default:
				return false;
		}
	}

	private List<object> OrderFriendlyUnitsForGroupMove(List<object> units, Vector2I targetCell)
	{
		object primaryUnit = GetPrimarySelectedFriendlyUnit();
		return units
			.OrderByDescending(unit => unit == primaryUnit)
			.ThenBy(unit => GetMovementStepDistance(GetFriendlyUnitCurrentCell(unit), targetCell))
			.ToList();
	}

	private List<object> OrderFriendlyUnitsForDirectionalGroupMove(List<object> units, Vector2I direction)
	{
		return direction switch
		{
			var d when d == new Vector2I(1, 0) => units.OrderByDescending(unit => GetFriendlyUnitCurrentCell(unit).X).ThenBy(unit => GetFriendlyUnitCurrentCell(unit).Y).ToList(),
			var d when d == new Vector2I(-1, 0) => units.OrderBy(unit => GetFriendlyUnitCurrentCell(unit).X).ThenBy(unit => GetFriendlyUnitCurrentCell(unit).Y).ToList(),
			var d when d == new Vector2I(0, 1) => units.OrderByDescending(unit => GetFriendlyUnitCurrentCell(unit).Y).ThenBy(unit => GetFriendlyUnitCurrentCell(unit).X).ToList(),
			var d when d == new Vector2I(0, -1) => units.OrderBy(unit => GetFriendlyUnitCurrentCell(unit).Y).ThenBy(unit => GetFriendlyUnitCurrentCell(unit).X).ToList(),
			_ => units
		};
	}

	private static bool IsCellOccupiedBySelectedUnitNotMoving(
		Vector2I cell,
		HashSet<Vector2I> movingOrigins,
		List<(object Unit, Vector2I Origin, Vector2I Destination)> movePlans)
	{
		if (!movingOrigins.Contains(cell))
		{
			return false;
		}

		return movePlans.All(plan => plan.Origin != cell);
	}

	private List<Vector2I> GetGroupDestinationCandidates(Vector2I targetCell, int maxCandidates)
	{
		List<Vector2I> candidates = new List<Vector2I>();
		if (_roomBuilder == null || maxCandidates <= 0)
		{
			return candidates;
		}

		Queue<Vector2I> frontier = new Queue<Vector2I>();
		HashSet<Vector2I> visited = new HashSet<Vector2I>();
		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		frontier.Enqueue(targetCell);
		visited.Add(targetCell);

		while (frontier.Count > 0 && candidates.Count < maxCandidates)
		{
			Vector2I current = frontier.Dequeue();
			if (_roomBuilder.IsWalkableMovementCell(current))
			{
				candidates.Add(current);
			}

			foreach (Vector2I direction in directions)
			{
				Vector2I next = current + direction;
				if (visited.Add(next))
				{
					frontier.Enqueue(next);
				}
			}
		}

		return candidates;
	}

	private bool TryMoveSelectedOfficerByDirection(Vector2I tileDirection)
	{
		OfficerPawn activeOfficer = GetSelectedOfficer();
		if (activeOfficer == null || _roomBuilder == null)
		{
			return false;
		}

		if (activeOfficer.IsMoving)
		{
			return false;
		}

		if (_combatActive && activeOfficer != GetActiveCombatOfficer())
		{
			return false;
		}

		Vector2I targetCell = ResolveMovementTargetCell(activeOfficer.CurrentCell + tileDirection);
		return TryMoveOfficerToCell(activeOfficer, targetCell);
	}

	private bool TryMoveSelectedEscortSurvivorByDirection(Vector2I tileDirection)
	{
		MissionNpcPawn survivor = GetSelectedEscortSurvivor();
		if (survivor == null || _roomBuilder == null)
		{
			return false;
		}

		if (survivor.IsMoving)
		{
			return false;
		}

		if (_combatActive && survivor != GetActiveCombatEscortSurvivor())
		{
			return false;
		}

		Vector2I targetCell = ResolveMovementTargetCell(survivor.CurrentCell + tileDirection);
		return TryMoveEscortSurvivorToCell(survivor, targetCell);
	}

	private bool TryMoveOfficerToCell(OfficerPawn officer, Vector2I targetCell)
	{
		return TryMoveOfficerToCell(officer, targetCell, null, true);
	}

	private bool TryMoveOfficerToCell(OfficerPawn officer, Vector2I targetCell, IReadOnlyCollection<string> ignoredOfficerIds)
	{
		return TryMoveOfficerToCell(officer, targetCell, ignoredOfficerIds, true);
	}

	private bool TryMoveOfficerToCell(OfficerPawn officer, Vector2I targetCell, IReadOnlyCollection<string> ignoredOfficerIds, bool allowPropRedirect, IReadOnlyCollection<string> ignoredSurvivorIds = null)
	{
		if (officer == null || _roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		targetCell = ResolveMovementTargetCell(targetCell);

		if (IsMovementCellBlockedByProp(targetCell))
		{
			if (allowPropRedirect
				&& (TryHandleBlockedPropMovement(officer, targetCell, ignoredOfficerIds)
					|| TryHandleBlockedInteractionMovement(officer, targetCell, ignoredOfficerIds)))
			{
				return true;
			}

			return false;
		}

		if (IsCellOccupiedByLivingActor(targetCell, officer, null, ignoredOfficerIds, ignoredSurvivorIds))
		{
			return false;
		}

		if (!TryGetTraversableMovementPath(officer.CurrentCell, targetCell, officer, null, ignoredOfficerIds, ignoredSurvivorIds, out List<Vector2I> pathCells))
		{
			return false;
		}

		if (pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells
			.Skip(1)
			.TakeWhile(cell => !IsCellOccupiedByLivingActor(cell, officer, null, ignoredOfficerIds, ignoredSurvivorIds) && !IsMovementCellBlockedByProp(cell))
			.ToList();
		if (steppedCells.Count == 0)
		{
			return false;
		}

		targetCell = steppedCells[^1];
		Vector2I targetBuildCell = GetBuildCell(targetCell);

		if (_combatActive)
		{
			int maxMovementSteps = GetMaxMovementStepsForActions(officer.CurrentActions);
			steppedCells = steppedCells.Take(maxMovementSteps).ToList();
			int moveCost = GetMovementCostForPathSteps(steppedCells.Count);
			if (!officer.CanSpendActions(moveCost))
			{
				return false;
			}

			targetCell = steppedCells[^1];
			officer.SpendActions(moveCost);
			_pendingCombatMoveOfficerId = officer.OfficerID;
			_pendingCombatMoveCost = moveCost;
			targetBuildCell = GetBuildCell(targetCell);
			AppendCombatLog($"{officer.OfficerName} repositions {moveCost} tile{(moveCost == 1 ? string.Empty : "s")} toward {targetBuildCell.X},{targetBuildCell.Y}, conserving {officer.WeaponName.ToLowerInvariant()} fire for the next opening.");
		}
		else
		{
			AppendActionLog($"{officer.OfficerName} moves to {targetBuildCell.X},{targetBuildCell.Y}.");
		}

		List<Vector2> pathPoints = steppedCells
			.Select(GetMovementCellGlobalPosition)
			.ToList();
		officer.MoveAlongPath(pathPoints, steppedCells, targetCell);
		return true;
	}

	private bool TryMoveEscortSurvivorToCell(MissionNpcPawn survivor, Vector2I targetCell)
	{
		return TryMoveEscortSurvivorToCell(survivor, targetCell, _combatActive);
	}

	private bool TryMoveEscortSurvivorToCell(
		MissionNpcPawn survivor,
		Vector2I targetCell,
		bool useCombatActions,
		IReadOnlyCollection<string> ignoredOfficerIds = null,
		IReadOnlyCollection<string> ignoredSurvivorIds = null)
	{
		if (!IsActiveEscortSurvivor(survivor) || _roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		if (survivor.IsMoving)
		{
			return false;
		}

		if (_combatActive && survivor != GetActiveCombatEscortSurvivor())
		{
			return false;
		}

		targetCell = ResolveMovementTargetCell(targetCell);
		if (IsMovementCellBlockedByProp(targetCell) || IsCellOccupiedByLivingActor(targetCell, null, survivor, ignoredOfficerIds, ignoredSurvivorIds))
		{
			return false;
		}

		if (!TryGetTraversableMovementPath(survivor.CurrentCell, targetCell, null, survivor, ignoredOfficerIds, ignoredSurvivorIds, out List<Vector2I> pathCells))
		{
			return false;
		}

		if (pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells
			.Skip(1)
			.TakeWhile(cell => !IsCellOccupiedByLivingActor(cell, null, survivor, ignoredOfficerIds, ignoredSurvivorIds) && !IsMovementCellBlockedByProp(cell))
			.ToList();
		if (steppedCells.Count == 0)
		{
			return false;
		}

		targetCell = steppedCells[^1];
		Vector2I targetBuildCell = GetBuildCell(targetCell);
		if (useCombatActions)
		{
			int maxMovementSteps = GetMaxMovementStepsForActions(survivor.CurrentActions);
			steppedCells = steppedCells.Take(maxMovementSteps).ToList();
			if (steppedCells.Count == 0)
			{
				return false;
			}

			int moveCost = GetMovementCostForPathSteps(steppedCells.Count);
			if (!survivor.CanSpendActions(moveCost))
			{
				return false;
			}

			targetCell = steppedCells[^1];
			survivor.SpendActions(moveCost);
			_pendingCombatMoveEscortSurvivorId = survivor.NpcId;
			targetBuildCell = GetBuildCell(targetCell);
			AppendCombatLog($"{survivor.DisplayName} moves {moveCost} tile{(moveCost == 1 ? string.Empty : "s")} toward {targetBuildCell.X},{targetBuildCell.Y}.");
		}
		else
		{
			AppendActionLog($"{survivor.DisplayName} moves to {targetBuildCell.X},{targetBuildCell.Y}.");
		}

		List<Vector2> pathPoints = steppedCells
			.Select(GetMovementCellGlobalPosition)
			.ToList();
		survivor.MoveAlongPath(pathPoints, steppedCells, targetCell);
		return true;
	}

	private bool TryHandleInteractionClick(OfficerPawn officer)
	{
		if (officer == null || _roomBuilder == null || _isoWorld == null || (_dialogueUi?.IsConversationOpen ?? false))
		{
			return false;
		}

		Vector2I clickedCell = _roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		Vector2I clickedBuildCell = GetBuildCell(clickedCell);
		if (_roomBuilder.IsDoorCell(clickedBuildCell))
		{
			// Door bulkheads are keyboard-only for now. Left-click should always remain a move order.
			return false;
		}

		if (!_visibleCells.Contains(clickedCell))
		{
			return false;
		}

		if (TryHandleNpcInteractionClick(officer, clickedCell))
		{
			return true;
		}

		if (TryHandlePropInteractionClick(officer, clickedCell))
		{
			return true;
		}

		List<MissionRoomBuilder.MarkerPlacement> interactions = _roomBuilder.GetInteractPlacementsAtCell(clickedBuildCell)
			.Where(placement =>
				(placement.LogicRole != "door" && string.Equals(placement.TriggerMode, "interact", System.StringComparison.OrdinalIgnoreCase))
				|| placement.LogicRole == "terminal")
			.Where(placement => !HasRuntimePropForPlacement(placement))
			.ToList();
		if (interactions.Count == 0)
		{
			return false;
		}

		MissionRoomBuilder.MarkerPlacement interaction = interactions
			.OrderByDescending(placement => placement.LogicRole == "terminal")
			.First();

		RequestStaticInteraction(officer, interaction);
		return true;
	}

	private bool TryHandleBlockedPropMovement(OfficerPawn officer, Vector2I blockedTargetCell, IReadOnlyCollection<string> ignoredOfficerIds)
	{
		if (!TryGetMissionPropAtMovementCell(blockedTargetCell, out MissionProp prop))
		{
			return false;
		}

		if (CanOfficerExecutePropInteraction(officer, prop))
		{
			ExecutePropInteraction(officer, prop);
			return true;
		}

		Vector2I? approachCell = FindBestPropApproachCell(officer, prop, ignoredOfficerIds);
		if (!approachCell.HasValue)
		{
			return false;
		}

		if (TryMoveOfficerToCell(officer, approachCell.Value, ignoredOfficerIds, false))
		{
			_pendingPropInstanceId = prop.PropInstanceId;
			_pendingInteractionOfficerId = officer.OfficerID;
			return true;
		}

		return false;
	}

	private bool TryHandleBlockedInteractionMovement(OfficerPawn officer, Vector2I blockedTargetCell, IReadOnlyCollection<string> ignoredOfficerIds)
	{
		if (!TryGetStaticInteractionAtMovementCell(blockedTargetCell, out MissionRoomBuilder.MarkerPlacement interaction))
		{
			return false;
		}

		if (CanOfficerExecuteInteraction(officer, interaction))
		{
			ExecuteInteraction(officer, interaction);
			return true;
		}

		Vector2I? approachCell = FindBestInteractionApproachCell(officer, interaction, ignoredOfficerIds);
		if (!approachCell.HasValue)
		{
			return false;
		}

		if (TryMoveOfficerToCell(officer, approachCell.Value, ignoredOfficerIds, false))
		{
			_pendingInteractionKey = BuildInteractionKey(interaction);
			_pendingInteractionOfficerId = officer.OfficerID;
			return true;
		}

		return false;
	}

	private bool TryHandleDoorKeyAction(OfficerPawn officer)
	{
		if (officer == null || _roomBuilder == null || _isoWorld == null || (_dialogueUi?.IsConversationOpen ?? false))
		{
			return false;
		}

		Vector2I hoveredCell = _roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		Vector2I hoveredBuildCell = GetBuildCell(hoveredCell);
		Vector2I officerBuildCell = GetBuildCell(officer.CurrentCell);
		List<Vector2I> candidateBuildCells = new List<Vector2I> { hoveredBuildCell };
		for (int offsetY = -1; offsetY <= 1; offsetY++)
		{
			for (int offsetX = -1; offsetX <= 1; offsetX++)
			{
				candidateBuildCells.Add(officerBuildCell + new Vector2I(offsetX, offsetY));
			}
		}

		foreach (Vector2I candidate in candidateBuildCells
			.Distinct()
			.Where(cell => _roomBuilder.TryGetDoorIdAtCell(cell, out string _) && IsStructureCellVisible(cell))
			.OrderByDescending(cell => cell == hoveredBuildCell)
			.ThenBy(cell => GetInteractionDistanceToBuildCell(officer.CurrentCell, cell)))
		{
			if (_roomBuilder.TryGetDoorIdAtCell(candidate, out string doorId)
				&& !string.IsNullOrWhiteSpace(doorId)
				&& TryHandleDirectDoorInteraction(officer, candidate, doorId))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryHandleDirectDoorClick(OfficerPawn officer, Vector2I clickedCell)
	{
		if (officer == null || _roomBuilder == null)
		{
			return false;
		}

		Vector2I clickedBuildCell = GetBuildCell(clickedCell);
		if (!_roomBuilder.TryGetDoorIdAtCell(clickedBuildCell, out string doorId) || string.IsNullOrWhiteSpace(doorId))
		{
			return false;
		}

		return TryHandleDirectDoorInteraction(officer, clickedBuildCell, doorId);
	}

	private bool TryHandleDirectDoorInteraction(OfficerPawn officer, Vector2I clickedCell, string doorId)
	{
		if (officer == null || _roomBuilder == null || string.IsNullOrWhiteSpace(doorId))
		{
			return false;
		}

		if (!IsStructureCellVisible(clickedCell))
		{
			return false;
		}

		return RequestDoorInteraction(officer, clickedCell, doorId);
	}

	private bool TryHandlePropInteractionClick(OfficerPawn officer, Vector2I clickedCell)
	{
		Vector2I clickedBuildCell = GetBuildCell(clickedCell);
		if (!_missionPropsByCell.TryGetValue(clickedBuildCell, out MissionProp prop) || prop == null)
		{
			return false;
		}

		if (!_visibleBuildCells.Contains(clickedBuildCell))
		{
			return false;
		}

		RequestPropInteraction(officer, prop);
		return true;
	}

	private bool TryHandleNpcInteractionClick(OfficerPawn officer, Vector2I clickedCell)
	{
		MissionNpcPawn npc = GetNpcAtMovementCell(clickedCell);
		if (npc == null)
		{
			return false;
		}

		if (!_visibleCells.Contains(npc.CurrentCell))
		{
			return false;
		}

		if (npc.IsHostile)
		{
			_focusedEnemy = npc;
			if (!string.IsNullOrWhiteSpace(npc.NpcId))
			{
				_engagedEnemyIds.Add(npc.NpcId);
			}
			if (!_combatActive)
			{
				StartMissionCombat();
			}
			else
			{
				RefreshCombatHud();
			}
			return false;
		}

		RequestNpcInteraction(officer, npc);
		return true;
	}

	private MissionNpcPawn GetNpcAtMovementCell(Vector2I movementCell)
	{
		if (_missionNpcsByCell.TryGetValue(movementCell, out MissionNpcPawn exactNpc) && exactNpc != null)
		{
			return exactNpc;
		}

		Vector2I buildCell = GetBuildCell(movementCell);
		return _missionNpcs.FirstOrDefault(npc => npc != null && !npc.IsDead && !npc.IsExtracted && GetBuildCell(npc.CurrentCell) == buildCell);
	}

	private bool CanOfficerExecuteInteraction(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (_combatActive && (officer == null || officer.CurrentActions < CombatInteractionActionCost))
		{
			return false;
		}

		return GetInteractionDistance(officer.CurrentCell, interaction) <= 1;
	}

	private int GetInteractionDistance(Vector2I officerCell, MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction.LogicRole == "terminal")
		{
			return GetInteractionDistanceToBuildCell(officerCell, interaction.Cell);
		}

		if (interaction.LogicRole == "door")
		{
			return GetInteractionDistanceToBuildCell(officerCell, interaction.Cell);
		}

		return GetBuildCell(officerCell) == interaction.Cell ? 0 : int.MaxValue;
	}

	private Vector2I? FindBestInteractionApproachCell(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction, IReadOnlyCollection<string> ignoredOfficerIds = null)
	{
		if (_roomBuilder == null || officer == null || interaction == null)
		{
			return null;
		}

		Vector2I interactionCell = GetMovementCell(interaction.Cell);
		List<Vector2I> candidates = _roomBuilder.GetReachableMovementCells(interactionCell, 1)
			.Where(candidate => _roomBuilder.IsWalkableMovementCell(candidate))
			.Where(candidate => !IsMovementCellBlockedByProp(candidate))
			.Where(candidate => !IsCellOccupiedByLivingActor(candidate, officer, null, ignoredOfficerIds))
			.ToList();

		foreach (Vector2I candidate in candidates.Distinct())
		{
			if (TryGetTraversableMovementPath(officer.CurrentCell, candidate, officer, null, ignoredOfficerIds, out List<Vector2I> _))
			{
				return candidate;
			}
		}

		return null;
	}

	private void ExecuteInteraction(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction)
	{
		string interactionKey = BuildInteractionKey(interaction);
		if (interaction.OneShot && _consumedTriggerKeys.Contains(interactionKey))
		{
			return;
		}

		if (!string.IsNullOrEmpty(interaction.RequiredFlag) && (_globalData?.StoryFlags?.Contains(interaction.RequiredFlag) != true))
		{
			return;
		}

		if (!string.IsNullOrEmpty(interaction.SetFlag) && _globalData != null && !_globalData.StoryFlags.Contains(interaction.SetFlag))
		{
			_globalData.StoryFlags.Add(interaction.SetFlag);
			UpdateMissionCompletionActions();
		}

		if (interaction.MarkerId == "evac_zone")
		{
			if (_combatActive)
			{
				officer.SpendActions(CombatInteractionActionCost);
			}
			AppendActionLog($"{officer.OfficerName} moves into the evac zone.");
			UpdateMissionCompletionActions();
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			return;
		}

		if (interaction.LogicRole == "door")
		{
			bool nextOpenState = !_roomBuilder.IsDoorOpen(interaction.TargetId);
			FaceNodeToward(officer, GetCellGlobalPosition(interaction.Cell));
			if (_combatActive)
			{
				officer.SpendActions(CombatInteractionActionCost);
			}
			ToggleDoorInteraction(interaction);
			AppendActionLog($"{officer.OfficerName} {(nextOpenState ? "opens" : "closes")} {GetInteractionDisplayName(interaction)}.");
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			UpdateFogOfWar();
			UpdateMissionCompletionActions();
			return;
		}

		if (interaction.LogicRole == "terminal")
		{
			bool controlsDoor = !string.IsNullOrEmpty(interaction.TargetId);
			bool nextOpenState = controlsDoor && !_roomBuilder.IsDoorOpen(interaction.TargetId);
			FaceNodeToward(officer, GetCellGlobalPosition(interaction.Cell));
			if (_combatActive)
			{
				officer.SpendActions(CombatInteractionActionCost);
			}
			if (!string.IsNullOrEmpty(interaction.TargetId))
			{
				_roomBuilder.TrySetDoorOpen(interaction.TargetId, nextOpenState, true);
				UpdateFogOfWar();
			}
			AppendActionLog(controlsDoor
				? $"{officer.OfficerName} uses {GetInteractionDisplayName(interaction)} to {(nextOpenState ? "open" : "close")} a bulkhead."
				: $"{officer.OfficerName} uses {GetInteractionDisplayName(interaction)}.");
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			UpdateMissionCompletionActions();
			return;
		}

		if (interaction.MarkerId == "trigger_dialogue")
		{
			FaceNodeToward(officer, GetCellGlobalPosition(interaction.Cell));
			if (_combatActive)
			{
				officer.SpendActions(CombatInteractionActionCost);
			}
			AppendActionLog($"{officer.OfficerName} initiates a conversation.");
			_dialogueUi.StartConversation(
				ResolveDialogueTargetId(interaction),
				officer.OfficerName,
				officer.PortraitPath,
				interaction.NpcPortraitPath);
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			UpdateMissionCompletionActions();
		}
	}

	private bool CanOfficerExecutePropInteraction(OfficerPawn officer, MissionProp prop)
	{
		if (officer == null || prop == null)
		{
			return false;
		}

		if (_combatActive && officer.CurrentActions < CombatInteractionActionCost)
		{
			return false;
		}

		Vector2I propCell = GetMovementCell(GetPropCell(prop));
		int interactionRange = Mathf.Max(1, prop.Definition?.InteractionRange ?? 1);
		int distance = GetTileDistance(officer.CurrentCell, propCell);
		return distance <= interactionRange;
	}

	private bool CanOfficerExecuteNpcInteraction(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null)
		{
			return false;
		}

		if (_combatActive && officer.CurrentActions < CombatInteractionActionCost)
		{
			return false;
		}

		int interactionRange = Mathf.Max(1, npc.InteractionRange);
		int distance = GetTileDistance(officer.CurrentCell, npc.CurrentCell);
		return distance <= interactionRange;
	}

	private Vector2I? FindBestPropApproachCell(OfficerPawn officer, MissionProp prop, IReadOnlyCollection<string> ignoredOfficerIds = null)
	{
		if (officer == null || prop == null || _roomBuilder == null)
		{
			return null;
		}

		Vector2I propBuildCell = GetPropCell(prop);
		Vector2I propMovementCell = GetMovementCell(propBuildCell);
		int interactionRange = Mathf.Max(1, prop.Definition?.InteractionRange ?? 1);

		List<Vector2I> candidates = _roomBuilder.GetReachableMovementCells(propMovementCell, interactionRange)
			.Where(candidate => GetBuildCell(candidate) != propBuildCell)
			.Where(candidate => _roomBuilder.IsWalkableMovementCell(candidate))
			.Where(candidate => !IsMovementCellBlockedByProp(candidate))
			.Where(candidate => !IsCellOccupiedByLivingActor(candidate, officer, null, ignoredOfficerIds))
			.ToList();

		foreach (Vector2I candidate in candidates.Distinct().OrderBy(candidate => GetTileDistance(candidate, propMovementCell)))
		{
			if (TryGetTraversableMovementPath(officer.CurrentCell, candidate, officer, null, ignoredOfficerIds, out List<Vector2I> _))
			{
				return candidate;
			}
		}

		return null;
	}

	private Vector2I? FindBestNpcApproachCell(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null || _roomBuilder == null)
		{
			return null;
		}

		List<Vector2I> candidates = _roomBuilder.GetReachableMovementCells(npc.CurrentCell, npc.InteractionRange)
			.Where(candidate => candidate != npc.CurrentCell && _roomBuilder.IsWalkableMovementCell(candidate))
			.ToList();

		foreach (Vector2I candidate in candidates.Distinct())
		{
			if (TryGetTraversableMovementPath(officer.CurrentCell, candidate, officer, null, null, out List<Vector2I> _))
			{
				return candidate;
			}
		}

		return null;
	}

	private void ExecutePropInteraction(OfficerPawn officer, MissionProp prop)
	{
		if (officer == null || prop == null)
		{
			return;
		}

		FaceNodeToward(officer, prop.GlobalPosition);
		if (TryPromptMedicalBedUse(officer, prop))
		{
			return;
		}

		PropInteractionContext context = BuildPropInteractionContext(officer, prop);
		PropInteractionResult result = prop.Interact(context);
		if (result == null || !result.Success)
		{
			return;
		}

		if (ShouldShowStoryEvent(prop))
		{
			ShowPropStoryEvent(prop, result, context);
			return;
		}

		ApplyPropInteractionResult(prop, result, context);
	}

	private void ExecuteNpcInteraction(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null)
		{
			return;
		}

		FaceNodeToward(officer, npc.GlobalPosition);
		FaceNodeToward(npc, officer.GlobalPosition, 0.2f);
		PropInteractionContext context = BuildNpcInteractionContext(officer, npc);
		PropInteractionResult result = npc.Interact(context);
		if (result == null || !result.Success)
		{
			return;
		}

		ApplyNpcInteractionResult(npc, result, context);
	}

	private void ToggleDoorInteraction(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (_roomBuilder == null || string.IsNullOrEmpty(interaction.TargetId))
		{
			return;
		}

		bool nextOpenState = !_roomBuilder.IsDoorOpen(interaction.TargetId);
		if (!nextOpenState && _officerPawns.Any(pawn => GetBuildCell(pawn.CurrentCell) == interaction.Cell))
		{
			return;
		}

		_roomBuilder.TrySetDoorOpen(interaction.TargetId, nextOpenState, true);
	}

	private Vector2 GetCellGlobalPosition(Vector2I cell)
	{
		Vector2 localPosition = _roomBuilder.GetCellWorldPosition(cell.X, cell.Y);
		return _isoWorld.ToGlobal(localPosition);
	}

	private Vector2 GetMovementCellGlobalPosition(Vector2I cell)
	{
		Vector2 localPosition = _roomBuilder.GetMovementCellWorldPosition(cell.X, cell.Y);
		return _isoWorld.ToGlobal(localPosition);
	}

	private Vector2I GetBuildCell(Vector2I movementCell)
	{
		return _roomBuilder?.GetBuildCellForMovementCell(movementCell) ?? movementCell;
	}

	private Vector2I GetMovementCell(Vector2I buildCell)
	{
		return _roomBuilder?.GetMovementCellForBuildCell(buildCell) ?? buildCell;
	}

	private Vector2I ResolveMovementTargetCell(Vector2I requestedCell)
	{
		if (_roomBuilder == null)
		{
			return requestedCell;
		}

		if (_roomBuilder.IsWalkableMovementCell(requestedCell))
		{
			return requestedCell;
		}

		Vector2I buildCell = GetBuildCell(requestedCell);
		if (!_roomBuilder.IsWalkableCell(buildCell))
		{
			return requestedCell;
		}

		Vector2I? normalizedCell = _roomBuilder.GetMovementCellsForBuildCell(buildCell)
			.Where(cell => _roomBuilder.IsWalkableMovementCell(cell))
			.OrderBy(cell => GetMovementStepDistance(cell, requestedCell))
			.ThenBy(cell => GetMovementStepDistance(cell, GetMovementCell(buildCell)))
			.Cast<Vector2I?>()
			.FirstOrDefault();
		return normalizedCell ?? requestedCell;
	}

	private bool IsMovementCellBlockedByProp(Vector2I movementCell, string ignoredPropInstanceId = null)
	{
		Vector2I buildCell = GetBuildCell(movementCell);
		if (_blockedStaticPropCells.Contains(buildCell))
		{
			return true;
		}

		if (_missionPropsByCell.TryGetValue(buildCell, out MissionProp prop)
			&& prop != null
			&& (string.IsNullOrWhiteSpace(ignoredPropInstanceId) || prop.PropInstanceId != ignoredPropInstanceId))
		{
			return true;
		}

		return false;
	}

	private bool TryGetMissionPropAtMovementCell(Vector2I movementCell, out MissionProp prop)
	{
		return _missionPropsByCell.TryGetValue(GetBuildCell(movementCell), out prop) && prop != null && !prop.IsConsumed;
	}

	private bool TryGetStaticInteractionAtMovementCell(Vector2I movementCell, out MissionRoomBuilder.MarkerPlacement interaction)
	{
		interaction = _roomBuilder?.GetInteractPlacementsAtCell(GetBuildCell(movementCell))
			.Where(placement =>
				(placement.LogicRole != "door" && string.Equals(placement.TriggerMode, "interact", System.StringComparison.OrdinalIgnoreCase))
				|| placement.LogicRole == "terminal")
			.Where(placement => !HasRuntimePropForPlacement(placement))
			.OrderByDescending(placement => placement.LogicRole == "terminal")
			.FirstOrDefault();
		return interaction != null;
	}

	private int GetMovementSubdivision()
	{
		return _roomBuilder?.MovementSubdivision ?? 1;
	}

	private int GetMovementStepDistance(Vector2I a, Vector2I b)
	{
		return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
	}

	private int GetTileDistance(Vector2I a, Vector2I b)
	{
		return Mathf.CeilToInt(GetMovementStepDistance(a, b) / (float)GetMovementSubdivision());
	}

	private int GetInteractionDistanceToBuildCell(Vector2I officerCell, Vector2I buildCell)
	{
		if (_roomBuilder == null)
		{
			return GetTileDistance(officerCell, GetMovementCell(buildCell));
		}

		int minimumDistance = int.MaxValue;
		foreach (Vector2I movementCell in _roomBuilder.GetMovementCellsForBuildCell(buildCell))
		{
			if (!_roomBuilder.IsWalkableMovementCell(movementCell))
			{
				continue;
			}

			minimumDistance = Mathf.Min(minimumDistance, GetTileDistance(officerCell, movementCell));
		}

		return minimumDistance == int.MaxValue
			? GetTileDistance(officerCell, GetMovementCell(buildCell))
			: minimumDistance;
	}

	private int GetMovementCostForPathSteps(int stepCount)
	{
		if (stepCount <= 0)
		{
			return 0;
		}

		return Mathf.Max(1, Mathf.CeilToInt(stepCount / (float)GetMovementSubdivision()));
	}

	private int GetMaxMovementStepsForActions(int actions)
	{
		return Mathf.Max(0, actions * GetMovementSubdivision());
	}

	private bool CanAttackTarget(Vector2I attackerCell, Vector2I targetCell, MissionAttackProfile attackProfile)
	{
		if (attackProfile == null || GetTileDistance(attackerCell, targetCell) > attackProfile.Range)
		{
			return false;
		}

		return HasClearLineOfSight(attackerCell, targetCell);
	}

	private bool HasClearLineOfSight(Vector2I fromMovementCell, Vector2I toMovementCell, string ignoredPropInstanceId = null)
	{
		if (_roomBuilder == null)
		{
			return true;
		}

		Vector2I fromBuildCell = GetBuildCell(fromMovementCell);
		Vector2I toBuildCell = GetBuildCell(toMovementCell);
		if (fromBuildCell == toBuildCell)
		{
			return true;
		}

		Vector2 start = _roomBuilder.GetLineOfSightGridPosition(fromMovementCell);
		Vector2 end = _roomBuilder.GetLineOfSightGridPosition(toMovementCell);
		Vector2 direction = end - start;
		int currentX = Mathf.FloorToInt(start.X);
		int currentY = Mathf.FloorToInt(start.Y);
		int targetX = Mathf.FloorToInt(end.X);
		int targetY = Mathf.FloorToInt(end.Y);
		int stepX = direction.X > 0f ? 1 : direction.X < 0f ? -1 : 0;
		int stepY = direction.Y > 0f ? 1 : direction.Y < 0f ? -1 : 0;
		float tDeltaX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs(1f / direction.X);
		float tDeltaY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs(1f / direction.Y);
		float nextBoundaryX = stepX > 0 ? currentX + 1f : currentX;
		float nextBoundaryY = stepY > 0 ? currentY + 1f : currentY;
		float tMaxX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs((nextBoundaryX - start.X) / direction.X);
		float tMaxY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs((nextBoundaryY - start.Y) / direction.Y);

		while (currentX != targetX || currentY != targetY)
		{
			Vector2I currentCell = new Vector2I(currentX, currentY);
			if (Mathf.IsEqualApprox(tMaxX, tMaxY))
			{
				Vector2I horizontalCell = new Vector2I(currentX + stepX, currentY);
				Vector2I verticalCell = new Vector2I(currentX, currentY + stepY);
				Vector2I diagonalCell = new Vector2I(currentX + stepX, currentY + stepY);

				if (IsLineOfSightTransitionBlocked(currentCell, horizontalCell)
					|| IsLineOfSightTransitionBlocked(currentCell, verticalCell)
					|| IsLineOfSightBuildCellBlocked(horizontalCell, fromBuildCell, toBuildCell, ignoredPropInstanceId)
					|| IsLineOfSightBuildCellBlocked(verticalCell, fromBuildCell, toBuildCell, ignoredPropInstanceId)
					|| IsLineOfSightTransitionBlocked(horizontalCell, diagonalCell)
					|| IsLineOfSightTransitionBlocked(verticalCell, diagonalCell)
					|| IsLineOfSightBuildCellBlocked(diagonalCell, fromBuildCell, toBuildCell, ignoredPropInstanceId))
				{
					return false;
				}

				currentX += stepX;
				currentY += stepY;
				tMaxX += tDeltaX;
				tMaxY += tDeltaY;
				continue;
			}

			Vector2I nextCell;
			if (tMaxX < tMaxY)
			{
				nextCell = new Vector2I(currentX + stepX, currentY);
				tMaxX += tDeltaX;
			}
			else
			{
				nextCell = new Vector2I(currentX, currentY + stepY);
				tMaxY += tDeltaY;
			}

			if (IsLineOfSightTransitionBlocked(currentCell, nextCell)
				|| IsLineOfSightBuildCellBlocked(nextCell, fromBuildCell, toBuildCell, ignoredPropInstanceId))
			{
				return false;
			}

			currentX = nextCell.X;
			currentY = nextCell.Y;
		}

		return true;
	}

	private bool IsLineOfSightTransitionBlocked(Vector2I fromBuildCell, Vector2I toBuildCell)
	{
		return _roomBuilder != null && _roomBuilder.IsLineOfSightBuildTransitionBlocked(fromBuildCell, toBuildCell);
	}

	private bool IsLineOfSightBuildCellBlocked(Vector2I buildCell, Vector2I startBuildCell, Vector2I targetBuildCell, string ignoredPropInstanceId)
	{
		if (buildCell == startBuildCell || buildCell == targetBuildCell)
		{
			return false;
		}

		if (_blockedStaticPropCells.Contains(buildCell))
		{
			return true;
		}

		return _missionPropsByCell.TryGetValue(buildCell, out MissionProp prop)
			&& prop != null
			&& !prop.IsConsumed
			&& (string.IsNullOrWhiteSpace(ignoredPropInstanceId) || prop.PropInstanceId != ignoredPropInstanceId);
	}

	private bool TrySelectControllableUnitAtMouse()
	{
		MissionNpcPawn activeCombatSurvivor = GetActiveCombatEscortSurvivor();
		if (_combatActive && activeCombatSurvivor == null)
		{
			return false;
		}

		Vector2 mousePosition = GetGlobalMousePosition();
		if (activeCombatSurvivor != null)
		{
			Rect2 survivorBounds = new Rect2(activeCombatSurvivor.GlobalPosition + new Vector2(-52f, -72f), new Vector2(104f, 120f));
			if (!survivorBounds.HasPoint(mousePosition))
			{
				return false;
			}

			SelectEscortSurvivor(activeCombatSurvivor);
			return true;
		}

		for (int i = 0; i < _officerPawns.Count; i++)
		{
			OfficerPawn pawn = _officerPawns[i];
			if (pawn == null || pawn.IsDead)
			{
				continue;
			}

			Rect2 bounds = new Rect2(pawn.GlobalPosition + new Vector2(-52f, -72f), new Vector2(104f, 120f));
			if (!bounds.HasPoint(mousePosition))
			{
				continue;
			}

			SelectOfficer(i);
			return true;
		}

		foreach (MissionNpcPawn survivor in GetAliveEscortSurvivors())
		{
			Rect2 bounds = new Rect2(survivor.GlobalPosition + new Vector2(-52f, -72f), new Vector2(104f, 120f));
			if (!bounds.HasPoint(mousePosition))
			{
				continue;
			}

			SelectEscortSurvivor(survivor);
			return true;
		}

		return false;
	}

	private void CheckEnterTriggers(OfficerPawn officer)
	{
		if (officer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (MissionRoomBuilder.MarkerPlacement marker in _roomBuilder.GetMarkerPlacements())
		{
			if (!string.Equals(marker.TriggerMode, "enter", System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (marker.Cell != GetBuildCell(officer.CurrentCell))
			{
				continue;
			}

			if (!string.IsNullOrEmpty(marker.RequiredFlag) && (_globalData?.StoryFlags?.Contains(marker.RequiredFlag) != true))
			{
				continue;
			}

			string triggerKey = BuildInteractionKey(marker);
			if (marker.OneShot && _consumedTriggerKeys.Contains(triggerKey))
			{
				continue;
			}

			if (!string.IsNullOrEmpty(marker.SetFlag) && _globalData != null && !_globalData.StoryFlags.Contains(marker.SetFlag))
			{
				_globalData.StoryFlags.Add(marker.SetFlag);
				UpdateMissionCompletionActions();
			}

			if (marker.OneShot)
			{
				_consumedTriggerKeys.Add(triggerKey);
			}

			if (marker.MarkerId == "evac_zone")
			{
				UpdateMissionCompletionActions();
				break;
			}

			if (marker.MarkerId == "trigger_dialogue" && _dialogueUi != null && !_dialogueUi.IsConversationOpen)
			{
				_dialogueUi.StartConversation(
					ResolveDialogueTargetId(marker),
					officer.OfficerName,
					officer.PortraitPath,
					marker.NpcPortraitPath);
			}
			break;
		}
	}

	private IEnumerable<OfficerPawn> GetAliveOfficers()
	{
		return _officerPawns.Where(pawn => pawn != null && !pawn.IsDead);
	}

	private bool IsEscortSurvivorId(string npcId)
	{
		return !string.IsNullOrWhiteSpace(npcId)
			&& (_escortSurvivorIds.Contains(npcId) || npcId.StartsWith("relay_survivor_", StringComparison.Ordinal));
	}

	private bool IsEscortSurvivor(MissionNpcPawn npc)
	{
		return npc != null && IsEscortSurvivorId(npc.NpcId);
	}

	private bool IsActiveEscortSurvivor(MissionNpcPawn npc)
	{
		return IsEscortSurvivor(npc) && !npc.IsDead && !npc.IsExtracted && !_extractedSurvivorIds.Contains(npc.NpcId);
	}

	private IEnumerable<MissionNpcPawn> GetAliveEscortSurvivors()
	{
		return _missionNpcs.Where(IsActiveEscortSurvivor);
	}

	private int GetExtractedEscortSurvivorCount()
	{
		return _extractedSurvivorIds.Count;
	}

	private IEnumerable<MissionNpcPawn> GetAliveHostileEnemies()
	{
		return _missionNpcs.Where(npc => npc != null && npc.IsHostile && !npc.IsDead);
	}

	private IEnumerable<MissionNpcPawn> GetEngagedHostileEnemies()
	{
		return GetAliveHostileEnemies().Where(npc => !string.IsNullOrWhiteSpace(npc.NpcId) && _engagedEnemyIds.Contains(npc.NpcId));
	}

	private IEnumerable<MissionNpcPawn> GetVisibleAliveHostileEnemies()
	{
		return GetAliveHostileEnemies().Where(npc => _visibleCells.Contains(npc.CurrentCell));
	}

	private bool IsAnyCombatActorMoving()
	{
		return _officerPawns.Any(pawn => pawn != null && pawn.IsMoving)
			|| _missionNpcs.Any(npc => npc != null && npc.IsMoving);
	}

	private bool IsCellOccupiedByLivingActor(
		Vector2I cell,
		OfficerPawn ignoreOfficer = null,
		MissionNpcPawn ignoreEnemy = null,
		IReadOnlyCollection<string> ignoredOfficerIds = null,
		IReadOnlyCollection<string> ignoredSurvivorIds = null)
	{
		foreach (OfficerPawn pawn in _officerPawns)
		{
			if (pawn == null || pawn == ignoreOfficer || pawn.IsDead)
			{
				continue;
			}

			if (ignoredOfficerIds != null && !string.IsNullOrWhiteSpace(pawn.OfficerID) && ignoredOfficerIds.Contains(pawn.OfficerID))
			{
				continue;
			}

			if (pawn.CurrentCell == cell)
			{
				return true;
			}
		}

		foreach (MissionNpcPawn npc in _missionNpcs)
		{
			if (npc == null || npc == ignoreEnemy || npc.IsDead || npc.IsExtracted)
			{
				continue;
			}

			if (ignoredSurvivorIds != null && !string.IsNullOrWhiteSpace(npc.NpcId) && ignoredSurvivorIds.Contains(npc.NpcId))
			{
				continue;
			}

			if (npc.CurrentCell == cell)
			{
				return true;
			}
		}

		return false;
	}

	private MissionCombatTurnEntry GetActiveCombatTurnEntry()
	{
		return _combatActiveIndex >= 0 && _combatActiveIndex < _combatQueue.Count
			? _combatQueue[_combatActiveIndex]
			: null;
	}

	private OfficerPawn GetActiveCombatOfficer()
	{
		MissionCombatTurnEntry entry = GetActiveCombatTurnEntry();
		return entry != null && entry.IsOfficer ? entry.Officer : null;
	}

	private MissionNpcPawn GetActiveCombatEscortSurvivor()
	{
		MissionCombatTurnEntry entry = GetActiveCombatTurnEntry();
		return entry != null && !entry.IsOfficer && entry.Enemy != null && !entry.Enemy.IsHostile ? entry.Enemy : null;
	}

	private MissionNpcPawn GetActiveCombatEnemy()
	{
		MissionCombatTurnEntry entry = GetActiveCombatTurnEntry();
		return entry != null && !entry.IsOfficer && entry.Enemy?.IsHostile == true ? entry.Enemy : null;
	}

	private bool IsPlayerTurnActive()
	{
		return _combatActive && (GetActiveCombatOfficer() != null || GetActiveCombatEscortSurvivor() != null) && !_enemyTurnInProgress;
	}

	private void EvaluateCombatState()
	{
		if (_missionGameOver || IsAnyCombatActorMoving())
		{
			return;
		}

		if (!GetAliveOfficers().Any())
		{
			HandleMissionGameOver();
			return;
		}

		List<MissionNpcPawn> visibleHostiles = GetVisibleAliveHostileEnemies().ToList();
		foreach (MissionNpcPawn hostile in visibleHostiles)
		{
			if (!string.IsNullOrWhiteSpace(hostile.NpcId))
			{
				_engagedEnemyIds.Add(hostile.NpcId);
			}
		}

		bool anyVisibleHostiles = visibleHostiles.Count > 0;
		if (!_combatActive)
		{
			if (_engagedEnemyIds.Count > 0 && anyVisibleHostiles)
			{
				StartMissionCombat();
			}
			else
			{
				RefreshCombatHud();
			}

			return;
		}

		if (!GetEngagedHostileEnemies().Any())
		{
			EndMissionCombat();
			return;
		}

		CleanupCombatQueue();
		if (_combatQueue.Count == 0 && !_enemyTurnInProgress)
		{
			_combatActiveIndex = -1;
			RebuildCombatQueue();
			BeginNextCombatTurn();
			return;
		}

		RefreshCombatHud();
	}

	private void StartMissionCombat()
	{
		if (_missionGameOver || _combatActive)
		{
			return;
		}

		_combatActive = true;
		_enemyTurnInProgress = false;
		_combatRound = 1;
		_combatActiveIndex = -1;
		_missionUi?.ClearCombatLog();
		AppendCombatLog("Combat erupts in the mission zone as hostile contacts emerge from cover.");
		RebuildCombatQueue();
		UpdateMovementGridVisibility();
		RefreshCombatHud();
		BeginNextCombatTurn();
	}

	private void EndMissionCombat()
	{
		_combatActive = false;
		_enemyTurnInProgress = false;
		_combatQueue.Clear();
		_combatActiveIndex = -1;
		_pendingCombatMoveOfficerId = string.Empty;
		_pendingCombatAttackEnemyId = string.Empty;
		_focusedEnemy = null;
		_engagedEnemyIds.Clear();
		_hoveredCombatActor = null;
		_missionUi?.HideHoverSummary();
		AppendCombatLog("The last engaged hostile goes quiet. Combat control returns to exploration.");
		UpdateMovementGridVisibility();
		RefreshCombatHud();
	}

	private void UpdateHostileRoaming(float delta)
	{
		if (_combatActive || _missionGameOver || _roomBuilder == null)
		{
			_hostileRoamClock = 0f;
			return;
		}

		if ((_dialogueUi?.IsConversationOpen ?? false) || IsAnyCombatActorMoving())
		{
			return;
		}

		_hostileRoamClock += delta;
		if (_hostileRoamClock < HostileRoamIntervalSeconds)
		{
			return;
		}

		_hostileRoamClock = 0f;
		List<MissionNpcPawn> roamingHostiles = _missionNpcs
			.Where(npc => npc != null && npc.IsHostile && !npc.IsDead && !npc.IsMoving && !_engagedEnemyIds.Contains(npc.NpcId))
			.OrderBy(_ => _combatRng.Randi())
			.ToList();
		foreach (MissionNpcPawn hostile in roamingHostiles)
		{
			if (TryRoamHostile(hostile))
			{
				break;
			}
		}
	}

	private bool TryRoamHostile(MissionNpcPawn hostile)
	{
		if (hostile == null || hostile.IsDead || hostile.IsMoving || _roomBuilder == null)
		{
			return false;
		}

		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};
		List<Vector2I> candidateCells = directions
			.Select(direction => hostile.CurrentCell + direction)
			.Where(cell => _roomBuilder.IsWalkableMovementCell(cell) && !IsMovementCellBlockedByProp(cell) && !IsCellOccupiedByLivingActor(cell, null, hostile))
			.OrderBy(_ => _combatRng.Randi())
			.ToList();
		if (candidateCells.Count == 0)
		{
			return false;
		}

		Vector2I destinationCell = candidateCells[0];
		if (!TryGetTraversableMovementPath(hostile.CurrentCell, destinationCell, null, hostile, null, out List<Vector2I> pathCells) || pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells.Skip(1).ToList();
		List<Vector2> pathPoints = steppedCells
			.Select(GetMovementCellGlobalPosition)
			.ToList();
		hostile.MoveAlongPath(pathPoints, steppedCells, destinationCell);
		return true;
	}

	private void UpdateEscortSurvivorBehavior(float delta)
	{
		_escortFollowClock = 0f;
	}

	private bool TryAdvanceEscortSurvivor(MissionNpcPawn survivor, bool useCombatActions)
	{
		if (survivor == null || survivor.IsDead || survivor.IsExtracted || survivor.IsMoving || _roomBuilder == null)
		{
			return false;
		}

		if (TryExtractEscortSurvivor(survivor))
		{
			return false;
		}

		int maxSteps = useCombatActions ? GetMaxMovementStepsForActions(Mathf.Max(1, survivor.CurrentActions)) : GetMovementSubdivision();
		if (maxSteps <= 0)
		{
			return false;
		}

		List<Vector2I> targetCells = AreAllOfficersOnEvacZone()
			? GetEvacRallyCells().ToList()
			: GetAliveOfficers().Select(officer => officer.CurrentCell).ToList();
		if (targetCells.Count == 0)
		{
			return false;
		}

		Vector2I destinationCell = ResolveEscortDestinationCell(survivor, targetCells, maxSteps);
		if (destinationCell == survivor.CurrentCell)
		{
			return false;
		}

		if (!TryGetTraversableMovementPath(survivor.CurrentCell, destinationCell, null, survivor, null, out List<Vector2I> pathCells) || pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells.Skip(1).Take(maxSteps).ToList();
		if (steppedCells.Count == 0)
		{
			return false;
		}

		int moveCost = GetMovementCostForPathSteps(steppedCells.Count);
		if (useCombatActions && !survivor.CanSpendActions(moveCost))
		{
			return false;
		}

		if (useCombatActions)
		{
			survivor.SpendActions(moveCost);
		}

		Vector2I finalDestination = steppedCells[^1];
		List<Vector2> pathPoints = steppedCells.Select(GetMovementCellGlobalPosition).ToList();
		survivor.MoveAlongPath(pathPoints, steppedCells, finalDestination);
		if (useCombatActions)
		{
			AppendCombatLog($"{survivor.DisplayName} falls back {moveCost} tile{(moveCost == 1 ? string.Empty : "s")} toward extraction.");
		}
		else
		{
			AppendActionLog($"{survivor.DisplayName} keeps close behind the officers.");
		}

		return true;
	}

	private Vector2I ResolveEscortDestinationCell(MissionNpcPawn survivor, IReadOnlyList<Vector2I> targetCells, int maxSteps)
	{
		Vector2I bestDestination = survivor.CurrentCell;
		int bestDistance = int.MaxValue;
		foreach (Vector2I targetCell in targetCells)
		{
			if (!TryGetTraversableMovementPath(survivor.CurrentCell, targetCell, null, survivor, null, out List<Vector2I> pathCells) || pathCells.Count <= 1)
			{
				continue;
			}

			List<Vector2I> steppedCells = pathCells
				.Skip(1)
				.TakeWhile(cell => !IsCellOccupiedByLivingActor(cell, null, survivor))
				.Take(maxSteps)
				.ToList();
			if (steppedCells.Count == 0)
			{
				continue;
			}

			Vector2I candidate = steppedCells[^1];
			int candidateDistance = GetTileDistance(candidate, targetCell);
			if (candidateDistance < bestDistance)
			{
				bestDistance = candidateDistance;
				bestDestination = candidate;
			}
		}

		return bestDestination;
	}

	private bool TryExtractEscortSurvivor(MissionNpcPawn survivor)
	{
		if (!IsActiveEscortSurvivor(survivor))
		{
			return false;
		}

		HashSet<Vector2I> evacCells = GetEvacRallyCells();
		if (evacCells.Count == 0 || !evacCells.Contains(survivor.CurrentCell))
		{
			return false;
		}

		_extractedSurvivorIds.Add(survivor.NpcId);
		survivor.SetExtracted();
		_selectedEscortSurvivorIds.Remove(survivor.NpcId);
		if (survivor.NpcId == _selectedEscortSurvivorId)
		{
			_selectedEscortSurvivorId = string.Empty;
		}
		NormalizeFriendlySelectionState();
		UpdateOfficerSelectionVisuals();
		UpdateSelectedOfficerDisplay();
		AppendActionLog($"{survivor.DisplayName} reaches the evac zone and is brought aboard the fleet.");
		ReindexMissionNpcCells();
		UpdateFogOfWar();
		UpdateMissionCompletionActions();
		return true;
	}

	private void CleanupCombatQueue()
	{
		string activeCombatantId = GetActiveCombatTurnEntry()?.CombatantId ?? string.Empty;
		_combatQueue.RemoveAll(entry => entry == null
			|| (entry.IsOfficer && (entry.Officer == null || entry.Officer.IsDead))
			|| (!entry.IsOfficer && (entry.Enemy == null || entry.Enemy.IsDead || entry.Enemy.IsExtracted || (entry.Enemy.IsHostile ? !_engagedEnemyIds.Contains(entry.Enemy.NpcId) : !IsActiveEscortSurvivor(entry.Enemy)))));

		if (_combatQueue.Count == 0)
		{
			_combatActiveIndex = -1;
			return;
		}

		if (!string.IsNullOrWhiteSpace(activeCombatantId))
		{
			int newIndex = _combatQueue.FindIndex(entry => entry.CombatantId == activeCombatantId);
			_combatActiveIndex = newIndex >= 0 ? newIndex : Mathf.Clamp(_combatActiveIndex, -1, _combatQueue.Count - 1);
			return;
		}

		_combatActiveIndex = Mathf.Clamp(_combatActiveIndex, -1, _combatQueue.Count - 1);
	}

	private void RebuildCombatQueue()
	{
		List<MissionCombatTurnEntry> nextQueue = new List<MissionCombatTurnEntry>();
		foreach (OfficerPawn officer in GetAliveOfficers())
		{
			nextQueue.Add(new MissionCombatTurnEntry
			{
				CombatantId = officer.OfficerID,
				IsOfficer = true,
				InitiativeScore = _combatRng.RandiRange(1, 20) + officer.InitiativeBonus,
				Officer = officer
			});
		}

		foreach (MissionNpcPawn enemy in GetEngagedHostileEnemies())
		{
			nextQueue.Add(new MissionCombatTurnEntry
			{
				CombatantId = enemy.NpcId,
				IsOfficer = false,
				InitiativeScore = _combatRng.RandiRange(1, 20) + enemy.InitiativeBonus,
				Enemy = enemy
			});
		}

		foreach (MissionNpcPawn survivor in GetAliveEscortSurvivors())
		{
			nextQueue.Add(new MissionCombatTurnEntry
			{
				CombatantId = survivor.NpcId,
				IsOfficer = false,
				InitiativeScore = _combatRng.RandiRange(1, 20) + survivor.InitiativeBonus,
				Enemy = survivor
			});
		}

		_combatQueue.Clear();
		_combatQueue.AddRange(nextQueue
			.OrderByDescending(entry => entry.InitiativeScore)
			.ThenBy(entry => entry.IsOfficer ? entry.Officer?.OfficerName : entry.Enemy?.DisplayName)
			.ToList());
	}

	private async void BeginNextCombatTurn()
	{
		if (_missionGameOver || _enemyTurnInProgress)
		{
			return;
		}

		if (!_combatActive)
		{
			RefreshCombatHud();
			return;
		}

		if (!GetAliveOfficers().Any())
		{
			HandleMissionGameOver();
			return;
		}

		if (!GetEngagedHostileEnemies().Any())
		{
			EndMissionCombat();
			return;
		}

		if (_combatQueue.Count == 0)
		{
			RebuildCombatQueue();
			if (_combatQueue.Count == 0)
			{
				EndMissionCombat();
				return;
			}
		}

		_combatActiveIndex++;
		if (_combatActiveIndex >= _combatQueue.Count)
		{
			_combatRound++;
			RebuildCombatQueue();
			if (_combatQueue.Count == 0)
			{
				EndMissionCombat();
				return;
			}

			_combatActiveIndex = 0;
		}

		MissionCombatTurnEntry entry = GetActiveCombatTurnEntry();
		if (entry == null)
		{
			EndMissionCombat();
			return;
		}

		if (entry.IsOfficer)
		{
			entry.Officer?.BeginTurn();
			int officerIndex = _officerPawns.IndexOf(entry.Officer);
			if (officerIndex >= 0)
			{
				SelectOfficer(officerIndex);
			}

			_focusedEnemy = GetClosestVisibleEnemy(entry.Officer?.CurrentCell ?? Vector2I.Zero);
			if (entry.Officer != null)
			{
				AppendCombatLog($"Round {_combatRound}: {entry.Officer.OfficerName} takes point with {entry.Officer.CurrentActions} AP and {entry.Officer.CurrentHP} HP.");
			}
			RefreshCombatHud();
			return;
		}

		MissionNpcPawn activeNpc = entry.Enemy;
		if (activeNpc == null || activeNpc.IsDead || activeNpc.IsExtracted)
		{
			EndCurrentCombatTurn();
			return;
		}

		activeNpc.BeginTurn();
		_focusedEnemy = activeNpc.IsHostile ? activeNpc : _focusedEnemy;
		AppendCombatLog(activeNpc.IsHostile
			? $"Round {_combatRound}: {activeNpc.DisplayName} advances with {activeNpc.CurrentActions} AP and {activeNpc.CurrentHP} HP."
			: $"Round {_combatRound}: {activeNpc.DisplayName} scrambles for evac with {activeNpc.CurrentActions} AP and {activeNpc.CurrentHP} HP.");
		RefreshCombatHud();
		if (activeNpc.IsHostile)
		{
			_enemyTurnInProgress = true;
			await ToSignal(GetTree().CreateTimer(0.35f), SceneTreeTimer.SignalName.Timeout);
			await ExecuteEnemyTurnAsync(activeNpc);
			_enemyTurnInProgress = false;
		}
		else
		{
			SelectEscortSurvivor(activeNpc);
			return;
		}

		if (_missionGameOver)
		{
			return;
		}

		if (!_combatActive)
		{
			RefreshCombatHud();
			return;
		}

		CleanupCombatQueue();
		EndCurrentCombatTurn();
	}

	private void EndCurrentCombatTurn()
	{
		if (_missionGameOver || !_combatActive)
		{
			RefreshCombatHud();
			return;
		}

		_pendingCombatMoveOfficerId = string.Empty;
		_pendingCombatAttackEnemyId = string.Empty;
		RefreshCombatHud();
		BeginNextCombatTurn();
	}

	private void OnCombatEndTurnPressed()
	{
		if (!_combatActive || _missionGameOver || _enemyTurnInProgress)
		{
			return;
		}

		OfficerPawn activeOfficer = GetActiveCombatOfficer();
		if (activeOfficer != null)
		{
			AppendActionLog($"{activeOfficer.OfficerName} ends their turn.");
		}
		else if (GetActiveCombatEscortSurvivor() is MissionNpcPawn survivor)
		{
			AppendActionLog($"{survivor.DisplayName} holds position and waits for the next opening.");
		}

		EndCurrentCombatTurn();
	}

	private bool TryHandleCombatAttackClick(OfficerPawn officer)
	{
		if (!_combatActive || officer == null || officer != GetActiveCombatOfficer() || !officer.CanSpendActions(CombatAttackActionCost) || _roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		Vector2I clickedCell = _roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		if (!_visibleCells.Contains(clickedCell))
		{
			return false;
		}

		MissionNpcPawn enemy = GetNpcAtMovementCell(clickedCell);
		if (enemy == null || enemy.IsDead || !enemy.IsHostile)
		{
			return false;
		}

		_focusedEnemy = enemy;
		MissionAttackProfile attackProfile = officer.GetAttackProfile();
		if (CanAttackTarget(officer.CurrentCell, enemy.CurrentCell, attackProfile))
		{
			PerformOfficerAttack(officer, enemy);
			return true;
		}

		Vector2I? approachCell = FindBestCombatApproachCell(officer.CurrentCell, enemy.CurrentCell, attackProfile, officer.CurrentActions, officer, null);
		if (approachCell.HasValue && TryMoveOfficerToCell(officer, approachCell.Value))
		{
			_pendingCombatAttackEnemyId = enemy.NpcId;
			_pendingInteractionOfficerId = string.Empty;
			_pendingNpcId = string.Empty;
			_pendingPropInstanceId = string.Empty;
			_pendingInteractionKey = string.Empty;
		}

		RefreshCombatHud();
		return true;
	}

	private async Task ExecuteEnemyTurnAsync(MissionNpcPawn enemy)
	{
		if (enemy == null || enemy.IsDead || !_combatActive || _missionGameOver)
		{
			return;
		}

		while (enemy.CurrentActions > 0 && !_missionGameOver && _combatActive)
		{
			OfficerPawn targetOfficer = null;
			MissionNpcPawn targetSurvivor = null;
			if (!TryGetEnemyTarget(enemy.CurrentCell, out targetOfficer, out targetSurvivor))
			{
				return;
			}

			Vector2I targetCell = targetOfficer != null ? targetOfficer.CurrentCell : targetSurvivor.CurrentCell;
			MissionAttackProfile attackProfile = enemy.GetAttackProfile();
			if (CanAttackTarget(enemy.CurrentCell, targetCell, attackProfile))
			{
				if (targetOfficer != null)
				{
					PerformEnemyAttack(enemy, targetOfficer);
				}
				else
				{
					PerformEnemyAttack(enemy, targetSurvivor);
				}

				RefreshCombatHud();
				if (_missionGameOver || !_combatActive)
				{
					return;
				}

				await ToSignal(GetTree().CreateTimer(0.28f), SceneTreeTimer.SignalName.Timeout);
				continue;
			}

			bool moved = await TryMoveEnemyTowardTargetAsync(enemy, targetCell);
			RefreshCombatHud();
			if (!moved)
			{
				return;
			}

			await ToSignal(GetTree().CreateTimer(0.22f), SceneTreeTimer.SignalName.Timeout);
			attackProfile = enemy.GetAttackProfile();
			targetCell = targetOfficer != null ? targetOfficer.CurrentCell : targetSurvivor.CurrentCell;
			if (enemy.CurrentActions > 0 && CanAttackTarget(enemy.CurrentCell, targetCell, attackProfile))
			{
				if (targetOfficer != null)
				{
					PerformEnemyAttack(enemy, targetOfficer);
				}
				else
				{
					PerformEnemyAttack(enemy, targetSurvivor);
				}

				RefreshCombatHud();
				if (_missionGameOver || !_combatActive)
				{
					return;
				}

				await ToSignal(GetTree().CreateTimer(0.28f), SceneTreeTimer.SignalName.Timeout);
			}
			else
			{
				return;
			}
		}
	}

	private async Task ExecuteEscortTurnAsync(MissionNpcPawn survivor)
	{
		if (survivor == null || survivor.IsDead || survivor.IsExtracted || !_combatActive || _missionGameOver)
		{
			return;
		}

		while (survivor.CurrentActions > 0 && !_missionGameOver && _combatActive && !survivor.IsExtracted)
		{
			if (TryExtractEscortSurvivor(survivor))
			{
				return;
			}

			bool moved = TryAdvanceEscortSurvivor(survivor, true);
			if (!moved)
			{
				return;
			}

			await ToSignal(survivor, MissionNpcPawn.SignalName.ReachedCell);
			if (TryExtractEscortSurvivor(survivor))
			{
				return;
			}
		}
	}

	private async Task<bool> TryMoveEnemyTowardTargetAsync(MissionNpcPawn enemy, Vector2I targetCell)
	{
		if (enemy == null || _roomBuilder == null || !enemy.CanSpendActions(1))
		{
			return false;
		}

		Vector2I? approachCell = FindBestCombatApproachCell(enemy.CurrentCell, targetCell, enemy.GetAttackProfile(), enemy.CurrentActions, null, enemy);
		if (!approachCell.HasValue || !TryGetTraversableMovementPath(enemy.CurrentCell, approachCell.Value, null, enemy, null, out List<Vector2I> pathCells) || pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells
			.Skip(1)
			.TakeWhile(cell => !IsCellOccupiedByLivingActor(cell, null, enemy))
			.ToList();
		if (steppedCells.Count == 0)
		{
			return false;
		}

		int maxMovementSteps = GetMaxMovementStepsForActions(enemy.CurrentActions);
		steppedCells = steppedCells.Take(maxMovementSteps).ToList();
		int moveCost = GetMovementCostForPathSteps(steppedCells.Count);
		if (!enemy.CanSpendActions(moveCost))
		{
			return false;
		}

		Vector2I destinationCell = steppedCells[^1];
		enemy.SpendActions(moveCost);
		AppendCombatLog($"{enemy.DisplayName} pushes {moveCost} tile{(moveCost == 1 ? string.Empty : "s")} toward the away team, closing with {enemy.WeaponName.ToLowerInvariant()} ready.");
		List<Vector2> pathPoints = steppedCells
			.Select(GetMovementCellGlobalPosition)
			.ToList();
		enemy.MoveAlongPath(pathPoints, steppedCells, destinationCell);
		await ToSignal(enemy, MissionNpcPawn.SignalName.ReachedCell);
		return true;
	}

	private Vector2I? FindBestCombatApproachCell(
		Vector2I startCell,
		Vector2I targetCell,
		MissionAttackProfile attackProfile,
		int maxSteps,
		OfficerPawn movingOfficer,
		MissionNpcPawn movingEnemy)
	{
		if (_roomBuilder == null || attackProfile == null || maxSteps <= 0)
		{
			return null;
		}

		List<(Vector2I Cell, int PathCost, int TargetDistance)> candidates = new List<(Vector2I, int, int)>();
		foreach (Vector2I candidate in _roomBuilder.GetReachableMovementCells(targetCell, attackProfile.Range))
		{
			if (candidate == targetCell || !_roomBuilder.IsWalkableMovementCell(candidate) || IsMovementCellBlockedByProp(candidate) || IsCellOccupiedByLivingActor(candidate, movingOfficer, movingEnemy))
			{
				continue;
			}

			if (!TryGetTraversableMovementPath(startCell, candidate, movingOfficer, movingEnemy, null, out List<Vector2I> pathCells))
			{
				continue;
			}

			int pathLength = Math.Max(0, pathCells.Count - 1);
			int pathCost = GetMovementCostForPathSteps(pathLength);
			if (pathLength <= 0 || pathCost > maxSteps)
			{
				continue;
			}

			if (!CanAttackTarget(candidate, targetCell, attackProfile))
			{
				continue;
			}

			candidates.Add((candidate, pathCost, GetTileDistance(candidate, targetCell)));
		}

		if (candidates.Count == 0)
		{
			return null;
		}

		return candidates
			.OrderBy(candidate => candidate.PathCost)
			.ThenBy(candidate => candidate.TargetDistance)
			.Select(candidate => (Vector2I?)candidate.Cell)
			.FirstOrDefault();
	}

	private void PerformOfficerAttack(OfficerPawn officer, MissionNpcPawn enemy)
	{
		if (!_combatActive || officer == null || enemy == null || officer.IsDead || enemy.IsDead || !officer.CanSpendActions(CombatAttackActionCost))
		{
			return;
		}

		MissionAttackProfile attackProfile = officer.GetAttackProfile();
		if (!CanAttackTarget(officer.CurrentCell, enemy.CurrentCell, attackProfile))
		{
			return;
		}

		FaceNodeToward(officer, enemy.GlobalPosition, 0.45f);
		officer.SpendActions(CombatAttackActionCost);
		PlayNodeAttackRecoil(officer, enemy.GlobalPosition);
		if (ShouldPlayOfficerLaserFireSound(attackProfile))
		{
			PlayOfficerLaserFireSound();
		}
		int damage = _combatRng.RandiRange(attackProfile.MinDamage, attackProfile.MaxDamage);
		CombatDamageResult result = ApplyAttackProfileToTarget(enemy, damage, attackProfile);
		PlayNodeHitReaction(enemy, officer.GlobalPosition, result);
		string statusText = TryApplyStatusEffect(enemy, attackProfile)
			? $" {enemy.DisplayName} is afflicted with {attackProfile.StatusEffectId}."
			: string.Empty;
		PlayAttackEffects(officer, enemy, attackProfile, result);
		AppendCombatLog(BuildDamageLog(
			$"{officer.OfficerName} fires {attackProfile.WeaponName.ToLowerInvariant()} at {enemy.DisplayName}",
			result,
			statusText));
		_focusedEnemy = enemy;
		_pendingCombatAttackEnemyId = string.Empty;
		ReindexMissionNpcCells();
		UpdateFogOfWar();
		RefreshCombatHud();

		if (!GetEngagedHostileEnemies().Any())
		{
			EndMissionCombat();
			return;
		}

		if (officer.CurrentActions <= 0)
		{
			EndCurrentCombatTurn();
		}
	}

	private void PerformEnemyAttack(MissionNpcPawn enemy, OfficerPawn officer)
	{
		if (!_combatActive || enemy == null || officer == null || enemy.IsDead || officer.IsDead || !enemy.CanSpendActions(CombatAttackActionCost))
		{
			return;
		}

		MissionAttackProfile attackProfile = enemy.GetAttackProfile();
		if (!CanAttackTarget(enemy.CurrentCell, officer.CurrentCell, attackProfile))
		{
			return;
		}

		FaceNodeToward(enemy, officer.GlobalPosition, 0.45f);
		enemy.SpendActions(CombatAttackActionCost);
		PlayNodeAttackRecoil(enemy, officer.GlobalPosition);
		if (ShouldPlayEnemyLaserFireSound(attackProfile))
		{
			PlayEnemyLaserFireSound();
		}
		int damage = _combatRng.RandiRange(attackProfile.MinDamage, attackProfile.MaxDamage);
		CombatDamageResult result = ApplyAttackProfileToTarget(officer, damage, attackProfile);
		PlayNodeHitReaction(officer, enemy.GlobalPosition, result);
		string statusText = TryApplyStatusEffect(officer, attackProfile)
			? $" {officer.OfficerName} is afflicted with {attackProfile.StatusEffectId}."
			: string.Empty;
		if (result?.ShieldDamage > 0)
		{
			PlayPlayerShieldHitSound();
		}
		PlayAttackEffects(enemy, officer, attackProfile, result);
		AppendCombatLog(BuildDamageLog(
			$"{enemy.DisplayName} answers with {attackProfile.WeaponName.ToLowerInvariant()}, hitting {officer.OfficerName}",
			result,
			statusText));
		UpdateFogOfWar();
		RefreshCombatHud();
		if (!GetAliveOfficers().Any())
		{
			HandleMissionGameOver();
		}
	}

	private void PerformEnemyAttack(MissionNpcPawn enemy, MissionNpcPawn survivor)
	{
		if (!_combatActive || enemy == null || survivor == null || enemy.IsDead || survivor.IsDead || survivor.IsExtracted || !enemy.CanSpendActions(CombatAttackActionCost))
		{
			return;
		}

		MissionAttackProfile attackProfile = enemy.GetAttackProfile();
		if (!CanAttackTarget(enemy.CurrentCell, survivor.CurrentCell, attackProfile))
		{
			return;
		}

		FaceNodeToward(enemy, survivor.GlobalPosition, 0.45f);
		enemy.SpendActions(CombatAttackActionCost);
		PlayNodeAttackRecoil(enemy, survivor.GlobalPosition);
		if (ShouldPlayEnemyLaserFireSound(attackProfile))
		{
			PlayEnemyLaserFireSound();
		}

		int damage = _combatRng.RandiRange(attackProfile.MinDamage, attackProfile.MaxDamage);
		CombatDamageResult result = ApplyAttackProfileToTarget(survivor, damage, attackProfile);
		PlayNodeHitReaction(survivor, enemy.GlobalPosition, result);
		string statusText = TryApplyStatusEffect(survivor, attackProfile)
			? $" {survivor.DisplayName} is afflicted with {attackProfile.StatusEffectId}."
			: string.Empty;
		PlayAttackEffects(enemy, survivor, attackProfile, result);
		AppendCombatLog(BuildDamageLog(
			$"{enemy.DisplayName} answers with {attackProfile.WeaponName.ToLowerInvariant()}, hitting {survivor.DisplayName}",
			result,
			statusText));
		UpdateFogOfWar();
		RefreshCombatHud();
	}

	private static string BuildDamageLog(string actionText, CombatDamageResult result, string suffix = "")
	{
		if (result == null)
		{
			return $"{actionText}.{suffix}";
		}

		List<string> impactParts = new List<string>();
		if (result.ShieldDamage > 0)
		{
			impactParts.Add($"stripping {result.ShieldDamage} shield");
		}

		if (result.HealthDamage > 0)
		{
			impactParts.Add($"dealing {result.HealthDamage} health damage");
		}

		if (impactParts.Count == 0)
		{
			impactParts.Add("but the shot disperses harmlessly");
		}

		string impactText = impactParts.Count == 1
			? impactParts[0]
			: $"{impactParts[0]} and {impactParts[1]}";
		return $"{actionText}, {impactText}, leaving {result.RemainingShields} shield and {result.RemainingHealth} HP.{suffix}";
	}

	private CombatDamageResult ApplyAttackProfileToTarget(MissionNpcPawn target, int rolledDamage, MissionAttackProfile attackProfile)
	{
		if (target == null || attackProfile == null)
		{
			return null;
		}

		return target.ApplyDamage(rolledDamage, attackProfile.BonusShieldDamage, 0);
	}

	private CombatDamageResult ApplyAttackProfileToTarget(OfficerPawn target, int rolledDamage, MissionAttackProfile attackProfile)
	{
		if (target == null || attackProfile == null)
		{
			return null;
		}

		return target.ApplyDamage(rolledDamage, attackProfile.BonusShieldDamage, 0);
	}

	private static void FaceNodeToward(Node2D actor, Vector2 targetGlobalPosition, float holdSeconds = 0.3f)
	{
		switch (actor)
		{
			case OfficerPawn officer:
				officer.FaceToward(targetGlobalPosition, holdSeconds);
				break;
			case MissionNpcPawn npc:
				npc.FaceToward(targetGlobalPosition, holdSeconds);
				break;
		}
	}

	private static void PlayNodeAttackRecoil(Node2D actor, Vector2 targetGlobalPosition)
	{
		switch (actor)
		{
			case OfficerPawn officer:
				officer.PlayAttackRecoil(targetGlobalPosition);
				break;
			case MissionNpcPawn npc:
				npc.PlayAttackRecoil(targetGlobalPosition);
				break;
		}
	}

	private static void PlayNodeHitReaction(Node2D actor, Vector2 sourceGlobalPosition, CombatDamageResult result)
	{
		if (actor == null || result == null)
		{
			return;
		}

		bool shieldHit = result.ShieldDamage > 0;
		bool hullHit = result.HealthDamage > 0;
		switch (actor)
		{
			case OfficerPawn officer:
				officer.PlayHitReaction(sourceGlobalPosition, shieldHit, hullHit);
				break;
			case MissionNpcPawn npc:
				npc.PlayHitReaction(sourceGlobalPosition, shieldHit, hullHit);
				break;
		}
	}

	private void PlayAttackEffects(Node2D attacker, Node2D target, MissionAttackProfile attackProfile, CombatDamageResult result)
	{
		if (attacker == null || target == null || attackProfile == null)
		{
			return;
		}

		EnsureCombatEffectLayer();
		if (_combatEffectLayer == null)
		{
			return;
		}

		bool shieldsHit = result?.ShieldDamage > 0;
		bool hullHit = result?.HealthDamage > 0;
		Color attackColor = shieldsHit && !hullHit
			? new Color(0.25f, 0.95f, 1f, 0.95f)
			: new Color(1f, 0.45f, 0.35f, 0.95f);

		if (attackProfile.IsMelee)
		{
			SpawnMeleeSlashEffect(target.GlobalPosition, attackColor);
		}
		else
		{
			SpawnRangedTracerEffect(attacker.GlobalPosition, target.GlobalPosition, attackColor);
		}

		SpawnImpactEffect(target.GlobalPosition, shieldsHit, hullHit);
		SpawnDamageText(target.GlobalPosition, result);
		if (hullHit)
		{
			PlayMissionHitSound();
		}
	}

	private void PlayMissionHitSound()
	{
		if (_hitSfxPlayer == null || _missionHitSound == null)
		{
			return;
		}

		_audioPlaybackService?.TryPlayLoaded(_hitSfxPlayer, _missionHitSound);
	}

	private void PlayPlayerShieldHitSound()
	{
		if (_playerShieldHitSfxPlayer == null || _missionPlayerShieldHitSound == null)
		{
			return;
		}

		_audioPlaybackService?.TryPlayLoaded(_playerShieldHitSfxPlayer, _missionPlayerShieldHitSound);
	}

	private void PlayOfficerLaserFireSound()
	{
		if (_officerLaserFireSfxPlayer == null || _missionOfficerLaserFireSound == null)
		{
			return;
		}

		_audioPlaybackService?.TryPlayLoaded(_officerLaserFireSfxPlayer, _missionOfficerLaserFireSound);
	}

	private static bool ShouldPlayOfficerLaserFireSound(MissionAttackProfile attackProfile)
	{
		if (attackProfile == null || attackProfile.IsMelee)
		{
			return false;
		}

		return attackProfile.WeaponId switch
		{
			"sidearm" => true,
			"heavy_sidearm" => true,
			"defense_pistol" => true,
			"pulse_carbine" => true,
			"pulse_lance" => true,
			_ => false
		};
	}

	private void PlayEnemyLaserFireSound()
	{
		if (_enemyLaserFireSfxPlayer == null || _missionEnemyLaserFireSound == null)
		{
			return;
		}

		_audioPlaybackService?.TryPlayLoaded(_enemyLaserFireSfxPlayer, _missionEnemyLaserFireSound);
	}

	private static bool ShouldPlayEnemyLaserFireSound(MissionAttackProfile attackProfile)
	{
		if (attackProfile == null || attackProfile.IsMelee || string.IsNullOrWhiteSpace(attackProfile.WeaponId))
		{
			return false;
		}

		string weaponId = attackProfile.WeaponId.ToLowerInvariant();
		return weaponId.Contains("laser") || weaponId.Contains("pulse");
	}

	private void SpawnRangedTracerEffect(Vector2 start, Vector2 end, Color color)
	{
		int effectZIndex = GetCombatEffectZIndexForWorldPositions(start, end);
		Line2D beam = new Line2D
		{
			Width = 5f,
			DefaultColor = color,
			ZIndex = effectZIndex
		};
		beam.ZAsRelative = false;
		beam.AddPoint(_combatEffectLayer.ToLocal(start));
		beam.AddPoint(_combatEffectLayer.ToLocal(end));
		_combatEffectLayer.AddChild(beam);

		Tween tween = CreateTween();
		tween.TweenProperty(beam, "modulate:a", 0f, 0.16f);
		tween.Parallel().TweenProperty(beam, "width", 1.5f, 0.16f);
		tween.TweenCallback(Callable.From(beam.QueueFree));
	}

	private void SpawnMeleeSlashEffect(Vector2 targetPosition, Color color)
	{
		Node2D root = new Node2D
		{
			Position = _combatEffectLayer.ToLocal(targetPosition),
			ZIndex = GetCombatEffectZIndexForWorldPosition(targetPosition)
		};
		root.ZAsRelative = false;
		_combatEffectLayer.AddChild(root);

		Line2D slashA = new Line2D
		{
			Width = 6f,
			DefaultColor = color
		};
		slashA.AddPoint(new Vector2(-22f, -16f));
		slashA.AddPoint(new Vector2(24f, 18f));
		root.AddChild(slashA);

		Line2D slashB = new Line2D
		{
			Width = 4f,
			DefaultColor = new Color(color.R, color.G, color.B, 0.72f)
		};
		slashB.AddPoint(new Vector2(-10f, 22f));
		slashB.AddPoint(new Vector2(18f, -20f));
		root.AddChild(slashB);

		Tween tween = CreateTween();
		tween.TweenProperty(root, "scale", new Vector2(1.25f, 1.25f), 0.12f);
		tween.Parallel().TweenProperty(root, "modulate:a", 0f, 0.18f);
		tween.TweenCallback(Callable.From(root.QueueFree));
	}

	private void SpawnImpactEffect(Vector2 targetPosition, bool shieldsHit, bool hullHit)
	{
		Node2D root = new Node2D
		{
			Position = _combatEffectLayer.ToLocal(targetPosition),
			ZIndex = GetCombatEffectZIndexForWorldPosition(targetPosition)
		};
		root.ZAsRelative = false;
		_combatEffectLayer.AddChild(root);

		Polygon2D burst = new Polygon2D
		{
			Color = shieldsHit && !hullHit
				? new Color(0.35f, 0.95f, 1f, 0.34f)
				: new Color(1f, 0.44f, 0.32f, 0.32f),
			Polygon = BuildEffectDiamond(20f, 12f)
		};
		root.AddChild(burst);

		Line2D outline = new Line2D
		{
			Width = 3.5f,
			DefaultColor = shieldsHit && !hullHit
				? new Color(0.55f, 1f, 1f, 0.95f)
				: new Color(1f, 0.72f, 0.48f, 0.95f),
			Closed = true
		};
		foreach (Vector2 point in BuildEffectDiamond(20f, 12f))
		{
			outline.AddPoint(point);
		}
		root.AddChild(outline);

		Tween tween = CreateTween();
		tween.TweenProperty(root, "scale", new Vector2(1.7f, 1.7f), 0.2f);
		tween.Parallel().TweenProperty(root, "modulate:a", 0f, 0.2f);
		tween.TweenCallback(Callable.From(root.QueueFree));
	}

	private void SpawnDamageText(Vector2 targetPosition, CombatDamageResult result)
	{
		if (_combatEffectLayer == null || result == null)
		{
			return;
		}

		if (result.ShieldDamage > 0)
		{
			SpawnFloatingCombatLabel(targetPosition + new Vector2(0f, -38f), $"-{result.ShieldDamage} SHD", new Color(0.35f, 0.95f, 1f, 1f));
		}

		if (result.HealthDamage > 0)
		{
			SpawnFloatingCombatLabel(targetPosition + new Vector2(0f, -16f), $"-{result.HealthDamage} HP", new Color(1f, 0.48f, 0.4f, 1f));
		}
	}

	private void SpawnFloatingCombatLabel(Vector2 worldPosition, string text, Color color)
	{
		Label label = new Label
		{
			Text = text,
			Position = _combatEffectLayer.ToLocal(worldPosition),
			ZIndex = GetCombatEffectZIndexForWorldPosition(worldPosition)
		};
		label.ZAsRelative = false;
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.04f, 0.06f, 0.95f));
		label.AddThemeConstantOverride("outline_size", 4);
		_combatEffectLayer.AddChild(label);

		Tween tween = CreateTween();
		tween.TweenProperty(label, "position:y", label.Position.Y - 28f, 0.42f);
		tween.Parallel().TweenProperty(label, "modulate:a", 0f, 0.42f);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private static Vector2[] BuildEffectDiamond(float halfWidth, float halfHeight)
	{
		return new[]
		{
			new Vector2(0f, -halfHeight),
			new Vector2(halfWidth, 0f),
			new Vector2(0f, halfHeight),
			new Vector2(-halfWidth, 0f)
		};
	}

	private int GetCombatEffectZIndexForWorldPositions(Vector2 a, Vector2 b)
	{
		if (_roomBuilder == null || _isoWorld == null)
		{
			return 240;
		}

		Vector2I cellA = GetBuildCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(a)));
		Vector2I cellB = GetBuildCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(b)));
		return Mathf.Max(
			_roomBuilder.GetCanvasSortOrderForBuildCell(cellA, CombatEffectSortBias),
			_roomBuilder.GetCanvasSortOrderForBuildCell(cellB, CombatEffectSortBias));
	}

	private int GetCombatEffectZIndexForWorldPosition(Vector2 worldPosition)
	{
		if (_roomBuilder == null || _isoWorld == null)
		{
			return 240;
		}

		Vector2I buildCell = GetBuildCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(worldPosition)));
		return _roomBuilder.GetCanvasSortOrderForBuildCell(buildCell, CombatEffectSortBias);
	}

	private bool TryApplyStatusEffect(OfficerPawn target, MissionAttackProfile attackProfile)
	{
		if (target == null || attackProfile == null || string.IsNullOrWhiteSpace(attackProfile.StatusEffectId) || attackProfile.StatusEffectChance <= 0f)
		{
			return false;
		}

		if (_combatRng.Randf() > attackProfile.StatusEffectChance)
		{
			return false;
		}

		return target.TryApplyStatusEffect(attackProfile.StatusEffectId);
	}

	private bool TryApplyStatusEffect(MissionNpcPawn target, MissionAttackProfile attackProfile)
	{
		if (target == null || attackProfile == null || string.IsNullOrWhiteSpace(attackProfile.StatusEffectId) || attackProfile.StatusEffectChance <= 0f)
		{
			return false;
		}

		if (_combatRng.Randf() > attackProfile.StatusEffectChance)
		{
			return false;
		}

		return target.TryApplyStatusEffect(attackProfile.StatusEffectId);
	}

	private MissionNpcPawn GetClosestVisibleEnemy(Vector2I fromCell)
	{
		return GetVisibleAliveHostileEnemies()
			.OrderBy(enemy => GetTileDistance(fromCell, enemy.CurrentCell))
			.FirstOrDefault();
	}

	private OfficerPawn GetClosestLivingOfficer(Vector2I fromCell)
	{
		return GetAliveOfficers()
			.OrderBy(officer => HasClearLineOfSight(fromCell, officer.CurrentCell) ? 0 : 1)
			.ThenBy(officer => GetTileDistance(fromCell, officer.CurrentCell))
			.FirstOrDefault();
	}

	private MissionNpcPawn GetClosestLivingEscortSurvivor(Vector2I fromCell)
	{
		return GetAliveEscortSurvivors()
			.OrderBy(npc => HasClearLineOfSight(fromCell, npc.CurrentCell) ? 0 : 1)
			.ThenBy(npc => GetTileDistance(fromCell, npc.CurrentCell))
			.FirstOrDefault();
	}

	private bool TryGetEnemyTarget(Vector2I fromCell, out OfficerPawn officer, out MissionNpcPawn survivor)
	{
		officer = GetClosestLivingOfficer(fromCell);
		survivor = GetClosestLivingEscortSurvivor(fromCell);
		if (officer == null && survivor == null)
		{
			return false;
		}

		if (officer == null)
		{
			return true;
		}

		if (survivor == null)
		{
			return true;
		}

		int officerLosPenalty = HasClearLineOfSight(fromCell, officer.CurrentCell) ? 0 : 1000;
		int survivorLosPenalty = HasClearLineOfSight(fromCell, survivor.CurrentCell) ? 0 : 1000;
		int officerScore = officerLosPenalty + GetTileDistance(fromCell, officer.CurrentCell);
		int survivorScore = survivorLosPenalty + GetTileDistance(fromCell, survivor.CurrentCell);
		if (officerScore <= survivorScore)
		{
			survivor = null;
		}
		else
		{
			officer = null;
		}

		return true;
	}

	private bool TryGetTraversableMovementPath(
		Vector2I startCell,
		Vector2I targetCell,
		OfficerPawn ignoreOfficer,
		MissionNpcPawn ignoreEnemy,
		IReadOnlyCollection<string> ignoredOfficerIds,
		out List<Vector2I> path)
	{
		return TryGetTraversableMovementPath(startCell, targetCell, ignoreOfficer, ignoreEnemy, ignoredOfficerIds, null, out path);
	}

	private bool TryGetTraversableMovementPath(
		Vector2I startCell,
		Vector2I targetCell,
		OfficerPawn ignoreOfficer,
		MissionNpcPawn ignoreEnemy,
		IReadOnlyCollection<string> ignoredOfficerIds,
		IReadOnlyCollection<string> ignoredSurvivorIds,
		out List<Vector2I> path)
	{
		path = new List<Vector2I>();
		if (_roomBuilder == null)
		{
			return false;
		}

		bool startIsWalkable = _roomBuilder.IsWalkableMovementCell(startCell) && !IsMovementCellBlockedByProp(startCell);
		bool targetIsWalkable = _roomBuilder.IsWalkableMovementCell(targetCell) && !IsMovementCellBlockedByProp(targetCell);
		if (!startIsWalkable || !targetIsWalkable)
		{
			return false;
		}

		if (startCell == targetCell)
		{
			path.Add(startCell);
			return true;
		}

		Queue<Vector2I> frontier = new Queue<Vector2I>();
		Dictionary<Vector2I, Vector2I> cameFrom = new Dictionary<Vector2I, Vector2I>();
		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		frontier.Enqueue(startCell);
		cameFrom[startCell] = startCell;

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (Vector2I direction in directions)
			{
				Vector2I next = current + direction;
				if (cameFrom.ContainsKey(next))
				{
					continue;
				}

				if (!_roomBuilder.IsWalkableMovementCell(next) || IsMovementCellBlockedByProp(next))
				{
					continue;
				}

				if (next != targetCell && IsCellOccupiedByLivingActor(next, ignoreOfficer, ignoreEnemy, ignoredOfficerIds, ignoredSurvivorIds))
				{
					continue;
				}

				if (!_roomBuilder.TryGetMovementPath(current, next, out List<Vector2I> localStepPath) || localStepPath.Count <= 1)
				{
					continue;
				}

				cameFrom[next] = current;
				if (next == targetCell)
				{
					path = ReconstructMovementPath(cameFrom, startCell, targetCell);
					return true;
				}

				frontier.Enqueue(next);
			}
		}

		return false;
	}

	private static List<Vector2I> ReconstructMovementPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I startCell, Vector2I targetCell)
	{
		List<Vector2I> path = new List<Vector2I>();
		Vector2I current = targetCell;
		path.Add(current);
		while (current != startCell)
		{
			current = cameFrom[current];
			path.Add(current);
		}

		path.Reverse();
		return path;
	}

	private void AppendCombatLog(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}

		_missionUi?.AppendCombatLog(message);
		_missionUi?.AppendActionLog(message);
	}

	private void AppendActionLog(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}

		_missionUi?.AppendActionLog(message);
	}

	private void UpdateCombatHoverSummary()
	{
		if (_missionUi == null || !_combatActive || _missionGameOver)
		{
			_hoveredCombatActor = null;
			_missionUi?.HideHoverSummary();
			return;
		}

		Node2D hoveredActor = FindHoveredCombatActor();
		if (hoveredActor == null)
		{
			_hoveredCombatActor = null;
			_missionUi.HideHoverSummary();
			return;
		}

		_hoveredCombatActor = hoveredActor;
		MissionCombatantSummary summary = hoveredActor switch
		{
			OfficerPawn officer => BuildOfficerSummary(officer),
			MissionNpcPawn enemy => BuildEnemySummary(enemy),
			_ => null
		};
		if (summary == null)
		{
			_missionUi.HideHoverSummary();
			return;
		}

		Vector2 screenPosition = GetViewport().GetCanvasTransform() * hoveredActor.GlobalPosition;
		_missionUi.ShowHoverSummary(summary, screenPosition);
	}

	private Node2D FindHoveredCombatActor()
	{
		Vector2 mousePosition = GetGlobalMousePosition();

		foreach (MissionNpcPawn enemy in _missionNpcs.Where(npc => npc != null && npc.Visible && !npc.IsDead))
		{
			if (BuildHoverBounds(enemy).HasPoint(mousePosition))
			{
				return enemy;
			}
		}

		foreach (OfficerPawn officer in _officerPawns.Where(pawn => pawn != null && pawn.Visible && !pawn.IsDead))
		{
			if (BuildHoverBounds(officer).HasPoint(mousePosition))
			{
				return officer;
			}
		}

		return null;
	}

	private static Rect2 BuildHoverBounds(Node2D actor)
	{
		return new Rect2(actor.GlobalPosition + new Vector2(-54f, -96f), new Vector2(108f, 144f));
	}

	private void UpdateMissionInteractionMenu()
	{
		if (_missionUi == null)
		{
			return;
		}

		if (ShouldSuppressInteractionMenu())
		{
			ClearInteractionMenu();
			return;
		}

		if (!_missionUi.IsInteractionMenuVisible || _activeInteractionMenuTarget == null)
		{
			return;
		}

		OfficerPawn activeOfficer = GetInteractionMenuOfficer();
		string officerId = activeOfficer?.OfficerID ?? string.Empty;
		if (_activeInteractionMenuOfficerId != officerId)
		{
			ShowInteractionMenuForTarget(_activeInteractionMenuTarget, activeOfficer);
		}
	}

	private bool ShouldSuppressInteractionMenu()
	{
		return _missionGameOver
			|| _isPanning
			|| _isSelectionDragging
			|| (_pauseMenuWrapper?.Visible ?? false)
			|| (_loadMenuWrapper?.Visible ?? false)
			|| (_dialogueUi?.IsConversationOpen ?? false)
			|| (_missionUi?.IsStoryEventVisible ?? false)
			|| (_missionUi?.IsConfirmationVisible ?? false)
			|| (_missionUi?.IsMissionSavePromptVisible ?? false)
			|| (_combatActive && (!IsPlayerTurnActive() || _enemyTurnInProgress));
	}

	private OfficerPawn GetInteractionMenuOfficer()
	{
		return GetSelectedEscortSurvivor() == null ? GetSelectedOfficer() : null;
	}

	private bool TryShowInteractionMenuAtMouse()
	{
		if (_missionUi == null || ShouldSuppressInteractionMenu())
		{
			return false;
		}

		if (!TryBuildHoveredInteractionMenuTarget(out MissionInteractionMenuTarget target))
		{
			return false;
		}

		ShowInteractionMenuForTarget(target, GetInteractionMenuOfficer());
		return true;
	}

	private bool TryBuildHoveredInteractionMenuTarget(out MissionInteractionMenuTarget target)
	{
		target = null;
		if (_roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		Vector2I hoveredCell = _roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		Vector2I hoveredBuildCell = GetBuildCell(hoveredCell);
		Vector2 screenPosition = GetViewport().GetMousePosition();

		if (GetNpcAtMovementCell(hoveredCell) is MissionNpcPawn npc
			&& !npc.IsHostile
			&& _visibleCells.Contains(npc.CurrentCell))
		{
			string npcDetails = !string.IsNullOrWhiteSpace(npc.Description)
				? npc.Description
				: !string.IsNullOrWhiteSpace(npc.Notes)
					? npc.Notes
					: "A mission contact awaiting instructions.";
			target = new MissionInteractionMenuTarget
			{
				Kind = MissionInteractionMenuTargetKind.Npc,
				TargetKey = $"npc:{npc.NpcId}",
				Title = npc.DisplayName,
				Body = npcDetails,
				ScreenPosition = screenPosition,
				BuildCell = hoveredBuildCell,
				Npc = npc
			};
			return true;
		}

		if (_missionPropsByCell.TryGetValue(hoveredBuildCell, out MissionProp prop)
			&& prop != null
			&& (_visibleBuildCells.Contains(hoveredBuildCell) || prop.Visible))
		{
			string propDescription = !string.IsNullOrWhiteSpace(prop.Definition?.Description)
				? prop.Definition.Description
				: !string.IsNullOrWhiteSpace(prop.Definition?.SuccessMessage)
					? prop.Definition.SuccessMessage
					: "Mission equipment ready for field interaction.";
			target = new MissionInteractionMenuTarget
			{
				Kind = MissionInteractionMenuTargetKind.Prop,
				TargetKey = $"prop:{prop.PropInstanceId}",
				Title = prop.Definition?.DisplayName ?? prop.Name,
				Body = propDescription,
				ScreenPosition = screenPosition,
				BuildCell = hoveredBuildCell,
				Prop = prop
			};
			return true;
		}

		if (_roomBuilder.TryGetDoorIdAtCell(hoveredBuildCell, out string doorId)
			&& !string.IsNullOrWhiteSpace(doorId)
			&& IsStructureCellVisible(hoveredBuildCell))
		{
			target = new MissionInteractionMenuTarget
			{
				Kind = MissionInteractionMenuTargetKind.Door,
				TargetKey = $"door:{doorId}",
				Title = "Bulkhead Door",
				Body = _roomBuilder.IsDoorOpen(doorId)
					? "An unlocked bulkhead with the passage already open."
					: "A sealed bulkhead blocking the route ahead.",
				ScreenPosition = screenPosition,
				BuildCell = hoveredBuildCell,
				DoorId = doorId
			};
			return true;
		}

		if (TryGetStaticInteractionAtMovementCell(hoveredCell, out MissionRoomBuilder.MarkerPlacement interaction)
			&& interaction != null
			&& _visibleBuildCells.Contains(interaction.Cell))
		{
			target = new MissionInteractionMenuTarget
			{
				Kind = MissionInteractionMenuTargetKind.StaticInteraction,
				TargetKey = $"marker:{BuildInteractionKey(interaction)}",
				Title = GetInteractionDisplayName(interaction),
				Body = BuildInteractionMenuDescription(interaction),
				ScreenPosition = screenPosition,
				BuildCell = interaction.Cell,
				Interaction = interaction
			};
			return true;
		}

		return false;
	}

	private void ShowInteractionMenuForTarget(MissionInteractionMenuTarget target, OfficerPawn officer)
	{
		if (_missionUi == null || target == null)
		{
			return;
		}

		_activeInteractionMenuTarget = target;
		_activeInteractionMenuOfficerId = officer?.OfficerID ?? string.Empty;
		_missionUi.ShowInteractionMenu(
			target.Title,
			target.Body,
			target.ScreenPosition,
			BuildInteractionMenuOptions(target, officer));
	}

	private void ClearInteractionMenu()
	{
		_activeInteractionMenuTarget = null;
		_activeInteractionMenuOfficerId = string.Empty;
		_missionUi?.HideInteractionMenu();
	}

	private List<MissionInteractionMenuOption> BuildInteractionMenuOptions(MissionInteractionMenuTarget target, OfficerPawn officer)
	{
		List<MissionInteractionMenuOption> options = new List<MissionInteractionMenuOption>();
		if (target == null)
		{
			return options;
		}

		string selectOfficerMessage = "Select an officer to issue interaction commands.";
		switch (target.Kind)
		{
			case MissionInteractionMenuTargetKind.Door:
			{
				MissionRoomBuilder.MarkerPlacement doorInteraction = BuildDoorInteractionMarker(target.BuildCell, target.DoorId);
				bool canExecute = officer != null && CanOfficerExecuteInteraction(officer, doorInteraction);
				bool canApproach = officer != null && FindBestInteractionApproachCell(officer, doorInteraction).HasValue;
				bool doorOpen = _roomBuilder?.IsDoorOpen(target.DoorId) == true;
				bool doorwayBlocked = doorOpen && _officerPawns.Any(pawn => pawn != null && !pawn.IsDead && GetBuildCell(pawn.CurrentCell) == target.BuildCell);
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "primary",
					Label = doorOpen ? "Close Bulkhead" : "Open Bulkhead",
					Description = doorwayBlocked
						? "Clear the doorway before sealing this bulkhead."
						: officer == null
						? selectOfficerMessage
						: canExecute
							? (doorOpen ? "Close this bulkhead immediately." : "Open this bulkhead immediately.")
							: canApproach
								? (doorOpen ? "Move adjacent, then close this bulkhead." : "Move adjacent, then open this bulkhead.")
								: "No clear path to operate this bulkhead.",
					Disabled = doorwayBlocked || officer == null || (!canExecute && !canApproach)
				});
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "move",
					Label = "Approach",
					Description = officer == null
						? selectOfficerMessage
						: canApproach
							? "Move into position beside this bulkhead."
							: "No clear path to the door.",
					Disabled = officer == null || !canApproach
				});
				break;
			}
			case MissionInteractionMenuTargetKind.Prop:
			{
				bool isAvailable = officer != null && target.Prop != null && target.Prop.CanInteract(BuildPropInteractionContext(officer, target.Prop));
				bool canExecute = officer != null && target.Prop != null && isAvailable && CanOfficerExecutePropInteraction(officer, target.Prop);
				bool canApproach = officer != null && target.Prop != null && isAvailable && FindBestPropApproachCell(officer, target.Prop).HasValue;
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "primary",
					Label = GetPropInteractionActionLabel(target.Prop),
					Description = officer == null
						? selectOfficerMessage
						: canExecute
							? "Use this mission object now."
							: canApproach
								? "Move into range, then interact with this object."
								: "This object is not available right now.",
					Disabled = officer == null || (!canExecute && !canApproach)
				});
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "move",
					Label = "Approach",
					Description = officer == null
						? selectOfficerMessage
						: canApproach
							? "Move into interaction range."
							: "No clear path to this object.",
					Disabled = officer == null || !canApproach
				});
				break;
			}
			case MissionInteractionMenuTargetKind.Npc:
			{
				bool isAvailable = officer != null && target.Npc != null && target.Npc.CanInteract(BuildNpcInteractionContext(officer, target.Npc));
				bool canExecute = officer != null && target.Npc != null && isAvailable && CanOfficerExecuteNpcInteraction(officer, target.Npc);
				bool canApproach = officer != null && target.Npc != null && isAvailable && FindBestNpcApproachCell(officer, target.Npc).HasValue;
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "primary",
					Label = string.IsNullOrWhiteSpace(target.Npc?.DialogueId) ? "Interact" : "Talk",
					Description = officer == null
						? selectOfficerMessage
						: canExecute
							? "Speak with this contact now."
							: canApproach
								? "Move into range, then speak with this contact."
								: "This contact is unavailable right now.",
					Disabled = officer == null || (!canExecute && !canApproach)
				});
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "move",
					Label = "Approach",
					Description = officer == null
						? selectOfficerMessage
						: canApproach
							? "Move next to this contact."
							: "No clear path to this contact.",
					Disabled = officer == null || !canApproach
				});
				break;
			}
			case MissionInteractionMenuTargetKind.StaticInteraction:
			{
				bool interactionReady = IsStaticInteractionAvailable(target.Interaction);
				bool canExecute = officer != null && interactionReady && CanOfficerExecuteInteraction(officer, target.Interaction);
				bool canApproach = officer != null && interactionReady && FindBestInteractionApproachCell(officer, target.Interaction).HasValue;
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "primary",
					Label = GetStaticInteractionActionLabel(target.Interaction),
					Description = officer == null
						? selectOfficerMessage
						: canExecute
							? "Use this mission interaction now."
							: canApproach
								? "Move into range, then use this interaction."
								: "This interaction is unavailable right now.",
					Disabled = officer == null || (!canExecute && !canApproach)
				});
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "move",
					Label = "Approach",
					Description = officer == null
						? selectOfficerMessage
						: canApproach
							? "Move into position for this interaction."
							: "No clear path to this interaction point.",
					Disabled = officer == null || !canApproach
				});
				break;
			}
		}

		return options;
	}

	private void OnInteractionMenuOptionChosen(string actionId)
	{
		if (string.IsNullOrWhiteSpace(actionId) || _activeInteractionMenuTarget == null)
		{
			return;
		}

		OfficerPawn officer = GetInteractionMenuOfficer();
		bool handled = actionId switch
		{
			"primary" => RequestInteractionMenuPrimary(officer, _activeInteractionMenuTarget),
			"move" => RequestInteractionMenuApproach(officer, _activeInteractionMenuTarget),
			_ => false
		};

		if (handled)
		{
			ClearInteractionMenu();
		}
	}

	private bool RequestInteractionMenuPrimary(OfficerPawn officer, MissionInteractionMenuTarget target)
	{
		if (officer == null || target == null)
		{
			return false;
		}

		return target.Kind switch
		{
			MissionInteractionMenuTargetKind.Door => RequestDoorInteraction(officer, target.BuildCell, target.DoorId),
			MissionInteractionMenuTargetKind.Prop => target.Prop != null && RequestPropInteraction(officer, target.Prop),
			MissionInteractionMenuTargetKind.Npc => target.Npc != null && RequestNpcInteraction(officer, target.Npc),
			MissionInteractionMenuTargetKind.StaticInteraction => target.Interaction != null && RequestStaticInteraction(officer, target.Interaction),
			_ => false
		};
	}

	private bool RequestInteractionMenuApproach(OfficerPawn officer, MissionInteractionMenuTarget target)
	{
		if (officer == null || target == null)
		{
			return false;
		}

		Vector2I? approachCell = target.Kind switch
		{
			MissionInteractionMenuTargetKind.Door => FindBestInteractionApproachCell(officer, BuildDoorInteractionMarker(target.BuildCell, target.DoorId)),
			MissionInteractionMenuTargetKind.Prop => target.Prop != null ? FindBestPropApproachCell(officer, target.Prop) : null,
			MissionInteractionMenuTargetKind.Npc => target.Npc != null ? FindBestNpcApproachCell(officer, target.Npc) : null,
			MissionInteractionMenuTargetKind.StaticInteraction => target.Interaction != null ? FindBestInteractionApproachCell(officer, target.Interaction) : null,
			_ => null
		};
		if (!approachCell.HasValue)
		{
			return false;
		}

		ClearPendingInteractionRequests();
		return TryMoveOfficerToCell(officer, approachCell.Value);
	}

	private bool RequestDoorInteraction(OfficerPawn officer, Vector2I buildCell, string doorId)
	{
		if (officer == null || _roomBuilder == null || string.IsNullOrWhiteSpace(doorId) || !IsStructureCellVisible(buildCell))
		{
			return false;
		}

		MissionRoomBuilder.MarkerPlacement interaction = BuildDoorInteractionMarker(buildCell, doorId);
		bool nextOpenState = !_roomBuilder.IsDoorOpen(doorId);
		if (!nextOpenState && _officerPawns.Any(pawn => pawn != null && !pawn.IsDead && GetBuildCell(pawn.CurrentCell) == buildCell))
		{
			return false;
		}

		if (CanOfficerExecuteInteraction(officer, interaction))
		{
			ClearPendingInteractionRequests();
			ExecuteInteraction(officer, interaction);
			return true;
		}

		Vector2I? approachCell = FindBestInteractionApproachCell(officer, interaction);
		if (!approachCell.HasValue || !TryMoveOfficerToCell(officer, approachCell.Value))
		{
			return false;
		}

		ClearPendingInteractionRequests();
		_pendingDoorId = doorId;
		_pendingInteractionOfficerId = officer.OfficerID;
		return true;
	}

	private bool RequestPropInteraction(OfficerPawn officer, MissionProp prop)
	{
		if (officer == null || prop == null)
		{
			return false;
		}

		if (CanOfficerExecutePropInteraction(officer, prop))
		{
			ClearPendingInteractionRequests();
			ExecutePropInteraction(officer, prop);
			return true;
		}

		Vector2I? approachCell = FindBestPropApproachCell(officer, prop);
		if (!approachCell.HasValue || !TryMoveOfficerToCell(officer, approachCell.Value))
		{
			return false;
		}

		ClearPendingInteractionRequests();
		_pendingPropInstanceId = prop.PropInstanceId;
		_pendingInteractionOfficerId = officer.OfficerID;
		return true;
	}

	private bool RequestNpcInteraction(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null)
		{
			return false;
		}

		if (npc.IsHostile)
		{
			return false;
		}

		if (CanOfficerExecuteNpcInteraction(officer, npc))
		{
			ClearPendingInteractionRequests();
			ExecuteNpcInteraction(officer, npc);
			return true;
		}

		Vector2I? approachCell = FindBestNpcApproachCell(officer, npc);
		if (!approachCell.HasValue || !TryMoveOfficerToCell(officer, approachCell.Value))
		{
			return false;
		}

		ClearPendingInteractionRequests();
		_pendingNpcId = npc.NpcId;
		_pendingInteractionOfficerId = officer.OfficerID;
		return true;
	}

	private bool RequestStaticInteraction(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (officer == null || interaction == null || !IsStaticInteractionAvailable(interaction))
		{
			return false;
		}

		if (CanOfficerExecuteInteraction(officer, interaction))
		{
			ClearPendingInteractionRequests();
			ExecuteInteraction(officer, interaction);
			return true;
		}

		Vector2I? approachCell = FindBestInteractionApproachCell(officer, interaction);
		if (!approachCell.HasValue || !TryMoveOfficerToCell(officer, approachCell.Value))
		{
			return false;
		}

		ClearPendingInteractionRequests();
		_pendingInteractionKey = BuildInteractionKey(interaction);
		_pendingInteractionOfficerId = officer.OfficerID;
		return true;
	}

	private void ClearPendingInteractionRequests()
	{
		_pendingInteractionKey = string.Empty;
		_pendingInteractionOfficerId = string.Empty;
		_pendingDoorId = string.Empty;
		_pendingPropInstanceId = string.Empty;
		_pendingNpcId = string.Empty;
	}

	private MissionRoomBuilder.MarkerPlacement BuildDoorInteractionMarker(Vector2I buildCell, string doorId)
	{
		return new MissionRoomBuilder.MarkerPlacement
		{
			LogicRole = "door",
			Label = "Bulkhead Door",
			TargetId = doorId,
			TriggerMode = "interact",
			Cell = buildCell
		};
	}

	private string BuildInteractionMenuDescription(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction == null)
		{
			return "Mission interaction available.";
		}

		if (interaction.MarkerId == "evac_zone")
		{
			return "Extract from the mission once your away team is assembled here.";
		}

		if (string.Equals(interaction.LogicRole, "terminal", StringComparison.OrdinalIgnoreCase))
		{
			return !string.IsNullOrWhiteSpace(interaction.TargetId)
				? "A control point linked to local bulkheads and mission systems."
				: "A mission terminal waiting for operator input.";
		}

		if (interaction.MarkerId == "trigger_dialogue")
		{
			return "A conversation point that can advance the current mission thread.";
		}

		return "A mission interaction point with contextual effects.";
	}

	private string GetPropInteractionActionLabel(MissionProp prop)
	{
		if (prop?.Definition == null)
		{
			return "Interact";
		}

		if (prop.Definition.PropId == MedicalBedHealPropId)
		{
			return "Treat Wounds";
		}

		return prop.Definition.InteractionType switch
		{
			PropInteractionType.Dialogue => "Access",
			PropInteractionType.Loot => "Loot",
			PropInteractionType.Hack => "Hack",
			PropInteractionType.DoorControl => "Use Control",
			PropInteractionType.Datapad => "Read",
			_ => "Interact"
		};
	}

	private string GetStaticInteractionActionLabel(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction == null)
		{
			return "Interact";
		}

		if (interaction.MarkerId == "evac_zone")
		{
			return "Secure Evac";
		}

		if (string.Equals(interaction.LogicRole, "terminal", StringComparison.OrdinalIgnoreCase))
		{
			return "Use Terminal";
		}

		if (interaction.MarkerId == "trigger_dialogue")
		{
			return "Talk";
		}

		return "Interact";
	}

	private bool IsStaticInteractionAvailable(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction == null)
		{
			return false;
		}

		if (interaction.OneShot && _consumedTriggerKeys.Contains(BuildInteractionKey(interaction)))
		{
			return false;
		}

		return string.IsNullOrEmpty(interaction.RequiredFlag)
			|| (_globalData?.StoryFlags?.Contains(interaction.RequiredFlag) == true);
	}

	private void ReindexMissionNpcCells()
	{
		_missionNpcsByCell.Clear();
		foreach (MissionNpcPawn npc in _missionNpcs.Where(npc => npc != null && !npc.IsDead && !npc.IsExtracted))
		{
			_missionNpcsByCell[npc.CurrentCell] = npc;
		}
	}

	private void OnOfficerCombatStateChanged(OfficerPawn pawn)
	{
		RefreshCombatHud();
	}

	private void OnMissionNpcCombatStateChanged(MissionNpcPawn pawn)
	{
		RefreshCombatHud();
	}

	private void OnOfficerDied(OfficerPawn pawn)
	{
		if (pawn != null)
		{
			AppendCombatLog($"{pawn.OfficerName} collapses under enemy fire. Their post goes dark.");
		}

		RefreshCombatHud();
		UpdateFogOfWar();
		if (!GetAliveOfficers().Any())
		{
			HandleMissionGameOver();
			return;
		}

		int nextLivingIndex = _officerPawns.FindIndex(candidate => candidate != null && !candidate.IsDead);
		if (nextLivingIndex >= 0)
		{
			SelectOfficer(nextLivingIndex);
		}
	}

	private void OnMissionNpcDied(MissionNpcPawn pawn)
	{
		if (pawn != null)
		{
			AppendCombatLog(IsEscortSurvivor(pawn)
				? $"{pawn.DisplayName} is caught in the crossfire and falls before reaching evac."
				: $"{pawn.DisplayName} goes down and stops fighting.");
		}

		if (pawn != null && !string.IsNullOrWhiteSpace(pawn.NpcId) && pawn.IsHostile)
		{
			_engagedEnemyIds.Remove(pawn.NpcId);
		}

		if (pawn != null && !string.IsNullOrWhiteSpace(pawn.NpcId))
		{
			_selectedEscortSurvivorIds.Remove(pawn.NpcId);
			if (pawn.NpcId == _selectedEscortSurvivorId)
			{
				_selectedEscortSurvivorId = string.Empty;
			}
		}
		NormalizeFriendlySelectionState();
		UpdateOfficerSelectionVisuals();
		UpdateSelectedOfficerDisplay();

		ReindexMissionNpcCells();
		UpdateFogOfWar();
		UpdateMissionCompletionActions();
		RefreshCombatHud();
	}

	private void OnMissionNpcEnteredCell(MissionNpcPawn pawn, Vector2I cell)
	{
		ReindexMissionNpcCells();
	}

	private void OnMissionNpcReachedCell(MissionNpcPawn pawn, Vector2I cell)
	{
		ReindexMissionNpcCells();
		TryExtractEscortSurvivor(pawn);
		if (pawn != null && pawn.NpcId == _pendingCombatMoveEscortSurvivorId)
		{
			_pendingCombatMoveEscortSurvivorId = string.Empty;
			if (_combatActive && GetActiveCombatEscortSurvivor() == pawn && (pawn.IsExtracted || pawn.CurrentActions <= 0))
			{
				EndCurrentCombatTurn();
			}
		}
		UpdateFogOfWar();
		RefreshCombatHud();
	}

	private void RefreshCombatHud()
	{
		if (_missionUi == null)
		{
			return;
		}

		bool showCombatHud = _combatActive && !_missionGameOver;
		_missionUi.SetCombatHudVisible(showCombatHud);
		if (!showCombatHud)
		{
			_missionUi.SetCombatEndTurnEnabled(false, false);
			_missionUi.SetPlayerCombatInfo(null);
			_missionUi.SetEnemyCombatInfo(null);
			_missionUi.SetExplorationSelectionInfo(GetSelectedFriendlySummaries(), !_missionGameOver);
			_missionUi.SetCombatTurnLabel("MISSION COMBAT");
			_missionUi.SetCombatInitiative(Array.Empty<MissionCombatantSummary>(), -1);
			RefreshMissionPrompt();
			return;
		}

		MissionCombatTurnEntry activeEntry = GetActiveCombatTurnEntry();
		string turnLabel = activeEntry == null
			? $"ROUND {_combatRound}"
			: activeEntry.IsOfficer
				? $"ROUND {_combatRound} - {activeEntry.Officer.OfficerName.ToUpperInvariant()} TURN"
				: $"ROUND {_combatRound} - {activeEntry.Enemy.DisplayName.ToUpperInvariant()} TURN";
		_missionUi.SetCombatTurnLabel(turnLabel);
		_missionUi.SetCombatInitiative(_combatQueue
			.Select(entry => entry.IsOfficer ? BuildOfficerSummary(entry.Officer) : BuildEnemySummary(entry.Enemy))
			.ToList(), _combatActiveIndex);

		MissionNpcPawn playerSurvivor = GetActiveCombatEscortSurvivor() ?? GetSelectedEscortSurvivor();
		OfficerPawn playerOfficer = playerSurvivor == null ? (GetActiveCombatOfficer() ?? GetSelectedOfficer()) : null;
		Vector2I playerCell = playerOfficer?.CurrentCell ?? playerSurvivor?.CurrentCell ?? Vector2I.Zero;
		MissionNpcPawn enemyFocus = GetActiveCombatEnemy() ?? (_focusedEnemy != null && !_focusedEnemy.IsDead ? _focusedEnemy : GetClosestVisibleEnemy(playerCell));
		_missionUi.SetPlayerCombatInfo(playerOfficer != null ? BuildOfficerSummary(playerOfficer) : BuildEnemySummary(playerSurvivor));
		_missionUi.SetEnemyCombatInfo(BuildEnemySummary(enemyFocus));
		bool canPlayerEndTurn = _combatActive && !_enemyTurnInProgress && (playerOfficer != null || playerSurvivor != null);
		_missionUi.SetCombatEndTurnEnabled(canPlayerEndTurn, _combatActive);
		RefreshMissionPrompt();
	}

	private MissionCombatantSummary BuildOfficerSummary(OfficerPawn officer)
	{
		if (officer == null)
		{
			return null;
		}

		Texture2D icon = LoadPortraitTexture(officer.PortraitPath);
		return new MissionCombatantSummary
		{
			DisplayName = officer.OfficerName,
			Subtitle = $"{officer.Specialty} | {officer.ShipName}",
			WeaponName = officer.WeaponName,
			ShieldName = officer.ShieldName,
			Icon = icon,
			CurrentHP = officer.CurrentHP,
			MaxHP = officer.MaxHP,
			CurrentShields = officer.CurrentShields,
			MaxShields = officer.MaxShields,
			CurrentAP = officer.CurrentActions,
			MaxAP = officer.MaxActions,
			AttackRange = officer.AttackRange,
			AttackMinDamage = officer.AttackMinDamage,
			AttackMaxDamage = officer.AttackDamage,
			Notes = BuildCombatantNotes(officer.InitiativeBonus, officer.ShieldRechargePerTurn, officer.BonusShieldDamage, officer.ShieldPiercingDamage, officer.WeaponStatusEffectId, officer.WeaponStatusEffectChance, officer.ActiveStatusEffectId)
		};
	}

	private MissionCombatantSummary BuildEnemySummary(MissionNpcPawn enemy)
	{
		if (enemy == null)
		{
			return null;
		}

		Texture2D icon = LoadPortraitTexture(enemy.PortraitPath);
		if (icon == null && enemy.GetNodeOrNull<Sprite2D>("Sprite2D") is Sprite2D sprite)
		{
			icon = sprite.Texture;
		}

		return new MissionCombatantSummary
		{
			DisplayName = enemy.DisplayName,
			Subtitle = enemy.IsHostile ? "Hostile Contact" : IsEscortSurvivor(enemy) ? $"Escort {CampaignText.RemnantsLabel.TrimEnd('s')}" : "Mission Contact",
			WeaponName = enemy.WeaponName,
			ShieldName = enemy.ShieldName,
			Icon = icon,
			CurrentHP = enemy.CurrentHP,
			MaxHP = enemy.MaxHP,
			CurrentShields = enemy.CurrentShields,
			MaxShields = enemy.MaxShields,
			CurrentAP = enemy.CurrentActions,
			MaxAP = enemy.MaxActions,
			AttackRange = enemy.AttackRange,
			AttackMinDamage = enemy.AttackMinDamage,
			AttackMaxDamage = enemy.AttackDamage,
			Notes = enemy.IsHostile || IsEscortSurvivor(enemy)
				? BuildCombatantNotes(enemy.InitiativeBonus, enemy.ShieldRechargePerTurn, enemy.BonusShieldDamage, enemy.ShieldPiercingDamage, enemy.WeaponStatusEffectId, enemy.WeaponStatusEffectChance, enemy.ActiveStatusEffectId)
				: "Non-hostile contact"
		};
	}

	private Texture2D LoadPortraitTexture(string resourcePath)
	{
		if (string.IsNullOrWhiteSpace(resourcePath) || !ResourceLoader.Exists(resourcePath))
		{
			return null;
		}

		if (_portraitTextureCache.TryGetValue(resourcePath, out Texture2D cachedTexture))
		{
			return cachedTexture;
		}

		Texture2D loadedTexture = ResourceLoader.Load<Texture2D>(resourcePath, string.Empty, ResourceLoader.CacheMode.IgnoreDeep);
		if (loadedTexture != null)
		{
			_portraitTextureCache[resourcePath] = loadedTexture;
		}

		return loadedTexture;
	}

	private static string BuildCombatantNotes(int initiativeBonus, int shieldRechargePerTurn, int bonusShieldDamage, int shieldPiercingDamage, string statusEffectId, float statusEffectChance, string activeStatusEffectId)
	{
		List<string> notes = new List<string>
		{
			$"Initiative bonus: +{initiativeBonus}",
			$"Shield recharge: +{shieldRechargePerTurn}/turn"
		};

		if (bonusShieldDamage > 0)
		{
			notes.Add($"Shield break: +{bonusShieldDamage}");
		}

		if (shieldPiercingDamage > 0)
		{
			notes.Add($"Piercing: +{shieldPiercingDamage}");
		}

		if (!string.IsNullOrWhiteSpace(statusEffectId) && statusEffectChance > 0f)
		{
			notes.Add($"Status: {statusEffectId} {(int)(statusEffectChance * 100f)}%");
		}

		if (!string.IsNullOrWhiteSpace(activeStatusEffectId))
		{
			notes.Add($"Afflicted: {activeStatusEffectId}");
		}

		return string.Join("\n", notes);
	}

	private static string BuildActionResultMessage(string actorName, string statusMessage, string fallbackMessage)
	{
		if (!string.IsNullOrWhiteSpace(statusMessage))
		{
			return string.IsNullOrWhiteSpace(actorName)
				? statusMessage.Trim()
				: $"{actorName}: {statusMessage.Trim()}";
		}

		return fallbackMessage;
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

		return label;
	}

	private string ResolveDialogueTargetId(MissionRoomBuilder.MarkerPlacement marker)
	{
		if (!string.IsNullOrEmpty(marker.TargetId))
		{
			return marker.TargetId;
		}

		if (marker.MarkerId == "trigger_dialogue" && !string.IsNullOrWhiteSpace(_missionTemplate?.DefaultDialogueId))
		{
			return _missionTemplate.DefaultDialogueId;
		}

		return marker.MarkerId;
	}

	private void OnMissionConversationEnded()
	{
		UpdateSelectedOfficerDisplay();
	}

	private void OnMissionDialogueStateChanged()
	{
		UpdateMissionCompletionActions();
	}

	private void OnOfficerEnteredCell(OfficerPawn pawn, Vector2I cell)
	{
		UpdateFogOfWar();
		UpdateMissionCompletionActions();
		CheckEnterTriggers(pawn);
	}

	private void OnOfficerReachedCell(OfficerPawn pawn, Vector2I cell)
	{
		UpdateMissionCompletionActions();
		RefreshCombatHud();
		EvaluateCombatState();

		if (!string.IsNullOrEmpty(_pendingCombatMoveOfficerId) && pawn != null && pawn.OfficerID == _pendingCombatMoveOfficerId)
		{
			_pendingCombatMoveOfficerId = string.Empty;
			_pendingCombatMoveCost = 0;

			if (!string.IsNullOrEmpty(_pendingCombatAttackEnemyId))
			{
				MissionNpcPawn pendingEnemy = _missionNpcs.FirstOrDefault(npc => npc != null && npc.NpcId == _pendingCombatAttackEnemyId && !npc.IsDead);
				_pendingCombatAttackEnemyId = string.Empty;
				if (pendingEnemy != null && _combatActive)
				{
					PerformOfficerAttack(pawn, pendingEnemy);
				}
			}
		}

		if (pawn == null || pawn.OfficerID != _pendingInteractionOfficerId)
		{
			TryPromptMedicalBedUseAtCurrentCell(pawn);
			return;
		}

		if (!string.IsNullOrEmpty(_pendingNpcId))
		{
			MissionNpcPawn pendingNpc = _missionNpcs.FirstOrDefault(npc => npc != null && npc.NpcId == _pendingNpcId);
			_pendingNpcId = string.Empty;
			_pendingInteractionOfficerId = string.Empty;
			if (pendingNpc != null && CanOfficerExecuteNpcInteraction(pawn, pendingNpc))
			{
				ExecuteNpcInteraction(pawn, pendingNpc);
			}
			return;
		}

		if (!string.IsNullOrEmpty(_pendingPropInstanceId))
		{
			MissionProp pendingProp = _missionPropsByCell.Values.FirstOrDefault(prop => prop != null && prop.PropInstanceId == _pendingPropInstanceId);
			_pendingPropInstanceId = string.Empty;
			_pendingInteractionOfficerId = string.Empty;
			if (pendingProp != null && CanOfficerExecutePropInteraction(pawn, pendingProp))
			{
				ExecutePropInteraction(pawn, pendingProp);
			}
			return;
		}

		if (!string.IsNullOrEmpty(_pendingDoorId))
		{
			string pendingDoorId = _pendingDoorId;
			_pendingDoorId = string.Empty;
			_pendingInteractionOfficerId = string.Empty;
			MissionRoomBuilder.MarkerPlacement directDoorInteraction = new MissionRoomBuilder.MarkerPlacement
			{
				LogicRole = "door",
				Label = "Bulkhead Door",
				TargetId = pendingDoorId,
				TriggerMode = "interact",
				Cell = cell
			};
			if (CanOfficerExecuteInteraction(pawn, directDoorInteraction))
			{
				ExecuteInteraction(pawn, directDoorInteraction);
			}
			return;
		}

		if (string.IsNullOrEmpty(_pendingInteractionKey))
		{
			return;
		}

		MissionRoomBuilder.MarkerPlacement interaction = _roomBuilder?.GetMarkerPlacements()
			.FirstOrDefault(placement => BuildInteractionKey(placement) == _pendingInteractionKey);
		_pendingInteractionKey = string.Empty;
		_pendingInteractionOfficerId = string.Empty;
		if (interaction != null && CanOfficerExecuteInteraction(pawn, interaction))
		{
			ExecuteInteraction(pawn, interaction);
			return;
		}

		TryPromptMedicalBedUseAtCurrentCell(pawn);
	}

	private void TryPromptMedicalBedUseAtCurrentCell(OfficerPawn officer)
	{
		if (officer == null || _missionUi == null || _pendingMedicalBedProp != null)
		{
			return;
		}

		Vector2I buildCell = GetBuildCell(officer.CurrentCell);
		if (!_missionPropsByCell.TryGetValue(buildCell, out MissionProp prop))
		{
			return;
		}

		TryPromptMedicalBedUse(officer, prop);
	}

	private bool TryPromptMedicalBedUse(OfficerPawn officer, MissionProp prop)
	{
		if (!ShouldOfferMedicalBedUse(officer, prop))
		{
			return false;
		}

		_pendingMedicalBedProp = prop;
		_pendingMedicalBedOfficerId = officer.OfficerID;
		_missionUi?.ShowConfirmationPrompt(
			"Medical Bed",
			$"{officer.OfficerName} is injured.\nUse the medical bed to restore their HP to full?",
			"Heal",
			"Skip");
		return true;
	}

	private bool ShouldOfferMedicalBedUse(OfficerPawn officer, MissionProp prop)
	{
		if (officer == null || prop == null || officer.IsDead || officer.CurrentHP >= officer.MaxHP)
		{
			return false;
		}

		if (!IsMedicalBedProp(prop))
		{
			return false;
		}

		if (_combatActive && officer.CurrentActions < CombatInteractionActionCost)
		{
			return false;
		}

		return true;
	}

	private static bool IsMedicalBedProp(MissionProp prop)
	{
		return string.Equals(prop?.Definition?.PropId, MedicalBedHealPropId, StringComparison.Ordinal);
	}

	private void ConfirmMedicalBedUse()
	{
		OfficerPawn officer = _officerPawns.FirstOrDefault(candidate => candidate != null && candidate.OfficerID == _pendingMedicalBedOfficerId);
		MissionProp prop = _pendingMedicalBedProp;
		if (officer == null || prop == null || !ShouldOfferMedicalBedUse(officer, prop))
		{
			ClearPendingConfirmation();
			return;
		}

		if (_combatActive)
		{
			officer.SpendActions(CombatInteractionActionCost);
		}

		int healedAmount = officer.RestoreHealthToFull();
		if (healedAmount > 0)
		{
			if (_missionUi?.PromptLabel != null)
			{
				_missionUi.PromptLabel.Text = $"{officer.OfficerName} used the medical bed and recovered {healedAmount} HP.";
			}
			AppendCombatLog($"{officer.OfficerName} used a medical bed and recovered {healedAmount} HP.");
		}

		UpdateSelectedOfficerDisplay();
		RefreshCombatHud();
		ClearPendingConfirmation();
	}

	private void ClearPendingConfirmation()
	{
		_pendingMedicalBedProp = null;
		_pendingMedicalBedOfficerId = string.Empty;
		_missionUi?.HideConfirmationPrompt();
	}

	private void SpawnMissionProps()
	{
		_missionPropsByCell.Clear();
		_propPlacementsByInstanceId.Clear();
		_blockedStaticPropCells.Clear();
		if (_roomBuilder == null || _isoWorld == null)
		{
			return;
		}

		Node2D runtimePropLayer = _isoWorld.GetNodeOrNull<Node2D>("RuntimePropLayer");
		if (runtimePropLayer == null)
		{
			runtimePropLayer = new Node2D
			{
				Name = "RuntimePropLayer",
				ZIndex = 6
			};
			_isoWorld.AddChild(runtimePropLayer);
		}

		foreach (Node child in runtimePropLayer.GetChildren())
		{
			runtimePropLayer.RemoveChild(child);
			child.QueueFree();
		}

		foreach (MissionRoomBuilder.MarkerPlacement placement in _roomBuilder.GetMarkerPlacements())
		{
			PropDefinition definition = ResolvePropDefinitionForPlacement(placement);
			if (definition == null || string.IsNullOrWhiteSpace(definition.ScenePath))
			{
				continue;
			}

			PackedScene propScene = GD.Load<PackedScene>(definition.ScenePath);
			if (propScene == null)
			{
				continue;
			}

			MissionProp prop = propScene.Instantiate<MissionProp>();
			if (prop == null)
			{
				continue;
			}

			prop.Definition = definition;
			prop.Name = $"{definition.PropId}_{placement.Cell.X}_{placement.Cell.Y}";
			prop.PropInstanceId = BuildPropInstanceId(placement, definition);
			prop.Position = _roomBuilder.GetCellWorldPosition(placement.Cell.X, placement.Cell.Y);
			prop.PlacementRotationDegrees = placement.RotationDegrees;
			prop.PlacementFlipH = placement.FlipH;
			prop.PlacementFlipV = placement.FlipV;
			runtimePropLayer.AddChild(prop);
			_missionPropsByCell[placement.Cell] = prop;
			_propPlacementsByInstanceId[prop.PropInstanceId] = placement;
		}

		RefreshBlockedPropCells();
	}

	private void RefreshBlockedPropCells()
	{
		_blockedStaticPropCells.Clear();
		Node2D propLayer = _isoWorld?.GetNodeOrNull<Node2D>("PropLayer");
		if (propLayer == null)
		{
			return;
		}

		foreach (Node child in propLayer.GetChildren())
		{
			if (child is not Sprite2D sprite)
			{
				continue;
			}

			string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
			if (string.IsNullOrWhiteSpace(tileId) || tileId.StartsWith("door_", System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			int column = sprite.GetMeta("column", int.MinValue).AsInt32();
			int row = sprite.GetMeta("row", int.MinValue).AsInt32();
			if (column == int.MinValue || row == int.MinValue)
			{
				continue;
			}

			_blockedStaticPropCells.Add(new Vector2I(column, row));
		}
	}

	private PropDefinition ResolvePropDefinitionForPlacement(MissionRoomBuilder.MarkerPlacement placement)
	{
		if (placement == null)
		{
			return null;
		}

		if (!string.IsNullOrWhiteSpace(placement.PropDefinitionPath))
		{
			PropDefinition baseDefinition = GD.Load<PropDefinition>(placement.PropDefinitionPath);
			if (baseDefinition == null)
			{
				return null;
			}

			PropDefinition resolvedDefinition = baseDefinition.Duplicate(true) as PropDefinition ?? baseDefinition;
			PropPlacementOverrides.ApplyRuntimeOverrides(resolvedDefinition, placement, _missionTemplate);
			return resolvedDefinition;
		}

		return null;
	}

	private bool HasRuntimePropForPlacement(MissionRoomBuilder.MarkerPlacement placement)
	{
		return placement != null && _missionPropsByCell.ContainsKey(placement.Cell) && ResolvePropDefinitionForPlacement(placement) != null;
	}

	private Vector2I GetPropCell(MissionProp prop)
	{
		foreach (KeyValuePair<Vector2I, MissionProp> kvp in _missionPropsByCell)
		{
			if (kvp.Value == prop)
			{
				return kvp.Key;
			}
		}

		return Vector2I.Zero;
	}

	private string BuildPropInstanceId(MissionRoomBuilder.MarkerPlacement placement, PropDefinition definition)
	{
		string key = !string.IsNullOrWhiteSpace(placement.MarkerId)
			? placement.MarkerId
			: !string.IsNullOrWhiteSpace(placement.LogicRole)
				? placement.LogicRole
				: definition.PropId;
		return $"{definition.PropId}:{key}:{placement.Cell.X},{placement.Cell.Y}:{placement.TargetId}";
	}

	private PropInteractionContext BuildPropInteractionContext(OfficerPawn officer, MissionProp prop)
	{
		return new PropInteractionContext
		{
			MissionMap = this,
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			DialogueUI = _dialogueUi,
			Officer = officer,
			TargetCell = GetPropCell(prop),
			PropInstanceId = prop.PropInstanceId,
			SourceInteractionKey = _missionState?.SourceInteractionKey ?? string.Empty,
			NpcPortraitPath = _propPlacementsByInstanceId.TryGetValue(prop.PropInstanceId, out MissionRoomBuilder.MarkerPlacement placement)
				? placement.NpcPortraitPath
				: string.Empty
		};
	}

	private PropInteractionContext BuildNpcInteractionContext(OfficerPawn officer, MissionNpcPawn npc)
	{
		return new PropInteractionContext
		{
			MissionMap = this,
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			DialogueUI = _dialogueUi,
			Officer = officer,
			TargetCell = npc?.CurrentCell ?? Vector2I.Zero,
			PropInstanceId = npc?.NpcId ?? string.Empty,
			SourceInteractionKey = _missionState?.SourceInteractionKey ?? string.Empty,
			NpcPortraitPath = npc?.PortraitPath ?? string.Empty
		};
	}

	private void ApplyPropInteractionResult(MissionProp prop, PropInteractionResult result, PropInteractionContext context)
	{
		if (prop == null || result == null || !result.Success)
		{
			return;
		}

		if (_combatActive && context?.Officer != null)
		{
			context.Officer.SpendActions(CombatInteractionActionCost);
		}

		foreach (string doorId in result.DoorIdsToToggle ?? new List<string>())
		{
			if (string.IsNullOrWhiteSpace(doorId))
			{
				continue;
			}

			bool nextOpenState = !_roomBuilder.IsDoorOpen(doorId);
			_roomBuilder.TrySetDoorOpen(doorId, nextOpenState, true);
		}

		prop.CommitInteractionResult(result, context);
		if (prop.Definition?.PropId == RelaySurvivorPodsPropId
			&& !_escortSurvivorIds.Any()
			&& GetExtractedEscortSurvivorCount() == 0)
		{
			SpawnRelaySurvivors(GetPropCell(prop));
		}

		AppendActionLog(BuildActionResultMessage(
			context?.Officer?.OfficerName,
			result.StatusMessage,
			$"{context?.Officer?.OfficerName ?? "Officer"} secures {prop.Definition?.DisplayName ?? "the objective"}.")); 
		UpdateFogOfWar();
		UpdateMissionCompletionActions();

		if (prop.IsConsumed && prop.Definition?.HideWhenConsumed == true)
		{
			Vector2I propCell = GetPropCell(prop);
			if (_missionPropsByCell.ContainsKey(propCell) && _missionPropsByCell[propCell] == prop)
			{
				_missionPropsByCell.Remove(propCell);
			}
		}

		if (!string.IsNullOrWhiteSpace(result.DialogueId) && _dialogueUi != null)
		{
			_dialogueUi.StartConversation(
				result.DialogueId,
				context.Officer?.OfficerName ?? "Officer",
				context.Officer?.PortraitPath ?? string.Empty,
				context.NpcPortraitPath ?? string.Empty);
		}
	}

	private bool ShouldShowStoryEvent(MissionProp prop)
	{
		return prop?.Definition != null
			&& (!string.IsNullOrWhiteSpace(prop.Definition.StoryImagePath)
				|| !string.IsNullOrWhiteSpace(prop.Definition.StoryDescriptionText)
				|| !string.IsNullOrWhiteSpace(prop.Definition.StoryConfirmButtonText));
	}

	private void ShowPropStoryEvent(MissionProp prop, PropInteractionResult result, PropInteractionContext context)
	{
		if (_missionUi == null || prop?.Definition == null)
		{
			ApplyPropInteractionResult(prop, result, context);
			return;
		}

		_pendingStoryProp = prop;
		_pendingStoryResult = result;
		_pendingStoryContext = context;
		_missionUi.ShowStoryEvent(
			prop.Definition.DisplayName,
			prop.Definition.StoryDescriptionText,
			prop.Definition.StoryImagePath,
			prop.Definition.StoryConfirmButtonText);
	}

	private void OnStoryEventConfirmed()
	{
		_missionUi?.HideStoryEvent();
		if (_pendingStoryProp != null && _pendingStoryResult != null && _pendingStoryContext != null)
		{
			ApplyPropInteractionResult(_pendingStoryProp, _pendingStoryResult, _pendingStoryContext);
		}

		_pendingStoryProp = null;
		_pendingStoryResult = null;
		_pendingStoryContext = null;
	}

	private void OnConfirmationAccepted()
	{
		if (_pendingMedicalBedProp != null && !string.IsNullOrWhiteSpace(_pendingMedicalBedOfficerId))
		{
			ConfirmMedicalBedUse();
			return;
		}

		ClearPendingConfirmation();
	}

	private void OnConfirmationCancelled()
	{
		ClearPendingConfirmation();
	}

	private void ApplyNpcInteractionResult(MissionNpcPawn npc, PropInteractionResult result, PropInteractionContext context)
	{
		if (npc == null || result == null || !result.Success)
		{
			return;
		}

		if (_combatActive && context?.Officer != null)
		{
			context.Officer.SpendActions(CombatInteractionActionCost);
		}

		if (_globalData?.StoryFlags != null && result.FlagsToSet != null)
		{
			foreach (string flag in result.FlagsToSet)
			{
				if (!string.IsNullOrWhiteSpace(flag) && !_globalData.StoryFlags.Contains(flag))
				{
					_globalData.StoryFlags.Add(flag);
				}
			}
		}

		npc.CommitInteractionResult(result, context);
		AppendActionLog(BuildActionResultMessage(
			context?.Officer?.OfficerName,
			result.StatusMessage,
			$"{context?.Officer?.OfficerName ?? "Officer"} speaks with {npc.DisplayName}.")); 
		UpdateMissionCompletionActions();

		if (!string.IsNullOrWhiteSpace(result.DialogueId) && _dialogueUi != null)
		{
			_dialogueUi.StartConversation(
				result.DialogueId,
				context.Officer?.OfficerName ?? "Officer",
				context.Officer?.PortraitPath ?? string.Empty,
				context.NpcPortraitPath ?? string.Empty);
		}
	}

	private static string BuildInteractionKey(MissionRoomBuilder.MarkerPlacement marker)
	{
		string roleOrMarker = !string.IsNullOrEmpty(marker.MarkerId) ? marker.MarkerId : marker.LogicRole;
		string tileId = marker.TileId ?? string.Empty;
		return $"{roleOrMarker}:{tileId}:{marker.Cell.X},{marker.Cell.Y}:{marker.TargetId}";
	}

	private static string GetInteractionDisplayName(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (!string.IsNullOrWhiteSpace(interaction?.Label))
		{
			return interaction.Label.Trim();
		}

		if (string.Equals(interaction?.LogicRole, "door", StringComparison.OrdinalIgnoreCase))
		{
			return "Bulkhead Door";
		}

		if (string.Equals(interaction?.LogicRole, "terminal", StringComparison.OrdinalIgnoreCase))
		{
			return "Terminal";
		}

		return "the objective";
	}

	private List<MissionExtractionOption> GetAvailableExtractionOptions()
	{
		List<MissionExtractionOption> options = new List<MissionExtractionOption>();
		string primaryOutcomeId = GetPrimaryOutcomeId();
		if (IsOutcomeReady(primaryOutcomeId))
		{
			options.Add(new MissionExtractionOption
			{
				OutcomeId = primaryOutcomeId,
				DisplayText = GetOutcomeDisplayName(primaryOutcomeId),
				Description = $"Complete the mission as {GetOutcomeDisplayName(primaryOutcomeId).ToLowerInvariant()}."
			});
		}

		string secondaryOutcomeId = GetSecondaryOutcomeId();
		if (IsOutcomeReady(secondaryOutcomeId))
		{
			options.Add(new MissionExtractionOption
			{
				OutcomeId = secondaryOutcomeId,
				DisplayText = GetOutcomeDisplayName(secondaryOutcomeId),
				Description = $"Complete the mission as {GetOutcomeDisplayName(secondaryOutcomeId).ToLowerInvariant()}."
			});
		}

		return options;
	}

	private HashSet<Vector2I> GetEvacRallyCells()
	{
		HashSet<Vector2I> rallyCells = new HashSet<Vector2I>();
		if (_roomBuilder == null)
		{
			return rallyCells;
		}

		Vector2I[] buildCellDirections =
		{
			Vector2I.Zero,
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		foreach (Vector2I evacCell in _roomBuilder.GetMarkerPlacements()
			.Where(marker => marker.MarkerId == "evac_zone")
			.Select(marker => marker.Cell))
		{
			foreach (Vector2I direction in buildCellDirections)
			{
				Vector2I buildCell = evacCell + direction;
				foreach (Vector2I candidate in _roomBuilder.GetMovementCellsForBuildCell(buildCell))
				{
					if (_roomBuilder.IsWalkableMovementCell(candidate))
					{
						rallyCells.Add(candidate);
					}
				}
			}
		}

		return rallyCells;
	}

	private bool AreAllOfficersOnEvacZone()
	{
		if (_roomBuilder == null)
		{
			return false;
		}

		List<OfficerPawn> survivingOfficers = GetAliveOfficers().ToList();
		if (survivingOfficers.Count == 0)
		{
			return false;
		}

		HashSet<Vector2I> evacCells = GetEvacRallyCells();
		if (evacCells.Count == 0)
		{
			return false;
		}

		return survivingOfficers.All(pawn => evacCells.Contains(pawn.CurrentCell));
	}

	private void UpdateMissionCompletionActions()
	{
		if (_missionUi == null)
		{
			return;
		}

		List<MissionExtractionOption> availableOptions = GetAvailableExtractionOptions();
		if (AreAllOfficersOnEvacZone() && availableOptions.Count > 0)
		{
			string message = availableOptions.Count == 1
				? $"All surviving officers are assembled at the evac zone. Confirm extraction to leave the mission as {availableOptions[0].DisplayText.ToLowerInvariant()}."
				: "All surviving officers are assembled at the evac zone. Choose which resolved outcome you want to extract with.";
			_missionUi.ShowExtractionPrompt("EXTRACTION READY", message, availableOptions);
		}
		else
		{
			_missionUi.HideExtractionPrompt();
		}

		RefreshMissionPrompt();
	}

	private bool AreRequiredFlagsSatisfied(Godot.Collections.Array<string> requiredFlags)
	{
		if (requiredFlags == null || requiredFlags.Count == 0)
		{
			return true;
		}

		if (_globalData?.StoryFlags == null)
		{
			return false;
		}

		foreach (string flag in requiredFlags)
		{
			if (!string.IsNullOrWhiteSpace(flag) && !_globalData.StoryFlags.Contains(flag))
			{
				return false;
			}
		}

		return true;
	}

	private bool AreBlockedFlagsClear(Godot.Collections.Array<string> blockedFlags)
	{
		if (blockedFlags == null || blockedFlags.Count == 0)
		{
			return true;
		}

		if (_globalData?.StoryFlags == null)
		{
			return true;
		}

		foreach (string flag in blockedFlags)
		{
			if (!string.IsNullOrWhiteSpace(flag) && _globalData.StoryFlags.Contains(flag))
			{
				return false;
			}
		}

		return true;
	}

	private string BuildMissingFlagsTooltip(Godot.Collections.Array<string> requiredFlags)
	{
		if (requiredFlags == null || requiredFlags.Count == 0 || _globalData?.StoryFlags == null)
		{
			return "Additional mission steps are still required.";
		}

		List<string> missingFlags = requiredFlags
			.Where(flag => !string.IsNullOrWhiteSpace(flag) && !_globalData.StoryFlags.Contains(flag))
			.ToList();
		return missingFlags.Count == 0
			? string.Empty
			: $"Missing mission steps: {string.Join(", ", missingFlags)}";
	}
}
