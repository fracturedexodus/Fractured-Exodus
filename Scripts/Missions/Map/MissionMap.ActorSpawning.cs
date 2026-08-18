using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap
{
	private void SpawnMissionOfficers()
	{
		_officerPawns.Clear();

		MissionSpawnContext spawnContext = new MissionSpawnContext
		{
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			RoomBuilder = _roomBuilder
		};
		_officerPawns.AddRange(_missionSpawner.SpawnPlayerOfficers(
			spawnContext,
			_characterLayer,
			GetMovementCellGlobalPosition,
			pawn =>
			{
				pawn.EnteredCell += OnOfficerEnteredCell;
				pawn.ReachedCell += OnOfficerReachedCell;
				pawn.CombatStateChanged += OnOfficerCombatStateChanged;
				pawn.Died += OnOfficerDied;
			}));

		SelectOfficer(0);
	}

	private void SpawnMissionNpcs()
	{
		_missionNpcs.Clear();
		_missionNpcsByCell.Clear();
		_escortSurvivorIds.Clear();
		_extractedSurvivorIds.Clear();

		MissionSpawnContext spawnContext = new MissionSpawnContext
		{
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			RoomBuilder = _roomBuilder
		};
		_missionNpcs.AddRange(_missionSpawner.SpawnMissionNpcs(
			spawnContext,
			_characterLayer,
			GetMovementCellGlobalPosition));
		foreach (MissionNpcPawn npc in _missionNpcs.Where(npc => npc != null))
		{
			npc.EnteredCell += OnMissionNpcEnteredCell;
			npc.ReachedCell += OnMissionNpcReachedCell;
			npc.CombatStateChanged += OnMissionNpcCombatStateChanged;
			npc.Died += OnMissionNpcDied;
			_missionNpcsByCell[npc.CurrentCell] = npc;
		}
	}

	private MissionNpcPawn SpawnRuntimeMissionNpc(MissionNpcDefinition definition, Vector2I movementCell)
	{
		if (definition == null || _characterLayer == null || _roomBuilder == null)
		{
			return null;
		}

		string scenePath = !string.IsNullOrWhiteSpace(definition.ScenePath)
			? definition.ScenePath
			: MissionNpcPawnScenePath;
		PackedScene npcScene = GD.Load<PackedScene>(scenePath);
		if (npcScene == null)
		{
			return null;
		}

		MissionNpcPawn npc = npcScene.Instantiate<MissionNpcPawn>();
		_characterLayer.AddChild(npc);
		npc.ApplyDefinition(definition);
		npc.SetGridCell(movementCell, GetMovementCellGlobalPosition(movementCell));
		npc.EnteredCell += OnMissionNpcEnteredCell;
		npc.ReachedCell += OnMissionNpcReachedCell;
		npc.CombatStateChanged += OnMissionNpcCombatStateChanged;
		npc.Died += OnMissionNpcDied;
		_missionNpcs.Add(npc);
		ReindexMissionNpcCells();
		MarkMissionDepthSortingDirty();
		return npc;
	}

	private void SpawnRelaySurvivors(Vector2I anchorBuildCell)
	{
		List<Vector2I> spawnCells = FindRelaySurvivorSpawnCells(anchorBuildCell, RelaySurvivorDefinitionPaths.Length);
		int spawnedCount = 0;
		for (int i = 0; i < RelaySurvivorDefinitionPaths.Length && i < spawnCells.Count; i++)
		{
			MissionNpcDefinition definition = GD.Load<MissionNpcDefinition>(RelaySurvivorDefinitionPaths[i]);
			if (definition == null)
			{
				continue;
			}

			MissionNpcPawn survivor = SpawnRuntimeMissionNpc(definition, spawnCells[i]);
			if (survivor == null || string.IsNullOrWhiteSpace(survivor.NpcId))
			{
				continue;
			}

			_escortSurvivorIds.Add(survivor.NpcId);
			spawnedCount++;
		}

		if (spawnedCount > 0)
		{
			AppendActionLog($"{spawnedCount} relay survivor{(spawnedCount == 1 ? string.Empty : "s")} join the away team and can now be guided to extraction.");
			if (_explorationPartyMovementEnabled)
			{
				SelectEntireExplorationParty();
			}
			UpdateSelectedOfficerDisplay();
			UpdateMissionCompletionActions();
			UpdateFogOfWar();
		}
	}

	private List<Vector2I> FindRelaySurvivorSpawnCells(Vector2I anchorBuildCell, int count)
	{
		List<Vector2I> cells = new List<Vector2I>();
		if (_roomBuilder == null || count <= 0)
		{
			return cells;
		}

		Vector2I anchorMovementCell = GetMovementCell(anchorBuildCell);
		List<Vector2I> nearbyCells = _roomBuilder.GetReachableMovementCells(anchorMovementCell, MissionGridRules.ScaleAuthoredUnit(2))
			.Where(candidate => candidate != anchorMovementCell)
			.Where(candidate => _roomBuilder.IsWalkableMovementCell(candidate))
			.Where(candidate => !IsMovementCellBlockedByProp(candidate))
			.Where(candidate => !IsCellOccupiedByLivingActor(candidate))
			.OrderBy(candidate => GetTileDistance(anchorMovementCell, candidate))
			.ToList();

		foreach (Vector2I candidate in nearbyCells)
		{
			cells.Add(candidate);
			if (cells.Count >= count)
			{
				return cells;
			}
		}

		if (_roomBuilder.IsWalkableMovementCell(anchorMovementCell) && !IsMovementCellBlockedByProp(anchorMovementCell) && !IsCellOccupiedByLivingActor(anchorMovementCell))
		{
			cells.Add(anchorMovementCell);
		}

		return cells;
	}
}

