using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchV3
{
	private ItemList _conversationList;
	private ItemList _conversationNodeList;
	private LineEdit _conversationSpeaker;
	private TextEdit _conversationText;
	private VBoxContainer _responseEditor;
	private VBoxContainer _branchPreview;
	private DialogueConversationData _activeConversation;
	private string _activeConversationNodeId = string.Empty;
	private double _dialogueAutosaveCountdown = -1d;
	private bool _suppressDialogueSignals;

	private void BuildConversationWorkspace()
	{
		VBoxContainer outer = new();
		outer.AddThemeConstantOverride("separation", 10);
		_conversationPage.AddChild(outer);
		HBoxContainer header = new();
		outer.AddChild(header);
		Control heading = Heading("Conversations", "Write dialogue, player responses, branches, conditions, and consequences without editing JSON.");
		heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(heading);
		header.AddChild(Button("+ New Conversation", CreateStandaloneConversation, 180, new Color(.24f, .62f, .49f)));

		HSplitContainer split = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		outer.AddChild(split);
		VBoxContainer navigation = new() { CustomMinimumSize = new Vector2(310, 0) };
		split.AddChild(navigation);
		navigation.AddChild(new Label { Text = "Conversations used by this mission" });
		_conversationList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 160) };
		_conversationList.ItemSelected += SelectConversation;
		navigation.AddChild(_conversationList);
		navigation.AddChild(new Label { Text = "Moments in this conversation" });
		_conversationNodeList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 220) };
		_conversationNodeList.ItemSelected += SelectConversationNode;
		navigation.AddChild(_conversationNodeList);
		HBoxContainer nodeButtons = new();
		nodeButtons.AddChild(Button("+ Add Moment", AddConversationNode, 135));
		nodeButtons.AddChild(Button("Remove", DeleteConversationNode, 90));
		navigation.AddChild(nodeButtons);

		TabContainer editorTabs = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		split.AddChild(editorTabs);
		ScrollContainer writeScroll = new() { Name = "Write", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		editorTabs.AddChild(writeScroll);
		VBoxContainer write = new();
		write.AddThemeConstantOverride("separation", 8);
		writeScroll.AddChild(write);
		write.AddChild(new Label { Text = "Speaker" });
		_conversationSpeaker = new LineEdit { PlaceholderText = "Character name" };
		_conversationSpeaker.TextChanged += _ => ScheduleDialogueSave();
		write.AddChild(_conversationSpeaker);
		write.AddChild(new Label { Text = "What they say" });
		_conversationText = new TextEdit { CustomMinimumSize = new Vector2(0, 150), WrapMode = TextEdit.LineWrappingMode.Boundary };
		_conversationText.TextChanged += () => ScheduleDialogueSave();
		write.AddChild(_conversationText);
		HBoxContainer responseHeader = new();
		responseHeader.AddChild(new Label { Text = "Player responses", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		responseHeader.AddChild(Button("+ Add Response", AddDialogueResponse, 150));
		write.AddChild(responseHeader);
		_responseEditor = new VBoxContainer();
		_responseEditor.AddThemeConstantOverride("separation", 8);
		write.AddChild(_responseEditor);
		write.AddChild(Button("Save Conversation Now", SaveActiveConversation, 190, new Color(.2f, .58f, .45f)));

		ScrollContainer previewScroll = new() { Name = "Branch Map" };
		editorTabs.AddChild(previewScroll);
		_branchPreview = new VBoxContainer();
		_branchPreview.AddThemeConstantOverride("separation", 8);
		previewScroll.AddChild(_branchPreview);
	}

	private void RefreshConversationWorkspace()
	{
		if (_conversationList == null) return;
		string keep = _activeConversation?.ConversationId ?? string.Empty;
		_conversationList.Clear();
		List<string> ids = (_store.Document?.DialogueConversationIds ?? new List<string>()).Distinct().OrderBy(id => id).ToList();
		foreach (string id in ids)
		{
			int item = _conversationList.ItemCount;
			_conversationList.AddItem(FriendlyConversationName(id));
			_conversationList.SetItemMetadata(item, id);
			if (id == keep) _conversationList.Select(item);
		}
		if (ids.Count == 0)
		{
			_activeConversation = null;
			RefreshConversationNodeList();
		}
	}

	private void SelectConversation(long index)
	{
		if (index < 0 || index >= _conversationList.ItemCount) return;
		SaveActiveConversation();
		string id = _conversationList.GetItemMetadata((int)index).AsString();
		_activeConversation = DialogueRegistry.LoadConversationData(id);
		_activeConversationNodeId = _activeConversation?.Nodes.FirstOrDefault()?.Id ?? string.Empty;
		RefreshConversationNodeList();
	}

	private void RefreshConversationNodeList()
	{
		if (_conversationNodeList == null) return;
		_suppressDialogueSignals = true;
		_conversationNodeList.Clear();
		foreach (DialogueNode node in _activeConversation?.Nodes ?? new List<DialogueNode>())
		{
			string excerpt = string.IsNullOrWhiteSpace(node.Text) ? "Empty moment" : node.Text.Replace('\n', ' ');
			if (excerpt.Length > 38) excerpt = excerpt[..38] + "…";
			int item = _conversationNodeList.ItemCount;
			_conversationNodeList.AddItem($"{node.SpeakerName}: {excerpt}");
			_conversationNodeList.SetItemMetadata(item, node.Id);
			if (node.Id == _activeConversationNodeId) _conversationNodeList.Select(item);
		}
		LoadActiveNodeIntoEditor();
		RefreshBranchPreview();
		_suppressDialogueSignals = false;
	}

	private void SelectConversationNode(long index)
	{
		if (_suppressDialogueSignals || index < 0 || index >= _conversationNodeList.ItemCount) return;
		CaptureActiveNodeEdits();
		_activeConversationNodeId = _conversationNodeList.GetItemMetadata((int)index).AsString();
		LoadActiveNodeIntoEditor();
	}

	private void LoadActiveNodeIntoEditor()
	{
		DialogueNode node = ActiveDialogueNode();
		_suppressDialogueSignals = true;
		_conversationSpeaker.Text = node?.SpeakerName ?? string.Empty;
		_conversationText.Text = node?.Text ?? string.Empty;
		RefreshResponseEditor(node);
		_conversationSpeaker.Editable = node != null;
		_conversationText.Editable = node != null;
		_suppressDialogueSignals = false;
	}

	private void RefreshResponseEditor(DialogueNode node)
	{
		ClearChildren(_responseEditor);
		if (node == null) return;
		node.Options ??= new List<DialogueOption>();
		for (int i = 0; i < node.Options.Count; i++) AddResponseCard(node, node.Options[i], i);
		if (node.Options.Count == 0)
		{
			Label help = new() { Text = "No player responses. This moment ends the conversation.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
			help.AddThemeColorOverride("font_color", new Color(.65f, .75f, .86f));
			_responseEditor.AddChild(help);
		}
	}

	private void AddResponseCard(DialogueNode node, DialogueOption response, int index)
	{
		PanelContainer card = new();
		card.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.32f, .7f, .58f), new Color(.045f, .075f, .07f, .98f)));
		_responseEditor.AddChild(card);
		VBoxContainer root = new();
		root.AddThemeConstantOverride("separation", 6);
		card.AddChild(root);
		HBoxContainer textRow = new();
		root.AddChild(textRow);
		LineEdit text = new() { Text = response.Text, PlaceholderText = "What can the player say?", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		text.TextChanged += value => { response.Text = value; ScheduleDialogueSave(); };
		textRow.AddChild(text);
		textRow.AddChild(Button("Remove", () => { node.Options.Remove(response); ScheduleDialogueSave(true); RefreshResponseEditor(node); }, 82));

		HBoxContainer nextRow = new();
		root.AddChild(nextRow);
		nextRow.AddChild(new Label { Text = "Go to", CustomMinimumSize = new Vector2(90, 0) });
		OptionButton next = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		next.AddItem("End conversation");
		next.SetItemMetadata(0, "End");
		int nextSelected = 0;
		foreach (DialogueNode candidate in _activeConversation.Nodes)
		{
			int item = next.ItemCount;
			next.AddItem($"{candidate.SpeakerName}: {ShortText(candidate.Text)}");
			next.SetItemMetadata(item, candidate.Id);
			if (candidate.Id == response.NextNodeId) nextSelected = item;
		}
		next.Select(nextSelected);
		next.ItemSelected += item => { response.NextNodeId = next.GetItemMetadata((int)item).AsString(); ScheduleDialogueSave(true); };
		nextRow.AddChild(next);

		HBoxContainer conditionRow = new();
		root.AddChild(conditionRow);
		conditionRow.AddChild(new Label { Text = "Available", CustomMinimumSize = new Vector2(90, 0) });
		OptionButton condition = EventFlagChoice(response.RequiredFlags?.FirstOrDefault(), "Always available");
		condition.ItemSelected += item => { response.RequiredFlags = ChoiceFlag(condition, (int)item); ScheduleDialogueSave(); };
		conditionRow.AddChild(condition);
		conditionRow.AddChild(new Label { Text = "After choosing", CustomMinimumSize = new Vector2(110, 0) });
		OptionButton consequence = EventFlagChoice(response.SetFlags?.FirstOrDefault(), "No extra effect");
		consequence.ItemSelected += item => { response.SetFlags = ChoiceFlag(consequence, (int)item); ScheduleDialogueSave(); };
		conditionRow.AddChild(consequence);
	}

	private OptionButton EventFlagChoice(string selectedFlag, string emptyText)
	{
		OptionButton option = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		option.AddItem(emptyText);
		option.SetItemMetadata(0, string.Empty);
		int selected = 0;
		foreach (MissionRuleData rule in _store.Document?.Authoring.Rules ?? new List<MissionRuleData>())
		{
			int item = option.ItemCount;
			option.AddItem("Event: " + RuleSentence(rule));
			option.SetItemMetadata(item, RuleFlag(rule));
			if (RuleFlag(rule) == selectedFlag) selected = item;
		}
		option.Select(selected);
		return option;
	}

	private static List<string> ChoiceFlag(OptionButton option, int index)
	{
		string flag = option.GetItemMetadata(index).AsString();
		return string.IsNullOrWhiteSpace(flag) ? new List<string>() : new List<string> { flag };
	}

	private void RefreshBranchPreview()
	{
		if (_branchPreview == null) return;
		ClearChildren(_branchPreview);
		_branchPreview.AddChild(Heading("Conversation Branch Map", "Each response points to another moment or ends the conversation."));
		foreach (DialogueNode node in _activeConversation?.Nodes ?? new List<DialogueNode>())
		{
			PanelContainer card = new();
			card.AddThemeStyleboxOverride("panel", PanelStyle(new Color(.38f, .78f, .65f)));
			_branchPreview.AddChild(card);
			VBoxContainer body = new();
			card.AddChild(body);
			Label speaker = new() { Text = node.SpeakerName };
			speaker.AddThemeFontSizeOverride("font_size", 19);
			body.AddChild(speaker);
			body.AddChild(new Label { Text = ShortText(node.Text, 120), AutowrapMode = TextServer.AutowrapMode.WordSmart });
			if (node.Options.Count == 0) body.AddChild(new Label { Text = "↳ End conversation" });
			foreach (DialogueOption option in node.Options)
			{
				string destination = option.NextNodeId == "End" ? "End conversation" : NodeDisplayName(option.NextNodeId);
				body.AddChild(new Label { Text = $"↳ “{ShortText(option.Text, 65)}”  →  {destination}", AutowrapMode = TextServer.AutowrapMode.WordSmart });
			}
		}
	}

	private void AddDialogueResponse()
	{
		DialogueNode node = ActiveDialogueNode();
		if (node == null) return;
		node.Options.Add(new DialogueOption { Text = "New response", NextNodeId = "End" });
		RefreshResponseEditor(node);
		ScheduleDialogueSave(true);
	}

	private void AddConversationNode()
	{
		if (_activeConversation == null) return;
		CaptureActiveNodeEdits();
		string id;
		int number = _activeConversation.Nodes.Count + 1;
		do id = $"moment_{number++}"; while (_activeConversation.Nodes.Any(node => node.Id == id));
		_activeConversation.Nodes.Add(new DialogueNode { Id = id, SpeakerName = "Speaker", Text = "New dialogue moment." });
		_activeConversationNodeId = id;
		RefreshConversationNodeList();
		ScheduleDialogueSave(true);
	}

	private void DeleteConversationNode()
	{
		DialogueNode node = ActiveDialogueNode();
		if (node == null || _activeConversation.Nodes.Count <= 1) { SetStatus("A conversation needs at least one moment.", true); return; }
		_activeConversation.Nodes.Remove(node);
		foreach (DialogueNode remaining in _activeConversation.Nodes)
			foreach (DialogueOption option in remaining.Options.Where(option => option.NextNodeId == node.Id)) option.NextNodeId = "End";
		_activeConversationNodeId = _activeConversation.Nodes[0].Id;
		RefreshConversationNodeList();
		ScheduleDialogueSave(true);
	}

	private void CreateStandaloneConversation()
	{
		string baseName = NormalizeId(_store.Document?.Metadata.Title ?? "mission") + "_conversation";
		CreateConversation(baseName);
	}

	private void CreateConversationForElement(MissionMapElement element)
	{
		string id = CreateConversation(NormalizeId(element.Logic.Label) + "_conversation");
		MissionMapElement before = MissionElementCloner.Clone(element);
		MissionMapElement after = MissionElementCloner.Clone(element);
		after.Logic.TargetId = id;
		after.Logic.TriggerMode = "interact";
		_store.Execute(new ReplaceMissionElementCommand(before, after));
	}

	private void CreateConversationForRule(MissionRuleData rule)
	{
		rule.Value = CreateConversation(NormalizeId(_store.Document.Metadata.Title) + "_event_conversation");
		rule.ActionType = "Start conversation";
		MarkAuthoringChanged("Conversation connected to the event.");
		SetPage(WorkspacePage.Conversations);
	}

	private string CreateConversation(string baseId)
	{
		string id = string.IsNullOrWhiteSpace(baseId) ? "new_conversation" : baseId;
		int suffix = 2;
		while (DialogueRegistry.LoadConversationData(id) != null) id = $"{baseId}_{suffix++}";
		DialogueConversationData conversation = new()
		{
			ConversationId = id,
			Nodes = new List<DialogueNode> { new() { Id = "start", SpeakerName = "Speaker", Text = "Write the opening line here." } }
		};
		DialogueRegistry.SaveConversationData(conversation);
		if (!_store.Document.DialogueConversationIds.Contains(id)) _store.Document.DialogueConversationIds.Add(id);
		_activeConversation = conversation;
		_activeConversationNodeId = "start";
		MarkAuthoringChanged("New conversation created.");
		RefreshConversationWorkspace();
		RefreshConversationNodeList();
		return id;
	}

	private void ScheduleDialogueSave(bool refreshPreview = false)
	{
		if (_suppressDialogueSignals || _activeConversation == null) return;
		CaptureActiveNodeEdits();
		_dialogueAutosaveCountdown = refreshPreview ? .15d : .9d;
		_saveStateLabel.Text = "Saving conversation…";
		if (refreshPreview) RefreshBranchPreview();
	}

	private void ProcessDialogueAutosave(double delta)
	{
		if (_dialogueAutosaveCountdown < 0d) return;
		_dialogueAutosaveCountdown -= delta;
		if (_dialogueAutosaveCountdown <= 0d) SaveActiveConversation();
	}

	private void SaveActiveConversation()
	{
		_dialogueAutosaveCountdown = -1d;
		if (_activeConversation == null) return;
		CaptureActiveNodeEdits();
		if (DialogueRegistry.SaveConversationData(_activeConversation))
		{
			_saveStateLabel.Text = _store.IsDirty ? "Saving draft…" : "All changes saved";
			RefreshBranchPreview();
		}
		else SetStatus("The conversation could not be saved.", true);
	}

	private void CaptureActiveNodeEdits()
	{
		if (_suppressDialogueSignals) return;
		DialogueNode node = ActiveDialogueNode();
		if (node == null) return;
		node.SpeakerName = _conversationSpeaker.Text.Trim();
		node.Text = _conversationText.Text;
	}

	private DialogueNode ActiveDialogueNode() => _activeConversation?.Nodes.FirstOrDefault(node => node.Id == _activeConversationNodeId);
	private string NodeDisplayName(string id) { DialogueNode node = _activeConversation?.Nodes.FirstOrDefault(item => item.Id == id); return node == null ? "Missing moment" : $"{node.SpeakerName}: {ShortText(node.Text)}"; }
	private static string ShortText(string value, int max = 48) { string text = (value ?? string.Empty).Replace('\n', ' ').Trim(); return text.Length <= max ? text : text[..max] + "…"; }
	private static string FriendlyConversationName(string id) => string.Join(" ", (id ?? string.Empty).Split('_', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}
