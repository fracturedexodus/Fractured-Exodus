using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap : Node2D
{
	private const string MissionId = "black_site_relay";
	private const float DefaultZoom = 0.52f;
	private const float MinZoom = 0.34f;
	private const float MaxZoom = 1.00f;
	private const float ZoomStep = 0.08f;

	private GlobalData _globalData;
	private MissionService _missionService;
	private MissionRuntimeState _missionState;
	private MissionUI _missionUi;
	private Node2D _characterLayer;
	private Camera2D _camera;
	private MissionRoomBuilder _roomBuilder;
	private readonly List<Node2D> _zoneMarkers = new List<Node2D>();
	private readonly List<Vector2> _zoneMarkerBasePositions = new List<Vector2>();
	private readonly List<OfficerPawn> _officerPawns = new List<OfficerPawn>();
	private int _selectedOfficerIndex;
	private float _markerPulseTime;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		_missionService = new MissionService(_globalData);
		_missionState = _missionService.GetCurrentMissionState();
		_characterLayer = GetNode<Node2D>("IsoWorld/CharacterLayer");
		_camera = GetNode<Camera2D>("Camera2D");
		_roomBuilder = GetNode<MissionRoomBuilder>("IsoWorld/RoomBuilder");
		_missionUi = GetNode<MissionUI>("MissionUI");

		if (_missionState == null || string.IsNullOrEmpty(_missionState.MissionID))
		{
			_missionState = _missionService.PrepareMission(MissionId, "res://exploration_battle.tscn", "Black Site Relay Beacon");
		}

		ConfigureMissionView();
		ConfigureMissionAnchors();
		CacheZoneMarkers();
		SpawnMissionOfficers();
		WireUi();
		UpdateSelectedOfficerDisplay();
	}

	public override void _Process(double delta)
	{
		_markerPulseTime += (float)delta;
		for (int i = 0; i < _zoneMarkers.Count; i++)
		{
			Node2D marker = _zoneMarkers[i];
			float pulse = Mathf.Sin((_markerPulseTime * 2.2f) + (i * 0.8f));
			float scale = 1.0f + (0.06f * pulse);
			marker.Scale = new Vector2(scale, scale);
			marker.Position = _zoneMarkerBasePositions[i] + new Vector2(0f, pulse * -3.5f);
			marker.Modulate = new Color(1f, 1f, 1f, 0.9f + (0.1f * ((pulse + 1f) * 0.5f)));
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
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
			OfficerPawn activeOfficer = GetSelectedOfficer();
			if (activeOfficer == null)
			{
				return;
			}

			activeOfficer.MoveTo(GetGlobalMousePosition());
			GetViewport().SetInputAsHandled();
		}
	}

	private void ConfigureMissionView()
	{
		if (_camera != null)
		{
			_camera.Position = new Vector2(0f, 58f);
			ApplyZoom(DefaultZoom);
		}
	}

	private void CacheZoneMarkers()
	{
		_zoneMarkers.Clear();
		_zoneMarkerBasePositions.Clear();

		string[] markerPaths =
		{
			"IsoWorld/MarkerLayer/SurvivorMarker",
			"IsoWorld/MarkerLayer/PowerMarker",
			"IsoWorld/MarkerLayer/ArchiveMarker"
		};

		foreach (string markerPath in markerPaths)
		{
			Node2D marker = GetNodeOrNull<Node2D>(markerPath);
			if (marker == null)
			{
				continue;
			}

			_zoneMarkers.Add(marker);
			_zoneMarkerBasePositions.Add(marker.Position);
		}
	}

	private void ConfigureMissionAnchors()
	{
		if (_roomBuilder == null)
		{
			return;
		}

		Marker2D spawnPointA = GetNodeOrNull<Marker2D>("IsoWorld/SpawnPointA");
		Marker2D spawnPointB = GetNodeOrNull<Marker2D>("IsoWorld/SpawnPointB");
		Node2D survivorMarker = GetNodeOrNull<Node2D>("IsoWorld/MarkerLayer/SurvivorMarker");
		Node2D powerMarker = GetNodeOrNull<Node2D>("IsoWorld/MarkerLayer/PowerMarker");
		Node2D archiveMarker = GetNodeOrNull<Node2D>("IsoWorld/MarkerLayer/ArchiveMarker");

		ApplyMarkerPosition(spawnPointA, "spawn_a", 3, 5, new Vector2(-16f, 10f));
		ApplyMarkerPosition(spawnPointB, "spawn_b", 4, 5, new Vector2(24f, 10f));
		ApplyMarkerPosition(survivorMarker, "objective_survivors", 1, 3, new Vector2(-12f, -72f));
		ApplyMarkerPosition(powerMarker, "objective_power", 4, 4, new Vector2(0f, -70f));
		ApplyMarkerPosition(archiveMarker, "objective_archive", 7, 3, new Vector2(12f, -72f));
	}

	private void ApplyMarkerPosition(Node2D marker, string markerId, int fallbackColumn, int fallbackRow, Vector2 fallbackOffset)
	{
		if (marker == null || _roomBuilder == null)
		{
			return;
		}

		if (_roomBuilder.TryGetMarkerWorldPosition(markerId, fallbackOffset, out Vector2 savedPosition))
		{
			marker.Position = savedPosition;
			return;
		}

		marker.Position = _roomBuilder.GetCellWorldPosition(fallbackColumn, fallbackRow, fallbackOffset);
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
		List<Marker2D> spawnPoints = new List<Marker2D>
		{
			GetNode<Marker2D>("IsoWorld/SpawnPointA"),
			GetNode<Marker2D>("IsoWorld/SpawnPointB")
		};

		PackedScene pawnScene = GD.Load<PackedScene>("res://officer_pawn.tscn");
		List<string> shipNames = _missionState?.ParticipatingShipNames ?? new List<string>();
		for (int i = 0; i < shipNames.Count && i < spawnPoints.Count; i++)
		{
			OfficerState officer = _globalData?.ShipOfficers != null && _globalData.ShipOfficers.TryGetValue(shipNames[i], out OfficerState state)
				? state
				: null;
			if (officer == null)
			{
				continue;
			}

			OfficerPawn pawn = pawnScene.Instantiate<OfficerPawn>();
			pawn.GlobalPosition = spawnPoints[i].GlobalPosition;
			pawn.SetOfficer(officer);
			_characterLayer.AddChild(pawn);
			_officerPawns.Add(pawn);
		}

		SelectOfficer(0);
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
			"Controls: left click to move, TAB or 1-2 to switch officers, mouse wheel or +/- to zoom, ESC to return.");

		_missionUi.SaveSurvivorsButton.Pressed += () => CompleteMission(BuildOutcome("survivors_saved"));
		_missionUi.SecureArchiveButton.Pressed += () => CompleteMission(BuildOutcome("archive_secured"));
		_missionUi.ReturnButton.Pressed += ReturnWithoutOutcome;
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
}
