using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionSceneBuilder3D : Node3D
{
	private const float DefaultCameraSize = 26f;
	private const float MinCameraSize = 10f;
	private const float MaxCameraSize = 52f;
	private const float ZoomStep = 2.5f;
	private const float CameraPanSpeed = 18f;
	private const float CellSize = 4f;
	private const float MoveNudge = 0.4f;
	private const float RotateStep = 15f;
	private const int GridRadius = 10;

	private Camera3D _camera;
	private Node3D _cameraRig;
	private Node3D _placementRoot;
	private Node3D _gridRoot;
	private VBoxContainer _paletteContainer;
	private Label _statusLabel;
	private LineEdit _layoutNameEdit;
	private OptionButton _skinOption;
	private Label _selectedLabel;
	private Mission3DTileDefinition _selectedDefinition;
	private Node3D _selectedPlacedTile;
	private Node3D _draggedTile;
	private Vector2I _draggedCell;
	private bool _isPanning;
	private Vector2 _lastMouseScreenPosition;
	private MeshInstance3D _hoverCell;

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("CameraRig/Camera3D");
		_cameraRig = GetNode<Node3D>("CameraRig");
		_placementRoot = GetNode<Node3D>("World/PlacementRoot");
		_gridRoot = GetNode<Node3D>("World/GridRoot");
		_paletteContainer = GetNode<VBoxContainer>("UILayer/PalettePanel/Margin/PaletteScroll/PaletteList");
		_statusLabel = GetNode<Label>("UILayer/BottomBar/Margin/StatusLabel");
		_layoutNameEdit = GetNode<LineEdit>("UILayer/TopBar/Margin/TopRow/LayoutNameEdit");
		_skinOption = GetNode<OptionButton>("UILayer/TopBar/Margin/TopRow/SkinOption");
		_selectedLabel = GetNode<Label>("UILayer/TopBar/Margin/TopRow/SelectedTileLabel");

		BuildSkinOptions();
		BuildPalette();
		BuildGrid();
		BuildHoverCell();
		WireUi();
		ApplyZoom(DefaultCameraSize);
		UpdateSelectedLabel();
		SetStatus("Left click to place/select. Drag placed tiles. Right click deletes. WASD pans. Mouse wheel zooms.");
		LoadLayout();
	}

	public override void _Process(double delta)
	{
		UpdateCameraPan((float)delta);
		UpdateHoverCell();
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

		if (@event is InputEventMouseButton releaseButton && !releaseButton.Pressed)
		{
			if (releaseButton.ButtonIndex == MouseButton.Left)
			{
				_draggedTile = null;
			}
			else if (releaseButton.ButtonIndex == MouseButton.Middle)
			{
				_isPanning = false;
			}
		}

		if (@event is InputEventMouseMotion motion)
		{
			if (_isPanning)
			{
				Vector2 deltaScreen = motion.Position - _lastMouseScreenPosition;
				_cameraRig.Position += new Vector3(-deltaScreen.X * 0.03f, 0f, -deltaScreen.Y * 0.03f) * (_camera.Size / 24f);
				_lastMouseScreenPosition = motion.Position;
				GetViewport().SetInputAsHandled();
				return;
			}

			if (_draggedTile != null)
			{
				Vector2I cell = GetMouseCell();
				if (cell != _draggedCell)
				{
					_draggedCell = cell;
					MoveTileToCell(_draggedTile, cell);
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

			if (_selectedPlacedTile != null)
			{
				if (keyEvent.Keycode == Key.Left)
				{
					AdjustSelectedTile(new Vector3(-MoveNudge, 0f, 0f), 0f);
					GetViewport().SetInputAsHandled();
					return;
				}
				if (keyEvent.Keycode == Key.Right)
				{
					AdjustSelectedTile(new Vector3(MoveNudge, 0f, 0f), 0f);
					GetViewport().SetInputAsHandled();
					return;
				}
				if (keyEvent.Keycode == Key.Up)
				{
					AdjustSelectedTile(new Vector3(0f, 0f, -MoveNudge), 0f);
					GetViewport().SetInputAsHandled();
					return;
				}
				if (keyEvent.Keycode == Key.Down)
				{
					AdjustSelectedTile(new Vector3(0f, 0f, MoveNudge), 0f);
					GetViewport().SetInputAsHandled();
					return;
				}
				if (keyEvent.Keycode == Key.Q)
				{
					AdjustSelectedTile(Vector3.Zero, -RotateStep);
					GetViewport().SetInputAsHandled();
					return;
				}
				if (keyEvent.Keycode == Key.E)
				{
					AdjustSelectedTile(Vector3.Zero, RotateStep);
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
		GetNode<Button>("UILayer/TopBar/Margin/TopRow/ExitButton").Pressed += ExitBuilder;
		_skinOption.ItemSelected += OnSkinSelected;
	}

	private void BuildSkinOptions()
	{
		_skinOption.Clear();
		foreach (Mission3DSkinDefinition skin in Mission3DTileCatalog.AllSkins)
		{
			_skinOption.AddItem(skin.DisplayName);
			_skinOption.SetItemMetadata(_skinOption.ItemCount - 1, skin.Id);
		}
		_skinOption.Select(0);
	}

	private void OnSkinSelected(long index)
	{
		if (_selectedPlacedTile == null)
		{
			UpdateSelectedLabel();
			return;
		}

		string skinId = GetSelectedSkinId();
		Mission3DTileCatalog.ApplySkin(_selectedPlacedTile, skinId);
		SetStatus($"Applied {GetSelectedSkinName()} skin to {_selectedPlacedTile.GetMeta("tile_id", "").AsString()}.");
		UpdateSelectedLabel();
	}

	private void BuildPalette()
	{
		foreach (Node child in _paletteContainer.GetChildren())
		{
			child.QueueFree();
		}

		foreach (MissionTileCategory category in new[] { MissionTileCategory.Floor, MissionTileCategory.Wall, MissionTileCategory.Prop })
		{
			_paletteContainer.AddChild(new Label { Text = category.ToString().ToUpper() });
			foreach (Mission3DTileDefinition definition in Mission3DTileCatalog.All.Where(def => def.Category == category))
			{
				Button button = new Button
				{
					Text = definition.DisplayName,
					Alignment = HorizontalAlignment.Left,
					CustomMinimumSize = new Vector2(0f, 38f)
				};
				button.Pressed += () =>
				{
					_selectedDefinition = definition;
					ClearPlacedSelection();
					UpdateSelectedLabel();
				};
				_paletteContainer.AddChild(button);
			}
		}

		_selectedDefinition = Mission3DTileCatalog.All.FirstOrDefault();
	}

	private void BuildGrid()
	{
		StandardMaterial3D lineMaterial = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.24f, 0.34f, 0.52f, 0.4f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};

		for (int x = -GridRadius; x <= GridRadius; x++)
		{
			AddGridStrip(new Vector3(x * CellSize, 0f, 0f), new Vector3(0.05f, 0.02f, GridRadius * CellSize * 2f), lineMaterial);
		}

		for (int z = -GridRadius; z <= GridRadius; z++)
		{
			AddGridStrip(new Vector3(0f, 0f, z * CellSize), new Vector3(GridRadius * CellSize * 2f, 0.02f, 0.05f), lineMaterial);
		}
	}

	private void AddGridStrip(Vector3 position, Vector3 size, Material material)
	{
		MeshInstance3D mesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = material,
			Position = position
		};
		_gridRoot.AddChild(mesh);
	}

	private void BuildHoverCell()
	{
		StandardMaterial3D hoverMaterial = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.40f, 0.90f, 1.00f, 0.18f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			NoDepthTest = true
		};

		_hoverCell = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(CellSize, 0.05f, CellSize) },
			MaterialOverride = hoverMaterial,
			Position = new Vector3(0f, 0.03f, 0f)
		};
		GetNode<Node3D>("World/HoverRoot").AddChild(_hoverCell);
	}

	private void UpdateHoverCell()
	{
		if (_hoverCell == null)
		{
			return;
		}

		Vector2I cell = GetMouseCell();
		_hoverCell.Position = GetCellCenter(cell) + new Vector3(0f, 0.03f, 0f);
	}

	private Vector2I GetMouseCell()
	{
		(Vector3? point, _) = GetMousePlaneIntersection();
		if (point == null)
		{
			return Vector2I.Zero;
		}

		int x = Mathf.FloorToInt(point.Value.X / CellSize);
		int z = Mathf.FloorToInt(point.Value.Z / CellSize);
		return new Vector2I(x, z);
	}

	private Vector3 GetCellCenter(Vector2I cell)
	{
		return new Vector3((cell.X + 0.5f) * CellSize, 0f, (cell.Y + 0.5f) * CellSize);
	}

	private (Vector3? point, Node collider) GetMousePlaneIntersection()
	{
		Vector2 mouse = GetViewport().GetMousePosition();
		Vector3 from = _camera.ProjectRayOrigin(mouse);
		Vector3 to = from + (_camera.ProjectRayNormal(mouse) * 500f);
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to);
		Godot.Collections.Dictionary result = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (result.Count > 0)
		{
			Vector3 hitPoint = result["position"].AsVector3();
			Node collider = result["collider"].AsGodotObject() as Node;
			return (new Vector3(hitPoint.X, 0f, hitPoint.Z), collider);
		}

		Plane plane = new Plane(Vector3.Up, 0f);
		Vector3? intersection = plane.IntersectsRay(from, _camera.ProjectRayNormal(mouse));
		return (intersection, null);
	}

	private void StartPlacementOrDrag()
	{
		(Vector3? point, Node collider) = GetMousePlaneIntersection();
		if (collider != null)
		{
			Node3D tileRoot = GetTileRootFromCollider(collider);
			if (tileRoot != null)
			{
				if (ShouldPlaceSelectedOnTile(tileRoot))
				{
					Vector2I floorCell = GetTileCell(tileRoot);
					Node3D stackedTile = CreatePlacedTile(_selectedDefinition, floorCell, Vector3.Zero, 0f, GetSelectedSkinId());
					_placementRoot.AddChild(stackedTile);
					SelectPlacedTile(stackedTile);
					SetStatus($"Placed {_selectedDefinition.DisplayName} on floor at {floorCell.X},{floorCell.Y}");
					return;
				}

				SelectPlacedTile(tileRoot);
				_draggedTile = tileRoot;
				_draggedCell = GetMouseCell();
				SetStatus($"Dragging {tileRoot.GetMeta("tile_id", "").AsString()}");
				return;
			}
		}

		if (_selectedDefinition == null || point == null)
		{
			return;
		}

		Vector2I cell = GetMouseCell();
		Node3D tile = CreatePlacedTile(_selectedDefinition, cell, Vector3.Zero, 0f, GetSelectedSkinId());
		_placementRoot.AddChild(tile);
		SelectPlacedTile(tile);
		SetStatus($"Placed {_selectedDefinition.DisplayName} with {GetSelectedSkinName()} skin at {cell.X},{cell.Y}");
	}

	private bool ShouldPlaceSelectedOnTile(Node3D tileRoot)
	{
		if (_selectedDefinition == null || _selectedDefinition.Category == MissionTileCategory.Floor)
		{
			return false;
		}

		if (!TryGetPlacedTileDefinition(tileRoot, out Mission3DTileDefinition clickedDefinition))
		{
			return false;
		}

		return clickedDefinition.Category == MissionTileCategory.Floor;
	}

	private static bool TryGetPlacedTileDefinition(Node3D tile, out Mission3DTileDefinition definition)
	{
		string tileId = tile.GetMeta("tile_id", "").AsString();
		return Mission3DTileCatalog.TryGetById(tileId, out definition);
	}

	private static Vector2I GetTileCell(Node3D tile)
	{
		return new Vector2I(
			tile.GetMeta("grid_x", 0).AsInt32(),
			tile.GetMeta("grid_z", 0).AsInt32());
	}

	private void DeleteTileAtMouse()
	{
		(_, Node collider) = GetMousePlaneIntersection();
		Node3D tileRoot = collider == null ? null : GetTileRootFromCollider(collider);
		if (tileRoot == null)
		{
			return;
		}

		if (tileRoot == _selectedPlacedTile)
		{
			ClearPlacedSelection();
		}

		SetStatus($"Removed {tileRoot.GetMeta("tile_id", "").AsString()}");
		tileRoot.QueueFree();
	}

	private static Node3D GetTileRootFromCollider(Node collider)
	{
		Node current = collider;
		while (current != null)
		{
			if (current is Node3D node3d && node3d.HasMeta("tile_id"))
			{
				return node3d;
			}

			current = current.GetParent();
		}

		return null;
	}

	private Node3D CreatePlacedTile(Mission3DTileDefinition definition, Vector2I cell, Vector3 adjustment, float yawDegrees, string skinId)
	{
		Node3D tile = Mission3DTileCatalog.CreateTileNode(definition, skinId);
		tile.SetMeta("tile_id", definition.Id);
		tile.SetMeta("skin_id", skinId);
		tile.SetMeta("grid_x", cell.X);
		tile.SetMeta("grid_z", cell.Y);
		tile.SetMeta("offset_x", adjustment.X);
		tile.SetMeta("offset_y", adjustment.Y);
		tile.SetMeta("offset_z", adjustment.Z);
		tile.SetMeta("rotation_degrees", yawDegrees);
		tile.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
		MoveTileToCell(tile, cell);
		return tile;
	}

	private void MoveTileToCell(Node3D tile, Vector2I cell)
	{
		string tileId = tile.GetMeta("tile_id", "").AsString();
		if (!Mission3DTileCatalog.TryGetById(tileId, out Mission3DTileDefinition definition))
		{
			return;
		}

		Vector3 adjustment = new Vector3(
			tile.GetMeta("offset_x", 0f).AsSingle(),
			tile.GetMeta("offset_y", 0f).AsSingle(),
			tile.GetMeta("offset_z", 0f).AsSingle());

		tile.Position = GetCellCenter(cell) + definition.DefaultOffset + adjustment;
		tile.SetMeta("grid_x", cell.X);
		tile.SetMeta("grid_z", cell.Y);
		UpdateSelectedLabel();
	}

	private void SelectPlacedTile(Node3D tile)
	{
		if (_selectedPlacedTile == tile)
		{
			return;
		}

		ClearPlacedSelection();
		_selectedPlacedTile = tile;
		SetTileSelectionVisible(_selectedPlacedTile, true);
		UpdateSelectedLabel();
	}

	private void ClearPlacedSelection()
	{
		if (_selectedPlacedTile != null)
		{
			SetTileSelectionVisible(_selectedPlacedTile, false);
			_selectedPlacedTile = null;
		}
		UpdateSelectedLabel();
	}

	private static void SetTileSelectionVisible(Node3D tile, bool visible)
	{
		Node3D outline = tile?.GetNodeOrNull<Node3D>("SelectionOutline");
		if (outline != null)
		{
			outline.Visible = visible;
		}
	}

	private void AdjustSelectedTile(Vector3 deltaOffset, float deltaYawDegrees)
	{
		if (_selectedPlacedTile == null)
		{
			return;
		}

		float offsetX = _selectedPlacedTile.GetMeta("offset_x", 0f).AsSingle() + deltaOffset.X;
		float offsetY = _selectedPlacedTile.GetMeta("offset_y", 0f).AsSingle() + deltaOffset.Y;
		float offsetZ = _selectedPlacedTile.GetMeta("offset_z", 0f).AsSingle() + deltaOffset.Z;
		float yaw = _selectedPlacedTile.GetMeta("rotation_degrees", 0f).AsSingle() + deltaYawDegrees;

		_selectedPlacedTile.SetMeta("offset_x", offsetX);
		_selectedPlacedTile.SetMeta("offset_y", offsetY);
		_selectedPlacedTile.SetMeta("offset_z", offsetZ);
		_selectedPlacedTile.SetMeta("rotation_degrees", yaw);
		_selectedPlacedTile.RotationDegrees = new Vector3(0f, yaw, 0f);

		Vector2I cell = new Vector2I(
			_selectedPlacedTile.GetMeta("grid_x", 0).AsInt32(),
			_selectedPlacedTile.GetMeta("grid_z", 0).AsInt32());
		MoveTileToCell(_selectedPlacedTile, cell);
		SetStatus($"Adjusted tile: offset ({offsetX:0.0},{offsetY:0.0},{offsetZ:0.0}) rotation {yaw:0}");
	}

	private void ResetSelectedTileAdjustment()
	{
		if (_selectedPlacedTile == null)
		{
			return;
		}

		_selectedPlacedTile.SetMeta("offset_x", 0f);
		_selectedPlacedTile.SetMeta("offset_y", 0f);
		_selectedPlacedTile.SetMeta("offset_z", 0f);
		_selectedPlacedTile.SetMeta("rotation_degrees", 0f);
		_selectedPlacedTile.RotationDegrees = Vector3.Zero;

		Vector2I cell = new Vector2I(
			_selectedPlacedTile.GetMeta("grid_x", 0).AsInt32(),
			_selectedPlacedTile.GetMeta("grid_z", 0).AsInt32());
		MoveTileToCell(_selectedPlacedTile, cell);
		SetStatus("Reset selected tile adjustment.");
	}

	private void UpdateSelectedLabel()
	{
		if (_selectedPlacedTile != null)
		{
			string tileId = _selectedPlacedTile.GetMeta("tile_id", "").AsString();
			string skinId = _selectedPlacedTile.GetMeta("skin_id", "default").AsString();
			int x = _selectedPlacedTile.GetMeta("grid_x", 0).AsInt32();
			int z = _selectedPlacedTile.GetMeta("grid_z", 0).AsInt32();
			string skinName = Mission3DTileCatalog.TryGetSkinById(skinId, out Mission3DSkinDefinition skin) ? skin.DisplayName : "Default";
			_selectedLabel.Text = $"Tile: {tileId} / {skinName} @ {x},{z}";
			return;
		}

		_selectedLabel.Text = _selectedDefinition == null
			? "Palette: None"
			: $"Palette: {_selectedDefinition.DisplayName} / {GetSelectedSkinName()}";
	}

	private void SaveLayout()
	{
		string path = GetLayoutAbsolutePath();
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Data/MissionLayouts3D"));
		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			SetStatus("Could not save 3D layout.");
			return;
		}

		Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>> items = new();
		foreach (Node child in _placementRoot.GetChildren())
		{
			if (child is not Node3D tile)
			{
				continue;
			}

			items.Add(new Godot.Collections.Dictionary<string, Variant>
			{
				{ "tile_id", tile.GetMeta("tile_id", "").AsString() },
				{ "skin_id", tile.GetMeta("skin_id", "default").AsString() },
				{ "grid_x", tile.GetMeta("grid_x", 0).AsInt32() },
				{ "grid_z", tile.GetMeta("grid_z", 0).AsInt32() },
				{ "offset_x", tile.GetMeta("offset_x", 0f).AsSingle() },
				{ "offset_y", tile.GetMeta("offset_y", 0f).AsSingle() },
				{ "offset_z", tile.GetMeta("offset_z", 0f).AsSingle() },
				{ "rotation_degrees", tile.GetMeta("rotation_degrees", 0f).AsSingle() }
			});
		}

		file.StoreString(Json.Stringify(items, "\t"));
		SetStatus($"Saved 3D layout to {ProjectSettings.LocalizePath(path)}");
	}

	private void LoadLayout()
	{
		string path = GetLayoutAbsolutePath();
		if (!FileAccess.FileExists(path))
		{
			SetStatus("No saved 3D layout yet. Start placing room pieces.");
			return;
		}

		ClearLayoutInternal();
		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			SetStatus("Could not open saved 3D layout.");
			return;
		}

		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Array)
		{
			SetStatus("Saved 3D layout format is invalid.");
			return;
		}

		foreach (Variant tileVariant in parsed.AsGodotArray())
		{
			Godot.Collections.Dictionary tile = tileVariant.AsGodotDictionary();
			string tileId = tile.TryGetValue("tile_id", out Variant tileIdVariant) ? tileIdVariant.AsString() : "";
			string skinId = tile.TryGetValue("skin_id", out Variant skinIdVariant) ? skinIdVariant.AsString() : "default";
			int gridX = tile.TryGetValue("grid_x", out Variant gridXVariant) ? gridXVariant.AsInt32() : 0;
			int gridZ = tile.TryGetValue("grid_z", out Variant gridZVariant) ? gridZVariant.AsInt32() : 0;
			float offsetX = tile.TryGetValue("offset_x", out Variant offsetXVariant) ? offsetXVariant.AsSingle() : 0f;
			float offsetY = tile.TryGetValue("offset_y", out Variant offsetYVariant) ? offsetYVariant.AsSingle() : 0f;
			float offsetZ = tile.TryGetValue("offset_z", out Variant offsetZVariant) ? offsetZVariant.AsSingle() : 0f;
			float rotationDegrees = tile.TryGetValue("rotation_degrees", out Variant rotationVariant) ? rotationVariant.AsSingle() : 0f;

			if (!Mission3DTileCatalog.TryGetById(tileId, out Mission3DTileDefinition definition))
			{
				continue;
			}

			Node3D placed = CreatePlacedTile(definition, new Vector2I(gridX, gridZ), new Vector3(offsetX, offsetY, offsetZ), rotationDegrees, skinId);
			_placementRoot.AddChild(placed);
		}

		SetStatus($"Loaded 3D layout from {ProjectSettings.LocalizePath(path)}");
	}

	private void ClearLayout()
	{
		ClearLayoutInternal();
		SetStatus("Cleared placed 3D room pieces.");
	}

	private void ClearLayoutInternal()
	{
		ClearPlacedSelection();
		foreach (Node child in _placementRoot.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void ExitBuilder()
	{
		GetTree().Quit();
	}

	private string GetSelectedSkinId()
	{
		int selected = _skinOption.Selected;
		if (selected < 0)
		{
			return "default";
		}

		return _skinOption.GetItemMetadata(selected).AsString();
	}

	private string GetSelectedSkinName()
	{
		int selected = _skinOption.Selected;
		return selected < 0 ? "Default" : _skinOption.GetItemText(selected);
	}

	private string GetLayoutAbsolutePath()
	{
		string layoutName = _layoutNameEdit.Text.StripEdges();
		if (string.IsNullOrEmpty(layoutName))
		{
			layoutName = "black_site_relay_3d";
			_layoutNameEdit.Text = layoutName;
		}

		return ProjectSettings.GlobalizePath($"res://Data/MissionLayouts3D/{layoutName}.json");
	}

	private void AdjustZoom(float delta)
	{
		ApplyZoom(_camera.Size + delta);
	}

	private void ApplyZoom(float size)
	{
		_camera.Size = Mathf.Clamp(size, MinCameraSize, MaxCameraSize);
	}

	private void UpdateCameraPan(float delta)
	{
		if (_isPanning)
		{
			return;
		}

		Vector3 input = Vector3.Zero;
		if (Input.IsKeyPressed(Key.W)) input.Z -= 1f;
		if (Input.IsKeyPressed(Key.S)) input.Z += 1f;
		if (Input.IsKeyPressed(Key.A)) input.X -= 1f;
		if (Input.IsKeyPressed(Key.D)) input.X += 1f;

		if (input == Vector3.Zero)
		{
			return;
		}

		_cameraRig.Position += input.Normalized() * CameraPanSpeed * delta * (_camera.Size / 26f);
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}
}
