using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// --- Custom Node to draw the drag-selection box ---
public partial class SelectionBox : Node2D
{
	public Vector2 StartPos;
	public Vector2 EndPos;
	public bool IsDragging = false;

	public override void _Draw()
	{
		if (IsDragging)
		{
			Rect2 rect = new Rect2(StartPos, EndPos - StartPos).Abs();
			DrawRect(rect, new Color(0.2f, 0.8f, 1f, 0.3f), true); 
			DrawRect(rect, new Color(0.2f, 0.8f, 1f, 0.8f), false, 2f); 
		}
	}
}

public partial class BattleMap : Node2D
{
	// --- MAP SETTINGS ---
	[Export] public float HexSize = 65f; 
	private int _maxMapRadius = 35; 
	private int _scanningRange = 5; 

	private PackedScene _hexScene = GD.Load<PackedScene>("res://hex_tile.tscn");
	private GlobalData _globalData;

	// --- CAMERA CONTROLS ---
	private Camera2D _camera;
	private float _panSpeed = 600f;
	private float _zoomSpeed = 0.1f;
	private float _minZoom = 0.3f;
	private float _maxZoom = 2.0f;

	// --- RENDER LAYERS ---
	private CanvasLayer _bgLayer = new CanvasLayer { Layer = -1 }; 
	private Node2D _gridLayer = new Node2D();  
	private Node2D _highlightLayer = new Node2D(); 
	private Node2D _entityLayer = new Node2D(); 
	private SelectionBox _selectionBox; 

	private Dictionary<Vector2I, Node2D> _hexGrid = new Dictionary<Vector2I, Node2D>();
	
	// --- AUDIO PLAYERS ---
	private AudioStreamPlayer _bgmPlayer;
	private AudioStreamPlayer _sfxPlayer; 
	private AudioStreamPlayer _laserPlayer;
	private AudioStreamPlayer _explosionPlayer;

	// --- ENTITY DATA STORAGE ---
	public class MapEntity
	{
		public string Name;
		public string Type;
		public string Details;
		
		public int MaxActions;
		public int CurrentActions; 
		
		public int AttackRange;
		public int AttackDamage;
		
		public int MaxHP;
		public int CurrentHP;
		public int MaxShields;
		public int CurrentShields;

		public int InitiativeBonus;
		public int CurrentInitiativeRoll;
		public bool IsDead = false;

		public Sprite2D VisualSprite; 
		public float BaseRotationOffset; 
	}
	private Dictionary<Vector2I, MapEntity> _hexContents = new Dictionary<Vector2I, MapEntity>();

	private string[] _enemyShipTypes = new string[] {
		"Aether Censor Obelisk", "Custodian Logic Barge", "Ignis Repurposed Terraformer",
		"Reformatter Dreadnought", "Scrap-Stick Subversion Drone"
	};

	private string[] _shipParts = new string[] {
		"Port Thrusters", "Starboard Hull", "Main Bridge", "Weapon Arrays", 
		"Shield Generators", "Aft Cargo Bay", "Navigational Sensors", "Life Support Systems"
	};
	private string[] _missTexts = new string[] {
		"Shot went wide!", "Evasive maneuvers successful!", 
		"Glanced harmlessly off the deflectors!", "Missed by a hair!"
	};

	// --- COMBAT STATE & SELECTION ---
	private bool _inCombat = false;
	private List<MapEntity> _initiativeQueue = new List<MapEntity>();
	private int _currentQueueIndex = 0;
	private MapEntity _activeShip = null;

	private List<Vector2I> _selectedHexes = new List<Vector2I>(); 
	private bool _isDragging = false;
	private Vector2 _dragStartPos;
	
	private int _currentTurn = 1; 
	private bool _isTargeting = false; 
	private bool _isJumping = false; 

	// --- UI ELEMENTS ---
	private PanelContainer _infoPanel; 
	private Label _infoLabel;
	private Button _endTurnButton;
	private Button _saveGameButton;
	private Button _repairFleetButton; 
	private Button _inventoryButton; 
	private Button _mainMenuButton;
	private Button _attackButton; 
	private Button _jumpButton; 
	private HBoxContainer _initiativeUI; 
	private Label _initiativeTurnLabel; 
	private ColorRect _gameOverPanel; 
	
	private PanelContainer _combatLogPanel;
	private RichTextLabel _combatLogText;

	// --- TACTICAL SHIP MENU UI ELEMENTS ---
	private PanelContainer _shipMenuPanel; 
	private Label _shipMenuTitle;
	private Label _shipMenuDetails;
	private TextureRect _shipImageDisplay;
	private ProgressBar _hpBar;
	private ProgressBar _shieldBar;
	private Label _hpLabel;
	private Label _shieldLabel;
	
	private Button _btnWeapons;
	private Button _btnShields;
	private Button _btnRepair;
	private Button _btnScan;     
	private Button _btnSalvage;  
	private Button _closeMenuButton;
	private MapEntity _currentlyViewedShip = null;

	private Vector2I[] _hexDirections = new Vector2I[] {
		new Vector2I(1, 0), new Vector2I(1, -1), new Vector2I(0, -1), 
		new Vector2I(-1, 0), new Vector2I(-1, 1), new Vector2I(0, 1)
	};

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (_globalData != null && _globalData.CurrentTurn > 0) _currentTurn = _globalData.CurrentTurn;
		
		AddChild(_bgLayer);
		AddChild(_gridLayer);
		AddChild(_highlightLayer);
		AddChild(_entityLayer);

		_selectionBox = new SelectionBox();
		AddChild(_selectionBox);

		SetupCamera();
		SetupSpaceBackground();
		SetupAudio(); 
		SetupUI(); 
		GenerateGrid();
		PopulateMapFromMemory();
		
		if (_globalData != null && _globalData.InCombat) RestoreCombatState();
		else CheckForCombatTrigger();

		_endTurnButton.Visible = _inCombat; 
		_repairFleetButton.Visible = !_inCombat; 
		_inventoryButton.Visible = !_inCombat;
	}

	private void SetupAudio() 
	{ 
		_bgmPlayer = new AudioStreamPlayer();
		AddChild(_bgmPlayer);
		AudioStream bgmStream = GD.Load<AudioStream>("res://battle_theme.mp3"); 
		if (bgmStream != null) { _bgmPlayer.Stream = bgmStream; _bgmPlayer.VolumeDb = -15.0f; _bgmPlayer.Play(); }

		_sfxPlayer = new AudioStreamPlayer();
		AddChild(_sfxPlayer);
		_sfxPlayer.VolumeDb = -5.0f; 

		_laserPlayer = new AudioStreamPlayer();
		AddChild(_laserPlayer);
		_laserPlayer.VolumeDb = -3.0f;
		AudioStream laserStream = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
		if (laserStream != null) _laserPlayer.Stream = laserStream;

		_explosionPlayer = new AudioStreamPlayer();
		AddChild(_explosionPlayer);
		_explosionPlayer.VolumeDb = 0.0f; 
		AudioStream boomStream = GD.Load<AudioStream>("res://Sounds/explosion.wav"); 
		if (boomStream != null) _explosionPlayer.Stream = boomStream;
	}

	private void SetupCamera() { _camera = new Camera2D(); AddChild(_camera); _camera.MakeCurrent(); }

	public override void _Process(double delta)
	{
		if (_isJumping) return;

		Vector2 panDirection = Vector2.Zero;
		if (Input.IsKeyPressed(Key.W)) panDirection.Y -= 1;
		if (Input.IsKeyPressed(Key.S)) panDirection.Y += 1;
		if (Input.IsKeyPressed(Key.A)) panDirection.X -= 1;
		if (Input.IsKeyPressed(Key.D)) panDirection.X += 1;
		if (panDirection != Vector2.Zero) _camera.Position += panDirection.Normalized() * _panSpeed * (float)delta * (1.0f / _camera.Zoom.X);
		
		bool isNearStargate = false;
		
		foreach (Vector2I hex in _selectedHexes)
		{
			if (_hexContents.ContainsKey(hex))
			{
				MapEntity selectedShip = _hexContents[hex];
				if (IsInstanceValid(selectedShip.VisualSprite))
				{
					Vector2 mousePos = GetGlobalMousePosition();
					float targetAngle = selectedShip.VisualSprite.GlobalPosition.AngleToPoint(mousePos) + selectedShip.BaseRotationOffset;
					selectedShip.VisualSprite.Rotation = Mathf.LerpAngle(selectedShip.VisualSprite.Rotation, targetAngle, 0.15f);
				}

				if (selectedShip.Type == "Player Fleet" && !_inCombat)
				{
					foreach(Vector2I dir in _hexDirections)
					{
						Vector2I neighbor = hex + dir;
						if (_hexContents.ContainsKey(neighbor) && _hexContents[neighbor].Type == "StarGate")
						{
							isNearStargate = true;
							break;
						}
					}
				}
			}
		}

		_jumpButton.Visible = isNearStargate;

		if (_selectedHexes.Count == 1 && _hexContents.ContainsKey(_selectedHexes[0]))
		{
			MapEntity singleShip = _hexContents[_selectedHexes[0]];
			if (singleShip.Type == "Player Fleet" && singleShip.CurrentActions > 0 && (!_inCombat || singleShip == _activeShip))
			{
				_attackButton.Visible = true;
			}
			else
			{
				_attackButton.Visible = false;
				_isTargeting = false;
				_attackButton.Text = "ATTACK";
			}
		}
		else
		{
			_attackButton.Visible = false;
			_isTargeting = false;
			_attackButton.Text = "ATTACK";
		}

		List<Vector2I> playerPositions = new List<Vector2I>();
		foreach (var kvp in _hexContents)
		{
			if (kvp.Value.Type == "Player Fleet") playerPositions.Add(kvp.Key);
		}

		foreach (var kvp in _hexContents)
		{
			if (kvp.Value.Type == "Enemy Fleet" && IsInstanceValid(kvp.Value.VisualSprite))
			{
				bool isVisible = false;
				foreach (Vector2I playerPos in playerPositions)
				{
					if (HexDistance(kvp.Key, playerPos) <= _scanningRange)
					{
						isVisible = true;
						break;
					}
				}
				kvp.Value.VisualSprite.Visible = isVisible;
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_isJumping) return;

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Space)
			{
				if (_inCombat && _activeShip != null && _activeShip.Type == "Player Fleet")
				{
					OnEndTurnPressed();
				}
			}
			else if (keyEvent.Keycode == Key.R)
			{
				OnRepairPressed();
			}
			else if (keyEvent.Keycode == Key.Q)
			{
				if (_inCombat && _activeShip != null && _activeShip.Type == "Player Fleet" && _activeShip.CurrentActions > 0)
				{
					Vector2I hoveredHex = PixelToHex(GetGlobalMousePosition());
					if (_hexContents.ContainsKey(hoveredHex) && _hexContents[hoveredHex].Type == "Enemy Fleet")
					{
						Vector2I activeHex = Vector2I.Zero;
						foreach(var kvp in _hexContents) if (kvp.Value == _activeShip) activeHex = kvp.Key;

						if (HexDistance(activeHex, hoveredHex) <= _activeShip.AttackRange)
						{
							PerformAttack(activeHex, hoveredHex);
							_isTargeting = false;
							_attackButton.Text = "ATTACK";
							CheckForCombatTrigger();
						}
						else
						{
							GD.Print("Target out of range!");
						}
					}
				}
			}
		}

		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.IsPressed())
			{
				if (mouseButton.ButtonIndex == MouseButton.WheelUp) _camera.Zoom += new Vector2(_zoomSpeed, _zoomSpeed);
				else if (mouseButton.ButtonIndex == MouseButton.WheelDown) _camera.Zoom -= new Vector2(_zoomSpeed, _zoomSpeed);
				_camera.Zoom = new Vector2(Mathf.Clamp(_camera.Zoom.X, _minZoom, _maxZoom), Mathf.Clamp(_camera.Zoom.Y, _minZoom, _maxZoom));
			}
			
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				if (mouseButton.IsPressed())
				{
					_isDragging = true;
					_dragStartPos = GetGlobalMousePosition();
					_selectionBox.StartPos = _dragStartPos;
					_selectionBox.EndPos = _dragStartPos;
					_selectionBox.IsDragging = true;
					_selectionBox.QueueRedraw();
				}
				else
				{
					_isDragging = false;
					_selectionBox.IsDragging = false;
					_selectionBox.QueueRedraw();

					Rect2 selectionRect = new Rect2(_dragStartPos, GetGlobalMousePosition() - _dragStartPos).Abs();
					
					if (!_isTargeting) _selectedHexes.Clear();

					if (selectionRect.Area < 100)
					{
						Vector2I clickedHex = PixelToHex(GetGlobalMousePosition());

						if (_isTargeting && _selectedHexes.Count == 1)
						{
							if (_hexContents.ContainsKey(clickedHex) && _hexContents[clickedHex].Type == "Enemy Fleet")
							{
								int dist = HexDistance(_selectedHexes[0], clickedHex);
								MapEntity attacker = _hexContents[_selectedHexes[0]];
								
								if (dist <= attacker.AttackRange)
								{
									PerformAttack(_selectedHexes[0], clickedHex);
									_isTargeting = false;
									_attackButton.Text = "ATTACK";
									CheckForCombatTrigger(); 
								}
								else GD.Print("Target out of range!");
							}
							else
							{
								_isTargeting = false;
								_attackButton.Text = "ATTACK";
							}
							return; 
						}

						if (_hexContents.ContainsKey(clickedHex) && (_hexContents[clickedHex].Type == "Player Fleet" || _hexContents[clickedHex].Type == "Enemy Fleet"))
						{
							if (_hexContents[clickedHex].Type == "Player Fleet")
							{
								if (!_inCombat || _hexContents[clickedHex] == _activeShip) _selectedHexes.Add(clickedHex);
							}
							if (_hexContents[clickedHex].VisualSprite.Visible) ToggleShipMenu(true, _hexContents[clickedHex]);
						}
						else ToggleShipMenu(false); 
					}
					else 
					{
						if (!_inCombat) 
						{
							foreach (var kvp in _hexContents)
							{
								if (kvp.Value.Type == "Player Fleet" && selectionRect.HasPoint(HexToPixel(kvp.Key)))
									_selectedHexes.Add(kvp.Key);
							}
						}
						ToggleShipMenu(false); 
					}
					UpdateHighlights();
				}
			}
			
			if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.IsPressed())
			{
				Vector2I clickedHex = PixelToHex(GetGlobalMousePosition());

				if (_inCombat)
				{
					if (_activeShip != null && _activeShip.Type == "Player Fleet" && _selectedHexes.Count > 0)
					{
						Vector2I activeHex = _selectedHexes[0];
						
						if (_hexContents.ContainsKey(clickedHex) && _hexContents[clickedHex].Type == "Enemy Fleet")
						{
							if (_activeShip.CurrentActions > 0 && HexDistance(activeHex, clickedHex) <= _activeShip.AttackRange)
							{
								PerformAttack(activeHex, clickedHex);
								_isTargeting = false;
								_attackButton.Text = "ATTACK";
								CheckForCombatTrigger();
							}
							else GD.Print("Target out of range or action spent!");
						}
						else
						{
							Dictionary<Vector2I, int> reachable = GetReachableHexes(activeHex, _activeShip.CurrentActions);
							if (reachable.ContainsKey(clickedHex) && clickedHex != activeHex)
							{
								MoveShip(activeHex, clickedHex, reachable[clickedHex]);
								_selectedHexes.Clear();
								_selectedHexes.Add(clickedHex);
								UpdateHighlights();
								CheckForCombatTrigger();
							}
						}
					}
				}
				else 
				{
					if (_selectedHexes.Count > 0) MoveGroup(_selectedHexes, clickedHex);
				}
			}
		}

		if (@event is InputEventMouseMotion)
		{
			if (_isDragging)
			{
				_selectionBox.EndPos = GetGlobalMousePosition();
				_selectionBox.QueueRedraw();
			}

			Vector2I hoveredHex = PixelToHex(GetGlobalMousePosition());
			if (_hexContents.ContainsKey(hoveredHex))
			{
				MapEntity entity = _hexContents[hoveredHex];
				if (entity.Type == "Enemy Fleet" && !entity.VisualSprite.Visible)
				{
					_infoPanel.Visible = false;
					return;
				}

				string dynamicStats = "";
				if (entity.Type == "Player Fleet" || entity.Type == "Enemy Fleet")
				{
					string initText = _inCombat ? $" | INIT: {entity.CurrentInitiativeRoll}" : "";
					dynamicStats = $"HP: {entity.CurrentHP}/{entity.MaxHP} | SHIELD: {entity.CurrentShields}/{entity.MaxShields}\n" +
								   $"ACTIONS: {entity.CurrentActions}/{entity.MaxActions}{initText}\n" +
								   $"RANGE: {entity.AttackRange} | DMG: 0-{entity.AttackDamage}\n";
				}

				_infoLabel.Text = $"[ {entity.Name.ToUpper()} ]\nType: {entity.Type}\n{dynamicStats}Data: {entity.Details}";
				_infoPanel.Visible = true;
			}
			else _infoPanel.Visible = false; 
		}
	}

	private MapEntity GetAdjacentPlanet(MapEntity ship)
	{
		Vector2I shipHex = Vector2I.Zero;
		foreach(var kvp in _hexContents) if (kvp.Value == ship) shipHex = kvp.Key;

		foreach(Vector2I dir in _hexDirections)
		{
			Vector2I n = shipHex + dir;
			if (_hexContents.ContainsKey(n) && _hexContents[n].Type == "Planet")
			{
				return _hexContents[n];
			}
		}
		return null;
	}

	private PlanetData GetPlanetData(string planetName)
	{
		if (_globalData == null || string.IsNullOrEmpty(_globalData.SavedSystem)) return null;
		if (!_globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem)) return null;
		
		SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
		foreach (PlanetData p in currentSystem.Planets)
		{
			if (p.Name == planetName) return p;
		}
		return null;
	}

	private void ToggleShipMenu(bool expand, MapEntity ship = null)
	{
		Tween tween = CreateTween();
		Vector2 screenSize = GetViewportRect().Size;
		
		float targetX = expand ? screenSize.X - 320 : screenSize.X + 50; 
		tween.TweenProperty(_shipMenuPanel, "position:x", targetX, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		
		if (expand && ship != null)
		{
			_currentlyViewedShip = ship;
			_shipMenuTitle.Text = $"== {ship.Name.ToUpper()} ==";
			string status = ship.Type == "Enemy Fleet" ? "[HOSTILE]" : "[ALLIED]";
			
			Texture2D tex = GD.Load<Texture2D>(GetShipTexturePath(ship.Name));
			if (tex != null) _shipImageDisplay.Texture = tex;
			float hpPercent = (float)ship.CurrentHP / ship.MaxHP;
			_shipImageDisplay.Modulate = new Color(1f, hpPercent, hpPercent); 

			_hpBar.MaxValue = ship.MaxHP;
			_hpBar.Value = ship.CurrentHP;
			_hpLabel.Text = $"HULL INTEGRITY: {ship.CurrentHP}/{ship.MaxHP}";

			_shieldBar.MaxValue = ship.MaxShields;
			_shieldBar.Value = ship.CurrentShields;
			_shieldLabel.Text = $"SHIELD CAPACITORS: {ship.CurrentShields}/{ship.MaxShields}";

			_shipMenuDetails.Text = 
				$"Classification: {ship.Type}\n" +
				$"Status: {status}\n" +
				$"Action Points: {ship.CurrentActions}/{ship.MaxActions}\n" +
				$"Weapon Payload: 0-{ship.AttackDamage} Dmg\n" + 
				$"Targeting Range: {ship.AttackRange} Hexes\n";
				
			bool isPlayer = ship.Type == "Player Fleet";
			_btnWeapons.Visible = isPlayer;
			_btnShields.Visible = isPlayer;
			_btnRepair.Visible = isPlayer;
			_btnRepair.Disabled = ship.CurrentActions < 2;

			_btnScan.Visible = false;
			_btnSalvage.Visible = false;

			if (isPlayer && !_inCombat)
			{
				MapEntity adjPlanet = GetAdjacentPlanet(ship);
				if (adjPlanet != null)
				{
					PlanetData pData = GetPlanetData(adjPlanet.Name);
					if (pData != null)
					{
						if (ship.Name == "The Aether Skimmer" || ship.Name == "The Relic Harvester")
						{
							_btnScan.Visible = true;
							_btnScan.Disabled = pData.HasBeenScanned || ship.CurrentActions < 1;
							_btnScan.Text = pData.HasBeenScanned ? "SCANNED" : "SCAN";
						}
						
						if (ship.Name == "The Relic Harvester" || ship.Name == "The Neptune Forge")
						{
							_btnSalvage.Visible = true;
							_btnSalvage.Disabled = pData.HasBeenSalvaged || ship.CurrentActions < 1;
							_btnSalvage.Text = pData.HasBeenSalvaged ? "SALVAGED" : "SALVAGE";
						}
					}
				}
			}
		}
	}

	private void OnScanPressed()
	{
		if (_currentlyViewedShip == null || _currentlyViewedShip.CurrentActions < 1) return;
		MapEntity planet = GetAdjacentPlanet(_currentlyViewedShip);
		if (planet == null) return;

		PlanetData pData = GetPlanetData(planet.Name);
		if (pData != null && pData.HasBeenScanned) return; 

		_currentlyViewedShip.CurrentActions -= 1;
		if (pData != null) pData.HasBeenScanned = true;

		float scale = planet.VisualSprite.Scale.X;
		string sizeClass = scale > 0.6f ? "Massive" : (scale > 0.5f ? "Standard" : "Dwarf");
		
		_combatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=#00ffff]--- SENSOR SWEEP COMPLETED ---[/color]");
		LogCombatMessage($"Target: {planet.Name}");
		LogCombatMessage($"Size Class: {sizeClass}");
		LogCombatMessage($"Projected Salvage Operation: {Mathf.Max(1, Mathf.RoundToInt(scale * 5f))} Turns");
		LogCombatMessage($"Caution: extended operations carry risk of hostile detection.");

		ToggleShipMenu(true, _currentlyViewedShip); 
	}

	private void OnSalvagePressed()
	{
		if (_currentlyViewedShip == null || _currentlyViewedShip.CurrentActions < 1) return;
		MapEntity planet = GetAdjacentPlanet(_currentlyViewedShip);
		if (planet == null) return;

		PlanetData pData = GetPlanetData(planet.Name);
		if (pData != null && pData.HasBeenSalvaged) return; 

		float scale = planet.VisualSprite.Scale.X;
		int turnsNeeded = Mathf.Max(1, Mathf.RoundToInt(scale * 5f)); 

		_currentlyViewedShip.CurrentActions = 0; 

		_combatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=yellow]--- SALVAGE OPERATION COMMENCED ({turnsNeeded} Turns) ---[/color]");

		bool ambushed = false;

		for (int i = 0; i < turnsNeeded; i++)
		{
			AdvanceExplorationTurn();

			List<Vector2I> players = new List<Vector2I>();
			List<Vector2I> enemies = new List<Vector2I>();
			foreach (var kvp in _hexContents)
			{
				if (kvp.Value.Type == "Player Fleet") players.Add(kvp.Key);
				if (kvp.Value.Type == "Enemy Fleet") enemies.Add(kvp.Key);
			}

			foreach (Vector2I p in players)
			{
				foreach (Vector2I e in enemies)
				{
					if (HexDistance(p, e) <= _scanningRange)
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
				CheckForCombatTrigger();
				break;
			}
		}

		if (!ambushed)
		{
			if (pData != null) pData.HasBeenSalvaged = true; 

			Random rng = new Random();
			int yieldAmount = turnsNeeded * rng.Next(25, 60);
			
			string[] resourceTypes = { "Raw Materials", "Energy Cores", "Ancient Tech" };
			string foundType = resourceTypes[rng.Next(resourceTypes.Length)];

			if (_globalData != null)
			{
				int currentAmount = (int)_globalData.FleetResources[foundType];
				_globalData.FleetResources[foundType] = currentAmount + yieldAmount;
			}

			LogCombatMessage($"[color=green]Operation Successful. Acquired {yieldAmount} {foundType}.[/color]");
		}

		ToggleShipMenu(true, _currentlyViewedShip); 
	}

	private void OnInventoryPressed()
	{
		_combatLogPanel.Visible = true;
		LogCombatMessage("\n[color=yellow]--- FLEET INVENTORY ---[/color]");
		if (_globalData != null)
		{
			foreach(var kvp in _globalData.FleetResources)
			{
				LogCombatMessage($"- {kvp.Key}: {kvp.Value}");
			}
		}
		LogCombatMessage("*(Note: Upgrade module linking requires adjacent fleet positioning.)*");
	}

	private void OnRepairPressed()
	{
		if (_currentlyViewedShip == null || _currentlyViewedShip.IsDead || _currentlyViewedShip.Type != "Player Fleet") return;
		
		if (_currentlyViewedShip.CurrentActions < 2) 
		{
			GD.Print("Not enough AP to repair!");
			return;
		}

		_currentlyViewedShip.CurrentActions -= 2;
		
		Random rng = new Random();
		int healAmount = rng.Next(15, 30); 
		bool healShield = rng.Next(0, 2) == 0; 

		if (healShield && _currentlyViewedShip.CurrentShields == _currentlyViewedShip.MaxShields) healShield = false;
		if (!healShield && _currentlyViewedShip.CurrentHP == _currentlyViewedShip.MaxHP) healShield = true;

		if (healShield)
		{
			_currentlyViewedShip.CurrentShields = Mathf.Min(_currentlyViewedShip.CurrentShields + healAmount, _currentlyViewedShip.MaxShields);
			LogCombatMessage($"[color=#00ffff]{_currentlyViewedShip.Name} routed emergency power to Shields (+{healAmount})![/color]");
		}
		else
		{
			_currentlyViewedShip.CurrentHP = Mathf.Min(_currentlyViewedShip.CurrentHP + healAmount, _currentlyViewedShip.MaxHP);
			LogCombatMessage($"[color=#44ff44]{_currentlyViewedShip.Name} deployed drones to patch Hull Breeches (+{healAmount})![/color]");
		}

		if (_sfxPlayer != null)
		{
			AudioStream repairSound = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (repairSound != null) { _sfxPlayer.Stream = repairSound; _sfxPlayer.PitchScale = 1.5f; _sfxPlayer.Play(); }
		}

		ToggleShipMenu(true, _currentlyViewedShip);
	}

	private void OnRepairFleetPressed()
	{
		if (_inCombat) return;

		int totalMissing = 0;
		foreach (var kvp in _hexContents)
		{
			if (kvp.Value.Type == "Player Fleet")
			{
				totalMissing += (kvp.Value.MaxHP - kvp.Value.CurrentHP) + (kvp.Value.MaxShields - kvp.Value.CurrentShields);
			}
		}

		if (totalMissing == 0)
		{
			GD.Print("Fleet is already at full strength!");
			return;
		}

		int turnsNeeded = (totalMissing / 20) + 1; 
		
		_combatLogPanel.Visible = true;
		LogCombatMessage($"\n[color=cyan]--- REPAIRING FLEET (Estimated {turnsNeeded} Turns) ---[/color]");

		bool ambushed = false;

		for (int i = 0; i < turnsNeeded; i++)
		{
			// Instead of manual duplication, call the central Turn Advance logic
			AdvanceExplorationTurn();

			foreach (var kvp in _hexContents)
			{
				if (kvp.Value.Type == "Player Fleet")
				{
					kvp.Value.CurrentHP = Mathf.Min(kvp.Value.CurrentHP + 15, kvp.Value.MaxHP);
					kvp.Value.CurrentShields = Mathf.Min(kvp.Value.CurrentShields + 10, kvp.Value.MaxShields);
				}
			}

			List<Vector2I> players = new List<Vector2I>();
			List<Vector2I> enemies = new List<Vector2I>();
			foreach (var kvp in _hexContents)
			{
				if (kvp.Value.Type == "Player Fleet") players.Add(kvp.Key);
				if (kvp.Value.Type == "Enemy Fleet") enemies.Add(kvp.Key);
			}

			foreach (Vector2I p in players)
			{
				foreach (Vector2I e in enemies)
				{
					if (HexDistance(p, e) <= _scanningRange)
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
				CheckForCombatTrigger();
				break;
			}
		}

		if (!ambushed)
		{
			foreach (var kvp in _hexContents)
			{
				if (kvp.Value.Type == "Player Fleet")
				{
					kvp.Value.CurrentHP = kvp.Value.MaxHP;
					kvp.Value.CurrentShields = kvp.Value.MaxShields;
				}
			}
			LogCombatMessage($"[color=green]Fleet repairs complete.[/color]");
		}
	}

	// --- NEW: Centralized Exploration Turn Logic ---
	private void AdvanceExplorationTurn()
	{
		if (_inCombat) return;

		_currentTurn++;
		foreach (var kvp in _hexContents)
		{
			if (kvp.Value.Type == "Player Fleet" || kvp.Value.Type == "Enemy Fleet")
			{
				kvp.Value.CurrentActions = kvp.Value.MaxActions; 
			}
		}
		
		_endTurnButton.Text = $"TURN {_currentTurn}";

		ProcessEnemyExplorationTurns();

		if (_selectedHexes.Count == 1 && _hexContents.ContainsKey(_selectedHexes[0]))
		{
			ToggleShipMenu(true, _hexContents[_selectedHexes[0]]);
		}
		else
		{
			ToggleShipMenu(false);
		}
	}

	private void MoveEnemiesInstantly()
	{
		List<Vector2I> playerPositions = new List<Vector2I>();
		foreach (var kvp in _hexContents) if (kvp.Value.Type == "Player Fleet") playerPositions.Add(kvp.Key);
		if (playerPositions.Count == 0) return;

		List<KeyValuePair<Vector2I, MapEntity>> enemies = _hexContents.Where(kvp => kvp.Value.Type == "Enemy Fleet").ToList();
		
		foreach (var kvp in enemies)
		{
			Vector2I currentPos = kvp.Key;
			MapEntity enemyShip = kvp.Value;

			Vector2I targetPlayer = playerPositions[0];
			int minDistance = HexDistance(currentPos, targetPlayer);
			foreach (Vector2I playerHex in playerPositions)
			{
				int dist = HexDistance(currentPos, playerHex);
				if (dist < minDistance) { minDistance = dist; targetPlayer = playerHex; }
			}

			Vector2I bestNeighbor = currentPos;
			int bestDist = minDistance;

			foreach (Vector2I dir in _hexDirections)
			{
				Vector2I neighbor = currentPos + dir;
				if (!_hexGrid.ContainsKey(neighbor)) continue;
				if (IsHexEmpty(neighbor))
				{
					int distToTarget = HexDistance(neighbor, targetPlayer);
					if (distToTarget < bestDist)
					{
						bestDist = distToTarget; bestNeighbor = neighbor;
					}
				}
			}

			if (bestNeighbor != currentPos)
			{
				_hexContents.Remove(currentPos);
				_hexContents[bestNeighbor] = enemyShip;
				enemyShip.VisualSprite.Position = HexToPixel(bestNeighbor);
			}
		}
	}

	private void MoveGroup(List<Vector2I> shipsToMove, Vector2I targetHex)
	{
		if (shipsToMove.Count == 0) return;

		Vector2I anchorHex = shipsToMove[0];
		List<Vector2I> newSelection = new List<Vector2I>();

		shipsToMove.Sort((a, b) => HexDistance(a, targetHex).CompareTo(HexDistance(b, targetHex)));

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
		
		_selectedHexes = newSelection;
		UpdateHighlights();

		// --- UPDATED: Movement now triggers the turn to advance automatically ---
		AdvanceExplorationTurn();
	}

	private Vector2I FindNearestEmptyHex(Vector2I target)
	{
		if (IsHexEmpty(target)) return target;
		
		int radius = 1;
		while (radius < 15) 
		{
			Vector2I current = target + _hexDirections[4] * radius;
			for (int i = 0; i < 6; i++)
			{
				for (int j = 0; j < radius; j++)
				{
					if (IsHexEmpty(current)) return current;
					current += _hexDirections[i];
				}
			}
			radius++;
		}
		return target; 
	}

	private bool IsHexEmpty(Vector2I hex)
	{
		if (!_hexGrid.ContainsKey(hex)) return false; 
		if (_hexContents.ContainsKey(hex))
		{
			string type = _hexContents[hex].Type;
			if (type == "Planet" || type == "Base Planet (Player Start)" || type == "Celestial Body" || type == "Player Fleet" || type == "Enemy Fleet" || type == "StarGate")
				return false; 
		}
		return true;
	}

	private void UpdateHighlights()
	{
		ClearHighlights(); 

		foreach (Vector2I hex in _selectedHexes) CreateHighlightPolygon(hex, new Color(1f, 0.8f, 0f, 0.6f)); 

		if (_inCombat && _selectedHexes.Count == 1 && _activeShip != null && _hexContents.ContainsKey(_selectedHexes[0]) && _hexContents[_selectedHexes[0]] == _activeShip)
		{
			Dictionary<Vector2I, int> reachable = GetReachableHexes(_selectedHexes[0], _activeShip.CurrentActions);
			foreach (Vector2I hex in reachable.Keys)
			{
				if (hex == _selectedHexes[0]) continue; 
				CreateHighlightPolygon(hex, new Color(0f, 1f, 0.3f, 0.4f)); 
			}
		}
	}

	private void LogCombatMessage(string message) { _combatLogText.Text += message + "\n"; }

	private void CheckForCombatTrigger()
	{
		if (_inCombat) return;

		List<Vector2I> players = new List<Vector2I>();
		List<Vector2I> enemies = new List<Vector2I>();

		foreach (var kvp in _hexContents)
		{
			if (kvp.Value.Type == "Player Fleet") players.Add(kvp.Key);
			if (kvp.Value.Type == "Enemy Fleet") enemies.Add(kvp.Key);
		}

		if (enemies.Count == 0 || players.Count == 0) return;

		foreach (Vector2I p in players)
		{
			foreach (Vector2I e in enemies)
			{
				if (HexDistance(p, e) <= _scanningRange)
				{
					StartCombat();
					return;
				}
			}
		}
	}

	private void StartCombat()
	{
		_inCombat = true;
		_endTurnButton.Visible = true; 
		_repairFleetButton.Visible = false; 
		_inventoryButton.Visible = false;
		
		_combatLogPanel.Visible = true;
		_combatLogText.Text = "[color=yellow]--- COMBAT INITIATED ---[/color]\n";

		_initiativeQueue.Clear();
		_selectedHexes.Clear();

		List<Vector2I> playerHexes = new List<Vector2I>();
		foreach (var kvp in _hexContents)
		{
			if (kvp.Value.Type == "Player Fleet") playerHexes.Add(kvp.Key);
		}

		int engagementRange = _scanningRange * 2; 

		Random rng = new Random();
		foreach (var kvp in _hexContents)
		{
			bool joinsCombat = false;

			if (kvp.Value.Type == "Player Fleet")
			{
				joinsCombat = true; 
			}
			else if (kvp.Value.Type == "Enemy Fleet")
			{
				foreach (Vector2I pHex in playerHexes)
				{
					if (HexDistance(kvp.Key, pHex) <= engagementRange)
					{
						joinsCombat = true;
						break;
					}
				}
			}

			if (joinsCombat)
			{
				kvp.Value.CurrentInitiativeRoll = rng.Next(1, 21) + kvp.Value.InitiativeBonus;
				kvp.Value.CurrentActions = kvp.Value.MaxActions; 
				
				if (IsInstanceValid(kvp.Value.VisualSprite)) kvp.Value.VisualSprite.Visible = true;
				_initiativeQueue.Add(kvp.Value);
			}
		}

		_initiativeQueue.Sort((a, b) => {
			int cmp = b.CurrentInitiativeRoll.CompareTo(a.CurrentInitiativeRoll);
			if (cmp == 0) return a.Name.CompareTo(b.Name); 
			return cmp;
		});

		_currentQueueIndex = 0;
		_currentTurn = 1;
		_endTurnButton.Text = "END TURN";

		UpdateInitiativeUI();
		StartActiveTurn();
	}

	private void RestoreCombatState()
	{
		_inCombat = true;
		_endTurnButton.Visible = true; 
		_repairFleetButton.Visible = false; 
		_inventoryButton.Visible = false;
		
		_combatLogPanel.Visible = true;
		_combatLogText.Text = "[color=yellow]--- COMBAT RESUMED ---[/color]\n";

		_currentQueueIndex = _globalData.CurrentQueueIndex;
		_initiativeQueue.Clear();
		_selectedHexes.Clear();

		List<Vector2I> playerHexes = new List<Vector2I>();
		foreach (var kvp in _hexContents)
		{
			if (kvp.Value.Type == "Player Fleet") playerHexes.Add(kvp.Key);
		}

		int engagementRange = _scanningRange * 2; 

		foreach (var kvp in _hexContents)
		{
			bool joinsCombat = false;

			if (kvp.Value.Type == "Player Fleet")
			{
				joinsCombat = true; 
			}
			else if (kvp.Value.Type == "Enemy Fleet")
			{
				foreach (Vector2I pHex in playerHexes)
				{
					if (HexDistance(kvp.Key, pHex) <= engagementRange)
					{
						joinsCombat = true;
						break;
					}
				}
			}

			if (joinsCombat)
			{
				if (IsInstanceValid(kvp.Value.VisualSprite)) kvp.Value.VisualSprite.Visible = true;
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
		_endTurnButton.Text = "END TURN";

		if (_initiativeQueue.Count > 0)
		{
			_activeShip = _initiativeQueue[_currentQueueIndex];
			Tween camTween = CreateTween();
			camTween.TweenProperty(_camera, "position", _activeShip.VisualSprite.Position, 0.5f).SetTrans(Tween.TransitionType.Sine);

			if (_activeShip.Type == "Enemy Fleet") GetTree().CreateTimer(1.0f).Timeout += () => ExecuteSingleEnemyAI(_activeShip);
			else
			{
				foreach (var kvp in _hexContents)
				{
					if (kvp.Value == _activeShip)
					{
						_selectedHexes.Add(kvp.Key);
						UpdateHighlights();
						break;
					}
				}
			}
		}
	}

	private void UpdateInitiativeUI()
	{
		foreach (Node child in _initiativeUI.GetChildren()) child.QueueFree();

		if (!_inCombat)
		{
			_initiativeTurnLabel.Text = "EXPLORATION MODE";
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
				_initiativeTurnLabel.Text = $"CURRENT TURN: {ship.Name.ToUpper()}"; 
			}

			TextureRect icon = new TextureRect();
			Texture2D tex = GD.Load<Texture2D>(GetShipTexturePath(ship.Name));
			icon.Texture = tex;
			icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			icon.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

			if (i == _currentQueueIndex) icon.Modulate = new Color(0.8f, 1f, 0.8f); 
			else if (ship.Type == "Enemy Fleet") icon.Modulate = new Color(1f, 0.5f, 0.5f); 
			else icon.Modulate = new Color(1f, 1f, 1f, 0.6f); 
			
			squarePanel.AddChild(icon);
			_initiativeUI.AddChild(squarePanel);
		}
	}

	private void StartActiveTurn()
	{
		_initiativeQueue.RemoveAll(s => s.IsDead);
		_selectedHexes.Clear();

		if (_initiativeQueue.Count == 0 || !AreBothSidesAlive())
		{
			EndCombat();
			return;
		}

		if (_currentQueueIndex >= _initiativeQueue.Count)
		{
			_currentQueueIndex = 0;
			_currentTurn++;
			LogCombatMessage($"\n[color=gray]--- ROUND {_currentTurn} ---[/color]");
		}

		_activeShip = _initiativeQueue[_currentQueueIndex];
		_activeShip.CurrentActions = _activeShip.MaxActions;

		UpdateInitiativeUI();
		
		Tween camTween = CreateTween();
		camTween.TweenProperty(_camera, "position", _activeShip.VisualSprite.Position, 0.5f).SetTrans(Tween.TransitionType.Sine);

		if (_activeShip.Type == "Enemy Fleet")
		{
			GetTree().CreateTimer(1.0f).Timeout += () => ExecuteSingleEnemyAI(_activeShip);
		}
		else
		{
			foreach (var kvp in _hexContents)
			{
				if (kvp.Value == _activeShip)
				{
					_selectedHexes.Add(kvp.Key);
					ToggleShipMenu(true, _activeShip); 
					UpdateHighlights();
					break;
				}
			}
		}
	}

	private void EndActiveTurn()
	{
		if (!_inCombat) return;
		_selectedHexes.Clear();
		ToggleShipMenu(false); 
		UpdateHighlights();
		_currentQueueIndex++;
		StartActiveTurn();
	}

	private bool AreBothSidesAlive()
	{
		bool playerAlive = false;
		bool enemyAlive = false;
		foreach (var ship in _initiativeQueue)
		{
			if (ship.Type == "Player Fleet") playerAlive = true;
			if (ship.Type == "Enemy Fleet") enemyAlive = true;
		}
		return playerAlive && enemyAlive;
	}

	private void EndCombat()
	{
		_inCombat = false;
		_activeShip = null;
		_combatLogPanel.Visible = false; 
		_endTurnButton.Visible = false; 
		_repairFleetButton.Visible = true; 
		_inventoryButton.Visible = true;
		
		bool playerAlive = false;
		foreach (var ship in _hexContents.Values)
		{
			if (ship.Type == "Player Fleet") playerAlive = true;
		}

		if (!playerAlive)
		{
			_gameOverPanel.Visible = true;
			_initiativeTurnLabel.Text = "FLEET DESTROYED";
		}
		else
		{
			_initiativeTurnLabel.Text = "EXPLORATION MODE";
		}
		
		foreach (Node child in _initiativeUI.GetChildren()) child.QueueFree();
	}

	private void OnAttackPressed()
	{
		_isTargeting = !_isTargeting;
		if (_isTargeting) _attackButton.Text = "CANCEL TARGET";
		else _attackButton.Text = "ATTACK";
	}

	private void PerformAttack(Vector2I attackerHex, Vector2I defenderHex)
	{
		MapEntity attacker = _hexContents[attackerHex];
		MapEntity defender = _hexContents[defenderHex];

		attacker.CurrentActions--; 
		
		DrawLaserBeam(HexToPixel(attackerHex), HexToPixel(defenderHex), attacker.Type);
		if (_laserPlayer.Stream != null) _laserPlayer.Play();

		Random rng = new Random();
		int damageRolled = rng.Next(0, attacker.AttackDamage + 1);
		string attackerColor = attacker.Type == "Player Fleet" ? "#44ff44" : "#ff4444"; 

		if (damageRolled == 0)
		{
			string missTxt = _missTexts[rng.Next(_missTexts.Length)];
			LogCombatMessage($"[color={attackerColor}]{attacker.Name}[/color] fired at {defender.Name}... {missTxt} [color=gray](0 DMG)[/color]");
			
			if (_selectedHexes.Count == 1 && _selectedHexes[0] == attackerHex) ToggleShipMenu(true, attacker);
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

		string hitPart = _shipParts[rng.Next(_shipParts.Length)];
		string logMsg = $"[color={attackerColor}]{attacker.Name}[/color] fires on {defender.Name}!\n";
		logMsg += $"-> Hit to the {hitPart} for [color=yellow]{damageRolled} DMG[/color]!";
		
		if (shieldDmg > 0) logMsg += $" ([color=#00ffff]Shields -{shieldDmg}[/color])";
		if (hullDmg > 0) logMsg += $" ([color=#ff4444]Hull -{hullDmg}[/color])";
		LogCombatMessage(logMsg);

		if (_selectedHexes.Count == 1 && _selectedHexes[0] == attackerHex) ToggleShipMenu(true, attacker);

		if (defender.CurrentHP <= 0)
		{
			LogCombatMessage($"[color=red]*** {defender.Name.ToUpper()} DESTROYED ***[/color]\n");
			
			if (_explosionPlayer.Stream != null) _explosionPlayer.Play();
			DrawExplosion(HexToPixel(defenderHex));

			defender.IsDead = true;
			defender.VisualSprite.QueueFree();
			_hexContents.Remove(defenderHex);
			
			if (_inCombat) UpdateInitiativeUI();
			if (_inCombat && !AreBothSidesAlive()) EndCombat(); 
		}
	}

	private void DrawLaserBeam(Vector2 startPos, Vector2 endPos, string attackerType)
	{
		Line2D laser = new Line2D();
		laser.AddPoint(startPos);
		laser.AddPoint(endPos);
		laser.Width = 4.0f;
		
		if (attackerType == "Player Fleet") laser.DefaultColor = new Color(0.2f, 1f, 0.2f, 1f); 
		else laser.DefaultColor = new Color(1f, 0.2f, 0.2f, 1f); 
		
		_entityLayer.AddChild(laser);

		Tween tween = CreateTween();
		tween.TweenProperty(laser, "modulate", new Color(1, 1, 1, 0), 0.4f);
		tween.TweenCallback(Callable.From(laser.QueueFree)); 
	}

	private void DrawExplosion(Vector2 pos)
	{
		Polygon2D shockwave = new Polygon2D();
		Vector2[] points = new Vector2[32];
		for (int i = 0; i < 32; i++)
		{
			float angle = (i / 32f) * Mathf.Pi * 2f;
			points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * HexSize * 0.5f;
		}
		shockwave.Polygon = points;
		shockwave.Color = new Color(1f, 1f, 1f, 0.8f); 
		shockwave.Position = pos;
		_entityLayer.AddChild(shockwave);

		Tween flashTween = CreateTween();
		flashTween.TweenProperty(shockwave, "scale", new Vector2(3.0f, 3.0f), 0.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		flashTween.Parallel().TweenProperty(shockwave, "color", new Color(1f, 0.4f, 0f, 0f), 0.8f); 
		flashTween.TweenCallback(Callable.From(shockwave.QueueFree)); 

		CpuParticles2D particles = new CpuParticles2D();
		particles.Position = pos;
		particles.Emitting = false;
		particles.OneShot = true;
		particles.Explosiveness = 0.6f; 
		particles.Lifetime = 2.5f; 
		particles.Amount = 60; 
		particles.Spread = 180f;
		particles.Gravity = Vector2.Zero; 
		particles.InitialVelocityMin = 20f; 
		particles.InitialVelocityMax = 80f; 
		particles.ScaleAmountMin = 8f;
		particles.ScaleAmountMax = 24f;
		
		Gradient grad = new Gradient();
		grad.Offsets = new float[] { 0.0f, 0.1f, 0.3f, 0.6f, 1.0f };
		grad.Colors = new Color[] {
			new Color(1f, 1f, 1f, 1f),       
			new Color(1f, 0.9f, 0.2f, 1f),   
			new Color(1f, 0.4f, 0f, 1f),     
			new Color(0.2f, 0.2f, 0.2f, 1f), 
			new Color(0.2f, 0.2f, 0.2f, 0f)  
		};
		particles.ColorRamp = grad;

		Curve sizeCurve = new Curve();
		sizeCurve.AddPoint(new Vector2(0f, 1f));
		sizeCurve.AddPoint(new Vector2(1f, 0f));
		particles.ScaleAmountCurve = sizeCurve;

		_entityLayer.AddChild(particles);
		particles.Emitting = true; 

		GetTree().CreateTimer(3.0f).Timeout += () => 
		{
			if (IsInstanceValid(particles)) particles.QueueFree();
		};
	}

	private void ExecuteSingleEnemyAI(MapEntity enemyShip)
	{
		if (enemyShip.IsDead || !_inCombat) return;

		Vector2I currentPos = new Vector2I(0,0);
		bool found = false;
		foreach(var kvp in _hexContents) { if (kvp.Value == enemyShip) { currentPos = kvp.Key; found = true; break; } }
		if (!found || enemyShip.CurrentActions <= 0) { EndActiveTurn(); return; }

		List<Vector2I> playerPositions = new List<Vector2I>();
		foreach (var kvp in _hexContents) if (kvp.Value.Type == "Player Fleet") playerPositions.Add(kvp.Key);
		
		if (playerPositions.Count == 0) { EndActiveTurn(); return; }

		Vector2I targetPlayer = playerPositions[0];
		int minDistance = HexDistance(currentPos, targetPlayer);

		foreach (Vector2I playerHex in playerPositions)
		{
			int dist = HexDistance(currentPos, playerHex);
			if (dist < minDistance) { minDistance = dist; targetPlayer = playerHex; }
		}

		int stepsTaken = 0;
		while (stepsTaken < enemyShip.CurrentActions && HexDistance(currentPos, targetPlayer) > enemyShip.AttackRange)
		{
			Vector2I bestNeighbor = currentPos;
			int bestDist = HexDistance(currentPos, targetPlayer);
			bool foundMove = false;

			foreach (Vector2I dir in _hexDirections)
			{
				Vector2I neighbor = currentPos + dir;
				if (!_hexGrid.ContainsKey(neighbor)) continue;
				
				bool isBlocked = false;
				if (_hexContents.ContainsKey(neighbor))
				{
					string type = _hexContents[neighbor].Type;
					if (type == "Planet" || type == "Base Planet (Player Start)" || type == "Celestial Body" || type == "Player Fleet" || type == "Enemy Fleet" || type == "StarGate") isBlocked = true; 
				}

				if (!isBlocked)
				{
					int distToTarget = HexDistance(neighbor, targetPlayer);
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
		foreach(var kvp in _hexContents) if (kvp.Value == enemyShip) oldPos = kvp.Key;

		if (currentPos != oldPos)
		{
			_hexContents.Remove(oldPos);
			_hexContents[currentPos] = enemyShip;
			enemyShip.CurrentActions -= stepsTaken;

			Tween tween = CreateTween();
			Vector2 targetPixelPos = HexToPixel(currentPos);
			float distance = enemyShip.VisualSprite.Position.DistanceTo(targetPixelPos);
			float duration = Mathf.Max(0.3f, distance / 500f); 

			string sfxPath = GetShipMovementSoundPath(enemyShip.Name);
			if (!string.IsNullOrEmpty(sfxPath))
			{
				AudioStream sfx = GD.Load<AudioStream>(sfxPath);
				if (sfx != null) { _sfxPlayer.Stream = sfx; _sfxPlayer.Play(); }
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
		if (enemyShip.IsDead || !_inCombat) return;

		if (!_hexContents.ContainsKey(targetPlayer) || _hexContents[targetPlayer].Type != "Player Fleet")
		{
			if (enemyShip.CurrentActions > 0) 
			{
				GetTree().CreateTimer(0.5f).Timeout += () => ExecuteSingleEnemyAI(enemyShip);
				return;
			}
		}
		else 
		{
			int finalDist = HexDistance(currentPos, targetPlayer);
			if (enemyShip.CurrentActions > 0 && finalDist <= enemyShip.AttackRange)
			{
				PerformAttack(currentPos, targetPlayer);
				
				if (enemyShip.CurrentActions > 0 && _inCombat)
				{
					GetTree().CreateTimer(0.6f).Timeout += () => EnemyActionLoop(enemyShip, currentPos, targetPlayer);
					return;
				}
			}
		}
		
		GetTree().CreateTimer(0.8f).Timeout += () => EndActiveTurn();
	}

	private void ProcessEnemyExplorationTurns()
	{
		List<Vector2I> playerPositions = new List<Vector2I>();
		foreach (var kvp in _hexContents)
			if (kvp.Value.Type == "Player Fleet") playerPositions.Add(kvp.Key);

		if (playerPositions.Count == 0) return;

		List<KeyValuePair<Vector2I, MapEntity>> enemies = _hexContents.Where(kvp => kvp.Value.Type == "Enemy Fleet").ToList();
		
		foreach (var kvp in enemies)
		{
			Vector2I currentPos = kvp.Key;
			MapEntity enemyShip = kvp.Value;

			Vector2I targetPlayer = playerPositions[0];
			int minDistance = HexDistance(currentPos, targetPlayer);
			foreach (Vector2I playerHex in playerPositions)
			{
				int dist = HexDistance(currentPos, playerHex);
				if (dist < minDistance) { minDistance = dist; targetPlayer = playerHex; }
			}

			Vector2I bestNeighbor = currentPos;
			int bestDist = minDistance;

			foreach (Vector2I dir in _hexDirections)
			{
				Vector2I neighbor = currentPos + dir;
				if (!_hexGrid.ContainsKey(neighbor)) continue;
				if (IsHexEmpty(neighbor))
				{
					int distToTarget = HexDistance(neighbor, targetPlayer);
					if (distToTarget < bestDist)
					{
						bestDist = distToTarget; bestNeighbor = neighbor;
					}
				}
			}

			if (bestNeighbor != currentPos)
			{
				MoveShip(currentPos, bestNeighbor, 0); 
			}
		}
		
		GetTree().CreateTimer(0.5f).Timeout += () => CheckForCombatTrigger();
	}

	private void OnEndTurnPressed()
	{
		if (!_inCombat)
		{
			AdvanceExplorationTurn();
			_selectedHexes.Clear();
			ToggleShipMenu(false);
			UpdateHighlights();
		}
		else
		{
			if (_activeShip != null && _activeShip.Type == "Player Fleet")
			{
				EndActiveTurn();
			}
		}
	}

	private void OnJumpPressed()
	{
		if (_isJumping) return;
		_isJumping = true;

		_jumpButton.Visible = false;
		_attackButton.Visible = false;
		_infoPanel.Visible = false;
		ToggleShipMenu(false);

		Vector2I gateHex = new Vector2I(0,0);
		bool gateFound = false;
		foreach(var kvp in _hexContents) {
			if (kvp.Value.Type == "StarGate") {
				foreach (var p_hex in _selectedHexes) {
					if (HexDistance(kvp.Key, p_hex) <= 1) {
						gateHex = kvp.Key;
						gateFound = true;
						break;
					}
				}
			}
			if (gateFound) break;
		}

		if (!gateFound) return;

		Vector2 gatePixelPos = HexToPixel(gateHex);

		Tween warpTween = CreateTween();
		warpTween.SetParallel(true);

		foreach(var p_hex in _selectedHexes)
		{
			if (_hexContents.ContainsKey(p_hex) && _hexContents[p_hex].Type == "Player Fleet")
			{
				Sprite2D shipSprite = _hexContents[p_hex].VisualSprite;
				if (IsInstanceValid(shipSprite))
				{
					warpTween.TweenProperty(shipSprite, "position", gatePixelPos, 1.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
					warpTween.TweenProperty(shipSprite, "scale", new Vector2(0.01f, 0.01f), 1.5f).SetTrans(Tween.TransitionType.Cubic);
					warpTween.TweenProperty(shipSprite, "rotation", shipSprite.Rotation + Mathf.Pi * 4, 1.5f);
					warpTween.TweenProperty(shipSprite, "modulate", new Color(1, 2, 3, 0), 1.5f); 
				}
			}
		}

		if (_sfxPlayer != null)
		{
			AudioStream jumpSound = GD.Load<AudioStream>("res://Sounds/laser.mp3"); 
			if (jumpSound != null) { _sfxPlayer.Stream = jumpSound; _sfxPlayer.PitchScale = 0.5f; _sfxPlayer.Play(); }
		}

		warpTween.Chain().TweenCallback(Callable.From(() => 
		{
			if (_globalData != null)
			{
				_globalData.JustJumped = true;
			}
			
			OnSaveGamePressed(); 
			SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
			if (transitioner != null) transitioner.ChangeScene("res://galactic_map.tscn");
			else GetTree().ChangeSceneToFile("res://galactic_map.tscn");
		}));
	}

	private void OnSaveGamePressed()
	{
		if (_globalData != null)
		{
			_globalData.CurrentTurn = _currentTurn;
			_globalData.InCombat = _inCombat;
			_globalData.CurrentQueueIndex = _currentQueueIndex;

			var playerState = new Godot.Collections.Array();
			var enemyState = new Godot.Collections.Array(); 
			
			foreach (var kvp in _hexContents)
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
			
			_saveGameButton.Text = "GAME SAVED!";
			_saveGameButton.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f)); 
			GetTree().CreateTimer(2.0f).Timeout += () => 
			{
				_saveGameButton.Text = "SAVE GAME";
				_saveGameButton.RemoveThemeColorOverride("font_color");
			};
		}
	}

	private void OnMainMenuPressed()
	{
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) transitioner.ChangeScene("res://main_menu.tscn");
		else GetTree().ChangeSceneToFile("res://main_menu.tscn");
	}

	private void MoveShip(Vector2I fromHex, Vector2I toHex, int cost)
	{
		MapEntity ship = _hexContents[fromHex];
		_hexContents.Remove(fromHex);
		_hexContents[toHex] = ship;
		
		ship.CurrentActions -= cost;

		if (_selectedHexes.Count == 1 && _selectedHexes[0] == fromHex) ToggleShipMenu(true, ship);

		string sfxPath = GetShipMovementSoundPath(ship.Name);
		if (!string.IsNullOrEmpty(sfxPath))
		{
			AudioStream sfx = GD.Load<AudioStream>(sfxPath);
			if (sfx != null)
			{
				_sfxPlayer.Stream = sfx;
				_sfxPlayer.Play();
			}
		}

		Tween tween = CreateTween();
		Vector2 targetPixelPos = HexToPixel(toHex);
		float distance = ship.VisualSprite.Position.DistanceTo(targetPixelPos);
		float duration = Mathf.Max(0.3f, distance / 500f); 
		tween.TweenProperty(ship.VisualSprite, "position", targetPixelPos, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private int HexDistance(Vector2I a, Vector2I b) { return (Mathf.Abs(a.X - b.X) + Mathf.Abs(a.X + a.Y - b.X - b.Y) + Mathf.Abs(a.Y - b.Y)) / 2; }

	private Dictionary<Vector2I, int> GetReachableHexes(Vector2I startHex, int movementRange)
	{
		Dictionary<Vector2I, int> costSoFar = new Dictionary<Vector2I, int>();
		costSoFar[startHex] = 0;
		Queue<Vector2I> frontier = new Queue<Vector2I>();
		frontier.Enqueue(startHex);

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (Vector2I dir in _hexDirections)
			{
				Vector2I next = current + dir;
				if (!_hexGrid.ContainsKey(next)) continue;

				bool isBlocked = false;
				if (_hexContents.ContainsKey(next))
				{
					string type = _hexContents[next].Type;
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

	private void ClearHighlights() { foreach (Node child in _highlightLayer.GetChildren()) child.QueueFree(); }

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
		poly.Polygon = points; poly.Color = color; poly.Position = HexToPixel(hexCoord);
		_highlightLayer.AddChild(poly);
	}

	private void SetupUI()
	{
		CanvasLayer uiLayer = new CanvasLayer { Layer = 10 }; 
		AddChild(uiLayer);

		Control uiRoot = new Control();
		uiRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		uiRoot.MouseFilter = Control.MouseFilterEnum.Ignore; 
		
		Font orbitronFont = GD.Load<Font>("res://Orbitron-VariableFont_wght.ttf");
		if (orbitronFont != null)
		{
			Theme customTheme = new Theme();
			customTheme.DefaultFont = orbitronFont;
			uiRoot.Theme = customTheme;
		}
		uiLayer.AddChild(uiRoot);

		Vector2 screenSize = GetViewportRect().Size;
		
		_gameOverPanel = new ColorRect();
		_gameOverPanel.Color = new Color(0, 0, 0, 0.85f); 
		_gameOverPanel.Size = screenSize;
		_gameOverPanel.Visible = false; 
		
		VBoxContainer goVbox = new VBoxContainer();
		goVbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		goVbox.Alignment = BoxContainer.AlignmentMode.Center;
		_gameOverPanel.AddChild(goVbox);

		Label goLabel = new Label();
		goLabel.Text = "FLEET DESTROYED";
		goLabel.AddThemeFontSizeOverride("font_size", 64);
		goLabel.AddThemeColorOverride("font_color", new Color(1f, 0.2f, 0.2f)); 
		goLabel.HorizontalAlignment = HorizontalAlignment.Center;
		goVbox.AddChild(goLabel);

		Button returnBtn = new Button();
		returnBtn.Text = "RETURN TO MAIN MENU";
		returnBtn.CustomMinimumSize = new Vector2(250, 60);
		returnBtn.Pressed += OnMainMenuPressed;
		goVbox.AddChild(returnBtn);

		VBoxContainer topContainer = new VBoxContainer();
		topContainer.Position = new Vector2(20, 20);
		topContainer.AddThemeConstantOverride("separation", 5);
		uiRoot.AddChild(topContainer);

		_initiativeTurnLabel = new Label();
		_initiativeTurnLabel.Text = "EXPLORATION MODE";
		_initiativeTurnLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
		topContainer.AddChild(_initiativeTurnLabel);

		_initiativeUI = new HBoxContainer();
		_initiativeUI.AddThemeConstantOverride("separation", 10); 
		topContainer.AddChild(_initiativeUI);

		StyleBoxFlat panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f); 
		panelStyle.BorderWidthBottom = 2; panelStyle.BorderWidthTop = 2; panelStyle.BorderWidthLeft = 2; panelStyle.BorderWidthRight = 2;
		panelStyle.BorderColor = new Color(0.2f, 0.8f, 1f, 1f); 
		panelStyle.CornerRadiusTopLeft = 5; panelStyle.CornerRadiusBottomRight = 5;
		panelStyle.ContentMarginLeft = 15; panelStyle.ContentMarginRight = 15; panelStyle.ContentMarginTop = 15; panelStyle.ContentMarginBottom = 15;

		_combatLogPanel = new PanelContainer();
		_combatLogPanel.CustomMinimumSize = new Vector2(350, 250);
		_combatLogPanel.AddThemeStyleboxOverride("panel", panelStyle);
		_combatLogPanel.Position = new Vector2(20, screenSize.Y / 2 - 125); 
		_combatLogPanel.Visible = false;

		_combatLogText = new RichTextLabel();
		_combatLogText.BbcodeEnabled = true;
		_combatLogText.ScrollFollowing = true; 
		_combatLogText.CustomMinimumSize = new Vector2(320, 220);
		_combatLogPanel.AddChild(_combatLogText);

		uiRoot.AddChild(_combatLogPanel);

		_infoPanel = new PanelContainer();
		_infoPanel.Position = new Vector2(20, screenSize.Y - 200); 
		_infoPanel.AddThemeStyleboxOverride("panel", panelStyle);

		_infoLabel = new Label();
		_infoLabel.CustomMinimumSize = new Vector2(250, 0); 
		_infoLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		_infoPanel.AddChild(_infoLabel);
		uiRoot.AddChild(_infoPanel);
		_infoPanel.Visible = false; 

		_endTurnButton = new Button();
		_endTurnButton.Text = $"TURN {_currentTurn}"; 
		_endTurnButton.CustomMinimumSize = new Vector2(160, 50);
		StyleBoxFlat btnStyle = new StyleBoxFlat();
		btnStyle.BgColor = new Color(0.1f, 0.3f, 0.1f, 0.9f); 
		btnStyle.BorderWidthBottom = 2; btnStyle.BorderWidthTop = 2; btnStyle.BorderWidthLeft = 2; btnStyle.BorderWidthRight = 2;
		btnStyle.BorderColor = new Color(0.3f, 1f, 0.3f, 1f); 
		btnStyle.CornerRadiusTopLeft = 5; btnStyle.CornerRadiusBottomRight = 5;
		_endTurnButton.AddThemeStyleboxOverride("normal", btnStyle);
		_endTurnButton.AddThemeStyleboxOverride("hover", panelStyle); 
		_endTurnButton.Position = new Vector2(screenSize.X - 180, 20);
		_endTurnButton.Pressed += OnEndTurnPressed;
		uiRoot.AddChild(_endTurnButton);

		_saveGameButton = new Button();
		_saveGameButton.Text = "SAVE GAME";
		_saveGameButton.CustomMinimumSize = new Vector2(160, 50);
		StyleBoxFlat saveStyle = new StyleBoxFlat();
		saveStyle.BgColor = new Color(0.1f, 0.2f, 0.4f, 0.9f); 
		saveStyle.BorderWidthBottom = 2; saveStyle.BorderWidthTop = 2; saveStyle.BorderWidthLeft = 2; saveStyle.BorderWidthRight = 2;
		saveStyle.BorderColor = new Color(0.3f, 0.6f, 1f, 1f); 
		saveStyle.CornerRadiusTopLeft = 5; saveStyle.CornerRadiusBottomRight = 5;
		_saveGameButton.AddThemeStyleboxOverride("normal", saveStyle);
		_saveGameButton.AddThemeStyleboxOverride("hover", panelStyle); 
		_saveGameButton.Position = new Vector2(screenSize.X - 360, 20); 
		_saveGameButton.Pressed += OnSaveGamePressed;
		uiRoot.AddChild(_saveGameButton);

		_repairFleetButton = new Button();
		_repairFleetButton.Text = "REPAIR FLEET";
		_repairFleetButton.CustomMinimumSize = new Vector2(160, 50);
		StyleBoxFlat repairFleetStyle = new StyleBoxFlat();
		repairFleetStyle.BgColor = new Color(0.4f, 0.4f, 0.1f, 0.9f); 
		repairFleetStyle.BorderWidthBottom = 2; repairFleetStyle.BorderWidthTop = 2; repairFleetStyle.BorderWidthLeft = 2; repairFleetStyle.BorderWidthRight = 2;
		repairFleetStyle.BorderColor = new Color(1f, 1f, 0.3f, 1f); 
		repairFleetStyle.CornerRadiusTopLeft = 5; repairFleetStyle.CornerRadiusBottomRight = 5;
		_repairFleetButton.AddThemeStyleboxOverride("normal", repairFleetStyle);
		_repairFleetButton.AddThemeStyleboxOverride("hover", panelStyle); 
		_repairFleetButton.Position = new Vector2(screenSize.X - 540, 20); 
		_repairFleetButton.Pressed += OnRepairFleetPressed;
		uiRoot.AddChild(_repairFleetButton);

		_inventoryButton = new Button();
		_inventoryButton.Text = "INVENTORY";
		_inventoryButton.CustomMinimumSize = new Vector2(160, 50);
		StyleBoxFlat invStyle = new StyleBoxFlat();
		invStyle.BgColor = new Color(0.1f, 0.4f, 0.4f, 0.9f); 
		invStyle.BorderWidthBottom = 2; invStyle.BorderWidthTop = 2; invStyle.BorderWidthLeft = 2; invStyle.BorderWidthRight = 2;
		invStyle.BorderColor = new Color(0.3f, 1f, 1f, 1f); 
		invStyle.CornerRadiusTopLeft = 5; invStyle.CornerRadiusBottomRight = 5;
		_inventoryButton.AddThemeStyleboxOverride("normal", invStyle);
		_inventoryButton.AddThemeStyleboxOverride("hover", panelStyle); 
		_inventoryButton.Position = new Vector2(screenSize.X - 720, 20); 
		_inventoryButton.Pressed += OnInventoryPressed;
		uiRoot.AddChild(_inventoryButton);

		_mainMenuButton = new Button();
		_mainMenuButton.Text = "MAIN MENU";
		_mainMenuButton.CustomMinimumSize = new Vector2(160, 50);
		StyleBoxFlat menuStyle = new StyleBoxFlat();
		menuStyle.BgColor = new Color(0.4f, 0.1f, 0.1f, 0.9f); 
		menuStyle.BorderWidthBottom = 2; menuStyle.BorderWidthTop = 2; menuStyle.BorderWidthLeft = 2; menuStyle.BorderWidthRight = 2;
		menuStyle.BorderColor = new Color(1f, 0.3f, 0.3f, 1f); 
		menuStyle.CornerRadiusTopLeft = 5; menuStyle.CornerRadiusBottomRight = 5;
		_mainMenuButton.AddThemeStyleboxOverride("normal", menuStyle);
		_mainMenuButton.AddThemeStyleboxOverride("hover", panelStyle); 
		_mainMenuButton.Position = new Vector2(screenSize.X - 900, 20); 
		_mainMenuButton.Pressed += OnMainMenuPressed;
		uiRoot.AddChild(_mainMenuButton);

		_jumpButton = new Button();
		_jumpButton.Text = "ENTER STARGATE";
		_jumpButton.CustomMinimumSize = new Vector2(250, 50);
		StyleBoxFlat jumpStyle = new StyleBoxFlat();
		jumpStyle.BgColor = new Color(0.4f, 0.1f, 0.7f, 0.9f); 
		jumpStyle.BorderWidthBottom = 2; jumpStyle.BorderWidthTop = 2; jumpStyle.BorderWidthLeft = 2; jumpStyle.BorderWidthRight = 2;
		jumpStyle.BorderColor = new Color(0.8f, 0.3f, 1f, 1f); 
		jumpStyle.CornerRadiusTopLeft = 5; jumpStyle.CornerRadiusBottomRight = 5;
		_jumpButton.AddThemeStyleboxOverride("normal", jumpStyle);
		_jumpButton.AddThemeStyleboxOverride("hover", panelStyle); 
		_jumpButton.Position = new Vector2(screenSize.X / 2 - 125, screenSize.Y - 140); 
		_jumpButton.Pressed += OnJumpPressed;
		_jumpButton.Visible = false; 
		uiRoot.AddChild(_jumpButton);

		_attackButton = new Button();
		_attackButton.Text = "ATTACK";
		_attackButton.CustomMinimumSize = new Vector2(160, 50);
		StyleBoxFlat atkStyle = new StyleBoxFlat();
		atkStyle.BgColor = new Color(0.6f, 0.1f, 0.1f, 0.9f); 
		atkStyle.BorderWidthBottom = 2; atkStyle.BorderWidthTop = 2; atkStyle.BorderWidthLeft = 2; atkStyle.BorderWidthRight = 2;
		atkStyle.BorderColor = new Color(1f, 0.5f, 0.5f, 1f); 
		atkStyle.CornerRadiusTopLeft = 5; atkStyle.CornerRadiusBottomRight = 5;
		_attackButton.AddThemeStyleboxOverride("normal", atkStyle);
		_attackButton.AddThemeStyleboxOverride("hover", panelStyle); 
		_attackButton.Position = new Vector2(screenSize.X / 2 - 80, screenSize.Y - 80); 
		_attackButton.Pressed += OnAttackPressed;
		_attackButton.Visible = false; 
		uiRoot.AddChild(_attackButton);
		
		_shipMenuPanel = new PanelContainer();
		_shipMenuPanel.CustomMinimumSize = new Vector2(350, 450);
		
		StyleBoxFlat terminalStyle = new StyleBoxFlat();
		terminalStyle.BgColor = new Color(0.05f, 0.1f, 0.15f, 0.95f); 
		terminalStyle.BorderWidthBottom = 2; terminalStyle.BorderWidthTop = 2; terminalStyle.BorderWidthLeft = 2; terminalStyle.BorderWidthRight = 2;
		terminalStyle.BorderColor = new Color(0.2f, 0.8f, 1f, 1f); 
		terminalStyle.CornerRadiusTopLeft = 10; terminalStyle.CornerRadiusBottomLeft = 10;
		_shipMenuPanel.AddThemeStyleboxOverride("panel", terminalStyle);
		
		_shipMenuPanel.Position = new Vector2(screenSize.X + 50, screenSize.Y / 2 - 225); 
		
		VBoxContainer shipMenuVbox = new VBoxContainer();
		shipMenuVbox.AddThemeConstantOverride("separation", 10);
		_shipMenuPanel.AddChild(shipMenuVbox);

		_shipMenuTitle = new Label();
		_shipMenuTitle.AddThemeFontSizeOverride("font_size", 18);
		_shipMenuTitle.AddThemeColorOverride("font_color", new Color(0.2f, 0.8f, 1f));
		_shipMenuTitle.HorizontalAlignment = HorizontalAlignment.Center;
		shipMenuVbox.AddChild(_shipMenuTitle);

		_shipImageDisplay = new TextureRect();
		_shipImageDisplay.CustomMinimumSize = new Vector2(150, 150);
		_shipImageDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_shipImageDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		shipMenuVbox.AddChild(_shipImageDisplay);

		_hpLabel = new Label();
		_hpLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
		shipMenuVbox.AddChild(_hpLabel);
		
		_hpBar = new ProgressBar();
		_hpBar.CustomMinimumSize = new Vector2(0, 15);
		_hpBar.ShowPercentage = false;
		StyleBoxFlat hpBg = new StyleBoxFlat { BgColor = new Color(0.2f, 0.0f, 0.0f) };
		StyleBoxFlat hpFg = new StyleBoxFlat { BgColor = new Color(0.2f, 0.8f, 0.2f) };
		_hpBar.AddThemeStyleboxOverride("background", hpBg);
		_hpBar.AddThemeStyleboxOverride("fill", hpFg);
		shipMenuVbox.AddChild(_hpBar);

		_shieldLabel = new Label();
		_shieldLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.8f, 1f));
		shipMenuVbox.AddChild(_shieldLabel);
		
		_shieldBar = new ProgressBar();
		_shieldBar.CustomMinimumSize = new Vector2(0, 15);
		_shieldBar.ShowPercentage = false;
		StyleBoxFlat shBg = new StyleBoxFlat { BgColor = new Color(0.0f, 0.0f, 0.2f) };
		StyleBoxFlat shFg = new StyleBoxFlat { BgColor = new Color(0.2f, 0.8f, 1f) };
		_shieldBar.AddThemeStyleboxOverride("background", shBg);
		_shieldBar.AddThemeStyleboxOverride("fill", shFg);
		shipMenuVbox.AddChild(_shieldBar);

		_shipMenuDetails = new Label();
		_shipMenuDetails.AutowrapMode = TextServer.AutowrapMode.Word;
		shipMenuVbox.AddChild(_shipMenuDetails);

		HBoxContainer btnRow1 = new HBoxContainer();
		btnRow1.AddThemeConstantOverride("separation", 10);
		btnRow1.Alignment = BoxContainer.AlignmentMode.Center;
		
		_btnWeapons = new Button { Text = "WEAPONS", CustomMinimumSize = new Vector2(100, 30) };
		_btnShields = new Button { Text = "SHIELDS", CustomMinimumSize = new Vector2(100, 30) };
		_btnRepair = new Button { Text = "REPAIR (2 AP)", CustomMinimumSize = new Vector2(100, 30) };
		
		_btnRepair.Pressed += OnRepairPressed;

		btnRow1.AddChild(_btnWeapons);
		btnRow1.AddChild(_btnShields);
		btnRow1.AddChild(_btnRepair); // <-- This line brings it back!
		shipMenuVbox.AddChild(btnRow1);

		HBoxContainer btnRow2 = new HBoxContainer();
		btnRow2.AddThemeConstantOverride("separation", 10);
		btnRow2.Alignment = BoxContainer.AlignmentMode.Center;
		
		_btnScan = new Button { Text = "SCAN", CustomMinimumSize = new Vector2(100, 30) };
		_btnSalvage = new Button { Text = "SALVAGE", CustomMinimumSize = new Vector2(100, 30) };
		
		_btnScan.Pressed += OnScanPressed;
		_btnSalvage.Pressed += OnSalvagePressed;

		btnRow2.AddChild(_btnScan);
		btnRow2.AddChild(_btnSalvage);
		shipMenuVbox.AddChild(btnRow2);

		_closeMenuButton = new Button();
		_closeMenuButton.Text = "CLOSE TERMINAL";
		_closeMenuButton.CustomMinimumSize = new Vector2(0, 35);
		_closeMenuButton.Pressed += () => ToggleShipMenu(false);
		shipMenuVbox.AddChild(_closeMenuButton);

		uiRoot.AddChild(_shipMenuPanel);
		uiRoot.AddChild(_gameOverPanel); 
	}

	private Vector2I PixelToHex(Vector2 pt)
	{
		float q = (Mathf.Sqrt(3f) / 3f * pt.X - 1f / 3f * pt.Y) / HexSize;
		float r = (2f / 3f * pt.Y) / HexSize;
		return AxialRound(q, r);
	}

	private Vector2I AxialRound(float q, float r)
	{
		float s = -q - r;
		int rq = Mathf.RoundToInt(q);
		int rr = Mathf.RoundToInt(r);
		int rs = Mathf.RoundToInt(s);
		float qDiff = Mathf.Abs(rq - q);
		float rDiff = Mathf.Abs(rr - r);
		float sDiff = Mathf.Abs(rs - s);
		if (qDiff > rDiff && qDiff > sDiff) rq = -rr - rs;
		else if (rDiff > sDiff) rr = -rq - rs;
		return new Vector2I(rq, rr);
	}

	private void SetupSpaceBackground()
	{
		Vector2 screenSize = GetViewportRect().Size;
		TextureRect spaceBackgroundRect = new TextureRect();
		Texture2D bgTex = GD.Load<Texture2D>("res://space_bg.png"); 
		if (bgTex != null)
		{
			spaceBackgroundRect.Texture = bgTex;
			spaceBackgroundRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize; 
			spaceBackgroundRect.Size = screenSize; 
			spaceBackgroundRect.Modulate = new Color(0.6f, 0.6f, 0.7f, 1.0f); 
			_bgLayer.AddChild(spaceBackgroundRect);
		}
	}

	private void GenerateGrid()
	{
		for (int q = -_maxMapRadius; q <= _maxMapRadius; q++)
		{
			int r1 = Mathf.Max(-_maxMapRadius, -q - _maxMapRadius);
			int r2 = Mathf.Min(_maxMapRadius, -q + _maxMapRadius);
			for (int r = r1; r <= r2; r++)
			{
				Vector2I hexCoord = new Vector2I(q, r);
				Vector2 worldPos = HexToPixel(hexCoord);
				Node2D hexTile = _hexScene.Instantiate<Node2D>();
				hexTile.Position = worldPos;
				_gridLayer.AddChild(hexTile);
				_hexGrid.Add(hexCoord, hexTile);
			}
		}
	}

	private Vector2 HexToPixel(Vector2I hex)
	{
		float x = HexSize * Mathf.Sqrt(3) * (hex.X + hex.Y / 2f);
		float y = HexSize * 3f / 2f * hex.Y;
		return new Vector2(x, y);
	}

	private void PopulateMapFromMemory()
	{
		Random rng = new Random();
		if (_globalData != null && !string.IsNullOrEmpty(_globalData.SavedSystem))
			rng = new Random(_globalData.SavedSystem.GetHashCode()); 

		MapEntity starData = new MapEntity { Name = "Main Sequence Star", Type = "Celestial Body", Details = "Extreme Heat Signature" };
		if (_globalData != null && !string.IsNullOrEmpty(_globalData.SavedSystem))
			starData.Name = _globalData.SavedSystem.ToUpper() + " PRIME";
			
		SpawnEntityAtHex(new Vector2I(0, 0), "res://YellowSUN.png", starData, 0.75f);

		if (_globalData == null || string.IsNullOrEmpty(_globalData.SavedSystem)) return;

		if (!_globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			SystemData newSys = new SystemData();
			newSys.SystemName = _globalData.SavedSystem;
			
			int pCount = rng.Next(1, 6);
			if (_globalData.CurrentSectorStars != null)
			{
				foreach (var star in _globalData.CurrentSectorStars)
				{
					if (star.SystemName == _globalData.SavedSystem)
					{
						pCount = star.PlanetCount;
						break;
					}
				}
			}

			string[] planetSuffixes = { "Prime", "Secundus", "Tertius", "Quartus", "Quintus", "Sextus", "Septimus", "Octavus" };

			for (int i = 0; i < pCount; i++)
			{
				PlanetData newP = new PlanetData();
				string suffix = i < planetSuffixes.Length ? planetSuffixes[i] : (i + 1).ToString();
				newP.Name = _globalData.SavedSystem + " " + suffix;
				newP.TypeIndex = rng.Next(0, 6);
				newP.Scale = 0.4f + (float)rng.NextDouble() * 0.4f;
				newP.Habitability = "Unknown";
				newSys.Planets.Add(newP);
			}

			_globalData.ExploredSystems[_globalData.SavedSystem] = newSys;
		}

		SystemData currentSystem = _globalData.ExploredSystems[_globalData.SavedSystem];
		Vector2I basePlanetLocation = new Vector2I(2, -1); 
		
		int currentOrbitRing = 2; 
		foreach (PlanetData pData in currentSystem.Planets)
		{
			Vector2I spawnHex = FindEmptyHexInRing(currentOrbitRing, rng);
			currentOrbitRing += 3; 
			string pTypeStr = GetPlanetTypeString(pData.TypeIndex);
			string pTex = GetTexturePathForType(pTypeStr);
			MapEntity planetEntity = new MapEntity { Name = pData.Name, Type = "Planet", Details = $"Biome Class: {pTypeStr.ToUpper()}\nHab: {pData.Habitability}" };
			SpawnEntityAtHex(spawnHex, pTex, planetEntity, pData.Scale);
			if (pData.Name == _globalData.SavedPlanet) basePlanetLocation = spawnHex;
		}

		if (!currentSystem.HasBeenVisited)
		{
			int numGates = rng.Next(1, 3);
			for (int i = 0; i < numGates; i++)
			{
				Vector2I gateHex = FindEmptyHexInRing(rng.Next(15, _maxMapRadius - 5), rng);
				currentSystem.StargateLocations.Add(gateHex); 
			}
		}
		
		foreach (Vector2I gateHex in currentSystem.StargateLocations)
		{
			MapEntity gateEntity = new MapEntity { Name = "Ancient StarGate", Type = "StarGate", Details = "Trans-dimensional warp gate connecting local star systems." };
			SpawnEntityAtHex(gateHex, "res://StarGate.png", gateEntity, 0.4f);
		}

		bool arrivedViaJump = false;
		if (_globalData != null) 
		{
			arrivedViaJump = _globalData.JustJumped;
			if (arrivedViaJump)
			{
				if (currentSystem.StargateLocations.Count > 0)
				{
					basePlanetLocation = currentSystem.StargateLocations[rng.Next(currentSystem.StargateLocations.Count)];
				}
				else
				{
					basePlanetLocation = FindEmptyHexInRing(rng.Next(10, _maxMapRadius - 5), rng);
				}
				_globalData.JustJumped = false;
			}
		}

		if (_globalData.SavedFleetState != null && _globalData.SavedFleetState.Count > 0)
		{
			int jumpSpawnOffset = 0; 
			foreach (var item in _globalData.SavedFleetState)
			{
				var shipDict = (Godot.Collections.Dictionary)item;
				string shipName = (string)shipDict["Name"];
				(int range, int dmg) = GetShipWeaponStats(shipName);
				
				Vector2I spawnPos = new Vector2I((int)shipDict["Q"], (int)shipDict["R"]);
				if (arrivedViaJump) 
				{
					spawnPos = basePlanetLocation + _hexDirections[jumpSpawnOffset % 6];
					jumpSpawnOffset++;
				}

				int actions = shipDict.ContainsKey("CurrentActions") ? (int)shipDict["CurrentActions"] : (int)shipDict["CurrentMovement"];
				int maxActs = shipDict.ContainsKey("MaxActions") ? (int)shipDict["MaxActions"] : (int)shipDict["MaxMovement"];

				MapEntity shipData = new MapEntity { 
					Name = shipName, Type = "Player Fleet", Details = "Status: Online",
					MaxActions = maxActs, CurrentActions = actions,
					AttackRange = range, AttackDamage = dmg,
					MaxHP = (int)shipDict["MaxHP"], CurrentHP = (int)shipDict["CurrentHP"],
					MaxShields = (int)shipDict["MaxShields"], CurrentShields = (int)shipDict["CurrentShields"],
					InitiativeBonus = GetShipInitiativeBonus(shipName),
					BaseRotationOffset = GetShipRotationOffset(shipName),
					CurrentInitiativeRoll = shipDict.ContainsKey("CurrentInitiativeRoll") ? (int)shipDict["CurrentInitiativeRoll"] : 0
				};
				SpawnEntityAtHex(spawnPos, GetShipTexturePath(shipName), shipData, 0.2f); 
			}
		}
		else if (_globalData.SelectedPlayerFleet != null && _globalData.SelectedPlayerFleet.Count > 0)
		{
			int currentDirIndex = 0;
			foreach (string shipName in _globalData.SelectedPlayerFleet)
			{
				while (currentDirIndex < 6)
				{
					Vector2I spawnPos = basePlanetLocation + _hexDirections[currentDirIndex];
					currentDirIndex++;
					if (_hexGrid.ContainsKey(spawnPos) && !_hexContents.ContainsKey(spawnPos))
					{
						int shipBaseActionPoints = GetShipBaseActions(shipName); 
						(int hp, int shields) = GetShipCombatStats(shipName);
						(int range, int dmg) = GetShipWeaponStats(shipName);

						MapEntity shipData = new MapEntity { 
							Name = shipName, Type = "Player Fleet", Details = "Status: Online",
							MaxActions = shipBaseActionPoints, CurrentActions = shipBaseActionPoints,
							AttackRange = range, AttackDamage = dmg,
							MaxHP = hp, CurrentHP = hp, MaxShields = shields, CurrentShields = shields,
							InitiativeBonus = GetShipInitiativeBonus(shipName),
							BaseRotationOffset = GetShipRotationOffset(shipName)
						};
						SpawnEntityAtHex(spawnPos, GetShipTexturePath(shipName), shipData, 0.2f); 
						break; 
					}
				}
			}
		}

		if (currentSystem.HasBeenVisited && currentSystem.EnemyFleets != null && currentSystem.EnemyFleets.Count > 0)
		{
			foreach (var item in currentSystem.EnemyFleets)
			{
				var shipDict = (Godot.Collections.Dictionary)item;
				string shipName = (string)shipDict["Name"];
				Vector2I spawnPos = new Vector2I((int)shipDict["Q"], (int)shipDict["R"]);
				(int range, int dmg) = GetShipWeaponStats(shipName);

				int actions = shipDict.ContainsKey("CurrentActions") ? (int)shipDict["CurrentActions"] : (int)shipDict["CurrentMovement"];
				int maxActs = shipDict.ContainsKey("MaxActions") ? (int)shipDict["MaxActions"] : (int)shipDict["MaxMovement"];

				MapEntity shipData = new MapEntity { 
					Name = shipName, Type = "Enemy Fleet", Details = "Status: Hostile Target",
					MaxActions = maxActs, CurrentActions = actions,
					AttackRange = range, AttackDamage = dmg,
					MaxHP = (int)shipDict["MaxHP"], CurrentHP = (int)shipDict["CurrentHP"],
					MaxShields = (int)shipDict["MaxShields"], CurrentShields = (int)shipDict["CurrentShields"],
					InitiativeBonus = GetShipInitiativeBonus(shipName),
					BaseRotationOffset = GetShipRotationOffset(shipName),
					CurrentInitiativeRoll = shipDict.ContainsKey("CurrentInitiativeRoll") ? (int)shipDict["CurrentInitiativeRoll"] : 0
				};
				SpawnEntityAtHex(spawnPos, GetShipTexturePath(shipName), shipData, 0.2f); 
			}
		}
		else if (!currentSystem.HasBeenVisited)
		{
			int enemyFleetCount = rng.Next(1, 6); 
			var savedEnemyArray = new Godot.Collections.Array();

			for (int fleet = 0; fleet < enemyFleetCount; fleet++)
			{
				Vector2I fleetBaseLocation = FindEmptyHexInRing(rng.Next(10, _maxMapRadius - 2), rng);
				int shipsInThisFleet = rng.Next(1, 4); 
				int enemyDirIndex = 0;

				for (int i = 0; i < shipsInThisFleet; i++)
				{
					string enemyName = _enemyShipTypes[rng.Next(_enemyShipTypes.Length)];
					while (enemyDirIndex < 18) 
					{
						int ring = (enemyDirIndex / 6) + 1;
						Vector2I spawnPos = fleetBaseLocation + _hexDirections[enemyDirIndex % 6] * ring;
						enemyDirIndex++;
						if (_hexGrid.ContainsKey(spawnPos) && !_hexContents.ContainsKey(spawnPos))
						{
							int shipBaseActionPoints = GetShipBaseActions(enemyName); 
							(int hp, int shields) = GetShipCombatStats(enemyName);
							(int range, int dmg) = GetShipWeaponStats(enemyName);

							MapEntity shipData = new MapEntity { 
								Name = enemyName, Type = "Enemy Fleet", Details = "Status: Hostile Target",
								MaxActions = shipBaseActionPoints, CurrentActions = shipBaseActionPoints,
								AttackRange = range, AttackDamage = dmg,
								MaxHP = hp, CurrentHP = hp, MaxShields = shields, CurrentShields = shields,
								InitiativeBonus = GetShipInitiativeBonus(enemyName),
								BaseRotationOffset = GetShipRotationOffset(enemyName)
							};
							SpawnEntityAtHex(spawnPos, GetShipTexturePath(enemyName), shipData, 0.2f); 

							var shipDict = new Godot.Collections.Dictionary<string, Variant>();
							shipDict["Name"] = enemyName; shipDict["Q"] = spawnPos.X; shipDict["R"] = spawnPos.Y;
							shipDict["CurrentHP"] = hp; shipDict["MaxHP"] = hp; shipDict["CurrentShields"] = shields; shipDict["MaxShields"] = shields;
							shipDict["MaxActions"] = shipBaseActionPoints; shipDict["CurrentActions"] = shipBaseActionPoints;
							savedEnemyArray.Add(shipDict);
							break; 
						}
					}
				}
			}
			currentSystem.EnemyFleets = savedEnemyArray;
			currentSystem.HasBeenVisited = true; 
		}
	}

	private Vector2I FindEmptyHexInRing(int radius, Random rng)
	{
		List<Vector2I> ringHexes = new List<Vector2I>();
		Vector2I currentHex = new Vector2I(0, -radius);
		Vector2I[] ringDirs = new Vector2I[] {
			new Vector2I(1, 0), new Vector2I(0, 1), new Vector2I(-1, 1), 
			new Vector2I(-1, 0), new Vector2I(0, -1), new Vector2I(1, -1)
		};
		foreach (Vector2I dir in ringDirs)
		{
			for (int i = 0; i < radius; i++)
			{
				ringHexes.Add(currentHex);
				currentHex += dir;
			}
		}
		ShuffleList(ringHexes, rng);
		foreach (var hex in ringHexes) if (_hexGrid.ContainsKey(hex) && !_hexContents.ContainsKey(hex)) return hex;
		return new Vector2I(radius, 0); 
	}

	private void ShuffleList<T>(List<T> list, Random rng)  
	{  
		int n = list.Count;  
		while (n > 1) { n--; int k = rng.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; }  
	}

	private void SpawnEntityAtHex(Vector2I hexCoord, string texturePath, MapEntity entityData, float scale)
	{
		if (!_hexGrid.ContainsKey(hexCoord)) return;
		Sprite2D entitySprite = new Sprite2D();
		Texture2D tex = GD.Load<Texture2D>(texturePath);
		if (tex != null) entitySprite.Texture = tex;
		entitySprite.Scale = new Vector2(scale, scale);
		entitySprite.Position = HexToPixel(hexCoord);
		_entityLayer.AddChild(entitySprite);
		entityData.VisualSprite = entitySprite;
		_hexContents[hexCoord] = entityData;
	}

	private string GetPlanetTypeString(int typeIndex)
	{
		string[] types = { "Terra", "Arid", "Ocean", "Toxic", "Frozen", "Lava" };
		if (typeIndex >= 0 && typeIndex < types.Length) return types[typeIndex];
		return "Terra";
	}

	private string GetTexturePathForType(string type)
	{
		if (string.IsNullOrEmpty(type)) return "res://Planets/terra_planet.png";
		switch (type.ToUpper())
		{
			case "TERRA": return "res://Planets/terra_planet.png";
			case "ARID": return "res://Planets/arid_planet.png";
			case "OCEAN": return "res://Planets/ocean_planet.png";
			case "TOXIC": return "res://Planets/toxic_planet.png";
			case "FROZEN": return "res://Planets/frozen_planet.png";
			case "LAVA": return "res://Planets/lava_planet.png";
			default: return "res://Planets/terra_planet.png"; 
		}
	}

	private string GetShipTexturePath(string shipName)
	{
		switch (shipName)
		{
			case "The Relic Harvester": return "res://Ships/RelicHarvesterSprite.png";
			case "The Panacea Spire": return "res://Ships/PanaceaSpireSprite.png";
			case "The Neptune Forge": return "res://Ships/NeptuneForgeSprite.png";
			case "The Genesis Ark": return "res://Ships/GenesisArkSprite.png";
			case "The Valkyrie Wing": return "res://Ships/ValkyrieWingSprite.png";
			case "The Aegis Bastion": return "res://Ships/AegisBastionSprite.png";
			case "The Aether Skimmer": return "res://Ships/AetherSkimmerSprite.png";
			
			case "Aether Censor Obelisk": return "res://EnemyShips/AetherCensorObeliskSprite.png";
			case "Custodian Logic Barge": return "res://EnemyShips/CustodianLogicBargeSprite.png";
			case "Ignis Repurposed Terraformer": return "res://EnemyShips/IgnisRepurposedTerraformerSprite.png";
			case "Reformatter Dreadnought": return "res://EnemyShips/ReformatterDreadnoughtSprite.png";
			case "Scrap-Stick Subversion Drone": return "res://EnemyShips/ScrapStickSubversionDroneSprite.png";
			default: return "res://icon.svg"; 
		}
	}

	private string GetShipMovementSoundPath(string shipName)
	{
		switch (shipName)
		{
			case "The Valkyrie Wing": return "res://Sounds/ValkyrieWing.wav";
			case "The Aegis Bastion": return "res://Sounds/HeavyThrusters.wav";
			case "The Panacea Spire": return "res://Sounds/PanaceaSpire.wav";
			case "The Aether Skimmer": return "res://Sounds/AetherSkimmer.wav";
			case "The Genesis Ark": return "res://Sounds/GenesisArk.wav";
			case "The Neptune Forge": return "res://Sounds/NeptuneForge.wav";
			case "The Relic Harvester": return "res://Sounds/RelicHarvester.mp3";
			default: return ""; 
		}
	}

	private int GetShipBaseActions(string shipName)
	{
		switch (shipName)
		{
			case "The Aether Skimmer": return 5;
			case "The Valkyrie Wing": return 4;
			case "The Genesis Ark": return 3;
			case "The Panacea Spire": return 3;
			case "The Relic Harvester": return 3;
			case "The Neptune Forge": return 2;
			case "The Aegis Bastion": return 2;
			
			case "Scrap-Stick Subversion Drone": return 5; 
			case "Aether Censor Obelisk": return 4; 
			case "Custodian Logic Barge": return 3; 
			case "Ignis Repurposed Terraformer": return 2; 
			case "Reformatter Dreadnought": return 2; 
			default: return 3; 
		}
	}

	private (int hp, int shields) GetShipCombatStats(string shipName)
	{
		switch (shipName)
		{
			case "The Aegis Bastion": return (100, 50);   
			case "The Neptune Forge": return (80, 20);    
			case "The Genesis Ark": return (50, 30);      
			case "The Panacea Spire": return (40, 40);    
			case "The Relic Harvester": return (50, 20);  
			case "The Valkyrie Wing": return (30, 20);    
			case "The Aether Skimmer": return (20, 10);   
			
			case "Reformatter Dreadnought": return (100, 50); 
			case "Ignis Repurposed Terraformer": return (80, 20); 
			case "Custodian Logic Barge": return (50, 30); 
			case "Aether Censor Obelisk": return (30, 20); 
			case "Scrap-Stick Subversion Drone": return (20, 10); 
			default: return (50, 25); 
		}
	}

	private (int range, int damage) GetShipWeaponStats(string shipName)
	{
		switch (shipName)
		{
			case "The Aegis Bastion": return (2, 25);   
			case "The Neptune Forge": return (2, 30);    
			case "The Genesis Ark": return (3, 20);      
			case "The Panacea Spire": return (2, 15);    
			case "The Relic Harvester": return (1, 35);  
			case "The Valkyrie Wing": return (2, 15);    
			case "The Aether Skimmer": return (1, 20);   
			
			case "Reformatter Dreadnought": return (2, 25); 
			case "Ignis Repurposed Terraformer": return (2, 30); 
			case "Custodian Logic Barge": return (3, 20); 
			case "Aether Censor Obelisk": return (2, 15); 
			case "Scrap-Stick Subversion Drone": return (1, 20); 
			default: return (2, 20); 
		}
	}

	private int GetShipInitiativeBonus(string shipName)
	{
		switch (shipName)
		{
			case "The Aether Skimmer":
			case "Scrap-Stick Subversion Drone": 
				return 5; 
			
			case "The Valkyrie Wing":
			case "Aether Censor Obelisk": 
				return 3; 

			case "The Genesis Ark":
			case "The Panacea Spire":
			case "The Relic Harvester":
			case "Custodian Logic Barge": 
				return 0; 
			
			case "The Neptune Forge":
			case "Ignis Repurposed Terraformer":
			case "The Aegis Bastion":
			case "Reformatter Dreadnought": 
				return -2; 
				
			default: return 0; 
		}
	}

	private float GetShipRotationOffset(string shipName)
	{
		switch (shipName)
		{
			case "The Genesis Ark":
			case "The Panacea Spire":
			case "The Relic Harvester":
			case "The Valkyrie Wing":
			case "The Aegis Bastion":
				return Mathf.Pi / 2f; 
				
			case "The Neptune Forge":
			case "Scrap-Stick Subversion Drone":
			case "Reformatter Dreadnought":
				return Mathf.Pi; 

			case "The Aether Skimmer":
			case "Aether Censor Obelisk":
			case "Custodian Logic Barge":
			case "Ignis Repurposed Terraformer":
				return 0f; 

			default:
				return 0f; 
		}
	}
}
