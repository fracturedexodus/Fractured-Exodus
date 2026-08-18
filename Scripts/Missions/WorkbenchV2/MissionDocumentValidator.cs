using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class MissionDocumentValidator
{
	public static List<MissionValidationIssue> Validate(MissionDocument document)
	{
		List<MissionValidationIssue> issues = new List<MissionValidationIssue>();
		if (document == null)
		{
			issues.Add(Error("document.missing", "Mission document is missing."));
			return issues;
		}

		if (string.IsNullOrWhiteSpace(document.MissionId)) issues.Add(Error("mission.id", "Mission ID is required."));
		if (string.IsNullOrWhiteSpace(document.Metadata?.MissionScenePath)) issues.Add(Error("mission.scene", "Mission scene path is required."));
		if (!MissionBackgroundCatalog.All.Any(background => background.Id == document.Environment?.BackgroundId))
		{
			issues.Add(Error("environment.background", $"Unknown background '{document.Environment?.BackgroundId}'."));
		}

		List<MissionMapElement> elements = document.Elements?.Where(element => element != null).ToList() ?? new List<MissionMapElement>();
		HashSet<Vector2I> floorCells = elements
			.Where(element => element.Kind == MissionMapElementKind.Tile
				&& MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition tile)
				&& tile.Category == MissionTileCategory.Floor)
			.Select(element => new Vector2I(element.Column, element.Row))
			.ToHashSet();
		foreach (IGrouping<string, MissionMapElement> duplicate in elements.Where(element => !string.IsNullOrWhiteSpace(element.Id)).GroupBy(element => element.Id).Where(group => group.Count() > 1))
		{
			issues.Add(Error("element.duplicate_id", $"Element ID '{duplicate.Key}' is duplicated.", duplicate.First().Id));
		}
		if (elements.All(element => element.Kind != MissionMapElementKind.Tile || !MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition definition) || definition.Category != MissionTileCategory.Floor))
		{
			issues.Add(Error("map.floor", "Mission requires at least one floor tile."));
		}

		ValidateRequiredMarker(elements, "spawn_a", issues);
		ValidateRequiredMarker(elements, "spawn_b", issues);
		if (!elements.Any(element => element.MarkerId == "evac_zone"))
		{
			issues.Add(new MissionValidationIssue { RuleId = "map.evac", Severity = MissionValidationSeverity.Warning, Message = "No evacuation zone is placed." });
		}

		HashSet<string> uniqueDoorTargetIds = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> producedFlags = MissionDocumentReferenceResolver.GetProducedFlags(document);
		foreach (MissionMapElement element in elements)
		{
			if (string.IsNullOrWhiteSpace(element.Id)) issues.Add(Error("element.id", "Map element is missing a stable ID."));
			if (element.Kind == MissionMapElementKind.Tile && !MissionTileCatalog.TryGetById(element.TileId, out _)) issues.Add(Error("element.tile", $"Unknown tile '{element.TileId}'.", element.Id));
			if (element.Kind == MissionMapElementKind.Marker && !MissionMarkerCatalog.TryGetById(element.MarkerId, out _)) issues.Add(Error("element.marker", $"Unknown marker '{element.MarkerId}'.", element.Id));
			if (element.Kind == MissionMapElementKind.PlacedProp && !ResourceLoader.Exists(element.PropDefinitionPath, "PropDefinition")) issues.Add(Error("element.prop", $"Invalid prop definition '{element.PropDefinitionPath}'.", element.Id));
			if (!string.IsNullOrWhiteSpace(element.NpcDefinitionPath) && !ResourceLoader.Exists(element.NpcDefinitionPath, "MissionNpcDefinition")) issues.Add(Error("element.npc", $"Invalid NPC definition '{element.NpcDefinitionPath}'.", element.Id));
			if (element.Kind != MissionMapElementKind.Tile && !floorCells.Contains(new Vector2I(element.Column, element.Row)))
			{
				issues.Add(new MissionValidationIssue { RuleId = "map.off_floor", Severity = MissionValidationSeverity.Warning, Message = $"{GetElementName(element)} is not placed on a floor cell.", ElementId = element.Id });
			}
			if (!string.IsNullOrWhiteSpace(element.NpcDefinitionPath) && ResourceLoader.Load<MissionNpcDefinition>(element.NpcDefinitionPath) is MissionNpcDefinition npc)
			{
				if (element.MarkerId == "hostile_spawn" && !npc.IsHostile) issues.Add(Error("spawn.alignment", $"Hostile spawn uses friendly NPC '{npc.DisplayName}'.", element.Id));
				if (element.MarkerId == "npc_spawn" && npc.IsHostile) issues.Add(Error("spawn.alignment", $"Friendly spawn uses hostile NPC '{npc.DisplayName}'.", element.Id));
			}

			string targetId = element.Logic?.TargetId ?? string.Empty;
			bool isDoor = element.Kind == MissionMapElementKind.Tile && element.TileId.StartsWith("door_", StringComparison.Ordinal);
			if (isDoor && !string.IsNullOrWhiteSpace(targetId) && !uniqueDoorTargetIds.Add(targetId))
			{
				issues.Add(new MissionValidationIssue { RuleId = "logic.door_id_duplicate", Severity = MissionValidationSeverity.Error, Message = $"Door ID '{targetId}' is reused.", ElementId = element.Id });
			}
			foreach (string requiredFlag in SplitFlags(element.Logic?.RequiredFlag))
			{
				if (!producedFlags.Contains(requiredFlag))
				{
					issues.Add(new MissionValidationIssue { RuleId = "logic.flag_unproduced", Severity = MissionValidationSeverity.Warning, Message = $"Required flag '{requiredFlag}' is never produced by this mission.", ElementId = element.Id });
				}
			}
		}

		ValidateReachability(elements, floorCells, issues);
		foreach (string dialogueId in MissionDocumentReferenceResolver.GetDialogueIds(document))
		{
			if (DialogueRegistry.LoadConversationData(dialogueId) == null)
			{
				issues.Add(Error("dialogue.missing", $"Dialogue conversation '{dialogueId}' does not exist."));
			}
		}

		foreach (MissionOutcomeData outcome in document.Outcomes ?? new List<MissionOutcomeData>())
		{
			if (string.IsNullOrWhiteSpace(outcome.Id)) issues.Add(Error("outcome.id", "Outcome ID is required."));
			if ((outcome.RequiredFlags ?? new List<string>()).Intersect(outcome.BlockedFlags ?? new List<string>()).Any()) issues.Add(Error("outcome.flags", $"Outcome '{outcome.Id}' requires and blocks the same flag."));
			foreach (string requiredFlag in outcome.RequiredFlags ?? new List<string>())
			{
				if (!producedFlags.Contains(requiredFlag)) issues.Add(new MissionValidationIssue { RuleId = "outcome.flag_unproduced", Severity = MissionValidationSeverity.Warning, Message = $"Outcome '{outcome.Id}' requires flag '{requiredFlag}', but no authored interaction produces it." });
			}
		}

		return issues;
	}

	private static void ValidateReachability(List<MissionMapElement> elements, HashSet<Vector2I> floorCells, List<MissionValidationIssue> issues)
	{
		MissionMapElement spawn = elements.FirstOrDefault(element => element.MarkerId == "spawn_a");
		if (spawn == null || floorCells.Count == 0) return;
		Vector2I start = new Vector2I(spawn.Column, spawn.Row);
		if (!floorCells.Contains(start)) return;
		HashSet<Vector2I> reachable = new HashSet<Vector2I> { start };
		Queue<Vector2I> frontier = new Queue<Vector2I>();
		frontier.Enqueue(start);
		Vector2I[] directions = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
		while (frontier.Count > 0)
		{
			Vector2I current = frontier.Dequeue();
			foreach (Vector2I direction in directions)
			{
				Vector2I next = current + direction;
				if (floorCells.Contains(next) && reachable.Add(next)) frontier.Enqueue(next);
			}
		}

		foreach (MissionMapElement target in elements.Where(element => element.MarkerId == "evac_zone" || element.MarkerId.StartsWith("objective_")))
		{
			if (!reachable.Contains(new Vector2I(target.Column, target.Row)))
			{
				issues.Add(Error("map.unreachable", $"{GetElementName(target)} is unreachable from spawn_a.", target.Id));
			}
		}
	}

	private static string GetElementName(MissionMapElement element)
	{
		return !string.IsNullOrWhiteSpace(element.Logic?.Label)
			? element.Logic.Label
			: !string.IsNullOrWhiteSpace(element.MarkerId) ? element.MarkerId : !string.IsNullOrWhiteSpace(element.TileId) ? element.TileId : element.Id;
	}

	private static void ValidateRequiredMarker(List<MissionMapElement> elements, string markerId, List<MissionValidationIssue> issues)
	{
		int count = elements.Count(element => element.MarkerId == markerId);
		if (count != 1) issues.Add(Error("map.required_marker", $"Mission requires exactly one '{markerId}' marker; found {count}."));
	}

	private static IEnumerable<string> SplitFlags(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? Enumerable.Empty<string>() : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	private static MissionValidationIssue Error(string ruleId, string message, string elementId = "")
	{
		return new MissionValidationIssue { RuleId = ruleId, Severity = MissionValidationSeverity.Error, Message = message, ElementId = elementId };
	}
}
