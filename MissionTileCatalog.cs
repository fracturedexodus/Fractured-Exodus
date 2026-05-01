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

	public MissionTileDefinition(string id, string displayName, MissionTileCategory category, char symbol, Rect2 region, Vector2 offset, Vector2 scale)
	{
		Id = id;
		DisplayName = displayName;
		Category = category;
		Symbol = symbol;
		Region = region;
		Offset = offset;
		Scale = scale;
	}
}

public static class MissionTileCatalog
{
	private static readonly List<MissionTileDefinition> Definitions = new List<MissionTileDefinition>
	{
		new MissionTileDefinition("floor_standard", "Floor Standard", MissionTileCategory.Floor, 'f', new Rect2(31, 31, 222, 129), Vector2.Zero, Vector2.One),
		new MissionTileDefinition("floor_grate", "Floor Grate", MissionTileCategory.Floor, 'g', new Rect2(267, 31, 219, 128), Vector2.Zero, new Vector2(1.014f, 1.008f)),
		new MissionTileDefinition("floor_hazard", "Floor Hazard", MissionTileCategory.Floor, 'h', new Rect2(718, 32, 212, 126), Vector2.Zero, new Vector2(1.047f, 1.024f)),
		new MissionTileDefinition("floor_panel", "Floor Panel", MissionTileCategory.Floor, 'v', new Rect2(941, 33, 203, 124), Vector2.Zero, new Vector2(1.094f, 1.040f)),
		new MissionTileDefinition("floor_plating", "Floor Plating", MissionTileCategory.Floor, 'p', new Rect2(496, 31, 211, 127), Vector2.Zero, new Vector2(1.052f, 1.016f)),
		new MissionTileDefinition("floor_glow", "Floor Glow", MissionTileCategory.Floor, 't', new Rect2(1350, 42, 168, 108), Vector2.Zero, new Vector2(1.321f, 1.194f)),
		new MissionTileDefinition("wall_corner_left", "Wall Corner Left", MissionTileCategory.Wall, 'l', new Rect2(54, 384, 175, 151), new Vector2(0f, -140f), Vector2.One),
		new MissionTileDefinition("wall_straight_left", "Wall Straight Left", MissionTileCategory.Wall, 'w', new Rect2(57, 179, 166, 178), new Vector2(0f, -156f), Vector2.One),
		new MissionTileDefinition("wall_straight_center", "Wall Straight Center", MissionTileCategory.Wall, 'x', new Rect2(769, 186, 148, 175), new Vector2(0f, -154f), Vector2.One),
		new MissionTileDefinition("wall_straight_right", "Wall Straight Right", MissionTileCategory.Wall, 'y', new Rect2(991, 183, 157, 174), new Vector2(0f, -156f), Vector2.One),
		new MissionTileDefinition("wall_corner_right", "Wall Corner Right", MissionTileCategory.Wall, 'r', new Rect2(521, 388, 192, 154), new Vector2(0f, -142f), Vector2.One),
		new MissionTileDefinition("door_survivors", "Door Survivors", MissionTileCategory.Prop, 'd', new Rect2(812, 391, 281, 174), new Vector2(0f, -124f), new Vector2(0.86f, 0.86f)),
		new MissionTileDefinition("door_archive", "Door Archive", MissionTileCategory.Prop, 'D', new Rect2(1165, 391, 280, 174), new Vector2(0f, -124f), new Vector2(0.86f, 0.86f)),
		new MissionTileDefinition("console_survivor", "Console Survivor", MissionTileCategory.Prop, 's', new Rect2(30, 588, 171, 167), new Vector2(-24f, -28f), Vector2.One),
		new MissionTileDefinition("medical_station", "Medical Station", MissionTileCategory.Prop, 'm', new Rect2(935, 776, 205, 188), new Vector2(34f, -8f), Vector2.One),
		new MissionTileDefinition("crate_survivor", "Crate Survivor", MissionTileCategory.Prop, 'c', new Rect2(785, 612, 135, 130), new Vector2(-48f, -8f), Vector2.One),
		new MissionTileDefinition("console_archive", "Console Archive", MissionTileCategory.Prop, 'a', new Rect2(263, 587, 208, 169), new Vector2(24f, -28f), Vector2.One),
		new MissionTileDefinition("archive_core", "Archive Core", MissionTileCategory.Prop, 'o', new Rect2(1214, 771, 185, 206), new Vector2(0f, -24f), Vector2.One),
		new MissionTileDefinition("crate_archive", "Crate Archive", MissionTileCategory.Prop, 'C', new Rect2(1236, 611, 147, 139), new Vector2(52f, -8f), Vector2.One),
		new MissionTileDefinition("pipe_bundle", "Pipe Bundle", MissionTileCategory.Prop, 'i', new Rect2(52, 795, 159, 145), new Vector2(-18f, 22f), Vector2.One),
		new MissionTileDefinition("console_power", "Console Power", MissionTileCategory.Prop, 'P', new Rect2(543, 584, 128, 161), new Vector2(54f, 18f), Vector2.One),
		new MissionTileDefinition("debris_left", "Debris Left", MissionTileCategory.Prop, 'j', new Rect2(478, 790, 151, 165), new Vector2(-72f, 18f), Vector2.One),
		new MissionTileDefinition("debris_right", "Debris Right", MissionTileCategory.Prop, 'k', new Rect2(669, 799, 215, 148), new Vector2(86f, 18f), Vector2.One)
	};

	public static IReadOnlyList<MissionTileDefinition> All => Definitions;
	public static IEnumerable<MissionTileDefinition> FloorTiles => Definitions.Where(def => def.Category == MissionTileCategory.Floor);
	public static IEnumerable<MissionTileDefinition> WallTiles => Definitions.Where(def => def.Category == MissionTileCategory.Wall);
	public static IEnumerable<MissionTileDefinition> PropTiles => Definitions.Where(def => def.Category == MissionTileCategory.Prop);

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
