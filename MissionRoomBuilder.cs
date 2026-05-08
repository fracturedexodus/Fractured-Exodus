using Godot;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class MissionRoomBuilder : Node
{
	private const string DefaultLayoutPath = "res://Data/MissionLayouts/black_site_relay_builder.json";

	public sealed class MarkerPlacement
	{
		public string MarkerId { get; init; } = string.Empty;
		public string TileId { get; init; } = string.Empty;
		public string LogicRole { get; init; } = string.Empty;
		public string PropDefinitionPath { get; init; } = string.Empty;
		public string NpcDefinitionPath { get; init; } = string.Empty;
		public string Label { get; init; } = string.Empty;
		public string TargetId { get; init; } = string.Empty;
		public string NpcPortraitPath { get; init; } = string.Empty;
		public string RequiredFlag { get; init; } = string.Empty;
		public string SetFlag { get; init; } = string.Empty;
		public string TriggerMode { get; init; } = "none";
		public bool OneShot { get; init; }
		public string Notes { get; init; } = string.Empty;
		public Vector2I Cell { get; init; } = Vector2I.Zero;
	}

	private sealed class PlacedTile
	{
		public string TileId { get; set; }
		public int Column { get; set; }
		public int Row { get; set; }
		public float OffsetX { get; set; }
		public float OffsetY { get; set; }
		public float RotationDegrees { get; set; }
	}

	private bool _rebuildNow;
	private readonly Dictionary<string, Vector2> _markerPositions = new Dictionary<string, Vector2>();
	private readonly Dictionary<string, Vector2I> _markerCells = new Dictionary<string, Vector2I>();
	private readonly Dictionary<string, Vector2I> _spawnMarkerCells = new Dictionary<string, Vector2I>();
	private readonly List<MarkerPlacement> _markerPlacements = new List<MarkerPlacement>();
	private readonly HashSet<Vector2I> _floorCells = new HashSet<Vector2I>();
	private readonly HashSet<string> _blockedTransitions = new HashSet<string>();
	private readonly Dictionary<string, MissionDoor2D> _doorsById = new Dictionary<string, MissionDoor2D>();
	private readonly Dictionary<Vector2I, string> _doorIdsByCell = new Dictionary<Vector2I, string>();
	private readonly HashSet<Vector2I> _closedDoorCells = new HashSet<Vector2I>();
	private string _selectedBackgroundId = MissionBackgroundCatalog.DefaultId;

	[Export] public Texture2D TilesetTexture { get; set; }
	[Export] public Vector2 Origin { get; set; } = new Vector2(0f, -18f);
	[Export] public Vector2 TileStep { get; set; } = new Vector2(226f, 133f);
	[Export] public NodePath FloorLayerPath { get; set; } = "../FloorLayer";
	[Export] public NodePath WallLayerPath { get; set; } = "../WallLayer";
	[Export] public NodePath PropLayerPath { get; set; } = "../PropLayer";
	[Export(PropertyHint.File, "*.json")] public string LayoutResourcePath { get; set; } = "";
	[Export] public bool BuildInEditor { get; set; } = true;
	[Export]
	public bool RebuildNow
	{
		get => _rebuildNow;
		set
		{
			_rebuildNow = false;
			if (value && IsInsideTree())
			{
				BuildRoom();
				NotifyPropertyListChanged();
			}
		}
	}

	[Export] public string[] FloorRows { get; set; } = System.Array.Empty<string>();
	[Export] public string[] WallRows { get; set; } = System.Array.Empty<string>();
	[Export] public string[] PropRows { get; set; } = System.Array.Empty<string>();

	public override void _Ready()
	{
		if (Engine.IsEditorHint() && !BuildInEditor)
		{
			return;
		}

		BuildRoom();
	}

	public Vector2 GetCellWorldPosition(int column, int row, Vector2 extraOffset)
	{
		return IsoGridHelper.GridToWorld(column, row, TileStep, Origin) + extraOffset;
	}

	public Vector2 GetCellWorldPosition(int column, int row)
	{
		return GetCellWorldPosition(column, row, Vector2.Zero);
	}

	public bool TryGetMarkerWorldPosition(string markerId, Vector2 extraOffset, out Vector2 position)
	{
		if (_markerPositions.TryGetValue(markerId, out Vector2 markerPosition))
		{
			position = markerPosition + extraOffset;
			return true;
		}

		position = Vector2.Zero;
		return false;
	}

	public bool TryGetMarkerCell(string markerId, out Vector2I cell)
	{
		return _markerCells.TryGetValue(markerId, out cell);
	}

	public bool TryGetSpawnCell(string spawnKey, out Vector2I cell)
	{
		if (_spawnMarkerCells.TryGetValue(spawnKey, out cell))
		{
			return true;
		}

		return _markerCells.TryGetValue(spawnKey, out cell);
	}

	public IReadOnlyList<MarkerPlacement> GetMarkerPlacements()
	{
		return _markerPlacements;
	}

	public IEnumerable<MarkerPlacement> GetInteractPlacementsAtCell(Vector2I cell)
	{
		return _markerPlacements.Where(placement => placement.Cell == cell);
	}

	public string GetSelectedBackgroundId()
	{
		return _selectedBackgroundId;
	}

	public IReadOnlyCollection<Vector2I> GetFloorCells()
	{
		return _floorCells;
	}

	public bool IsWalkableCell(Vector2I cell)
	{
		return _floorCells.Contains(cell) && !_closedDoorCells.Contains(cell);
	}

	public bool IsDoorOpen(string doorId)
	{
		return _doorsById.TryGetValue(doorId, out MissionDoor2D door) && door.IsOpen;
	}

	public bool TrySetDoorOpen(string doorId, bool open, bool animated = true)
	{
		if (!_doorsById.TryGetValue(doorId, out MissionDoor2D door))
		{
			return false;
		}

		door.SetOpen(open, animated);
		if (open)
		{
			_closedDoorCells.Remove(door.Cell);
		}
		else
		{
			_closedDoorCells.Add(door.Cell);
		}

		return true;
	}

	public bool TryGetDoorIdAtCell(Vector2I cell, out string doorId)
	{
		return _doorIdsByCell.TryGetValue(cell, out doorId);
	}

	public Vector2I GetNearestCell(Vector2 localPosition)
	{
		return IsoGridHelper.WorldToGrid(localPosition, TileStep, Origin);
	}

	public Vector2 GetRoomCenterWorldPosition()
	{
		if (_floorCells.Count == 0)
		{
			return Origin;
		}

		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minY = float.MaxValue;
		float maxY = float.MinValue;

		foreach (Vector2I cell in _floorCells)
		{
			Vector2 position = GetCellWorldPosition(cell.X, cell.Y);
			minX = Mathf.Min(minX, position.X);
			maxX = Mathf.Max(maxX, position.X);
			minY = Mathf.Min(minY, position.Y);
			maxY = Mathf.Max(maxY, position.Y);
		}

		return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
	}

	public bool TryGetPath(Vector2I startCell, Vector2I targetCell, out List<Vector2I> path)
	{
		path = new List<Vector2I>();
		if (!IsWalkableCell(startCell) || !IsWalkableCell(targetCell))
		{
			return false;
		}

		if (startCell == targetCell)
		{
			path.Add(startCell);
			return true;
		}

		Queue<Vector2I> frontier = new Queue<Vector2I>();
		Dictionary<Vector2I, Vector2I> cameFrom = new Dictionary<Vector2I, Vector2I>();
		frontier.Enqueue(startCell);
		cameFrom[startCell] = startCell;

		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (Vector2I direction in directions)
			{
				Vector2I next = current + direction;
				if (cameFrom.ContainsKey(next) || !IsWalkableCell(next) || IsTransitionBlocked(current, next))
				{
					continue;
				}

				cameFrom[next] = current;
				if (next == targetCell)
				{
					path = ReconstructPath(cameFrom, startCell, targetCell);
					return true;
				}

				frontier.Enqueue(next);
			}
		}

		return false;
	}

	public HashSet<Vector2I> GetReachableCells(Vector2I startCell, int maxSteps)
	{
		HashSet<Vector2I> reachable = new HashSet<Vector2I>();
		if (!IsWalkableCell(startCell) || maxSteps < 0)
		{
			return reachable;
		}

		Queue<(Vector2I Cell, int Steps)> frontier = new Queue<(Vector2I Cell, int Steps)>();
		frontier.Enqueue((startCell, 0));
		reachable.Add(startCell);

		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		while (frontier.Count > 0)
		{
			(Vector2I current, int steps) = frontier.Dequeue();
			if (steps >= maxSteps)
			{
				continue;
			}

			foreach (Vector2I direction in directions)
			{
				Vector2I next = current + direction;
				if (reachable.Contains(next) || !IsWalkableCell(next) || IsTransitionBlocked(current, next))
				{
					continue;
				}

				reachable.Add(next);
				frontier.Enqueue((next, steps + 1));
			}
		}

		return reachable;
	}

	public void BuildRoom()
	{
		Node2D floorLayer = GetNodeOrNull<Node2D>(FloorLayerPath);
		Node2D wallLayer = GetNodeOrNull<Node2D>(WallLayerPath);
		Node2D propLayer = GetNodeOrNull<Node2D>(PropLayerPath);
		if (floorLayer == null || wallLayer == null || propLayer == null)
		{
			return;
		}

		ClearLayer(floorLayer);
		ClearLayer(wallLayer);
		ClearLayer(propLayer);
		_markerPositions.Clear();
		_markerCells.Clear();
		_spawnMarkerCells.Clear();
		_markerPlacements.Clear();
		_floorCells.Clear();
		_blockedTransitions.Clear();
		_doorsById.Clear();
		_doorIdsByCell.Clear();
		_closedDoorCells.Clear();
		_selectedBackgroundId = MissionBackgroundCatalog.DefaultId;

		if (!BuildFromSavedLayout(floorLayer, wallLayer, propLayer))
		{
			BuildLayer(FloorRows, MissionTileCatalog.FloorTiles.ToDictionary(def => def.Symbol), floorLayer, "Floor");
			BuildLayer(WallRows, MissionTileCatalog.WallTiles.ToDictionary(def => def.Symbol), wallLayer, "Wall");
			BuildLayer(PropRows, MissionTileCatalog.PropTiles.ToDictionary(def => def.Symbol), propLayer, "Prop");
		}
	}

	private bool BuildFromSavedLayout(Node2D floorLayer, Node2D wallLayer, Node2D propLayer)
	{
		string resourcePath = GetEffectiveLayoutResourcePath();
		if (string.IsNullOrEmpty(resourcePath))
		{
			return false;
		}

		string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
		if (!FileAccess.FileExists(absolutePath))
		{
			return false;
		}

		using FileAccess file = FileAccess.Open(absolutePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return false;
		}

		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Array)
		{
			return false;
		}

		Godot.Collections.Array tiles = parsed.AsGodotArray();
		bool placedAnyTile = false;
		foreach (Variant tileVariant in tiles)
		{
			Godot.Collections.Dictionary tileDict = tileVariant.AsGodotDictionary();
			string itemType = tileDict.TryGetValue("item_type", out Variant itemTypeVariant) ? itemTypeVariant.AsString() : "tile";
			string markerId = tileDict.TryGetValue("marker_id", out Variant markerIdVariant) ? markerIdVariant.AsString() : "";
			string tileId = tileDict.TryGetValue("tile_id", out Variant tileIdVariant) ? tileIdVariant.AsString() : "";
			int column = tileDict.TryGetValue("column", out Variant columnVariant) ? columnVariant.AsInt32() : 0;
			int row = tileDict.TryGetValue("row", out Variant rowVariant) ? rowVariant.AsInt32() : 0;
			float offsetX = tileDict.TryGetValue("offset_x", out Variant offsetXVariant) ? offsetXVariant.AsSingle() : 0f;
			float offsetY = tileDict.TryGetValue("offset_y", out Variant offsetYVariant) ? offsetYVariant.AsSingle() : 0f;
			float rotationDegrees = tileDict.TryGetValue("rotation_degrees", out Variant rotationVariant) ? rotationVariant.AsSingle() : 0f;
			string logicRole = tileDict.TryGetValue("logic_role", out Variant logicRoleVariant) ? logicRoleVariant.AsString() : string.Empty;
			string logicLabel = tileDict.TryGetValue("logic_label", out Variant logicLabelVariant) ? logicLabelVariant.AsString() : string.Empty;
			string logicTargetId = tileDict.TryGetValue("logic_target_id", out Variant logicTargetVariant) ? logicTargetVariant.AsString() : string.Empty;
			string logicNpcPortrait = tileDict.TryGetValue("logic_npc_portrait", out Variant logicPortraitVariant) ? logicPortraitVariant.AsString() : string.Empty;
			string logicRequiredFlag = tileDict.TryGetValue("logic_required_flag", out Variant logicRequiredVariant) ? logicRequiredVariant.AsString() : string.Empty;
			string logicSetFlag = tileDict.TryGetValue("logic_set_flag", out Variant logicSetVariant) ? logicSetVariant.AsString() : string.Empty;
			string logicTriggerMode = tileDict.TryGetValue("logic_trigger_mode", out Variant logicTriggerVariant) ? logicTriggerVariant.AsString() : "none";
			bool logicOnce = tileDict.TryGetValue("logic_once", out Variant logicOnceVariant) && logicOnceVariant.AsBool();
			string logicNotes = tileDict.TryGetValue("logic_notes", out Variant logicNotesVariant) ? logicNotesVariant.AsString() : string.Empty;
			string propDefinitionPath = tileDict.TryGetValue("prop_definition_path", out Variant propDefinitionVariant) ? propDefinitionVariant.AsString() : string.Empty;
			string npcDefinitionPath = tileDict.TryGetValue("npc_definition_path", out Variant npcDefinitionVariant) ? npcDefinitionVariant.AsString() : string.Empty;

			if (itemType == "background")
			{
				_selectedBackgroundId = tileDict.TryGetValue("background_id", out Variant backgroundVariant)
					? backgroundVariant.AsString()
					: MissionBackgroundCatalog.DefaultId;
				continue;
			}

			if (itemType == "marker" || !string.IsNullOrEmpty(markerId))
			{
				_markerPositions[markerId] = GetCellWorldPosition(column, row, new Vector2(offsetX, offsetY));
				_markerCells[markerId] = new Vector2I(column, row);
				string spawnKey = string.IsNullOrEmpty(logicTargetId) ? markerId : logicTargetId;
				if ((markerId.StartsWith("spawn_") || markerId == "npc_spawn") && !string.IsNullOrWhiteSpace(spawnKey))
				{
					_spawnMarkerCells[spawnKey] = new Vector2I(column, row);
				}
				_markerPlacements.Add(new MarkerPlacement
				{
					MarkerId = markerId,
					LogicRole = "marker",
					PropDefinitionPath = propDefinitionPath,
					NpcDefinitionPath = npcDefinitionPath,
					Label = string.IsNullOrEmpty(logicLabel) ? markerId : logicLabel,
					TargetId = string.IsNullOrEmpty(logicTargetId) ? markerId : logicTargetId,
					NpcPortraitPath = logicNpcPortrait,
					RequiredFlag = logicRequiredFlag,
					SetFlag = logicSetFlag,
					TriggerMode = string.IsNullOrEmpty(logicTriggerMode) ? GetDefaultTriggerMode(markerId) : logicTriggerMode,
					OneShot = logicOnce || markerId.StartsWith("trigger_"),
					Notes = logicNotes,
					Cell = new Vector2I(column, row)
				});
				continue;
			}

			if (itemType == "placed_prop" || (string.IsNullOrEmpty(tileId) && !string.IsNullOrEmpty(propDefinitionPath)))
			{
				_markerPlacements.Add(new MarkerPlacement
				{
					LogicRole = string.IsNullOrEmpty(logicRole) ? "prop" : logicRole,
					PropDefinitionPath = propDefinitionPath,
					NpcDefinitionPath = npcDefinitionPath,
					Label = string.IsNullOrEmpty(logicLabel) ? GetPropDefinitionDisplayName(propDefinitionPath) : logicLabel,
					TargetId = logicTargetId,
					NpcPortraitPath = logicNpcPortrait,
					RequiredFlag = logicRequiredFlag,
					SetFlag = logicSetFlag,
					TriggerMode = string.IsNullOrEmpty(logicTriggerMode) ? "interact" : logicTriggerMode,
					OneShot = logicOnce,
					Notes = logicNotes,
					Cell = new Vector2I(column, row)
				});
				placedAnyTile = true;
				continue;
			}

			if (!MissionTileCatalog.TryGetById(tileId, out MissionTileDefinition definition))
			{
				continue;
			}

			Node2D targetLayer = definition.Category switch
			{
				MissionTileCategory.Floor => floorLayer,
				MissionTileCategory.Wall => wallLayer,
				_ => propLayer
			};
			RegisterTileCell(definition, column, row);
			Vector2I cell = new Vector2I(column, row);
			Vector2 extraOffset = new Vector2(offsetX, offsetY);
			if (logicRole == "door" && definition.Category == MissionTileCategory.Prop)
			{
				MissionDoor2D doorNode = CreateDoorNode(definition, cell, extraOffset, logicTargetId, rotationDegrees);
				targetLayer.AddChild(doorNode);
				_doorsById[logicTargetId] = doorNode;
				_doorIdsByCell[cell] = logicTargetId;
				_closedDoorCells.Add(cell);
			}
			else
			{
				Sprite2D sprite = CreateSprite(definition, column, row, $"{definition.Category}_{column}_{row}_{definition.Id}", extraOffset, rotationDegrees);
				sprite.SetMeta("logic_role", logicRole);
				sprite.SetMeta("logic_label", logicLabel);
				sprite.SetMeta("logic_target_id", logicTargetId);
				sprite.SetMeta("logic_npc_portrait", logicNpcPortrait);
				sprite.SetMeta("logic_required_flag", logicRequiredFlag);
				sprite.SetMeta("logic_set_flag", logicSetFlag);
				sprite.SetMeta("logic_trigger_mode", logicTriggerMode);
				sprite.SetMeta("logic_once", logicOnce);
				sprite.SetMeta("logic_notes", logicNotes);
				sprite.SetMeta("prop_definition_path", propDefinitionPath);
				sprite.SetMeta("npc_definition_path", npcDefinitionPath);
				targetLayer.AddChild(sprite);
			}

			if (!string.IsNullOrEmpty(logicRole))
			{
				_markerPlacements.Add(new MarkerPlacement
				{
					TileId = definition.Id,
					LogicRole = logicRole,
					PropDefinitionPath = propDefinitionPath,
					NpcDefinitionPath = npcDefinitionPath,
					Label = string.IsNullOrEmpty(logicLabel) ? definition.DisplayName : logicLabel,
					TargetId = logicTargetId,
					NpcPortraitPath = logicNpcPortrait,
					RequiredFlag = logicRequiredFlag,
					SetFlag = logicSetFlag,
					TriggerMode = string.IsNullOrEmpty(logicTriggerMode) ? "none" : logicTriggerMode,
					OneShot = logicOnce,
					Notes = logicNotes,
					Cell = cell
				});
			}
			placedAnyTile = true;
		}

		return placedAnyTile;
	}

	private string GetEffectiveLayoutResourcePath()
	{
		string resourcePath = LayoutResourcePath?.Trim();
		if (!string.IsNullOrEmpty(resourcePath))
		{
			return resourcePath;
		}

		return DefaultLayoutPath;
	}

	private static void ClearLayer(Node layer)
	{
		foreach (Node child in layer.GetChildren())
		{
			layer.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void BuildLayer(string[] rows, Dictionary<char, MissionTileDefinition> definitions, Node2D targetLayer, string prefix)
	{
		if (rows == null)
		{
			return;
		}

		for (int row = 0; row < rows.Length; row++)
		{
			string rowText = rows[row] ?? string.Empty;
			for (int column = 0; column < rowText.Length; column++)
			{
				char symbol = rowText[column];
				if (!definitions.TryGetValue(symbol, out MissionTileDefinition definition))
				{
					continue;
				}

				RegisterTileCell(definition, column, row);
				targetLayer.AddChild(CreateSprite(definition, column, row, $"{prefix}_{column}_{row}_{symbol}", Vector2.Zero, 0f));
			}
		}
	}

	private void RegisterTileCell(MissionTileDefinition definition, int column, int row)
	{
		Vector2I cell = new Vector2I(column, row);
		if (definition.Category == MissionTileCategory.Floor)
		{
			_floorCells.Add(cell);
			return;
		}

		if (definition.Category == MissionTileCategory.Wall)
		{
			RegisterBlockedTransition(definition.Id, cell);
		}
	}

	private void RegisterBlockedTransition(string tileId, Vector2I cell)
	{
		Vector2I neighbor = tileId switch
		{
			"wall_nw_panel" or "wall_nw_window" => cell + new Vector2I(-1, 0),
			"wall_ne_panel" or "wall_ne_window" => cell + new Vector2I(0, -1),
			"wall_se_panel" or "wall_se_window" => cell + new Vector2I(1, 0),
			"wall_sw_panel" or "wall_sw_window" => cell + new Vector2I(0, 1),
			_ => new Vector2I(int.MinValue, int.MinValue)
		};

		if (neighbor.X == int.MinValue)
		{
			return;
		}

		_blockedTransitions.Add(GetTransitionKey(cell, neighbor));
	}

	private bool IsTransitionBlocked(Vector2I fromCell, Vector2I toCell)
	{
		return _blockedTransitions.Contains(GetTransitionKey(fromCell, toCell));
	}

	private static string GetTransitionKey(Vector2I a, Vector2I b)
	{
		if (a.X < b.X || (a.X == b.X && a.Y <= b.Y))
		{
			return $"{a.X},{a.Y}|{b.X},{b.Y}";
		}

		return $"{b.X},{b.Y}|{a.X},{a.Y}";
	}

	private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I startCell, Vector2I targetCell)
	{
		List<Vector2I> path = new List<Vector2I>();
		Vector2I current = targetCell;
		path.Add(current);
		while (current != startCell)
		{
			current = cameFrom[current];
			path.Add(current);
		}

		path.Reverse();
		return path;
	}

	private static string GetDefaultTriggerMode(string markerId)
	{
		return markerId == "trigger_dialogue" ? "enter" : "none";
	}

	private static string GetPropDefinitionDisplayName(string path)
	{
		string fileName = System.IO.Path.GetFileNameWithoutExtension(path ?? string.Empty);
		return string.IsNullOrEmpty(fileName) ? "Prop" : fileName.Replace('_', ' ');
	}

	private Sprite2D CreateSprite(MissionTileDefinition definition, int column, int row, string name, Vector2 extraOffset, float rotationDegrees)
	{
		bool usesAtlasRegion = string.IsNullOrEmpty(definition.TexturePath) && definition.Category != MissionTileCategory.Floor;
		Sprite2D sprite = new Sprite2D
		{
			Name = name,
			Texture = GetTileTexture(definition),
			RegionEnabled = usesAtlasRegion,
			RegionRect = definition.Region,
			Position = GetCellWorldPosition(column, row, definition.Offset + extraOffset),
			Scale = definition.Scale,
			RotationDegrees = rotationDegrees
		};
		sprite.SetMeta("tile_id", definition.Id);
		sprite.SetMeta("column", column);
		sprite.SetMeta("row", row);
		sprite.SetMeta("offset_x", extraOffset.X);
		sprite.SetMeta("offset_y", extraOffset.Y);
		sprite.SetMeta("rotation_degrees", rotationDegrees);
		return sprite;
	}

	private MissionDoor2D CreateDoorNode(MissionTileDefinition definition, Vector2I cell, Vector2 extraOffset, string doorId, float rotationDegrees)
	{
		string resolvedTexturePath = ResolveDoorTexturePath(definition, rotationDegrees);
		MissionDoor2D doorNode = new MissionDoor2D
		{
			Name = $"Door_{cell.X}_{cell.Y}_{definition.Id}",
			RotationDegrees = 0f
		};
		doorNode.Configure(
			GD.Load<Texture2D>(resolvedTexturePath),
			definition.Scale,
			GetCellWorldPosition(cell.X, cell.Y, definition.Offset + extraOffset),
			string.IsNullOrEmpty(doorId) ? $"{definition.Id}_{cell.X}_{cell.Y}" : doorId,
			cell,
			false);
		return doorNode;
	}

	private static string ResolveDoorTexturePath(MissionTileDefinition definition, float rotationDegrees)
	{
		string basePath = definition.TexturePath;
		if (string.IsNullOrEmpty(basePath))
		{
			return basePath;
		}

		string suffix = GetDoorOrientationSuffix(rotationDegrees);
		return basePath.Replace("_nw_", $"_{suffix}_");
	}

	private static string GetDoorOrientationSuffix(float rotationDegrees)
	{
		int quarterTurns = Mathf.PosMod(Mathf.RoundToInt(rotationDegrees / 90f), 4);
		return quarterTurns switch
		{
			0 => "nw",
			1 => "ne",
			2 => "se",
			3 => "sw",
			_ => "nw"
		};
	}

	private Texture2D GetTileTexture(MissionTileDefinition definition)
	{
		if (definition.Category == MissionTileCategory.Floor)
		{
			return MissionFloorTextureFactory.GetTexture(definition.Id);
		}

		if (!string.IsNullOrEmpty(definition.TexturePath))
		{
			return GD.Load<Texture2D>(definition.TexturePath);
		}

		return TilesetTexture;
	}
}
