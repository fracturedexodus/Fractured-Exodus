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
	private Node2D _hoverLayer;
	private Label _selectedLabel;
	private Label _logicSelectionLabel;
	private Label _logicHintLabel;
	private LineEdit _logicLabelEdit;
	private LineEdit _logicTargetIdEdit;
	private LineEdit _logicRequiredFlagEdit;
	private LineEdit _logicSetFlagEdit;
	private OptionButton _logicTriggerModeOption;
	private CheckBox _logicOneShotCheck;
	private TextEdit _logicNotesEdit;
	private RichTextLabel _validationReport;
	private MissionTileDefinition _selectedTile;
	private MissionMarkerDefinition _selectedMarker;
	private Sprite2D _draggedSprite;
	private Sprite2D _selectedPlacedSprite;
	private Vector2I _draggedCell;
	private bool _isPanning;
	private bool _isUpdatingLogicUi;
	private Vector2 _lastMouseScreenPosition;
	private readonly List<Line2D> _gridLines = new List<Line2D>();
	private Polygon2D _hoverDiamond;
	private readonly Vector2 _tileStep = MissionFloorTextureFactory.TileSize;
	private readonly Vector2 _gridOrigin = new Vector2(0f, -20f);

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

		_logicLabelEdit = AddInspectorField(root, "Marker Label");
		_logicTargetIdEdit = AddInspectorField(root, "Target ID");
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
					ClearPlacedSelection();
					UpdateSelectedLabel();
					UpdateLogicInspector();
				};
				_paletteContainer.AddChild(button);
			}
		}

		_selectedTile = MissionTileCatalog.All.FirstOrDefault();
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
		SetLogicEditorEnabled(isMarker);
		_isUpdatingLogicUi = true;

		if (!isMarker)
		{
			_logicSelectionLabel.Text = _selectedPlacedSprite == null
				? "Select a marker to edit mission logic."
				: "Selected item is not a logic marker.";
			_logicHintLabel.Text = "Marker properties are where we hook up spawns, triggers, and future dialogue/events.";
			_logicLabelEdit.Text = string.Empty;
			_logicTargetIdEdit.Text = string.Empty;
			_logicRequiredFlagEdit.Text = string.Empty;
			_logicSetFlagEdit.Text = string.Empty;
			_logicTriggerModeOption.Select(0);
			_logicOneShotCheck.ButtonPressed = false;
			_logicNotesEdit.Text = string.Empty;
			_isUpdatingLogicUi = false;
			RefreshValidationReport();
			return;
		}

		Sprite2D marker = _selectedPlacedSprite;
		string markerId = marker.GetMeta("marker_id", "").AsString();
		string markerLabel = marker.GetMeta("logic_label", markerId).AsString();
		int column = marker.GetMeta("column", 0).AsInt32();
		int row = marker.GetMeta("row", 0).AsInt32();

		_logicSelectionLabel.Text = $"Editing {markerId} @ {column},{row}";
		_logicHintLabel.Text = "These fields save into the layout file and define how the mission should react to this marker.";
		_logicLabelEdit.Text = markerLabel;
		_logicTargetIdEdit.Text = marker.GetMeta("logic_target_id", string.Empty).AsString();
		_logicRequiredFlagEdit.Text = marker.GetMeta("logic_required_flag", string.Empty).AsString();
		_logicSetFlagEdit.Text = marker.GetMeta("logic_set_flag", string.Empty).AsString();
		_logicTriggerModeOption.Select(GetTriggerModeIndex(marker.GetMeta("logic_trigger_mode", "none").AsString()));
		_logicOneShotCheck.ButtonPressed = marker.GetMeta("logic_once", false).AsBool();
		_logicNotesEdit.Text = marker.GetMeta("logic_notes", string.Empty).AsString();
		_isUpdatingLogicUi = false;
		RefreshValidationReport();
	}

	private void SetLogicEditorEnabled(bool enabled)
	{
		_logicLabelEdit.Editable = enabled;
		_logicTargetIdEdit.Editable = enabled;
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

		string markerId = _selectedPlacedSprite.GetMeta("marker_id", "").AsString();
		if (string.IsNullOrEmpty(markerId))
		{
			return;
		}

		_selectedPlacedSprite.SetMeta("logic_label", _logicLabelEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("logic_target_id", _logicTargetIdEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("logic_required_flag", _logicRequiredFlagEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("logic_set_flag", _logicSetFlagEdit.Text.StripEdges());
		_selectedPlacedSprite.SetMeta("logic_trigger_mode", GetTriggerModeValue(_logicTriggerModeOption.Selected));
		_selectedPlacedSprite.SetMeta("logic_once", _logicOneShotCheck.ButtonPressed);
		_selectedPlacedSprite.SetMeta("logic_notes", _logicNotesEdit.Text.StripEdges());
		UpdateMarkerCaption(_selectedPlacedSprite);
		RefreshValidationReport();
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
		sprite.AddChild(CreateSelectionOutline(GetSpriteBoundsSize(sprite)));
		return sprite;
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
		sprite.SetMeta("logic_required_flag", string.Empty);
		sprite.SetMeta("logic_set_flag", string.Empty);
		sprite.SetMeta("logic_trigger_mode", definition.Category == MissionMarkerCategory.Trigger ? "enter" : "none");
		sprite.SetMeta("logic_once", definition.Category == MissionMarkerCategory.Trigger);
		sprite.SetMeta("logic_notes", string.Empty);
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
					item["logic_required_flag"] = sprite.GetMeta("logic_required_flag", string.Empty).AsString();
					item["logic_set_flag"] = sprite.GetMeta("logic_set_flag", string.Empty).AsString();
					item["logic_trigger_mode"] = sprite.GetMeta("logic_trigger_mode", "none").AsString();
					item["logic_once"] = sprite.GetMeta("logic_once", false).AsBool();
					item["logic_notes"] = sprite.GetMeta("logic_notes", string.Empty).AsString();
				}
				else
				{
					item["tile_id"] = sprite.GetMeta("tile_id", "").AsString();
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
				marker.SetMeta("logic_required_flag", tile.TryGetValue("logic_required_flag", out Variant logicRequiredVariant) ? logicRequiredVariant.AsString() : string.Empty);
				marker.SetMeta("logic_set_flag", tile.TryGetValue("logic_set_flag", out Variant logicSetVariant) ? logicSetVariant.AsString() : string.Empty);
				marker.SetMeta("logic_trigger_mode", tile.TryGetValue("logic_trigger_mode", out Variant logicTriggerVariant) ? logicTriggerVariant.AsString() : marker.GetMeta("logic_trigger_mode", "none").AsString());
				marker.SetMeta("logic_once", tile.TryGetValue("logic_once", out Variant logicOnceVariant) ? logicOnceVariant.AsBool() : marker.GetMeta("logic_once", false).AsBool());
				marker.SetMeta("logic_notes", tile.TryGetValue("logic_notes", out Variant logicNotesVariant) ? logicNotesVariant.AsString() : string.Empty);
				marker.RotationDegrees = rotationDegrees;
				MoveSpriteToCell(marker, column, row);
				UpdateMarkerCaption(marker);
				GetPlacementLayer(BuilderLayer.Marker).AddChild(marker);
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
			sprite.RotationDegrees = rotationDegrees;
			MoveSpriteToCell(sprite, column, row);
			GetPlacementLayer(GetLayerForTile(definition)).AddChild(sprite);
		}

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
