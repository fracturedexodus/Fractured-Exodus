using System.Collections.Generic;

public class DialogueNode
{
	public string Id { get; set; } = string.Empty;
	public string SpeakerName { get; set; } = string.Empty;
	public string Text { get; set; } = string.Empty;
	public List<DialogueOption> Options { get; set; } = new List<DialogueOption>();
	public string QuestToTrigger { get; set; } = string.Empty;
	public List<string> RequiredFlags { get; set; } = new List<string>();
	public List<string> BlockedFlags { get; set; } = new List<string>();
	public List<string> SetFlags { get; set; } = new List<string>();
}
