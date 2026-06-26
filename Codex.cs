using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
	private RemnantRecord _activeRemnantRecord;

	// Fullscreen Overlay Containers
	private PanelContainer _fullscreenPanel;
	private TextureRect _fullscreenImage;
	private ColorRect _remnantInventoryOverlay;
	private PanelContainer _remnantInventoryPanel;
	private TextureRect _remnantInventoryPortrait;
	private Label _remnantInventoryNameLabel;
	private Label _remnantInventoryRoleLabel;
	private Label _remnantInventoryStatsLabel;
	private Label _remnantInventorySummaryLabel;
	private Label _remnantInventoryFooterLabel;
	private VBoxContainer _remnantInventoryLoadoutStack;
	private VBoxContainer _remnantInventoryWeaponStack;
	private VBoxContainer _remnantInventoryShieldStack;
	private VBoxContainer _remnantInventoryItemStack;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		SetupUI();
		ShowMainMenu();
	}

	public override void _Input(InputEvent @event)
	{
		if (_remnantInventoryOverlay != null
			&& _remnantInventoryOverlay.Visible
			&& @event is InputEventKey escapeEvent
			&& escapeEvent.Pressed
			&& !escapeEvent.Echo
			&& escapeEvent.Keycode == Key.Escape)
		{
			HideRemnantInventory();
			GetViewport().SetInputAsHandled();
		}
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

		Button btnMissionIntel = CreateMenuButton("MISSION INTEL");
		btnMissionIntel.Pressed += LoadMissionIntel;
		_mainMenuContainer.AddChild(btnMissionIntel);

		Button btnRemnants = CreateMenuButton("REMNANT REGISTRY");
		btnRemnants.Pressed += LoadRemnantRegistry;
		_mainMenuContainer.AddChild(btnRemnants);

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
		_detailImage.MouseFilter = Control.MouseFilterEnum.Stop;
		_detailImage.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		_detailImage.GuiInput += OnDetailImageGuiInput;
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

		BuildRemnantInventoryPanel();
	}

	private void BuildRemnantInventoryPanel()
	{
		_remnantInventoryOverlay = new ColorRect
		{
			Visible = false,
			Color = new Color(0.04f, 0.02f, 0.01f, 0.84f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_remnantInventoryOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
		_remnantInventoryOverlay.GuiInput += OnRemnantInventoryOverlayGuiInput;
		AddChild(_remnantInventoryOverlay);

		_remnantInventoryPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(1120f, 720f)
		};
		_remnantInventoryPanel.SetAnchorsPreset(LayoutPreset.Center);
		_remnantInventoryPanel.OffsetLeft = -560f;
		_remnantInventoryPanel.OffsetTop = -360f;
		_remnantInventoryPanel.OffsetRight = 560f;
		_remnantInventoryPanel.OffsetBottom = 360f;
		_remnantInventoryPanel.AddThemeStyleboxOverride("panel", CreateInventoryWindowStyle());
		_remnantInventoryOverlay.AddChild(_remnantInventoryPanel);

		MarginContainer outerMargin = new MarginContainer();
		outerMargin.AddThemeConstantOverride("margin_left", 20);
		outerMargin.AddThemeConstantOverride("margin_top", 20);
		outerMargin.AddThemeConstantOverride("margin_right", 20);
		outerMargin.AddThemeConstantOverride("margin_bottom", 20);
		_remnantInventoryPanel.AddChild(outerMargin);

		VBoxContainer layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 16);
		outerMargin.AddChild(layout);

		HBoxContainer headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 14);
		layout.AddChild(headerRow);

		VBoxContainer rail = new VBoxContainer();
		rail.CustomMinimumSize = new Vector2(76f, 0f);
		rail.AddThemeConstantOverride("separation", 10);
		headerRow.AddChild(rail);
		rail.AddChild(BuildInventoryRailTag("LOADOUT", true));
		rail.AddChild(BuildInventoryRailTag("STATUS", false));
		rail.AddChild(BuildInventoryRailTag("PACK", false));

		VBoxContainer mainColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		mainColumn.AddThemeConstantOverride("separation", 14);
		headerRow.AddChild(mainColumn);

		HBoxContainer titleRow = new HBoxContainer();
		titleRow.Alignment = BoxContainer.AlignmentMode.Center;
		titleRow.AddThemeConstantOverride("separation", 12);
		mainColumn.AddChild(titleRow);

		Control leftSpacer = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		titleRow.AddChild(leftSpacer);

		PanelContainer titlePlaque = new PanelContainer
		{
			CustomMinimumSize = new Vector2(420f, 74f)
		};
		titlePlaque.AddThemeStyleboxOverride("panel", CreateInventoryBannerStyle());
		titleRow.AddChild(titlePlaque);

		Label titleLabel = new Label
		{
			Text = "INVENTORY",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		titleLabel.SetAnchorsPreset(LayoutPreset.FullRect);
		titleLabel.AddThemeFontSizeOverride("font_size", 34);
		titleLabel.AddThemeColorOverride("font_color", new Color(0.93f, 0.90f, 0.84f));
		titlePlaque.AddChild(titleLabel);

		Control rightSpacer = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		titleRow.AddChild(rightSpacer);

		Button closeButton = new Button
		{
			Text = "CLOSE",
			CustomMinimumSize = new Vector2(120f, 44f)
		};
		closeButton.AddThemeStyleboxOverride("normal", CreateInventoryButtonStyle());
		closeButton.AddThemeStyleboxOverride("hover", CreateInventoryButtonStyle(true));
		closeButton.AddThemeStyleboxOverride("pressed", CreateInventoryButtonStyle(true));
		closeButton.Pressed += HideRemnantInventory;
		titleRow.AddChild(closeButton);

		PanelContainer identityPanel = new PanelContainer();
		identityPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle(true));
		mainColumn.AddChild(identityPanel);

		MarginContainer identityMargin = new MarginContainer();
		identityMargin.AddThemeConstantOverride("margin_left", 18);
		identityMargin.AddThemeConstantOverride("margin_top", 14);
		identityMargin.AddThemeConstantOverride("margin_right", 18);
		identityMargin.AddThemeConstantOverride("margin_bottom", 14);
		identityPanel.AddChild(identityMargin);

		VBoxContainer identityContent = new VBoxContainer();
		identityContent.AddThemeConstantOverride("separation", 6);
		identityMargin.AddChild(identityContent);

		_remnantInventoryNameLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_remnantInventoryNameLabel.AddThemeFontSizeOverride("font_size", 30);
		identityContent.AddChild(_remnantInventoryNameLabel);

		_remnantInventoryRoleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_remnantInventoryRoleLabel.AddThemeFontSizeOverride("font_size", 16);
		_remnantInventoryRoleLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.78f, 0.62f));
		identityContent.AddChild(_remnantInventoryRoleLabel);

		HBoxContainer bodyRow = new HBoxContainer();
		bodyRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		bodyRow.AddThemeConstantOverride("separation", 14);
		mainColumn.AddChild(bodyRow);

		PanelContainer portraitPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(260f, 0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		portraitPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		bodyRow.AddChild(portraitPanel);

		MarginContainer portraitMargin = new MarginContainer();
		portraitMargin.AddThemeConstantOverride("margin_left", 14);
		portraitMargin.AddThemeConstantOverride("margin_top", 14);
		portraitMargin.AddThemeConstantOverride("margin_right", 14);
		portraitMargin.AddThemeConstantOverride("margin_bottom", 14);
		portraitPanel.AddChild(portraitMargin);

		VBoxContainer portraitContent = new VBoxContainer();
		portraitContent.AddThemeConstantOverride("separation", 12);
		portraitMargin.AddChild(portraitContent);

		_remnantInventoryPortrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(0f, 240f),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
		};
		portraitContent.AddChild(_remnantInventoryPortrait);

		_remnantInventoryStatsLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_remnantInventoryStatsLabel.AddThemeFontSizeOverride("font_size", 15);
		portraitContent.AddChild(_remnantInventoryStatsLabel);

		VBoxContainer centerColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		centerColumn.AddThemeConstantOverride("separation", 14);
		bodyRow.AddChild(centerColumn);

		PanelContainer summaryPanel = new PanelContainer();
		summaryPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		centerColumn.AddChild(summaryPanel);

		MarginContainer summaryMargin = new MarginContainer();
		summaryMargin.AddThemeConstantOverride("margin_left", 14);
		summaryMargin.AddThemeConstantOverride("margin_top", 14);
		summaryMargin.AddThemeConstantOverride("margin_right", 14);
		summaryMargin.AddThemeConstantOverride("margin_bottom", 14);
		summaryPanel.AddChild(summaryMargin);

		VBoxContainer summaryContent = new VBoxContainer();
		summaryContent.AddThemeConstantOverride("separation", 10);
		summaryMargin.AddChild(summaryContent);
		summaryContent.AddChild(BuildInventorySectionHeader("OPERATIVE DOSSIER"));

		_remnantInventorySummaryLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_remnantInventorySummaryLabel.AddThemeFontSizeOverride("font_size", 15);
		summaryContent.AddChild(_remnantInventorySummaryLabel);

		PanelContainer loadoutPanel = BuildInventorySectionCard("ACTIVE GEAR", out _remnantInventoryLoadoutStack);
		loadoutPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		centerColumn.AddChild(loadoutPanel);

		VBoxContainer rightColumn = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(340f, 0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		rightColumn.AddThemeConstantOverride("separation", 14);
		bodyRow.AddChild(rightColumn);

		rightColumn.AddChild(BuildInventorySectionCard("WEAPONS LOCKER", out _remnantInventoryWeaponStack));
		rightColumn.AddChild(BuildInventorySectionCard("SHIELD RACK", out _remnantInventoryShieldStack));

		PanelContainer itemPanel = new PanelContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		itemPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		rightColumn.AddChild(itemPanel);

		MarginContainer itemMargin = new MarginContainer();
		itemMargin.AddThemeConstantOverride("margin_left", 14);
		itemMargin.AddThemeConstantOverride("margin_top", 14);
		itemMargin.AddThemeConstantOverride("margin_right", 14);
		itemMargin.AddThemeConstantOverride("margin_bottom", 14);
		itemPanel.AddChild(itemMargin);

		VBoxContainer itemContent = new VBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		itemContent.AddThemeConstantOverride("separation", 10);
		itemMargin.AddChild(itemContent);
		itemContent.AddChild(BuildInventorySectionHeader("FIELD PACK"));

		_remnantInventoryItemStack = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		_remnantInventoryItemStack.AddThemeConstantOverride("separation", 8);
		itemContent.AddChild(_remnantInventoryItemStack);

		_remnantInventoryFooterLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_remnantInventoryFooterLabel.AddThemeFontSizeOverride("font_size", 14);
		_remnantInventoryFooterLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.76f, 0.65f));
		mainColumn.AddChild(_remnantInventoryFooterLabel);
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

	private void OnDetailImageGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseEvent || !mouseEvent.Pressed)
		{
			return;
		}

		if (mouseEvent.ButtonIndex == MouseButton.Right && _activeRemnantRecord != null)
		{
			ShowRemnantInventory(_activeRemnantRecord);
			_detailImage.AcceptEvent();
			return;
		}

		if (_activeRemnantRecord != null)
		{
			_detailImage.AcceptEvent();
		}
	}

	private void OnFullscreenOverlayGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Right)
		{
			_fullscreenPanel.Visible = false; 
		}
	}

	private void OnRemnantInventoryOverlayGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent
			&& mouseEvent.Pressed
			&& mouseEvent.ButtonIndex == MouseButton.Left)
		{
			HideRemnantInventory();
			_remnantInventoryOverlay.AcceptEvent();
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
		_activeRemnantRecord = null;
		_detailTitle.Text = "SYSTEM STANDBY";
		_detailImage.Texture = null;
		_detailImage.TooltipText = string.Empty;
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

	private void LoadMissionIntel()
	{
		_mainMenuContainer.Visible = false;
		_listContainer.Visible = true;
		foreach (Node child in _itemList.GetChildren()) child.QueueFree();

		List<CodexIntelEntry> entries = (_globalData?.UnlockedCodexEntryIDs ?? new List<string>())
			.Select(CodexIntelRegistry.GetEntry)
			.Where(entry => entry != null)
			.OrderBy(entry => entry.Category)
			.ThenBy(entry => entry.Title)
			.ToList();

		if (entries.Count == 0)
		{
			Label noneLabel = new Label();
			noneLabel.Text = "No mission intel archived yet.\nRecover datapads, route ledgers, and tactical files in away missions.";
			noneLabel.HorizontalAlignment = HorizontalAlignment.Center;
			noneLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
			_itemList.AddChild(noneLabel);
			return;
		}

		foreach (CodexIntelEntry entry in entries)
		{
			Button button = CreateMenuButton(entry.Title.ToUpper());
			button.Pressed += () => ShowMissionIntelDetails(entry);
			_itemList.AddChild(button);
		}
	}

	private void LoadRemnantRegistry()
	{
		_mainMenuContainer.Visible = false;
		_listContainer.Visible = true;
		foreach (Node child in _itemList.GetChildren()) child.QueueFree();

		List<RemnantRecord> remnants = (_globalData?.RescuedRemnants ?? new List<RemnantRecord>())
			.Where(record => record != null)
			.OrderBy(record => record.RescuedOnTurn)
			.ThenBy(record => record.DisplayName)
			.ToList();

		if (remnants.Count == 0)
		{
			Label noneLabel = new Label();
			noneLabel.Text = "No rescued remnants archived yet.\nBring survivors home from away missions to unlock their records.";
			noneLabel.HorizontalAlignment = HorizontalAlignment.Center;
			noneLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
			_itemList.AddChild(noneLabel);
			return;
		}

		foreach (RemnantRecord remnant in remnants)
		{
			Button button = CreateMenuButton(remnant.DisplayName.ToUpperInvariant());
			button.Pressed += () => ShowRemnantDetails(remnant);
			_itemList.AddChild(button);
		}
	}

	// ==========================================
	// DETAIL RENDERING
	// ==========================================

	private void ShowShipDetails(string shipName, bool isPlayer)
	{
		_activeRemnantRecord = null;
		_detailImage.TooltipText = string.Empty;
		HideRemnantInventory();
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
		_activeRemnantRecord = null;
		_detailImage.TooltipText = string.Empty;
		HideRemnantInventory();
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
		_activeRemnantRecord = null;
		_detailImage.TooltipText = string.Empty;
		HideRemnantInventory();
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

	private void ShowMissionIntelDetails(CodexIntelEntry entry)
	{
		if (entry == null)
		{
			return;
		}

		_activeRemnantRecord = null;
		HideRemnantInventory();
		_detailTitle.Text = entry.Title.ToUpper();
		_detailImage.Texture = !string.IsNullOrWhiteSpace(entry.ImagePath) && ResourceLoader.Exists(entry.ImagePath)
			? GD.Load<Texture2D>(entry.ImagePath)
			: null;
		_detailImage.TooltipText = string.Empty;
		_detailBlueprintImage.Texture = null;
		_detailBlueprintImage.Visible = false;
		_detailText.Text =
			$"[center][color=#7cffc6]--- {entry.Category.ToUpper()} ---[/color][/center]\n\n" +
			$"[b]SUMMARY:[/b] {entry.Summary}\n\n" +
			$"{entry.DetailText}";
	}

	private void ShowRemnantDetails(RemnantRecord remnant)
	{
		if (remnant == null)
		{
			return;
		}

		HideRemnantInventory();
		_activeRemnantRecord = remnant;
		_detailTitle.Text = remnant.DisplayName.ToUpperInvariant();
		_detailImage.Texture = !string.IsNullOrWhiteSpace(remnant.PortraitPath) && ResourceLoader.Exists(remnant.PortraitPath)
			? GD.Load<Texture2D>(remnant.PortraitPath)
			: ResourceLoader.Exists("res://Assets/Missions/Cutscenes/survivors.png")
				? GD.Load<Texture2D>("res://Assets/Missions/Cutscenes/survivors.png")
				: null;
		_detailImage.TooltipText = "Right-click to inspect this remnant's inventory.";
		_detailBlueprintImage.Texture = null;
		_detailBlueprintImage.Visible = false;

		string missionTitle = string.IsNullOrWhiteSpace(remnant.MissionTitle) ? "Unknown Recovery" : remnant.MissionTitle;
		string description = string.IsNullOrWhiteSpace(remnant.Description) ? "No additional field profile was recorded." : remnant.Description;
		string notesBlock = string.IsNullOrWhiteSpace(remnant.Notes)
			? string.Empty
			: $"\n\n[b]FIELD NOTES:[/b] {remnant.Notes}";
		_detailText.Text =
			$"[center][color=#7cffc6]--- {CampaignText.RemnantsLabel.ToUpperInvariant()} REGISTRY ---[/color][/center]\n\n" +
			$"[b]RECOVERED FROM:[/b] {missionTitle}\n" +
			$"[b]RESCUED ON TURN:[/b] {Mathf.Max(1, remnant.RescuedOnTurn)}\n\n" +
			$"[b]PROFILE:[/b] {description}" +
			notesBlock;
	}

	private void ShowRemnantInventory(RemnantRecord remnant)
	{
		if (_remnantInventoryOverlay == null
			|| _remnantInventoryPortrait == null
			|| _remnantInventoryNameLabel == null
			|| _remnantInventoryRoleLabel == null
			|| _remnantInventoryStatsLabel == null
			|| _remnantInventorySummaryLabel == null
			|| _remnantInventoryFooterLabel == null
			|| remnant == null)
		{
			return;
		}

		MissionOfficerInventoryPanelData data = BuildRemnantInventoryPanelData(remnant);
		if (data == null)
		{
			return;
		}

		_remnantInventoryPortrait.Texture = data.Portrait;
		_remnantInventoryNameLabel.Text = string.IsNullOrWhiteSpace(data.DisplayName)
			? "RESCUED REMNANT"
			: data.DisplayName.ToUpperInvariant();
		_remnantInventoryRoleLabel.Text = $"{(data.Specialty ?? string.Empty).ToUpperInvariant()}  |  {(data.ShipName ?? string.Empty).ToUpperInvariant()}";
		_remnantInventoryStatsLabel.Text = data.VitalStatsText ?? string.Empty;
		_remnantInventorySummaryLabel.Text = data.SummaryText ?? string.Empty;
		_remnantInventoryFooterLabel.Text = data.FooterText ?? "Registry view only. Loadout changes are unavailable from the Codex.";

		PopulateInventoryEntryStack(_remnantInventoryLoadoutStack, data.LoadoutEntries, "No active loadout data.");
		PopulateInventoryEntryStack(_remnantInventoryWeaponStack, data.WeaponEntries, "No owned weapons.");
		PopulateInventoryEntryStack(_remnantInventoryShieldStack, data.ShieldEntries, "No owned shields.");
		PopulateInventoryItemStack(data.ItemEntries);
		_remnantInventoryOverlay.Visible = true;
	}

	private void HideRemnantInventory()
	{
		if (_remnantInventoryOverlay != null)
		{
			_remnantInventoryOverlay.Visible = false;
		}
	}

	private MissionOfficerInventoryPanelData BuildRemnantInventoryPanelData(RemnantRecord remnant)
	{
		if (remnant == null)
		{
			return null;
		}

		OfficerState officerState = remnant.StoredOfficerState?.ToRuntime();
		if (officerState != null)
		{
			OfficerMissionLoadoutService.EnsureOfficerLoadout(officerState);
		}

		List<MissionInventoryEntry> loadoutEntries = BuildRemnantLoadoutEntries(remnant, officerState);
		List<MissionInventoryEntry> weaponEntries = BuildRemnantWeaponEntries(officerState);
		List<MissionInventoryEntry> shieldEntries = BuildRemnantShieldEntries(officerState);
		List<MissionInventoryEntry> itemEntries = BuildRemnantItemEntries(remnant, officerState);
		string registrySource = string.IsNullOrWhiteSpace(remnant.MissionTitle) ? "Remnant Registry" : remnant.MissionTitle;
		string roleText = officerState?.Specialty ?? "Rescued Remnant";
		string footerText = itemEntries.Count == 0
			? "No tracked field items are archived for this record."
			: $"Archived pack contains {itemEntries.Count} tracked entr{(itemEntries.Count == 1 ? "y" : "ies")}.";

		return new MissionOfficerInventoryPanelData
		{
			OfficerId = remnant.RecordId,
			DisplayName = string.IsNullOrWhiteSpace(remnant.DisplayName) ? "Rescued Remnant" : remnant.DisplayName,
			ShipName = registrySource,
			Specialty = roleText,
			Portrait = !string.IsNullOrWhiteSpace(remnant.PortraitPath) && ResourceLoader.Exists(remnant.PortraitPath)
				? GD.Load<Texture2D>(remnant.PortraitPath)
				: ResourceLoader.Exists("res://Assets/Missions/Cutscenes/survivors.png")
					? GD.Load<Texture2D>("res://Assets/Missions/Cutscenes/survivors.png")
					: null,
			SummaryText = BuildRemnantInventorySummary(remnant, officerState),
			VitalStatsText = BuildRemnantVitalStatsText(remnant, officerState),
			FooterText = footerText,
			LoadoutEntries = loadoutEntries,
			WeaponEntries = weaponEntries,
			ShieldEntries = shieldEntries,
			ItemEntries = itemEntries
		};
	}

	private static string BuildRemnantInventorySummary(RemnantRecord remnant, OfficerState officerState)
	{
		List<string> notes = new List<string>();
		if (officerState != null && !string.IsNullOrWhiteSpace(officerState.Biography))
		{
			notes.Add(officerState.Biography.Trim());
		}
		else if (!string.IsNullOrWhiteSpace(remnant?.Description))
		{
			notes.Add(remnant.Description.Trim());
		}
		else
		{
			notes.Add("No detailed field profile was archived for this remnant.");
		}

		if (officerState != null && (!string.IsNullOrWhiteSpace(officerState.Archetype) || !string.IsNullOrWhiteSpace(officerState.Ideology)))
		{
			notes.Add($"Alignment: {officerState.Archetype} with {officerState.Ideology} leanings.");
		}
		else if (!string.IsNullOrWhiteSpace(remnant?.MissionTitle))
		{
			notes.Add($"Recovered during {remnant.MissionTitle}.");
		}

		if (officerState != null && !string.IsNullOrWhiteSpace(officerState.Flaw))
		{
			notes.Add($"Watchpoint: {officerState.Flaw}.");
		}
		else if (!string.IsNullOrWhiteSpace(remnant?.Notes))
		{
			notes.Add(remnant.Notes.Trim());
		}

		return string.Join("\n\n", notes.Where(note => !string.IsNullOrWhiteSpace(note)));
	}

	private static string BuildRemnantVitalStatsText(RemnantRecord remnant, OfficerState officerState)
	{
		if (officerState == null)
		{
			int itemCount = remnant?.PersonalInventoryItemIDs?.Count ?? 0;
			return string.Join("\n", new[]
			{
				$"STATUS    Archived survivor",
				$"RESCUED   Turn {Mathf.Max(1, remnant?.RescuedOnTurn ?? 1)}",
				$"PACK      {itemCount} item{(itemCount == 1 ? string.Empty : "s")}",
				$"SOURCE    {(string.IsNullOrWhiteSpace(remnant?.MissionTitle) ? "Unknown Recovery" : remnant.MissionTitle)}"
			});
		}

		OfficerMissionLoadoutService.EnsureOfficerLoadout(officerState);
		MissionWeaponDefinition weapon = OfficerMissionLoadoutService.GetEquippedWeapon(officerState);
		MissionShieldDefinition shield = OfficerMissionLoadoutService.GetEquippedShield(officerState);
		List<string> lines = new List<string>
		{
			$"SPECIALTY {officerState.Specialty}",
			$"WEAPON    {(weapon?.DisplayName ?? officerState.EquippedMissionWeaponId ?? "Unassigned")}",
			$"SHIELD    {(shield?.DisplayName ?? officerState.EquippedMissionShieldId ?? "Unassigned")}",
			$"APPROVAL  {officerState.Approval}",
			$"STRESS    {officerState.Stress}"
		};

		if (!string.IsNullOrWhiteSpace(officerState.ShipName))
		{
			lines.Add($"POSTED    {officerState.ShipName}");
		}

		return string.Join("\n", lines);
	}

	private static List<MissionInventoryEntry> BuildRemnantLoadoutEntries(RemnantRecord remnant, OfficerState officerState)
	{
		if (officerState == null)
		{
			return new List<MissionInventoryEntry>
			{
				new MissionInventoryEntry
				{
					Title = "Survivor Effects",
					Detail = "No formal combat loadout archived for this rescued remnant.",
					Highlighted = true
				},
				new MissionInventoryEntry
				{
					Title = "Registry Notes",
					Detail = string.IsNullOrWhiteSpace(remnant?.Notes) ? "No additional notes recorded." : remnant.Notes
				}
			};
		}

		MissionWeaponDefinition weapon = OfficerMissionLoadoutService.GetEquippedWeapon(officerState);
		MissionShieldDefinition shield = OfficerMissionLoadoutService.GetEquippedShield(officerState);
		return new List<MissionInventoryEntry>
		{
			new MissionInventoryEntry
			{
				Title = weapon?.DisplayName ?? officerState.EquippedMissionWeaponId ?? "Unassigned Weapon",
				Detail = BuildWeaponDetailText(weapon),
				Highlighted = true
			},
			new MissionInventoryEntry
			{
				Title = shield?.DisplayName ?? officerState.EquippedMissionShieldId ?? "Unassigned Shield",
				Detail = BuildShieldDetailText(shield),
				Highlighted = true
			},
			new MissionInventoryEntry
			{
				Title = string.IsNullOrWhiteSpace(officerState.CombatAbilityID) ? "Combat Discipline" : officerState.CombatAbilityID,
				Detail = "Archived reserve officer profile."
			}
		};
	}

	private static List<MissionInventoryEntry> BuildRemnantWeaponEntries(OfficerState officerState)
	{
		if (officerState == null)
		{
			return new List<MissionInventoryEntry>();
		}

		return OfficerMissionLoadoutService.GetOwnedWeaponIds(officerState)
			.Where(weaponId => !string.IsNullOrWhiteSpace(weaponId))
			.Distinct(StringComparer.Ordinal)
			.Select(weaponId =>
			{
				MissionWeaponDefinition definition = MissionEquipmentRegistry.GetWeapon(weaponId);
				return new MissionInventoryEntry
				{
					Title = definition?.DisplayName ?? weaponId,
					Detail = BuildWeaponDetailText(definition),
					Highlighted = string.Equals(weaponId, officerState.EquippedMissionWeaponId, StringComparison.Ordinal)
				};
			})
			.OrderByDescending(entry => entry.Highlighted)
			.ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static List<MissionInventoryEntry> BuildRemnantShieldEntries(OfficerState officerState)
	{
		if (officerState == null)
		{
			return new List<MissionInventoryEntry>();
		}

		return OfficerMissionLoadoutService.GetOwnedShieldIds(officerState)
			.Where(shieldId => !string.IsNullOrWhiteSpace(shieldId))
			.Distinct(StringComparer.Ordinal)
			.Select(shieldId =>
			{
				MissionShieldDefinition definition = MissionEquipmentRegistry.GetShield(shieldId);
				return new MissionInventoryEntry
				{
					Title = definition?.DisplayName ?? shieldId,
					Detail = BuildShieldDetailText(definition),
					Highlighted = string.Equals(shieldId, officerState.EquippedMissionShieldId, StringComparison.Ordinal)
				};
			})
			.OrderByDescending(entry => entry.Highlighted)
			.ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static List<MissionInventoryEntry> BuildRemnantItemEntries(RemnantRecord remnant, OfficerState officerState)
	{
		IEnumerable<string> itemIds = officerState?.PersonalInventoryItemIDs ?? remnant?.PersonalInventoryItemIDs ?? new List<string>();
		return itemIds
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.GroupBy(itemId => itemId, StringComparer.Ordinal)
			.Select(group =>
			{
				CampaignItemDefinition definition = CampaignItemRegistry.GetItem(group.Key);
				return new MissionInventoryEntry
				{
					Title = definition?.DisplayName ?? group.Key,
					Detail = BuildItemDetailText(definition),
					QuantityText = group.Count() > 1 ? $"x{group.Count()}" : string.Empty
				};
			})
			.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private void ReturnToGame()
	{
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://exploration_battle.tscn");
		else GetTree().ChangeSceneToFile("res://exploration_battle.tscn");
	}

	private PanelContainer BuildInventorySectionCard(string title, out VBoxContainer body)
	{
		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);
		content.AddChild(BuildInventorySectionHeader(title));

		body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 8);
		content.AddChild(body);
		return panel;
	}

	private PanelContainer BuildInventoryRailTag(string text, bool active)
	{
		PanelContainer tag = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 70f)
		};
		tag.AddThemeStyleboxOverride("panel", CreateInventoryRailStyle(active));

		Label label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.SetAnchorsPreset(LayoutPreset.FullRect);
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", active ? new Color(0.96f, 0.90f, 0.80f) : new Color(0.72f, 0.68f, 0.62f));
		tag.AddChild(label);
		return tag;
	}

	private Label BuildInventorySectionHeader(string title)
	{
		Label header = new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		header.AddThemeFontSizeOverride("font_size", 17);
		header.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.74f));
		return header;
	}

	private void PopulateInventoryEntryStack(VBoxContainer container, IReadOnlyList<MissionInventoryEntry> entries, string emptyText)
	{
		if (container == null)
		{
			return;
		}

		ClearContainerChildren(container);
		if (entries == null || entries.Count == 0)
		{
			container.AddChild(BuildInventoryPlaceholder(emptyText));
			return;
		}

		foreach (MissionInventoryEntry entry in entries)
		{
			if (entry == null)
			{
				continue;
			}

			container.AddChild(BuildInventoryEntryCard(entry));
		}
	}

	private void PopulateInventoryItemStack(IReadOnlyList<MissionInventoryEntry> entries)
	{
		if (_remnantInventoryItemStack == null)
		{
			return;
		}

		ClearContainerChildren(_remnantInventoryItemStack);
		int visibleCount = 0;
		foreach (MissionInventoryEntry entry in entries ?? Array.Empty<MissionInventoryEntry>())
		{
			if (entry == null)
			{
				continue;
			}

			_remnantInventoryItemStack.AddChild(BuildInventoryItemCell(entry));
			visibleCount++;
		}

		if (visibleCount == 0)
		{
			_remnantInventoryItemStack.AddChild(BuildInventoryPlaceholder("No field items are archived here."));
			return;
		}

		for (int slotIndex = visibleCount; slotIndex < 4; slotIndex++)
		{
			_remnantInventoryItemStack.AddChild(BuildInventoryEmptyCell());
		}
	}

	private Control BuildInventoryEntryCard(MissionInventoryEntry entry)
	{
		PanelContainer card = new PanelContainer
		{
			TooltipText = entry.Detail,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 76f)
		};
		card.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(entry.Highlighted, false));

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		card.AddChild(margin);

		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		margin.AddChild(row);

		VBoxContainer textColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		textColumn.AddThemeConstantOverride("separation", 2);
		row.AddChild(textColumn);

		Label title = new Label
		{
			Text = entry.Title
		};
		title.AddThemeFontSizeOverride("font_size", 15);
		title.AddThemeColorOverride("font_color", entry.Highlighted ? new Color(0.98f, 0.92f, 0.82f) : Colors.White);
		textColumn.AddChild(title);

		if (!string.IsNullOrWhiteSpace(entry.Detail))
		{
			Label detail = new Label
			{
				Text = entry.Detail,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			detail.AddThemeFontSizeOverride("font_size", 12);
			detail.AddThemeColorOverride("font_color", new Color(0.83f, 0.79f, 0.72f));
			textColumn.AddChild(detail);
		}

		if (!string.IsNullOrWhiteSpace(entry.QuantityText))
		{
			Label quantity = new Label
			{
				Text = entry.QuantityText,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center
			};
			quantity.CustomMinimumSize = new Vector2(40f, 0f);
			quantity.AddThemeFontSizeOverride("font_size", 14);
			quantity.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.56f));
			row.AddChild(quantity);
		}

		return card;
	}

	private Control BuildInventoryItemCell(MissionInventoryEntry entry)
	{
		PanelContainer cell = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 92f),
			TooltipText = entry.Detail,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		cell.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(entry.Highlighted, false));

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		cell.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 4);
		margin.AddChild(content);

		Label title = new Label
		{
			Text = entry.Title,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		title.AddThemeFontSizeOverride("font_size", 14);
		title.AddThemeColorOverride("font_color", Colors.White);
		content.AddChild(title);

		Label detail = new Label
		{
			Text = string.IsNullOrWhiteSpace(entry.Detail) ? "Field gear slot." : entry.Detail,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		detail.AddThemeFontSizeOverride("font_size", 11);
		detail.AddThemeColorOverride("font_color", new Color(0.82f, 0.77f, 0.70f));
		content.AddChild(detail);

		if (!string.IsNullOrWhiteSpace(entry.QuantityText))
		{
			Label quantity = new Label
			{
				Text = entry.QuantityText,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			quantity.AddThemeFontSizeOverride("font_size", 13);
			quantity.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.52f));
			content.AddChild(quantity);
		}

		return cell;
	}

	private Control BuildInventoryPlaceholder(string text)
	{
		PanelContainer card = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		card.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(false, true));

		Label label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.SetAnchorsPreset(LayoutPreset.FullRect);
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", new Color(0.68f, 0.64f, 0.60f));
		card.CustomMinimumSize = new Vector2(0f, 64f);
		card.AddChild(label);
		return card;
	}

	private Control BuildInventoryEmptyCell()
	{
		PanelContainer cell = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 56f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		cell.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(false, true));
		return cell;
	}

	private static void ClearContainerChildren(Node container)
	{
		if (container == null)
		{
			return;
		}

		foreach (Node child in container.GetChildren())
		{
			child.QueueFree();
		}
	}

	private static string BuildWeaponDetailText(MissionWeaponDefinition definition)
	{
		if (definition == null)
		{
			return "No weapon telemetry archived.";
		}

		List<string> parts = new List<string>
		{
			definition.IsMelee ? "Melee" : $"Range {definition.AttackRange}",
			$"DMG {definition.MinDamage}-{definition.MaxDamage}"
		};
		if (definition.BonusShieldDamage > 0)
		{
			parts.Add($"+{definition.BonusShieldDamage} vs shields");
		}

		if (definition.ShieldPiercingDamage > 0)
		{
			parts.Add($"{definition.ShieldPiercingDamage} pierce");
		}

		if (!string.IsNullOrWhiteSpace(definition.StatusEffectId))
		{
			parts.Add($"{definition.StatusEffectId.Replace('_', ' ')} {Mathf.RoundToInt(definition.StatusEffectChance * 100f)}%");
		}

		if (!string.IsNullOrWhiteSpace(definition.Description))
		{
			parts.Add(definition.Description);
		}

		return string.Join(" | ", parts);
	}

	private static string BuildShieldDetailText(MissionShieldDefinition definition)
	{
		if (definition == null)
		{
			return "No shield telemetry archived.";
		}

		List<string> parts = new List<string>
		{
			$"+{definition.CapacityBonus} capacity",
			$"+{definition.RechargePerTurn}/turn"
		};
		if (!string.IsNullOrWhiteSpace(definition.Description))
		{
			parts.Add(definition.Description);
		}

		return string.Join(" | ", parts);
	}

	private static string BuildItemDetailText(CampaignItemDefinition definition)
	{
		if (definition == null)
		{
			return "Recovered field salvage.";
		}

		List<string> parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(definition.Category))
		{
			parts.Add(definition.Category);
		}

		if (!string.IsNullOrWhiteSpace(definition.Description))
		{
			parts.Add(definition.Description);
		}

		return parts.Count == 0 ? "Recovered field salvage." : string.Join(" | ", parts);
	}

	private static StyleBoxFlat CreateInventoryWindowStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.17f, 0.13f, 0.11f, 0.98f),
			BorderColor = new Color(0.55f, 0.50f, 0.44f, 1f),
			BorderWidthLeft = 5,
			BorderWidthTop = 5,
			BorderWidthRight = 5,
			BorderWidthBottom = 5,
			CornerRadiusTopLeft = 16,
			CornerRadiusTopRight = 16,
			CornerRadiusBottomRight = 16,
			CornerRadiusBottomLeft = 16,
			ShadowColor = new Color(0f, 0f, 0f, 0.42f),
			ShadowSize = 12,
			ContentMarginLeft = 4f,
			ContentMarginTop = 4f,
			ContentMarginRight = 4f,
			ContentMarginBottom = 4f
		};
	}

	private static StyleBoxFlat CreateInventoryBannerStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.31f, 0.24f, 0.18f, 0.98f),
			BorderColor = new Color(0.68f, 0.60f, 0.45f, 1f),
			BorderWidthLeft = 4,
			BorderWidthTop = 4,
			BorderWidthRight = 4,
			BorderWidthBottom = 4,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12
		};
	}

	private static StyleBoxFlat CreateInventorySectionStyle(bool highlighted = false)
	{
		return new StyleBoxFlat
		{
			BgColor = highlighted ? new Color(0.25f, 0.20f, 0.17f, 0.96f) : new Color(0.14f, 0.11f, 0.09f, 0.96f),
			BorderColor = highlighted ? new Color(0.72f, 0.60f, 0.42f, 1f) : new Color(0.44f, 0.40f, 0.36f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
	}

	private static StyleBoxFlat CreateInventorySlotStyle(bool highlighted, bool empty)
	{
		return new StyleBoxFlat
		{
			BgColor = empty
				? new Color(0.09f, 0.08f, 0.07f, 0.82f)
				: highlighted
					? new Color(0.34f, 0.23f, 0.14f, 0.94f)
					: new Color(0.18f, 0.15f, 0.12f, 0.94f),
			BorderColor = empty
				? new Color(0.24f, 0.22f, 0.20f, 0.82f)
				: highlighted
					? new Color(0.86f, 0.70f, 0.42f, 1f)
					: new Color(0.46f, 0.40f, 0.34f, 1f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8
		};
	}

	private static StyleBoxFlat CreateInventoryRailStyle(bool active)
	{
		return new StyleBoxFlat
		{
			BgColor = active ? new Color(0.30f, 0.22f, 0.14f, 0.96f) : new Color(0.11f, 0.09f, 0.08f, 0.92f),
			BorderColor = active ? new Color(0.82f, 0.68f, 0.40f, 1f) : new Color(0.36f, 0.33f, 0.30f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
	}

	private static StyleBoxFlat CreateInventoryButtonStyle(bool hover = false)
	{
		return new StyleBoxFlat
		{
			BgColor = hover ? new Color(0.39f, 0.26f, 0.16f, 0.98f) : new Color(0.24f, 0.18f, 0.12f, 0.96f),
			BorderColor = hover ? new Color(0.90f, 0.74f, 0.44f, 1f) : new Color(0.62f, 0.50f, 0.30f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
	}

	// ==========================================
	// DATA HELPERS 
	// ==========================================
	
	private string GetPlayerShipImage(string shipName)
	{
		return Database.GetShipTexturePath(shipName);
	}

	private string GetPlayerBlueprintImage(string shipName)
	{
		return Database.GetShipBlueprintPath(shipName);
	}

	private string GetEnemyShipImage(string shipName)
	{
		return Database.GetShipTexturePath(shipName);
	}

	private string GetEnemyBlueprintImage(string shipName)
	{
		return Database.GetShipBlueprintPath(shipName);
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
		return Database.GetShipBaseActions(shipName);
	}

	private (int hp, int shields) GetShipCombatStats(string shipName)
	{
		return Database.GetShipCombatStats(shipName);
	}

	private (int range, int damage) GetShipWeaponStats(string shipName)
	{
		return Database.GetShipWeaponStats(shipName);
	}
}
