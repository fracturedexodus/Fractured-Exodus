using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionSceneBuilder : Node2D
{
	private const float DefaultZoom = 0.72f;
	private const float MinZoom = 0.32f;
	private const float MaxZoom = 1.35f;
	private const float ZoomStep = 0.08f;
	private const float CameraPanSpeed = 780f;
	private const float TileNudgeStep = 10f;
	private const float TileRotateStep = 15f;
	private const int GridPreviewColumns = 20;
	private const int GridPreviewRows = 20;
	private const string DefaultLayoutName = "black_site_relay_builder";
	private const string LayoutDirectoryResourcePath = "res://Data/MissionLayouts";

	private enum BuilderLayer
	{
		Floor,
		Wall,
		Prop,
		Marker
	}

	private Camera2D _camera;
	private Node2D _floorLayer;
	private Node2D _wallLayer;
	private Node2D _propLayer;
	private Node2D _markerLayer;
	private VBoxContainer _paletteContainer;
	private LineEdit _paletteSearchEdit;
	private Label _statusLabel;
	private LineEdit _layoutNameEdit;
	private ConfirmationDialog _loadLayoutDialog;
	private ItemList _loadLayoutList;
	private ConfirmationDialog _nameMissionDialog;
	private LineEdit _nameMissionEdit;
	private OptionButton _backgroundOption;
	private Node2D _hoverLayer;
	private TextureRect _backgroundBackdrop;
	private Sprite2D _backgroundFeatureSprite;
	private Label _selectedLabel;
	private Label _logicSelectionLabel;
	private Label _logicHintLabel;
	private OptionButton _logicRoleOption;
	private LineEdit _logicLabelEdit;
	private LineEdit _logicTargetIdEdit;
	private Label _logicTargetIdHelpLabel;
	private LineEdit _logicPropDefinitionPathEdit;
	private OptionButton _logicPropDefinitionOption;
	private Button _logicPropDefinitionRefreshButton;
	private TextureRect _logicPropDefinitionPreviewIcon;
	private Label _logicPropDefinitionPreviewLabel;
	private LineEdit _logicNpcDefinitionPathEdit;
	private OptionButton _logicNpcDefinitionOption;
	private Button _logicNpcDefinitionRefreshButton;
	private Label _logicNpcDefinitionPreviewLabel;
	private OptionButton _logicNpcPortraitOption;
	private LineEdit _logicRequiredFlagEdit;
	private Label _logicRequiredFlagHelpLabel;
	private LineEdit _logicSetFlagEdit;
	private Label _logicSetFlagHelpLabel;
	private OptionButton _logicTriggerModeOption;
	private CheckBox _logicOneShotCheck;
	private TextEdit _logicNotesEdit;
	private PanelContainer _contextObjectEditorPanel;
	private PanelContainer _logicPanel;
	private PanelContainer _controlsPanel;
	private Label _contextSelectionLabel;
	private Label _contextHintLabel;
	private OptionButton _contextRoleOption;
	private LineEdit _contextLabelEdit;
	private LineEdit _contextTargetIdEdit;
	private Label _contextTargetIdHelpLabel;
	private LineEdit _contextPropDefinitionPathEdit;
	private OptionButton _contextPropDefinitionOption;
	private LineEdit _contextNpcDefinitionPathEdit;
	private OptionButton _contextNpcDefinitionOption;
	private Label _contextNpcDefinitionPreviewLabel;
	private OptionButton _contextNpcPortraitOption;
	private LineEdit _contextRequiredFlagEdit;
	private Label _contextRequiredFlagHelpLabel;
	private LineEdit _contextSetFlagEdit;
	private Label _contextSetFlagHelpLabel;
	private OptionButton _contextTriggerModeOption;
	private CheckBox _contextOneShotCheck;
	private TextEdit _contextNotesEdit;
	private RichTextLabel _validationReport;
	private OptionButton _validationFilterOption;
	private Label _dialogueSelectionContextLabel;
	private OptionButton _dialogueConversationOption;
	private Button _dialogueConversationRefreshButton;
	private Button _dialogueConversationNewButton;
	private Button _dialogueConversationLoadSelectedButton;
	private Button _dialogueConversationAssignSelectedButton;
	private LineEdit _dialogueConversationIdEdit;
	private OptionButton _dialogueNodeOption;
	private Button _dialogueNodeNewButton;
	private Button _dialogueNodeDeleteButton;
	private LineEdit _dialogueNodeIdEdit;
	private LineEdit _dialogueSpeakerEdit;
	private LineEdit _dialogueQuestTriggerEdit;
	private LineEdit _dialogueRequiredFlagsEdit;
	private LineEdit _dialogueBlockedFlagsEdit;
	private LineEdit _dialogueSetFlagsEdit;
	private TextEdit _dialogueTextEdit;
	private VBoxContainer _dialogueOptionsContainer;
	private Button _dialogueAddOptionButton;
	private Button _dialogueSaveButton;
	private Label _dialogueEditorStatusLabel;
	private MissionTileDefinition _selectedTile;
	private MissionMarkerDefinition _selectedMarker;
	private string _selectedPropDefinitionPath = string.Empty;
	private string _selectedHostileNpcDefinitionPath = string.Empty;
	private Sprite2D _draggedSprite;
	private Sprite2D _selectedPlacedSprite;
	private Vector2I _draggedCell;
	private bool _isPanning;
	private bool _isUpdatingBackgroundUi;
	private bool _isUpdatingLogicUi;
	private bool _isUpdatingContextLogicUi;
	private bool _isUpdatingDialogueUi;
	private Vector2 _lastMouseScreenPosition;
	private readonly List<Line2D> _gridLines = new List<Line2D>();
	private readonly Dictionary<string, PropDefinitionPreview> _propDefinitionPreviewCache = new Dictionary<string, PropDefinitionPreview>();
	private readonly Dictionary<string, NpcDefinitionPreview> _npcDefinitionPreviewCache = new Dictionary<string, NpcDefinitionPreview>();
	private readonly List<ValidationIssueEntry> _validationEntries = new List<ValidationIssueEntry>();
	private readonly Dictionary<string, Texture2D> _tileIconCache = new Dictionary<string, Texture2D>();
	private readonly Dictionary<string, Texture2D> _markerIconCache = new Dictionary<string, Texture2D>();
	private readonly Dictionary<string, bool> _paletteSectionExpanded = new Dictionary<string, bool>();
	private Polygon2D _hoverDiamond;
	private readonly Vector2 _tileStep = MissionFloorTextureFactory.TileSize;
	private readonly Vector2 _gridOrigin = new Vector2(0f, -20f);
	private string _selectedBackgroundId = MissionBackgroundCatalog.DefaultId;
	private string _paletteSearchQuery = string.Empty;
	private Texture2D _fallbackPropPreviewTexture;
	private bool _placedMapCenterDirty = true;
	private Vector2 _cachedPlacedMapCenter = Vector2.Zero;
	private DialogueConversationData _activeDialogueConversation;
	private string _activeDialogueNodeId = string.Empty;
	private string _currentLayoutName = DefaultLayoutName;

	private sealed class PropDefinitionPreview
	{
		public string Path { get; init; } = string.Empty;
		public string DisplayName { get; init; } = string.Empty;
		public string Description { get; init; } = string.Empty;
		public Texture2D Icon { get; init; }
		public bool Exists { get; init; }
		public float VisualScaleMultiplier { get; init; } = PropVisualSizing.DefaultVisualScaleMultiplier;
	}

	private sealed class ValidationIssueEntry
	{
		public string Message { get; init; } = string.Empty;
		public string TargetKey { get; init; } = string.Empty;
		public ValidationSeverity Severity { get; init; } = ValidationSeverity.Warning;
	}

	private sealed class NpcDefinitionPreview
	{
		public string Path { get; init; } = string.Empty;
		public string DisplayName { get; init; } = string.Empty;
		public string Description { get; init; } = string.Empty;
		public Texture2D Icon { get; init; }
		public bool Exists { get; init; }
		public bool IsHostile { get; init; }
	}

	private enum ValidationSeverity
	{
		Info,
		Warning,
		Error
	}

	private enum ValidationFilter
	{
		All,
		ErrorsOnly,
		WarningsAndErrors,
		InfoOnly
	}

	private enum DialogueBindingKind
	{
		None,
		LayoutTargetId,
		NpcDefinition
	}

	private sealed class DialogueBindingInfo
	{
		public DialogueBindingKind Kind { get; init; } = DialogueBindingKind.None;
		public Sprite2D Sprite { get; init; }
		public string ConversationId { get; init; } = string.Empty;
		public string Description { get; init; } = string.Empty;
		public string NpcDefinitionPath { get; init; } = string.Empty;
	}

	public override void _Ready()
	{
		_camera = GetNode<Camera2D>("Camera2D");
		_floorLayer = GetNode<Node2D>("World/FloorPlacementLayer");
		_wallLayer = GetNode<Node2D>("World/WallPlacementLayer");
		_propLayer = GetNode<Node2D>("World/PropPlacementLayer");
		_markerLayer = GetNode<Node2D>("World/MarkerPlacementLayer");
		_paletteContainer = GetNode<VBoxContainer>("UILayer/PalettePanel/Margin/PaletteScroll/PaletteList");
		_statusLabel = GetNode<Label>("UILayer/BottomBar/Margin/StatusLabel");
		_layoutNameEdit = GetNode<LineEdit>("UILayer/TopBar/Margin/TopRow/LayoutNameEdit");
		_layoutNameEdit.Editable = false;
		_selectedLabel = GetNode<Label>("UILayer/TopBar/Margin/TopRow/SelectedTileLabel");
		_hoverLayer = GetNode<Node2D>("World/HoverLayer");
		_controlsPanel = GetNodeOrNull<PanelContainer>("UILayer/ControlsPanel");
		SetCurrentLayoutName(DefaultLayoutName);

		EnsureBackgroundPreviewNodes();
		BuildMissionDialogs();
		BuildBackgroundControls();
		BuildPalette();
		BuildGrid();
		BuildHoverDiamond();
		BuildLogicPanel();
		BuildContextObjectEditorPanel();
		ApplyBuilderPanelStyles();
		WireUi();
		ApplyZoom(DefaultZoom);
		UpdateSelectedLabel();
		SetControlsPanelVisible(false);
		SetStatus("Build floors and walls on the left, add mission markers and props, then wire logic and dialogue on the right. Point at any placed tile and press Y for quick setup. Right click deletes, middle mouse pans, wheel zooms.");
		LoadLayout();
	}

	public override void _Process(double delta)
	{
		SanitizeTransientSpriteReferences();
		UpdateCameraPan((float)delta);
		UpdateHoverDiamond();
		UpdateBackgroundFeaturePlacement();
		UpdateContextObjectEditorPosition();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		SanitizeTransientSpriteReferences();

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			if (mouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				AdjustZoom(-ZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				AdjustZoom(ZoomStep);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (mouseButton.ButtonIndex == MouseButton.Middle)
			{
				_isPanning = true;
				_lastMouseScreenPosition = mouseButton.Position;
				GetViewport().SetInputAsHandled();
				return;
			}

			if (mouseButton.ButtonIndex == MouseButton.Right)
			{
				DeleteTileAtMouse();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				StartPlacementOrDrag();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseButton mouseRelease && !mouseRelease.Pressed && mouseRelease.ButtonIndex == MouseButton.Left)
		{
			_draggedSprite = null;
		}

		if (@event is InputEventMouseButton middleRelease && !middleRelease.Pressed && middleRelease.ButtonIndex == MouseButton.Middle)
		{
			_isPanning = false;
		}

		if (@event is InputEventMouseMotion motion)
		{
			if (_isPanning && _camera != null)
			{
				Vector2 deltaScreen = motion.Position - _lastMouseScreenPosition;
				_camera.Position -= deltaScreen * _camera.Zoom;
				_lastMouseScreenPosition = motion.Position;
				GetViewport().SetInputAsHandled();
				return;
			}

			if (TryGetDraggedSprite(out Sprite2D draggedSprite))
			{
				Vector2I cell = GetMouseCell();
				if (cell != _draggedCell)
				{
					_draggedCell = cell;
					MoveSpriteToCell(draggedSprite, cell.X, cell.Y);
				}
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.C)
			{
				ToggleControlsPanelVisibility();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.Y)
			{
				ShowContextObjectEditorAtHoveredOrSelected();
				GetViewport().SetInputAsHandled();
				return;
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

			if (TryGetSelectedPlacedSprite(out _))
			{
				if (keyEvent.Keycode == Key.F)
				{
					ToggleSelectedPropFlip(horizontal: true);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (keyEvent.Keycode == Key.X)
				{
					ToggleSelectedPropFlip(horizontal: false);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (keyEvent.Keycode == Key.Left)
				{
					AdjustSelectedTile(new Vector2(-TileNudgeStep, 0f), 0f);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (keyEvent.Keycode == Key.Right)
				{
					AdjustSelectedTile(new Vector2(TileNudgeStep, 0f), 0f);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (keyEvent.Keycode == Key.Up)
				{
					AdjustSelectedTile(new Vector2(0f, -TileNudgeStep), 0f);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (keyEvent.Keycode == Key.Down)
				{
					AdjustSelectedTile(new Vector2(0f, TileNudgeStep), 0f);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (keyEvent.Keycode == Key.Q)
				{
					AdjustSelectedTile(Vector2.Zero, -TileRotateStep);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (keyEvent.Keycode == Key.E)
				{
					AdjustSelectedTile(Vector2.Zero, TileRotateStep);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (keyEvent.Keycode == Key.Home)
				{
					ResetSelectedTileAdjustment();
					GetViewport().SetInputAsHandled();
					return;
				}
			}
		}
	}

	private void WireUi()
	{
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/SaveButton").Pressed += SaveLayout;
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/LoadButton").Pressed += ShowLoadLayoutDialog;
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/NameMissionButton").Pressed += ShowNameMissionDialog;
		Button validateButton = new Button { Text = "Validate" };
		validateButton.Pressed += ValidateLayout;
		HBoxContainer topRow = GetNode<HBoxContainer>("UILayer/TopBar/Margin/TopRow");
		topRow.AddChild(validateButton);
		Button frameMapButton = new Button { Text = "Frame Map" };
		frameMapButton.Pressed += FramePlacedMap;
		topRow.AddChild(frameMapButton);
		Button openV2Button = new Button { Text = "Open Workbench v3" };
		openV2Button.Pressed += OpenWorkbenchV2;
		topRow.AddChild(openV2Button);
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/ClearButton").Pressed += ClearLayout;
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/ExitButton").Pressed += ExitBuilder;
	}

	private void BuildMissionDialogs()
	{
		_loadLayoutDialog = new ConfirmationDialog
		{
			Title = "Load Mission Layout",
			Exclusive = true,
			MinSize = new Vector2I(560, 420)
		};
		AddChild(_loadLayoutDialog);
		_loadLayoutDialog.GetOkButton().Text = "Load Selected";
		_loadLayoutDialog.Confirmed += LoadSelectedLayoutFromDialog;

		VBoxContainer loadRoot = new VBoxContainer();
		loadRoot.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		loadRoot.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		loadRoot.AddThemeConstantOverride("separation", 10);
		_loadLayoutDialog.AddChild(loadRoot);

		Label loadHelp = new Label
		{
			Text = "Choose which mission layout to open in the builder.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		loadRoot.AddChild(loadHelp);

		_loadLayoutList = new ItemList
		{
			SelectMode = ItemList.SelectModeEnum.Single,
			CustomMinimumSize = new Vector2(0f, 280f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		_loadLayoutList.ItemActivated += OnLoadLayoutItemActivated;
		loadRoot.AddChild(_loadLayoutList);

		_nameMissionDialog = new ConfirmationDialog
		{
			Title = "Name Mission",
			Exclusive = true,
			MinSize = new Vector2I(520, 180)
		};
		AddChild(_nameMissionDialog);
		_nameMissionDialog.GetOkButton().Text = "Use Name";
		_nameMissionDialog.Confirmed += ConfirmMissionNameFromDialog;

		VBoxContainer nameRoot = new VBoxContainer();
		nameRoot.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameRoot.AddThemeConstantOverride("separation", 10);
		_nameMissionDialog.AddChild(nameRoot);

		Label nameHelp = new Label
		{
			Text = "Set the mission layout name for the current builder work. Save will write to that mission file later.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		nameRoot.AddChild(nameHelp);

		_nameMissionEdit = new LineEdit
		{
			PlaceholderText = DefaultLayoutName,
			Text = _currentLayoutName
		};
		_nameMissionEdit.TextSubmitted += _ => ConfirmMissionNameFromDialog();
		nameRoot.AddChild(_nameMissionEdit);
	}

	private void BuildBackgroundControls()
	{
		HBoxContainer topRow = GetNode<HBoxContainer>("UILayer/TopBar/Margin/TopRow");
		Label backgroundLabel = new Label { Text = "Background" };
		_backgroundOption = new OptionButton
		{
			CustomMinimumSize = new Vector2(260f, 0f)
		};

		for (int i = 0; i < MissionBackgroundCatalog.All.Count; i++)
		{
			MissionBackgroundDefinition definition = MissionBackgroundCatalog.All[i];
			_backgroundOption.AddItem(definition.DisplayName, i);
			_backgroundOption.SetItemMetadata(i, definition.Id);
		}

		_backgroundOption.ItemSelected += OnBackgroundOptionSelected;
		topRow.AddChild(backgroundLabel);
		topRow.AddChild(_backgroundOption);
		topRow.MoveChild(backgroundLabel, 1);
		topRow.MoveChild(_backgroundOption, 2);
		SelectBackgroundById(MissionBackgroundCatalog.DefaultId, true, false);
	}

	private void EnsureBackgroundPreviewNodes()
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
				Modulate = new Color(0.72f, 0.78f, 0.92f, 0.28f)
			};
			backdropCanvas.AddChild(_backgroundBackdrop);
			backdropCanvas.MoveChild(_backgroundBackdrop, 1);
		}

		Node2D world = GetNode<Node2D>("World");
		Node2D backgroundLayer = world.GetNodeOrNull<Node2D>("BackgroundArtLayer");
		if (backgroundLayer == null)
		{
			backgroundLayer = new Node2D
			{
				Name = "BackgroundArtLayer",
				ZIndex = -10
			};
			world.AddChild(backgroundLayer);
			world.MoveChild(backgroundLayer, 0);
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

	private void OnBackgroundOptionSelected(long selectedIndex)
	{
		if (_isUpdatingBackgroundUi)
		{
			return;
		}

		string backgroundId = _backgroundOption.GetItemMetadata((int)selectedIndex).AsString();
		SelectBackgroundById(backgroundId, false, true);
	}

	private void SelectBackgroundById(string backgroundId, bool syncUi, bool updateStatus)
	{
		MissionBackgroundDefinition definition = MissionBackgroundCatalog.GetById(backgroundId);
		_selectedBackgroundId = definition.Id;

		if (syncUi && _backgroundOption != null)
		{
			_isUpdatingBackgroundUi = true;
			for (int i = 0; i < _backgroundOption.ItemCount; i++)
			{
				if (_backgroundOption.GetItemMetadata(i).AsString() != _selectedBackgroundId)
				{
					continue;
				}

				_backgroundOption.Select(i);
				break;
			}
			_isUpdatingBackgroundUi = false;
		}

		ApplySelectedBackgroundPreview();
		if (updateStatus)
		{
			SetStatus($"Background set to {definition.DisplayName}.");
		}
	}

	private void ApplySelectedBackgroundPreview()
	{
		MissionBackgroundDefinition definition = MissionBackgroundCatalog.GetById(_selectedBackgroundId);

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
		if (_backgroundFeatureSprite.Texture != null)
		{
			_backgroundFeatureSprite.Scale = new Vector2(definition.FeatureScale, definition.FeatureScale);
			_backgroundFeatureSprite.Modulate = definition.FeatureModulate;
			UpdateBackgroundFeaturePlacement();
		}
	}

	private void UpdateBackgroundFeaturePlacement()
	{
		if (_backgroundFeatureSprite == null || !_backgroundFeatureSprite.Visible)
		{
			return;
		}

		MissionBackgroundDefinition definition = MissionBackgroundCatalog.GetById(_selectedBackgroundId);
		_backgroundFeatureSprite.Position = GetPlacedMapCenter() + definition.FeatureOffset;
	}

	private Vector2 GetPlacedMapCenter()
	{
		if (!_placedMapCenterDirty)
		{
			return _cachedPlacedMapCenter;
		}

		List<Sprite2D> floorSprites = EnumerateLiveSprites(_floorLayer).ToList();
		if (floorSprites.Count == 0)
		{
			_cachedPlacedMapCenter = Vector2.Zero;
			_placedMapCenterDirty = false;
			return _cachedPlacedMapCenter;
		}

		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minY = float.MaxValue;
		float maxY = float.MinValue;
		foreach (Sprite2D sprite in floorSprites)
		{
			Vector2 position = sprite.Position;
			minX = Mathf.Min(minX, position.X);
			maxX = Mathf.Max(maxX, position.X);
			minY = Mathf.Min(minY, position.Y);
			maxY = Mathf.Max(maxY, position.Y);
		}

		_cachedPlacedMapCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
		_placedMapCenterDirty = false;
		return _cachedPlacedMapCenter;
	}

	private void BuildLogicPanel()
	{
		CanvasLayer uiLayer = GetNode<CanvasLayer>("UILayer");
		_logicPanel = new PanelContainer();
		_logicPanel.Name = "LogicPanel";
		_logicPanel.Position = new Vector2(1540f, 332f);
		_logicPanel.Size = new Vector2(368f, 736f);
		uiLayer.AddChild(_logicPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		_logicPanel.AddChild(margin);

		ScrollContainer scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		margin.AddChild(scroll);

		VBoxContainer root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 10);
		root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(root);

		Label title = new Label { Text = "MISSION LOGIC" };
		title.AddThemeFontSizeOverride("font_size", 20);
		root.AddChild(title);

		Label workflowLabel = new Label
		{
			Text = "Workflow: build floors and walls on the left, place props and markers, then wire mission logic and dialogue here.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		workflowLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.85f, 0.94f, 0.95f));
		root.AddChild(workflowLabel);

		AddPanelSectionHeader(root, "Selection & Mission Binding");

		_logicSelectionLabel = new Label
		{
			Text = "Select a marker to edit mission logic.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		root.AddChild(_logicSelectionLabel);

		_logicHintLabel = new Label
		{
			Text = "Marker properties are where we hook up spawns, triggers, and future dialogue/events.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		root.AddChild(_logicHintLabel);

		root.AddChild(new Label { Text = "Interaction Role" });
		_logicRoleOption = new OptionButton();
		_logicRoleOption.AddItem("None", 0);
		_logicRoleOption.AddItem("Door", 1);
		_logicRoleOption.AddItem("Terminal", 2);
		_logicRoleOption.ItemSelected += _ => ApplyLogicFieldChanges();
		root.AddChild(_logicRoleOption);

		_logicLabelEdit = AddInspectorField(root, "Item Label");
		_logicTargetIdEdit = AddInspectorField(root, "Target ID");
		_logicTargetIdHelpLabel = new Label
		{
			Text = "Target ID meaning depends on the selected marker or prop.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_logicTargetIdHelpLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.88f, 0.95f));
		root.AddChild(_logicTargetIdHelpLabel);
		_logicPropDefinitionPathEdit = AddInspectorField(root, "Prop Definition Path");
		_logicPropDefinitionPathEdit.PlaceholderText = "res://Data/Missions/Props/Definitions/...";
		_logicPropDefinitionPathEdit.TextChanged += OnPropDefinitionPathChanged;
		root.AddChild(new Label { Text = "Prop Definition Library" });
		HBoxContainer propDefinitionRow = new HBoxContainer();
		_logicPropDefinitionOption = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_logicPropDefinitionOption.ItemSelected += OnPropDefinitionOptionSelected;
		propDefinitionRow.AddChild(_logicPropDefinitionOption);
		_logicPropDefinitionRefreshButton = new Button { Text = "Refresh" };
		_logicPropDefinitionRefreshButton.Pressed += RefreshPropDefinitionOptions;
		propDefinitionRow.AddChild(_logicPropDefinitionRefreshButton);
		root.AddChild(propDefinitionRow);
		HBoxContainer propPreviewRow = new HBoxContainer();
		propPreviewRow.AddThemeConstantOverride("separation", 10);
		_logicPropDefinitionPreviewIcon = new TextureRect
		{
			CustomMinimumSize = new Vector2(52f, 52f),
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Visible = false
		};
		propPreviewRow.AddChild(_logicPropDefinitionPreviewIcon);
		_logicPropDefinitionPreviewLabel = new Label
		{
			Text = "No prop definition selected.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		};
		propPreviewRow.AddChild(_logicPropDefinitionPreviewLabel);
		root.AddChild(propPreviewRow);
		_logicNpcDefinitionPathEdit = AddInspectorField(root, "NPC Definition Path");
		_logicNpcDefinitionPathEdit.PlaceholderText = "res://Data/Missions/Entities/Npcs/...";
		_logicNpcDefinitionPathEdit.TextChanged += OnNpcDefinitionPathChanged;
		root.AddChild(new Label { Text = "NPC Definition Library" });
		HBoxContainer npcDefinitionRow = new HBoxContainer();
		_logicNpcDefinitionOption = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_logicNpcDefinitionOption.ItemSelected += OnNpcDefinitionOptionSelected;
		npcDefinitionRow.AddChild(_logicNpcDefinitionOption);
		_logicNpcDefinitionRefreshButton = new Button { Text = "Refresh" };
		_logicNpcDefinitionRefreshButton.Pressed += RefreshNpcDefinitionOptions;
		npcDefinitionRow.AddChild(_logicNpcDefinitionRefreshButton);
		root.AddChild(npcDefinitionRow);
		_logicNpcDefinitionPreviewLabel = new Label
		{
			Text = "No NPC definition selected.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		root.AddChild(_logicNpcDefinitionPreviewLabel);
		root.AddChild(new Label { Text = "Conversation Portrait" });
		_logicNpcPortraitOption = new OptionButton();
		for (int i = 0; i < MissionDialoguePortraitCatalog.All.Count; i++)
		{
			MissionDialoguePortraitDefinition definition = MissionDialoguePortraitCatalog.All[i];
			_logicNpcPortraitOption.AddItem(definition.DisplayName, i);
			_logicNpcPortraitOption.SetItemMetadata(i, definition.TexturePath);
		}
		_logicNpcPortraitOption.ItemSelected += _ => ApplyLogicFieldChanges();
		root.AddChild(_logicNpcPortraitOption);

		AddPanelSectionHeader(root, "Mission State & Trigger Rules");
		_logicRequiredFlagEdit = AddInspectorField(root, "Required Flag");
		_logicRequiredFlagHelpLabel = new Label
		{
			Text = "Required Flag gates whether this interaction is available.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_logicRequiredFlagHelpLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.88f, 0.95f));
		root.AddChild(_logicRequiredFlagHelpLabel);
		_logicSetFlagEdit = AddInspectorField(root, "Set Flag");
		_logicSetFlagHelpLabel = new Label
		{
			Text = "Set Flag is awarded when this interaction succeeds.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_logicSetFlagHelpLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.88f, 0.95f));
		root.AddChild(_logicSetFlagHelpLabel);

		root.AddChild(new Label { Text = "Trigger Mode" });
		_logicTriggerModeOption = new OptionButton();
		_logicTriggerModeOption.AddItem("None", 0);
		_logicTriggerModeOption.AddItem("Enter", 1);
		_logicTriggerModeOption.AddItem("Interact", 2);
		_logicTriggerModeOption.ItemSelected += _ => ApplyLogicFieldChanges();
		root.AddChild(_logicTriggerModeOption);

		_logicOneShotCheck = new CheckBox { Text = "One Shot Trigger" };
		_logicOneShotCheck.Toggled += _ => ApplyLogicFieldChanges();
		root.AddChild(_logicOneShotCheck);

		root.AddChild(new Label { Text = "Notes" });
		_logicNotesEdit = new TextEdit
		{
			CustomMinimumSize = new Vector2(0f, 150f),
			WrapMode = TextEdit.LineWrappingMode.Boundary
		};
		_logicNotesEdit.TextChanged += ApplyLogicFieldChanges;
		root.AddChild(_logicNotesEdit);

		root.AddChild(new HSeparator());
		HBoxContainer validationHeaderRow = new HBoxContainer();
		validationHeaderRow.AddThemeConstantOverride("separation", 8);
		Label validationTitle = new Label { Text = "Validation" };
		validationTitle.AddThemeFontSizeOverride("font_size", 18);
		validationHeaderRow.AddChild(validationTitle);
		_validationFilterOption = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_validationFilterOption.AddItem("All", (int)ValidationFilter.All);
		_validationFilterOption.AddItem("Errors Only", (int)ValidationFilter.ErrorsOnly);
		_validationFilterOption.AddItem("Warnings + Errors", (int)ValidationFilter.WarningsAndErrors);
		_validationFilterOption.AddItem("Info Only", (int)ValidationFilter.InfoOnly);
		_validationFilterOption.Select((int)ValidationFilter.All);
		_validationFilterOption.ItemSelected += _ => RefreshValidationReport();
		validationHeaderRow.AddChild(_validationFilterOption);
		root.AddChild(validationHeaderRow);

		_validationReport = new RichTextLabel
		{
			CustomMinimumSize = new Vector2(0f, 220f),
			ScrollActive = true,
			FitContent = false,
			BbcodeEnabled = true,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_validationReport.MetaClicked += OnValidationReportMetaClicked;
		root.AddChild(_validationReport);

		BuildDialogueEditor(root);

		RefreshPropDefinitionOptions();
		RefreshNpcDefinitionOptions();
		RefreshDialogueConversationOptions();
		UpdateLogicInspector();
	}

	private void BuildContextObjectEditorPanel()
	{
		CanvasLayer uiLayer = GetNode<CanvasLayer>("UILayer");
		_contextObjectEditorPanel = new PanelContainer
		{
			Name = "ContextObjectEditorPanel",
			Visible = false,
			Size = new Vector2(420f, 540f),
			CustomMinimumSize = new Vector2(420f, 540f)
		};
		uiLayer.AddChild(_contextObjectEditorPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		_contextObjectEditorPanel.AddChild(margin);

		ScrollContainer scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		margin.AddChild(scroll);

		VBoxContainer root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(root);

		HBoxContainer titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		Label title = new Label { Text = "QUICK SETUP (Y)" };
		title.AddThemeFontSizeOverride("font_size", 18);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(title);
		Button closeButton = new Button { Text = "Close" };
		closeButton.Pressed += () => _contextObjectEditorPanel.Visible = false;
		titleRow.AddChild(closeButton);
		root.AddChild(titleRow);

		Label quickHelp = new Label
		{
			Text = "Point at a placed tile and press Y to open fast setup beside it. The full mission editor still stays on the right for deeper dialogue and validation work.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		quickHelp.AddThemeColorOverride("font_color", new Color(0.78f, 0.85f, 0.94f, 0.95f));
		root.AddChild(quickHelp);

		_contextSelectionLabel = new Label
		{
			Text = "No item selected.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		root.AddChild(_contextSelectionLabel);

		_contextHintLabel = new Label
		{
			Text = "This popup mirrors the current object setup fields.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		root.AddChild(_contextHintLabel);

		root.AddChild(new Label { Text = "Interaction Role" });
		_contextRoleOption = new OptionButton();
		_contextRoleOption.AddItem("None", 0);
		_contextRoleOption.AddItem("Door", 1);
		_contextRoleOption.AddItem("Terminal", 2);
		_contextRoleOption.ItemSelected += _ => SyncMainInspectorFromContextPopup();
		root.AddChild(_contextRoleOption);

		_contextLabelEdit = AddContextInspectorField(root, "Item Label");
		_contextTargetIdEdit = AddContextInspectorField(root, "Target ID");
		_contextTargetIdHelpLabel = new Label
		{
			Text = "Target ID meaning depends on the selected marker or prop.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_contextTargetIdHelpLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.88f, 0.95f));
		root.AddChild(_contextTargetIdHelpLabel);

		_contextPropDefinitionPathEdit = AddContextInspectorField(root, "Prop Definition Path");
		_contextPropDefinitionPathEdit.PlaceholderText = "res://Data/Missions/Props/Definitions/...";
		root.AddChild(new Label { Text = "Prop Definition Library" });
		_contextPropDefinitionOption = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_contextPropDefinitionOption.ItemSelected += OnContextPropDefinitionOptionSelected;
		root.AddChild(_contextPropDefinitionOption);

		_contextNpcDefinitionPathEdit = AddContextInspectorField(root, "NPC Definition Path");
		_contextNpcDefinitionPathEdit.PlaceholderText = "res://Data/Missions/Entities/Npcs/...";
		root.AddChild(new Label { Text = "NPC Definition Library" });
		_contextNpcDefinitionOption = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_contextNpcDefinitionOption.ItemSelected += OnContextNpcDefinitionOptionSelected;
		root.AddChild(_contextNpcDefinitionOption);
		_contextNpcDefinitionPreviewLabel = new Label
		{
			Text = "No NPC definition selected.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		root.AddChild(_contextNpcDefinitionPreviewLabel);

		root.AddChild(new Label { Text = "Conversation Portrait" });
		_contextNpcPortraitOption = new OptionButton();
		for (int i = 0; i < MissionDialoguePortraitCatalog.All.Count; i++)
		{
			MissionDialoguePortraitDefinition definition = MissionDialoguePortraitCatalog.All[i];
			_contextNpcPortraitOption.AddItem(definition.DisplayName, i);
			_contextNpcPortraitOption.SetItemMetadata(i, definition.TexturePath);
		}
		_contextNpcPortraitOption.ItemSelected += _ => SyncMainInspectorFromContextPopup();
		root.AddChild(_contextNpcPortraitOption);

		root.AddChild(new HSeparator());
		_contextRequiredFlagEdit = AddContextInspectorField(root, "Required Flag");
		_contextRequiredFlagHelpLabel = new Label
		{
			Text = "Required Flag gates whether this interaction is available.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_contextRequiredFlagHelpLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.88f, 0.95f));
		root.AddChild(_contextRequiredFlagHelpLabel);

		_contextSetFlagEdit = AddContextInspectorField(root, "Set Flag");
		_contextSetFlagHelpLabel = new Label
		{
			Text = "Set Flag is awarded when this interaction succeeds.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_contextSetFlagHelpLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.88f, 0.95f));
		root.AddChild(_contextSetFlagHelpLabel);

		root.AddChild(new Label { Text = "Trigger Mode" });
		_contextTriggerModeOption = new OptionButton();
		_contextTriggerModeOption.AddItem("None", 0);
		_contextTriggerModeOption.AddItem("Enter", 1);
		_contextTriggerModeOption.AddItem("Interact", 2);
		_contextTriggerModeOption.ItemSelected += _ => SyncMainInspectorFromContextPopup();
		root.AddChild(_contextTriggerModeOption);

		_contextOneShotCheck = new CheckBox { Text = "One Shot Trigger" };
		_contextOneShotCheck.Toggled += _ => SyncMainInspectorFromContextPopup();
		root.AddChild(_contextOneShotCheck);

		root.AddChild(new Label { Text = "Notes" });
		_contextNotesEdit = new TextEdit
		{
			CustomMinimumSize = new Vector2(0f, 120f),
			WrapMode = TextEdit.LineWrappingMode.Boundary
		};
		_contextNotesEdit.TextChanged += SyncMainInspectorFromContextPopup;
		root.AddChild(_contextNotesEdit);

		Label footerLabel = new Label
		{
			Text = "Use the right-side editor for conversation authoring, mission validation, and full dialogue graph editing.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		footerLabel.AddThemeColorOverride("font_color", new Color(0.76f, 0.82f, 0.9f, 0.92f));
		root.AddChild(footerLabel);
	}

	private LineEdit AddInspectorField(VBoxContainer root, string label)
	{
		root.AddChild(new Label { Text = label });
		LineEdit lineEdit = new LineEdit();
		lineEdit.TextChanged += _ => ApplyLogicFieldChanges();
		root.AddChild(lineEdit);
		return lineEdit;
	}

	private LineEdit AddContextInspectorField(VBoxContainer root, string label)
	{
		root.AddChild(new Label { Text = label });
		LineEdit lineEdit = new LineEdit();
		lineEdit.TextChanged += _ => SyncMainInspectorFromContextPopup();
		root.AddChild(lineEdit);
		return lineEdit;
	}

	private void ApplyBuilderPanelStyles()
	{
		StyleBoxFlat sidePanelStyle = CreateBuilderPanelStyle(0.95f);
		StyleBoxFlat popupPanelStyle = CreateBuilderPanelStyle(0.985f);

		GetNodeOrNull<PanelContainer>("UILayer/PalettePanel")?.AddThemeStyleboxOverride("panel", sidePanelStyle.Duplicate() as StyleBoxFlat ?? sidePanelStyle);
		_controlsPanel?.AddThemeStyleboxOverride("panel", sidePanelStyle.Duplicate() as StyleBoxFlat ?? sidePanelStyle);
		_logicPanel?.AddThemeStyleboxOverride("panel", sidePanelStyle.Duplicate() as StyleBoxFlat ?? sidePanelStyle);
		_contextObjectEditorPanel?.AddThemeStyleboxOverride("panel", popupPanelStyle);
	}

	private void ToggleControlsPanelVisibility()
	{
		SetControlsPanelVisible(!(_controlsPanel?.Visible ?? false));
	}

	private void SetControlsPanelVisible(bool visible)
	{
		if (_controlsPanel == null)
		{
			return;
		}

		_controlsPanel.Visible = visible;
		SetStatus(visible
			? "Builder controls are open. Press C to hide them again."
			: "Builder controls are hidden. Press C any time to show the control reference.");
	}

	private static StyleBoxFlat CreateBuilderPanelStyle(float alpha)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.03f, 0.03f, 0.04f, alpha),
			BorderColor = new Color(0.32f, 0.4f, 0.52f, Mathf.Clamp(alpha + 0.01f, 0f, 1f)),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = new Color(0f, 0f, 0f, 0.45f),
			ShadowSize = 10
		};
	}

	private void ShowContextObjectEditorAtHoveredOrSelected()
	{
		Vector2I hoveredCell = GetMouseCell();
		List<Sprite2D> stackedSprites = GetSpritesAtCellOrdered(hoveredCell, includeFloors: true);
		Sprite2D targetSprite = null;
		if (stackedSprites.Count > 0)
		{
			targetSprite = GetNextQuickSetupSprite(stackedSprites, hoveredCell);
		}

		if (!IsLiveSprite(targetSprite) && !TryGetSelectedPlacedSprite(out targetSprite))
		{
			SetStatus("Point at a placed tile and press Y to open quick setup.");
			return;
		}

		if (!IsLiveSprite(targetSprite))
		{
			SetStatus("Quick setup could not find a live placed tile.");
			return;
		}

		SelectPlacedSprite(targetSprite);
		UpdateContextObjectEditor();
		_contextObjectEditorPanel.Visible = true;
		UpdateContextObjectEditorPosition();
		if (stackedSprites.Count > 1)
		{
			int currentIndex = stackedSprites.IndexOf(targetSprite) + 1;
			SetStatus($"Quick setup opened for {GetItemDisplayId(targetSprite)}. Press Y again to cycle this stack ({currentIndex}/{stackedSprites.Count}).");
			return;
		}

		SetStatus($"Quick setup opened for {GetItemDisplayId(targetSprite)}. Use the popup beside the tile to wire it up.");
	}

	private Sprite2D GetNextQuickSetupSprite(IReadOnlyList<Sprite2D> stackedSprites, Vector2I hoveredCell)
	{
		if (stackedSprites == null || stackedSprites.Count == 0)
		{
			return null;
		}

		if (!TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			return stackedSprites[0];
		}

		int selectedColumn = selectedSprite.GetMeta("column", int.MinValue).AsInt32();
		int selectedRow = selectedSprite.GetMeta("row", int.MinValue).AsInt32();
		if (selectedColumn != hoveredCell.X || selectedRow != hoveredCell.Y)
		{
			return stackedSprites[0];
		}

		int selectedIndex = -1;
		for (int i = 0; i < stackedSprites.Count; i++)
		{
			if (stackedSprites[i] == selectedSprite)
			{
				selectedIndex = i;
				break;
			}
		}
		if (selectedIndex < 0)
		{
			return stackedSprites[0];
		}

		return stackedSprites[(selectedIndex + 1) % stackedSprites.Count];
	}

	private List<Sprite2D> GetSpritesAtCellOrdered(Vector2I cell, bool includeFloors)
	{
		List<Sprite2D> sprites = new List<Sprite2D>();
		foreach (Node2D layer in GetSelectableLayers())
		{
			if (!includeFloors && layer == _floorLayer)
			{
				continue;
			}

			Godot.Collections.Array<Node> children = layer.GetChildren();
			for (int index = children.Count - 1; index >= 0; index--)
			{
				if (children[index] is not Sprite2D sprite || !IsLiveSprite(sprite))
				{
					continue;
				}

				if (sprite.GetMeta("column", int.MinValue).AsInt32() != cell.X)
				{
					continue;
				}

				if (sprite.GetMeta("row", int.MinValue).AsInt32() != cell.Y)
				{
					continue;
				}

				sprites.Add(sprite);
			}
		}

		return sprites;
	}

	private void UpdateContextObjectEditor()
	{
		if (_contextObjectEditorPanel == null)
		{
			return;
		}

		if (!TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			_contextObjectEditorPanel.Visible = false;
			return;
		}

		bool isMarker = !string.IsNullOrEmpty(selectedSprite.GetMeta("marker_id", string.Empty).AsString());
		bool isLogicProp = IsLogicCapableSprite(selectedSprite);
		bool isPlacedProp = IsPlacedPropSprite(selectedSprite);
		bool isLogicItem = isMarker || isLogicProp || isPlacedProp;

		_isUpdatingContextLogicUi = true;
		_contextSelectionLabel.Text = _logicSelectionLabel?.Text ?? $"Editing {GetItemDisplayId(selectedSprite)}";
		_contextHintLabel.Text = _logicHintLabel?.Text ?? "Quick setup is ready.";
		_contextRoleOption.Select(Mathf.Clamp(_logicRoleOption?.Selected ?? 0, 0, _contextRoleOption.ItemCount - 1));
		_contextLabelEdit.Text = _logicLabelEdit?.Text ?? string.Empty;
		_contextTargetIdEdit.Text = _logicTargetIdEdit?.Text ?? string.Empty;
		_contextTargetIdEdit.PlaceholderText = _logicTargetIdEdit?.PlaceholderText ?? string.Empty;
		_contextTargetIdHelpLabel.Text = _logicTargetIdHelpLabel?.Text ?? "Target ID meaning depends on the selected marker or prop.";
		_contextPropDefinitionPathEdit.Text = _logicPropDefinitionPathEdit?.Text ?? string.Empty;
		CopyOptionButtonItems(_logicPropDefinitionOption, _contextPropDefinitionOption);
		_contextNpcDefinitionPathEdit.Text = _logicNpcDefinitionPathEdit?.Text ?? string.Empty;
		CopyOptionButtonItems(_logicNpcDefinitionOption, _contextNpcDefinitionOption);
		if (_contextNpcDefinitionPreviewLabel != null)
		{
			_contextNpcDefinitionPreviewLabel.Text = _logicNpcDefinitionPreviewLabel?.Text ?? "No NPC definition selected.";
		}
		if (_contextNpcPortraitOption.ItemCount > 0)
		{
			_contextNpcPortraitOption.Select(Mathf.Clamp(_logicNpcPortraitOption?.Selected ?? 0, 0, _contextNpcPortraitOption.ItemCount - 1));
		}
		_contextRequiredFlagEdit.Text = _logicRequiredFlagEdit?.Text ?? string.Empty;
		_contextRequiredFlagEdit.PlaceholderText = _logicRequiredFlagEdit?.PlaceholderText ?? string.Empty;
		_contextRequiredFlagHelpLabel.Text = _logicRequiredFlagHelpLabel?.Text ?? "Required Flag gates whether this interaction is available.";
		_contextSetFlagEdit.Text = _logicSetFlagEdit?.Text ?? string.Empty;
		_contextSetFlagEdit.PlaceholderText = _logicSetFlagEdit?.PlaceholderText ?? string.Empty;
		_contextSetFlagHelpLabel.Text = _logicSetFlagHelpLabel?.Text ?? "Set Flag is awarded when this interaction succeeds.";
		_contextTriggerModeOption.Select(Mathf.Clamp(_logicTriggerModeOption?.Selected ?? 0, 0, _contextTriggerModeOption.ItemCount - 1));
		_contextOneShotCheck.ButtonPressed = _logicOneShotCheck?.ButtonPressed ?? false;
		_contextNotesEdit.Text = _logicNotesEdit?.Text ?? string.Empty;
		_isUpdatingContextLogicUi = false;

		SetContextObjectEditorEnabled(isLogicItem);
		_contextRoleOption.Disabled = !isLogicItem || isPlacedProp;
		if (_contextObjectEditorPanel.Visible)
		{
			UpdateContextObjectEditorPosition();
		}
	}

	private void UpdateContextObjectEditorPosition()
	{
		if (_contextObjectEditorPanel == null || !_contextObjectEditorPanel.Visible || !TryGetSelectedPlacedSprite(out _))
		{
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		Vector2 panelSize = _contextObjectEditorPanel.Size;
		if (panelSize.X <= 0f || panelSize.Y <= 0f)
		{
			panelSize = _contextObjectEditorPanel.CustomMinimumSize;
		}

		float leftEdge = GetLeftSidebarRightEdge();
		float rightEdge = GetRightSidebarLeftEdge(viewportSize.X);
		float availableWidth = Mathf.Max(0f, rightEdge - leftEdge);
		float targetX = leftEdge + ((availableWidth - panelSize.X) * 0.5f);
		float bottomLimit = GetBottomBarTopEdge(viewportSize.Y);
		Vector2 position = new Vector2(
			targetX,
			bottomLimit - panelSize.Y - 18f);
		position.X = Mathf.Clamp(position.X, 12f, Mathf.Max(12f, viewportSize.X - panelSize.X - 12f));
		position.Y = Mathf.Clamp(position.Y, 12f, Mathf.Max(12f, viewportSize.Y - panelSize.Y - 12f));
		_contextObjectEditorPanel.Position = position;
	}

	private float GetLeftSidebarRightEdge()
	{
		Control palettePanel = GetNodeOrNull<Control>("UILayer/PalettePanel");
		return palettePanel == null ? 12f : palettePanel.Position.X + palettePanel.Size.X + 16f;
	}

	private float GetRightSidebarLeftEdge(float viewportWidth)
	{
		float rightEdge = viewportWidth - 12f;
		Control controlsPanel = GetNodeOrNull<Control>("UILayer/ControlsPanel");
		if (controlsPanel != null)
		{
			rightEdge = Mathf.Min(rightEdge, controlsPanel.Position.X - 16f);
		}

		if (_logicPanel != null)
		{
			rightEdge = Mathf.Min(rightEdge, _logicPanel.Position.X - 16f);
		}

		return rightEdge;
	}

	private float GetBottomBarTopEdge(float viewportHeight)
	{
		Control bottomBar = GetNodeOrNull<Control>("UILayer/BottomBar");
		return bottomBar == null ? viewportHeight - 12f : bottomBar.Position.Y - 10f;
	}

	private void SetContextObjectEditorEnabled(bool enabled)
	{
		_contextRoleOption.Disabled = !enabled;
		_contextLabelEdit.Editable = enabled;
		_contextTargetIdEdit.Editable = enabled;
		_contextPropDefinitionPathEdit.Editable = enabled;
		_contextPropDefinitionOption.Disabled = !enabled;
		_contextNpcDefinitionPathEdit.Editable = enabled;
		_contextNpcDefinitionOption.Disabled = !enabled;
		_contextNpcPortraitOption.Disabled = !enabled;
		_contextRequiredFlagEdit.Editable = enabled;
		_contextSetFlagEdit.Editable = enabled;
		_contextTriggerModeOption.Disabled = !enabled;
		_contextOneShotCheck.Disabled = !enabled;
		_contextNotesEdit.Editable = enabled;
	}

	private void SyncMainInspectorFromContextPopup()
	{
		if (_isUpdatingContextLogicUi || _isUpdatingLogicUi || !TryGetSelectedPlacedSprite(out _))
		{
			return;
		}

		_isUpdatingLogicUi = true;
		_logicRoleOption.Select(Mathf.Clamp(_contextRoleOption.Selected, 0, _logicRoleOption.ItemCount - 1));
		_logicLabelEdit.Text = _contextLabelEdit.Text;
		_logicTargetIdEdit.Text = _contextTargetIdEdit.Text;
		_logicPropDefinitionPathEdit.Text = _contextPropDefinitionPathEdit.Text;
		_logicNpcDefinitionPathEdit.Text = _contextNpcDefinitionPathEdit.Text;
		if (_logicNpcPortraitOption.ItemCount > 0)
		{
			_logicNpcPortraitOption.Select(Mathf.Clamp(_contextNpcPortraitOption.Selected, 0, _logicNpcPortraitOption.ItemCount - 1));
		}
		_logicRequiredFlagEdit.Text = _contextRequiredFlagEdit.Text;
		_logicSetFlagEdit.Text = _contextSetFlagEdit.Text;
		_logicTriggerModeOption.Select(Mathf.Clamp(_contextTriggerModeOption.Selected, 0, _logicTriggerModeOption.ItemCount - 1));
		_logicOneShotCheck.ButtonPressed = _contextOneShotCheck.ButtonPressed;
		_logicNotesEdit.Text = _contextNotesEdit.Text;
		_isUpdatingLogicUi = false;

		RefreshPropDefinitionOptions(_logicPropDefinitionPathEdit.Text);
		RefreshNpcDefinitionOptions(_logicNpcDefinitionPathEdit.Text);
		UpdatePropDefinitionPreview(_logicPropDefinitionPathEdit.Text);
		UpdateTargetIdFieldContextForCurrentSelection();
		UpdateFlagFieldContextForCurrentSelection();
		ApplyLogicFieldChanges();
		UpdateLogicInspector();
	}

	private void OnContextPropDefinitionOptionSelected(long selectedIndex)
	{
		if (_isUpdatingContextLogicUi || _contextPropDefinitionOption == null)
		{
			return;
		}

		string selectedPath = _contextPropDefinitionOption.GetItemMetadata((int)selectedIndex).AsString();
		_isUpdatingContextLogicUi = true;
		_contextPropDefinitionPathEdit.Text = selectedPath;
		_isUpdatingContextLogicUi = false;
		SyncMainInspectorFromContextPopup();
	}

	private void OnContextNpcDefinitionOptionSelected(long selectedIndex)
	{
		if (_isUpdatingContextLogicUi || _contextNpcDefinitionOption == null)
		{
			return;
		}

		string selectedPath = _contextNpcDefinitionOption.GetItemMetadata((int)selectedIndex).AsString();
		_isUpdatingContextLogicUi = true;
		_contextNpcDefinitionPathEdit.Text = selectedPath;
		_isUpdatingContextLogicUi = false;
		SyncMainInspectorFromContextPopup();
	}

	private static void CopyOptionButtonItems(OptionButton source, OptionButton target)
	{
		if (source == null || target == null)
		{
			return;
		}

		target.Clear();
		for (int i = 0; i < source.ItemCount; i++)
		{
			target.AddItem(source.GetItemText(i), source.GetItemId(i));
			target.SetItemMetadata(i, source.GetItemMetadata(i));
			Texture2D icon = source.GetItemIcon(i);
			if (icon != null)
			{
				target.SetItemIcon(i, icon);
			}
			target.SetItemDisabled(i, source.IsItemDisabled(i));
		}

		if (target.ItemCount > 0)
		{
			target.Select(Mathf.Clamp(source.Selected, 0, target.ItemCount - 1));
		}
	}

	private static void AddPanelSectionHeader(VBoxContainer root, string title, string helpText = "")
	{
		root.AddChild(new HSeparator());

		Label titleLabel = new Label { Text = title };
		titleLabel.AddThemeFontSizeOverride("font_size", 16);
		root.AddChild(titleLabel);

		if (!string.IsNullOrWhiteSpace(helpText))
		{
			Label helpLabel = new Label
			{
				Text = helpText,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			helpLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.8f, 0.9f, 0.9f));
			root.AddChild(helpLabel);
		}
	}

	private void OnPaletteSearchChanged(string newText)
	{
		string normalizedQuery = newText?.StripEdges() ?? string.Empty;
		if (_paletteSearchQuery == normalizedQuery)
		{
			return;
		}

		_paletteSearchQuery = normalizedQuery;
		BuildPalette();
	}

	private bool IsPaletteSectionExpanded(string key)
	{
		return !_paletteSectionExpanded.TryGetValue(key, out bool expanded) || expanded;
	}

	private void TogglePaletteSection(string key)
	{
		_paletteSectionExpanded[key] = !IsPaletteSectionExpanded(key);
		BuildPalette();
	}

	private void AddPaletteSectionHeader(string title, string helpText)
	{
		_paletteContainer.AddChild(new HSeparator());

		Label titleLabel = new Label { Text = title };
		titleLabel.AddThemeFontSizeOverride("font_size", 16);
		_paletteContainer.AddChild(titleLabel);

		Label helpLabel = new Label
		{
			Text = helpText,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		helpLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.8f, 0.9f, 0.9f));
		_paletteContainer.AddChild(helpLabel);
	}

	private void AddPaletteCollapsibleSection(string key, string title, string helpText, Func<VBoxContainer, int> populateContent)
	{
		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 6);
		int itemCount = populateContent(content);
		if (itemCount <= 0 && !string.IsNullOrWhiteSpace(_paletteSearchQuery))
		{
			content.QueueFree();
			return;
		}

		AddPaletteSectionHeader(title, helpText);

		bool forceExpanded = !string.IsNullOrWhiteSpace(_paletteSearchQuery);
		bool expanded = forceExpanded || IsPaletteSectionExpanded(key);
		Button toggleButton = new Button
		{
			Text = expanded ? "Collapse Section" : "Expand Section",
			Alignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(0f, 32f),
			Disabled = forceExpanded
		};
		toggleButton.Pressed += () => TogglePaletteSection(key);
		_paletteContainer.AddChild(toggleButton);

		content.Visible = expanded;
		_paletteContainer.AddChild(content);
	}

	private int BuildMapTilesPaletteContent(VBoxContainer content)
	{
		int itemCount = 0;
		foreach (MissionTileCategory category in new[] { MissionTileCategory.Floor, MissionTileCategory.Wall, MissionTileCategory.Prop })
		{
			List<MissionTileDefinition> definitions = MissionTileCatalog.All
				.Where(def => def.Category == category && def.VisibleInPalette && PaletteMatchesSearch(def.DisplayName, def.Id))
				.ToList();
			if (definitions.Count == 0)
			{
				continue;
			}

			AddPaletteSubsectionLabel(content, category switch
			{
				MissionTileCategory.Floor => "Floors",
				MissionTileCategory.Wall => "Walls",
				_ => "Visual Props"
			});

			foreach (MissionTileDefinition definition in definitions)
			{
				content.AddChild(CreatePaletteTileButton(definition));
				itemCount++;
			}
		}

		return itemCount;
	}

	private int BuildMarkerPaletteContent(VBoxContainer content)
	{
		int itemCount = 0;
		foreach (MissionMarkerCategory category in new[] { MissionMarkerCategory.Spawn, MissionMarkerCategory.Objective, MissionMarkerCategory.Trigger })
		{
			List<MissionMarkerDefinition> definitions = MissionMarkerCatalog.All
				.Where(def => def.Category == category && def.Id != "hostile_spawn" && PaletteMatchesSearch(def.DisplayName, def.Id))
				.ToList();
			if (definitions.Count == 0)
			{
				continue;
			}

			AddPaletteSubsectionLabel(content, category switch
			{
				MissionMarkerCategory.Spawn => "Spawn Markers",
				MissionMarkerCategory.Objective => "Objective Markers",
				_ => "Trigger Markers"
			});

			foreach (MissionMarkerDefinition definition in definitions)
			{
				content.AddChild(CreatePaletteMarkerButton(definition));
				itemCount++;
			}
		}

		return itemCount;
	}

	private int BuildHostileSpawnPaletteContent(VBoxContainer content)
	{
		int itemCount = 0;
		foreach (string npcDefinitionPath in GetAvailableHostileNpcDefinitionPaths())
		{
			NpcDefinitionPreview preview = GetNpcDefinitionPreview(npcDefinitionPath);
			if (!PaletteMatchesSearch(preview.DisplayName, npcDefinitionPath, preview.Description, "hostile spawn"))
			{
				continue;
			}

			content.AddChild(CreatePaletteHostileSpawnButton(npcDefinitionPath, preview));
			itemCount++;
		}

		return itemCount;
	}

	private int BuildRuntimePropPaletteContent(VBoxContainer content)
	{
		int itemCount = 0;
		foreach (string propDefinitionPath in GetAvailablePropDefinitionPaths())
		{
			PropDefinitionPreview preview = GetPropDefinitionPreview(propDefinitionPath);
			if (!PaletteMatchesSearch(preview.DisplayName, propDefinitionPath, preview.Description))
			{
				continue;
			}

			content.AddChild(CreatePaletteRuntimePropButton(propDefinitionPath, preview));
			itemCount++;
		}

		return itemCount;
	}

	private static void AddPaletteSubsectionLabel(VBoxContainer content, string labelText)
	{
		Label label = new Label { Text = labelText };
		label.AddThemeFontSizeOverride("font_size", 14);
		content.AddChild(label);
	}

	private Button CreatePaletteTileButton(MissionTileDefinition definition)
	{
		Button button = new Button
		{
			Text = definition.DisplayName,
			Icon = GetTileIconTexture(definition),
			Alignment = HorizontalAlignment.Left,
			ExpandIcon = true,
			CustomMinimumSize = new Vector2(0f, 40f),
			TooltipText = definition.Id
		};
		button.Pressed += () =>
		{
			_selectedTile = definition;
			_selectedMarker = null;
			_selectedPropDefinitionPath = string.Empty;
			_selectedHostileNpcDefinitionPath = string.Empty;
			ClearPlacedSelection();
			UpdateSelectedLabel();
			UpdateLogicInspector();
		};
		return button;
	}

	private Button CreatePaletteMarkerButton(MissionMarkerDefinition definition)
	{
		Button button = new Button
		{
			Text = definition.DisplayName,
			Icon = GetMarkerIconTexture(definition),
			Alignment = HorizontalAlignment.Left,
			ExpandIcon = true,
			CustomMinimumSize = new Vector2(0f, 40f),
			TooltipText = definition.Id
		};
		button.Pressed += () =>
		{
			_selectedTile = null;
			_selectedMarker = definition;
			_selectedPropDefinitionPath = string.Empty;
			_selectedHostileNpcDefinitionPath = string.Empty;
			ClearPlacedSelection();
			UpdateSelectedLabel();
			UpdateLogicInspector();
		};
		return button;
	}

	private Button CreatePaletteRuntimePropButton(string propDefinitionPath, PropDefinitionPreview preview)
	{
		Button button = new Button
		{
			Text = preview.DisplayName,
			Icon = preview.Icon,
			Alignment = HorizontalAlignment.Left,
			ExpandIcon = true,
			CustomMinimumSize = new Vector2(0f, 40f),
			TooltipText = string.IsNullOrWhiteSpace(preview.Description) ? propDefinitionPath : $"{preview.Description}\n{propDefinitionPath}"
		};
		button.Pressed += () =>
		{
			_selectedTile = null;
			_selectedMarker = null;
			_selectedPropDefinitionPath = propDefinitionPath;
			_selectedHostileNpcDefinitionPath = string.Empty;
			ClearPlacedSelection();
			UpdateSelectedLabel();
			UpdateLogicInspector();
		};
		return button;
	}

	private Button CreatePaletteHostileSpawnButton(string npcDefinitionPath, NpcDefinitionPreview preview)
	{
		Button button = new Button
		{
			Text = preview.DisplayName,
			Icon = preview.Icon,
			Alignment = HorizontalAlignment.Left,
			ExpandIcon = true,
			CustomMinimumSize = new Vector2(0f, 40f),
			TooltipText = string.IsNullOrWhiteSpace(preview.Description) ? npcDefinitionPath : $"{preview.Description}\n{npcDefinitionPath}"
		};
		button.Pressed += () =>
		{
			_selectedTile = null;
			_selectedMarker = null;
			_selectedPropDefinitionPath = string.Empty;
			_selectedHostileNpcDefinitionPath = npcDefinitionPath;
			ClearPlacedSelection();
			UpdateSelectedLabel();
			UpdateLogicInspector();
		};
		return button;
	}

	private bool PaletteMatchesSearch(params string[] values)
	{
		if (string.IsNullOrWhiteSpace(_paletteSearchQuery))
		{
			return true;
		}

		string query = _paletteSearchQuery.Trim();
		return values.Any(value => !string.IsNullOrWhiteSpace(value)
			&& value.Contains(query, StringComparison.OrdinalIgnoreCase));
	}

	private void BuildDialogueEditor(VBoxContainer root)
	{
		root.AddChild(new HSeparator());

		Label title = new Label { Text = "Dialogue Editor" };
		title.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(title);

		_dialogueSelectionContextLabel = new Label
		{
			Text = "Select a dialogue trigger, dialogue prop, or NPC spawn to bind a conversation.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_dialogueSelectionContextLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.88f, 0.95f));
		root.AddChild(_dialogueSelectionContextLabel);

		root.AddChild(new Label { Text = "Conversation Library" });
		HBoxContainer conversationLibraryRow = new HBoxContainer();
		conversationLibraryRow.AddThemeConstantOverride("separation", 8);
		_dialogueConversationOption = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_dialogueConversationOption.ItemSelected += OnDialogueConversationOptionSelected;
		conversationLibraryRow.AddChild(_dialogueConversationOption);
		_dialogueConversationRefreshButton = new Button { Text = "Refresh" };
		_dialogueConversationRefreshButton.Pressed += RefreshDialogueConversationOptions;
		conversationLibraryRow.AddChild(_dialogueConversationRefreshButton);
		root.AddChild(conversationLibraryRow);

		HBoxContainer conversationActionRow = new HBoxContainer();
		conversationActionRow.AddThemeConstantOverride("separation", 8);
		_dialogueConversationNewButton = new Button
		{
			Text = "New Conversation",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_dialogueConversationNewButton.Pressed += CreateNewDialogueConversation;
		conversationActionRow.AddChild(_dialogueConversationNewButton);
		_dialogueConversationLoadSelectedButton = new Button
		{
			Text = "Load Selected Binding",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_dialogueConversationLoadSelectedButton.Pressed += LoadDialogueConversationFromSelection;
		conversationActionRow.AddChild(_dialogueConversationLoadSelectedButton);
		root.AddChild(conversationActionRow);

		HBoxContainer conversationBindRow = new HBoxContainer();
		conversationBindRow.AddThemeConstantOverride("separation", 8);
		_dialogueConversationAssignSelectedButton = new Button
		{
			Text = "Assign Current To Selection",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_dialogueConversationAssignSelectedButton.Pressed += AssignActiveDialogueConversationToSelection;
		conversationBindRow.AddChild(_dialogueConversationAssignSelectedButton);
		root.AddChild(conversationBindRow);

		root.AddChild(new Label { Text = "Conversation ID" });
		_dialogueConversationIdEdit = new LineEdit
		{
			PlaceholderText = "smuggler_exchange_intro"
		};
		_dialogueConversationIdEdit.TextChanged += OnDialogueConversationIdChanged;
		root.AddChild(_dialogueConversationIdEdit);

		root.AddChild(new Label { Text = "Node Library" });
		HBoxContainer nodeLibraryRow = new HBoxContainer();
		nodeLibraryRow.AddThemeConstantOverride("separation", 8);
		_dialogueNodeOption = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_dialogueNodeOption.ItemSelected += OnDialogueNodeOptionSelected;
		nodeLibraryRow.AddChild(_dialogueNodeOption);
		_dialogueNodeNewButton = new Button { Text = "New Node" };
		_dialogueNodeNewButton.Pressed += AddDialogueNode;
		nodeLibraryRow.AddChild(_dialogueNodeNewButton);
		_dialogueNodeDeleteButton = new Button { Text = "Delete Node" };
		_dialogueNodeDeleteButton.Pressed += DeleteActiveDialogueNode;
		nodeLibraryRow.AddChild(_dialogueNodeDeleteButton);
		root.AddChild(nodeLibraryRow);

		root.AddChild(new Label { Text = "Node ID" });
		_dialogueNodeIdEdit = new LineEdit
		{
			PlaceholderText = "Start"
		};
		_dialogueNodeIdEdit.TextChanged += OnDialogueNodeIdChanged;
		root.AddChild(_dialogueNodeIdEdit);

		root.AddChild(new Label { Text = "Speaker" });
		_dialogueSpeakerEdit = new LineEdit();
		_dialogueSpeakerEdit.TextChanged += _ => OnDialogueNodeFieldsChanged();
		root.AddChild(_dialogueSpeakerEdit);

		root.AddChild(new Label { Text = "Text" });
		_dialogueTextEdit = new TextEdit
		{
			CustomMinimumSize = new Vector2(0f, 130f),
			WrapMode = TextEdit.LineWrappingMode.Boundary
		};
		_dialogueTextEdit.TextChanged += OnDialogueNodeFieldsChanged;
		root.AddChild(_dialogueTextEdit);

		root.AddChild(new Label { Text = "Quest Trigger" });
		_dialogueQuestTriggerEdit = new LineEdit
		{
			PlaceholderText = "Optional quest id"
		};
		_dialogueQuestTriggerEdit.TextChanged += _ => OnDialogueNodeFieldsChanged();
		root.AddChild(_dialogueQuestTriggerEdit);

		root.AddChild(new Label { Text = "Required Flags" });
		_dialogueRequiredFlagsEdit = new LineEdit
		{
			PlaceholderText = "flag_a, flag_b"
		};
		_dialogueRequiredFlagsEdit.TextChanged += _ => OnDialogueNodeFieldsChanged();
		root.AddChild(_dialogueRequiredFlagsEdit);

		root.AddChild(new Label { Text = "Blocked Flags" });
		_dialogueBlockedFlagsEdit = new LineEdit
		{
			PlaceholderText = "flag_c"
		};
		_dialogueBlockedFlagsEdit.TextChanged += _ => OnDialogueNodeFieldsChanged();
		root.AddChild(_dialogueBlockedFlagsEdit);

		root.AddChild(new Label { Text = "Set Flags" });
		_dialogueSetFlagsEdit = new LineEdit
		{
			PlaceholderText = "flag_rewarded"
		};
		_dialogueSetFlagsEdit.TextChanged += _ => OnDialogueNodeFieldsChanged();
		root.AddChild(_dialogueSetFlagsEdit);

		root.AddChild(new Label { Text = "Options" });
		_dialogueOptionsContainer = new VBoxContainer();
		_dialogueOptionsContainer.AddThemeConstantOverride("separation", 10);
		root.AddChild(_dialogueOptionsContainer);

		_dialogueAddOptionButton = new Button
		{
			Text = "Add Option",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_dialogueAddOptionButton.Pressed += AddDialogueOption;
		root.AddChild(_dialogueAddOptionButton);

		_dialogueSaveButton = new Button
		{
			Text = "Save Conversation",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_dialogueSaveButton.Pressed += SaveActiveDialogueConversation;
		root.AddChild(_dialogueSaveButton);

		_dialogueEditorStatusLabel = new Label
		{
			Text = "Load a conversation or create a new one to start authoring dialogue.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_dialogueEditorStatusLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.87f, 0.9f, 0.95f));
		root.AddChild(_dialogueEditorStatusLabel);

		UpdateDialogueSelectionContext();
		UpdateDialogueEditorUi();
	}

	private void RefreshDialogueConversationOptions()
	{
		RefreshDialogueConversationOptions(_activeDialogueConversation?.ConversationId ?? string.Empty);
	}

	private void RefreshDialogueConversationOptions(string selectedConversationId)
	{
		if (_dialogueConversationOption == null)
		{
			return;
		}

		string normalizedId = selectedConversationId?.StripEdges() ?? string.Empty;
		List<string> conversationIds = DialogueRegistry.GetConversationIds().ToList();
		if (!string.IsNullOrEmpty(normalizedId) && !conversationIds.Contains(normalizedId))
		{
			conversationIds.Add(normalizedId);
			conversationIds = conversationIds.OrderBy(id => id).ToList();
		}

		_isUpdatingDialogueUi = true;
		_dialogueConversationOption.Clear();
		_dialogueConversationOption.AddItem("None", 0);
		_dialogueConversationOption.SetItemMetadata(0, string.Empty);
		int selectedIndex = 0;
		for (int i = 0; i < conversationIds.Count; i++)
		{
			string conversationId = conversationIds[i];
			int itemIndex = i + 1;
			string label = conversationId == normalizedId && !DialogueRegistry.GetConversationIds().Contains(conversationId)
				? $"{conversationId} (unsaved)"
				: conversationId;
			_dialogueConversationOption.AddItem(label, itemIndex);
			_dialogueConversationOption.SetItemMetadata(itemIndex, conversationId);
			if (conversationId == normalizedId)
			{
				selectedIndex = itemIndex;
			}
		}
		_dialogueConversationOption.Select(selectedIndex);
		_isUpdatingDialogueUi = false;
	}

	private void OnDialogueConversationOptionSelected(long selectedIndex)
	{
		if (_isUpdatingDialogueUi || _dialogueConversationOption == null)
		{
			return;
		}

		string conversationId = _dialogueConversationOption.GetItemMetadata((int)selectedIndex).AsString();
		if (string.IsNullOrWhiteSpace(conversationId))
		{
			_activeDialogueConversation = null;
			_activeDialogueNodeId = string.Empty;
			SetDialogueEditorStatus("Dialogue editor cleared.");
			UpdateDialogueEditorUi();
			return;
		}

		LoadDialogueConversation(conversationId);
	}

	private void LoadDialogueConversation(string conversationId)
	{
		string normalizedId = conversationId?.StripEdges() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(normalizedId))
		{
			_activeDialogueConversation = null;
			_activeDialogueNodeId = string.Empty;
			UpdateDialogueEditorUi();
			return;
		}

		DialogueConversationData conversation = DialogueRegistry.LoadConversationData(normalizedId) ?? CreateDefaultDialogueConversation(normalizedId);
		_activeDialogueConversation = conversation;
		EnsureDialogueConversationHasNodes();
		_activeDialogueNodeId = _activeDialogueConversation.Nodes.FirstOrDefault()?.Id ?? string.Empty;
		RefreshDialogueConversationOptions(_activeDialogueConversation.ConversationId);
		UpdateDialogueEditorUi();
		SetDialogueEditorStatus($"Loaded conversation `{_activeDialogueConversation.ConversationId}`.");
	}

	private void CreateNewDialogueConversation()
	{
		string suggestedId = GenerateUniqueDialogueConversationId(GetSuggestedDialogueConversationId());
		_activeDialogueConversation = CreateDefaultDialogueConversation(suggestedId);
		_activeDialogueNodeId = _activeDialogueConversation.Nodes[0].Id;
		RefreshDialogueConversationOptions(_activeDialogueConversation.ConversationId);
		UpdateDialogueEditorUi();
		SetDialogueEditorStatus($"Created draft conversation `{suggestedId}`. Save when you're ready.");
	}

	private static DialogueConversationData CreateDefaultDialogueConversation(string conversationId)
	{
		return new DialogueConversationData
		{
			ConversationId = conversationId,
			Nodes = new List<DialogueNode>
			{
				new DialogueNode
				{
					Id = "Start",
					SpeakerName = string.Empty,
					Text = string.Empty,
					Options = new List<DialogueOption>
					{
						new DialogueOption
						{
							Text = "End conversation",
							NextNodeId = "End"
						}
					}
				}
			}
		};
	}

	private void EnsureDialogueConversationHasNodes()
	{
		if (_activeDialogueConversation == null)
		{
			return;
		}

		_activeDialogueConversation.Nodes ??= new List<DialogueNode>();
		if (_activeDialogueConversation.Nodes.Count == 0)
		{
			_activeDialogueConversation.Nodes.Add(new DialogueNode
			{
				Id = "Start",
				Options = new List<DialogueOption>()
			});
		}

		foreach (DialogueNode node in _activeDialogueConversation.Nodes)
		{
			node.Options ??= new List<DialogueOption>();
			node.RequiredFlags ??= new List<string>();
			node.BlockedFlags ??= new List<string>();
			node.SetFlags ??= new List<string>();
		}
	}

	private void OnDialogueConversationIdChanged(string newText)
	{
		if (_isUpdatingDialogueUi || _activeDialogueConversation == null)
		{
			return;
		}

		_activeDialogueConversation.ConversationId = SanitizeDialogueId(newText);
		RefreshDialogueConversationOptions(_activeDialogueConversation.ConversationId);
		UpdateDialogueSelectionContext();
		SetDialogueEditorStatus("Conversation ID updated. Save to write the JSON file.");
	}

	private void RefreshDialogueNodeOptions()
	{
		if (_dialogueNodeOption == null)
		{
			return;
		}

		_isUpdatingDialogueUi = true;
		_dialogueNodeOption.Clear();
		_dialogueNodeOption.AddItem("None", 0);
		_dialogueNodeOption.SetItemMetadata(0, string.Empty);
		int selectedIndex = 0;
		if (_activeDialogueConversation != null)
		{
			for (int i = 0; i < _activeDialogueConversation.Nodes.Count; i++)
			{
				DialogueNode node = _activeDialogueConversation.Nodes[i];
				int itemIndex = i + 1;
				string label = string.IsNullOrWhiteSpace(node.SpeakerName)
					? node.Id
					: $"{node.Id} ({node.SpeakerName})";
				_dialogueNodeOption.AddItem(label, itemIndex);
				_dialogueNodeOption.SetItemMetadata(itemIndex, node.Id);
				if (node.Id == _activeDialogueNodeId)
				{
					selectedIndex = itemIndex;
				}
			}
		}
		_dialogueNodeOption.Select(selectedIndex);
		_isUpdatingDialogueUi = false;
	}

	private void OnDialogueNodeOptionSelected(long selectedIndex)
	{
		if (_isUpdatingDialogueUi || _dialogueNodeOption == null)
		{
			return;
		}

		_activeDialogueNodeId = _dialogueNodeOption.GetItemMetadata((int)selectedIndex).AsString();
		UpdateDialogueEditorUi();
	}

	private void AddDialogueNode()
	{
		if (_activeDialogueConversation == null)
		{
			CreateNewDialogueConversation();
		}

		EnsureDialogueConversationHasNodes();
		string nodeId = GenerateUniqueDialogueNodeId();
		DialogueNode node = new DialogueNode
		{
			Id = nodeId,
			Options = new List<DialogueOption>()
		};
		_activeDialogueConversation.Nodes.Add(node);
		_activeDialogueNodeId = nodeId;
		RefreshDialogueNodeOptions();
		UpdateDialogueEditorUi();
		SetDialogueEditorStatus($"Added node `{nodeId}`.");
	}

	private void DeleteActiveDialogueNode()
	{
		DialogueNode node = GetActiveDialogueNode();
		if (_activeDialogueConversation == null || node == null)
		{
			return;
		}

		_activeDialogueConversation.Nodes.Remove(node);
		EnsureDialogueConversationHasNodes();
		_activeDialogueNodeId = _activeDialogueConversation.Nodes.FirstOrDefault()?.Id ?? string.Empty;
		RefreshDialogueNodeOptions();
		UpdateDialogueEditorUi();
		SetDialogueEditorStatus($"Deleted node `{node.Id}`.");
	}

	private void OnDialogueNodeIdChanged(string newText)
	{
		if (_isUpdatingDialogueUi)
		{
			return;
		}

		DialogueNode node = GetActiveDialogueNode();
		if (node == null)
		{
			return;
		}

		string sanitizedId = SanitizeDialogueId(newText);
		if (string.IsNullOrWhiteSpace(sanitizedId))
		{
			return;
		}

		node.Id = sanitizedId;
		_activeDialogueNodeId = sanitizedId;
		RefreshDialogueNodeOptions();
		SetDialogueEditorStatus("Node ID updated. Save to persist it.");
	}

	private void OnDialogueNodeFieldsChanged()
	{
		if (_isUpdatingDialogueUi)
		{
			return;
		}

		DialogueNode node = GetActiveDialogueNode();
		if (node == null)
		{
			return;
		}

		node.SpeakerName = _dialogueSpeakerEdit?.Text?.StripEdges() ?? string.Empty;
		node.Text = _dialogueTextEdit?.Text?.StripEdges() ?? string.Empty;
		node.QuestToTrigger = _dialogueQuestTriggerEdit?.Text?.StripEdges() ?? string.Empty;
		node.RequiredFlags = ParseDialogueFlagList(_dialogueRequiredFlagsEdit?.Text);
		node.BlockedFlags = ParseDialogueFlagList(_dialogueBlockedFlagsEdit?.Text);
		node.SetFlags = ParseDialogueFlagList(_dialogueSetFlagsEdit?.Text);
		RefreshDialogueNodeOptions();
		SetDialogueEditorStatus("Updated active node fields.");
	}

	private void AddDialogueOption()
	{
		DialogueNode node = GetActiveDialogueNode();
		if (node == null)
		{
			return;
		}

		node.Options.Add(new DialogueOption
		{
			Text = "New option",
			NextNodeId = "End"
		});
		RebuildDialogueOptionsEditor();
		SetDialogueEditorStatus("Added a dialogue option.");
	}

	private void RemoveDialogueOption(int optionIndex)
	{
		DialogueNode node = GetActiveDialogueNode();
		if (node == null || optionIndex < 0 || optionIndex >= node.Options.Count)
		{
			return;
		}

		node.Options.RemoveAt(optionIndex);
		RebuildDialogueOptionsEditor();
		SetDialogueEditorStatus("Removed a dialogue option.");
	}

	private void RebuildDialogueOptionsEditor()
	{
		if (_dialogueOptionsContainer == null)
		{
			return;
		}

		foreach (Node child in _dialogueOptionsContainer.GetChildren())
		{
			child.QueueFree();
		}

		DialogueNode node = GetActiveDialogueNode();
		if (node == null)
		{
			_dialogueOptionsContainer.AddChild(new Label
			{
				Text = "No node selected.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			});
			return;
		}

		if (node.Options.Count == 0)
		{
			_dialogueOptionsContainer.AddChild(new Label
			{
				Text = "This node has no options yet. Add one below.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			});
			return;
		}

		for (int i = 0; i < node.Options.Count; i++)
		{
			int optionIndex = i;
			DialogueOption option = node.Options[optionIndex];

			PanelContainer optionPanel = new PanelContainer();
			_dialogueOptionsContainer.AddChild(optionPanel);

			MarginContainer optionMargin = new MarginContainer();
			optionMargin.AddThemeConstantOverride("margin_left", 8);
			optionMargin.AddThemeConstantOverride("margin_top", 8);
			optionMargin.AddThemeConstantOverride("margin_right", 8);
			optionMargin.AddThemeConstantOverride("margin_bottom", 8);
			optionPanel.AddChild(optionMargin);

			VBoxContainer optionRoot = new VBoxContainer();
			optionRoot.AddThemeConstantOverride("separation", 6);
			optionMargin.AddChild(optionRoot);

			HBoxContainer optionHeader = new HBoxContainer();
			optionHeader.AddThemeConstantOverride("separation", 8);
			Label optionLabel = new Label
			{
				Text = $"Option {optionIndex + 1}",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			optionHeader.AddChild(optionLabel);
			Button removeButton = new Button { Text = "Remove" };
			removeButton.Pressed += () => RemoveDialogueOption(optionIndex);
			optionHeader.AddChild(removeButton);
			optionRoot.AddChild(optionHeader);

			optionRoot.AddChild(new Label { Text = "Text" });
			LineEdit optionTextEdit = new LineEdit
			{
				Text = option.Text
			};
			optionTextEdit.TextChanged += value =>
			{
				option.Text = value.StripEdges();
				SetDialogueEditorStatus("Updated option text.");
			};
			optionRoot.AddChild(optionTextEdit);

			optionRoot.AddChild(new Label { Text = "Next Node" });
			LineEdit optionNextEdit = new LineEdit
			{
				Text = option.NextNodeId,
				PlaceholderText = "End"
			};
			optionNextEdit.TextChanged += value =>
			{
				option.NextNodeId = string.IsNullOrWhiteSpace(value) ? "End" : value.StripEdges();
				SetDialogueEditorStatus("Updated option destination.");
			};
			optionRoot.AddChild(optionNextEdit);

			optionRoot.AddChild(new Label { Text = "Required Flags" });
			LineEdit optionRequiredEdit = new LineEdit
			{
				Text = FormatDialogueFlagList(option.RequiredFlags),
				PlaceholderText = "flag_a, flag_b"
			};
			optionRequiredEdit.TextChanged += value =>
			{
				option.RequiredFlags = ParseDialogueFlagList(value);
				SetDialogueEditorStatus("Updated option required flags.");
			};
			optionRoot.AddChild(optionRequiredEdit);

			optionRoot.AddChild(new Label { Text = "Blocked Flags" });
			LineEdit optionBlockedEdit = new LineEdit
			{
				Text = FormatDialogueFlagList(option.BlockedFlags),
				PlaceholderText = "flag_c"
			};
			optionBlockedEdit.TextChanged += value =>
			{
				option.BlockedFlags = ParseDialogueFlagList(value);
				SetDialogueEditorStatus("Updated option blocked flags.");
			};
			optionRoot.AddChild(optionBlockedEdit);

			optionRoot.AddChild(new Label { Text = "Set Flags" });
			LineEdit optionSetEdit = new LineEdit
			{
				Text = FormatDialogueFlagList(option.SetFlags),
				PlaceholderText = "flag_rewarded"
			};
			optionSetEdit.TextChanged += value =>
			{
				option.SetFlags = ParseDialogueFlagList(value);
				SetDialogueEditorStatus("Updated option reward flags.");
			};
			optionRoot.AddChild(optionSetEdit);

			optionRoot.AddChild(new Label { Text = "Quest Trigger" });
			LineEdit optionQuestEdit = new LineEdit
			{
				Text = option.QuestToTrigger,
				PlaceholderText = "Optional quest id"
			};
			optionQuestEdit.TextChanged += value =>
			{
				option.QuestToTrigger = value.StripEdges();
				SetDialogueEditorStatus("Updated option quest trigger.");
			};
			optionRoot.AddChild(optionQuestEdit);
		}
	}

	private void UpdateDialogueEditorUi()
	{
		if (_dialogueConversationIdEdit == null)
		{
			return;
		}

		EnsureDialogueConversationHasNodes();
		DialogueNode node = GetActiveDialogueNode();
		bool hasConversation = _activeDialogueConversation != null;
		bool hasNode = node != null;

		_isUpdatingDialogueUi = true;
		_dialogueConversationIdEdit.Editable = hasConversation;
		_dialogueConversationIdEdit.Text = _activeDialogueConversation?.ConversationId ?? string.Empty;
		RefreshDialogueNodeOptions();
		_dialogueNodeIdEdit.Editable = hasNode;
		_dialogueNodeIdEdit.Text = node?.Id ?? string.Empty;
		_dialogueSpeakerEdit.Editable = hasNode;
		_dialogueSpeakerEdit.Text = node?.SpeakerName ?? string.Empty;
		_dialogueTextEdit.Editable = hasNode;
		_dialogueTextEdit.Text = node?.Text ?? string.Empty;
		_dialogueQuestTriggerEdit.Editable = hasNode;
		_dialogueQuestTriggerEdit.Text = node?.QuestToTrigger ?? string.Empty;
		_dialogueRequiredFlagsEdit.Editable = hasNode;
		_dialogueRequiredFlagsEdit.Text = FormatDialogueFlagList(node?.RequiredFlags);
		_dialogueBlockedFlagsEdit.Editable = hasNode;
		_dialogueBlockedFlagsEdit.Text = FormatDialogueFlagList(node?.BlockedFlags);
		_dialogueSetFlagsEdit.Editable = hasNode;
		_dialogueSetFlagsEdit.Text = FormatDialogueFlagList(node?.SetFlags);
		_dialogueAddOptionButton.Disabled = !hasNode;
		_dialogueNodeNewButton.Disabled = !hasConversation;
		_dialogueNodeDeleteButton.Disabled = !hasNode;
		_dialogueSaveButton.Disabled = !hasConversation;
		_isUpdatingDialogueUi = false;

		RebuildDialogueOptionsEditor();
		UpdateDialogueSelectionContext();
	}

	private DialogueNode GetActiveDialogueNode()
	{
		if (_activeDialogueConversation == null)
		{
			return null;
		}

		DialogueNode node = _activeDialogueConversation.Nodes.FirstOrDefault(item => item.Id == _activeDialogueNodeId);
		if (node == null)
		{
			node = _activeDialogueConversation.Nodes.FirstOrDefault();
			_activeDialogueNodeId = node?.Id ?? string.Empty;
		}

		return node;
	}

	private void SaveActiveDialogueConversation()
	{
		if (_activeDialogueConversation == null)
		{
			SetDialogueEditorStatus("No active conversation to save.");
			return;
		}

		string conversationId = SanitizeDialogueId(_activeDialogueConversation.ConversationId);
		if (string.IsNullOrWhiteSpace(conversationId))
		{
			SetDialogueEditorStatus("Conversation ID cannot be empty.");
			return;
		}

		_activeDialogueConversation.ConversationId = conversationId;
		EnsureDialogueConversationHasNodes();
		HashSet<string> nodeIds = new HashSet<string>();
		foreach (DialogueNode node in _activeDialogueConversation.Nodes)
		{
			node.Id = SanitizeDialogueId(node.Id);
			if (string.IsNullOrWhiteSpace(node.Id))
			{
				SetDialogueEditorStatus("Every dialogue node needs an ID before saving.");
				return;
			}

			if (!nodeIds.Add(node.Id))
			{
				SetDialogueEditorStatus($"Duplicate node ID `{node.Id}`. Give each node a unique ID.");
				return;
			}
		}

		if (!DialogueRegistry.SaveConversationData(_activeDialogueConversation))
		{
			SetDialogueEditorStatus($"Failed to save `{conversationId}`.");
			return;
		}

		RefreshDialogueConversationOptions(conversationId);
		UpdateDialogueEditorUi();
		SetDialogueEditorStatus($"Saved conversation `{conversationId}`.");
	}

	private void LoadDialogueConversationFromSelection()
	{
		if (!TryGetSelectedDialogueBinding(out DialogueBindingInfo binding))
		{
			SetDialogueEditorStatus("Selected item does not expose a dialogue binding yet.");
			return;
		}

		if (string.IsNullOrWhiteSpace(binding.ConversationId))
		{
			SetDialogueEditorStatus("Selected item has no dialogue conversation assigned yet. Create a new one first.");
			return;
		}

		LoadDialogueConversation(binding.ConversationId);
	}

	private void AssignActiveDialogueConversationToSelection()
	{
		if (_activeDialogueConversation == null || string.IsNullOrWhiteSpace(_activeDialogueConversation.ConversationId))
		{
			SetDialogueEditorStatus("Create or load a conversation before assigning it.");
			return;
		}

		if (!TryGetSelectedDialogueBinding(out DialogueBindingInfo binding))
		{
			SetDialogueEditorStatus("Selected item does not support dialogue binding.");
			return;
		}

		switch (binding.Kind)
		{
			case DialogueBindingKind.LayoutTargetId:
				binding.Sprite.SetMeta("logic_target_id", _activeDialogueConversation.ConversationId);
				UpdateMarkerCaption(binding.Sprite);
				UpdateLogicInspector();
				SetDialogueEditorStatus($"Assigned `{_activeDialogueConversation.ConversationId}` to {binding.Description}.");
				break;
			case DialogueBindingKind.NpcDefinition:
				if (string.IsNullOrWhiteSpace(binding.NpcDefinitionPath) || !ResourceLoader.Exists(binding.NpcDefinitionPath))
				{
					SetDialogueEditorStatus("Selected NPC definition is missing.");
					return;
				}

				MissionNpcDefinition npcDefinition = GD.Load<MissionNpcDefinition>(binding.NpcDefinitionPath);
				npcDefinition.DefaultDialogueId = _activeDialogueConversation.ConversationId;
				Error saveResult = ResourceSaver.Save(npcDefinition, binding.NpcDefinitionPath);
				if (saveResult != Error.Ok)
				{
					SetDialogueEditorStatus($"Failed to update NPC definition at `{binding.NpcDefinitionPath}`.");
					return;
				}

				_npcDefinitionPreviewCache.Remove(binding.NpcDefinitionPath);
				UpdateLogicInspector();
				SetDialogueEditorStatus($"Assigned `{_activeDialogueConversation.ConversationId}` to {binding.Description}.");
				break;
			default:
				SetDialogueEditorStatus("Selected item does not support dialogue binding.");
				break;
		}
	}

	private void UpdateDialogueSelectionContext()
	{
		if (_dialogueSelectionContextLabel == null)
		{
			return;
		}

		if (TryGetSelectedDialogueBinding(out DialogueBindingInfo binding))
		{
			string conversationText = string.IsNullOrWhiteSpace(binding.ConversationId)
				? "No conversation assigned yet."
				: $"Current conversation: `{binding.ConversationId}`.";
			_dialogueSelectionContextLabel.Text = $"{binding.Description}. {conversationText}";
			_dialogueConversationLoadSelectedButton.Disabled = string.IsNullOrWhiteSpace(binding.ConversationId);
			_dialogueConversationAssignSelectedButton.Disabled = _activeDialogueConversation == null || string.IsNullOrWhiteSpace(_activeDialogueConversation.ConversationId);
			return;
		}

		_dialogueSelectionContextLabel.Text = "Select a dialogue trigger, dialogue prop, or NPC spawn to bind a conversation.";
		if (_dialogueConversationLoadSelectedButton != null)
		{
			_dialogueConversationLoadSelectedButton.Disabled = true;
		}
		if (_dialogueConversationAssignSelectedButton != null)
		{
			_dialogueConversationAssignSelectedButton.Disabled = true;
		}
	}

	private bool TryGetSelectedDialogueBinding(out DialogueBindingInfo binding)
	{
		binding = null;
		if (!TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			return false;
		}

		string markerId = selectedSprite.GetMeta("marker_id", string.Empty).AsString();
		string npcDefinitionPath = selectedSprite.GetMeta("npc_definition_path", string.Empty).AsString();
		if ((markerId == "npc_spawn" || markerId == "hostile_spawn") && !string.IsNullOrWhiteSpace(npcDefinitionPath) && ResourceLoader.Exists(npcDefinitionPath))
		{
			MissionNpcDefinition definition = GD.Load<MissionNpcDefinition>(npcDefinitionPath);
			binding = new DialogueBindingInfo
			{
				Kind = DialogueBindingKind.NpcDefinition,
				Sprite = selectedSprite,
				ConversationId = definition?.DefaultDialogueId ?? string.Empty,
				Description = $"{(markerId == "hostile_spawn" ? "Hostile spawn" : "NPC spawn")} `{GetItemDisplayId(selectedSprite)}` uses its NPC definition dialogue",
				NpcDefinitionPath = npcDefinitionPath
			};
			return true;
		}

		bool isPlacedProp = IsPlacedPropSprite(selectedSprite);
		PropDefinition definitionForItem = ResolveTargetIdContextDefinition(selectedSprite, isPlacedProp);
		bool isDialogueMarker = markerId == "trigger_dialogue";
		bool isDialogueProp = definitionForItem?.InteractionType == PropInteractionType.Dialogue;
		if (!isDialogueMarker && !isDialogueProp)
		{
			return false;
		}

		binding = new DialogueBindingInfo
		{
			Kind = DialogueBindingKind.LayoutTargetId,
			Sprite = selectedSprite,
			ConversationId = selectedSprite.GetMeta("logic_target_id", string.Empty).AsString(),
			Description = $"{GetItemDisplayId(selectedSprite)} uses a layout dialogue target id"
		};
		return true;
	}

	private string GetSuggestedDialogueConversationId()
	{
		if (TryGetSelectedDialogueBinding(out DialogueBindingInfo binding))
		{
			if (!string.IsNullOrWhiteSpace(binding.ConversationId))
			{
				return binding.ConversationId;
			}

			if (binding.Kind == DialogueBindingKind.NpcDefinition && !string.IsNullOrWhiteSpace(binding.NpcDefinitionPath))
			{
				string npcFileName = System.IO.Path.GetFileNameWithoutExtension(binding.NpcDefinitionPath);
				return $"{SanitizeDialogueId(npcFileName)}_dialogue";
			}

			if (binding.Sprite != null)
			{
				string itemId = SanitizeDialogueId(GetItemDisplayId(binding.Sprite));
				if (!string.IsNullOrWhiteSpace(itemId))
				{
					return itemId;
				}
			}
		}

		string layoutId = SanitizeDialogueId(_layoutNameEdit?.Text);
		return string.IsNullOrWhiteSpace(layoutId) ? "new_dialogue" : $"{layoutId}_dialogue";
	}

	private string GenerateUniqueDialogueConversationId(string baseId)
	{
		string sanitizedBaseId = SanitizeDialogueId(baseId);
		if (string.IsNullOrWhiteSpace(sanitizedBaseId))
		{
			sanitizedBaseId = "new_dialogue";
		}

		HashSet<string> existingIds = DialogueRegistry.GetConversationIds().ToHashSet();
		string candidate = sanitizedBaseId;
		int suffix = 2;
		while (existingIds.Contains(candidate))
		{
			candidate = $"{sanitizedBaseId}_{suffix}";
			suffix++;
		}

		return candidate;
	}

	private string GenerateUniqueDialogueNodeId()
	{
		HashSet<string> existingIds = _activeDialogueConversation?.Nodes.Select(node => node.Id).ToHashSet()
			?? new HashSet<string>();
		string baseId = "Node";
		int suffix = 1;
		string candidate = $"{baseId}_{suffix}";
		while (existingIds.Contains(candidate))
		{
			suffix++;
			candidate = $"{baseId}_{suffix}";
		}

		return candidate;
	}

	private static string SanitizeDialogueId(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		char[] characters = value.Trim().ToLowerInvariant().ToCharArray();
		for (int i = 0; i < characters.Length; i++)
		{
			char current = characters[i];
			if ((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9') || current == '_')
			{
				continue;
			}

			characters[i] = '_';
		}

		string sanitized = new string(characters);
		while (sanitized.Contains("__"))
		{
			sanitized = sanitized.Replace("__", "_");
		}

		return sanitized.Trim('_');
	}

	private static List<string> ParseDialogueFlagList(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new List<string>();
		}

		return value
			.Split(new[] { ',', ';', '|', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
			.Select(flag => flag.StripEdges())
			.Where(flag => !string.IsNullOrWhiteSpace(flag))
			.Distinct()
			.ToList();
	}

	private static string FormatDialogueFlagList(IEnumerable<string> flags)
	{
		return string.Join(", ", flags?.Where(flag => !string.IsNullOrWhiteSpace(flag)) ?? Enumerable.Empty<string>());
	}

	private void SetDialogueEditorStatus(string message)
	{
		if (_dialogueEditorStatusLabel != null && !string.IsNullOrWhiteSpace(message))
		{
			_dialogueEditorStatusLabel.Text = message;
		}
	}

	private void BuildPalette()
	{
		foreach (Node child in _paletteContainer.GetChildren())
		{
			child.QueueFree();
		}

		Label workflowLabel = new Label
		{
			Text = "Build Order: 1. Floors and walls. 2. Props and terminals. 3. Spawns, objectives, and NPC markers.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		workflowLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.85f, 0.94f, 0.95f));
		_paletteContainer.AddChild(workflowLabel);

		_paletteContainer.AddChild(new Label { Text = "Search" });
		_paletteSearchEdit = new LineEdit
		{
			Text = _paletteSearchQuery,
			PlaceholderText = "Search tiles, markers, and prop definitions..."
		};
		_paletteSearchEdit.TextChanged += OnPaletteSearchChanged;
		_paletteContainer.AddChild(_paletteSearchEdit);

		AddPaletteCollapsibleSection("map_tiles", "MAP TILES", "Use floors first to block out rooms, then add walls and visual prop tiles.", BuildMapTilesPaletteContent);
		AddPaletteCollapsibleSection("mission_markers", "MISSION MARKERS", "Markers define officer insertion, objectives, dialogue triggers, and NPC spawn anchors.", BuildMarkerPaletteContent);
		AddPaletteCollapsibleSection("hostile_spawns", "HOSTILE SPAWNS", "Place engaged combat enemies directly from your hostile NPC definitions.", BuildHostileSpawnPaletteContent);
		AddPaletteCollapsibleSection("runtime_props", "RUNTIME MISSION PROPS", "These spawn real interactable prop definitions in-mission, not just decorative map art.", BuildRuntimePropPaletteContent);

		if (_selectedTile == null && _selectedMarker == null && string.IsNullOrWhiteSpace(_selectedPropDefinitionPath) && string.IsNullOrWhiteSpace(_selectedHostileNpcDefinitionPath))
		{
			_selectedTile = MissionTileCatalog.All.FirstOrDefault();
		}
	}

	private void UpdateSelectedLabel()
	{
		if (TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			string placedId = GetItemDisplayId(selectedSprite);
			int column = selectedSprite.GetMeta("column", 0).AsInt32();
			int row = selectedSprite.GetMeta("row", 0).AsInt32();
			_selectedLabel.Text = $"Selected: {placedId} @ {column},{row}";
			return;
		}

		if (_selectedMarker != null)
		{
			_selectedLabel.Text = $"Palette: {_selectedMarker.DisplayName}";
			return;
		}

		if (!string.IsNullOrWhiteSpace(_selectedHostileNpcDefinitionPath))
		{
			_selectedLabel.Text = $"Palette: Hostile Spawn - {GetNpcDefinitionPreview(_selectedHostileNpcDefinitionPath).DisplayName}";
			return;
		}

		if (!string.IsNullOrWhiteSpace(_selectedPropDefinitionPath))
		{
			_selectedLabel.Text = $"Palette: {GetPropDefinitionPreview(_selectedPropDefinitionPath).DisplayName}";
			return;
		}

		_selectedLabel.Text = _selectedTile == null ? "Selected: None" : $"Palette: {_selectedTile.DisplayName}";
	}

	private void BuildGrid()
	{
		Node2D gridLayer = GetNode<Node2D>("World/GridLayer");
		foreach (Line2D line in _gridLines)
		{
			line.QueueFree();
		}
		_gridLines.Clear();

		for (int row = 0; row < GridPreviewRows; row++)
		{
			for (int column = 0; column < GridPreviewColumns; column++)
			{
				Vector2 center = IsoGridHelper.GridToWorld(column, row, _tileStep, _gridOrigin);
				Vector2[] points =
				{
					center + new Vector2(0f, -_tileStep.Y * 0.5f),
					center + new Vector2(_tileStep.X * 0.5f, 0f),
					center + new Vector2(0f, _tileStep.Y * 0.5f),
					center + new Vector2(-_tileStep.X * 0.5f, 0f)
				};

				Line2D line = new Line2D
				{
					DefaultColor = new Color(0.34f, 0.52f, 0.74f, 0.42f),
					Width = 2f,
					Closed = true,
					Points = points
				};
				gridLayer.AddChild(line);
				_gridLines.Add(line);
			}
		}
	}

	private void BuildHoverDiamond()
	{
		_hoverDiamond = new Polygon2D
		{
			Color = new Color(0.46f, 0.85f, 0.97f, 0.16f),
			Polygon = new Vector2[]
			{
				new Vector2(0f, -_tileStep.Y * 0.5f),
				new Vector2(_tileStep.X * 0.5f, 0f),
				new Vector2(0f, _tileStep.Y * 0.5f),
				new Vector2(-_tileStep.X * 0.5f, 0f)
			}
		};
		_hoverLayer.AddChild(_hoverDiamond);
	}

	private void UpdateHoverDiamond()
	{
		if (_hoverDiamond == null)
		{
			return;
		}

		Vector2I cell = GetMouseCell();
		_hoverDiamond.Position = IsoGridHelper.GridToWorld(cell.X, cell.Y, _tileStep, _gridOrigin);
	}

	private void FramePlacedMap()
	{
		if (_camera == null)
		{
			return;
		}

		if (!TryGetPlacedSpriteBounds(out Rect2 bounds))
		{
			_camera.Position = Vector2.Zero;
			ApplyZoom(DefaultZoom);
			SetStatus("No placed map content yet. Camera reset to the builder origin.");
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		float marginFactor = 1.2f;
		float widthZoom = bounds.Size.X <= 0f ? DefaultZoom : (bounds.Size.X * marginFactor) / Mathf.Max(viewportSize.X, 1f);
		float heightZoom = bounds.Size.Y <= 0f ? DefaultZoom : (bounds.Size.Y * marginFactor) / Mathf.Max(viewportSize.Y, 1f);
		float targetZoom = Mathf.Clamp(Mathf.Max(Mathf.Max(widthZoom, heightZoom), DefaultZoom), MinZoom, MaxZoom);

		_camera.Position = bounds.GetCenter();
		ApplyZoom(targetZoom);
		SetStatus("Framed the placed map in view.");
	}

	private Vector2I GetMouseCell()
	{
		Vector2 worldPos = GetGlobalMousePosition() - _gridOrigin;
		float halfWidth = _tileStep.X * 0.5f;
		float halfHeight = _tileStep.Y * 0.5f;
		float a = worldPos.X / halfWidth;
		float b = worldPos.Y / halfHeight;
		int column = Mathf.RoundToInt((a + b) * 0.5f);
		int row = Mathf.RoundToInt((b - a) * 0.5f);
		return new Vector2I(column, row);
	}

	private void StartPlacementOrDrag()
	{
		Vector2I cell = GetMouseCell();
		bool wantsDirectEdit = Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Meta);
		if (wantsDirectEdit)
		{
			Sprite2D existing = FindInteractableSpriteForCurrentTool(cell);
			if (existing != null)
			{
				SelectPlacedSprite(existing);
				_draggedSprite = existing;
				_draggedCell = cell;
				SetStatus($"Dragging {GetItemDisplayId(existing)}");
				return;
			}
		}

		if (_selectedTile == null)
		{
			Sprite2D existing = FindInteractableSpriteForCurrentTool(cell);
			if (existing != null)
			{
				SelectPlacedSprite(existing);
				_draggedSprite = existing;
				_draggedCell = cell;
				SetStatus($"Dragging {GetItemDisplayId(existing)}");
				return;
			}

			if (_selectedMarker == null)
			{
				if (!string.IsNullOrWhiteSpace(_selectedHostileNpcDefinitionPath))
				{
					Sprite2D hostileMarker = CreateHostileSpawnMarker(_selectedHostileNpcDefinitionPath, cell.X, cell.Y);
					GetPlacementLayer(BuilderLayer.Marker).AddChild(hostileMarker);
					SelectPlacedSprite(hostileMarker);
					SetStatus($"Placed hostile spawn {GetNpcDefinitionPreview(_selectedHostileNpcDefinitionPath).DisplayName} at {cell.X},{cell.Y}");
					return;
				}

				if (string.IsNullOrWhiteSpace(_selectedPropDefinitionPath))
				{
					return;
				}

				Sprite2D propSprite = CreatePlacedPropSprite(_selectedPropDefinitionPath, cell.X, cell.Y);
				GetPlacementLayer(BuilderLayer.Prop).AddChild(propSprite);
				SelectPlacedSprite(propSprite);
				SetStatus($"Placed {GetPropDefinitionPreview(_selectedPropDefinitionPath).DisplayName} at {cell.X},{cell.Y}");
				return;
			}

			Sprite2D marker = CreateMarker(_selectedMarker, cell.X, cell.Y);
			GetPlacementLayer(BuilderLayer.Marker).AddChild(marker);
			SelectPlacedSprite(marker);
			SetStatus($"Placed {_selectedMarker.DisplayName} at {cell.X},{cell.Y}");
			return;
		}

		if (!AllowsStackingAtCell(_selectedTile))
		{
			Sprite2D existing = FindInteractableSpriteForCurrentTool(cell);
			if (existing != null)
			{
				SelectPlacedSprite(existing);
				_draggedSprite = existing;
				_draggedCell = cell;
				SetStatus($"Dragging {GetItemDisplayId(existing)}");
				return;
			}
		}

		Sprite2D sprite = CreateSprite(_selectedTile, cell.X, cell.Y);
		GetPlacementLayer(GetLayerForTile(_selectedTile)).AddChild(sprite);
		if (_selectedTile.Category == MissionTileCategory.Floor)
		{
			MarkPlacedMapCenterDirty();
		}
		SelectPlacedSprite(sprite);
		SetStatus($"Placed {_selectedTile.DisplayName} at {cell.X},{cell.Y}");
	}

	private void DeleteTileAtMouse()
	{
		SanitizeTransientSpriteReferences();
		Sprite2D sprite = FindSpriteAtMouse();
		if (!IsLiveSprite(sprite))
		{
			return;
		}

		SetStatus($"Removed {GetItemDisplayId(sprite)}");
		if (sprite == _selectedPlacedSprite)
		{
			ClearPlacedSelection();
		}

		if (sprite == _draggedSprite)
		{
			_draggedSprite = null;
		}

		if (IsFloorSprite(sprite))
		{
			MarkPlacedMapCenterDirty();
		}

		sprite.QueueFree();
		if (DoesSpriteAffectValidation(sprite))
		{
			CallDeferred(nameof(RefreshValidationReport));
		}
	}

	private void SelectPlacedSprite(Sprite2D sprite)
	{
		if (!IsLiveSprite(sprite))
		{
			ClearPlacedSelection();
			return;
		}

		if (_selectedPlacedSprite == sprite)
		{
			return;
		}

		ClearPlacedSelection();
		_selectedPlacedSprite = sprite;
		_selectedPlacedSprite.Modulate = Brighten(GetBaseModulate(_selectedPlacedSprite));
		ToggleSelectionOutline(_selectedPlacedSprite, true);
		UpdateSelectedLabel();
		UpdateLogicInspector();
	}

	private void ClearPlacedSelection()
	{
		if (TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			selectedSprite.Modulate = GetBaseModulate(selectedSprite);
			ToggleSelectionOutline(selectedSprite, false);
			_selectedPlacedSprite = null;
		}

		UpdateSelectedLabel();
		UpdateLogicInspector();
	}

	private Sprite2D FindSpriteAtMouse(bool includeFloors = true)
	{
		Vector2 mouseWorld = GetGlobalMousePosition();
		foreach (Node2D layer in GetSelectableLayers())
		{
			if (!includeFloors && layer == _floorLayer)
			{
				continue;
			}

			Godot.Collections.Array<Node> children = layer.GetChildren();
			for (int index = children.Count - 1; index >= 0; index--)
			{
				Node child = children[index];
				if (child is not Sprite2D sprite || !IsLiveSprite(sprite))
				{
					continue;
				}

				Vector2 size = GetSpriteBoundsSize(sprite);
				Rect2 bounds = new Rect2(sprite.GlobalPosition - (size * 0.5f), size);
				if (bounds.HasPoint(mouseWorld))
				{
					return sprite;
				}
			}
		}

		return null;
	}

	private Sprite2D FindInteractableSpriteForCurrentTool(Vector2I cell)
	{
		if (_selectedMarker != null)
		{
			return FindSpriteAtCell(_markerLayer, cell.X, cell.Y);
		}

		if (!string.IsNullOrWhiteSpace(_selectedHostileNpcDefinitionPath))
		{
			return FindSpriteAtCell(_markerLayer, cell.X, cell.Y);
		}

		if (!string.IsNullOrWhiteSpace(_selectedPropDefinitionPath))
		{
			return FindSpriteAtCell(_propLayer, cell.X, cell.Y);
		}

		if (_selectedTile == null)
		{
			return FindSpriteAtMouse();
		}

		Node2D targetLayer = GetPlacementLayer(GetLayerForTile(_selectedTile));
		return FindSpriteAtCell(targetLayer, cell.X, cell.Y);
	}

	private bool AllowsStackingAtCell(MissionTileDefinition definition)
	{
		return definition.Category == MissionTileCategory.Wall || definition.Category == MissionTileCategory.Prop;
	}

	private Sprite2D FindSpriteAtCell(Node2D layer, int column, int row)
	{
		Godot.Collections.Array<Node> children = layer.GetChildren();
		for (int index = children.Count - 1; index >= 0; index--)
		{
			if (children[index] is not Sprite2D sprite || !IsLiveSprite(sprite))
			{
				continue;
			}

			if (sprite.GetMeta("column", int.MinValue).AsInt32() != column)
			{
				continue;
			}

			if (sprite.GetMeta("row", int.MinValue).AsInt32() != row)
			{
				continue;
			}

			return sprite;
		}

		return null;
	}

	private bool ShouldIncludeFloorsForSelection()
	{
		if (_selectedMarker != null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(_selectedHostileNpcDefinitionPath))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(_selectedPropDefinitionPath))
		{
			return false;
		}

		return _selectedTile == null || _selectedTile.Category == MissionTileCategory.Floor;
	}

	private void MoveSpriteToCell(Sprite2D sprite, int column, int row)
	{
		string tileId = sprite.GetMeta("tile_id", "").AsString();
		string markerId = sprite.GetMeta("marker_id", "").AsString();
		if (!string.IsNullOrEmpty(markerId))
		{
			if (!MissionMarkerCatalog.TryGetById(markerId, out MissionMarkerDefinition markerDefinition))
			{
				return;
			}

			Vector2 markerAdjustment = new Vector2(
				sprite.GetMeta("offset_x", 0f).AsSingle(),
				sprite.GetMeta("offset_y", 0f).AsSingle());
			sprite.Position = IsoGridHelper.GridToWorld(column, row, _tileStep, _gridOrigin) + markerDefinition.Offset + markerAdjustment;
			sprite.SetMeta("column", column);
			sprite.SetMeta("row", row);
			UpdateSelectedLabel();
			return;
		}

		if (!MissionTileCatalog.TryGetById(tileId, out MissionTileDefinition definition))
		{
			if (!IsPlacedPropSprite(sprite))
			{
				return;
			}

			Vector2 propAdjustment = new Vector2(
				sprite.GetMeta("offset_x", 0f).AsSingle(),
				sprite.GetMeta("offset_y", 0f).AsSingle());
			sprite.Position = IsoGridHelper.GridToWorld(column, row, _tileStep, _gridOrigin) + propAdjustment;
			sprite.SetMeta("column", column);
			sprite.SetMeta("row", row);
			UpdateSelectedLabel();
			return;
		}

		Vector2 adjustment = new Vector2(
			sprite.GetMeta("offset_x", 0f).AsSingle(),
			sprite.GetMeta("offset_y", 0f).AsSingle());
		sprite.Position = IsoGridHelper.GridToWorld(column, row, _tileStep, _gridOrigin) + definition.Offset + adjustment;
		sprite.SetMeta("column", column);
		sprite.SetMeta("row", row);
		if (definition.Category == MissionTileCategory.Floor)
		{
			MarkPlacedMapCenterDirty();
		}
		UpdateSelectedLabel();
	}

	private void UpdateLogicInspector()
	{
		if (_logicSelectionLabel == null)
		{
			return;
		}

		TryGetSelectedPlacedSprite(out Sprite2D selectedSprite);
		bool isMarker = selectedSprite != null && !string.IsNullOrEmpty(selectedSprite.GetMeta("marker_id", "").AsString());
		bool isLogicProp = selectedSprite != null && IsLogicCapableSprite(selectedSprite);
		bool isPlacedProp = IsPlacedPropSprite(selectedSprite);
		bool isLogicItem = isMarker || isLogicProp || isPlacedProp;
		SetLogicEditorEnabled(isLogicItem);
		_isUpdatingLogicUi = true;

		if (!isLogicItem)
		{
			_logicSelectionLabel.Text = selectedSprite == null
				? "Select a marker, direct mission prop, door, or terminal to edit mission logic."
				: "Selected item does not support mission logic.";
			_logicHintLabel.Text = "Use mission logic on markers, direct mission props, doors, and computer terminals.";
			_logicRoleOption.Select(0);
			_logicLabelEdit.Text = string.Empty;
			_logicTargetIdEdit.Text = string.Empty;
			_logicTargetIdEdit.PlaceholderText = string.Empty;
			_logicTargetIdHelpLabel.Text = "Target ID meaning depends on the selected marker or prop.";
			_logicPropDefinitionPathEdit.Text = string.Empty;
			SelectPropDefinitionOptionWithoutRefresh(string.Empty);
			UpdatePropDefinitionPreview(string.Empty);
			_logicNpcDefinitionPathEdit.Text = string.Empty;
			SelectNpcDefinitionOptionWithoutRefresh(string.Empty);
			UpdateNpcDefinitionPreview(string.Empty);
			_logicNpcPortraitOption.Select(0);
			_logicRequiredFlagEdit.Text = string.Empty;
			_logicRequiredFlagEdit.PlaceholderText = string.Empty;
			_logicRequiredFlagHelpLabel.Text = "Required Flag gates whether this interaction is available.";
			_logicSetFlagEdit.Text = string.Empty;
			_logicSetFlagEdit.PlaceholderText = string.Empty;
			_logicSetFlagHelpLabel.Text = "Set Flag is awarded when this interaction succeeds.";
			_logicTriggerModeOption.Select(0);
			_logicOneShotCheck.ButtonPressed = false;
			_logicNotesEdit.Text = string.Empty;
			_isUpdatingLogicUi = false;
			UpdateDialogueSelectionContext();
			UpdateContextObjectEditor();
			return;
		}

		Sprite2D item = selectedSprite;
		EnsureTileRuntimeDefaults(item);
		string itemId = GetItemDisplayId(item);
		string logicLabel = item.GetMeta("logic_label", itemId).AsString();
		int column = item.GetMeta("column", 0).AsInt32();
		int row = item.GetMeta("row", 0).AsInt32();
		string logicRole = item.GetMeta("logic_role", isMarker ? "marker" : string.Empty).AsString();

		_logicSelectionLabel.Text = $"Editing {itemId} @ {column},{row}";
		_logicHintLabel.Text = isMarker
			? "These fields save into the layout file and define how the mission should react to this marker."
			: isPlacedProp
				? "Direct props spawn from PropDefinitions at runtime. Use these fields for labels, portraits, and mission state hooks."
				: "Doors can be opened directly. Terminals can target door IDs and operate them from a nearby console.";
		_logicRoleOption.Select(GetLogicRoleIndex(logicRole));
		_logicLabelEdit.Text = logicLabel;
		_logicTargetIdEdit.Text = item.GetMeta("logic_target_id", string.Empty).AsString();
		_logicPropDefinitionPathEdit.Text = item.GetMeta("prop_definition_path", string.Empty).AsString();
		EnsurePropDefinitionOptionSelection(_logicPropDefinitionPathEdit.Text);
		_logicNpcDefinitionPathEdit.Text = item.GetMeta("npc_definition_path", string.Empty).AsString();
		EnsureNpcDefinitionOptionSelection(_logicNpcDefinitionPathEdit.Text);
		UpdateNpcDefinitionPreview(_logicNpcDefinitionPathEdit.Text);
		UpdateTargetIdFieldContext(item, isMarker, isPlacedProp);
		UpdateFlagFieldContext(item, isMarker, isPlacedProp);
		SelectNpcPortraitOption(item.GetMeta("logic_npc_portrait", string.Empty).AsString());
		_logicRequiredFlagEdit.Text = item.GetMeta("logic_required_flag", string.Empty).AsString();
		_logicSetFlagEdit.Text = item.GetMeta("logic_set_flag", string.Empty).AsString();
		_logicTriggerModeOption.Select(GetTriggerModeIndex(item.GetMeta("logic_trigger_mode", "none").AsString()));
		_logicOneShotCheck.ButtonPressed = item.GetMeta("logic_once", false).AsBool();
		_logicNotesEdit.Text = item.GetMeta("logic_notes", string.Empty).AsString();
		_logicRoleOption.Disabled = isPlacedProp;
		_isUpdatingLogicUi = false;
		UpdateDialogueSelectionContext();
		RefreshValidationReport();
		UpdateContextObjectEditor();
	}

	private void SetLogicEditorEnabled(bool enabled)
	{
		_logicRoleOption.Disabled = !enabled;
		_logicLabelEdit.Editable = enabled;
		_logicTargetIdEdit.Editable = enabled;
		_logicPropDefinitionPathEdit.Editable = enabled;
		_logicPropDefinitionOption.Disabled = !enabled;
		_logicPropDefinitionRefreshButton.Disabled = !enabled;
		_logicNpcDefinitionPathEdit.Editable = enabled;
		_logicNpcDefinitionOption.Disabled = !enabled;
		_logicNpcDefinitionRefreshButton.Disabled = !enabled;
		_logicNpcPortraitOption.Disabled = !enabled;
		_logicRequiredFlagEdit.Editable = enabled;
		_logicSetFlagEdit.Editable = enabled;
		_logicTriggerModeOption.Disabled = !enabled;
		_logicOneShotCheck.Disabled = !enabled;
		_logicNotesEdit.Editable = enabled;
	}

	private void ApplyLogicFieldChanges()
	{
		if (_isUpdatingLogicUi || !TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			return;
		}

		bool isMarker = !string.IsNullOrEmpty(selectedSprite.GetMeta("marker_id", "").AsString());
		bool isLogicProp = IsLogicCapableSprite(selectedSprite);
		bool isPlacedProp = IsPlacedPropSprite(selectedSprite);
		if (!isMarker && !isLogicProp && !isPlacedProp)
		{
			return;
		}

		selectedSprite.SetMeta("logic_role", isPlacedProp ? "prop" : GetLogicRoleValue(_logicRoleOption.Selected, isMarker));
		selectedSprite.SetMeta("logic_label", _logicLabelEdit.Text.StripEdges());
		selectedSprite.SetMeta("logic_target_id", _logicTargetIdEdit.Text.StripEdges());
		selectedSprite.SetMeta("prop_definition_path", _logicPropDefinitionPathEdit.Text.StripEdges());
		selectedSprite.SetMeta("npc_definition_path", _logicNpcDefinitionPathEdit.Text.StripEdges());
		selectedSprite.SetMeta("logic_npc_portrait", _logicNpcPortraitOption.GetItemMetadata(_logicNpcPortraitOption.Selected).AsString());
		selectedSprite.SetMeta("logic_required_flag", _logicRequiredFlagEdit.Text.StripEdges());
		selectedSprite.SetMeta("logic_set_flag", _logicSetFlagEdit.Text.StripEdges());
		selectedSprite.SetMeta("logic_trigger_mode", GetTriggerModeValue(_logicTriggerModeOption.Selected));
		selectedSprite.SetMeta("logic_once", _logicOneShotCheck.ButtonPressed);
		selectedSprite.SetMeta("logic_notes", _logicNotesEdit.Text.StripEdges());
		EnsureTileRuntimeDefaults(selectedSprite);
		UpdateMarkerCaption(selectedSprite);
		_isUpdatingLogicUi = true;
		_logicRoleOption.Select(GetLogicRoleIndex(selectedSprite.GetMeta("logic_role", isMarker ? "marker" : string.Empty).AsString()));
		_logicTargetIdEdit.Text = selectedSprite.GetMeta("logic_target_id", string.Empty).AsString();
		_logicTriggerModeOption.Select(GetTriggerModeIndex(selectedSprite.GetMeta("logic_trigger_mode", "none").AsString()));
		_isUpdatingLogicUi = false;
		RefreshValidationReport();
		UpdateContextObjectEditor();
	}

	private void OnPropDefinitionPathChanged(string newText)
	{
		if (_isUpdatingLogicUi)
		{
			return;
		}

		RefreshPropDefinitionOptions(newText);
		UpdateTargetIdFieldContextForCurrentSelection();
		UpdateFlagFieldContextForCurrentSelection();
	}

	private void OnNpcDefinitionPathChanged(string newText)
	{
		if (_isUpdatingLogicUi)
		{
			return;
		}

		RefreshNpcDefinitionOptions(newText);
		UpdateNpcDefinitionPreview(newText);
		UpdateTargetIdFieldContextForCurrentSelection();
	}

	private void OnPropDefinitionOptionSelected(long selectedIndex)
	{
		if (_isUpdatingLogicUi || _logicPropDefinitionOption == null)
		{
			return;
		}

		string selectedPath = _logicPropDefinitionOption.GetItemMetadata((int)selectedIndex).AsString();
		_isUpdatingLogicUi = true;
		_logicPropDefinitionPathEdit.Text = selectedPath;
		_isUpdatingLogicUi = false;
		UpdatePropDefinitionPreview(selectedPath);
		ApplyLogicFieldChanges();
	}

	private void OnNpcDefinitionOptionSelected(long selectedIndex)
	{
		if (_isUpdatingLogicUi || _logicNpcDefinitionOption == null)
		{
			return;
		}

		string selectedPath = _logicNpcDefinitionOption.GetItemMetadata((int)selectedIndex).AsString();
		_isUpdatingLogicUi = true;
		_logicNpcDefinitionPathEdit.Text = selectedPath;
		_isUpdatingLogicUi = false;
		UpdateNpcDefinitionPreview(selectedPath);
		ApplyLogicFieldChanges();
	}

	private void RefreshPropDefinitionOptions()
	{
		_propDefinitionPreviewCache.Clear();
		RefreshPropDefinitionOptions(_logicPropDefinitionPathEdit?.Text ?? string.Empty);
	}

	private void RefreshNpcDefinitionOptions()
	{
		_npcDefinitionPreviewCache.Clear();
		RefreshNpcDefinitionOptions(_logicNpcDefinitionPathEdit?.Text ?? string.Empty);
	}

	private void RefreshPropDefinitionOptions(string selectedPath)
	{
		if (_logicPropDefinitionOption == null)
		{
			return;
		}

		string normalizedPath = selectedPath?.StripEdges() ?? string.Empty;
		List<string> propDefinitionPaths = GetAvailablePropDefinitionPaths();

		_logicPropDefinitionOption.Clear();
		_logicPropDefinitionOption.AddItem("None", 0);
		_logicPropDefinitionOption.SetItemMetadata(0, string.Empty);

		int selectedIndex = 0;
		for (int i = 0; i < propDefinitionPaths.Count; i++)
		{
			string path = propDefinitionPaths[i];
			PropDefinitionPreview preview = GetPropDefinitionPreview(path);
			int itemIndex = i + 1;
			_logicPropDefinitionOption.AddItem(preview.DisplayName, itemIndex);
			_logicPropDefinitionOption.SetItemMetadata(itemIndex, path);
			if (preview.Icon != null)
			{
				_logicPropDefinitionOption.SetItemIcon(itemIndex, preview.Icon);
			}
			if (path == normalizedPath)
			{
				selectedIndex = itemIndex;
			}
		}

		if (!string.IsNullOrEmpty(normalizedPath) && selectedIndex == 0)
		{
			PropDefinitionPreview preview = GetPropDefinitionPreview(normalizedPath);
			selectedIndex = _logicPropDefinitionOption.ItemCount;
			_logicPropDefinitionOption.AddItem($"Custom: {preview.DisplayName}", selectedIndex);
			_logicPropDefinitionOption.SetItemMetadata(selectedIndex, normalizedPath);
			if (preview.Icon != null)
			{
				_logicPropDefinitionOption.SetItemIcon(selectedIndex, preview.Icon);
			}
		}

		_logicPropDefinitionOption.Select(selectedIndex);
		UpdatePropDefinitionPreview(normalizedPath);
	}

	private void RefreshNpcDefinitionOptions(string selectedPath)
	{
		if (_logicNpcDefinitionOption == null)
		{
			return;
		}

		string normalizedPath = selectedPath?.StripEdges() ?? string.Empty;
		List<string> npcDefinitionPaths = GetAvailableNpcDefinitionPaths();

		_logicNpcDefinitionOption.Clear();
		_logicNpcDefinitionOption.AddItem("None", 0);
		_logicNpcDefinitionOption.SetItemMetadata(0, string.Empty);

		int selectedIndex = 0;
		for (int i = 0; i < npcDefinitionPaths.Count; i++)
		{
			string path = npcDefinitionPaths[i];
			NpcDefinitionPreview preview = GetNpcDefinitionPreview(path);
			int itemIndex = i + 1;
			_logicNpcDefinitionOption.AddItem(preview.IsHostile ? $"[HOSTILE] {preview.DisplayName}" : preview.DisplayName, itemIndex);
			_logicNpcDefinitionOption.SetItemMetadata(itemIndex, path);
			if (preview.Icon != null)
			{
				_logicNpcDefinitionOption.SetItemIcon(itemIndex, preview.Icon);
			}
			if (path == normalizedPath)
			{
				selectedIndex = itemIndex;
			}
		}

		if (!string.IsNullOrEmpty(normalizedPath) && selectedIndex == 0)
		{
			NpcDefinitionPreview preview = GetNpcDefinitionPreview(normalizedPath);
			selectedIndex = _logicNpcDefinitionOption.ItemCount;
			_logicNpcDefinitionOption.AddItem($"Custom: {(preview.IsHostile ? "[HOSTILE] " : string.Empty)}{preview.DisplayName}", selectedIndex);
			_logicNpcDefinitionOption.SetItemMetadata(selectedIndex, normalizedPath);
			if (preview.Icon != null)
			{
				_logicNpcDefinitionOption.SetItemIcon(selectedIndex, preview.Icon);
			}
		}

		_logicNpcDefinitionOption.Select(selectedIndex);
		UpdateNpcDefinitionPreview(normalizedPath);
	}

	private void EnsurePropDefinitionOptionSelection(string selectedPath)
	{
		if (_logicPropDefinitionOption == null)
		{
			return;
		}

		string normalizedPath = selectedPath?.StripEdges() ?? string.Empty;
		for (int i = 0; i < _logicPropDefinitionOption.ItemCount; i++)
		{
			if (_logicPropDefinitionOption.GetItemMetadata(i).AsString() != normalizedPath)
			{
				continue;
			}

			_logicPropDefinitionOption.Select(i);
			UpdatePropDefinitionPreview(normalizedPath);
			return;
		}

		RefreshPropDefinitionOptions(normalizedPath);
	}

	private void EnsureNpcDefinitionOptionSelection(string selectedPath)
	{
		if (_logicNpcDefinitionOption == null)
		{
			return;
		}

		string normalizedPath = selectedPath?.StripEdges() ?? string.Empty;
		for (int i = 0; i < _logicNpcDefinitionOption.ItemCount; i++)
		{
			if (_logicNpcDefinitionOption.GetItemMetadata(i).AsString() != normalizedPath)
			{
				continue;
			}

			_logicNpcDefinitionOption.Select(i);
			return;
		}

		RefreshNpcDefinitionOptions(normalizedPath);
	}

	private void SelectPropDefinitionOptionWithoutRefresh(string selectedPath)
	{
		if (_logicPropDefinitionOption == null)
		{
			return;
		}

		string normalizedPath = selectedPath?.StripEdges() ?? string.Empty;
		for (int i = 0; i < _logicPropDefinitionOption.ItemCount; i++)
		{
			if (_logicPropDefinitionOption.GetItemMetadata(i).AsString() != normalizedPath)
			{
				continue;
			}

			_logicPropDefinitionOption.Select(i);
			return;
		}

		if (_logicPropDefinitionOption.ItemCount > 0)
		{
			_logicPropDefinitionOption.Select(0);
		}
	}

	private void SelectNpcDefinitionOptionWithoutRefresh(string selectedPath)
	{
		if (_logicNpcDefinitionOption == null)
		{
			return;
		}

		string normalizedPath = selectedPath?.StripEdges() ?? string.Empty;
		for (int i = 0; i < _logicNpcDefinitionOption.ItemCount; i++)
		{
			if (_logicNpcDefinitionOption.GetItemMetadata(i).AsString() != normalizedPath)
			{
				continue;
			}

			_logicNpcDefinitionOption.Select(i);
			return;
		}

		if (_logicNpcDefinitionOption.ItemCount > 0)
		{
			_logicNpcDefinitionOption.Select(0);
		}
	}

	private void UpdateTargetIdFieldContextForCurrentSelection()
	{
		if (!TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			_logicTargetIdEdit.PlaceholderText = string.Empty;
			if (_logicTargetIdHelpLabel != null)
			{
				_logicTargetIdHelpLabel.Text = "Target ID meaning depends on the selected marker or prop.";
			}
			return;
		}

		bool isMarker = !string.IsNullOrEmpty(selectedSprite.GetMeta("marker_id", string.Empty).AsString());
		bool isPlacedProp = IsPlacedPropSprite(selectedSprite);
		UpdateTargetIdFieldContext(selectedSprite, isMarker, isPlacedProp);
	}

	private void UpdateFlagFieldContextForCurrentSelection()
	{
		if (!TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			_logicRequiredFlagEdit.PlaceholderText = string.Empty;
			_logicSetFlagEdit.PlaceholderText = string.Empty;
			if (_logicRequiredFlagHelpLabel != null)
			{
				_logicRequiredFlagHelpLabel.Text = "Required Flag gates whether this interaction is available.";
			}
			if (_logicSetFlagHelpLabel != null)
			{
				_logicSetFlagHelpLabel.Text = "Set Flag is awarded when this interaction succeeds.";
			}
			return;
		}

		bool isMarker = !string.IsNullOrEmpty(selectedSprite.GetMeta("marker_id", string.Empty).AsString());
		bool isPlacedProp = IsPlacedPropSprite(selectedSprite);
		UpdateFlagFieldContext(selectedSprite, isMarker, isPlacedProp);
	}

	private void UpdateTargetIdFieldContext(Sprite2D item, bool isMarker, bool isPlacedProp)
	{
		if (item == null || _logicTargetIdEdit == null || _logicTargetIdHelpLabel == null)
		{
			return;
		}

		string placeholderText = string.Empty;
		string helpText = "Target ID meaning depends on the selected marker or prop.";
		string markerId = item.GetMeta("marker_id", string.Empty).AsString();
		string logicRole = item.GetMeta("logic_role", string.Empty).AsString();
		PropDefinition definition = ResolveTargetIdContextDefinition(item, isPlacedProp);

		if (isMarker)
		{
			if (markerId == "npc_spawn")
			{
				placeholderText = "npc_broker_veil";
				helpText = "NPC spawn markers use Target ID as a stable spawn key. Pair them with an NPC Definition Path to spawn a named mission character.";
			}
			else if (markerId == "hostile_spawn")
			{
				placeholderText = "hostile_raider_alpha";
				helpText = "Hostile spawn markers use Target ID as a stable enemy spawn key. Pair them with a hostile NPC Definition Path to place a combat encounter.";
			}
			else if (markerId == "trigger_dialogue")
			{
				placeholderText = "Dialogue ID";
				helpText = "Dialogue trigger markers expect a dialogue id like `trigger_dialogue` or `smuggler_exchange_dialogue`.";
			}
			else if (markerId.StartsWith("trigger_"))
			{
				placeholderText = "Trigger target key";
				helpText = "Trigger markers use Target ID as a mission-specific routing key, such as a dialogue id or custom event id.";
			}
			else if (markerId.StartsWith("spawn_"))
			{
				placeholderText = "Spawn marker key";
				helpText = "Spawn markers usually keep their built-in ids. Change this only if another system needs a custom spawn key.";
			}
			else
			{
				placeholderText = "Objective or marker key";
				helpText = "Use Target ID to give this marker a stable mission-facing identifier.";
			}
		}
		else if (definition != null)
		{
			switch (definition.InteractionType)
			{
				case PropInteractionType.Dialogue:
					placeholderText = "Dialogue ID";
					helpText = "Dialogue props open the dialogue id in Target ID. Leave it blank to fall back to the prop definition or mission template default.";
					break;
				case PropInteractionType.DoorControl:
					placeholderText = "door_alpha,door_beta";
					helpText = "Door-control props accept one or more door ids, separated by commas, semicolons, pipes, or new lines.";
					break;
				case PropInteractionType.Loot:
					placeholderText = "relay_archive_cache";
					helpText = "Loot props use Target ID as an optional loot preset id, such as `relay_archive_cache` or `smuggler_contraband_cache`.";
					break;
				default:
					placeholderText = "Prop-specific target id";
					helpText = "This prop can interpret Target ID in a custom way at runtime.";
					break;
			}
		}
		else if (logicRole == "door")
		{
			placeholderText = "door_bulkhead_a";
			helpText = "Door tiles use Target ID as their door id. Terminals and other props can reference this id later.";
		}
		else if (logicRole == "terminal")
		{
			placeholderText = "door_bulkhead_a";
			helpText = "Terminal tiles usually point at one linked door id unless a Prop Definition overrides that behavior.";
		}

		_logicTargetIdEdit.PlaceholderText = placeholderText;
		_logicTargetIdHelpLabel.Text = helpText;
	}

	private void UpdateFlagFieldContext(Sprite2D item, bool isMarker, bool isPlacedProp)
	{
		if (item == null || _logicRequiredFlagEdit == null || _logicSetFlagEdit == null || _logicRequiredFlagHelpLabel == null || _logicSetFlagHelpLabel == null)
		{
			return;
		}

		string requiredPlaceholder = string.Empty;
		string setPlaceholder = string.Empty;
		string requiredHelp = "Required Flag gates whether this interaction is available.";
		string setHelp = "Set Flag is awarded when this interaction succeeds.";
		string markerId = item.GetMeta("marker_id", string.Empty).AsString();
		string logicRole = item.GetMeta("logic_role", string.Empty).AsString();
		PropDefinition definition = ResolveTargetIdContextDefinition(item, isPlacedProp);

		if (isMarker)
		{
			if (markerId == "npc_spawn")
			{
				requiredPlaceholder = "broker_contact_unlocked";
				setPlaceholder = string.Empty;
				requiredHelp = "NPC spawn markers can require a story flag if the character should only appear after a certain mission phase.";
				setHelp = "NPC spawn markers usually do not set flags themselves; the spawned NPC interaction should own that.";
			}
			else if (markerId == "hostile_spawn")
			{
				requiredPlaceholder = "alert_state_triggered";
				setPlaceholder = string.Empty;
				requiredHelp = "Hostile spawn markers can require a flag if enemies should only appear after an alarm, breach, or story escalation.";
				setHelp = "Hostile spawn markers usually do not set flags themselves; combat outcomes or props should own those state changes.";
			}
			else if (markerId.StartsWith("trigger_"))
			{
				requiredPlaceholder = "relay_access_granted";
				setPlaceholder = "relay_dialogue_seen";
				requiredHelp = "Required Flag can lock this trigger until earlier mission progress or exploration has happened.";
				setHelp = "Set Flag is useful for one-shot story beats, follow-up triggers, or unlocking downstream props.";
			}
			else if (markerId.StartsWith("spawn_"))
			{
				requiredPlaceholder = string.Empty;
				setPlaceholder = string.Empty;
				requiredHelp = "Spawn markers usually do not need flags unless a special mission script wants alternate insertion rules.";
				setHelp = "Spawn markers rarely set flags by themselves.";
			}
			else
			{
				requiredPlaceholder = "objective_unlocked";
				setPlaceholder = "objective_completed";
				requiredHelp = "Objective markers can use Required Flag to hide or defer optional content until the right mission phase.";
				setHelp = "Set Flag works well for tracking completed objectives or branching outcomes.";
			}
		}
		else if (definition != null)
		{
			switch (definition.InteractionType)
			{
				case PropInteractionType.Dialogue:
					requiredPlaceholder = "console_powered";
					setPlaceholder = "console_logs_read";
					requiredHelp = "Dialogue props often gate access behind a prior event, power restore, or officer-side discovery flag.";
					setHelp = "Set Flag is great for remembering that this conversation or intel pickup has already been seen.";
					break;
				case PropInteractionType.DoorControl:
					requiredPlaceholder = "bulkhead_access";
					setPlaceholder = "bulkhead_rerouted";
					requiredHelp = "Door-control props can require an access flag before officers are allowed to reroute doors.";
					setHelp = "Set Flag can mark the door network as rerouted for later props, encounters, or mission outcomes.";
					break;
				case PropInteractionType.Loot:
					requiredPlaceholder = "cache_revealed";
					setPlaceholder = "cache_opened";
					requiredHelp = "Loot props often use Required Flag to make the cache appear locked until the crew finds a clue or key.";
					setHelp = "Set Flag is the clean way to prevent duplicate rewards or unlock a follow-up encounter after looting.";
					break;
				default:
					requiredPlaceholder = "interaction_unlocked";
					setPlaceholder = "interaction_completed";
					requiredHelp = "Use Required Flag when this prop should only activate after another mission event or prop chain completes.";
					setHelp = "Set Flag lets this prop feed the next stage of your mission logic.";
					break;
			}
		}
		else if (logicRole == "door")
		{
			requiredPlaceholder = "bulkhead_access";
			setPlaceholder = "bulkhead_opened";
			requiredHelp = "Door tiles can use Required Flag if direct interaction should be locked until an access condition is met.";
			setHelp = "Set Flag can mark that the player opened this route, though terminals usually own that flow.";
		}
		else if (logicRole == "terminal")
		{
			requiredPlaceholder = "terminal_powered";
			setPlaceholder = "terminal_used";
			requiredHelp = "Terminal tiles commonly use Required Flag to represent powered systems, credentials, or puzzle prerequisites.";
			setHelp = "Set Flag can chain this terminal into other interactions like doors, loot rooms, or story reveals.";
		}

		_logicRequiredFlagEdit.PlaceholderText = requiredPlaceholder;
		_logicSetFlagEdit.PlaceholderText = setPlaceholder;
		_logicRequiredFlagHelpLabel.Text = requiredHelp;
		_logicSetFlagHelpLabel.Text = setHelp;
	}

	private PropDefinition ResolveTargetIdContextDefinition(Sprite2D item, bool isPlacedProp)
	{
		if (item == null)
		{
			return null;
		}

		string propDefinitionPath = item.GetMeta("prop_definition_path", string.Empty).AsString();
		if (!string.IsNullOrWhiteSpace(propDefinitionPath) && ResourceLoader.Exists(propDefinitionPath))
		{
			return GD.Load<PropDefinition>(propDefinitionPath);
		}

		if (isPlacedProp)
		{
			return null;
		}

		string tileId = item.GetMeta("tile_id", string.Empty).AsString();
		string defaultPropPath = GetDefaultPropDefinitionPath(tileId);
		if (!string.IsNullOrWhiteSpace(defaultPropPath) && ResourceLoader.Exists(defaultPropPath))
		{
			return GD.Load<PropDefinition>(defaultPropPath);
		}

		return null;
	}

	private Sprite2D CreateSprite(MissionTileDefinition definition, int column, int row)
	{
		bool usesAtlasRegion = string.IsNullOrEmpty(definition.TexturePath) && definition.Category != MissionTileCategory.Floor;
		Sprite2D sprite = new Sprite2D
		{
			Texture = GetTileTexture(definition),
			RegionEnabled = usesAtlasRegion,
			RegionRect = definition.Region,
			Scale = definition.Scale,
			Position = IsoGridHelper.GridToWorld(column, row, _tileStep, _gridOrigin) + definition.Offset
		};
		sprite.SetMeta("tile_id", definition.Id);
		sprite.SetMeta("item_type", "tile");
		sprite.SetMeta("layer", GetLayerForTile(definition).ToString().ToLower());
		sprite.SetMeta("column", column);
		sprite.SetMeta("row", row);
		sprite.SetMeta("offset_x", 0f);
		sprite.SetMeta("offset_y", 0f);
		sprite.SetMeta("rotation_degrees", 0f);
		sprite.SetMeta("flip_h", false);
		sprite.SetMeta("flip_v", false);
		sprite.SetMeta("base_modulate", Colors.White);
		ApplyDefaultTileLogic(sprite, definition, column, row);
		sprite.AddChild(CreateSelectionOutline(GetSpriteBoundsSize(sprite)));
		return sprite;
	}

	private Sprite2D CreatePlacedPropSprite(string propDefinitionPath, int column, int row)
	{
		PropDefinitionPreview preview = GetPropDefinitionPreview(propDefinitionPath);
		Texture2D texture = preview.Icon ?? GetFallbackPropPreviewTexture();
		Sprite2D sprite = new Sprite2D
		{
			Texture = texture,
			Position = IsoGridHelper.GridToWorld(column, row, _tileStep, _gridOrigin),
			Scale = GetPlacedPropPreviewScale(texture, preview.VisualScaleMultiplier)
		};
		sprite.SetMeta("tile_id", string.Empty);
		sprite.SetMeta("item_type", "placed_prop");
		sprite.SetMeta("layer", "prop");
		sprite.SetMeta("column", column);
		sprite.SetMeta("row", row);
		sprite.SetMeta("offset_x", 0f);
		sprite.SetMeta("offset_y", 0f);
		sprite.SetMeta("rotation_degrees", 0f);
		sprite.SetMeta("flip_h", false);
		sprite.SetMeta("flip_v", false);
		sprite.SetMeta("base_modulate", preview.Exists ? Colors.White : new Color(1f, 0.76f, 0.76f, 1f));
		ApplyDefaultPlacedPropLogic(sprite, preview, propDefinitionPath);
		sprite.AddChild(CreateSelectionOutline(GetSpriteBoundsSize(sprite)));
		return sprite;
	}

	private void ApplyDefaultTileLogic(Sprite2D sprite, MissionTileDefinition definition, int column, int row)
	{
		string defaultRole = string.Empty;
		string defaultTargetId = string.Empty;
		string defaultTriggerMode = "none";
		if (IsDoorTileId(definition.Id))
		{
			defaultRole = "door";
			defaultTargetId = $"{definition.Id}_{column}_{row}";
			defaultTriggerMode = "interact";
		}
		else if (definition.Id == "medical_station")
		{
			defaultRole = "prop";
			defaultTriggerMode = "interact";
		}
		else if (IsTerminalTileId(definition.Id))
		{
			defaultRole = "terminal";
			defaultTriggerMode = "interact";
		}

		sprite.SetMeta("logic_role", defaultRole);
		sprite.SetMeta("logic_label", definition.DisplayName);
		sprite.SetMeta("logic_target_id", defaultTargetId);
		sprite.SetMeta("logic_npc_portrait", string.Empty);
		sprite.SetMeta("logic_required_flag", string.Empty);
		sprite.SetMeta("logic_set_flag", string.Empty);
		sprite.SetMeta("logic_trigger_mode", defaultTriggerMode);
		sprite.SetMeta("logic_once", false);
		sprite.SetMeta("logic_notes", string.Empty);
		sprite.SetMeta("prop_definition_path", GetDefaultPropDefinitionPath(definition.Id));
	}

	private Sprite2D CreateMarker(MissionMarkerDefinition definition, int column, int row)
	{
		Sprite2D sprite = new Sprite2D
		{
			Texture = GetMarkerIconTexture(definition),
			Position = IsoGridHelper.GridToWorld(column, row, _tileStep, _gridOrigin) + definition.Offset,
			Scale = new Vector2(0.82f, 0.82f),
			Modulate = definition.Color
		};
		sprite.SetMeta("item_type", "marker");
		sprite.SetMeta("marker_id", definition.Id);
		sprite.SetMeta("layer", "marker");
		sprite.SetMeta("npc_definition_path", string.Empty);
		sprite.SetMeta("column", column);
		sprite.SetMeta("row", row);
		sprite.SetMeta("offset_x", 0f);
		sprite.SetMeta("offset_y", 0f);
		sprite.SetMeta("rotation_degrees", 0f);
		sprite.SetMeta("flip_h", false);
		sprite.SetMeta("flip_v", false);
		sprite.SetMeta("base_modulate", definition.Color);
		ApplyDefaultMarkerLogic(sprite, definition);
		sprite.AddChild(CreateSelectionOutline(GetSpriteBoundsSize(sprite)));

		Label label = new Label
		{
			Name = "MarkerCaption",
			Text = definition.DisplayName,
			Position = new Vector2(-70f, 38f),
			Size = new Vector2(140f, 22f),
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		sprite.AddChild(label);
		UpdateMarkerCaption(sprite);
		return sprite;
	}

	private Sprite2D CreateHostileSpawnMarker(string npcDefinitionPath, int column, int row)
	{
		if (!MissionMarkerCatalog.TryGetById("hostile_spawn", out MissionMarkerDefinition markerDefinition))
		{
			return CreateMarker(new MissionMarkerDefinition("hostile_spawn", "Hostile Spawn", MissionMarkerCategory.Spawn, new Color(1f, 0.2f, 0.18f, 0.92f), Vector2.Zero), column, row);
		}

		Sprite2D sprite = CreateMarker(markerDefinition, column, row);
		NpcDefinitionPreview preview = GetNpcDefinitionPreview(npcDefinitionPath);
		string spawnKeyBase = string.IsNullOrWhiteSpace(preview.DisplayName)
			? "hostile_spawn"
			: preview.DisplayName.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
		sprite.SetMeta("npc_definition_path", npcDefinitionPath);
		sprite.SetMeta("logic_label", $"Hostile Spawn: {preview.DisplayName}");
		sprite.SetMeta("logic_target_id", $"{spawnKeyBase}_{column}_{row}");
		sprite.SetMeta("logic_notes", string.IsNullOrWhiteSpace(preview.Description) ? "Hostile combat spawn." : preview.Description);
		UpdateMarkerCaption(sprite);
		return sprite;
	}

	private void ApplyDefaultMarkerLogic(Sprite2D sprite, MissionMarkerDefinition definition)
	{
		sprite.SetMeta("logic_label", definition.DisplayName);
		sprite.SetMeta("logic_target_id", definition.Id == "npc_spawn" || definition.Id == "hostile_spawn" ? string.Empty : definition.Id);
		sprite.SetMeta("logic_npc_portrait", string.Empty);
		sprite.SetMeta("logic_required_flag", string.Empty);
		sprite.SetMeta("logic_set_flag", string.Empty);
		sprite.SetMeta("logic_trigger_mode", definition.Category == MissionMarkerCategory.Trigger ? "enter" : "none");
		sprite.SetMeta("logic_once", definition.Category == MissionMarkerCategory.Trigger);
		sprite.SetMeta("logic_notes", string.Empty);
		sprite.SetMeta("prop_definition_path", GetDefaultPropDefinitionPath(definition.Id));
		sprite.SetMeta("npc_definition_path", GetDefaultNpcDefinitionPath(definition.Id));
	}

	private void ApplyDefaultPlacedPropLogic(Sprite2D sprite, PropDefinitionPreview preview, string propDefinitionPath)
	{
		sprite.SetMeta("logic_role", "prop");
		sprite.SetMeta("logic_label", preview.DisplayName);
		sprite.SetMeta("logic_target_id", string.Empty);
		sprite.SetMeta("logic_npc_portrait", string.Empty);
		sprite.SetMeta("logic_required_flag", string.Empty);
		sprite.SetMeta("logic_set_flag", string.Empty);
		sprite.SetMeta("logic_trigger_mode", "interact");
		sprite.SetMeta("logic_once", false);
		sprite.SetMeta("logic_notes", preview.Description);
		sprite.SetMeta("prop_definition_path", propDefinitionPath);
		ApplyMissionSpecificPlacedPropDefaults(sprite, propDefinitionPath);
	}

	private void ApplyMissionSpecificPlacedPropDefaults(Sprite2D sprite, string propDefinitionPath)
	{
		if (sprite == null || string.IsNullOrWhiteSpace(propDefinitionPath) || !ResourceLoader.Exists(propDefinitionPath))
		{
			return;
		}

		MissionTemplate missionTemplate = GetMissionTemplateForCurrentLayout();
		if (missionTemplate == null)
		{
			return;
		}

		PropDefinition definition = GD.Load<PropDefinition>(propDefinitionPath);
		if (definition == null)
		{
			return;
		}

		string suggestedTargetId = PropPlacementOverrides.GetSuggestedTargetId(missionTemplate, definition);
		if (!string.IsNullOrWhiteSpace(suggestedTargetId))
		{
			sprite.SetMeta("logic_target_id", suggestedTargetId);
		}

		string suggestedNotes = PropPlacementOverrides.GetSuggestedNotes(missionTemplate, definition);
		if (!string.IsNullOrWhiteSpace(suggestedNotes))
		{
			sprite.SetMeta("logic_notes", suggestedNotes);
		}
	}

	private MissionTemplate GetMissionTemplateForCurrentLayout()
	{
		string currentLayoutPath = GetCurrentLayoutResourcePath();
		if (string.IsNullOrWhiteSpace(currentLayoutPath))
		{
			return null;
		}

		const string missionTemplateDirectory = "res://Data/Missions/Templates";
		foreach (string file in DirAccess.GetFilesAt(missionTemplateDirectory).Where(name => name.EndsWith(".tres") || name.EndsWith(".res")))
		{
			string resourcePath = $"{missionTemplateDirectory}/{file}";
			MissionTemplate template = GD.Load<MissionTemplate>(resourcePath);
			if (template != null && string.Equals(template.LayoutResourcePath, currentLayoutPath, System.StringComparison.OrdinalIgnoreCase))
			{
				return template;
			}
		}

		return null;
	}

	private string GetCurrentLayoutResourcePath()
	{
		return GetLayoutResourcePath(GetCurrentLayoutName());
	}

	private void SetCurrentLayoutName(string layoutName)
	{
		_currentLayoutName = string.IsNullOrWhiteSpace(layoutName) ? DefaultLayoutName : layoutName.StripEdges();
		if (_layoutNameEdit != null)
		{
			_layoutNameEdit.Text = _currentLayoutName;
		}
	}

	private string GetCurrentLayoutName()
	{
		if (string.IsNullOrWhiteSpace(_currentLayoutName))
		{
			_currentLayoutName = DefaultLayoutName;
		}

		return _currentLayoutName;
	}

	private string GetLayoutResourcePath(string layoutName)
	{
		return $"{LayoutDirectoryResourcePath}/{layoutName}.json";
	}

	private string GetLayoutAbsolutePath(string layoutName)
	{
		return ProjectSettings.GlobalizePath(GetLayoutResourcePath(layoutName));
	}

	private string GetLayoutDirectoryAbsolutePath()
	{
		return ProjectSettings.GlobalizePath(LayoutDirectoryResourcePath);
	}

	private bool TryNormalizeLayoutName(string rawName, out string normalizedName, out string errorMessage)
	{
		normalizedName = string.Empty;
		errorMessage = string.Empty;

		string trimmed = rawName?.StripEdges() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(trimmed))
		{
			errorMessage = "Enter a mission name first.";
			return false;
		}

		char[] sanitizedChars = trimmed
			.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : (ch == ' ' || ch == '-' ? '_' : ch))
			.Where(ch => char.IsLetterOrDigit(ch) || ch == '_')
			.ToArray();
		normalizedName = new string(sanitizedChars).Trim('_');

		if (string.IsNullOrWhiteSpace(normalizedName))
		{
			errorMessage = "Mission names need at least one letter or number.";
			return false;
		}

		return true;
	}

	private List<string> GetAvailableLayoutNames()
	{
		List<string> layoutNames = new List<string>();
		DirAccess dir = DirAccess.Open(LayoutDirectoryResourcePath);
		if (dir == null)
		{
			return layoutNames;
		}

		dir.ListDirBegin();
		while (true)
		{
			string entryName = dir.GetNext();
			if (string.IsNullOrEmpty(entryName))
			{
				break;
			}

			if (dir.CurrentIsDir() || !entryName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			layoutNames.Add(entryName[..^5]);
		}

		dir.ListDirEnd();
		layoutNames.Sort(StringComparer.OrdinalIgnoreCase);
		return layoutNames;
	}

	private void ShowLoadLayoutDialog()
	{
		if (_loadLayoutDialog == null || _loadLayoutList == null)
		{
			return;
		}

		List<string> layoutNames = GetAvailableLayoutNames();
		_loadLayoutList.Clear();
		foreach (string layoutName in layoutNames)
		{
			_loadLayoutList.AddItem(layoutName);
		}

		if (layoutNames.Count == 0)
		{
			SetStatus("No mission layouts were found to load.");
			return;
		}

		int selectedIndex = Math.Max(0, layoutNames.FindIndex(name => string.Equals(name, GetCurrentLayoutName(), StringComparison.OrdinalIgnoreCase)));
		_loadLayoutList.Select(selectedIndex);
		_loadLayoutDialog.PopupCentered();
	}

	private void OnLoadLayoutItemActivated(long index)
	{
		if (_loadLayoutList == null || index < 0 || index >= _loadLayoutList.ItemCount)
		{
			return;
		}

		LoadSelectedLayoutFromDialog();
	}

	private void LoadSelectedLayoutFromDialog()
	{
		if (_loadLayoutList == null)
		{
			return;
		}

		int[] selectedItems = _loadLayoutList.GetSelectedItems();
		if (selectedItems.Length == 0)
		{
			SetStatus("Choose a mission layout to load.");
			return;
		}

		string selectedLayoutName = _loadLayoutList.GetItemText(selectedItems[0]);
		SetCurrentLayoutName(selectedLayoutName);
		LoadLayout();
		_loadLayoutDialog?.Hide();
	}

	private void ShowNameMissionDialog()
	{
		if (_nameMissionDialog == null || _nameMissionEdit == null)
		{
			return;
		}

		_nameMissionEdit.Text = GetCurrentLayoutName();
		_nameMissionDialog.PopupCentered();
		_nameMissionEdit.GrabFocus();
		_nameMissionEdit.SelectAll();
	}

	private void ConfirmMissionNameFromDialog()
	{
		if (_nameMissionEdit == null)
		{
			return;
		}

		if (!TryNormalizeLayoutName(_nameMissionEdit.Text, out string layoutName, out string errorMessage))
		{
			SetStatus(errorMessage);
			return;
		}

		SetCurrentLayoutName(layoutName);
		_nameMissionDialog?.Hide();

		string absolutePath = GetLayoutAbsolutePath(layoutName);
		if (FileAccess.FileExists(absolutePath))
		{
			SetStatus($"Mission name set to {layoutName}. That file already exists, so Save Mission will overwrite it.");
			return;
		}

		SetStatus($"Mission name set to {layoutName}. Build or edit the layout, then press Save Mission when you're ready.");
	}

	private void UpdateMarkerCaption(Sprite2D sprite)
	{
		Label label = sprite?.GetNodeOrNull<Label>("MarkerCaption");
		if (label == null)
		{
			return;
		}

		string caption = sprite.GetMeta("logic_label", sprite.GetMeta("marker_id", "Marker").AsString()).AsString();
		label.Text = string.IsNullOrEmpty(caption) ? "Marker" : caption;
	}

	private void SelectNpcPortraitOption(string portraitPath)
	{
		string targetPath = portraitPath ?? string.Empty;
		for (int i = 0; i < _logicNpcPortraitOption.ItemCount; i++)
		{
			if (_logicNpcPortraitOption.GetItemMetadata(i).AsString() != targetPath)
			{
				continue;
			}

			_logicNpcPortraitOption.Select(i);
			return;
		}

		_logicNpcPortraitOption.Select(0);
	}

	private Node2D CreateSelectionOutline(Vector2 size)
	{
		Node2D container = new Node2D
		{
			Name = "SelectionOutline",
			Visible = false
		};

		Vector2 halfSize = size * 0.5f;
		Line2D line = new Line2D
		{
			DefaultColor = new Color(0.45f, 0.92f, 1.0f, 0.98f),
			Width = 3.0f,
			Closed = true,
			Points = new Vector2[]
			{
				new Vector2(-halfSize.X, -halfSize.Y),
				new Vector2(halfSize.X, -halfSize.Y),
				new Vector2(halfSize.X, halfSize.Y),
				new Vector2(-halfSize.X, halfSize.Y)
			}
		};
		container.AddChild(line);

		Line2D inner = new Line2D
		{
			DefaultColor = new Color(0.15f, 0.55f, 0.64f, 0.6f),
			Width = 1.5f,
			Closed = true,
			Points = new Vector2[]
			{
				new Vector2(-halfSize.X + 4f, -halfSize.Y + 4f),
				new Vector2(halfSize.X - 4f, -halfSize.Y + 4f),
				new Vector2(halfSize.X - 4f, halfSize.Y - 4f),
				new Vector2(-halfSize.X + 4f, halfSize.Y - 4f)
			}
		};
		container.AddChild(inner);

		return container;
	}

	private void ToggleSelectionOutline(Sprite2D sprite, bool isVisible)
	{
		if (!IsLiveSprite(sprite))
		{
			return;
		}

		Node2D outline = sprite?.GetNodeOrNull<Node2D>("SelectionOutline");
		if (outline != null)
		{
			outline.Visible = isVisible;
		}
	}

	private void AdjustSelectedTile(Vector2 deltaOffset, float deltaRotationDegrees)
	{
		if (!TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			return;
		}

		float offsetX = selectedSprite.GetMeta("offset_x", 0f).AsSingle() + deltaOffset.X;
		float offsetY = selectedSprite.GetMeta("offset_y", 0f).AsSingle() + deltaOffset.Y;
		float rotationDegrees = selectedSprite.GetMeta("rotation_degrees", 0f).AsSingle() + deltaRotationDegrees;

		selectedSprite.SetMeta("offset_x", offsetX);
		selectedSprite.SetMeta("offset_y", offsetY);
		selectedSprite.SetMeta("rotation_degrees", rotationDegrees);
		selectedSprite.RotationDegrees = rotationDegrees;

		MoveSpriteToCell(
			selectedSprite,
			selectedSprite.GetMeta("column", 0).AsInt32(),
			selectedSprite.GetMeta("row", 0).AsInt32());

		SetStatus($"Adjusted tile: offset ({offsetX:0},{offsetY:0}) rotation {rotationDegrees:0}");
	}

	private void ToggleSelectedPropFlip(bool horizontal)
	{
		if (!TryGetSelectedPlacedSprite(out Sprite2D selectedSprite) || !IsFlippablePropSprite(selectedSprite))
		{
			return;
		}

		string key = horizontal ? "flip_h" : "flip_v";
		bool nextValue = !selectedSprite.GetMeta(key, false).AsBool();
		selectedSprite.SetMeta(key, nextValue);
		ApplySpriteFlipState(selectedSprite);

		string axisLabel = horizontal ? "Y-axis" : "X-axis";
		SetStatus($"Flipped selected prop across the {axisLabel}.");
	}

	private void ResetSelectedTileAdjustment()
	{
		if (!TryGetSelectedPlacedSprite(out Sprite2D selectedSprite))
		{
			return;
		}

		selectedSprite.SetMeta("offset_x", 0f);
		selectedSprite.SetMeta("offset_y", 0f);
		selectedSprite.SetMeta("rotation_degrees", 0f);
		selectedSprite.RotationDegrees = 0f;
		MoveSpriteToCell(
			selectedSprite,
			selectedSprite.GetMeta("column", 0).AsInt32(),
			selectedSprite.GetMeta("row", 0).AsInt32());
		SetStatus("Reset selected tile adjustment.");
	}

	private void SaveLayout()
	{
		string layoutName = GetCurrentLayoutName();
		string path = GetLayoutAbsolutePath(layoutName);
		DirAccess.MakeDirRecursiveAbsolute(GetLayoutDirectoryAbsolutePath());
		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			SetStatus("Could not save layout.");
			return;
		}

		Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>> items = new();
		items.Add(new Godot.Collections.Dictionary<string, Variant>
		{
			{ "item_type", "background" },
			{ "background_id", _selectedBackgroundId }
		});

		foreach (Node2D layer in GetSaveLayers())
		{
			foreach (Node child in layer.GetChildren())
			{
				if (child is not Sprite2D sprite || !IsLiveSprite(sprite))
				{
					continue;
				}

				EnsureTileRuntimeDefaults(sprite);

				Godot.Collections.Dictionary<string, Variant> item = new Godot.Collections.Dictionary<string, Variant>
				{
					{ "item_type", sprite.GetMeta("item_type", "tile").AsString() },
					{ "layer", sprite.GetMeta("layer", "prop").AsString() },
					{ "column", sprite.GetMeta("column", 0).AsInt32() },
					{ "row", sprite.GetMeta("row", 0).AsInt32() },
					{ "offset_x", sprite.GetMeta("offset_x", 0f).AsSingle() },
					{ "offset_y", sprite.GetMeta("offset_y", 0f).AsSingle() },
					{ "rotation_degrees", sprite.GetMeta("rotation_degrees", 0f).AsSingle() },
					{ "flip_h", sprite.GetMeta("flip_h", false).AsBool() },
					{ "flip_v", sprite.GetMeta("flip_v", false).AsBool() }
				};

				string markerId = sprite.GetMeta("marker_id", "").AsString();
				if (!string.IsNullOrEmpty(markerId))
				{
					item["marker_id"] = markerId;
					item["logic_label"] = sprite.GetMeta("logic_label", string.Empty).AsString();
					item["logic_target_id"] = sprite.GetMeta("logic_target_id", string.Empty).AsString();
					item["npc_definition_path"] = sprite.GetMeta("npc_definition_path", string.Empty).AsString();
					item["logic_npc_portrait"] = sprite.GetMeta("logic_npc_portrait", string.Empty).AsString();
					item["logic_required_flag"] = sprite.GetMeta("logic_required_flag", string.Empty).AsString();
					item["logic_set_flag"] = sprite.GetMeta("logic_set_flag", string.Empty).AsString();
					item["logic_trigger_mode"] = sprite.GetMeta("logic_trigger_mode", "none").AsString();
					item["logic_once"] = sprite.GetMeta("logic_once", false).AsBool();
					item["logic_notes"] = sprite.GetMeta("logic_notes", string.Empty).AsString();
					item["prop_definition_path"] = sprite.GetMeta("prop_definition_path", string.Empty).AsString();
				}
				else
				{
					item["tile_id"] = sprite.GetMeta("tile_id", "").AsString();
					item["logic_role"] = sprite.GetMeta("logic_role", string.Empty).AsString();
					item["logic_label"] = sprite.GetMeta("logic_label", string.Empty).AsString();
					item["logic_target_id"] = sprite.GetMeta("logic_target_id", string.Empty).AsString();
					item["npc_definition_path"] = sprite.GetMeta("npc_definition_path", string.Empty).AsString();
					item["logic_npc_portrait"] = sprite.GetMeta("logic_npc_portrait", string.Empty).AsString();
					item["logic_required_flag"] = sprite.GetMeta("logic_required_flag", string.Empty).AsString();
					item["logic_set_flag"] = sprite.GetMeta("logic_set_flag", string.Empty).AsString();
					item["logic_trigger_mode"] = sprite.GetMeta("logic_trigger_mode", "none").AsString();
					item["logic_once"] = sprite.GetMeta("logic_once", false).AsBool();
					item["logic_notes"] = sprite.GetMeta("logic_notes", string.Empty).AsString();
					item["prop_definition_path"] = sprite.GetMeta("prop_definition_path", string.Empty).AsString();
				}

				items.Add(item);
			}
		}

		file.StoreString(Json.Stringify(items, "\t"));
		SetStatus($"Saved layout to {ProjectSettings.LocalizePath(path)}");
		RefreshValidationReport();
	}

	private void LoadLayout()
	{
		string layoutName = GetCurrentLayoutName();
		string path = GetLayoutAbsolutePath(layoutName);
		if (!FileAccess.FileExists(path))
		{
			SetStatus($"No saved layout exists for {layoutName} yet. Start placing tiles or name a different mission.");
			return;
		}

		ClearLayoutInternal(false);
		string loadedBackgroundId = MissionBackgroundCatalog.DefaultId;

		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			SetStatus("Could not open saved layout.");
			return;
		}

		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Array)
		{
			SetStatus("Saved layout format is invalid.");
			return;
		}

		foreach (Variant tileVariant in parsed.AsGodotArray())
		{
			Godot.Collections.Dictionary tile = tileVariant.AsGodotDictionary();
			string itemType = tile.TryGetValue("item_type", out Variant itemTypeVariant) ? itemTypeVariant.AsString() : "tile";
			string markerId = tile.TryGetValue("marker_id", out Variant markerIdVariant) ? markerIdVariant.AsString() : "";
			string tileId = tile.TryGetValue("tile_id", out Variant tileIdVariant) ? tileIdVariant.AsString() : "";
			int column = tile.TryGetValue("column", out Variant columnVariant) ? columnVariant.AsInt32() : 0;
			int row = tile.TryGetValue("row", out Variant rowVariant) ? rowVariant.AsInt32() : 0;
			float offsetX = tile.TryGetValue("offset_x", out Variant offsetXVariant) ? offsetXVariant.AsSingle() : 0f;
			float offsetY = tile.TryGetValue("offset_y", out Variant offsetYVariant) ? offsetYVariant.AsSingle() : 0f;
			float rotationDegrees = tile.TryGetValue("rotation_degrees", out Variant rotationVariant) ? rotationVariant.AsSingle() : 0f;
			bool flipH = tile.TryGetValue("flip_h", out Variant flipHVariant) && flipHVariant.AsBool();
			bool flipV = tile.TryGetValue("flip_v", out Variant flipVVariant) && flipVVariant.AsBool();
			if (itemType == "background")
			{
				loadedBackgroundId = tile.TryGetValue("background_id", out Variant backgroundVariant)
					? backgroundVariant.AsString()
					: MissionBackgroundCatalog.DefaultId;
				continue;
			}

			if (itemType == "marker" || !string.IsNullOrEmpty(markerId))
			{
				if (!MissionMarkerCatalog.TryGetById(markerId, out MissionMarkerDefinition markerDefinition))
				{
					continue;
				}

				Sprite2D marker = CreateMarker(markerDefinition, column, row);
				marker.SetMeta("offset_x", offsetX);
				marker.SetMeta("offset_y", offsetY);
				marker.SetMeta("rotation_degrees", rotationDegrees);
				marker.SetMeta("flip_h", flipH);
				marker.SetMeta("flip_v", flipV);
				marker.SetMeta("logic_label", tile.TryGetValue("logic_label", out Variant logicLabelVariant) ? logicLabelVariant.AsString() : marker.GetMeta("logic_label", markerDefinition.DisplayName).AsString());
				marker.SetMeta("logic_target_id", tile.TryGetValue("logic_target_id", out Variant logicTargetVariant) ? logicTargetVariant.AsString() : marker.GetMeta("logic_target_id", markerDefinition.Id).AsString());
				marker.SetMeta("npc_definition_path", tile.TryGetValue("npc_definition_path", out Variant npcDefinitionVariant) ? npcDefinitionVariant.AsString() : marker.GetMeta("npc_definition_path", string.Empty).AsString());
				marker.SetMeta("logic_npc_portrait", tile.TryGetValue("logic_npc_portrait", out Variant logicPortraitVariant) ? logicPortraitVariant.AsString() : string.Empty);
				marker.SetMeta("logic_required_flag", tile.TryGetValue("logic_required_flag", out Variant logicRequiredVariant) ? logicRequiredVariant.AsString() : string.Empty);
				marker.SetMeta("logic_set_flag", tile.TryGetValue("logic_set_flag", out Variant logicSetVariant) ? logicSetVariant.AsString() : string.Empty);
				marker.SetMeta("logic_trigger_mode", tile.TryGetValue("logic_trigger_mode", out Variant logicTriggerVariant) ? logicTriggerVariant.AsString() : marker.GetMeta("logic_trigger_mode", "none").AsString());
				marker.SetMeta("logic_once", tile.TryGetValue("logic_once", out Variant logicOnceVariant) ? logicOnceVariant.AsBool() : marker.GetMeta("logic_once", false).AsBool());
				marker.SetMeta("logic_notes", tile.TryGetValue("logic_notes", out Variant logicNotesVariant) ? logicNotesVariant.AsString() : string.Empty);
				marker.SetMeta("prop_definition_path", tile.TryGetValue("prop_definition_path", out Variant propDefinitionVariant) ? propDefinitionVariant.AsString() : string.Empty);
				marker.RotationDegrees = rotationDegrees;
				ApplySpriteFlipState(marker);
				MoveSpriteToCell(marker, column, row);
				UpdateMarkerCaption(marker);
				GetPlacementLayer(BuilderLayer.Marker).AddChild(marker);
				continue;
			}

			if (itemType == "placed_prop" || (string.IsNullOrEmpty(tileId) && tile.TryGetValue("prop_definition_path", out Variant placedPropPathVariant) && !string.IsNullOrEmpty(placedPropPathVariant.AsString())))
			{
				string placedPropDefinitionPath = tile.TryGetValue("prop_definition_path", out Variant placedPropDefinitionVariant)
					? placedPropDefinitionVariant.AsString()
					: string.Empty;
				Sprite2D placedPropSprite = CreatePlacedPropSprite(placedPropDefinitionPath, column, row);
				placedPropSprite.SetMeta("offset_x", offsetX);
				placedPropSprite.SetMeta("offset_y", offsetY);
				placedPropSprite.SetMeta("rotation_degrees", rotationDegrees);
				placedPropSprite.SetMeta("flip_h", flipH);
				placedPropSprite.SetMeta("flip_v", flipV);
				placedPropSprite.SetMeta("logic_role", tile.TryGetValue("logic_role", out Variant placedPropLogicRoleVariant) ? placedPropLogicRoleVariant.AsString() : "prop");
				placedPropSprite.SetMeta("logic_label", tile.TryGetValue("logic_label", out Variant placedPropLogicLabelVariant) ? placedPropLogicLabelVariant.AsString() : placedPropSprite.GetMeta("logic_label", GetPropDefinitionPreview(placedPropDefinitionPath).DisplayName).AsString());
				placedPropSprite.SetMeta("logic_target_id", tile.TryGetValue("logic_target_id", out Variant placedPropLogicTargetVariant) ? placedPropLogicTargetVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("npc_definition_path", tile.TryGetValue("npc_definition_path", out Variant placedPropNpcDefinitionVariant) ? placedPropNpcDefinitionVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("logic_npc_portrait", tile.TryGetValue("logic_npc_portrait", out Variant placedPropLogicPortraitVariant) ? placedPropLogicPortraitVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("logic_required_flag", tile.TryGetValue("logic_required_flag", out Variant placedPropLogicRequiredVariant) ? placedPropLogicRequiredVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("logic_set_flag", tile.TryGetValue("logic_set_flag", out Variant placedPropLogicSetVariant) ? placedPropLogicSetVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("logic_trigger_mode", tile.TryGetValue("logic_trigger_mode", out Variant placedPropLogicTriggerVariant) ? placedPropLogicTriggerVariant.AsString() : "interact");
				placedPropSprite.SetMeta("logic_once", tile.TryGetValue("logic_once", out Variant placedPropLogicOnceVariant) ? placedPropLogicOnceVariant.AsBool() : false);
				placedPropSprite.SetMeta("logic_notes", tile.TryGetValue("logic_notes", out Variant placedPropLogicNotesVariant) ? placedPropLogicNotesVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("prop_definition_path", placedPropDefinitionPath);
				placedPropSprite.RotationDegrees = rotationDegrees;
				ApplySpriteFlipState(placedPropSprite);
				MoveSpriteToCell(placedPropSprite, column, row);
				GetPlacementLayer(BuilderLayer.Prop).AddChild(placedPropSprite);
				continue;
			}

			if (!MissionTileCatalog.TryGetById(tileId, out MissionTileDefinition definition))
			{
				continue;
			}

			Sprite2D sprite = CreateSprite(definition, column, row);
			sprite.SetMeta("offset_x", offsetX);
			sprite.SetMeta("offset_y", offsetY);
			sprite.SetMeta("rotation_degrees", rotationDegrees);
			sprite.SetMeta("flip_h", flipH);
			sprite.SetMeta("flip_v", flipV);
			sprite.SetMeta("logic_role", tile.TryGetValue("logic_role", out Variant logicRoleVariant) ? logicRoleVariant.AsString() : sprite.GetMeta("logic_role", string.Empty).AsString());
			sprite.SetMeta("logic_label", tile.TryGetValue("logic_label", out Variant tileLogicLabelVariant) ? tileLogicLabelVariant.AsString() : sprite.GetMeta("logic_label", definition.DisplayName).AsString());
			sprite.SetMeta("logic_target_id", tile.TryGetValue("logic_target_id", out Variant tileLogicTargetVariant) ? tileLogicTargetVariant.AsString() : sprite.GetMeta("logic_target_id", string.Empty).AsString());
			sprite.SetMeta("npc_definition_path", tile.TryGetValue("npc_definition_path", out Variant tileNpcDefinitionVariant) ? tileNpcDefinitionVariant.AsString() : string.Empty);
			sprite.SetMeta("logic_npc_portrait", tile.TryGetValue("logic_npc_portrait", out Variant tileLogicPortraitVariant) ? tileLogicPortraitVariant.AsString() : string.Empty);
			sprite.SetMeta("logic_required_flag", tile.TryGetValue("logic_required_flag", out Variant tileLogicRequiredVariant) ? tileLogicRequiredVariant.AsString() : string.Empty);
			sprite.SetMeta("logic_set_flag", tile.TryGetValue("logic_set_flag", out Variant tileLogicSetVariant) ? tileLogicSetVariant.AsString() : string.Empty);
			sprite.SetMeta("logic_trigger_mode", tile.TryGetValue("logic_trigger_mode", out Variant tileLogicTriggerVariant) ? tileLogicTriggerVariant.AsString() : sprite.GetMeta("logic_trigger_mode", "none").AsString());
			sprite.SetMeta("logic_once", tile.TryGetValue("logic_once", out Variant tileLogicOnceVariant) ? tileLogicOnceVariant.AsBool() : sprite.GetMeta("logic_once", false).AsBool());
			sprite.SetMeta("logic_notes", tile.TryGetValue("logic_notes", out Variant tileLogicNotesVariant) ? tileLogicNotesVariant.AsString() : string.Empty);
			sprite.SetMeta("prop_definition_path", tile.TryGetValue("prop_definition_path", out Variant tilePropDefinitionVariant) ? tilePropDefinitionVariant.AsString() : string.Empty);
			EnsureTileRuntimeDefaults(sprite);
			sprite.RotationDegrees = rotationDegrees;
			ApplySpriteFlipState(sprite);
			MoveSpriteToCell(sprite, column, row);
			GetPlacementLayer(GetLayerForTile(definition)).AddChild(sprite);
		}

		SelectBackgroundById(loadedBackgroundId, true, false);
		MarkPlacedMapCenterDirty();
		UpdateBackgroundFeaturePlacement();
		FramePlacedMap();
		SetStatus($"Loaded layout from {ProjectSettings.LocalizePath(path)}");
		RefreshValidationReport();
	}

	private void ClearLayout()
	{
		ClearLayoutInternal();
		SetStatus("Cleared placed tiles.");
	}

	private void ClearLayoutInternal(bool refreshValidation = true)
	{
		ClearPlacedSelection();
		foreach (Node2D layer in GetSaveLayers())
		{
			foreach (Node child in layer.GetChildren())
			{
				child.QueueFree();
			}
		}
		MarkPlacedMapCenterDirty();
		if (refreshValidation)
		{
			CallDeferred(nameof(RefreshValidationReport));
		}
	}

	private void ExitBuilder()
	{
		GetTree().Quit();
	}

	private void OpenWorkbenchV2()
	{
		GetTree().ChangeSceneToFile("res://mission_workbench_v3.tscn");
	}

	private void AdjustZoom(float delta)
	{
		ApplyZoom(_camera.Zoom.X + delta);
	}

	private void ApplyZoom(float zoomValue)
	{
		float clamped = Mathf.Clamp(zoomValue, MinZoom, MaxZoom);
		_camera.Zoom = new Vector2(clamped, clamped);
	}

	private void UpdateCameraPan(float delta)
	{
		if (_camera == null || _isPanning)
		{
			return;
		}

		Vector2 input = Vector2.Zero;
		if (Input.IsKeyPressed(Key.W)) input.Y -= 1f;
		if (Input.IsKeyPressed(Key.S)) input.Y += 1f;
		if (Input.IsKeyPressed(Key.A)) input.X -= 1f;
		if (Input.IsKeyPressed(Key.D)) input.X += 1f;

		if (input == Vector2.Zero)
		{
			return;
		}

		_camera.Position += input.Normalized() * CameraPanSpeed * delta * _camera.Zoom.X;
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}

	private void ValidateLayout()
	{
		List<ValidationIssueEntry> issues = CollectValidationIssueEntries();
		RefreshValidationReport(issues);
		if (issues.Count == 0)
		{
			SetStatus("Validation passed. Mission logic markers look healthy.");
			return;
		}

		int errorCount = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
		int warningCount = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
		int infoCount = issues.Count(issue => issue.Severity == ValidationSeverity.Info);
		SetStatus($"Validation found {issues.Count} issue(s): {errorCount} error(s), {warningCount} warning(s), {infoCount} info item(s).");
	}

	private void RefreshValidationReport()
	{
		SanitizeTransientSpriteReferences();
		RefreshValidationReport(CollectValidationIssueEntries());
	}

	private void RefreshValidationReport(List<ValidationIssueEntry> issues)
	{
		if (_validationReport == null)
		{
			return;
		}

		List<ValidationIssueEntry> allIssues = issues ?? new List<ValidationIssueEntry>();
		List<ValidationIssueEntry> visibleIssues = ApplyValidationFilter(allIssues);
		_validationEntries.Clear();
		_validationEntries.AddRange(visibleIssues);

		if (allIssues.Count == 0)
		{
			_validationReport.Text = "[color=lime]No validation issues. The mission layout has the core logic markers it needs.[/color]";
			return;
		}

		int errorCount = allIssues.Count(issue => issue.Severity == ValidationSeverity.Error);
		int warningCount = allIssues.Count(issue => issue.Severity == ValidationSeverity.Warning);
		int infoCount = allIssues.Count(issue => issue.Severity == ValidationSeverity.Info);
		List<string> lines = new List<string>();
		lines.Add($"[color=#ff6b6b]Errors: {errorCount}[/color]  [color=#ffb86b]Warnings: {warningCount}[/color]  [color=#8be9fd]Info: {infoCount}[/color]");
		if (visibleIssues.Count != allIssues.Count)
		{
			lines.Add($"[color=#bdc7d8]Filter: {GetValidationFilterDisplayName(GetCurrentValidationFilter())} ({visibleIssues.Count} shown)[/color]");
		}
		for (int i = 0; i < visibleIssues.Count; i++)
		{
			ValidationIssueEntry issue = visibleIssues[i];
			string escapedMessage = issue.Message.Replace("[", "[lb]").Replace("]", "[rb]");
			string color = GetValidationSeverityColor(issue.Severity);
			string prefix = GetValidationSeverityPrefix(issue.Severity);
			if (!string.IsNullOrWhiteSpace(issue.TargetKey))
			{
				lines.Add($"[color={color}]{prefix} [url=validation:{i}]{escapedMessage}[/url][/color]");
			}
			else
			{
				lines.Add($"[color={color}]{prefix} {escapedMessage}[/color]");
			}
		}

		_validationReport.Text = string.Join("\n", lines);
	}

	private List<ValidationIssueEntry> ApplyValidationFilter(List<ValidationIssueEntry> issues)
	{
		if (issues == null || issues.Count == 0)
		{
			return new List<ValidationIssueEntry>();
		}

		ValidationFilter filter = GetCurrentValidationFilter();
		return issues
			.Where(issue => filter switch
			{
				ValidationFilter.ErrorsOnly => issue.Severity == ValidationSeverity.Error,
				ValidationFilter.WarningsAndErrors => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Warning,
				ValidationFilter.InfoOnly => issue.Severity == ValidationSeverity.Info,
				_ => true
			})
			.ToList();
	}

	private ValidationFilter GetCurrentValidationFilter()
	{
		if (_validationFilterOption == null)
		{
			return ValidationFilter.All;
		}

		return (ValidationFilter)_validationFilterOption.Selected;
	}

	private static string GetValidationFilterDisplayName(ValidationFilter filter)
	{
		return filter switch
		{
			ValidationFilter.ErrorsOnly => "Errors Only",
			ValidationFilter.WarningsAndErrors => "Warnings + Errors",
			ValidationFilter.InfoOnly => "Info Only",
			_ => "All"
		};
	}

	private List<ValidationIssueEntry> CollectValidationIssueEntries()
	{
		List<string> issues = new List<string>();
		List<Sprite2D> markers = EnumerateLiveSprites(_markerLayer).ToList();
		List<Sprite2D> logicProps = EnumerateLiveSprites(_propLayer)
			.Where(sprite => !string.IsNullOrEmpty(sprite.GetMeta("logic_role", string.Empty).AsString()))
			.ToList();
		MissionTemplate missionTemplate = GetMissionTemplateForCurrentLayout();
		Dictionary<string, List<Sprite2D>> markersById = markers
			.GroupBy(marker => marker.GetMeta("marker_id", string.Empty).AsString())
			.ToDictionary(group => group.Key, group => group.ToList());
		Dictionary<string, List<string>> oneShotSetFlagOwners = new Dictionary<string, List<string>>();
		Dictionary<string, List<string>> semanticIdOwners = new Dictionary<string, List<string>>();

		ValidateRequiredUniqueMarker(markersById, "spawn_a", "Officer Spawn A", issues);
		ValidateRequiredUniqueMarker(markersById, "spawn_b", "Officer Spawn B", issues);

		int objectiveCount = MissionMarkerCatalog.All
			.Where(def => def.Category == MissionMarkerCategory.Objective)
			.Sum(def => markersById.TryGetValue(def.Id, out List<Sprite2D> found) ? found.Count : 0);
		if (objectiveCount == 0)
		{
			issues.Add("No objective markers are placed. Add at least one mission objective.");
		}

		foreach (MissionMarkerDefinition definition in MissionMarkerCatalog.All.Where(def => def.Category == MissionMarkerCategory.Objective))
		{
			ValidateUniqueMarker(markersById, definition.Id, definition.DisplayName, issues);
		}

		foreach (Sprite2D marker in markers)
		{
			string markerId = marker.GetMeta("marker_id", string.Empty).AsString();
			string triggerMode = marker.GetMeta("logic_trigger_mode", "none").AsString();
			string targetId = marker.GetMeta("logic_target_id", string.Empty).AsString();
			string markerLabel = marker.GetMeta("logic_label", markerId).AsString();
			string requiredFlag = marker.GetMeta("logic_required_flag", string.Empty).AsString();
			string setFlag = marker.GetMeta("logic_set_flag", string.Empty).AsString();
			bool oneShot = marker.GetMeta("logic_once", false).AsBool();
			string propDefinitionPath = marker.GetMeta("prop_definition_path", string.Empty).AsString();
			string npcDefinitionPath = marker.GetMeta("npc_definition_path", string.Empty).AsString();

			ValidateFlagConsistency(markerLabel, requiredFlag, setFlag, issues);
			if (oneShot)
			{
				TrackDuplicateUsage(oneShotSetFlagOwners, setFlag, markerLabel);
			}

			if (markerId.StartsWith("trigger_"))
			{
				if (string.IsNullOrEmpty(targetId))
				{
					issues.Add($"{markerLabel} is missing a Target ID.");
				}

				if (triggerMode == "none")
				{
					issues.Add($"{markerLabel} should use Enter or Interact trigger mode.");
				}

				if (triggerMode == "interact" && string.IsNullOrEmpty(propDefinitionPath))
				{
					issues.Add($"{markerLabel} uses Interact mode but has no Prop Definition Path assigned.");
				}

				if (markerId != "trigger_dialogue")
				{
					TrackDuplicateUsage(semanticIdOwners, BuildSemanticUsageKey("trigger_route", targetId), markerLabel);
				}
			}
			else if (markerId == "npc_spawn" || markerId == "hostile_spawn")
			{
				if (string.IsNullOrWhiteSpace(targetId) || targetId == "npc_spawn" || targetId == "hostile_spawn")
				{
					issues.Add($"{markerLabel} needs a unique Target ID spawn key.");
				}
				else
				{
					TrackDuplicateUsage(semanticIdOwners, BuildSemanticUsageKey(markerId == "hostile_spawn" ? "hostile_spawn_key" : "npc_spawn_key", targetId), markerLabel);
				}

				if (string.IsNullOrWhiteSpace(npcDefinitionPath))
				{
					issues.Add($"{markerLabel} is missing an NPC Definition Path.");
				}
				else if (!ResourceLoader.Exists(npcDefinitionPath))
				{
					issues.Add($"{markerLabel} points to missing NPC definition {npcDefinitionPath}.");
				}
				else
				{
					MissionNpcDefinition npcDefinition = GD.Load<MissionNpcDefinition>(npcDefinitionPath);
					if (markerId == "hostile_spawn" && npcDefinition != null && !npcDefinition.IsHostile)
					{
						issues.Add($"{markerLabel} uses a non-hostile NPC definition. Hostile Spawn markers should point to hostile NPC resources.");
					}
					else if (markerId == "npc_spawn" && npcDefinition != null && npcDefinition.IsHostile)
					{
						issues.Add($"{markerLabel} uses a hostile NPC definition. Use a Hostile Spawn marker for combat enemies.");
					}
				}
			}

			if (!string.IsNullOrEmpty(propDefinitionPath) && !ResourceLoader.Exists(propDefinitionPath))
			{
				issues.Add($"{markerLabel} points to missing prop definition {propDefinitionPath}.");
			}
		}

		HashSet<string> doorIds = new HashSet<string>();
		foreach (Sprite2D prop in logicProps)
		{
			string itemType = prop.GetMeta("item_type", "tile").AsString();
			string logicRole = prop.GetMeta("logic_role", string.Empty).AsString();
			string label = prop.GetMeta("logic_label", GetItemDisplayId(prop)).AsString();
			string targetId = prop.GetMeta("logic_target_id", string.Empty).AsString();
			string requiredFlag = prop.GetMeta("logic_required_flag", string.Empty).AsString();
			string setFlag = prop.GetMeta("logic_set_flag", string.Empty).AsString();
			bool oneShot = prop.GetMeta("logic_once", false).AsBool();
			string propDefinitionPath = prop.GetMeta("prop_definition_path", string.Empty).AsString();
			PropDefinition effectiveDefinition = ResolveEffectiveValidationPropDefinition(prop, missionTemplate);

			ValidateFlagConsistency(label, requiredFlag, setFlag, issues);
			if (oneShot)
			{
				TrackDuplicateUsage(oneShotSetFlagOwners, setFlag, label);
			}

			if (effectiveDefinition?.SetFlags != null && (oneShot || effectiveDefinition.OneShot))
			{
				foreach (string effectiveFlag in effectiveDefinition.SetFlags.Distinct())
				{
					TrackDuplicateUsage(oneShotSetFlagOwners, effectiveFlag, label);
				}
			}

			if (itemType == "placed_prop" && string.IsNullOrEmpty(propDefinitionPath))
			{
				issues.Add($"{label} is a placed prop but has no Prop Definition Path assigned.");
			}

			if (logicRole == "door")
			{
				if (string.IsNullOrEmpty(targetId))
				{
					issues.Add($"{label} needs a Door ID in Target ID.");
				}
				else if (!doorIds.Add(targetId))
				{
					issues.Add($"Door ID {targetId} is used more than once.");
				}
			}
			else if (logicRole == "terminal" && string.IsNullOrEmpty(propDefinitionPath))
			{
				issues.Add($"{label} needs a Prop Definition Path to spawn a runtime terminal prop.");
			}

			if (!string.IsNullOrEmpty(propDefinitionPath) && !ResourceLoader.Exists(propDefinitionPath))
			{
				issues.Add($"{label} points to missing prop definition {propDefinitionPath}.");
			}
			else if (itemType == "placed_prop" && !string.IsNullOrEmpty(propDefinitionPath))
			{
				PropDefinition definition = GD.Load<PropDefinition>(propDefinitionPath);
				if (definition != null && string.IsNullOrWhiteSpace(definition.ScenePath))
				{
					issues.Add($"{label} uses {definition.DisplayName} but that prop definition has no ScenePath.");
				}
			}

			if (effectiveDefinition != null)
			{
				switch (effectiveDefinition.InteractionType)
				{
					case PropInteractionType.Loot:
						TrackDuplicateUsage(semanticIdOwners, BuildSemanticUsageKey("loot_preset", targetId), label);
						break;
					case PropInteractionType.Custom:
						TrackDuplicateUsage(semanticIdOwners, BuildSemanticUsageKey("custom_prop_target", targetId), label);
						break;
				}
			}
		}

		foreach (Sprite2D prop in logicProps)
		{
			string logicRole = prop.GetMeta("logic_role", string.Empty).AsString();
			if (logicRole != "terminal")
			{
				continue;
			}

			string label = prop.GetMeta("logic_label", GetItemDisplayId(prop)).AsString();
			string targetId = prop.GetMeta("logic_target_id", string.Empty).AsString();
			if (string.IsNullOrEmpty(targetId))
			{
				issues.Add($"{label} needs a linked Door ID in Target ID.");
			}
			else if (!doorIds.Contains(targetId))
			{
				issues.Add($"{label} points to missing door ID {targetId}.");
			}
		}

		AddDuplicateUsageIssues(oneShotSetFlagOwners, "one-shot set flag", issues);
		AddDuplicateUsageIssues(semanticIdOwners, "semantic id", issues);

		return BuildValidationIssueEntries(issues, markers, logicProps);
	}

	private static void ValidateFlagConsistency(string label, string requiredFlag, string setFlag, List<string> issues)
	{
		if (!string.IsNullOrWhiteSpace(requiredFlag)
			&& !string.IsNullOrWhiteSpace(setFlag)
			&& string.Equals(requiredFlag, setFlag, System.StringComparison.OrdinalIgnoreCase))
		{
			issues.Add($"{label} requires and sets the same flag `{requiredFlag}`. This can create a self-blocking interaction chain.");
		}
	}

	private static void TrackDuplicateUsage(Dictionary<string, List<string>> usageMap, string usageKey, string ownerLabel)
	{
		if (string.IsNullOrWhiteSpace(usageKey) || string.IsNullOrWhiteSpace(ownerLabel))
		{
			return;
		}

		if (!usageMap.TryGetValue(usageKey, out List<string> owners))
		{
			owners = new List<string>();
			usageMap[usageKey] = owners;
		}

		if (!owners.Contains(ownerLabel))
		{
			owners.Add(ownerLabel);
		}
	}

	private static string BuildSemanticUsageKey(string kind, string value)
	{
		return string.IsNullOrWhiteSpace(value) ? string.Empty : $"{kind}:{value.Trim()}";
	}

	private static void AddDuplicateUsageIssues(Dictionary<string, List<string>> usageMap, string usageLabel, List<string> issues)
	{
		foreach (KeyValuePair<string, List<string>> kvp in usageMap.Where(pair => pair.Value.Count > 1))
		{
			string rawKey = kvp.Key;
			int separatorIndex = rawKey.IndexOf(':');
			string value = separatorIndex >= 0 && separatorIndex < rawKey.Length - 1
				? rawKey[(separatorIndex + 1)..]
				: rawKey;
			string owners = string.Join(", ", kvp.Value.OrderBy(name => name));
			issues.Add($"The {usageLabel} `{value}` is reused by multiple items: {owners}. Confirm this shared mission logic is intentional.");
		}
	}

	private List<ValidationIssueEntry> BuildValidationIssueEntries(List<string> issues, List<Sprite2D> markers, List<Sprite2D> logicProps)
	{
		List<ValidationIssueEntry> entries = new List<ValidationIssueEntry>();
		List<Sprite2D> candidates = new List<Sprite2D>();
		candidates.AddRange(markers ?? Enumerable.Empty<Sprite2D>());
		candidates.AddRange(logicProps ?? Enumerable.Empty<Sprite2D>());

		foreach (string issue in issues)
		{
			entries.Add(new ValidationIssueEntry
			{
				Message = issue,
				TargetKey = ResolveValidationTargetKey(issue, candidates),
				Severity = InferValidationSeverity(issue)
			});
		}

		return entries;
	}

	private string ResolveValidationTargetKey(string issue, List<Sprite2D> candidates)
	{
		if (string.IsNullOrWhiteSpace(issue) || candidates == null || candidates.Count == 0)
		{
			return string.Empty;
		}

		foreach (Sprite2D candidate in candidates
			.Where(sprite => sprite != null && GodotObject.IsInstanceValid(sprite))
			.OrderByDescending(sprite => GetValidationLookupNames(sprite).DefaultIfEmpty(string.Empty).Max(name => name.Length)))
		{
			foreach (string lookupName in GetValidationLookupNames(candidate))
			{
				if (!string.IsNullOrWhiteSpace(lookupName)
					&& issue.Contains(lookupName, System.StringComparison.OrdinalIgnoreCase))
				{
					return BuildValidationTargetKey(candidate);
				}
			}
		}

		return string.Empty;
	}

	private static ValidationSeverity InferValidationSeverity(string issue)
	{
		if (string.IsNullOrWhiteSpace(issue))
		{
			return ValidationSeverity.Info;
		}

		string normalized = issue.ToLowerInvariant();

		if (normalized.Contains("missing required marker")
			|| normalized.Contains("no objective markers are placed")
			|| normalized.Contains("missing a target id")
			|| normalized.Contains("should use enter or interact")
			|| normalized.Contains("has no prop definition path assigned")
			|| normalized.Contains("points to missing prop definition")
			|| normalized.Contains("needs a door id")
			|| normalized.Contains("needs a linked door id")
			|| normalized.Contains("points to missing door id")
			|| normalized.Contains("has no scenepath")
			|| normalized.Contains("requires and sets the same flag"))
		{
			return ValidationSeverity.Error;
		}

		if (normalized.Contains("appears ")
			|| normalized.Contains("is reused by multiple items")
			|| normalized.Contains("shared mission logic is intentional"))
		{
			return ValidationSeverity.Warning;
		}

		return ValidationSeverity.Info;
	}

	private static string GetValidationSeverityColor(ValidationSeverity severity)
	{
		return severity switch
		{
			ValidationSeverity.Error => "#ff6b6b",
			ValidationSeverity.Warning => "#ffb86b",
			_ => "#8be9fd"
		};
	}

	private static string GetValidationSeverityPrefix(ValidationSeverity severity)
	{
		return severity switch
		{
			ValidationSeverity.Error => "[ERROR]",
			ValidationSeverity.Warning => "[WARN]",
			_ => "[INFO]"
		};
	}

	private string BuildValidationTargetKey(Sprite2D sprite)
	{
		if (sprite == null)
		{
			return string.Empty;
		}

		int column = sprite.GetMeta("column", int.MinValue).AsInt32();
		int row = sprite.GetMeta("row", int.MinValue).AsInt32();
		string itemType = sprite.GetMeta("item_type", string.Empty).AsString();
		string markerId = sprite.GetMeta("marker_id", string.Empty).AsString();
		string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
		string label = sprite.GetMeta("logic_label", string.Empty).AsString();
		return $"{itemType}|{column}|{row}|{markerId}|{tileId}|{label}";
	}

	private Sprite2D ResolveValidationTargetFromKey(string targetKey)
	{
		if (string.IsNullOrWhiteSpace(targetKey))
		{
			return null;
		}

		string[] parts = targetKey.Split('|');
		if (parts.Length < 6)
		{
			return null;
		}

		string itemType = parts[0];
		if (!int.TryParse(parts[1], out int column) || !int.TryParse(parts[2], out int row))
		{
			return null;
		}

		string markerId = parts[3];
		string tileId = parts[4];
		string label = parts[5];

		IEnumerable<Node2D> layers = itemType == "marker"
			? new[] { _markerLayer }
			: new[] { _propLayer, _wallLayer, _floorLayer };
		foreach (Node2D layer in layers.Where(layer => layer != null))
		{
			foreach (Sprite2D sprite in EnumerateLiveSprites(layer))
			{
				if (sprite.GetMeta("column", int.MinValue).AsInt32() != column || sprite.GetMeta("row", int.MinValue).AsInt32() != row)
				{
					continue;
				}

				if (!string.IsNullOrWhiteSpace(markerId) && sprite.GetMeta("marker_id", string.Empty).AsString() != markerId)
				{
					continue;
				}

				if (!string.IsNullOrWhiteSpace(tileId) && sprite.GetMeta("tile_id", string.Empty).AsString() != tileId)
				{
					continue;
				}

				if (!string.IsNullOrWhiteSpace(label) && sprite.GetMeta("logic_label", string.Empty).AsString() != label)
				{
					continue;
				}

				return sprite;
			}
		}

		return null;
	}

	private IEnumerable<string> GetValidationLookupNames(Sprite2D sprite)
	{
		if (sprite == null)
		{
			yield break;
		}

		string label = sprite.GetMeta("logic_label", string.Empty).AsString();
		if (!string.IsNullOrWhiteSpace(label))
		{
			yield return label;
		}

		string markerId = sprite.GetMeta("marker_id", string.Empty).AsString();
		if (!string.IsNullOrWhiteSpace(markerId))
		{
			yield return markerId;
		}

		string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
		if (!string.IsNullOrWhiteSpace(tileId))
		{
			yield return tileId;
		}

		string displayId = GetItemDisplayId(sprite);
		if (!string.IsNullOrWhiteSpace(displayId))
		{
			yield return displayId;
		}
	}

	private static PropDefinition ResolveEffectiveValidationPropDefinition(Sprite2D prop, MissionTemplate missionTemplate)
	{
		if (prop == null)
		{
			return null;
		}

		string propDefinitionPath = prop.GetMeta("prop_definition_path", string.Empty).AsString();
		if (string.IsNullOrWhiteSpace(propDefinitionPath))
		{
			string tileId = prop.GetMeta("tile_id", string.Empty).AsString();
			propDefinitionPath = GetDefaultPropDefinitionPath(tileId);
		}

		if (string.IsNullOrWhiteSpace(propDefinitionPath) || !ResourceLoader.Exists(propDefinitionPath))
		{
			return null;
		}

		PropDefinition baseDefinition = GD.Load<PropDefinition>(propDefinitionPath);
		if (baseDefinition == null)
		{
			return null;
		}

		PropDefinition definition = baseDefinition.Duplicate(true) as PropDefinition ?? baseDefinition;
		MissionRoomBuilder.MarkerPlacement placement = new MissionRoomBuilder.MarkerPlacement
		{
			MarkerId = prop.GetMeta("marker_id", string.Empty).AsString(),
			TileId = prop.GetMeta("tile_id", string.Empty).AsString(),
			LogicRole = prop.GetMeta("logic_role", string.Empty).AsString(),
			PropDefinitionPath = propDefinitionPath,
			Label = prop.GetMeta("logic_label", string.Empty).AsString(),
			TargetId = prop.GetMeta("logic_target_id", string.Empty).AsString(),
			NpcPortraitPath = prop.GetMeta("logic_npc_portrait", string.Empty).AsString(),
			RequiredFlag = prop.GetMeta("logic_required_flag", string.Empty).AsString(),
			SetFlag = prop.GetMeta("logic_set_flag", string.Empty).AsString(),
			TriggerMode = prop.GetMeta("logic_trigger_mode", "none").AsString(),
			OneShot = prop.GetMeta("logic_once", false).AsBool(),
			Notes = prop.GetMeta("logic_notes", string.Empty).AsString(),
			Cell = new Vector2I(
				prop.GetMeta("column", 0).AsInt32(),
				prop.GetMeta("row", 0).AsInt32())
		};
		PropPlacementOverrides.ApplyRuntimeOverrides(definition, placement, missionTemplate);
		return definition;
	}

	private void OnValidationReportMetaClicked(Variant meta)
	{
		string value = meta.AsString();
		if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("validation:"))
		{
			return;
		}

		string indexText = value["validation:".Length..];
		if (int.TryParse(indexText, out int issueIndex))
		{
			FocusValidationIssue(issueIndex);
		}
	}

	private void FocusValidationIssue(int issueIndex)
	{
		if (issueIndex < 0 || issueIndex >= _validationEntries.Count)
		{
			return;
		}

		ValidationIssueEntry entry = _validationEntries[issueIndex];
		Sprite2D target = ResolveValidationTargetFromKey(entry?.TargetKey ?? string.Empty);
		if (target == null || !GodotObject.IsInstanceValid(target))
		{
			SetStatus(entry?.Message ?? "Validation issue has no linked item.");
			return;
		}

		SelectPlacedSprite(target);
		if (_camera != null)
		{
			_camera.Position = target.Position;
		}

		int column = target.GetMeta("column", 0).AsInt32();
		int row = target.GetMeta("row", 0).AsInt32();
		SetStatus($"Focused validation issue at {column},{row}: {entry.Message}");
	}

	private static void ValidateRequiredUniqueMarker(Dictionary<string, List<Sprite2D>> markersById, string markerId, string displayName, List<string> issues)
	{
		if (!markersById.TryGetValue(markerId, out List<Sprite2D> markers) || markers.Count == 0)
		{
			issues.Add($"Missing required marker: {displayName}.");
			return;
		}

		if (markers.Count > 1)
		{
			issues.Add($"{displayName} appears {markers.Count} times. Keep it to one.");
		}
	}

	private static void ValidateUniqueMarker(Dictionary<string, List<Sprite2D>> markersById, string markerId, string displayName, List<string> issues)
	{
		if (markersById.TryGetValue(markerId, out List<Sprite2D> markers) && markers.Count > 1)
		{
			issues.Add($"{displayName} appears {markers.Count} times. Keep objective markers unique.");
		}
	}

	private static int GetTriggerModeIndex(string triggerMode)
	{
		return triggerMode switch
		{
			"enter" => 1,
			"interact" => 2,
			_ => 0
		};
	}

	private static string GetTriggerModeValue(int selectedIndex)
	{
		return selectedIndex switch
		{
			1 => "enter",
			2 => "interact",
			_ => "none"
		};
	}

	private static int GetLogicRoleIndex(string logicRole)
	{
		return logicRole switch
		{
			"door" => 1,
			"terminal" => 2,
			_ => 0
		};
	}

	private static string GetLogicRoleValue(int selectedIndex, bool isMarker)
	{
		if (isMarker)
		{
			return "marker";
		}

		return selectedIndex switch
		{
			1 => "door",
			2 => "terminal",
			_ => string.Empty
		};
	}

	private static bool IsDoorTileId(string tileId)
	{
		return tileId.StartsWith("door_");
	}

	private static string BuildDefaultDoorTargetId(Sprite2D sprite)
	{
		if (sprite == null)
		{
			return string.Empty;
		}

		string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
		if (string.IsNullOrWhiteSpace(tileId))
		{
			return string.Empty;
		}

		int column = sprite.GetMeta("column", 0).AsInt32();
		int row = sprite.GetMeta("row", 0).AsInt32();
		return $"{tileId}_{column}_{row}";
	}

	private void EnsureTileRuntimeDefaults(Sprite2D sprite)
	{
		if (sprite == null)
		{
			return;
		}

		string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
		if (!IsDoorTileId(tileId))
		{
			return;
		}

		string currentRole = sprite.GetMeta("logic_role", string.Empty).AsString();
		if (!string.Equals(currentRole, "door", System.StringComparison.OrdinalIgnoreCase))
		{
			sprite.SetMeta("logic_role", "door");
		}

		string currentTargetId = sprite.GetMeta("logic_target_id", string.Empty).AsString();
		if (string.IsNullOrWhiteSpace(currentTargetId))
		{
			sprite.SetMeta("logic_target_id", BuildDefaultDoorTargetId(sprite));
		}

		string currentTriggerMode = sprite.GetMeta("logic_trigger_mode", string.Empty).AsString();
		if (string.IsNullOrWhiteSpace(currentTriggerMode) || string.Equals(currentTriggerMode, "none", System.StringComparison.OrdinalIgnoreCase))
		{
			sprite.SetMeta("logic_trigger_mode", "interact");
		}

		string currentLabel = sprite.GetMeta("logic_label", string.Empty).AsString();
		if (string.IsNullOrWhiteSpace(currentLabel) && MissionTileCatalog.TryGetById(tileId, out MissionTileDefinition definition))
		{
			sprite.SetMeta("logic_label", definition.DisplayName);
		}
	}

	private static bool IsTerminalTileId(string tileId)
	{
		return tileId.StartsWith("console_");
	}

	private static bool IsLogicCapableTileId(string tileId)
	{
		return IsDoorTileId(tileId) || IsTerminalTileId(tileId);
	}

	private static string GetDefaultPropDefinitionPath(string itemId)
	{
		return itemId switch
		{
			"trigger_dialogue" => "res://Data/Missions/Props/Definitions/dialogue_terminal.tres",
			"objective_archive" => "res://Data/Missions/Props/Definitions/smuggler_cache_crate.tres",
			"medical_station" => "res://Data/Missions/Props/Definitions/medical_bed_heal.tres",
			_ when IsTerminalTileId(itemId) => "res://Data/Missions/Props/Definitions/door_control_terminal.tres",
			_ => string.Empty
		};
	}

	private static string GetDefaultNpcDefinitionPath(string itemId)
	{
		return itemId switch
		{
			_ => string.Empty
		};
	}

	private PropDefinitionPreview GetPropDefinitionPreview(string path)
	{
		string normalizedPath = path?.StripEdges() ?? string.Empty;
		if (_propDefinitionPreviewCache.TryGetValue(normalizedPath, out PropDefinitionPreview cached))
		{
			return cached;
		}

		PropDefinitionPreview preview = BuildPropDefinitionPreview(normalizedPath);
		_propDefinitionPreviewCache[normalizedPath] = preview;
		return preview;
	}

	private PropDefinitionPreview BuildPropDefinitionPreview(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return new PropDefinitionPreview
			{
				Path = string.Empty,
				DisplayName = "None",
				Description = "No prop definition selected.",
				Exists = true,
				VisualScaleMultiplier = 0.60f
			};
		}

		bool exists = ResourceLoader.Exists(path);
		PropDefinition definition = exists ? GD.Load<PropDefinition>(path) : null;
		Texture2D icon = null;
		if (definition != null && !string.IsNullOrEmpty(definition.SpriteTexturePath) && ResourceLoader.Exists(definition.SpriteTexturePath))
		{
			icon = GD.Load<Texture2D>(definition.SpriteTexturePath);
		}

		string fallbackName = System.IO.Path.GetFileNameWithoutExtension(path);
		string displayName = definition?.DisplayName;
		if (string.IsNullOrEmpty(displayName))
		{
			displayName = string.IsNullOrEmpty(fallbackName) ? "Unnamed" : fallbackName.Replace('_', ' ');
		}

		string description = definition?.Description ?? string.Empty;
		if (!exists)
		{
			description = $"Missing prop resource.\n{path}";
		}
		else if (string.IsNullOrEmpty(description))
		{
			description = path;
		}

		return new PropDefinitionPreview
		{
			Path = path,
			DisplayName = displayName,
			Description = description,
			Icon = icon,
			Exists = exists,
			VisualScaleMultiplier = definition?.VisualScaleMultiplier > 0f ? definition.VisualScaleMultiplier : PropVisualSizing.DefaultVisualScaleMultiplier
		};
	}

	private void UpdatePropDefinitionPreview(string path)
	{
		if (_logicPropDefinitionPreviewLabel == null || _logicPropDefinitionPreviewIcon == null)
		{
			return;
		}

		PropDefinitionPreview preview = GetPropDefinitionPreview(path);
		_logicPropDefinitionPreviewIcon.Texture = preview.Icon;
		_logicPropDefinitionPreviewIcon.Visible = preview.Icon != null;
		_logicPropDefinitionPreviewLabel.Text = string.IsNullOrEmpty(preview.Path)
			? "No prop definition selected."
			: $"{preview.DisplayName}\n{preview.Description}";
	}

	private static List<string> GetAvailablePropDefinitionPaths()
	{
		const string propDefinitionsDirectory = "res://Data/Missions/Props/Definitions";
		return DirAccess.GetFilesAt(propDefinitionsDirectory)
			.Where(file => file.EndsWith(".tres") || file.EndsWith(".res"))
			.OrderBy(file => file)
			.Select(file => $"{propDefinitionsDirectory}/{file}")
			.ToList();
	}

	private static List<string> GetAvailableNpcDefinitionPaths()
	{
		const string npcDefinitionsDirectory = "res://Data/Missions/Entities/Npcs";
		return DirAccess.GetFilesAt(npcDefinitionsDirectory)
			.Where(file => file.EndsWith(".tres") || file.EndsWith(".res"))
			.OrderBy(file => file)
			.Select(file => $"{npcDefinitionsDirectory}/{file}")
			.ToList();
	}

	private static List<string> GetAvailableHostileNpcDefinitionPaths()
	{
		return GetAvailableNpcDefinitionPaths()
			.Where(path =>
			{
				if (!ResourceLoader.Exists(path))
				{
					return false;
				}

				MissionNpcDefinition definition = GD.Load<MissionNpcDefinition>(path);
				return definition?.IsHostile == true;
			})
			.ToList();
	}

	private NpcDefinitionPreview GetNpcDefinitionPreview(string path)
	{
		string normalizedPath = path?.StripEdges() ?? string.Empty;
		if (_npcDefinitionPreviewCache.TryGetValue(normalizedPath, out NpcDefinitionPreview cachedPreview))
		{
			return cachedPreview;
		}

		bool exists = !string.IsNullOrEmpty(normalizedPath) && ResourceLoader.Exists(normalizedPath);
		MissionNpcDefinition definition = exists ? GD.Load<MissionNpcDefinition>(normalizedPath) : null;
		Texture2D icon = !string.IsNullOrEmpty(definition?.PortraitPath)
			? GD.Load<Texture2D>(definition.PortraitPath)
			: (!string.IsNullOrEmpty(definition?.SpriteTexturePath) ? GD.Load<Texture2D>(definition.SpriteTexturePath) : null);
		string fallbackName = normalizedPath.Split('/').LastOrDefault()?.Replace(".tres", string.Empty).Replace(".res", string.Empty);
		string displayName = string.IsNullOrWhiteSpace(definition?.DisplayName)
			? (string.IsNullOrEmpty(fallbackName) ? "Unnamed NPC" : fallbackName.Replace('_', ' '))
			: definition.DisplayName;
		string description = definition?.Description ?? string.Empty;
		if (!exists)
		{
			description = $"Missing NPC resource.\n{normalizedPath}";
		}
		else if (string.IsNullOrEmpty(description))
		{
			description = normalizedPath;
		}

		NpcDefinitionPreview preview = new NpcDefinitionPreview
		{
			Path = normalizedPath,
			DisplayName = displayName,
			Description = description,
			Icon = icon,
			Exists = exists,
			IsHostile = definition?.IsHostile == true
		};
		_npcDefinitionPreviewCache[normalizedPath] = preview;
		return preview;
	}

	private void UpdateNpcDefinitionPreview(string path)
	{
		if (_logicNpcDefinitionPreviewLabel == null)
		{
			return;
		}

		NpcDefinitionPreview preview = GetNpcDefinitionPreview(path);
		_logicNpcDefinitionPreviewLabel.Text = string.IsNullOrEmpty(preview.Path)
			? "No NPC definition selected."
			: $"{(preview.IsHostile ? "[HOSTILE] " : string.Empty)}{preview.DisplayName}\n{preview.Description}";

		if (_contextNpcDefinitionPreviewLabel != null)
		{
			_contextNpcDefinitionPreviewLabel.Text = _logicNpcDefinitionPreviewLabel.Text;
		}
	}

	private Texture2D GetFallbackPropPreviewTexture()
	{
		if (_fallbackPropPreviewTexture != null)
		{
			return _fallbackPropPreviewTexture;
		}

		using Image image = Image.CreateEmpty(84, 84, false, Image.Format.Rgba8);
		Color frame = new Color(0.47f, 0.86f, 0.92f, 1f);
		Color fill = new Color(0.13f, 0.22f, 0.28f, 0.85f);
		for (int y = 0; y < 84; y++)
		{
			for (int x = 0; x < 84; x++)
			{
				bool border = x < 6 || x >= 78 || y < 6 || y >= 78;
				image.SetPixel(x, y, border ? frame : fill);
			}
		}

		_fallbackPropPreviewTexture = ImageTexture.CreateFromImage(image);
		return _fallbackPropPreviewTexture;
	}

	private static Vector2 GetPlacedPropPreviewScale(Texture2D texture, float visualScaleMultiplier)
		=> PropVisualSizing.GetScale(texture, visualScaleMultiplier);

	private static bool IsLogicCapableSprite(Sprite2D sprite)
	{
		string tileId = sprite?.GetMeta("tile_id", string.Empty).AsString() ?? string.Empty;
		return !string.IsNullOrEmpty(tileId) && IsLogicCapableTileId(tileId);
	}

	private static bool IsPlacedPropSprite(Sprite2D sprite)
	{
		return sprite?.GetMeta("item_type", string.Empty).AsString() == "placed_prop";
	}

	private static bool IsFlippablePropSprite(Sprite2D sprite)
	{
		if (sprite == null || !string.IsNullOrEmpty(sprite.GetMeta("marker_id", string.Empty).AsString()))
		{
			return false;
		}

		if (IsPlacedPropSprite(sprite))
		{
			return true;
		}

		if (sprite.GetMeta("layer", string.Empty).AsString() != "prop")
		{
			return false;
		}

		string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
		return !string.IsNullOrWhiteSpace(tileId) && !IsDoorTileId(tileId);
	}

	private static bool DoesSpriteAffectValidation(Sprite2D sprite)
	{
		if (!IsLiveSprite(sprite))
		{
			return false;
		}

		if (!string.IsNullOrEmpty(sprite.GetMeta("marker_id", string.Empty).AsString()))
		{
			return true;
		}

		return IsPlacedPropSprite(sprite) || IsLogicCapableSprite(sprite);
	}

	private static bool IsFloorSprite(Sprite2D sprite)
	{
		if (!IsLiveSprite(sprite))
		{
			return false;
		}

		string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
		return MissionTileCatalog.TryGetById(tileId, out MissionTileDefinition definition)
			&& definition.Category == MissionTileCategory.Floor;
	}

	private void MarkPlacedMapCenterDirty()
	{
		_placedMapCenterDirty = true;
	}

	private static bool IsLiveNode(Node node)
	{
		return node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
	}

	private static bool IsLiveSprite(Sprite2D sprite)
	{
		return IsLiveNode(sprite);
	}

	private static void ApplySpriteFlipState(Sprite2D sprite)
	{
		if (sprite == null)
		{
			return;
		}

		sprite.FlipH = sprite.GetMeta("flip_h", false).AsBool();
		sprite.FlipV = sprite.GetMeta("flip_v", false).AsBool();
	}

	private void SanitizeTransientSpriteReferences()
	{
		if (!IsLiveSprite(_draggedSprite))
		{
			_draggedSprite = null;
		}

		if (!IsLiveSprite(_selectedPlacedSprite))
		{
			_selectedPlacedSprite = null;
			if (_contextObjectEditorPanel != null)
			{
				_contextObjectEditorPanel.Visible = false;
			}
		}
	}

	private bool TryGetDraggedSprite(out Sprite2D sprite)
	{
		if (!IsLiveSprite(_draggedSprite))
		{
			_draggedSprite = null;
		}

		sprite = _draggedSprite;
		return sprite != null;
	}

	private bool TryGetSelectedPlacedSprite(out Sprite2D sprite)
	{
		if (!IsLiveSprite(_selectedPlacedSprite))
		{
			_selectedPlacedSprite = null;
		}

		sprite = _selectedPlacedSprite;
		return sprite != null;
	}

	private static IEnumerable<Sprite2D> EnumerateLiveSprites(Node2D layer)
	{
		if (!IsLiveNode(layer))
		{
			yield break;
		}

		foreach (Node child in layer.GetChildren())
		{
			if (child is Sprite2D sprite && IsLiveSprite(sprite))
			{
				yield return sprite;
			}
		}
	}

	private bool TryGetPlacedSpriteBounds(out Rect2 bounds)
	{
		bool hasAny = false;
		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minY = float.MaxValue;
		float maxY = float.MinValue;

		foreach (Node2D layer in GetSaveLayers())
		{
			foreach (Sprite2D sprite in EnumerateLiveSprites(layer))
			{
				Vector2 halfSize = GetSpriteBoundsSize(sprite) * 0.5f;
				Vector2 position = sprite.Position;
				minX = Mathf.Min(minX, position.X - halfSize.X);
				maxX = Mathf.Max(maxX, position.X + halfSize.X);
				minY = Mathf.Min(minY, position.Y - halfSize.Y);
				maxY = Mathf.Max(maxY, position.Y + halfSize.Y);
				hasAny = true;
			}
		}

		if (!hasAny)
		{
			bounds = new Rect2();
			return false;
		}

		bounds = new Rect2(minX, minY, maxX - minX, maxY - minY);
		return true;
	}

	private BuilderLayer GetLayerForTile(MissionTileDefinition definition)
	{
		return definition.Category switch
		{
			MissionTileCategory.Floor => BuilderLayer.Floor,
			MissionTileCategory.Wall => BuilderLayer.Wall,
			_ => BuilderLayer.Prop
		};
	}

	private Node2D GetPlacementLayer(BuilderLayer layer)
	{
		return layer switch
		{
			BuilderLayer.Floor => _floorLayer,
			BuilderLayer.Wall => _wallLayer,
			BuilderLayer.Marker => _markerLayer,
			_ => _propLayer
		};
	}

	private IEnumerable<Node2D> GetSelectableLayers()
	{
		yield return _markerLayer;
		yield return _propLayer;
		yield return _wallLayer;
		yield return _floorLayer;
	}

	private IEnumerable<Node2D> GetSaveLayers()
	{
		yield return _floorLayer;
		yield return _wallLayer;
		yield return _propLayer;
		yield return _markerLayer;
	}

	private string GetItemDisplayId(Sprite2D sprite)
	{
		string markerId = sprite.GetMeta("marker_id", "").AsString();
		if (!string.IsNullOrEmpty(markerId))
		{
			string label = sprite.GetMeta("logic_label", string.Empty).AsString();
			return string.IsNullOrWhiteSpace(label) ? markerId : label;
		}

		if (IsPlacedPropSprite(sprite))
		{
			return GetPropDefinitionPreview(sprite.GetMeta("prop_definition_path", string.Empty).AsString()).DisplayName;
		}

		return sprite.GetMeta("tile_id", "").AsString();
	}

	private Vector2 GetSpriteBoundsSize(Sprite2D sprite)
	{
		if (sprite.RegionEnabled)
		{
			return sprite.RegionRect.Size * sprite.Scale.Abs();
		}

		return sprite.Texture != null ? sprite.Texture.GetSize() * sprite.Scale.Abs() : new Vector2(72f, 72f);
	}

	private Color GetBaseModulate(Sprite2D sprite)
	{
		return sprite.GetMeta("base_modulate", Colors.White).AsColor();
	}

	private Color Brighten(Color color)
	{
		return new Color(
			Mathf.Min(color.R * 1.25f, 1f),
			Mathf.Min(color.G * 1.25f, 1f),
			Mathf.Min(color.B * 1.25f, 1f),
			color.A);
	}

	private Texture2D GetTileTexture(MissionTileDefinition definition)
	{
		if (definition.Category == MissionTileCategory.Floor)
		{
			return MissionFloorTextureFactory.GetTexture(definition.Id);
		}

		if (!string.IsNullOrEmpty(definition.TexturePath))
		{
			return GD.Load<Texture2D>(definition.TexturePath);
		}

		return GD.Load<Texture2D>("res://Assets/Missions/BlackSiteRelay/black_site_relay_tileset.png");
	}

	private Texture2D GetTileIconTexture(MissionTileDefinition definition)
	{
		if (_tileIconCache.TryGetValue(definition.Id, out Texture2D cachedTexture))
		{
			return cachedTexture;
		}

		Texture2D texture;
		if (!string.IsNullOrEmpty(definition.TexturePath))
		{
			texture = GD.Load<Texture2D>(definition.TexturePath);
			_tileIconCache[definition.Id] = texture;
			return texture;
		}

		texture = new AtlasTexture
		{
			Atlas = GD.Load<Texture2D>("res://Assets/Missions/BlackSiteRelay/black_site_relay_tileset.png"),
			Region = definition.Region
		};
		_tileIconCache[definition.Id] = texture;
		return texture;
	}

	private Texture2D GetMarkerIconTexture(MissionMarkerDefinition definition)
	{
		if (definition == null)
		{
			return null;
		}

		if (_markerIconCache.TryGetValue(definition.Id, out Texture2D cachedTexture))
		{
			return cachedTexture;
		}

		using Image image = Image.CreateEmpty(72, 72, false, Image.Format.Rgba8);
		Color white = Colors.White;
		Vector2 center = new Vector2(36f, 36f);
		for (int y = 0; y < 72; y++)
		{
			for (int x = 0; x < 72; x++)
			{
				float diamond = Mathf.Abs(x - center.X) + Mathf.Abs(y - center.Y);
				if (diamond <= 30f && diamond >= 22f)
				{
					image.SetPixel(x, y, white);
				}
				else if (definition.Category == MissionMarkerCategory.Objective && diamond <= 14f)
				{
					image.SetPixel(x, y, white);
				}
				else if (definition.Category == MissionMarkerCategory.Spawn && (new Vector2(x, y) - center).Length() <= 11f)
				{
					image.SetPixel(x, y, white);
				}
				else if (definition.Category == MissionMarkerCategory.Trigger && Mathf.Abs(x - center.X) <= 4f && Mathf.Abs(y - center.Y) <= 18f)
				{
					image.SetPixel(x, y, white);
				}
				else
				{
					image.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
				}
			}
		}

		Texture2D texture = ImageTexture.CreateFromImage(image);
		_markerIconCache[definition.Id] = texture;
		return texture;
	}
}
