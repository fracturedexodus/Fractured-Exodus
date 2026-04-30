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

	private Camera2D _camera;
	private Node2D _placementLayer;
	private VBoxContainer _paletteContainer;
	private Label _statusLabel;
	private LineEdit _layoutNameEdit;
	private Node2D _hoverLayer;
	private Label _selectedLabel;
	private MissionTileDefinition _selectedTile;
	private Sprite2D _draggedSprite;
	private Sprite2D _selectedPlacedSprite;
	private Vector2I _draggedCell;
	private bool _isPanning;
	private Vector2 _lastMouseScreenPosition;
	private readonly List<Line2D> _gridLines = new List<Line2D>();
	private Polygon2D _hoverDiamond;
	private readonly Vector2 _tileStep = new Vector2(250f, 122f);
	private readonly Vector2 _gridOrigin = new Vector2(0f, -20f);

	public override void _Ready()
	{
		_camera = GetNode<Camera2D>("Camera2D");
		_placementLayer = GetNode<Node2D>("World/PlacementLayer");
		_paletteContainer = GetNode<VBoxContainer>("UILayer/PalettePanel/Margin/PaletteScroll/PaletteList");
		_statusLabel = GetNode<Label>("UILayer/BottomBar/Margin/StatusLabel");
		_layoutNameEdit = GetNode<LineEdit>("UILayer/TopBar/Margin/TopRow/LayoutNameEdit");
		_selectedLabel = GetNode<Label>("UILayer/TopBar/Margin/TopRow/SelectedTileLabel");
		_hoverLayer = GetNode<Node2D>("World/HoverLayer");

		BuildPalette();
		BuildGrid();
		BuildHoverDiamond();
		WireUi();
		ApplyZoom(DefaultZoom);
		UpdateSelectedLabel();
		SetStatus("Left click to place. Drag tiles to move. Right click deletes. Mouse wheel zooms.");
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
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/ClearButton").Pressed += ClearLayout;
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

			foreach (MissionTileDefinition definition in MissionTileCatalog.All.Where(def => def.Category == category))
			{
		Button button = new Button
				{
					Text = definition.DisplayName,
					Icon = new AtlasTexture
					{
						Atlas = GD.Load<Texture2D>("res://Assets/Missions/BlackSiteRelay/black_site_relay_tileset.png"),
						Region = definition.Region
					},
					Alignment = HorizontalAlignment.Left,
					ExpandIcon = true,
					CustomMinimumSize = new Vector2(0f, 40f)
				};
				button.Pressed += () =>
				{
					_selectedTile = definition;
					ClearPlacedSelection();
					UpdateSelectedLabel();
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
			string placedId = _selectedPlacedSprite.GetMeta("tile_id", "").AsString();
			int column = _selectedPlacedSprite.GetMeta("column", 0).AsInt32();
			int row = _selectedPlacedSprite.GetMeta("row", 0).AsInt32();
			_selectedLabel.Text = $"Tile: {placedId} @ {column},{row}";
			return;
		}

		_selectedLabel.Text = _selectedTile == null
			? "Selected: None"
			: $"Palette: {_selectedTile.DisplayName}";
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
		Sprite2D existing = FindSpriteAtMouse();
		if (existing != null)
		{
			SelectPlacedSprite(existing);
			_draggedSprite = existing;
			_draggedCell = GetMouseCell();
			SetStatus($"Dragging {existing.GetMeta("tile_id", "").AsString()}");
			return;
		}

		if (_selectedTile == null)
		{
			return;
		}

		Vector2I cell = GetMouseCell();
		Sprite2D sprite = CreateSprite(_selectedTile, cell.X, cell.Y);
		_placementLayer.AddChild(sprite);
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

		SetStatus($"Removed {sprite.GetMeta("tile_id", "").AsString()}");
		if (sprite == _selectedPlacedSprite)
		{
			ClearPlacedSelection();
		}
		sprite.QueueFree();
	}

	private void SelectPlacedSprite(Sprite2D sprite)
	{
		if (_selectedPlacedSprite == sprite)
		{
			return;
		}

		ClearPlacedSelection();
		_selectedPlacedSprite = sprite;
		_selectedPlacedSprite.Modulate = new Color(1.15f, 1.15f, 1.15f, 1f);
		ToggleSelectionOutline(_selectedPlacedSprite, true);
		UpdateSelectedLabel();
	}

	private void ClearPlacedSelection()
	{
		if (_selectedPlacedSprite != null)
		{
			_selectedPlacedSprite.Modulate = Colors.White;
			ToggleSelectionOutline(_selectedPlacedSprite, false);
			_selectedPlacedSprite = null;
		}

		UpdateSelectedLabel();
	}

	private Sprite2D FindSpriteAtMouse()
	{
		Vector2 mouseWorld = GetGlobalMousePosition();
		Godot.Collections.Array<Node> children = _placementLayer.GetChildren();
		for (int index = children.Count - 1; index >= 0; index--)
		{
			Node child = children[index];
			if (child is not Sprite2D sprite)
			{
				continue;
			}

			Rect2 region = sprite.RegionRect;
			Vector2 size = region.Size * sprite.Scale;
			Rect2 bounds = new Rect2(sprite.GlobalPosition - (size * 0.5f), size);
			if (bounds.HasPoint(mouseWorld))
			{
				return sprite;
			}
		}

		return null;
	}

	private void MoveSpriteToCell(Sprite2D sprite, int column, int row)
	{
		string tileId = sprite.GetMeta("tile_id", "").AsString();
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

	private Sprite2D CreateSprite(MissionTileDefinition definition, int column, int row)
	{
		Sprite2D sprite = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://Assets/Missions/BlackSiteRelay/black_site_relay_tileset.png"),
			RegionEnabled = true,
			RegionRect = definition.Region,
			Scale = definition.Scale,
			Position = IsoGridHelper.GridToWorld(column, row, _tileStep, _gridOrigin) + definition.Offset
		};
		sprite.SetMeta("tile_id", definition.Id);
		sprite.SetMeta("column", column);
		sprite.SetMeta("row", row);
		sprite.SetMeta("offset_x", 0f);
		sprite.SetMeta("offset_y", 0f);
		sprite.SetMeta("rotation_degrees", 0f);
		sprite.AddChild(CreateSelectionOutline(definition.Region.Size));
		return sprite;
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
		foreach (Node child in _placementLayer.GetChildren())
		{
			if (child is not Sprite2D sprite)
			{
				continue;
			}

			items.Add(new Godot.Collections.Dictionary<string, Variant>
			{
				{ "tile_id", sprite.GetMeta("tile_id", "").AsString() },
				{ "column", sprite.GetMeta("column", 0).AsInt32() },
				{ "row", sprite.GetMeta("row", 0).AsInt32() },
				{ "offset_x", sprite.GetMeta("offset_x", 0f).AsSingle() },
				{ "offset_y", sprite.GetMeta("offset_y", 0f).AsSingle() },
				{ "rotation_degrees", sprite.GetMeta("rotation_degrees", 0f).AsSingle() }
			});
		}

		file.StoreString(Json.Stringify(items, "\t"));
		SetStatus($"Saved layout to {ProjectSettings.LocalizePath(path)}");
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
			string tileId = tile.TryGetValue("tile_id", out Variant tileIdVariant) ? tileIdVariant.AsString() : "";
			int column = tile.TryGetValue("column", out Variant columnVariant) ? columnVariant.AsInt32() : 0;
			int row = tile.TryGetValue("row", out Variant rowVariant) ? rowVariant.AsInt32() : 0;
			float offsetX = tile.TryGetValue("offset_x", out Variant offsetXVariant) ? offsetXVariant.AsSingle() : 0f;
			float offsetY = tile.TryGetValue("offset_y", out Variant offsetYVariant) ? offsetYVariant.AsSingle() : 0f;
			float rotationDegrees = tile.TryGetValue("rotation_degrees", out Variant rotationVariant) ? rotationVariant.AsSingle() : 0f;
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
			_placementLayer.AddChild(sprite);
		}

		SetStatus($"Loaded layout from {ProjectSettings.LocalizePath(path)}");
	}

	private void ClearLayout()
	{
		ClearLayoutInternal();
		SetStatus("Cleared placed tiles.");
	}

	private void ClearLayoutInternal()
	{
		ClearPlacedSelection();
		foreach (Node child in _placementLayer.GetChildren())
		{
			child.QueueFree();
		}
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
}
