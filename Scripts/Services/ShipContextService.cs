using Godot;
using System.Collections.Generic;

public class MissionInteractionContext
{
	public string InteractionKey { get; set; } = string.Empty;
	public string SourceNodeType { get; set; } = string.Empty;
	public string SourceNodeID { get; set; } = string.Empty;
	public string SourceEncounterName { get; set; } = string.Empty;
	public MissionDefinition Definition { get; set; }
	public MapEntity SourceEntity { get; set; }
}

public class AmbientEventInteractionContext
{
	public string InstanceId { get; set; } = string.Empty;
	public AmbientEventDefinition Definition { get; set; }
	public AmbientEventInstanceData Instance { get; set; }
	public MapEntity SourceEntity { get; set; }
}

public class ShipMenuState
{
	public string Title { get; set; }
	public string ImagePath { get; set; }
	public float HpPercent { get; set; }
	public string DetailsText { get; set; }
	public bool IsPlayerShip { get; set; }
	public bool CanRepair { get; set; }
	public bool ShowLongRange { get; set; }
	public bool DisableLongRange { get; set; }
	public bool ShowEquip { get; set; }
	public bool ShowTrade { get; set; }
	public bool ShowScan { get; set; }
	public bool DisableScan { get; set; }
	public string ScanText { get; set; }
	public bool ShowSalvage { get; set; }
	public bool DisableSalvage { get; set; }
	public string SalvageText { get; set; }
	public bool ShowMission { get; set; }
	public string MissionText { get; set; }
	public string MissionInteractionKey { get; set; }
	public bool ShowAmbientEvent { get; set; }
	public string AmbientEventText { get; set; }
}

public class ShipContextService
{
	private readonly GlobalData _globalData;
	private MissionManager _missionManager;

	public ShipContextService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public ShipMenuState BuildMenuState(MapEntity ship, bool inCombat, Dictionary<Vector2I, MapEntity> hexContents)
	{
		if (ship == null)
		{
			return null;
		}

		bool isPlayer = ship.Type == GameConstants.EntityTypes.PlayerFleet;
		PlanetData adjacentPlanetData = GetAdjacentPlanetData(ship, hexContents);
		MissionInteractionContext missionContext = GetAdjacentMissionContext(ship, hexContents);
		AmbientEventInteractionContext ambientEventContext = GetAdjacentAmbientEventContext(ship, hexContents);
		bool hasAdjacentOutpost = HasAdjacentOutpost(ship, hexContents);

		return new ShipMenuState
		{
			Title = $"== {ship.Name.ToUpper()} ==",
			ImagePath = Database.GetShipTexturePath(ship.Name),
			HpPercent = ship.MaxHP > 0 ? (float)ship.CurrentHP / ship.MaxHP : 0f,
			DetailsText = $"Classification: {ship.Type}\nAction Points: {ship.CurrentActions}/{ship.MaxActions}\nWeapon Payload: 0-{ship.AttackDamage} Dmg\nTargeting Range: {ship.AttackRange} Hexes\n",
			IsPlayerShip = isPlayer,
			CanRepair = ship.CurrentActions >= 2,
			ShowLongRange = isPlayer && !inCombat && ship.Name == "The Aether Skimmer",
			DisableLongRange = _globalData == null || _globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() < 5f,
			ShowEquip = isPlayer && !inCombat,
			ShowTrade = isPlayer && !inCombat && hasAdjacentOutpost,
			ShowScan = isPlayer && !inCombat && adjacentPlanetData != null && (ship.Name == "The Aether Skimmer" || ship.Name == "The Relic Harvester"),
			DisableScan = adjacentPlanetData == null || adjacentPlanetData.HasBeenScanned || ship.CurrentActions < 1,
			ScanText = adjacentPlanetData != null && adjacentPlanetData.HasBeenScanned ? "SCANNED" : "SCAN",
			ShowSalvage = isPlayer && !inCombat && adjacentPlanetData != null && (ship.Name == "The Relic Harvester" || ship.Name == "The Neptune Forge"),
			DisableSalvage = adjacentPlanetData == null || adjacentPlanetData.HasBeenSalvaged || ship.CurrentActions < 1,
			SalvageText = adjacentPlanetData != null && adjacentPlanetData.HasBeenSalvaged ? "SALVAGED" : "SALVAGE",
			ShowMission = isPlayer && !inCombat && missionContext != null,
			MissionText = missionContext?.Definition != null ? missionContext.Definition.Title.ToUpper() : "MISSION",
			MissionInteractionKey = missionContext?.InteractionKey ?? string.Empty,
			ShowAmbientEvent = isPlayer && !inCombat && ambientEventContext != null,
			AmbientEventText = ambientEventContext?.Definition != null
				? $"ANSWER {ambientEventContext.Definition.MapDisplayName.ToUpper()}"
				: "ANSWER SIGNAL"
		};
	}

	public AmbientEventInteractionContext GetAdjacentAmbientEventContext(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		MapEntity entity = GetAdjacentEntityOfType(ship, hexContents, GameConstants.EntityTypes.AmbientEvent);
		if (entity == null || string.IsNullOrWhiteSpace(entity.AmbientEventInstanceId))
		{
			return null;
		}

		AmbientEventInstanceData instance = GetAmbientEventInstance(entity.AmbientEventInstanceId);
		AmbientEventDefinition definition = AmbientEventRegistry.GetEvent(entity.AmbientEventId);
		if (instance == null || instance.IsResolved || definition == null)
		{
			return null;
		}

		return new AmbientEventInteractionContext
		{
			InstanceId = instance.InstanceId,
			Definition = definition,
			Instance = instance,
			SourceEntity = entity
		};
	}

	public MissionInteractionContext GetAdjacentMissionContext(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		MapEntity entity = GetAdjacentMissionEntity(ship, hexContents);
		if (entity == null || string.IsNullOrWhiteSpace(entity.MissionInteractionKey))
		{
			return null;
		}

		MissionManager missionManager = ResolveMissionManager();
		if (missionManager == null || !missionManager.TryGetTemplateForInteraction(entity.MissionInteractionKey, out MissionTemplate template))
		{
			return null;
		}

		if (missionManager.HasCompletedMission(template.MissionId))
		{
			return null;
		}

		return new MissionInteractionContext
		{
			InteractionKey = entity.MissionInteractionKey,
			SourceNodeType = entity.Type,
			SourceNodeID = entity.Name,
			SourceEncounterName = entity.Name,
			Definition = template.ToDefinition(),
			SourceEntity = entity
		};
	}

	public MapEntity GetAdjacentPlanet(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		Vector2I shipHex = GetShipHex(ship, hexContents);
		foreach (Vector2I dir in HexMath.Directions)
		{
			Vector2I neighbor = shipHex + dir;
			if (hexContents.ContainsKey(neighbor) && hexContents[neighbor].Type == GameConstants.EntityTypes.Planet)
			{
				return hexContents[neighbor];
			}
		}

		return null;
	}

	public PlanetData GetAdjacentPlanetData(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		MapEntity planet = GetAdjacentPlanet(ship, hexContents);
		return planet == null ? null : GetPlanetData(planet.Name);
	}

	public bool HasAdjacentOutpost(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		return GetAdjacentOutpost(ship, hexContents) != null;
	}

	public MapEntity GetAdjacentOutpost(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		Vector2I shipHex = GetShipHex(ship, hexContents);
		foreach (Vector2I dir in HexMath.Directions)
		{
			Vector2I neighbor = shipHex + dir;
			if (hexContents.ContainsKey(neighbor) && hexContents[neighbor].Type == GameConstants.EntityTypes.Outpost)
			{
				return hexContents[neighbor];
			}
		}

		return null;
	}

	private MapEntity GetAdjacentMissionEntity(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		Vector2I shipHex = GetShipHex(ship, hexContents);
		foreach (Vector2I dir in HexMath.Directions)
		{
			Vector2I neighbor = shipHex + dir;
			if (!hexContents.ContainsKey(neighbor))
			{
				continue;
			}

			MapEntity entity = hexContents[neighbor];
			if (entity != null && !string.IsNullOrWhiteSpace(entity.MissionInteractionKey))
			{
				return entity;
			}
		}

		return null;
	}

	private MapEntity GetAdjacentEntityOfType(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents, string entityType)
	{
		Vector2I shipHex = GetShipHex(ship, hexContents);
		foreach (Vector2I dir in HexMath.Directions)
		{
			Vector2I neighbor = shipHex + dir;
			if (hexContents.TryGetValue(neighbor, out MapEntity entity) && entity?.Type == entityType)
			{
				return entity;
			}
		}

		return null;
	}

	private AmbientEventInstanceData GetAmbientEventInstance(string instanceId)
	{
		if (_globalData == null
			|| string.IsNullOrWhiteSpace(_globalData.SavedSystem)
			|| !_globalData.ExploredSystems.TryGetValue(_globalData.SavedSystem, out SystemData currentSystem))
		{
			return null;
		}

		return currentSystem.AmbientEvents?.Find(instance => instance != null && instance.InstanceId == instanceId);
	}

	private PlanetData GetPlanetData(string planetName)
	{
		if (_globalData == null || string.IsNullOrEmpty(_globalData.SavedSystem) || !_globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			return null;
		}

		SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
		foreach (PlanetData planet in currentSystem.Planets)
		{
			if (planet.Name == planetName)
			{
				return planet;
			}
		}

		return null;
	}

	private Vector2I GetShipHex(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		foreach (var kvp in hexContents)
		{
			if (kvp.Value == ship)
			{
				return kvp.Key;
			}
		}

		return Vector2I.Zero;
	}

	private MissionManager ResolveMissionManager()
	{
		if (_missionManager != null && GodotObject.IsInstanceValid(_missionManager))
		{
			return _missionManager;
		}

		SceneTree tree = Engine.GetMainLoop() as SceneTree;
		_missionManager = tree?.Root?.GetNodeOrNull<MissionManager>("/root/MissionManager");
		return _missionManager;
	}
}
