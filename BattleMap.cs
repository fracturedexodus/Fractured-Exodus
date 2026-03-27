using Godot;
using System;
using System.Collections.Generic;

public partial class BattleMap : Node2D
{
	// --- MAP SETTINGS ---
	[Export] public float HexSize = 65f; 
	private int _maxMapRadius = 35; 

	private PackedScene _hexScene = GD.Load<PackedScene>("res://hex_tile.tscn");
	private GlobalData _globalData;

	// --- CAMERA CONTROLS ---
	private Camera2D _camera;
	private float _panSpeed = 600f;
	private float _zoomSpeed = 0.1f;
	private float _minZoom = 0.3f;
	private float _maxZoom = 2.0f;

	// --- RENDER LAYERS ---
	private CanvasLayer _bgLayer = new CanvasLayer { Layer = -1 }; 
	private Node2D _gridLayer = new Node2D();  
	private Node2D _highlightLayer = new Node2D(); 
	private Node2D _entityLayer = new Node2D(); 

	private Dictionary<Vector2I, Node2D> _hexGrid = new Dictionary<Vector2I, Node2D>();

	// --- AUDIO ---
	private AudioStreamPlayer _bgmPlayer;

	// --- ENTITY DATA STORAGE ---
	public class MapEntity
	{
		public string Name;
		public string Type;
		public string Details;
		public int MaxMovement;
		public int CurrentMovement; 
		
		public int MaxHP;
		public int CurrentHP;
		public int MaxShields;
		public int CurrentShields;

		public Sprite2D VisualSprite; 
		public float BaseRotationOffset; 
	}
	private Dictionary<Vector2I, MapEntity> _hexContents = new Dictionary<Vector2I, MapEntity>();

	// --- GAME STATE ---
	private Vector2I? _selectedHex = null; 
	private int _currentTurn = 1; 

	// --- UI ELEMENTS ---
	private PanelContainer _infoPanel; 
	private Label _infoLabel;
	private Button _endTurnButton;
	private Button _saveGameButton;
	private Button _mainMenuButton;

	private Vector2I[] _hexDirections = new Vector2I[] {
		new Vector2I(1, 0), new Vector2I(1, -1), new Vector2I(0, -1), 
		new Vector2I(-1, 0), new Vector2I(-1, 1), new Vector2I(0, 1)
	};

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		
		// --- NEW: Load the saved turn number ---
		if (_globalData != null && _globalData.CurrentTurn > 0)
		{
			_currentTurn = _globalData.CurrentTurn;
		}
		
		AddChild(_bgLayer);
		AddChild(_gridLayer);
		AddChild(_highlightLayer);
		AddChild(_entityLayer);

		SetupCamera();
		SetupSpaceBackground();
		SetupMusic(); 
		SetupUI(); 
		GenerateGrid();
		PopulateMapFromMemory();
	}

	private void SetupMusic()
	{
		_bgmPlayer = new AudioStreamPlayer();
		AddChild(_bgmPlayer);

		AudioStream bgmStream = GD.Load<AudioStream>("res://battle_theme.mp3"); 
		
		if (bgmStream != null)
		{
			_bgmPlayer.Stream = bgmStream;
			_bgmPlayer.VolumeDb = -15.0f; 
			_bgmPlayer.Play();
		}
	}

	private void SetupCamera()
	{
		_camera = new Camera2D();
		AddChild(_camera);
		_camera.MakeCurrent();
	}

	public override void _Process(double delta)
	{
		Vector2 panDirection = Vector2.Zero;
		if (Input.IsKeyPressed(Key.W)) panDirection.Y -= 1;
		if (Input.IsKeyPressed(Key.S)) panDirection.Y += 1;
		if (Input.IsKeyPressed(Key.A)) panDirection.X -= 1;
		if (Input.IsKeyPressed(Key.D)) panDirection.X += 1;

		if (panDirection != Vector2.Zero)
		{
			_camera.Position += panDirection.Normalized() * _panSpeed * (float)delta * (1.0f / _camera.Zoom.X);
		}
		
		if (_selectedHex.HasValue && _hexContents.ContainsKey(_selectedHex.Value))
		{
			MapEntity selectedShip = _hexContents[_selectedHex.Value];
			
			if (IsInstanceValid(selectedShip.VisualSprite))
			{
				Vector2 mousePos = GetGlobalMousePosition();
				float targetAngle = selectedShip.VisualSprite.GlobalPosition.AngleToPoint(mousePos) + selectedShip.BaseRotationOffset;
				selectedShip.VisualSprite.Rotation = Mathf.LerpAngle(selectedShip.VisualSprite.Rotation, targetAngle, 0.15f);
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton && mouseButton.IsPressed())
		{
			if (mouseButton.ButtonIndex == MouseButton.WheelUp) _camera.Zoom += new Vector2(_zoomSpeed, _zoomSpeed);
			else if (mouseButton.ButtonIndex == MouseButton.WheelDown) _camera.Zoom -= new Vector2(_zoomSpeed, _zoomSpeed);

			_camera.Zoom = new Vector2(
				Mathf.Clamp(_camera.Zoom.X, _minZoom, _maxZoom),
				Mathf.Clamp(_camera.Zoom.Y, _minZoom, _maxZoom)
			);
			
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				Vector2I clickedHex = PixelToHex(GetGlobalMousePosition());

				if (_selectedHex.HasValue)
				{
					MapEntity ship = _hexContents[_selectedHex.Value];
					Dictionary<Vector2I, int> reachable = GetReachableHexes(_selectedHex.Value, ship.CurrentMovement);

					if (reachable.ContainsKey(clickedHex) && clickedHex != _selectedHex.Value)
					{
						int movementCost = reachable[clickedHex];
						MoveShip(_selectedHex.Value, clickedHex, movementCost);
						
						_selectedHex = null;
						ClearHighlights();
					}
					else if (_hexContents.ContainsKey(clickedHex) && _hexContents[clickedHex].Type == "Player Fleet")
					{
						_selectedHex = clickedHex;
						reachable = GetReachableHexes(clickedHex, _hexContents[clickedHex].CurrentMovement);
						DrawHighlights(clickedHex, reachable.Keys);
					}
					else
					{
						_selectedHex = null;
						ClearHighlights();
					}
				}
				else
				{
					if (_hexContents.ContainsKey(clickedHex) && _hexContents[clickedHex].Type == "Player Fleet")
					{
						_selectedHex = clickedHex;
						Dictionary<Vector2I, int> reachable = GetReachableHexes(clickedHex, _hexContents[clickedHex].CurrentMovement);
						DrawHighlights(clickedHex, reachable.Keys);
					}
				}
			}
		}

		if (@event is InputEventMouseMotion)
		{
			Vector2I hoveredHex = PixelToHex(GetGlobalMousePosition());

			if (_hexContents.ContainsKey(hoveredHex))
			{
				MapEntity entity = _hexContents[hoveredHex];
				
				string dynamicStats = "";
				if (entity.Type == "Player Fleet")
				{
					dynamicStats = $"HP: {entity.CurrentHP}/{entity.MaxHP} | SHIELD: {entity.CurrentShields}/{entity.MaxShields}\n" +
								   $"MOVE: {entity.CurrentMovement}/{entity.MaxMovement}\n";
				}

				_infoLabel.Text = $"[ {entity.Name.ToUpper()} ]\n" +
								  $"Type: {entity.Type}\n" +
								  $"{dynamicStats}" + 
								  $"Data: {entity.Details}";
				_infoPanel.Visible = true;
			}
			else
			{
				_infoPanel.Visible = false; 
			}
		}
	}

	// ==========================================
	// TURN LOGIC, SAVING & MOVEMENT
	// ==========================================
	private void OnEndTurnPressed()
	{
		_currentTurn++;
		GD.Print($"--- TURN {_currentTurn} START ---");

		foreach (var kvp in _hexContents)
		{
			MapEntity entity = kvp.Value;
			if (entity.Type == "Player Fleet")
			{
				entity.CurrentMovement = entity.MaxMovement;
			}
		}

		_selectedHex = null;
		ClearHighlights();
		_endTurnButton.Text = $"TURN {_currentTurn}";
	}

	private void OnSaveGamePressed()
	{
		if (_globalData != null)
		{
			// 1. Save the turn counter
			_globalData.CurrentTurn = _currentTurn;

			// 2. Iterate through the map and record the exact state of every ship
			var fleetState = new Godot.Collections.Array();
			foreach (var kvp in _hexContents)
			{
				if (kvp.Value.Type == "Player Fleet")
				{
					var shipDict = new Godot.Collections.Dictionary<string, Variant>();
					shipDict["Name"] = kvp.Value.Name;
					shipDict["Q"] = kvp.Key.X; // The Hex Grid X coordinate
					shipDict["R"] = kvp.Key.Y; // The Hex Grid Y coordinate
					shipDict["CurrentHP"] = kvp.Value.CurrentHP;
					shipDict["MaxHP"] = kvp.Value.MaxHP;
					shipDict["CurrentShields"] = kvp.Value.CurrentShields;
					shipDict["MaxShields"] = kvp.Value.MaxShields;
					shipDict["CurrentMovement"] = kvp.Value.CurrentMovement;
					shipDict["MaxMovement"] = kvp.Value.MaxMovement;
					
					fleetState.Add(shipDict);
				}
			}
			
			_globalData.SavedFleetState = fleetState;

			// 3. Trigger the file write
			if (_globalData.HasMethod("SaveGame"))
			{
				_globalData.Call("SaveGame");
			}
			
			_saveGameButton.Text = "GAME SAVED!";
			_saveGameButton.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f)); 
			
			GetTree().CreateTimer(2.0f).Timeout += () => 
			{
				_saveGameButton.Text = "SAVE GAME";
				_saveGameButton.RemoveThemeColorOverride("font_color");
			};
		}
	}

	private void OnMainMenuPressed()
	{
		GD.Print("Returning to Main Menu...");
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene("res://main_menu.tscn");
		}
		else
		{
			GetTree().ChangeSceneToFile("res://main_menu.tscn");
		}
	}

	private void MoveShip(Vector2I fromHex, Vector2I toHex, int cost)
	{
		MapEntity ship = _hexContents[fromHex];
		
		_hexContents.Remove(fromHex);
		_hexContents[toHex] = ship;
		
		ship.CurrentMovement -= cost;

		Tween tween = CreateTween();
		Vector2 targetPixelPos = HexToPixel(toHex);
		
		float distance = ship.VisualSprite.Position.DistanceTo(targetPixelPos);
		float duration = Mathf.Max(0.3f, distance / 500f); 

		tween.TweenProperty(ship.VisualSprite, "position", targetPixelPos, duration)
			 .SetTrans(Tween.TransitionType.Sine)
			 .SetEase(Tween.EaseType.InOut);
	}

	// ==========================================
	// PATHFINDING & HIGHLIGHTS
	// ==========================================
	private Dictionary<Vector2I, int> GetReachableHexes(Vector2I startHex, int movementRange)
	{
		Dictionary<Vector2I, int> costSoFar = new Dictionary<Vector2I, int>();
		costSoFar[startHex] = 0;

		Queue<Vector2I> frontier = new Queue<Vector2I>();
		frontier.Enqueue(startHex);

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();

			foreach (Vector2I dir in _hexDirections)
			{
				Vector2I next = current + dir;

				if (!_hexGrid.ContainsKey(next)) continue;

				bool isBlocked = false;
				if (_hexContents.ContainsKey(next))
				{
					string type = _hexContents[next].Type;
					if (type == "Planet" || type == "Base Planet (Player Start)" || type == "Celestial Body" || type == "Player Fleet")
					{
						isBlocked = true; 
					}
				}

				if (isBlocked) continue;

				int newCost = costSoFar[current] + 1; 

				if (newCost <= movementRange)
				{
					if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
					{
						costSoFar[next] = newCost;
						frontier.Enqueue(next);
					}
				}
			}
		}
		return costSoFar;
	}

	private void DrawHighlights(Vector2I selectedShipHex, IEnumerable<Vector2I> reachableHexes)
	{
		ClearHighlights(); 

		foreach (Vector2I hex in reachableHexes)
		{
			if (hex == selectedShipHex) continue; 
			CreateHighlightPolygon(hex, new Color(0f, 1f, 0.3f, 0.4f)); 
		}

		CreateHighlightPolygon(selectedShipHex, new Color(1f, 0.8f, 0f, 0.6f)); 
	}

	private void ClearHighlights()
	{
		foreach (Node child in _highlightLayer.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void CreateHighlightPolygon(Vector2I hexCoord, Color color)
	{
		Polygon2D poly = new Polygon2D();
		Vector2[] points = new Vector2[6];
		
		for (int i = 0; i < 6; i++)
		{
			float angle_deg = 60 * i - 30;
			float angle_rad = Mathf.DegToRad(angle_deg);
			points[i] = new Vector2(HexSize * Mathf.Cos(angle_rad), HexSize * Mathf.Sin(angle_rad));
		}
		
		poly.Polygon = points;
		poly.Color = color;
		poly.Position = HexToPixel(hexCoord);
		_highlightLayer.AddChild(poly);
	}

	// ==========================================
	// UI LOGIC
	// ==========================================
	private void SetupUI()
	{
		CanvasLayer uiLayer = new CanvasLayer { Layer = 10 }; 
		AddChild(uiLayer);

		_infoPanel = new PanelContainer();
		_infoPanel.Position = new Vector2(20, 20); 
		
		StyleBoxFlat panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f); 
		panelStyle.BorderWidthBottom = 2;
		panelStyle.BorderWidthTop = 2;
		panelStyle.BorderWidthLeft = 2;
		panelStyle.BorderWidthRight = 2;
		panelStyle.BorderColor = new Color(0.2f, 0.8f, 1f, 1f); 
		panelStyle.CornerRadiusTopLeft = 5;
		panelStyle.CornerRadiusBottomRight = 5;
		panelStyle.ContentMarginLeft = 15;
		panelStyle.ContentMarginRight = 15;
		panelStyle.ContentMarginTop = 15;
		panelStyle.ContentMarginBottom = 15;
		
		_infoPanel.AddThemeStyleboxOverride("panel", panelStyle);

		_infoLabel = new Label();
		_infoLabel.CustomMinimumSize = new Vector2(250, 0); 
		_infoLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		
		_infoPanel.AddChild(_infoLabel);
		uiLayer.AddChild(_infoPanel);
		_infoPanel.Visible = false; 

		Vector2 screenSize = GetViewportRect().Size;

		_endTurnButton = new Button();
		_endTurnButton.Text = $"TURN {_currentTurn}"; // Loads the current turn dynamically!
		_endTurnButton.CustomMinimumSize = new Vector2(160, 50);
		
		StyleBoxFlat btnStyle = new StyleBoxFlat();
		btnStyle.BgColor = new Color(0.1f, 0.3f, 0.1f, 0.9f); 
		btnStyle.BorderWidthBottom = 2;
		btnStyle.BorderWidthTop = 2;
		btnStyle.BorderWidthLeft = 2;
		btnStyle.BorderWidthRight = 2;
		btnStyle.BorderColor = new Color(0.3f, 1f, 0.3f, 1f); 
		btnStyle.CornerRadiusTopLeft = 5;
		btnStyle.CornerRadiusBottomRight = 5;
		_endTurnButton.AddThemeStyleboxOverride("normal", btnStyle);
		_endTurnButton.AddThemeStyleboxOverride("hover", panelStyle); 
		
		_endTurnButton.Position = new Vector2(screenSize.X - 180, 20);
		_endTurnButton.Pressed += OnEndTurnPressed;
		uiLayer.AddChild(_endTurnButton);

		_saveGameButton = new Button();
		_saveGameButton.Text = "SAVE GAME";
		_saveGameButton.CustomMinimumSize = new Vector2(160, 50);
		
		StyleBoxFlat saveStyle = new StyleBoxFlat();
		saveStyle.BgColor = new Color(0.1f, 0.2f, 0.4f, 0.9f); 
		saveStyle.BorderWidthBottom = 2;
		saveStyle.BorderWidthTop = 2;
		saveStyle.BorderWidthLeft = 2;
		saveStyle.BorderWidthRight = 2;
		saveStyle.BorderColor = new Color(0.3f, 0.6f, 1f, 1f); 
		saveStyle.CornerRadiusTopLeft = 5;
		saveStyle.CornerRadiusBottomRight = 5;
		_saveGameButton.AddThemeStyleboxOverride("normal", saveStyle);
		_saveGameButton.AddThemeStyleboxOverride("hover", panelStyle); 
		
		_saveGameButton.Position = new Vector2(screenSize.X - 360, 20); 
		_saveGameButton.Pressed += OnSaveGamePressed;
		uiLayer.AddChild(_saveGameButton);

		_mainMenuButton = new Button();
		_mainMenuButton.Text = "MAIN MENU";
		_mainMenuButton.CustomMinimumSize = new Vector2(160, 50);
		
		StyleBoxFlat menuStyle = new StyleBoxFlat();
		menuStyle.BgColor = new Color(0.4f, 0.1f, 0.1f, 0.9f); 
		menuStyle.BorderWidthBottom = 2;
		menuStyle.BorderWidthTop = 2;
		menuStyle.BorderWidthLeft = 2;
		menuStyle.BorderWidthRight = 2;
		menuStyle.BorderColor = new Color(1f, 0.3f, 0.3f, 1f); 
		menuStyle.CornerRadiusTopLeft = 5;
		menuStyle.CornerRadiusBottomRight = 5;
		_mainMenuButton.AddThemeStyleboxOverride("normal", menuStyle);
		_mainMenuButton.AddThemeStyleboxOverride("hover", panelStyle); 
		
		_mainMenuButton.Position = new Vector2(screenSize.X - 540, 20); 
		_mainMenuButton.Pressed += OnMainMenuPressed;
		uiLayer.AddChild(_mainMenuButton);
	}

	private Vector2I PixelToHex(Vector2 pt)
	{
		float q = (Mathf.Sqrt(3f) / 3f * pt.X - 1f / 3f * pt.Y) / HexSize;
		float r = (2f / 3f * pt.Y) / HexSize;
		return AxialRound(q, r);
	}

	private Vector2I AxialRound(float q, float r)
	{
		float s = -q - r;
		int rq = Mathf.RoundToInt(q);
		int rr = Mathf.RoundToInt(r);
		int rs = Mathf.RoundToInt(s);

		float qDiff = Mathf.Abs(rq - q);
		float rDiff = Mathf.Abs(rr - r);
		float sDiff = Mathf.Abs(rs - s);

		if (qDiff > rDiff && qDiff > sDiff) rq = -rr - rs;
		else if (rDiff > sDiff) rr = -rq - rs;

		return new Vector2I(rq, rr);
	}

	// ==========================================
	// SPACE BACKGROUND
	// ==========================================
	private void SetupSpaceBackground()
	{
		Vector2 screenSize = GetViewportRect().Size;
		TextureRect spaceBackground = new TextureRect();
		Texture2D bgTex = GD.Load<Texture2D>("res://space_bg.png"); 
		
		if (bgTex != null)
		{
			spaceBackground.Texture = bgTex;
			spaceBackground.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize; 
			spaceBackground.Size = screenSize; 
			spaceBackground.Modulate = new Color(0.6f, 0.6f, 0.7f, 1.0f); 
			_bgLayer.AddChild(spaceBackground);
		}
		else
		{
			ColorRect darkVoid = new ColorRect();
			darkVoid.Size = screenSize;
			darkVoid.Color = new Color(0.02f, 0.02f, 0.08f);
			_bgLayer.AddChild(darkVoid);
		}
	}

	// ==========================================
	// DYNAMIC GRID GENERATION
	// ==========================================
	private void GenerateGrid()
	{
		for (int q = -_maxMapRadius; q <= _maxMapRadius; q++)
		{
			int r1 = Mathf.Max(-_maxMapRadius, -q - _maxMapRadius);
			int r2 = Mathf.Min(_maxMapRadius, -q + _maxMapRadius);

			for (int r = r1; r <= r2; r++)
			{
				Vector2I hexCoord = new Vector2I(q, r);
				Vector2 worldPos = HexToPixel(hexCoord);

				Node2D hexTile = _hexScene.Instantiate<Node2D>();
				hexTile.Position = worldPos;
				
				Label coordLabel = hexTile.GetNodeOrNull<Label>("CoordLabel");
				if (coordLabel != null) coordLabel.Text = $"{q},{r}";

				_gridLayer.AddChild(hexTile);
				_hexGrid.Add(hexCoord, hexTile);
			}
		}
	}

	private Vector2 HexToPixel(Vector2I hex)
	{
		float x = HexSize * Mathf.Sqrt(3) * (hex.X + hex.Y / 2f);
		float y = HexSize * 3f / 2f * hex.Y;
		return new Vector2(x, y);
	}

	// ==========================================
	// POPULATING THE ENTIRE SYSTEM
	// ==========================================
	private void PopulateMapFromMemory()
	{
		MapEntity starData = new MapEntity { Name = "Main Sequence Star", Type = "Celestial Body", Details = "Extreme Heat Signature" };
		if (_globalData != null && !string.IsNullOrEmpty(_globalData.SavedSystem))
			starData.Name = _globalData.SavedSystem.ToUpper() + " PRIME";
			
		SpawnEntityAtHex(new Vector2I(0, 0), "res://YellowSUN.png", starData, 0.75f);

		if (_globalData == null || string.IsNullOrEmpty(_globalData.SavedSystem) || !_globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem)) 
		{
			GD.Print("Running without GlobalData System Memory. Skipping planet generation.");
			return;
		}

		SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
		Random rng = new Random(_globalData.SavedSystem.GetHashCode()); 
		
		Vector2I basePlanetLocation = new Vector2I(2, -1); 
		int currentOrbitRing = 2; 

		foreach (PlanetData pData in currentSystem.Planets)
		{
			Vector2I spawnHex = FindEmptyHexInRing(currentOrbitRing, rng);
			currentOrbitRing += 3; 

			string pTypeStr = GetPlanetTypeString(pData.TypeIndex);
			string pTex = GetTexturePathForType(pTypeStr);

			MapEntity planetEntity = new MapEntity { 
				Name = pData.Name, 
				Type = "Planet", 
				Details = $"Biome Class: {pTypeStr.ToUpper()}\nHab: {pData.Habitability}" 
			};

			SpawnEntityAtHex(spawnHex, pTex, planetEntity, pData.Scale);

			if (pData.Name == _globalData.SavedPlanet)
			{
				basePlanetLocation = spawnHex;
				planetEntity.Type = "Base Planet (Player Start)";
			}
		}

		// --- NEW: Check if we are loading a saved game ---
		if (_globalData.SavedFleetState != null && _globalData.SavedFleetState.Count > 0)
		{
			GD.Print("Spawning fleet from Save Data!");
			
			foreach (var item in _globalData.SavedFleetState)
			{
				var shipDict = (Godot.Collections.Dictionary)item;
				
				string shipName = (string)shipDict["Name"];
				
				// Pull the exact coordinates from the dictionary
				Vector2I spawnPos = new Vector2I(
					(int)shipDict["Q"], 
					(int)shipDict["R"]
				);

				MapEntity shipData = new MapEntity { 
					Name = shipName, 
					Type = "Player Fleet", 
					Details = "Status: Online",
					MaxMovement = (int)shipDict["MaxMovement"],
					CurrentMovement = (int)shipDict["CurrentMovement"],
					MaxHP = (int)shipDict["MaxHP"],
					CurrentHP = (int)shipDict["CurrentHP"],
					MaxShields = (int)shipDict["MaxShields"],
					CurrentShields = (int)shipDict["CurrentShields"],
					BaseRotationOffset = GetShipRotationOffset(shipName)
				};
				
				string shipTexPath = GetShipTexturePath(shipName);
				SpawnEntityAtHex(spawnPos, shipTexPath, shipData, 0.2f); 
			}
		}
		// --- IF NOT loading, do a fresh spawn around the base planet ---
		else if (_globalData.SelectedPlayerFleet != null && _globalData.SelectedPlayerFleet.Count > 0)
		{
			GD.Print("Spawning fresh fleet around Base Planet.");
			int currentDirIndex = 0;

			foreach (string shipName in _globalData.SelectedPlayerFleet)
			{
				while (currentDirIndex < 6)
				{
					Vector2I spawnPos = basePlanetLocation + _hexDirections[currentDirIndex];
					currentDirIndex++;
					
					if (_hexGrid.ContainsKey(spawnPos) && !_hexContents.ContainsKey(spawnPos))
					{
						int shipMovement = GetShipBaseMovement(shipName); 
						(int hp, int shields) = GetShipCombatStats(shipName);

						MapEntity shipData = new MapEntity { 
							Name = shipName, 
							Type = "Player Fleet", 
							Details = "Status: Online",
							MaxMovement = shipMovement,
							CurrentMovement = shipMovement,
							MaxHP = hp,
							CurrentHP = hp,
							MaxShields = shields,
							CurrentShields = shields,
							BaseRotationOffset = GetShipRotationOffset(shipName)
						};
						
						string shipTexPath = GetShipTexturePath(shipName);
						SpawnEntityAtHex(spawnPos, shipTexPath, shipData, 0.2f); 
						break; 
					}
				}
			}
		}
	}

	private Vector2I FindEmptyHexInRing(int radius, Random rng)
	{
		List<Vector2I> ringHexes = new List<Vector2I>();
		Vector2I currentHex = new Vector2I(0, -radius);
		
		Vector2I[] ringDirs = new Vector2I[] {
			new Vector2I(1, 0), new Vector2I(0, 1), new Vector2I(-1, 1), 
			new Vector2I(-1, 0), new Vector2I(0, -1), new Vector2I(1, -1)
		};
		
		foreach (Vector2I dir in ringDirs)
		{
			for (int i = 0; i < radius; i++)
			{
				ringHexes.Add(currentHex);
				currentHex += dir;
			}
		}
		
		ShuffleList(ringHexes, rng);
		foreach (var hex in ringHexes)
		{
			if (_hexGrid.ContainsKey(hex) && !_hexContents.ContainsKey(hex))
			{
				return hex;
			}
		}
		
		return new Vector2I(radius, 0); 
	}

	private void ShuffleList<T>(List<T> list, Random rng)  
	{  
		int n = list.Count;  
		while (n > 1) {  
			n--;  
			int k = rng.Next(n + 1);  
			T value = list[k];  
			list[k] = list[n];  
			list[n] = value;  
		}  
	}

	private void SpawnEntityAtHex(Vector2I hexCoord, string texturePath, MapEntity entityData, float scale)
	{
		if (!_hexGrid.ContainsKey(hexCoord)) return;

		Sprite2D entitySprite = new Sprite2D();
		
		Texture2D tex = GD.Load<Texture2D>(texturePath);
		if (tex != null) entitySprite.Texture = tex;
		
		entitySprite.Scale = new Vector2(scale, scale);
		entitySprite.Position = HexToPixel(hexCoord);

		_entityLayer.AddChild(entitySprite);
		
		entityData.VisualSprite = entitySprite;
		_hexContents[hexCoord] = entityData;
	}

	// ==========================================
	// DATA DICTIONARIES
	// ==========================================
	private string GetPlanetTypeString(int typeIndex)
	{
		string[] types = { "Terra", "Arid", "Ocean", "Toxic", "Frozen", "Lava" };
		if (typeIndex >= 0 && typeIndex < types.Length)
			return types[typeIndex];
		return "Terra";
	}

	private string GetTexturePathForType(string type)
	{
		if (string.IsNullOrEmpty(type)) return "res://Planets/terra_planet.png";
		
		switch (type.ToUpper())
		{
			case "TERRA": return "res://Planets/terra_planet.png";
			case "ARID": return "res://Planets/arid_planet.png";
			case "OCEAN": return "res://Planets/ocean_planet.png";
			case "TOXIC": return "res://Planets/toxic_planet.png";
			case "FROZEN": return "res://Planets/frozen_planet.png";
			case "LAVA": return "res://Planets/lava_planet.png";
			default: return "res://Planets/terra_planet.png"; 
		}
	}

	private string GetShipTexturePath(string shipName)
	{
		switch (shipName)
		{
			case "The Relic Harvester": return "res://Ships/RelicHarvesterSprite.png";
			case "The Panacea Spire": return "res://Ships/PanaceaSpireSprite.png";
			case "The Neptune Forge": return "res://Ships/NeptuneForgeSprite.png";
			case "The Genesis Ark": return "res://Ships/GenesisArkSprite.png";
			case "The Valkyrie Wing": return "res://Ships/ValkyrieWingSprite.png";
			case "The Aegis Bastion": return "res://Ships/AegisBastionSprite.png";
			case "The Aether Skimmer": return "res://Ships/AetherSkimmerSprite.png";
			default: return "res://icon.svg"; 
		}
	}

	private int GetShipBaseMovement(string shipName)
	{
		switch (shipName)
		{
			case "The Aether Skimmer": return 5;
			case "The Valkyrie Wing": return 4;
			case "The Genesis Ark": return 3;
			case "The Panacea Spire": return 3;
			case "The Relic Harvester": return 3;
			case "The Neptune Forge": return 2;
			case "The Aegis Bastion": return 2;
			default: return 3; 
		}
	}

	private (int hp, int shields) GetShipCombatStats(string shipName)
	{
		switch (shipName)
		{
			case "The Aegis Bastion": return (100, 50);   
			case "The Neptune Forge": return (80, 20);    
			case "The Genesis Ark": return (50, 30);      
			case "The Panacea Spire": return (40, 40);    
			case "The Relic Harvester": return (50, 20);  
			case "The Valkyrie Wing": return (30, 20);    
			case "The Aether Skimmer": return (20, 10);   
			default: return (50, 25); 
		}
	}

	private float GetShipRotationOffset(string shipName)
	{
		switch (shipName)
		{
			case "The Genesis Ark":
			case "The Panacea Spire":
			case "The Relic Harvester":
			case "The Valkyrie Wing":
			case "The Aegis Bastion":
				return Mathf.Pi / 2f; 
				
			case "The Neptune Forge":
				return Mathf.Pi; 

			case "The Aether Skimmer":
				return 0f; 

			default:
				return 0f; 
		}
	}
}
