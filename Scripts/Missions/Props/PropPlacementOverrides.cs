using Godot;
using System;

public static class PropPlacementOverrides
{
	public static void ApplyRuntimeOverrides(PropDefinition definition, MissionRoomBuilder.MarkerPlacement placement, MissionTemplate missionTemplate)
	{
		if (definition == null || placement == null)
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(placement.Label))
		{
			definition.DisplayName = placement.Label;
		}

		MergeFlag(definition.RequiredFlags, placement.RequiredFlag);
		MergeFlag(definition.SetFlags, placement.SetFlag);

		if (placement.OneShot)
		{
			definition.OneShot = true;
		}

		if (string.IsNullOrWhiteSpace(placement.TargetId))
		{
			return;
		}

		switch (definition.InteractionType)
		{
			case PropInteractionType.Dialogue:
				definition.DialogueId = placement.TargetId;
				break;
			case PropInteractionType.DoorControl:
				definition.DoorTargetIds = BuildDoorTargetArray(placement.TargetId);
				break;
			case PropInteractionType.Loot:
				ApplyLootPreset(definition, placement.TargetId, missionTemplate);
				break;
		}
	}

	public static string GetSuggestedTargetId(MissionTemplate missionTemplate, PropDefinition definition)
	{
		if (missionTemplate == null || definition == null)
		{
			return string.Empty;
		}

		return definition.InteractionType switch
		{
			PropInteractionType.Dialogue => string.IsNullOrWhiteSpace(missionTemplate.DefaultDialogueId)
				? definition.DialogueId
				: missionTemplate.DefaultDialogueId,
			PropInteractionType.Loot => GetSuggestedLootPresetId(missionTemplate),
			_ => string.Empty
		};
	}

	public static string GetSuggestedNotes(MissionTemplate missionTemplate, PropDefinition definition)
	{
		if (missionTemplate == null || definition == null)
		{
			return string.Empty;
		}

		if (definition.InteractionType == PropInteractionType.Dialogue && !string.IsNullOrWhiteSpace(missionTemplate.DefaultDialogueId))
		{
			return $"Default dialogue route: {missionTemplate.DefaultDialogueId}";
		}

		if (definition.InteractionType == PropInteractionType.Loot)
		{
			string presetId = GetSuggestedLootPresetId(missionTemplate);
			return string.IsNullOrWhiteSpace(presetId)
				? string.Empty
				: $"Loot preset: {presetId}";
		}

		return string.Empty;
	}

	private static Godot.Collections.Array<string> BuildDoorTargetArray(string rawTargetIds)
	{
		Godot.Collections.Array<string> doorIds = new Godot.Collections.Array<string>();
		foreach (string token in rawTargetIds.Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string trimmed = token.Trim();
			if (!string.IsNullOrWhiteSpace(trimmed))
			{
				doorIds.Add(trimmed);
			}
		}

		return doorIds;
	}

	private static void MergeFlag(Godot.Collections.Array<string> flags, string flag)
	{
		if (flags == null || string.IsNullOrWhiteSpace(flag) || flags.Contains(flag))
		{
			return;
		}

		flags.Add(flag);
	}

	private static string GetSuggestedLootPresetId(MissionTemplate missionTemplate)
	{
		return missionTemplate.MissionId switch
		{
			"outpost_smuggler_exchange" => "smuggler_contraband_cache",
			"black_site_relay" => "relay_archive_cache",
			_ => string.Empty
		};
	}

	private static void ApplyLootPreset(PropDefinition definition, string presetId, MissionTemplate missionTemplate)
	{
		switch (presetId)
		{
			case "smuggler_contraband_cache":
				definition.RewardRawMaterials = 30;
				definition.RewardEnergyCores = 1;
				definition.RewardAncientTech = 1;
				MergeFlag(definition.SetFlags, "smuggler_cache_opened");
				definition.SuccessMessage = "Cache breached. Contraband manifests secured.";
				break;
			case "smuggler_route_cache":
				definition.RewardRawMaterials = 10;
				definition.RewardEnergyCores = 1;
				definition.RewardAncientTech = 3;
				MergeFlag(definition.SetFlags, "smuggler_route_cache_secured");
				definition.SuccessMessage = "Route ledgers extracted from the vault core.";
				break;
			case "relay_archive_cache":
				definition.RewardRawMaterials = 12;
				definition.RewardEnergyCores = 1;
				definition.RewardAncientTech = 1;
				MergeFlag(definition.SetFlags, "relay_archive_cache_opened");
				definition.SuccessMessage = "Archive cache secured.";
				break;
			default:
				if (!string.IsNullOrWhiteSpace(missionTemplate?.MissionId)
					&& string.Equals(missionTemplate.MissionId, "outpost_smuggler_exchange", StringComparison.OrdinalIgnoreCase))
				{
					MergeFlag(definition.SetFlags, "smuggler_cache_opened");
				}
				break;
		}
	}
}
