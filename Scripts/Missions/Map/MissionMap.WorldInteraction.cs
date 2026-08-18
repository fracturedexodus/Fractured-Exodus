using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private bool TryHandleInteractionClick(OfficerPawn officer)
	{
		if (officer == null || _roomBuilder == null || _isoWorld == null || (_dialogueUi?.IsConversationOpen ?? false))
		{
			return false;
		}

		Vector2I clickedCell = _roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		Vector2I clickedBuildCell = GetBuildCell(clickedCell);
		if (_roomBuilder.IsDoorCell(clickedBuildCell))
		{
			// Door bulkheads are keyboard-only for now. Left-click should always remain a move order.
			return false;
		}

		if (!_visibleCells.Contains(clickedCell))
		{
			return false;
		}

		if (TryHandleNpcInteractionClick(officer, clickedCell))
		{
			return true;
		}

		if (TryHandlePropInteractionClick(officer, clickedCell))
		{
			return true;
		}

		List<MissionRoomBuilder.MarkerPlacement> interactions = _roomBuilder.GetInteractPlacementsAtCell(clickedBuildCell)
			.Where(placement =>
				(placement.LogicRole != "door" && string.Equals(placement.TriggerMode, "interact", System.StringComparison.OrdinalIgnoreCase))
				|| placement.LogicRole == "terminal")
			.Where(placement => !HasRuntimePropForPlacement(placement))
			.ToList();
		if (interactions.Count == 0)
		{
			return false;
		}

		MissionRoomBuilder.MarkerPlacement interaction = interactions
			.OrderByDescending(placement => placement.LogicRole == "terminal")
			.First();

		RequestStaticInteraction(officer, interaction);
		return true;
	}

	private bool TryHandleBlockedPropMovement(OfficerPawn officer, Vector2I blockedTargetCell, IReadOnlyCollection<string> ignoredOfficerIds)
	{
		if (!TryGetMissionPropAtMovementCell(blockedTargetCell, out MissionProp prop))
		{
			return false;
		}

		if (CanOfficerExecutePropInteraction(officer, prop))
		{
			ExecutePropInteraction(officer, prop);
			return true;
		}

		Vector2I? approachCell = FindBestPropApproachCell(officer, prop, ignoredOfficerIds);
		if (!approachCell.HasValue)
		{
			return false;
		}

		if (TryMoveOfficerToCell(officer, approachCell.Value, ignoredOfficerIds, false))
		{
			_pendingPropInstanceId = prop.PropInstanceId;
			_pendingInteractionOfficerId = officer.OfficerID;
			return true;
		}

		return false;
	}

	private bool TryHandleBlockedInteractionMovement(OfficerPawn officer, Vector2I blockedTargetCell, IReadOnlyCollection<string> ignoredOfficerIds)
	{
		if (!TryGetStaticInteractionAtMovementCell(blockedTargetCell, out MissionRoomBuilder.MarkerPlacement interaction))
		{
			return false;
		}

		if (CanOfficerExecuteInteraction(officer, interaction))
		{
			ExecuteInteraction(officer, interaction);
			return true;
		}

		Vector2I? approachCell = FindBestInteractionApproachCell(officer, interaction, ignoredOfficerIds);
		if (!approachCell.HasValue)
		{
			return false;
		}

		if (TryMoveOfficerToCell(officer, approachCell.Value, ignoredOfficerIds, false))
		{
			_pendingInteractionKey = BuildInteractionKey(interaction);
			_pendingInteractionOfficerId = officer.OfficerID;
			return true;
		}

		return false;
	}

	private bool TryHandleDoorKeyAction(OfficerPawn officer)
	{
		if (officer == null || _roomBuilder == null || _isoWorld == null || (_dialogueUi?.IsConversationOpen ?? false))
		{
			return false;
		}

		Vector2I hoveredCell = _roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		Vector2I hoveredBuildCell = GetBuildCell(hoveredCell);
		Vector2I officerBuildCell = GetBuildCell(officer.CurrentCell);
		List<Vector2I> candidateBuildCells = new List<Vector2I> { hoveredBuildCell };
		for (int offsetY = -1; offsetY <= 1; offsetY++)
		{
			for (int offsetX = -1; offsetX <= 1; offsetX++)
			{
				candidateBuildCells.Add(officerBuildCell + new Vector2I(offsetX, offsetY));
			}
		}

		foreach (Vector2I candidate in candidateBuildCells
			.Distinct()
			.Where(cell => _roomBuilder.TryGetDoorIdAtCell(cell, out string _) && IsStructureCellVisible(cell))
			.OrderByDescending(cell => cell == hoveredBuildCell)
			.ThenBy(cell => GetInteractionDistanceToBuildCell(officer.CurrentCell, cell)))
		{
			if (_roomBuilder.TryGetDoorIdAtCell(candidate, out string doorId)
				&& !string.IsNullOrWhiteSpace(doorId)
				&& TryHandleDirectDoorInteraction(officer, candidate, doorId))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryHandleDirectDoorClick(OfficerPawn officer, Vector2I clickedCell)
	{
		if (officer == null || _roomBuilder == null)
		{
			return false;
		}

		Vector2I clickedBuildCell = GetBuildCell(clickedCell);
		if (!_roomBuilder.TryGetDoorIdAtCell(clickedBuildCell, out string doorId) || string.IsNullOrWhiteSpace(doorId))
		{
			return false;
		}

		return TryHandleDirectDoorInteraction(officer, clickedBuildCell, doorId);
	}

	private bool TryHandleDirectDoorInteraction(OfficerPawn officer, Vector2I clickedCell, string doorId)
	{
		if (officer == null || _roomBuilder == null || string.IsNullOrWhiteSpace(doorId))
		{
			return false;
		}

		if (!IsStructureCellVisible(clickedCell))
		{
			return false;
		}

		return RequestDoorInteraction(officer, clickedCell, doorId);
	}

	private bool TryHandlePropInteractionClick(OfficerPawn officer, Vector2I clickedCell)
	{
		Vector2I clickedBuildCell = GetBuildCell(clickedCell);
		if (!_missionPropsByCell.TryGetValue(clickedBuildCell, out MissionProp prop) || prop == null)
		{
			return false;
		}

		if (!_visibleBuildCells.Contains(clickedBuildCell))
		{
			return false;
		}

		RequestPropInteraction(officer, prop);
		return true;
	}

	private bool TryHandleNpcInteractionClick(OfficerPawn officer, Vector2I clickedCell)
	{
		MissionNpcPawn npc = GetNpcAtMovementCell(clickedCell);
		if (npc == null)
		{
			return false;
		}

		if (!_visibleCells.Contains(npc.CurrentCell))
		{
			return false;
		}

		if (npc.IsHostile)
		{
			_focusedEnemy = npc;
			if (!string.IsNullOrWhiteSpace(npc.NpcId))
			{
				_engagedEnemyIds.Add(npc.NpcId);
			}
			if (!_combatActive)
			{
				StartMissionCombat();
			}
			else
			{
				RefreshCombatHud();
			}
			return false;
		}

		RequestNpcInteraction(officer, npc);
		return true;
	}

	private MissionNpcPawn GetNpcAtMovementCell(Vector2I movementCell)
	{
		if (_missionNpcsByCell.TryGetValue(movementCell, out MissionNpcPawn exactNpc) && exactNpc != null)
		{
			return exactNpc;
		}

		Vector2I buildCell = GetBuildCell(movementCell);
		return _missionNpcs.FirstOrDefault(npc => npc != null && !npc.IsDead && !npc.IsExtracted && GetBuildCell(npc.CurrentCell) == buildCell);
	}

	private bool CanOfficerExecuteInteraction(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (_combatActive && (officer == null || officer.CurrentActions < CombatInteractionActionCost))
		{
			return false;
		}

		return GetInteractionDistance(officer.CurrentCell, interaction) <= MissionGridRules.StandardActionCost;
	}

	private int GetInteractionDistance(Vector2I officerCell, MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction.LogicRole == "terminal")
		{
			return GetInteractionDistanceToBuildCell(officerCell, interaction.Cell);
		}

		if (interaction.LogicRole == "door")
		{
			return GetInteractionDistanceToBuildCell(officerCell, interaction.Cell);
		}

		return GetBuildCell(officerCell) == interaction.Cell ? 0 : int.MaxValue;
	}

	private Vector2I? FindBestInteractionApproachCell(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction, IReadOnlyCollection<string> ignoredOfficerIds = null)
	{
		if (_roomBuilder == null || officer == null || interaction == null)
		{
			return null;
		}

		Vector2I interactionCell = GetMovementCell(interaction.Cell);
		List<Vector2I> candidates = _roomBuilder.GetReachableMovementCells(interactionCell, MissionGridRules.ScaleAuthoredUnit(1))
			.Where(candidate => _roomBuilder.IsWalkableMovementCell(candidate))
			.Where(candidate => !IsMovementCellBlockedByProp(candidate))
			.Where(candidate => !IsCellOccupiedByLivingActor(candidate, officer, null, ignoredOfficerIds))
			.ToList();

		foreach (Vector2I candidate in candidates.Distinct())
		{
			if (TryGetTraversableMovementPath(officer.CurrentCell, candidate, officer, null, ignoredOfficerIds, out List<Vector2I> _))
			{
				return candidate;
			}
		}

		return null;
	}

	private void ExecuteInteraction(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction)
	{
		string interactionKey = BuildInteractionKey(interaction);
		if (interaction.OneShot && _consumedTriggerKeys.Contains(interactionKey))
		{
			return;
		}

		if (!string.IsNullOrEmpty(interaction.RequiredFlag) && (_globalData?.StoryFlags?.Contains(interaction.RequiredFlag) != true))
		{
			return;
		}

		if (!string.IsNullOrEmpty(interaction.SetFlag) && _globalData != null && !_globalData.StoryFlags.Contains(interaction.SetFlag))
		{
			_globalData.StoryFlags.Add(interaction.SetFlag);
			UpdateMissionCompletionActions();
		}

		if (interaction.MarkerId == "evac_zone")
		{
			if (_combatActive)
			{
				officer.SpendActions(CombatInteractionActionCost);
			}
			AppendActionLog($"{officer.OfficerName} moves into the evac zone.");
			UpdateMissionCompletionActions();
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			return;
		}

		if (interaction.LogicRole == "door")
		{
			bool nextOpenState = !_roomBuilder.IsDoorOpen(interaction.TargetId);
			FaceNodeToward(officer, GetCellGlobalPosition(interaction.Cell));
			if (_combatActive)
			{
				officer.SpendActions(CombatInteractionActionCost);
			}
			ToggleDoorInteraction(interaction);
			AppendActionLog($"{officer.OfficerName} {(nextOpenState ? "opens" : "closes")} {GetInteractionDisplayName(interaction)}.");
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			UpdateFogOfWar();
			UpdateMissionCompletionActions();
			return;
		}

		if (interaction.LogicRole == "terminal")
		{
			bool controlsDoor = !string.IsNullOrEmpty(interaction.TargetId);
			bool nextOpenState = controlsDoor && !_roomBuilder.IsDoorOpen(interaction.TargetId);
			FaceNodeToward(officer, GetCellGlobalPosition(interaction.Cell));
			if (_combatActive)
			{
				officer.SpendActions(CombatInteractionActionCost);
			}
			if (!string.IsNullOrEmpty(interaction.TargetId))
			{
				_roomBuilder.TrySetDoorOpen(interaction.TargetId, nextOpenState, true);
				UpdateFogOfWar();
			}
			AppendActionLog(controlsDoor
				? $"{officer.OfficerName} uses {GetInteractionDisplayName(interaction)} to {(nextOpenState ? "open" : "close")} a bulkhead."
				: $"{officer.OfficerName} uses {GetInteractionDisplayName(interaction)}.");
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			UpdateMissionCompletionActions();
			return;
		}

		if (interaction.MarkerId == "trigger_dialogue")
		{
			FaceNodeToward(officer, GetCellGlobalPosition(interaction.Cell));
			if (_combatActive)
			{
				officer.SpendActions(CombatInteractionActionCost);
			}
			AppendActionLog($"{officer.OfficerName} initiates a conversation.");
			_dialogueUi.StartConversation(
				ResolveDialogueTargetId(interaction),
				officer.OfficerName,
				officer.PortraitPath,
				interaction.NpcPortraitPath);
			if (interaction.OneShot)
			{
				_consumedTriggerKeys.Add(interactionKey);
			}
			UpdateMissionCompletionActions();
		}
	}

	private bool CanOfficerExecutePropInteraction(OfficerPawn officer, MissionProp prop)
	{
		if (officer == null || prop == null)
		{
			return false;
		}

		if (_combatActive && officer.CurrentActions < CombatInteractionActionCost)
		{
			return false;
		}

		Vector2I propCell = GetMovementCell(GetPropCell(prop));
		int interactionRange = MissionGridRules.ScaleAuthoredUnit(prop.Definition?.InteractionRange ?? 1);
		int distance = GetTileDistance(officer.CurrentCell, propCell);
		return distance <= interactionRange;
	}

	private bool CanOfficerExecuteNpcInteraction(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null)
		{
			return false;
		}

		if (_combatActive && officer.CurrentActions < CombatInteractionActionCost)
		{
			return false;
		}

		int interactionRange = Mathf.Max(1, npc.InteractionRange);
		int distance = GetTileDistance(officer.CurrentCell, npc.CurrentCell);
		return distance <= interactionRange;
	}

	private Vector2I? FindBestPropApproachCell(OfficerPawn officer, MissionProp prop, IReadOnlyCollection<string> ignoredOfficerIds = null)
	{
		if (officer == null || prop == null || _roomBuilder == null)
		{
			return null;
		}

		Vector2I propBuildCell = GetPropCell(prop);
		Vector2I propMovementCell = GetMovementCell(propBuildCell);
		int interactionRange = MissionGridRules.ScaleAuthoredUnit(prop.Definition?.InteractionRange ?? 1);

		List<Vector2I> candidates = _roomBuilder.GetReachableMovementCells(propMovementCell, interactionRange)
			.Where(candidate => GetBuildCell(candidate) != propBuildCell)
			.Where(candidate => _roomBuilder.IsWalkableMovementCell(candidate))
			.Where(candidate => !IsMovementCellBlockedByProp(candidate))
			.Where(candidate => !IsCellOccupiedByLivingActor(candidate, officer, null, ignoredOfficerIds))
			.ToList();

		foreach (Vector2I candidate in candidates.Distinct().OrderBy(candidate => GetTileDistance(candidate, propMovementCell)))
		{
			if (TryGetTraversableMovementPath(officer.CurrentCell, candidate, officer, null, ignoredOfficerIds, out List<Vector2I> _))
			{
				return candidate;
			}
		}

		return null;
	}

	private Vector2I? FindBestNpcApproachCell(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null || _roomBuilder == null)
		{
			return null;
		}

		List<Vector2I> candidates = _roomBuilder.GetReachableMovementCells(npc.CurrentCell, npc.InteractionRange)
			.Where(candidate => candidate != npc.CurrentCell && _roomBuilder.IsWalkableMovementCell(candidate))
			.ToList();

		foreach (Vector2I candidate in candidates.Distinct())
		{
			if (TryGetTraversableMovementPath(officer.CurrentCell, candidate, officer, null, null, out List<Vector2I> _))
			{
				return candidate;
			}
		}

		return null;
	}

	private void ExecutePropInteraction(OfficerPawn officer, MissionProp prop)
	{
		if (officer == null || prop == null)
		{
			return;
		}

		FaceNodeToward(officer, prop.GlobalPosition);
		if (TryPromptMedicalBedUse(officer, prop))
		{
			return;
		}

		PropInteractionContext context = BuildPropInteractionContext(officer, prop);
		PropInteractionResult result = prop.Interact(context);
		if (result == null || !result.Success)
		{
			return;
		}

		if (ShouldShowStoryEvent(prop))
		{
			ShowPropStoryEvent(prop, result, context);
			return;
		}

		ApplyPropInteractionResult(prop, result, context);
	}

	private void ExecuteNpcInteraction(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null)
		{
			return;
		}

		FaceNodeToward(officer, npc.GlobalPosition);
		FaceNodeToward(npc, officer.GlobalPosition, 0.2f);
		PropInteractionContext context = BuildNpcInteractionContext(officer, npc);
		PropInteractionResult result = npc.Interact(context);
		if (result == null || !result.Success)
		{
			return;
		}

		ApplyNpcInteractionResult(npc, result, context);
	}

	private void ToggleDoorInteraction(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (_roomBuilder == null || string.IsNullOrEmpty(interaction.TargetId))
		{
			return;
		}

		bool nextOpenState = !_roomBuilder.IsDoorOpen(interaction.TargetId);
		if (!nextOpenState && _officerPawns.Any(pawn => GetBuildCell(pawn.CurrentCell) == interaction.Cell))
		{
			return;
		}

		if (_roomBuilder.TrySetDoorOpen(interaction.TargetId, nextOpenState, true))
		{
			MarkMissionDepthSortingDirty();
		}
	}
}

