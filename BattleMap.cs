using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattleMap : Node2D
{
	[Export] public float HexSize = 65f; 
	[Export] public BattleUI UI;
	[Export] public DialogueUI ConversationUI; 

	internal int ScanningRange = 3; 
	internal int CurrentTurn = 1; 

	internal GlobalData _globalData;
	internal Dictionary<Vector2I, Node2D> HexGrid = new Dictionary<Vector2I, Node2D>();
	internal Dictionary<Vector2I, MapEntity> HexContents = new Dictionary<Vector2I, MapEntity>();
	internal List<Vector2I> SelectedHexes = new List<Vector2I>(); 
	
	internal TacticalCamera MapCamera;
	
	internal Node2D EntityLayer = new Node2D(); 
	internal Node2D EnvironmentLayer = new Node2D(); 
	internal Node2D RadiationLayer = new Node2D(); 
	internal Node2D FogLayer = new Node2D(); 

	internal AudioStreamPlayer SfxPlayer; 
	internal AudioStreamPlayer LaserPlayer;
	internal AudioStreamPlayer ExplosionPlayer;

	internal HashSet<Vector2I> AsteroidHexes = new HashSet<Vector2I>(); 
	internal HashSet<Vector2I> RadiationHexes = new HashSet<Vector2I>(); 

	public CombatManager Combat { get; private set; }
	public HazardManager Hazards { get; private set; }
	public FogOfWarManager Fog { get; private set; }

	public int ActiveMovementTweens = 0;
	public bool IsFleetMoving => ActiveMovementTweens > 0;

	private int _maxMapRadius = 35; 
	private PackedScene _hexScene = GD.Load<PackedScene>("res://hex_tile.tscn");
	
	private CanvasLayer _bgLayer = new CanvasLayer { Layer = -1 }; 
	private Node2D _gridLayer = new Node2D();  
	private Node2D _highlightLayer = new Node2D(); 
	
	private Polygon2D _hoverHighlight;
	private Vector2I _currentHoveredHex;
	
	// --- NEW: Radar variables ---
	private Polygon2D _radarHighlight; 
	public bool IsTargetingLongRange = false;

	internal SelectionBox SelectionBoxUI; 
	private AudioStreamPlayer _bgmPlayer;

	internal bool IsJumping = false; 
	internal MapEntity CurrentlyViewedShip = null;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (_globalData != null && _globalData.CurrentTurn > 0) CurrentTurn = _globalData.CurrentTurn;
		
		Texture2D cursorTex = GD.Load<Texture2D>("res://cursor.png");
		if (cursorTex != null)
		{
			Input.SetCustomMouseCursor(cursorTex, Input.CursorShape.Arrow, Vector2.Zero);
		}

		Combat = new CombatManager(this);
		Hazards = new HazardManager(this);
		Fog = new FogOfWarManager(this);

		AddChild(_bgLayer);
		AddChild(_gridLayer);
		AddChild(RadiationLayer); 
		AddChild(EnvironmentLayer); 
		AddChild(FogLayer); 
		AddChild(_highlightLayer);
		AddChild(EntityLayer);

		SelectionBoxUI = new SelectionBox();
		AddChild(SelectionBoxUI);

		_hoverHighlight = new Polygon2D();
		Vector2[] points = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float angle_deg = 60 * i - 30;
			float angle_rad = Mathf.DegToRad(angle_deg);
			points[i] = new Vector2(HexSize * Mathf.Cos(angle_rad), HexSize * Mathf.Sin(angle_rad));
		}
		_hoverHighlight.Polygon = points;
		_hoverHighlight.Color = new Color(0f, 1f, 1f, 0.4f); 
		_hoverHighlight.Visible = false; 
		AddChild(_hoverHighlight);

		// --- Massive Radar Circle Setup ---
		_radarHighlight = new Polygon2D();
		int circlePoints = 32;
		Vector2[] radarPoints = new Vector2[circlePoints];
		float radiusPixels = 10f * HexSize * Mathf.Sqrt(3); // Radius of 10 hexes
		for(int i = 0; i < circlePoints; i++) {
			float angle = (Mathf.Pi * 2 / circlePoints) * i;
			radarPoints[i] = new Vector2(Mathf.Cos(angle) * radiusPixels, Mathf.Sin(angle) * radiusPixels);
		}
		_radarHighlight.Polygon = radarPoints;
		_radarHighlight.Color = new Color(0f, 1f, 0.3f, 0.2f); // Semi-transparent bright green
		_radarHighlight.Visible = false;
		AddChild(_radarHighlight);

		SetupCamera();
		SetupAudio(); 
		ConnectUIButtons(); 
		
		MapSpawner.SetupSpaceBackground(_bgLayer, GetViewportRect().Size);
		MapSpawner.GenerateGrid(_maxMapRadius, HexSize, _hexScene, _gridLayer, HexGrid);
		
		Fog.GenerateFog(FogLayer, HexGrid.Keys, HexSize);

		if (_globalData != null && !string.IsNullOrEmpty(_globalData.SavedSystem) && _globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			Fog.SetExploredHexes(_globalData.ExploredSystems[_globalData.SavedSystem].ExploredHexes);
		}

		MapSpawner.PopulateMapFromMemory(_globalData, _maxMapRadius, HexSize, HexGrid, HexContents, EntityLayer, EnvironmentLayer, RadiationLayer, AsteroidHexes, RadiationHexes);
		
		Fog.UpdateVisibility();
		CenterCameraOnFleet(); 
		
		if (_globalData != null && _globalData.InCombat) Combat.RestoreCombatState(_globalData.CurrentQueueIndex);
		else Combat.CheckForCombatTrigger();

		if (UI != null)
		{
			UI.EndTurnButton.Visible = Combat.InCombat; 
			UI.RepairFleetButton.Visible = !Combat.InCombat; 
			UI.InventoryButton.Visible = !Combat.InCombat;
			UI.GameOverPanel.Visible = false;
			UI.InfoPanel.Visible = false;
			UI.CombatLogPanel.Visible = false;
			UI.AttackButton.Visible = false;
			UI.JumpButton.Visible = false;
			UI.ShipMenuPanel.Position = new Vector2(GetViewportRect().Size.X + 50, UI.ShipMenuPanel.Position.Y);
			
			UpdateResourceUI();
		}
	}

	internal void UpdateResourceUI()
	{
		if (UI == null || UI.InventoryDisplay == null || _globalData == null) return;
		
		float raw = _globalData.FleetResources["Raw Materials"].AsSingle();
		float energy = _globalData.FleetResources["Energy Cores"].AsSingle();
		float tech = _globalData.FleetResources["Ancient Tech"].AsSingle();

		UI.InventoryDisplay.Text = $"Raw Materials: {raw:0.##}\nEnergy Cores: {energy:0.##}\nAncient Tech: {tech:0.##}";
	}

	private void ConnectUIButtons()
	{
		if (UI == null) return;
		UI.EndTurnButton.Pressed += OnEndTurnPressed;
		UI.SaveGameButton.Pressed += OnSaveGamePressed;
		UI.RepairFleetButton.Pressed += OnRepairFleetPressed;
		UI.InventoryButton.Pressed += OnInventoryPressed;
		UI.MainMenuButton.Pressed += OnMainMenuPressed;
		UI.JumpButton.Pressed += OnJumpPressed;
		UI.AttackButton.Pressed += OnAttackPressed;
		UI.GameOverReturnButton.Pressed += OnMainMenuPressed;
		UI.BtnRepair.Pressed += OnRepairPressed;
		UI.BtnScan.Pressed += OnScanPressed;
		UI.BtnSalvage.Pressed += OnSalvagePressed;
		UI.CodexButton.Pressed += OnCodexPressed;
		UI.CloseMenuButton.Pressed += () => ToggleShipMenu(false);
		UI.TurnLabel.Text = $"TURN {CurrentTurn}";
		
		if (UI.BtnLongRange != null) UI.BtnLongRange.Pressed += OnLongRangePressed;
	}

	private void CenterCameraOnFleet()
	{
		foreach (var kvp in HexContents)
		{
			if (kvp.Value.Type == "Player Fleet")
			{
				MapCamera.Position = HexMath.HexToPixel(kvp.Key, HexSize);
				break; 
			}
		}
	}

	private void SetupAudio() 
	{ 
		_bgmPlayer = new AudioStreamPlayer();
		AddChild(_bgmPlayer);
		AudioStream bgmStream = GD.Load<AudioStream>("res://battle_theme.mp3"); 
		if (bgmStream != null) { _bgmPlayer.Stream = bgmStream; _bgmPlayer.VolumeDb = -15.0f; _bgmPlayer.Play(); }

		SfxPlayer = new AudioStreamPlayer();
		AddChild(SfxPlayer);
		SfxPlayer.VolumeDb = -5.0f; 

		LaserPlayer = new AudioStreamPlayer();
		AddChild(LaserPlayer);
		LaserPlayer.VolumeDb = -3.0f;
		AudioStream laserStream = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
		if (laserStream != null) LaserPlayer.Stream = laserStream;

		ExplosionPlayer = new AudioStreamPlayer();
		AddChild(ExplosionPlayer);
		ExplosionPlayer.VolumeDb = 0.0f; 
		AudioStream boomStream = GD.Load<AudioStream>("res://Sounds/explosion.wav"); 
		if (boomStream != null) ExplosionPlayer.Stream = boomStream;
	}

	private void SetupCamera() 
	{ 
		MapCamera = new TacticalCamera(); 
		AddChild(MapCamera); 
		float w = _maxMapRadius * HexSize * Mathf.Sqrt(3);
		float h = _maxMapRadius * HexSize * 1.5f;
		int padding = 300; 
		MapCamera.Initialize(this, SelectionBoxUI, -w - padding, w + padding, -h - padding, h + padding);
	}

	public override void _Input(InputEvent @event)
	{
		// Intercept clicks if we are using the deep scanner
		if (IsTargetingLongRange)
		{
			if (@event is InputEventMouseButton targetEvent && targetEvent.Pressed)
			{
				if (targetEvent.ButtonIndex == MouseButton.Left)
				{
					ExecuteLongRangeScan(HexMath.PixelToHex(GetGlobalMousePosition(), HexSize));
				}
				else if (targetEvent.ButtonIndex == MouseButton.Right)
				{
					IsTargetingLongRange = false; // Cancel scan
					LogCombatMessage("[color=yellow]Long Range Scan Cancelled.[/color]");
				}
			}
			return; // Consume input so ships don't accidentally move!
		}

		bool isMoveCommand = false;
		Vector2I targetHex = _currentHoveredHex;

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.M)
		{
			isMoveCommand = true;
		}
		else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Right)
		{
			isMoveCommand = true;
			targetHex = HexMath.PixelToHex(GetGlobalMousePosition(), HexSize); 
		}

		if (isMoveCommand && !IsFleetMoving)
		{
			if (SelectedHexes.Count > 0 && HexGrid.ContainsKey(targetHex))
			{
				float totalFuelNeeded = 0f;
				foreach (Vector2I sHex in SelectedHexes)
				{
					if (HexContents.ContainsKey(sHex) && HexContents[sHex].Type == "Player Fleet")
					{
						totalFuelNeeded += HexMath.HexDistance(sHex, targetHex) * 0.25f;
					}
				}

				if (_globalData != null && _globalData.FleetResources["Raw Materials"].AsSingle() < totalFuelNeeded)
				{
					if (UI != null) UI.CombatLogPanel.Visible = true;
					LogCombatMessage($"\n[color=red]*** MOVEMENT ABORTED: INSUFFICIENT FUEL ({totalFuelNeeded} Raw Materials Req) ***[/color]");
					return; 
				}

				bool playerActuallyMoved = false;

				if (SelectedHexes.Count == 1)
				{
					Vector2I shipHex = SelectedHexes[0];
					if (HexContents.ContainsKey(shipHex) && HexContents[shipHex].Type == "Player Fleet")
					{
						MapEntity ship = HexContents[shipHex];
						
						if (!Combat.InCombat)
						{
							if (MapSpawner.IsHexEmpty(targetHex, HexGrid, HexContents))
							{
								MoveShip(shipHex, targetHex, 0);
								SelectedHexes[0] = targetHex;
								UpdateHighlights();
								playerActuallyMoved = true;
							}
						}
						else
						{
							Dictionary<Vector2I, int> reachable = GetReachableHexes(shipHex, ship.CurrentActions);
							if (reachable.ContainsKey(targetHex))
							{
								int cost = reachable[targetHex];
								MoveShip(shipHex, targetHex, cost);
								SelectedHexes[0] = targetHex;
								UpdateHighlights();
							}
						}
					}
				}
				else if (!Combat.InCombat) 
				{
					MoveGroup(SelectedHexes, targetHex);
					playerActuallyMoved = true;
				}

				if (playerActuallyMoved && !Combat.InCombat)
				{
					AdvanceExplorationTurn(); 
				}
			}
		}
	}

	private void ExecuteLongRangeScan(Vector2I targetHex)
	{
		IsTargetingLongRange = false;
		
		if (_globalData == null || _globalData.FleetResources["Energy Cores"].AsSingle() < 5f) 
		{
			LogCombatMessage("\n[color=red]*** SCAN FAILED: INSUFFICIENT ENERGY CORES (5.0 Req) ***[/color]");
			return;
		}

		_globalData.FleetResources["Energy Cores"] = _globalData.FleetResources["Energy Cores"].AsSingle() - 5f;
		UpdateResourceUI();

		SystemData sys = _globalData.ExploredSystems[_globalData.SavedSystem];
		
		// Generate hexes within a radius of 10
		for (int q = -10; q <= 10; q++)
		{
			int r1 = Mathf.Max(-10, -q - 10);
			int r2 = Mathf.Min(10, -q + 10);
			for (int r = r1; r <= r2; r++)
			{
				Vector2I h = new Vector2I(targetHex.X + q, targetHex.Y + r);
				if (!sys.RadarRevealedHexes.Contains(h)) sys.RadarRevealedHexes.Add(h);
			}
		}

		Fog.UpdateVisibility(); // Lift the fog immediately!
		
		UI.CombatLogPanel.Visible = true;
		LogCombatMessage("\n[color=#00ffff]*** DEEP SPACE TELEMETRY UPDATED ***[/color]");
		LogCombatMessage("[color=yellow]-5.0 Energy Cores[/color]");

		if (SfxPlayer != null)
		{
			AudioStream scanSfx = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (scanSfx != null) { SfxPlayer.Stream = scanSfx; SfxPlayer.PitchScale = 0.4f; SfxPlayer.Play(); }
		}
		
		ToggleShipMenu(false);
	}

	private void OnLongRangePressed()
	{
		if (IsFleetMoving) return;
		IsTargetingLongRange = !IsTargetingLongRange;
		if (UI != null) UI.CombatLogPanel.Visible = true;
		if (IsTargetingLongRange) LogCombatMessage("\n[color=yellow]Awaiting Deep Scan Coordinates... (Left-Click to Scan, Right-Click to Cancel)[/color]");
	}

	internal void ProcessRoamingEnemies()
	{
		if (Combat.InCombat) return;

		List<Vector2I> playerPositions = new List<Vector2I>();
		foreach (var kvp in HexContents) if (kvp.Value.Type == "Player Fleet") playerPositions.Add(kvp.Key);
		if (playerPositions.Count == 0) return;

		List<KeyValuePair<Vector2I, MapEntity>> enemies = HexContents.Where(kvp => kvp.Value.Type == "Enemy Fleet").ToList();
		Random rng = new Random();

		foreach (var kvp in enemies)
		{
			Vector2I currentPos = kvp.Key;
			MapEntity enemyShip = kvp.Value;
			Vector2I targetPlayer = playerPositions[0];
			int minDistance = HexMath.HexDistance(currentPos, targetPlayer);
			
			foreach (Vector2I playerHex in playerPositions)
			{
				int dist = HexMath.HexDistance(currentPos, playerHex);
				if (dist < minDistance) { minDistance = dist; targetPlayer = playerHex; }
			}
			
			Vector2I bestNeighbor = currentPos;

			if (minDistance <= ScanningRange + 2)
			{
				int bestDist = minDistance;
				foreach (Vector2I dir in HexMath.Directions)
				{
					Vector2I neighbor = currentPos + dir;
					if (!HexGrid.ContainsKey(neighbor)) continue;
					if (MapSpawner.IsHexEmpty(neighbor, HexGrid, HexContents))
					{
						int distToTarget = HexMath.HexDistance(neighbor, targetPlayer);
						if (distToTarget < bestDist) { bestDist = distToTarget; bestNeighbor = neighbor; }
					}
				}
			}
			else 
			{
				List<Vector2I> validMoves = new List<Vector2I>();
				foreach (Vector2I dir in HexMath.Directions)
				{
					Vector2I neighbor = currentPos + dir;
					if (HexGrid.ContainsKey(neighbor) && MapSpawner.IsHexEmpty(neighbor, HexGrid, HexContents))
					{
						if (HexMath.HexDistance(neighbor, targetPlayer) > ScanningRange)
						{
							validMoves.Add(neighbor);
						}
					}
				}

				if (validMoves.Count > 0)
				{
					bestNeighbor = validMoves[rng.Next(validMoves.Count)];
				}
			}
			
			if (bestNeighbor != currentPos)
			{
				MoveShip(currentPos, bestNeighbor, 0); 
			}
		}

		GetTree().CreateTimer(0.5f).Timeout += () => 
		{
			if (Combat != null && !Combat.InCombat) 
			{
				Combat.CheckForCombatTrigger();
			}
		};
	}

	public override void _Process(double delta)
	{
		if (IsJumping || UI == null) return;
		
		if (IsFleetMoving) Fog.UpdateVisibility();

		Vector2 globalMousePos = GetGlobalMousePosition();
		_currentHoveredHex = HexMath.PixelToHex(globalMousePos, HexSize);
		
		if (IsTargetingLongRange)
		{
			_radarHighlight.Visible = true;
			_radarHighlight.Position = HexMath.HexToPixel(_currentHoveredHex, HexSize);
			_hoverHighlight.Visible = false;
		}
		else
		{
			_radarHighlight.Visible = false;
			if (HexGrid.ContainsKey(_currentHoveredHex))
			{
				_hoverHighlight.Visible = true;
				_hoverHighlight.Position = HexMath.HexToPixel(_currentHoveredHex, HexSize);
			}
			else
			{
				_hoverHighlight.Visible = false;
			}
		}

		foreach (Vector2I hex in SelectedHexes)
		{
			if (HexContents.ContainsKey(hex))
			{
				MapEntity selectedShip = HexContents[hex];
				if (GodotObject.IsInstanceValid(selectedShip.VisualSprite))
				{
					float targetAngle = selectedShip.VisualSprite.GlobalPosition.AngleToPoint(globalMousePos) + selectedShip.BaseRotationOffset;
					selectedShip.VisualSprite.Rotation = Mathf.LerpAngle(selectedShip.VisualSprite.Rotation, targetAngle, 0.15f);
				}
			}
		}

		EnvironmentLayer.Rotation -= 0.05f * (float)delta; 
		foreach (Node child in EnvironmentLayer.GetChildren())
		{
			if (child is Polygon2D rock)
			{
				float spin = rock.GetMeta("spin_speed").AsSingle();
				rock.Rotation += spin * (float)delta;
			}
		}
		
		Hazards.ProcessHazards(delta);
		UpdateJumpButton();
		UpdateAttackButton();
	}

	private void UpdateJumpButton()
	{
		bool isNearStargate = false;
		if (!Combat.InCombat)
		{
			UI.JumpButton.Text = "ENTER STARGATE";
			foreach (Vector2I hex in SelectedHexes)
			{
				if (HexContents.ContainsKey(hex) && HexContents[hex].Type == "Player Fleet")
				{
					foreach(Vector2I dir in HexMath.Directions)
					{
						Vector2I neighbor = hex + dir;
						if (HexContents.ContainsKey(neighbor) && HexContents[neighbor].Type == "StarGate")
						{
							isNearStargate = true;
							break;
						}
					}
				}
				if (isNearStargate) break;
			}
		}
		else
		{
			UI.JumpButton.Text = "EMERGENCY JUMP";
			isNearStargate = CanFleetEscape();
		}
		UI.JumpButton.Visible = isNearStargate;
	}

	private void UpdateAttackButton()
	{
		if (SelectedHexes.Count == 1 && HexContents.ContainsKey(SelectedHexes[0]))
		{
			MapEntity singleShip = HexContents[SelectedHexes[0]];
			if (singleShip.Type == "Player Fleet" && singleShip.CurrentActions > 0 && (!Combat.InCombat || singleShip == Combat.ActiveShip))
			{
				UI.AttackButton.Visible = true;
			}
			else
			{
				UI.AttackButton.Visible = false;
				Combat.IsTargeting = false;
				UI.AttackButton.Text = "ATTACK";
			}
		}
		else
		{
			UI.AttackButton.Visible = false;
			Combat.IsTargeting = false;
			UI.AttackButton.Text = "ATTACK";
		}
	}

	private bool CanFleetEscape()
	{
		List<Vector2I> playerHexes = new List<Vector2I>();
		List<Vector2I> gateHexes = new List<Vector2I>();
		foreach (var kvp in HexContents)
		{
			if (kvp.Value.Type == "Player Fleet") playerHexes.Add(kvp.Key);
			if (kvp.Value.Type == "StarGate") gateHexes.Add(kvp.Key);
		}
		if (playerHexes.Count == 0 || gateHexes.Count == 0) return false;
		foreach (Vector2I gate in gateHexes)
		{
			bool allNear = true;
			foreach (Vector2I ship in playerHexes) if (HexMath.HexDistance(gate, ship) > 1) { allNear = false; break; }
			if (allNear) return true;
		}
		return false;
	}

	private MapEntity GetAdjacentPlanet(MapEntity ship)
	{
		Vector2I shipHex = Vector2I.Zero;
		foreach(var kvp in HexContents) if (kvp.Value == ship) shipHex = kvp.Key;
		foreach(Vector2I dir in HexMath.Directions)
		{
			Vector2I n = shipHex + dir;
			if (HexContents.ContainsKey(n) && HexContents[n].Type == "Planet") return HexContents[n];
		}
		return null;
	}

	private PlanetData GetPlanetData(string planetName)
	{
		if (_globalData == null || string.IsNullOrEmpty(_globalData.SavedSystem)) return null;
		if (!_globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem)) return null;
		
		SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
		foreach (PlanetData p in currentSystem.Planets) if (p.Name == planetName) return p;
		return null;
	}

	internal void ToggleShipMenu(bool expand, MapEntity ship = null)
	{
		if (UI == null) return;
		Tween tween = CreateTween();
		Vector2 screenSize = GetViewportRect().Size;
		float targetX = expand ? screenSize.X - 320 : screenSize.X + 50; 
		tween.TweenProperty(UI.ShipMenuPanel, "position:x", targetX, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		
		if (UI.BtnLongRange != null) UI.BtnLongRange.Visible = false;

		if (expand && ship != null)
		{
			CurrentlyViewedShip = ship;
			UI.ShipMenuTitle.Text = $"== {ship.Name.ToUpper()} ==";

			// --- FIX: Safely check if the image path exists before loading! ---
			string imagePath = Database.GetShipTexturePath(ship.Name);
			if (!string.IsNullOrEmpty(imagePath))
			{
				Texture2D tex = GD.Load<Texture2D>(imagePath);
				if (tex != null) UI.ShipImageDisplay.Texture = tex;
			}
			
			float hpPercent = (float)ship.CurrentHP / ship.MaxHP;
			UI.ShipImageDisplay.Modulate = new Color(1f, hpPercent, hpPercent); 

			UI.HpBar.MaxValue = ship.MaxHP;
			UI.HpBar.Value = ship.CurrentHP;
			UI.HpLabel.Text = $"HULL INTEGRITY: {ship.CurrentHP}/{ship.MaxHP}";
			UI.ShieldBar.MaxValue = ship.MaxShields;
			UI.ShieldBar.Value = ship.CurrentShields;
			UI.ShieldLabel.Text = $"SHIELD CAPACITORS: {ship.CurrentShields}/{ship.MaxShields}";

			UI.ShipMenuDetails.Text = $"Classification: {ship.Type}\nAction Points: {ship.CurrentActions}/{ship.MaxActions}\nWeapon Payload: 0-{ship.AttackDamage} Dmg\nTargeting Range: {ship.AttackRange} Hexes\n";
				
			bool isPlayer = ship.Type == "Player Fleet";
			UI.BtnWeapons.Visible = isPlayer;
			UI.BtnShields.Visible = isPlayer;
			UI.BtnRepair.Visible = isPlayer;
			UI.BtnRepair.Disabled = ship.CurrentActions < 2;
			UI.CodexButton.Visible = isPlayer;
			UI.BtnScan.Visible = false;
			UI.BtnSalvage.Visible = false;

			if (isPlayer && !Combat.InCombat)
			{
				if (ship.Name == "The Aether Skimmer")
				{
					if (UI.BtnLongRange != null)
					{
						UI.BtnLongRange.Visible = true;
						UI.BtnLongRange.Disabled = _globalData.FleetResources["Energy Cores"].AsSingle() < 5f;
					}
				}

				MapEntity adjPlanet = GetAdjacentPlanet(ship);
				if (adjPlanet != null)
				{
					PlanetData pData = GetPlanetData(adjPlanet.Name);
					if (pData != null)
					{
						if (ship.Name == "The Aether Skimmer" || ship.Name == "The Relic Harvester")
						{
							UI.BtnScan.Visible = true;
							UI.BtnScan.Disabled = pData.HasBeenScanned || ship.CurrentActions < 1;
							UI.BtnScan.Text = pData.HasBeenScanned ? "SCANNED" : "SCAN";
						}
						
						if (ship.Name == "The Relic Harvester" || ship.Name == "The Neptune Forge")
						{
							UI.BtnSalvage.Visible = true;
							UI.BtnSalvage.Disabled = pData.HasBeenSalvaged || ship.CurrentActions < 1;
							UI.BtnSalvage.Text = pData.HasBeenSalvaged ? "SALVAGED" : "SALVAGE";
						}
					}
				}
			}
		}
	}

	private void OnScanPressed()
	{
		if (IsFleetMoving) return; 
		if (CurrentlyViewedShip == null || CurrentlyViewedShip.CurrentActions < 1) return;
		MapEntity planet = GetAdjacentPlanet(CurrentlyViewedShip);
		if (planet == null) return;

		PlanetData pData = GetPlanetData(planet.Name);
		if (pData != null && pData.HasBeenScanned) return; 

		if (_globalData != null)
		{
			float currentCores = _globalData.FleetResources["Energy Cores"].AsSingle();
			if (currentCores < 0.5f)
			{
				UI.CombatLogPanel.Visible = true;
				LogCombatMessage($"\n[color=red]*** SCAN FAILED: INSUFFICIENT ENERGY CORES (0.5 Req) ***[/color]");
				return;
			}
			_globalData.FleetResources["Energy Cores"] = currentCores - 0.5f;
			UpdateResourceUI(); 
		}

		CurrentlyViewedShip.CurrentActions -= 1;
		if (pData != null) pData.HasBeenScanned = true;

		Random rng = new Random();
		
		if (rng.Next(0, 100) < 30 && ConversationUI != null) 
		{
			UI.CombatLogPanel.Visible = true;
			LogCombatMessage($"\n[color=yellow]*** INCOMING TRANSMISSION FROM SURFACE ***[/color]");
			
			ConversationUI.StartConversation("Stranded_Miner"); 
			
			ToggleShipMenu(true, CurrentlyViewedShip);
			return; 
		}

		float scale = planet.VisualSprite.Scale.X;
		string sizeClass = scale > 0.6f ? "Massive" : (scale > 0.5f ? "Standard" : "Dwarf");
		
		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=#00ffff]--- SENSOR SWEEP COMPLETED (-0.5 Energy) ---[/color]");
		LogCombatMessage($"Target: {planet.Name}");
		LogCombatMessage($"Size Class: {sizeClass}");
		LogCombatMessage($"Projected Salvage Operation: {Mathf.Max(1, Mathf.RoundToInt(scale * 5f))} Turns");
		LogCombatMessage($"Caution: extended operations carry risk of hostile detection.");

		ToggleShipMenu(true, CurrentlyViewedShip); 
	}

	private void OnSalvagePressed()
	{
		if (IsFleetMoving) return; 
		if (CurrentlyViewedShip == null || CurrentlyViewedShip.CurrentActions < 1) return;
		MapEntity planet = GetAdjacentPlanet(CurrentlyViewedShip);
		if (planet == null) return;

		PlanetData pData = GetPlanetData(planet.Name);
		if (pData != null && pData.HasBeenSalvaged) return; 

		if (_globalData != null)
		{
			float currentRaw = _globalData.FleetResources["Raw Materials"].AsSingle();
			if (currentRaw < 1.0f)
			{
				UI.CombatLogPanel.Visible = true;
				LogCombatMessage($"\n[color=red]*** SALVAGE FAILED: INSUFFICIENT RAW MATERIALS (1.0 Req) ***[/color]");
				return;
			}
			_globalData.FleetResources["Raw Materials"] = currentRaw - 1.0f;
			UpdateResourceUI(); 
		}

		float scale = planet.VisualSprite.Scale.X;
		int turnsNeeded = Mathf.Max(1, Mathf.RoundToInt(scale * 5f)); 

		CurrentlyViewedShip.CurrentActions = 0; 

		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=yellow]--- SALVAGE OPERATION COMMENCED (-1.0 Raw Materials) ---[/color]");

		bool ambushed = false;

		for (int i = 0; i < turnsNeeded; i++)
		{
			AdvanceExplorationTurn();

			List<Vector2I> players = new List<Vector2I>();
			List<Vector2I> enemies = new List<Vector2I>();
			foreach (var kvp in HexContents)
			{
				if (kvp.Value.Type == "Player Fleet") players.Add(kvp.Key);
				if (kvp.Value.Type == "Enemy Fleet") enemies.Add(kvp.Key);
			}

			foreach (Vector2I p in players)
			{
				foreach (Vector2I e in enemies)
				{
					if (HexMath.HexDistance(p, e) <= ScanningRange)
					{
						ambushed = true;
						break;
					}
				}
				if (ambushed) break;
			}

			if (ambushed)
			{
				LogCombatMessage($"[color=red]*** HOSTILES DETECTED! SALVAGE ABORTED! ***[/color]");
				Combat.CheckForCombatTrigger();
				break;
			}
		}

		if (!ambushed)
		{
			if (pData != null) pData.HasBeenSalvaged = true; 

			Random rng = new Random();
			float rawYield = rng.Next(50, 151); 
			float energyYield = rng.Next(1, 9); 
			float techYield = rng.Next(1, 4); 

			if (_globalData != null)
			{
				_globalData.FleetResources["Raw Materials"] = _globalData.FleetResources["Raw Materials"].AsSingle() + rawYield;
				_globalData.FleetResources["Energy Cores"] = _globalData.FleetResources["Energy Cores"].AsSingle() + energyYield;
				_globalData.FleetResources["Ancient Tech"] = _globalData.FleetResources["Ancient Tech"].AsSingle() + techYield;
				UpdateResourceUI(); 
			}

			LogCombatMessage($"[color=green]Operation Successful. Acquired:[/color]");
			LogCombatMessage($"[color=green]- {rawYield} Raw Materials[/color]");
			LogCombatMessage($"[color=green]- {energyYield} Energy Cores[/color]");
			LogCombatMessage($"[color=green]- {techYield} Ancient Tech[/color]");
		}

		ToggleShipMenu(true, CurrentlyViewedShip); 
	}

	private void OnCodexPressed() 
	{ 
		if (IsFleetMoving) return; 
		OnSaveGamePressed(); 
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://codex.tscn");
		else GetTree().ChangeSceneToFile("res://codex.tscn");
	}

	private void OnInventoryPressed()
	{
		UI.CombatLogPanel.Visible = true;
		LogCombatMessage("\n[color=yellow]--- FLEET INVENTORY ---[/color]");
		if (_globalData != null)
		{
			foreach(var kvp in _globalData.FleetResources)
			{
				LogCombatMessage($"- {kvp.Key}: {kvp.Value.AsSingle():0.##}");
			}
		}
		LogCombatMessage("*(Note: Upgrade module linking requires adjacent fleet positioning.)*");
	}

	internal void OnRepairPressed()
	{
		if (IsFleetMoving) return; 
		if (CurrentlyViewedShip == null || CurrentlyViewedShip.IsDead || CurrentlyViewedShip.Type != "Player Fleet") return;
		if (CurrentlyViewedShip.CurrentActions < 2) return;
		CurrentlyViewedShip.CurrentActions -= 2;
		
		Random rng = new Random();
		int healAmount = rng.Next(15, 30); 
		CurrentlyViewedShip.CurrentHP = Mathf.Min(CurrentlyViewedShip.CurrentHP + healAmount, CurrentlyViewedShip.MaxHP);
		
		if (SfxPlayer != null)
		{
			AudioStream repairSound = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (repairSound != null) { SfxPlayer.Stream = repairSound; SfxPlayer.PitchScale = 1.5f; SfxPlayer.Play(); }
		}
		
		ToggleShipMenu(true, CurrentlyViewedShip);
	}

	private void OnRepairFleetPressed()
	{
		if (IsFleetMoving || Combat.InCombat) return; 

		int totalMissing = 0;
		foreach (var kvp in HexContents)
		{
			if (kvp.Value.Type == "Player Fleet")
			{
				totalMissing += (kvp.Value.MaxHP - kvp.Value.CurrentHP) + (kvp.Value.MaxShields - kvp.Value.CurrentShields);
			}
		}

		if (totalMissing == 0) return;

		int turnsNeeded = (totalMissing / 20) + 1; 
		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=cyan]--- REPAIRING FLEET (Estimated {turnsNeeded} Turns) ---[/color]");

		bool ambushed = false;

		for (int i = 0; i < turnsNeeded; i++)
		{
			AdvanceExplorationTurn();

			foreach (var kvp in HexContents)
			{
				if (kvp.Value.Type == "Player Fleet")
				{
					kvp.Value.CurrentHP = Mathf.Min(kvp.Value.CurrentHP + 15, kvp.Value.MaxHP);
					kvp.Value.CurrentShields = Mathf.Min(kvp.Value.CurrentShields + 10, kvp.Value.MaxShields);
				}
			}

			List<Vector2I> players = new List<Vector2I>();
			List<Vector2I> enemies = new List<Vector2I>();
			foreach (var kvp in HexContents)
			{
				if (kvp.Value.Type == "Player Fleet") players.Add(kvp.Key);
				if (kvp.Value.Type == "Enemy Fleet") enemies.Add(kvp.Key);
			}

			foreach (Vector2I p in players)
			{
				foreach (Vector2I e in enemies)
				{
					if (HexMath.HexDistance(p, e) <= ScanningRange)
					{
						ambushed = true;
						break;
					}
				}
				if (ambushed) break;
			}

			if (ambushed)
			{
				LogCombatMessage($"[color=red]*** REPAIRS INTERRUPTED BY ENEMY FLEET! ***[/color]");
				Combat.CheckForCombatTrigger();
				break;
			}
		}

		if (!ambushed) LogCombatMessage($"[color=green]Fleet repairs complete.[/color]");
	}

	internal void OnEndTurnPressed()
	{
		if (IsFleetMoving) return; 

		if (!Combat.InCombat)
		{
			AdvanceExplorationTurn();
			
			SelectedHexes.Clear();
			ToggleShipMenu(false);
			UpdateHighlights();
			Fog.UpdateVisibility();
		}
		else if (Combat.ActiveShip != null && Combat.ActiveShip.Type == "Player Fleet") Combat.EndActiveTurn();
	}

	private void AdvanceExplorationTurn()
	{
		CurrentTurn++;
		foreach (var kvp in HexContents)
		{
			if (kvp.Value.Type == "Player Fleet" || kvp.Value.Type == "Enemy Fleet") kvp.Value.CurrentActions = kvp.Value.MaxActions; 
		}
		if (UI != null) UI.TurnLabel.Text = $"TURN {CurrentTurn}";
		
		ProcessRoamingEnemies(); 
	}

	private void OnJumpPressed()
	{
		if (IsJumping || IsFleetMoving) return; 
		IsJumping = true;

		if (UI != null)
		{
			UI.JumpButton.Visible = false;
			UI.AttackButton.Visible = false;
			UI.InfoPanel.Visible = false;
		}
		ToggleShipMenu(false);

		Vector2I gateHex = new Vector2I(0,0);
		bool gateFound = false;
		bool isEmergencyJump = Combat.InCombat; 
		
		if (Combat.InCombat)
		{
			List<Vector2I> playerHexes = new List<Vector2I>();
			List<Vector2I> gateHexes = new List<Vector2I>();
			foreach(var kvp in HexContents) {
				if (kvp.Value.Type == "Player Fleet") playerHexes.Add(kvp.Key);
				if (kvp.Value.Type == "StarGate") gateHexes.Add(kvp.Key);
			}
			foreach (Vector2I gate in gateHexes) {
				bool allNear = true;
				foreach (Vector2I ship in playerHexes) {
					if (HexMath.HexDistance(gate, ship) > 1) { allNear = false; break; }
				}
				if (allNear) { gateHex = gate; gateFound = true; break; }
			}
		}
		else
		{
			foreach(var kvp in HexContents) {
				if (kvp.Value.Type == "StarGate") {
					foreach (var p_hex in SelectedHexes) {
						if (HexMath.HexDistance(kvp.Key, p_hex) <= 1) { gateHex = kvp.Key; gateFound = true; break; }
					}
				}
				if (gateFound) break;
			}
		}

		if (!gateFound) return;

		if (_globalData != null) _globalData.InCombat = false;

		Vector2 gatePixelPos = HexMath.HexToPixel(gateHex, HexSize);

		Tween warpTween = CreateTween();
		warpTween.SetParallel(true);

		foreach(var kvp in HexContents)
		{
			if (kvp.Value.Type == "Player Fleet")
			{
				Sprite2D shipSprite = kvp.Value.VisualSprite;
				if (GodotObject.IsInstanceValid(shipSprite))
				{
					warpTween.TweenProperty(shipSprite, "position", gatePixelPos, 1.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
					warpTween.TweenProperty(shipSprite, "scale", new Vector2(0.01f, 0.01f), 1.5f).SetTrans(Tween.TransitionType.Cubic);
					warpTween.TweenProperty(shipSprite, "rotation", shipSprite.Rotation + Mathf.Pi * 4, 1.5f);
					warpTween.TweenProperty(shipSprite, "modulate", new Color(1, 2, 3, 0), 1.5f); 
				}
			}
		}

		if (SfxPlayer != null)
		{
			AudioStream jumpSound = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (jumpSound != null) { SfxPlayer.Stream = jumpSound; SfxPlayer.PitchScale = 0.5f; SfxPlayer.Play(); }
		}

		warpTween.Chain().TweenCallback(Callable.From(() => 
		{
			if (_globalData != null)
			{
				_globalData.JustJumped = true;
				if (isEmergencyJump)
				{
					Random rng = new Random();
					if (_globalData.CurrentSectorStars != null && _globalData.CurrentSectorStars.Count > 1)
					{
						var availableStars = _globalData.CurrentSectorStars.Where(s => s.SystemName != _globalData.SavedSystem).ToList();
						if (availableStars.Count > 0) _globalData.SavedSystem = availableStars[rng.Next(availableStars.Count)].SystemName;
						else _globalData.SavedSystem = "SECTOR-" + rng.Next(1000, 9999);
					}
					else _globalData.SavedSystem = "SECTOR-" + rng.Next(1000, 9999);
					_globalData.SavedPlanet = ""; 
				}
			}
			
			OnSaveGamePressed(); 
			SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
			string nextScene = isEmergencyJump ? "res://exploration_battle.tscn" : "res://galactic_map.tscn";
			
			if (transitioner != null) transitioner.ChangeScene(nextScene);
			else GetTree().ChangeSceneToFile(nextScene);
		}));
	}

	private void OnSaveGamePressed()
	{
		if (_globalData != null)
		{
			_globalData.CurrentTurn = CurrentTurn;
			_globalData.InCombat = Combat.InCombat;
			_globalData.CurrentQueueIndex = Combat.GetCurrentQueueIndex();

			if (!string.IsNullOrEmpty(_globalData.SavedSystem) && _globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
			{
				SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
				currentSystem.AsteroidHexes = AsteroidHexes.ToList();
				currentSystem.RadiationHexes = RadiationHexes.ToList();
				if (Fog != null) currentSystem.ExploredHexes = Fog.GetExploredHexes().ToList();
			}
			
			var playerState = new Godot.Collections.Array();
			var enemyState = new Godot.Collections.Array(); 
			
			foreach (var kvp in HexContents)
			{
				if (kvp.Value.Type == "Player Fleet" || kvp.Value.Type == "Enemy Fleet")
				{
					var shipDict = new Godot.Collections.Dictionary<string, Variant>();
					shipDict["Name"] = kvp.Value.Name;
					shipDict["Q"] = kvp.Key.X; shipDict["R"] = kvp.Key.Y; 
					shipDict["CurrentHP"] = kvp.Value.CurrentHP; shipDict["MaxHP"] = kvp.Value.MaxHP;
					shipDict["CurrentShields"] = kvp.Value.CurrentShields; shipDict["MaxShields"] = kvp.Value.MaxShields;
					shipDict["MaxActions"] = kvp.Value.MaxActions;
					shipDict["CurrentActions"] = kvp.Value.CurrentActions;
					shipDict["CurrentInitiativeRoll"] = kvp.Value.CurrentInitiativeRoll; 
					
					if (kvp.Value.Type == "Player Fleet") playerState.Add(shipDict);
					if (kvp.Value.Type == "Enemy Fleet") enemyState.Add(shipDict);
				}
			}
			
			_globalData.SavedFleetState = playerState;
			
			if (!string.IsNullOrEmpty(_globalData.SavedSystem) && _globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
			{
				_globalData.ExploredSystems[_globalData.SavedSystem].EnemyFleets = enemyState;
			}

			if (_globalData.HasMethod("SaveGame")) _globalData.Call("SaveGame");

			if (UI != null)
			{
				UI.SaveGameButton.Text = "GAME SAVED!";
				UI.SaveGameButton.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f)); 
				GetTree().CreateTimer(2.0f).Timeout += () => 
				{
					UI.SaveGameButton.Text = "SAVE GAME";
					UI.SaveGameButton.RemoveThemeColorOverride("font_color");
				};
			}
		}
	}

	private void OnMainMenuPressed() 
	{
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://main_menu.tscn");
		else GetTree().ChangeSceneToFile("res://main_menu.tscn");
	}

	internal void MoveShip(Vector2I fromHex, Vector2I toHex, int cost)
	{
		MapEntity ship = HexContents[fromHex];
		HexContents.Remove(fromHex);
		HexContents[toHex] = ship;
		ship.CurrentActions -= cost;
		if (SelectedHexes.Count == 1 && SelectedHexes[0] == fromHex) ToggleShipMenu(true, ship);
		
		string sfxPath = Database.GetShipMovementSoundPath(ship.Name);
		if (!string.IsNullOrEmpty(sfxPath) && SfxPlayer != null && ship.VisualSprite.Visible)
		{
			AudioStream sfx = GD.Load<AudioStream>(sfxPath);
			if (sfx != null) { SfxPlayer.Stream = sfx; SfxPlayer.Play(); }
		}

		if (ship.Type == "Player Fleet" && _globalData != null)
		{
			int distance = HexMath.HexDistance(fromHex, toHex);
			float fuelCost = distance * 0.25f;
			float currentFuel = _globalData.FleetResources["Raw Materials"].AsSingle();
			_globalData.FleetResources["Raw Materials"] = Mathf.Max(0f, currentFuel - fuelCost);
			UpdateResourceUI(); 
		}

		ActiveMovementTweens++;

		Tween tween = CreateTween();
		Vector2 targetPixelPos = HexMath.HexToPixel(toHex, HexSize);
		float distancePixels = ship.VisualSprite.Position.DistanceTo(targetPixelPos);
		float duration = Mathf.Max(0.3f, distancePixels / 500f); 
		tween.TweenProperty(ship.VisualSprite, "position", targetPixelPos, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		
		tween.TweenCallback(Callable.From(() => 
		{
			ActiveMovementTweens--;
			if (ActiveMovementTweens <= 0) 
			{
				Fog.UpdateVisibility(); 
			}
		}));
	}

	internal void MoveGroup(List<Vector2I> shipsToMove, Vector2I targetHex)
	{
		if (IsFleetMoving) return; 

		shipsToMove = shipsToMove.Where(h => HexContents.ContainsKey(h)).ToList();
		if (shipsToMove.Count == 0) return;

		Vector2I anchorHex = shipsToMove[0];
		List<Vector2I> newSelection = new List<Vector2I>();

		shipsToMove.Sort((a, b) => HexMath.HexDistance(a, targetHex).CompareTo(HexMath.HexDistance(b, targetHex)));

		foreach (Vector2I shipHex in shipsToMove)
		{
			Vector2I offset = new Vector2I(shipHex.X - anchorHex.X, shipHex.Y - anchorHex.Y);
			Vector2I desiredTarget = new Vector2I(targetHex.X + offset.X, targetHex.Y + offset.Y);
			Vector2I finalHex = FindNearestEmptyHex(desiredTarget);

			if (finalHex != shipHex)
			{
				MoveShip(shipHex, finalHex, 0); 
				newSelection.Add(finalHex);
			}
			else newSelection.Add(shipHex);
		}
		
		SelectedHexes = newSelection;
		UpdateHighlights();
	}

	internal Dictionary<Vector2I, int> GetReachableHexes(Vector2I startHex, int movementRange)
	{
		Dictionary<Vector2I, int> costSoFar = new Dictionary<Vector2I, int>();
		costSoFar[startHex] = 0;
		Queue<Vector2I> frontier = new Queue<Vector2I>();
		frontier.Enqueue(startHex);

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (Vector2I dir in HexMath.Directions)
			{
				Vector2I next = current + dir;
				if (!HexGrid.ContainsKey(next)) continue;

				bool isBlocked = false;
				if (HexContents.ContainsKey(next))
				{
					string type = HexContents[next].Type;
					if (type == "Planet" || type == "Base Planet (Player Start)" || type == "Celestial Body" || type == "Player Fleet" || type == "Enemy Fleet" || type == "StarGate") isBlocked = true; 
				}

				if (isBlocked) continue;

				int newCost = costSoFar[current] + 1; 
				if (newCost <= movementRange)
				{
					if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
					{
						costSoFar[next] = newCost;
						frontier.Enqueue(next);
					}
				}
			}
		}
		return costSoFar;
	}

	internal void UpdateHighlights()
	{
		foreach (Node child in _highlightLayer.GetChildren()) child.QueueFree();

		foreach (Vector2I hex in SelectedHexes) CreateHighlightPolygon(hex, new Color(1f, 0.8f, 0f, 0.6f)); 

		if (Combat.InCombat && SelectedHexes.Count == 1 && Combat.ActiveShip != null && HexContents.ContainsKey(SelectedHexes[0]) && HexContents[SelectedHexes[0]] == Combat.ActiveShip)
		{
			Dictionary<Vector2I, int> reachable = GetReachableHexes(SelectedHexes[0], Combat.ActiveShip.CurrentActions);
			foreach (Vector2I hex in reachable.Keys)
			{
				if (hex == SelectedHexes[0]) continue; 
				CreateHighlightPolygon(hex, new Color(0f, 1f, 0.3f, 0.4f)); 
			}
		}
	}

	private void CreateHighlightPolygon(Vector2I hexCoord, Color color)
	{
		Polygon2D poly = new Polygon2D();
		Vector2[] points = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float angle_deg = 60 * i - 30;
			float angle_rad = Mathf.DegToRad(angle_deg);
			points[i] = new Vector2(HexSize * Mathf.Cos(angle_rad), HexSize * Mathf.Sin(angle_rad));
		}
		poly.Polygon = points; poly.Color = color; poly.Position = HexMath.HexToPixel(hexCoord, HexSize);
		_highlightLayer.AddChild(poly);
	}

	internal void LogCombatMessage(string message) { if (UI != null && UI.CombatLogText != null) UI.CombatLogText.Text += message + "\n"; }

	internal void CheckGameOver()
	{
		if (!HexContents.Values.Any(s => s.Type == "Player Fleet")) UI.GameOverPanel.Visible = true;
	}

	private void OnAttackPressed()
	{
		if (IsFleetMoving) return; 
		Combat.IsTargeting = !Combat.IsTargeting;
		if (UI != null) UI.AttackButton.Text = Combat.IsTargeting ? "CANCEL TARGET" : "ATTACK";
	}

	private Vector2I FindNearestEmptyHex(Vector2I target)
	{
		if (HexGrid.ContainsKey(target) && MapSpawner.IsHexEmpty(target, HexGrid, HexContents)) return target;
		int radius = 1;
		while (radius < 15) 
		{
			Vector2I current = target + HexMath.Directions[4] * radius;
			for (int i = 0; i < 6; i++)
			{
				for (int j = 0; j < radius; j++)
				{
					if (HexGrid.ContainsKey(current) && MapSpawner.IsHexEmpty(current, HexGrid, HexContents)) return current;
					current += HexMath.Directions[i];
				}
			}
			radius++;
		}
		return target; 
	}
	internal void AwardEnemyKillSalvage(string enemyName)
	{
		if (_globalData == null || UI == null) return;

		// Generate random loot for the kill
		Random rng = new Random();
		float rawYield = rng.Next(15, 41); // 15 to 40 Raw Materials
		float energyYield = rng.Next(1, 4); // 1 to 3 Energy Cores
		float techYield = rng.Next(0, 100) < 25 ? 1f : 0f; // 25% chance to drop 1 Ancient Tech

		// Add to Global Inventory
		_globalData.FleetResources["Raw Materials"] = _globalData.FleetResources["Raw Materials"].AsSingle() + rawYield;
		_globalData.FleetResources["Energy Cores"] = _globalData.FleetResources["Energy Cores"].AsSingle() + energyYield;
		_globalData.FleetResources["Ancient Tech"] = _globalData.FleetResources["Ancient Tech"].AsSingle() + techYield;

		UpdateResourceUI();

		// Print the loot drop to the Combat Log
		UI.CombatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=#00ff00]*** {enemyName.ToUpper()} DESTROYED ***[/color]");
		LogCombatMessage($"[color=cyan]Combat Salvage:[/color] {rawYield} Raw Materials, {energyYield} Energy Cores{(techYield > 0 ? ", 1 Ancient Tech" : "")}");
	}
}
