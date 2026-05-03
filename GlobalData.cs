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
	public bool IsBlackSiteRelaySite { get; set; } = false;
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

public class MissionRuntimeState
{
	public string MissionID { get; set; } = string.Empty;
	public string MissionTitle { get; set; } = string.Empty;
	public string ReturnScenePath { get; set; } = string.Empty;
	public string SourceSystem { get; set; } = string.Empty;
	public string SourceEncounterName { get; set; } = string.Empty;
	public List<string> ParticipatingShipNames { get; set; } = new List<string>();
	public List<string> ParticipatingOfficerIDs { get; set; } = new List<string>();
}

public class OfficerTemplate
{
	public string OfficerID { get; set; } = string.Empty;
	public string ShipName { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string PortraitPath { get; set; } = string.Empty;
	public string Biography { get; set; } = string.Empty;
	public string Ideology { get; set; } = string.Empty;
	public string Archetype { get; set; } = string.Empty;
	public string Specialty { get; set; } = string.Empty;
	public string Flaw { get; set; } = string.Empty;
	public int StartingApproval { get; set; } = 0;
	public string PersonalQuestID { get; set; } = string.Empty;
	public string CombatAbilityID { get; set; } = string.Empty;
}

public class OfficerState
{
	public string OfficerID { get; set; } = string.Empty;
	public string TemplateOfficerID { get; set; } = string.Empty;
	public string ShipName { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public bool IsCustom { get; set; } = false;
	public string PortraitPath { get; set; } = string.Empty;
	public string Biography { get; set; } = string.Empty;
	public string BiographySeed { get; set; } = string.Empty;
	public string Ideology { get; set; } = string.Empty;
	public string Archetype { get; set; } = string.Empty;
	public string Specialty { get; set; } = string.Empty;
	public string Flaw { get; set; } = string.Empty;
	public int Approval { get; set; } = 0;
	public int Stress { get; set; } = 0;
	public string CombatAbilityID { get; set; } = string.Empty;
	public string PersonalQuestID { get; set; } = string.Empty;
	public List<string> Flags { get; set; } = new List<string>();
	public List<string> CompletedScenes { get; set; } = new List<string>();
}

// --- NEW: EQUIPMENT AND LOADOUT CLASSES ---
public class EquipmentData
{
	public string ItemID { get; set; }
	public string Name { get; set; }
	public string Category { get; set; } // "Weapon", "Shield", "Armor", or "Missile"
	public int BonusStat { get; set; } // The amount of extra Dmg, HP, or Shields it gives
	public float CostTech { get; set; }
	public float CostRaw { get; set; }
	public string Description { get; set; }
	public int MissileDamage { get; set; } = 0;
	public int MissileRange { get; set; } = 0;
	public string MissileAbility { get; set; } = string.Empty;
}

public class ShipLoadout
{
	public string WeaponID { get; set; } = "";
	public string ShieldID { get; set; } = "";
	public string ArmorID { get; set; } = "";
	public string MissileID { get; set; } = "";
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
	public int SelectedFleetCapacity { get; set; } = 0;

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
	public Dictionary<string, OfficerState> ShipOfficers { get; set; } = new Dictionary<string, OfficerState>();
	public List<string> PendingDowntimeEvents { get; set; } = new List<string>();
	public string CurrentMissionID { get; set; } = string.Empty;
	public string CurrentMissionTitle { get; set; } = string.Empty;
	public string MissionReturnScenePath { get; set; } = string.Empty;
	public string MissionSourceEncounterName { get; set; } = string.Empty;
	public List<string> SelectedMissionOfficerShipNames { get; set; } = new List<string>();
	public List<string> SelectedMissionOfficerIDs { get; set; } = new List<string>();
	public List<string> CompletedMissionIDs { get; set; } = new List<string>();
	public Dictionary<string, string> MissionOutcomes { get; set; } = new Dictionary<string, string>();
	public List<string> StoryFlags { get; set; } = new List<string>();

	public List<QuestData> ActiveQuests { get; set; } = new List<QuestData>();
	public Godot.Collections.Array CompletedQuestIDs { get; set; } = new Godot.Collections.Array();

	private readonly SaveGameService _saveGameService = new SaveGameService();

	// --- MASTER EQUIPMENT DATABASE ---
	// Loaded from res://Data/equipment_catalog.json at startup.
	public Dictionary<string, EquipmentData> MasterEquipmentDB { get; set; } = new Dictionary<string, EquipmentData>();
	public Dictionary<string, OfficerTemplate> MasterOfficerDB { get; set; } = new Dictionary<string, OfficerTemplate>();

	public override void _Ready()
	{
		MasterEquipmentDB = EquipmentCatalogLoader.LoadCatalog();
		MasterOfficerDB = OfficerCatalogLoader.LoadCatalog();
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

	public MissionRuntimeState GetCurrentMissionState()
	{
		return new MissionRuntimeState
		{
			MissionID = CurrentMissionID,
			MissionTitle = CurrentMissionTitle,
			ReturnScenePath = MissionReturnScenePath,
			SourceSystem = SavedSystem,
			SourceEncounterName = MissionSourceEncounterName,
			ParticipatingShipNames = new List<string>(SelectedMissionOfficerShipNames ?? new List<string>()),
			ParticipatingOfficerIDs = new List<string>(SelectedMissionOfficerIDs ?? new List<string>())
		};
	}

	public void SetCurrentMissionState(MissionRuntimeState state)
	{
		CurrentMissionID = state?.MissionID ?? string.Empty;
		CurrentMissionTitle = state?.MissionTitle ?? string.Empty;
		MissionReturnScenePath = state?.ReturnScenePath ?? string.Empty;
		MissionSourceEncounterName = state?.SourceEncounterName ?? string.Empty;
		SelectedMissionOfficerShipNames = state?.ParticipatingShipNames != null ? new List<string>(state.ParticipatingShipNames) : new List<string>();
		SelectedMissionOfficerIDs = state?.ParticipatingOfficerIDs != null ? new List<string>(state.ParticipatingOfficerIDs) : new List<string>();
	}

	public void ClearCurrentMissionState()
	{
		CurrentMissionID = string.Empty;
		CurrentMissionTitle = string.Empty;
		MissionReturnScenePath = string.Empty;
		MissionSourceEncounterName = string.Empty;
		SelectedMissionOfficerShipNames.Clear();
		SelectedMissionOfficerIDs.Clear();
	}

	public void ResetForNewGame()
	{
		SavedSystem = ""; SavedPlanet = ""; SavedType = ""; SelectedBasePlanetType = "";
		SelectedBasePlanetHexCoords = Vector2.Zero; SelectedPlayerFleet.Clear(); SelectedFleetCapacity = 0;
		ExploredSystems.Clear(); CurrentSectorStars.Clear();
		CurrentTurn = 1; InCombat = false; CurrentQueueIndex = 0; JustJumped = false; 
		SavedFleetState.Clear(); UnequippedInventory.Clear(); FleetLoadouts.Clear(); ShipOfficers.Clear(); PendingDowntimeEvents.Clear();
		ClearCurrentMissionState(); CompletedMissionIDs.Clear(); MissionOutcomes.Clear(); StoryFlags.Clear();
		
		FleetResources = new Godot.Collections.Dictionary<string, Variant> {
			{ GameConstants.ResourceKeys.RawMaterials, 350.0f },
			{ GameConstants.ResourceKeys.EnergyCores, 5.0f },
			{ GameConstants.ResourceKeys.AncientTech, 0.0f }
		};

		_saveGameService.DeleteSave();
		GD.Print("GlobalData has been completely wiped for a new campaign.");
	}
}
