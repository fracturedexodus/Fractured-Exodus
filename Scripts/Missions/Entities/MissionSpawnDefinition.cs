using Godot;

[GlobalClass]
public partial class MissionSpawnDefinition : Resource
{
	[Export] public string SpawnId { get; set; } = string.Empty;
	[Export] public string MarkerId { get; set; } = string.Empty;
	[Export] public MissionActorType ActorType { get; set; } = MissionActorType.PlayerOfficer;
	[Export(PropertyHint.Range, "0,7,1")] public int OfficerSlotIndex { get; set; } = 0;
	[Export(PropertyHint.File, "*.tres,*.res")] public string NpcDefinitionPath { get; set; } = string.Empty;
	[Export] public Vector2I FallbackCell { get; set; } = Vector2I.Zero;
	[Export] public Godot.Collections.Array<string> RequiredFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export] public Godot.Collections.Array<string> BlockedFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export(PropertyHint.MultilineText)] public string Notes { get; set; } = string.Empty;
}
