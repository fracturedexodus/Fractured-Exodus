using Godot;
using System;
using System.Collections.Generic;

// --- Moved from BattleMap: Custom Node to draw the drag-selection box ---
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

public partial class TacticalCamera : Camera2D
{
	public float PanSpeed = 600f;
	public float ZoomSpeed = 0.1f;
	public float MinZoom = 0.3f;
	public float MaxZoom = 2.0f;

	private BattleMap _map;
	private SelectionBox _selectionBox;
	private bool _isDragging = false;
	private Vector2 _dragStartPos;

	public void Initialize(BattleMap map, SelectionBox selectionBox, float limitLeft, float limitRight, float limitTop, float limitBottom)
	{
		_map = map;
		_selectionBox = selectionBox;
		LimitLeft = (int)limitLeft;
		LimitRight = (int)limitRight;
		LimitTop = (int)limitTop;
		LimitBottom = (int)limitBottom;
		MakeCurrent();
	}

	public override void _Process(double delta)
	{
		if (_map == null || _map.IsJumping || _map.UI == null) return;

		Vector2 panDirection = Vector2.Zero;
		
		if (Input.IsKeyPressed(Key.W)) panDirection.Y -= 1;
		if (Input.IsKeyPressed(Key.S)) panDirection.Y += 1;
		if (Input.IsKeyPressed(Key.A)) panDirection.X -= 1;
		if (Input.IsKeyPressed(Key.D)) panDirection.X += 1;

		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector2 screenSize = GetViewportRect().Size;
		float edgeMargin = 30f; 

		if (mousePos.X >= 0 && mousePos.X <= screenSize.X && mousePos.Y >= 0 && mousePos.Y <= screenSize.Y)
		{
			if (mousePos.X < edgeMargin) panDirection.X -= 1;
			else if (mousePos.X > screenSize.X - edgeMargin) panDirection.X += 1;

			if (mousePos.Y < edgeMargin) panDirection.Y -= 1;
			else if (mousePos.Y > screenSize.Y - edgeMargin) panDirection.Y += 1;
		}

		if (panDirection != Vector2.Zero) 
		{
			Position += panDirection.Normalized() * PanSpeed * (float)delta * (1.0f / Zoom.X);
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_map == null || _map.IsJumping || _map.UI == null) return;

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (!_map.Combat.InCombat)
			{
				if (keyEvent.Keycode == Key.F)
				{
					_map.ActivateFleetTravelMode();
					return;
				}

				int shipHotkey = GetShipHotkeyNumber(keyEvent.Keycode);
				if (shipHotkey > 0)
				{
					_map.TrySelectPlayerShipByHotkey(shipHotkey);
					return;
				}
			}

			if (keyEvent.Keycode == Key.Space)
			{
				if (_map.Combat.InCombat && _map.Combat.ActiveShip != null && _map.Combat.ActiveShip.Type == GameConstants.EntityTypes.PlayerFleet)
				{
					_map.OnEndTurnPressed();
				}
			}
			else if (keyEvent.Keycode == Key.R)
			{
				_map.OnRepairPressed();
			}
			else if (keyEvent.Keycode == Key.Q)
			{
				if (_map.Combat.InCombat && _map.Combat.ActiveShip != null && _map.Combat.ActiveShip.Type == GameConstants.EntityTypes.PlayerFleet && _map.Combat.ActiveShip.CurrentActions > 0)
				{
					Vector2I hoveredHex = HexMath.PixelToHex(GetGlobalMousePosition(), _map.HexSize);
					if (_map.HexContents.ContainsKey(hoveredHex) && _map.HexContents[hoveredHex].Type == GameConstants.EntityTypes.EnemyFleet)
					{
						Vector2I activeHex = Vector2I.Zero;
						foreach(var kvp in _map.HexContents) if (kvp.Value == _map.Combat.ActiveShip) activeHex = kvp.Key;

						if (HexMath.HexDistance(activeHex, hoveredHex) <= _map.Combat.ActiveShip.AttackRange)
						{
							_map.Combat.PerformAttack(activeHex, hoveredHex);
							_map.Combat.IsTargeting = false;
							_map.UI.AttackButton.Text = "ATTACK";
							_map.Combat.CheckForCombatTrigger();
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
				if (mouseButton.ButtonIndex == MouseButton.WheelUp) Zoom += new Vector2(ZoomSpeed, ZoomSpeed);
				else if (mouseButton.ButtonIndex == MouseButton.WheelDown) Zoom -= new Vector2(ZoomSpeed, ZoomSpeed);
				Zoom = new Vector2(Mathf.Clamp(Zoom.X, MinZoom, MaxZoom), Mathf.Clamp(Zoom.Y, MinZoom, MaxZoom));
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
					bool preserveFleetSelection = !_map.Combat.InCombat && _map.IsFleetTravelMode;

					if (selectionRect.Area < 100)
					{
						Vector2I clickedHex = HexMath.PixelToHex(GetGlobalMousePosition(), _map.HexSize);

						if (!_map.HexGrid.ContainsKey(clickedHex))
						{
							if (preserveFleetSelection)
							{
								_map.ToggleShipMenu(false);
								_map.UpdateHighlights();
							}
							else
							{
								_map.ClearSelectionState();
							}
							return;
						}

						if (_map.Combat.IsTargeting && _map.SelectedHexes.Count == 1)
						{
							if (_map.HexContents.ContainsKey(clickedHex) && _map.HexContents[clickedHex].Type == GameConstants.EntityTypes.EnemyFleet)
							{
								int dist = HexMath.HexDistance(_map.SelectedHexes[0], clickedHex);
								MapEntity attacker = _map.HexContents[_map.SelectedHexes[0]];
								
								if (dist <= attacker.AttackRange)
								{
									_map.Combat.PerformAttack(_map.SelectedHexes[0], clickedHex);
									_map.Combat.IsTargeting = false;
									_map.UI.AttackButton.Text = "ATTACK";
									_map.Combat.CheckForCombatTrigger(); 
								}
								else GD.Print("Target out of range!");
							}
							else
							{
								_map.Combat.IsTargeting = false;
								_map.UI.AttackButton.Text = "ATTACK";
							}
							return; 
						}

						if (_map.HexContents.ContainsKey(clickedHex) && (_map.HexContents[clickedHex].Type == GameConstants.EntityTypes.PlayerFleet || _map.HexContents[clickedHex].Type == GameConstants.EntityTypes.EnemyFleet))
						{
							MapEntity clickedShip = _map.HexContents[clickedHex];
							if (clickedShip.Type == GameConstants.EntityTypes.PlayerFleet)
							{
								_map.SelectSinglePlayerShip(clickedHex, clickedShip.VisualSprite.Visible);
								return;
							}

							if (!preserveFleetSelection)
							{
								_map.SelectedHexes.Clear();
							}

							if (clickedShip.VisualSprite.Visible) _map.ToggleShipMenu(true, clickedShip);
						}
						else if (preserveFleetSelection)
						{
							_map.ToggleShipMenu(false);
						}
						else
						{
							_map.ClearSelectionState();
							return;
						}
					}
					else 
					{
						if (!_map.Combat.InCombat) 
						{
							List<Vector2I> selectedPlayerShips = new List<Vector2I>();
							foreach (var kvp in _map.HexContents)
							{
								if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet && selectionRect.HasPoint(HexMath.HexToPixel(kvp.Key, _map.HexSize)))
								{
									selectedPlayerShips.Add(kvp.Key);
								}
							}
							_map.SetManualPlayerSelection(selectedPlayerShips);
							return;
						}
						_map.ToggleShipMenu(false); 
					}
					_map.UpdateHighlights();
				}
			}
			
			if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.IsPressed())
			{
				Vector2I clickedHex = HexMath.PixelToHex(GetGlobalMousePosition(), _map.HexSize);
				
				if (!_map.HexGrid.ContainsKey(clickedHex)) return; 

				if (_map.Combat.InCombat)
				{
					if (_map.Combat.ActiveShip != null && _map.Combat.ActiveShip.Type == GameConstants.EntityTypes.PlayerFleet && _map.SelectedHexes.Count > 0)
					{
						Vector2I activeHex = _map.SelectedHexes[0];
						
						if (_map.HexContents.ContainsKey(clickedHex) && _map.HexContents[clickedHex].Type == GameConstants.EntityTypes.EnemyFleet)
						{
							if (_map.Combat.ActiveShip.CurrentActions > 0 && HexMath.HexDistance(activeHex, clickedHex) <= _map.Combat.ActiveShip.AttackRange)
							{
								_map.Combat.PerformAttack(activeHex, clickedHex);
								_map.Combat.IsTargeting = false;
								_map.UI.AttackButton.Text = "ATTACK";
								_map.Combat.CheckForCombatTrigger();
							}
							else GD.Print("Target out of range or action spent!");
						}
						else
						{
							Dictionary<Vector2I, int> reachable = _map.GetReachableHexes(activeHex, _map.Combat.ActiveShip.CurrentActions);
							if (reachable.ContainsKey(clickedHex) && clickedHex != activeHex)
							{
								_map.MoveShip(activeHex, clickedHex, reachable[clickedHex]);
								_map.SelectedHexes.Clear();
								_map.SelectedHexes.Add(clickedHex);
								_map.UpdateHighlights();
								_map.Combat.CheckForCombatTrigger();
							}
						}
					}
				}
				else 
				{
					if (_map.SelectedHexes.Count > 0) _map.MoveGroup(_map.SelectedHexes, clickedHex);
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

			Vector2I hoveredHex = HexMath.PixelToHex(GetGlobalMousePosition(), _map.HexSize);
			if (_map.HexContents.ContainsKey(hoveredHex))
			{
				MapEntity entity = _map.HexContents[hoveredHex];
				if (entity.Type == GameConstants.EntityTypes.EnemyFleet && !entity.VisualSprite.Visible)
				{
					_map.UI.InfoPanel.Visible = false;
					return;
				}

				string dynamicStats = "";
				if (entity.Type == GameConstants.EntityTypes.PlayerFleet || entity.Type == GameConstants.EntityTypes.EnemyFleet)
				{
					string initText = _map.Combat.InCombat ? $" | INIT: {entity.CurrentInitiativeRoll}" : "";
					dynamicStats = $"HP: {entity.CurrentHP}/{entity.MaxHP} | SHIELD: {entity.CurrentShields}/{entity.MaxShields}\n" +
								   $"ACTIONS: {entity.CurrentActions}/{entity.MaxActions}{initText}\n" +
								   $"RANGE: {entity.AttackRange} | DMG: 0-{entity.AttackDamage}\n";
				}

				_map.UI.InfoLabel.Text = $"[ {entity.Name.ToUpper()} ]\nType: {entity.Type}\n{dynamicStats}Data: {entity.Details}";
				_map.UI.InfoPanel.Visible = true;
			}
			else _map.UI.InfoPanel.Visible = false; 
		}
	}

	private static int GetShipHotkeyNumber(Key keycode)
	{
		return keycode switch
		{
			Key.Key1 or Key.Kp1 => 1,
			Key.Key2 or Key.Kp2 => 2,
			Key.Key3 or Key.Kp3 => 3,
			Key.Key4 or Key.Kp4 => 4,
			Key.Key5 or Key.Kp5 => 5,
			_ => 0
		};
	}
}
