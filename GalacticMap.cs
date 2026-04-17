using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
	private StarMapData _hoveredStarData = null; 
	private Vector2 _hoverTarget = Vector2.Zero;
	
	private Line2D _warpLine;
	private Polygon2D _originMarker;

	// --- STARTING MENU PANELS ---
	private PanelContainer _regionSelectionMenu;
	private PanelContainer _regionInfoPanel;
	private RichTextLabel _regionInfoText;

	// --- HOVER PANELS (System Info & FTL Jump) ---
	private PanelContainer _jumpInfoPanel;
	private RichTextLabel _jumpInfoText;
	private PanelContainer _systemHoverPanel;
	private RichTextLabel _systemHoverText;

	// --- LORE DATA ---
	private List<string> prefixes = new List<string> { 
		"Nova", "Astra", "Helios", "Kaelen", "Triton", "Orion", "Vanguard", 
		"Polaris", "Sirius", "Altair", "Draxis", "Vesper", "Sol", "Rigel" 
	};
	private List<string> suffixes = new List<string> { 
		"Cluster", "Sector", "Expanse", "Nebula", "Drift", "Abyss", "Zone", 
		"Quadrant", "Matrix", "System", "Cloud", "Frontier", "Tide", "Halo" 
	};

	// --- REGION LORE DICTIONARY (From World Bible) ---
	private Dictionary<string, string> regionLore = new Dictionary<string, string>()
	{
		{ "Far Silence", "The vast, seemingly empty edge of known space. Believed to be untainted by the madness of the Core Spindle, it offers a true chance to escape the Aetherweb entirely." },
		{ "Verdant Shroud", "One of the few regions with functioning biospheres, where factions like the Verdant Pact fight over habitable worlds shadowed by dormant machines." },
		{ "Core Spindle", "The hyper-dense center of the galaxy, now a chaotic maelstrom of rogue AI, temporal anomalies, and malfunctioning megastructures." },
		{ "Luminous Verge", "A brightly lit nebula sector teeming with ancient energy anomalies, dangerous stellar phenomena, and the remnants of crystalline entities." },
		{ "Ember Wastes", "A volatile sector of dying red giants scoured by solar winds. It is dominated by mercenary scavengers like the Pyric Consortium and the Furnace Kin." },
		{ "Shattered Reach", "A chaotic expanse of broken planets, shattered moons, and gravitational anomalies resulting from catastrophic ancient weapons." },
		{ "Obsidian Belt", "A dark asteroid megastructure ring that serves as a hub for black market trade and forbidden AI experimentation. Home to data traffickers and memory-encrypting mystics." },
		{ "Echo Spiral", "A warped spiral arm plagued by causality loops and time fractures resulting from intense AI temporal manipulation. Factions here worship the paradox." }
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

		BuildHoverUI(); 

		if (string.IsNullOrEmpty(_globalData.SavedSystem))
		{
			BuildRegionSelectionUI();
		}
		else
		{
			Button randomizeBtn = FindChild("RandomizeButton", true, false) as Button;
			Button menuBtn = FindChild("MenuButton", true, false) as Button;
			
			if (randomizeBtn != null) randomizeBtn.Visible = false;
			if (menuBtn != null) menuBtn.Visible = false;
		}
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
		Vector2 mousePos = GetGlobalMousePosition();
		Vector2 screenSize = GetViewportRect().Size;

		// 1. MANAGE THE SYSTEM HOVER BOX (LEFT SIDE)
		if (_isHoveringStar && _hoveredStarData != null)
		{
			_systemHoverText.Text = $"[color=cyan][b]=== {_hoveredStarData.SystemName.ToUpper()} ===[/b][/color]\nPlanets: {_hoveredStarData.PlanetCount}\nRegion: {_hoveredStarData.Region}";
			
			// Position on the LEFT side of the cursor
			Vector2 sysPos = mousePos - new Vector2(_systemHoverPanel.Size.X + 20, 20);
			
			// Screen boundaries check
			if (sysPos.X < 0) sysPos.X = mousePos.X + 20; // Flip to right if off screen left
			if (sysPos.Y + _systemHoverPanel.Size.Y > screenSize.Y) sysPos.Y = screenSize.Y - _systemHoverPanel.Size.Y - 10;
			if (sysPos.Y < 0) sysPos.Y = 10;

			_systemHoverPanel.Position = sysPos;
			_systemHoverPanel.Visible = true;
		}
		else
		{
			_systemHoverPanel.Visible = false;
		}

		// 2. MANAGE THE FTL JUMP TRAJECTORY (RIGHT SIDE)
		if (_globalData != null && _globalData.JustJumped && _currentSystemMapPos != Vector2.Zero)
		{
			_originMarker.Position = _currentSystemMapPos;
			_originMarker.Visible = true;

			Vector2 targetPos = _isHoveringStar ? _hoverTarget : mousePos;
			float dist = _currentSystemMapPos.DistanceTo(targetPos);
			
			_warpLine.ClearPoints();
			_warpLine.AddPoint(_currentSystemMapPos);
			_warpLine.AddPoint(targetPos);

			// Hide the warp line if we are hovering directly over the star we are currently in
			if (dist < 5.0f) 
			{
				_warpLine.DefaultColor = new Color(0,0,0,0);
				if (IsInstanceValid(_jumpInfoPanel)) _jumpInfoPanel.Visible = false;
			}
			else
			{
				// --- UPDATED: 10 RAW MATERIALS PER 5 PIXELS MULTIPLIER (x2.0) ---
				float rawCost = Mathf.Max(1f, Mathf.Round(dist * 2.0f));
				float energyCost = 1.0f; // Flat cost to fire the gate
				
				bool canAfford = true;
				float cRaw = 0f;
				float cEne = 0f;

				if (_globalData.FleetResources != null && _globalData.FleetResources.ContainsKey("Raw Materials"))
				{
					 cRaw = _globalData.FleetResources["Raw Materials"].AsSingle();
					 cEne = _globalData.FleetResources["Energy Cores"].AsSingle();
					 if (cRaw < rawCost || cEne < energyCost) canAfford = false;
				}
				
				_warpLine.DefaultColor = canAfford ? new Color(0f, 1f, 0.5f, 0.8f) : new Color(1f, 0f, 0f, 0.8f);

				if (_isHoveringStar && IsInstanceValid(_jumpInfoPanel))
				{
					_jumpInfoText.Text = $"[color=yellow][b]=== FTL TRAJECTORY ===[/b][/color]\n\nDistance: {Mathf.Round(dist)}ly\n\n[b]JUMP COST:[/b]\nRaw Materials: {rawCost}\nEnergy Cores: {energyCost}\n\n" + 
										 (canAfford ? "[color=green]RESOURCES OPTIMAL[/color]" : $"[color=red]INSUFFICIENT RESOURCES[/color]\nAvailable: {cRaw:0.#} Raw, {cEne:0.#} Energy");
					
					// Position on the RIGHT side of the cursor
					Vector2 jumpPos = mousePos + new Vector2(20, -20);
					
					// Screen boundaries check
					if (jumpPos.X + _jumpInfoPanel.Size.X > screenSize.X) jumpPos.X = mousePos.X - _jumpInfoPanel.Size.X - 20;
					if (jumpPos.Y + _jumpInfoPanel.Size.Y > screenSize.Y) jumpPos.Y = screenSize.Y - _jumpInfoPanel.Size.Y - 10;
					if (jumpPos.Y < 0) jumpPos.Y = 10;

					_jumpInfoPanel.Position = jumpPos;
					_jumpInfoPanel.Visible = true;

					// Conflict resolution: If the jump panel had to flip left, push the system panel to the right
					if (jumpPos.X < mousePos.X && _systemHoverPanel.Visible) 
					{
						_systemHoverPanel.Position = new Vector2(mousePos.X + 20, _systemHoverPanel.Position.Y);
					}
				}
				else if (IsInstanceValid(_jumpInfoPanel))
				{
					_jumpInfoPanel.Visible = false;
				}
			}
		}
		else
		{
			_warpLine.ClearPoints();
			_originMarker.Visible = false;
			if (IsInstanceValid(_jumpInfoPanel)) _jumpInfoPanel.Visible = false;
		}
	}

	// ==========================================
	// START MENU & HOVER UI BUILDERS
	// ==========================================

	private void BuildHoverUI()
	{
		// 1. Build the FTL Jump Info Panel (Right Side)
		_jumpInfoPanel = new PanelContainer();
		_jumpInfoPanel.ZIndex = 200;
		_jumpInfoPanel.Visible = false;

		StyleBoxFlat jumpStyle = new StyleBoxFlat();
		jumpStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
		jumpStyle.BorderWidthTop = 2; jumpStyle.BorderWidthBottom = 2; jumpStyle.BorderWidthLeft = 2; jumpStyle.BorderWidthRight = 2;
		jumpStyle.BorderColor = new Color(1f, 0.6f, 0f, 0.8f); 
		jumpStyle.ContentMarginLeft = 15; jumpStyle.ContentMarginRight = 15; jumpStyle.ContentMarginTop = 15; jumpStyle.ContentMarginBottom = 15;
		_jumpInfoPanel.AddThemeStyleboxOverride("panel", jumpStyle);

		_jumpInfoPanel.CustomMinimumSize = new Vector2(250, 0);
		AddChild(_jumpInfoPanel);

		_jumpInfoText = new RichTextLabel();
		_jumpInfoText.BbcodeEnabled = true;
		_jumpInfoText.FitContent = true;
		_jumpInfoText.AddThemeColorOverride("default_color", new Color(1f, 1f, 1f));
		_jumpInfoText.AddThemeFontSizeOverride("normal_font_size", 14);
		_jumpInfoPanel.AddChild(_jumpInfoText);

		// 2. Build the System Hover Info Panel (Left Side)
		_systemHoverPanel = new PanelContainer();
		_systemHoverPanel.ZIndex = 200;
		_systemHoverPanel.Visible = false;

		StyleBoxFlat sysStyle = new StyleBoxFlat();
		sysStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
		sysStyle.BorderWidthTop = 2; sysStyle.BorderWidthBottom = 2; sysStyle.BorderWidthLeft = 2; sysStyle.BorderWidthRight = 2;
		sysStyle.BorderColor = new Color(0f, 1f, 1f, 0.8f); // Cyan border
		sysStyle.ContentMarginLeft = 15; sysStyle.ContentMarginRight = 15; sysStyle.ContentMarginTop = 15; sysStyle.ContentMarginBottom = 15;
		_systemHoverPanel.AddThemeStyleboxOverride("panel", sysStyle);

		_systemHoverPanel.CustomMinimumSize = new Vector2(200, 0);
		AddChild(_systemHoverPanel);

		_systemHoverText = new RichTextLabel();
		_systemHoverText.BbcodeEnabled = true;
		_systemHoverText.FitContent = true;
		_systemHoverText.AddThemeColorOverride("default_color", new Color(1f, 1f, 1f));
		_systemHoverText.AddThemeFontSizeOverride("normal_font_size", 14);
		_systemHoverPanel.AddChild(_systemHoverText);
	}

	private void BuildRegionSelectionUI()
	{
		_regionSelectionMenu = new PanelContainer();
		_regionSelectionMenu.ZIndex = 200; 
		
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
		style.BorderWidthTop = 2; style.BorderWidthBottom = 2; style.BorderWidthLeft = 2; style.BorderWidthRight = 2;
		style.BorderColor = new Color(0f, 1f, 1f, 0.5f);
		
		style.ContentMarginLeft = 15; style.ContentMarginRight = 15; style.ContentMarginTop = 15; style.ContentMarginBottom = 15;
		_regionSelectionMenu.AddThemeStyleboxOverride("panel", style);

		_regionSelectionMenu.SetAnchorsPreset(LayoutPreset.CenterLeft);
		_regionSelectionMenu.GrowVertical = GrowDirection.Both; 
		_regionSelectionMenu.Position = new Vector2(40, 0); 
		AddChild(_regionSelectionMenu);

		VBoxContainer menuContainer = new VBoxContainer();
		menuContainer.AddThemeConstantOverride("separation", 11);
		_regionSelectionMenu.AddChild(menuContainer);

		Label titleLabel = new Label();
		titleLabel.Text = "=== CHOOSE STARTING REGION ===";
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.AddThemeColorOverride("font_color", new Color(0f, 1f, 1f)); 
		titleLabel.AddThemeFontSizeOverride("font_size", 14); 
		menuContainer.AddChild(titleLabel);

		_regionInfoPanel = new PanelContainer();
		_regionInfoPanel.ZIndex = 200;
		_regionInfoPanel.Visible = false; // Hidden by default

		StyleBoxFlat infoStyle = new StyleBoxFlat();
		infoStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
		infoStyle.BorderWidthTop = 2; infoStyle.BorderWidthBottom = 2; infoStyle.BorderWidthLeft = 2; infoStyle.BorderWidthRight = 2;
		infoStyle.BorderColor = new Color(0f, 1f, 1f, 0.5f);
		infoStyle.ContentMarginLeft = 15; infoStyle.ContentMarginRight = 15; infoStyle.ContentMarginTop = 15; infoStyle.ContentMarginBottom = 15;
		_regionInfoPanel.AddThemeStyleboxOverride("panel", infoStyle);

		_regionInfoPanel.SetAnchorsPreset(LayoutPreset.CenterLeft);
		_regionInfoPanel.GrowVertical = GrowDirection.Both; 
		_regionInfoPanel.CustomMinimumSize = new Vector2(320, 0); 
		_regionInfoPanel.Position = new Vector2(330, 0); 
		AddChild(_regionInfoPanel);

		_regionInfoText = new RichTextLabel();
		_regionInfoText.BbcodeEnabled = true;
		_regionInfoText.FitContent = true; 
		_regionInfoText.AddThemeColorOverride("default_color", new Color(1f, 1f, 1f));
		_regionInfoText.AddThemeFontSizeOverride("normal_font_size", 14);
		_regionInfoPanel.AddChild(_regionInfoText);

		foreach (string regionName in regionColors.Keys)
		{
			Button btn = new Button();
			btn.Text = $"START IN: {regionName.ToUpper()}";
			
			btn.CustomMinimumSize = new Vector2(225, 38);
			btn.AddThemeFontSizeOverride("font_size", 13); 
			
			btn.AddThemeColorOverride("font_color", regionColors[regionName]); 
			
			string targetRegion = regionName; 
			btn.Pressed += () => StartGameInRegion(targetRegion);

			btn.MouseEntered += () => ShowRegionLore(targetRegion);
			btn.MouseExited += () => HideRegionLore();
			
			menuContainer.AddChild(btn);
		}
	}

	private void ShowRegionLore(string regionName)
	{
		if (!IsInstanceValid(_regionInfoPanel) || !IsInstanceValid(_regionInfoText)) return;

		string hexColor = regionColors.ContainsKey(regionName) ? "#" + regionColors[regionName].ToHtml(false) : "#ffffff";
		string lore = regionLore.ContainsKey(regionName) ? regionLore[regionName] : "Data corrupted. Unknown sector.";

		_regionInfoText.Text = $"[color={hexColor}][b]=== {regionName.ToUpper()} ===[/b][/color]\n\n{lore}";
		_regionInfoPanel.Visible = true;
	}

	private void HideRegionLore()
	{
		if (IsInstanceValid(_regionInfoPanel))
		{
			_regionInfoPanel.Visible = false;
		}
	}

	private void StartGameInRegion(string targetRegion)
	{
		if (_globalData == null || _globalData.CurrentSectorStars.Count == 0) return;

		List<StarMapData> starsInRegion = _globalData.CurrentSectorStars
			.Where(star => star.Region == targetRegion)
			.ToList();

		if (starsInRegion.Count == 0)
		{
			GD.PrintErr($"No stars generated in {targetRegion}! Falling back to a completely random system.");
			starsInRegion = _globalData.CurrentSectorStars;
		}

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		StarMapData startingStar = starsInRegion[rng.RandiRange(0, starsInRegion.Count - 1)];

		_globalData.SavedSystem = startingStar.SystemName;
		_globalData.JustJumped = false; 
		
		GD.Print($"Starting new campaign in {targetRegion}. Randomly picked {startingStar.SystemName}.");

		var transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) 
		{
			transitioner.ChangeScene("res://system_view.tscn");
		} 
		else 
		{
			GetTree().ChangeSceneToFile("res://system_view.tscn");
		}
	}

	// ==========================================
	// DATA GENERATION
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

			SystemData newSystem = new SystemData();
			newSystem.SystemName = newStarData.SystemName;
			newSystem.HasBeenVisited = false;

			for (int p = 0; p < newStarData.PlanetCount; p++)
			{
				PlanetData newPlanet = new PlanetData();
				newPlanet.Name = $"{newStarData.SystemName} Prime-{p + 1}";
				newPlanet.TypeIndex = rng.RandiRange(0, 5); 
				
				newPlanet.Scale = rng.RandfRange(0.15f, 0.40f);
				newPlanet.Distance = rng.RandfRange(200f, 300f) + (p * rng.RandfRange(120f, 200f)); 
				newPlanet.Speed = rng.RandfRange(0.1f, 0.4f) / (p + 1f); 
				newPlanet.StartingAngle = rng.RandfRange(0f, Mathf.Pi * 2f);

				newPlanet.Habitability = habitabilityTypes[rng.RandiRange(0, habitabilityTypes.Length - 1)];
				newPlanet.HasBeenScanned = false;
				newPlanet.HasBeenSalvaged = false;

				newSystem.Planets.Add(newPlanet);
			}

			_globalData.ExploredSystems[newSystem.SystemName] = newSystem;
		}

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
		
		newStar.TooltipText = ""; 

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
			_hoveredStarData = data; 
		};
		newStar.MouseExited += () => 
		{
			_isHoveringStar = false;
			_hoveredStarData = null;
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

		if (_globalData.JustJumped)
		{
			if (data.SystemName == _globalData.SavedSystem) return; 

			Vector2 FTLTarget = clickedStar.Position + new Vector2(50, 30);
			float distance = _currentSystemMapPos.DistanceTo(FTLTarget);
			
			// --- UPDATED: 10 RAW MATERIALS PER 5 PIXELS MULTIPLIER (x2.0) ---
			float rawCost = Mathf.Max(1f, Mathf.Round(distance * 2.0f));
			float energyCost = 1.0f;

			float currentRaw = 0f;
			float currentEnergy = 0f;
			
			if (_globalData.FleetResources != null && _globalData.FleetResources.ContainsKey("Raw Materials"))
			{
				currentRaw = _globalData.FleetResources["Raw Materials"].AsSingle();
				currentEnergy = _globalData.FleetResources["Energy Cores"].AsSingle();
			}

			if (currentRaw < rawCost || currentEnergy < energyCost)
			{
				return; 
			}

			if (_globalData.FleetResources != null && _globalData.FleetResources.ContainsKey("Raw Materials"))
			{
				_globalData.FleetResources["Raw Materials"] = currentRaw - rawCost;
				_globalData.FleetResources["Energy Cores"] = currentEnergy - energyCost;
			}

			_globalData.SavedSystem = data.SystemName; 
			
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

		_globalData.SavedSystem = data.SystemName; 
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

		if (IsInstanceValid(_regionSelectionMenu))
		{
			_regionSelectionMenu.QueueFree();
		}
		
		if (IsInstanceValid(_regionInfoPanel))
		{
			_regionInfoPanel.QueueFree();
		}

		_globalData.CurrentSectorStars.Clear();
		GenerateAndSaveSector(40);
		
		BuildRegionSelectionUI();
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
