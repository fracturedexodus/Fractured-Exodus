using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchV3NewMissionSmoke : Node
{
	public override async void _Ready()
	{
		string missionId = $"v3_creation_smoke_{DateTime.UtcNow:yyyyMMddHHmmss}";
		string templatePath = $"res://Data/Missions/Templates/{missionId}.tres";
		string documentPath = MissionDocumentSerializer.GetDocumentResourcePath(missionId);
		string layoutPath = $"res://Data/MissionLayouts/{missionId}_builder.json";
		List<string> errors = new();
		MissionWorkbenchV3 workbench = ResourceLoader.Load<PackedScene>("res://mission_workbench_v3.tscn")?.Instantiate<MissionWorkbenchV3>();
		if (workbench == null)
		{
			GD.PushError("Workbench v3 creation smoke could not instantiate the scene.");
			GetTree().Quit(1);
			return;
		}

		AddChild(workbench);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		Control mapChrome = workbench.FindChild("MapChrome", true, false) as Control;
		ScrollContainer inspectorScroll = workbench.FindChild("InspectorScroll", true, false) as ScrollContainer;
		Control inspectorContent = workbench.FindChild("InspectorContent", true, false) as Control;
		if (mapChrome?.MouseFilter != Control.MouseFilterEnum.Ignore) errors.Add("Map chrome intercepts center-map mouse input.");
		if (inspectorScroll?.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled) errors.Add("Inspector horizontal sizing is not constrained.");
		if (inspectorContent == null || inspectorContent.CustomMinimumSize.X < 350) errors.Add("Inspector content can collapse to a one-character width.");

		Button mapPageButton = Descendants<Button>(workbench).FirstOrDefault(button => button.Text.Contains("Build Map", StringComparison.Ordinal));
		Button characterCard = Descendants<Button>(workbench).FirstOrDefault(button => button.Text == "Friendly Character");
		Node markerLayer = workbench.GetNodeOrNull("World/MarkerLayer");
		Control mapBlocker = workbench.FindChild("MapBlocker", true, false) as Control;
		int markerCount = markerLayer?.GetChildCount() ?? -1;
		mapPageButton?.EmitSignal(BaseButton.SignalName.Pressed);
		characterCard?.EmitSignal(BaseButton.SignalName.Pressed);
		workbench._Input(new InputEventMouseButton { Position = new Vector2(900, 500), ButtonIndex = MouseButton.Left, Pressed = true });
		workbench._Input(new InputEventMouseButton { Position = new Vector2(900, 500), ButtonIndex = MouseButton.Left, Pressed = false });
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (mapPageButton == null) errors.Add("Build Map navigation button was not found.");
		if (characterCard == null) errors.Add("Friendly Character palette card was not found.");
		if (markerLayer == null) errors.Add("Marker map layer was not found.");
		if (mapBlocker?.Visible != false) errors.Add("Map blocker remained visible after opening Build Map.");
		if (markerLayer != null && markerLayer.GetChildCount() <= markerCount) errors.Add($"A center-map click did not place the selected palette object (markers {markerCount} → {markerLayer.GetChildCount()}).");

		ConfirmationDialog dialog = Descendants<ConfirmationDialog>(workbench).FirstOrDefault(item => item.Title == "Create and Save a New Mission");
		if (dialog == null) errors.Add("New Mission dialog was not created.");
		else
		{
			List<LineEdit> lines = Descendants<LineEdit>(dialog).ToList();
			List<TextEdit> textAreas = Descendants<TextEdit>(dialog).ToList();
			if (lines.Count < 2 || textAreas.Count < 2) errors.Add("New Mission dialog fields are incomplete.");
			else
			{
				lines[0].Text = "V3 Creation Smoke";
				lines[1].Text = missionId;
				textAreas[0].Text = "Temporary mission created by the automated smoke test.";
				textAreas[1].Text = "Reach the objective and evacuate.";
				dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
		}

		if (!FileAccess.FileExists(templatePath)) errors.Add("Mission template was not saved.");
		if (!FileAccess.FileExists(documentPath)) errors.Add("Mission source document was not saved.");
		if (!FileAccess.FileExists(layoutPath)) errors.Add("Compiled mission layout was not saved.");
		MissionTemplate template = new MissionRegistry().LoadTemplates().FirstOrDefault(item => item.MissionId == missionId);
		string interactionKey = $"mission:{missionId}";
		if (template == null) errors.Add("Saved mission was not discoverable through MissionRegistry.");
		else if (!template.InteractionKeys.Contains(interactionKey)) errors.Add("Saved mission is missing its overworld interaction key.");
		OverworldContentDefinition placement = new()
		{
			ContentId = missionId,
			DisplayName = "V3 Creation Smoke",
			ContentType = OverworldContentType.Mission,
			ContentReference = interactionKey,
			Placement = new OverworldPlacementRule { NodeType = OverworldPlacementNodeType.Planet }
		};
		if (OverworldContentValidator.Validate(new[] { placement }).Any(issue => issue.Severity == OverworldContentIssueSeverity.Error))
			errors.Add("The new mission could not be selected by a valid overworld placement definition.");

		foreach (string path in new[] { templatePath, documentPath, layoutPath })
		{
			if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
		}
		GetNodeOrNull<MissionManager>("/root/MissionManager")?.ReloadTemplates();
		if (errors.Count == 0) GD.Print("Mission Workbench v3 new-mission smoke passed; temporary mission files were removed.");
		else foreach (string error in errors) GD.PushError(error);
		GetTree().Quit(errors.Count == 0 ? 0 : 1);
	}

	private static IEnumerable<T> Descendants<T>(Node root) where T : Node
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is T typed) yield return typed;
			foreach (T descendant in Descendants<T>(child)) yield return descendant;
		}
	}
}
