using Godot;
using System.Collections.Generic;
using System.Linq;

public enum MissionTileCategory
{
	Floor,
	Wall,
	Prop
}

public sealed class MissionTileDefinition
{
	public string Id { get; }
	public string DisplayName { get; }
	public MissionTileCategory Category { get; }
	public char Symbol { get; }
	public Rect2 Region { get; }
	public Vector2 Offset { get; }
	public Vector2 Scale { get; }
	public string TexturePath { get; }

	public MissionTileDefinition(string id, string displayName, MissionTileCategory category, char symbol, Rect2 region, Vector2 offset, Vector2 scale, string texturePath = "")
	{
		Id = id;
		DisplayName = displayName;
		Category = category;
		Symbol = symbol;
		Region = region;
		Offset = offset;
		Scale = scale;
		TexturePath = texturePath;
	}
}

public static class MissionTileCatalog
{
	private const string RemadeRoot = "res://Assets/Missions/BlackSiteRelay/RemadeOriginal226/";
	private const float TileHalfWidth = 113f;
	private static readonly Vector2 StraightWallRunOffset = new Vector2(-50f, -88f);

	private static readonly List<MissionTileDefinition> Definitions = new List<MissionTileDefinition>
	{
		new MissionTileDefinition("floor_standard", "Floor Standard", MissionTileCategory.Floor, 'f', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, RemadeRoot + "floors/floor_standard_226.png"),
		new MissionTileDefinition("floor_grate", "Floor Grate", MissionTileCategory.Floor, 'g', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, RemadeRoot + "floors/floor_grate_226.png"),
		new MissionTileDefinition("floor_hazard", "Floor Hazard", MissionTileCategory.Floor, 'h', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, RemadeRoot + "floors/floor_hazard_226.png"),
		new MissionTileDefinition("floor_panel", "Floor Vented", MissionTileCategory.Floor, 'v', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, RemadeRoot + "floors/floor_vented_226.png"),
		new MissionTileDefinition("floor_plating", "Floor Plating", MissionTileCategory.Floor, 'p', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, RemadeRoot + "floors/floor_plating_226.png"),
		new MissionTileDefinition("floor_compact_panel", "Floor Compact Panel", MissionTileCategory.Floor, 'q', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, RemadeRoot + "floors/floor_compact_panel_226.png"),
		new MissionTileDefinition("floor_glow", "Floor Relay Glow", MissionTileCategory.Floor, 't', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, RemadeRoot + "floors/floor_relay_glow_226.png"),
		new MissionTileDefinition("wall_corner_left", "Wall Corner Left", MissionTileCategory.Wall, 'l', new Rect2(0, 0, 175, 151), AnchorLocalToCell(175f, 151f, 17f, 150f, -TileHalfWidth, 0f), Vector2.One, RemadeRoot + "objects/wall_corner_left.png"),
		new MissionTileDefinition("wall_straight_left", "Wall Panel Left", MissionTileCategory.Wall, 'w', new Rect2(0, 0, 166, 178), StraightWallRunOffset, Vector2.One, RemadeRoot + "objects/wall_panel_left.png"),
		new MissionTileDefinition("wall_window_left", "Window Wall Left", MissionTileCategory.Wall, 'n', new Rect2(0, 0, 161, 178), StraightWallRunOffset, Vector2.One, RemadeRoot + "objects/wall_window_left.png"),
		new MissionTileDefinition("wall_panel_mid_a", "Wall Panel Mid A", MissionTileCategory.Wall, 'u', new Rect2(0, 0, 155, 173), StraightWallRunOffset, Vector2.One, RemadeRoot + "objects/wall_panel_mid_a.png"),
		new MissionTileDefinition("wall_straight_center", "Wall Panel Mid B", MissionTileCategory.Wall, 'x', new Rect2(0, 0, 148, 175), StraightWallRunOffset, Vector2.One, RemadeRoot + "objects/wall_panel_mid_b.png"),
		new MissionTileDefinition("wall_straight_right", "Wall Panel Right", MissionTileCategory.Wall, 'y', new Rect2(0, 0, 157, 174), StraightWallRunOffset, Vector2.One, RemadeRoot + "objects/wall_panel_right.png"),
		new MissionTileDefinition("wall_corner_ne", "Wall Corner NE", MissionTileCategory.Wall, 'e', new Rect2(0, 0, 155, 139), AnchorLocalToCell(155f, 139f, 77f, 138f, 0f, 0f), Vector2.One, RemadeRoot + "objects/wall_corner_ne.png"),
		new MissionTileDefinition("wall_corner_center", "Wall Corner Center", MissionTileCategory.Wall, 'b', new Rect2(0, 0, 191, 156), AnchorLocalToCell(191f, 156f, 17f, 155f, -TileHalfWidth, 0f), Vector2.One, RemadeRoot + "objects/wall_corner_center.png"),
		new MissionTileDefinition("wall_corner_right", "Wall Corner Right", MissionTileCategory.Wall, 'r', new Rect2(0, 0, 192, 154), AnchorLocalToCell(192f, 154f, 174f, 153f, TileHalfWidth, 0f), Vector2.One, RemadeRoot + "objects/wall_corner_right.png"),
		new MissionTileDefinition("door_survivors", "Door Survivors", MissionTileCategory.Prop, 'd', new Rect2(0, 0, 281, 174), AnchorLocalToCell(281f, 174f, 140f, 173f, 0f, 0f, 0.86f), new Vector2(0.86f, 0.86f), RemadeRoot + "objects/door_survivors.png"),
		new MissionTileDefinition("door_archive", "Door Archive", MissionTileCategory.Prop, 'D', new Rect2(0, 0, 280, 174), AnchorLocalToCell(280f, 174f, 140f, 173f, 0f, 0f, 0.86f), new Vector2(0.86f, 0.86f), RemadeRoot + "objects/door_archive.png"),
		new MissionTileDefinition("console_survivor", "Console Single", MissionTileCategory.Prop, 's', new Rect2(0, 0, 171, 167), new Vector2(-24f, -28f), Vector2.One, RemadeRoot + "objects/console_single.png"),
		new MissionTileDefinition("medical_station", "Medical Bed", MissionTileCategory.Prop, 'm', new Rect2(0, 0, 205, 188), new Vector2(34f, -8f), Vector2.One, RemadeRoot + "objects/medical_bed.png"),
		new MissionTileDefinition("crate_survivor", "Crate Small", MissionTileCategory.Prop, 'c', new Rect2(0, 0, 135, 130), new Vector2(-48f, -8f), Vector2.One, RemadeRoot + "objects/crate_small.png"),
		new MissionTileDefinition("crate_medium", "Crate Medium", MissionTileCategory.Prop, 'B', new Rect2(0, 0, 145, 134), new Vector2(0f, -8f), Vector2.One, RemadeRoot + "objects/crate_medium.png"),
		new MissionTileDefinition("console_archive", "Console Triple", MissionTileCategory.Prop, 'a', new Rect2(0, 0, 208, 169), new Vector2(24f, -28f), Vector2.One, RemadeRoot + "objects/console_triple.png"),
		new MissionTileDefinition("archive_core", "Archive Core", MissionTileCategory.Prop, 'o', new Rect2(0, 0, 185, 206), new Vector2(0f, -24f), Vector2.One, RemadeRoot + "objects/archive_core.png"),
		new MissionTileDefinition("crate_archive", "Crate Grate", MissionTileCategory.Prop, 'C', new Rect2(0, 0, 147, 139), new Vector2(52f, -8f), Vector2.One, RemadeRoot + "objects/crate_grate.png"),
		new MissionTileDefinition("pipe_bundle", "Pipe Elbow", MissionTileCategory.Prop, 'i', new Rect2(0, 0, 159, 145), new Vector2(-18f, 22f), Vector2.One, RemadeRoot + "objects/pipe_elbow.png"),
		new MissionTileDefinition("pipe_tank", "Pipe Tank", MissionTileCategory.Prop, 'I', new Rect2(0, 0, 162, 124), new Vector2(-18f, 22f), Vector2.One, RemadeRoot + "objects/pipe_tank.png"),
		new MissionTileDefinition("console_power", "Console Power", MissionTileCategory.Prop, 'P', new Rect2(0, 0, 128, 161), new Vector2(54f, 18f), Vector2.One, RemadeRoot + "objects/console_power.png"),
		new MissionTileDefinition("debris_left", "Debris Slab", MissionTileCategory.Prop, 'j', new Rect2(0, 0, 151, 165), new Vector2(-72f, 18f), Vector2.One, RemadeRoot + "objects/debris_slab.png"),
		new MissionTileDefinition("debris_right", "Debris Wreck", MissionTileCategory.Prop, 'k', new Rect2(0, 0, 215, 148), new Vector2(86f, 18f), Vector2.One, RemadeRoot + "objects/debris_wreck.png")
	};

	public static IReadOnlyList<MissionTileDefinition> All => Definitions;
	public static IEnumerable<MissionTileDefinition> FloorTiles => Definitions.Where(def => def.Category == MissionTileCategory.Floor);
	public static IEnumerable<MissionTileDefinition> WallTiles => Definitions.Where(def => def.Category == MissionTileCategory.Wall);
	public static IEnumerable<MissionTileDefinition> PropTiles => Definitions.Where(def => def.Category == MissionTileCategory.Prop);

	private static Vector2 AnchorLocalToCell(float textureWidth, float textureHeight, float localX, float localY, float targetX, float targetY, float scale = 1f)
	{
		return new Vector2(
			targetX - ((localX - (textureWidth * 0.5f)) * scale),
			targetY - ((localY - (textureHeight * 0.5f)) * scale));
	}

	public static bool TryGetById(string id, out MissionTileDefinition definition)
	{
		definition = Definitions.FirstOrDefault(def => def.Id == id);
		return definition != null;
	}

	public static bool TryGetBySymbol(MissionTileCategory category, char symbol, out MissionTileDefinition definition)
	{
		definition = Definitions.FirstOrDefault(def => def.Category == category && def.Symbol == symbol);
		return definition != null;
	}
}
