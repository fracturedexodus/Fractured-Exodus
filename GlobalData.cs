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
}

public class SystemData 
{
	public string SystemName { get; set; }
	public Vector2 StarPosition { get; set; }
	public List<PlanetData> Planets { get; set; } = new List<PlanetData>();
	
	// --- NEW: SYSTEM-SPECIFIC MEMORY ---
	public bool HasBeenVisited { get; set; } = false;
	public Godot.Collections.Array EnemyFleets { get; set; } = new Godot.Collections.Array();
	public List<Vector2I> StargateLocations { get; set; } = new List<Vector2I>();
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

// ==========================================
// THE SINGLETON
// ==========================================
public partial class GlobalData : Node
{
	// --- 1. Current UI Selection ---
	public string SavedSystem { get; set; } = "";
	public string SavedPlanet { get; set; } = "";
	public string SavedType { get; set; } = "";

	// --- 2. BASE PLANET & FLEET SELECTION ---
	public string SelectedBasePlanetType { get; set; } = ""; 
	public Vector2 SelectedBasePlanetHexCoords { get; set; } = new Vector2(0, 0);
	public List<string> SelectedPlayerFleet { get; set; } = new List<string>();

	// --- 3. System Memory ---
	public Dictionary<string, SystemData> ExploredSystems { get; set; } = new Dictionary<string, SystemData>();
	public List<StarMapData> CurrentSectorStars { get; set; } = new List<StarMapData>();

	// --- 4. BATTLE STATE MEMORY ---
	public int CurrentTurn { get; set; } = 1;
	public bool InCombat { get; set; } = false;
	public int CurrentQueueIndex { get; set; } = 0;
	public bool JustJumped { get; set; } = false;

	// Only the PLAYER fleet is saved globally because it travels with you between systems.
	public Godot.Collections.Array SavedFleetState { get; set; } = new Godot.Collections.Array();

	public override void _Ready()
	{
		GD.Print("GlobalData Singleton Initialized successfully.");
	}

	// ==========================================
	// SAVE & LOAD SYSTEM
	// ==========================================
	private string _savePath = "user://savegame.json"; 

	public void SaveGame()
	{
		var saveData = new Godot.Collections.Dictionary<string, Variant>();
		
		saveData["SavedSystem"] = SavedSystem;
		saveData["SavedPlanet"] = SavedPlanet;
		
		saveData["CurrentTurn"] = CurrentTurn;
		saveData["InCombat"] = InCombat;
		saveData["CurrentQueueIndex"] = CurrentQueueIndex;
		saveData["SavedFleetState"] = SavedFleetState;
		
		var fleetArray = new Godot.Collections.Array();
		if (SelectedPlayerFleet != null)
		{
			foreach (string ship in SelectedPlayerFleet) 
			{
				fleetArray.Add(ship);
			}
		}
		saveData["SelectedPlayerFleet"] = fleetArray;

		var exploredDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (var sysKvp in ExploredSystems)
		{
			var sysData = new Godot.Collections.Dictionary<string, Variant>();
			sysData["SystemName"] = sysKvp.Value.SystemName;
			
			// Save the new system-specific data
			sysData["HasBeenVisited"] = sysKvp.Value.HasBeenVisited;
			sysData["EnemyFleets"] = sysKvp.Value.EnemyFleets;
			
			var gateArray = new Godot.Collections.Array();
			foreach (Vector2I gate in sysKvp.Value.StargateLocations)
			{
				var gateDict = new Godot.Collections.Dictionary<string, Variant>();
				gateDict["Q"] = gate.X; gateDict["R"] = gate.Y;
				gateArray.Add(gateDict);
			}
			sysData["StargateLocations"] = gateArray;
			
			var pArray = new Godot.Collections.Array<Variant>();
			foreach (var p in sysKvp.Value.Planets)
			{
				var pDict = new Godot.Collections.Dictionary<string, Variant>();
				pDict["Name"] = p.Name;
				pDict["TypeIndex"] = p.TypeIndex;
				pDict["Scale"] = p.Scale;
				pDict["Habitability"] = p.Habitability;
				pArray.Add(pDict);
			}
			sysData["Planets"] = pArray;
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
		if (!FileAccess.FileExists(_savePath))
		{
			GD.PrintErr("No save file found at: " + _savePath);
			return false;
		}

		using var file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Read);
		string jsonString = file.GetAsText();

		var json = new Json();
		var parseResult = json.Parse(jsonString);
		
		if (parseResult != Error.Ok) return false;

		var savedData = (Godot.Collections.Dictionary)json.Data;

		if (savedData.ContainsKey("SavedSystem")) SavedSystem = (string)savedData["SavedSystem"];
		if (savedData.ContainsKey("SavedPlanet")) SavedPlanet = (string)savedData["SavedPlanet"];
		
		if (savedData.ContainsKey("CurrentTurn")) CurrentTurn = (int)savedData["CurrentTurn"];
		if (savedData.ContainsKey("InCombat")) InCombat = (bool)savedData["InCombat"];
		if (savedData.ContainsKey("CurrentQueueIndex")) CurrentQueueIndex = (int)savedData["CurrentQueueIndex"];
		
		if (savedData.ContainsKey("SavedFleetState")) SavedFleetState = (Godot.Collections.Array)savedData["SavedFleetState"];
		
		if (savedData.ContainsKey("SelectedPlayerFleet"))
		{
			var fleetArray = (Godot.Collections.Array)savedData["SelectedPlayerFleet"];
			SelectedPlayerFleet = new List<string>();
			foreach (var ship in fleetArray) SelectedPlayerFleet.Add((string)ship);
		}

		if (savedData.ContainsKey("ExploredSystems"))
		{
			var exploredDict = (Godot.Collections.Dictionary)savedData["ExploredSystems"];
			ExploredSystems.Clear();
			
			foreach (var key in exploredDict.Keys)
			{
				string sysName = (string)key;
				var sysDataDict = (Godot.Collections.Dictionary)exploredDict[key];
				
				SystemData newSys = new SystemData();
				newSys.SystemName = (string)sysDataDict["SystemName"];
				
				// Load new system-specific data
				if (sysDataDict.ContainsKey("HasBeenVisited")) newSys.HasBeenVisited = (bool)sysDataDict["HasBeenVisited"];
				if (sysDataDict.ContainsKey("EnemyFleets")) newSys.EnemyFleets = (Godot.Collections.Array)sysDataDict["EnemyFleets"];
				
				if (sysDataDict.ContainsKey("StargateLocations"))
				{
					var gateArray = (Godot.Collections.Array)sysDataDict["StargateLocations"];
					foreach (var gateVar in gateArray)
					{
						var gateDict = (Godot.Collections.Dictionary)gateVar;
						newSys.StargateLocations.Add(new Vector2I((int)gateDict["Q"], (int)gateDict["R"]));
					}
				}
				
				var pArray = (Godot.Collections.Array)sysDataDict["Planets"];
				foreach (var pVar in pArray)
				{
					var pDict = (Godot.Collections.Dictionary)pVar;
					PlanetData newP = new PlanetData();
					
					newP.Name = (string)pDict["Name"];
					newP.TypeIndex = (int)pDict["TypeIndex"];
					newP.Scale = (float)pDict["Scale"];
					newP.Habitability = (string)pDict["Habitability"];
					
					newSys.Planets.Add(newP);
				}
				ExploredSystems[sysName] = newSys;
			}
		}

		GD.Print("Game successfully loaded from: " + _savePath);
		return true;
	}

	public void ResetForNewGame()
	{
		SavedSystem = "";
		SavedPlanet = "";
		SavedType = "";
		SelectedBasePlanetType = "";
		SelectedBasePlanetHexCoords = new Vector2(0, 0);
		SelectedPlayerFleet.Clear();
		ExploredSystems.Clear();
		CurrentSectorStars.Clear();
		CurrentTurn = 1;
		InCombat = false;
		CurrentQueueIndex = 0;
		JustJumped = false; 
		SavedFleetState.Clear();

		if (FileAccess.FileExists(_savePath))
		{
			DirAccess.RemoveAbsolute(_savePath);
			GD.Print("Old save file deleted.");
		}
		GD.Print("GlobalData has been completely wiped for a new campaign.");
	}
}
