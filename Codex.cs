using Godot;
using System;
using System.Collections.Generic;

public partial class Codex : Control
{
	[Export] public Texture2D CodexBackgroundImage;

	private GlobalData _globalData;
	
	// UI Containers
	private VBoxContainer _mainMenuContainer;
	private VBoxContainer _listContainer;
	private VBoxContainer _itemList;
	private PanelContainer _detailPanel;
	private RichTextLabel _detailText;
	private TextureRect _detailImage;
	private TextureRect _detailBlueprintImage; 
	private Label _detailTitle;

	// Fullscreen Overlay Containers
	private PanelContainer _fullscreenPanel;
	private TextureRect _fullscreenImage;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		SetupUI();
		ShowMainMenu();
	}

	private void SetupUI()
	{
		// 1. Setup Background
		TextureRect bg = new TextureRect();
		if (CodexBackgroundImage != null) bg.Texture = CodexBackgroundImage;
		else bg.Texture = GD.Load<Texture2D>("res://Assets/UI/CodexScreen.png");
		bg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		// 2. Constrain UI to the "screen" part of the image
		MarginContainer screenArea = new MarginContainer();
		screenArea.SetAnchorsPreset(LayoutPreset.FullRect);
		screenArea.AddThemeConstantOverride("margin_left", 180);
		screenArea.AddThemeConstantOverride("margin_top", 120);
		screenArea.AddThemeConstantOverride("margin_right", 180);
		screenArea.AddThemeConstantOverride("margin_bottom", 150);
		AddChild(screenArea);

		HBoxContainer splitScreen = new HBoxContainer();
		splitScreen.AddThemeConstantOverride("separation", 20);
		screenArea.AddChild(splitScreen);

		// --- LEFT PANEL (Navigation) ---
		PanelContainer leftPanel = new PanelContainer();
		leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		leftPanel.SizeFlagsStretchRatio = 1;
		StyleBoxFlat clearStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.5f) };
		leftPanel.AddThemeStyleboxOverride("panel", clearStyle);
		splitScreen.AddChild(leftPanel);

		MarginContainer leftMargin = new MarginContainer();
		leftMargin.AddThemeConstantOverride("margin_left", 10);
		leftMargin.AddThemeConstantOverride("margin_top", 10);
		leftMargin.AddThemeConstantOverride("margin_right", 10);
		leftMargin.AddThemeConstantOverride("margin_bottom", 10);
		leftPanel.AddChild(leftMargin);

		VBoxContainer leftVBox = new VBoxContainer();
		leftMargin.AddChild(leftVBox);

		// Main Menu Buttons
		_mainMenuContainer = new VBoxContainer();
		_mainMenuContainer.AddThemeConstantOverride("separation", 15);
		leftVBox.AddChild(_mainMenuContainer);

		Button btnSystems = CreateMenuButton("SCANNED SYSTEMS");
		btnSystems.Pressed += LoadScannedSystems;
		_mainMenuContainer.AddChild(btnSystems);

		Button btnPlayer = CreateMenuButton("PLAYER FLEET");
		btnPlayer.Pressed += LoadPlayerShips;
		_mainMenuContainer.AddChild(btnPlayer);

		Button btnEnemy = CreateMenuButton("HOSTILE INTEL");
		btnEnemy.Pressed += LoadEnemyShips;
		_mainMenuContainer.AddChild(btnEnemy);

		// --- NEW: VISITED SYSTEMS BUTTON ---
		Button btnVisited = CreateMenuButton("VISITED SYSTEMS");
		btnVisited.Pressed += ShowVisitedSystems;
		_mainMenuContainer.AddChild(btnVisited);

		Button btnBack = CreateMenuButton("RETURN TO TACTICAL");
		btnBack.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
		btnBack.Pressed += ReturnToGame;
		_mainMenuContainer.AddChild(btnBack);

		// Sub-Menu List
		_listContainer = new VBoxContainer();
		_listContainer.Visible = false;
		_listContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
		leftVBox.AddChild(_listContainer);

		Button btnBackToMenu = CreateMenuButton("<< BACK TO CATEGORIES");
		btnBackToMenu.Pressed += ShowMainMenu;
		_listContainer.AddChild(btnBackToMenu);

		ScrollContainer leftScroll = new ScrollContainer();
		leftScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		_listContainer.AddChild(leftScroll);

		_itemList = new VBoxContainer();
		_itemList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		leftScroll.AddChild(_itemList);

		// --- RIGHT PANEL (Details) ---
		_detailPanel = new PanelContainer();
		_detailPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_detailPanel.SizeFlagsStretchRatio = 2;
		StyleBoxFlat darkStyle = new StyleBoxFlat();
		darkStyle.BgColor = new Color(0.05f, 0.1f, 0.15f, 0.85f);
		darkStyle.BorderWidthBottom = 2; darkStyle.BorderWidthTop = 2; darkStyle.BorderWidthLeft = 2; darkStyle.BorderWidthRight = 2;
		darkStyle.BorderColor = new Color(0.2f, 0.8f, 1f, 1f);
		darkStyle.CornerRadiusTopLeft = 10; darkStyle.CornerRadiusBottomRight = 10;
		_detailPanel.AddThemeStyleboxOverride("panel", darkStyle);
		splitScreen.AddChild(_detailPanel);

		ScrollContainer rightScroll = new ScrollContainer();
		rightScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		rightScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		_detailPanel.AddChild(rightScroll);

		VBoxContainer detailVBox = new VBoxContainer();
		detailVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		detailVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
		rightScroll.AddChild(detailVBox);

		_detailTitle = new Label();
		_detailTitle.Text = "AWAITING INPUT...";
		_detailTitle.HorizontalAlignment = HorizontalAlignment.Center;
		_detailTitle.AddThemeFontSizeOverride("font_size", 24);
		_detailTitle.AddThemeColorOverride("font_color", new Color(0.2f, 0.8f, 1f));
		detailVBox.AddChild(_detailTitle);

		// Map Sprite
		_detailImage = new TextureRect();
		_detailImage.CustomMinimumSize = new Vector2(0, 150); 
		_detailImage.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_detailImage.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		detailVBox.AddChild(_detailImage);

		// Text Area
		_detailText = new RichTextLabel();
		_detailText.BbcodeEnabled = true;
		_detailText.FitContent = true;    
		_detailText.ScrollActive = false; 
		_detailText.SizeFlagsVertical = SizeFlags.ExpandFill;
		detailVBox.AddChild(_detailText);

		// Blueprint Image
		_detailBlueprintImage = new TextureRect();
		_detailBlueprintImage.CustomMinimumSize = new Vector2(0, 280); 
		_detailBlueprintImage.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_detailBlueprintImage.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_detailBlueprintImage.MouseFilter = Control.MouseFilterEnum.Stop; 
		_detailBlueprintImage.MouseDefaultCursorShape = Control.CursorShape.PointingHand; 
		_detailBlueprintImage.GuiInput += OnBlueprintGuiInput;
		detailVBox.AddChild(_detailBlueprintImage);

		// Fullscreen Overlay Panel
		_fullscreenPanel = new PanelContainer();
		_fullscreenPanel.SetAnchorsPreset(LayoutPreset.FullRect);
		StyleBoxFlat fullscreenBg = new StyleBoxFlat { BgColor = new Color(0.0f, 0.05f, 0.1f, 0.95f) }; 
		_fullscreenPanel.AddThemeStyleboxOverride("panel", fullscreenBg);
		_fullscreenPanel.Visible = false; 
		_fullscreenPanel.MouseFilter = Control.MouseFilterEnum.Stop;
		_fullscreenPanel.GuiInput += OnFullscreenOverlayGuiInput;
		AddChild(_fullscreenPanel);

		_fullscreenImage = new TextureRect();
		_fullscreenImage.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_fullscreenImage.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_fullscreenPanel.AddChild(_fullscreenImage);
	}

	private void OnBlueprintGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			if (_detailBlueprintImage.Texture != null)
			{
				_fullscreenImage.Texture = _detailBlueprintImage.Texture;
				_fullscreenPanel.Visible = true; 
			}
		}
	}

	private void OnFullscreenOverlayGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Right)
		{
			_fullscreenPanel.Visible = false; 
		}
	}

	private Button CreateMenuButton(string text)
	{
		Button btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(0, 45);
		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = new Color(0.1f, 0.2f, 0.3f, 0.8f);
		style.BorderWidthBottom = 1; style.BorderWidthTop = 1; style.BorderWidthLeft = 1; style.BorderWidthRight = 1;
		style.BorderColor = new Color(0.2f, 0.8f, 1f, 1f);
		btn.AddThemeStyleboxOverride("normal", style);
		return btn;
	}

	private void ShowMainMenu()
	{
		_mainMenuContainer.Visible = true;
		_listContainer.Visible = false;
		ClearDetails();
	}

	private void ClearDetails()
	{
		_detailTitle.Text = "SYSTEM STANDBY";
		_detailImage.Texture = null;
		_detailBlueprintImage.Texture = null;
		_detailBlueprintImage.Visible = false;
		_detailText.Text = "\n[center][color=gray]Select an entry from the registry to view data.[/color][/center]";
	}

	// ==========================================
	// DATA POPULATION
	// ==========================================

	private void LoadPlayerShips()
	{
		_mainMenuContainer.Visible = false;
		_listContainer.Visible = true;
		foreach (Node child in _itemList.GetChildren()) child.QueueFree();

		string[] playerShips = { "The Relic Harvester", "The Panacea Spire", "The Neptune Forge", "The Genesis Ark", "The Valkyrie Wing", "The Aegis Bastion", "The Aether Skimmer" };
		
		foreach (string ship in playerShips)
		{
			Button btn = CreateMenuButton(ship);
			btn.Pressed += () => ShowShipDetails(ship, true);
			_itemList.AddChild(btn);
		}
	}

	private void LoadEnemyShips()
	{
		_mainMenuContainer.Visible = false;
		_listContainer.Visible = true;
		foreach (Node child in _itemList.GetChildren()) child.QueueFree();

		string[] enemyShips = { "Aether Censor Obelisk", "Custodian Logic Barge", "Ignis Repurposed Terraformer", "Reformatter Dreadnought", "Scrap-Stick Subversion Drone" };
		
		foreach (string ship in enemyShips)
		{
			Button btn = CreateMenuButton(ship);
			btn.Pressed += () => ShowShipDetails(ship, false);
			_itemList.AddChild(btn);
		}
	}

	private void LoadScannedSystems()
	{
		_mainMenuContainer.Visible = false;
		_listContainer.Visible = true;
		foreach (Node child in _itemList.GetChildren()) child.QueueFree();

		bool foundAny = false;

		if (_globalData != null)
		{
			foreach (var sysKvp in _globalData.ExploredSystems)
			{
				foreach (PlanetData planet in sysKvp.Value.Planets)
				{
					if (planet.HasBeenScanned) 
					{
						foundAny = true;
						Button btn = CreateMenuButton(planet.Name);
						btn.Pressed += () => ShowPlanetDetails(planet, sysKvp.Value.SystemName);
						_itemList.AddChild(btn);
					}
				}
			}
		}

		if (!foundAny)
		{
			Label noneLabel = new Label();
			noneLabel.Text = "No survey data found.\nInitiate SCAN from tactical map.";
			noneLabel.HorizontalAlignment = HorizontalAlignment.Center;
			noneLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
			_itemList.AddChild(noneLabel);
		}
	}

	// --- NEW: Load Visited Systems ---
	private void ShowVisitedSystems()
	{
		_mainMenuContainer.Visible = false;
		_listContainer.Visible = true;
		foreach (Node child in _itemList.GetChildren()) child.QueueFree();

		bool foundAny = false;

		if (_globalData != null && _globalData.ExploredSystems != null)
		{
			foreach (var sysKvp in _globalData.ExploredSystems)
			{
				SystemData sys = sysKvp.Value;
				if (sys.HasBeenVisited) 
				{
					foundAny = true;
					Button btn = CreateMenuButton(sys.SystemName.ToUpper());
					btn.Pressed += () => ShowSystemDetails(sys);
					_itemList.AddChild(btn);
				}
			}
		}

		if (!foundAny)
		{
			Label noneLabel = new Label();
			noneLabel.Text = "No systems visited yet.\nExplore the galaxy!";
			noneLabel.HorizontalAlignment = HorizontalAlignment.Center;
			noneLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
			_itemList.AddChild(noneLabel);
		}
	}

	// ==========================================
	// DETAIL RENDERING
	// ==========================================

	private void ShowShipDetails(string shipName, bool isPlayer)
	{
		_detailTitle.Text = shipName.ToUpper();
		
		if (isPlayer)
		{
			// Map Sprite
			_detailImage.Texture = GD.Load<Texture2D>(GetPlayerShipImage(shipName));
			
			// Player Blueprint
			string blueprintPath = GetPlayerBlueprintImage(shipName);
			if (!string.IsNullOrEmpty(blueprintPath))
			{
				_detailBlueprintImage.Texture = GD.Load<Texture2D>(blueprintPath);
				_detailBlueprintImage.Visible = true;
			}
			else
			{
				_detailBlueprintImage.Texture = null;
				_detailBlueprintImage.Visible = false;
			}
		}
		else
		{
			// Map Sprite
			_detailImage.Texture = GD.Load<Texture2D>(GetEnemyShipImage(shipName));
			
			// Enemy Blueprint
			string blueprintPath = GetEnemyBlueprintImage(shipName);
			if (!string.IsNullOrEmpty(blueprintPath))
			{
				_detailBlueprintImage.Texture = GD.Load<Texture2D>(blueprintPath);
				_detailBlueprintImage.Visible = true;
			}
			else
			{
				_detailBlueprintImage.Texture = null;
				_detailBlueprintImage.Visible = false;
			}
		}

		(int hp, int shields) = GetShipCombatStats(shipName);
		(int range, int damage) = GetShipWeaponStats(shipName);
		int ap = GetShipBaseActions(shipName);

		string color = isPlayer ? "#00ffff" : "#ff4444";
		
		_detailText.Text = 
			$"[center][color={color}]--- TACTICAL SPECIFICATIONS ---[/color][/center]\n\n" +
			$"[b]HULL INTEGRITY:[/b] {hp}\n" +
			$"[b]SHIELD CAPACITY:[/b] {shields}\n" +
			$"[b]ACTION POINTS (AP):[/b] {ap}\n" +
			$"[b]WEAPON YIELD:[/b] 0 - {damage} DMG\n" +
			$"[b]TARGETING RANGE:[/b] {range} Hexes\n\n" +
			$"[color=gray]Additional tactical analysis unavailable at this time.[/color]";
	}

	private void ShowPlanetDetails(PlanetData planet, string systemName)
	{
		_detailTitle.Text = planet.Name.ToUpper();
		_detailImage.Texture = GD.Load<Texture2D>(GetPlanetTexturePath(planet.TypeIndex));
		
		_detailBlueprintImage.Texture = null;
		_detailBlueprintImage.Visible = false;

		string typeString = GetPlanetTypeString(planet.TypeIndex);
		string salvageStatus = planet.HasBeenSalvaged ? "[color=red]DEPLETED[/color]" : "[color=green]PRISTINE[/color]";

		_detailText.Text = 
			$"[center][color=#00ffff]--- GEOLOGICAL SURVEY ---[/color][/center]\n\n" +
			$"[b]STAR SYSTEM:[/b] {systemName}\n" +
			$"[b]BIOME CLASSIFICATION:[/b] {typeString}\n" +
			$"[b]RELATIVE MASS:[/b] {(planet.Scale * 100f).ToString("F1")}%\n" +
			$"[b]HABITABILITY PROJECTION:[/b] {planet.Habitability}\n\n" +
			$"[b]SALVAGE STATUS:[/b] {salvageStatus}\n\n" +
			$"[color=gray]Note: Depleted worlds yield no further resources for fleet acquisition.[/color]";
	}

	// --- NEW: Show System Details ---
	private void ShowSystemDetails(SystemData sys)
	{
		_detailTitle.Text = $"SYSTEM: {sys.SystemName.ToUpper()}";
		
		// Hide images for the system log view
		_detailImage.Texture = null; 
		_detailBlueprintImage.Texture = null;
		_detailBlueprintImage.Visible = false;

		int scanned = 0;
		int salvaged = 0;
		foreach (PlanetData p in sys.Planets)
		{
			if (p.HasBeenScanned) scanned++;
			if (p.HasBeenSalvaged) salvaged++;
		}

		string region = "Unknown Region";
		if (_globalData != null && _globalData.CurrentSectorStars != null)
		{
			foreach (var star in _globalData.CurrentSectorStars)
			{
				if (star.SystemName == sys.SystemName)
				{
					region = star.Region;
					break;
				}
			}
		}

		string info = 
			$"[center][color=#00ffff]--- SYSTEM TELEMETRY ---[/color][/center]\n\n" +
			$"[b]GALACTIC REGION:[/b] {region}\n" +
			$"[b]ORBITING PLANETS:[/b] {sys.Planets.Count}\n" +
			$"[b]PLANETS SCANNED:[/b] {scanned} / {sys.Planets.Count}\n" +
			$"[b]PLANETS SALVAGED:[/b] {salvaged} / {sys.Planets.Count}\n\n" +
			$"[color=yellow][b]--- PLANETARY DATA ---[/b][/color]\n";

		for (int i = 0; i < sys.Planets.Count; i++)
		{
			PlanetData p = sys.Planets[i];
			
			string status = "[color=gray]Unexplored[/color]";
			if (p.HasBeenSalvaged) status = "[color=red]Depleted[/color]";
			else if (p.HasBeenScanned) status = "[color=cyan]Scanned[/color]";
			
			info += $"- Planet {i+1} ({p.Habitability}): {status}\n";
		}

		_detailText.Text = info;
	}

	private void ReturnToGame()
	{
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://exploration_battle.tscn");
		else GetTree().ChangeSceneToFile("res://exploration_battle.tscn");
	}

	// ==========================================
	// DATA HELPERS 
	// ==========================================
	
	private string GetPlayerShipImage(string shipName)
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
			default: return "res://Assets/UI/icon.svg";
		}
	}

	private string GetPlayerBlueprintImage(string shipName)
	{
		switch (shipName)
		{
			case "The Aegis Bastion": return "res://Ships/AegisBastion.png";
			case "The Aether Skimmer": return "res://Ships/AetherSkimmer.png";
			case "The Panacea Spire": return "res://Ships/PanaceSpire.png"; 
			case "The Neptune Forge": return "res://Ships/NeptuneForge.png";
			case "The Genesis Ark": return "res://Ships/GenesisArk.png";
			case "The Relic Harvester": return "res://Ships/RelicHarvester.png";
			case "The Valkyrie Wing": return "res://Ships/ValkyrieWing.png";
			default: return ""; 
		}
	}

	private string GetEnemyShipImage(string shipName)
	{
		switch (shipName)
		{
			case "Aether Censor Obelisk": return "res://EnemyShips/AetherCensorObeliskSprite.png";
			case "Custodian Logic Barge": return "res://EnemyShips/CustodianLogicBargeSprite.png";
			case "Ignis Repurposed Terraformer": return "res://EnemyShips/IgnisRepurposedTerraformerSprite.png";
			case "Reformatter Dreadnought": return "res://EnemyShips/ReformatterDreadnoughtSprite.png";
			case "Scrap-Stick Subversion Drone": return "res://EnemyShips/ScrapStickSubversionDroneSprite.png";
			default: return "res://Assets/UI/icon.svg";
		}
	}

	private string GetEnemyBlueprintImage(string shipName)
	{
		switch (shipName)
		{
			case "Aether Censor Obelisk": return "res://EnemyShips/AetherCensorObelisk.png";
			case "Custodian Logic Barge": return "res://EnemyShips/CustodianLogicBarge.png";
			case "Ignis Repurposed Terraformer": return "res://EnemyShips/IgnisRepurposedTerraformer.png";
			case "Reformatter Dreadnought": return "res://EnemyShips/ReformatterDreadnought.png";
			case "Scrap-Stick Subversion Drone": return "res://EnemyShips/ScraptickSubversionDrone.png";
			default: return ""; 
		}
	}

	private string GetPlanetTypeString(int typeIndex)
	{
		string[] types = { "Terra", "Arid", "Ocean", "Toxic", "Frozen", "Lava" };
		if (typeIndex >= 0 && typeIndex < types.Length) return types[typeIndex];
		return "Terra";
	}

	private string GetPlanetTexturePath(int typeIndex)
	{
		string type = GetPlanetTypeString(typeIndex);
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

	private int GetShipBaseActions(string shipName)
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
			case "Scrap-Stick Subversion Drone": return 5; 
			case "Aether Censor Obelisk": return 4; 
			case "Custodian Logic Barge": return 3; 
			case "Ignis Repurposed Terraformer": return 2; 
			case "Reformatter Dreadnought": return 2; 
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
			case "Reformatter Dreadnought": return (100, 50); 
			case "Ignis Repurposed Terraformer": return (80, 20); 
			case "Custodian Logic Barge": return (50, 30); 
			case "Aether Censor Obelisk": return (30, 20); 
			case "Scrap-Stick Subversion Drone": return (20, 10); 
			default: return (50, 25); 
		}
	}

	private (int range, int damage) GetShipWeaponStats(string shipName)
	{
		switch (shipName)
		{
			case "The Aegis Bastion": return (2, 25);   
			case "The Neptune Forge": return (2, 30);    
			case "The Genesis Ark": return (3, 20);      
			case "The Panacea Spire": return (2, 15);    
			case "The Relic Harvester": return (1, 35);  
			case "The Valkyrie Wing": return (2, 15);    
			case "The Aether Skimmer": return (1, 20);   
			case "Reformatter Dreadnought": return (2, 25); 
			case "Ignis Repurposed Terraformer": return (2, 30); 
			case "Custodian Logic Barge": return (3, 20); 
			case "Aether Censor Obelisk": return (2, 15); 
			case "Scrap-Stick Subversion Drone": return (1, 20); 
			default: return (2, 20); 
		}
	}
}
