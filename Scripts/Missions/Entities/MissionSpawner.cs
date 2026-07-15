using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class MissionSpawner
{
	private const string OfficerPawnScenePath = "res://officer_pawn.tscn";
	private const string MissionNpcPawnScenePath = "res://Scenes/Missions/Entities/MissionNpcPawn.tscn";
	private readonly MissionSpawnResolver _spawnResolver = new MissionSpawnResolver();

	public List<OfficerPawn> SpawnPlayerOfficers(
		MissionSpawnContext context,
		Node2D characterLayer,
		Func<Vector2I, Vector2> cellToGlobalPosition,
		Action<OfficerPawn> configurePawn)
	{
		List<OfficerPawn> spawnedPawns = new List<OfficerPawn>();
		if (context == null || characterLayer == null || cellToGlobalPosition == null)
		{
			return spawnedPawns;
		}

		PackedScene pawnScene = GD.Load<PackedScene>(OfficerPawnScenePath);
		if (pawnScene == null)
		{
			return spawnedPawns;
		}

		foreach (MissionSpawnResult spawn in _spawnResolver.ResolvePlayerOfficerSpawns(context))
		{
			if (spawn?.Officer == null)
			{
				continue;
			}

			OfficerPawn pawn = pawnScene.Instantiate<OfficerPawn>();
			pawn.SetOfficer(spawn.Officer);
			configurePawn?.Invoke(pawn);
			characterLayer.AddChild(pawn);
			pawn.SetGridCell(spawn.Cell, cellToGlobalPosition(spawn.Cell));
			spawnedPawns.Add(pawn);
		}

		return spawnedPawns;
	}

	public List<MissionNpcPawn> SpawnMissionNpcs(
		MissionSpawnContext context,
		Node2D characterLayer,
		Func<Vector2I, Vector2> cellToGlobalPosition)
	{
		List<MissionNpcPawn> spawnedNpcs = new List<MissionNpcPawn>();
		if (context == null || characterLayer == null || cellToGlobalPosition == null)
		{
			return spawnedNpcs;
		}

		List<MissionSpawnResult> npcSpawns = _spawnResolver.ResolveNpcSpawns(context);
		HashSet<string> reservedPortraitPaths = new HashSet<string>();
		foreach (MissionSpawnResult spawn in npcSpawns)
		{
			if (spawn?.NpcDefinition == null)
			{
				continue;
			}

			MissionNpcDefinition resolvedDefinition = ResolvePortraitAssignment(spawn.NpcDefinition, reservedPortraitPaths);
			if (resolvedDefinition == null)
			{
				continue;
			}

			string scenePath = !string.IsNullOrWhiteSpace(resolvedDefinition.ScenePath)
				? resolvedDefinition.ScenePath
				: MissionNpcPawnScenePath;
			PackedScene npcScene = GD.Load<PackedScene>(scenePath);
			if (npcScene == null)
			{
				continue;
			}

			MissionNpcPawn npc = npcScene.Instantiate<MissionNpcPawn>();
			characterLayer.AddChild(npc);
			npc.ApplyDefinition(resolvedDefinition);
			npc.SetGridCell(spawn.Cell, cellToGlobalPosition(spawn.Cell));
			spawnedNpcs.Add(npc);
		}

		return spawnedNpcs;
	}

	private static MissionNpcDefinition ResolvePortraitAssignment(MissionNpcDefinition definition, ISet<string> reservedPortraitPaths)
	{
		if (definition == null)
		{
			return null;
		}

		MissionNpcDefinition resolvedDefinition = definition.Duplicate() as MissionNpcDefinition ?? definition;
		if (resolvedDefinition.IsHostile)
		{
			return resolvedDefinition;
		}

		string portraitPath = resolvedDefinition.PortraitPath ?? string.Empty;
		bool canKeepPortrait = MissionNpcPortraitCatalog.IsNpcPortraitPath(portraitPath)
			&& !string.IsNullOrWhiteSpace(portraitPath)
			&& !reservedPortraitPaths.Contains(portraitPath);
		if (!canKeepPortrait)
		{
			string fallbackPortraitPath = MissionNpcPortraitCatalog.GetNextUnusedPortraitPath(reservedPortraitPaths);
			if (!string.IsNullOrWhiteSpace(fallbackPortraitPath))
			{
				resolvedDefinition.PortraitPath = fallbackPortraitPath;
				portraitPath = fallbackPortraitPath;
			}
		}

		if (!string.IsNullOrWhiteSpace(portraitPath))
		{
			reservedPortraitPaths.Add(portraitPath);
		}

		return resolvedDefinition;
	}
}
