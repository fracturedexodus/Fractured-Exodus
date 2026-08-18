using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class OverworldContentAssignmentService
{
	private static bool _reportedRegistryErrors;

	public static List<OverworldAssignmentResult> AssignNodeContent(GlobalData globalData, SystemData currentSystem, Random rng)
	{
		List<OverworldAssignmentResult> results = new List<OverworldAssignmentResult>();
		if (globalData == null || currentSystem == null) return results;
		rng ??= new Random(BuildStableHash(currentSystem.SystemName));
		List<OverworldContentDefinition> runnableDefinitions = GetRunnableDefinitions().ToList();
		if (runnableDefinitions.Any(definition => definition.ContentReference == "planet:black_site_relay")) MigrateLegacyPlanetMarkers(globalData);

		foreach (OverworldContentDefinition definition in runnableDefinitions
			.Where(definition => definition.ContentType == OverworldContentType.Mission && definition.Placement.NodeType != OverworldPlacementNodeType.FreeHex))
		{
			OverworldAssignmentResult result = BuildResult(definition, currentSystem);
			if (!IsEligible(definition, globalData, currentSystem, rng, out string reason))
			{
				result.Reason = reason;
				results.Add(result);
				continue;
			}

			if (definition.Placement.NodeType == OverworldPlacementNodeType.Planet)
			{
				PlanetData planet = SelectPlanet(definition, globalData, currentSystem);
				if (planet == null) result.Reason = "No eligible planet is available.";
				else
				{
					planet.MissionInteractionKey = definition.ContentReference;
					if (definition.ContentReference == "planet:black_site_relay") planet.IsBlackSiteRelaySite = true;
					result.Assigned = true;
					result.TargetDescription = $"Planet: {planet.Name}";
				}
			}
			else
			{
				OutpostData outpost = SelectOutpost(definition, currentSystem);
				if (outpost == null) result.Reason = "No eligible outpost is available.";
				else
				{
					outpost.MissionInteractionKey = definition.ContentReference;
					result.Assigned = true;
					result.TargetDescription = $"Outpost: {outpost.Name}";
				}
			}
			results.Add(result);
		}

		return results;
	}

	public static List<OverworldAssignmentResult> AssignFreeHexContent(
		GlobalData globalData,
		SystemData currentSystem,
		Random rng,
		int maxMapRadius,
		Dictionary<Vector2I, Node2D> hexGrid,
		Dictionary<Vector2I, MapEntity> hexContents)
	{
		List<OverworldAssignmentResult> results = new List<OverworldAssignmentResult>();
		if (globalData == null || currentSystem == null || hexGrid == null || hexContents == null) return results;
		rng ??= new Random(BuildStableHash(currentSystem.SystemName));
		currentSystem.AmbientEvents ??= new List<AmbientEventInstanceData>();

		foreach (OverworldContentDefinition definition in GetRunnableDefinitions()
			.Where(definition => definition.ContentType == OverworldContentType.AmbientEvent && definition.Placement.NodeType == OverworldPlacementNodeType.FreeHex))
		{
			OverworldAssignmentResult result = BuildResult(definition, currentSystem);
			if (!IsEligible(definition, globalData, currentSystem, rng, out string reason))
			{
				result.Reason = reason;
				results.Add(result);
				continue;
			}

			AmbientEventDefinition eventDefinition = AmbientEventRegistry.GetEvent(definition.ContentReference);
			if (eventDefinition == null)
			{
				result.Reason = "Ambient event definition is missing.";
				results.Add(result);
				continue;
			}

			Vector2I eventHex = FindFreeHex(definition.Placement, currentSystem, rng, maxMapRadius, hexGrid, hexContents);
			currentSystem.AmbientEvents.Add(new AmbientEventInstanceData
			{
				InstanceId = BuildAmbientInstanceId(currentSystem, definition.ContentReference),
				EventId = definition.ContentReference,
				HexPosition = eventHex,
				CurrentNodeId = eventDefinition.StartNodeId
			});
			result.Assigned = true;
			result.TargetDescription = $"Hex: {eventHex.X},{eventHex.Y}";
			results.Add(result);
		}

		return results;
	}

	private static IEnumerable<OverworldContentDefinition> GetRunnableDefinitions()
	{
		IReadOnlyList<OverworldContentIssue> issues = OverworldContentRegistry.GetIssues();
		if (!_reportedRegistryErrors)
		{
			foreach (OverworldContentIssue issue in issues.Where(issue => issue.Severity == OverworldContentIssueSeverity.Error))
			{
				GD.PushError($"Overworld content [{issue.RuleId}] {issue.ContentId}: {issue.Message}");
			}
			_reportedRegistryErrors = true;
		}
		return OverworldContentRegistry.GetDefinitions().Where(definition =>
			definition.Enabled && !issues.Any(issue => issue.Severity == OverworldContentIssueSeverity.Error
				&& ((!string.IsNullOrWhiteSpace(issue.ContentId) && string.Equals(issue.ContentId, definition.ContentId, StringComparison.OrdinalIgnoreCase))
					|| (!string.IsNullOrWhiteSpace(issue.SourcePath) && string.Equals(issue.SourcePath, definition.SourcePath, StringComparison.OrdinalIgnoreCase)))));
	}

	private static bool IsEligible(OverworldContentDefinition definition, GlobalData globalData, SystemData currentSystem, Random rng, out string reason)
	{
		OverworldPlacementRule placement = definition.Placement;
		string region = GetCurrentRegion(globalData, currentSystem.SystemName);
		if (placement.Regions.Count > 0 && !placement.Regions.Contains(region, StringComparer.Ordinal))
		{
			reason = $"Region '{region}' is not eligible.";
			return false;
		}

		HashSet<string> storyFlags = (globalData.StoryFlags ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
		string missingFlag = placement.RequiredFlags.FirstOrDefault(flag => !storyFlags.Contains(flag));
		if (!string.IsNullOrWhiteSpace(missingFlag))
		{
			reason = $"Required story flag '{missingFlag}' is missing.";
			return false;
		}
		string blockedFlag = placement.BlockedFlags.FirstOrDefault(storyFlags.Contains);
		if (!string.IsNullOrWhiteSpace(blockedFlag))
		{
			reason = $"Blocked by story flag '{blockedFlag}'.";
			return false;
		}

		bool assignedInCurrentSystem = IsAssignedInSystem(definition, currentSystem);
		bool assignedAnywhere = (globalData.ExploredSystems?.Values ?? Enumerable.Empty<SystemData>()).Any(system => IsAssignedInSystem(definition, system));
		if (placement.UniqueScope == OverworldUniqueScope.Campaign && assignedAnywhere)
		{
			reason = "Already assigned in this campaign.";
			return false;
		}
		if (placement.UniqueScope == OverworldUniqueScope.System && assignedInCurrentSystem)
		{
			reason = "Already assigned in this system.";
			return false;
		}

		bool guaranteed = placement.GuaranteeFirstPlacement && !assignedAnywhere;
		if (!guaranteed && rng.NextDouble() >= placement.SpawnChance)
		{
			reason = "Spawn chance did not pass.";
			return false;
		}

		reason = string.Empty;
		return true;
	}

	private static bool IsAssignedInSystem(OverworldContentDefinition definition, SystemData system)
	{
		if (system == null) return false;
		return definition.ContentType switch
		{
			OverworldContentType.Mission when definition.Placement.NodeType == OverworldPlacementNodeType.Planet =>
				(system.Planets ?? new List<PlanetData>()).Any(planet => planet != null && string.Equals(planet.MissionInteractionKey, definition.ContentReference, StringComparison.OrdinalIgnoreCase)),
			OverworldContentType.Mission when definition.Placement.NodeType == OverworldPlacementNodeType.Outpost =>
				(system.Outposts ?? new List<OutpostData>()).Any(outpost => outpost != null && string.Equals(outpost.MissionInteractionKey, definition.ContentReference, StringComparison.OrdinalIgnoreCase)),
			OverworldContentType.AmbientEvent =>
				(system.AmbientEvents ?? new List<AmbientEventInstanceData>()).Any(instance => instance != null && string.Equals(instance.EventId, definition.ContentReference, StringComparison.OrdinalIgnoreCase)),
			_ => false
		};
	}

	private static PlanetData SelectPlanet(OverworldContentDefinition definition, GlobalData globalData, SystemData system)
	{
		List<PlanetData> candidates = (system.Planets ?? new List<PlanetData>())
			.Where(planet => planet != null && string.IsNullOrWhiteSpace(planet.MissionInteractionKey))
			.ToList();
		if (definition.Placement.AvoidStartingPlanet)
		{
			PlanetData nonStarting = candidates.FirstOrDefault(planet => !string.Equals(planet.Name, globalData.SavedPlanet, StringComparison.Ordinal));
			if (nonStarting != null) return nonStarting;
		}
		return definition.Placement.AllowFallbackTarget ? candidates.FirstOrDefault() : null;
	}

	private static OutpostData SelectOutpost(OverworldContentDefinition definition, SystemData system)
	{
		List<OutpostData> candidates = (system.Outposts ?? new List<OutpostData>())
			.Where(outpost => outpost != null && string.IsNullOrWhiteSpace(outpost.MissionInteractionKey))
			.ToList();
		string preferred = definition.Placement.PreferredSpriteContains;
		if (!string.IsNullOrWhiteSpace(preferred))
		{
			OutpostData preferredOutpost = candidates.FirstOrDefault(outpost => !string.IsNullOrWhiteSpace(outpost.SpritePath) && outpost.SpritePath.Contains(preferred, StringComparison.OrdinalIgnoreCase));
			if (preferredOutpost != null) return preferredOutpost;
		}
		return definition.Placement.AllowFallbackTarget ? candidates.FirstOrDefault() : null;
	}

	private static Vector2I FindFreeHex(OverworldPlacementRule placement, SystemData system, Random rng, int maxMapRadius, Dictionary<Vector2I, Node2D> hexGrid, Dictionary<Vector2I, MapEntity> hexContents)
	{
		int minimumRadius = Math.Clamp(placement.MinimumRadius, 0, Math.Max(0, maxMapRadius));
		int maximumRadius = Math.Clamp(placement.MaximumRadius, minimumRadius, Math.Max(minimumRadius, maxMapRadius));
		HashSet<Vector2I> reservedOutposts = (system.Outposts ?? new List<OutpostData>()).Where(outpost => outpost != null).Select(outpost => outpost.HexPosition).ToHashSet();

		for (int attempt = 0; attempt < 24; attempt++)
		{
			int radius = rng.Next(minimumRadius, maximumRadius + 1);
			List<Vector2I> candidates = hexGrid.Keys
				.Where(hex => HexMath.HexDistance(Vector2I.Zero, hex) == radius && !hexContents.ContainsKey(hex) && !reservedOutposts.Contains(hex))
				.OrderBy(hex => hex.X).ThenBy(hex => hex.Y).ToList();
			if (candidates.Count > 0) return candidates[rng.Next(candidates.Count)];
		}

		return hexGrid.Keys
			.Where(hex => HexMath.HexDistance(Vector2I.Zero, hex) >= minimumRadius && HexMath.HexDistance(Vector2I.Zero, hex) <= maximumRadius && !hexContents.ContainsKey(hex) && !reservedOutposts.Contains(hex))
			.OrderBy(hex => hex.X).ThenBy(hex => hex.Y).FirstOrDefault(new Vector2I(maximumRadius, 0));
	}

	private static void MigrateLegacyPlanetMarkers(GlobalData globalData)
	{
		foreach (SystemData system in globalData.ExploredSystems?.Values ?? Enumerable.Empty<SystemData>())
		{
			foreach (PlanetData planet in system?.Planets ?? new List<PlanetData>())
			{
				if (planet != null && planet.IsBlackSiteRelaySite && string.IsNullOrWhiteSpace(planet.MissionInteractionKey)) planet.MissionInteractionKey = "planet:black_site_relay";
			}
		}
	}

	private static string GetCurrentRegion(GlobalData globalData, string systemName)
	{
		return globalData.CurrentSectorStars?.FirstOrDefault(star => star != null && star.SystemName == systemName)?.Region ?? string.Empty;
	}

	private static string BuildAmbientInstanceId(SystemData system, string eventId)
	{
		string baseId = $"{system.SystemName}:{eventId}";
		HashSet<string> ids = (system.AmbientEvents ?? new List<AmbientEventInstanceData>()).Where(instance => instance != null).Select(instance => instance.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (!ids.Contains(baseId)) return baseId;
		int suffix = 2;
		while (ids.Contains($"{baseId}:{suffix}")) suffix++;
		return $"{baseId}:{suffix}";
	}

	private static OverworldAssignmentResult BuildResult(OverworldContentDefinition definition, SystemData system)
	{
		return new OverworldAssignmentResult { ContentId = definition.ContentId, SystemName = system.SystemName };
	}

	private static int BuildStableHash(string value)
	{
		unchecked
		{
			int hash = 23;
			foreach (char character in value ?? string.Empty) hash = hash * 31 + character;
			return hash;
		}
	}
}
