using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class MissionSpawnResolver
{
	public List<MissionSpawnResult> ResolvePlayerOfficerSpawns(MissionSpawnContext context)
	{
		List<MissionSpawnResult> results = new List<MissionSpawnResult>();
		if (context?.RoomBuilder == null)
		{
			return results;
		}

		List<string> shipNames = context.MissionState?.ParticipatingShipNames ?? new List<string>();
		if (shipNames.Count == 0)
		{
			return results;
		}

		List<MissionSpawnDefinition> definitions = GetPlayerOfficerDefinitions(context.MissionTemplate, shipNames.Count);
		HashSet<Vector2I> reservedCells = new HashSet<Vector2I>();
		foreach (MissionSpawnDefinition definition in definitions.OrderBy(def => def.OfficerSlotIndex))
		{
			if (definition == null || !AreFlagConditionsSatisfied(definition, context.GlobalData))
			{
				continue;
			}

			int officerSlot = definition.OfficerSlotIndex;
			if (officerSlot < 0 || officerSlot >= shipNames.Count)
			{
				continue;
			}

			OfficerState officer = ResolveOfficer(context.GlobalData, shipNames[officerSlot]);
			if (officer == null)
			{
				continue;
			}

			Vector2I fallbackCell = GetFallbackCellForOfficerSlot(definition, officerSlot);
			bool usedFallbackCell = false;
			Vector2I spawnBuildCell = ResolveSpawnCell(context.RoomBuilder, definition.MarkerId, fallbackCell, reservedCells, ref usedFallbackCell);
			reservedCells.Add(spawnBuildCell);

			results.Add(new MissionSpawnResult
			{
				Definition = definition,
				Officer = officer,
				Cell = context.RoomBuilder.GetMovementCellForBuildCell(spawnBuildCell),
				UsedFallbackCell = usedFallbackCell
			});
		}

		return results;
	}

	public List<MissionSpawnResult> ResolveNpcSpawns(MissionSpawnContext context)
	{
		List<MissionSpawnResult> results = new List<MissionSpawnResult>();
		List<MissionSpawnDefinition> templateDefinitions = GetTemplateSpawnDefinitions(context?.MissionTemplate);
		if (context?.RoomBuilder == null)
		{
			return results;
		}

		HashSet<Vector2I> reservedCells = new HashSet<Vector2I>();
		foreach (MissionSpawnDefinition definition in templateDefinitions
			.Where(def => def != null && def.ActorType == MissionActorType.MissionNpc))
		{
			if (!AreFlagConditionsSatisfied(definition, context.GlobalData))
			{
				continue;
			}

			MissionNpcDefinition npcDefinition = string.IsNullOrWhiteSpace(definition.NpcDefinitionPath)
				? null
				: GD.Load<MissionNpcDefinition>(definition.NpcDefinitionPath);
			if (npcDefinition == null)
			{
				continue;
			}

			bool usedFallbackCell = false;
			Vector2I spawnBuildCell = ResolveSpawnCell(context.RoomBuilder, definition.MarkerId, definition.FallbackCell, reservedCells, ref usedFallbackCell);
			reservedCells.Add(spawnBuildCell);

			results.Add(new MissionSpawnResult
			{
				Definition = definition,
				NpcDefinition = npcDefinition,
				Cell = context.RoomBuilder.GetMovementCellForBuildCell(spawnBuildCell),
				UsedFallbackCell = usedFallbackCell
			});
		}

		foreach (MissionRoomBuilder.MarkerPlacement placement in context.RoomBuilder.GetMarkerPlacements()
			.Where(placement => placement != null
				&& (placement.MarkerId == "npc_spawn" || placement.MarkerId == "hostile_spawn")
				&& !string.IsNullOrWhiteSpace(placement.NpcDefinitionPath)))
		{
			MissionNpcDefinition npcDefinition = GD.Load<MissionNpcDefinition>(placement.NpcDefinitionPath);
			if (npcDefinition == null || reservedCells.Contains(placement.Cell))
			{
				continue;
			}

			bool wantsHostile = placement.MarkerId == "hostile_spawn";
			if (npcDefinition.IsHostile != wantsHostile)
			{
				continue;
			}

			reservedCells.Add(placement.Cell);
			results.Add(new MissionSpawnResult
			{
				Definition = new MissionSpawnDefinition
				{
					SpawnId = string.IsNullOrWhiteSpace(placement.TargetId) ? $"{placement.MarkerId}_{placement.Cell.X}_{placement.Cell.Y}" : placement.TargetId,
					MarkerId = placement.TargetId,
					ActorType = MissionActorType.MissionNpc,
					NpcDefinitionPath = placement.NpcDefinitionPath,
					FallbackCell = placement.Cell,
					Notes = placement.Notes
				},
				NpcDefinition = npcDefinition,
				Cell = context.RoomBuilder.GetMovementCellForBuildCell(placement.Cell),
				UsedFallbackCell = false
			});
		}

		return results;
	}

	private static List<MissionSpawnDefinition> GetPlayerOfficerDefinitions(MissionTemplate missionTemplate, int officerCount)
	{
		List<MissionSpawnDefinition> templateDefinitions = GetTemplateSpawnDefinitions(missionTemplate)
			.Where(def => def != null && def.ActorType == MissionActorType.PlayerOfficer)
			.ToList();
		if (templateDefinitions.Count > 0)
		{
			return templateDefinitions;
		}

		List<MissionSpawnDefinition> fallbackDefinitions = new List<MissionSpawnDefinition>();
		for (int slot = 0; slot < officerCount; slot++)
		{
			fallbackDefinitions.Add(new MissionSpawnDefinition
			{
				SpawnId = $"player_officer_{slot}",
				MarkerId = GetDefaultMarkerIdForOfficerSlot(slot),
				ActorType = MissionActorType.PlayerOfficer,
				OfficerSlotIndex = slot,
				FallbackCell = GetDefaultFallbackCellForOfficerSlot(slot),
				Notes = "Generated fallback spawn definition."
			});
		}

		return fallbackDefinitions;
	}

	private static List<MissionSpawnDefinition> GetTemplateSpawnDefinitions(MissionTemplate missionTemplate)
	{
		List<MissionSpawnDefinition> definitions = missionTemplate?.SpawnDefinitions?
			.Where(def => def != null)
			.ToList() ?? new List<MissionSpawnDefinition>();
		if (definitions.Count > 0)
		{
			return definitions;
		}

		if (missionTemplate?.SpawnDefinitionPaths == null || missionTemplate.SpawnDefinitionPaths.Count == 0)
		{
			return definitions;
		}

		foreach (string path in missionTemplate.SpawnDefinitionPaths)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				continue;
			}

			MissionSpawnDefinition definition = GD.Load<MissionSpawnDefinition>(path);
			if (definition != null)
			{
				definitions.Add(definition);
			}
		}

		return definitions;
	}

	private static string GetDefaultMarkerIdForOfficerSlot(int officerSlot)
	{
		return officerSlot switch
		{
			0 => "spawn_a",
			1 => "spawn_b",
			_ => $"spawn_{officerSlot + 1}"
		};
	}

	private static Vector2I GetDefaultFallbackCellForOfficerSlot(int officerSlot)
	{
		return officerSlot switch
		{
			0 => new Vector2I(3, 5),
			1 => new Vector2I(4, 5),
			_ => new Vector2I(3 + officerSlot, 5)
		};
	}

	private static Vector2I GetFallbackCellForOfficerSlot(MissionSpawnDefinition definition, int officerSlot)
	{
		return definition != null && definition.FallbackCell != Vector2I.Zero
			? definition.FallbackCell
			: GetDefaultFallbackCellForOfficerSlot(officerSlot);
	}

	private static OfficerState ResolveOfficer(GlobalData globalData, string shipName)
	{
		if (globalData?.ShipOfficers == null || string.IsNullOrWhiteSpace(shipName))
		{
			return null;
		}

		return globalData.ShipOfficers.TryGetValue(shipName, out OfficerState officer)
			? officer
			: null;
	}

	private static bool AreFlagConditionsSatisfied(MissionSpawnDefinition definition, GlobalData globalData)
	{
		HashSet<string> activeFlags = globalData?.StoryFlags != null
			? globalData.StoryFlags.ToHashSet()
			: new HashSet<string>();

		foreach (string requiredFlag in definition.RequiredFlags ?? new Godot.Collections.Array<string>())
		{
			if (!string.IsNullOrWhiteSpace(requiredFlag) && !activeFlags.Contains(requiredFlag))
			{
				return false;
			}
		}

		foreach (string blockedFlag in definition.BlockedFlags ?? new Godot.Collections.Array<string>())
		{
			if (!string.IsNullOrWhiteSpace(blockedFlag) && activeFlags.Contains(blockedFlag))
			{
				return false;
			}
		}

		return true;
	}

	private static Vector2I ResolveSpawnCell(MissionRoomBuilder roomBuilder, string markerId, Vector2I fallbackCell, HashSet<Vector2I> reservedCells, ref bool usedFallbackCell)
	{
		Vector2I spawnCell = fallbackCell;
		if (!string.IsNullOrWhiteSpace(markerId)
			&& roomBuilder.TryGetSpawnCell(markerId, out Vector2I markerCell)
			&& roomBuilder.IsWalkableCell(markerCell)
			&& !reservedCells.Contains(markerCell))
		{
			return markerCell;
		}

		usedFallbackCell = true;
		if (roomBuilder.IsWalkableCell(fallbackCell) && !reservedCells.Contains(fallbackCell))
		{
			return fallbackCell;
		}

		Vector2I fallbackSearchOrigin = fallbackCell != Vector2I.Zero ? fallbackCell : Vector2I.Zero;
		Vector2I? nearbyAvailable = FindNearestAvailableCell(roomBuilder, fallbackSearchOrigin, reservedCells);
		if (nearbyAvailable.HasValue)
		{
			return nearbyAvailable.Value;
		}

		Vector2I? firstAvailable = roomBuilder.GetFloorCells()
			.Where(cell => roomBuilder.IsWalkableCell(cell) && !reservedCells.Contains(cell))
			.OrderBy(cell => cell.Y)
			.ThenBy(cell => cell.X)
			.Cast<Vector2I?>()
			.FirstOrDefault();
		return firstAvailable ?? fallbackCell;
	}

	private static Vector2I? FindNearestAvailableCell(MissionRoomBuilder roomBuilder, Vector2I origin, HashSet<Vector2I> reservedCells)
	{
		if (roomBuilder == null)
		{
			return null;
		}

		Queue<Vector2I> frontier = new Queue<Vector2I>();
		HashSet<Vector2I> visited = new HashSet<Vector2I>();
		frontier.Enqueue(origin);
		visited.Add(origin);

		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			if (roomBuilder.IsWalkableCell(current) && !reservedCells.Contains(current))
			{
				return current;
			}

			foreach (Vector2I direction in directions)
			{
				Vector2I next = current + direction;
				if (visited.Contains(next))
				{
					continue;
				}

				visited.Add(next);
				frontier.Enqueue(next);
			}
		}

		return null;
	}
}
