using Godot;
using System;
using System.Collections.Generic;

public class HazardManager
{
	private BattleMap _map;
	private float _asteroidTimer = 0f;
	private float _radiationTimer = 0f;

	public HazardManager(BattleMap map)
	{
		_map = map;
	}

	public void ProcessHazards(double delta)
	{
		_asteroidTimer += (float)delta;
		if (_asteroidTimer >= 1.0f)
		{
			_asteroidTimer -= 1.0f;
			ApplyAsteroidDamage();
		}

		_radiationTimer += (float)delta;
		if (_radiationTimer >= 5.0f)
		{
			_radiationTimer -= 5.0f;
			ApplyRadiationDamage();
		}
	}

	private void ApplyAsteroidDamage()
	{
		HashSet<Vector2I> liveAsteroidHexes = new HashSet<Vector2I>();
		foreach (Node child in _map.EnvironmentLayer.GetChildren())
		{
			if (child is Polygon2D rock) liveAsteroidHexes.Add(HexMath.PixelToHex(rock.GlobalPosition, _map.HexSize));
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
