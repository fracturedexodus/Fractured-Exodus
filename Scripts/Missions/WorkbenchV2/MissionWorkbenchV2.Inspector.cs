using Godot;
using System;

public partial class MissionWorkbenchV2
{
	private void RefreshInspector()
	{
		foreach (Node child in _inspectorFields.GetChildren()) { _inspectorFields.RemoveChild(child); child.QueueFree(); }
		MissionMapElement selected = _store.GetSelectedElement();
		if (selected == null)
		{
			_selectionLabel.Text = _store.Document == null ? "No mission loaded" : $"Mission: {_store.Document.Metadata.Title}\n{_store.Document.Elements.Count} map elements";
			return;
		}

		_selectionLabel.Text = $"{selected.Kind}: {GetElementDisplayName(selected)}\nID: {selected.Id}";
		LineEdit column = AddInspectorField("Column", selected.Column.ToString());
		LineEdit row = AddInspectorField("Row", selected.Row.ToString());
		LineEdit offsetX = AddInspectorField("Offset X", selected.OffsetX.ToString("0.###"));
		LineEdit offsetY = AddInspectorField("Offset Y", selected.OffsetY.ToString("0.###"));
		LineEdit rotation = AddInspectorField("Rotation", selected.RotationDegrees.ToString("0.###"));
		LineEdit label = AddInspectorField("Label", selected.Logic.Label);
		LineEdit target = AddInspectorField("Target ID", selected.Logic.TargetId);
		LineEdit role = AddInspectorField("Logic Role", selected.Logic.Role);
		LineEdit requiredFlag = AddInspectorField("Required Flags", selected.Logic.RequiredFlag);
		LineEdit setFlag = AddInspectorField("Set Flags", selected.Logic.SetFlag);
		LineEdit propPath = AddInspectorField("Prop Definition", selected.PropDefinitionPath);
		LineEdit npcPath = AddInspectorField("NPC Definition", selected.NpcDefinitionPath);
		LineEdit notes = AddInspectorField("Notes", selected.Logic.Notes);

		Label triggerLabel = new Label { Text = "Trigger Mode" };
		_inspectorFields.AddChild(triggerLabel);
		OptionButton trigger = new OptionButton();
		foreach (string option in new[] { "none", "enter", "interact" }) trigger.AddItem(option);
		int triggerIndex = Array.IndexOf(new[] { "none", "enter", "interact" }, selected.Logic.TriggerMode);
		trigger.Select(Math.Max(0, triggerIndex));
		_inspectorFields.AddChild(trigger);
		CheckBox oneShot = new CheckBox { Text = "One Shot", ButtonPressed = selected.Logic.OneShot };
		_inspectorFields.AddChild(oneShot);

		Button apply = BuildButton("Apply Changes", () =>
		{
			MissionMapElement before = _store.GetSelectedElement();
			if (before == null) return;
			MissionMapElement after = MissionElementCloner.Clone(before);
			if (int.TryParse(column.Text, out int parsedColumn)) after.Column = parsedColumn;
			if (int.TryParse(row.Text, out int parsedRow)) after.Row = parsedRow;
			if (float.TryParse(offsetX.Text, out float parsedOffsetX)) after.OffsetX = parsedOffsetX;
			if (float.TryParse(offsetY.Text, out float parsedOffsetY)) after.OffsetY = parsedOffsetY;
			if (float.TryParse(rotation.Text, out float parsedRotation)) after.RotationDegrees = parsedRotation;
			after.Logic.Label = label.Text.Trim();
			after.Logic.TargetId = target.Text.Trim();
			after.Logic.Role = role.Text.Trim();
			after.Logic.RequiredFlag = requiredFlag.Text.Trim();
			after.Logic.SetFlag = setFlag.Text.Trim();
			after.PropDefinitionPath = propPath.Text.Trim();
			after.NpcDefinitionPath = npcPath.Text.Trim();
			after.Logic.Notes = notes.Text;
			after.Logic.TriggerMode = trigger.GetItemText(trigger.Selected);
			after.Logic.OneShot = oneShot.ButtonPressed;
			_store.Execute(new ReplaceMissionElementCommand(before, after));
			SetStatus($"Updated {GetElementDisplayName(after)}.");
		});
		apply.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_inspectorFields.AddChild(apply);
	}

	private LineEdit AddInspectorField(string label, string value)
	{
		_inspectorFields.AddChild(new Label { Text = label });
		LineEdit edit = new LineEdit { Text = value ?? string.Empty, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_inspectorFields.AddChild(edit);
		return edit;
	}
}
