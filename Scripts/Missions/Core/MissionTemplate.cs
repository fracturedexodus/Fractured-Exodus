using Godot;

[GlobalClass]
public partial class MissionTemplate : Resource
{
	[Export] public string MissionId { get; set; } = string.Empty;
	[Export] public string Title { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
	[Export(PropertyHint.File, "*.tscn")] public string MissionScenePath { get; set; } = string.Empty;
	[Export(PropertyHint.File, "*.json")] public string LayoutResourcePath { get; set; } = string.Empty;
	[Export(PropertyHint.File, "*.tscn")] public string DefaultReturnScenePath { get; set; } = "res://exploration_battle.tscn";
	[Export] public int RecommendedOfficerCount { get; set; } = 2;
	[Export] public string SourceNodeType { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string ObjectiveText { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string PromptText { get; set; } = string.Empty;
	[Export] public string PrimaryActionText { get; set; } = string.Empty;
	[Export] public string PrimaryOutcomeId { get; set; } = string.Empty;
	[Export] public Godot.Collections.Array<string> PrimaryOutcomeRequiredFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export] public string SecondaryActionText { get; set; } = string.Empty;
	[Export] public string SecondaryOutcomeId { get; set; } = string.Empty;
	[Export] public Godot.Collections.Array<string> SecondaryOutcomeRequiredFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export] public string DefaultDialogueId { get; set; } = string.Empty;
	[Export] public Godot.Collections.Array<MissionSpawnDefinition> SpawnDefinitions { get; set; } = new Godot.Collections.Array<MissionSpawnDefinition>();
	[Export] public Godot.Collections.Array<string> SpawnDefinitionPaths { get; set; } = new Godot.Collections.Array<string>();
	[Export] public Godot.Collections.Array<string> InteractionKeys { get; set; } = new Godot.Collections.Array<string>();
	[Export] public bool IsEnabled { get; set; } = true;

	public MissionDefinition ToDefinition()
	{
		return new MissionDefinition
		{
			MissionID = MissionId,
			ScenePath = MissionScenePath,
			Title = Title,
			Description = Description,
			DefaultReturnScenePath = DefaultReturnScenePath,
			RecommendedOfficerCount = RecommendedOfficerCount
		};
	}
}
