using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private Vector2 GetCellGlobalPosition(Vector2I cell)
	{
		Vector2 localPosition = _roomBuilder.GetCellWorldPosition(cell.X, cell.Y);
		return _isoWorld.ToGlobal(localPosition);
	}

	private Vector2 GetMovementCellGlobalPosition(Vector2I cell)
	{
		Vector2 localPosition = _roomBuilder.GetMovementCellWorldPosition(cell.X, cell.Y);
		return _isoWorld.ToGlobal(localPosition);
	}

	private Vector2I GetBuildCell(Vector2I movementCell)
	{
		return _roomBuilder?.GetBuildCellForMovementCell(movementCell) ?? movementCell;
	}

	private Vector2I GetMovementCell(Vector2I buildCell)
	{
		return _roomBuilder?.GetMovementCellForBuildCell(buildCell) ?? buildCell;
	}

	private static string FormatMovementGridCoordinate(Vector2I movementCell)
	{
		return $"{movementCell.X},{movementCell.Y}";
	}

	private int GetMovementOverlayZIndex(Vector2I movementCell, int bias = 0)
	{
		if (_roomBuilder == null)
		{
			return bias;
		}

		Vector2I buildCell = GetBuildCell(movementCell);
		return _roomBuilder.GetCanvasSortOrderForBuildCell(
			buildCell,
			_roomBuilder.MovementSubdivision + bias);
	}

	private Vector2I ResolveMovementTargetCell(Vector2I requestedCell)
	{
		if (_roomBuilder == null)
		{
			return requestedCell;
		}

		if (_roomBuilder.IsWalkableMovementCell(requestedCell))
		{
			return requestedCell;
		}

		Vector2I buildCell = GetBuildCell(requestedCell);
		if (!_roomBuilder.IsWalkableCell(buildCell))
		{
			return requestedCell;
		}

		Vector2I? normalizedCell = _roomBuilder.GetMovementCellsForBuildCell(buildCell)
			.Where(cell => _roomBuilder.IsWalkableMovementCell(cell))
			.OrderBy(cell => GetMovementStepDistance(cell, requestedCell))
			.ThenBy(cell => GetMovementStepDistance(cell, GetMovementCell(buildCell)))
			.Cast<Vector2I?>()
			.FirstOrDefault();
		return normalizedCell ?? requestedCell;
	}

	private bool IsMovementCellBlockedByProp(Vector2I movementCell, string ignoredPropInstanceId = null)
	{
		Vector2I buildCell = GetBuildCell(movementCell);
		if (_blockedStaticPropCells.Contains(buildCell))
		{
			return true;
		}

		if (_missionPropsByCell.TryGetValue(buildCell, out MissionProp prop)
			&& prop != null
			&& prop.Definition?.BlocksMovement != false
			&& (string.IsNullOrWhiteSpace(ignoredPropInstanceId) || prop.PropInstanceId != ignoredPropInstanceId))
		{
			return true;
		}

		return false;
	}

	private bool TryGetMissionPropAtMovementCell(Vector2I movementCell, out MissionProp prop)
	{
		return _missionPropsByCell.TryGetValue(GetBuildCell(movementCell), out prop) && prop != null && !prop.IsConsumed;
	}

	private MissionProp SpawnEnemyLootDropProp(Vector2I buildCell, IReadOnlyCollection<string> itemIds, string propInstanceId = null)
	{
		if (_isoWorld == null || _roomBuilder == null)
		{
			return null;
		}

		List<string> rewardItemIds = (itemIds ?? Array.Empty<string>())
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.ToList();
		if (rewardItemIds.Count == 0)
		{
			return null;
		}

		Node2D runtimePropLayer = _isoWorld.GetNodeOrNull<Node2D>("RuntimePropLayer");
		if (runtimePropLayer == null)
		{
			runtimePropLayer = new Node2D
			{
				Name = "RuntimePropLayer",
				ZIndex = 6
			};
			_isoWorld.AddChild(runtimePropLayer);
		}

		PackedScene propScene = GD.Load<PackedScene>(EnemyLootDropScenePath);
		if (propScene == null)
		{
			return null;
		}

		if (_missionPropsByCell.TryGetValue(buildCell, out MissionProp existingProp) && existingProp != null)
		{
			if (IsEnemyLootDropProp(existingProp))
			{
				foreach (string itemId in rewardItemIds)
				{
					if (!string.IsNullOrWhiteSpace(itemId))
					{
						existingProp.Definition.RewardOfficerItemIds.Add(itemId);
					}
				}

				existingProp.Definition.Description = BuildEnemyLootDropDescription(existingProp.Definition.RewardOfficerItemIds);
				return existingProp;
			}

			return null;
		}

		MissionProp prop = propScene.Instantiate<MissionProp>();
		if (prop == null)
		{
			return null;
		}

		prop.Definition = BuildEnemyLootDropDefinition(rewardItemIds);
		prop.Name = $"{EnemyLootDropPropId}_{buildCell.X}_{buildCell.Y}";
		prop.PropInstanceId = string.IsNullOrWhiteSpace(propInstanceId)
			? $"{EnemyLootDropPropId}:{buildCell.X},{buildCell.Y}:{Guid.NewGuid():N}"
			: propInstanceId;
		prop.Position = _roomBuilder.GetCellWorldPosition(buildCell.X, buildCell.Y);
		runtimePropLayer.AddChild(prop);
		_missionPropsByCell[buildCell] = prop;
		MarkMissionDepthSortingDirty();
		return prop;
	}

	private static PropDefinition BuildEnemyLootDropDefinition(IReadOnlyCollection<string> itemIds)
	{
		PropDefinition definition = new PropDefinition
		{
			PropId = EnemyLootDropPropId,
			DisplayName = "Dropped Supplies",
			Description = BuildEnemyLootDropDescription(itemIds),
			InteractionType = PropInteractionType.Loot,
			ScenePath = EnemyLootDropScenePath,
			SpriteTexturePath = EnemyLootDropSpritePath,
			InteractionRange = 0,
			OneShot = true,
			HideWhenConsumed = true,
			BlocksMovement = false,
			SuccessMessage = "Recovered supplies secured."
		};
		foreach (string itemId in itemIds ?? Array.Empty<string>())
		{
			if (!string.IsNullOrWhiteSpace(itemId))
			{
				definition.RewardOfficerItemIds.Add(itemId);
			}
		}

		return definition;
	}

	private static string BuildEnemyLootDropDescription(IEnumerable<string> itemIds)
	{
		List<string> displayNames = (itemIds ?? Array.Empty<string>())
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.Select(itemId => CampaignItemRegistry.GetItem(itemId)?.DisplayName ?? itemId)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
			.ToList();
		return displayNames.Count == 0
			? "Recovered gear and mission salvage."
			: $"Recovered gear: {string.Join(", ", displayNames)}.";
	}

	private static bool IsEnemyLootDropProp(MissionProp prop)
	{
		return string.Equals(prop?.Definition?.PropId, EnemyLootDropPropId, StringComparison.Ordinal);
	}

	private static bool IsEnemyLootDropPropInstanceId(string propInstanceId)
	{
		return !string.IsNullOrWhiteSpace(propInstanceId)
			&& propInstanceId.StartsWith($"{EnemyLootDropPropId}:", StringComparison.Ordinal);
	}

	private static IReadOnlyList<string> GetLootDropRewardItemIds(MissionProp prop)
	{
		return IsEnemyLootDropProp(prop)
			? (prop.Definition?.RewardOfficerItemIds ?? new Godot.Collections.Array<string>())
				.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
				.ToList()
			: Array.Empty<string>();
	}

	private bool TryCollectLootAtCurrentCell(OfficerPawn officer)
	{
		if (officer == null || !TryGetOfficerState(officer, out OfficerState officerState))
		{
			return false;
		}

		List<string> unlockedEquipmentNames = new List<string>();
		bool collected = TryCollectLootAtBuildCell(
			GetBuildCell(officer.CurrentCell),
			officer.OfficerName,
			items =>
			{
				officerState.PersonalInventoryItemIDs ??= new List<string>();
				officerState.PersonalInventoryItemIDs.AddRange(items);
				unlockedEquipmentNames = OfficerMissionLoadoutService.ApplyCampaignItemUnlocks(officerState).ToList();
			});
		if (!collected)
		{
			return false;
		}

		if (unlockedEquipmentNames.Count > 0)
		{
			officer.RefreshLoadoutFromState();
			string unlockedSummary = string.Join(", ", unlockedEquipmentNames);
			string message = $"{officer.OfficerName} salvages and readies {unlockedSummary}.";
			if (_combatActive)
			{
				AppendCombatLog(message);
			}
			else
			{
				AppendActionLog(message);
			}
		}

		return true;
	}

	private bool TryCollectLootAtCurrentCell(MissionNpcPawn survivor)
	{
		if (!IsActiveEscortSurvivor(survivor))
		{
			return false;
		}

		List<string> unlockedEquipmentNames = new List<string>();
		bool collected = TryCollectLootAtBuildCell(
			GetBuildCell(survivor.CurrentCell),
			survivor.DisplayName,
			items =>
			{
				survivor.PersonalInventoryItemIDs.AddRange(items);
				unlockedEquipmentNames = survivor.ApplyCampaignItemUnlocks().ToList();
			});
		if (!collected)
		{
			return false;
		}

		if (unlockedEquipmentNames.Count > 0)
		{
			survivor.RefreshLoadout();
			string unlockedSummary = string.Join(", ", unlockedEquipmentNames);
			string message = $"{survivor.DisplayName} salvages and readies {unlockedSummary}.";
			if (_combatActive)
			{
				AppendCombatLog(message);
			}
			else
			{
				AppendActionLog(message);
			}
		}

		return true;
	}

	private bool TryCollectLootAtBuildCell(Vector2I buildCell, string collectorName, Action<List<string>> assignItems)
	{
		if (!_missionPropsByCell.TryGetValue(buildCell, out MissionProp prop) || !IsEnemyLootDropProp(prop) || prop.IsConsumed)
		{
			return false;
		}

		List<string> itemIds = GetLootDropRewardItemIds(prop).ToList();
		if (itemIds.Count == 0)
		{
			prop.ApplySavedConsumptionState(true);
			MarkMissionDepthSortingDirty();
			return false;
		}

		assignItems?.Invoke(itemIds);
		prop.ApplySavedConsumptionState(true);
		if (_missionPropsByCell.TryGetValue(buildCell, out MissionProp currentProp) && currentProp == prop)
		{
			_missionPropsByCell.Remove(buildCell);
		}
		MarkMissionDepthSortingDirty();

		AppendLootCollectionLog(collectorName, itemIds);
		UpdateSelectedOfficerDisplay();
		UpdateMissionCompletionActions();
		RefreshCombatHud();
		return true;
	}

	private void AppendLootCollectionLog(string collectorName, IReadOnlyCollection<string> itemIds)
	{
		List<string> displayNames = (itemIds ?? Array.Empty<string>())
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.Select(itemId => CampaignItemRegistry.GetItem(itemId)?.DisplayName ?? itemId)
			.ToList();
		if (displayNames.Count == 0)
		{
			return;
		}

		string itemSummary = string.Join(", ", displayNames);
		string message = $"{collectorName} recovers {itemSummary} from the battlefield.";
		if (_combatActive)
		{
			AppendCombatLog(message);
		}
		else
		{
			AppendActionLog(message);
		}
	}

	private bool TryGetStaticInteractionAtMovementCell(Vector2I movementCell, out MissionRoomBuilder.MarkerPlacement interaction)
	{
		interaction = _roomBuilder?.GetInteractPlacementsAtCell(GetBuildCell(movementCell))
			.Where(placement =>
				(placement.LogicRole != "door" && string.Equals(placement.TriggerMode, "interact", System.StringComparison.OrdinalIgnoreCase))
				|| placement.LogicRole == "terminal")
			.Where(placement => !HasRuntimePropForPlacement(placement))
			.OrderByDescending(placement => placement.LogicRole == "terminal")
			.FirstOrDefault();
		return interaction != null;
	}

	private int GetMovementSubdivision()
	{
		return _roomBuilder?.MovementSubdivision ?? 1;
	}

	private int GetMovementStepDistance(Vector2I a, Vector2I b)
	{
		return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
	}

	private int GetTileDistance(Vector2I a, Vector2I b)
	{
		return GetMovementStepDistance(a, b);
	}

	private int GetInteractionDistanceToBuildCell(Vector2I officerCell, Vector2I buildCell)
	{
		if (_roomBuilder == null)
		{
			return GetTileDistance(officerCell, GetMovementCell(buildCell));
		}

		int minimumDistance = int.MaxValue;
		foreach (Vector2I movementCell in _roomBuilder.GetMovementCellsForBuildCell(buildCell))
		{
			if (!_roomBuilder.IsWalkableMovementCell(movementCell))
			{
				continue;
			}

			minimumDistance = Mathf.Min(minimumDistance, GetTileDistance(officerCell, movementCell));
		}

		return minimumDistance == int.MaxValue
			? GetTileDistance(officerCell, GetMovementCell(buildCell))
			: minimumDistance;
	}

	private int GetMovementCostForPathSteps(int stepCount)
	{
		return Mathf.Max(0, stepCount);
	}

	private int GetMaxMovementStepsForActions(int actions)
	{
		return Mathf.Max(0, actions);
	}

	private bool CanAttackTarget(Vector2I attackerCell, Vector2I targetCell, MissionAttackProfile attackProfile)
	{
		if (attackProfile == null || GetTileDistance(attackerCell, targetCell) > attackProfile.Range)
		{
			return false;
		}

		return HasClearLineOfSight(attackerCell, targetCell);
	}

	private bool HasClearLineOfSight(Vector2I fromMovementCell, Vector2I toMovementCell, string ignoredPropInstanceId = null)
	{
		if (_roomBuilder == null)
		{
			return true;
		}

		Vector2I fromBuildCell = GetBuildCell(fromMovementCell);
		Vector2I toBuildCell = GetBuildCell(toMovementCell);
		if (fromBuildCell == toBuildCell)
		{
			return true;
		}

		Vector2 start = _roomBuilder.GetLineOfSightGridPosition(fromMovementCell);
		Vector2 end = _roomBuilder.GetLineOfSightGridPosition(toMovementCell);
		Vector2 direction = end - start;
		int currentX = Mathf.FloorToInt(start.X);
		int currentY = Mathf.FloorToInt(start.Y);
		int targetX = Mathf.FloorToInt(end.X);
		int targetY = Mathf.FloorToInt(end.Y);
		int stepX = direction.X > 0f ? 1 : direction.X < 0f ? -1 : 0;
		int stepY = direction.Y > 0f ? 1 : direction.Y < 0f ? -1 : 0;
		float tDeltaX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs(1f / direction.X);
		float tDeltaY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs(1f / direction.Y);
		float nextBoundaryX = stepX > 0 ? currentX + 1f : currentX;
		float nextBoundaryY = stepY > 0 ? currentY + 1f : currentY;
		float tMaxX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs((nextBoundaryX - start.X) / direction.X);
		float tMaxY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs((nextBoundaryY - start.Y) / direction.Y);

		while (currentX != targetX || currentY != targetY)
		{
			Vector2I currentCell = new Vector2I(currentX, currentY);
			if (Mathf.IsEqualApprox(tMaxX, tMaxY))
			{
				Vector2I horizontalCell = new Vector2I(currentX + stepX, currentY);
				Vector2I verticalCell = new Vector2I(currentX, currentY + stepY);
				Vector2I diagonalCell = new Vector2I(currentX + stepX, currentY + stepY);

				if (IsLineOfSightTransitionBlocked(currentCell, horizontalCell)
					|| IsLineOfSightTransitionBlocked(currentCell, verticalCell)
					|| IsLineOfSightBuildCellBlocked(horizontalCell, fromBuildCell, toBuildCell, ignoredPropInstanceId)
					|| IsLineOfSightBuildCellBlocked(verticalCell, fromBuildCell, toBuildCell, ignoredPropInstanceId)
					|| IsLineOfSightTransitionBlocked(horizontalCell, diagonalCell)
					|| IsLineOfSightTransitionBlocked(verticalCell, diagonalCell)
					|| IsLineOfSightBuildCellBlocked(diagonalCell, fromBuildCell, toBuildCell, ignoredPropInstanceId))
				{
					return false;
				}

				currentX += stepX;
				currentY += stepY;
				tMaxX += tDeltaX;
				tMaxY += tDeltaY;
				continue;
			}

			Vector2I nextCell;
			if (tMaxX < tMaxY)
			{
				nextCell = new Vector2I(currentX + stepX, currentY);
				tMaxX += tDeltaX;
			}
			else
			{
				nextCell = new Vector2I(currentX, currentY + stepY);
				tMaxY += tDeltaY;
			}

			if (IsLineOfSightTransitionBlocked(currentCell, nextCell)
				|| IsLineOfSightBuildCellBlocked(nextCell, fromBuildCell, toBuildCell, ignoredPropInstanceId))
			{
				return false;
			}

			currentX = nextCell.X;
			currentY = nextCell.Y;
		}

		return true;
	}

	private bool IsLineOfSightTransitionBlocked(Vector2I fromBuildCell, Vector2I toBuildCell)
	{
		return _roomBuilder != null && _roomBuilder.IsLineOfSightBuildTransitionBlocked(fromBuildCell, toBuildCell);
	}

	private bool IsLineOfSightBuildCellBlocked(Vector2I buildCell, Vector2I startBuildCell, Vector2I targetBuildCell, string ignoredPropInstanceId)
	{
		if (buildCell == startBuildCell || buildCell == targetBuildCell)
		{
			return false;
		}

		if (_blockedStaticPropCells.Contains(buildCell))
		{
			return true;
		}

		return _missionPropsByCell.TryGetValue(buildCell, out MissionProp prop)
			&& prop != null
			&& !prop.IsConsumed
			&& (string.IsNullOrWhiteSpace(ignoredPropInstanceId) || prop.PropInstanceId != ignoredPropInstanceId);
	}

	private bool TrySelectControllableUnitAtMouse(Vector2I clickedMovementCell)
	{
		MissionNpcPawn activeCombatSurvivor = GetActiveCombatEscortSurvivor();
		if (_combatActive && activeCombatSurvivor == null)
		{
			return false;
		}

		Vector2 mousePosition = GetGlobalMousePosition();
		if (activeCombatSurvivor != null)
		{
			Rect2 survivorBounds = new Rect2(activeCombatSurvivor.GlobalPosition + new Vector2(-52f, -72f), new Vector2(104f, 120f));
			if (!survivorBounds.HasPoint(mousePosition))
			{
				return false;
			}

			if (ShouldTreatClickAsMovementOrder(activeCombatSurvivor, clickedMovementCell))
			{
				return false;
			}

			SelectEscortSurvivor(activeCombatSurvivor);
			return true;
		}

		for (int i = 0; i < _officerPawns.Count; i++)
		{
			OfficerPawn pawn = _officerPawns[i];
			if (pawn == null || pawn.IsDead)
			{
				continue;
			}

			Rect2 bounds = new Rect2(pawn.GlobalPosition + new Vector2(-52f, -72f), new Vector2(104f, 120f));
			if (!bounds.HasPoint(mousePosition))
			{
				continue;
			}

			if (ShouldTreatClickAsMovementOrder(pawn, clickedMovementCell))
			{
				return false;
			}

			SelectOfficer(i);
			return true;
		}

		foreach (MissionNpcPawn survivor in GetAliveEscortSurvivors())
		{
			Rect2 bounds = new Rect2(survivor.GlobalPosition + new Vector2(-52f, -72f), new Vector2(104f, 120f));
			if (!bounds.HasPoint(mousePosition))
			{
				continue;
			}

			if (ShouldTreatClickAsMovementOrder(survivor, clickedMovementCell))
			{
				return false;
			}

			SelectEscortSurvivor(survivor);
			return true;
		}

		return false;
	}

	private bool ShouldTreatClickAsMovementOrder(object clickedUnit, Vector2I clickedMovementCell)
	{
		if (_roomBuilder == null || !_roomBuilder.IsWalkableMovementCell(clickedMovementCell))
		{
			return false;
		}

		if (_combatActive && !_visibleCells.Contains(clickedMovementCell))
		{
			return false;
		}

		List<object> selectedUnits = GetSelectedFriendlyUnits();
		if (selectedUnits.Count == 0 || !selectedUnits.Contains(clickedUnit))
		{
			return false;
		}

		Vector2I currentCell = clickedUnit switch
		{
			OfficerPawn officer => officer.CurrentCell,
			MissionNpcPawn survivor => survivor.CurrentCell,
			_ => Vector2I.Zero
		};

		return currentCell != clickedMovementCell;
	}

	private void CheckEnterTriggers(OfficerPawn officer)
	{
		if (officer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (MissionRoomBuilder.MarkerPlacement marker in _roomBuilder.GetMarkerPlacements())
		{
			if (!string.Equals(marker.TriggerMode, "enter", System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (marker.Cell != GetBuildCell(officer.CurrentCell))
			{
				continue;
			}

			if (!string.IsNullOrEmpty(marker.RequiredFlag) && (_globalData?.StoryFlags?.Contains(marker.RequiredFlag) != true))
			{
				continue;
			}

			string triggerKey = BuildInteractionKey(marker);
			if (marker.OneShot && _consumedTriggerKeys.Contains(triggerKey))
			{
				continue;
			}

			if (!string.IsNullOrEmpty(marker.SetFlag) && _globalData != null && !_globalData.StoryFlags.Contains(marker.SetFlag))
			{
				_globalData.StoryFlags.Add(marker.SetFlag);
				UpdateMissionCompletionActions();
			}

			if (marker.OneShot)
			{
				_consumedTriggerKeys.Add(triggerKey);
			}

			if (marker.MarkerId == "evac_zone")
			{
				UpdateMissionCompletionActions();
				break;
			}

			if (marker.MarkerId == "trigger_dialogue" && _dialogueUi != null && !_dialogueUi.IsConversationOpen)
			{
				_dialogueUi.StartConversation(
					ResolveDialogueTargetId(marker),
					officer.OfficerName,
					officer.PortraitPath,
					marker.NpcPortraitPath);
			}
			break;
		}
	}
}

