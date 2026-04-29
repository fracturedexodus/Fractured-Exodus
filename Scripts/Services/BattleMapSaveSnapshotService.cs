using Godot;
using System.Collections.Generic;
using System.Linq;

public class BattleMapSaveSnapshotService
{
	public void CaptureSnapshot(
		GlobalData globalData,
		int currentTurn,
		bool inCombat,
		int currentQueueIndex,
		IEnumerable<Vector2I> asteroidHexes,
		IEnumerable<Vector2I> radiationHexes,
		IEnumerable<Vector2I> exploredHexes,
		Dictionary<Vector2I, MapEntity> hexContents)
	{
		if (globalData == null) return;

		globalData.CurrentTurn = currentTurn;
		globalData.InCombat = inCombat;
		globalData.CurrentQueueIndex = currentQueueIndex;

		if (!string.IsNullOrEmpty(globalData.SavedSystem) && globalData.ExploredSystems.ContainsKey(globalData.SavedSystem))
		{
			SystemData currentSystem = globalData.ExploredSystems[globalData.SavedSystem];
			currentSystem.AsteroidHexes = (asteroidHexes ?? Enumerable.Empty<Vector2I>()).ToList();
			currentSystem.RadiationHexes = (radiationHexes ?? Enumerable.Empty<Vector2I>()).ToList();
			currentSystem.ExploredHexes = (exploredHexes ?? Enumerable.Empty<Vector2I>()).ToList();
		}

		Godot.Collections.Array playerState = BuildShipStateArray(hexContents, GameConstants.EntityTypes.PlayerFleet);
		Godot.Collections.Array enemyState = BuildShipStateArray(hexContents, GameConstants.EntityTypes.EnemyFleet);
		globalData.SavedFleetState = playerState;

		if (!string.IsNullOrEmpty(globalData.SavedSystem) && globalData.ExploredSystems.ContainsKey(globalData.SavedSystem))
		{
			globalData.ExploredSystems[globalData.SavedSystem].EnemyFleets = enemyState;
		}
	}

	private static Godot.Collections.Array BuildShipStateArray(Dictionary<Vector2I, MapEntity> hexContents, string entityType)
	{
		List<ShipStateSaveData> ships = new List<ShipStateSaveData>();
		foreach (KeyValuePair<Vector2I, MapEntity> kvp in hexContents)
		{
			if (kvp.Value.Type != entityType) continue;

			ships.Add(new ShipStateSaveData
			{
				Name = kvp.Value.Name,
				Q = kvp.Key.X,
				R = kvp.Key.Y,
				CurrentHP = kvp.Value.CurrentHP,
				MaxHP = kvp.Value.MaxHP,
				CurrentShields = kvp.Value.CurrentShields,
				MaxShields = kvp.Value.MaxShields,
				MaxActions = kvp.Value.MaxActions,
				CurrentActions = kvp.Value.CurrentActions,
				CurrentInitiativeRoll = kvp.Value.CurrentInitiativeRoll
			});
		}

		return CampaignSaveData.ToShipStateArray(ships);
	}
}
