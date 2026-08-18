using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class MissionDocumentReferenceResolver
{
	public static List<string> GetDialogueIds(MissionDocument document)
	{
		HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
		if (document == null) return ids.ToList();

		AddIfPresent(ids, document.Metadata?.DefaultDialogueId);
		foreach (string id in document.DialogueConversationIds ?? new List<string>()) AddIfPresent(ids, id);
		foreach (MissionMapElement element in document.Elements ?? new List<MissionMapElement>())
		{
			if (element == null) continue;
			string targetId = element.Logic?.TargetId ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(targetId) && DialogueRegistry.LoadConversationData(targetId) != null) ids.Add(targetId);

			if (!string.IsNullOrWhiteSpace(element.PropDefinitionPath)
				&& ResourceLoader.Load<PropDefinition>(element.PropDefinitionPath) is PropDefinition prop)
			{
				AddIfPresent(ids, prop.DialogueId);
				if (prop.InteractionType == PropInteractionType.Dialogue) AddIfPresent(ids, targetId);
			}

			if (!string.IsNullOrWhiteSpace(element.NpcDefinitionPath)
				&& ResourceLoader.Load<MissionNpcDefinition>(element.NpcDefinitionPath) is MissionNpcDefinition npc)
			{
				AddIfPresent(ids, npc.DefaultDialogueId);
			}
		}

		return ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
	}

	public static HashSet<string> GetProducedFlags(MissionDocument document)
	{
		HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
		if (document == null) return flags;

		foreach (MissionMapElement element in document.Elements ?? new List<MissionMapElement>())
		{
			if (element == null) continue;
			AddFlags(flags, SplitFlags(element.Logic?.SetFlag));
			if (!string.IsNullOrWhiteSpace(element.PropDefinitionPath)
				&& ResourceLoader.Load<PropDefinition>(element.PropDefinitionPath) is PropDefinition prop)
			{
				AddFlags(flags, prop.SetFlags);
			}
			if (!string.IsNullOrWhiteSpace(element.NpcDefinitionPath)
				&& ResourceLoader.Load<MissionNpcDefinition>(element.NpcDefinitionPath) is MissionNpcDefinition npc)
			{
				AddFlags(flags, npc.SetFlags);
			}
		}

		foreach (MissionFlowNodeData flow in document.FlowNodes ?? new List<MissionFlowNodeData>()) AddFlags(flags, flow?.SetFlags);
		foreach (string dialogueId in GetDialogueIds(document)) AddDialogueFlags(flags, dialogueId);
		return flags;
	}

	public static void RefreshDialogueIds(MissionDocument document)
	{
		if (document != null) document.DialogueConversationIds = GetDialogueIds(document);
	}

	private static void AddDialogueFlags(HashSet<string> flags, string dialogueId)
	{
		DialogueConversationData conversation = DialogueRegistry.LoadConversationData(dialogueId);
		foreach (DialogueNode node in conversation?.Nodes ?? new List<DialogueNode>())
		{
			AddFlags(flags, node?.SetFlags);
			foreach (DialogueOption option in node?.Options ?? new List<DialogueOption>()) AddFlags(flags, option?.SetFlags);
		}
	}

	private static void AddIfPresent(HashSet<string> ids, string id)
	{
		if (!string.IsNullOrWhiteSpace(id)) ids.Add(id.Trim());
	}

	private static void AddFlags(HashSet<string> flags, IEnumerable<string> values)
	{
		foreach (string value in values ?? Enumerable.Empty<string>())
		{
			if (!string.IsNullOrWhiteSpace(value)) flags.Add(value.Trim());
		}
	}

	private static IEnumerable<string> SplitFlags(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? Enumerable.Empty<string>()
			: value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}
}
