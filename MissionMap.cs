using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap : Node2D
{
	private const string DefaultMissionId = "black_site_relay";
	private const float DefaultZoom = 0.52f;
	private const float MinZoom = 0.75f;
	private const float MaxZoom = 2.00f;
	private const float ZoomStep = 0.08f;
	private const float CameraPanSpeed = 720f;
	private const int FogRevealRadius = 4;

	private GlobalData _globalData;
	private MissionService _missionService;
	private MissionRuntimeState _missionState;
	private MissionTemplate _missionTemplate;
	private MissionUI _missionUi;
	private DialogueUI _dialogueUi;
	private Node2D _isoWorld;
	private Node2D _characterLayer;
	private Camera2D _camera;
	private MissionRoomBuilder _roomBuilder;
	private TextureRect _backgroundBackdrop;
	private Sprite2D _backgroundFeatureSprite;
	private Node2D _evacZoneLayer;
	private readonly List<OfficerPawn> _officerPawns = new List<OfficerPawn>();
	private readonly List<MissionNpcPawn> _missionNpcs = new List<MissionNpcPawn>();
	private readonly Dictionary<Vector2I, MissionNpcPawn> _missionNpcsByCell = new Dictionary<Vector2I, MissionNpcPawn>();
	private readonly Dictionary<Vector2I, MissionProp> _missionPropsByCell = new Dictionary<Vector2I, MissionProp>();
	private readonly Dictionary<string, MissionRoomBuilder.MarkerPlacement> _propPlacementsByInstanceId = new Dictionary<string, MissionRoomBuilder.MarkerPlacement>();
	private readonly MissionSpawner _missionSpawner = new MissionSpawner();
	private readonly HashSet<string> _consumedTriggerKeys = new HashSet<string>();
	private readonly HashSet<Vector2I> _exploredCells = new HashSet<Vector2I>();
	private readonly HashSet<Vector2I> _visibleCells = new HashSet<Vector2I>();
	private readonly List<Node2D> _evacZoneVisualRoots = new List<Node2D>();
	private readonly List<Polygon2D> _evacZoneHighlightPolygons = new List<Polygon2D>();
	private readonly List<Line2D> _evacZoneHighlightOutlines = new List<Line2D>();
	private int _selectedOfficerIndex;
	private bool _isPanning;
	private Vector2 _lastMouseScreenPosition;
	private string _pendingInteractionKey = string.Empty;
	private string _pendingInteractionOfficerId = string.Empty;
	private string _pendingPropInstanceId = string.Empty;
	private string _pendingNpcId = string.Empty;
	private float _evacPulseClock;

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
			_missionState = _missionService.PrepareMission(DefaultMissionId, "res://exploration_battle.tscn", "Black Site Relay Beacon");
		}

		_missionTemplate = _missionService.GetTemplate(GetActiveMissionId());
		ApplyMissionTemplateToRoomBuilder();

		_roomBuilder?.BuildRoom();
		SpawnMissionProps();
		ApplyMissionBackground();
		BuildEvacZoneHighlights();
		ConfigureMissionView();
		SpawnMissionNpcs();
		SpawnMissionOfficers();
		UpdateFogOfWar();
		WireUi();
		WireDialogue();
		UpdateSelectedOfficerDisplay();
	}

	public override void _Process(double delta)
	{
		UpdateCameraPan((float)delta);
		UpdateEvacZoneHighlightVisuals((float)delta);
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

			if (TryHandleInteractionClick(activeOfficer))
			{
				GetViewport().SetInputAsHandled();
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
			GetCellGlobalPosition,
			pawn =>
			{
				pawn.EnteredCell += OnOfficerEnteredCell;
				pawn.ReachedCell += OnOfficerReachedCell;
			}));

		SelectOfficer(0);
	}

	private void SpawnMissionNpcs()
	{
		_missionNpcs.Clear();
		_missionNpcsByCell.Clear();

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
			GetCellGlobalPosition));
		foreach (MissionNpcPawn npc in _missionNpcs.Where(npc => npc != null))
		{
			_missionNpcsByCell[npc.CurrentCell] = npc;
		}
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
			if (child is MissionDoor2D door)
			{
				Vector2I doorCell = door.Cell;
				Color fogColor = _visibleCells.Contains(doorCell)
					? Colors.White
					: (_exploredCells.Contains(doorCell) ? MultiplyColor(Colors.White, 0.38f) : MultiplyColor(Colors.White, 0.08f));
				door.SetVisualModulate(fogColor);
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

		_missionUi.SetMissionText(
			(_missionState?.MissionTitle ?? "Away Mission").ToUpper(),
			GetMissionObjectiveText(),
			GetMissionPromptText());
		_missionUi.ExtractionOutcomeChosen += OnExtractionOutcomeChosen;
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
		string missionId = GetActiveMissionId();
		MissionOutcome outcome = new MissionOutcome
		{
			MissionID = missionId,
			OutcomeID = outcomeId,
			IsSuccess = true
		};

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
		return string.IsNullOrWhiteSpace(_missionTemplate?.PromptText)
			? "Controls: left click an officer to select, left click a floor tile to move, TAB or 1-2 to switch officers, middle mouse drag or WASD to pan, mouse wheel or +/- to zoom."
			: _missionTemplate.PromptText;
	}

	private string GetMissionPromptText()
	{
		string basePrompt = GetBaseMissionPromptText();
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
			return AreRequiredFlagsSatisfied(_missionTemplate?.PrimaryOutcomeRequiredFlags);
		}

		if (outcomeId == GetSecondaryOutcomeId())
		{
			return AreRequiredFlagsSatisfied(_missionTemplate?.SecondaryOutcomeRequiredFlags);
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
		TryMoveOfficerToCell(activeOfficer, targetCell);
	}

	private bool TryMoveOfficerToCell(OfficerPawn officer, Vector2I targetCell)
	{
		if (officer == null || _roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		if (!_roomBuilder.TryGetPath(officer.CurrentCell, targetCell, out List<Vector2I> pathCells))
		{
			return false;
		}

		if (pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2> pathPoints = pathCells
			.Skip(1)
			.Select(GetCellGlobalPosition)
			.ToList();
		List<Vector2I> steppedCells = pathCells
			.Skip(1)
			.ToList();
		officer.MoveAlongPath(pathPoints, steppedCells, targetCell);
		return true;
	}

	private bool TryHandleInteractionClick(OfficerPawn officer)
	{
		if (officer == null || _roomBuilder == null || _isoWorld == null || (_dialogueUi?.IsConversationOpen ?? false))
		{
			return false;
		}

		Vector2I clickedCell = _roomBuilder.GetNearestCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		if (TryHandleNpcInteractionClick(officer, clickedCell))
		{
			return true;
		}

		if (TryHandlePropInteractionClick(officer, clickedCell))
		{
			return true;
		}

		List<MissionRoomBuilder.MarkerPlacement> interactions = _roomBuilder.GetInteractPlacementsAtCell(clickedCell)
			.Where(placement => string.Equals(placement.TriggerMode, "interact", System.StringComparison.OrdinalIgnoreCase)
				|| placement.LogicRole == "door"
				|| placement.LogicRole == "terminal")
			.Where(placement => !HasRuntimePropForPlacement(placement))
			.ToList();
		if (interactions.Count == 0)
		{
			return false;
		}

		MissionRoomBuilder.MarkerPlacement interaction = interactions
			.OrderByDescending(placement => placement.LogicRole == "door")
			.ThenByDescending(placement => placement.LogicRole == "terminal")
			.First();

		if (CanOfficerExecuteInteraction(officer, interaction))
		{
			ExecuteInteraction(officer, interaction);
			return true;
		}

		Vector2I? approachCell = FindBestInteractionApproachCell(officer, interaction);
		if (approachCell.HasValue && TryMoveOfficerToCell(officer, approachCell.Value))
		{
			_pendingInteractionKey = BuildInteractionKey(interaction);
			_pendingInteractionOfficerId = officer.OfficerID;
			return true;
		}

		return true;
	}

	private bool TryHandlePropInteractionClick(OfficerPawn officer, Vector2I clickedCell)
	{
		if (!_missionPropsByCell.TryGetValue(clickedCell, out MissionProp prop) || prop == null)
		{
			return false;
		}

		if (CanOfficerExecutePropInteraction(officer, prop))
		{
			ExecutePropInteraction(officer, prop);
			return true;
		}

		if (TryMoveOfficerToCell(officer, clickedCell))
		{
			_pendingPropInstanceId = prop.PropInstanceId;
			_pendingInteractionOfficerId = officer.OfficerID;
		}

		return true;
	}

	private bool TryHandleNpcInteractionClick(OfficerPawn officer, Vector2I clickedCell)
	{
		if (!_missionNpcsByCell.TryGetValue(clickedCell, out MissionNpcPawn npc) || npc == null)
		{
			return false;
		}

		if (CanOfficerExecuteNpcInteraction(officer, npc))
		{
			ExecuteNpcInteraction(officer, npc);
			return true;
		}

		Vector2I? approachCell = FindBestNpcApproachCell(officer, npc);
		if (approachCell.HasValue && TryMoveOfficerToCell(officer, approachCell.Value))
		{
			_pendingNpcId = npc.NpcId;
			_pendingInteractionOfficerId = officer.OfficerID;
		}

		return true;
	}

	private bool CanOfficerExecuteInteraction(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction)
	{
		return GetInteractionDistance(officer.CurrentCell, interaction) <= 1;
	}

	private int GetInteractionDistance(Vector2I officerCell, MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction.LogicRole == "terminal")
		{
			return Mathf.Abs(officerCell.X - interaction.Cell.X) + Mathf.Abs(officerCell.Y - interaction.Cell.Y);
		}

		if (interaction.LogicRole == "door")
		{
			return Mathf.Abs(officerCell.X - interaction.Cell.X) + Mathf.Abs(officerCell.Y - interaction.Cell.Y);
		}

		return officerCell == interaction.Cell ? 0 : int.MaxValue;
	}

	private Vector2I? FindBestInteractionApproachCell(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (_roomBuilder == null)
		{
			return null;
		}

		List<Vector2I> candidates = new List<Vector2I>();
		if (interaction.LogicRole == "terminal")
		{
			if (_roomBuilder.IsWalkableCell(interaction.Cell))
			{
				candidates.Add(interaction.Cell);
			}
		}

		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};
		foreach (Vector2I direction in directions)
		{
			Vector2I candidate = interaction.Cell + direction;
			if (_roomBuilder.IsWalkableCell(candidate))
			{
				candidates.Add(candidate);
			}
		}

		foreach (Vector2I candidate in candidates.Distinct())
		{
			if (_roomBuilder.TryGetPath(officer.CurrentCell, candidate, out List<Vector2I> _))
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
			UpdateMissionCompletionActions();
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			return;
		}

		if (interaction.LogicRole == "door")
		{
			ToggleDoorInteraction(interaction);
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
			if (!string.IsNullOrEmpty(interaction.TargetId))
			{
				bool nextOpenState = !_roomBuilder.IsDoorOpen(interaction.TargetId);
				_roomBuilder.TrySetDoorOpen(interaction.TargetId, nextOpenState, true);
				UpdateFogOfWar();
			}
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			UpdateMissionCompletionActions();
			return;
		}

		if (interaction.MarkerId == "trigger_dialogue")
		{
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

		Vector2I propCell = GetPropCell(prop);
		int interactionRange = Mathf.Max(1, prop.Definition?.InteractionRange ?? 1);
		int distance = Mathf.Abs(officer.CurrentCell.X - propCell.X) + Mathf.Abs(officer.CurrentCell.Y - propCell.Y);
		return distance <= interactionRange;
	}

	private bool CanOfficerExecuteNpcInteraction(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null)
		{
			return false;
		}

		int interactionRange = Mathf.Max(1, npc.InteractionRange);
		int distance = Mathf.Abs(officer.CurrentCell.X - npc.CurrentCell.X) + Mathf.Abs(officer.CurrentCell.Y - npc.CurrentCell.Y);
		return distance <= interactionRange;
	}

	private Vector2I? FindBestNpcApproachCell(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null || _roomBuilder == null)
		{
			return null;
		}

		List<Vector2I> candidates = new List<Vector2I>();
		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};
		foreach (Vector2I direction in directions)
		{
			Vector2I candidate = npc.CurrentCell + direction;
			if (_roomBuilder.IsWalkableCell(candidate))
			{
				candidates.Add(candidate);
			}
		}

		foreach (Vector2I candidate in candidates.Distinct())
		{
			if (_roomBuilder.TryGetPath(officer.CurrentCell, candidate, out List<Vector2I> _))
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

		PropInteractionContext context = BuildPropInteractionContext(officer, prop);
		PropInteractionResult result = prop.Interact(context);
		if (result == null || !result.Success)
		{
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

		PropInteractionContext context = new PropInteractionContext
		{
			MissionMap = this,
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			DialogueUI = _dialogueUi,
			Officer = officer,
			TargetCell = npc.CurrentCell,
			PropInstanceId = npc.NpcId,
			SourceInteractionKey = _missionState?.SourceInteractionKey ?? string.Empty,
			NpcPortraitPath = npc.PortraitPath ?? string.Empty
		};
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
		if (!nextOpenState && _officerPawns.Any(pawn => pawn.CurrentCell == interaction.Cell))
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

			if (marker.Cell != officer.CurrentCell)
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

		if (pawn == null || pawn.OfficerID != _pendingInteractionOfficerId)
		{
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
		}
	}

	private void SpawnMissionProps()
	{
		_missionPropsByCell.Clear();
		_propPlacementsByInstanceId.Clear();
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
			runtimePropLayer.AddChild(prop);
			_missionPropsByCell[placement.Cell] = prop;
			_propPlacementsByInstanceId[prop.PropInstanceId] = placement;
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

	private void ApplyPropInteractionResult(MissionProp prop, PropInteractionResult result, PropInteractionContext context)
	{
		if (prop == null || result == null || !result.Success)
		{
			return;
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

	private void ApplyNpcInteractionResult(MissionNpcPawn npc, PropInteractionResult result, PropInteractionContext context)
	{
		if (npc == null || result == null || !result.Success)
		{
			return;
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

	private bool AreAllOfficersOnEvacZone()
	{
		if (_officerPawns.Count == 0 || _roomBuilder == null)
		{
			return false;
		}

		HashSet<Vector2I> evacCells = _roomBuilder.GetMarkerPlacements()
			.Where(marker => marker.MarkerId == "evac_zone")
			.Select(marker => marker.Cell)
			.ToHashSet();
		if (evacCells.Count == 0)
		{
			return false;
		}

		return _officerPawns.All(pawn => pawn != null && evacCells.Contains(pawn.CurrentCell));
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
