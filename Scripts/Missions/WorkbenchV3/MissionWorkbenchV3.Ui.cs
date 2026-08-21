using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchV3
{
	private Button _undoButton;
	private Button _redoButton;
	private LineEdit _detailTitleEdit;
	private TextEdit _detailBriefingEdit;
	private TextEdit _detailObjectiveEdit;
	private SpinBox _detailOfficerCount;
	private OptionButton _detailBackgroundOption;
	private VBoxContainer _detailsChecklist;
	private VBoxContainer _testChecklist;
	private VBoxContainer _eventTimeline;
	private VBoxContainer _inspectorContent;
	private FlowContainer _paletteGrid;
	private LineEdit _paletteSearch;
	private OptionButton _paletteCategory;
	private Label _toolHint;
	private readonly List<MissionValidationIssue> _visibleIssues = new();

	private void BuildUi()
	{
		CanvasLayer background = new() { Layer = -20 };
		AddChild(background);
		ColorRect fill = new() { Color = new Color(.014f, .021f, .038f), MouseFilter = Control.MouseFilterEnum.Ignore };
		fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		background.AddChild(fill);
		_uiLayer = new CanvasLayer { Name = "WorkbenchUI", Layer = 20 };
		AddChild(_uiLayer);

		BuildTopBar();
		BuildWorkspacePages();
		BuildMapChrome();
		BuildProblemBar();
		BuildWelcomeDialog();
		BuildNewMissionDialog();
	}

	private void BuildTopBar()
	{
		PanelContainer panel = AnchoredPanel(0f, 0f, 1f, 0f, 8f, 8f, -8f, 116f, new Color(.25f, .88f, 1f));
		VBoxContainer root = new();
		root.AddThemeConstantOverride("separation", 6);
		panel.AddChild(root);

		HBoxContainer toolbar = new();
		toolbar.AddThemeConstantOverride("separation", 8);
		root.AddChild(toolbar);
		Label title = new() { Text = "MISSION WORKBENCH", CustomMinimumSize = new Vector2(230, 0) };
		title.AddThemeFontSizeOverride("font_size", 20);
		toolbar.AddChild(title);
		_missionOption = new OptionButton { CustomMinimumSize = new Vector2(260, 38) };
		_missionOption.ItemSelected += SwitchMission;
		toolbar.AddChild(_missionOption);
		toolbar.AddChild(Button("+ New Mission", ShowNewMissionDialog, 132, new Color(.23f, .64f, .48f)));
		toolbar.AddChild(Button("Start Guide", ShowWelcomeGuide, 105));
		_undoButton = Button("Undo", _store.Undo, 86);
		_redoButton = Button("Redo", _store.Redo, 86);
		toolbar.AddChild(_undoButton);
		toolbar.AddChild(_redoButton);
		Control spacer = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		toolbar.AddChild(spacer);
		_saveStateLabel = new Label { Text = "All changes saved", VerticalAlignment = VerticalAlignment.Center };
		_saveStateLabel.AddThemeColorOverride("font_color", new Color(.55f, .82f, .72f));
		toolbar.AddChild(_saveStateLabel);
		toolbar.AddChild(Button("Test Mission", TestMission, 125, new Color(.19f, .58f, .78f)));
		toolbar.AddChild(Button("Publish Mission", PublishMission, 140, new Color(.22f, .68f, .43f)));
		toolbar.AddChild(Button("v2", OpenV2, 54));
		toolbar.AddChild(Button("Exit", ExitV3, 64));

		HBoxContainer navigation = new();
		navigation.AddThemeConstantOverride("separation", 7);
		root.AddChild(navigation);
		string[] names = { "1  Mission Details", "2  Build Map", "3  Mission Events", "4  Conversations", "5  Test & Publish" };
		_pageButtons = new Button[names.Length];
		for (int i = 0; i < names.Length; i++)
		{
			int pageIndex = i;
			Button nav = Button(names[i], () => SetPage((WorkspacePage)pageIndex), 180);
			nav.ToggleMode = true;
			nav.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_pageButtons[i] = nav;
			navigation.AddChild(nav);
		}
	}

	private void BuildWorkspacePages()
	{
		_mapBlocker = new ColorRect { Name = "MapBlocker", Color = new Color(.014f, .021f, .038f, .99f), MouseFilter = Control.MouseFilterEnum.Stop };
		_mapBlocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_mapBlocker.OffsetTop = 122f;
		_mapBlocker.OffsetBottom = -146f;
		_uiLayer.AddChild(_mapBlocker);

		_detailsPage = PagePanel(new Color(.29f, .73f, .98f));
		_eventsPage = PagePanel(new Color(.92f, .61f, .24f));
		_conversationPage = PagePanel(new Color(.46f, .86f, .72f));
		_testPage = PagePanel(new Color(.72f, .51f, .95f));
		BuildDetailsPage();
		BuildEventsWorkspace();
		BuildConversationWorkspace();
		BuildTestPage();
	}

	private Control PagePanel(Color border)
	{
		PanelContainer panel = new();
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panel.OffsetLeft = 8f;
		panel.OffsetTop = 122f;
		panel.OffsetRight = -8f;
		panel.OffsetBottom = -146f;
		panel.AddThemeStyleboxOverride("panel", PanelStyle(border));
		_uiLayer.AddChild(panel);
		return panel;
	}

	private void BuildDetailsPage()
	{
		HBoxContainer columns = new();
		columns.AddThemeConstantOverride("separation", 18);
		_detailsPage.AddChild(columns);

		VBoxContainer form = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		columns.AddChild(form);
		form.AddChild(Heading("Mission Details", "Describe the experience in plain language. Technical names are generated for you."));
		_detailTitleEdit = LabeledLine(form, "Mission name");
		_detailBriefingEdit = LabeledText(form, "Player briefing", 110);
		_detailObjectiveEdit = LabeledText(form, "Main objective", 90);
		form.AddChild(new Label { Text = "Number of officers" });
		_detailOfficerCount = new SpinBox { MinValue = 1, MaxValue = 8, Step = 1 };
		form.AddChild(_detailOfficerCount);
		form.AddChild(new Label { Text = "Location backdrop" });
		_detailBackgroundOption = new OptionButton();
		foreach (MissionBackgroundDefinition background in MissionBackgroundCatalog.All) _detailBackgroundOption.AddItem(background.DisplayName);
		form.AddChild(_detailBackgroundOption);
		Button apply = Button("Apply Mission Details", ApplyMissionDetails, 200, new Color(.18f, .55f, .76f));
		apply.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		form.AddChild(apply);

		VBoxContainer side = new() { CustomMinimumSize = new Vector2(470, 0) };
		columns.AddChild(side);
		side.AddChild(Heading("Getting Started", "Workbench keeps a safe draft automatically while you work."));
		_detailsChecklist = new VBoxContainer();
		_detailsChecklist.AddThemeConstantOverride("separation", 8);
		side.AddChild(_detailsChecklist);
		side.AddChild(new HSeparator());
		side.AddChild(Heading("Where Players Find It", "Choose the planet, region, campaign conditions, and appearance chance in the overworld placement screen."));
		side.AddChild(Button("Edit Overworld Placement", OpenOverworldPlacement, 220));
		side.AddChild(new HSeparator());
		side.AddChild(Heading("Quick Starts", "Replace the map with a working example. You can undo individual edits afterward."));
		foreach (string template in new[] { "Rescue Mission", "Investigation", "Combat Encounter", "Retrieve and Escape", "Blank Room" })
			side.AddChild(Button(template, () => RequestStarterTemplate(template), 210));
	}

	private void ApplyMissionDetails()
	{
		MissionDocument doc = _store.Document;
		if (doc == null) return;
		doc.Metadata.Title = _detailTitleEdit.Text.Trim();
		doc.Metadata.Description = _detailBriefingEdit.Text.Trim();
		doc.Metadata.PromptText = _detailBriefingEdit.Text.Trim();
		doc.Metadata.ObjectiveText = _detailObjectiveEdit.Text.Trim();
		doc.Metadata.RecommendedOfficerCount = (int)_detailOfficerCount.Value;
		if (_detailBackgroundOption.Selected >= 0 && _detailBackgroundOption.Selected < MissionBackgroundCatalog.All.Count)
			doc.Environment.BackgroundId = MissionBackgroundCatalog.All[_detailBackgroundOption.Selected].Id;
		MarkAuthoringChanged("Mission details updated.");
	}

	private void RefreshDetailsPage()
	{
		if (_detailTitleEdit == null || _store.Document == null) return;
		MissionDocument doc = _store.Document;
		_detailTitleEdit.Text = doc.Metadata.Title;
		_detailBriefingEdit.Text = string.IsNullOrWhiteSpace(doc.Metadata.PromptText) ? doc.Metadata.Description : doc.Metadata.PromptText;
		_detailObjectiveEdit.Text = doc.Metadata.ObjectiveText;
		_detailOfficerCount.Value = doc.Metadata.RecommendedOfficerCount;
		int bg = MissionBackgroundCatalog.All.ToList().FindIndex(item => item.Id == doc.Environment.BackgroundId);
		_detailBackgroundOption.Select(Math.Max(0, bg));
		RefreshDetailsSummary();
	}

	private void RefreshDetailsSummary()
	{
		if (_detailsChecklist == null || _store.Document == null) return;
		ClearChildren(_detailsChecklist);
		MissionDocument doc = _store.Document;
		AddChecklist(_detailsChecklist, !string.IsNullOrWhiteSpace(doc.Metadata.Title), "Name the mission", WorkspacePage.Details);
		AddChecklist(_detailsChecklist, doc.Elements.Any(IsFloor), "Build a playable floor", WorkspacePage.Map);
		AddChecklist(_detailsChecklist, doc.Elements.Any(e => e.MarkerId == "spawn_a") && doc.Elements.Any(e => e.MarkerId == "spawn_b"), "Place both officer starts", WorkspacePage.Map);
		AddChecklist(_detailsChecklist, doc.Elements.Any(e => e.MarkerId.StartsWith("objective_")), "Place an objective", WorkspacePage.Map);
		AddChecklist(_detailsChecklist, doc.Authoring.Rules.Count > 0, "Add a mission event", WorkspacePage.Events);
		AddChecklist(_detailsChecklist, doc.DialogueConversationIds.Count > 0, "Add a conversation", WorkspacePage.Conversations);
	}

	private void BuildTestPage()
	{
		VBoxContainer root = new();
		root.AddThemeConstantOverride("separation", 14);
		_testPage.AddChild(root);
		root.AddChild(Heading("Test & Publish", "Problems are checked continuously. Select a problem below to jump to the related map object."));
		_testChecklist = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		_testChecklist.AddThemeConstantOverride("separation", 9);
		root.AddChild(_testChecklist);
		HBoxContainer buttons = new();
		buttons.AddThemeConstantOverride("separation", 12);
		buttons.AddChild(Button("▶  Test Mission", TestMission, 210, new Color(.19f, .58f, .78f)));
		buttons.AddChild(Button("✓  Publish Mission", PublishMission, 220, new Color(.22f, .68f, .43f)));
		buttons.AddChild(Button("Check Again", () => RunFriendlyValidation(true), 130));
		root.AddChild(buttons);
	}

	private void RefreshTestPage()
	{
		if (_testChecklist == null || _store.Document == null) return;
		ClearChildren(_testChecklist);
		List<MissionValidationIssue> issues = MissionDocumentValidator.Validate(_store.Document);
		issues.AddRange(ValidateAuthoringRules());
		int errors = issues.Count(issue => issue.Severity == MissionValidationSeverity.Error);
		int warnings = issues.Count - errors;
		Label summary = new() { Text = errors == 0 ? "✓ This mission is ready to test." : $"{errors} item{(errors == 1 ? "" : "s")} must be fixed before testing." };
		summary.AddThemeFontSizeOverride("font_size", 24);
		summary.AddThemeColorOverride("font_color", errors == 0 ? new Color(.45f, .95f, .62f) : new Color(1f, .43f, .38f));
		_testChecklist.AddChild(summary);
		if (warnings > 0) _testChecklist.AddChild(new Label { Text = $"{warnings} suggestion{(warnings == 1 ? "" : "s")} can be reviewed but will not block testing." });
		foreach (MissionValidationIssue issue in issues.Take(12))
		{
			Button item = Button(FriendlyProblemText(issue), () => FocusProblem(issue), 0);
			item.Alignment = HorizontalAlignment.Left;
			item.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_testChecklist.AddChild(item);
		}
	}

	private void BuildProblemBar()
	{
		PanelContainer panel = AnchoredPanel(0f, 1f, 1f, 1f, 8f, -138f, -8f, -8f, new Color(1f, .58f, .28f));
		VBoxContainer root = new();
		panel.AddChild(root);
		HBoxContainer header = new();
		root.AddChild(header);
		_problemSummaryLabel = new Label { Text = "Mission check", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_problemSummaryLabel.AddThemeFontSizeOverride("font_size", 16);
		header.AddChild(_problemSummaryLabel);
		_statusLabel = new Label { Text = "Ready.", HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		header.AddChild(_statusLabel);
		_problemsList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		_problemsList.ItemActivated += index => { if (index >= 0 && index < _visibleIssues.Count) FocusProblem(_visibleIssues[(int)index]); };
		root.AddChild(_problemsList);
	}

	private void ShowFriendlyProblems(List<MissionValidationIssue> issues)
	{
		_visibleIssues.Clear();
		_visibleIssues.AddRange(issues);
		_problemsList.Clear();
		int errors = issues.Count(issue => issue.Severity == MissionValidationSeverity.Error);
		int warnings = issues.Count - errors;
		_problemSummaryLabel.Text = errors == 0 ? (warnings == 0 ? "✓ Ready to test" : $"✓ Ready • {warnings} suggestion{(warnings == 1 ? "" : "s")}") : $"● {errors} problem{(errors == 1 ? "" : "s")} to fix";
		foreach (MissionValidationIssue issue in issues) _problemsList.AddItem(FriendlyProblemText(issue));
		if (issues.Count == 0) _problemsList.AddItem("Everything looks good. Test the mission when you are ready.");
	}

	private static string FriendlyProblemText(MissionValidationIssue issue)
	{
		string text = issue.Message
			.Replace("spawn_a", "Officer Start 1")
			.Replace("spawn_b", "Officer Start 2")
			.Replace("evac_zone", "Evacuation Area")
			.Replace("Mission ID", "Mission name")
			.Replace("stable ID", "internal name");
		return $"{(issue.Severity == MissionValidationSeverity.Error ? "Fix" : "Suggestion")}: {text}";
	}

	private void FocusProblem(MissionValidationIssue issue)
	{
		if (!string.IsNullOrWhiteSpace(issue.ElementId))
		{
			_store.Select(issue.ElementId);
			SetPage(WorkspacePage.Map);
			MissionMapElement element = _store.GetSelectedElement();
			if (element != null) _camera.Position = CellToWorld(element.Column, element.Row);
		}
		else if (issue.RuleId.StartsWith("event.")) SetPage(WorkspacePage.Events);
		else if (issue.RuleId == "map.floor")
		{
			SetPage(WorkspacePage.Map);
			_placementAsset = "tile:floor_standard";
			SetMapTool(MapTool.Paint);
			SetStatus("Standard Floor selected. Paint a playable area on the map.");
		}
		else if (issue.RuleId == "map.required_marker")
		{
			string marker = issue.Message.Contains("spawn_b") ? "spawn_b" : "spawn_a";
			SetPage(WorkspacePage.Map);
			_placementAsset = $"marker:{marker}";
			SetMapTool(MapTool.Place);
			SetStatus($"{(marker == "spawn_a" ? "Officer Start 1" : "Officer Start 2")} selected. Click a floor cell to place it.");
		}
		else if (issue.RuleId == "map.evac")
		{
			SetPage(WorkspacePage.Map);
			_placementAsset = "marker:evac_zone";
			SetMapTool(MapTool.Place);
			SetStatus("Evacuation Area selected. Click a floor cell to place it.");
		}
		else if (issue.RuleId.StartsWith("mission.") || issue.RuleId == "environment.background") SetPage(WorkspacePage.Details);
		else if (issue.RuleId.StartsWith("dialogue.")) SetPage(WorkspacePage.Conversations);
	}

	private void BuildWelcomeDialog()
	{
		_welcomeDialog = new AcceptDialog { Title = "Create a mission without code", OkButtonText = "Create Starter Mission", MinSize = new Vector2I(760, 610) };
		_welcomeDialog.Confirmed += ApplyWelcomeGuide;
		VBoxContainer root = new();
		root.AddThemeConstantOverride("separation", 9);
		_welcomeDialog.AddChild(root);
		root.AddChild(new Label { Text = "Choose a starting point. Workbench will create the playable basics for the selected mission." });
		root.AddChild(new Label { Text = "Starting point" });
		_welcomeTemplateOption = new OptionButton();
		foreach (string item in new[] { "Rescue Mission", "Investigation", "Combat Encounter", "Retrieve and Escape", "Blank Room", "Keep Existing Map" }) _welcomeTemplateOption.AddItem(item);
		root.AddChild(_welcomeTemplateOption);
		_welcomeTitleEdit = LabeledLine(root, "Mission name");
		_welcomeBriefingEdit = LabeledText(root, "What should the player do?", 150);
		Label note = new() { Text = "You can change every choice later. Existing maps are only replaced after confirmation.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		note.AddThemeColorOverride("font_color", new Color(.68f, .78f, .9f));
		root.AddChild(note);
		AddChild(_welcomeDialog);

		_replaceMapConfirmation = new ConfirmationDialog { Title = "Replace the current map?", DialogText = "This quick start replaces the current map with a working starter layout.", OkButtonText = "Replace Map", CancelButtonText = "Cancel" };
		_replaceMapConfirmation.Confirmed += ApplyPendingStarterTemplate;
		AddChild(_replaceMapConfirmation);
	}

	private void ShowWelcomeGuide()
	{
		if (_store.Document == null) return;
		_welcomeTitleEdit.Text = _store.Document.Metadata.Title;
		_welcomeBriefingEdit.Text = _store.Document.Metadata.ObjectiveText;
		_welcomeDialog.PopupCentered(new Vector2I(780, 630));
	}

	private void BuildNewMissionDialog()
	{
		_newMissionDialog = new ConfirmationDialog
		{
			Title = "Create and Save a New Mission",
			OkButtonText = "Create Mission",
			CancelButtonText = "Cancel",
			MinSize = new Vector2I(800, 690)
		};
		_newMissionDialog.Confirmed += CreateNewMission;
		VBoxContainer root = new();
		root.AddThemeConstantOverride("separation", 8);
		_newMissionDialog.AddChild(root);
		root.AddChild(new Label
		{
			Text = "This creates a completely new mission with its own editable source, runtime layout, and Overworld interaction key.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});
		_newMissionNameEdit = LabeledLine(root, "Mission name");
		_newMissionIdEdit = LabeledLine(root, "Mission ID (used internally and in filenames)");
		_newMissionNameEdit.TextChanged += value =>
		{
			if (_updatingNewMissionId) return;
			_updatingNewMissionId = true;
			_newMissionIdEdit.Text = NormalizeMissionId(value);
			_updatingNewMissionId = false;
		};
		_newMissionBriefingEdit = LabeledText(root, "Player briefing", 105);
		_newMissionObjectiveEdit = LabeledText(root, "Main objective", 85);
		root.AddChild(new Label { Text = "Starter layout" });
		_newMissionStarterOption = new OptionButton();
		foreach (string starter in new[] { "Rescue Mission", "Investigation", "Combat Encounter", "Retrieve and Escape", "Blank Room" }) _newMissionStarterOption.AddItem(starter);
		root.AddChild(_newMissionStarterOption);
		root.AddChild(new Label { Text = "Number of officers" });
		_newMissionOfficerCount = new SpinBox { MinValue = 1, MaxValue = 8, Step = 1, Value = 2 };
		root.AddChild(_newMissionOfficerCount);
		_newMissionErrorLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		root.AddChild(_newMissionErrorLabel);
		Label note = new()
		{
			Text = "After creation, open Overworld Placement and choose this mission from the registered mission list. Creating the mission does not place it automatically.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		note.AddThemeColorOverride("font_color", new Color(.68f, .78f, .9f));
		root.AddChild(note);
		AddChild(_newMissionDialog);
	}

	private void ApplyWelcomeGuide()
	{
		if (_store.Document == null) return;
		_store.Document.Metadata.Title = _welcomeTitleEdit.Text.Trim();
		_store.Document.Metadata.ObjectiveText = _welcomeBriefingEdit.Text.Trim();
		_store.Document.Metadata.PromptText = _welcomeBriefingEdit.Text.Trim();
		string kind = _welcomeTemplateOption.GetItemText(_welcomeTemplateOption.Selected);
		if (kind != "Keep Existing Map") ApplyStarterTemplate(kind);
		_store.Document.Authoring.WelcomeCompleted = true;
		_store.Document.Authoring.TemplateKind = kind;
		MarkAuthoringChanged("Starter mission created. Build the map or add events next.");
		SetPage(WorkspacePage.Map);
	}

	private void RequestStarterTemplate(string kind)
	{
		_pendingStarterTemplate = kind;
		_replaceMapConfirmation.PopupCentered();
	}

	private void ApplyPendingStarterTemplate()
	{
		if (string.IsNullOrWhiteSpace(_pendingStarterTemplate)) return;
		string kind = _pendingStarterTemplate;
		_pendingStarterTemplate = string.Empty;
		ApplyStarterTemplate(kind);
		MarkAuthoringChanged($"{kind} starter applied.");
		SetPage(WorkspacePage.Map);
	}

	private void AddChecklist(VBoxContainer parent, bool complete, string text, WorkspacePage page)
	{
		Button row = Button($"{(complete ? "✓" : "○")}  {text}", () => SetPage(page), 0);
		row.Alignment = HorizontalAlignment.Left;
		row.Disabled = complete;
		row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		parent.AddChild(row);
	}

	private PanelContainer AnchoredPanel(float la, float ta, float ra, float ba, float left, float top, float right, float bottom, Color border)
	{
		PanelContainer panel = new() { AnchorLeft = la, AnchorTop = ta, AnchorRight = ra, AnchorBottom = ba, OffsetLeft = left, OffsetTop = top, OffsetRight = right, OffsetBottom = bottom };
		panel.AddThemeStyleboxOverride("panel", PanelStyle(border));
		_uiLayer.AddChild(panel);
		return panel;
	}

	private static Button Button(string text, Action action, float width = 96, Color? color = null)
	{
		Button button = new() { Text = text, CustomMinimumSize = new Vector2(width, 36) };
		if (color.HasValue) button.AddThemeStyleboxOverride("normal", PanelStyle(color.Value, new Color(color.Value.R * .35f, color.Value.G * .35f, color.Value.B * .35f, 1f)));
		button.Pressed += () => action?.Invoke();
		return button;
	}

	private static VBoxContainer Heading(string title, string subtitle)
	{
		VBoxContainer box = new();
		Label heading = new() { Text = title };
		heading.AddThemeFontSizeOverride("font_size", 25);
		box.AddChild(heading);
		Label help = new() { Text = subtitle, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		help.AddThemeColorOverride("font_color", new Color(.66f, .77f, .9f));
		box.AddChild(help);
		return box;
	}

	private static LineEdit LabeledLine(VBoxContainer parent, string label)
	{
		parent.AddChild(new Label { Text = label });
		LineEdit edit = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		parent.AddChild(edit);
		return edit;
	}

	private static TextEdit LabeledText(VBoxContainer parent, string label, float height)
	{
		parent.AddChild(new Label { Text = label });
		TextEdit edit = new() { CustomMinimumSize = new Vector2(0, height), WrapMode = TextEdit.LineWrappingMode.Boundary };
		parent.AddChild(edit);
		return edit;
	}

	private void RefreshUndoButtons()
	{
		if (_undoButton == null) return;
		_undoButton.Disabled = !_store.CanUndo;
		_redoButton.Disabled = !_store.CanRedo;
		_undoButton.Text = _store.CanUndo ? $"Undo {_store.UndoLabel}" : "Undo";
		_redoButton.Text = _store.CanRedo ? $"Redo {_store.RedoLabel}" : "Redo";
	}

	private static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
	}
}
