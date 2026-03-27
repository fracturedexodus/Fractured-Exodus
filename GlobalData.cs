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
	// This array will hold dictionaries containing the X/Y coordinates and HP/Shield stats of every player ship
	public Godot.Collections.Array SavedFleetState { get; set; } = new Godot.Collections.Array();
	
	// --- NEW: Enemy Fleet Memory ---
	public Godot.Collections.Array SavedEnemyFleetState { get; set; } = new Godot.Collections.Array();

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
		
		// Save the Turn Number and the exact Ship Grid States
		saveData["CurrentTurn"] = CurrentTurn;
		saveData["SavedFleetState"] = SavedFleetState;
		saveData["SavedEnemyFleetState"] = SavedEnemyFleetState; // Save Enemies!
		
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
		
		// Unpack the exact Battle State!
		if (savedData.ContainsKey("CurrentTurn")) CurrentTurn = (int)savedData["CurrentTurn"];
		if (savedData.ContainsKey("SavedFleetState")) SavedFleetState = (Godot.Collections.Array)savedData["SavedFleetState"];
		if (savedData.ContainsKey("SavedEnemyFleetState")) SavedEnemyFleetState = (Godot.Collections.Array)savedData["SavedEnemyFleetState"]; // Load Enemies!
		
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

	// ==========================================
	// RESET SYSTEM
	// ==========================================
	public void ResetForNewGame()
	{
		// 1. Clear all current memory variables
		SavedSystem = "";
		SavedPlanet = "";
		SavedType = "";
		SelectedBasePlanetType = "";
		SelectedBasePlanetHexCoords = new Vector2(0, 0);
		SelectedPlayerFleet.Clear();
		ExploredSystems.Clear();
		CurrentSectorStars.Clear();
		CurrentTurn = 1;
		SavedFleetState.Clear();
		SavedEnemyFleetState.Clear(); // Reset Enemies!

		// 2. Delete the physical save file from the hard drive!
		if (FileAccess.FileExists(_savePath))
		{
			// Godot 4 uses DirAccess to delete files
			DirAccess.RemoveAbsolute(_savePath);
			GD.Print("Old save file deleted.");
		}

		GD.Print("GlobalData has been completely wiped for a new campaign.");
	}
}
