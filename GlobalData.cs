using Godot;
using System.Collections.Generic;
using System.Linq;

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

	private string _savePath = "user://savegame.json"; 

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
		var saveData = new Godot.Collections.Dictionary<string, Variant>();
		
		saveData["SavedSystem"] = SavedSystem;
		saveData["SavedPlanet"] = SavedPlanet;
		saveData["CurrentTurn"] = CurrentTurn;
		saveData["InCombat"] = InCombat;
		saveData["CurrentQueueIndex"] = CurrentQueueIndex;
		saveData["SavedFleetState"] = SavedFleetState;
		saveData["FleetResources"] = FleetResources;
		
		// Save Inventory
		var invArray = new Godot.Collections.Array();
		foreach (string item in UnequippedInventory) invArray.Add(item);
		saveData["UnequippedInventory"] = invArray;

		// Save Loadouts
		var loadoutDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var kvp in FleetLoadouts)
		{
			var sDict = new Godot.Collections.Dictionary<string, string>();
			sDict["WeaponID"] = kvp.Value.WeaponID;
			sDict["ShieldID"] = kvp.Value.ShieldID;
			sDict["ArmorID"] = kvp.Value.ArmorID;
			loadoutDict[kvp.Key] = sDict;
		}
		saveData["FleetLoadouts"] = loadoutDict;

		var fleetArray = new Godot.Collections.Array();
		if (SelectedPlayerFleet != null)
		{
			foreach (string ship in SelectedPlayerFleet) fleetArray.Add(ship);
		}
		saveData["SelectedPlayerFleet"] = fleetArray;

		var exploredDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var sysKvp in ExploredSystems)
		{
			var sysData = new Godot.Collections.Dictionary<string, Variant>();
			sysData["SystemName"] = sysKvp.Value.SystemName;
			sysData["HasBeenVisited"] = sysKvp.Value.HasBeenVisited;
			sysData["EnemyFleets"] = sysKvp.Value.EnemyFleets;
			
			sysData["StargateHexes"] = ConvertVectorListToVariantArray(sysKvp.Value.StargateHexes);
			sysData["AsteroidHexes"] = ConvertVectorListToVariantArray(sysKvp.Value.AsteroidHexes);
			sysData["RadiationHexes"] = ConvertVectorListToVariantArray(sysKvp.Value.RadiationHexes);
			sysData["ExploredHexes"] = ConvertVectorListToVariantArray(sysKvp.Value.ExploredHexes);
			sysData["RadarRevealedHexes"] = ConvertVectorListToVariantArray(sysKvp.Value.RadarRevealedHexes);
			
			var pArray = new Godot.Collections.Array<Variant>();
			foreach (var p in sysKvp.Value.Planets)
			{
				var pDict = new Godot.Collections.Dictionary<string, Variant>();
				pDict["Name"] = p.Name;
				pDict["TypeIndex"] = p.TypeIndex;
				pDict["Scale"] = p.Scale;
				pDict["Habitability"] = p.Habitability;
				
				pDict["Distance"] = p.Distance;
				pDict["Speed"] = p.Speed;
				pDict["StartingAngle"] = p.StartingAngle;
				
				pDict["HasBeenScanned"] = p.HasBeenScanned;
				pDict["HasBeenSalvaged"] = p.HasBeenSalvaged;
				pArray.Add(pDict);
			}
			sysData["Planets"] = pArray;

			var oArray = new Godot.Collections.Array<Variant>();
			if (sysKvp.Value.Outposts != null)
			{
				foreach (var o in sysKvp.Value.Outposts)
				{
					var oDict = new Godot.Collections.Dictionary<string, Variant>();
					oDict["Name"] = o.Name;
					oDict["Q"] = o.HexPosition.X;
					oDict["R"] = o.HexPosition.Y;
					oDict["SpritePath"] = o.SpritePath;
					oArray.Add(oDict);
				}
			}
			sysData["Outposts"] = oArray;

			exploredDict[sysKvp.Key] = sysData;
		}
		saveData["ExploredSystems"] = exploredDict;

		string jsonString = Json.Stringify(saveData);
		using var file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Write);
		if (file != null)
		{
			file.StoreString(jsonString);
			GD.Print("Game successfully saved to: " + _savePath);
		}
	}

	public bool LoadGame()
	{
		if (!FileAccess.FileExists(_savePath)) return false;

		using var file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Read);
		var json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok) return false;

		var savedData = (Godot.Collections.Dictionary)json.Data;

		if (savedData.ContainsKey("SavedSystem")) SavedSystem = (string)savedData["SavedSystem"];
		if (savedData.ContainsKey("SavedPlanet")) SavedPlanet = (string)savedData["SavedPlanet"];
		if (savedData.ContainsKey("CurrentTurn")) CurrentTurn = (int)savedData["CurrentTurn"];
		if (savedData.ContainsKey("InCombat")) InCombat = (bool)savedData["InCombat"];
		if (savedData.ContainsKey("CurrentQueueIndex")) CurrentQueueIndex = (int)savedData["CurrentQueueIndex"];
		if (savedData.ContainsKey("SavedFleetState")) SavedFleetState = (Godot.Collections.Array)savedData["SavedFleetState"];
		if (savedData.ContainsKey("FleetResources")) FleetResources = (Godot.Collections.Dictionary<string, Variant>)savedData["FleetResources"];

		// Load Inventory
		UnequippedInventory.Clear();
		if (savedData.ContainsKey("UnequippedInventory"))
		{
			var invArray = (Godot.Collections.Array)savedData["UnequippedInventory"];
			foreach(var item in invArray) UnequippedInventory.Add((string)item);
		}

		// Load Loadouts
		FleetLoadouts.Clear();
		if (savedData.ContainsKey("FleetLoadouts"))
		{
			var loadoutDict = (Godot.Collections.Dictionary)savedData["FleetLoadouts"];
			foreach (var key in loadoutDict.Keys)
			{
				var sDict = (Godot.Collections.Dictionary)loadoutDict[key];
				FleetLoadouts[(string)key] = new ShipLoadout {
					WeaponID = (string)sDict["WeaponID"],
					ShieldID = (string)sDict["ShieldID"],
					ArmorID = (string)sDict["ArmorID"]
				};
			}
		}

		if (savedData.ContainsKey("SelectedPlayerFleet"))
		{
			SelectedPlayerFleet = ((Godot.Collections.Array)savedData["SelectedPlayerFleet"]).Select(v => (string)v).ToList();
		}

		if (savedData.ContainsKey("ExploredSystems"))
		{
			var exploredDict = (Godot.Collections.Dictionary)savedData["ExploredSystems"];
			ExploredSystems.Clear();
			
			foreach (var key in exploredDict.Keys)
			{
				string sysName = (string)key;
				var sysDict = (Godot.Collections.Dictionary)exploredDict[key];
				
				SystemData newSys = new SystemData { SystemName = (string)sysDict["SystemName"] };
				
				newSys.HasBeenVisited = sysDict.ContainsKey("HasBeenVisited") ? (bool)sysDict["HasBeenVisited"] : false;
				newSys.EnemyFleets = sysDict.ContainsKey("EnemyFleets") ? (Godot.Collections.Array)sysDict["EnemyFleets"] : new Godot.Collections.Array();
				
				newSys.StargateHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("StargateHexes") ? (Godot.Collections.Array)sysDict["StargateHexes"] : new Godot.Collections.Array());
				newSys.AsteroidHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("AsteroidHexes") ? (Godot.Collections.Array)sysDict["AsteroidHexes"] : new Godot.Collections.Array());
				newSys.RadiationHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("RadiationHexes") ? (Godot.Collections.Array)sysDict["RadiationHexes"] : new Godot.Collections.Array());
				newSys.ExploredHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("ExploredHexes") ? (Godot.Collections.Array)sysDict["ExploredHexes"] : new Godot.Collections.Array());
				newSys.RadarRevealedHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("RadarRevealedHexes") ? (Godot.Collections.Array)sysDict["RadarRevealedHexes"] : new Godot.Collections.Array());
				
				var pArray = (Godot.Collections.Array)sysDict["Planets"];
				foreach (var pVar in pArray)
				{
					var pDict = (Godot.Collections.Dictionary)pVar;
					newSys.Planets.Add(new PlanetData {
						Name = (string)pDict["Name"],
						TypeIndex = (int)pDict["TypeIndex"],
						Scale = (float)pDict["Scale"],
						Habitability = (string)pDict["Habitability"],
						
						Distance = pDict.ContainsKey("Distance") ? (float)pDict["Distance"] : 0f,
						Speed = pDict.ContainsKey("Speed") ? (float)pDict["Speed"] : 0f,
						StartingAngle = pDict.ContainsKey("StartingAngle") ? (float)pDict["StartingAngle"] : 0f,
						
						HasBeenScanned = pDict.ContainsKey("HasBeenScanned") ? (bool)pDict["HasBeenScanned"] : false,
						HasBeenSalvaged = pDict.ContainsKey("HasBeenSalvaged") ? (bool)pDict["HasBeenSalvaged"] : false
					});
				}

				if (sysDict.ContainsKey("Outposts"))
				{
					var oArray = (Godot.Collections.Array)sysDict["Outposts"];
					foreach (var oVar in oArray)
					{
						var oDict = (Godot.Collections.Dictionary)oVar;
						newSys.Outposts.Add(new OutpostData {
							Name = (string)oDict["Name"],
							HexPosition = new Vector2I((int)oDict["Q"], (int)oDict["R"]),
							SpritePath = (string)oDict["SpritePath"]
						});
					}
				}

				ExploredSystems[sysName] = newSys;
			}
		}
		return true;
	}

	private Godot.Collections.Array ConvertVectorListToVariantArray(List<Vector2I> list)
	{
		var arr = new Godot.Collections.Array();
		foreach (var vec in list)
		{
			var dict = new Godot.Collections.Dictionary<string, int> { { "Q", vec.X }, { "R", vec.Y } };
			arr.Add(dict);
		}
		return arr;
	}

	private List<Vector2I> ConvertVariantArrayToVectorList(Godot.Collections.Array arr)
	{
		var list = new List<Vector2I>();
		foreach (var item in arr)
		{
			var dict = (Godot.Collections.Dictionary)item;
			list.Add(new Vector2I((int)dict["Q"], (int)dict["R"]));
		}
		return list;
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

		if (FileAccess.FileExists(_savePath)) DirAccess.RemoveAbsolute(_savePath);
		GD.Print("GlobalData has been completely wiped for a new campaign.");
	}
}
