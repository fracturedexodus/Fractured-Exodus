using System.Collections.Generic;

public class AmbientEventDefinition
{
	public string EventId { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Region { get; set; } = string.Empty;
	public string MapDisplayName { get; set; } = string.Empty;
	public string MapDetails { get; set; } = string.Empty;
	public string MapSpritePath { get; set; } = string.Empty;
	public float MapScale { get; set; } = 0.09f;
	public float SpawnChance { get; set; } = 0.35f;
	public string StartNodeId { get; set; } = "Start";
	public List<AmbientEventNode> Nodes { get; set; } = new List<AmbientEventNode>();

	public AmbientEventNode GetNode(string nodeId)
	{
		return Nodes?.Find(node => node != null && node.NodeId == nodeId);
	}
}

public class AmbientEventNode
{
	public string NodeId { get; set; } = string.Empty;
	public string SpeakerName { get; set; } = string.Empty;
	public string Text { get; set; } = string.Empty;
	public List<AmbientEventChoice> Choices { get; set; } = new List<AmbientEventChoice>();
}

public class AmbientEventChoice
{
	public string ChoiceId { get; set; } = string.Empty;
	public string Text { get; set; } = string.Empty;
	public string NextNodeId { get; set; } = string.Empty;
	public bool CompleteEvent { get; set; }
	public int ActionCost { get; set; }
	public List<string> RequiredActingShips { get; set; } = new List<string>();
	public Dictionary<string, float> ResourceCosts { get; set; } = new Dictionary<string, float>();
	public AmbientEventEffects Effects { get; set; } = new AmbientEventEffects();
}

public class AmbientEventEffects
{
	public Dictionary<string, float> ResourceChanges { get; set; } = new Dictionary<string, float>();
	public Dictionary<string, int> RegionalCounterChanges { get; set; } = new Dictionary<string, int>();
	public List<string> SetFlags { get; set; } = new List<string>();
	public string OfficerDecisionTag { get; set; } = string.Empty;
	public string ResultText { get; set; } = string.Empty;
}
