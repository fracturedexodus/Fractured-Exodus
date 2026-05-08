using System.Collections.Generic;

public class DialogueOption
{
	public string Text { get; set; } = string.Empty;
	public string NextNodeId { get; set; } = "End";
	public List<string> RequiredFlags { get; set; } = new List<string>();
	public List<string> BlockedFlags { get; set; } = new List<string>();
	public List<string> SetFlags { get; set; } = new List<string>();
	public string QuestToTrigger { get; set; } = string.Empty;
}
