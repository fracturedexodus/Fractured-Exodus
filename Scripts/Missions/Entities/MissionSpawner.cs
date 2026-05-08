using Godot;
using System;
using System.Collections.Generic;

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

		foreach (MissionSpawnResult spawn in _spawnResolver.ResolveNpcSpawns(context))
		{
			if (spawn?.NpcDefinition == null)
			{
				continue;
			}

			string scenePath = !string.IsNullOrWhiteSpace(spawn.NpcDefinition.ScenePath)
				? spawn.NpcDefinition.ScenePath
				: MissionNpcPawnScenePath;
			PackedScene npcScene = GD.Load<PackedScene>(scenePath);
			if (npcScene == null)
			{
				continue;
			}

			MissionNpcPawn npc = npcScene.Instantiate<MissionNpcPawn>();
			characterLayer.AddChild(npc);
			npc.ApplyDefinition(spawn.NpcDefinition);
			npc.SetGridCell(spawn.Cell, cellToGlobalPosition(spawn.Cell));
			spawnedNpcs.Add(npc);
		}

		return spawnedNpcs;
	}
}
