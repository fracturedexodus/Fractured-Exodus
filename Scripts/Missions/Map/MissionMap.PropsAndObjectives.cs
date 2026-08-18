using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap
{
	private string ResolveDialogueTargetId(MissionRoomBuilder.MarkerPlacement marker)
	{
		if (!string.IsNullOrEmpty(marker.TargetId))
		{
			return marker.TargetId;
		}

		if (marker.MarkerId == "trigger_dialogue" && !string.IsNullOrWhiteSpace(_missionTemplate?.DefaultDialogueId))
		{
			return _missionTemplate.DefaultDialogueId;
		}

		return marker.MarkerId;
	}

	private void OnMissionConversationEnded()
	{
		UpdateSelectedOfficerDisplay();
	}

	private void OnMissionDialogueStateChanged()
	{
		UpdateMissionCompletionActions();
	}

	private void OnOfficerEnteredCell(OfficerPawn pawn, Vector2I cell)
	{
		UpdateFogOfWar();
		UpdateMissionCompletionActions();
		TryCollectLootAtCurrentCell(pawn);
		CheckEnterTriggers(pawn);
	}

	private void OnOfficerReachedCell(OfficerPawn pawn, Vector2I cell)
	{
		UpdateMissionCompletionActions();
		RefreshCombatHud();
		EvaluateCombatState();

		if (!string.IsNullOrEmpty(_pendingCombatMoveOfficerId) && pawn != null && pawn.OfficerID == _pendingCombatMoveOfficerId)
		{
			_pendingCombatMoveOfficerId = string.Empty;
			_pendingCombatMoveCost = 0;

			if (!string.IsNullOrEmpty(_pendingCombatAttackEnemyId))
			{
				MissionNpcPawn pendingEnemy = _missionNpcs.FirstOrDefault(npc => npc != null && npc.NpcId == _pendingCombatAttackEnemyId && !npc.IsDead);
				_pendingCombatAttackEnemyId = string.Empty;
				if (pendingEnemy != null && _combatActive)
				{
					PerformOfficerAttack(pawn, pendingEnemy);
				}
			}
		}

		if (pawn == null || pawn.OfficerID != _pendingInteractionOfficerId)
		{
			TryPromptMedicalBedUseAtCurrentCell(pawn);
			return;
		}

		if (!string.IsNullOrEmpty(_pendingNpcId))
		{
			MissionNpcPawn pendingNpc = _missionNpcs.FirstOrDefault(npc => npc != null && npc.NpcId == _pendingNpcId);
			_pendingNpcId = string.Empty;
			_pendingInteractionOfficerId = string.Empty;
			if (pendingNpc != null && CanOfficerExecuteNpcInteraction(pawn, pendingNpc))
			{
				ExecuteNpcInteraction(pawn, pendingNpc);
			}
			return;
		}

		if (!string.IsNullOrEmpty(_pendingPropInstanceId))
		{
			MissionProp pendingProp = _missionPropsByCell.Values.FirstOrDefault(prop => prop != null && prop.PropInstanceId == _pendingPropInstanceId);
			_pendingPropInstanceId = string.Empty;
			_pendingInteractionOfficerId = string.Empty;
			if (pendingProp != null && CanOfficerExecutePropInteraction(pawn, pendingProp))
			{
				ExecutePropInteraction(pawn, pendingProp);
			}
			return;
		}

		if (!string.IsNullOrEmpty(_pendingDoorId))
		{
			string pendingDoorId = _pendingDoorId;
			_pendingDoorId = string.Empty;
			_pendingInteractionOfficerId = string.Empty;
			MissionRoomBuilder.MarkerPlacement directDoorInteraction = new MissionRoomBuilder.MarkerPlacement
			{
				LogicRole = "door",
				Label = "Bulkhead Door",
				TargetId = pendingDoorId,
				TriggerMode = "interact",
				Cell = cell
			};
			if (CanOfficerExecuteInteraction(pawn, directDoorInteraction))
			{
				ExecuteInteraction(pawn, directDoorInteraction);
			}
			return;
		}

		if (string.IsNullOrEmpty(_pendingInteractionKey))
		{
			return;
		}

		MissionRoomBuilder.MarkerPlacement interaction = _roomBuilder?.GetMarkerPlacements()
			.FirstOrDefault(placement => BuildInteractionKey(placement) == _pendingInteractionKey);
		_pendingInteractionKey = string.Empty;
		_pendingInteractionOfficerId = string.Empty;
		if (interaction != null && CanOfficerExecuteInteraction(pawn, interaction))
		{
			ExecuteInteraction(pawn, interaction);
			return;
		}

		TryPromptMedicalBedUseAtCurrentCell(pawn);
	}

	private void TryPromptMedicalBedUseAtCurrentCell(OfficerPawn officer)
	{
		if (officer == null || _missionUi == null || _pendingMedicalBedProp != null)
		{
			return;
		}

		Vector2I buildCell = GetBuildCell(officer.CurrentCell);
		if (!_missionPropsByCell.TryGetValue(buildCell, out MissionProp prop))
		{
			return;
		}

		TryPromptMedicalBedUse(officer, prop);
	}

	private bool TryPromptMedicalBedUse(OfficerPawn officer, MissionProp prop)
	{
		if (!ShouldOfferMedicalBedUse(officer, prop))
		{
			return false;
		}

		_pendingMedicalBedProp = prop;
		_pendingMedicalBedOfficerId = officer.OfficerID;
		_missionUi?.ShowConfirmationPrompt(
			"Medical Bed",
			$"{officer.OfficerName} is injured.\nUse the medical bed to restore their HP to full?",
			"Heal",
			"Skip");
		return true;
	}

	private bool ShouldOfferMedicalBedUse(OfficerPawn officer, MissionProp prop)
	{
		if (officer == null || prop == null || officer.IsDead || officer.CurrentHP >= officer.MaxHP)
		{
			return false;
		}

		if (!IsMedicalBedProp(prop))
		{
			return false;
		}

		if (_combatActive && officer.CurrentActions < CombatInteractionActionCost)
		{
			return false;
		}

		return true;
	}

	private static bool IsMedicalBedProp(MissionProp prop)
	{
		return string.Equals(prop?.Definition?.PropId, MedicalBedHealPropId, StringComparison.Ordinal);
	}

	private void ConfirmMedicalBedUse()
	{
		OfficerPawn officer = _officerPawns.FirstOrDefault(candidate => candidate != null && candidate.OfficerID == _pendingMedicalBedOfficerId);
		MissionProp prop = _pendingMedicalBedProp;
		if (officer == null || prop == null || !ShouldOfferMedicalBedUse(officer, prop))
		{
			ClearPendingConfirmation();
			return;
		}

		if (_combatActive)
		{
			officer.SpendActions(CombatInteractionActionCost);
		}

		int healedAmount = officer.RestoreHealthToFull();
		if (healedAmount > 0)
		{
			if (_missionUi?.PromptLabel != null)
			{
				_missionUi.PromptLabel.Text = $"{officer.OfficerName} used the medical bed and recovered {healedAmount} HP.";
			}
			AppendCombatLog($"{officer.OfficerName} used a medical bed and recovered {healedAmount} HP.");
		}

		UpdateSelectedOfficerDisplay();
		RefreshCombatHud();
		ClearPendingConfirmation();
	}

	private void ClearPendingConfirmation()
	{
		_pendingMedicalBedProp = null;
		_pendingMedicalBedOfficerId = string.Empty;
		_missionUi?.HideConfirmationPrompt();
	}

	private void SpawnMissionProps()
	{
		_missionPropsByCell.Clear();
		_propPlacementsByInstanceId.Clear();
		_blockedStaticPropCells.Clear();
		if (_roomBuilder == null || _isoWorld == null)
		{
			return;
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

		foreach (Node child in runtimePropLayer.GetChildren())
		{
			runtimePropLayer.RemoveChild(child);
			child.QueueFree();
		}

		foreach (MissionRoomBuilder.MarkerPlacement placement in _roomBuilder.GetMarkerPlacements())
		{
			PropDefinition definition = ResolvePropDefinitionForPlacement(placement);
			if (definition == null || string.IsNullOrWhiteSpace(definition.ScenePath))
			{
				continue;
			}

			PackedScene propScene = GD.Load<PackedScene>(definition.ScenePath);
			if (propScene == null)
			{
				continue;
			}

			MissionProp prop = propScene.Instantiate<MissionProp>();
			if (prop == null)
			{
				continue;
			}

			prop.Definition = definition;
			prop.Name = $"{definition.PropId}_{placement.Cell.X}_{placement.Cell.Y}";
			prop.PropInstanceId = BuildPropInstanceId(placement, definition);
			prop.Position = _roomBuilder.GetCellWorldPosition(placement.Cell.X, placement.Cell.Y);
			prop.PlacementRotationDegrees = placement.RotationDegrees;
			prop.PlacementFlipH = placement.FlipH;
			prop.PlacementFlipV = placement.FlipV;
			runtimePropLayer.AddChild(prop);
			_missionPropsByCell[placement.Cell] = prop;
			_propPlacementsByInstanceId[prop.PropInstanceId] = placement;
		}

		RefreshBlockedPropCells();
		MarkMissionDepthSortingDirty();
	}

	private void RefreshBlockedPropCells()
	{
		_blockedStaticPropCells.Clear();
		Node2D propLayer = _isoWorld?.GetNodeOrNull<Node2D>("PropLayer");
		if (propLayer == null)
		{
			return;
		}

		foreach (Node child in propLayer.GetChildren())
		{
			if (child is not Sprite2D sprite)
			{
				continue;
			}

			string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
			if (string.IsNullOrWhiteSpace(tileId) || tileId.StartsWith("door_", System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			int column = sprite.GetMeta("column", int.MinValue).AsInt32();
			int row = sprite.GetMeta("row", int.MinValue).AsInt32();
			if (column == int.MinValue || row == int.MinValue)
			{
				continue;
			}

			_blockedStaticPropCells.Add(new Vector2I(column, row));
		}
	}

	private PropDefinition ResolvePropDefinitionForPlacement(MissionRoomBuilder.MarkerPlacement placement)
	{
		if (placement == null)
		{
			return null;
		}

		if (!string.IsNullOrWhiteSpace(placement.PropDefinitionPath))
		{
			PropDefinition baseDefinition = GD.Load<PropDefinition>(placement.PropDefinitionPath);
			if (baseDefinition == null)
			{
				return null;
			}

			PropDefinition resolvedDefinition = baseDefinition.Duplicate(true) as PropDefinition ?? baseDefinition;
			PropPlacementOverrides.ApplyRuntimeOverrides(resolvedDefinition, placement, _missionTemplate);
			return resolvedDefinition;
		}

		return null;
	}

	private bool HasRuntimePropForPlacement(MissionRoomBuilder.MarkerPlacement placement)
	{
		return placement != null && _missionPropsByCell.ContainsKey(placement.Cell) && ResolvePropDefinitionForPlacement(placement) != null;
	}

	private Vector2I GetPropCell(MissionProp prop)
	{
		foreach (KeyValuePair<Vector2I, MissionProp> kvp in _missionPropsByCell)
		{
			if (kvp.Value == prop)
			{
				return kvp.Key;
			}
		}

		return Vector2I.Zero;
	}

	private string BuildPropInstanceId(MissionRoomBuilder.MarkerPlacement placement, PropDefinition definition)
	{
		string key = !string.IsNullOrWhiteSpace(placement.MarkerId)
			? placement.MarkerId
			: !string.IsNullOrWhiteSpace(placement.LogicRole)
				? placement.LogicRole
				: definition.PropId;
		return $"{definition.PropId}:{key}:{placement.Cell.X},{placement.Cell.Y}:{placement.TargetId}";
	}

	private PropInteractionContext BuildPropInteractionContext(OfficerPawn officer, MissionProp prop)
	{
		return new PropInteractionContext
		{
			MissionMap = this,
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			DialogueUI = _dialogueUi,
			Officer = officer,
			TargetCell = GetPropCell(prop),
			PropInstanceId = prop.PropInstanceId,
			SourceInteractionKey = _missionState?.SourceInteractionKey ?? string.Empty,
			NpcPortraitPath = _propPlacementsByInstanceId.TryGetValue(prop.PropInstanceId, out MissionRoomBuilder.MarkerPlacement placement)
				? placement.NpcPortraitPath
				: string.Empty
		};
	}

	private PropInteractionContext BuildNpcInteractionContext(OfficerPawn officer, MissionNpcPawn npc)
	{
		return new PropInteractionContext
		{
			MissionMap = this,
			GlobalData = _globalData,
			MissionState = _missionState,
			MissionTemplate = _missionTemplate,
			DialogueUI = _dialogueUi,
			Officer = officer,
			TargetCell = npc?.CurrentCell ?? Vector2I.Zero,
			PropInstanceId = npc?.NpcId ?? string.Empty,
			SourceInteractionKey = _missionState?.SourceInteractionKey ?? string.Empty,
			NpcPortraitPath = npc?.PortraitPath ?? string.Empty
		};
	}

	private void ApplyPropInteractionResult(MissionProp prop, PropInteractionResult result, PropInteractionContext context)
	{
		if (prop == null || result == null || !result.Success)
		{
			return;
		}

		if (_combatActive && context?.Officer != null)
		{
			context.Officer.SpendActions(CombatInteractionActionCost);
		}

		foreach (string doorId in result.DoorIdsToToggle ?? new List<string>())
		{
			if (string.IsNullOrWhiteSpace(doorId))
			{
				continue;
			}

			bool nextOpenState = !_roomBuilder.IsDoorOpen(doorId);
			_roomBuilder.TrySetDoorOpen(doorId, nextOpenState, true);
		}

		prop.CommitInteractionResult(result, context);
		if (prop.Definition?.PropId == RelaySurvivorPodsPropId
			&& !_escortSurvivorIds.Any()
			&& GetExtractedEscortSurvivorCount() == 0)
		{
			SpawnRelaySurvivors(GetPropCell(prop));
		}

		AppendActionLog(BuildActionResultMessage(
			context?.Officer?.OfficerName,
			result.StatusMessage,
			$"{context?.Officer?.OfficerName ?? "Officer"} secures {prop.Definition?.DisplayName ?? "the objective"}.")); 
		UpdateFogOfWar();
		UpdateMissionCompletionActions();

		if (prop.IsConsumed && prop.Definition?.HideWhenConsumed == true)
		{
			Vector2I propCell = GetPropCell(prop);
			if (_missionPropsByCell.ContainsKey(propCell) && _missionPropsByCell[propCell] == prop)
			{
				_missionPropsByCell.Remove(propCell);
				MarkMissionDepthSortingDirty();
			}
		}

		if (!string.IsNullOrWhiteSpace(result.DialogueId) && _dialogueUi != null)
		{
			_dialogueUi.StartConversation(
				result.DialogueId,
				context.Officer?.OfficerName ?? "Officer",
				context.Officer?.PortraitPath ?? string.Empty,
				context.NpcPortraitPath ?? string.Empty);
		}
	}

	private bool ShouldShowStoryEvent(MissionProp prop)
	{
		return prop?.Definition != null
			&& (!string.IsNullOrWhiteSpace(prop.Definition.StoryImagePath)
				|| !string.IsNullOrWhiteSpace(prop.Definition.StoryDescriptionText)
				|| !string.IsNullOrWhiteSpace(prop.Definition.StoryConfirmButtonText));
	}

	private void ShowPropStoryEvent(MissionProp prop, PropInteractionResult result, PropInteractionContext context)
	{
		if (_missionUi == null || prop?.Definition == null)
		{
			ApplyPropInteractionResult(prop, result, context);
			return;
		}

		_pendingStoryProp = prop;
		_pendingStoryResult = result;
		_pendingStoryContext = context;
		_missionUi.ShowStoryEvent(
			prop.Definition.DisplayName,
			prop.Definition.StoryDescriptionText,
			prop.Definition.StoryImagePath,
			prop.Definition.StoryConfirmButtonText);
	}

	private void OnStoryEventConfirmed()
	{
		_missionUi?.HideStoryEvent();
		if (_pendingStoryProp != null && _pendingStoryResult != null && _pendingStoryContext != null)
		{
			ApplyPropInteractionResult(_pendingStoryProp, _pendingStoryResult, _pendingStoryContext);
		}

		_pendingStoryProp = null;
		_pendingStoryResult = null;
		_pendingStoryContext = null;
	}

	private void OnConfirmationAccepted()
	{
		if (_pendingMedicalBedProp != null && !string.IsNullOrWhiteSpace(_pendingMedicalBedOfficerId))
		{
			ConfirmMedicalBedUse();
			return;
		}

		ClearPendingConfirmation();
	}

	private void OnConfirmationCancelled()
	{
		ClearPendingConfirmation();
	}

	private void ApplyNpcInteractionResult(MissionNpcPawn npc, PropInteractionResult result, PropInteractionContext context)
	{
		if (npc == null || result == null || !result.Success)
		{
			return;
		}

		if (_combatActive && context?.Officer != null)
		{
			context.Officer.SpendActions(CombatInteractionActionCost);
		}

		if (_globalData?.StoryFlags != null && result.FlagsToSet != null)
		{
			foreach (string flag in result.FlagsToSet)
			{
				if (!string.IsNullOrWhiteSpace(flag) && !_globalData.StoryFlags.Contains(flag))
				{
					_globalData.StoryFlags.Add(flag);
				}
			}
		}

		npc.CommitInteractionResult(result, context);
		MarkMissionDepthSortingDirty();
		AppendActionLog(BuildActionResultMessage(
			context?.Officer?.OfficerName,
			result.StatusMessage,
			$"{context?.Officer?.OfficerName ?? "Officer"} speaks with {npc.DisplayName}.")); 
		UpdateMissionCompletionActions();

		if (!string.IsNullOrWhiteSpace(result.DialogueId) && _dialogueUi != null)
		{
			_dialogueUi.StartConversation(
				result.DialogueId,
				context.Officer?.OfficerName ?? "Officer",
				context.Officer?.PortraitPath ?? string.Empty,
				context.NpcPortraitPath ?? string.Empty);
		}
	}

	private static string BuildInteractionKey(MissionRoomBuilder.MarkerPlacement marker)
	{
		string roleOrMarker = !string.IsNullOrEmpty(marker.MarkerId) ? marker.MarkerId : marker.LogicRole;
		string tileId = marker.TileId ?? string.Empty;
		return $"{roleOrMarker}:{tileId}:{marker.Cell.X},{marker.Cell.Y}:{marker.TargetId}";
	}

	private static string GetInteractionDisplayName(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (!string.IsNullOrWhiteSpace(interaction?.Label))
		{
			return interaction.Label.Trim();
		}

		if (string.Equals(interaction?.LogicRole, "door", StringComparison.OrdinalIgnoreCase))
		{
			return "Bulkhead Door";
		}

		if (string.Equals(interaction?.LogicRole, "terminal", StringComparison.OrdinalIgnoreCase))
		{
			return "Terminal";
		}

		return "the objective";
	}

	private List<MissionExtractionOption> GetAvailableExtractionOptions()
	{
		List<MissionExtractionOption> options = new List<MissionExtractionOption>();
		string primaryOutcomeId = GetPrimaryOutcomeId();
		if (IsOutcomeReady(primaryOutcomeId))
		{
			options.Add(new MissionExtractionOption
			{
				OutcomeId = primaryOutcomeId,
				DisplayText = GetOutcomeDisplayName(primaryOutcomeId),
				Description = $"Complete the mission as {GetOutcomeDisplayName(primaryOutcomeId).ToLowerInvariant()}."
			});
		}

		string secondaryOutcomeId = GetSecondaryOutcomeId();
		if (IsOutcomeReady(secondaryOutcomeId))
		{
			options.Add(new MissionExtractionOption
			{
				OutcomeId = secondaryOutcomeId,
				DisplayText = GetOutcomeDisplayName(secondaryOutcomeId),
				Description = $"Complete the mission as {GetOutcomeDisplayName(secondaryOutcomeId).ToLowerInvariant()}."
			});
		}

		return options;
	}

	private HashSet<Vector2I> GetEvacRallyCells()
	{
		HashSet<Vector2I> rallyCells = new HashSet<Vector2I>();
		if (_roomBuilder == null)
		{
			return rallyCells;
		}

		Vector2I[] buildCellDirections =
		{
			Vector2I.Zero,
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		foreach (Vector2I evacCell in _roomBuilder.GetMarkerPlacements()
			.Where(marker => marker.MarkerId == "evac_zone")
			.Select(marker => marker.Cell))
		{
			foreach (Vector2I direction in buildCellDirections)
			{
				Vector2I buildCell = evacCell + direction;
				foreach (Vector2I candidate in _roomBuilder.GetMovementCellsForBuildCell(buildCell))
				{
					if (_roomBuilder.IsWalkableMovementCell(candidate))
					{
						rallyCells.Add(candidate);
					}
				}
			}
		}

		return rallyCells;
	}

	private bool AreAllOfficersOnEvacZone()
	{
		if (_roomBuilder == null)
		{
			return false;
		}

		List<OfficerPawn> survivingOfficers = GetAliveOfficers().ToList();
		if (survivingOfficers.Count == 0)
		{
			return false;
		}

		HashSet<Vector2I> evacCells = GetEvacRallyCells();
		if (evacCells.Count == 0)
		{
			return false;
		}

		return survivingOfficers.All(pawn => evacCells.Contains(pawn.CurrentCell));
	}

	private void UpdateMissionCompletionActions()
	{
		if (_missionUi == null)
		{
			return;
		}

		List<MissionExtractionOption> availableOptions = GetAvailableExtractionOptions();
		if (AreAllOfficersOnEvacZone() && availableOptions.Count > 0)
		{
			string message = availableOptions.Count == 1
				? $"All surviving officers are assembled at the evac zone. Confirm extraction to leave the mission as {availableOptions[0].DisplayText.ToLowerInvariant()}."
				: "All surviving officers are assembled at the evac zone. Choose which resolved outcome you want to extract with.";
			_missionUi.ShowExtractionPrompt("EXTRACTION READY", message, availableOptions);
		}
		else
		{
			_missionUi.HideExtractionPrompt();
		}

		RefreshMissionPrompt();
	}

	private bool AreRequiredFlagsSatisfied(Godot.Collections.Array<string> requiredFlags)
	{
		if (requiredFlags == null || requiredFlags.Count == 0)
		{
			return true;
		}

		if (_globalData?.StoryFlags == null)
		{
			return false;
		}

		foreach (string flag in requiredFlags)
		{
			if (!string.IsNullOrWhiteSpace(flag) && !_globalData.StoryFlags.Contains(flag))
			{
				return false;
			}
		}

		return true;
	}

	private bool AreBlockedFlagsClear(Godot.Collections.Array<string> blockedFlags)
	{
		if (blockedFlags == null || blockedFlags.Count == 0)
		{
			return true;
		}

		if (_globalData?.StoryFlags == null)
		{
			return true;
		}

		foreach (string flag in blockedFlags)
		{
			if (!string.IsNullOrWhiteSpace(flag) && _globalData.StoryFlags.Contains(flag))
			{
				return false;
			}
		}

		return true;
	}

	private string BuildMissingFlagsTooltip(Godot.Collections.Array<string> requiredFlags)
	{
		if (requiredFlags == null || requiredFlags.Count == 0 || _globalData?.StoryFlags == null)
		{
			return "Additional mission steps are still required.";
		}

		List<string> missingFlags = requiredFlags
			.Where(flag => !string.IsNullOrWhiteSpace(flag) && !_globalData.StoryFlags.Contains(flag))
			.ToList();
		return missingFlags.Count == 0
			? string.Empty
			: $"Missing mission steps: {string.Join(", ", missingFlags)}";
	}
}

