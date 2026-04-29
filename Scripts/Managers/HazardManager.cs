using Godot;
using System;
using System.Collections.Generic;

public class HazardManager
{
	private BattleMap _map;
	private float _asteroidTimer = 0f;
	private float _radiationTimer = 0f;
	private const float CombatAsteroidStepAngle = -0.18f;

	public HazardManager(BattleMap map)
	{
		_map = map;
	}

	public void ProcessHazards(double delta)
	{
		if (!_map.Combat.InCombat)
		{
			_asteroidTimer += (float)delta;
			if (_asteroidTimer >= 1.0f)
			{
				_asteroidTimer -= 1.0f;
				ApplyAsteroidDamage();
			}
		}

		_radiationTimer += (float)delta;
		if (_radiationTimer >= 5.0f)
		{
			_radiationTimer -= 5.0f;
			ApplyRadiationDamage();
		}
	}

	public void AdvanceAsteroidsForCombatTurn()
	{
		if (_map == null || _map.EnvironmentLayer == null || _map.Combat == null || !_map.Combat.InCombat)
		{
			return;
		}

		List<(Polygon2D Rock, Vector2I CurrentHex, Vector2I NextHex)> movements = new List<(Polygon2D, Vector2I, Vector2I)>();
		HashSet<Vector2I> updatedHexes = new HashSet<Vector2I>();

		foreach (Polygon2D rock in GetAsteroidRocks())
		{
			Vector2I currentHex = GetAsteroidHex(rock);
			Vector2I nextHex = GetNextAsteroidHex(currentHex, rock.GlobalPosition);
			movements.Add((rock, currentHex, nextHex));
			updatedHexes.Add(nextHex);
		}

		foreach ((Polygon2D rock, Vector2I _, Vector2I nextHex) movement in movements)
		{
			Vector2 targetGlobal = HexMath.HexToPixel(movement.nextHex, _map.HexSize);
			movement.rock.Position = _map.EnvironmentLayer.ToLocal(targetGlobal);
			movement.rock.SetMeta("asteroid_hex_q", movement.nextHex.X);
			movement.rock.SetMeta("asteroid_hex_r", movement.nextHex.Y);
		}

		_map.AsteroidHexes = updatedHexes;
		ApplyAsteroidDamage();
	}

	private void ApplyAsteroidDamage()
	{
		HashSet<Vector2I> liveAsteroidHexes = new HashSet<Vector2I>();
		foreach (Polygon2D rock in GetAsteroidRocks())
		{
			liveAsteroidHexes.Add(HexMath.PixelToHex(rock.GlobalPosition, _map.HexSize));
		}

		bool uiNeedsUpdate = false;
		List<Vector2I> destroyedHexes = new List<Vector2I>();

		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet || kvp.Value.Type == GameConstants.EntityTypes.EnemyFleet)
			{
				if (liveAsteroidHexes.Contains(kvp.Key))
				{
					int damage = 5;
					bool tookHullDamage = false;

					if (kvp.Value.CurrentShields > 0)
					{
						if (kvp.Value.CurrentShields >= damage) { kvp.Value.CurrentShields -= damage; damage = 0; }
						else { damage -= kvp.Value.CurrentShields; kvp.Value.CurrentShields = 0; }
					}
					
					if (damage > 0) { kvp.Value.CurrentHP -= damage; tookHullDamage = true; }

					if (GodotObject.IsInstanceValid(kvp.Value.VisualSprite))
					{
						Tween flash = _map.CreateTween();
						Color flashColor = tookHullDamage ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 0.5f, 1f);
						flash.TweenProperty(kvp.Value.VisualSprite, "modulate", flashColor, 0.15f);
						flash.TweenProperty(kvp.Value.VisualSprite, "modulate", Colors.White, 0.15f);
					}

					if (_map.CurrentlyViewedShip == kvp.Value) uiNeedsUpdate = true;

					if (kvp.Value.CurrentHP <= 0)
					{
						_map.LogCombatMessage($"[color=red]*** {kvp.Value.Name.ToUpper()} DESTROYED BY ASTEROID IMPACT ***[/color]\n");
						if (_map.ExplosionPlayer.Stream != null) _map.ExplosionPlayer.Play();
						BattleVFX.DrawExplosion(_map.EntityLayer, HexMath.HexToPixel(kvp.Key, _map.HexSize), _map.HexSize);

						kvp.Value.IsDead = true;
						if (GodotObject.IsInstanceValid(kvp.Value.VisualSprite)) kvp.Value.VisualSprite.QueueFree();
						
						destroyedHexes.Add(kvp.Key);
						_map.SelectedHexes.Remove(kvp.Key); 
						if (_map.CurrentlyViewedShip == kvp.Value) _map.ToggleShipMenu(false);
					}
				}
			}
		}
		
		foreach (Vector2I deadHex in destroyedHexes) _map.HexContents.Remove(deadHex);

		if (destroyedHexes.Count > 0)
		{
			if (_map.Combat.InCombat) { _map.Combat.UpdateInitiativeUI(); if (!_map.Combat.AreBothSidesAlive()) _map.Combat.EndCombat(); }
			_map.CheckGameOver();
		}

		if (uiNeedsUpdate && _map.UI != null && _map.UI.ShipMenuPanel.Position.X < _map.GetViewportRect().Size.X && _map.CurrentlyViewedShip != null && !_map.CurrentlyViewedShip.IsDead) 
		{
			_map.ToggleShipMenu(true, _map.CurrentlyViewedShip);
		}
	}

	private IEnumerable<Polygon2D> GetAsteroidRocks()
	{
		foreach (Node child in _map.EnvironmentLayer.GetChildren())
		{
			if (child is Polygon2D rock && rock.HasMeta("is_asteroid") && rock.GetMeta("is_asteroid").AsBool())
			{
				yield return rock;
			}
		}
	}

	private Vector2I GetAsteroidHex(Polygon2D rock)
	{
		if (rock != null && rock.HasMeta("asteroid_hex_q") && rock.HasMeta("asteroid_hex_r"))
		{
			return new Vector2I((int)rock.GetMeta("asteroid_hex_q"), (int)rock.GetMeta("asteroid_hex_r"));
		}

		return HexMath.PixelToHex(rock.GlobalPosition, _map.HexSize);
	}

	private Vector2I GetNextAsteroidHex(Vector2I currentHex, Vector2 currentGlobalPosition)
	{
		Vector2 desiredGlobal = currentGlobalPosition.Rotated(CombatAsteroidStepAngle);
		Vector2I bestHex = currentHex;
		float bestDistance = float.MaxValue;

		foreach (Vector2I direction in HexMath.Directions)
		{
			Vector2I candidate = currentHex + direction;
			if (!_map.HexGrid.ContainsKey(candidate))
			{
				continue;
			}

			Vector2 candidateGlobal = HexMath.HexToPixel(candidate, _map.HexSize);
			float candidateDistance = candidateGlobal.DistanceTo(desiredGlobal);
			if (candidateDistance < bestDistance)
			{
				bestDistance = candidateDistance;
				bestHex = candidate;
			}
		}

		return bestHex;
	}

	private void ApplyRadiationDamage()
	{
		bool uiNeedsUpdate = false;
		List<Vector2I> destroyedHexes = new List<Vector2I>();

		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet || kvp.Value.Type == GameConstants.EntityTypes.EnemyFleet)
			{
				if (_map.RadiationHexes.Contains(kvp.Key))
				{
					bool tookHullDamage = false;

					if (kvp.Value.CurrentShields > 0) kvp.Value.CurrentShields = Mathf.Max(0, kvp.Value.CurrentShields - 1);
					else { kvp.Value.CurrentHP -= 2; tookHullDamage = true; }

					if (GodotObject.IsInstanceValid(kvp.Value.VisualSprite))
					{
						Tween flash = _map.CreateTween();
						Color flashColor = tookHullDamage ? new Color(1f, 0.3f, 0.3f) : new Color(0.5f, 1f, 0.2f);
						flash.TweenProperty(kvp.Value.VisualSprite, "modulate", flashColor, 0.15f);
						flash.TweenProperty(kvp.Value.VisualSprite, "modulate", Colors.White, 0.15f);
					}

					if (_map.CurrentlyViewedShip == kvp.Value) uiNeedsUpdate = true;

					if (kvp.Value.CurrentHP <= 0)
					{
						_map.LogCombatMessage($"[color=red]*** {kvp.Value.Name.ToUpper()} DESTROYED BY RADIATION EXPOSURE ***[/color]\n");
						if (_map.ExplosionPlayer.Stream != null) _map.ExplosionPlayer.Play();
						BattleVFX.DrawExplosion(_map.EntityLayer, HexMath.HexToPixel(kvp.Key, _map.HexSize), _map.HexSize);

						kvp.Value.IsDead = true;
						if (GodotObject.IsInstanceValid(kvp.Value.VisualSprite)) kvp.Value.VisualSprite.QueueFree();
						
						destroyedHexes.Add(kvp.Key);
						_map.SelectedHexes.Remove(kvp.Key); 
						if (_map.CurrentlyViewedShip == kvp.Value) _map.ToggleShipMenu(false);
					}
				}
			}
		}
		
		foreach (Vector2I deadHex in destroyedHexes) _map.HexContents.Remove(deadHex);

		if (destroyedHexes.Count > 0)
		{
			if (_map.Combat.InCombat) { _map.Combat.UpdateInitiativeUI(); if (!_map.Combat.AreBothSidesAlive()) _map.Combat.EndCombat(); }
			_map.CheckGameOver();
		}

		if (uiNeedsUpdate && _map.UI != null && _map.UI.ShipMenuPanel.Position.X < _map.GetViewportRect().Size.X && _map.CurrentlyViewedShip != null && !_map.CurrentlyViewedShip.IsDead) 
		{
			_map.ToggleShipMenu(true, _map.CurrentlyViewedShip);
		}
	}
}
