using System.Collections.Generic;
using System.Text.Json.Serialization;

public enum OverworldContentType
{
	Mission,
	AmbientEvent
}

public enum OverworldPlacementNodeType
{
	Planet,
	Outpost,
	FreeHex
}

public enum OverworldUniqueScope
{
	Campaign,
	System
}

public sealed class OverworldContentDefinition
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public string ContentId { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public OverworldContentType ContentType { get; set; }
	public string ContentReference { get; set; } = string.Empty;
	public bool Enabled { get; set; } = true;
	public int Priority { get; set; }
	public OverworldPlacementRule Placement { get; set; } = new OverworldPlacementRule();

	[JsonIgnore]
	public string SourcePath { get; set; } = string.Empty;
}

public sealed class OverworldPlacementRule
{
	public OverworldPlacementNodeType NodeType { get; set; }
	public OverworldUniqueScope UniqueScope { get; set; } = OverworldUniqueScope.Campaign;
	public List<string> Regions { get; set; } = new List<string>();
	public float SpawnChance { get; set; } = 1f;
	public bool GuaranteeFirstPlacement { get; set; }
	public bool AvoidStartingPlanet { get; set; }
	public string PreferredSpriteContains { get; set; } = string.Empty;
	public bool AllowFallbackTarget { get; set; } = true;
	public int MinimumRadius { get; set; } = 7;
	public int MaximumRadius { get; set; } = 14;
	public List<string> RequiredFlags { get; set; } = new List<string>();
	public List<string> BlockedFlags { get; set; } = new List<string>();
}

public enum OverworldContentIssueSeverity
{
	Info,
	Warning,
	Error
}

public sealed class OverworldContentIssue
{
	public OverworldContentIssueSeverity Severity { get; set; }
	public string RuleId { get; set; } = string.Empty;
	public string ContentId { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string SourcePath { get; set; } = string.Empty;
}

public sealed class OverworldAssignmentResult
{
	public string ContentId { get; set; } = string.Empty;
	public string SystemName { get; set; } = string.Empty;
	public bool Assigned { get; set; }
	public string TargetDescription { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
}
