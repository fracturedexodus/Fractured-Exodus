using Godot;
using System.Collections.Generic;
using System.Linq;

public static class DialogueRegistry
{
	private const string DialogueDirectory = "res://Data/Dialogue/Conversations";
	private static readonly Dictionary<string, Dictionary<string, DialogueNode>> Conversations = new Dictionary<string, Dictionary<string, DialogueNode>>();
	private static bool _isLoaded;

	public static IReadOnlyList<string> GetConversationIds()
	{
		EnsureLoaded();
		return Conversations.Keys.OrderBy(id => id).ToList();
	}

	public static DialogueNode GetDialogue(string conversationId, string nodeId)
	{
		EnsureLoaded();
		if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(nodeId))
		{
			return null;
		}

		if (Conversations.TryGetValue(conversationId, out Dictionary<string, DialogueNode> nodes)
			&& nodes.TryGetValue(nodeId, out DialogueNode node))
		{
			return node;
		}

		return null;
	}

	public static void Reload()
	{
		_isLoaded = false;
		Conversations.Clear();
		EnsureLoaded();
	}

	private static void EnsureLoaded()
	{
		if (_isLoaded)
		{
			return;
		}

		_isLoaded = true;
		Conversations.Clear();
		if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(DialogueDirectory)))
		{
			return;
		}

		foreach (string file in DirAccess.GetFilesAt(DialogueDirectory)
			.Where(file => file.EndsWith(".json")))
		{
			LoadConversationFile($"{DialogueDirectory}/{file}");
		}
	}

	public static DialogueConversationData LoadConversationData(string conversationId)
	{
		if (string.IsNullOrWhiteSpace(conversationId))
		{
			return null;
		}

		string resourcePath = GetConversationResourcePath(conversationId);
		return LoadConversationDataFromFile(resourcePath);
	}

	public static bool SaveConversationData(DialogueConversationData conversation)
	{
		if (conversation == null || string.IsNullOrWhiteSpace(conversation.ConversationId))
		{
			return false;
		}

		string resourcePath = GetConversationResourcePath(conversation.ConversationId);
		DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(DialogueDirectory));
		using FileAccess file = FileAccess.Open(ProjectSettings.GlobalizePath(resourcePath), FileAccess.ModeFlags.Write);
		if (file == null)
		{
			return false;
		}

		Godot.Collections.Dictionary<string, Variant> root = new Godot.Collections.Dictionary<string, Variant>
		{
			{ "conversation_id", conversation.ConversationId },
			{ "nodes", BuildNodeArray(conversation.Nodes) }
		};
		file.StoreString(Json.Stringify(root, "\t"));
		Reload();
		return true;
	}

	private static void LoadConversationFile(string resourcePath)
	{
		DialogueConversationData conversation = LoadConversationDataFromFile(resourcePath);
		if (conversation == null || string.IsNullOrWhiteSpace(conversation.ConversationId))
		{
			return;
		}

		Dictionary<string, DialogueNode> nodes = new Dictionary<string, DialogueNode>();
		foreach (DialogueNode node in conversation.Nodes)
		{
			if (node == null || string.IsNullOrWhiteSpace(node.Id))
			{
				continue;
			}

			nodes[node.Id] = node;
		}

		Conversations[conversation.ConversationId] = nodes;
	}

	private static DialogueConversationData LoadConversationDataFromFile(string resourcePath)
	{
		using FileAccess file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return null;
		}

		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			return null;
		}

		Godot.Collections.Dictionary root = parsed.AsGodotDictionary();
		string conversationId = root.TryGetValue("conversation_id", out Variant conversationIdVariant)
			? conversationIdVariant.AsString()
			: string.Empty;
		if (string.IsNullOrWhiteSpace(conversationId))
		{
			return null;
		}

		DialogueConversationData conversation = new DialogueConversationData
		{
			ConversationId = conversationId
		};
		Godot.Collections.Array nodeArray = root.TryGetValue("nodes", out Variant nodesVariant)
			? nodesVariant.AsGodotArray()
			: new Godot.Collections.Array();
		foreach (Variant nodeVariant in nodeArray)
		{
			Godot.Collections.Dictionary nodeDict = nodeVariant.AsGodotDictionary();
			string nodeId = nodeDict.TryGetValue("id", out Variant nodeIdVariant)
				? nodeIdVariant.AsString()
				: string.Empty;
			if (string.IsNullOrWhiteSpace(nodeId))
			{
				continue;
			}

			conversation.Nodes.Add(new DialogueNode
			{
				Id = nodeId,
				SpeakerName = nodeDict.TryGetValue("speaker_name", out Variant speakerVariant) ? speakerVariant.AsString() : string.Empty,
				Text = nodeDict.TryGetValue("text", out Variant textVariant) ? textVariant.AsString() : string.Empty,
				QuestToTrigger = nodeDict.TryGetValue("quest_to_trigger", out Variant questVariant) ? questVariant.AsString() : string.Empty,
				RequiredFlags = ParseStringList(nodeDict, "required_flags"),
				BlockedFlags = ParseStringList(nodeDict, "blocked_flags"),
				SetFlags = ParseStringList(nodeDict, "set_flags"),
				Options = ParseOptions(nodeDict)
			});
		}

		return conversation;
	}

	private static Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>> BuildNodeArray(IEnumerable<DialogueNode> nodes)
	{
		Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>> items = new();
		foreach (DialogueNode node in nodes ?? Enumerable.Empty<DialogueNode>())
		{
			if (node == null || string.IsNullOrWhiteSpace(node.Id))
			{
				continue;
			}

			items.Add(new Godot.Collections.Dictionary<string, Variant>
			{
				{ "id", node.Id },
				{ "speaker_name", node.SpeakerName ?? string.Empty },
				{ "text", node.Text ?? string.Empty },
				{ "quest_to_trigger", node.QuestToTrigger ?? string.Empty },
				{ "required_flags", BuildStringArray(node.RequiredFlags) },
				{ "blocked_flags", BuildStringArray(node.BlockedFlags) },
				{ "set_flags", BuildStringArray(node.SetFlags) },
				{ "options", BuildOptionArray(node.Options) }
			});
		}

		return items;
	}

	private static Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>> BuildOptionArray(IEnumerable<DialogueOption> options)
	{
		Godot.Collections.Array<Godot.Collections.Dictionary<string, Variant>> items = new();
		foreach (DialogueOption option in options ?? Enumerable.Empty<DialogueOption>())
		{
			if (option == null || string.IsNullOrWhiteSpace(option.Text))
			{
				continue;
			}

			items.Add(new Godot.Collections.Dictionary<string, Variant>
			{
				{ "text", option.Text },
				{ "next", string.IsNullOrWhiteSpace(option.NextNodeId) ? "End" : option.NextNodeId },
				{ "required_flags", BuildStringArray(option.RequiredFlags) },
				{ "blocked_flags", BuildStringArray(option.BlockedFlags) },
				{ "set_flags", BuildStringArray(option.SetFlags) },
				{ "quest_to_trigger", option.QuestToTrigger ?? string.Empty }
			});
		}

		return items;
	}

	private static Godot.Collections.Array<string> BuildStringArray(IEnumerable<string> values)
	{
		Godot.Collections.Array<string> array = new Godot.Collections.Array<string>();
		foreach (string value in values ?? Enumerable.Empty<string>())
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				array.Add(value);
			}
		}

		return array;
	}

	private static string GetConversationResourcePath(string conversationId)
	{
		return $"{DialogueDirectory}/{conversationId}.json";
	}

	private static List<DialogueOption> ParseOptions(Godot.Collections.Dictionary nodeDict)
	{
		List<DialogueOption> options = new List<DialogueOption>();
		Godot.Collections.Array optionArray = nodeDict.TryGetValue("options", out Variant optionsVariant)
			? optionsVariant.AsGodotArray()
			: new Godot.Collections.Array();
		foreach (Variant optionVariant in optionArray)
		{
			Godot.Collections.Dictionary optionDict = optionVariant.AsGodotDictionary();
			string text = optionDict.TryGetValue("text", out Variant textVariant)
				? textVariant.AsString()
				: string.Empty;
			string nextNodeId = optionDict.TryGetValue("next", out Variant nextVariant)
				? nextVariant.AsString()
				: "End";
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}

			options.Add(new DialogueOption
			{
				Text = text,
				NextNodeId = string.IsNullOrWhiteSpace(nextNodeId) ? "End" : nextNodeId,
				RequiredFlags = ParseStringList(optionDict, "required_flags"),
				BlockedFlags = ParseStringList(optionDict, "blocked_flags"),
				SetFlags = ParseStringList(optionDict, "set_flags"),
				QuestToTrigger = optionDict.TryGetValue("quest_to_trigger", out Variant optionQuestVariant)
					? optionQuestVariant.AsString()
					: string.Empty
			});
		}

		return options;
	}

	private static List<string> ParseStringList(Godot.Collections.Dictionary dict, string key)
	{
		List<string> values = new List<string>();
		Godot.Collections.Array array = dict.TryGetValue(key, out Variant variant)
			? variant.AsGodotArray()
			: new Godot.Collections.Array();
		foreach (Variant item in array)
		{
			string value = item.AsString();
			if (!string.IsNullOrWhiteSpace(value))
			{
				values.Add(value);
			}
		}

		return values;
	}
}
