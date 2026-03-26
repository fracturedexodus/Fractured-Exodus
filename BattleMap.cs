using Godot;
using System;
using System.Collections.Generic;

public partial class BattleMap : Node2D
{
	// --- MAP SETTINGS ---
	[Export] public float HexSize = 65f; 
	// The grid radius is now massive to accommodate a huge solar system (35 rings out!)
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
	// Background and UI are CanvasLayers so they stick to your screen while the camera moves!
	private CanvasLayer _bgLayer = new CanvasLayer { Layer = -1 }; 
	private Node2D _gridLayer = new Node2D();  
	private Node2D _entityLayer = new Node2D(); 

	private Dictionary<Vector2I, Node2D> _hexGrid = new Dictionary<Vector2I, Node2D>();

	// --- ENTITY DATA STORAGE ---
	public class MapEntity
	{
		public string Name;
		public string Type;
		public string Details;
	}
	private Dictionary<Vector2I, MapEntity> _hexContents = new Dictionary<Vector2I, MapEntity>();

	// --- UI ELEMENTS ---
	private Panel _infoPanel;
	private Label _infoLabel;

	private Vector2I[] _hexDirections = new Vector2I[] {
		new Vector2I(1, 0), new Vector2I(1, -1), new Vector2I(0, -1), 
		new Vector2I(-1, 0), new Vector2I(-1, 1), new Vector2I(0, 1)
	};

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		
		AddChild(_bgLayer);
		AddChild(_gridLayer);
		AddChild(_entityLayer);

		SetupCamera();
		SetupSpaceBackground();
		SetupHoverUI(); 
		GenerateGrid();
		PopulateMapFromMemory();
	}

	// ==========================================
	// CAMERA & CONTROLS
	// ==========================================
	private void SetupCamera()
	{
		_camera = new Camera2D();
		AddChild(_camera);
		_camera.MakeCurrent();
	}

	public override void _Process(double delta)
	{
		// WASD Panning Logic
		Vector2 panDirection = Vector2.Zero;

		if (Input.IsKeyPressed(Key.W)) panDirection.Y -= 1;
		if (Input.IsKeyPressed(Key.S)) panDirection.Y += 1;
		if (Input.IsKeyPressed(Key.A)) panDirection.X -= 1;
		if (Input.IsKeyPressed(Key.D)) panDirection.X += 1;

		if (panDirection != Vector2.Zero)
		{
			// Normalize ensures diagonal movement isn't faster. 
			// Dividing by zoom keeps panning speed consistent whether zoomed in or out!
			_camera.Position += panDirection.Normalized() * _panSpeed * (float)delta * (1.0f / _camera.Zoom.X);
		}
	}

	public override void _Input(InputEvent @event)
	{
		// --- MOUSE WHEEL ZOOMING ---
		if (@event is InputEventMouseButton mouseButton && mouseButton.IsPressed())
		{
			if (mouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				_camera.Zoom += new Vector2(_zoomSpeed, _zoomSpeed);
			}
			else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				_camera.Zoom -= new Vector2(_zoomSpeed, _zoomSpeed);
			}

			// Clamp the zoom so the player can't zoom into infinity or flip the camera
			_camera.Zoom = new Vector2(
				Mathf.Clamp(_camera.Zoom.X, _minZoom, _maxZoom),
				Mathf.Clamp(_camera.Zoom.Y, _minZoom, _maxZoom)
			);
		}

		// --- MOUSE HOVER UI ---
		if (@event is InputEventMouseMotion)
		{
			// GetGlobalMousePosition automatically does the math for Camera pan and zoom!
			Vector2I hoveredHex = PixelToHex(GetGlobalMousePosition());

			if (_hexContents.ContainsKey(hoveredHex))
			{
				MapEntity entity = _hexContents[hoveredHex];
				_infoLabel.Text = $"[ {entity.Name.ToUpper()} ]\n" +
								  $"Type: {entity.Type}\n" +
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
	// UI LOGIC
	// ==========================================
	private void SetupHoverUI()
	{
		CanvasLayer uiLayer = new CanvasLayer { Layer = 10 }; // Always on top
		AddChild(uiLayer);

		_infoPanel = new Panel();
		_infoPanel.CustomMinimumSize = new Vector2(250, 110);
		_infoPanel.Position = new Vector2(20, 20); 
		
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f); 
		style.BorderWidthBottom = 2;
		style.BorderWidthTop = 2;
		style.BorderWidthLeft = 2;
		style.BorderWidthRight = 2;
		style.BorderColor = new Color(0.2f, 0.8f, 1f, 1f); 
		style.CornerRadiusTopLeft = 5;
		style.CornerRadiusBottomRight = 5;
		_infoPanel.AddThemeStyleboxOverride("panel", style);

		_infoLabel = new Label();
		_infoLabel.Position = new Vector2(15, 15);
		_infoLabel.CustomMinimumSize = new Vector2(220, 80); 
		_infoLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		
		_infoPanel.AddChild(_infoLabel);
		uiLayer.AddChild(_infoPanel);
		
		_infoPanel.Visible = false; 
	}

	private Vector2I PixelToHex(Vector2 pt)
	{
		// No more mapCenter offset needed! The grid is built around real absolute Zero now.
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
		// Since we have a camera now, we generate a massive static board around (0,0).
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
				
				// Optional: Comment these lines out if you want to hide the coordinate text
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
		int currentOrbitRing = 2; // The first planet spawns 2 hexes away from the sun

		foreach (PlanetData pData in currentSystem.Planets)
		{
			Vector2I spawnHex = FindEmptyHexInRing(currentOrbitRing, rng);
			currentOrbitRing += 3; // Planets now spawn 3 hexes apart so there is plenty of room for ships!

			string pTypeStr = GetPlanetTypeString(pData.TypeIndex);
			string pTex = GetTexturePathForType(pTypeStr);

			MapEntity planetEntity = new MapEntity { 
				Name = pData.Name, 
				Type = "Planet", 
				Details = $"Biome Class: {pTypeStr.ToUpper()}\nHab: {pData.Habitability}" 
			};

			SpawnEntityAtHex(spawnHex, pTex, planetEntity, 0.4f);

			if (pData.Name == _globalData.SavedPlanet)
			{
				basePlanetLocation = spawnHex;
				planetEntity.Type = "Base Planet (Player Start)";
			}
		}

		if (_globalData.SelectedPlayerFleet != null && _globalData.SelectedPlayerFleet.Count > 0)
		{
			int currentDirIndex = 0;

			foreach (string shipName in _globalData.SelectedPlayerFleet)
			{
				while (currentDirIndex < 6)
				{
					Vector2I spawnPos = basePlanetLocation + _hexDirections[currentDirIndex];
					currentDirIndex++;
					
					if (_hexGrid.ContainsKey(spawnPos) && !_hexContents.ContainsKey(spawnPos))
					{
						MapEntity shipData = new MapEntity { Name = shipName, Type = "Player Fleet", Details = "Status: Online & Awaiting Orders" };
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
}
