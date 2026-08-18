using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private IEnumerable<OfficerPawn> GetAliveOfficers()
	{
		return _officerPawns.Where(pawn => pawn != null && !pawn.IsDead);
	}

	private bool IsEscortSurvivorId(string npcId)
	{
		return !string.IsNullOrWhiteSpace(npcId)
			&& (_escortSurvivorIds.Contains(npcId) || npcId.StartsWith("relay_survivor_", StringComparison.Ordinal));
	}

	private bool IsEscortSurvivor(MissionNpcPawn npc)
	{
		return npc != null && IsEscortSurvivorId(npc.NpcId);
	}

	private bool IsActiveEscortSurvivor(MissionNpcPawn npc)
	{
		return IsEscortSurvivor(npc) && !npc.IsDead && !npc.IsExtracted && !_extractedSurvivorIds.Contains(npc.NpcId);
	}

	private IEnumerable<MissionNpcPawn> GetAliveEscortSurvivors()
	{
		return _missionNpcs.Where(IsActiveEscortSurvivor);
	}

	private int GetExtractedEscortSurvivorCount()
	{
		return _extractedSurvivorIds.Count;
	}

	private IEnumerable<MissionNpcPawn> GetAliveHostileEnemies()
	{
		return _missionNpcs.Where(npc => npc != null && npc.IsHostile && !npc.IsDead);
	}

	private IEnumerable<MissionNpcPawn> GetEngagedHostileEnemies()
	{
		return GetAliveHostileEnemies().Where(npc => !string.IsNullOrWhiteSpace(npc.NpcId) && _engagedEnemyIds.Contains(npc.NpcId));
	}

	private IEnumerable<MissionNpcPawn> GetVisibleAliveHostileEnemies()
	{
		return GetAliveHostileEnemies().Where(npc => _visibleCells.Contains(npc.CurrentCell));
	}

	private bool IsAnyCombatActorMoving()
	{
		return _officerPawns.Any(pawn => pawn != null && pawn.IsMoving)
			|| _missionNpcs.Any(npc => npc != null && npc.IsMoving);
	}

	private bool IsCellOccupiedByLivingActor(
		Vector2I cell,
		OfficerPawn ignoreOfficer = null,
		MissionNpcPawn ignoreEnemy = null,
		IReadOnlyCollection<string> ignoredOfficerIds = null,
		IReadOnlyCollection<string> ignoredSurvivorIds = null)
	{
		foreach (OfficerPawn pawn in _officerPawns)
		{
			if (pawn == null || pawn == ignoreOfficer || pawn.IsDead)
			{
				continue;
			}

			if (ignoredOfficerIds != null && !string.IsNullOrWhiteSpace(pawn.OfficerID) && ignoredOfficerIds.Contains(pawn.OfficerID))
			{
				continue;
			}

			if (pawn.CurrentCell == cell)
			{
				return true;
			}
		}

		foreach (MissionNpcPawn npc in _missionNpcs)
		{
			if (npc == null || npc == ignoreEnemy || npc.IsDead || npc.IsExtracted)
			{
				continue;
			}

			if (ignoredSurvivorIds != null && !string.IsNullOrWhiteSpace(npc.NpcId) && ignoredSurvivorIds.Contains(npc.NpcId))
			{
				continue;
			}

			if (npc.CurrentCell == cell)
			{
				return true;
			}
		}

		return false;
	}

	private MissionCombatTurnEntry GetActiveCombatTurnEntry()
	{
		return _combatActiveIndex >= 0 && _combatActiveIndex < _combatQueue.Count
			? _combatQueue[_combatActiveIndex]
			: null;
	}

	private OfficerPawn GetActiveCombatOfficer()
	{
		MissionCombatTurnEntry entry = GetActiveCombatTurnEntry();
		return entry != null && entry.IsOfficer ? entry.Officer : null;
	}

	private MissionNpcPawn GetActiveCombatEscortSurvivor()
	{
		MissionCombatTurnEntry entry = GetActiveCombatTurnEntry();
		return entry != null && !entry.IsOfficer && entry.Enemy != null && !entry.Enemy.IsHostile ? entry.Enemy : null;
	}

	private MissionNpcPawn GetActiveCombatEnemy()
	{
		MissionCombatTurnEntry entry = GetActiveCombatTurnEntry();
		return entry != null && !entry.IsOfficer && entry.Enemy?.IsHostile == true ? entry.Enemy : null;
	}

	private bool IsPlayerTurnActive()
	{
		return _combatActive && (GetActiveCombatOfficer() != null || GetActiveCombatEscortSurvivor() != null) && !_enemyTurnInProgress;
	}

	private bool TryGetOfficerState(OfficerPawn officer, out OfficerState officerState)
	{
		officerState = null;
		if (officer == null || _globalData?.ShipOfficers == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(officer.OfficerID))
		{
			officerState = _globalData.ShipOfficers.Values.FirstOrDefault(candidate =>
				candidate != null
				&& string.Equals(candidate.OfficerID, officer.OfficerID, StringComparison.Ordinal));
			if (officerState != null)
			{
				return true;
			}
		}

		if (!string.IsNullOrWhiteSpace(officer.ShipName)
			&& _globalData.ShipOfficers.TryGetValue(officer.ShipName, out officerState)
			&& officerState != null)
		{
			return true;
		}

		return officerState != null;
	}

	private string GetCombatActionPromptText(OfficerPawn activeOfficer)
	{
		if (!_combatActive)
		{
			return string.Empty;
		}

		if (activeOfficer == null)
		{
			return "Click the ground to move the active escort and use END TURN when that unit is done.";
		}

		string attackVerb = activeOfficer.UsesMeleeWeapon ? "strike" : "fire";
		return _selectedCombatActionMode == MissionPlayerCombatActionMode.Move
			? "MOVE is selected: click the ground or a reachable interaction target to reposition the active officer."
			: $"{(activeOfficer.UsesMeleeWeapon ? "MELEE" : "RANGED")} ATTACK is selected: click the highlighted ground square beneath a visible hostile to {attackVerb} with the equipped weapon.";
	}

	private List<MissionCombatActionOption> BuildCombatActionOptions(OfficerPawn officer)
	{
		return new List<MissionCombatActionOption>
		{
			new MissionCombatActionOption
			{
				ActionId = "attack",
				Label = $"{(officer?.UsesMeleeWeapon == true ? "Melee" : "Ranged")} Attack ({CombatAttackActionCost} AP)",
				Description = officer == null
					? "Attack with the active officer."
					: $"Attack with {officer.WeaponName}. Costs {CombatAttackActionCost} AP.",
				Disabled = officer == null || officer.IsDead || !officer.CanSpendActions(CombatAttackActionCost),
				Selected = _selectedCombatActionMode == MissionPlayerCombatActionMode.Attack
			},
			new MissionCombatActionOption
			{
				ActionId = "move",
				Label = "Move",
				Description = "Click a floor cell to reposition the active officer. Distance determines AP cost.",
				Disabled = officer == null || officer.IsDead || officer.CurrentActions <= 0,
				Selected = _selectedCombatActionMode == MissionPlayerCombatActionMode.Move
			}
		};
	}

	private List<MissionCombatWeaponOption> BuildCombatWeaponOptions(OfficerPawn officer)
	{
		List<MissionCombatWeaponOption> options = new List<MissionCombatWeaponOption>();
		if (officer == null || !TryGetOfficerState(officer, out OfficerState officerState))
		{
			return options;
		}

		IReadOnlyList<string> ownedWeaponIds = OfficerMissionLoadoutService.GetOwnedWeaponIds(officerState);
		string equippedWeaponId = officerState.EquippedMissionWeaponId;

		foreach (string weaponId in ownedWeaponIds)
		{
			MissionWeaponDefinition weapon = MissionEquipmentRegistry.GetWeapon(weaponId);
			if (weapon == null)
			{
				continue;
			}

			bool equipped = string.Equals(equippedWeaponId, weapon.WeaponId, StringComparison.Ordinal);
			string stance = weapon.IsMelee ? "Melee" : "Ranged";
			options.Add(new MissionCombatWeaponOption
			{
				WeaponId = weapon.WeaponId,
				Label = $"{weapon.DisplayName} [{stance} | R{MissionGridRules.ScaleAuthoredUnit(weapon.AttackRange)} | {weapon.MinDamage}-{weapon.MaxDamage}]",
				Description = $"{weapon.Description}\nCosts {CombatAttackActionCost} AP to equip during combat.",
				Disabled = equipped || !officer.CanSpendActions(CombatAttackActionCost),
				Equipped = equipped
			});
		}

		return options;
	}

	private void EvaluateCombatState()
	{
		if (_missionGameOver || IsAnyCombatActorMoving())
		{
			return;
		}

		if (!GetAliveOfficers().Any())
		{
			HandleMissionGameOver();
			return;
		}

		List<MissionNpcPawn> visibleHostiles = GetVisibleAliveHostileEnemies().ToList();
		foreach (MissionNpcPawn hostile in visibleHostiles)
		{
			if (!string.IsNullOrWhiteSpace(hostile.NpcId))
			{
				_engagedEnemyIds.Add(hostile.NpcId);
			}
		}

		bool anyVisibleHostiles = visibleHostiles.Count > 0;
		if (!_combatActive)
		{
			if (_engagedEnemyIds.Count > 0 && anyVisibleHostiles)
			{
				StartMissionCombat();
			}
			else
			{
				RefreshCombatHud();
			}

			return;
		}

		if (!GetEngagedHostileEnemies().Any())
		{
			EndMissionCombat();
			return;
		}

		CleanupCombatQueue();
		if (_combatQueue.Count == 0 && !_enemyTurnInProgress)
		{
			_combatActiveIndex = -1;
			RebuildCombatQueue();
			BeginNextCombatTurn();
			return;
		}

		RefreshCombatHud();
	}

	private void StartMissionCombat()
	{
		if (_missionGameOver || _combatActive)
		{
			return;
		}

		_combatActive = true;
		_enemyTurnInProgress = false;
		_selectedCombatActionMode = MissionPlayerCombatActionMode.Attack;
		_combatRound = 1;
		_combatActiveIndex = -1;
		_missionUi?.ClearCombatLog();
		AppendCombatLog("Combat erupts in the mission zone as hostile contacts emerge from cover.");
		RebuildCombatQueue();
		UpdateMovementGridVisibility();
		RefreshCombatHud();
		BeginNextCombatTurn();
	}

	private void EndMissionCombat()
	{
		_combatActive = false;
		_enemyTurnInProgress = false;
		_selectedCombatActionMode = MissionPlayerCombatActionMode.Attack;
		_combatQueue.Clear();
		_combatActiveIndex = -1;
		_pendingCombatMoveOfficerId = string.Empty;
		_pendingCombatAttackEnemyId = string.Empty;
		_focusedEnemy = null;
		_engagedEnemyIds.Clear();
		_hoveredCombatActor = null;
		_missionUi?.HideHoverSummary();
		AppendCombatLog("The last engaged hostile goes quiet. Combat control returns to exploration.");
		if (_explorationPartyMovementEnabled)
		{
			SelectEntireExplorationParty();
		}
		UpdateMovementGridVisibility();
		RefreshCombatHud();
	}

	private void UpdateHostileRoaming(float delta)
	{
		if (_combatActive || _missionGameOver || _roomBuilder == null)
		{
			_hostileRoamClock = 0f;
			return;
		}

		if ((_dialogueUi?.IsConversationOpen ?? false) || IsAnyCombatActorMoving())
		{
			return;
		}

		_hostileRoamClock += delta;
		if (_hostileRoamClock < HostileRoamIntervalSeconds)
		{
			return;
		}

		_hostileRoamClock = 0f;
		List<MissionNpcPawn> roamingHostiles = _missionNpcs
			.Where(npc => npc != null && npc.IsHostile && !npc.IsDead && !npc.IsMoving && !_engagedEnemyIds.Contains(npc.NpcId))
			.OrderBy(_ => _combatRng.Randi())
			.ToList();
		foreach (MissionNpcPawn hostile in roamingHostiles)
		{
			if (TryRoamHostile(hostile))
			{
				break;
			}
		}
	}

	private bool TryRoamHostile(MissionNpcPawn hostile)
	{
		if (hostile == null || hostile.IsDead || hostile.IsMoving || _roomBuilder == null)
		{
			return false;
		}

		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};
		List<Vector2I> candidateCells = directions
			.Select(direction => hostile.CurrentCell + direction)
			.Where(cell => _roomBuilder.IsWalkableMovementCell(cell) && !IsMovementCellBlockedByProp(cell) && !IsCellOccupiedByLivingActor(cell, null, hostile))
			.OrderBy(_ => _combatRng.Randi())
			.ToList();
		if (candidateCells.Count == 0)
		{
			return false;
		}

		Vector2I destinationCell = candidateCells[0];
		if (!TryGetTraversableMovementPath(hostile.CurrentCell, destinationCell, null, hostile, null, out List<Vector2I> pathCells) || pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells.Skip(1).ToList();
		List<Vector2> pathPoints = steppedCells
			.Select(GetMovementCellGlobalPosition)
			.ToList();
		hostile.MoveAlongPath(pathPoints, steppedCells, destinationCell);
		return true;
	}

	private void UpdateEscortSurvivorBehavior(float delta)
	{
		_escortFollowClock = 0f;
	}

	private bool TryAdvanceEscortSurvivor(MissionNpcPawn survivor, bool useCombatActions)
	{
		if (survivor == null || survivor.IsDead || survivor.IsExtracted || survivor.IsMoving || _roomBuilder == null)
		{
			return false;
		}

		if (TryExtractEscortSurvivor(survivor))
		{
			return false;
		}

		int maxSteps = useCombatActions ? GetMaxMovementStepsForActions(Mathf.Max(1, survivor.CurrentActions)) : GetMovementSubdivision();
		if (maxSteps <= 0)
		{
			return false;
		}

		List<Vector2I> targetCells = AreAllOfficersOnEvacZone()
			? GetEvacRallyCells().ToList()
			: GetAliveOfficers().Select(officer => officer.CurrentCell).ToList();
		if (targetCells.Count == 0)
		{
			return false;
		}

		Vector2I destinationCell = ResolveEscortDestinationCell(survivor, targetCells, maxSteps);
		if (destinationCell == survivor.CurrentCell)
		{
			return false;
		}

		if (!TryGetTraversableMovementPath(survivor.CurrentCell, destinationCell, null, survivor, null, out List<Vector2I> pathCells) || pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells.Skip(1).Take(maxSteps).ToList();
		if (steppedCells.Count == 0)
		{
			return false;
		}

		int moveCost = GetMovementCostForPathSteps(steppedCells.Count);
		if (useCombatActions && !survivor.CanSpendActions(moveCost))
		{
			return false;
		}

		if (useCombatActions)
		{
			survivor.SpendActions(moveCost);
		}

		Vector2I finalDestination = steppedCells[^1];
		List<Vector2> pathPoints = steppedCells.Select(GetMovementCellGlobalPosition).ToList();
		survivor.MoveAlongPath(pathPoints, steppedCells, finalDestination);
		if (useCombatActions)
		{
			AppendCombatLog($"{survivor.DisplayName} falls back {moveCost} cell{(moveCost == 1 ? string.Empty : "s")} toward extraction.");
		}
		else
		{
			AppendActionLog($"{survivor.DisplayName} keeps close behind the officers.");
		}

		return true;
	}

	private Vector2I ResolveEscortDestinationCell(MissionNpcPawn survivor, IReadOnlyList<Vector2I> targetCells, int maxSteps)
	{
		Vector2I bestDestination = survivor.CurrentCell;
		int bestDistance = int.MaxValue;
		foreach (Vector2I targetCell in targetCells)
		{
			if (!TryGetTraversableMovementPath(survivor.CurrentCell, targetCell, null, survivor, null, out List<Vector2I> pathCells) || pathCells.Count <= 1)
			{
				continue;
			}

			List<Vector2I> steppedCells = pathCells
				.Skip(1)
				.TakeWhile(cell => !IsCellOccupiedByLivingActor(cell, null, survivor))
				.Take(maxSteps)
				.ToList();
			if (steppedCells.Count == 0)
			{
				continue;
			}

			Vector2I candidate = steppedCells[^1];
			int candidateDistance = GetTileDistance(candidate, targetCell);
			if (candidateDistance < bestDistance)
			{
				bestDistance = candidateDistance;
				bestDestination = candidate;
			}
		}

		return bestDestination;
	}

	private bool TryExtractEscortSurvivor(MissionNpcPawn survivor)
	{
		if (!IsActiveEscortSurvivor(survivor))
		{
			return false;
		}

		HashSet<Vector2I> evacCells = GetEvacRallyCells();
		if (evacCells.Count == 0 || !evacCells.Contains(survivor.CurrentCell))
		{
			return false;
		}

		_extractedSurvivorIds.Add(survivor.NpcId);
		survivor.SetExtracted();
		_selectedEscortSurvivorIds.Remove(survivor.NpcId);
		if (survivor.NpcId == _selectedEscortSurvivorId)
		{
			_selectedEscortSurvivorId = string.Empty;
		}
		NormalizeFriendlySelectionState();
		UpdateOfficerSelectionVisuals();
		UpdateSelectedOfficerDisplay();
		AppendActionLog($"{survivor.DisplayName} reaches the evac zone and is brought aboard the fleet.");
		ReindexMissionNpcCells();
		UpdateFogOfWar();
		UpdateMissionCompletionActions();
		return true;
	}

	private void CleanupCombatQueue()
	{
		string activeCombatantId = GetActiveCombatTurnEntry()?.CombatantId ?? string.Empty;
		_combatQueue.RemoveAll(entry => entry == null
			|| (entry.IsOfficer && (entry.Officer == null || entry.Officer.IsDead))
			|| (!entry.IsOfficer && (entry.Enemy == null || entry.Enemy.IsDead || entry.Enemy.IsExtracted || (entry.Enemy.IsHostile ? !_engagedEnemyIds.Contains(entry.Enemy.NpcId) : !IsActiveEscortSurvivor(entry.Enemy)))));

		if (_combatQueue.Count == 0)
		{
			_combatActiveIndex = -1;
			return;
		}

		if (!string.IsNullOrWhiteSpace(activeCombatantId))
		{
			int newIndex = _combatQueue.FindIndex(entry => entry.CombatantId == activeCombatantId);
			_combatActiveIndex = newIndex >= 0 ? newIndex : Mathf.Clamp(_combatActiveIndex, -1, _combatQueue.Count - 1);
			return;
		}

		_combatActiveIndex = Mathf.Clamp(_combatActiveIndex, -1, _combatQueue.Count - 1);
	}

	private void RebuildCombatQueue()
	{
		List<MissionCombatTurnEntry> nextQueue = new List<MissionCombatTurnEntry>();
		foreach (OfficerPawn officer in GetAliveOfficers())
		{
			nextQueue.Add(new MissionCombatTurnEntry
			{
				CombatantId = officer.OfficerID,
				IsOfficer = true,
				InitiativeScore = _combatRng.RandiRange(1, 20) + officer.InitiativeBonus,
				Officer = officer
			});
		}

		foreach (MissionNpcPawn enemy in GetEngagedHostileEnemies())
		{
			nextQueue.Add(new MissionCombatTurnEntry
			{
				CombatantId = enemy.NpcId,
				IsOfficer = false,
				InitiativeScore = _combatRng.RandiRange(1, 20) + enemy.InitiativeBonus,
				Enemy = enemy
			});
		}

		foreach (MissionNpcPawn survivor in GetAliveEscortSurvivors())
		{
			nextQueue.Add(new MissionCombatTurnEntry
			{
				CombatantId = survivor.NpcId,
				IsOfficer = false,
				InitiativeScore = _combatRng.RandiRange(1, 20) + survivor.InitiativeBonus,
				Enemy = survivor
			});
		}

		_combatQueue.Clear();
		_combatQueue.AddRange(nextQueue
			.OrderByDescending(entry => entry.InitiativeScore)
			.ThenBy(entry => entry.IsOfficer ? entry.Officer?.OfficerName : entry.Enemy?.DisplayName)
			.ToList());
	}

	private async void BeginNextCombatTurn()
	{
		if (_missionGameOver || _enemyTurnInProgress)
		{
			return;
		}

		if (!_combatActive)
		{
			RefreshCombatHud();
			return;
		}

		if (!GetAliveOfficers().Any())
		{
			HandleMissionGameOver();
			return;
		}

		if (!GetEngagedHostileEnemies().Any())
		{
			EndMissionCombat();
			return;
		}

		if (_combatQueue.Count == 0)
		{
			RebuildCombatQueue();
			if (_combatQueue.Count == 0)
			{
				EndMissionCombat();
				return;
			}
		}

		_combatActiveIndex++;
		if (_combatActiveIndex >= _combatQueue.Count)
		{
			_combatRound++;
			RebuildCombatQueue();
			if (_combatQueue.Count == 0)
			{
				EndMissionCombat();
				return;
			}

			_combatActiveIndex = 0;
		}

		MissionCombatTurnEntry entry = GetActiveCombatTurnEntry();
		if (entry == null)
		{
			EndMissionCombat();
			return;
		}

		if (entry.IsOfficer)
		{
			entry.Officer?.BeginTurn();
			_selectedCombatActionMode = MissionPlayerCombatActionMode.Attack;
			int officerIndex = _officerPawns.IndexOf(entry.Officer);
			if (officerIndex >= 0)
			{
				SelectOfficer(officerIndex);
			}

			_focusedEnemy = GetClosestVisibleEnemy(entry.Officer?.CurrentCell ?? Vector2I.Zero);
			if (entry.Officer != null)
			{
				AppendCombatLog($"Round {_combatRound}: {entry.Officer.OfficerName} takes point with {entry.Officer.CurrentActions} AP and {entry.Officer.CurrentHP} HP.");
			}
			RefreshCombatHud();
			return;
		}

		MissionNpcPawn activeNpc = entry.Enemy;
		if (activeNpc == null || activeNpc.IsDead || activeNpc.IsExtracted)
		{
			EndCurrentCombatTurn();
			return;
		}

		activeNpc.BeginTurn();
		_selectedCombatActionMode = MissionPlayerCombatActionMode.Move;
		_focusedEnemy = activeNpc.IsHostile ? activeNpc : _focusedEnemy;
		AppendCombatLog(activeNpc.IsHostile
			? $"Round {_combatRound}: {activeNpc.DisplayName} advances with {activeNpc.CurrentActions} AP and {activeNpc.CurrentHP} HP."
			: $"Round {_combatRound}: {activeNpc.DisplayName} scrambles for evac with {activeNpc.CurrentActions} AP and {activeNpc.CurrentHP} HP.");
		RefreshCombatHud();
		if (activeNpc.IsHostile)
		{
			_enemyTurnInProgress = true;
			await ToSignal(GetTree().CreateTimer(0.35f), SceneTreeTimer.SignalName.Timeout);
			await ExecuteEnemyTurnAsync(activeNpc);
			_enemyTurnInProgress = false;
		}
		else
		{
			SelectEscortSurvivor(activeNpc);
			return;
		}

		if (_missionGameOver)
		{
			return;
		}

		if (!_combatActive)
		{
			RefreshCombatHud();
			return;
		}

		CleanupCombatQueue();
		EndCurrentCombatTurn();
	}

	private void EndCurrentCombatTurn()
	{
		if (_missionGameOver || !_combatActive)
		{
			RefreshCombatHud();
			return;
		}

		_pendingCombatMoveOfficerId = string.Empty;
		_pendingCombatAttackEnemyId = string.Empty;
		RefreshCombatHud();
		BeginNextCombatTurn();
	}

	private void OnCombatEndTurnPressed()
	{
		if (!_combatActive || _missionGameOver || _enemyTurnInProgress)
		{
			return;
		}

		OfficerPawn activeOfficer = GetActiveCombatOfficer();
		if (activeOfficer != null)
		{
			AppendActionLog($"{activeOfficer.OfficerName} ends their turn.");
		}
		else if (GetActiveCombatEscortSurvivor() is MissionNpcPawn survivor)
		{
			AppendActionLog($"{survivor.DisplayName} holds position and waits for the next opening.");
		}

		EndCurrentCombatTurn();
	}

	private void OnExplorationControlModeChosen(string modeId)
	{
		if (_combatActive || _missionGameOver)
		{
			return;
		}

		SetExplorationMovementMode(string.Equals(modeId, "party", StringComparison.OrdinalIgnoreCase));
	}

	private void OnExplorationOfficerChosen(string officerId)
	{
		if (_combatActive || _missionGameOver || string.IsNullOrWhiteSpace(officerId))
		{
			return;
		}

		if (officerId.StartsWith("officer:", StringComparison.Ordinal))
		{
			string resolvedOfficerId = officerId["officer:".Length..];
			OfficerPawn officer = _officerPawns.FirstOrDefault(candidate => candidate != null && !candidate.IsDead && candidate.OfficerID == resolvedOfficerId);
			if (officer == null)
			{
				return;
			}

			SetExplorationMovementMode(false, officer);
			return;
		}

		if (officerId.StartsWith("survivor:", StringComparison.Ordinal))
		{
			string survivorNpcId = officerId["survivor:".Length..];
			MissionNpcPawn survivor = _missionNpcs.FirstOrDefault(candidate => candidate != null && candidate.NpcId == survivorNpcId && IsActiveEscortSurvivor(candidate));
			if (survivor != null)
			{
				_explorationPartyMovementEnabled = false;
				SelectEscortSurvivor(survivor);
			}
		}
	}

	private void OnExplorationUnitFocusRequested(string unitId)
	{
		if (_missionGameOver || string.IsNullOrWhiteSpace(unitId))
		{
			return;
		}

		switch (ResolveExplorationUnit(unitId))
		{
			case OfficerPawn officer:
				FocusCameraOnUnit(officer);
				break;
			case MissionNpcPawn survivor:
				FocusCameraOnUnit(survivor);
				break;
		}
	}

	private void OnExplorationOfficerInventoryRequested(string officerId)
	{
		if (_missionUi == null || _combatActive || _missionGameOver || string.IsNullOrWhiteSpace(officerId))
		{
			return;
		}

		MissionOfficerInventoryPanelData data = ResolveExplorationUnit(officerId) switch
		{
			OfficerPawn officer => BuildOfficerInventoryPanelData(officer),
			MissionNpcPawn survivor => BuildSurvivorInventoryPanelData(survivor),
			_ => null
		};
		if (data != null)
		{
			_missionUi.ShowOfficerInventory(data);
		}
	}

	private void OnOfficerInventoryWeaponEquipRequested(string officerId, string weaponId)
	{
		if (_missionUi == null
			|| _combatActive
			|| _missionGameOver
			|| string.IsNullOrWhiteSpace(officerId)
			|| string.IsNullOrWhiteSpace(weaponId))
		{
			return;
		}

		switch (ResolveExplorationUnit(officerId))
		{
			case OfficerPawn officer when TryGetOfficerState(officer, out OfficerState officerState):
			{
				MissionWeaponDefinition weapon = MissionEquipmentRegistry.GetWeapon(weaponId);
				if (weapon == null || !OfficerMissionLoadoutService.EquipWeapon(officerState, weaponId))
				{
					return;
				}

				officer.RefreshLoadoutFromState();
				AppendActionLog($"{officer.OfficerName} equips {weapon.DisplayName}.");
				UpdateSelectedOfficerDisplay();
				RefreshCombatHud();
				_missionUi.ShowOfficerInventory(BuildOfficerInventoryPanelData(officer));
				return;
			}
			case MissionNpcPawn survivor:
			{
				MissionWeaponDefinition weapon = MissionEquipmentRegistry.GetWeapon(weaponId);
				if (weapon == null || !survivor.EquipWeapon(weaponId))
				{
					return;
				}

				AppendActionLog($"{survivor.DisplayName} equips {weapon.DisplayName}.");
				UpdateSelectedOfficerDisplay();
				RefreshCombatHud();
				_missionUi.ShowOfficerInventory(BuildSurvivorInventoryPanelData(survivor));
				return;
			}
		}
	}

	private void OnOfficerInventoryShieldEquipRequested(string officerId, string shieldId)
	{
		if (_missionUi == null
			|| _combatActive
			|| _missionGameOver
			|| string.IsNullOrWhiteSpace(officerId)
			|| string.IsNullOrWhiteSpace(shieldId))
		{
			return;
		}

		switch (ResolveExplorationUnit(officerId))
		{
			case OfficerPawn officer when TryGetOfficerState(officer, out OfficerState officerState):
			{
				MissionShieldDefinition shield = MissionEquipmentRegistry.GetShield(shieldId);
				if (shield == null || !OfficerMissionLoadoutService.EquipShield(officerState, shieldId))
				{
					return;
				}

				officer.RefreshLoadoutFromState();
				AppendActionLog($"{officer.OfficerName} calibrates {shield.DisplayName}.");
				UpdateSelectedOfficerDisplay();
				RefreshCombatHud();
				_missionUi.ShowOfficerInventory(BuildOfficerInventoryPanelData(officer));
				return;
			}
			case MissionNpcPawn survivor:
			{
				MissionShieldDefinition shield = MissionEquipmentRegistry.GetShield(shieldId);
				if (shield == null || !survivor.EquipShield(shieldId))
				{
					return;
				}

				AppendActionLog($"{survivor.DisplayName} calibrates {shield.DisplayName}.");
				UpdateSelectedOfficerDisplay();
				RefreshCombatHud();
				_missionUi.ShowOfficerInventory(BuildSurvivorInventoryPanelData(survivor));
				return;
			}
		}
	}

	private void OnCombatActionChosen(string actionId)
	{
		if (!_combatActive || _missionGameOver || _enemyTurnInProgress)
		{
			return;
		}

		if (string.Equals(actionId, "move", StringComparison.OrdinalIgnoreCase))
		{
			_selectedCombatActionMode = MissionPlayerCombatActionMode.Move;
		}
		else
		{
			_selectedCombatActionMode = MissionPlayerCombatActionMode.Attack;
		}

		RefreshCombatHud();
	}

	private void OnCombatWeaponSwapRequested(string weaponId)
	{
		OfficerPawn officer = GetActiveCombatOfficer();
		if (!_combatActive || _missionGameOver || _enemyTurnInProgress || officer == null || officer.IsDead || string.IsNullOrWhiteSpace(weaponId))
		{
			return;
		}

		if (!TryGetOfficerState(officer, out OfficerState officerState))
		{
			return;
		}

		if (string.Equals(officer.WeaponId, weaponId, StringComparison.Ordinal))
		{
			RefreshCombatHud();
			return;
		}

		MissionWeaponDefinition weapon = MissionEquipmentRegistry.GetWeapon(weaponId);
		if (weapon == null || !officer.CanSpendActions(CombatAttackActionCost) || !OfficerMissionLoadoutService.EquipWeapon(officerState, weaponId))
		{
			RefreshCombatHud();
			return;
		}

		officer.SpendActions(CombatAttackActionCost);
		officer.RefreshEquippedWeaponFromLoadout();
		_selectedCombatActionMode = MissionPlayerCombatActionMode.Attack;
		string attackMode = weapon.IsMelee ? "melee" : "ranged";
		AppendCombatLog($"{officer.OfficerName} swaps to {weapon.DisplayName.ToLowerInvariant()}, spending {CombatAttackActionCost} AP to ready a {attackMode} attack.");
		RefreshCombatHud();

		if (officer.CurrentActions <= 0)
		{
			EndCurrentCombatTurn();
		}
	}
}

