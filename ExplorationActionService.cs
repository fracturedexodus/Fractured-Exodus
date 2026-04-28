using Godot;
using System;
using System.Collections.Generic;

public class ScanActionResult
{
	public bool Allowed { get; set; }
	public string FailureMessage { get; set; } = "";
	public bool TriggerConversation { get; set; }
	public string ConversationId { get; set; } = "";
	public float EnergyCost { get; set; }
	public string SizeClass { get; set; } = "";
	public int ProjectedSalvageTurns { get; set; }
}

public class SalvageActionResult
{
	public bool Allowed { get; set; }
	public string FailureMessage { get; set; } = "";
	public bool Ambushed { get; set; }
	public float RawCost { get; set; }
	public int TurnsNeeded { get; set; }
	public float RawYield { get; set; }
	public float EnergyYield { get; set; }
	public float TechYield { get; set; }
}

public class ShipRepairResult
{
	public bool Allowed { get; set; }
	public int HealAmount { get; set; }
	public int HealedHull { get; set; }
	public int HealedShields { get; set; }
}

public class FleetRepairResult
{
	public bool Allowed { get; set; }
	public int TurnsNeeded { get; set; }
	public bool Ambushed { get; set; }
	public bool FullyRepaired { get; set; }
}

public class ExplorationActionService
{
	private readonly GlobalData _globalData;

	public ExplorationActionService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public ScanActionResult PerformScan(MapEntity ship, MapEntity planet, PlanetData planetData, bool allowConversation)
	{
		ScanActionResult result = new ScanActionResult { Allowed = false };
		if (ship == null || planet == null || ship.CurrentActions < 1)
		{
			return result;
		}

		if (planetData != null && planetData.HasBeenScanned)
		{
			return result;
		}

		const float energyCost = 0.5f;
		if (_globalData != null)
		{
			float currentCores = _globalData.FleetResources["Energy Cores"].AsSingle();
			if (currentCores < energyCost)
			{
				result.FailureMessage = "*** SCAN FAILED: INSUFFICIENT ENERGY CORES (0.5 Req) ***";
				return result;
			}

			_globalData.FleetResources["Energy Cores"] = currentCores - energyCost;
		}

		ship.CurrentActions -= 1;
		if (planetData != null)
		{
			planetData.HasBeenScanned = true;
		}

		Random rng = new Random();
		float scale = planet.VisualSprite.Scale.X;
		result.Allowed = true;
		result.EnergyCost = energyCost;
		result.TriggerConversation = allowConversation && rng.Next(0, 100) < 30;
		result.ConversationId = "Stranded_Miner";
		result.SizeClass = scale > 0.6f ? "Massive" : (scale > 0.5f ? "Standard" : "Dwarf");
		result.ProjectedSalvageTurns = Mathf.Max(1, Mathf.RoundToInt(scale * 5f));
		return result;
	}

	public SalvageActionResult PerformSalvage(
		MapEntity ship,
		MapEntity planet,
		PlanetData planetData,
		Dictionary<Vector2I, MapEntity> hexContents,
		int scanningRange,
		Action advanceExplorationTurn)
	{
		SalvageActionResult result = new SalvageActionResult { Allowed = false };
		if (ship == null || planet == null || ship.CurrentActions < 1)
		{
			return result;
		}

		if (planetData != null && planetData.HasBeenSalvaged)
		{
			return result;
		}

		const float rawCost = 1.0f;
		if (_globalData != null)
		{
			float currentRaw = _globalData.FleetResources["Raw Materials"].AsSingle();
			if (currentRaw < rawCost)
			{
				result.FailureMessage = "*** SALVAGE FAILED: INSUFFICIENT RAW MATERIALS (1.0 Req) ***";
				return result;
			}

			_globalData.FleetResources["Raw Materials"] = currentRaw - rawCost;
		}

		float scale = planet.VisualSprite.Scale.X;
		result.TurnsNeeded = Mathf.Max(1, Mathf.RoundToInt(scale * 5f));
		result.RawCost = rawCost;
		ship.CurrentActions = 0;
		result.Allowed = true;

		for (int i = 0; i < result.TurnsNeeded; i++)
		{
			advanceExplorationTurn?.Invoke();
			if (HasHostilesInRange(hexContents, scanningRange))
			{
				result.Ambushed = true;
				return result;
			}
		}

		if (planetData != null)
		{
			planetData.HasBeenSalvaged = true;
		}

		Random rng = new Random();
		result.RawYield = rng.Next(50, 151);
		result.EnergyYield = rng.Next(1, 9);
		result.TechYield = rng.Next(1, 4);

		if (_globalData != null)
		{
			_globalData.FleetResources["Raw Materials"] = _globalData.FleetResources["Raw Materials"].AsSingle() + result.RawYield;
			_globalData.FleetResources["Energy Cores"] = _globalData.FleetResources["Energy Cores"].AsSingle() + result.EnergyYield;
			_globalData.FleetResources["Ancient Tech"] = _globalData.FleetResources["Ancient Tech"].AsSingle() + result.TechYield;
		}

		return result;
	}

	public ShipRepairResult PerformShipRepair(MapEntity ship)
	{
		ShipRepairResult result = new ShipRepairResult { Allowed = false };
		if (ship == null || ship.IsDead || ship.Type != "Player Fleet" || ship.CurrentActions < 2)
		{
			return result;
		}

		ship.CurrentActions -= 2;

		Random rng = new Random();
		result.HealAmount = rng.Next(15, 30);

		int remainingRepair = result.HealAmount;
		int missingHull = ship.MaxHP - ship.CurrentHP;
		if (missingHull > 0)
		{
			result.HealedHull = Mathf.Min(remainingRepair, missingHull);
			ship.CurrentHP += result.HealedHull;
			remainingRepair -= result.HealedHull;
		}

		int missingShields = ship.MaxShields - ship.CurrentShields;
		if (remainingRepair > 0 && missingShields > 0)
		{
			result.HealedShields = Mathf.Min(remainingRepair, missingShields);
			ship.CurrentShields += result.HealedShields;
		}

		result.Allowed = true;
		return result;
	}

	public FleetRepairResult PerformFleetRepair(
		Dictionary<Vector2I, MapEntity> hexContents,
		int scanningRange,
		Action advanceExplorationTurn)
	{
		FleetRepairResult result = new FleetRepairResult { Allowed = false };
		int totalMissing = 0;
		foreach (var kvp in hexContents)
		{
			if (kvp.Value.Type == "Player Fleet")
			{
				totalMissing += (kvp.Value.MaxHP - kvp.Value.CurrentHP) + (kvp.Value.MaxShields - kvp.Value.CurrentShields);
			}
		}

		if (totalMissing == 0)
		{
			return result;
		}

		result.Allowed = true;
		result.TurnsNeeded = (totalMissing / 20) + 1;

		for (int i = 0; i < result.TurnsNeeded; i++)
		{
			advanceExplorationTurn?.Invoke();

			foreach (var kvp in hexContents)
			{
				if (kvp.Value.Type == "Player Fleet")
				{
					kvp.Value.CurrentHP = Mathf.Min(kvp.Value.CurrentHP + 15, kvp.Value.MaxHP);
					kvp.Value.CurrentShields = Mathf.Min(kvp.Value.CurrentShields + 10, kvp.Value.MaxShields);
				}
			}

			if (HasHostilesInRange(hexContents, scanningRange))
			{
				result.Ambushed = true;
				return result;
			}
		}

		result.FullyRepaired = true;
		return result;
	}

	private bool HasHostilesInRange(Dictionary<Vector2I, MapEntity> hexContents, int scanningRange)
	{
		List<Vector2I> players = new List<Vector2I>();
		List<Vector2I> enemies = new List<Vector2I>();
		foreach (var kvp in hexContents)
		{
			if (kvp.Value.Type == "Player Fleet") players.Add(kvp.Key);
			if (kvp.Value.Type == "Enemy Fleet") enemies.Add(kvp.Key);
		}

		foreach (Vector2I playerHex in players)
		{
			foreach (Vector2I enemyHex in enemies)
			{
				if (HexMath.HexDistance(playerHex, enemyHex) <= scanningRange)
				{
					return true;
				}
			}
		}

		return false;
	}
}
