using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap3D : Node3D
{
	private const string MissionId = "black_site_relay";
	private const string LayoutResourcePath = "res://Data/MissionLayouts3D/black_site_relay_3d.json";
	private const float CellSize = 4f;
	private const float DefaultCameraSize = 34f;
	private const float MinCameraSize = 12f;
	private const float MaxCameraSize = 72f;
	private const float ZoomStep = 2.5f;
	private const float CameraPanSpeed = 20f;

	private GlobalData _globalData;
	private MissionService _missionService;
	private MissionRuntimeState _missionState;
	private Camera3D _camera;
	private Node3D _cameraRig;
	private Node3D _gridRoot;
	private Node3D _placementRoot;
	private Node3D _officerRoot;
	private Label _statusLabel;
	private Label _selectedOfficerLabel;
	private readonly List<Node3D> _officerMarkers = new List<Node3D>();
	private int _selectedOfficerIndex;
	private bool _isPanning;
	private Vector2 _lastMouseScreenPosition;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		_missionService = new MissionService(_globalData);
		_missionState = _missionService.GetCurrentMissionState();
		_camera = GetNode<Camera3D>("CameraRig/Camera3D");
		_cameraRig = GetNode<Node3D>("CameraRig");
		_gridRoot = GetNode<Node3D>("World/GridRoot");
		_placementRoot = GetNode<Node3D>("World/PlacementRoot");
		_officerRoot = GetNode<Node3D>("World/OfficerRoot");
		_statusLabel = GetNode<Label>("UILayer/TopLeftPanel/Margin/Content/StatusLabel");
		_selectedOfficerLabel = GetNode<Label>("UILayer/TopLeftPanel/Margin/Content/SelectedOfficerLabel");

		if (_missionState == null || string.IsNullOrEmpty(_missionState.MissionID))
		{
			_missionState = _missionService.PrepareMission(MissionId, "res://exploration_battle.tscn", "Black Site Relay Beacon");
		}

		LoadLayout();
		SpawnOfficerMarkers();
		WireUi();
		ApplyZoom(DefaultCameraSize);
		UpdateSelectedOfficerDisplay();
		SetStatus("3D mission loaded. Left click moves the selected officer. TAB switches officer. Mouse wheel zooms.");
	}

	public override void _Process(double delta)
	{
		UpdateCameraPan((float)delta);
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

			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				MoveSelectedOfficerToMouse();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (@event is InputEventMouseButton releaseButton && !releaseButton.Pressed && releaseButton.ButtonIndex == MouseButton.Middle)
		{
			_isPanning = false;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventMouseMotion motion && _isPanning)
		{
			Vector2 deltaScreen = motion.Position - _lastMouseScreenPosition;
			_cameraRig.Position += new Vector3(-deltaScreen.X * 0.03f, 0f, -deltaScreen.Y * 0.03f) * (_camera.Size / 24f);
			_lastMouseScreenPosition = motion.Position;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Tab)
			{
				CycleOfficerSelection();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.Escape)
			{
				ReturnWithoutOutcome();
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
		}
	}

	private void LoadLayout()
	{
		string absolutePath = ProjectSettings.GlobalizePath(LayoutResourcePath);
		if (!FileAccess.FileExists(absolutePath))
		{
			SetStatus($"Missing 3D layout: {LayoutResourcePath}");
			return;
		}

		using FileAccess file = FileAccess.Open(absolutePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			SetStatus($"Could not open 3D layout: {LayoutResourcePath}");
			return;
		}

		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Array)
		{
			SetStatus("3D layout format is invalid.");
			return;
		}

		foreach (Node child in _placementRoot.GetChildren())
		{
			child.QueueFree();
		}

		List<Vector2I> floorCells = new List<Vector2I>();
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

			Vector2I cell = new Vector2I(gridX, gridZ);
			if (definition.Category == MissionTileCategory.Floor)
			{
				floorCells.Add(cell);
			}

			Node3D placed = CreatePlacedTile(definition, cell, new Vector3(offsetX, offsetY, offsetZ), rotationDegrees, skinId);
			_placementRoot.AddChild(placed);
		}

		FrameMissionMap(floorCells);
	}

	private Node3D CreatePlacedTile(Mission3DTileDefinition definition, Vector2I cell, Vector3 adjustment, float yawDegrees, string skinId)
	{
		Node3D tile = Mission3DTileCatalog.CreateTileNode(definition, skinId);
		tile.SetMeta("tile_id", definition.Id);
		tile.SetMeta("grid_x", cell.X);
		tile.SetMeta("grid_z", cell.Y);
		tile.Position = GetCellCenter(cell) + definition.DefaultOffset + adjustment;
		tile.RotationDegrees = new Vector3(0f, yawDegrees, 0f);
		return tile;
	}

	private void FrameMissionMap(List<Vector2I> cells)
	{
		if (cells.Count == 0)
		{
			FrameCameraAt(Vector3.Zero);
			return;
		}

		float minX = cells.Min(cell => cell.X);
		float maxX = cells.Max(cell => cell.X);
		float minZ = cells.Min(cell => cell.Y);
		float maxZ = cells.Max(cell => cell.Y);
		Vector3 center = new Vector3(((minX + maxX + 1f) * 0.5f) * CellSize, 0f, ((minZ + maxZ + 1f) * 0.5f) * CellSize);
		FrameCameraAt(center);
		BuildGrid(new Vector2I((int)minX, (int)minZ), new Vector2I((int)maxX, (int)maxZ));
	}

	private void FrameCameraAt(Vector3 center)
	{
		_cameraRig.Position = center;
		_cameraRig.Rotation = Vector3.Zero;
		_camera.Position = new Vector3(0f, 48f, 48f);
		_camera.LookAt(center, Vector3.Up);
	}

	private void BuildGrid(Vector2I minCell, Vector2I maxCell)
	{
		foreach (Node child in _gridRoot.GetChildren())
		{
			child.QueueFree();
		}

		StandardMaterial3D lineMaterial = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.24f, 0.45f, 0.75f, 0.35f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};

		float minX = minCell.X * CellSize;
		float maxX = (maxCell.X + 1) * CellSize;
		float minZ = minCell.Y * CellSize;
		float maxZ = (maxCell.Y + 1) * CellSize;
		float width = maxX - minX;
		float depth = maxZ - minZ;
		float centerX = (minX + maxX) * 0.5f;
		float centerZ = (minZ + maxZ) * 0.5f;

		for (int x = minCell.X; x <= maxCell.X + 1; x++)
		{
			AddGridStrip(new Vector3(x * CellSize, 0.04f, centerZ), new Vector3(0.05f, 0.02f, depth), lineMaterial);
		}

		for (int z = minCell.Y; z <= maxCell.Y + 1; z++)
		{
			AddGridStrip(new Vector3(centerX, 0.04f, z * CellSize), new Vector3(width, 0.02f, 0.05f), lineMaterial);
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

	private void SpawnOfficerMarkers()
	{
		foreach (Node child in _officerRoot.GetChildren())
		{
			child.QueueFree();
		}
		_officerMarkers.Clear();

		List<string> shipNames = _missionState?.ParticipatingShipNames ?? new List<string>();
		if (shipNames.Count == 0 && _globalData?.SelectedPlayerFleet != null)
		{
			shipNames = _globalData.SelectedPlayerFleet.Take(2).ToList();
		}

		List<Vector2I> spawnCells = GetFloorCells().Take(Mathf.Max(shipNames.Count, 1)).ToList();
		if (spawnCells.Count == 0)
		{
			spawnCells.Add(Vector2I.Zero);
		}

		for (int i = 0; i < shipNames.Count && i < spawnCells.Count; i++)
		{
			Node3D marker = CreateOfficerMarker(shipNames[i], i);
			marker.Position = GetCellCenter(spawnCells[i]) + new Vector3(0f, 0.8f, 0f);
			_officerRoot.AddChild(marker);
			_officerMarkers.Add(marker);
		}

		SelectOfficer(0);
	}

	private IEnumerable<Vector2I> GetFloorCells()
	{
		return _placementRoot.GetChildren()
			.OfType<Node3D>()
			.Where(tile => Mission3DTileCatalog.TryGetById(tile.GetMeta("tile_id", "").AsString(), out Mission3DTileDefinition definition)
				&& definition.Category == MissionTileCategory.Floor)
			.Select(tile => new Vector2I(tile.GetMeta("grid_x", 0).AsInt32(), tile.GetMeta("grid_z", 0).AsInt32()))
			.OrderBy(cell => cell.Y)
			.ThenBy(cell => cell.X);
	}

	private Node3D CreateOfficerMarker(string shipName, int index)
	{
		Node3D root = new Node3D { Name = $"Officer_{index + 1}" };
		Color color = index == 0 ? new Color(0.3f, 0.9f, 1f) : new Color(1f, 0.75f, 0.25f);
		StandardMaterial3D material = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = 0.8f
		};

		MeshInstance3D body = new MeshInstance3D
		{
			Mesh = new CapsuleMesh { Radius = 0.32f, Height = 1.35f },
			MaterialOverride = material,
			Position = new Vector3(0f, 0.7f, 0f)
		};
		root.AddChild(body);

		Label3D label = new Label3D
		{
			Text = shipName,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(0f, 1.75f, 0f),
			FontSize = 42,
			Modulate = Colors.White
		};
		root.AddChild(label);
		root.SetMeta("ship_name", shipName);
		return root;
	}

	private void MoveSelectedOfficerToMouse()
	{
		Node3D marker = GetSelectedOfficerMarker();
		if (marker == null)
		{
			return;
		}

		(Vector3? point, _) = GetMousePlaneIntersection();
		if (point == null)
		{
			return;
		}

		Vector2I cell = new Vector2I(
			Mathf.FloorToInt(point.Value.X / CellSize),
			Mathf.FloorToInt(point.Value.Z / CellSize));
		marker.Position = GetCellCenter(cell) + new Vector3(0f, 0.8f, 0f);
		SetStatus($"Moved {marker.GetMeta("ship_name", "Officer").AsString()} to {cell.X},{cell.Y}");
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

	private Vector3 GetCellCenter(Vector2I cell)
	{
		return new Vector3((cell.X + 0.5f) * CellSize, 0f, (cell.Y + 0.5f) * CellSize);
	}

	private void WireUi()
	{
		GetNode<Button>("UILayer/BottomRightPanel/Margin/ButtonStack/SaveSurvivorsButton").Pressed += () => CompleteMission(BuildOutcome("survivors_saved"));
		GetNode<Button>("UILayer/BottomRightPanel/Margin/ButtonStack/SecureArchiveButton").Pressed += () => CompleteMission(BuildOutcome("archive_secured"));
		GetNode<Button>("UILayer/BottomRightPanel/Margin/ButtonStack/ReturnButton").Pressed += ReturnWithoutOutcome;
	}

	private void SelectOfficer(int index)
	{
		if (_officerMarkers.Count == 0)
		{
			UpdateSelectedOfficerDisplay();
			return;
		}

		_selectedOfficerIndex = Mathf.Clamp(index, 0, _officerMarkers.Count - 1);
		for (int i = 0; i < _officerMarkers.Count; i++)
		{
			_officerMarkers[i].Scale = i == _selectedOfficerIndex ? new Vector3(1.25f, 1.25f, 1.25f) : Vector3.One;
		}
		UpdateSelectedOfficerDisplay();
	}

	private void CycleOfficerSelection()
	{
		if (_officerMarkers.Count == 0)
		{
			return;
		}

		SelectOfficer((_selectedOfficerIndex + 1) % _officerMarkers.Count);
	}

	private Node3D GetSelectedOfficerMarker()
	{
		return _selectedOfficerIndex >= 0 && _selectedOfficerIndex < _officerMarkers.Count
			? _officerMarkers[_selectedOfficerIndex]
			: null;
	}

	private void UpdateSelectedOfficerDisplay()
	{
		Node3D marker = GetSelectedOfficerMarker();
		_selectedOfficerLabel.Text = marker == null
			? "ACTIVE OFFICER: None assigned"
			: $"ACTIVE OFFICER: {marker.GetMeta("ship_name", "Officer").AsString()}";
	}

	private MissionOutcome BuildOutcome(string outcomeId)
	{
		MissionOutcome outcome = new MissionOutcome
		{
			MissionID = MissionId,
			OutcomeID = outcomeId,
			IsSuccess = true
		};

		if (outcomeId == "survivors_saved")
		{
			outcome.Reward.RawMaterials = 70;
			outcome.Reward.EnergyCores = 1;
			outcome.FlagsToSet.Add("relay_survivors_saved");
		}
		else
		{
			outcome.Reward.EnergyCores = 2;
			outcome.Reward.AncientTech = 2;
			outcome.FlagsToSet.Add("relay_archive_secured");
		}

		return outcome;
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
		if (_statusLabel != null)
		{
			_statusLabel.Text = text;
		}
	}
}
