using Godot;
using System.Collections.Generic;
using System.Linq;

public enum MissionMarkerCategory
{
	Spawn,
	Objective,
	Trigger
}

public sealed class MissionMarkerDefinition
{
	public string Id { get; }
	public string DisplayName { get; }
	public MissionMarkerCategory Category { get; }
	public Color Color { get; }
	public Vector2 Offset { get; }

	public MissionMarkerDefinition(string id, string displayName, MissionMarkerCategory category, Color color, Vector2 offset)
	{
		Id = id;
		DisplayName = displayName;
		Category = category;
		Color = color;
		Offset = offset;
	}
}

public static class MissionMarkerCatalog
{
	private static readonly List<MissionMarkerDefinition> Definitions = new List<MissionMarkerDefinition>
	{
		new MissionMarkerDefinition("spawn_a", "Officer Spawn A", MissionMarkerCategory.Spawn, new Color(0.28f, 0.88f, 1.00f, 0.92f), Vector2.Zero),
		new MissionMarkerDefinition("spawn_b", "Officer Spawn B", MissionMarkerCategory.Spawn, new Color(1.00f, 0.72f, 0.24f, 0.92f), Vector2.Zero),
		new MissionMarkerDefinition("objective_survivors", "Objective: Survivors", MissionMarkerCategory.Objective, new Color(0.35f, 0.95f, 0.78f, 0.92f), new Vector2(0f, -36f)),
		new MissionMarkerDefinition("objective_power", "Objective: Power", MissionMarkerCategory.Objective, new Color(1.00f, 0.78f, 0.24f, 0.92f), new Vector2(0f, -36f)),
		new MissionMarkerDefinition("objective_archive", "Objective: Archive", MissionMarkerCategory.Objective, new Color(0.60f, 0.78f, 1.00f, 0.92f), new Vector2(0f, -36f)),
		new MissionMarkerDefinition("trigger_dialogue", "Trigger: Dialogue", MissionMarkerCategory.Trigger, new Color(0.80f, 0.48f, 1.00f, 0.92f), Vector2.Zero),
		new MissionMarkerDefinition("trigger_enemy", "Trigger: Enemy", MissionMarkerCategory.Trigger, new Color(1.00f, 0.25f, 0.18f, 0.92f), Vector2.Zero),
		new MissionMarkerDefinition("evac_zone", "Evac Zone", MissionMarkerCategory.Trigger, new Color(0.45f, 1.00f, 0.35f, 0.92f), Vector2.Zero)
	};

	public static IReadOnlyList<MissionMarkerDefinition> All => Definitions;

	public static bool TryGetById(string id, out MissionMarkerDefinition definition)
	{
		definition = Definitions.FirstOrDefault(def => def.Id == id);
		return definition != null;
	}
}
