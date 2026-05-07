using Godot;

public class PropInteractionContext
{
	public MissionMap MissionMap { get; set; }
	public GlobalData GlobalData { get; set; }
	public MissionRuntimeState MissionState { get; set; }
	public MissionTemplate MissionTemplate { get; set; }
	public DialogueUI DialogueUI { get; set; }
	public OfficerPawn Officer { get; set; }
	public Vector2I TargetCell { get; set; } = Vector2I.Zero;
	public string PropInstanceId { get; set; } = string.Empty;
	public string SourceInteractionKey { get; set; } = string.Empty;
	public string NpcPortraitPath { get; set; } = string.Empty;
}
