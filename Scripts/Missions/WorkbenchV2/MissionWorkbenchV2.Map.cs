using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchV2
{
	private static readonly Vector2 GridOrigin = new Vector2(0f, -20f);
	private readonly Dictionary<string, Node2D> _nodesByElementId = new Dictionary<string, Node2D>();
	private bool _isPanning;
	private bool _isDraggingElement;
	private Vector2 _lastMousePosition;
	private int _dragStartColumn;
	private int _dragStartRow;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.CtrlPressed && key.Keycode == Key.S) { SaveDocument(); HandleInput(); return; }
			if (key.CtrlPressed && key.Keycode == Key.Z) { _store.Undo(); HandleInput(); return; }
			if (key.CtrlPressed && (key.Keycode == Key.Y || (key.ShiftPressed && key.Keycode == Key.Z))) { _store.Redo(); HandleInput(); return; }
			if (key.Keycode == Key.Escape) { _placementAsset = string.Empty; _paletteList.DeselectAll(); HandleInput(); return; }
			if (_mode == WorkbenchMode.Map && key.Keycode == Key.Delete) { DeleteSelectedElement(); HandleInput(); return; }
			if (_mode == WorkbenchMode.Map && key.CtrlPressed && key.Keycode == Key.D) { DuplicateSelectedElement(); HandleInput(); return; }
			if (_mode == WorkbenchMode.Map && TryNudgeSelectedElement(key.Keycode)) { HandleInput(); return; }
		}

		if (_mode != WorkbenchMode.Map) return;
		if (@event is InputEventMouseButton mouse)
		{
			if (mouse.ButtonIndex == MouseButton.Middle)
			{
				_isPanning = mouse.Pressed;
				_lastMousePosition = mouse.Position;
				HandleInput();
				return;
			}
			if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelUp) { SetZoom(_camera.Zoom.X + 0.08f); HandleInput(); return; }
			if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelDown) { SetZoom(_camera.Zoom.X - 0.08f); HandleInput(); return; }
			if (!IsMapScreenPosition(mouse.Position)) return;
			if (mouse.ButtonIndex == MouseButton.Left)
			{
				if (mouse.Pressed) BeginMapAction();
				else EndMapAction();
				HandleInput();
				return;
			}
			if (mouse.Pressed && mouse.ButtonIndex == MouseButton.Right)
			{
				SelectAtCell(GetMouseCell());
				DeleteSelectedElement();
				HandleInput();
			}
		}

		if (@event is InputEventMouseMotion motion && _isPanning)
		{
			_camera.Position -= motion.Relative / Mathf.Max(_camera.Zoom.X, 0.01f);
			_lastMousePosition = motion.Position;
			HandleInput();
		}
	}

	private void BeginMapAction()
	{
		Vector2I cell = GetMouseCell();
		if (!string.IsNullOrWhiteSpace(_placementAsset))
		{
			MissionMapElement element = CreateElementForAsset(_placementAsset, cell);
			if (element == null) return;
			_store.Execute(new AddMissionElementCommand(element));
			_store.Select(element.Id);
			SetStatus($"Placed {GetElementDisplayName(element)} at {cell.X},{cell.Y}.");
			return;
		}

		SelectAtCell(cell);
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null) return;
		_isDraggingElement = true;
		_dragStartColumn = selected.Column;
		_dragStartRow = selected.Row;
	}

	private void EndMapAction()
	{
		if (!_isDraggingElement) return;
		_isDraggingElement = false;
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null) return;
		Vector2I cell = GetMouseCell();
		if (cell.X == _dragStartColumn && cell.Y == _dragStartRow) return;
		_store.Execute(new MoveMissionElementCommand(selected.Id, _dragStartColumn, _dragStartRow, cell.X, cell.Y));
		SetStatus($"Moved {GetElementDisplayName(selected)} to {cell.X},{cell.Y}.");
	}

	private void SelectAtCell(Vector2I cell)
	{
		MissionMapElement selected = _store.Document?.Elements
			.Where(element => element.Column == cell.X && element.Row == cell.Y)
			.OrderByDescending(element => GetElementSelectionPriority(element))
			.ThenByDescending(element => element.SourceOrder)
			.FirstOrDefault();
		_store.Select(selected?.Id ?? string.Empty);
	}

	private void DeleteSelectedElement()
	{
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null) return;
		_store.Execute(new RemoveMissionElementCommand(selected));
		_store.Select(string.Empty);
		SetStatus($"Deleted {GetElementDisplayName(selected)}.");
	}

	private void DuplicateSelectedElement()
	{
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null) return;
		MissionMapElement duplicate = MissionElementCloner.Clone(selected);
		duplicate.Id = $"map_{Guid.NewGuid():N}";
		duplicate.Column++;
		duplicate.Row++;
		duplicate.SourceOrder = NextSourceOrder();
		_store.Execute(new AddMissionElementCommand(duplicate));
		_store.Select(duplicate.Id);
	}

	private bool TryNudgeSelectedElement(Key key)
	{
		Vector2I delta = key switch
		{
			Key.Left => Vector2I.Left,
			Key.Right => Vector2I.Right,
			Key.Up => Vector2I.Up,
			Key.Down => Vector2I.Down,
			_ => Vector2I.Zero
		};
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null || delta == Vector2I.Zero) return false;
		_store.Execute(new MoveMissionElementCommand(selected.Id, selected.Column, selected.Row, selected.Column + delta.X, selected.Row + delta.Y));
		return true;
	}

	private MissionMapElement CreateElementForAsset(string asset, Vector2I cell)
	{
		string[] parts = asset.Split(':', 2);
		if (parts.Length != 2) return null;
		MissionMapElement element = new MissionMapElement
		{
			Id = $"map_{Guid.NewGuid():N}", SourceOrder = NextSourceOrder(), Column = cell.X, Row = cell.Y
		};
		if (parts[0] == "tile")
		{
			element.Kind = MissionMapElementKind.Tile;
			element.TileId = parts[1];
			if (parts[1].StartsWith("door_"))
			{
				element.Logic.Role = "door";
				element.Logic.Label = MissionTileCatalog.TryGetById(parts[1], out MissionTileDefinition door) ? door.DisplayName : parts[1];
				element.Logic.TargetId = $"{parts[1]}_{cell.X}_{cell.Y}";
			}
		}
		else if (parts[0] == "marker")
		{
			element.Kind = MissionMapElementKind.Marker;
			element.MarkerId = parts[1];
			element.Logic.Role = "marker";
			element.Logic.Label = MissionMarkerCatalog.TryGetById(parts[1], out MissionMarkerDefinition marker) ? marker.DisplayName : parts[1];
			element.Logic.TargetId = parts[1] is "npc_spawn" or "hostile_spawn" ? $"{parts[1]}_{cell.X}_{cell.Y}" : parts[1];
			element.Logic.TriggerMode = parts[1].StartsWith("trigger_") || parts[1] == "evac_zone" ? "enter" : "none";
			element.Logic.OneShot = parts[1].StartsWith("trigger_");
		}
		else return null;
		return element;
	}

	private void RefreshMap()
	{
		if (_floorLayer == null) return;
		ClearLayer(_floorLayer);
		ClearLayer(_wallLayer);
		ClearLayer(_propLayer);
		ClearLayer(_markerLayer);
		_nodesByElementId.Clear();
		foreach (MissionMapElement element in _store.Document?.Elements ?? new List<MissionMapElement>())
		{
			Node2D node = CreateElementNode(element);
			if (node == null) continue;
			GetLayerForElement(element).AddChild(node);
			_nodesByElementId[element.Id] = node;
		}
		RefreshSelectionVisuals();
	}

	private Node2D CreateElementNode(MissionMapElement element)
	{
		Node2D root = new Node2D
		{
			Name = element.Id,
			Position = IsoGridHelper.GridToWorld(element.Column, element.Row, MissionFloorTextureFactory.TileSize, GridOrigin) + new Vector2(element.OffsetX, element.OffsetY),
			ZIndex = (element.Column + element.Row) * 10 + GetElementSelectionPriority(element)
		};
		root.SetMeta("mission_element_id", element.Id);

		if (element.Kind == MissionMapElementKind.Marker)
		{
			Color color = MissionMarkerCatalog.TryGetById(element.MarkerId, out MissionMarkerDefinition marker) ? marker.Color : Colors.Magenta;
			root.AddChild(new Polygon2D { Polygon = BuildDiamond(34f, 18f), Color = new Color(color.R, color.G, color.B, 0.82f) });
			Label label = new Label { Text = string.IsNullOrWhiteSpace(element.Logic.Label) ? element.MarkerId : element.Logic.Label, Position = new Vector2(-70f, -52f), Size = new Vector2(140f, 28f), HorizontalAlignment = HorizontalAlignment.Center };
			label.AddThemeColorOverride("font_color", color);
			root.AddChild(label);
			return root;
		}

		Texture2D texture = null;
		Vector2 scale = Vector2.One;
		Vector2 catalogOffset = Vector2.Zero;
		if (element.Kind == MissionMapElementKind.Tile && MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition definition))
		{
			texture = ResourceLoader.Load<Texture2D>(definition.TexturePath);
			scale = definition.Scale;
			catalogOffset = definition.Offset;
		}
		else if (element.Kind == MissionMapElementKind.PlacedProp)
		{
			PropDefinition prop = ResourceLoader.Load<PropDefinition>(element.PropDefinitionPath);
			texture = string.IsNullOrWhiteSpace(prop?.SpriteTexturePath) ? null : ResourceLoader.Load<Texture2D>(prop.SpriteTexturePath);
			scale = Vector2.One * (prop?.VisualScaleMultiplier ?? 0.25f);
		}
		if (texture == null)
		{
			root.AddChild(new Polygon2D { Polygon = BuildDiamond(42f, 24f), Color = new Color(0.85f, 0.34f, 0.75f, 0.8f) });
			return root;
		}

		root.AddChild(new Sprite2D
		{
			Texture = texture, Position = catalogOffset, Scale = scale, RotationDegrees = element.RotationDegrees,
			FlipH = element.FlipH, FlipV = element.FlipV
		});
		return root;
	}

	private void RefreshSelectionVisuals()
	{
		foreach ((string id, Node2D node) in _nodesByElementId)
		{
			Node existing = node.GetNodeOrNull("SelectionOutline");
			existing?.QueueFree();
			if (id != _store.SelectedElementId) continue;
			Line2D outline = new Line2D { Name = "SelectionOutline", Width = 4f, DefaultColor = new Color(0.2f, 0.95f, 1f), Closed = true, ZIndex = 1000 };
			foreach (Vector2 point in BuildDiamond(58f, 32f)) outline.AddPoint(point);
			node.AddChild(outline);
		}
	}

	private void FrameDocument()
	{
		List<MissionMapElement> elements = _store.Document?.Elements ?? new List<MissionMapElement>();
		if (elements.Count == 0) { _camera.Position = Vector2.Zero; return; }
		Vector2 sum = Vector2.Zero;
		foreach (MissionMapElement element in elements) sum += IsoGridHelper.GridToWorld(element.Column, element.Row, MissionFloorTextureFactory.TileSize, GridOrigin);
		_camera.Position = sum / elements.Count;
		SetZoom(DefaultZoom);
	}

	private Vector2I GetMouseCell() => IsoGridHelper.WorldToGrid(GetGlobalMousePosition(), MissionFloorTextureFactory.TileSize, GridOrigin);
	private void SetZoom(float zoom) => _camera.Zoom = Vector2.One * Mathf.Clamp(zoom, MinZoom, MaxZoom);
	private int NextSourceOrder() => (_store.Document?.Elements?.Count ?? 0) == 0 ? 1 : _store.Document.Elements.Max(element => element.SourceOrder) + 1;
	private bool IsMapScreenPosition(Vector2 position)
	{
		Vector2 size = GetViewportRect().Size;
		return position.X >= 300f && position.X <= size.X - 380f && position.Y >= 74f && position.Y <= size.Y - 190f;
	}
	private void HandleInput() => GetViewport().SetInputAsHandled();
	private static int GetElementSelectionPriority(MissionMapElement element) => element.Kind switch { MissionMapElementKind.Marker => 4, MissionMapElementKind.PlacedProp => 3, _ => MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition definition) ? definition.Category switch { MissionTileCategory.Prop => 3, MissionTileCategory.Wall => 2, _ => 1 } : 1 };
	private Node2D GetLayerForElement(MissionMapElement element) => element.Kind switch { MissionMapElementKind.Marker => _markerLayer, MissionMapElementKind.PlacedProp => _propLayer, _ => MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition definition) ? definition.Category switch { MissionTileCategory.Floor => _floorLayer, MissionTileCategory.Wall => _wallLayer, _ => _propLayer } : _propLayer };
	private static string GetElementDisplayName(MissionMapElement element) => element.Kind switch { MissionMapElementKind.Marker => element.MarkerId, MissionMapElementKind.PlacedProp => string.IsNullOrWhiteSpace(element.Logic?.Label) ? "placed prop" : element.Logic.Label, _ => element.TileId };
	private static Vector2[] BuildDiamond(float halfWidth, float halfHeight) => new[] { new Vector2(0f, -halfHeight), new Vector2(halfWidth, 0f), new Vector2(0f, halfHeight), new Vector2(-halfWidth, 0f) };
	private static void ClearLayer(Node layer)
	{
		foreach (Node child in layer.GetChildren()) { layer.RemoveChild(child); child.QueueFree(); }
	}
}
