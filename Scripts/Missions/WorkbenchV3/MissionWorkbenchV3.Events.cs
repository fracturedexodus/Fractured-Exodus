using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchV3
{
	private static readonly string[] WhenChoices =
	{
		"Player enters", "Player uses", "Player talks to", "Player collects", "Enemies are defeated", "Mission starts"
	};

	private static readonly string[] ActionChoices =
	{
		"Open door", "Unlock object", "Enable object", "Spawn enemies", "Start conversation", "Change objective", "Show message", "Complete mission"
	};

	private void BuildEventsWorkspace()
	{
		VBoxContainer root = new();
		root.AddThemeConstantOverride("separation", 12);
		_eventsPage.AddChild(root);
		HBoxContainer header = new();
		root.AddChild(header);
		Control heading = Heading("Mission Events", "Build the mission as readable When → Then sentences. Internal flags and target names are created automatically.");
		heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(heading);
		header.AddChild(Button("+ Add Event", AddBlankRule, 130, new Color(.68f, .42f, .18f)));
		ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		root.AddChild(scroll);
		_eventTimeline = new VBoxContainer();
		_eventTimeline.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_eventTimeline);
	}

	private void AddBlankRule()
	{
		MissionMapElement source = _store.Document?.Elements.FirstOrDefault(IsUsefulEventElement);
		_store.Document?.Authoring.Rules.Add(NewRule(source?.Id));
		MarkAuthoringChanged("New event added.");
	}

	private MissionRuleData NewRule(string sourceId = "")
	{
		string id = Guid.NewGuid().ToString("N")[..10];
		return new MissionRuleData
		{
			Id = id,
			Label = $"Event {_store.Document?.Authoring.Rules.Count + 1}",
			WhenType = "Player uses",
			SourceElementId = sourceId ?? string.Empty,
			ActionType = "Open door"
		};
	}

	private void RefreshEventTimeline()
	{
		if (_eventTimeline == null) return;
		ClearChildren(_eventTimeline);
		List<MissionRuleData> rules = _store.Document?.Authoring.Rules ?? new List<MissionRuleData>();
		if (rules.Count == 0)
		{
			VBoxContainer empty = new();
			empty.AddChild(Heading("No events yet", "Example: When the player uses the Power Console, then open the Archive Door."));
			empty.AddChild(Button("Create the First Event", AddBlankRule, 190));
			_eventTimeline.AddChild(empty);
			return;
		}

		for (int i = 0; i < rules.Count; i++) AddRuleCard(rules[i], i);
	}

	private void AddRuleCard(MissionRuleData rule, int index)
	{
		PanelContainer card = new();
		card.AddThemeStyleboxOverride("panel", PanelStyle(rule.Enabled ? new Color(.85f, .55f, .22f) : new Color(.36f, .39f, .44f), new Color(.055f, .063f, .08f, .98f)));
		_eventTimeline.AddChild(card);
		VBoxContainer root = new();
		root.AddThemeConstantOverride("separation", 8);
		card.AddChild(root);

		HBoxContainer titleRow = new();
		root.AddChild(titleRow);
		CheckBox enabled = new() { Text = $"Step {index + 1}", ButtonPressed = rule.Enabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		enabled.Toggled += value => { rule.Enabled = value; MarkAuthoringChanged(); };
		titleRow.AddChild(enabled);
		titleRow.AddChild(Button("↑", () => MoveRule(index, -1), 42));
		titleRow.AddChild(Button("↓", () => MoveRule(index, 1), 42));
		titleRow.AddChild(Button("Remove", () => RemoveRule(rule), 80));

		HBoxContainer sentence = new();
		sentence.AddThemeConstantOverride("separation", 7);
		root.AddChild(sentence);
		Label whenLabel = new() { Text = "WHEN", VerticalAlignment = VerticalAlignment.Center };
		whenLabel.AddThemeColorOverride("font_color", new Color(1f, .72f, .35f));
		sentence.AddChild(whenLabel);
		OptionButton when = Choice(WhenChoices, rule.WhenType, 175);
		sentence.AddChild(when);
		OptionButton source = ElementChoice(rule.SourceElementId, IsUsefulEventElement, "Choose an object or area…", 280);
		sentence.AddChild(source);
		Label thenLabel = new() { Text = "THEN", VerticalAlignment = VerticalAlignment.Center };
		thenLabel.AddThemeColorOverride("font_color", new Color(.45f, .9f, 1f));
		sentence.AddChild(thenLabel);
		OptionButton action = Choice(ActionChoices, rule.ActionType, 190);
		sentence.AddChild(action);

		OptionButton target = null;
		if (RuleNeedsTarget(rule.ActionType))
		{
			target = ElementChoice(rule.TargetElementId, element => TargetMatchesAction(rule.ActionType, element), "Choose the target…", 260);
			sentence.AddChild(target);
		}

		Control valueEditor = BuildRuleValueEditor(rule);
		if (valueEditor != null) root.AddChild(valueEditor);
		Label preview = new() { Text = "Result: " + RuleSentence(rule), AutowrapMode = TextServer.AutowrapMode.WordSmart };
		preview.AddThemeColorOverride("font_color", new Color(.68f, .8f, .92f));
		root.AddChild(preview);

		when.ItemSelected += selected => { rule.WhenType = when.GetItemText((int)selected); MarkAuthoringChanged(); };
		source.ItemSelected += selected => { rule.SourceElementId = source.GetItemMetadata((int)selected).AsString(); MarkAuthoringChanged(); };
		action.ItemSelected += selected => { rule.ActionType = action.GetItemText((int)selected); rule.TargetElementId = string.Empty; MarkAuthoringChanged(); };
		if (target != null) target.ItemSelected += selected => { rule.TargetElementId = target.GetItemMetadata((int)selected).AsString(); MarkAuthoringChanged(); };
	}

	private Control BuildRuleValueEditor(MissionRuleData rule)
	{
		if (rule.ActionType == "Start conversation")
		{
			HBoxContainer row = new();
			row.AddChild(new Label { Text = "Conversation", CustomMinimumSize = new Vector2(120, 0) });
			OptionButton choices = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			choices.AddItem("Choose a conversation…");
			choices.SetItemMetadata(0, string.Empty);
			int selected = 0;
			foreach (string id in DialogueRegistry.GetConversationIds())
			{
				int item = choices.ItemCount;
				choices.AddItem(FriendlyConversationName(id));
				choices.SetItemMetadata(item, id);
				if (id == rule.Value) selected = item;
			}
			choices.Select(selected);
			choices.ItemSelected += item => { rule.Value = choices.GetItemMetadata((int)item).AsString(); MarkAuthoringChanged(); };
			row.AddChild(choices);
			row.AddChild(Button("New Conversation", () => CreateConversationForRule(rule), 165));
			return row;
		}
		if (rule.ActionType is "Change objective" or "Show message")
		{
			HBoxContainer row = new();
			row.AddChild(new Label { Text = rule.ActionType == "Change objective" ? "New objective" : "Message", CustomMinimumSize = new Vector2(120, 0) });
			LineEdit value = new() { Text = rule.Value, PlaceholderText = rule.ActionType == "Change objective" ? "Reach the evacuation area" : "The archive door is now open", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			value.FocusExited += () => { if (rule.Value != value.Text.Trim()) { rule.Value = value.Text.Trim(); MarkAuthoringChanged(); } };
			value.TextSubmitted += _ => { rule.Value = value.Text.Trim(); MarkAuthoringChanged(); };
			row.AddChild(value);
			return row;
		}
		return null;
	}

	private OptionButton ElementChoice(string selectedId, Func<MissionMapElement, bool> filter, string placeholder, float width)
	{
		OptionButton option = new() { CustomMinimumSize = new Vector2(width, 36) };
		option.AddItem(placeholder);
		option.SetItemMetadata(0, string.Empty);
		int selected = 0;
		foreach (MissionMapElement element in _store.Document?.Elements.Where(filter).OrderBy(DisplayName) ?? Enumerable.Empty<MissionMapElement>())
		{
			int item = option.ItemCount;
			option.AddItem(DisplayName(element));
			option.SetItemMetadata(item, element.Id);
			if (element.Id == selectedId) selected = item;
		}
		option.Select(selected);
		return option;
	}

	private static OptionButton Choice(IEnumerable<string> choices, string selectedText, float width)
	{
		OptionButton option = new() { CustomMinimumSize = new Vector2(width, 36) };
		int selected = 0, index = 0;
		foreach (string choice in choices)
		{
			option.AddItem(choice);
			if (choice == selectedText) selected = index;
			index++;
		}
		option.Select(selected);
		return option;
	}

	private void MoveRule(int index, int delta)
	{
		List<MissionRuleData> rules = _store.Document.Authoring.Rules;
		int target = Math.Clamp(index + delta, 0, rules.Count - 1);
		if (target == index) return;
		MissionRuleData rule = rules[index];
		rules.RemoveAt(index);
		rules.Insert(target, rule);
		MarkAuthoringChanged("Mission sequence reordered.");
	}

	private void RemoveRule(MissionRuleData rule)
	{
		_store.Document.Authoring.Rules.Remove(rule);
		MarkAuthoringChanged("Event removed.");
	}

	private string RuleSentence(MissionRuleData rule)
	{
		MissionMapElement source = _store.Document?.Elements.FirstOrDefault(e => e.Id == rule.SourceElementId);
		MissionMapElement target = _store.Document?.Elements.FirstOrDefault(e => e.Id == rule.TargetElementId);
		string sentence = $"{rule.WhenType} {(source == null ? "something" : DisplayName(source))}, {rule.ActionType.ToLowerInvariant()}";
		if (target != null) sentence += $" {DisplayName(target)}";
		if (!string.IsNullOrWhiteSpace(rule.Value)) sentence += $" — {FriendlyConversationName(rule.Value)}";
		return sentence + ".";
	}

	private static bool IsUsefulEventElement(MissionMapElement element) => element != null && !IsFloor(element) && !(element.Kind == MissionMapElementKind.Tile && MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition tile) && tile.Category == MissionTileCategory.Wall);
	private static bool TargetMatchesAction(string action, MissionMapElement element) => action switch
	{
		"Open door" => IsDoor(element),
		"Spawn enemies" => element.MarkerId == "hostile_spawn",
		"Unlock object" or "Enable object" => IsUsefulEventElement(element),
		_ => true
	};
}
