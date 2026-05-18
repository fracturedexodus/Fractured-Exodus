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
	public string MissionInteractionKey { get; set; } = string.Empty;
}

public class OutpostData
{
	public string Name { get; set; }
	public Vector2I HexPosition { get; set; }
	public string SpritePath { get; set; }
	public string MissionInteractionKey { get; set; } = string.Empty;
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
	public string ScenePath { get; set; } = string.Empty;
	public string TemplateResourcePath { get; set; } = string.Empty;
	public string ReturnScenePath { get; set; } = string.Empty;
	public string SourceSystem { get; set; } = string.Empty;
	public string SourceEncounterName { get; set; } = string.Empty;
	public string SourceNodeType { get; set; } = string.Empty;
	public string SourceNodeID { get; set; } = string.Empty;
	public string SourceInteractionKey { get; set; } = string.Empty;
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
	public List<string> PersonalInventoryItemIDs { get; set; } = new List<string>();
	public string EquippedMissionWeaponId { get; set; } = string.Empty;
	public string EquippedMissionShieldId { get; set; } = string.Empty;
	public List<string> OwnedMissionWeaponIds { get; set; } = new List<string>();
	public List<string> OwnedMissionShieldIds { get; set; } = new List<string>();
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
		{ GameConstants.ResourceKeys.AncientTech, 0.0f },
		{ GameConstants.ResourceKeys.Population, 0.0f }
	};
	
	// --- NEW: INVENTORY MEMORY ---
	// Stores ItemIDs of gear you own but haven't equipped yet
	public List<string> UnequippedInventory { get; set; } = new List<string>(); 
	public List<string> FleetCargoItemIDs { get; set; } = new List<string>();
	public List<string> UnlockedCodexEntryIDs { get; set; } = new List<string>();
	
	// Maps a Ship's Name to its specific loadout
	public Dictionary<string, ShipLoadout> FleetLoadouts { get; set; } = new Dictionary<string, ShipLoadout>();
	public Dictionary<string, OfficerState> ShipOfficers { get; set; } = new Dictionary<string, OfficerState>();
	public List<string> PendingDowntimeEvents { get; set; } = new List<string>();
	public List<string> PendingOfficerReplacementShipNames { get; set; } = new List<string>();
	public string SaveDisplayName { get; set; } = string.Empty;
	public string SavedAtUtc { get; set; } = string.Empty;
	public string LastSavedScenePath { get; set; } = string.Empty;
	public string CurrentMissionID { get; set; } = string.Empty;
	public string CurrentMissionTitle { get; set; } = string.Empty;
	public string CurrentMissionScenePath { get; set; } = string.Empty;
	public string CurrentMissionTemplatePath { get; set; } = string.Empty;
	public string MissionReturnScenePath { get; set; } = string.Empty;
	public string MissionSourceEncounterName { get; set; } = string.Empty;
	public string MissionSourceNodeType { get; set; } = string.Empty;
	public string MissionSourceNodeID { get; set; } = string.Empty;
	public string MissionSourceInteractionKey { get; set; } = string.Empty;
	public List<string> SelectedMissionOfficerShipNames { get; set; } = new List<string>();
	public List<string> SelectedMissionOfficerIDs { get; set; } = new List<string>();
	public MissionRuntimeSaveData CurrentMissionSaveState { get; set; }
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

	public void SaveGame(bool autoSave = false, string currentScenePath = "")
	{
		if (!string.IsNullOrWhiteSpace(currentScenePath))
		{
			LastSavedScenePath = currentScenePath;
		}

		_saveGameService.Save(this, autoSave);
	}

	public void SaveNamedGame(string saveName, string currentScenePath)
	{
		if (!string.IsNullOrWhiteSpace(currentScenePath))
		{
			LastSavedScenePath = currentScenePath;
		}

		_saveGameService.SaveNamed(this, saveName);
	}

	public bool LoadGame(string slotId = "")
	{
		return _saveGameService.Load(this, slotId);
	}

	public List<SaveGameSlotInfo> GetAvailableSaveGames()
	{
		return _saveGameService.GetAvailableSaves();
	}

	public bool DeleteSaveGame(string slotId)
	{
		return _saveGameService.DeleteSlot(slotId);
	}

	public MissionRuntimeState GetCurrentMissionState()
	{
		return new MissionRuntimeState
		{
			MissionID = CurrentMissionID,
			MissionTitle = CurrentMissionTitle,
			ScenePath = CurrentMissionScenePath,
			TemplateResourcePath = CurrentMissionTemplatePath,
			ReturnScenePath = MissionReturnScenePath,
			SourceSystem = SavedSystem,
			SourceEncounterName = MissionSourceEncounterName,
			SourceNodeType = MissionSourceNodeType,
			SourceNodeID = MissionSourceNodeID,
			SourceInteractionKey = MissionSourceInteractionKey,
			ParticipatingShipNames = new List<string>(SelectedMissionOfficerShipNames ?? new List<string>()),
			ParticipatingOfficerIDs = new List<string>(SelectedMissionOfficerIDs ?? new List<string>())
		};
	}

	public void SetCurrentMissionState(MissionRuntimeState state)
	{
		CurrentMissionID = state?.MissionID ?? string.Empty;
		CurrentMissionTitle = state?.MissionTitle ?? string.Empty;
		CurrentMissionScenePath = state?.ScenePath ?? string.Empty;
		CurrentMissionTemplatePath = state?.TemplateResourcePath ?? string.Empty;
		MissionReturnScenePath = state?.ReturnScenePath ?? string.Empty;
		MissionSourceEncounterName = state?.SourceEncounterName ?? string.Empty;
		MissionSourceNodeType = state?.SourceNodeType ?? string.Empty;
		MissionSourceNodeID = state?.SourceNodeID ?? string.Empty;
		MissionSourceInteractionKey = state?.SourceInteractionKey ?? string.Empty;
		SelectedMissionOfficerShipNames = state?.ParticipatingShipNames != null ? new List<string>(state.ParticipatingShipNames) : new List<string>();
		SelectedMissionOfficerIDs = state?.ParticipatingOfficerIDs != null ? new List<string>(state.ParticipatingOfficerIDs) : new List<string>();
		CurrentMissionSaveState = null;
	}

	public void ClearCurrentMissionState()
	{
		CurrentMissionID = string.Empty;
		CurrentMissionTitle = string.Empty;
		CurrentMissionScenePath = string.Empty;
		CurrentMissionTemplatePath = string.Empty;
		MissionReturnScenePath = string.Empty;
		MissionSourceEncounterName = string.Empty;
		MissionSourceNodeType = string.Empty;
		MissionSourceNodeID = string.Empty;
		MissionSourceInteractionKey = string.Empty;
		SelectedMissionOfficerShipNames.Clear();
		SelectedMissionOfficerIDs.Clear();
		CurrentMissionSaveState = null;
	}

	public void ResetForNewGame()
	{
		SavedSystem = ""; SavedPlanet = ""; SavedType = ""; SelectedBasePlanetType = "";
		SelectedBasePlanetHexCoords = Vector2.Zero; SelectedPlayerFleet.Clear(); SelectedFleetCapacity = 0;
		ExploredSystems.Clear(); CurrentSectorStars.Clear();
		CurrentTurn = 1; InCombat = false; CurrentQueueIndex = 0; JustJumped = false; 
		SavedFleetState.Clear(); UnequippedInventory.Clear(); FleetCargoItemIDs.Clear(); UnlockedCodexEntryIDs.Clear(); FleetLoadouts.Clear(); ShipOfficers.Clear(); PendingDowntimeEvents.Clear(); PendingOfficerReplacementShipNames.Clear();
		SaveDisplayName = string.Empty; SavedAtUtc = string.Empty; LastSavedScenePath = string.Empty;
		ClearCurrentMissionState(); CompletedMissionIDs.Clear(); MissionOutcomes.Clear(); StoryFlags.Clear();
		
		FleetResources = new Godot.Collections.Dictionary<string, Variant> {
			{ GameConstants.ResourceKeys.RawMaterials, 350.0f },
			{ GameConstants.ResourceKeys.EnergyCores, 5.0f },
			{ GameConstants.ResourceKeys.AncientTech, 0.0f },
			{ GameConstants.ResourceKeys.Population, 0.0f }
		};

		_saveGameService.DeleteSave();
		GD.Print("GlobalData has been completely wiped for a new campaign.");
	}
}
