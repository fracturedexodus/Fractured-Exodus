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
	public bool VisibleInPalette { get; }

	public MissionTileDefinition(string id, string displayName, MissionTileCategory category, char symbol, Rect2 region, Vector2 offset, Vector2 scale, string texturePath = "", bool visibleInPalette = true)
	{
		Id = id;
		DisplayName = displayName;
		Category = category;
		Symbol = symbol;
		Region = region;
		Offset = offset;
		Scale = scale;
		TexturePath = texturePath;
		VisibleInPalette = visibleInPalette;
	}
}

public static class MissionTileCatalog
{
	private const string AlignedRoot = "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/";
	private static readonly Vector2 PropScale = new Vector2(0.54f, 0.54f);
	private static readonly Vector2 DoorScale = new Vector2(0.46f, 0.46f);

	private static readonly List<MissionTileDefinition> Definitions = new List<MissionTileDefinition>
	{
		new MissionTileDefinition("floor_standard", "Floor Standard", MissionTileCategory.Floor, 'f', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, AlignedRoot + "floors/floor_standard_226.png"),
		new MissionTileDefinition("floor_grate", "Floor Grate", MissionTileCategory.Floor, 'g', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, AlignedRoot + "floors/floor_grate_226.png"),
		new MissionTileDefinition("floor_hazard", "Floor Hazard", MissionTileCategory.Floor, 'h', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, AlignedRoot + "floors/floor_hazard_226.png"),
		new MissionTileDefinition("floor_panel", "Floor Vented", MissionTileCategory.Floor, 'v', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, AlignedRoot + "floors/floor_vented_226.png"),
		new MissionTileDefinition("floor_plating", "Floor Plating", MissionTileCategory.Floor, 'p', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, AlignedRoot + "floors/floor_plating_226.png"),
		new MissionTileDefinition("floor_compact_panel", "Floor Compact Panel", MissionTileCategory.Floor, 'q', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, AlignedRoot + "floors/floor_compact_panel_226.png"),
		new MissionTileDefinition("floor_glow", "Floor Relay Glow", MissionTileCategory.Floor, 't', new Rect2(0, 0, 226, 133), Vector2.Zero, Vector2.One, AlignedRoot + "floors/floor_relay_glow_226.png"),

		new MissionTileDefinition("wall_nw_panel", "Wall NW Panel", MissionTileCategory.Wall, 'w', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NW_panel_a.png"),
		new MissionTileDefinition("wall_ne_panel", "Wall NE Panel", MissionTileCategory.Wall, 'e', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NE_panel_a.png"),
		new MissionTileDefinition("wall_se_panel", "Wall SE Panel", MissionTileCategory.Wall, 's', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SE_panel_a.png"),
		new MissionTileDefinition("wall_sw_panel", "Wall SW Panel", MissionTileCategory.Wall, 'z', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SW_panel_a.png"),

		new MissionTileDefinition("wall_nw_window", "Window NW Wall", MissionTileCategory.Wall, 'n', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NW_window.png"),
		new MissionTileDefinition("wall_ne_window", "Window NE Wall", MissionTileCategory.Wall, 'N', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NE_window.png"),
		new MissionTileDefinition("wall_se_window", "Window SE Wall", MissionTileCategory.Wall, 'A', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SE_window.png"),
		new MissionTileDefinition("wall_sw_window", "Window SW Wall", MissionTileCategory.Wall, 'G', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SW_window.png"),
		new MissionTileDefinition("wall_nw_hazard", "Hazard NW Wall", MissionTileCategory.Wall, 'u', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NW_panel_a.png", false),
		new MissionTileDefinition("wall_ne_hazard", "Hazard NE Wall", MissionTileCategory.Wall, 'U', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NE_panel_a.png", false),
		new MissionTileDefinition("wall_se_hazard", "Hazard SE Wall", MissionTileCategory.Wall, 'H', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SE_panel_a.png", false),
		new MissionTileDefinition("wall_sw_hazard", "Hazard SW Wall", MissionTileCategory.Wall, 'J', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SW_panel_a.png", false),
		new MissionTileDefinition("wall_corner_north", "Wall Corner North", MissionTileCategory.Wall, 'b', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NE_panel_a.png", false),
		new MissionTileDefinition("wall_corner_east", "Wall Corner East", MissionTileCategory.Wall, 'r', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SE_panel_a.png", false),
		new MissionTileDefinition("wall_corner_south", "Wall Corner South", MissionTileCategory.Wall, 'l', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SW_panel_a.png", false),
		new MissionTileDefinition("wall_corner_west", "Wall Corner West", MissionTileCategory.Wall, 'o', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NW_panel_a.png", false),

		new MissionTileDefinition("wall_corner_left", "Wall Corner Left", MissionTileCategory.Wall, 'L', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NW_panel_a.png", false),
		new MissionTileDefinition("wall_straight_left", "Wall Panel Left", MissionTileCategory.Wall, 'W', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NW_panel_a.png", false),
		new MissionTileDefinition("wall_window_left", "Window Wall Left", MissionTileCategory.Wall, 'V', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NW_panel_a.png", false),
		new MissionTileDefinition("wall_panel_mid_a", "Wall Panel Mid A", MissionTileCategory.Wall, 'X', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NW_panel_a.png", false),
		new MissionTileDefinition("wall_straight_center", "Wall Panel Mid B", MissionTileCategory.Wall, 'Y', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NE_panel_a.png", false),
		new MissionTileDefinition("wall_straight_right", "Wall Panel Right", MissionTileCategory.Wall, 'Z', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NE_panel_a.png", false),
		new MissionTileDefinition("wall_corner_ne", "Wall Corner NE", MissionTileCategory.Wall, 'E', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_NE_panel_a.png", false),
		new MissionTileDefinition("wall_corner_center", "Wall Corner Center", MissionTileCategory.Wall, 'B', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SE_panel_a.png", false),
		new MissionTileDefinition("wall_corner_right", "Wall Corner Right", MissionTileCategory.Wall, 'R', new Rect2(0, 0, 226, 360), Vector2.Zero, Vector2.One, AlignedRoot + "walls/wall_SW_panel_a.png", false),

		new MissionTileDefinition("door_survivors", "Door Survivors", MissionTileCategory.Prop, 'd', new Rect2(0, 0, 532, 302), AnchorBottomCenter(532f, 302f, DoorScale.X), DoorScale, AlignedRoot + "props/door_survivors.png"),
		new MissionTileDefinition("door_archive", "Door Archive", MissionTileCategory.Prop, 'D', new Rect2(0, 0, 516, 302), AnchorBottomCenter(516f, 302f, DoorScale.X), DoorScale, AlignedRoot + "props/door_archive.png"),
		new MissionTileDefinition("console_survivor", "Console Single", MissionTileCategory.Prop, 'S', new Rect2(0, 0, 318, 311), AnchorBottomCenter(318f, 311f, PropScale.X), PropScale, AlignedRoot + "props/console_single.png"),
		new MissionTileDefinition("medical_station", "Medical Bed", MissionTileCategory.Prop, 'm', new Rect2(0, 0, 378, 346), AnchorBottomCenter(378f, 346f, PropScale.X), PropScale, AlignedRoot + "props/medical_bed.png"),
		new MissionTileDefinition("crate_survivor", "Crate Small", MissionTileCategory.Prop, 'c', new Rect2(0, 0, 251, 241), AnchorBottomCenter(251f, 241f, PropScale.X), PropScale, AlignedRoot + "props/crate_small.png"),
		new MissionTileDefinition("crate_medium", "Crate Medium", MissionTileCategory.Prop, 'M', new Rect2(0, 0, 269, 246), AnchorBottomCenter(269f, 246f, PropScale.X), PropScale, AlignedRoot + "props/crate_medium.png"),
		new MissionTileDefinition("console_archive", "Console Triple", MissionTileCategory.Prop, 'a', new Rect2(0, 0, 389, 309), AnchorBottomCenter(389f, 309f, PropScale.X), PropScale, AlignedRoot + "props/console_triple.png"),
		new MissionTileDefinition("archive_core", "Archive Core", MissionTileCategory.Prop, 'O', new Rect2(0, 0, 319, 358), AnchorBottomCenter(319f, 358f, PropScale.X), PropScale, AlignedRoot + "props/archive_core.png"),
		new MissionTileDefinition("crate_archive", "Crate Grate", MissionTileCategory.Prop, 'C', new Rect2(0, 0, 271, 250), AnchorBottomCenter(271f, 250f, PropScale.X), PropScale, AlignedRoot + "props/crate_grate.png"),
		new MissionTileDefinition("pipe_bundle", "Pipe Elbow", MissionTileCategory.Prop, 'i', new Rect2(0, 0, 291, 264), AnchorBottomCenter(291f, 264f, PropScale.X), PropScale, AlignedRoot + "props/pipe_elbow.png"),
		new MissionTileDefinition("pipe_tank", "Pipe Tank", MissionTileCategory.Prop, 'I', new Rect2(0, 0, 296, 227), AnchorBottomCenter(296f, 227f, PropScale.X), PropScale, AlignedRoot + "props/pipe_tank.png"),
		new MissionTileDefinition("console_power", "Console Power", MissionTileCategory.Prop, 'P', new Rect2(0, 0, 239, 299), AnchorBottomCenter(239f, 299f, PropScale.X), PropScale, AlignedRoot + "props/console_power.png"),
		new MissionTileDefinition("debris_left", "Debris Slab", MissionTileCategory.Prop, 'j', new Rect2(0, 0, 288, 300), AnchorBottomCenter(288f, 300f, PropScale.X), PropScale, AlignedRoot + "props/debris_slab.png"),
		new MissionTileDefinition("debris_right", "Debris Wreck", MissionTileCategory.Prop, 'k', new Rect2(0, 0, 390, 273), AnchorBottomCenter(390f, 273f, PropScale.X), PropScale, AlignedRoot + "props/debris_wreck.png")
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

	private static Vector2 AnchorBottomCenter(float textureWidth, float textureHeight, float scale = 1f)
	{
		return AnchorLocalToCell(textureWidth, textureHeight, textureWidth * 0.5f, textureHeight - 1f, 0f, 0f, scale);
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
