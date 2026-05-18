using System.Collections.Generic;

public class MissionDefinition
{
	public string MissionID { get; set; } = string.Empty;
	public string ScenePath { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string DefaultReturnScenePath { get; set; } = "res://exploration_battle.tscn";
	public int RecommendedOfficerCount { get; set; } = 2;
}

public class MissionReward
{
	public int RawMaterials { get; set; }
	public int EnergyCores { get; set; }
	public int AncientTech { get; set; }
	public List<string> FleetItemIds { get; set; } = new List<string>();
	public List<string> OfficerItemIds { get; set; } = new List<string>();
	public List<string> CodexEntryIds { get; set; } = new List<string>();
}

public class MissionOutcome
{
	public string MissionID { get; set; } = string.Empty;
	public string OutcomeID { get; set; } = string.Empty;
	public bool IsSuccess { get; set; } = true;
	public MissionReward Reward { get; set; } = new MissionReward();
	public int PopulationSaved { get; set; }
	public Dictionary<string, int> ApprovalChanges { get; set; } = new Dictionary<string, int>();
	public List<string> FlagsToSet { get; set; } = new List<string>();
	public List<string> FallenOfficerShipNames { get; set; } = new List<string>();
}
