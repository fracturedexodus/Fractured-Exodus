using System.Collections.Generic;

public class DialogueConversationData
{
	public string ConversationId { get; set; } = string.Empty;
	public List<DialogueNode> Nodes { get; set; } = new List<DialogueNode>();
}
