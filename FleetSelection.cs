using Godot;
using System.Collections.Generic;

public class ShipBlueprint
{
	public string Name { get; set; }
	public string ClassType { get; set; }
	public string TexturePath { get; set; }
	
	// --- Path to the large promo image ---
	public string DescriptionImagePath { get; set; } 
}

public partial class FleetSelection : Control
{
	private GlobalData _globalData;

	// --- LEFT PANEL UI ---
	private Label _planetInfoLabel;
	private TextureRect _planetPreview;
	private Button _backButton;
	private TextureRect _shipDescriptionPreview; 

	// --- RIGHT PANEL UI ---
	private VBoxContainer _styleSelectionMenu;
	private VBoxContainer _fleetBuilderMenu;
	private HBoxContainer _slotContainer;
	private VBoxContainer _availableShipsList;
	
	// --- NEW: THE DEPLOY BUTTON ---
	private Button _deployButton;

	// --- FLEET LOGIC ---
	private int _maxSlots = 0;
	private List<ShipBlueprint> _masterShipList = new List<ShipBlueprint>();
	private ShipBlueprint[] _selectedFleet;

	public override void _Ready()
	{
		_globalData = GetNode<GlobalData>("/root/GlobalData");

		// Link Left Panel Nodes
		_planetInfoLabel = GetNodeOrNull<Label>("HBoxContainer/LeftPanel/VBoxContainer/PlanetInfoLabel");
		_planetPreview = GetNodeOrNull<TextureRect>("HBoxContainer/LeftPanel/VBoxContainer/PlanetPreview");
		_backButton = GetNodeOrNull<Button>("HBoxContainer/LeftPanel/VBoxContainer/BackButton");
		
		_shipDescriptionPreview = GetNodeOrNull<TextureRect>("HBoxContainer/LeftPanel/VBoxContainer/ShipDescriptionPreview");
		if (_shipDescriptionPreview != null) _shipDescriptionPreview.Visible = false; 

		// Link Right Panel Nodes
		_styleSelectionMenu = GetNodeOrNull<VBoxContainer>("HBoxContainer/RightPanel/StyleSelection");
		_fleetBuilderMenu = GetNodeOrNull<VBoxContainer>("HBoxContainer/RightPanel/FleetBuilder");
		_slotContainer = GetNodeOrNull<HBoxContainer>("HBoxContainer/RightPanel/FleetBuilder/SlotContainer");
		_availableShipsList = GetNodeOrNull<VBoxContainer>("HBoxContainer/RightPanel/FleetBuilder/ScrollContainer/AvailableShipList");

		// --- NEW: LINK THE DEPLOY BUTTON ---
		// Note: Make sure the path matches where you put the button in the Godot Editor!
		_deployButton = GetNodeOrNull<Button>("HBoxContainer/RightPanel/FleetBuilder/DeployButton");
		if (_deployButton != null) _deployButton.Pressed += OnDeployButtonPressed;

		// Link Style Buttons
		Button loneWolfBtn = GetNodeOrNull<Button>("HBoxContainer/RightPanel/StyleSelection/LoneWolfButton");
		Button vanguardBtn = GetNodeOrNull<Button>("HBoxContainer/RightPanel/StyleSelection/VanguardButton");
		Button budgetBtn = GetNodeOrNull<Button>("HBoxContainer/RightPanel/StyleSelection/BudgetButton");

		if (loneWolfBtn != null) loneWolfBtn.Pressed += () => StartFleetBuilder(1);
		if (vanguardBtn != null) vanguardBtn.Pressed += () => StartFleetBuilder(3);
		if (budgetBtn != null) budgetBtn.Pressed += () => StartFleetBuilder(5); 

		if (_backButton != null) _backButton.Pressed += _on_back_button_pressed;

		// --- SETUP PLANET INFO & TEXTURE ---
		if (_planetInfoLabel != null)
		{
			_planetInfoLabel.Text = $"=== HOMEWORLD ===\n\n" +
									$"System: {_globalData.SavedSystem.ToUpper()}\n" +
									$"Planet: {_globalData.SavedPlanet}\n" +
									$"Class: {_globalData.SavedType}";
		}

		if (_planetPreview != null && !string.IsNullOrEmpty(_globalData.SavedType))
		{
			string texturePath = GetTexturePathForType(_globalData.SavedType);
			Texture2D tex = GD.Load<Texture2D>(texturePath);
			if (tex != null)
			{
				_planetPreview.Texture = tex;
			}
		}

		// Load the real ship data
		LoadAvailableShips();

		if (_globalData.SelectedPlayerFleet != null && _globalData.SelectedPlayerFleet.Count > 0)
		{
			int restoredSlots = _globalData.SelectedFleetCapacity > 0 ? _globalData.SelectedFleetCapacity : _globalData.SelectedPlayerFleet.Count;
			StartFleetBuilder(restoredSlots);
			RestoreSelectedFleet();
		}
	}

	// --- HELPER FOR PLANET TEXTURES ---
	private string GetTexturePathForType(string type)
	{
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

	// --- PHASE 2: INITIALIZE BUILDER ---
	private void StartFleetBuilder(int slots)
	{
		_maxSlots = slots;
		_selectedFleet = new ShipBlueprint[slots];

		_styleSelectionMenu.Visible = false;
		_fleetBuilderMenu.Visible = true;

		RefreshSlotsVisuals();
		PopulateAvailableShips();
	}

	private void RestoreSelectedFleet()
	{
		if (_selectedFleet == null || _globalData.SelectedPlayerFleet == null)
		{
			return;
		}

		for (int i = 0; i < _selectedFleet.Length && i < _globalData.SelectedPlayerFleet.Count; i++)
		{
			_selectedFleet[i] = _masterShipList.Find(ship => ship.Name == _globalData.SelectedPlayerFleet[i]);
		}

		RefreshSlotsVisuals();
	}

	// --- SLOT MANAGEMENT ---
	private void RefreshSlotsVisuals()
	{
		foreach (Node child in _slotContainer.GetChildren())
		{
			child.QueueFree();
		}

		for (int i = 0; i < _maxSlots; i++)
		{
			Button slotBtn = new Button();
			slotBtn.CustomMinimumSize = new Vector2(120, 120); 
			
			if (_selectedFleet[i] == null)
			{
				slotBtn.Text = $"[ Slot {i + 1} ]\nEmpty";
			}
			else
			{
				slotBtn.Text = _selectedFleet[i].Name;
				Texture2D shipTex = GD.Load<Texture2D>(_selectedFleet[i].TexturePath);
				if (shipTex != null)
				{
					slotBtn.Icon = shipTex;
					slotBtn.ExpandIcon = true;
					slotBtn.IconAlignment = HorizontalAlignment.Center;
					slotBtn.VerticalIconAlignment = VerticalAlignment.Top;
				}
				
				int slotIndex = i; 
				slotBtn.Pressed += () => RemoveShipFromSlot(slotIndex);
			}

			_slotContainer.AddChild(slotBtn);
		}
	}

	private void PopulateAvailableShips()
	{
		foreach (Node child in _availableShipsList.GetChildren())
		{
			child.QueueFree();
		}

		foreach (ShipBlueprint ship in _masterShipList)
		{
			Button shipBtn = new Button();
			shipBtn.Text = $"  {ship.Name}\n  Class: {ship.ClassType}";
			shipBtn.CustomMinimumSize = new Vector2(300, 80);
			shipBtn.Alignment = HorizontalAlignment.Left;

			Texture2D shipTex = GD.Load<Texture2D>(ship.TexturePath);
			if (shipTex != null)
			{
				shipBtn.Icon = shipTex;
				shipBtn.ExpandIcon = true;
			}
			else
			{
				GD.PrintErr($"Could not load texture for {ship.Name} at: {ship.TexturePath}");
			}
			
			shipBtn.Pressed += () => AssignShipToNextEmptySlot(ship);
			shipBtn.MouseEntered += () => OnShipHovered(ship);
			shipBtn.MouseExited += OnShipHoverExited;

			_availableShipsList.AddChild(shipBtn);
		}
	}

	// --- HOVER LOGIC ---
	private void OnShipHovered(ShipBlueprint ship)
	{
		if (_shipDescriptionPreview != null && !string.IsNullOrEmpty(ship.DescriptionImagePath))
		{
			Texture2D descTex = GD.Load<Texture2D>(ship.DescriptionImagePath);
			if (descTex != null)
			{
				_shipDescriptionPreview.Texture = descTex;
				_shipDescriptionPreview.Visible = true;
			}
		}
	}

	private void OnShipHoverExited()
	{
		if (_shipDescriptionPreview != null)
		{
			_shipDescriptionPreview.Visible = false; // Hide when mouse leaves
		}
	}

	// --- CLICK TO ASSIGN LOGIC ---
	private void AssignShipToNextEmptySlot(ShipBlueprint ship)
	{
		for (int i = 0; i < _maxSlots; i++)
		{
			if (_selectedFleet[i] == null)
			{
				_selectedFleet[i] = ship;
				RefreshSlotsVisuals();
				return;
			}
		}
	}

	private void RemoveShipFromSlot(int index)
	{
		_selectedFleet[index] = null;
		RefreshSlotsVisuals();
	}

	// --- DATA ---
	private void LoadAvailableShips()
	{
		_masterShipList.Add(new ShipBlueprint { 
			Name = "The Relic Harvester", ClassType = "Tech Salvager", 
			TexturePath = "res://Ships/RelicHarvesterSprite.png", DescriptionImagePath = "res://Ships/RelicHarvester.png" 
		});
		_masterShipList.Add(new ShipBlueprint { 
			Name = "The Panacea Spire", ClassType = "Medical & Rescue", 
			TexturePath = "res://Ships/PanaceaSpireSprite.png", DescriptionImagePath = "res://Ships/PanaceaSpire.png" 
		}); 
		_masterShipList.Add(new ShipBlueprint { 
			Name = "The Neptune Forge", ClassType = "Mining & Refinery", 
			TexturePath = "res://Ships/NeptuneForgeSprite.png", DescriptionImagePath = "res://Ships/NeptuneForge.png" 
		});
		_masterShipList.Add(new ShipBlueprint { 
			Name = "The Genesis Ark", ClassType = "Balanced Custodian", 
			TexturePath = "res://Ships/GenesisArkSprite.png", DescriptionImagePath = "res://Ships/GenesisArk.png" 
		});
		_masterShipList.Add(new ShipBlueprint { 
			Name = "The Valkyrie Wing", ClassType = "Military Gunship", 
			TexturePath = "res://Ships/ValkyrieWingSprite.png", DescriptionImagePath = "res://Ships/ValkyrieWing.png" 
		});
		_masterShipList.Add(new ShipBlueprint { 
			Name = "The Aegis Bastion", ClassType = "Survival Dreadnought", 
			TexturePath = "res://Ships/AegisBastionSprite.png", DescriptionImagePath = "res://Ships/AegisBastion.png" 
		});
		_masterShipList.Add(new ShipBlueprint { 
			Name = "The Aether Skimmer", ClassType = "Advanced Explorer", 
			TexturePath = "res://Ships/AetherSkimmerSprite.png", DescriptionImagePath = "res://Ships/AetherSkimmer.png" 
		});
	}

	// --- NEW: TRANSITION TO BATTLE MAP ---
	private void OnDeployButtonPressed()
	{
		// 1. Save the current selected fleet
		_globalData.SelectedPlayerFleet.Clear();
		_globalData.SelectedFleetCapacity = _maxSlots;

		bool hasShips = false;

		// 2. Add currently assigned ships
		for (int i = 0; i < _maxSlots; i++)
		{
			if (_selectedFleet[i] != null)
			{
				_globalData.SelectedPlayerFleet.Add(_selectedFleet[i].Name);
				hasShips = true;
			}
		}

		// (Optional) Prevent deploying an empty fleet
		if (!hasShips)
		{
			GD.Print("Cannot deploy an empty fleet! Please assign at least one ship.");
			return;
		}

		HashSet<string> selectedShips = new HashSet<string>(_globalData.SelectedPlayerFleet);
		foreach (string shipName in new List<string>(_globalData.ShipOfficers.Keys))
		{
			if (!selectedShips.Contains(shipName))
			{
				_globalData.ShipOfficers.Remove(shipName);
			}
		}

		GD.Print($"Deploying Fleet: {string.Join(", ", _globalData.SelectedPlayerFleet)}");

		// 3. Hand off to officer assignment before entering the run.
		var transitioner = GetNode<SceneTransition>("/root/SceneTransition");
		transitioner.ChangeScene("res://officer_assignment.tscn");
	}

	public void _on_back_button_pressed()
	{
		var transitioner = GetNode<SceneTransition>("/root/SceneTransition");
		transitioner.ChangeScene("res://system_view.tscn");
	}
}
