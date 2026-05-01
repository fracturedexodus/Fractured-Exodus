using Godot;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class MissionRoomBuilder : Node
{
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

	public void BuildRoom()
	{
		Node2D floorLayer = GetNodeOrNull<Node2D>(FloorLayerPath);
		Node2D wallLayer = GetNodeOrNull<Node2D>(WallLayerPath);
		Node2D propLayer = GetNodeOrNull<Node2D>(PropLayerPath);
		if (TilesetTexture == null || floorLayer == null || wallLayer == null || propLayer == null)
		{
			return;
		}

		ClearLayer(floorLayer);
		ClearLayer(wallLayer);
		ClearLayer(propLayer);
		_markerPositions.Clear();

		if (!BuildFromSavedLayout(floorLayer, wallLayer, propLayer))
		{
			BuildLayer(FloorRows, MissionTileCatalog.FloorTiles.ToDictionary(def => def.Symbol), floorLayer, "Floor");
			BuildLayer(WallRows, MissionTileCatalog.WallTiles.ToDictionary(def => def.Symbol), wallLayer, "Wall");
			BuildLayer(PropRows, MissionTileCatalog.PropTiles.ToDictionary(def => def.Symbol), propLayer, "Prop");
		}
	}

	private bool BuildFromSavedLayout(Node2D floorLayer, Node2D wallLayer, Node2D propLayer)
	{
		string resourcePath = LayoutResourcePath?.Trim();
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

			if (itemType == "marker" || !string.IsNullOrEmpty(markerId))
			{
				_markerPositions[markerId] = GetCellWorldPosition(column, row, new Vector2(offsetX, offsetY));
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
			targetLayer.AddChild(CreateSprite(definition, column, row, $"{definition.Category}_{column}_{row}_{definition.Id}", new Vector2(offsetX, offsetY), rotationDegrees));
			placedAnyTile = true;
		}

		return placedAnyTile;
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

				targetLayer.AddChild(CreateSprite(definition, column, row, $"{prefix}_{column}_{row}_{symbol}", Vector2.Zero, 0f));
			}
		}
	}

	private Sprite2D CreateSprite(MissionTileDefinition definition, int column, int row, string name, Vector2 extraOffset, float rotationDegrees)
	{
		bool isFloor = definition.Category == MissionTileCategory.Floor;
		Sprite2D sprite = new Sprite2D
		{
			Name = name,
			Texture = isFloor ? MissionFloorTextureFactory.GetTexture(definition.Id) : TilesetTexture,
			RegionEnabled = !isFloor,
			RegionRect = definition.Region,
			Position = GetCellWorldPosition(column, row, definition.Offset + extraOffset),
			Scale = isFloor ? Vector2.One : definition.Scale,
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
}
