using Godot;
using System.Collections.Generic;

public class EnemyRoamingMove
{
	public Vector2I FromHex { get; set; }
	public Vector2I ToHex { get; set; }
}

public class ExplorationTurnService
{
	public int AdvanceTurn(int currentTurn, Dictionary<Vector2I, MapEntity> hexContents)
	{
		foreach (var kvp in hexContents)
		{
			if (kvp.Value.Type == "Player Fleet" || kvp.Value.Type == "Enemy Fleet")
			{
				kvp.Value.CurrentActions = kvp.Value.MaxActions;
			}
		}

		return currentTurn + 1;
	}

	public List<EnemyRoamingMove> PlanRoamingEnemyMoves(
		Dictionary<Vector2I, MapEntity> hexContents,
		ICollection<Vector2I> validHexes,
		int scanningRange)
	{
		List<EnemyRoamingMove> plannedMoves = new List<EnemyRoamingMove>();
		List<Vector2I> playerPositions = new List<Vector2I>();
		List<KeyValuePair<Vector2I, MapEntity>> enemies = new List<KeyValuePair<Vector2I, MapEntity>>();
		HashSet<Vector2I> validHexSet = validHexes as HashSet<Vector2I> ?? new HashSet<Vector2I>(validHexes);
		Dictionary<Vector2I, string> occupiedTypes = new Dictionary<Vector2I, string>();

		foreach (var kvp in hexContents)
		{
			occupiedTypes[kvp.Key] = kvp.Value.Type;
			if (kvp.Value.Type == "Player Fleet")
			{
				playerPositions.Add(kvp.Key);
			}
			else if (kvp.Value.Type == "Enemy Fleet")
			{
				enemies.Add(kvp);
			}
		}

		if (playerPositions.Count == 0)
		{
			return plannedMoves;
		}

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		foreach (var kvp in enemies)
		{
			Vector2I currentPos = kvp.Key;
			Vector2I targetPlayer = playerPositions[0];
			int minDistance = HexMath.HexDistance(currentPos, targetPlayer);

			foreach (Vector2I playerHex in playerPositions)
			{
				int dist = HexMath.HexDistance(currentPos, playerHex);
				if (dist < minDistance)
				{
					minDistance = dist;
					targetPlayer = playerHex;
				}
			}

			Vector2I bestNeighbor = currentPos;
			if (minDistance <= scanningRange + 2)
			{
				int bestDist = minDistance;
				foreach (Vector2I dir in HexMath.Directions)
				{
					Vector2I neighbor = currentPos + dir;
					if (!IsHexWalkable(neighbor, validHexSet, occupiedTypes))
					{
						continue;
					}

					int distToTarget = HexMath.HexDistance(neighbor, targetPlayer);
					if (distToTarget < bestDist)
					{
						bestDist = distToTarget;
						bestNeighbor = neighbor;
					}
				}
			}
			else
			{
				List<Vector2I> validMoves = new List<Vector2I>();
				foreach (Vector2I dir in HexMath.Directions)
				{
					Vector2I neighbor = currentPos + dir;
					if (IsHexWalkable(neighbor, validHexSet, occupiedTypes) && HexMath.HexDistance(neighbor, targetPlayer) > scanningRange)
					{
						validMoves.Add(neighbor);
					}
				}

				if (validMoves.Count > 0)
				{
					bestNeighbor = validMoves[rng.RandiRange(0, validMoves.Count - 1)];
				}
			}

			if (bestNeighbor != currentPos)
			{
				plannedMoves.Add(new EnemyRoamingMove
				{
					FromHex = currentPos,
					ToHex = bestNeighbor
				});
				occupiedTypes.Remove(currentPos);
				occupiedTypes[bestNeighbor] = "Enemy Fleet";
			}
		}

		return plannedMoves;
	}

	private bool IsHexWalkable(Vector2I hex, HashSet<Vector2I> validHexes, Dictionary<Vector2I, string> occupiedTypes)
	{
		return validHexes.Contains(hex) && !occupiedTypes.ContainsKey(hex);
	}
}
