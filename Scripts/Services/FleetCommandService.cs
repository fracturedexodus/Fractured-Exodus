using Godot;
using System.Collections.Generic;
using System.Linq;

public class FleetSelectionState
{
	public bool IsFleetTravelMode;
	public List<Vector2I> SelectedHexes = new List<Vector2I>();
	public MapEntity ShipToOpen;
	public bool ExpandShipMenu;
}

public class FleetMovePlan
{
	public bool Allowed;
	public bool ShowStrandedMenu;
	public string FailureMessage = string.Empty;
	public bool IsGroupMove;
	public bool ShouldAdvanceTurnAfterMovement;
	public Vector2I FromHex;
	public Vector2I ToHex;
	public int MovementCost;
	public List<Vector2I> UpdatedSelection = new List<Vector2I>();
}

public class FleetCommandService
{
	public void RefreshPlayerShipHotkeys(Dictionary<string, int> hotkeys, Dictionary<Vector2I, MapEntity> hexContents)
	{
		HashSet<string> currentPlayerShipNames = hexContents.Values
			.Where(entity => entity.Type == GameConstants.EntityTypes.PlayerFleet && !string.IsNullOrEmpty(entity.Name))
			.Select(entity => entity.Name)
			.ToHashSet();

		List<string> staleShipNames = hotkeys.Keys.Where(name => !currentPlayerShipNames.Contains(name)).ToList();
		foreach (string staleShipName in staleShipNames)
		{
			hotkeys.Remove(staleShipName);
		}

		HashSet<int> usedSlots = hotkeys.Values.ToHashSet();
		foreach (string shipName in currentPlayerShipNames.Where(name => !hotkeys.ContainsKey(name)).OrderBy(name => name))
		{
			for (int slot = 1; slot <= 5; slot++)
			{
				if (usedSlots.Contains(slot)) continue;
				hotkeys[shipName] = slot;
				usedSlots.Add(slot);
				break;
			}
		}
	}

	public List<Vector2I> GetOrderedPlayerShipHexes(Dictionary<Vector2I, MapEntity> hexContents, Dictionary<string, int> hotkeys)
	{
		RefreshPlayerShipHotkeys(hotkeys, hexContents);

		return hexContents
			.Where(kvp => kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet)
			.OrderBy(kvp => hotkeys.TryGetValue(kvp.Value.Name, out int slot) ? slot : int.MaxValue)
			.ThenBy(kvp => kvp.Value.Name)
			.ThenBy(kvp => kvp.Key.X)
			.ThenBy(kvp => kvp.Key.Y)
			.Select(kvp => kvp.Key)
			.ToList();
	}

	public FleetSelectionState BuildFleetTravelSelection(bool inCombat, bool isFleetMoving, Dictionary<Vector2I, MapEntity> hexContents, Dictionary<string, int> hotkeys)
	{
		if (inCombat || isFleetMoving) return null;

		List<Vector2I> playerShipHexes = GetOrderedPlayerShipHexes(hexContents, hotkeys);
		if (playerShipHexes.Count == 0) return null;

		return new FleetSelectionState
		{
			IsFleetTravelMode = true,
			SelectedHexes = playerShipHexes,
			ExpandShipMenu = false
		};
	}

	public Vector2I? TryFindPlayerShipHexByHotkey(int slotNumber, bool inCombat, bool isFleetMoving, Dictionary<Vector2I, MapEntity> hexContents, Dictionary<string, int> hotkeys)
	{
		if (inCombat || isFleetMoving || slotNumber < 1 || slotNumber > 5) return null;

		RefreshPlayerShipHotkeys(hotkeys, hexContents);
		foreach (KeyValuePair<Vector2I, MapEntity> kvp in hexContents)
		{
			if (kvp.Value.Type != GameConstants.EntityTypes.PlayerFleet) continue;
			if (hotkeys.TryGetValue(kvp.Value.Name, out int slot) && slot == slotNumber) return kvp.Key;
		}

		return null;
	}

	public FleetSelectionState BuildSinglePlayerSelection(Vector2I shipHex, bool inCombat, MapEntity activeShip, Dictionary<Vector2I, MapEntity> hexContents, bool openMenu)
	{
		if (!hexContents.ContainsKey(shipHex)) return null;

		MapEntity ship = hexContents[shipHex];
		if (ship.Type != GameConstants.EntityTypes.PlayerFleet) return null;
		if (inCombat && ship != activeShip) return null;

		return new FleetSelectionState
		{
			IsFleetTravelMode = false,
			SelectedHexes = new List<Vector2I> { shipHex },
			ShipToOpen = ship,
			ExpandShipMenu = openMenu && ship.VisualSprite != null && ship.VisualSprite.Visible
		};
	}

	public FleetSelectionState BuildManualPlayerSelection(IEnumerable<Vector2I> selectedShipHexes, Dictionary<Vector2I, MapEntity> hexContents)
	{
		return new FleetSelectionState
		{
			IsFleetTravelMode = false,
			SelectedHexes = selectedShipHexes
				.Where(hex => hexContents.ContainsKey(hex) && hexContents[hex].Type == GameConstants.EntityTypes.PlayerFleet)
				.Distinct()
				.ToList(),
			ExpandShipMenu = false
		};
	}

	public FleetSelectionState BuildClearedSelection(List<Vector2I> existingSelection, bool currentFleetTravelMode, bool deactivateFleetMode)
	{
		return new FleetSelectionState
		{
			IsFleetTravelMode = deactivateFleetMode ? false : currentFleetTravelMode,
			SelectedHexes = new List<Vector2I>(),
			ExpandShipMenu = false
		};
	}

	public FleetMovePlan BuildMovePlan(
		List<Vector2I> selectedHexes,
		Vector2I targetHex,
		bool inCombat,
		float currentFuel,
		Dictionary<Vector2I, Node2D> hexGrid,
		Dictionary<Vector2I, MapEntity> hexContents,
		System.Func<Vector2I, bool> isHexWalkable,
		System.Func<Vector2I, int, Dictionary<Vector2I, int>> getReachableHexes)
	{
		FleetMovePlan plan = new FleetMovePlan();
		if (selectedHexes == null || selectedHexes.Count == 0 || !hexGrid.ContainsKey(targetHex)) return plan;

		float totalFuelNeeded = 0f;
		bool containsPlayerFleet = false;

		foreach (Vector2I selectedHex in selectedHexes)
		{
			if (!hexContents.ContainsKey(selectedHex) || hexContents[selectedHex].Type != GameConstants.EntityTypes.PlayerFleet) continue;
			totalFuelNeeded += HexMath.HexDistance(selectedHex, targetHex) * 0.25f;
			containsPlayerFleet = true;
		}

		if (!containsPlayerFleet) return plan;
		if (!inCombat && totalFuelNeeded == 0f) return plan;

		if (!inCombat)
		{
			if (currentFuel < 0.25f)
			{
				plan.ShowStrandedMenu = true;
				return plan;
			}

			if (currentFuel < totalFuelNeeded)
			{
				plan.FailureMessage = $"*** MOVEMENT ABORTED: INSUFFICIENT FUEL ({totalFuelNeeded} {GameConstants.ResourceKeys.RawMaterials} Req) ***";
				return plan;
			}
		}

		if (selectedHexes.Count == 1)
		{
			Vector2I shipHex = selectedHexes[0];
			if (!hexContents.ContainsKey(shipHex) || hexContents[shipHex].Type != GameConstants.EntityTypes.PlayerFleet) return plan;

			MapEntity ship = hexContents[shipHex];
			if (!inCombat)
			{
				if (!isHexWalkable(targetHex)) return plan;

				plan.Allowed = true;
				plan.FromHex = shipHex;
				plan.ToHex = targetHex;
				plan.UpdatedSelection = new List<Vector2I> { targetHex };
				plan.ShouldAdvanceTurnAfterMovement = true;
				return plan;
			}

			Dictionary<Vector2I, int> reachable = getReachableHexes(shipHex, ship.CurrentActions);
			if (!reachable.ContainsKey(targetHex)) return plan;

			plan.Allowed = true;
			plan.FromHex = shipHex;
			plan.ToHex = targetHex;
			plan.MovementCost = reachable[targetHex];
			plan.UpdatedSelection = new List<Vector2I> { targetHex };
			return plan;
		}

		if (!inCombat)
		{
			plan.Allowed = true;
			plan.IsGroupMove = true;
			plan.ShouldAdvanceTurnAfterMovement = true;
		}

		return plan;
	}
}
