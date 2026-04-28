using Godot;
using System.Collections.Generic;

// ==========================================
// DATA CONTAINERS
// ==========================================
public class PlanetData 
{
	public string Name { get; set; }
	public int TypeIndex { get; set; }
	public float Distance { get; set; }
	public float Speed { get; set; }
	public string Habitability { get; set; }
	public string Resources { get; set; }
	public Vector2 Position { get; set; } 
	public float Scale { get; set; } 
	public float StartingAngle { get; set; } 
	
	public bool HasBeenScanned { get; set; } = false;
	public bool HasBeenSalvaged { get; set; } = false;
}

public class OutpostData
{
	public string Name { get; set; }
	public Vector2I HexPosition { get; set; }
	public string SpritePath { get; set; }
}

public class SystemData 
{
	public string SystemName { get; set; }
	public Vector2 StarPosition { get; set; }
	public List<PlanetData> Planets { get; set; } = new List<PlanetData>();
	
	public bool HasBeenVisited { get; set; } = false;
	public Godot.Collections.Array EnemyFleets { get; set; } = new Godot.Collections.Array();

	public List<Vector2I> StargateHexes { get; set; } = new List<Vector2I>(); 
	public List<Vector2I> AsteroidHexes { get; set; } = new List<Vector2I>();
	public List<Vector2I> RadiationHexes { get; set; } = new List<Vector2I>();
	public List<Vector2I> ExploredHexes { get; set; } = new List<Vector2I>();
	public List<Vector2I> RadarRevealedHexes { get; set; } = new List<Vector2I>();
	public List<OutpostData> Outposts { get; set; } = new List<OutpostData>();
}

public class StarMapData
{
	public string SystemName { get; set; }
	public Vector2 MapPosition { get; set; }
	public int PlanetCount { get; set; }
	public float StarScale { get; set; }
	public Color StarColor { get; set; }
	public string Region { get; set; }
}

public class QuestData
{
	public string QuestID { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
	public string TargetSystem { get; set; }
	public bool IsComplete { get; set; } = false;
}

// --- NEW: EQUIPMENT AND LOADOUT CLASSES ---
public class EquipmentData
{
	public string ItemID { get; set; }
	public string Name { get; set; }
	public string Category { get; set; } // "Weapon", "Shield", or "Armor"
	public int BonusStat { get; set; } // The amount of extra Dmg, HP, or Shields it gives
	public float CostTech { get; set; }
	public float CostRaw { get; set; }
	public string Description { get; set; }
}

public class ShipLoadout
{
	public string WeaponID { get; set; } = "";
	public string ShieldID { get; set; } = "";
	public string ArmorID { get; set; } = "";
}

// ==========================================
// THE SINGLETON
// ==========================================
public partial class GlobalData : Node
{
	public string SavedSystem { get; set; } = "";
	public string SavedPlanet { get; set; } = "";
	public string SavedType { get; set; } = "";

	public string SelectedBasePlanetType { get; set; } = ""; 
	public Vector2 SelectedBasePlanetHexCoords { get; set; } = new Vector2(0, 0);
	public List<string> SelectedPlayerFleet { get; set; } = new List<string>();

	public Dictionary<string, SystemData> ExploredSystems { get; set; } = new Dictionary<string, SystemData>();
	public List<StarMapData> CurrentSectorStars { get; set; } = new List<StarMapData>();

	public int CurrentTurn { get; set; } = 1;
	public bool InCombat { get; set; } = false;
	public int CurrentQueueIndex { get; set; } = 0;
	public bool JustJumped { get; set; } = false;

	public Godot.Collections.Array SavedFleetState { get; set; } = new Godot.Collections.Array();

	public Godot.Collections.Dictionary<string, Variant> FleetResources { get; set; } = new Godot.Collections.Dictionary<string, Variant>
	{
		{ GameConstants.ResourceKeys.RawMaterials, 350.0f },
		{ GameConstants.ResourceKeys.EnergyCores, 5.0f },
		{ GameConstants.ResourceKeys.AncientTech, 0.0f }
	};
	
	// --- NEW: INVENTORY MEMORY ---
	// Stores ItemIDs of gear you own but haven't equipped yet
	public List<string> UnequippedInventory { get; set; } = new List<string>(); 
	
	// Maps a Ship's Name to its specific loadout
	public Dictionary<string, ShipLoadout> FleetLoadouts { get; set; } = new Dictionary<string, ShipLoadout>();

	public List<QuestData> ActiveQuests { get; set; } = new List<QuestData>();
	public Godot.Collections.Array CompletedQuestIDs { get; set; } = new Godot.Collections.Array();

	private readonly SaveGameService _saveGameService = new SaveGameService();

	// --- MASTER EQUIPMENT DATABASE ---
	// This acts as the universal catalog of all items in the game
	public Dictionary<string, EquipmentData> MasterEquipmentDB { get; set; } = new Dictionary<string, EquipmentData>
	{
		// WEAPONS
		{ "WPN_LASER_MK2", new EquipmentData { ItemID = "WPN_LASER_MK2", Name = "Mk II Pulse Laser", Category = GameConstants.EquipmentCategories.Weapon, BonusStat = 10, CostTech = 1, CostRaw = 100, Description = "+10 Max Attack Damage" } },
		{ "WPN_RAILGUN", new EquipmentData { ItemID = "WPN_RAILGUN", Name = "Magnetic Railgun", Category = GameConstants.EquipmentCategories.Weapon, BonusStat = 25, CostTech = 3, CostRaw = 300, Description = "+25 Max Attack Damage" } },
		// SHIELDS
		{ "SHLD_DEFLECTOR", new EquipmentData { ItemID = "SHLD_DEFLECTOR", Name = "Ion Deflector", Category = GameConstants.EquipmentCategories.Shield, BonusStat = 50, CostTech = 1, CostRaw = 150, Description = "+50 Max Shields" } },
		{ "SHLD_AEGIS", new EquipmentData { ItemID = "SHLD_AEGIS", Name = "Aegis Generator", Category = GameConstants.EquipmentCategories.Shield, BonusStat = 120, CostTech = 3, CostRaw = 400, Description = "+120 Max Shields" } },
		// ARMOR
		{ "ARMR_TITANIUM", new EquipmentData { ItemID = "ARMR_TITANIUM", Name = "Titanium Plating", Category = GameConstants.EquipmentCategories.Armor, BonusStat = 100, CostTech = 1, CostRaw = 200, Description = "+100 Max Hull HP" } },
		{ "ARMR_NEUTRONIUM", new EquipmentData { ItemID = "ARMR_NEUTRONIUM", Name = "Neutronium Hull", Category = GameConstants.EquipmentCategories.Armor, BonusStat = 300, CostTech = 4, CostRaw = 500, Description = "+300 Max Hull HP" } }
	};

	public override void _Ready()
	{
		GD.Print("GlobalData Singleton Initialized successfully.");
	}

	public void SaveGame()
	{
		_saveGameService.Save(this);
	}

	public bool LoadGame()
	{
		return _saveGameService.Load(this);
	}

	public void ResetForNewGame()
	{
		SavedSystem = ""; SavedPlanet = ""; SavedType = ""; SelectedBasePlanetType = "";
		SelectedBasePlanetHexCoords = Vector2.Zero; SelectedPlayerFleet.Clear();
		ExploredSystems.Clear(); CurrentSectorStars.Clear();
		CurrentTurn = 1; InCombat = false; CurrentQueueIndex = 0; JustJumped = false; 
		SavedFleetState.Clear(); UnequippedInventory.Clear(); FleetLoadouts.Clear();
		
		FleetResources = new Godot.Collections.Dictionary<string, Variant> {
			{ GameConstants.ResourceKeys.RawMaterials, 350.0f },
			{ GameConstants.ResourceKeys.EnergyCores, 5.0f },
			{ GameConstants.ResourceKeys.AncientTech, 0.0f }
		};

		_saveGameService.DeleteSave();
		GD.Print("GlobalData has been completely wiped for a new campaign.");
	}
}
