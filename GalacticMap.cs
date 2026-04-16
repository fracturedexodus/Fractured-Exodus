using Godot;
using System;
using System.Collections.Generic;

public partial class GalacticMap : Control
{
	// --- THE REGION IMAGE MASK ---
	[Export] public Texture2D RegionMaskTexture; 
	private Image _regionImage;

	// --- SCENE & UI REFERENCES ---
	private PackedScene starScene = GD.Load<PackedScene>("res://star_node.tscn");
	private SystemWindow _systemWindow;
	private GlobalData _globalData; 

	// --- WARP TRAJECTORY DRAWING ---
	private Vector2 _currentSystemMapPos = Vector2.Zero;
	private bool _isHoveringStar = false;
	private Vector2 _hoverTarget = Vector2.Zero;
	
	private Line2D _warpLine;
	private Polygon2D _originMarker;

	// --- LORE DATA ---
	private List<string> prefixes = new List<string> { 
		"Aether", "Void", "Core", "Verdant", "Obsidian", "Ember", "Luminous", 
		"Echo", "Spindle", "Nadir", "Helix", "Throne", "Nexus", "Zenith" 
	};
	private List<string> suffixes = new List<string> { 
		"Reach", "Hold", "Gate", "Station", "Spire", "Belt", "Wastes", 
		"Shroud", "Wound", "Spiral", "Silence", "Verge", "Relic", "Vault" 
	};

	// --- COLOR DEFINITIONS FOR YOUR REGIONS ---
	private Dictionary<string, Color> regionColors = new Dictionary<string, Color>()
	{
		{ "Far Silence", Color.FromHtml("#CF1278") },     // Pink
		{ "Verdant Shroud", new Color(0f, 1f, 0f) },  // Green
		{ "Core Spindle", new Color(1f, 1f, 1f) },    // White
		{ "Luminous Verge", new Color(0.5f, 0f, 0.5f)},// Purple
		{ "Ember Wastes", new Color(1f, 0f, 0f) },    // Red
		{ "Shattered Reach", new Color(1f, 0.5f, 0f)},// Orange
		{ "Obsidian Belt", new Color(1f, 1f, 0f) },   // Yellow
		{ "Echo Spiral", new Color(0f, 0f, 1f) }      // Blue
	};

	public override void _Ready()
	{
		_globalData = GetNode<GlobalData>("/root/GlobalData");
		_systemWindow = GetNode<SystemWindow>("SystemWindow");
		_systemWindow.Visible = false;

		_warpLine = new Line2D();
		_warpLine.Width = 3.0f;
		_warpLine.DefaultColor = new Color(1f, 1f, 1f, 0.8f); 
		_warpLine.ZIndex = 100; 
		AddChild(_warpLine);

		_originMarker = new Polygon2D();
		_originMarker.Color = new Color(0.2f, 0.8f, 1f, 1f); 
		_originMarker.Polygon = CreateCirclePolygon(6.0f);
		_originMarker.ZIndex = 100;
		_originMarker.Visible = false;
		AddChild(_originMarker);

		if (RegionMaskTexture != null)
		{
			_regionImage = RegionMaskTexture.GetImage();
		}
		else
		{
			GD.PrintErr("WARNING: No Region Mask Image assigned to GalacticMap!");
		}

		if (_globalData.CurrentSectorStars != null && _globalData.CurrentSectorStars.Count > 0)
			LoadSectorFromMemory();
		else
			GenerateAndSaveSector(40);
	}

	private Vector2[] CreateCirclePolygon(float radius)
	{
		Vector2[] points = new Vector2[32];
		for (int i = 0; i < 32; i++)
		{
			float angle = (i / 32f) * Mathf.Pi * 2f;
			points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}
		return points;
	}

	public override void _Process(double delta)
	{
		if (_globalData != null && _globalData.JustJumped && _currentSystemMapPos != Vector2.Zero)
		{
			_originMarker.Position = _currentSystemMapPos;
			_originMarker.Visible = true;

			Vector2 targetPos = _isHoveringStar ? _hoverTarget : GetGlobalMousePosition();
			
			_warpLine.ClearPoints();
			_warpLine.AddPoint(_currentSystemMapPos);
			_warpLine.AddPoint(targetPos);
		}
		else
		{
			_warpLine.ClearPoints();
			_originMarker.Visible = false;
		}
	}

	// ==========================================
	// DATA GENERATION (THE UNIVERSE BUILDER)
	// ==========================================

	private void GenerateAndSaveSector(int amount)
	{
		Vector2 screenSize = GetViewportRect().Size;
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		string[] habitabilityTypes = { "Barren", "Toxic", "Frozen", "Volcanic", "Temperate", "Gas Giant" };

		for (int i = 0; i < amount; i++)
		{
			StarMapData newStarData = new StarMapData();
			
			newStarData.SystemName = prefixes[rng.RandiRange(0, prefixes.Count - 1)] + "-" + 
									 suffixes[rng.RandiRange(0, suffixes.Count - 1)] + " " + 
									 rng.RandiRange(1, 99);
			
			newStarData.PlanetCount = rng.RandiRange(1, 8);
			
			int randomX = rng.RandiRange(200, (int)screenSize.X - 250);
			int randomY = rng.RandiRange(150, (int)screenSize.Y - 150);
			newStarData.MapPosition = new Vector2(randomX, randomY);

			newStarData.Region = DetermineRegionFromImage(newStarData.MapPosition, screenSize);

			newStarData.StarScale = rng.RandfRange(0.003f, 0.006f);
			newStarData.StarColor = new Color(
				rng.RandfRange(0.7f, 1.0f), rng.RandfRange(0.7f, 1.0f), rng.RandfRange(0.7f, 1.0f)
			);

			_globalData.CurrentSectorStars.Add(newStarData);
			DrawStarNode(newStarData);

			// --- NEW: PRE-GENERATE THE ENTIRE SYSTEM IN MEMORY ---
			SystemData newSystem = new SystemData();
			newSystem.SystemName = newStarData.SystemName;
			newSystem.HasBeenVisited = false;

			// Generate the Planets for this system upfront
			for (int p = 0; p < newStarData.PlanetCount; p++)
			{
				PlanetData newPlanet = new PlanetData();
				
				// Naming convention (e.g., "Aether-Reach 45 Prime-1")
				newPlanet.Name = $"{newStarData.SystemName} Prime-{p + 1}";
				newPlanet.TypeIndex = rng.RandiRange(0, 5); 
				newPlanet.Scale = rng.RandfRange(0.4f, 1.2f);
				newPlanet.Habitability = habitabilityTypes[rng.RandiRange(0, habitabilityTypes.Length - 1)];
				newPlanet.HasBeenScanned = false;
				newPlanet.HasBeenSalvaged = false;

				newSystem.Planets.Add(newPlanet);
			}

			// Add the newly built system to the universal memory bank
			_globalData.ExploredSystems[newSystem.SystemName] = newSystem;
		}

		// --- NEW: Save the generated universe to the hard drive immediately ---
		if (_globalData != null)
		{
			_globalData.SaveGame();
			GD.Print($"[UNIVERSE BUILDER] Successfully generated and saved {amount} distinct star systems.");
		}
	}

	private void LoadSectorFromMemory()
	{
		foreach (StarMapData savedStar in _globalData.CurrentSectorStars)
		{
			DrawStarNode(savedStar);
		}
	}

	private void DrawStarNode(StarMapData data)
	{
		Button newStar = starScene.Instantiate<Button>();
		AddChild(newStar); 

		newStar.Position = data.MapPosition;
		newStar.Text = data.SystemName;
		newStar.Alignment = HorizontalAlignment.Center;
		newStar.CustomMinimumSize = new Vector2(1, 1);
		newStar.ClipText = false;

		newStar.TooltipText = $"System: {data.SystemName}\nPlanets: {data.PlanetCount}\nRegion: {data.Region}";

		Sprite2D starSprite = newStar.GetNode<Sprite2D>("Sprite2D");
		starSprite.Scale = new Vector2(data.StarScale, data.StarScale);
		starSprite.Modulate = data.StarColor;
		starSprite.Position = new Vector2(newStar.Size.X / 2, 30);

		if (_globalData != null && _globalData.JustJumped && data.SystemName == _globalData.SavedSystem)
		{
			_currentSystemMapPos = data.MapPosition + new Vector2(50, 30); 
		}

		newStar.MouseEntered += () => 
		{
			_isHoveringStar = true;
			_hoverTarget = newStar.Position + new Vector2(50, 30);
		};
		newStar.MouseExited += () => 
		{
			_isHoveringStar = false;
		};

		newStar.Pressed += () => _on_star_clicked(data, newStar);
	}

	// ==========================================
	// THE IMAGE READING LOGIC
	// ==========================================

	private string DetermineRegionFromImage(Vector2 starPos, Vector2 screenSize)
	{
		if (_regionImage == null) return "Unknown Sector";

		float ratioX = starPos.X / screenSize.X;
		float ratioY = starPos.Y / screenSize.Y;
		
		int pixelX = Mathf.FloorToInt(ratioX * _regionImage.GetWidth());
		int pixelY = Mathf.FloorToInt(ratioY * _regionImage.GetHeight());

		pixelX = Mathf.Clamp(pixelX, 0, _regionImage.GetWidth() - 1);
		pixelY = Mathf.Clamp(pixelY, 0, _regionImage.GetHeight() - 1);

		Color pixelColor = _regionImage.GetPixel(pixelX, pixelY);

		string closestRegion = "Unknown Sector";
		float minDistance = float.MaxValue;

		foreach (var kvp in regionColors)
		{
			float dist = Mathf.Pow(pixelColor.R - kvp.Value.R, 2) + 
						 Mathf.Pow(pixelColor.G - kvp.Value.G, 2) + 
						 Mathf.Pow(pixelColor.B - kvp.Value.B, 2);

			if (dist < minDistance)
			{
				minDistance = dist;
				closestRegion = kvp.Key;
			}
		}

		return closestRegion;
	}

	// ==========================================
	// UI SIGNALS
	// ==========================================

	private void _on_star_clicked(StarMapData data, Button clickedStar)
	{
		if (!IsInstanceValid(_systemWindow)) return;

		_globalData.SavedSystem = data.SystemName; 
		
		if (_globalData.JustJumped)
		{
			var transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
			if (transitioner != null) 
			{
				transitioner.ChangeScene("res://exploration_battle.tscn");
			}
			else 
			{
				GetTree().ChangeSceneToFile("res://exploration_battle.tscn");
			}
			return; 
		}

		_systemWindow.SetupWindow(data.SystemName, data.PlanetCount, data.Region);
		
		Vector2 starPos = clickedStar.Position;
		Vector2 windowSize = _systemWindow.Size;
		Vector2 screenSize = GetViewportRect().Size;

		Vector2 targetPosition = new Vector2(starPos.X + 60, starPos.Y - (windowSize.Y / 2));
		targetPosition.X = Mathf.Clamp(targetPosition.X, 0, screenSize.X - windowSize.X);
		targetPosition.Y = Mathf.Clamp(targetPosition.Y, 0, screenSize.Y - windowSize.Y);

		_systemWindow.Position = targetPosition;
		_systemWindow.Visible = true;
	}

	public void _on_randomize_button_pressed()
	{
		if (IsInstanceValid(_systemWindow)) _systemWindow.Visible = false;

		foreach (Node child in GetChildren())
		{
			if (child is Button && child.Name != "RandomizeButton" && child.Name != "MenuButton")
			{
				child.QueueFree();
			}
		}

		_globalData.CurrentSectorStars.Clear();
		GenerateAndSaveSector(40);
	}

	public void _on_close_button_pressed()
	{
		if (IsInstanceValid(_systemWindow)) _systemWindow.Visible = false;
	}

	public void _on_menu_button_pressed()
	{
		var transitioner = GetNode<SceneTransition>("/root/SceneTransition");
		transitioner.ChangeScene("res://main_menu.tscn");
	}
}
