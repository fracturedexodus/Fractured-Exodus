using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private void UpdateCombatHoverSummary()
	{
		if (_missionUi == null || !_combatActive || _missionGameOver)
		{
			_hoveredCombatActor = null;
			_missionUi?.HideHoverSummary();
			return;
		}

		Node2D hoveredActor = FindHoveredCombatActor();
		if (hoveredActor == null)
		{
			_hoveredCombatActor = null;
			_missionUi.HideHoverSummary();
			return;
		}

		_hoveredCombatActor = hoveredActor;
		MissionCombatantSummary summary = hoveredActor switch
		{
			OfficerPawn officer => BuildOfficerSummary(officer),
			MissionNpcPawn enemy => BuildEnemySummary(enemy),
			_ => null
		};
		if (summary == null)
		{
			_missionUi.HideHoverSummary();
			return;
		}

		Vector2 screenPosition = GetViewport().GetCanvasTransform() * hoveredActor.GlobalPosition;
		_missionUi.ShowHoverSummary(summary, screenPosition);
	}

	private Node2D FindHoveredCombatActor()
	{
		Vector2 mousePosition = GetGlobalMousePosition();

		foreach (MissionNpcPawn enemy in _missionNpcs.Where(npc => npc != null && npc.Visible && !npc.IsDead))
		{
			if (BuildHoverBounds(enemy).HasPoint(mousePosition))
			{
				return enemy;
			}
		}

		foreach (OfficerPawn officer in _officerPawns.Where(pawn => pawn != null && pawn.Visible && !pawn.IsDead))
		{
			if (BuildHoverBounds(officer).HasPoint(mousePosition))
			{
				return officer;
			}
		}

		return null;
	}

	private MissionNpcPawn GetHoveredVisibleHostileAtMouse()
	{
		Vector2 mousePosition = GetGlobalMousePosition();
		return _missionNpcs.FirstOrDefault(npc =>
			npc != null
			&& npc.IsHostile
			&& npc.Visible
			&& !npc.IsDead
			&& _visibleCells.Contains(npc.CurrentCell)
			&& BuildHoverBounds(npc).HasPoint(mousePosition));
	}

	private static Rect2 BuildHoverBounds(Node2D actor)
	{
		return new Rect2(actor.GlobalPosition + new Vector2(-54f, -96f), new Vector2(108f, 144f));
	}

	private void UpdateMissionInteractionMenu()
	{
		if (_missionUi == null)
		{
			return;
		}

		if (ShouldSuppressInteractionMenu())
		{
			ClearInteractionMenu();
			return;
		}

		if (!_missionUi.IsInteractionMenuVisible || _activeInteractionMenuTarget == null)
		{
			return;
		}

		OfficerPawn activeOfficer = GetInteractionMenuOfficer();
		string officerId = activeOfficer?.OfficerID ?? string.Empty;
		if (_activeInteractionMenuOfficerId != officerId)
		{
			ShowInteractionMenuForTarget(_activeInteractionMenuTarget, activeOfficer);
		}
	}

	private bool ShouldSuppressInteractionMenu()
	{
		return _missionGameOver
			|| _isPanning
			|| _isSelectionDragging
			|| (_pauseMenuWrapper?.Visible ?? false)
			|| (_loadMenuWrapper?.Visible ?? false)
			|| (_dialogueUi?.IsConversationOpen ?? false)
			|| (_missionUi?.IsStoryEventVisible ?? false)
			|| (_missionUi?.IsConfirmationVisible ?? false)
			|| (_missionUi?.IsMissionSavePromptVisible ?? false)
			|| (_combatActive && (!IsPlayerTurnActive() || _enemyTurnInProgress));
	}

	private OfficerPawn GetInteractionMenuOfficer()
	{
		return GetSelectedEscortSurvivor() == null ? GetSelectedOfficer() : null;
	}

	private bool TryShowInteractionMenuAtMouse()
	{
		if (_missionUi == null || ShouldSuppressInteractionMenu())
		{
			return false;
		}

		if (!TryBuildHoveredInteractionMenuTarget(out MissionInteractionMenuTarget target))
		{
			return false;
		}

		ShowInteractionMenuForTarget(target, GetInteractionMenuOfficer());
		return true;
	}

	private bool TryBuildHoveredInteractionMenuTarget(out MissionInteractionMenuTarget target)
	{
		target = null;
		if (_roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		Vector2I hoveredCell = _roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition()));
		Vector2I hoveredBuildCell = GetBuildCell(hoveredCell);
		Vector2 screenPosition = GetViewport().GetMousePosition();

		if (GetNpcAtMovementCell(hoveredCell) is MissionNpcPawn npc
			&& !npc.IsHostile
			&& _visibleCells.Contains(npc.CurrentCell))
		{
			string npcDetails = !string.IsNullOrWhiteSpace(npc.Description)
				? npc.Description
				: !string.IsNullOrWhiteSpace(npc.Notes)
					? npc.Notes
					: "A mission contact awaiting instructions.";
			target = new MissionInteractionMenuTarget
			{
				Kind = MissionInteractionMenuTargetKind.Npc,
				TargetKey = $"npc:{npc.NpcId}",
				Title = npc.DisplayName,
				Body = npcDetails,
				ScreenPosition = screenPosition,
				BuildCell = hoveredBuildCell,
				Npc = npc
			};
			return true;
		}

		if (_missionPropsByCell.TryGetValue(hoveredBuildCell, out MissionProp prop)
			&& prop != null
			&& (_visibleBuildCells.Contains(hoveredBuildCell) || prop.Visible))
		{
			string propDescription = !string.IsNullOrWhiteSpace(prop.Definition?.Description)
				? prop.Definition.Description
				: !string.IsNullOrWhiteSpace(prop.Definition?.SuccessMessage)
					? prop.Definition.SuccessMessage
					: "Mission equipment ready for field interaction.";
			target = new MissionInteractionMenuTarget
			{
				Kind = MissionInteractionMenuTargetKind.Prop,
				TargetKey = $"prop:{prop.PropInstanceId}",
				Title = prop.Definition?.DisplayName ?? prop.Name,
				Body = propDescription,
				ScreenPosition = screenPosition,
				BuildCell = hoveredBuildCell,
				Prop = prop
			};
			return true;
		}

		if (_roomBuilder.TryGetDoorIdAtCell(hoveredBuildCell, out string doorId)
			&& !string.IsNullOrWhiteSpace(doorId)
			&& IsStructureCellVisible(hoveredBuildCell))
		{
			target = new MissionInteractionMenuTarget
			{
				Kind = MissionInteractionMenuTargetKind.Door,
				TargetKey = $"door:{doorId}",
				Title = "Bulkhead Door",
				Body = _roomBuilder.IsDoorOpen(doorId)
					? "An unlocked bulkhead with the passage already open."
					: "A sealed bulkhead blocking the route ahead.",
				ScreenPosition = screenPosition,
				BuildCell = hoveredBuildCell,
				DoorId = doorId
			};
			return true;
		}

		if (TryGetStaticInteractionAtMovementCell(hoveredCell, out MissionRoomBuilder.MarkerPlacement interaction)
			&& interaction != null
			&& _visibleBuildCells.Contains(interaction.Cell))
		{
			target = new MissionInteractionMenuTarget
			{
				Kind = MissionInteractionMenuTargetKind.StaticInteraction,
				TargetKey = $"marker:{BuildInteractionKey(interaction)}",
				Title = GetInteractionDisplayName(interaction),
				Body = BuildInteractionMenuDescription(interaction),
				ScreenPosition = screenPosition,
				BuildCell = interaction.Cell,
				Interaction = interaction
			};
			return true;
		}

		return false;
	}

	private void ShowInteractionMenuForTarget(MissionInteractionMenuTarget target, OfficerPawn officer)
	{
		if (_missionUi == null || target == null)
		{
			return;
		}

		_activeInteractionMenuTarget = target;
		_activeInteractionMenuOfficerId = officer?.OfficerID ?? string.Empty;
		_missionUi.ShowInteractionMenu(
			target.Title,
			target.Body,
			target.ScreenPosition,
			BuildInteractionMenuOptions(target, officer));
	}

	private void ClearInteractionMenu()
	{
		_activeInteractionMenuTarget = null;
		_activeInteractionMenuOfficerId = string.Empty;
		_missionUi?.HideInteractionMenu();
	}

	private List<MissionInteractionMenuOption> BuildInteractionMenuOptions(MissionInteractionMenuTarget target, OfficerPawn officer)
	{
		List<MissionInteractionMenuOption> options = new List<MissionInteractionMenuOption>();
		if (target == null)
		{
			return options;
		}

		string selectOfficerMessage = "Select an officer to issue interaction commands.";
		switch (target.Kind)
		{
			case MissionInteractionMenuTargetKind.Door:
			{
				MissionRoomBuilder.MarkerPlacement doorInteraction = BuildDoorInteractionMarker(target.BuildCell, target.DoorId);
				bool canExecute = officer != null && CanOfficerExecuteInteraction(officer, doorInteraction);
				bool canApproach = officer != null && FindBestInteractionApproachCell(officer, doorInteraction).HasValue;
				bool doorOpen = _roomBuilder?.IsDoorOpen(target.DoorId) == true;
				bool doorwayBlocked = doorOpen && _officerPawns.Any(pawn => pawn != null && !pawn.IsDead && GetBuildCell(pawn.CurrentCell) == target.BuildCell);
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "primary",
					Label = doorOpen ? "Close Bulkhead" : "Open Bulkhead",
					Description = doorwayBlocked
						? "Clear the doorway before sealing this bulkhead."
						: officer == null
						? selectOfficerMessage
						: canExecute
							? (doorOpen ? "Close this bulkhead immediately." : "Open this bulkhead immediately.")
							: canApproach
								? (doorOpen ? "Move adjacent, then close this bulkhead." : "Move adjacent, then open this bulkhead.")
								: "No clear path to operate this bulkhead.",
					Disabled = doorwayBlocked || officer == null || (!canExecute && !canApproach)
				});
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "move",
					Label = "Approach",
					Description = officer == null
						? selectOfficerMessage
						: canApproach
							? "Move into position beside this bulkhead."
							: "No clear path to the door.",
					Disabled = officer == null || !canApproach
				});
				break;
			}
			case MissionInteractionMenuTargetKind.Prop:
			{
				bool isAvailable = officer != null && target.Prop != null && target.Prop.CanInteract(BuildPropInteractionContext(officer, target.Prop));
				bool canExecute = officer != null && target.Prop != null && isAvailable && CanOfficerExecutePropInteraction(officer, target.Prop);
				bool canApproach = officer != null && target.Prop != null && isAvailable && FindBestPropApproachCell(officer, target.Prop).HasValue;
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "primary",
					Label = GetPropInteractionActionLabel(target.Prop),
					Description = officer == null
						? selectOfficerMessage
						: canExecute
							? "Use this mission object now."
							: canApproach
								? "Move into range, then interact with this object."
								: "This object is not available right now.",
					Disabled = officer == null || (!canExecute && !canApproach)
				});
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "move",
					Label = "Approach",
					Description = officer == null
						? selectOfficerMessage
						: canApproach
							? "Move into interaction range."
							: "No clear path to this object.",
					Disabled = officer == null || !canApproach
				});
				break;
			}
			case MissionInteractionMenuTargetKind.Npc:
			{
				bool isAvailable = officer != null && target.Npc != null && target.Npc.CanInteract(BuildNpcInteractionContext(officer, target.Npc));
				bool canExecute = officer != null && target.Npc != null && isAvailable && CanOfficerExecuteNpcInteraction(officer, target.Npc);
				bool canApproach = officer != null && target.Npc != null && isAvailable && FindBestNpcApproachCell(officer, target.Npc).HasValue;
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "primary",
					Label = string.IsNullOrWhiteSpace(target.Npc?.DialogueId) ? "Interact" : "Talk",
					Description = officer == null
						? selectOfficerMessage
						: canExecute
							? "Speak with this contact now."
							: canApproach
								? "Move into range, then speak with this contact."
								: "This contact is unavailable right now.",
					Disabled = officer == null || (!canExecute && !canApproach)
				});
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "move",
					Label = "Approach",
					Description = officer == null
						? selectOfficerMessage
						: canApproach
							? "Move next to this contact."
							: "No clear path to this contact.",
					Disabled = officer == null || !canApproach
				});
				break;
			}
			case MissionInteractionMenuTargetKind.StaticInteraction:
			{
				bool interactionReady = IsStaticInteractionAvailable(target.Interaction);
				bool canExecute = officer != null && interactionReady && CanOfficerExecuteInteraction(officer, target.Interaction);
				bool canApproach = officer != null && interactionReady && FindBestInteractionApproachCell(officer, target.Interaction).HasValue;
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "primary",
					Label = GetStaticInteractionActionLabel(target.Interaction),
					Description = officer == null
						? selectOfficerMessage
						: canExecute
							? "Use this mission interaction now."
							: canApproach
								? "Move into range, then use this interaction."
								: "This interaction is unavailable right now.",
					Disabled = officer == null || (!canExecute && !canApproach)
				});
				options.Add(new MissionInteractionMenuOption
				{
					ActionId = "move",
					Label = "Approach",
					Description = officer == null
						? selectOfficerMessage
						: canApproach
							? "Move into position for this interaction."
							: "No clear path to this interaction point.",
					Disabled = officer == null || !canApproach
				});
				break;
			}
		}

		return options;
	}

	private void OnInteractionMenuOptionChosen(string actionId)
	{
		if (string.IsNullOrWhiteSpace(actionId) || _activeInteractionMenuTarget == null)
		{
			return;
		}

		OfficerPawn officer = GetInteractionMenuOfficer();
		bool handled = actionId switch
		{
			"primary" => RequestInteractionMenuPrimary(officer, _activeInteractionMenuTarget),
			"move" => RequestInteractionMenuApproach(officer, _activeInteractionMenuTarget),
			_ => false
		};

		if (handled)
		{
			ClearInteractionMenu();
		}
	}

	private bool RequestInteractionMenuPrimary(OfficerPawn officer, MissionInteractionMenuTarget target)
	{
		if (officer == null || target == null)
		{
			return false;
		}

		return target.Kind switch
		{
			MissionInteractionMenuTargetKind.Door => RequestDoorInteraction(officer, target.BuildCell, target.DoorId),
			MissionInteractionMenuTargetKind.Prop => target.Prop != null && RequestPropInteraction(officer, target.Prop),
			MissionInteractionMenuTargetKind.Npc => target.Npc != null && RequestNpcInteraction(officer, target.Npc),
			MissionInteractionMenuTargetKind.StaticInteraction => target.Interaction != null && RequestStaticInteraction(officer, target.Interaction),
			_ => false
		};
	}

	private bool RequestInteractionMenuApproach(OfficerPawn officer, MissionInteractionMenuTarget target)
	{
		if (officer == null || target == null)
		{
			return false;
		}

		Vector2I? approachCell = target.Kind switch
		{
			MissionInteractionMenuTargetKind.Door => FindBestInteractionApproachCell(officer, BuildDoorInteractionMarker(target.BuildCell, target.DoorId)),
			MissionInteractionMenuTargetKind.Prop => target.Prop != null ? FindBestPropApproachCell(officer, target.Prop) : null,
			MissionInteractionMenuTargetKind.Npc => target.Npc != null ? FindBestNpcApproachCell(officer, target.Npc) : null,
			MissionInteractionMenuTargetKind.StaticInteraction => target.Interaction != null ? FindBestInteractionApproachCell(officer, target.Interaction) : null,
			_ => null
		};
		if (!approachCell.HasValue)
		{
			return false;
		}

		ClearPendingInteractionRequests();
		return TryMoveOfficerToCell(officer, approachCell.Value);
	}

	private bool RequestDoorInteraction(OfficerPawn officer, Vector2I buildCell, string doorId)
	{
		if (officer == null || _roomBuilder == null || string.IsNullOrWhiteSpace(doorId) || !IsStructureCellVisible(buildCell))
		{
			return false;
		}

		MissionRoomBuilder.MarkerPlacement interaction = BuildDoorInteractionMarker(buildCell, doorId);
		bool nextOpenState = !_roomBuilder.IsDoorOpen(doorId);
		if (!nextOpenState && _officerPawns.Any(pawn => pawn != null && !pawn.IsDead && GetBuildCell(pawn.CurrentCell) == buildCell))
		{
			return false;
		}

		if (CanOfficerExecuteInteraction(officer, interaction))
		{
			ClearPendingInteractionRequests();
			ExecuteInteraction(officer, interaction);
			return true;
		}

		Vector2I? approachCell = FindBestInteractionApproachCell(officer, interaction);
		if (!approachCell.HasValue || !TryMoveOfficerToCell(officer, approachCell.Value))
		{
			return false;
		}

		ClearPendingInteractionRequests();
		_pendingDoorId = doorId;
		_pendingInteractionOfficerId = officer.OfficerID;
		return true;
	}

	private bool RequestPropInteraction(OfficerPawn officer, MissionProp prop)
	{
		if (officer == null || prop == null)
		{
			return false;
		}

		if (CanOfficerExecutePropInteraction(officer, prop))
		{
			ClearPendingInteractionRequests();
			ExecutePropInteraction(officer, prop);
			return true;
		}

		Vector2I? approachCell = FindBestPropApproachCell(officer, prop);
		if (!approachCell.HasValue || !TryMoveOfficerToCell(officer, approachCell.Value))
		{
			return false;
		}

		ClearPendingInteractionRequests();
		_pendingPropInstanceId = prop.PropInstanceId;
		_pendingInteractionOfficerId = officer.OfficerID;
		return true;
	}

	private bool RequestNpcInteraction(OfficerPawn officer, MissionNpcPawn npc)
	{
		if (officer == null || npc == null)
		{
			return false;
		}

		if (npc.IsHostile)
		{
			return false;
		}

		if (CanOfficerExecuteNpcInteraction(officer, npc))
		{
			ClearPendingInteractionRequests();
			ExecuteNpcInteraction(officer, npc);
			return true;
		}

		Vector2I? approachCell = FindBestNpcApproachCell(officer, npc);
		if (!approachCell.HasValue || !TryMoveOfficerToCell(officer, approachCell.Value))
		{
			return false;
		}

		ClearPendingInteractionRequests();
		_pendingNpcId = npc.NpcId;
		_pendingInteractionOfficerId = officer.OfficerID;
		return true;
	}

	private bool RequestStaticInteraction(OfficerPawn officer, MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (officer == null || interaction == null || !IsStaticInteractionAvailable(interaction))
		{
			return false;
		}

		if (CanOfficerExecuteInteraction(officer, interaction))
		{
			ClearPendingInteractionRequests();
			ExecuteInteraction(officer, interaction);
			return true;
		}

		Vector2I? approachCell = FindBestInteractionApproachCell(officer, interaction);
		if (!approachCell.HasValue || !TryMoveOfficerToCell(officer, approachCell.Value))
		{
			return false;
		}

		ClearPendingInteractionRequests();
		_pendingInteractionKey = BuildInteractionKey(interaction);
		_pendingInteractionOfficerId = officer.OfficerID;
		return true;
	}

	private void ClearPendingInteractionRequests()
	{
		_pendingInteractionKey = string.Empty;
		_pendingInteractionOfficerId = string.Empty;
		_pendingDoorId = string.Empty;
		_pendingPropInstanceId = string.Empty;
		_pendingNpcId = string.Empty;
	}

	private MissionRoomBuilder.MarkerPlacement BuildDoorInteractionMarker(Vector2I buildCell, string doorId)
	{
		return new MissionRoomBuilder.MarkerPlacement
		{
			LogicRole = "door",
			Label = "Bulkhead Door",
			TargetId = doorId,
			TriggerMode = "interact",
			Cell = buildCell
		};
	}

	private string BuildInteractionMenuDescription(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction == null)
		{
			return "Mission interaction available.";
		}

		if (interaction.MarkerId == "evac_zone")
		{
			return "Extract from the mission once your away team is assembled here.";
		}

		if (string.Equals(interaction.LogicRole, "terminal", StringComparison.OrdinalIgnoreCase))
		{
			return !string.IsNullOrWhiteSpace(interaction.TargetId)
				? "A control point linked to local bulkheads and mission systems."
				: "A mission terminal waiting for operator input.";
		}

		if (interaction.MarkerId == "trigger_dialogue")
		{
			return "A conversation point that can advance the current mission thread.";
		}

		return "A mission interaction point with contextual effects.";
	}

	private string GetPropInteractionActionLabel(MissionProp prop)
	{
		if (prop?.Definition == null)
		{
			return "Interact";
		}

		if (prop.Definition.PropId == MedicalBedHealPropId)
		{
			return "Treat Wounds";
		}

		return prop.Definition.InteractionType switch
		{
			PropInteractionType.Dialogue => "Access",
			PropInteractionType.Loot => "Loot",
			PropInteractionType.Hack => "Hack",
			PropInteractionType.DoorControl => "Use Control",
			PropInteractionType.Datapad => "Read",
			_ => "Interact"
		};
	}

	private string GetStaticInteractionActionLabel(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction == null)
		{
			return "Interact";
		}

		if (interaction.MarkerId == "evac_zone")
		{
			return "Secure Evac";
		}

		if (string.Equals(interaction.LogicRole, "terminal", StringComparison.OrdinalIgnoreCase))
		{
			return "Use Terminal";
		}

		if (interaction.MarkerId == "trigger_dialogue")
		{
			return "Talk";
		}

		return "Interact";
	}

	private bool IsStaticInteractionAvailable(MissionRoomBuilder.MarkerPlacement interaction)
	{
		if (interaction == null)
		{
			return false;
		}

		if (interaction.OneShot && _consumedTriggerKeys.Contains(BuildInteractionKey(interaction)))
		{
			return false;
		}

		return string.IsNullOrEmpty(interaction.RequiredFlag)
			|| (_globalData?.StoryFlags?.Contains(interaction.RequiredFlag) == true);
	}

	private void ReindexMissionNpcCells()
	{
		_missionNpcsByCell.Clear();
		foreach (MissionNpcPawn npc in _missionNpcs.Where(npc => npc != null && !npc.IsDead && !npc.IsExtracted))
		{
			_missionNpcsByCell[npc.CurrentCell] = npc;
		}
	}

	private void OnOfficerCombatStateChanged(OfficerPawn pawn)
	{
		RefreshCombatHud();
	}

	private void OnMissionNpcCombatStateChanged(MissionNpcPawn pawn)
	{
		RefreshCombatHud();
	}

	private void OnOfficerDied(OfficerPawn pawn)
	{
		if (pawn != null)
		{
			AppendCombatLog($"{pawn.OfficerName} collapses under enemy fire. Their post goes dark.");
		}

		RefreshCombatHud();
		UpdateFogOfWar();
		if (!GetAliveOfficers().Any())
		{
			HandleMissionGameOver();
			return;
		}

		int nextLivingIndex = _officerPawns.FindIndex(candidate => candidate != null && !candidate.IsDead);
		if (nextLivingIndex >= 0)
		{
			SelectOfficer(nextLivingIndex);
		}
	}

	private void OnMissionNpcDied(MissionNpcPawn pawn)
	{
		if (pawn != null)
		{
			AppendCombatLog(IsEscortSurvivor(pawn)
				? $"{pawn.DisplayName} is caught in the crossfire and falls before reaching evac."
				: $"{pawn.DisplayName} goes down and stops fighting.");
		}

		if (pawn != null && !string.IsNullOrWhiteSpace(pawn.NpcId) && pawn.IsHostile)
		{
			_engagedEnemyIds.Remove(pawn.NpcId);
			if (pawn.PersonalInventoryItemIDs.Count > 0)
			{
				if (SpawnEnemyLootDropProp(GetBuildCell(pawn.CurrentCell), pawn.PersonalInventoryItemIDs) != null)
				{
					AppendCombatLog($"{pawn.DisplayName} drops recoverable supplies where they fell.");
					pawn.PersonalInventoryItemIDs.Clear();
				}
			}
		}

		if (pawn != null && !string.IsNullOrWhiteSpace(pawn.NpcId))
		{
			_selectedEscortSurvivorIds.Remove(pawn.NpcId);
			if (pawn.NpcId == _selectedEscortSurvivorId)
			{
				_selectedEscortSurvivorId = string.Empty;
			}
		}
		NormalizeFriendlySelectionState();
		UpdateOfficerSelectionVisuals();
		UpdateSelectedOfficerDisplay();

		ReindexMissionNpcCells();
		UpdateFogOfWar();
		UpdateMissionCompletionActions();
		RefreshCombatHud();
	}

	private void OnMissionNpcEnteredCell(MissionNpcPawn pawn, Vector2I cell)
	{
		ReindexMissionNpcCells();
		MarkMissionDepthSortingDirty();
		TryCollectLootAtCurrentCell(pawn);
	}

	private void OnMissionNpcReachedCell(MissionNpcPawn pawn, Vector2I cell)
	{
		ReindexMissionNpcCells();
		TryExtractEscortSurvivor(pawn);
		if (pawn != null && pawn.NpcId == _pendingCombatMoveEscortSurvivorId)
		{
			_pendingCombatMoveEscortSurvivorId = string.Empty;
			if (_combatActive && GetActiveCombatEscortSurvivor() == pawn && (pawn.IsExtracted || pawn.CurrentActions <= 0))
			{
				EndCurrentCombatTurn();
			}
		}
		UpdateFogOfWar();
		RefreshCombatHud();
	}
}

