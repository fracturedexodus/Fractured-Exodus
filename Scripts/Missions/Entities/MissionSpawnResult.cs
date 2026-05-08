using Godot;

public sealed class MissionSpawnResult
{
	public MissionSpawnDefinition Definition { get; init; }
	public OfficerState Officer { get; init; }
	public MissionNpcDefinition NpcDefinition { get; init; }
	public Vector2I Cell { get; init; } = Vector2I.Zero;
	public bool UsedFallbackCell { get; init; }
}
