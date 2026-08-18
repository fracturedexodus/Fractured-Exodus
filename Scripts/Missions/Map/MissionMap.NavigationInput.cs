using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private void UpdateCameraPan(float delta)
	{
		if (_camera == null || _isPanning || _isSelectionDragging)
		{
			return;
		}

		Vector2 input = Vector2.Zero;
		Vector2 mousePosition = GetViewport().GetMousePosition();
		Vector2 screenSize = GetViewportRect().Size;

		if (mousePosition.X >= 0f && mousePosition.X <= screenSize.X && mousePosition.Y >= 0f && mousePosition.Y <= screenSize.Y)
		{
			if (mousePosition.X < CameraEdgePanMargin)
			{
				input.X -= 1f;
			}
			else if (mousePosition.X > screenSize.X - CameraEdgePanMargin)
			{
				input.X += 1f;
			}

			if (mousePosition.Y < CameraEdgePanMargin)
			{
				input.Y -= 1f;
			}
			else if (mousePosition.Y > screenSize.Y - CameraEdgePanMargin)
			{
				input.Y += 1f;
			}
		}

		if (input == Vector2.Zero)
		{
			return;
		}

		_camera.Position += input.Normalized() * CameraPanSpeed * delta * (1.0f / _camera.Zoom.X);
	}

	private void UpdateKeyboardMovementCameraFollow()
	{
		if (_camera == null || _isPanning || _isSelectionDragging || GetHeldOfficerMoveDirection() == Vector2I.Zero)
		{
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		List<Node2D> movingSelectedUnits = GetSelectedFriendlyUnits()
			.Select(unit => unit as Node2D)
			.Where(unit => unit != null && unit switch
			{
				OfficerPawn officer => officer.IsMoving,
				MissionNpcPawn survivor => survivor.IsMoving,
				_ => false
			})
			.ToList();
		if (movingSelectedUnits.Count == 0)
		{
			return;
		}

		bool reachedScreenEdge = movingSelectedUnits.Any(unit =>
		{
			Vector2 screenPosition = GetViewport().GetCanvasTransform() * unit.GlobalPosition;
			return screenPosition.X <= CameraKeyboardFollowEdgeMargin
				|| screenPosition.X >= viewportSize.X - CameraKeyboardFollowEdgeMargin
				|| screenPosition.Y <= CameraKeyboardFollowEdgeMargin
				|| screenPosition.Y >= viewportSize.Y - CameraKeyboardFollowEdgeMargin;
		});
		if (!reachedScreenEdge)
		{
			return;
		}

		Vector2 focusPosition = Vector2.Zero;
		foreach (Node2D unit in movingSelectedUnits)
		{
			focusPosition += unit.GlobalPosition;
		}

		_camera.Position = focusPosition / movingSelectedUnits.Count;
	}

	private void UpdateSelectionDrag()
	{
		if (!_isSelectionDragging || _selectionBox == null)
		{
			return;
		}

		_selectionBox.EndPos = GetGlobalMousePosition();
		_selectionBox.QueueRedraw();
	}

	private void FinalizeLeftMouseAction()
	{
		Rect2 selectionRect = new Rect2(_selectionDragStartWorldPosition, GetGlobalMousePosition() - _selectionDragStartWorldPosition).Abs();
		_isSelectionDragging = false;
		if (_selectionBox != null)
		{
			_selectionBox.IsDragging = false;
			_selectionBox.QueueRedraw();
		}

		ClearInteractionMenu();

		if (!_combatActive && selectionRect.Area >= 100f)
		{
			SelectControllableUnitsInRect(selectionRect);
			return;
		}

		ProcessLeftClickAction();
	}

	private void ProcessLeftClickAction()
	{
		if (_combatActive && !IsPlayerTurnActive())
		{
			return;
		}

		Vector2I clickedMovementCell = _roomBuilder != null && _isoWorld != null
			? ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition())))
			: Vector2I.Zero;
		if (TrySelectControllableUnitAtMouse(clickedMovementCell))
		{
			return;
		}

		List<object> selectedUnits = GetSelectedFriendlyUnits();
		if (!_combatActive && selectedUnits.Count > 1 && TryMoveSelectedFriendlyGroup())
		{
			return;
		}

		OfficerPawn activeOfficer = GetSelectedOfficer();
		MissionNpcPawn activeSurvivor = GetSelectedEscortSurvivor();
		if (activeOfficer == null && activeSurvivor == null)
		{
			return;
		}

		if (activeSurvivor != null)
		{
			TryMoveSelectedEscortSurvivor();
			return;
		}

		if (_combatActive && activeOfficer == GetActiveCombatOfficer())
		{
			if (_selectedCombatActionMode == MissionPlayerCombatActionMode.Attack)
			{
				TryHandleCombatAttackClick(activeOfficer);
				return;
			}

			if (TryHandleInteractionClick(activeOfficer))
			{
				return;
			}

			TryMoveSelectedOfficer();
			return;
		}

		if (TryHandleInteractionClick(activeOfficer))
		{
			return;
		}

		if (_combatActive && TryHandleCombatAttackClick(activeOfficer))
		{
			return;
		}

		TryMoveSelectedOfficer();
	}

	private void SelectControllableUnitsInRect(Rect2 selectionRect)
	{
		if (_combatActive)
		{
			return;
		}

		List<OfficerPawn> officersInRect = _officerPawns
			.Where(officer => officer != null && !officer.IsDead && selectionRect.HasPoint(officer.GlobalPosition))
			.ToList();
		List<MissionNpcPawn> survivorsInRect = GetAliveEscortSurvivors()
			.Where(survivor => selectionRect.HasPoint(survivor.GlobalPosition))
			.ToList();
		if (officersInRect.Count == 0 && survivorsInRect.Count == 0)
		{
			return;
		}

		object primaryUnit = officersInRect.Cast<object>().Concat(survivorsInRect).FirstOrDefault();
		SetSelectedFriendlyUnits(officersInRect, survivorsInRect, primaryUnit);
	}

	private bool TryHandleOfficerMoveKey(Key keycode)
	{
		Vector2I direction = keycode switch
		{
			Key.W => new Vector2I(0, -1),
			Key.S => new Vector2I(0, 1),
			Key.A => new Vector2I(-1, 0),
			Key.D => new Vector2I(1, 0),
			_ => Vector2I.Zero
		};

		if (direction == Vector2I.Zero)
		{
			return false;
		}

		if (!_combatActive && GetSelectedFriendlyUnits().Count > 1)
		{
			return TryMoveSelectedFriendlyGroupByDirection(direction);
		}

		if (GetSelectedEscortSurvivor() != null)
		{
			return TryMoveSelectedEscortSurvivorByDirection(direction);
		}

		return TryMoveSelectedOfficerByDirection(direction);
	}

	private void UpdateHeldOfficerMovement()
	{
		if (_missionGameOver
			|| _isPanning
			|| _isSelectionDragging
			|| (_dialogueUi?.IsConversationOpen ?? false)
			|| (_missionUi?.IsStoryEventVisible ?? false))
		{
			return;
		}

		object primaryUnit = GetPrimarySelectedFriendlyUnit();
		if (primaryUnit == null)
		{
			return;
		}

		Vector2I direction = GetHeldOfficerMoveDirection();
		if (direction == Vector2I.Zero)
		{
			return;
		}

		if (!_combatActive && GetSelectedFriendlyUnits().Count > 1)
		{
			TryMoveSelectedFriendlyGroupByDirection(direction);
			return;
		}

		switch (primaryUnit)
		{
			case MissionNpcPawn survivor when !survivor.IsMoving:
				TryMoveSelectedEscortSurvivorByDirection(direction);
				break;
			case OfficerPawn officer when !officer.IsMoving:
				TryMoveSelectedOfficerByDirection(direction);
				break;
		}
	}

	private Vector2I GetHeldOfficerMoveDirection()
	{
		if (Input.IsKeyPressed(Key.W))
		{
			return new Vector2I(0, -1);
		}

		if (Input.IsKeyPressed(Key.S))
		{
			return new Vector2I(0, 1);
		}

		if (Input.IsKeyPressed(Key.A))
		{
			return new Vector2I(-1, 0);
		}

		if (Input.IsKeyPressed(Key.D))
		{
			return new Vector2I(1, 0);
		}

		return Vector2I.Zero;
	}

	private void TryMoveSelectedOfficer()
	{
		OfficerPawn activeOfficer = GetSelectedOfficer();
		if (activeOfficer == null || _roomBuilder == null || _isoWorld == null)
		{
			return;
		}

		Vector2 localMousePosition = _isoWorld.ToLocal(GetGlobalMousePosition());
		Vector2I targetCell = ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(localMousePosition));
		TryMoveOfficerToCell(activeOfficer, targetCell);
	}

	private void TryMoveSelectedEscortSurvivor()
	{
		MissionNpcPawn survivor = GetSelectedEscortSurvivor();
		if (survivor == null || _roomBuilder == null || _isoWorld == null)
		{
			return;
		}

		Vector2 localMousePosition = _isoWorld.ToLocal(GetGlobalMousePosition());
		Vector2I targetCell = ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(localMousePosition));
		TryMoveEscortSurvivorToCell(survivor, targetCell);
	}

	private bool TryMoveSelectedFriendlyGroup()
	{
		if (_roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		List<object> selectedUnits = GetSelectedFriendlyUnits()
			.Where(unit => !IsFriendlyUnitMoving(unit))
			.ToList();
		if (selectedUnits.Count <= 1)
		{
			return false;
		}

		Vector2I targetCell = ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition())));
		List<Vector2I> candidateCells = GetGroupDestinationCandidates(targetCell, selectedUnits.Count * 6);
		if (candidateCells.Count == 0)
		{
			return false;
		}

		HashSet<string> selectedOfficerIds = GetSelectedOfficers()
			.Where(officer => !string.IsNullOrWhiteSpace(officer.OfficerID))
			.Select(officer => officer.OfficerID)
			.ToHashSet();
		HashSet<string> selectedSurvivorIds = GetSelectedEscortSurvivors()
			.Where(survivor => !string.IsNullOrWhiteSpace(survivor.NpcId))
			.Select(survivor => survivor.NpcId)
			.ToHashSet();
		HashSet<Vector2I> reservedDestinations = new HashSet<Vector2I>();
		bool movedAnyUnit = false;

		foreach (object unit in OrderFriendlyUnitsForGroupMove(selectedUnits, targetCell))
		{
			Vector2I unitCell = GetFriendlyUnitCurrentCell(unit);
			Vector2I? destination = candidateCells
				.Where(candidate => !reservedDestinations.Contains(candidate))
				.Where(candidate => !IsCellOccupiedByLivingActor(candidate, unit as OfficerPawn, unit as MissionNpcPawn, selectedOfficerIds, selectedSurvivorIds))
				.Where(candidate => TryGetFriendlyUnitMovementPath(unit, candidate, selectedOfficerIds, selectedSurvivorIds, out List<Vector2I> _))
				.OrderBy(candidate => GetMovementStepDistance(candidate, targetCell))
				.ThenBy(candidate => GetMovementStepDistance(unitCell, candidate))
				.Cast<Vector2I?>()
				.FirstOrDefault();
			if (!destination.HasValue)
			{
				continue;
			}

			if (TryMoveFriendlyUnitToCell(unit, destination.Value, selectedOfficerIds, selectedSurvivorIds))
			{
				reservedDestinations.Add(destination.Value);
				movedAnyUnit = true;
			}
		}

		return movedAnyUnit;
	}

	private bool TryMoveSelectedFriendlyGroupByDirection(Vector2I direction)
	{
		if (_combatActive || _roomBuilder == null)
		{
			return false;
		}

		List<object> selectedUnits = GetSelectedFriendlyUnits()
			.Where(unit => !IsFriendlyUnitMoving(unit))
			.ToList();
		if (selectedUnits.Count <= 1)
		{
			return false;
		}

		HashSet<string> selectedOfficerIds = GetSelectedOfficers()
			.Where(officer => !string.IsNullOrWhiteSpace(officer.OfficerID))
			.Select(officer => officer.OfficerID)
			.ToHashSet();
		HashSet<string> selectedSurvivorIds = GetSelectedEscortSurvivors()
			.Where(survivor => !string.IsNullOrWhiteSpace(survivor.NpcId))
			.Select(survivor => survivor.NpcId)
			.ToHashSet();
		HashSet<Vector2I> movingOrigins = selectedUnits
			.Select(GetFriendlyUnitCurrentCell)
			.ToHashSet();
		HashSet<Vector2I> reservedDestinations = new HashSet<Vector2I>();
		List<(object Unit, Vector2I Origin, Vector2I Destination)> movePlans = new List<(object, Vector2I, Vector2I)>();

		foreach (object unit in OrderFriendlyUnitsForDirectionalGroupMove(selectedUnits, direction))
		{
			Vector2I origin = GetFriendlyUnitCurrentCell(unit);
			Vector2I destination = ResolveMovementTargetCell(origin + direction);
			if (!_roomBuilder.IsWalkableMovementCell(destination))
			{
				continue;
			}

			if (reservedDestinations.Contains(destination))
			{
				continue;
			}

			if (IsCellOccupiedByLivingActor(destination, unit as OfficerPawn, unit as MissionNpcPawn, selectedOfficerIds, selectedSurvivorIds)
				|| IsCellOccupiedBySelectedUnitNotMoving(destination, movingOrigins, movePlans))
			{
				continue;
			}

			if (!TryGetFriendlyUnitMovementPath(unit, destination, selectedOfficerIds, selectedSurvivorIds, out List<Vector2I> pathCells)
				|| pathCells.Count <= 1)
			{
				continue;
			}

			movePlans.Add((unit, origin, destination));
			reservedDestinations.Add(destination);
		}

		bool movedAnyUnit = false;
		foreach ((object unit, Vector2I _, Vector2I destination) in movePlans)
		{
			if (TryMoveFriendlyUnitToCell(unit, destination, selectedOfficerIds, selectedSurvivorIds))
			{
				movedAnyUnit = true;
			}
		}

		return movedAnyUnit;
	}

	private bool IsFriendlyUnitMoving(object unit)
	{
		return unit switch
		{
			OfficerPawn officer => officer.IsMoving,
			MissionNpcPawn survivor => survivor.IsMoving,
			_ => false
		};
	}

	private Vector2I GetFriendlyUnitCurrentCell(object unit)
	{
		return unit switch
		{
			OfficerPawn officer => officer.CurrentCell,
			MissionNpcPawn survivor => survivor.CurrentCell,
			_ => Vector2I.Zero
		};
	}

	private bool TryGetFriendlyUnitMovementPath(
		object unit,
		Vector2I destination,
		IReadOnlyCollection<string> ignoredOfficerIds,
		IReadOnlyCollection<string> ignoredSurvivorIds,
		out List<Vector2I> pathCells)
	{
		switch (unit)
		{
			case OfficerPawn officer:
				return TryGetTraversableMovementPath(officer.CurrentCell, destination, officer, null, ignoredOfficerIds, ignoredSurvivorIds, out pathCells);
			case MissionNpcPawn survivor:
				return TryGetTraversableMovementPath(survivor.CurrentCell, destination, null, survivor, ignoredOfficerIds, ignoredSurvivorIds, out pathCells);
			default:
				pathCells = new List<Vector2I>();
				return false;
		}
	}

	private bool TryMoveFriendlyUnitToCell(
		object unit,
		Vector2I destination,
		IReadOnlyCollection<string> ignoredOfficerIds,
		IReadOnlyCollection<string> ignoredSurvivorIds)
	{
		switch (unit)
		{
			case OfficerPawn officer:
				return TryMoveOfficerToCell(officer, destination, ignoredOfficerIds, true, ignoredSurvivorIds);
			case MissionNpcPawn survivor:
				return TryMoveEscortSurvivorToCell(survivor, destination, false, ignoredOfficerIds, ignoredSurvivorIds);
			default:
				return false;
		}
	}

	private List<object> OrderFriendlyUnitsForGroupMove(List<object> units, Vector2I targetCell)
	{
		object primaryUnit = GetPrimarySelectedFriendlyUnit();
		return units
			.OrderByDescending(unit => unit == primaryUnit)
			.ThenBy(unit => GetMovementStepDistance(GetFriendlyUnitCurrentCell(unit), targetCell))
			.ToList();
	}

	private List<object> OrderFriendlyUnitsForDirectionalGroupMove(List<object> units, Vector2I direction)
	{
		return direction switch
		{
			var d when d == new Vector2I(1, 0) => units.OrderByDescending(unit => GetFriendlyUnitCurrentCell(unit).X).ThenBy(unit => GetFriendlyUnitCurrentCell(unit).Y).ToList(),
			var d when d == new Vector2I(-1, 0) => units.OrderBy(unit => GetFriendlyUnitCurrentCell(unit).X).ThenBy(unit => GetFriendlyUnitCurrentCell(unit).Y).ToList(),
			var d when d == new Vector2I(0, 1) => units.OrderByDescending(unit => GetFriendlyUnitCurrentCell(unit).Y).ThenBy(unit => GetFriendlyUnitCurrentCell(unit).X).ToList(),
			var d when d == new Vector2I(0, -1) => units.OrderBy(unit => GetFriendlyUnitCurrentCell(unit).Y).ThenBy(unit => GetFriendlyUnitCurrentCell(unit).X).ToList(),
			_ => units
		};
	}

	private static bool IsCellOccupiedBySelectedUnitNotMoving(
		Vector2I cell,
		HashSet<Vector2I> movingOrigins,
		List<(object Unit, Vector2I Origin, Vector2I Destination)> movePlans)
	{
		if (!movingOrigins.Contains(cell))
		{
			return false;
		}

		return movePlans.All(plan => plan.Origin != cell);
	}

	private List<Vector2I> GetGroupDestinationCandidates(Vector2I targetCell, int maxCandidates)
	{
		List<Vector2I> candidates = new List<Vector2I>();
		if (_roomBuilder == null || maxCandidates <= 0)
		{
			return candidates;
		}

		Queue<Vector2I> frontier = new Queue<Vector2I>();
		HashSet<Vector2I> visited = new HashSet<Vector2I>();
		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		frontier.Enqueue(targetCell);
		visited.Add(targetCell);

		while (frontier.Count > 0 && candidates.Count < maxCandidates)
		{
			Vector2I current = frontier.Dequeue();
			if (_roomBuilder.IsWalkableMovementCell(current))
			{
				candidates.Add(current);
			}

			foreach (Vector2I direction in directions)
			{
				Vector2I next = current + direction;
				if (visited.Add(next))
				{
					frontier.Enqueue(next);
				}
			}
		}

		return candidates;
	}

	private bool TryMoveSelectedOfficerByDirection(Vector2I tileDirection)
	{
		OfficerPawn activeOfficer = GetSelectedOfficer();
		if (activeOfficer == null || _roomBuilder == null)
		{
			return false;
		}

		if (activeOfficer.IsMoving)
		{
			return false;
		}

		if (_combatActive && activeOfficer != GetActiveCombatOfficer())
		{
			return false;
		}

		Vector2I targetCell = ResolveMovementTargetCell(activeOfficer.CurrentCell + tileDirection);
		return TryMoveOfficerToCell(activeOfficer, targetCell);
	}

	private bool TryMoveSelectedEscortSurvivorByDirection(Vector2I tileDirection)
	{
		MissionNpcPawn survivor = GetSelectedEscortSurvivor();
		if (survivor == null || _roomBuilder == null)
		{
			return false;
		}

		if (survivor.IsMoving)
		{
			return false;
		}

		if (_combatActive && survivor != GetActiveCombatEscortSurvivor())
		{
			return false;
		}

		Vector2I targetCell = ResolveMovementTargetCell(survivor.CurrentCell + tileDirection);
		return TryMoveEscortSurvivorToCell(survivor, targetCell);
	}

	private bool TryMoveOfficerToCell(OfficerPawn officer, Vector2I targetCell)
	{
		return TryMoveOfficerToCell(officer, targetCell, null, true);
	}

	private bool TryMoveOfficerToCell(OfficerPawn officer, Vector2I targetCell, IReadOnlyCollection<string> ignoredOfficerIds)
	{
		return TryMoveOfficerToCell(officer, targetCell, ignoredOfficerIds, true);
	}

	private bool TryMoveOfficerToCell(OfficerPawn officer, Vector2I targetCell, IReadOnlyCollection<string> ignoredOfficerIds, bool allowPropRedirect, IReadOnlyCollection<string> ignoredSurvivorIds = null)
	{
		if (officer == null || _roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		targetCell = ResolveMovementTargetCell(targetCell);

		if (IsMovementCellBlockedByProp(targetCell))
		{
			if (allowPropRedirect
				&& (TryHandleBlockedPropMovement(officer, targetCell, ignoredOfficerIds)
					|| TryHandleBlockedInteractionMovement(officer, targetCell, ignoredOfficerIds)))
			{
				return true;
			}

			return false;
		}

		if (IsCellOccupiedByLivingActor(targetCell, officer, null, ignoredOfficerIds, ignoredSurvivorIds))
		{
			return false;
		}

		if (!TryGetTraversableMovementPath(officer.CurrentCell, targetCell, officer, null, ignoredOfficerIds, ignoredSurvivorIds, out List<Vector2I> pathCells))
		{
			return false;
		}

		if (pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells
			.Skip(1)
			.TakeWhile(cell => !IsCellOccupiedByLivingActor(cell, officer, null, ignoredOfficerIds, ignoredSurvivorIds) && !IsMovementCellBlockedByProp(cell))
			.ToList();
		if (steppedCells.Count == 0)
		{
			return false;
		}

		targetCell = steppedCells[^1];
		string targetGridCoordinate = FormatMovementGridCoordinate(targetCell);

		if (_combatActive)
		{
			int maxMovementSteps = GetMaxMovementStepsForActions(officer.CurrentActions);
			steppedCells = steppedCells.Take(maxMovementSteps).ToList();
			int moveCost = GetMovementCostForPathSteps(steppedCells.Count);
			if (!officer.CanSpendActions(moveCost))
			{
				return false;
			}

			targetCell = steppedCells[^1];
			officer.SpendActions(moveCost);
			_pendingCombatMoveOfficerId = officer.OfficerID;
			_pendingCombatMoveCost = moveCost;
			targetGridCoordinate = FormatMovementGridCoordinate(targetCell);
			AppendCombatLog($"{officer.OfficerName} repositions {moveCost} cell{(moveCost == 1 ? string.Empty : "s")} toward {targetGridCoordinate}, conserving {officer.WeaponName.ToLowerInvariant()} fire for the next opening.");
		}
		else
		{
			AppendActionLog($"{officer.OfficerName} moves to {targetGridCoordinate}.");
		}

		List<Vector2> pathPoints = steppedCells
			.Select(GetMovementCellGlobalPosition)
			.ToList();
		officer.MoveAlongPath(pathPoints, steppedCells, targetCell);
		return true;
	}

	private bool TryMoveEscortSurvivorToCell(MissionNpcPawn survivor, Vector2I targetCell)
	{
		return TryMoveEscortSurvivorToCell(survivor, targetCell, _combatActive);
	}

	private bool TryMoveEscortSurvivorToCell(
		MissionNpcPawn survivor,
		Vector2I targetCell,
		bool useCombatActions,
		IReadOnlyCollection<string> ignoredOfficerIds = null,
		IReadOnlyCollection<string> ignoredSurvivorIds = null)
	{
		if (!IsActiveEscortSurvivor(survivor) || _roomBuilder == null || _isoWorld == null)
		{
			return false;
		}

		if (survivor.IsMoving)
		{
			return false;
		}

		if (_combatActive && survivor != GetActiveCombatEscortSurvivor())
		{
			return false;
		}

		targetCell = ResolveMovementTargetCell(targetCell);
		if (IsMovementCellBlockedByProp(targetCell) || IsCellOccupiedByLivingActor(targetCell, null, survivor, ignoredOfficerIds, ignoredSurvivorIds))
		{
			return false;
		}

		if (!TryGetTraversableMovementPath(survivor.CurrentCell, targetCell, null, survivor, ignoredOfficerIds, ignoredSurvivorIds, out List<Vector2I> pathCells))
		{
			return false;
		}

		if (pathCells.Count <= 1)
		{
			return false;
		}

		List<Vector2I> steppedCells = pathCells
			.Skip(1)
			.TakeWhile(cell => !IsCellOccupiedByLivingActor(cell, null, survivor, ignoredOfficerIds, ignoredSurvivorIds) && !IsMovementCellBlockedByProp(cell))
			.ToList();
		if (steppedCells.Count == 0)
		{
			return false;
		}

		targetCell = steppedCells[^1];
		string targetGridCoordinate = FormatMovementGridCoordinate(targetCell);
		if (useCombatActions)
		{
			int maxMovementSteps = GetMaxMovementStepsForActions(survivor.CurrentActions);
			steppedCells = steppedCells.Take(maxMovementSteps).ToList();
			if (steppedCells.Count == 0)
			{
				return false;
			}

			int moveCost = GetMovementCostForPathSteps(steppedCells.Count);
			if (!survivor.CanSpendActions(moveCost))
			{
				return false;
			}

			targetCell = steppedCells[^1];
			survivor.SpendActions(moveCost);
			_pendingCombatMoveEscortSurvivorId = survivor.NpcId;
			targetGridCoordinate = FormatMovementGridCoordinate(targetCell);
			AppendCombatLog($"{survivor.DisplayName} moves {moveCost} cell{(moveCost == 1 ? string.Empty : "s")} toward {targetGridCoordinate}.");
		}
		else
		{
			AppendActionLog($"{survivor.DisplayName} moves to {targetGridCoordinate}.");
		}

		List<Vector2> pathPoints = steppedCells
			.Select(GetMovementCellGlobalPosition)
			.ToList();
		survivor.MoveAlongPath(pathPoints, steppedCells, targetCell);
		return true;
	}
}

