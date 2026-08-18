using System;
using System.Collections.Generic;
using System.Linq;

public static class OverworldContentValidator
{
	public static readonly IReadOnlyList<string> KnownRegions = new[]
	{
		"Far Silence", "Verdant Shroud", "Core Spindle", "Luminous Verge",
		"Ember Wastes", "Shattered Reach", "Obsidian Belt", "Echo Spiral"
	};

	public static List<OverworldContentIssue> Validate(IEnumerable<OverworldContentDefinition> source)
	{
		List<OverworldContentIssue> issues = new List<OverworldContentIssue>();
		List<OverworldContentDefinition> definitions = source?.Where(definition => definition != null).ToList() ?? new List<OverworldContentDefinition>();
		List<MissionTemplate> missionTemplates = new MissionRegistry().LoadTemplates().Where(template => template != null && template.IsEnabled).ToList();

		foreach (OverworldContentDefinition definition in definitions)
		{
			ValidateDefinition(definition, missionTemplates, issues);
		}

		foreach (IGrouping<string, OverworldContentDefinition> duplicate in definitions
			.Where(definition => definition.Enabled && !string.IsNullOrWhiteSpace(definition.ContentReference))
			.GroupBy(definition => $"{definition.ContentType}:{definition.ContentReference}", StringComparer.OrdinalIgnoreCase)
			.Where(group => group.Count() > 1))
		{
			issues.Add(Issue(OverworldContentIssueSeverity.Warning, "content.reference_duplicate", duplicate.First(), $"Content reference '{duplicate.First().ContentReference}' is used by multiple enabled placement definitions."));
		}

		return issues;
	}

	private static void ValidateDefinition(OverworldContentDefinition definition, List<MissionTemplate> templates, List<OverworldContentIssue> issues)
	{
		if (string.IsNullOrWhiteSpace(definition.ContentId)) issues.Add(Issue(OverworldContentIssueSeverity.Error, "content.id", definition, "Content ID is required."));
		if (string.IsNullOrWhiteSpace(definition.DisplayName)) issues.Add(Issue(OverworldContentIssueSeverity.Warning, "content.name", definition, "Display name is empty."));
		if (string.IsNullOrWhiteSpace(definition.ContentReference)) issues.Add(Issue(OverworldContentIssueSeverity.Error, "content.reference", definition, "Content reference is required."));
		if (definition.Placement == null)
		{
			issues.Add(Issue(OverworldContentIssueSeverity.Error, "placement.missing", definition, "Placement rule is missing."));
			return;
		}

		OverworldPlacementRule placement = definition.Placement;
		if (placement.SpawnChance < 0f || placement.SpawnChance > 1f) issues.Add(Issue(OverworldContentIssueSeverity.Error, "placement.chance", definition, "Spawn chance must be between 0 and 1."));
		if (placement.MinimumRadius < 0 || placement.MaximumRadius < placement.MinimumRadius) issues.Add(Issue(OverworldContentIssueSeverity.Error, "placement.radius", definition, "Free-hex radius range is invalid."));
		foreach (string region in placement.Regions ?? new List<string>())
		{
			if (!KnownRegions.Contains(region, StringComparer.Ordinal)) issues.Add(Issue(OverworldContentIssueSeverity.Error, "placement.region", definition, $"Unknown region '{region}'."));
		}
		if ((placement.RequiredFlags ?? new List<string>()).Intersect(placement.BlockedFlags ?? new List<string>(), StringComparer.Ordinal).Any())
		{
			issues.Add(Issue(OverworldContentIssueSeverity.Error, "placement.flags", definition, "The same story flag is both required and blocked."));
		}
		if (!string.IsNullOrWhiteSpace(placement.PreferredSpriteContains) && placement.NodeType != OverworldPlacementNodeType.Outpost)
		{
			issues.Add(Issue(OverworldContentIssueSeverity.Warning, "placement.sprite_filter", definition, "Preferred sprite matching only applies to outpost placement."));
		}

		if (definition.ContentType == OverworldContentType.Mission)
		{
			if (placement.NodeType == OverworldPlacementNodeType.FreeHex) issues.Add(Issue(OverworldContentIssueSeverity.Error, "mission.node_type", definition, "Mission placement currently requires a planet or outpost node."));
			int matches = templates.Count(template => (template.InteractionKeys ?? new Godot.Collections.Array<string>()).Any(key => string.Equals(key?.Trim(), definition.ContentReference, StringComparison.OrdinalIgnoreCase)));
			if (matches == 0) issues.Add(Issue(OverworldContentIssueSeverity.Error, "mission.reference_missing", definition, $"No enabled mission template declares interaction key '{definition.ContentReference}'."));
			if (matches > 1) issues.Add(Issue(OverworldContentIssueSeverity.Error, "mission.reference_ambiguous", definition, $"Multiple mission templates declare interaction key '{definition.ContentReference}'."));
		}
		else
		{
			if (placement.NodeType != OverworldPlacementNodeType.FreeHex) issues.Add(Issue(OverworldContentIssueSeverity.Error, "event.node_type", definition, "Ambient events currently require free-hex placement."));
			AmbientEventDefinition ambientEvent = AmbientEventRegistry.GetEvent(definition.ContentReference);
			if (ambientEvent == null) issues.Add(Issue(OverworldContentIssueSeverity.Error, "event.reference_missing", definition, $"Ambient event '{definition.ContentReference}' does not exist."));
			else if (!string.IsNullOrWhiteSpace(ambientEvent.Region) && placement.Regions?.Count > 0 && !placement.Regions.Contains(ambientEvent.Region, StringComparer.Ordinal))
			{
				issues.Add(Issue(OverworldContentIssueSeverity.Warning, "event.region_mismatch", definition, $"Ambient event region '{ambientEvent.Region}' is not included in its placement regions."));
			}
		}
	}

	private static OverworldContentIssue Issue(OverworldContentIssueSeverity severity, string ruleId, OverworldContentDefinition definition, string message)
	{
		return new OverworldContentIssue { Severity = severity, RuleId = ruleId, ContentId = definition?.ContentId ?? string.Empty, SourcePath = definition?.SourcePath ?? string.Empty, Message = message };
	}
}
