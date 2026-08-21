using Godot;
using System;
using System.Linq;

public partial class MissionWorkbenchV3
{
	private void RefreshFriendlyInspector()
	{
		if (_inspectorContent == null) return;
		ClearChildren(_inspectorContent);
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null)
		{
			Label empty = new()
			{
				Text = "Click an object on the map to change it.\n\nUse the visual cards on the left to add something new.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(350, 0),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			empty.AddThemeColorOverride("font_color", new Color(.68f, .78f, .9f));
			_inspectorContent.AddChild(empty);
			return;
		}

		string kind = FriendlyElementKind(selected);
		Label kindLabel = new() { Text = kind };
		kindLabel.AddThemeFontSizeOverride("font_size", 20);
		_inspectorContent.AddChild(kindLabel);
		LineEdit name = InspectorLine("Name shown to the player", DisplayName(selected));

		OptionButton behavior = null;
		OptionButton conversation = null;
		OptionButton unlockEvent = null;
		CheckBox oneUse = null;
		TextEdit notes = null;

		if (IsDoor(selected))
		{
			_inspectorContent.AddChild(new Label { Text = "Door behavior" });
			behavior = new OptionButton();
			foreach (string item in new[] { "Closed — player can open it", "Locked until an event", "Decorative — cannot be used" }) behavior.AddItem(item);
			behavior.Select(string.IsNullOrWhiteSpace(selected.Logic.RequiredFlag) ? 0 : 1);
			_inspectorContent.AddChild(behavior);
			_inspectorContent.AddChild(new Label { Text = "Unlock after" });
			unlockEvent = new OptionButton();
			unlockEvent.AddItem("Choose an event…");
			foreach (MissionRuleData rule in _store.Document.Authoring.Rules) unlockEvent.AddItem(RuleSentence(rule));
			int matching = _store.Document.Authoring.Rules.FindIndex(rule => SplitFlags(selected.Logic.RequiredFlag).Contains(RuleFlag(rule)));
			unlockEvent.Select(matching + 1);
			_inspectorContent.AddChild(unlockEvent);
		}
		else if (selected.MarkerId is "npc_spawn" or "hostile_spawn")
		{
			_inspectorContent.AddChild(new Label { Text = selected.MarkerId == "hostile_spawn" ? "This character is an enemy." : "This character is friendly." });
			_inspectorContent.AddChild(new Label { Text = "Conversation" });
			conversation = new OptionButton();
			conversation.AddItem("No conversation");
			foreach (string id in DialogueRegistry.GetConversationIds()) conversation.AddItem(FriendlyConversationName(id));
			int conversationIndex = DialogueRegistry.GetConversationIds().ToList().FindIndex(id => id == selected.Logic.TargetId);
			conversation.Select(conversationIndex + 1);
			_inspectorContent.AddChild(conversation);
			_inspectorContent.AddChild(Button("Create a Conversation", () => { CreateConversationForElement(selected); SetPage(WorkspacePage.Conversations); }, 210));
		}
		else if (selected.Kind == MissionMapElementKind.Marker)
		{
			_inspectorContent.AddChild(new Label { Text = "Activates when" });
			behavior = new OptionButton();
			foreach (string item in new[] { "Nothing — visual marker only", "Player enters this area", "Player interacts here" }) behavior.AddItem(item);
			behavior.Select(selected.Logic.TriggerMode == "enter" ? 1 : selected.Logic.TriggerMode == "interact" ? 2 : 0);
			_inspectorContent.AddChild(behavior);
			oneUse = new CheckBox { Text = "Only activate once", ButtonPressed = selected.Logic.OneShot };
			_inspectorContent.AddChild(oneUse);
		}
		else if (IsInteractiveProp(selected))
		{
			_inspectorContent.AddChild(new Label { Text = "Player interaction" });
			behavior = new OptionButton();
			foreach (string item in new[] { "Decoration only", "Player can use it", "Starts an event" }) behavior.AddItem(item);
			behavior.Select(selected.Logic.TriggerMode == "interact" ? 1 : 0);
			_inspectorContent.AddChild(behavior);
			oneUse = new CheckBox { Text = "Can only be used once", ButtonPressed = selected.Logic.OneShot };
			_inspectorContent.AddChild(oneUse);
		}

		_inspectorContent.AddChild(new Label { Text = "Author notes (optional)" });
		notes = new TextEdit { Text = selected.Logic.Notes, CustomMinimumSize = new Vector2(0, 75), WrapMode = TextEdit.LineWrappingMode.Boundary };
		_inspectorContent.AddChild(notes);
		_inspectorContent.AddChild(Button("Add Event for This Object", () => AddRuleForElement(selected), 230, new Color(.48f, .34f, .68f)));

		VBoxContainer advanced = new() { Visible = false };
		CheckButton advancedToggle = new() { Text = "Advanced placement" };
		advancedToggle.Toggled += open => advanced.Visible = open;
		_inspectorContent.AddChild(advancedToggle);
		_inspectorContent.AddChild(advanced);
		LineEdit column = InspectorLine(advanced, "Column", selected.Column.ToString());
		LineEdit row = InspectorLine(advanced, "Row", selected.Row.ToString());
		LineEdit rotation = InspectorLine(advanced, "Rotation", selected.RotationDegrees.ToString("0.##"));
		LineEdit targetId = InspectorLine(advanced, "Internal target name", selected.Logic.TargetId);
		LineEdit npcPath = InspectorLine(advanced, "Character definition path", selected.NpcDefinitionPath);
		LineEdit propPath = InspectorLine(advanced, "Prop definition path", selected.PropDefinitionPath);

		Button apply = Button("Apply Changes", () =>
		{
			MissionMapElement before = _store.GetSelectedElement();
			if (before == null) return;
			MissionMapElement after = MissionElementCloner.Clone(before);
			after.Logic.Label = name.Text.Trim();
			after.Logic.Notes = notes.Text;
			if (int.TryParse(column.Text, out int c)) after.Column = c;
			if (int.TryParse(row.Text, out int r)) after.Row = r;
			if (float.TryParse(rotation.Text, out float angle)) after.RotationDegrees = angle;
			after.Logic.TargetId = targetId.Text.Trim();
			after.NpcDefinitionPath = npcPath.Text.Trim();
			after.PropDefinitionPath = propPath.Text.Trim();

			if (IsDoor(after))
			{
				if (behavior.Selected == 1 && unlockEvent.Selected > 0)
					after.Logic.RequiredFlag = RuleFlag(_store.Document.Authoring.Rules[unlockEvent.Selected - 1]);
				else if (behavior.Selected == 0) after.Logic.RequiredFlag = RemoveGeneratedFlags(after.Logic.RequiredFlag);
				after.Logic.TriggerMode = behavior.Selected == 2 ? "none" : "interact";
			}
			else if (after.MarkerId is "npc_spawn" or "hostile_spawn")
			{
				if (conversation.Selected > 0)
				{
					string id = DialogueRegistry.GetConversationIds()[conversation.Selected - 1];
					after.Logic.TargetId = id;
					if (!_store.Document.DialogueConversationIds.Contains(id)) _store.Document.DialogueConversationIds.Add(id);
				}
			}
			else if (behavior != null)
			{
				after.Logic.TriggerMode = behavior.Selected switch { 1 => after.Kind == MissionMapElementKind.Marker ? "enter" : "interact", 2 => "interact", _ => "none" };
				after.Logic.OneShot = oneUse?.ButtonPressed ?? false;
			}
			_store.Execute(new ReplaceMissionElementCommand(before, after));
			SetStatus($"Updated {after.Logic.Label}.");
		}, 180, new Color(.42f, .29f, .64f));
		apply.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_inspectorContent.AddChild(apply);
		_inspectorContent.AddChild(Button("Remove from Map", DeleteSelectedElement, 180));
	}

	private LineEdit InspectorLine(string label, string value) => InspectorLine(_inspectorContent, label, value);

	private static LineEdit InspectorLine(VBoxContainer parent, string label, string value)
	{
		parent.AddChild(new Label { Text = label });
		LineEdit edit = new() { Text = value ?? string.Empty, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		parent.AddChild(edit);
		return edit;
	}

	private void AddRuleForElement(MissionMapElement element)
	{
		MissionRuleData rule = NewRule(element.Id);
		_store.Document.Authoring.Rules.Add(rule);
		MarkAuthoringChanged("Event added. Complete the sentence to decide what happens.");
		SetPage(WorkspacePage.Events);
	}

	private static bool IsDoor(MissionMapElement element) => element.Kind == MissionMapElementKind.Tile && element.TileId.StartsWith("door_", StringComparison.Ordinal);
	private static bool IsInteractiveProp(MissionMapElement element) => element.Kind == MissionMapElementKind.PlacedProp || element.Kind == MissionMapElementKind.Tile && MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition d) && d.Category == MissionTileCategory.Prop;
	private static string FriendlyElementKind(MissionMapElement element)
	{
		if (IsDoor(element)) return "Door";
		if (element.MarkerId == "npc_spawn") return "Friendly Character";
		if (element.MarkerId == "hostile_spawn") return "Enemy";
		if (element.MarkerId.StartsWith("objective_")) return "Mission Goal";
		if (element.MarkerId == "evac_zone") return "Evacuation Area";
		if (element.Kind == MissionMapElementKind.Marker) return "Gameplay Area";
		if (IsFloor(element)) return "Floor";
		return IsInteractiveProp(element) ? "Object" : "Wall";
	}
}
