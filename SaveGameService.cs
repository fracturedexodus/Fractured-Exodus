using Godot;
using System.Collections.Generic;
using System.Linq;

public class SaveGameService
{
	private readonly string _savePath;

	public SaveGameService(string savePath = "user://savegame.json")
	{
		_savePath = savePath;
	}

	public void Save(GlobalData globalData)
	{
		var saveData = new Godot.Collections.Dictionary<string, Variant>();

		saveData["SavedSystem"] = globalData.SavedSystem;
		saveData["SavedPlanet"] = globalData.SavedPlanet;
		saveData["CurrentTurn"] = globalData.CurrentTurn;
		saveData["InCombat"] = globalData.InCombat;
		saveData["CurrentQueueIndex"] = globalData.CurrentQueueIndex;
		saveData["SavedFleetState"] = globalData.SavedFleetState;
		saveData["FleetResources"] = globalData.FleetResources;

		var invArray = new Godot.Collections.Array();
		foreach (string item in globalData.UnequippedInventory) invArray.Add(item);
		saveData["UnequippedInventory"] = invArray;

		var loadoutDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (KeyValuePair<string, ShipLoadout> kvp in globalData.FleetLoadouts)
		{
			var shipDict = new Godot.Collections.Dictionary<string, string>();
			shipDict["WeaponID"] = kvp.Value.WeaponID;
			shipDict["ShieldID"] = kvp.Value.ShieldID;
			shipDict["ArmorID"] = kvp.Value.ArmorID;
			loadoutDict[kvp.Key] = shipDict;
		}
		saveData["FleetLoadouts"] = loadoutDict;

		var fleetArray = new Godot.Collections.Array();
		if (globalData.SelectedPlayerFleet != null)
		{
			foreach (string ship in globalData.SelectedPlayerFleet) fleetArray.Add(ship);
		}
		saveData["SelectedPlayerFleet"] = fleetArray;

		var exploredDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (KeyValuePair<string, SystemData> sysKvp in globalData.ExploredSystems)
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

			var planetArray = new Godot.Collections.Array<Variant>();
			foreach (PlanetData planet in sysKvp.Value.Planets)
			{
				var planetDict = new Godot.Collections.Dictionary<string, Variant>();
				planetDict["Name"] = planet.Name;
				planetDict["TypeIndex"] = planet.TypeIndex;
				planetDict["Scale"] = planet.Scale;
				planetDict["Habitability"] = planet.Habitability;
				planetDict["Distance"] = planet.Distance;
				planetDict["Speed"] = planet.Speed;
				planetDict["StartingAngle"] = planet.StartingAngle;
				planetDict["HasBeenScanned"] = planet.HasBeenScanned;
				planetDict["HasBeenSalvaged"] = planet.HasBeenSalvaged;
				planetArray.Add(planetDict);
			}
			sysData["Planets"] = planetArray;

			var outpostArray = new Godot.Collections.Array<Variant>();
			if (sysKvp.Value.Outposts != null)
			{
				foreach (OutpostData outpost in sysKvp.Value.Outposts)
				{
					var outpostDict = new Godot.Collections.Dictionary<string, Variant>();
					outpostDict["Name"] = outpost.Name;
					outpostDict["Q"] = outpost.HexPosition.X;
					outpostDict["R"] = outpost.HexPosition.Y;
					outpostDict["SpritePath"] = outpost.SpritePath;
					outpostArray.Add(outpostDict);
				}
			}
			sysData["Outposts"] = outpostArray;

			exploredDict[sysKvp.Key] = sysData;
		}
		saveData["ExploredSystems"] = exploredDict;

		string jsonString = Json.Stringify(saveData);
		using FileAccess file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Write);
		if (file != null)
		{
			file.StoreString(jsonString);
			GD.Print("Game successfully saved to: " + _savePath);
		}
	}

	public bool Load(GlobalData globalData)
	{
		if (!FileAccess.FileExists(_savePath)) return false;

		using FileAccess file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Read);
		var json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok) return false;

		var savedData = (Godot.Collections.Dictionary)json.Data;

		if (savedData.ContainsKey("SavedSystem")) globalData.SavedSystem = (string)savedData["SavedSystem"];
		if (savedData.ContainsKey("SavedPlanet")) globalData.SavedPlanet = (string)savedData["SavedPlanet"];
		if (savedData.ContainsKey("CurrentTurn")) globalData.CurrentTurn = (int)savedData["CurrentTurn"];
		if (savedData.ContainsKey("InCombat")) globalData.InCombat = (bool)savedData["InCombat"];
		if (savedData.ContainsKey("CurrentQueueIndex")) globalData.CurrentQueueIndex = (int)savedData["CurrentQueueIndex"];
		if (savedData.ContainsKey("SavedFleetState")) globalData.SavedFleetState = (Godot.Collections.Array)savedData["SavedFleetState"];
		if (savedData.ContainsKey("FleetResources")) globalData.FleetResources = (Godot.Collections.Dictionary<string, Variant>)savedData["FleetResources"];

		globalData.UnequippedInventory.Clear();
		if (savedData.ContainsKey("UnequippedInventory"))
		{
			var invArray = (Godot.Collections.Array)savedData["UnequippedInventory"];
			foreach (Variant item in invArray) globalData.UnequippedInventory.Add((string)item);
		}

		globalData.FleetLoadouts.Clear();
		if (savedData.ContainsKey("FleetLoadouts"))
		{
			var loadoutDict = (Godot.Collections.Dictionary)savedData["FleetLoadouts"];
			foreach (Variant key in loadoutDict.Keys)
			{
				var shipDict = (Godot.Collections.Dictionary)loadoutDict[key];
				globalData.FleetLoadouts[(string)key] = new ShipLoadout
				{
					WeaponID = (string)shipDict["WeaponID"],
					ShieldID = (string)shipDict["ShieldID"],
					ArmorID = (string)shipDict["ArmorID"]
				};
			}
		}

		if (savedData.ContainsKey("SelectedPlayerFleet"))
		{
			globalData.SelectedPlayerFleet = ((Godot.Collections.Array)savedData["SelectedPlayerFleet"]).Select(v => (string)v).ToList();
		}

		if (savedData.ContainsKey("ExploredSystems"))
		{
			var exploredDict = (Godot.Collections.Dictionary)savedData["ExploredSystems"];
			globalData.ExploredSystems.Clear();

			foreach (Variant key in exploredDict.Keys)
			{
				string sysName = (string)key;
				var sysDict = (Godot.Collections.Dictionary)exploredDict[key];

				SystemData newSystem = new SystemData { SystemName = (string)sysDict["SystemName"] };
				newSystem.HasBeenVisited = sysDict.ContainsKey("HasBeenVisited") ? (bool)sysDict["HasBeenVisited"] : false;
				newSystem.EnemyFleets = sysDict.ContainsKey("EnemyFleets") ? (Godot.Collections.Array)sysDict["EnemyFleets"] : new Godot.Collections.Array();
				newSystem.StargateHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("StargateHexes") ? (Godot.Collections.Array)sysDict["StargateHexes"] : new Godot.Collections.Array());
				newSystem.AsteroidHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("AsteroidHexes") ? (Godot.Collections.Array)sysDict["AsteroidHexes"] : new Godot.Collections.Array());
				newSystem.RadiationHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("RadiationHexes") ? (Godot.Collections.Array)sysDict["RadiationHexes"] : new Godot.Collections.Array());
				newSystem.ExploredHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("ExploredHexes") ? (Godot.Collections.Array)sysDict["ExploredHexes"] : new Godot.Collections.Array());
				newSystem.RadarRevealedHexes = ConvertVariantArrayToVectorList(sysDict.ContainsKey("RadarRevealedHexes") ? (Godot.Collections.Array)sysDict["RadarRevealedHexes"] : new Godot.Collections.Array());

				var planetArray = (Godot.Collections.Array)sysDict["Planets"];
				foreach (Variant planetVariant in planetArray)
				{
					var planetDict = (Godot.Collections.Dictionary)planetVariant;
					newSystem.Planets.Add(new PlanetData
					{
						Name = (string)planetDict["Name"],
						TypeIndex = (int)planetDict["TypeIndex"],
						Scale = (float)planetDict["Scale"],
						Habitability = (string)planetDict["Habitability"],
						Distance = planetDict.ContainsKey("Distance") ? (float)planetDict["Distance"] : 0f,
						Speed = planetDict.ContainsKey("Speed") ? (float)planetDict["Speed"] : 0f,
						StartingAngle = planetDict.ContainsKey("StartingAngle") ? (float)planetDict["StartingAngle"] : 0f,
						HasBeenScanned = planetDict.ContainsKey("HasBeenScanned") ? (bool)planetDict["HasBeenScanned"] : false,
						HasBeenSalvaged = planetDict.ContainsKey("HasBeenSalvaged") ? (bool)planetDict["HasBeenSalvaged"] : false
					});
				}

				if (sysDict.ContainsKey("Outposts"))
				{
					var outpostArray = (Godot.Collections.Array)sysDict["Outposts"];
					foreach (Variant outpostVariant in outpostArray)
					{
						var outpostDict = (Godot.Collections.Dictionary)outpostVariant;
						newSystem.Outposts.Add(new OutpostData
						{
							Name = (string)outpostDict["Name"],
							HexPosition = new Vector2I((int)outpostDict["Q"], (int)outpostDict["R"]),
							SpritePath = (string)outpostDict["SpritePath"]
						});
					}
				}

				globalData.ExploredSystems[sysName] = newSystem;
			}
		}

		return true;
	}

	public void DeleteSave()
	{
		if (FileAccess.FileExists(_savePath)) DirAccess.RemoveAbsolute(_savePath);
	}

	private Godot.Collections.Array ConvertVectorListToVariantArray(List<Vector2I> list)
	{
		var array = new Godot.Collections.Array();
		foreach (Vector2I vec in list)
		{
			var dict = new Godot.Collections.Dictionary<string, int> { { "Q", vec.X }, { "R", vec.Y } };
			array.Add(dict);
		}
		return array;
	}

	private List<Vector2I> ConvertVariantArrayToVectorList(Godot.Collections.Array array)
	{
		var list = new List<Vector2I>();
		foreach (Variant item in array)
		{
			var dict = (Godot.Collections.Dictionary)item;
			list.Add(new Vector2I((int)dict["Q"], (int)dict["R"]));
		}
		return list;
	}
}
