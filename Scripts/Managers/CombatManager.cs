using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class CombatManager
{
	private BattleMap _map;

	public bool InCombat { get; private set; } = false;
	public bool IsTargeting { get; set; } = false;
	public bool IsTargetingMissile { get; set; } = false;
	public MapEntity ActiveShip { get; private set; } = null;

	private List<MapEntity> _initiativeQueue = new List<MapEntity>();
	private int _currentQueueIndex = 0;

	public CombatManager(BattleMap map)
	{
		_map = map;
	}

	public int GetCurrentQueueIndex() => _currentQueueIndex;

	private bool CanUseBattleScene()
	{
		return _map != null
			&& GodotObject.IsInstanceValid(_map)
			&& GodotObject.IsInstanceValid(_map.EntityLayer);
	}

	public void CheckForCombatTrigger()
	{
		if (!CanUseBattleScene()) return;
		if (InCombat) return;
		if (_map.IsFleetMoving)
		{
			_map.RunAfterMovementCompletes(CheckForCombatTrigger);
			return;
		}

		List<Vector2I> players = new List<Vector2I>();
		List<Vector2I> enemies = new List<Vector2I>();

		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet) players.Add(kvp.Key);
			if (kvp.Value.Type == GameConstants.EntityTypes.EnemyFleet) enemies.Add(kvp.Key);
		}

		if (enemies.Count == 0 || players.Count == 0) return;

		foreach (Vector2I p in players)
		{
			foreach (Vector2I e in enemies)
			{
				if (HexMath.HexDistance(p, e) <= _map.ScanningRange)
				{
					StartCombat();
					return;
				}
			}
		}
	}

	public void StartCombat()
	{
		InCombat = true;
		_map.PlayCombatMusic();
		if (_map.UI != null)
		{
			_map.UI.EndTurnButton.Visible = true; 
			_map.UI.RepairFleetButton.Visible = false; 
			_map.UI.InventoryButton.Visible = false;
			_map.UI.CombatLogPanel.Visible = true;
			_map.UI.CombatLogText.Text = "[color=yellow]--- COMBAT INITIATED ---[/color]\n";
		}

		_initiativeQueue.Clear();
		_map.SelectedHexes.Clear();

		List<Vector2I> playerHexes = new List<Vector2I>();
		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet) playerHexes.Add(kvp.Key);
		}

		int engagementRange = _map.ScanningRange * 2; 

		Random rng = new Random();
		foreach (var kvp in _map.HexContents)
		{
			bool joinsCombat = false;

			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet)
			{
				joinsCombat = true; 
			}
			else if (kvp.Value.Type == GameConstants.EntityTypes.EnemyFleet)
			{
				foreach (Vector2I pHex in playerHexes)
				{
					if (HexMath.HexDistance(kvp.Key, pHex) <= engagementRange)
					{
						joinsCombat = true;
						break;
					}
				}
			}

			if (joinsCombat)
			{
				kvp.Value.CurrentInitiativeRoll = rng.Next(1, 21) + Database.GetShipInitiativeBonus(kvp.Value.Name);
				kvp.Value.CurrentActions = kvp.Value.MaxActions; 
				
				if (GodotObject.IsInstanceValid(kvp.Value.VisualSprite)) kvp.Value.VisualSprite.Visible = true;
				_initiativeQueue.Add(kvp.Value);
			}
		}

		_initiativeQueue.Sort((a, b) => {
			int cmp = b.CurrentInitiativeRoll.CompareTo(a.CurrentInitiativeRoll);
			if (cmp == 0) return a.Name.CompareTo(b.Name); 
			return cmp;
		});

		_currentQueueIndex = 0;
		_map.CurrentTurn = 1;
		if (_map.UI != null) _map.UI.EndTurnButton.Text = "END TURN";

		UpdateInitiativeUI();
		StartActiveTurn();
	}

	public void RestoreCombatState(int savedQueueIndex)
	{
		InCombat = true;
		_map.PlayCombatMusic();
		if (_map.UI != null)
		{
			_map.UI.EndTurnButton.Visible = true; 
			_map.UI.RepairFleetButton.Visible = false; 
			_map.UI.InventoryButton.Visible = false;
			_map.UI.CombatLogPanel.Visible = true;
			_map.UI.CombatLogText.Text = "[color=yellow]--- COMBAT RESUMED ---[/color]\n";
		}

		_currentQueueIndex = savedQueueIndex;
		_initiativeQueue.Clear();
		_map.SelectedHexes.Clear();

		List<Vector2I> playerHexes = new List<Vector2I>();
		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet) playerHexes.Add(kvp.Key);
		}

		int engagementRange = _map.ScanningRange * 2; 

		foreach (var kvp in _map.HexContents)
		{
			bool joinsCombat = false;

			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet)
			{
				joinsCombat = true; 
			}
			else if (kvp.Value.Type == GameConstants.EntityTypes.EnemyFleet)
			{
				foreach (Vector2I pHex in playerHexes)
				{
					if (HexMath.HexDistance(kvp.Key, pHex) <= engagementRange)
					{
						joinsCombat = true;
						break;
					}
				}
			}

			if (joinsCombat)
			{
				if (GodotObject.IsInstanceValid(kvp.Value.VisualSprite)) kvp.Value.VisualSprite.Visible = true;
				_initiativeQueue.Add(kvp.Value);
			}
		}

		_initiativeQueue.Sort((a, b) => {
			int cmp = b.CurrentInitiativeRoll.CompareTo(a.CurrentInitiativeRoll);
			if (cmp == 0) return a.Name.CompareTo(b.Name); 
			return cmp;
		});

		if (_currentQueueIndex >= _initiativeQueue.Count) _currentQueueIndex = 0;

		UpdateInitiativeUI();
		if (_map.UI != null) _map.UI.EndTurnButton.Text = "END TURN";

		if (_initiativeQueue.Count > 0)
		{
			ActiveShip = _initiativeQueue[_currentQueueIndex];
			Tween camTween = _map.CreateTween();
			camTween.TweenProperty(_map.MapCamera, "position", ActiveShip.VisualSprite.Position, 0.5f).SetTrans(Tween.TransitionType.Sine);

			if (ActiveShip.Type == GameConstants.EntityTypes.EnemyFleet) _map.GetTree().CreateTimer(1.0f).Timeout += () => ExecuteSingleEnemyAI(ActiveShip);
			else
			{
				foreach (var kvp in _map.HexContents)
				{
					if (kvp.Value == ActiveShip)
					{
						_map.SelectedHexes.Add(kvp.Key);
						_map.UpdateHighlights();
						break;
					}
				}
			}
		}
	}

	public void UpdateInitiativeUI()
	{
		if (_map.UI == null || _map.UI.InitiativeUI == null) return;
		foreach (Node child in _map.UI.InitiativeUI.GetChildren()) child.QueueFree();

		if (!InCombat)
		{
			_map.UI.TurnLabel.Text = "EXPLORATION MODE";
			return;
		}

		StyleBoxFlat blackSquareStyle = new StyleBoxFlat();
		blackSquareStyle.BgColor = new Color(0, 0, 0, 1); 
		blackSquareStyle.BorderWidthBottom = 2; blackSquareStyle.BorderWidthTop = 2; blackSquareStyle.BorderWidthLeft = 2; blackSquareStyle.BorderWidthRight = 2;
		blackSquareStyle.BorderColor = new Color(0.3f, 0.3f, 0.3f, 1f); 

		for (int i = 0; i < _initiativeQueue.Count; i++)
		{
			MapEntity ship = _initiativeQueue[i];
			if (ship.IsDead) continue;

			PanelContainer squarePanel = new PanelContainer();
			squarePanel.AddThemeStyleboxOverride("panel", blackSquareStyle);
			squarePanel.CustomMinimumSize = new Vector2(64, 64); 
			
			if (i == _currentQueueIndex)
			{
				StyleBoxFlat activeStyle = blackSquareStyle.Duplicate() as StyleBoxFlat;
				activeStyle.BorderColor = new Color(0.2f, 1f, 0.2f, 1f); 
				squarePanel.AddThemeStyleboxOverride("panel", activeStyle);
				_map.UI.TurnLabel.Text = $"CURRENT TURN: {ship.Name.ToUpper()}"; 
			}

			TextureRect icon = new TextureRect();
			Texture2D tex = GD.Load<Texture2D>(Database.GetShipTexturePath(ship.Name));
			icon.Texture = tex;
			icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			icon.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

			if (i == _currentQueueIndex) icon.Modulate = new Color(0.8f, 1f, 0.8f); 
			else if (ship.Type == GameConstants.EntityTypes.EnemyFleet) icon.Modulate = new Color(1f, 0.5f, 0.5f);
			else icon.Modulate = new Color(1f, 1f, 1f, 0.6f); 
			
			squarePanel.AddChild(icon);
			_map.UI.InitiativeUI.AddChild(squarePanel);
		}
	}

	public void StartActiveTurn()
	{
		_initiativeQueue.RemoveAll(s => s.IsDead);
		_map.SelectedHexes.Clear();
		IsTargeting = false;
		IsTargetingMissile = false;

		if (_initiativeQueue.Count == 0 || !AreBothSidesAlive())
		{
			EndCombat();
			return;
		}

		if (_currentQueueIndex >= _initiativeQueue.Count)
		{
			_currentQueueIndex = 0;
			_map.CurrentTurn++;
			_map.LogCombatMessage($"\n[color=gray]--- ROUND {_map.CurrentTurn} ---[/color]");
		}

		ActiveShip = _initiativeQueue[_currentQueueIndex];
		ActiveShip.CurrentActions = ActiveShip.MaxActions;

		UpdateInitiativeUI();
		
		Tween camTween = _map.CreateTween();
		camTween.TweenProperty(_map.MapCamera, "position", ActiveShip.VisualSprite.Position, 0.5f).SetTrans(Tween.TransitionType.Sine);

		if (ActiveShip.Type == GameConstants.EntityTypes.EnemyFleet)
		{
			_map.GetTree().CreateTimer(1.0f).Timeout += () => ExecuteSingleEnemyAI(ActiveShip);
		}
		else
		{
			foreach (var kvp in _map.HexContents)
			{
				if (kvp.Value == ActiveShip)
				{
					_map.SelectedHexes.Add(kvp.Key);
					_map.ToggleShipMenu(true, ActiveShip); 
					_map.UpdateHighlights();
					break;
				}
			}
		}
	}

	public void EndActiveTurn()
	{
		if (!InCombat) return;
		IsTargeting = false;
		IsTargetingMissile = false;
		_map.SelectedHexes.Clear();
		_map.ToggleShipMenu(false); 
		_map.UpdateHighlights();
		_currentQueueIndex++;
		StartActiveTurn();
	}

	public bool AreBothSidesAlive()
	{
		bool playerAlive = false;
		bool enemyAlive = false;
		foreach (var ship in _initiativeQueue)
		{
			if (ship.Type == GameConstants.EntityTypes.PlayerFleet) playerAlive = true;
			if (ship.Type == GameConstants.EntityTypes.EnemyFleet) enemyAlive = true;
		}
		return playerAlive && enemyAlive;
	}

	public void EndCombat()
	{
		InCombat = false;
		IsTargeting = false;
		IsTargetingMissile = false;
		ActiveShip = null;
		_map.PlayExplorationMusic();
		
		if (_map.UI != null)
		{
			_map.UI.CombatLogPanel.Visible = true; 
			_map.UI.EndTurnButton.Visible = false; 
			_map.UI.RepairFleetButton.Visible = true; 
			_map.UI.InventoryButton.Visible = true;
		}
		
		bool playerAlive = false;
		foreach (var ship in _map.HexContents.Values)
		{
			if (ship.Type == GameConstants.EntityTypes.PlayerFleet) playerAlive = true;
		}

		if (!playerAlive)
		{
			if (_map.UI != null)
			{
				_map.UI.GameOverPanel.Visible = true;
				_map.UI.TurnLabel.Text = "FLEET DESTROYED";
			}
		}
		else
		{
			if (_map.UI != null) _map.UI.TurnLabel.Text = "EXPLORATION MODE";
		}
		
		if (_map.UI != null && _map.UI.InitiativeUI != null)
		{
			foreach (Node child in _map.UI.InitiativeUI.GetChildren()) child.QueueFree();
		}
	}

	// --- NEW: PARTICLE EFFECT SPAWNER ---
	private void SpawnAttackEffect(Vector2I targetHex, bool hitShields)
	{
		CpuParticles2D effect = new CpuParticles2D();
		
		effect.Emitting = false;
		effect.OneShot = true;
		effect.Explosiveness = 0.9f; 
		effect.Lifetime = 0.5f; 
		
		effect.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
		effect.EmissionSphereRadius = 10f;
		
		effect.Gravity = Vector2.Zero;
		
		if (hitShields)
		{
			// SHIELD IMPACT: Bright, fast blue energy burst
			effect.Amount = 40;
			effect.Spread = 180f;
			effect.InitialVelocityMin = 100f;
			effect.InitialVelocityMax = 250f;
			effect.ScaleAmountMin = 2f;
			effect.ScaleAmountMax = 4f;
			effect.Color = new Color(0f, 0.8f, 1f, 0.8f); 
		}
		else
		{
			// HULL IMPACT: Fiery, debris-like explosion
			effect.Amount = 60;
			effect.Spread = 180f;
			effect.InitialVelocityMin = 50f;
			effect.InitialVelocityMax = 150f;
			effect.ScaleAmountMin = 3f;
			effect.ScaleAmountMax = 6f;
			effect.Color = new Color(1f, 0.4f, 0f, 0.9f); 
		}

		effect.Position = HexMath.HexToPixel(targetHex, _map.HexSize);
		effect.ZIndex = 50; 
		
		_map.AddChild(effect);
		effect.Emitting = true;
		
		// Clean up the particle node after it finishes
		SceneTreeTimer timer = _map.GetTree().CreateTimer(effect.Lifetime + 0.1f);
		timer.Timeout += () => 
		{
			if (GodotObject.IsInstanceValid(effect)) effect.QueueFree();
		};
	}

	public void PerformAttack(Vector2I attackerHex, Vector2I defenderHex)
	{
		if (!CanUseBattleScene()) return;
		if (!_map.HexContents.ContainsKey(attackerHex) || !_map.HexContents.ContainsKey(defenderHex)) return;

		MapEntity attacker = _map.HexContents[attackerHex];
		MapEntity defender = _map.HexContents[defenderHex];

		attacker.CurrentActions--; 
		
		BattleVFX.DrawLaserBeam(_map.EntityLayer, HexMath.HexToPixel(attackerHex, _map.HexSize), HexMath.HexToPixel(defenderHex, _map.HexSize), attacker.Type);
		if (_map.LaserPlayer.Stream != null) _map.LaserPlayer.Play();

		Random rng = new Random();
		int damageRolled = rng.Next(0, attacker.AttackDamage + 1);
		string attackerColor = attacker.Type == GameConstants.EntityTypes.PlayerFleet ? "#44ff44" : "#ff4444";

		if (damageRolled == 0)
		{
			string missTxt = Database.MissTexts[rng.Next(Database.MissTexts.Length)];
			_map.LogCombatMessage($"[color={attackerColor}]{attacker.Name}[/color] fired at {defender.Name}... {missTxt} [color=gray](0 DMG)[/color]");
			
			if (_map.SelectedHexes.Count == 1 && _map.SelectedHexes[0] == attackerHex) _map.ToggleShipMenu(true, attacker);
			return; 
		}

		int shieldDmg = 0;
		int hullDmg = 0;
		int damageRemaining = damageRolled;

		if (defender.CurrentShields > 0)
		{
			if (defender.CurrentShields >= damageRemaining)
			{
				shieldDmg = damageRemaining;
				defender.CurrentShields -= damageRemaining;
				damageRemaining = 0;
			}
			else
			{
				shieldDmg = defender.CurrentShields;
				damageRemaining -= defender.CurrentShields;
				defender.CurrentShields = 0;
			}
		}
		
		hullDmg = damageRemaining;
		defender.CurrentHP -= hullDmg;

		string hitPart = Database.ShipParts[rng.Next(Database.ShipParts.Length)];
		string logMsg = $"[color={attackerColor}]{attacker.Name}[/color] fires on {defender.Name}!\n";
		logMsg += $"-> Hit to the {hitPart} for [color=yellow]{damageRolled} DMG[/color]!";
		
		if (shieldDmg > 0) logMsg += $" ([color=#00ffff]Shields -{shieldDmg}[/color])";
		if (hullDmg > 0) logMsg += $" ([color=#ff4444]Hull -{hullDmg}[/color])";
		_map.LogCombatMessage(logMsg);

		// --- NEW: FIRE OFF THE VISUAL FEEDBACK! ---
		bool hitShields = (shieldDmg > 0 && hullDmg == 0);
		SpawnAttackEffect(defenderHex, hitShields);

		if (_map.SelectedHexes.Count == 1 && _map.SelectedHexes[0] == attackerHex) _map.ToggleShipMenu(true, attacker);

		if (defender.CurrentHP <= 0)
		{
			_map.LogCombatMessage($"[color=red]*** {defender.Name.ToUpper()} DESTROYED ***[/color]\n");
			
			// --- NEW: Give Player Salvage if it was an Enemy Fleet! ---
			if (defender.Type == GameConstants.EntityTypes.EnemyFleet)
			{
				_map.AwardEnemyKillSalvage(defender.Name);
			}

			if (_map.ExplosionPlayer.Stream != null) _map.ExplosionPlayer.Play();
			BattleVFX.DrawExplosion(_map.EntityLayer, HexMath.HexToPixel(defenderHex, _map.HexSize), _map.HexSize);

			defender.IsDead = true;
			if (GodotObject.IsInstanceValid(defender.VisualSprite)) defender.VisualSprite.QueueFree();
			_map.HexContents.Remove(defenderHex);
			
			if (InCombat) UpdateInitiativeUI();
			if (InCombat && !AreBothSidesAlive()) EndCombat(); 
			
			_map.SelectedHexes.Remove(defenderHex);
		}
	}

	public EquipmentData GetEquippedMissile(MapEntity attacker)
	{
		if (_map?._globalData == null || attacker == null || string.IsNullOrEmpty(attacker.Name)) return null;
		if (!_map._globalData.FleetLoadouts.ContainsKey(attacker.Name)) return null;

		ShipLoadout loadout = _map._globalData.FleetLoadouts[attacker.Name];
		if (string.IsNullOrEmpty(loadout.MissileID) || !_map._globalData.MasterEquipmentDB.ContainsKey(loadout.MissileID))
		{
			return null;
		}

		return _map._globalData.MasterEquipmentDB[loadout.MissileID];
	}

	public void PerformMissileAttack(Vector2I attackerHex, Vector2I defenderHex)
	{
		if (!CanUseBattleScene()) return;
		if (!_map.HexContents.ContainsKey(attackerHex) || !_map.HexContents.ContainsKey(defenderHex)) return;

		MapEntity attacker = _map.HexContents[attackerHex];
		MapEntity defender = _map.HexContents[defenderHex];
		EquipmentData missile = GetEquippedMissile(attacker);
		if (missile == null || missile.Category != GameConstants.EquipmentCategories.Missile) return;
		if (HexMath.HexDistance(attackerHex, defenderHex) > missile.MissileRange) return;

		attacker.CurrentActions--;

		BattleVFX.DrawMissileStrike(_map.EntityLayer, HexMath.HexToPixel(attackerHex, _map.HexSize), HexMath.HexToPixel(defenderHex, _map.HexSize), attacker.Type);
		if (_map.LaserPlayer.Stream != null) _map.LaserPlayer.Play();

		int damageRolled = missile.MissileDamage;
		int shieldDmg = 0;
		int hullDmg = 0;
		string attackerColor = attacker.Type == GameConstants.EntityTypes.PlayerFleet ? "#44ff44" : "#ff4444";

		if (missile.MissileAbility == "ShieldPiercing")
		{
			hullDmg = damageRolled;
			defender.CurrentHP -= hullDmg;
		}
		else if (missile.MissileAbility == "ShieldBreaker")
		{
			int boostedShieldDamage = damageRolled + 20;
			if (defender.CurrentShields > 0)
			{
				shieldDmg = Mathf.Min(defender.CurrentShields, boostedShieldDamage);
				defender.CurrentShields -= shieldDmg;
			}

			if (defender.CurrentShields <= 0)
			{
				hullDmg = Mathf.Max(0, damageRolled - Mathf.Max(0, shieldDmg - 20));
				defender.CurrentHP -= hullDmg;
			}
		}
		else
		{
			int damageRemaining = damageRolled;
			if (defender.CurrentShields > 0)
			{
				if (defender.CurrentShields >= damageRemaining)
				{
					shieldDmg = damageRemaining;
					defender.CurrentShields -= damageRemaining;
					damageRemaining = 0;
				}
				else
				{
					shieldDmg = defender.CurrentShields;
					damageRemaining -= defender.CurrentShields;
					defender.CurrentShields = 0;
				}
			}

			hullDmg = damageRemaining;
			defender.CurrentHP -= hullDmg;
		}

		string logMsg = $"[color={attackerColor}]{attacker.Name}[/color] launches [color=orange]{missile.Name}[/color] at {defender.Name}!\n";
		logMsg += $"-> Missile payload detonates for [color=yellow]{damageRolled} DMG[/color]!";
		if (shieldDmg > 0) logMsg += $" ([color=#00ffff]Shields -{shieldDmg}[/color])";
		if (hullDmg > 0) logMsg += $" ([color=#ff4444]Hull -{hullDmg}[/color])";
		if (missile.MissileAbility == "ShieldPiercing") logMsg += " [color=orange](Shield-Piercing)[/color]";
		if (missile.MissileAbility == "ShieldBreaker") logMsg += " [color=orange](Shield Breaker)[/color]";
		_map.LogCombatMessage(logMsg);

		bool hitShields = shieldDmg > 0 && hullDmg == 0;
		SpawnAttackEffect(defenderHex, hitShields);

		if (_map.SelectedHexes.Count == 1 && _map.SelectedHexes[0] == attackerHex) _map.ToggleShipMenu(true, attacker);

		if (defender.CurrentHP <= 0)
		{
			_map.LogCombatMessage($"[color=red]*** {defender.Name.ToUpper()} DESTROYED ***[/color]\n");

			if (defender.Type == GameConstants.EntityTypes.EnemyFleet)
			{
				_map.AwardEnemyKillSalvage(defender.Name);
			}

			if (_map.ExplosionPlayer.Stream != null) _map.ExplosionPlayer.Play();
			BattleVFX.DrawExplosion(_map.EntityLayer, HexMath.HexToPixel(defenderHex, _map.HexSize), _map.HexSize);

			defender.IsDead = true;
			if (GodotObject.IsInstanceValid(defender.VisualSprite)) defender.VisualSprite.QueueFree();
			_map.HexContents.Remove(defenderHex);

			if (InCombat) UpdateInitiativeUI();
			if (InCombat && !AreBothSidesAlive()) EndCombat();

			_map.SelectedHexes.Remove(defenderHex);
		}
	}

	public void ExecuteSingleEnemyAI(MapEntity enemyShip)
	{
		if (!CanUseBattleScene()) return;
		if (enemyShip.IsDead || !InCombat) return;

		Vector2I currentPos = new Vector2I(0,0);
		bool found = false;
		foreach(var kvp in _map.HexContents) { if (kvp.Value == enemyShip) { currentPos = kvp.Key; found = true; break; } }
		if (!found || enemyShip.CurrentActions <= 0) { EndActiveTurn(); return; }

		List<Vector2I> playerPositions = new List<Vector2I>();
		foreach (var kvp in _map.HexContents) if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet) playerPositions.Add(kvp.Key);
		
		if (playerPositions.Count == 0) { EndActiveTurn(); return; }

		Vector2I targetPlayer = playerPositions[0];
		int minDistance = HexMath.HexDistance(currentPos, targetPlayer);

		foreach (Vector2I playerHex in playerPositions)
		{
			int dist = HexMath.HexDistance(currentPos, playerHex);
			if (dist < minDistance) { minDistance = dist; targetPlayer = playerHex; }
		}

		int stepsTaken = 0;
		while (stepsTaken < enemyShip.CurrentActions && HexMath.HexDistance(currentPos, targetPlayer) > enemyShip.AttackRange)
		{
			Vector2I bestNeighbor = currentPos;
			int bestDist = HexMath.HexDistance(currentPos, targetPlayer);
			bool foundMove = false;

			foreach (Vector2I dir in HexMath.Directions)
			{
				Vector2I neighbor = currentPos + dir;
				if (!_map.HexGrid.ContainsKey(neighbor)) continue;
				
				bool isBlocked = false;
				if (_map.HexContents.ContainsKey(neighbor))
				{
					string type = _map.HexContents[neighbor].Type;
					// --- THE FIX: AI CAN NO LONGER PATHFIND OVER OUTPOSTS! ---
					if (type == GameConstants.EntityTypes.Planet || type == GameConstants.EntityTypes.BasePlanetPlayerStart || type == GameConstants.EntityTypes.CelestialBody || type == GameConstants.EntityTypes.PlayerFleet || type == GameConstants.EntityTypes.EnemyFleet || type == GameConstants.EntityTypes.StarGate || type == GameConstants.EntityTypes.Outpost) isBlocked = true;
				}

				if (!isBlocked)
				{
					int distToTarget = HexMath.HexDistance(neighbor, targetPlayer);
					if (distToTarget < bestDist)
					{
						bestDist = distToTarget; bestNeighbor = neighbor; foundMove = true;
					}
				}
			}

			if (!foundMove) break; 
			currentPos = bestNeighbor;
			stepsTaken++;
		}

		Vector2I oldPos = new Vector2I(0,0);
		foreach(var kvp in _map.HexContents) if (kvp.Value == enemyShip) oldPos = kvp.Key;

		if (currentPos != oldPos)
		{
			_map.HexContents.Remove(oldPos);
			_map.HexContents[currentPos] = enemyShip;
			enemyShip.CurrentActions -= stepsTaken;

			Tween tween = _map.CreateTween();
			Vector2 targetPixelPos = HexMath.HexToPixel(currentPos, _map.HexSize);
			float distance = enemyShip.VisualSprite.Position.DistanceTo(targetPixelPos);
			float duration = Mathf.Max(0.3f, distance / 500f); 

			string sfxPath = Database.GetShipMovementSoundPath(enemyShip.Name);
			if (!string.IsNullOrEmpty(sfxPath))
			{
				AudioStream sfx = GD.Load<AudioStream>(sfxPath);
				if (sfx != null) { _map.SfxPlayer.Stream = sfx; _map.SfxPlayer.Play(); }
			}

			tween.TweenProperty(enemyShip.VisualSprite, "position", targetPixelPos, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			float targetAngle = enemyShip.VisualSprite.Position.AngleToPoint(targetPixelPos) + enemyShip.BaseRotationOffset;
			tween.Parallel().TweenProperty(enemyShip.VisualSprite, "rotation", targetAngle, 0.2f);
			
			tween.Finished += () => { EnemyActionLoop(enemyShip, currentPos, targetPlayer); };
		}
		else
		{
			EnemyActionLoop(enemyShip, currentPos, targetPlayer);
		}
	}

	private void EnemyActionLoop(MapEntity enemyShip, Vector2I currentPos, Vector2I targetPlayer)
	{
		if (!CanUseBattleScene()) return;
		if (enemyShip.IsDead || !InCombat) return;

		if (!_map.HexContents.ContainsKey(targetPlayer) || _map.HexContents[targetPlayer].Type != GameConstants.EntityTypes.PlayerFleet)
		{
			if (enemyShip.CurrentActions > 0) 
			{
				_map.GetTree().CreateTimer(0.5f).Timeout += () => ExecuteSingleEnemyAI(enemyShip);
				return;
			}
		}
		else 
		{
			int finalDist = HexMath.HexDistance(currentPos, targetPlayer);
			if (enemyShip.CurrentActions > 0 && finalDist <= enemyShip.AttackRange)
			{
				PerformAttack(currentPos, targetPlayer);
				
				if (enemyShip.CurrentActions > 0 && InCombat)
				{
					_map.GetTree().CreateTimer(0.6f).Timeout += () => EnemyActionLoop(enemyShip, currentPos, targetPlayer);
					return;
				}
			}
		}
		
		_map.GetTree().CreateTimer(0.8f).Timeout += () => EndActiveTurn();
	}
}
