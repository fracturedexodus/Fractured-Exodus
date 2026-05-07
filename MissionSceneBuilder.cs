using Godot;
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
	private Label _statusLabel;
	private LineEdit _layoutNameEdit;
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
	private OptionButton _logicNpcPortraitOption;
	private LineEdit _logicRequiredFlagEdit;
	private LineEdit _logicSetFlagEdit;
	private OptionButton _logicTriggerModeOption;
	private CheckBox _logicOneShotCheck;
	private TextEdit _logicNotesEdit;
	private RichTextLabel _validationReport;
	private MissionTileDefinition _selectedTile;
	private MissionMarkerDefinition _selectedMarker;
	private string _selectedPropDefinitionPath = string.Empty;
	private Sprite2D _draggedSprite;
	private Sprite2D _selectedPlacedSprite;
	private Vector2I _draggedCell;
	private bool _isPanning;
	private bool _isUpdatingBackgroundUi;
	private bool _isUpdatingLogicUi;
	private Vector2 _lastMouseScreenPosition;
	private readonly List<Line2D> _gridLines = new List<Line2D>();
	private readonly Dictionary<string, PropDefinitionPreview> _propDefinitionPreviewCache = new Dictionary<string, PropDefinitionPreview>();
	private Polygon2D _hoverDiamond;
	private readonly Vector2 _tileStep = MissionFloorTextureFactory.TileSize;
	private readonly Vector2 _gridOrigin = new Vector2(0f, -20f);
	private string _selectedBackgroundId = MissionBackgroundCatalog.DefaultId;

	private sealed class PropDefinitionPreview
	{
		public string Path { get; init; } = string.Empty;
		public string DisplayName { get; init; } = string.Empty;
		public string Description { get; init; } = string.Empty;
		public Texture2D Icon { get; init; }
		public bool Exists { get; init; }
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
		_selectedLabel = GetNode<Label>("UILayer/TopBar/Margin/TopRow/SelectedTileLabel");
		_hoverLayer = GetNode<Node2D>("World/HoverLayer");

		EnsureBackgroundPreviewNodes();
		BuildBackgroundControls();
		BuildPalette();
		BuildGrid();
		BuildHoverDiamond();
		BuildLogicPanel();
		WireUi();
		ApplyZoom(DefaultZoom);
		UpdateSelectedLabel();
		SetStatus("Left click to place/select. Drag items to move. Right click deletes. Mouse wheel zooms.");
		LoadLayout();
	}

	public override void _Process(double delta)
	{
		UpdateCameraPan((float)delta);
		UpdateHoverDiamond();
		UpdateBackgroundFeaturePlacement();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
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

			if (_draggedSprite != null)
			{
				Vector2I cell = GetMouseCell();
				if (cell != _draggedCell)
				{
					_draggedCell = cell;
					MoveSpriteToCell(_draggedSprite, cell.X, cell.Y);
				}
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
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

			if (_selectedPlacedSprite != null)
			{
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
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/LoadButton").Pressed += LoadLayout;
		Button validateButton = new Button { Text = "Validate" };
		validateButton.Pressed += ValidateLayout;
		GetNode<HBoxContainer>("UILayer/TopBar/Margin/TopRow").AddChild(validateButton);
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/ClearButton").Pressed += ClearLayout;
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/ExitButton").Pressed += ExitBuilder;
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
		List<Sprite2D> floorSprites = _floorLayer.GetChildren().OfType<Sprite2D>().ToList();
		if (floorSprites.Count == 0)
		{
			return Vector2.Zero;
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

		return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
	}

	private void BuildLogicPanel()
	{
		CanvasLayer uiLayer = GetNode<CanvasLayer>("UILayer");
		PanelContainer panel = new PanelContainer();
		panel.Name = "LogicPanel";
		panel.Position = new Vector2(1540f, 332f);
		panel.Size = new Vector2(368f, 736f);
		uiLayer.AddChild(panel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margin);

		VBoxContainer root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 10);
		margin.AddChild(root);

		Label title = new Label { Text = "MISSION LOGIC" };
		title.AddThemeFontSizeOverride("font_size", 20);
		root.AddChild(title);

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
		_logicRequiredFlagEdit = AddInspectorField(root, "Required Flag");
		_logicSetFlagEdit = AddInspectorField(root, "Set Flag");

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
		Label validationTitle = new Label { Text = "Validation" };
		validationTitle.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(validationTitle);

		_validationReport = new RichTextLabel
		{
			CustomMinimumSize = new Vector2(0f, 220f),
			ScrollActive = true,
			FitContent = false,
			BbcodeEnabled = true,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		root.AddChild(_validationReport);

		RefreshPropDefinitionOptions();
		UpdateLogicInspector();
	}

	private LineEdit AddInspectorField(VBoxContainer root, string label)
	{
		root.AddChild(new Label { Text = label });
		LineEdit lineEdit = new LineEdit();
		lineEdit.TextChanged += _ => ApplyLogicFieldChanges();
		root.AddChild(lineEdit);
		return lineEdit;
	}

	private void BuildPalette()
	{
		foreach (Node child in _paletteContainer.GetChildren())
		{
			child.QueueFree();
		}

		foreach (MissionTileCategory category in new[] { MissionTileCategory.Floor, MissionTileCategory.Wall, MissionTileCategory.Prop })
		{
			Label categoryLabel = new Label
			{
				Text = category.ToString().ToUpper()
			};
			_paletteContainer.AddChild(categoryLabel);

			foreach (MissionTileDefinition definition in MissionTileCatalog.All.Where(def => def.Category == category && def.VisibleInPalette))
			{
		Button button = new Button
				{
					Text = definition.DisplayName,
					Icon = GetTileIconTexture(definition),
					Alignment = HorizontalAlignment.Left,
					ExpandIcon = true,
					CustomMinimumSize = new Vector2(0f, 40f)
				};
				button.Pressed += () =>
				{
					_selectedTile = definition;
					_selectedMarker = null;
					_selectedPropDefinitionPath = string.Empty;
					ClearPlacedSelection();
					UpdateSelectedLabel();
					UpdateLogicInspector();
				};
				_paletteContainer.AddChild(button);
			}
		}

		_paletteContainer.AddChild(new Label { Text = "MARKERS" });
		foreach (MissionMarkerCategory category in new[] { MissionMarkerCategory.Spawn, MissionMarkerCategory.Objective, MissionMarkerCategory.Trigger })
		{
			_paletteContainer.AddChild(new Label { Text = $"  {category.ToString().ToUpper()}" });
			foreach (MissionMarkerDefinition definition in MissionMarkerCatalog.All.Where(def => def.Category == category))
			{
				Button button = new Button
				{
					Text = definition.DisplayName,
					Icon = GetMarkerIconTexture(definition),
					Alignment = HorizontalAlignment.Left,
					ExpandIcon = true,
					CustomMinimumSize = new Vector2(0f, 40f)
				};
				button.Pressed += () =>
				{
					_selectedTile = null;
					_selectedMarker = definition;
					_selectedPropDefinitionPath = string.Empty;
					ClearPlacedSelection();
					UpdateSelectedLabel();
					UpdateLogicInspector();
				};
				_paletteContainer.AddChild(button);
			}
		}

		_paletteContainer.AddChild(new Label { Text = "MISSION PROPS" });
		foreach (string propDefinitionPath in GetAvailablePropDefinitionPaths())
		{
			PropDefinitionPreview preview = GetPropDefinitionPreview(propDefinitionPath);
			Button button = new Button
			{
				Text = preview.DisplayName,
				Icon = preview.Icon,
				Alignment = HorizontalAlignment.Left,
				ExpandIcon = true,
				CustomMinimumSize = new Vector2(0f, 40f),
				TooltipText = preview.Description
			};
			button.Pressed += () =>
			{
				_selectedTile = null;
				_selectedMarker = null;
				_selectedPropDefinitionPath = propDefinitionPath;
				ClearPlacedSelection();
				UpdateSelectedLabel();
				UpdateLogicInspector();
			};
			_paletteContainer.AddChild(button);
		}

		_selectedTile = MissionTileCatalog.All.FirstOrDefault();
		_selectedPropDefinitionPath = string.Empty;
	}

	private void UpdateSelectedLabel()
	{
		if (_selectedPlacedSprite != null)
		{
			string placedId = GetItemDisplayId(_selectedPlacedSprite);
			int column = _selectedPlacedSprite.GetMeta("column", 0).AsInt32();
			int row = _selectedPlacedSprite.GetMeta("row", 0).AsInt32();
			_selectedLabel.Text = $"Selected: {placedId} @ {column},{row}";
			return;
		}

		if (_selectedMarker != null)
		{
			_selectedLabel.Text = $"Palette: {_selectedMarker.DisplayName}";
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

		for (int row = 0; row < 10; row++)
		{
			for (int column = 0; column < 10; column++)
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
					DefaultColor = new Color(0.26f, 0.38f, 0.56f, 0.28f),
					Width = 1.5f,
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
		SelectPlacedSprite(sprite);
		SetStatus($"Placed {_selectedTile.DisplayName} at {cell.X},{cell.Y}");
	}

	private void DeleteTileAtMouse()
	{
		Sprite2D sprite = FindSpriteAtMouse();
		if (sprite == null)
		{
			return;
		}

		SetStatus($"Removed {GetItemDisplayId(sprite)}");
		if (sprite == _selectedPlacedSprite)
		{
			ClearPlacedSelection();
		}
		sprite.QueueFree();
		RefreshValidationReport();
	}

	private void SelectPlacedSprite(Sprite2D sprite)
	{
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
		if (_selectedPlacedSprite != null)
		{
			_selectedPlacedSprite.Modulate = GetBaseModulate(_selectedPlacedSprite);
			ToggleSelectionOutline(_selectedPlacedSprite, false);
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
				if (child is not Sprite2D sprite)
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
			if (children[index] is not Sprite2D sprite)
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
		UpdateSelectedLabel();
	}

	private void UpdateLogicInspector()
	{
		if (_logicSelectionLabel == null)
		{
			return;
		}

		bool isMarker = _selectedPlacedSprite != null && !string.IsNullOrEmpty(_selectedPlacedSprite.GetMeta("marker_id", "").AsString());
		bool isLogicProp = _selectedPlacedSprite != null && IsLogicCapableSprite(_selectedPlacedSprite);
		bool isPlacedProp = IsPlacedPropSprite(_selectedPlacedSprite);
		bool isLogicItem = isMarker || isLogicProp || isPlacedProp;
		SetLogicEditorEnabled(isLogicItem);
		_isUpdatingLogicUi = true;

		if (!isLogicItem)
		{
			_logicSelectionLabel.Text = _selectedPlacedSprite == null
				? "Select a marker, direct mission prop, door, or terminal to edit mission logic."
				: "Selected item does not support mission logic.";
			_logicHintLabel.Text = "Use mission logic on markers, direct mission props, doors, and computer terminals.";
			_logicRoleOption.Select(0);
			_logicLabelEdit.Text = string.Empty;
			_logicTargetIdEdit.Text = string.Empty;
			_logicTargetIdEdit.PlaceholderText = string.Empty;
			_logicTargetIdHelpLabel.Text = "Target ID meaning depends on the selected marker or prop.";
			_logicPropDefinitionPathEdit.Text = string.Empty;
			RefreshPropDefinitionOptions();
			_logicNpcPortraitOption.Select(0);
			_logicRequiredFlagEdit.Text = string.Empty;
			_logicSetFlagEdit.Text = string.Empty;
			_logicTriggerModeOption.Select(0);
			_logicOneShotCheck.ButtonPressed = false;
			_logicNotesEdit.Text = string.Empty;
			_isUpdatingLogicUi = false;
			RefreshValidationReport();
			return;
		}

		Sprite2D item = _selectedPlacedSprite;
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
		RefreshPropDefinitionOptions(_logicPropDefinitionPathEdit.Text);
		UpdateTargetIdFieldContext(item, isMarker, isPlacedProp);
		SelectNpcPortraitOption(item.GetMeta("logic_npc_portrait", string.Empty).AsString());
		_logicRequiredFlagEdit.Text = item.GetMeta("logic_required_flag", string.Empty).AsString();
		_logicSetFlagEdit.Text = item.GetMeta("logic_set_flag", string.Empty).AsString();
		_logicTriggerModeOption.Select(GetTriggerModeIndex(item.GetMeta("logic_trigger_mode", "none").AsString()));
		_logicOneShotCheck.ButtonPressed = item.GetMeta("logic_once", false).AsBool();
		_logicNotesEdit.Text = item.GetMeta("logic_notes", string.Empty).AsString();
		_logicRoleOption.Disabled = isPlacedProp;
		_isUpdatingLogicUi = false;
		RefreshValidationReport();
	}

	private void SetLogicEditorEnabled(bool enabled)
	{
		_logicRoleOption.Disabled = !enabled;
		_logicLabelEdit.Editable = enabled;
		_logicTargetIdEdit.Editable = enabled;
		_logicPropDefinitionPathEdit.Editable = enabled;
		_logicPropDefinitionOption.Disabled = !enabled;
		_logicPropDefinitionRefreshButton.Disabled = !enabled;
		_logicNpcPortraitOption.Disabled = !enabled;
		_logicRequiredFlagEdit.Editable = enabled;
		_logicSetFlagEdit.Editable = enabled;
		_logicTriggerModeOption.Disabled = !enabled;
		_logicOneShotCheck.Disabled = !enabled;
		_logicNotesEdit.Editable = enabled;
	}

	private void ApplyLogicFieldChanges()
	{
		if (_isUpdatingLogicUi || _selectedPlacedSprite == null)
		{
			return;
		}

		bool isMarker = !string.IsNullOrEmpty(_selectedPlacedSprite.GetMeta("marker_id", "").AsString());
		bool isLogicProp = IsLogicCapableSprite(_selectedPlacedSprite);
		bool isPlacedProp = IsPlacedPropSprite(_selectedPlacedSprite);
		if (!isMarker && !isLogicProp && !isPlacedProp)
		{
			return;
		}

		_selectedPlacedSprite.SetMeta("logic_role", isPlacedProp ? "prop" : GetLogicRoleValue(_logicRoleOption.Selected, isMarker));
		_selectedPlacedSprite.SetMeta("logic_label", _logicLabelEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("logic_target_id", _logicTargetIdEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("prop_definition_path", _logicPropDefinitionPathEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("logic_npc_portrait", _logicNpcPortraitOption.GetItemMetadata(_logicNpcPortraitOption.Selected).AsString());
		_selectedPlacedSprite.SetMeta("logic_required_flag", _logicRequiredFlagEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("logic_set_flag", _logicSetFlagEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("logic_trigger_mode", GetTriggerModeValue(_logicTriggerModeOption.Selected));
		_selectedPlacedSprite.SetMeta("logic_once", _logicOneShotCheck.ButtonPressed);
		_selectedPlacedSprite.SetMeta("logic_notes", _logicNotesEdit.Text.StripEdges());
		UpdateMarkerCaption(_selectedPlacedSprite);
		RefreshValidationReport();
	}

	private void OnPropDefinitionPathChanged(string newText)
	{
		if (_isUpdatingLogicUi)
		{
			return;
		}

		RefreshPropDefinitionOptions(newText);
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

	private void RefreshPropDefinitionOptions()
	{
		_propDefinitionPreviewCache.Clear();
		RefreshPropDefinitionOptions(_logicPropDefinitionPathEdit?.Text ?? string.Empty);
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

	private void UpdateTargetIdFieldContextForCurrentSelection()
	{
		if (_selectedPlacedSprite == null)
		{
			_logicTargetIdEdit.PlaceholderText = string.Empty;
			if (_logicTargetIdHelpLabel != null)
			{
				_logicTargetIdHelpLabel.Text = "Target ID meaning depends on the selected marker or prop.";
			}
			return;
		}

		bool isMarker = !string.IsNullOrEmpty(_selectedPlacedSprite.GetMeta("marker_id", string.Empty).AsString());
		bool isPlacedProp = IsPlacedPropSprite(_selectedPlacedSprite);
		UpdateTargetIdFieldContext(_selectedPlacedSprite, isMarker, isPlacedProp);
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
			if (markerId == "trigger_dialogue")
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
			Scale = GetPlacedPropPreviewScale(texture)
		};
		sprite.SetMeta("tile_id", string.Empty);
		sprite.SetMeta("item_type", "placed_prop");
		sprite.SetMeta("layer", "prop");
		sprite.SetMeta("column", column);
		sprite.SetMeta("row", row);
		sprite.SetMeta("offset_x", 0f);
		sprite.SetMeta("offset_y", 0f);
		sprite.SetMeta("rotation_degrees", 0f);
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
		sprite.SetMeta("column", column);
		sprite.SetMeta("row", row);
		sprite.SetMeta("offset_x", 0f);
		sprite.SetMeta("offset_y", 0f);
		sprite.SetMeta("rotation_degrees", 0f);
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

	private void ApplyDefaultMarkerLogic(Sprite2D sprite, MissionMarkerDefinition definition)
	{
		sprite.SetMeta("logic_label", definition.DisplayName);
		sprite.SetMeta("logic_target_id", definition.Id);
		sprite.SetMeta("logic_npc_portrait", string.Empty);
		sprite.SetMeta("logic_required_flag", string.Empty);
		sprite.SetMeta("logic_set_flag", string.Empty);
		sprite.SetMeta("logic_trigger_mode", definition.Category == MissionMarkerCategory.Trigger ? "enter" : "none");
		sprite.SetMeta("logic_once", definition.Category == MissionMarkerCategory.Trigger);
		sprite.SetMeta("logic_notes", string.Empty);
		sprite.SetMeta("prop_definition_path", GetDefaultPropDefinitionPath(definition.Id));
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
		string layoutName = _layoutNameEdit?.Text.StripEdges() ?? string.Empty;
		if (string.IsNullOrEmpty(layoutName))
		{
			layoutName = "black_site_relay_builder";
		}

		return $"res://Data/MissionLayouts/{layoutName}.json";
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
		Node2D outline = sprite?.GetNodeOrNull<Node2D>("SelectionOutline");
		if (outline != null)
		{
			outline.Visible = isVisible;
		}
	}

	private void AdjustSelectedTile(Vector2 deltaOffset, float deltaRotationDegrees)
	{
		if (_selectedPlacedSprite == null)
		{
			return;
		}

		float offsetX = _selectedPlacedSprite.GetMeta("offset_x", 0f).AsSingle() + deltaOffset.X;
		float offsetY = _selectedPlacedSprite.GetMeta("offset_y", 0f).AsSingle() + deltaOffset.Y;
		float rotationDegrees = _selectedPlacedSprite.GetMeta("rotation_degrees", 0f).AsSingle() + deltaRotationDegrees;

		_selectedPlacedSprite.SetMeta("offset_x", offsetX);
		_selectedPlacedSprite.SetMeta("offset_y", offsetY);
		_selectedPlacedSprite.SetMeta("rotation_degrees", rotationDegrees);
		_selectedPlacedSprite.RotationDegrees = rotationDegrees;

		MoveSpriteToCell(
			_selectedPlacedSprite,
			_selectedPlacedSprite.GetMeta("column", 0).AsInt32(),
			_selectedPlacedSprite.GetMeta("row", 0).AsInt32());

		SetStatus($"Adjusted tile: offset ({offsetX:0},{offsetY:0}) rotation {rotationDegrees:0}");
	}

	private void ResetSelectedTileAdjustment()
	{
		if (_selectedPlacedSprite == null)
		{
			return;
		}

		_selectedPlacedSprite.SetMeta("offset_x", 0f);
		_selectedPlacedSprite.SetMeta("offset_y", 0f);
		_selectedPlacedSprite.SetMeta("rotation_degrees", 0f);
		_selectedPlacedSprite.RotationDegrees = 0f;
		MoveSpriteToCell(
			_selectedPlacedSprite,
			_selectedPlacedSprite.GetMeta("column", 0).AsInt32(),
			_selectedPlacedSprite.GetMeta("row", 0).AsInt32());
		SetStatus("Reset selected tile adjustment.");
	}

	private void SaveLayout()
	{
		string path = GetLayoutAbsolutePath();
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Data/MissionLayouts"));
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
				if (child is not Sprite2D sprite)
				{
					continue;
				}

				Godot.Collections.Dictionary<string, Variant> item = new Godot.Collections.Dictionary<string, Variant>
				{
					{ "item_type", sprite.GetMeta("item_type", "tile").AsString() },
					{ "layer", sprite.GetMeta("layer", "prop").AsString() },
					{ "column", sprite.GetMeta("column", 0).AsInt32() },
					{ "row", sprite.GetMeta("row", 0).AsInt32() },
					{ "offset_x", sprite.GetMeta("offset_x", 0f).AsSingle() },
					{ "offset_y", sprite.GetMeta("offset_y", 0f).AsSingle() },
					{ "rotation_degrees", sprite.GetMeta("rotation_degrees", 0f).AsSingle() }
				};

				string markerId = sprite.GetMeta("marker_id", "").AsString();
				if (!string.IsNullOrEmpty(markerId))
				{
					item["marker_id"] = markerId;
					item["logic_label"] = sprite.GetMeta("logic_label", string.Empty).AsString();
					item["logic_target_id"] = sprite.GetMeta("logic_target_id", string.Empty).AsString();
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
		string path = GetLayoutAbsolutePath();
		if (!FileAccess.FileExists(path))
		{
			SetStatus("No saved layout yet. Start placing tiles.");
			return;
		}

		ClearLayoutInternal();
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
				marker.SetMeta("logic_label", tile.TryGetValue("logic_label", out Variant logicLabelVariant) ? logicLabelVariant.AsString() : marker.GetMeta("logic_label", markerDefinition.DisplayName).AsString());
				marker.SetMeta("logic_target_id", tile.TryGetValue("logic_target_id", out Variant logicTargetVariant) ? logicTargetVariant.AsString() : marker.GetMeta("logic_target_id", markerDefinition.Id).AsString());
				marker.SetMeta("logic_npc_portrait", tile.TryGetValue("logic_npc_portrait", out Variant logicPortraitVariant) ? logicPortraitVariant.AsString() : string.Empty);
				marker.SetMeta("logic_required_flag", tile.TryGetValue("logic_required_flag", out Variant logicRequiredVariant) ? logicRequiredVariant.AsString() : string.Empty);
				marker.SetMeta("logic_set_flag", tile.TryGetValue("logic_set_flag", out Variant logicSetVariant) ? logicSetVariant.AsString() : string.Empty);
				marker.SetMeta("logic_trigger_mode", tile.TryGetValue("logic_trigger_mode", out Variant logicTriggerVariant) ? logicTriggerVariant.AsString() : marker.GetMeta("logic_trigger_mode", "none").AsString());
				marker.SetMeta("logic_once", tile.TryGetValue("logic_once", out Variant logicOnceVariant) ? logicOnceVariant.AsBool() : marker.GetMeta("logic_once", false).AsBool());
				marker.SetMeta("logic_notes", tile.TryGetValue("logic_notes", out Variant logicNotesVariant) ? logicNotesVariant.AsString() : string.Empty);
				marker.SetMeta("prop_definition_path", tile.TryGetValue("prop_definition_path", out Variant propDefinitionVariant) ? propDefinitionVariant.AsString() : string.Empty);
				marker.RotationDegrees = rotationDegrees;
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
				placedPropSprite.SetMeta("logic_role", tile.TryGetValue("logic_role", out Variant placedPropLogicRoleVariant) ? placedPropLogicRoleVariant.AsString() : "prop");
				placedPropSprite.SetMeta("logic_label", tile.TryGetValue("logic_label", out Variant placedPropLogicLabelVariant) ? placedPropLogicLabelVariant.AsString() : placedPropSprite.GetMeta("logic_label", GetPropDefinitionPreview(placedPropDefinitionPath).DisplayName).AsString());
				placedPropSprite.SetMeta("logic_target_id", tile.TryGetValue("logic_target_id", out Variant placedPropLogicTargetVariant) ? placedPropLogicTargetVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("logic_npc_portrait", tile.TryGetValue("logic_npc_portrait", out Variant placedPropLogicPortraitVariant) ? placedPropLogicPortraitVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("logic_required_flag", tile.TryGetValue("logic_required_flag", out Variant placedPropLogicRequiredVariant) ? placedPropLogicRequiredVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("logic_set_flag", tile.TryGetValue("logic_set_flag", out Variant placedPropLogicSetVariant) ? placedPropLogicSetVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("logic_trigger_mode", tile.TryGetValue("logic_trigger_mode", out Variant placedPropLogicTriggerVariant) ? placedPropLogicTriggerVariant.AsString() : "interact");
				placedPropSprite.SetMeta("logic_once", tile.TryGetValue("logic_once", out Variant placedPropLogicOnceVariant) ? placedPropLogicOnceVariant.AsBool() : false);
				placedPropSprite.SetMeta("logic_notes", tile.TryGetValue("logic_notes", out Variant placedPropLogicNotesVariant) ? placedPropLogicNotesVariant.AsString() : string.Empty);
				placedPropSprite.SetMeta("prop_definition_path", placedPropDefinitionPath);
				placedPropSprite.RotationDegrees = rotationDegrees;
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
			sprite.SetMeta("logic_role", tile.TryGetValue("logic_role", out Variant logicRoleVariant) ? logicRoleVariant.AsString() : sprite.GetMeta("logic_role", string.Empty).AsString());
			sprite.SetMeta("logic_label", tile.TryGetValue("logic_label", out Variant tileLogicLabelVariant) ? tileLogicLabelVariant.AsString() : sprite.GetMeta("logic_label", definition.DisplayName).AsString());
			sprite.SetMeta("logic_target_id", tile.TryGetValue("logic_target_id", out Variant tileLogicTargetVariant) ? tileLogicTargetVariant.AsString() : sprite.GetMeta("logic_target_id", string.Empty).AsString());
			sprite.SetMeta("logic_npc_portrait", tile.TryGetValue("logic_npc_portrait", out Variant tileLogicPortraitVariant) ? tileLogicPortraitVariant.AsString() : string.Empty);
			sprite.SetMeta("logic_required_flag", tile.TryGetValue("logic_required_flag", out Variant tileLogicRequiredVariant) ? tileLogicRequiredVariant.AsString() : string.Empty);
			sprite.SetMeta("logic_set_flag", tile.TryGetValue("logic_set_flag", out Variant tileLogicSetVariant) ? tileLogicSetVariant.AsString() : string.Empty);
			sprite.SetMeta("logic_trigger_mode", tile.TryGetValue("logic_trigger_mode", out Variant tileLogicTriggerVariant) ? tileLogicTriggerVariant.AsString() : sprite.GetMeta("logic_trigger_mode", "none").AsString());
			sprite.SetMeta("logic_once", tile.TryGetValue("logic_once", out Variant tileLogicOnceVariant) ? tileLogicOnceVariant.AsBool() : sprite.GetMeta("logic_once", false).AsBool());
			sprite.SetMeta("logic_notes", tile.TryGetValue("logic_notes", out Variant tileLogicNotesVariant) ? tileLogicNotesVariant.AsString() : string.Empty);
			sprite.SetMeta("prop_definition_path", tile.TryGetValue("prop_definition_path", out Variant tilePropDefinitionVariant) ? tilePropDefinitionVariant.AsString() : string.Empty);
			sprite.RotationDegrees = rotationDegrees;
			MoveSpriteToCell(sprite, column, row);
			GetPlacementLayer(GetLayerForTile(definition)).AddChild(sprite);
		}

		SelectBackgroundById(loadedBackgroundId, true, false);
		SetStatus($"Loaded layout from {ProjectSettings.LocalizePath(path)}");
		RefreshValidationReport();
	}

	private void ClearLayout()
	{
		ClearLayoutInternal();
		SetStatus("Cleared placed tiles.");
		RefreshValidationReport();
	}

	private void ClearLayoutInternal()
	{
		ClearPlacedSelection();
		foreach (Node2D layer in GetSaveLayers())
		{
			foreach (Node child in layer.GetChildren())
			{
				child.QueueFree();
			}
		}
		RefreshValidationReport();
	}

	private void ExitBuilder()
	{
		GetTree().Quit();
	}

	private string GetLayoutAbsolutePath()
	{
		string layoutName = _layoutNameEdit.Text.StripEdges();
		if (string.IsNullOrEmpty(layoutName))
		{
			layoutName = "black_site_relay_builder";
			_layoutNameEdit.Text = layoutName;
		}

		return ProjectSettings.GlobalizePath($"res://Data/MissionLayouts/{layoutName}.json");
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
		List<string> issues = CollectValidationIssues();
		RefreshValidationReport(issues);
		if (issues.Count == 0)
		{
			SetStatus("Validation passed. Mission logic markers look healthy.");
			return;
		}

		SetStatus($"Validation found {issues.Count} issue(s). Review the mission logic panel.");
	}

	private void RefreshValidationReport()
	{
		RefreshValidationReport(CollectValidationIssues());
	}

	private void RefreshValidationReport(List<string> issues)
	{
		if (_validationReport == null)
		{
			return;
		}

		if (issues == null || issues.Count == 0)
		{
			_validationReport.Text = "[color=lime]No validation issues. The mission layout has the core logic markers it needs.[/color]";
			return;
		}

		_validationReport.Text = string.Join("\n", issues.Select(issue => $"[color=#ffb86b]- {issue}[/color]"));
	}

	private List<string> CollectValidationIssues()
	{
		List<string> issues = new List<string>();
		List<Sprite2D> markers = _markerLayer.GetChildren().OfType<Sprite2D>().ToList();
		Dictionary<string, List<Sprite2D>> markersById = markers
			.GroupBy(marker => marker.GetMeta("marker_id", string.Empty).AsString())
			.ToDictionary(group => group.Key, group => group.ToList());

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
			string propDefinitionPath = marker.GetMeta("prop_definition_path", string.Empty).AsString();

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
			}

			if (!string.IsNullOrEmpty(propDefinitionPath) && !ResourceLoader.Exists(propDefinitionPath))
			{
				issues.Add($"{markerLabel} points to missing prop definition {propDefinitionPath}.");
			}
		}

		List<Sprite2D> logicProps = _propLayer.GetChildren()
			.OfType<Sprite2D>()
			.Where(sprite => !string.IsNullOrEmpty(sprite.GetMeta("logic_role", string.Empty).AsString()))
			.ToList();
		HashSet<string> doorIds = new HashSet<string>();
		foreach (Sprite2D prop in logicProps)
		{
			string itemType = prop.GetMeta("item_type", "tile").AsString();
			string logicRole = prop.GetMeta("logic_role", string.Empty).AsString();
			string label = prop.GetMeta("logic_label", GetItemDisplayId(prop)).AsString();
			string targetId = prop.GetMeta("logic_target_id", string.Empty).AsString();
			string propDefinitionPath = prop.GetMeta("prop_definition_path", string.Empty).AsString();

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

		return issues;
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
			_ when IsTerminalTileId(itemId) => "res://Data/Missions/Props/Definitions/door_control_terminal.tres",
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
				Exists = true
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
			Exists = exists
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

	private static Texture2D GetFallbackPropPreviewTexture()
	{
		Image image = Image.CreateEmpty(84, 84, false, Image.Format.Rgba8);
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

		return ImageTexture.CreateFromImage(image);
	}

	private static Vector2 GetPlacedPropPreviewScale(Texture2D texture)
	{
		if (texture == null)
		{
			return Vector2.One;
		}

		Vector2 size = texture.GetSize();
		float maxDimension = Mathf.Max(size.X, size.Y);
		if (maxDimension <= 0f)
		{
			return Vector2.One;
		}

		float scale = maxDimension > 132f ? 132f / maxDimension : 1f;
		return new Vector2(scale, scale);
	}

	private static bool IsLogicCapableSprite(Sprite2D sprite)
	{
		string tileId = sprite?.GetMeta("tile_id", string.Empty).AsString() ?? string.Empty;
		return !string.IsNullOrEmpty(tileId) && IsLogicCapableTileId(tileId);
	}

	private static bool IsPlacedPropSprite(Sprite2D sprite)
	{
		return sprite?.GetMeta("item_type", string.Empty).AsString() == "placed_prop";
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
			return markerId;
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
		if (!string.IsNullOrEmpty(definition.TexturePath))
		{
			return GD.Load<Texture2D>(definition.TexturePath);
		}

		return new AtlasTexture
		{
			Atlas = GD.Load<Texture2D>("res://Assets/Missions/BlackSiteRelay/black_site_relay_tileset.png"),
			Region = definition.Region
		};
	}

	private Texture2D GetMarkerIconTexture(MissionMarkerDefinition definition)
	{
		Image image = Image.CreateEmpty(72, 72, false, Image.Format.Rgba8);
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

		return ImageTexture.CreateFromImage(image);
	}
}
