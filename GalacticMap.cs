using Godot;
using System;
using System.Collections.Generic;

public partial class GalacticMap : Control
{
	// --- SCENE & UI REFERENCES ---
	private PackedScene starScene = GD.Load<PackedScene>("res://star_node.tscn");
	private SystemWindow _systemWindow;
	private GlobalData _globalData; // Reference to our singleton

	// --- LORE DATA ---
	private List<string> prefixes = new List<string> { 
		"Aether", "Void", "Core", "Verdant", "Obsidian", "Ember", "Luminous", 
		"Echo", "Spindle", "Nadir", "Helix", "Throne", "Nexus", "Zenith" 
	};
	private List<string> suffixes = new List<string> { 
		"Reach", "Hold", "Gate", "Station", "Spire", "Belt", "Wastes", 
		"Shroud", "Wound", "Spiral", "Silence", "Verge", "Relic", "Vault" 
	};

	public override void _Ready()
	{
		// Link our singletons and UI nodes
		_globalData = GetNode<GlobalData>("/root/GlobalData");
		_systemWindow = GetNode<SystemWindow>("SystemWindow");
		
		// Hide window initially
		_systemWindow.Visible = false;

		// --- THE DATA CHECK ---
		// If we already have stars saved in memory, load them. Otherwise, generate.
		if (_globalData.CurrentSectorStars != null && _globalData.CurrentSectorStars.Count > 0)
		{
			LoadSectorFromMemory();
		}
		else
		{
			GenerateAndSaveSector(40);
		}
	}

	// ==========================================
	// DATA GENERATION & LOADING
	// ==========================================

	private void GenerateAndSaveSector(int amount)
	{
		Vector2 screenSize = GetViewportRect().Size;
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		for (int i = 0; i < amount; i++)
		{
			// 1. Create the Blueprint (Data)
			StarMapData newStarData = new StarMapData();
			
			newStarData.SystemName = prefixes[rng.RandiRange(0, prefixes.Count - 1)] + "-" + 
									 suffixes[rng.RandiRange(0, suffixes.Count - 1)] + " " + 
									 rng.RandiRange(1, 99);
			
			newStarData.PlanetCount = rng.RandiRange(1, 8);
			
			int randomX = rng.RandiRange(200, (int)screenSize.X - 250);
			int randomY = rng.RandiRange(150, (int)screenSize.Y - 150);
			newStarData.MapPosition = new Vector2(randomX, randomY);

			newStarData.StarScale = rng.RandfRange(0.003f, 0.006f);
			newStarData.StarColor = new Color(
				rng.RandfRange(0.7f, 1.0f), 
				rng.RandfRange(0.7f, 1.0f), 
				rng.RandfRange(0.7f, 1.0f)
			);

			// 2. Save it to Global Memory
			_globalData.CurrentSectorStars.Add(newStarData);

			// 3. Draw it on screen
			DrawStarNode(newStarData);
		}
	}

	private void LoadSectorFromMemory()
	{
		// Loop through the saved data and draw each star exactly as it was
		foreach (StarMapData savedStar in _globalData.CurrentSectorStars)
		{
			DrawStarNode(savedStar);
		}
	}

	// ==========================================
	// VISUAL RENDERING
	// ==========================================

	private void DrawStarNode(StarMapData data)
	{
		Button newStar = starScene.Instantiate<Button>();
		AddChild(newStar); 

		// Apply saved positioning and text
		newStar.Position = data.MapPosition;
		newStar.Text = data.SystemName;

		// Button Settings
		newStar.Alignment = HorizontalAlignment.Center;
		newStar.CustomMinimumSize = new Vector2(1, 1);
		newStar.ClipText = false;

		// Apply saved visuals
		Sprite2D starSprite = newStar.GetNode<Sprite2D>("Sprite2D");
		starSprite.Scale = new Vector2(data.StarScale, data.StarScale);
		starSprite.Modulate = data.StarColor;
		starSprite.Position = new Vector2(newStar.Size.X / 2, 30);

		// Connect the click event using the data blueprint
		newStar.Pressed += () => _on_star_clicked(data, newStar);
	}

	// ==========================================
	// UI SIGNALS
	// ==========================================

	private void _on_star_clicked(StarMapData data, Button clickedStar)
	{
		if (!IsInstanceValid(_systemWindow)) return;

		// Save the currently selected system name to global memory
		_globalData.SavedSystem = data.SystemName; 

		// Pass data to the SystemWindow
		_systemWindow.SetupWindow(data.SystemName, data.PlanetCount);
		
		// Position the window
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
		if (IsInstanceValid(_systemWindow))
			_systemWindow.Visible = false;

		// Clear existing visual nodes
		foreach (Node child in GetChildren())
		{
			if (child is Button && child.Name != "RandomizeButton" && child.Name != "MenuButton")
			{
				child.QueueFree();
			}
		}

		// Wipe the old sector data from memory and generate a fresh one
		_globalData.CurrentSectorStars.Clear();
		GenerateAndSaveSector(40);
	}

	public void _on_close_button_pressed()
	{
		if (IsInstanceValid(_systemWindow))
			_systemWindow.Visible = false;
	}

	public void _on_menu_button_pressed()
	{
		//GetTree().ChangeSceneToFile("res://main_menu.tscn");
		var transitioner = GetNode<SceneTransition>("/root/SceneTransition");
		transitioner.ChangeScene("res://main_menu.tscn");
	}
}
