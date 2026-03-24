using Godot;
using System;
using System.Collections.Generic;

public partial class SystemView : Node2D
{
	// --- GLOBAL DATA REFERENCE ---
	private GlobalData _globalData;

	// --- UI & SCENE REFERENCES ---
	private Label _systemNameLabel;
	private Label _sunNameLabel;
	private Node2D _planetContainer;

	// UI Panel References
	private Panel _infoPanel;
	private Label _pNameLabel;
	private Label _pTypeLabel;
	private Label _pHabitabilityLabel; 
	private Label _pResourceLabel;     
	private TextureRect _pPreview;
	private Button _startFleetButton; 

	private PackedScene _planetScene = GD.Load<PackedScene>("res://planet_base.tscn");
	private List<Texture2D> _planetTextures = new List<Texture2D>();

	// --- ORBIT TRACKING ---
	private Vector2 _sunPos;
	private List<PlanetOrbitData> _planets = new List<PlanetOrbitData>();
	
	// --- BACKGROUND STARS ---
	private List<Vector2> _bgStarPositions = new List<Vector2>();
	private int _bgStarCount = 150;

	// --- LORE DATA FOR PLANETS ---
	private string[] _planetTypeNames = { "Terra", "Arid", "Ocean", "Toxic", "Frozen", "Lava" };

	private List<string> planetPrefixes = new List<string> { 
		"Aero", "Geo", "Cryo", "Pyro", "Litho", "Xeno", "Nova", "Terra", 
		"Vex", "Dune", "Krios", "Pell", "Zan", "Cor", "Min", "Alta", 
		"Bale", "Ceti", "Drax", "Elen", "Feron", "Gali", "Helio", "Icar", 
		"Jova", "Kael", "Lira", "Muta", "Nari", "Orio", "Pax", "Qira", 
		"Ryl", "Sola", "Tari", "Ura", "Vega", "Xyl", "Yura", "Zeta", 
		"Aegis", "Bore", "Chron", "Draco", "Elys", "Fen", "Gorg", "Hath", 
		"Ignis", "Kora", "Lumos", "Mora", "Nyx", "Omic", "Prom", "Quas"
	};

	private List<string> planetSuffixes = new List<string> { 
		"Prime", "Major", "Minor", "Alpha", "Beta", "Gamma", "Delta", 
		"Secundus", "Tertius", "Quartus", "Quintus", "Dawn", "Fall", 
		"V", "VII", "IX", "Blight", "Ridge", "Point", "World", "Sphere", 
		"Core", "Abyss", "Exis", "ius", "ia", "ium", "on", "en", "ath", 
		"eth", "ith", "oth", "uth", "Centauri", "Eridani", "Cygni", 
		"Lyrae", "Draconis", "Pegasi", "Persei", "Tauri", "Aquilae", 
		"Arietis", "Cancri", "Canis", "Ceti", "-A", "-B", "-X", 
		"Station", "Outpost", "Terminus", "Vanguard", "Haven", "Reach"
	};

	private class PlanetOrbitData
	{
		public Node2D Node;
		public PlanetData Data; 
		public float Angle;
	}

	public override void _Ready()
	{
		// Link to the Autoload Singleton!
		_globalData = GetNode<GlobalData>("/root/GlobalData");

		_systemNameLabel = GetNodeOrNull<Label>("SystemNameLabel");
		_sunNameLabel = GetNodeOrNull<Label>("SunNameLabel");
		_planetContainer = GetNodeOrNull<Node2D>("PlanetContainer");

		// Info Panel UI Setup
		_infoPanel = GetNodeOrNull<Panel>("InfoPanel");
		_pNameLabel = GetNodeOrNull<Label>("InfoPanel/VBoxContainer/PlanetNameLabel");
		_pTypeLabel = GetNodeOrNull<Label>("InfoPanel/VBoxContainer/PlanetTypeLabel");
		_pHabitabilityLabel = GetNodeOrNull<Label>("InfoPanel/VBoxContainer/PlanetHabitabilityLabel");
		_pResourceLabel = GetNodeOrNull<Label>("InfoPanel/VBoxContainer/PlanetResourceLabel");
		_pPreview = GetNodeOrNull<TextureRect>("InfoPanel/VBoxContainer/PlanetPreview");
		_startFleetButton = GetNodeOrNull<Button>("InfoPanel/VBoxContainer/StartFleetButton");

		if (_infoPanel != null) _infoPanel.Visible = false;

		if (_startFleetButton != null)
		{
			_startFleetButton.Pressed += OnStartFleetPressed;
		}

		// Load Textures
		string pPath = "res://Planets/";
		string[] files = { "terra_planet.png", "arid_planet.png", "ocean_planet.png", "toxic_planet.png", "frozen_planet.png", "lava_planet.png" };
		foreach (var f in files)
		{
			Texture2D tex = GD.Load<Texture2D>(pPath + f);
			if (tex != null) _planetTextures.Add(tex);
		}

		GenerateBackgroundStars();

		// --- NEW: THE RETURN TRIP CHECK ---
		// Automatically reload the system if we just came back from the Fleet screen!
		if (!string.IsNullOrEmpty(_globalData.SavedSystem) && _globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			InitializeSystem(_globalData.SavedSystem, 0); 
		}
	}

	public void InitializeSystem(string systemName, int planetCount)
	{
		_sunPos = GetViewportRect().Size / 2;

		if (_sunNameLabel != null) 
		{
			_sunNameLabel.Text = systemName.ToUpper();
			_sunNameLabel.Position = _sunPos + new Vector2(0, -70);
			_sunNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		}
		
		if (_systemNameLabel != null) 
		{
			_systemNameLabel.Text = $"SECTOR: {systemName.ToUpper()}";
		}

		var sun = GetNodeOrNull<Node2D>("SunVisual");
		if (sun != null) sun.Position = _sunPos;

		// Clear old moving planets
		foreach(var p in _planets) p.Node.QueueFree();
		_planets.Clear();

		// --- THE MEMORY CHECK ---
		SystemData currentSystemData;

		// Check our GlobalData dictionary
		if (_globalData.ExploredSystems.ContainsKey(systemName))
		{
			currentSystemData = _globalData.ExploredSystems[systemName];
		}
		else
		{
			currentSystemData = GenerateNewSystemData(systemName, planetCount);
			_globalData.ExploredSystems.Add(systemName, currentSystemData);
		}

		// Spawn the graphics based on the data
		SpawnPlanetVisuals(currentSystemData);
	}

	private SystemData GenerateNewSystemData(string sysName, int count)
	{
		Random rng = new Random();
		var newSys = new SystemData { SystemName = sysName };

		for (int i = 0; i < count; i++)
		{
			int texIndex = rng.Next(_planetTextures.Count);
			string typeName = _planetTypeNames[texIndex];

			var pData = new PlanetData
			{
				Name = GenerateRandomPlanetName(rng).ToUpper(),
				TypeIndex = texIndex,
				Distance = 150f + (i * 80f),
				Speed = 0.5f / (1.0f + i * 0.5f),
				Habitability = GenerateHabitability(typeName),
				Resources = GenerateResources(typeName),
				
				// --- VISUALS GENERATED ONCE AND SAVED ---
				Scale = (float)rng.NextDouble() * 0.35f + 0.05f,
				StartingAngle = (float)rng.NextDouble() * Mathf.Pi * 2
			};

			newSys.Planets.Add(pData);
		}
		return newSys;
	}

	private void SpawnPlanetVisuals(SystemData sysData)
	{
		// Notice we removed the random generator from here! 
		// We only want to use saved data now.

		foreach (var planetData in sysData.Planets)
		{
			Node2D planetInstance = _planetScene.Instantiate<Node2D>();
			Sprite2D sprite = planetInstance.GetNodeOrNull<Sprite2D>("PlanetSprite");
			
			if (sprite != null && _planetTextures.Count > 0)
			{
				sprite.Texture = _planetTextures[planetData.TypeIndex];
				
				// --- APPLY THE EXACT SAVED SCALE ---
				sprite.Scale = new Vector2(planetData.Scale, planetData.Scale); 

				Area2D area = planetInstance.GetNodeOrNull<Area2D>("ClickArea");
				if (area != null)
				{
					PlanetData dataCopy = planetData;
					area.InputEvent += (viewport, @event, shapeIdx) => 
						_on_Planet_input_event(@event, dataCopy);
				}
			}

			Label nameLabel = planetInstance.GetNodeOrNull<Label>("PlanetNameLabel");
			if (nameLabel != null)
			{
				nameLabel.Text = planetData.Name;
				nameLabel.Position = new Vector2(-25, 25);
				nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			}

			_planetContainer.AddChild(planetInstance);

			var visualOrbit = new PlanetOrbitData
			{
				Node = planetInstance,
				Data = planetData, 
				
				// --- APPLY THE EXACT SAVED ANGLE ---
				Angle = planetData.StartingAngle
			};
			_planets.Add(visualOrbit);
		}
	}

	private void _on_Planet_input_event(InputEvent @event, PlanetData pData)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			if (_infoPanel == null) return;
			
			_pNameLabel.Text = pData.Name;
			_pTypeLabel.Text = $"TYPE: {_planetTypeNames[pData.TypeIndex].ToUpper()}";
			
			if (_pPreview != null && pData.TypeIndex < _planetTextures.Count)
			{
				_pPreview.Texture = _planetTextures[pData.TypeIndex];
			}

			if (_pHabitabilityLabel != null) _pHabitabilityLabel.Text = $"HABITABILITY: {pData.Habitability}";
			if (_pResourceLabel != null) _pResourceLabel.Text = $"RESOURCES: {pData.Resources}";

			_infoPanel.Visible = true;
		}
	}

	private void OnStartFleetPressed()
	{
		// 1. Save the locked-in choice to the memory singleton
		_globalData.SavedPlanet = _pNameLabel.Text;
		string currentType = _pTypeLabel.Text.Replace("TYPE: ", "").Trim();
		_globalData.SavedType = currentType;

		// --- 2. THE DEBUG CHECK ---
		GD.Print("\n=== SAVING TO GLOBAL DATA ===");
		GD.Print($"Selected Star: {_globalData.SavedSystem}");
		GD.Print($"Saved Planet: {_globalData.SavedPlanet}");
		GD.Print($"Saved Type: {_globalData.SavedType}");
		GD.Print($"Total Systems Explored: {_globalData.ExploredSystems.Count}");
		GD.Print("=============================\n");

		// 3. Switch scenes
		GetTree().ChangeSceneToFile("res://fleet_selection.tscn");
	}

	// --- HELPER METHODS ---
	private string GenerateHabitability(string type)
	{
		switch (type) {
			case "Terra": return "Optimal (Breathable)";
			case "Arid": return "Challenging (Low Humidity)";
			case "Ocean": return "Moist (High Pressure)";
			case "Toxic": return "Lethal (Corrosive)";
			case "Frozen": return "Extreme Cold (Sub-Zero)";
			case "Lava": return "Molten (Unstable)";
			default: return "Unknown";
		}
	}

	private string GenerateResources(string type)
	{
		switch (type) {
			case "Terra": return "Organic Compounds, Fresh Water, Biodiversity";
			case "Arid": return "Silica, Rare Earth Metals, Solar Potential";
			case "Ocean": return "Hydrogen Isotopes, Exotic Bio-matter, Salt";
			case "Toxic": return "Chemical Sludge, Radioactive Isotopes, Sulfur";
			case "Frozen": return "Cryo-Gases, Solid Methane, Ice";
			case "Lava": return "Magmatic Ore, Diamonds, Obsidian";
			default: return "Scanned Data Unavailable";
		}
	}

	public void _on_close_info_pressed()
	{
		if (_infoPanel != null) _infoPanel.Visible = false;
	}

	private string GenerateRandomPlanetName(Random rng)
	{
		string prefix = planetPrefixes[rng.Next(planetPrefixes.Count)];
		string suffix = planetSuffixes[rng.Next(planetSuffixes.Count)];
		return (rng.NextDouble() > 0.5) ? $"{prefix} {suffix}" : $"{prefix}{suffix}";
	}

	public override void _Process(double delta)
	{
		foreach (var p in _planets)
		{
			p.Angle += p.Data.Speed * (float)delta;
			float x = _sunPos.X + Mathf.Cos(p.Angle) * p.Data.Distance;
			float y = _sunPos.Y + Mathf.Sin(p.Angle) * p.Data.Distance;
			if (IsInstanceValid(p.Node)) p.Node.Position = new Vector2(x, y);
		}
	}

	private void GenerateBackgroundStars()
	{
		var size = GetViewportRect().Size;
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < _bgStarCount; i++)
			_bgStarPositions.Add(new Vector2(rng.RandfRange(0, size.X), rng.RandfRange(0, size.Y)));
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), Colors.Black);
		foreach (var pos in _bgStarPositions) DrawCircle(pos, 1.0f, Colors.White);
	}

	public void _on_close_button_pressed()
	{
		GetTree().ChangeSceneToFile("res://galactic_map.tscn"); 
	}
}
