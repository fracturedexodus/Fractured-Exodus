using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap : Node2D
{
	private const string MissionId = "black_site_relay";
	private const float DefaultZoom = 0.52f;
	private const float MinZoom = 0.75f;
	private const float MaxZoom = 2.00f;
	private const float ZoomStep = 0.08f;
	private const float CameraPanSpeed = 720f;
	private const int FogRevealRadius = 4;

	private GlobalData _globalData;
	private MissionService _missionService;
	private MissionRuntimeState _missionState;
	private MissionUI _missionUi;
	private DialogueUI _dialogueUi;
	private Node2D _isoWorld;
	private Node2D _characterLayer;
	private Camera2D _camera;
	private MissionRoomBuilder _roomBuilder;
	private TextureRect _backgroundBackdrop;
	private Sprite2D _backgroundFeatureSprite;
	private readonly List<OfficerPawn> _officerPawns = new List<OfficerPawn>();
	private readonly HashSet<string> _consumedTriggerKeys = new HashSet<string>();
	private readonly HashSet<Vector2I> _exploredCells = new HashSet<Vector2I>();
	private readonly HashSet<Vector2I> _visibleCells = new HashSet<Vector2I>();
	private int _selectedOfficerIndex;
	private bool _isPanning;
	private Vector2 _lastMouseScreenPosition;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		_missionService = new MissionService(_globalData);
		_missionState = _missionService.GetCurrentMissionState();
		_isoWorld = GetNode<Node2D>("IsoWorld");
		_characterLayer = GetNode<Node2D>("IsoWorld/CharacterLayer");
		_camera = GetNode<Camera2D>("Camera2D");
		_roomBuilder = GetNode<MissionRoomBuilder>("IsoWorld/RoomBuilder");
		_missionUi = GetNode<MissionUI>("MissionUI");
		_dialogueUi = GetNode<DialogueUI>("DialogueUI");
		EnsureBackgroundNodes();

		if (_missionState == null || string.IsNullOrEmpty(_missionState.MissionID))
		{
			_missionState = _missionService.PrepareMission(MissionId, "res://exploration_battle.tscn", "Black Site Relay Beacon");
		}

		_roomBuilder?.BuildRoom();
		ApplyMissionBackground();
		ConfigureMissionView();
		SpawnMissionOfficers();
		UpdateFogOfWar();
		WireUi();
		WireDialogue();
		UpdateSelectedOfficerDisplay();
	}

	public override void _Process(double delta)
	{
		UpdateCameraPan((float)delta);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_dialogueUi != null && _dialogueUi.IsConversationOpen)
		{
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

			if (keyEvent.Keycode == Key.Key1)
			{
				SelectOfficer(0);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (keyEvent.Keycode == Key.Key2)
			{
				SelectOfficer(1);
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

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (TrySelectOfficerAtMouse())
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			OfficerPawn activeOfficer = GetSelectedOfficer();
			if (activeOfficer == null)
			{
				return;
			}

			TryMoveSelectedOfficer();
			GetViewport().SetInputAsHandled();
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
				Modulate = new Color(0.72f, 0.78f, 0.92f, 0.26f)
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
		_backgroundFeatureSprite.Modulate = definition.FeatureModulate;
		_backgroundFeatureSprite.Position = _roomBuilder.GetRoomCenterWorldPosition() + definition.FeatureOffset;
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

		PackedScene pawnScene = GD.Load<PackedScene>("res://officer_pawn.tscn");
		List<string> shipNames = _missionState?.ParticipatingShipNames ?? new List<string>();
		for (int i = 0; i < shipNames.Count && i < 2; i++)
		{
			OfficerState officer = _globalData?.ShipOfficers != null && _globalData.ShipOfficers.TryGetValue(shipNames[i], out OfficerState state)
				? state
				: null;
			if (officer == null)
			{
				continue;
			}

			OfficerPawn pawn = pawnScene.Instantiate<OfficerPawn>();
			pawn.SetOfficer(officer);
			pawn.EnteredCell += OnOfficerEnteredCell;
			_characterLayer.AddChild(pawn);
			PlaceOfficerAtSpawn(pawn, i);
			_officerPawns.Add(pawn);
		}

		SelectOfficer(0);
	}

	private void UpdateFogOfWar()
	{
		if (_roomBuilder == null)
		{
			return;
		}

		_visibleCells.Clear();
		foreach (OfficerPawn pawn in _officerPawns)
		{
			if (pawn == null)
			{
				continue;
			}

			foreach (Vector2I cell in _roomBuilder.GetReachableCells(pawn.CurrentCell, FogRevealRadius))
			{
				_visibleCells.Add(cell);
				_exploredCells.Add(cell);
			}
		}

		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/FloorLayer"));
		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/WallLayer"));
		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/PropLayer"));
	}

	private void ApplyFogToLayer(Node2D layer)
	{
		if (layer == null)
		{
			return;
		}

		foreach (Node child in layer.GetChildren())
		{
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

			if (_visibleCells.Contains(cell))
			{
				sprite.Modulate = baseColor;
			}
			else if (_exploredCells.Contains(cell))
			{
				sprite.Modulate = MultiplyColor(baseColor, 0.38f);
			}
			else
			{
				sprite.Modulate = MultiplyColor(baseColor, 0.08f);
			}
		}
	}

	private static Color MultiplyColor(Color color, float factor)
	{
		return new Color(
			Mathf.Clamp(color.R * factor, 0f, 1f),
			Mathf.Clamp(color.G * factor, 0f, 1f),
			Mathf.Clamp(color.B * factor, 0f, 1f),
			color.A);
	}

	private void WireUi()
	{
		if (_missionUi == null)
		{
			return;
		}

		string title = _missionState?.MissionTitle ?? "Away Mission";
		_missionUi.SetMissionText(
			title.ToUpper(),
			"OBJECTIVE: Investigate the relay, assess the survivors, and decide what to save.",
			"Controls: left click an officer to select, left click a floor tile to move, TAB or 1-2 to switch officers, middle mouse drag or WASD to pan, mouse wheel or +/- to zoom, ESC to return.");

		_missionUi.SaveSurvivorsButton.Pressed += () => CompleteMission(BuildOutcome("survivors_saved"));
		_missionUi.SecureArchiveButton.Pressed += () => CompleteMission(BuildOutcome("archive_secured"));
		_missionUi.ReturnButton.Pressed += ReturnWithoutOutcome;
	}

	private void WireDialogue()
	{
		if (_dialogueUi == null)
		{
			return;
		}

		_dialogueUi.ConversationEnded += OnMissionConversationEnded;
	}

	private void SelectOfficer(int index)
	{
		if (_officerPawns.Count == 0)
		{
			return;
		}

		_selectedOfficerIndex = Mathf.Clamp(index, 0, _officerPawns.Count - 1);
		for (int i = 0; i < _officerPawns.Count; i++)
		{
			_officerPawns[i].SetSelected(i == _selectedOfficerIndex);
		}

		UpdateSelectedOfficerDisplay();
	}

	private void CycleOfficerSelection()
	{
		if (_officerPawns.Count == 0)
		{
			return;
		}

		SelectOfficer((_selectedOfficerIndex + 1) % _officerPawns.Count);
	}

	private OfficerPawn GetSelectedOfficer()
	{
		return _selectedOfficerIndex >= 0 && _selectedOfficerIndex < _officerPawns.Count
			? _officerPawns[_selectedOfficerIndex]
			: null;
	}

	private void UpdateSelectedOfficerDisplay()
	{
		OfficerPawn activeOfficer = GetSelectedOfficer();
		if (activeOfficer == null || _missionUi == null)
		{
			return;
		}

		_missionUi.SetSelectedOfficer(activeOfficer.OfficerName, activeOfficer.ShipName, activeOfficer.Specialty);
	}

	private MissionOutcome BuildOutcome(string outcomeId)
	{
		MissionOutcome outcome = new MissionOutcome
		{
			MissionID = MissionId,
			OutcomeID = outcomeId,
			IsSuccess = true
		};

		List<OfficerState> officers = (_missionState?.ParticipatingShipNames ?? new List<string>())
			.Select(shipName => _globalData?.ShipOfficers != null && _globalData.ShipOfficers.TryGetValue(shipName, out OfficerState officer) ? officer : null)
			.Where(officer => officer != null)
			.ToList();

		if (outcomeId == "survivors_saved")
		{
			outcome.Reward.RawMaterials = 70;
			outcome.Reward.EnergyCores = 1;
			outcome.FlagsToSet.Add("relay_survivors_saved");

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
		if (_camera == null || _isPanning)
		{
			return;
		}

		Vector2 input = Vector2.Zero;
		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
		{
			input.Y -= 1f;
		}
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
		{
			input.Y += 1f;
		}
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
		{
			input.X -= 1f;
		}
		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
		{
			input.X += 1f;
		}

		if (input == Vector2.Zero)
		{
			return;
		}

		_camera.Position += input.Normalized() * CameraPanSpeed * delta * _camera.Zoom.X;
	}

	private void TryMoveSelectedOfficer()
	{
		OfficerPawn activeOfficer = GetSelectedOfficer();
		if (activeOfficer == null || _roomBuilder == null || _isoWorld == null)
		{
			return;
		}

		Vector2 localMousePosition = _isoWorld.ToLocal(GetGlobalMousePosition());
		Vector2I targetCell = _roomBuilder.GetNearestCell(localMousePosition);
		if (!_roomBuilder.TryGetPath(activeOfficer.CurrentCell, targetCell, out List<Vector2I> pathCells))
		{
			return;
		}

		if (pathCells.Count <= 1)
		{
			return;
		}

		List<Vector2> pathPoints = pathCells
			.Skip(1)
			.Select(GetCellGlobalPosition)
			.ToList();
		List<Vector2I> steppedCells = pathCells
			.Skip(1)
			.ToList();
		activeOfficer.MoveAlongPath(pathPoints, steppedCells, targetCell);
	}

	private void PlaceOfficerAtSpawn(OfficerPawn pawn, int spawnIndex)
	{
		if (pawn == null || _roomBuilder == null)
		{
			return;
		}

		string markerId = spawnIndex == 0 ? "spawn_a" : "spawn_b";
		Vector2I fallbackCell = spawnIndex == 0 ? new Vector2I(3, 5) : new Vector2I(4, 5);
		Vector2I spawnCell = fallbackCell;
		if (_roomBuilder.TryGetMarkerCell(markerId, out Vector2I markerCell) && _roomBuilder.IsWalkableCell(markerCell))
		{
			spawnCell = markerCell;
		}

		pawn.SetGridCell(spawnCell, GetCellGlobalPosition(spawnCell));
	}

	private Vector2 GetCellGlobalPosition(Vector2I cell)
	{
		Vector2 localPosition = _roomBuilder.GetCellWorldPosition(cell.X, cell.Y);
		return _isoWorld.ToGlobal(localPosition);
	}

	private bool TrySelectOfficerAtMouse()
	{
		Vector2 mousePosition = GetGlobalMousePosition();
		for (int i = 0; i < _officerPawns.Count; i++)
		{
			OfficerPawn pawn = _officerPawns[i];
			if (pawn == null)
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

		return false;
	}

	private void CheckDialogueTriggers(OfficerPawn officer)
	{
		if (officer == null || _roomBuilder == null || _dialogueUi == null || _dialogueUi.IsConversationOpen)
		{
			return;
		}

		foreach (MissionRoomBuilder.MarkerPlacement marker in _roomBuilder.GetMarkerPlacements())
		{
			if (marker.MarkerId != "trigger_dialogue")
			{
				continue;
			}

			if (!string.Equals(marker.TriggerMode, "enter", System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (marker.Cell != officer.CurrentCell)
			{
				continue;
			}

			if (!string.IsNullOrEmpty(marker.RequiredFlag) && (_globalData?.StoryFlags?.Contains(marker.RequiredFlag) != true))
			{
				continue;
			}

			string triggerKey = BuildTriggerKey(marker);
			if (marker.OneShot && _consumedTriggerKeys.Contains(triggerKey))
			{
				continue;
			}

			if (!string.IsNullOrEmpty(marker.SetFlag) && _globalData != null && !_globalData.StoryFlags.Contains(marker.SetFlag))
			{
				_globalData.StoryFlags.Add(marker.SetFlag);
			}

			if (marker.OneShot)
			{
				_consumedTriggerKeys.Add(triggerKey);
			}

			_dialogueUi.StartConversation(
				string.IsNullOrEmpty(marker.TargetId) ? marker.MarkerId : marker.TargetId,
				officer.OfficerName,
				officer.PortraitPath,
				marker.NpcPortraitPath);
			break;
		}
	}

	private void OnMissionConversationEnded()
	{
		UpdateSelectedOfficerDisplay();
	}

	private void OnOfficerEnteredCell(OfficerPawn pawn, Vector2I cell)
	{
		UpdateFogOfWar();
		CheckDialogueTriggers(pawn);
	}

	private static string BuildTriggerKey(MissionRoomBuilder.MarkerPlacement marker)
	{
		return $"{marker.MarkerId}:{marker.Cell.X},{marker.Cell.Y}:{marker.TargetId}";
	}
}
