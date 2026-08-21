using System.Collections.Generic;

public sealed class MissionDocument
{
	public const int CurrentSchemaVersion = 2;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public string MissionId { get; set; } = string.Empty;
	public string LayoutName { get; set; } = string.Empty;
	public MissionDocumentMetadata Metadata { get; set; } = new MissionDocumentMetadata();
	public MissionEnvironmentData Environment { get; set; } = new MissionEnvironmentData();
	public List<MissionMapElement> Elements { get; set; } = new List<MissionMapElement>();
	public List<MissionFlowNodeData> FlowNodes { get; set; } = new List<MissionFlowNodeData>();
	public List<string> DialogueConversationIds { get; set; } = new List<string>();
	public List<MissionOutcomeData> Outcomes { get; set; } = new List<MissionOutcomeData>();
	public MissionAuthoringData Authoring { get; set; } = new MissionAuthoringData();
}

// Author-friendly information used by Workbench v3. The runtime compiler continues
// to consume the established element logic fields, which v3 derives from these rules.
public sealed class MissionAuthoringData
{
	public bool WelcomeCompleted { get; set; }
	public string TemplateKind { get; set; } = "Existing mission";
	public List<MissionRuleData> Rules { get; set; } = new List<MissionRuleData>();
}

public sealed class MissionRuleData
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string WhenType { get; set; } = "Player uses";
	public string SourceElementId { get; set; } = string.Empty;
	public string ActionType { get; set; } = "Open door";
	public string TargetElementId { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
	public bool Enabled { get; set; } = true;
}

public sealed class MissionDocumentMetadata
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string MissionScenePath { get; set; } = string.Empty;
	public string DefaultReturnScenePath { get; set; } = string.Empty;
	public int RecommendedOfficerCount { get; set; } = 2;
	public string SourceNodeType { get; set; } = string.Empty;
	public string ObjectiveText { get; set; } = string.Empty;
	public string PromptText { get; set; } = string.Empty;
	public string DefaultDialogueId { get; set; } = string.Empty;
	public List<string> InteractionKeys { get; set; } = new List<string>();
	public bool IsEnabled { get; set; } = true;
}

public sealed class MissionEnvironmentData
{
	public string BackgroundId { get; set; } = MissionBackgroundCatalog.DefaultId;
}

public enum MissionMapElementKind
{
	Tile,
	Marker,
	PlacedProp
}

public sealed class MissionMapElement
{
	public string Id { get; set; } = string.Empty;
	public int SourceOrder { get; set; }
	public MissionMapElementKind Kind { get; set; }
	public string TileId { get; set; } = string.Empty;
	public string MarkerId { get; set; } = string.Empty;
	public int Column { get; set; }
	public int Row { get; set; }
	public float OffsetX { get; set; }
	public float OffsetY { get; set; }
	public float RotationDegrees { get; set; }
	public bool FlipH { get; set; }
	public bool FlipV { get; set; }
	public string PropDefinitionPath { get; set; } = string.Empty;
	public string NpcDefinitionPath { get; set; } = string.Empty;
	public MissionElementLogic Logic { get; set; } = new MissionElementLogic();
}

public sealed class MissionElementLogic
{
	public string Role { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string TargetId { get; set; } = string.Empty;
	public string NpcPortraitPath { get; set; } = string.Empty;
	public string RequiredFlag { get; set; } = string.Empty;
	public string SetFlag { get; set; } = string.Empty;
	public string TriggerMode { get; set; } = "none";
	public bool OneShot { get; set; }
	public string Notes { get; set; } = string.Empty;
}

public enum MissionFlowNodeKind
{
	Trigger,
	Interaction,
	Spawn,
	Objective,
	Outcome
}

public sealed class MissionFlowNodeData
{
	public string Id { get; set; } = string.Empty;
	public MissionFlowNodeKind Kind { get; set; }
	public string MapElementId { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string TriggerMode { get; set; } = "none";
	public List<string> RequiredFlags { get; set; } = new List<string>();
	public List<string> BlockedFlags { get; set; } = new List<string>();
	public List<string> SetFlags { get; set; } = new List<string>();
	public string TargetId { get; set; } = string.Empty;
	public float CanvasX { get; set; }
	public float CanvasY { get; set; }
}

public sealed class MissionOutcomeData
{
	public string Id { get; set; } = string.Empty;
	public string ActionText { get; set; } = string.Empty;
	public List<string> RequiredFlags { get; set; } = new List<string>();
	public List<string> BlockedFlags { get; set; } = new List<string>();
}

public enum MissionValidationSeverity
{
	Info,
	Warning,
	Error
}

public sealed class MissionValidationIssue
{
	public string RuleId { get; set; } = string.Empty;
	public MissionValidationSeverity Severity { get; set; }
	public string Message { get; set; } = string.Empty;
	public string ElementId { get; set; } = string.Empty;
}

public sealed class MissionImportResult
{
	public MissionDocument Document { get; set; }
	public List<string> Errors { get; set; } = new List<string>();
	public bool Success => Document != null && Errors.Count == 0;
}

public sealed class MissionCompileResult
{
	public string OutputResourcePath { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
	public List<MissionValidationIssue> Issues { get; set; } = new List<MissionValidationIssue>();
	public bool Success { get; set; }
}
