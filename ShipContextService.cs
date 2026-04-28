using Godot;
using System.Collections.Generic;

public class ShipMenuState
{
	public string Title { get; set; }
	public string ImagePath { get; set; }
	public float HpPercent { get; set; }
	public string DetailsText { get; set; }
	public bool IsPlayerShip { get; set; }
	public bool CanRepair { get; set; }
	public bool ShowLongRange { get; set; }
	public bool DisableLongRange { get; set; }
	public bool ShowEquip { get; set; }
	public bool ShowTrade { get; set; }
	public bool ShowScan { get; set; }
	public bool DisableScan { get; set; }
	public string ScanText { get; set; }
	public bool ShowSalvage { get; set; }
	public bool DisableSalvage { get; set; }
	public string SalvageText { get; set; }
}

public class ShipContextService
{
	private readonly GlobalData _globalData;

	public ShipContextService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public ShipMenuState BuildMenuState(MapEntity ship, bool inCombat, Dictionary<Vector2I, MapEntity> hexContents)
	{
		if (ship == null)
		{
			return null;
		}

		bool isPlayer = ship.Type == "Player Fleet";
		PlanetData adjacentPlanetData = GetAdjacentPlanetData(ship, hexContents);
		bool hasAdjacentOutpost = HasAdjacentOutpost(ship, hexContents);

		return new ShipMenuState
		{
			Title = $"== {ship.Name.ToUpper()} ==",
			ImagePath = Database.GetShipTexturePath(ship.Name),
			HpPercent = ship.MaxHP > 0 ? (float)ship.CurrentHP / ship.MaxHP : 0f,
			DetailsText = $"Classification: {ship.Type}\nAction Points: {ship.CurrentActions}/{ship.MaxActions}\nWeapon Payload: 0-{ship.AttackDamage} Dmg\nTargeting Range: {ship.AttackRange} Hexes\n",
			IsPlayerShip = isPlayer,
			CanRepair = ship.CurrentActions >= 2,
			ShowLongRange = isPlayer && !inCombat && ship.Name == "The Aether Skimmer",
			DisableLongRange = _globalData == null || _globalData.FleetResources["Energy Cores"].AsSingle() < 5f,
			ShowEquip = isPlayer && !inCombat,
			ShowTrade = isPlayer && !inCombat && hasAdjacentOutpost,
			ShowScan = isPlayer && !inCombat && adjacentPlanetData != null && (ship.Name == "The Aether Skimmer" || ship.Name == "The Relic Harvester"),
			DisableScan = adjacentPlanetData == null || adjacentPlanetData.HasBeenScanned || ship.CurrentActions < 1,
			ScanText = adjacentPlanetData != null && adjacentPlanetData.HasBeenScanned ? "SCANNED" : "SCAN",
			ShowSalvage = isPlayer && !inCombat && adjacentPlanetData != null && (ship.Name == "The Relic Harvester" || ship.Name == "The Neptune Forge"),
			DisableSalvage = adjacentPlanetData == null || adjacentPlanetData.HasBeenSalvaged || ship.CurrentActions < 1,
			SalvageText = adjacentPlanetData != null && adjacentPlanetData.HasBeenSalvaged ? "SALVAGED" : "SALVAGE"
		};
	}

	public MapEntity GetAdjacentPlanet(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		Vector2I shipHex = GetShipHex(ship, hexContents);
		foreach (Vector2I dir in HexMath.Directions)
		{
			Vector2I neighbor = shipHex + dir;
			if (hexContents.ContainsKey(neighbor) && hexContents[neighbor].Type == "Planet")
			{
				return hexContents[neighbor];
			}
		}

		return null;
	}

	public PlanetData GetAdjacentPlanetData(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		MapEntity planet = GetAdjacentPlanet(ship, hexContents);
		return planet == null ? null : GetPlanetData(planet.Name);
	}

	public bool HasAdjacentOutpost(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		Vector2I shipHex = GetShipHex(ship, hexContents);
		foreach (Vector2I dir in HexMath.Directions)
		{
			Vector2I neighbor = shipHex + dir;
			if (hexContents.ContainsKey(neighbor) && hexContents[neighbor].Type == "Outpost")
			{
				return true;
			}
		}

		return false;
	}

	private PlanetData GetPlanetData(string planetName)
	{
		if (_globalData == null || string.IsNullOrEmpty(_globalData.SavedSystem) || !_globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			return null;
		}

		SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
		foreach (PlanetData planet in currentSystem.Planets)
		{
			if (planet.Name == planetName)
			{
				return planet;
			}
		}

		return null;
	}

	private Vector2I GetShipHex(MapEntity ship, Dictionary<Vector2I, MapEntity> hexContents)
	{
		foreach (var kvp in hexContents)
		{
			if (kvp.Value == ship)
			{
				return kvp.Key;
			}
		}

		return Vector2I.Zero;
	}
}
