using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CyberCityPrototype : Node2D
{
	private const string TileRoot = "res://Assets/Missions/CyberCityPrototype/Tiles/";
	private const string SampleMaleFrameRoot = "res://Assets/Missions/CyberCityPrototype/Characters/SampleMale/Frames/";
	private const float HalfTileWidth = 30f;
	private const float HalfTileHeight = 17.5f;
	private const float RaisedWallHeight = 48f;

	// Faithful transcription of Data/MissionLayouts/black_site_relay_builder.json.
	// . = standard floor, g = grated service floor, * = illuminated medical floor.
	private static readonly string[] FloorLayout =
	{
		"         ...      ",
		" ...........      ",
		" .       ........ ",
		" .        .     . ",
		" .        .     . ",
		" .        .     . ",
		" .        .     . ",
		" .        .    ***",
		" .        .    ***",
		" .        .    ***",
		" .        .     . ",
		" *        .     . ",
		" *        .     . ",
		"ggg .     .     . ",
		"ggg...........  . ",
		"ggg    ..    .  ..",
		"       ..    .   .",
		"     gg..    .   .",
		"     gg..    .....",
		"     gg..         "
	};

	private static readonly Vector2I[] DoorCells =
	{
		new Vector2I(3, 14), new Vector2I(10, 14), new Vector2I(16, 3), new Vector2I(10, 3),
		new Vector2I(11, 2), new Vector2I(8, 1), new Vector2I(1, 13), new Vector2I(16, 10)
	};

	private static readonly Vector2 MapOrigin = new Vector2(30f, -315f);
	private static readonly Vector2 CellColumnBasis = new Vector2(HalfTileWidth, HalfTileHeight);
	private static readonly Vector2 CellRowBasis = new Vector2(-HalfTileWidth, HalfTileHeight);

	private readonly Dictionary<Vector2I, char> _floorTypes = new();
	private Camera2D _camera;
	private Node2D _floorLayer;
	private Node2D _wallLayer;
	private Node2D _gridLayer;
	private Node2D _sortedLayer;
	private Node2D _markerLayer;
	private Polygon2D _selectedCellFill;
	private Line2D _selectedCellOutline;
	private Node2D _operative;
	private AnimatedSprite2D _sampleMaleSprite;
	private Label _statusLabel;
	private Vector2I _selectedCell = new Vector2I(7, 19);
	private Tween _moveTween;
	private int _facingDirection = 8;
	private bool _isAttacking;

	public override void _Ready()
	{
		RenderingServer.SetDefaultClearColor(new Color("050b12"));
		BuildBackdrop();
		BuildCameraAndWorld();
		BuildMissionMap();
		BuildInterface();
		SelectCell(_selectedCell, false);
		if (OS.GetCmdlineUserArgs().Contains("--capture-cybercity")) CapturePrototypeAndQuit();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				Vector2I cell = FindNearestCell(GetGlobalMousePosition());
				if (IsWalkable(cell))
				{
					SelectCell(cell, true);
					GetViewport().SetInputAsHandled();
				}
			}
			else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				SetCameraZoom(_camera.Zoom.X + 0.08f);
				GetViewport().SetInputAsHandled();
			}
			else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				SetCameraZoom(_camera.Zoom.X - 0.08f);
				GetViewport().SetInputAsHandled();
			}
		}

		if (inputEvent is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo) return;
		if (keyEvent.Keycode == Key.Space)
		{
			PlayAttackAnimation();
			GetViewport().SetInputAsHandled();
			return;
		}

		Vector2I delta = keyEvent.Keycode switch
		{
			Key.W or Key.Up => new Vector2I(0, -1),
			Key.S or Key.Down => new Vector2I(0, 1),
			Key.A or Key.Left => new Vector2I(-1, 0),
			Key.D or Key.Right => new Vector2I(1, 0),
			_ => Vector2I.Zero
		};
		if (delta != Vector2I.Zero)
		{
			Vector2I next = _selectedCell + delta;
			if (IsWalkable(next)) SelectCell(next, true);
			GetViewport().SetInputAsHandled();
		}
		else if (keyEvent.Keycode == Key.Escape)
		{
			GetTree().ChangeSceneToFile("res://main_menu.tscn");
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildBackdrop()
	{
		CanvasLayer backdrop = new CanvasLayer { Name = "Backdrop", Layer = -20 };
		AddChild(backdrop);
		backdrop.AddChild(new ColorRect
		{
			Name = "Background", Position = Vector2.Zero, Size = new Vector2(1920f, 1080f),
			Color = new Color("050b12"), MouseFilter = Control.MouseFilterEnum.Ignore
		});
		for (int band = 0; band < 5; band++)
		{
			backdrop.AddChild(new ColorRect
			{
				Position = new Vector2(0f, 260f + (band * 142f)), Size = new Vector2(1920f, 1f),
				Color = new Color(0.08f, 0.77f, 0.91f, 0.07f), MouseFilter = Control.MouseFilterEnum.Ignore
			});
		}
	}

	private void BuildCameraAndWorld()
	{
		_camera = new Camera2D { Name = "Camera2D", Position = new Vector2(0f, 38f), Zoom = new Vector2(1.10f, 1.10f) };
		AddChild(_camera);
		_floorLayer = new Node2D { Name = "FloorLayer", ZIndex = -100 };
		_wallLayer = new Node2D { Name = "WallLayer", ZIndex = -30 };
		_gridLayer = new Node2D { Name = "GridLayer", ZIndex = -20 };
		_markerLayer = new Node2D { Name = "MarkerLayer", ZIndex = -10 };
		_sortedLayer = new Node2D { Name = "SortedLayer", YSortEnabled = true };
		AddChild(_floorLayer);
		AddChild(_wallLayer);
		AddChild(_gridLayer);
		AddChild(_markerLayer);
		AddChild(_sortedLayer);
	}

	private void BuildMissionMap()
	{
		Texture2D groundTexture = LoadTexture(TileRoot + "Ground_Tile_CyberCityCore_3.png");
		for (int row = 0; row < FloorLayout.Length; row++)
		{
			for (int column = 0; column < FloorLayout[row].Length; column++)
			{
				char floorType = FloorLayout[row][column];
				if (floorType == ' ') continue;
				Vector2I cell = new Vector2I(column, row);
				_floorTypes[cell] = floorType;
				AddFloorCell(cell, floorType, groundTexture);
			}
		}
		BuildFacilityWalls();
		BuildGridAndSelection();
		BuildMissionMarkers();
		BuildDoors();
		BuildSetDressing();
		BuildActors();
	}

	private void AddFloorCell(Vector2I cell, char floorType, Texture2D texture)
	{
		Vector2[] polygon = GetCellPolygon(cell);
		_floorLayer.AddChild(new Polygon2D
		{
			Polygon = polygon.Select(point => point + new Vector2(0f, 8f)).ToArray(),
			Color = new Color(0.03f, 0.68f, 0.82f, 0.12f)
		});
		Color tint = floorType switch
		{
			'g' => new Color(0.47f, 0.57f, 0.60f, 1f),
			'*' => new Color(0.30f, 0.83f, 0.91f, 1f),
			_ => new Color(0.50f, 0.62f, 0.67f, 1f)
		};
		float textureWidth = texture.GetWidth();
		float textureHeight = texture.GetHeight();
		_floorLayer.AddChild(new Polygon2D
		{
			Name = $"Floor_{cell.X}_{cell.Y}", Polygon = polygon, Texture = texture,
			UV = new[]
			{
				new Vector2(textureWidth * 0.5f, 0f), new Vector2(textureWidth, textureHeight * 0.5f),
				new Vector2(textureWidth * 0.5f, textureHeight), new Vector2(0f, textureHeight * 0.5f)
			},
			Color = tint
		});
		if (floorType == 'g')
		{
			_floorLayer.AddChild(new Line2D
			{
				Points = new[] { polygon[3].Lerp(polygon[0], 0.35f), polygon[1].Lerp(polygon[2], 0.65f) },
				Width = 2.0f, DefaultColor = new Color(0.95f, 0.65f, 0.18f, 0.52f), Antialiased = true
			});
		}
	}

	private void BuildFacilityWalls()
	{
		foreach (Vector2I cell in _floorTypes.Keys)
		{
			Vector2[] polygon = GetCellPolygon(cell);
			if (!IsWalkable(cell + Vector2I.Left)) AddRaisedWall(polygon[0], polygon[3], cell, "NW");
			if (!IsWalkable(cell + Vector2I.Up)) AddRaisedWall(polygon[1], polygon[0], cell, "NE");
			Color rim = new Color(0.08f, 0.69f, 0.77f, 0.46f);
			if (!IsWalkable(cell + Vector2I.Right)) AddEdgeLine(polygon[1], polygon[2], rim, 3.2f);
			if (!IsWalkable(cell + Vector2I.Down)) AddEdgeLine(polygon[2], polygon[3], rim, 3.2f);
		}
	}

	private void AddRaisedWall(Vector2 edgeStart, Vector2 edgeEnd, Vector2I cell, string direction)
	{
		Vector2 rise = new Vector2(0f, -RaisedWallHeight);
		_wallLayer.AddChild(new Polygon2D
		{
			Name = $"Wall_{direction}_{cell.X}_{cell.Y}",
			Polygon = new[] { edgeStart, edgeEnd, edgeEnd + rise, edgeStart + rise },
			Color = direction == "NW" ? new Color(0.055f, 0.105f, 0.125f, 0.98f) : new Color(0.07f, 0.14f, 0.16f, 0.98f)
		});
		AddEdgeLine(edgeStart + rise, edgeEnd + rise, new Color(0.20f, 0.91f, 1f, 0.62f), 2f, _wallLayer);
		AddEdgeLine(edgeStart, edgeEnd, new Color(0.03f, 0.25f, 0.30f, 0.90f), 2f, _wallLayer);
	}

	private void AddEdgeLine(Vector2 start, Vector2 end, Color color, float width, Node parent = null)
	{
		Line2D line = new Line2D { Points = new[] { start, end }, Width = width, DefaultColor = color, Antialiased = true };
		(parent ?? _gridLayer).AddChild(line);
	}

	private void BuildGridAndSelection()
	{
		foreach (Vector2I cell in _floorTypes.Keys)
		{
			_gridLayer.AddChild(new Line2D
			{
				Name = $"Cell_{cell.X}_{cell.Y}", Points = GetCellPolygon(cell), Closed = true, Width = 0.9f,
				DefaultColor = new Color(0.25f, 0.90f, 1f, 0.14f), Antialiased = true
			});
		}
		_selectedCellFill = new Polygon2D { Name = "SelectedCellFill", Color = new Color(0.10f, 0.89f, 1f, 0.27f) };
		_gridLayer.AddChild(_selectedCellFill);
		_selectedCellOutline = new Line2D
		{
			Name = "SelectedCellOutline", Closed = true, Width = 2.4f,
			DefaultColor = new Color(0.40f, 0.98f, 1f, 0.95f), Antialiased = true
		};
		_gridLayer.AddChild(_selectedCellOutline);
	}

	private void BuildMissionMarkers()
	{
		AddCellMarker(new Vector2I(10, 0), new Color(0.92f, 0.55f, 0.12f, 0.42f), "ARCHIVE CORE", new Vector2(0f, -84f));
		AddCellMarker(new Vector2I(0, 13), new Color(0.22f, 0.92f, 0.77f, 0.42f), "SURVIVOR PODS", new Vector2(-22f, -84f));
		AddCellMarker(new Vector2I(16, 8), new Color(0.21f, 1f, 0.53f, 0.40f), "EVAC", new Vector2(0f, -65f));
		AddCellMarker(new Vector2I(5, 18), new Color(0.35f, 0.78f, 1f, 0.33f), "UPLINK", new Vector2(0f, -66f));
		AddCellMarker(new Vector2I(7, 19), new Color(0.22f, 0.86f, 1f, 0.30f), "INSERTION", new Vector2(-45f, 34f));
	}

	private void AddCellMarker(Vector2I cell, Color color, string text, Vector2 labelOffset)
	{
		_markerLayer.AddChild(new Polygon2D { Name = text.Replace(" ", string.Empty) + "Marker", Polygon = GetCellPolygon(cell), Color = color });
		_markerLayer.AddChild(new Line2D
		{
			Points = GetCellPolygon(cell), Closed = true, Width = 2.2f,
			DefaultColor = new Color(color.R, color.G, color.B, 0.92f), Antialiased = true
		});
		AddWorldLabel(text, CellToWorld(cell) + labelOffset, new Color(color.R, color.G, color.B, 1f), 12);
	}

	private void BuildDoors()
	{
		for (int index = 0; index < DoorCells.Length; index++)
		{
			Vector2I cell = DoorCells[index];
			string texture = index == 0 ? "DoorGarage2_4.png" : "DoorGarage2_3.png";
			Node2D door = AddSortedSprite(TileRoot + texture, cell, $"Bulkhead_{cell.X}_{cell.Y}", 0.45f);
			door.Position += new Vector2(0f, 2f);
			AddCellDiamond(cell, new Color(1f, 0.56f, 0.12f, 0.22f), _markerLayer);
		}
	}

	private void BuildSetDressing()
	{
		AddSortedSprite(TileRoot + "Bed3_3.png", new Vector2I(0, 13), "CryoPodA", 0.52f);
		AddSortedSprite(TileRoot + "Bed3_3.png", new Vector2I(0, 14), "CryoPodB", 0.48f);
		AddSortedSprite(TileRoot + "Bed3_3.png", new Vector2I(1, 14), "CryoPodC", 0.48f);
		AddSortedSprite(TileRoot + "Generator1_3.png", new Vector2I(10, 0), "ArchiveCore", 0.62f);
		AddSortedSprite(TileRoot + "Terminal1_3.png", new Vector2I(11, 0), "ArchiveTerminal", 0.78f);
		AddSortedSprite(TileRoot + "Bed1_3.png", new Vector2I(15, 7), "MedicalBedA", 0.46f);
		AddSortedSprite(TileRoot + "Bed1_3.png", new Vector2I(15, 8), "MedicalBedB", 0.46f);
		AddSortedSprite(TileRoot + "Bed1_3.png", new Vector2I(15, 9), "MedicalBedC", 0.46f);
		AddSortedSprite(TileRoot + "Terminal2_3.png", new Vector2I(5, 18), "DialogueUplink", 0.52f);
		AddSortedSprite(TileRoot + "CrateStack1_3.png", new Vector2I(2, 13), "WestDebris", 0.42f);
		AddSortedSprite(TileRoot + "Barrels1_3.png", new Vector2I(2, 15), "SouthDebris", 0.50f);
		AddSortedSprite(TileRoot + "VendingMachine1_1.png", new Vector2I(17, 9), "MedicalSupplyCabinet", 0.42f);
		AddSortedSprite(TileRoot + "CeilingLight1_3.png", new Vector2I(9, 14), "RelayLight", 0.72f);
	}

	private void BuildActors()
	{
		_operative = AddAnimatedSampleMale(new Vector2I(7, 19), "SampleMaleOperative", new Color(0.18f, 0.92f, 1f, 0.42f));
		AddStaticSampleMale(new Vector2I(8, 19), "SecondOfficer");
		AddActor("res://Assets/Missions/Characters/PortraitMatched/Hostiles/relay_scavenger_brute/relay_scavenger_brute_se.png", new Vector2I(0, 15), "Scavenger Brute", new Color(0.94f, 0.54f, 0.18f, 0.42f), 0.074f);
		AddActor("res://Assets/Missions/Characters/PortraitMatched/Hostiles/relay_custodian_sentry/relay_custodian_sentry_se.png", new Vector2I(9, 0), "Custodian Sentry", new Color(0.23f, 0.75f, 1f, 0.42f), 0.071f);
		AddActor("res://Assets/Missions/Characters/PortraitMatched/Hostiles/relay_scavenger_raider/relay_scavenger_raider_se.png", new Vector2I(17, 7), "Scavenger Raider", new Color(1f, 0.26f, 0.20f, 0.42f), 0.071f);
		AddActor("res://Assets/Missions/Characters/PortraitMatched/Hostiles/relay_scavenger_raider/relay_scavenger_raider_sw.png", new Vector2I(7, 14), "Relay Ambusher", new Color(1f, 0.26f, 0.20f, 0.42f), 0.067f);
	}

	private Node2D AddAnimatedSampleMale(Vector2I cell, string name, Color ringColor)
	{
		Node2D actor = new Node2D { Name = name, Position = CellToWorld(cell) };
		_sortedLayer.AddChild(actor);
		actor.AddChild(CreateActorRing(ringColor));
		_sampleMaleSprite = new AnimatedSprite2D
		{
			Name = "AnimatedVisual", SpriteFrames = BuildSampleMaleSpriteFrames(),
			Position = new Vector2(0f, -34f), Scale = new Vector2(0.72f, 0.72f)
		};
		_sampleMaleSprite.AnimationFinished += OnSampleMaleAnimationFinished;
		actor.AddChild(_sampleMaleSprite);
		PlayIdleAnimation();
		return actor;
	}

	private void AddStaticSampleMale(Vector2I cell, string name)
	{
		Node2D actor = new Node2D { Name = name, Position = CellToWorld(cell) };
		_sortedLayer.AddChild(actor);
		actor.AddChild(CreateActorRing(new Color(0.30f, 0.62f, 1f, 0.30f)));
		actor.AddChild(new Sprite2D
		{
			Texture = LoadTexture(SampleMaleFrameRoot + "SampleMale_Unarmed Idle_36.png"),
			Position = new Vector2(0f, -34f), Scale = new Vector2(0.72f, 0.72f),
			Modulate = new Color(0.72f, 0.86f, 1f, 1f)
		});
	}

	private static Polygon2D CreateActorRing(Color ringColor) => new Polygon2D
	{
		Name = "SelectionRing",
		Polygon = new[] { new Vector2(0f, -7f), new Vector2(22f, 0f), new Vector2(0f, 7f), new Vector2(-22f, 0f) },
		Color = ringColor, ZIndex = -1
	};

	private static SpriteFrames BuildSampleMaleSpriteFrames()
	{
		SpriteFrames frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		AddDirectionalAnimationSet(frames, "idle", "SampleMale_Unarmed Idle", 5, 6f, true);
		AddDirectionalAnimationSet(frames, "walk", "SampleMale_Walking", 8, 10f, true);
		AddDirectionalAnimationSet(frames, "attack", "SampleMale_Unarmed Attack 1", 5, 12f, false);
		return frames;
	}

	private static void AddDirectionalAnimationSet(SpriteFrames spriteFrames, string animationPrefix, string filePrefix, int framesPerDirection, float framesPerSecond, bool loop)
	{
		for (int direction = 1; direction <= 8; direction++)
		{
			StringName animationName = $"{animationPrefix}_{direction}";
			spriteFrames.AddAnimation(animationName);
			spriteFrames.SetAnimationSpeed(animationName, framesPerSecond);
#pragma warning disable CS0618
			spriteFrames.SetAnimationLoop(animationName, loop);
#pragma warning restore CS0618
			int firstFrame = ((direction - 1) * framesPerDirection) + 1;
			for (int offset = 0; offset < framesPerDirection; offset++)
			{
				spriteFrames.AddFrame(animationName, LoadTexture(SampleMaleFrameRoot + $"{filePrefix}_{firstFrame + offset:00}.png"));
			}
		}
	}

	private Node2D AddActor(string path, Vector2I cell, string name, Color ringColor, float scale)
	{
		Node2D actor = new Node2D { Name = name, Position = CellToWorld(cell) };
		_sortedLayer.AddChild(actor);
		actor.AddChild(CreateActorRing(ringColor));
		Texture2D texture = LoadTexture(path);
		actor.AddChild(new Sprite2D
		{
			Name = "Visual", Texture = texture, Centered = false,
			Position = new Vector2(-(texture.GetWidth() * scale * 0.5f), -(texture.GetHeight() * scale)),
			Scale = new Vector2(scale, scale)
		});
		AddActorLabel(actor, name, ringColor);
		return actor;
	}

	private static void AddActorLabel(Node2D actor, string text, Color color)
	{
		Label label = MakeLabel(text.ToUpperInvariant(), 9, new Color(color.R, color.G, color.B, 0.95f));
		label.Position = new Vector2(-55f, 7f);
		label.Size = new Vector2(110f, 18f);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		actor.AddChild(label);
	}

	private void BuildInterface()
	{
		CanvasLayer ui = new CanvasLayer { Name = "Interface", Layer = 20 };
		AddChild(ui);
		PanelContainer infoPanel = MakePanel(new Vector2(28f, 26f), new Vector2(610f, 172f));
		ui.AddChild(infoPanel);
		VBoxContainer info = new VBoxContainer();
		info.AddThemeConstantOverride("separation", 5);
		infoPanel.AddChild(info);
		info.AddChild(MakeLabel("MISSION PROTOTYPE  /  RELAY 07", 13, new Color("56e9ff")));
		info.AddChild(MakeLabel("BLACK SITE RELAY", 28, Colors.White));
		info.AddChild(MakeLabel("Investigate the failing Custodian relay. Assess the survivors,\nsecure one outcome, then rally the team at evac.", 15, new Color("a9bbc7")));
		info.AddChild(MakeLabel("CLICK / WASD move   SPACE attack   WHEEL zoom   ESC exit", 12, new Color("d6f8ff")));

		PanelContainer objectivePanel = MakePanel(new Vector2(1360f, 28f), new Vector2(532f, 176f));
		ui.AddChild(objectivePanel);
		VBoxContainer objectives = new VBoxContainer();
		objectives.AddThemeConstantOverride("separation", 7);
		objectivePanel.AddChild(objectives);
		Label projection = MakeLabel("1:1 DIMETRIC  /  111 WALKABLE CELLS", 13, new Color("56e9ff"));
		projection.HorizontalAlignment = HorizontalAlignment.Right;
		objectives.AddChild(projection);
		Label survivorRoute = MakeLabel("PRIMARY   SAVE SURVIVORS", 17, new Color("59f0c7"));
		survivorRoute.HorizontalAlignment = HorizontalAlignment.Right;
		objectives.AddChild(survivorRoute);
		Label archiveRoute = MakeLabel("ALTERNATE   SECURE ARCHIVE", 17, new Color("ffad42"));
		archiveRoute.HorizontalAlignment = HorizontalAlignment.Right;
		objectives.AddChild(archiveRoute);
		Label routeWarning = MakeLabel("Outcomes are mutually exclusive · Evac east", 12, new Color("a9bbc7"));
		routeWarning.HorizontalAlignment = HorizontalAlignment.Right;
		objectives.AddChild(routeWarning);

		PanelContainer legendPanel = MakePanel(new Vector2(28f, 923f), new Vector2(470f, 126f));
		ui.AddChild(legendPanel);
		VBoxContainer legend = new VBoxContainer();
		legend.AddThemeConstantOverride("separation", 4);
		legendPanel.AddChild(legend);
		legend.AddChild(MakeLabel("SITE TOPOLOGY", 13, new Color("56e9ff")));
		legend.AddChild(MakeLabel("NORTH  Archive core     WEST  Cryo shelter\nEAST   Medical / evac    SOUTH Insertion uplink", 13, new Color("c5d6df")));
		legend.AddChild(MakeLabel("Orange bulkheads mirror all 8 source doors", 11, new Color("ffbc63")));

		PanelContainer statusPanel = MakePanel(new Vector2(590f, 988f), new Vector2(740f, 61f));
		ui.AddChild(statusPanel);
		_statusLabel = MakeLabel(string.Empty, 14, new Color("dffbff"));
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_statusLabel.VerticalAlignment = VerticalAlignment.Center;
		statusPanel.AddChild(_statusLabel);
		UpdateStatus();
	}

	private static PanelContainer MakePanel(Vector2 position, Vector2 size)
	{
		StyleBoxFlat style = new StyleBoxFlat
		{
			BgColor = new Color(0.018f, 0.043f, 0.060f, 0.94f), BorderColor = new Color(0.16f, 0.73f, 0.82f, 0.50f),
			BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
			ContentMarginLeft = 18f, ContentMarginTop = 12f, ContentMarginRight = 18f, ContentMarginBottom = 12f
		};
		PanelContainer panel = new PanelContainer { Position = position, Size = size, MouseFilter = Control.MouseFilterEnum.Stop };
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	private static Label MakeLabel(string text, int fontSize, Color color)
	{
		Label label = new Label { Text = text, Modulate = color };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private void SelectCell(Vector2I cell, bool animate)
	{
		_selectedCell = cell;
		_selectedCellFill.Polygon = GetCellPolygon(cell);
		_selectedCellOutline.Points = GetCellPolygon(cell);
		if (_operative != null)
		{
			_moveTween?.Kill();
			_isAttacking = false;
			if (animate)
			{
				Vector2 targetPosition = CellToWorld(cell);
				Vector2 movementVector = targetPosition - _operative.Position;
				_facingDirection = GetFacingDirection(movementVector);
				PlaySampleMaleAnimation("walk");
				float moveDuration = Mathf.Clamp(movementVector.Length() / 420f, 0.16f, 0.68f);
				_moveTween = CreateTween();
				_moveTween.SetTrans(Tween.TransitionType.Cubic);
				_moveTween.SetEase(Tween.EaseType.Out);
				_moveTween.TweenProperty(_operative, "position", targetPosition, moveDuration);
				_moveTween.Finished += PlayIdleAnimation;
			}
			else
			{
				_operative.Position = CellToWorld(cell);
				PlayIdleAnimation();
			}
		}
		UpdateStatus();
	}

	private void UpdateStatus()
	{
		if (_statusLabel == null) return;
		string animation = _sampleMaleSprite?.Animation.ToString() ?? "idle_8";
		_statusLabel.Text = $"CELL {_selectedCell.X:00}:{_selectedCell.Y:00}   ·   {GetZoneName(_selectedCell)}   ·   FACING {_facingDirection}   ·   {animation.ToUpperInvariant()}";
	}

	private static string GetZoneName(Vector2I cell)
	{
		if (cell.X <= 2 && cell.Y >= 11) return "CRYO SHELTER";
		if (cell.X >= 9 && cell.X <= 11 && cell.Y <= 2) return "ARCHIVE CHAMBER";
		if (cell.X >= 15 && cell.Y >= 7 && cell.Y <= 10) return "MEDICAL / EVAC";
		if (cell.X >= 5 && cell.X <= 8 && cell.Y >= 17) return "INSERTION AIRLOCK";
		return "RELAY SERVICE NETWORK";
	}

	private void PlayAttackAnimation()
	{
		if (_sampleMaleSprite == null || _isAttacking || (_moveTween != null && _moveTween.IsRunning())) return;
		_isAttacking = true;
		PlaySampleMaleAnimation("attack");
	}

	private void OnSampleMaleAnimationFinished()
	{
		if (!_isAttacking) return;
		_isAttacking = false;
		PlayIdleAnimation();
	}

	private void PlayIdleAnimation()
	{
		_isAttacking = false;
		PlaySampleMaleAnimation("idle");
	}

	private void PlaySampleMaleAnimation(string animationPrefix)
	{
		if (_sampleMaleSprite == null) return;
		_sampleMaleSprite.Play($"{animationPrefix}_{_facingDirection}");
		UpdateStatus();
	}

	private static int GetFacingDirection(Vector2 screenDirection)
	{
		float absX = Mathf.Abs(screenDirection.X);
		float absY = Mathf.Abs(screenDirection.Y);
		if (absX < absY * 0.45f) return screenDirection.Y < 0f ? 8 : 5;
		if (absY < absX * 0.45f) return screenDirection.X > 0f ? 1 : 6;
		if (screenDirection.X > 0f) return screenDirection.Y > 0f ? 2 : 3;
		return screenDirection.Y > 0f ? 7 : 4;
	}

	private void SetCameraZoom(float zoom)
	{
		float clamped = Mathf.Clamp(zoom, 0.82f, 1.42f);
		_camera.Zoom = new Vector2(clamped, clamped);
	}

	private void AddWorldLabel(string text, Vector2 position, Color color, int size)
	{
		Label label = MakeLabel(text, size, color);
		label.Position = position - new Vector2(70f, 0f);
		label.Size = new Vector2(140f, 22f);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		_markerLayer.AddChild(label);
	}

	private static void AddCellDiamond(Vector2I cell, Color color, Node parent) => parent.AddChild(new Polygon2D { Polygon = GetCellPolygon(cell), Color = color });

	private Node2D AddSortedSprite(string path, Vector2I cell, string name, float scale)
	{
		Texture2D texture = LoadTexture(path);
		Node2D item = new Node2D { Name = name, Position = CellToWorld(cell) };
		_sortedLayer.AddChild(item);
		item.AddChild(new Sprite2D
		{
			Name = "Visual", Texture = texture, Centered = false,
			Position = new Vector2(-(texture.GetWidth() * scale * 0.5f), -(texture.GetHeight() * scale)),
			Scale = new Vector2(scale, scale)
		});
		return item;
	}

	private static Texture2D LoadTexture(string path)
	{
		if (ResourceLoader.Exists(path))
		{
			Texture2D importedTexture = ResourceLoader.Load<Texture2D>(path);
			if (importedTexture != null) return importedTexture;
		}
		string absolutePath = ProjectSettings.GlobalizePath(path);
		using Image image = new Image();
		if (image.Load(absolutePath) != Error.Ok) throw new InvalidOperationException($"CyberCity prototype texture could not be loaded: {path}");
		return ImageTexture.CreateFromImage(image);
	}

	private static Vector2 CellToWorld(Vector2I cell) => MapOrigin + (CellColumnBasis * (cell.X + 0.5f)) + (CellRowBasis * (cell.Y + 0.5f));

	private static Vector2[] GetCellPolygon(Vector2I cell)
	{
		Vector2 center = CellToWorld(cell);
		return new[]
		{
			center + new Vector2(0f, -HalfTileHeight), center + new Vector2(HalfTileWidth, 0f),
			center + new Vector2(0f, HalfTileHeight), center + new Vector2(-HalfTileWidth, 0f)
		};
	}

	private Vector2I FindNearestCell(Vector2 worldPosition)
	{
		Vector2I nearest = new Vector2I(-1, -1);
		float nearestDistanceSquared = float.MaxValue;
		foreach (Vector2I cell in _floorTypes.Keys)
		{
			float distanceSquared = worldPosition.DistanceSquaredTo(CellToWorld(cell));
			if (distanceSquared >= nearestDistanceSquared) continue;
			nearestDistanceSquared = distanceSquared;
			nearest = cell;
		}
		float maximumDistance = HalfTileWidth * 1.08f;
		return nearestDistanceSquared <= maximumDistance * maximumDistance ? nearest : new Vector2I(-1, -1);
	}

	private bool IsWalkable(Vector2I cell) => _floorTypes.ContainsKey(cell);

	private async void CapturePrototypeAndQuit()
	{
		for (int frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Image image = GetViewport().GetTexture().GetImage();
		string outputPath = ProjectSettings.GlobalizePath("res://tmp/cybercity_prototype.png");
		Error result = image.SavePng(outputPath);
		GD.Print(result == Error.Ok ? $"CyberCity prototype capture saved: {outputPath}" : $"CyberCity prototype capture failed: {result}");
		GetTree().Quit(result == Error.Ok ? 0 : 1);
	}
}
