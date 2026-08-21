using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchV3
{
	private static readonly Vector2 GridOrigin = new(0f, -20f);
	private readonly Dictionary<string, Node2D> _nodesByElementId = new();
	private bool _isPanning;
	private bool _isDraggingElement;
	private Vector2 _dragStartMouse;
	private int _dragStartColumn;
	private int _dragStartRow;
	private bool _rectangleActive;
	private Vector2I _rectangleStart;
	private Vector2I _lastPaintCell = new(int.MinValue, int.MinValue);
	private Node2D _placementPreview;

	private void BuildMapChrome()
	{
		_mapChrome = new Control { Name = "MapChrome", MouseFilter = Control.MouseFilterEnum.Ignore };
		_mapChrome.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_uiLayer.AddChild(_mapChrome);

		PanelContainer left = new() { AnchorRight = 0f, AnchorBottom = 1f, OffsetLeft = 8, OffsetTop = 122, OffsetRight = 332, OffsetBottom = -146 };
		left.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.28f, .72f, .98f)));
		_mapChrome.AddChild(left);
		VBoxContainer palette = new();
		palette.AddThemeConstantOverride("separation", 7);
		left.AddChild(palette);
		palette.AddChild(Heading("Build Map", "Pick something visual, then place it on the map."));
		_paletteSearch = new LineEdit { PlaceholderText = "Search floors, doors, characters…" };
		_paletteSearch.TextChanged += _ => RefreshVisualPalette();
		palette.AddChild(_paletteSearch);
		_paletteCategory = new OptionButton();
		foreach (string category in new[] { "Everything", "Rooms", "Floors", "Walls", "Doors & Furniture", "Characters & Gameplay" }) _paletteCategory.AddItem(category);
		_paletteCategory.ItemSelected += _ => RefreshVisualPalette();
		palette.AddChild(_paletteCategory);
		ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		palette.AddChild(scroll);
		_paletteGrid = new FlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_paletteGrid.AddThemeConstantOverride("h_separation", 7);
		_paletteGrid.AddThemeConstantOverride("v_separation", 7);
		scroll.AddChild(_paletteGrid);

		PanelContainer tools = new() { AnchorLeft = 0f, AnchorTop = 1f, AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 338, OffsetTop = -206, OffsetRight = -426, OffsetBottom = -146 };
		tools.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.26f, .8f, .84f)));
		_mapChrome.AddChild(tools);
		HBoxContainer toolRow = new();
		toolRow.AddThemeConstantOverride("separation", 7);
		tools.AddChild(toolRow);
		toolRow.AddChild(Button("Cursor", () => SetMapTool(MapTool.Select), 88));
		toolRow.AddChild(Button("Paint", () => SetMapTool(MapTool.Paint), 82));
		toolRow.AddChild(Button("Rectangle", () => SetMapTool(MapTool.Rectangle), 105));
		toolRow.AddChild(Button("Eraser", () => SetMapTool(MapTool.Erase), 82));
		toolRow.AddChild(Button("Room Stamp", () => SetMapTool(MapTool.Room), 112));
		_toolHint = new Label { Text = "Cursor: select and drag objects", VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		toolRow.AddChild(_toolHint);
		toolRow.AddChild(Button("Frame Map", FrameDocument, 102));

		PanelContainer right = new() { AnchorLeft = 1f, AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = -420, OffsetTop = 122, OffsetRight = -8, OffsetBottom = -146 };
		right.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.78f, .48f, 1f)));
		_mapChrome.AddChild(right);
		VBoxContainer inspector = new();
		inspector.AddThemeConstantOverride("separation", 8);
		right.AddChild(inspector);
		inspector.AddChild(Heading("Selected Object", "Only choices that matter for this kind of object are shown."));
		ScrollContainer inspectorScroll = new()
		{
			Name = "InspectorScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		inspector.AddChild(inspectorScroll);
		_inspectorContent = new VBoxContainer
		{
			Name = "InspectorContent",
			CustomMinimumSize = new Vector2(360, 0),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_inspectorContent.AddThemeConstantOverride("separation", 7);
		inspectorScroll.AddChild(_inspectorContent);

		RefreshVisualPalette();
		SetMapTool(MapTool.Select);
	}

	private void SetMapTool(MapTool tool)
	{
		_mapTool = tool;
		if (tool != MapTool.Place && tool != MapTool.Paint && tool != MapTool.Rectangle) _placementAsset = string.Empty;
		_toolHint.Text = tool switch
		{
			MapTool.Select => "Cursor: select and drag objects",
			MapTool.Place => "Place: click a cell to add the selected item",
			MapTool.Paint => "Paint: hold and drag to paint the selected floor",
			MapTool.Rectangle => "Rectangle: drag to fill an area with the selected floor",
			MapTool.Erase => "Eraser: click objects to remove them",
			MapTool.Room => $"Room stamp: click to place {_roomStamp}",
			_ => string.Empty
		};
	}

	private void RefreshVisualPalette()
	{
		if (_paletteGrid == null) return;
		ClearChildren(_paletteGrid);
		string query = _paletteSearch?.Text?.Trim() ?? string.Empty;
		string category = _paletteCategory?.Selected >= 0 ? _paletteCategory.GetItemText(_paletteCategory.Selected) : "Everything";
		if (category is "Everything" or "Rooms")
		{
			foreach (string room in new[] { "Starter Room", "Control Room", "Medical Bay", "Long Corridor" })
				if (Matches(room, query)) AddPaletteCard(room, $"room:{room}", null, new Color(.28f, .67f, .76f));
		}
		foreach (MissionTileDefinition definition in MissionTileCatalog.FloorTiles.Where(item => item.VisibleInPalette))
			if (category is "Everything" or "Floors" && Matches(definition.DisplayName, query)) AddPaletteCard(definition.DisplayName, $"tile:{definition.Id}", definition.TexturePath, new Color(.25f, .55f, .72f));
		foreach (MissionTileDefinition definition in MissionTileCatalog.WallTiles.Where(item => item.VisibleInPalette))
			if (category is "Everything" or "Walls" && Matches(definition.DisplayName, query)) AddPaletteCard(definition.DisplayName, $"tile:{definition.Id}", definition.TexturePath, new Color(.39f, .49f, .68f));
		foreach (MissionTileDefinition definition in MissionTileCatalog.PropTiles.Where(item => item.VisibleInPalette))
			if (category is "Everything" or "Doors & Furniture" && Matches(definition.DisplayName, query)) AddPaletteCard(definition.DisplayName, $"tile:{definition.Id}", definition.TexturePath, new Color(.55f, .42f, .68f));
		if (category is "Everything" or "Characters & Gameplay")
		{
			foreach (MissionMarkerDefinition definition in MissionMarkerCatalog.All)
				if (Matches(definition.DisplayName, query)) AddPaletteCard(FriendlyMarkerName(definition), $"marker:{definition.Id}", null, definition.Color);
		}
	}

	private void AddPaletteCard(string name, string asset, string texturePath, Color tint)
	{
		Button card = new()
		{
			Text = name,
			TooltipText = $"Place {name}",
			CustomMinimumSize = new Vector2(142, 92),
			ClipText = true,
			ExpandIcon = true,
			VerticalIconAlignment = VerticalAlignment.Top
		};
		card.AddThemeConstantOverride("icon_max_width", 68);
		if (!string.IsNullOrWhiteSpace(texturePath)) card.Icon = ResourceLoader.Load<Texture2D>(texturePath);
		else card.AddThemeStyleboxOverride("normal", PanelStyle(tint, new Color(tint.R * .22f, tint.G * .22f, tint.B * .22f, 1f)));
		card.Pressed += () => SelectPaletteAsset(asset, name);
		_paletteGrid.AddChild(card);
	}

	private void SelectPaletteAsset(string asset, string name)
	{
		if (asset.StartsWith("room:"))
		{
			_roomStamp = asset[5..];
			SetMapTool(MapTool.Room);
		}
		else
		{
			_placementAsset = asset;
			bool floor = asset.StartsWith("tile:") && MissionTileCatalog.TryGetById(asset[5..], out MissionTileDefinition tile) && tile.Category == MissionTileCategory.Floor;
			SetMapTool(floor ? MapTool.Paint : MapTool.Place);
			_placementAsset = asset;
		}
		SetStatus($"{name} selected. Click the map to place it.");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			Control focusOwner = GetViewport().GuiGetFocusOwner();
			if (focusOwner is LineEdit or TextEdit or SpinBox) return;
			if (key.CtrlPressed && key.Keycode == Key.S) { SaveDocument(false); Handled(); return; }
			if (key.CtrlPressed && key.Keycode == Key.Z) { _store.Undo(); Handled(); return; }
			if (key.CtrlPressed && (key.Keycode == Key.Y || key.ShiftPressed && key.Keycode == Key.Z)) { _store.Redo(); Handled(); return; }
			if (key.Keycode == Key.Escape) { SetMapTool(MapTool.Select); Handled(); return; }
			if (_page == WorkspacePage.Map && key.Keycode == Key.Delete) { DeleteSelectedElement(); Handled(); return; }
		}
		if (_page != WorkspacePage.Map) return;
		if (@event is InputEventMouseButton mouse)
		{
			if (mouse.ButtonIndex == MouseButton.Middle)
			{
				if (mouse.Pressed && !IsMapScreenPosition(mouse.Position)) return;
				_isPanning = mouse.Pressed;
				_dragStartMouse = mouse.Position;
				Handled();
				return;
			}
			if (!IsMapScreenPosition(mouse.Position)) return;
			if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelUp) { SetZoom(_camera.Zoom.X + .08f); Handled(); return; }
			if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelDown) { SetZoom(_camera.Zoom.X - .08f); Handled(); return; }
			if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed) { SelectAtCell(GetMouseCell()); DeleteSelectedElement(); Handled(); return; }
			if (mouse.ButtonIndex == MouseButton.Left)
			{
				if (mouse.Pressed) BeginMapAction(); else EndMapAction();
				Handled();
				return;
			}
		}
		if (@event is InputEventMouseMotion motion)
		{
			if (_isPanning)
			{
				_camera.Position -= motion.Relative / Mathf.Max(_camera.Zoom.X, .01f);
				Handled();
			}
			else if (_mapTool == MapTool.Paint && Input.IsMouseButtonPressed(MouseButton.Left) && IsMapScreenPosition(motion.Position)) PaintAt(GetMouseCell());
		}
	}

	private void BeginMapAction()
	{
		Vector2I cell = GetMouseCell();
		_lastPaintCell = new Vector2I(int.MinValue, int.MinValue);
		switch (_mapTool)
		{
			case MapTool.Erase: SelectAtCell(cell); DeleteSelectedElement(); return;
			case MapTool.Room: PlaceRoomStamp(_roomStamp, cell); return;
			case MapTool.Rectangle: _rectangleActive = true; _rectangleStart = cell; return;
			case MapTool.Paint: PaintAt(cell); return;
			case MapTool.Place: PlaceAssetAt(_placementAsset, cell); return;
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
		if (_rectangleActive)
		{
			_rectangleActive = false;
			FillRectangle(_rectangleStart, GetMouseCell());
			return;
		}
		if (!_isDraggingElement) return;
		_isDraggingElement = false;
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null) return;
		Vector2I cell = GetMouseCell();
		if (cell.X != _dragStartColumn || cell.Y != _dragStartRow)
			_store.Execute(new MoveMissionElementCommand(selected.Id, _dragStartColumn, _dragStartRow, cell.X, cell.Y));
	}

	private void PaintAt(Vector2I cell)
	{
		if (cell == _lastPaintCell || string.IsNullOrWhiteSpace(_placementAsset)) return;
		_lastPaintCell = cell;
		PlaceAssetAt(_placementAsset, cell, true);
	}

	private void FillRectangle(Vector2I a, Vector2I b)
	{
		if (string.IsNullOrWhiteSpace(_placementAsset)) { SetStatus("Choose a floor before using Rectangle.", true); return; }
		int minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X), minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
		for (int x = minX; x <= maxX; x++) for (int y = minY; y <= maxY; y++) AddElementDirect(_placementAsset, new Vector2I(x, y), true);
		_store.NotifyExternalChange();
		SetStatus($"Painted a {maxX - minX + 1} × {maxY - minY + 1} floor area.");
	}

	private void PlaceAssetAt(string asset, Vector2I cell, bool replaceFloor = false)
	{
		MissionMapElement element = CreateElementForAsset(asset, cell);
		if (element == null) return;
		if (replaceFloor && IsFloor(element))
		{
			MissionMapElement old = _store.Document.Elements.FirstOrDefault(item => item.Column == cell.X && item.Row == cell.Y && IsFloor(item));
			if (old?.TileId == element.TileId) return;
			if (old != null) _store.Document.Elements.Remove(old);
		}
		_store.Execute(new AddMissionElementCommand(element));
		_store.Select(element.Id);
	}

	private void AddElementDirect(string asset, Vector2I cell, bool replaceFloor = false)
	{
		MissionMapElement element = CreateElementForAsset(asset, cell);
		if (element == null) return;
		if (replaceFloor && IsFloor(element)) _store.Document.Elements.RemoveAll(item => item.Column == cell.X && item.Row == cell.Y && IsFloor(item));
		_store.Document.Elements.Add(element);
	}

	private void PlaceRoomStamp(string kind, Vector2I origin)
	{
		(int width, int height) = kind == "Long Corridor" ? (9, 3) : kind == "Control Room" ? (7, 6) : (6, 6);
		string floor = kind == "Medical Bay" ? "floor_panel" : kind == "Control Room" ? "floor_glow" : "floor_standard";
		for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) AddElementDirect($"tile:{floor}", origin + new Vector2I(x, y), true);
		for (int x = 0; x < width; x++)
		{
			AddElementDirect("tile:wall_nw_panel", origin + new Vector2I(x, 0));
			AddElementDirect("tile:wall_se_panel", origin + new Vector2I(x, height - 1));
		}
		for (int y = 1; y < height - 1; y++)
		{
			AddElementDirect("tile:wall_sw_panel", origin + new Vector2I(0, y));
			AddElementDirect("tile:wall_ne_panel", origin + new Vector2I(width - 1, y));
		}
		if (kind == "Control Room") AddElementDirect("tile:console_power", origin + new Vector2I(width / 2, height / 2));
		if (kind == "Medical Bay") AddElementDirect("tile:medical_station", origin + new Vector2I(width / 2, height / 2));
		_store.NotifyExternalChange();
		SetStatus($"Placed {kind}. Walls were oriented automatically.");
	}

	private void ApplyStarterTemplate(string kind)
	{
		MissionDocument doc = _store.Document;
		if (doc == null) return;
		doc.Elements.Clear();
		doc.FlowNodes.Clear();
		doc.Authoring.Rules.Clear();
		PlaceRoomStampDirect(kind == "Combat Encounter" ? 10 : 8, kind == "Combat Encounter" ? 8 : 7);
		AddElementDirect("marker:spawn_a", new Vector2I(1, 1));
		AddElementDirect("marker:spawn_b", new Vector2I(2, 1));
		AddElementDirect("marker:evac_zone", new Vector2I(1, 5));
		string objective = kind switch { "Rescue Mission" => "objective_survivors", "Investigation" => "objective_archive", "Retrieve and Escape" => "objective_archive", _ => "objective_power" };
		AddElementDirect($"marker:{objective}", new Vector2I(6, 4));
		if (kind == "Combat Encounter")
		{
			AddElementDirect("marker:hostile_spawn", new Vector2I(6, 2));
			AddElementDirect("marker:hostile_spawn", new Vector2I(7, 4));
		}
		else if (kind == "Rescue Mission") AddElementDirect("marker:npc_spawn", new Vector2I(6, 3));
		else if (kind == "Investigation") AddElementDirect("tile:console_archive", new Vector2I(6, 3));
		doc.Authoring.TemplateKind = kind;
		FrameDocument();
	}

	private void PlaceRoomStampDirect(int width, int height)
	{
		for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) AddElementDirect("tile:floor_standard", new Vector2I(x, y), true);
		for (int x = 0; x < width; x++) { AddElementDirect("tile:wall_nw_panel", new Vector2I(x, 0)); AddElementDirect("tile:wall_se_panel", new Vector2I(x, height - 1)); }
		for (int y = 1; y < height - 1; y++) { AddElementDirect("tile:wall_sw_panel", new Vector2I(0, y)); AddElementDirect("tile:wall_ne_panel", new Vector2I(width - 1, y)); }
	}

	private MissionMapElement CreateElementForAsset(string asset, Vector2I cell)
	{
		string[] parts = (asset ?? string.Empty).Split(':', 2);
		if (parts.Length != 2) return null;
		MissionMapElement element = new() { Id = $"map_{Guid.NewGuid():N}", SourceOrder = NextSourceOrder(), Column = cell.X, Row = cell.Y, Logic = new MissionElementLogic() };
		if (parts[0] == "tile")
		{
			element.Kind = MissionMapElementKind.Tile;
			element.TileId = parts[1];
			if (parts[1].StartsWith("door_"))
			{
				element.Logic.Role = "door";
				element.Logic.Label = MissionTileCatalog.TryGetById(parts[1], out MissionTileDefinition door) ? door.DisplayName : "Door";
				element.Logic.TargetId = $"door_{Guid.NewGuid():N}";
			}
		}
		else if (parts[0] == "marker")
		{
			element.Kind = MissionMapElementKind.Marker;
			element.MarkerId = parts[1];
			element.Logic.Role = "marker";
			element.Logic.Label = MissionMarkerCatalog.TryGetById(parts[1], out MissionMarkerDefinition marker) ? FriendlyMarkerName(marker) : "Gameplay marker";
			element.Logic.TargetId = parts[1] is "npc_spawn" or "hostile_spawn" ? $"{parts[1]}_{Guid.NewGuid():N}" : parts[1];
			element.Logic.TriggerMode = parts[1].StartsWith("trigger_") || parts[1] == "evac_zone" ? "enter" : "none";
		}
		else return null;
		return element;
	}

	private void SelectAtCell(Vector2I cell)
	{
		MissionMapElement selected = _store.Document?.Elements.Where(e => e.Column == cell.X && e.Row == cell.Y).OrderByDescending(SelectionPriority).ThenByDescending(e => e.SourceOrder).FirstOrDefault();
		_store.Select(selected?.Id ?? string.Empty);
	}

	private void DeleteSelectedElement()
	{
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null) return;
		_store.Execute(new RemoveMissionElementCommand(selected));
		_store.Select(string.Empty);
		SetStatus($"Removed {DisplayName(selected)}.");
	}

	private void RefreshMap()
	{
		if (_floorLayer == null) return;
		ClearLayer(_floorLayer); ClearLayer(_wallLayer); ClearLayer(_propLayer); ClearLayer(_markerLayer);
		_nodesByElementId.Clear();
		foreach (MissionMapElement element in _store.Document?.Elements ?? new List<MissionMapElement>())
		{
			Node2D node = CreateElementNode(element);
			if (node == null) continue;
			LayerFor(element).AddChild(node);
			_nodesByElementId[element.Id] = node;
		}
		RefreshSelectionVisuals();
	}

	private Node2D CreateElementNode(MissionMapElement element)
	{
		Node2D root = new() { Name = element.Id, Position = CellToWorld(element.Column, element.Row) + new Vector2(element.OffsetX, element.OffsetY), ZIndex = (element.Column + element.Row) * 10 + SelectionPriority(element) };
		if (element.Kind == MissionMapElementKind.Marker)
		{
			Color color = MissionMarkerCatalog.TryGetById(element.MarkerId, out MissionMarkerDefinition marker) ? marker.Color : Colors.Magenta;
			root.AddChild(new Polygon2D { Polygon = Diamond(34, 18), Color = new Color(color.R, color.G, color.B, .84f) });
			Label label = new() { Text = DisplayName(element), Position = new Vector2(-82, -50), Size = new Vector2(164, 28), HorizontalAlignment = HorizontalAlignment.Center };
			label.AddThemeColorOverride("font_color", color);
			root.AddChild(label);
			return root;
		}
		Texture2D texture = null;
		Vector2 scale = Vector2.One, offset = Vector2.Zero;
		if (element.Kind == MissionMapElementKind.Tile && MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition definition))
		{
			texture = ResourceLoader.Load<Texture2D>(definition.TexturePath); scale = definition.Scale; offset = definition.Offset;
		}
		if (texture == null) root.AddChild(new Polygon2D { Polygon = Diamond(42, 24), Color = new Color(.85f, .34f, .75f, .8f) });
		else root.AddChild(new Sprite2D { Texture = texture, Position = offset, Scale = scale, RotationDegrees = element.RotationDegrees, FlipH = element.FlipH, FlipV = element.FlipV });
		return root;
	}

	private void RefreshSelectionVisuals()
	{
		foreach ((string id, Node2D node) in _nodesByElementId)
		{
			node.GetNodeOrNull("SelectionOutline")?.QueueFree();
			if (id != _store.SelectedElementId) continue;
			Line2D outline = new() { Name = "SelectionOutline", Width = 4, DefaultColor = new Color(.2f, .95f, 1f), Closed = true, ZIndex = 1000 };
			foreach (Vector2 point in Diamond(58, 32)) outline.AddPoint(point);
			node.AddChild(outline);
		}
	}

	private void UpdatePlacementPreview()
	{
		if (_placementPreview != null) { _placementPreview.QueueFree(); _placementPreview = null; }
		if (_mapTool is not (MapTool.Place or MapTool.Paint or MapTool.Rectangle or MapTool.Room) || !IsMapScreenPosition(GetViewport().GetMousePosition())) return;
		_placementPreview = new Node2D { Position = CellToWorld(GetMouseCell().X, GetMouseCell().Y), ZIndex = 9999 };
		_placementPreview.AddChild(new Polygon2D { Polygon = Diamond(72, 38), Color = new Color(.2f, .9f, 1f, .22f) });
		GetNode<Node2D>("World").AddChild(_placementPreview);
	}

	private void FrameDocument()
	{
		List<MissionMapElement> elements = _store.Document?.Elements ?? new List<MissionMapElement>();
		_camera.Position = elements.Count == 0 ? Vector2.Zero : elements.Aggregate(Vector2.Zero, (sum, e) => sum + CellToWorld(e.Column, e.Row)) / elements.Count;
		SetZoom(DefaultZoom);
	}

	private Vector2I GetMouseCell() => IsoGridHelper.WorldToGrid(GetGlobalMousePosition(), MissionFloorTextureFactory.TileSize, GridOrigin);
	private static Vector2 CellToWorld(int column, int row) => IsoGridHelper.GridToWorld(column, row, MissionFloorTextureFactory.TileSize, GridOrigin);
	private void SetZoom(float value) => _camera.Zoom = Vector2.One * Mathf.Clamp(value, MinZoom, MaxZoom);
	private bool IsMapScreenPosition(Vector2 p) { Vector2 s = GetViewportRect().Size; return p.X >= 338 && p.X <= s.X - 426 && p.Y >= 122 && p.Y <= s.Y - 212; }
	private int NextSourceOrder() => _store.Document?.Elements?.Count > 0 ? _store.Document.Elements.Max(e => e.SourceOrder) + 1 : 1;
	private void Handled() => GetViewport().SetInputAsHandled();
	private static bool Matches(string text, string query) => string.IsNullOrWhiteSpace(query) || text.Contains(query, StringComparison.OrdinalIgnoreCase);
	private static bool IsFloor(MissionMapElement e) => e.Kind == MissionMapElementKind.Tile && MissionTileCatalog.TryGetById(e.TileId, out MissionTileDefinition d) && d.Category == MissionTileCategory.Floor;
	private static int SelectionPriority(MissionMapElement e) => e.Kind switch { MissionMapElementKind.Marker => 4, MissionMapElementKind.PlacedProp => 3, _ => MissionTileCatalog.TryGetById(e.TileId, out MissionTileDefinition d) ? d.Category switch { MissionTileCategory.Prop => 3, MissionTileCategory.Wall => 2, _ => 1 } : 1 };
	private Node2D LayerFor(MissionMapElement e) => e.Kind switch { MissionMapElementKind.Marker => _markerLayer, MissionMapElementKind.PlacedProp => _propLayer, _ => MissionTileCatalog.TryGetById(e.TileId, out MissionTileDefinition d) ? d.Category switch { MissionTileCategory.Floor => _floorLayer, MissionTileCategory.Wall => _wallLayer, _ => _propLayer } : _propLayer };
	private static string DisplayName(MissionMapElement e) => !string.IsNullOrWhiteSpace(e.Logic?.Label) ? e.Logic.Label : e.Kind == MissionMapElementKind.Marker ? e.MarkerId : e.TileId;
	private static string FriendlyMarkerName(MissionMarkerDefinition marker) => marker.Id switch { "spawn_a" => "Officer Start 1", "spawn_b" => "Officer Start 2", "npc_spawn" => "Friendly Character", "hostile_spawn" => "Enemy", "trigger_dialogue" => "Conversation Area", "trigger_enemy" => "Enemy Ambush Area", "evac_zone" => "Evacuation Area", _ => marker.DisplayName.Replace("Objective:", "Goal:") };
	private static Vector2[] Diamond(float w, float h) => new[] { new Vector2(0, -h), new Vector2(w, 0), new Vector2(0, h), new Vector2(-w, 0) };
	private static void ClearLayer(Node layer) { foreach (Node child in layer.GetChildren()) { layer.RemoveChild(child); child.QueueFree(); } }
}
