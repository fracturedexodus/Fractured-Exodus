using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private void PlayAttackEffects(Node2D attacker, Node2D target, MissionAttackProfile attackProfile, CombatDamageResult result)
	{
		if (attacker == null || target == null || attackProfile == null)
		{
			return;
		}

		EnsureCombatEffectLayer();
		if (_combatEffectLayer == null)
		{
			return;
		}

		bool shieldsHit = result?.ShieldDamage > 0;
		bool hullHit = result?.HealthDamage > 0;
		Color attackColor = shieldsHit && !hullHit
			? new Color(0.25f, 0.95f, 1f, 0.95f)
			: new Color(1f, 0.45f, 0.35f, 0.95f);

		if (attackProfile.IsMelee)
		{
			SpawnMeleeSlashEffect(target.GlobalPosition, attackColor);
		}
		else
		{
			SpawnRangedTracerEffect(attacker.GlobalPosition, target.GlobalPosition, attackColor);
		}

		SpawnImpactEffect(target.GlobalPosition, shieldsHit, hullHit);
		SpawnDamageText(target.GlobalPosition, result);
		if (hullHit)
		{
			PlayMissionHitSound();
		}
	}

	private void PlayMissionHitSound()
	{
		if (_hitSfxPlayer == null || _missionHitSound == null)
		{
			return;
		}

		_audioPlaybackService?.TryPlayLoaded(_hitSfxPlayer, _missionHitSound);
	}

	private void PlayPlayerShieldHitSound()
	{
		if (_playerShieldHitSfxPlayer == null || _missionPlayerShieldHitSound == null)
		{
			return;
		}

		_audioPlaybackService?.TryPlayLoaded(_playerShieldHitSfxPlayer, _missionPlayerShieldHitSound);
	}

	private void PlayOfficerLaserFireSound()
	{
		if (_officerLaserFireSfxPlayer == null || _missionOfficerLaserFireSound == null)
		{
			return;
		}

		_audioPlaybackService?.TryPlayLoaded(_officerLaserFireSfxPlayer, _missionOfficerLaserFireSound);
	}

	private static bool ShouldPlayOfficerLaserFireSound(MissionAttackProfile attackProfile)
	{
		if (attackProfile == null || attackProfile.IsMelee)
		{
			return false;
		}

		return attackProfile.WeaponId switch
		{
			"sidearm" => true,
			"heavy_sidearm" => true,
			"defense_pistol" => true,
			"pulse_carbine" => true,
			"pulse_lance" => true,
			_ => false
		};
	}

	private void PlayEnemyLaserFireSound()
	{
		if (_enemyLaserFireSfxPlayer == null || _missionEnemyLaserFireSound == null)
		{
			return;
		}

		_audioPlaybackService?.TryPlayLoaded(_enemyLaserFireSfxPlayer, _missionEnemyLaserFireSound);
	}

	private static bool ShouldPlayEnemyLaserFireSound(MissionAttackProfile attackProfile)
	{
		if (attackProfile == null || attackProfile.IsMelee || string.IsNullOrWhiteSpace(attackProfile.WeaponId))
		{
			return false;
		}

		string weaponId = attackProfile.WeaponId.ToLowerInvariant();
		return weaponId.Contains("laser") || weaponId.Contains("pulse");
	}

	private void SpawnRangedTracerEffect(Vector2 start, Vector2 end, Color color)
	{
		int effectZIndex = GetCombatEffectZIndexForWorldPositions(start, end);
		Line2D beam = new Line2D
		{
			Width = 5f,
			DefaultColor = color,
			ZIndex = effectZIndex
		};
		beam.ZAsRelative = false;
		beam.AddPoint(_combatEffectLayer.ToLocal(start));
		beam.AddPoint(_combatEffectLayer.ToLocal(end));
		_combatEffectLayer.AddChild(beam);

		Tween tween = CreateTween();
		tween.TweenProperty(beam, "modulate:a", 0f, 0.16f);
		tween.Parallel().TweenProperty(beam, "width", 1.5f, 0.16f);
		tween.TweenCallback(Callable.From(beam.QueueFree));
	}

	private void SpawnMeleeSlashEffect(Vector2 targetPosition, Color color)
	{
		Node2D root = new Node2D
		{
			Position = _combatEffectLayer.ToLocal(targetPosition),
			ZIndex = GetCombatEffectZIndexForWorldPosition(targetPosition)
		};
		root.ZAsRelative = false;
		_combatEffectLayer.AddChild(root);

		Line2D slashA = new Line2D
		{
			Width = 6f,
			DefaultColor = color
		};
		slashA.AddPoint(new Vector2(-22f, -16f));
		slashA.AddPoint(new Vector2(24f, 18f));
		root.AddChild(slashA);

		Line2D slashB = new Line2D
		{
			Width = 4f,
			DefaultColor = new Color(color.R, color.G, color.B, 0.72f)
		};
		slashB.AddPoint(new Vector2(-10f, 22f));
		slashB.AddPoint(new Vector2(18f, -20f));
		root.AddChild(slashB);

		Tween tween = CreateTween();
		tween.TweenProperty(root, "scale", new Vector2(1.25f, 1.25f), 0.12f);
		tween.Parallel().TweenProperty(root, "modulate:a", 0f, 0.18f);
		tween.TweenCallback(Callable.From(root.QueueFree));
	}

	private void SpawnImpactEffect(Vector2 targetPosition, bool shieldsHit, bool hullHit)
	{
		Node2D root = new Node2D
		{
			Position = _combatEffectLayer.ToLocal(targetPosition),
			ZIndex = GetCombatEffectZIndexForWorldPosition(targetPosition)
		};
		root.ZAsRelative = false;
		_combatEffectLayer.AddChild(root);

		Polygon2D burst = new Polygon2D
		{
			Color = shieldsHit && !hullHit
				? new Color(0.35f, 0.95f, 1f, 0.34f)
				: new Color(1f, 0.44f, 0.32f, 0.32f),
			Polygon = BuildEffectDiamond(20f, 12f)
		};
		root.AddChild(burst);

		Line2D outline = new Line2D
		{
			Width = 3.5f,
			DefaultColor = shieldsHit && !hullHit
				? new Color(0.55f, 1f, 1f, 0.95f)
				: new Color(1f, 0.72f, 0.48f, 0.95f),
			Closed = true
		};
		foreach (Vector2 point in BuildEffectDiamond(20f, 12f))
		{
			outline.AddPoint(point);
		}
		root.AddChild(outline);

		Tween tween = CreateTween();
		tween.TweenProperty(root, "scale", new Vector2(1.7f, 1.7f), 0.2f);
		tween.Parallel().TweenProperty(root, "modulate:a", 0f, 0.2f);
		tween.TweenCallback(Callable.From(root.QueueFree));
	}

	private void SpawnDamageText(Vector2 targetPosition, CombatDamageResult result)
	{
		if (_combatEffectLayer == null || result == null)
		{
			return;
		}

		if (result.ShieldDamage > 0)
		{
			SpawnFloatingCombatLabel(targetPosition + new Vector2(0f, -38f), $"-{result.ShieldDamage} SHD", new Color(0.35f, 0.95f, 1f, 1f));
		}

		if (result.HealthDamage > 0)
		{
			SpawnFloatingCombatLabel(targetPosition + new Vector2(0f, -16f), $"-{result.HealthDamage} HP", new Color(1f, 0.48f, 0.4f, 1f));
		}
	}

	private void SpawnFloatingCombatLabel(Vector2 worldPosition, string text, Color color)
	{
		Label label = new Label
		{
			Text = text,
			Position = _combatEffectLayer.ToLocal(worldPosition),
			ZIndex = GetCombatEffectZIndexForWorldPosition(worldPosition)
		};
		label.ZAsRelative = false;
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.04f, 0.06f, 0.95f));
		label.AddThemeConstantOverride("outline_size", 4);
		_combatEffectLayer.AddChild(label);

		Tween tween = CreateTween();
		tween.TweenProperty(label, "position:y", label.Position.Y - 28f, 0.42f);
		tween.Parallel().TweenProperty(label, "modulate:a", 0f, 0.42f);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private static Vector2[] BuildEffectDiamond(float halfWidth, float halfHeight)
	{
		return new[]
		{
			new Vector2(0f, -halfHeight),
			new Vector2(halfWidth, 0f),
			new Vector2(0f, halfHeight),
			new Vector2(-halfWidth, 0f)
		};
	}

	private int GetCombatEffectZIndexForWorldPositions(Vector2 a, Vector2 b)
	{
		if (_roomBuilder == null || _isoWorld == null)
		{
			return 240;
		}

		Vector2I cellA = GetBuildCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(a)));
		Vector2I cellB = GetBuildCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(b)));
		return Mathf.Max(
			_roomBuilder.GetCanvasSortOrderForBuildCell(cellA, CombatEffectSortBias),
			_roomBuilder.GetCanvasSortOrderForBuildCell(cellB, CombatEffectSortBias));
	}

	private int GetCombatEffectZIndexForWorldPosition(Vector2 worldPosition)
	{
		if (_roomBuilder == null || _isoWorld == null)
		{
			return 240;
		}

		Vector2I buildCell = GetBuildCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(worldPosition)));
		return _roomBuilder.GetCanvasSortOrderForBuildCell(buildCell, CombatEffectSortBias);
	}

	private bool TryApplyStatusEffect(OfficerPawn target, MissionAttackProfile attackProfile)
	{
		if (target == null || attackProfile == null || string.IsNullOrWhiteSpace(attackProfile.StatusEffectId) || attackProfile.StatusEffectChance <= 0f)
		{
			return false;
		}

		if (_combatRng.Randf() > attackProfile.StatusEffectChance)
		{
			return false;
		}

		return target.TryApplyStatusEffect(attackProfile.StatusEffectId);
	}

	private bool TryApplyStatusEffect(MissionNpcPawn target, MissionAttackProfile attackProfile)
	{
		if (target == null || attackProfile == null || string.IsNullOrWhiteSpace(attackProfile.StatusEffectId) || attackProfile.StatusEffectChance <= 0f)
		{
			return false;
		}

		if (_combatRng.Randf() > attackProfile.StatusEffectChance)
		{
			return false;
		}

		return target.TryApplyStatusEffect(attackProfile.StatusEffectId);
	}

	private MissionNpcPawn GetClosestVisibleEnemy(Vector2I fromCell)
	{
		return GetVisibleAliveHostileEnemies()
			.OrderBy(enemy => GetTileDistance(fromCell, enemy.CurrentCell))
			.FirstOrDefault();
	}

	private OfficerPawn GetClosestLivingOfficer(Vector2I fromCell)
	{
		return GetAliveOfficers()
			.OrderBy(officer => HasClearLineOfSight(fromCell, officer.CurrentCell) ? 0 : 1)
			.ThenBy(officer => GetTileDistance(fromCell, officer.CurrentCell))
			.FirstOrDefault();
	}

	private MissionNpcPawn GetClosestLivingEscortSurvivor(Vector2I fromCell)
	{
		return GetAliveEscortSurvivors()
			.OrderBy(npc => HasClearLineOfSight(fromCell, npc.CurrentCell) ? 0 : 1)
			.ThenBy(npc => GetTileDistance(fromCell, npc.CurrentCell))
			.FirstOrDefault();
	}

	private bool TryGetEnemyTarget(Vector2I fromCell, out OfficerPawn officer, out MissionNpcPawn survivor)
	{
		officer = GetClosestLivingOfficer(fromCell);
		survivor = GetClosestLivingEscortSurvivor(fromCell);
		if (officer == null && survivor == null)
		{
			return false;
		}

		if (officer == null)
		{
			return true;
		}

		if (survivor == null)
		{
			return true;
		}

		int officerLosPenalty = HasClearLineOfSight(fromCell, officer.CurrentCell) ? 0 : 1000;
		int survivorLosPenalty = HasClearLineOfSight(fromCell, survivor.CurrentCell) ? 0 : 1000;
		int officerScore = officerLosPenalty + GetTileDistance(fromCell, officer.CurrentCell);
		int survivorScore = survivorLosPenalty + GetTileDistance(fromCell, survivor.CurrentCell);
		if (officerScore <= survivorScore)
		{
			survivor = null;
		}
		else
		{
			officer = null;
		}

		return true;
	}

	private bool TryGetTraversableMovementPath(
		Vector2I startCell,
		Vector2I targetCell,
		OfficerPawn ignoreOfficer,
		MissionNpcPawn ignoreEnemy,
		IReadOnlyCollection<string> ignoredOfficerIds,
		out List<Vector2I> path)
	{
		return TryGetTraversableMovementPath(startCell, targetCell, ignoreOfficer, ignoreEnemy, ignoredOfficerIds, null, out path);
	}

	private bool TryGetTraversableMovementPath(
		Vector2I startCell,
		Vector2I targetCell,
		OfficerPawn ignoreOfficer,
		MissionNpcPawn ignoreEnemy,
		IReadOnlyCollection<string> ignoredOfficerIds,
		IReadOnlyCollection<string> ignoredSurvivorIds,
		out List<Vector2I> path)
	{
		path = new List<Vector2I>();
		if (_roomBuilder == null)
		{
			return false;
		}

		bool startIsWalkable = _roomBuilder.IsWalkableMovementCell(startCell) && !IsMovementCellBlockedByProp(startCell);
		bool targetIsWalkable = _roomBuilder.IsWalkableMovementCell(targetCell) && !IsMovementCellBlockedByProp(targetCell);
		if (!startIsWalkable || !targetIsWalkable)
		{
			return false;
		}

		if (startCell == targetCell)
		{
			path.Add(startCell);
			return true;
		}

		Queue<Vector2I> frontier = new Queue<Vector2I>();
		Dictionary<Vector2I, Vector2I> cameFrom = new Dictionary<Vector2I, Vector2I>();
		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		frontier.Enqueue(startCell);
		cameFrom[startCell] = startCell;

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (Vector2I direction in directions)
			{
				Vector2I next = current + direction;
				if (cameFrom.ContainsKey(next))
				{
					continue;
				}

				if (!_roomBuilder.IsWalkableMovementCell(next) || IsMovementCellBlockedByProp(next))
				{
					continue;
				}

				if (next != targetCell && IsCellOccupiedByLivingActor(next, ignoreOfficer, ignoreEnemy, ignoredOfficerIds, ignoredSurvivorIds))
				{
					continue;
				}

				if (!_roomBuilder.CanTraverseMovementTransition(current, next))
				{
					continue;
				}

				cameFrom[next] = current;
				if (next == targetCell)
				{
					path = ReconstructMovementPath(cameFrom, startCell, targetCell);
					return true;
				}

				frontier.Enqueue(next);
			}
		}

		return false;
	}

	private static List<Vector2I> ReconstructMovementPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I startCell, Vector2I targetCell)
	{
		List<Vector2I> path = new List<Vector2I>();
		Vector2I current = targetCell;
		path.Add(current);
		while (current != startCell)
		{
			current = cameFrom[current];
			path.Add(current);
		}

		path.Reverse();
		return path;
	}

	private void AppendCombatLog(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}

		_missionUi?.AppendCombatLog(message);
		_missionUi?.AppendActionLog(message);
	}

	private void AppendActionLog(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}

		_missionUi?.AppendActionLog(message);
	}
}

