using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private bool TryHandleCombatAttackClick(OfficerPawn officer)
	{
		if (!_combatActive || officer == null || officer != GetActiveCombatOfficer() || !officer.CanSpendActions(CombatAttackActionCost) || _roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		Vector2I clickedCell = _roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		if (!_visibleCells.Contains(clickedCell))
		{
			return false;
		}

		MissionNpcPawn enemy = GetNpcAtMovementCell(clickedCell);
		if (enemy == null || enemy.IsDead || !enemy.IsHostile)
		{
			return false;
		}

		_focusedEnemy = enemy;
		MissionAttackProfile attackProfile = officer.GetAttackProfile();
		if (CanAttackTarget(officer.CurrentCell, enemy.CurrentCell, attackProfile))
		{
			PerformOfficerAttack(officer, enemy);
			return true;
		}

		Vector2I? approachCell = FindBestCombatApproachCell(officer.CurrentCell, enemy.CurrentCell, attackProfile, officer.CurrentActions, officer, null);
		if (approachCell.HasValue && TryMoveOfficerToCell(officer, approachCell.Value))
		{
			_pendingCombatAttackEnemyId = enemy.NpcId;
			_pendingInteractionOfficerId = string.Empty;
			_pendingNpcId = string.Empty;
			_pendingPropInstanceId = string.Empty;
			_pendingInteractionKey = string.Empty;
		}

		RefreshCombatHud();
		return true;
	}

	private async Task ExecuteEnemyTurnAsync(MissionNpcPawn enemy)
	{
		if (enemy == null || enemy.IsDead || !_combatActive || _missionGameOver)
		{
			return;
		}

		while (enemy.CurrentActions > 0 && !_missionGameOver && _combatActive)
		{
			OfficerPawn targetOfficer = null;
			MissionNpcPawn targetSurvivor = null;
			if (!TryGetEnemyTarget(enemy.CurrentCell, out targetOfficer, out targetSurvivor))
			{
				return;
			}

			Vector2I targetCell = targetOfficer != null ? targetOfficer.CurrentCell : targetSurvivor.CurrentCell;
			MissionAttackProfile attackProfile = enemy.GetAttackProfile();
			if (CanAttackTarget(enemy.CurrentCell, targetCell, attackProfile))
			{
				if (targetOfficer != null)
				{
					PerformEnemyAttack(enemy, targetOfficer);
				}
				else
				{
					PerformEnemyAttack(enemy, targetSurvivor);
				}

				RefreshCombatHud();
				if (_missionGameOver || !_combatActive)
				{
					return;
				}

				await ToSignal(GetTree().CreateTimer(0.28f), SceneTreeTimer.SignalName.Timeout);
				continue;
			}

			bool moved = await TryMoveEnemyTowardTargetAsync(enemy, targetCell);
			RefreshCombatHud();
			if (!moved)
			{
				return;
			}

			await ToSignal(GetTree().CreateTimer(0.22f), SceneTreeTimer.SignalName.Timeout);
			attackProfile = enemy.GetAttackProfile();
			targetCell = targetOfficer != null ? targetOfficer.CurrentCell : targetSurvivor.CurrentCell;
			if (enemy.CurrentActions > 0 && CanAttackTarget(enemy.CurrentCell, targetCell, attackProfile))
			{
				if (targetOfficer != null)
				{
					PerformEnemyAttack(enemy, targetOfficer);
				}
				else
				{
					PerformEnemyAttack(enemy, targetSurvivor);
				}

				RefreshCombatHud();
				if (_missionGameOver || !_combatActive)
				{
					return;
				}

				await ToSignal(GetTree().CreateTimer(0.28f), SceneTreeTimer.SignalName.Timeout);
			}
			else
			{
				return;
			}
		}
	}

	private async Task ExecuteEscortTurnAsync(MissionNpcPawn survivor)
	{
		if (survivor == null || survivor.IsDead || survivor.IsExtracted || !_combatActive || _missionGameOver)
		{
			return;
		}

		while (survivor.CurrentActions > 0 && !_missionGameOver && _combatActive && !survivor.IsExtracted)
		{
			if (TryExtractEscortSurvivor(survivor))
			{
				return;
			}

			bool moved = TryAdvanceEscortSurvivor(survivor, true);
			if (!moved)
			{
				return;
			}

			await ToSignal(survivor, MissionNpcPawn.SignalName.ReachedCell);
			if (TryExtractEscortSurvivor(survivor))
			{
				return;
			}
		}
	}

	private async Task<bool> TryMoveEnemyTowardTargetAsync(MissionNpcPawn enemy, Vector2I targetCell)
	{
		if (enemy == null || _roomBuilder == null || !enemy.CanSpendActions(1))
		{
			return false;
		}

		Vector2I? approachCell = FindBestCombatApproachCell(enemy.CurrentCell, targetCell, enemy.GetAttackProfile(), enemy.CurrentActions, null, enemy);
		if (!approachCell.HasValue || !TryGetTraversableMovementPath(enemy.CurrentCell, approachCell.Value, null, enemy, null, out List<Vector2I> pathCells) || pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells
			.Skip(1)
			.TakeWhile(cell => !IsCellOccupiedByLivingActor(cell, null, enemy))
			.ToList();
		if (steppedCells.Count == 0)
		{
			return false;
		}

		int maxMovementSteps = GetMaxMovementStepsForActions(enemy.CurrentActions);
		steppedCells = steppedCells.Take(maxMovementSteps).ToList();
		int moveCost = GetMovementCostForPathSteps(steppedCells.Count);
		if (!enemy.CanSpendActions(moveCost))
		{
			return false;
		}

		Vector2I destinationCell = steppedCells[^1];
		enemy.SpendActions(moveCost);
		AppendCombatLog($"{enemy.DisplayName} pushes {moveCost} cell{(moveCost == 1 ? string.Empty : "s")} toward the away team, closing with {enemy.WeaponName.ToLowerInvariant()} ready.");
		List<Vector2> pathPoints = steppedCells
			.Select(GetMovementCellGlobalPosition)
			.ToList();
		enemy.MoveAlongPath(pathPoints, steppedCells, destinationCell);
		await ToSignal(enemy, MissionNpcPawn.SignalName.ReachedCell);
		return true;
	}

	private Vector2I? FindBestCombatApproachCell(
		Vector2I startCell,
		Vector2I targetCell,
		MissionAttackProfile attackProfile,
		int maxSteps,
		OfficerPawn movingOfficer,
		MissionNpcPawn movingEnemy)
	{
		if (_roomBuilder == null || attackProfile == null || maxSteps <= 0)
		{
			return null;
		}

		List<(Vector2I Cell, int PathCost, int TargetDistance)> candidates = new List<(Vector2I, int, int)>();
		foreach (Vector2I candidate in _roomBuilder.GetReachableMovementCells(targetCell, attackProfile.Range))
		{
			if (candidate == targetCell || !_roomBuilder.IsWalkableMovementCell(candidate) || IsMovementCellBlockedByProp(candidate) || IsCellOccupiedByLivingActor(candidate, movingOfficer, movingEnemy))
			{
				continue;
			}

			if (!TryGetTraversableMovementPath(startCell, candidate, movingOfficer, movingEnemy, null, out List<Vector2I> pathCells))
			{
				continue;
			}

			int pathLength = Math.Max(0, pathCells.Count - 1);
			int pathCost = GetMovementCostForPathSteps(pathLength);
			if (pathLength <= 0 || pathCost > maxSteps)
			{
				continue;
			}

			if (!CanAttackTarget(candidate, targetCell, attackProfile))
			{
				continue;
			}

			candidates.Add((candidate, pathCost, GetTileDistance(candidate, targetCell)));
		}

		if (candidates.Count == 0)
		{
			return null;
		}

		return candidates
			.OrderBy(candidate => candidate.PathCost)
			.ThenBy(candidate => candidate.TargetDistance)
			.Select(candidate => (Vector2I?)candidate.Cell)
			.FirstOrDefault();
	}

	private void PerformOfficerAttack(OfficerPawn officer, MissionNpcPawn enemy)
	{
		if (!_combatActive || officer == null || enemy == null || officer.IsDead || enemy.IsDead || !officer.CanSpendActions(CombatAttackActionCost))
		{
			return;
		}

		MissionAttackProfile attackProfile = officer.GetAttackProfile();
		if (!CanAttackTarget(officer.CurrentCell, enemy.CurrentCell, attackProfile))
		{
			return;
		}

		FaceNodeToward(officer, enemy.GlobalPosition, 0.45f);
		officer.SpendActions(CombatAttackActionCost);
		PlayNodeAttackRecoil(officer, enemy.GlobalPosition);
		if (ShouldPlayOfficerLaserFireSound(attackProfile))
		{
			PlayOfficerLaserFireSound();
		}
		int damage = _combatRng.RandiRange(attackProfile.MinDamage, attackProfile.MaxDamage);
		CombatDamageResult result = ApplyAttackProfileToTarget(enemy, damage, attackProfile);
		PlayNodeHitReaction(enemy, officer.GlobalPosition, result);
		string statusText = TryApplyStatusEffect(enemy, attackProfile)
			? $" {enemy.DisplayName} is afflicted with {attackProfile.StatusEffectId}."
			: string.Empty;
		PlayAttackEffects(officer, enemy, attackProfile, result);
		string attackLog = attackProfile.IsMelee
			? $"{officer.OfficerName} strikes {enemy.DisplayName} with {attackProfile.WeaponName.ToLowerInvariant()}"
			: $"{officer.OfficerName} fires {attackProfile.WeaponName.ToLowerInvariant()} at {enemy.DisplayName}";
		AppendCombatLog(BuildDamageLog(
			attackLog,
			result,
			statusText));
		_focusedEnemy = enemy;
		_pendingCombatAttackEnemyId = string.Empty;
		ReindexMissionNpcCells();
		UpdateFogOfWar();
		RefreshCombatHud();

		if (!GetEngagedHostileEnemies().Any())
		{
			EndMissionCombat();
			return;
		}

		if (officer.CurrentActions <= 0)
		{
			EndCurrentCombatTurn();
		}
	}

	private void PerformEnemyAttack(MissionNpcPawn enemy, OfficerPawn officer)
	{
		if (!_combatActive || enemy == null || officer == null || enemy.IsDead || officer.IsDead || !enemy.CanSpendActions(CombatAttackActionCost))
		{
			return;
		}

		MissionAttackProfile attackProfile = enemy.GetAttackProfile();
		if (!CanAttackTarget(enemy.CurrentCell, officer.CurrentCell, attackProfile))
		{
			return;
		}

		FaceNodeToward(enemy, officer.GlobalPosition, 0.45f);
		enemy.SpendActions(CombatAttackActionCost);
		PlayNodeAttackRecoil(enemy, officer.GlobalPosition);
		if (ShouldPlayEnemyLaserFireSound(attackProfile))
		{
			PlayEnemyLaserFireSound();
		}
		int damage = _combatRng.RandiRange(attackProfile.MinDamage, attackProfile.MaxDamage);
		CombatDamageResult result = ApplyAttackProfileToTarget(officer, damage, attackProfile);
		PlayNodeHitReaction(officer, enemy.GlobalPosition, result);
		string statusText = TryApplyStatusEffect(officer, attackProfile)
			? $" {officer.OfficerName} is afflicted with {attackProfile.StatusEffectId}."
			: string.Empty;
		if (result?.ShieldDamage > 0)
		{
			PlayPlayerShieldHitSound();
		}
		PlayAttackEffects(enemy, officer, attackProfile, result);
		AppendCombatLog(BuildDamageLog(
			$"{enemy.DisplayName} answers with {attackProfile.WeaponName.ToLowerInvariant()}, hitting {officer.OfficerName}",
			result,
			statusText));
		UpdateFogOfWar();
		RefreshCombatHud();
		if (!GetAliveOfficers().Any())
		{
			HandleMissionGameOver();
		}
	}

	private void PerformEnemyAttack(MissionNpcPawn enemy, MissionNpcPawn survivor)
	{
		if (!_combatActive || enemy == null || survivor == null || enemy.IsDead || survivor.IsDead || survivor.IsExtracted || !enemy.CanSpendActions(CombatAttackActionCost))
		{
			return;
		}

		MissionAttackProfile attackProfile = enemy.GetAttackProfile();
		if (!CanAttackTarget(enemy.CurrentCell, survivor.CurrentCell, attackProfile))
		{
			return;
		}

		FaceNodeToward(enemy, survivor.GlobalPosition, 0.45f);
		enemy.SpendActions(CombatAttackActionCost);
		PlayNodeAttackRecoil(enemy, survivor.GlobalPosition);
		if (ShouldPlayEnemyLaserFireSound(attackProfile))
		{
			PlayEnemyLaserFireSound();
		}

		int damage = _combatRng.RandiRange(attackProfile.MinDamage, attackProfile.MaxDamage);
		CombatDamageResult result = ApplyAttackProfileToTarget(survivor, damage, attackProfile);
		PlayNodeHitReaction(survivor, enemy.GlobalPosition, result);
		string statusText = TryApplyStatusEffect(survivor, attackProfile)
			? $" {survivor.DisplayName} is afflicted with {attackProfile.StatusEffectId}."
			: string.Empty;
		PlayAttackEffects(enemy, survivor, attackProfile, result);
		AppendCombatLog(BuildDamageLog(
			$"{enemy.DisplayName} answers with {attackProfile.WeaponName.ToLowerInvariant()}, hitting {survivor.DisplayName}",
			result,
			statusText));
		UpdateFogOfWar();
		RefreshCombatHud();
	}

	private static string BuildDamageLog(string actionText, CombatDamageResult result, string suffix = "")
	{
		if (result == null)
		{
			return $"{actionText}.{suffix}";
		}

		List<string> impactParts = new List<string>();
		if (result.ShieldDamage > 0)
		{
			impactParts.Add($"stripping {result.ShieldDamage} shield");
		}

		if (result.HealthDamage > 0)
		{
			impactParts.Add($"dealing {result.HealthDamage} health damage");
		}

		if (impactParts.Count == 0)
		{
			impactParts.Add("but the shot disperses harmlessly");
		}

		string impactText = impactParts.Count == 1
			? impactParts[0]
			: $"{impactParts[0]} and {impactParts[1]}";
		return $"{actionText}, {impactText}, leaving {result.RemainingShields} shield and {result.RemainingHealth} HP.{suffix}";
	}

	private CombatDamageResult ApplyAttackProfileToTarget(MissionNpcPawn target, int rolledDamage, MissionAttackProfile attackProfile)
	{
		if (target == null || attackProfile == null)
		{
			return null;
		}

		return target.ApplyDamage(rolledDamage, attackProfile.BonusShieldDamage, 0);
	}

	private CombatDamageResult ApplyAttackProfileToTarget(OfficerPawn target, int rolledDamage, MissionAttackProfile attackProfile)
	{
		if (target == null || attackProfile == null)
		{
			return null;
		}

		return target.ApplyDamage(rolledDamage, attackProfile.BonusShieldDamage, 0);
	}

	private static void FaceNodeToward(Node2D actor, Vector2 targetGlobalPosition, float holdSeconds = 0.3f)
	{
		switch (actor)
		{
			case OfficerPawn officer:
				officer.FaceToward(targetGlobalPosition, holdSeconds);
				break;
			case MissionNpcPawn npc:
				npc.FaceToward(targetGlobalPosition, holdSeconds);
				break;
		}
	}

	private static void PlayNodeAttackRecoil(Node2D actor, Vector2 targetGlobalPosition)
	{
		switch (actor)
		{
			case OfficerPawn officer:
				officer.PlayAttackRecoil(targetGlobalPosition);
				break;
			case MissionNpcPawn npc:
				npc.PlayAttackRecoil(targetGlobalPosition);
				break;
		}
	}

	private static void PlayNodeHitReaction(Node2D actor, Vector2 sourceGlobalPosition, CombatDamageResult result)
	{
		if (actor == null || result == null)
		{
			return;
		}

		bool shieldHit = result.ShieldDamage > 0;
		bool hullHit = result.HealthDamage > 0;
		switch (actor)
		{
			case OfficerPawn officer:
				officer.PlayHitReaction(sourceGlobalPosition, shieldHit, hullHit);
				break;
			case MissionNpcPawn npc:
				npc.PlayHitReaction(sourceGlobalPosition, shieldHit, hullHit);
				break;
		}
	}
}

