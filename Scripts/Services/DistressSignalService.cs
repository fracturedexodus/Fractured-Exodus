using Godot;
using System.Collections.Generic;

public class DistressSignalResult
{
	public bool RescueArrived { get; set; }
	public int FuelSalvaged { get; set; }
	public bool TriggerAmbush { get; set; }
}

public class AmbushSpawnSpec
{
	public string EnemyName { get; set; }
	public Vector2I SpawnPos { get; set; }
}

public class DistressSignalService
{
	private readonly GlobalData _globalData;

	public DistressSignalService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public DistressSignalResult ResolveDistressSignal()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		if (rng.RandiRange(0, 99) < 50)
		{
			int fuelSalvaged = rng.RandiRange(25, 75);
			if (_globalData != null)
			{
				_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] = _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle() + fuelSalvaged;
			}

			return new DistressSignalResult
			{
				RescueArrived = true,
				FuelSalvaged = fuelSalvaged
			};
		}

		return new DistressSignalResult
		{
			TriggerAmbush = true
		};
	}

	public List<AmbushSpawnSpec> PlanAmbushFleet(
		Dictionary<Vector2I, MapEntity> hexContents,
		ICollection<Vector2I> validHexes)
	{
		List<AmbushSpawnSpec> spawnSpecs = new List<AmbushSpawnSpec>();
		HashSet<Vector2I> validHexSet = validHexes as HashSet<Vector2I> ?? new HashSet<Vector2I>(validHexes);
		HashSet<Vector2I> occupiedHexes = new HashSet<Vector2I>(hexContents.Keys);
		Vector2I ambushBaseLocation = Vector2I.Zero;

		foreach (var kvp in hexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet)
			{
				ambushBaseLocation = kvp.Key;
				break;
			}
		}

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		int enemyShipCount = rng.RandiRange(1, 5);
		int enemyDirIndex = 0;

		for (int i = 0; i < enemyShipCount; i++)
		{
			string enemyName = Database.EnemyShipTypes[rng.RandiRange(0, Database.EnemyShipTypes.Length - 1)];
			while (enemyDirIndex < 36)
			{
				int ring = (enemyDirIndex / 6) + 1;
				Vector2I spawnPos = ambushBaseLocation + HexMath.Directions[enemyDirIndex % 6] * ring;
				enemyDirIndex++;

				if (validHexSet.Contains(spawnPos) && !occupiedHexes.Contains(spawnPos))
				{
					spawnSpecs.Add(new AmbushSpawnSpec
					{
						EnemyName = enemyName,
						SpawnPos = spawnPos
					});
					occupiedHexes.Add(spawnPos);
					break;
				}
			}
		}

		return spawnSpecs;
	}
}
