using Godot;
using System.Collections.Generic;
using System.Linq;

public class Vector2ISaveData
{
	public int Q { get; set; }
	public int R { get; set; }

	public static Vector2ISaveData FromVector2I(Vector2I value)
	{
		return new Vector2ISaveData { Q = value.X, R = value.Y };
	}

	public Vector2I ToVector2I()
	{
		return new Vector2I(Q, R);
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		return new Godot.Collections.Dictionary<string, Variant> { { "Q", Q }, { "R", R } };
	}

	public static Vector2ISaveData FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new Vector2ISaveData
		{
			Q = dict.ContainsKey("Q") ? (int)dict["Q"] : 0,
			R = dict.ContainsKey("R") ? (int)dict["R"] : 0
		};
	}
}

public class ShipStateSaveData
{
	public string Name { get; set; } = string.Empty;
	public int Q { get; set; }
	public int R { get; set; }
	public int CurrentHP { get; set; }
	public int MaxHP { get; set; }
	public int CurrentShields { get; set; }
	public int MaxShields { get; set; }
	public int MaxActions { get; set; }
	public int CurrentActions { get; set; }
	public int CurrentInitiativeRoll { get; set; }

	public static ShipStateSaveData FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new ShipStateSaveData
		{
			Name = dict.ContainsKey("Name") ? (string)dict["Name"] : string.Empty,
			Q = dict.ContainsKey("Q") ? (int)dict["Q"] : 0,
			R = dict.ContainsKey("R") ? (int)dict["R"] : 0,
			CurrentHP = dict.ContainsKey("CurrentHP") ? (int)dict["CurrentHP"] : 0,
			MaxHP = dict.ContainsKey("MaxHP") ? (int)dict["MaxHP"] : 0,
			CurrentShields = dict.ContainsKey("CurrentShields") ? (int)dict["CurrentShields"] : 0,
			MaxShields = dict.ContainsKey("MaxShields") ? (int)dict["MaxShields"] : 0,
			MaxActions = dict.ContainsKey("MaxActions") ? (int)dict["MaxActions"] : 0,
			CurrentActions = dict.ContainsKey("CurrentActions") ? (int)dict["CurrentActions"] : 0,
			CurrentInitiativeRoll = dict.ContainsKey("CurrentInitiativeRoll") ? (int)dict["CurrentInitiativeRoll"] : 0
		};
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "Name", Name },
			{ "Q", Q },
			{ "R", R },
			{ "CurrentHP", CurrentHP },
			{ "MaxHP", MaxHP },
			{ "CurrentShields", CurrentShields },
			{ "MaxShields", MaxShields },
			{ "MaxActions", MaxActions },
			{ "CurrentActions", CurrentActions },
			{ "CurrentInitiativeRoll", CurrentInitiativeRoll }
		};
	}
}

public class FleetLoadoutSaveData
{
	public string WeaponID { get; set; } = string.Empty;
	public string ShieldID { get; set; } = string.Empty;
	public string ArmorID { get; set; } = string.Empty;

	public static FleetLoadoutSaveData FromRuntime(ShipLoadout loadout)
	{
		return new FleetLoadoutSaveData
		{
			WeaponID = loadout?.WeaponID ?? string.Empty,
			ShieldID = loadout?.ShieldID ?? string.Empty,
			ArmorID = loadout?.ArmorID ?? string.Empty
		};
	}

	public ShipLoadout ToRuntime()
	{
		return new ShipLoadout
		{
			WeaponID = WeaponID,
			ShieldID = ShieldID,
			ArmorID = ArmorID
		};
	}

	public static FleetLoadoutSaveData FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new FleetLoadoutSaveData
		{
			WeaponID = dict.ContainsKey("WeaponID") ? (string)dict["WeaponID"] : string.Empty,
			ShieldID = dict.ContainsKey("ShieldID") ? (string)dict["ShieldID"] : string.Empty,
			ArmorID = dict.ContainsKey("ArmorID") ? (string)dict["ArmorID"] : string.Empty
		};
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "WeaponID", WeaponID },
			{ "ShieldID", ShieldID },
			{ "ArmorID", ArmorID }
		};
	}
}

public class PlanetSaveData
{
	public string Name { get; set; } = string.Empty;
	public int TypeIndex { get; set; }
	public float Scale { get; set; }
	public string Habitability { get; set; } = string.Empty;
	public float Distance { get; set; }
	public float Speed { get; set; }
	public float StartingAngle { get; set; }
	public bool HasBeenScanned { get; set; }
	public bool HasBeenSalvaged { get; set; }

	public static PlanetSaveData FromRuntime(PlanetData planet)
	{
		return new PlanetSaveData
		{
			Name = planet.Name,
			TypeIndex = planet.TypeIndex,
			Scale = planet.Scale,
			Habitability = planet.Habitability,
			Distance = planet.Distance,
			Speed = planet.Speed,
			StartingAngle = planet.StartingAngle,
			HasBeenScanned = planet.HasBeenScanned,
			HasBeenSalvaged = planet.HasBeenSalvaged
		};
	}

	public PlanetData ToRuntime()
	{
		return new PlanetData
		{
			Name = Name,
			TypeIndex = TypeIndex,
			Scale = Scale,
			Habitability = Habitability,
			Distance = Distance,
			Speed = Speed,
			StartingAngle = StartingAngle,
			HasBeenScanned = HasBeenScanned,
			HasBeenSalvaged = HasBeenSalvaged
		};
	}

	public static PlanetSaveData FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new PlanetSaveData
		{
			Name = dict.ContainsKey("Name") ? (string)dict["Name"] : string.Empty,
			TypeIndex = dict.ContainsKey("TypeIndex") ? (int)dict["TypeIndex"] : 0,
			Scale = dict.ContainsKey("Scale") ? (float)dict["Scale"] : 0f,
			Habitability = dict.ContainsKey("Habitability") ? (string)dict["Habitability"] : string.Empty,
			Distance = dict.ContainsKey("Distance") ? (float)dict["Distance"] : 0f,
			Speed = dict.ContainsKey("Speed") ? (float)dict["Speed"] : 0f,
			StartingAngle = dict.ContainsKey("StartingAngle") ? (float)dict["StartingAngle"] : 0f,
			HasBeenScanned = dict.ContainsKey("HasBeenScanned") && (bool)dict["HasBeenScanned"],
			HasBeenSalvaged = dict.ContainsKey("HasBeenSalvaged") && (bool)dict["HasBeenSalvaged"]
		};
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "Name", Name },
			{ "TypeIndex", TypeIndex },
			{ "Scale", Scale },
			{ "Habitability", Habitability },
			{ "Distance", Distance },
			{ "Speed", Speed },
			{ "StartingAngle", StartingAngle },
			{ "HasBeenScanned", HasBeenScanned },
			{ "HasBeenSalvaged", HasBeenSalvaged }
		};
	}
}

public class OutpostSaveData
{
	public string Name { get; set; } = string.Empty;
	public Vector2ISaveData HexPosition { get; set; } = new Vector2ISaveData();
	public string SpritePath { get; set; } = string.Empty;

	public static OutpostSaveData FromRuntime(OutpostData outpost)
	{
		return new OutpostSaveData
		{
			Name = outpost.Name,
			HexPosition = Vector2ISaveData.FromVector2I(outpost.HexPosition),
			SpritePath = outpost.SpritePath
		};
	}

	public OutpostData ToRuntime()
	{
		return new OutpostData
		{
			Name = Name,
			HexPosition = HexPosition.ToVector2I(),
			SpritePath = SpritePath
		};
	}

	public static OutpostSaveData FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new OutpostSaveData
		{
			Name = dict.ContainsKey("Name") ? (string)dict["Name"] : string.Empty,
			HexPosition = Vector2ISaveData.FromVariantDictionary(new Godot.Collections.Dictionary
			{
				{ "Q", dict.ContainsKey("Q") ? dict["Q"] : 0 },
				{ "R", dict.ContainsKey("R") ? dict["R"] : 0 }
			}),
			SpritePath = dict.ContainsKey("SpritePath") ? (string)dict["SpritePath"] : string.Empty
		};
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "Name", Name },
			{ "Q", HexPosition.Q },
			{ "R", HexPosition.R },
			{ "SpritePath", SpritePath }
		};
	}
}

public class StarMapSaveData
{
	public string SystemName { get; set; } = string.Empty;
	public float MapPositionX { get; set; }
	public float MapPositionY { get; set; }
	public int PlanetCount { get; set; }
	public float StarScale { get; set; }
	public string StarColorHtml { get; set; } = "#ffffff";
	public string Region { get; set; } = string.Empty;

	public static StarMapSaveData FromRuntime(StarMapData star)
	{
		return new StarMapSaveData
		{
			SystemName = star.SystemName,
			MapPositionX = star.MapPosition.X,
			MapPositionY = star.MapPosition.Y,
			PlanetCount = star.PlanetCount,
			StarScale = star.StarScale,
			StarColorHtml = "#" + star.StarColor.ToHtml(false),
			Region = star.Region
		};
	}

	public StarMapData ToRuntime()
	{
		return new StarMapData
		{
			SystemName = SystemName,
			MapPosition = new Vector2(MapPositionX, MapPositionY),
			PlanetCount = PlanetCount,
			StarScale = StarScale,
			StarColor = Color.FromHtml(StarColorHtml),
			Region = Region
		};
	}

	public static StarMapSaveData FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new StarMapSaveData
		{
			SystemName = dict.ContainsKey("SystemName") ? (string)dict["SystemName"] : string.Empty,
			MapPositionX = dict.ContainsKey("MapPositionX") ? (float)dict["MapPositionX"] : 0f,
			MapPositionY = dict.ContainsKey("MapPositionY") ? (float)dict["MapPositionY"] : 0f,
			PlanetCount = dict.ContainsKey("PlanetCount") ? (int)dict["PlanetCount"] : 0,
			StarScale = dict.ContainsKey("StarScale") ? (float)dict["StarScale"] : 0f,
			StarColorHtml = dict.ContainsKey("StarColorHtml") ? (string)dict["StarColorHtml"] : "#ffffff",
			Region = dict.ContainsKey("Region") ? (string)dict["Region"] : string.Empty
		};
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "SystemName", SystemName },
			{ "MapPositionX", MapPositionX },
			{ "MapPositionY", MapPositionY },
			{ "PlanetCount", PlanetCount },
			{ "StarScale", StarScale },
			{ "StarColorHtml", StarColorHtml },
			{ "Region", Region }
		};
	}
}

public class SystemSaveData
{
	public string SystemName { get; set; } = string.Empty;
	public bool HasBeenVisited { get; set; }
	public List<ShipStateSaveData> EnemyFleets { get; set; } = new List<ShipStateSaveData>();
	public List<Vector2ISaveData> StargateHexes { get; set; } = new List<Vector2ISaveData>();
	public List<Vector2ISaveData> AsteroidHexes { get; set; } = new List<Vector2ISaveData>();
	public List<Vector2ISaveData> RadiationHexes { get; set; } = new List<Vector2ISaveData>();
	public List<Vector2ISaveData> ExploredHexes { get; set; } = new List<Vector2ISaveData>();
	public List<Vector2ISaveData> RadarRevealedHexes { get; set; } = new List<Vector2ISaveData>();
	public List<PlanetSaveData> Planets { get; set; } = new List<PlanetSaveData>();
	public List<OutpostSaveData> Outposts { get; set; } = new List<OutpostSaveData>();

	public static SystemSaveData FromRuntime(SystemData system)
	{
		return new SystemSaveData
		{
			SystemName = system.SystemName,
			HasBeenVisited = system.HasBeenVisited,
			EnemyFleets = CampaignSaveData.FromShipStateArray(system.EnemyFleets),
			StargateHexes = (system.StargateHexes ?? new List<Vector2I>()).Select(Vector2ISaveData.FromVector2I).ToList(),
			AsteroidHexes = (system.AsteroidHexes ?? new List<Vector2I>()).Select(Vector2ISaveData.FromVector2I).ToList(),
			RadiationHexes = (system.RadiationHexes ?? new List<Vector2I>()).Select(Vector2ISaveData.FromVector2I).ToList(),
			ExploredHexes = (system.ExploredHexes ?? new List<Vector2I>()).Select(Vector2ISaveData.FromVector2I).ToList(),
			RadarRevealedHexes = (system.RadarRevealedHexes ?? new List<Vector2I>()).Select(Vector2ISaveData.FromVector2I).ToList(),
			Planets = (system.Planets ?? new List<PlanetData>()).Select(PlanetSaveData.FromRuntime).ToList(),
			Outposts = (system.Outposts ?? new List<OutpostData>()).Select(OutpostSaveData.FromRuntime).ToList()
		};
	}

	public SystemData ToRuntime()
	{
		return new SystemData
		{
			SystemName = SystemName,
			HasBeenVisited = HasBeenVisited,
			EnemyFleets = CampaignSaveData.ToShipStateArray(EnemyFleets),
			StargateHexes = StargateHexes.Select(v => v.ToVector2I()).ToList(),
			AsteroidHexes = AsteroidHexes.Select(v => v.ToVector2I()).ToList(),
			RadiationHexes = RadiationHexes.Select(v => v.ToVector2I()).ToList(),
			ExploredHexes = ExploredHexes.Select(v => v.ToVector2I()).ToList(),
			RadarRevealedHexes = RadarRevealedHexes.Select(v => v.ToVector2I()).ToList(),
			Planets = Planets.Select(p => p.ToRuntime()).ToList(),
			Outposts = Outposts.Select(o => o.ToRuntime()).ToList()
		};
	}

	public static SystemSaveData FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new SystemSaveData
		{
			SystemName = dict.ContainsKey("SystemName") ? (string)dict["SystemName"] : string.Empty,
			HasBeenVisited = dict.ContainsKey("HasBeenVisited") && (bool)dict["HasBeenVisited"],
			EnemyFleets = CampaignSaveData.FromShipStateArray(dict.ContainsKey("EnemyFleets") ? (Godot.Collections.Array)dict["EnemyFleets"] : new Godot.Collections.Array()),
			StargateHexes = CampaignSaveData.FromVectorArray(dict.ContainsKey("StargateHexes") ? (Godot.Collections.Array)dict["StargateHexes"] : new Godot.Collections.Array()),
			AsteroidHexes = CampaignSaveData.FromVectorArray(dict.ContainsKey("AsteroidHexes") ? (Godot.Collections.Array)dict["AsteroidHexes"] : new Godot.Collections.Array()),
			RadiationHexes = CampaignSaveData.FromVectorArray(dict.ContainsKey("RadiationHexes") ? (Godot.Collections.Array)dict["RadiationHexes"] : new Godot.Collections.Array()),
			ExploredHexes = CampaignSaveData.FromVectorArray(dict.ContainsKey("ExploredHexes") ? (Godot.Collections.Array)dict["ExploredHexes"] : new Godot.Collections.Array()),
			RadarRevealedHexes = CampaignSaveData.FromVectorArray(dict.ContainsKey("RadarRevealedHexes") ? (Godot.Collections.Array)dict["RadarRevealedHexes"] : new Godot.Collections.Array()),
			Planets = CampaignSaveData.FromVariantObjectList(dict.ContainsKey("Planets") ? (Godot.Collections.Array)dict["Planets"] : new Godot.Collections.Array(), PlanetSaveData.FromVariantDictionary),
			Outposts = CampaignSaveData.FromVariantObjectList(dict.ContainsKey("Outposts") ? (Godot.Collections.Array)dict["Outposts"] : new Godot.Collections.Array(), OutpostSaveData.FromVariantDictionary)
		};
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "SystemName", SystemName },
			{ "HasBeenVisited", HasBeenVisited },
			{ "EnemyFleets", CampaignSaveData.ToShipStateArray(EnemyFleets) },
			{ "StargateHexes", CampaignSaveData.ToVariantArray(StargateHexes.Select(v => v.ToVariantDictionary())) },
			{ "AsteroidHexes", CampaignSaveData.ToVariantArray(AsteroidHexes.Select(v => v.ToVariantDictionary())) },
			{ "RadiationHexes", CampaignSaveData.ToVariantArray(RadiationHexes.Select(v => v.ToVariantDictionary())) },
			{ "ExploredHexes", CampaignSaveData.ToVariantArray(ExploredHexes.Select(v => v.ToVariantDictionary())) },
			{ "RadarRevealedHexes", CampaignSaveData.ToVariantArray(RadarRevealedHexes.Select(v => v.ToVariantDictionary())) },
			{ "Planets", CampaignSaveData.ToVariantArray(Planets.Select(p => p.ToVariantDictionary())) },
			{ "Outposts", CampaignSaveData.ToVariantArray(Outposts.Select(o => o.ToVariantDictionary())) }
		};
	}
}

public class CampaignSaveData
{
	public string SavedSystem { get; set; } = string.Empty;
	public string SavedPlanet { get; set; } = string.Empty;
	public int CurrentTurn { get; set; } = 1;
	public bool InCombat { get; set; }
	public int CurrentQueueIndex { get; set; }
	public bool JustJumped { get; set; }
	public List<ShipStateSaveData> SavedFleetState { get; set; } = new List<ShipStateSaveData>();
	public Dictionary<string, float> FleetResources { get; set; } = new Dictionary<string, float>();
	public List<string> UnequippedInventory { get; set; } = new List<string>();
	public Dictionary<string, FleetLoadoutSaveData> FleetLoadouts { get; set; } = new Dictionary<string, FleetLoadoutSaveData>();
	public List<string> SelectedPlayerFleet { get; set; } = new List<string>();
	public Dictionary<string, SystemSaveData> ExploredSystems { get; set; } = new Dictionary<string, SystemSaveData>();
	public List<StarMapSaveData> CurrentSectorStars { get; set; } = new List<StarMapSaveData>();

	public static CampaignSaveData FromRuntime(GlobalData globalData)
	{
		return new CampaignSaveData
		{
			SavedSystem = globalData.SavedSystem,
			SavedPlanet = globalData.SavedPlanet,
			CurrentTurn = globalData.CurrentTurn,
			InCombat = globalData.InCombat,
			CurrentQueueIndex = globalData.CurrentQueueIndex,
			JustJumped = globalData.JustJumped,
			SavedFleetState = FromShipStateArray(globalData.SavedFleetState ?? new Godot.Collections.Array()),
			FleetResources = (globalData.FleetResources ?? new Godot.Collections.Dictionary<string, Variant>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AsSingle()),
			UnequippedInventory = (globalData.UnequippedInventory ?? new List<string>()).ToList(),
			FleetLoadouts = (globalData.FleetLoadouts ?? new Dictionary<string, ShipLoadout>()).ToDictionary(kvp => kvp.Key, kvp => FleetLoadoutSaveData.FromRuntime(kvp.Value)),
			SelectedPlayerFleet = (globalData.SelectedPlayerFleet ?? new List<string>()).ToList(),
			ExploredSystems = (globalData.ExploredSystems ?? new Dictionary<string, SystemData>()).ToDictionary(kvp => kvp.Key, kvp => SystemSaveData.FromRuntime(kvp.Value)),
			CurrentSectorStars = (globalData.CurrentSectorStars ?? new List<StarMapData>()).Select(StarMapSaveData.FromRuntime).ToList()
		};
	}

	public void ApplyTo(GlobalData globalData)
	{
		globalData.SavedSystem = SavedSystem;
		globalData.SavedPlanet = SavedPlanet;
		globalData.CurrentTurn = CurrentTurn;
		globalData.InCombat = InCombat;
		globalData.CurrentQueueIndex = CurrentQueueIndex;
		globalData.JustJumped = JustJumped;
		globalData.SavedFleetState = ToShipStateArray(SavedFleetState);
		globalData.FleetResources = new Godot.Collections.Dictionary<string, Variant>();
		foreach (KeyValuePair<string, float> kvp in FleetResources)
		{
			globalData.FleetResources[kvp.Key] = kvp.Value;
		}

		globalData.UnequippedInventory = UnequippedInventory.ToList();
		globalData.FleetLoadouts = FleetLoadouts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToRuntime());
		globalData.SelectedPlayerFleet = SelectedPlayerFleet.ToList();
		globalData.ExploredSystems = ExploredSystems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToRuntime());
		globalData.CurrentSectorStars = CurrentSectorStars.Select(s => s.ToRuntime()).ToList();
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		var loadoutDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (KeyValuePair<string, FleetLoadoutSaveData> kvp in FleetLoadouts)
		{
			loadoutDict[kvp.Key] = kvp.Value.ToVariantDictionary();
		}

		var systemDict = new Godot.Collections.Dictionary<string, Variant>();
		foreach (KeyValuePair<string, SystemSaveData> kvp in ExploredSystems)
		{
			systemDict[kvp.Key] = kvp.Value.ToVariantDictionary();
		}

		var fleetResources = new Godot.Collections.Dictionary<string, Variant>();
		foreach (KeyValuePair<string, float> kvp in FleetResources)
		{
			fleetResources[kvp.Key] = kvp.Value;
		}

		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "SavedSystem", SavedSystem },
			{ "SavedPlanet", SavedPlanet },
			{ "CurrentTurn", CurrentTurn },
			{ "InCombat", InCombat },
			{ "CurrentQueueIndex", CurrentQueueIndex },
			{ "JustJumped", JustJumped },
			{ "SavedFleetState", ToShipStateArray(SavedFleetState) },
			{ "FleetResources", fleetResources },
			{ "UnequippedInventory", ToVariantArray(UnequippedInventory) },
			{ "FleetLoadouts", loadoutDict },
			{ "SelectedPlayerFleet", ToVariantArray(SelectedPlayerFleet) },
			{ "ExploredSystems", systemDict },
			{ "CurrentSectorStars", ToVariantArray(CurrentSectorStars.Select(s => s.ToVariantDictionary())) }
		};
	}

	public static CampaignSaveData FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new CampaignSaveData
		{
			SavedSystem = dict.ContainsKey("SavedSystem") ? (string)dict["SavedSystem"] : string.Empty,
			SavedPlanet = dict.ContainsKey("SavedPlanet") ? (string)dict["SavedPlanet"] : string.Empty,
			CurrentTurn = dict.ContainsKey("CurrentTurn") ? (int)dict["CurrentTurn"] : 1,
			InCombat = dict.ContainsKey("InCombat") && (bool)dict["InCombat"],
			CurrentQueueIndex = dict.ContainsKey("CurrentQueueIndex") ? (int)dict["CurrentQueueIndex"] : 0,
			JustJumped = dict.ContainsKey("JustJumped") && (bool)dict["JustJumped"],
			SavedFleetState = FromShipStateArray(dict.ContainsKey("SavedFleetState") ? (Godot.Collections.Array)dict["SavedFleetState"] : new Godot.Collections.Array()),
			FleetResources = FromResourceDictionary(dict.ContainsKey("FleetResources") ? (Godot.Collections.Dictionary)dict["FleetResources"] : new Godot.Collections.Dictionary()),
			UnequippedInventory = FromStringArray(dict.ContainsKey("UnequippedInventory") ? (Godot.Collections.Array)dict["UnequippedInventory"] : new Godot.Collections.Array()),
			FleetLoadouts = FromLoadoutDictionary(dict.ContainsKey("FleetLoadouts") ? (Godot.Collections.Dictionary)dict["FleetLoadouts"] : new Godot.Collections.Dictionary()),
			SelectedPlayerFleet = FromStringArray(dict.ContainsKey("SelectedPlayerFleet") ? (Godot.Collections.Array)dict["SelectedPlayerFleet"] : new Godot.Collections.Array()),
			ExploredSystems = FromSystemDictionary(dict.ContainsKey("ExploredSystems") ? (Godot.Collections.Dictionary)dict["ExploredSystems"] : new Godot.Collections.Dictionary()),
			CurrentSectorStars = FromVariantObjectList(dict.ContainsKey("CurrentSectorStars") ? (Godot.Collections.Array)dict["CurrentSectorStars"] : new Godot.Collections.Array(), StarMapSaveData.FromVariantDictionary)
		};
	}

	public static Godot.Collections.Array ToShipStateArray(IEnumerable<ShipStateSaveData> ships)
	{
		return ToVariantArray(ships.Select(s => s.ToVariantDictionary()));
	}

	public static List<ShipStateSaveData> FromShipStateArray(Godot.Collections.Array array)
	{
		return FromVariantObjectList(array, ShipStateSaveData.FromVariantDictionary);
	}

	public static Godot.Collections.Array ToVariantArray(IEnumerable<string> values)
	{
		var array = new Godot.Collections.Array();
		foreach (string value in values)
		{
			array.Add(value);
		}
		return array;
	}

	public static Godot.Collections.Array ToVariantArray(IEnumerable<Godot.Collections.Dictionary<string, Variant>> values)
	{
		var array = new Godot.Collections.Array();
		foreach (Godot.Collections.Dictionary<string, Variant> value in values)
		{
			array.Add(value);
		}
		return array;
	}

	public static List<Vector2ISaveData> FromVectorArray(Godot.Collections.Array array)
	{
		return FromVariantObjectList(array, Vector2ISaveData.FromVariantDictionary);
	}

	public static List<T> FromVariantObjectList<T>(Godot.Collections.Array array, System.Func<Godot.Collections.Dictionary, T> factory)
	{
		var list = new List<T>();
		foreach (Variant item in array)
		{
			list.Add(factory((Godot.Collections.Dictionary)item));
		}
		return list;
	}

	private static Dictionary<string, float> FromResourceDictionary(Godot.Collections.Dictionary dict)
	{
		var resources = new Dictionary<string, float>();
		foreach (Variant key in dict.Keys)
		{
			resources[(string)key] = ((Variant)dict[key]).AsSingle();
		}

		if (!resources.ContainsKey(GameConstants.ResourceKeys.RawMaterials)) resources[GameConstants.ResourceKeys.RawMaterials] = 350.0f;
		if (!resources.ContainsKey(GameConstants.ResourceKeys.EnergyCores)) resources[GameConstants.ResourceKeys.EnergyCores] = 5.0f;
		if (!resources.ContainsKey(GameConstants.ResourceKeys.AncientTech)) resources[GameConstants.ResourceKeys.AncientTech] = 0.0f;
		return resources;
	}

	private static Dictionary<string, FleetLoadoutSaveData> FromLoadoutDictionary(Godot.Collections.Dictionary dict)
	{
		var loadouts = new Dictionary<string, FleetLoadoutSaveData>();
		foreach (Variant key in dict.Keys)
		{
			loadouts[(string)key] = FleetLoadoutSaveData.FromVariantDictionary((Godot.Collections.Dictionary)dict[key]);
		}
		return loadouts;
	}

	private static Dictionary<string, SystemSaveData> FromSystemDictionary(Godot.Collections.Dictionary dict)
	{
		var systems = new Dictionary<string, SystemSaveData>();
		foreach (Variant key in dict.Keys)
		{
			systems[(string)key] = SystemSaveData.FromVariantDictionary((Godot.Collections.Dictionary)dict[key]);
		}
		return systems;
	}

	private static List<string> FromStringArray(Godot.Collections.Array array)
	{
		return array.Select(v => (string)v).ToList();
	}
}
