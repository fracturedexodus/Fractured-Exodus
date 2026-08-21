using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchV3 : Node2D
{
	private const string WorkbenchScenePath = "res://mission_workbench_v3.tscn";
	private const string LegacyWorkbenchPath = "res://mission_workbench_v2.tscn";
	private const string OverworldRegistryPath = "res://overworld_content_registry.tscn";
	private const string MissionTemplateDirectory = "res://Data/Missions/Templates";
	private const string MissionLayoutDirectory = "res://Data/MissionLayouts";
	private const float DefaultZoom = 0.62f;
	private const float MinZoom = 0.25f;
	private const float MaxZoom = 1.6f;

	private enum WorkspacePage { Details, Map, Events, Conversations, Test }
	private enum MapTool { Select, Place, Paint, Rectangle, Erase, Room }

	private readonly MissionWorkbenchStore _store = new MissionWorkbenchStore();
	private readonly List<MissionTemplate> _templates = new();
	private MissionTemplate _activeTemplate;
	private WorkspacePage _page;
	private MapTool _mapTool = MapTool.Select;
	private string _placementAsset = string.Empty;
	private string _roomStamp = "Starter Room";
	private double _autosaveCountdown = -1d;
	private double _validationCountdown = -1d;
	private bool _suppressDocumentSignals;

	private Camera2D _camera;
	private Node2D _floorLayer;
	private Node2D _wallLayer;
	private Node2D _propLayer;
	private Node2D _markerLayer;
	private CanvasLayer _uiLayer;
	private OptionButton _missionOption;
	private Label _saveStateLabel;
	private Label _statusLabel;
	private Label _problemSummaryLabel;
	private ItemList _problemsList;
	private Button[] _pageButtons;
	private Control _detailsPage;
	private Control _eventsPage;
	private Control _conversationPage;
	private Control _testPage;
	private Control _mapChrome;
	private Control _mapBlocker;
	private ConfirmationDialog _replaceMapConfirmation;
	private AcceptDialog _welcomeDialog;
	private OptionButton _welcomeTemplateOption;
	private LineEdit _welcomeTitleEdit;
	private TextEdit _welcomeBriefingEdit;
	private string _pendingStarterTemplate = string.Empty;
	private ConfirmationDialog _newMissionDialog;
	private LineEdit _newMissionNameEdit;
	private LineEdit _newMissionIdEdit;
	private TextEdit _newMissionBriefingEdit;
	private TextEdit _newMissionObjectiveEdit;
	private OptionButton _newMissionStarterOption;
	private SpinBox _newMissionOfficerCount;
	private Label _newMissionErrorLabel;
	private bool _updatingNewMissionId;

	public override void _Ready()
	{
		_camera = GetNode<Camera2D>("Camera2D");
		_floorLayer = GetNode<Node2D>("World/FloorLayer");
		_wallLayer = GetNode<Node2D>("World/WallLayer");
		_propLayer = GetNode<Node2D>("World/PropLayer");
		_markerLayer = GetNode<Node2D>("World/MarkerLayer");
		_camera.Zoom = Vector2.One * DefaultZoom;
		BuildUi();
		_store.DocumentChanged += OnDocumentChanged;
		_store.SelectionChanged += OnSelectionChanged;
		LoadTemplates();
		if (_templates.Count > 0) LoadMission(_templates[0]);
		SetPage(WorkspacePage.Details);
	}

	public override void _ExitTree()
	{
		_store.DocumentChanged -= OnDocumentChanged;
		_store.SelectionChanged -= OnSelectionChanged;
	}

	public override void _Process(double delta)
	{
		if (_page == WorkspacePage.Map) UpdatePlacementPreview();
		if (_autosaveCountdown >= 0d)
		{
			_autosaveCountdown -= delta;
			if (_autosaveCountdown <= 0d) SaveDocument(true);
		}
		if (_validationCountdown >= 0d)
		{
			_validationCountdown -= delta;
			if (_validationCountdown <= 0d) RunFriendlyValidation(false);
		}
		ProcessDialogueAutosave(delta);
	}

	private void LoadTemplates()
	{
		_templates.Clear();
		_templates.AddRange(new MissionRegistry().LoadTemplates()
			.Where(template => template != null && template.IsEnabled)
			.OrderBy(template => template.Title));
		_missionOption.Clear();
		foreach (MissionTemplate template in _templates) _missionOption.AddItem(template.Title);
	}

	private void ShowNewMissionDialog()
	{
		string title = "Untitled Mission";
		_updatingNewMissionId = true;
		_newMissionNameEdit.Text = title;
		_newMissionIdEdit.Text = NormalizeMissionId(title);
		_updatingNewMissionId = false;
		_newMissionBriefingEdit.Text = "Brief the officers on what they are about to face.";
		_newMissionObjectiveEdit.Text = "Complete the mission objective and reach the evacuation area.";
		_newMissionStarterOption.Select(0);
		_newMissionOfficerCount.Value = 2;
		_newMissionErrorLabel.Text = string.Empty;
		_newMissionDialog.PopupCentered(new Vector2I(820, 720));
	}

	private void CreateNewMission()
	{
		string title = _newMissionNameEdit.Text.Trim();
		string missionId = NormalizeMissionId(_newMissionIdEdit.Text);
		string briefing = _newMissionBriefingEdit.Text.Trim();
		string objective = _newMissionObjectiveEdit.Text.Trim();
		string starter = _newMissionStarterOption.GetItemText(_newMissionStarterOption.Selected);
		if (string.IsNullOrWhiteSpace(title)) { ReopenNewMissionDialog("Enter a mission name."); return; }
		if (string.IsNullOrWhiteSpace(missionId)) { ReopenNewMissionDialog("Enter a mission ID using letters and numbers."); return; }
		if (_templates.Any(template => string.Equals(template.MissionId, missionId, StringComparison.OrdinalIgnoreCase)))
		{
			ReopenNewMissionDialog($"A mission named ‘{missionId}’ already exists. Choose another ID.");
			return;
		}

		string templatePath = $"{MissionTemplateDirectory}/{missionId}.tres";
		string documentPath = MissionDocumentSerializer.GetDocumentResourcePath(missionId);
		string layoutPath = $"{MissionLayoutDirectory}/{missionId}_builder.json";
		if (FileAccess.FileExists(templatePath) || FileAccess.FileExists(documentPath) || FileAccess.FileExists(layoutPath))
		{
			ReopenNewMissionDialog("Files already exist for that mission ID. Choose another ID so nothing is overwritten.");
			return;
		}

		MissionTemplate template = new()
		{
			MissionId = missionId,
			Title = title,
			Description = briefing,
			MissionScenePath = "res://black_site_relay.tscn",
			LayoutResourcePath = layoutPath,
			DefaultReturnScenePath = "res://exploration_battle.tscn",
			RecommendedOfficerCount = (int)_newMissionOfficerCount.Value,
			SourceNodeType = "Planet",
			ObjectiveText = objective,
			PromptText = briefing,
			PrimaryActionText = "COMPLETE MISSION",
			PrimaryOutcomeId = $"{missionId}_complete",
			IsEnabled = true,
			InteractionKeys = new Godot.Collections.Array<string> { $"mission:{missionId}" }
		};

		MissionDocument document = new()
		{
			MissionId = missionId,
			LayoutName = $"{missionId}_builder",
			Metadata = new MissionDocumentMetadata
			{
				Title = title,
				Description = briefing,
				MissionScenePath = template.MissionScenePath,
				DefaultReturnScenePath = template.DefaultReturnScenePath,
				RecommendedOfficerCount = template.RecommendedOfficerCount,
				SourceNodeType = template.SourceNodeType,
				ObjectiveText = objective,
				PromptText = briefing,
				IsEnabled = true
			},
			Outcomes = new List<MissionOutcomeData>
			{
				new() { Id = template.PrimaryOutcomeId, ActionText = template.PrimaryActionText }
			},
			Authoring = new MissionAuthoringData { WelcomeCompleted = true, TemplateKind = starter }
		};

		_activeTemplate = template;
		_suppressDocumentSignals = true;
		_store.SetDocument(document);
		ApplyStarterTemplate(starter);
		_suppressDocumentSignals = false;
		ApplyAuthoringRulesToDocument();
		_store.RefreshDerivedData();

		if (!MissionDocumentSerializer.Save(document, documentPath, out string documentError))
		{
			CleanupIncompleteNewMission(missionId, documentPath, layoutPath, templatePath);
			ReopenNewMissionDialog(documentError + " Incomplete mission files were removed, so you can safely try again.");
			return;
		}
		MissionCompileResult compile = MissionDocumentCompiler.Compile(document, layoutPath);
		if (!compile.Success)
		{
			CleanupIncompleteNewMission(missionId, documentPath, layoutPath, templatePath);
			ReopenNewMissionDialog("The starter mission could not be compiled: " + string.Join(" ", compile.Issues.Select(issue => issue.Message)) + " Incomplete mission files were removed.");
			return;
		}
		Error templateSave = ResourceSaver.Save(template, templatePath);
		if (templateSave != Error.Ok)
		{
			CleanupIncompleteNewMission(missionId, documentPath, layoutPath, templatePath);
			ReopenNewMissionDialog($"The mission template could not be saved ({templateSave}). Incomplete mission files were removed.");
			return;
		}

		MissionManager manager = GetNodeOrNull<MissionManager>("/root/MissionManager");
		manager?.ReloadTemplates();
		LoadTemplates();
		_activeTemplate = _templates.FirstOrDefault(item => item.MissionId == missionId) ?? template;
		int optionIndex = _templates.FindIndex(item => item.MissionId == missionId);
		if (optionIndex >= 0) _missionOption.Select(optionIndex);
		_store.MarkSaved();
		RefreshAllViews();
		FrameDocument();
		RunFriendlyValidation(false);
		SetPage(WorkspacePage.Details);
		SetStatus($"Created and saved ‘{title}’. Assign ‘mission:{missionId}’ in the Overworld Editor.");
	}

	private static void CleanupIncompleteNewMission(string missionId, params string[] paths)
	{
		if (string.IsNullOrWhiteSpace(missionId)) return;
		foreach (string path in paths)
		{
			if (string.IsNullOrWhiteSpace(path) || !path.Contains(missionId, StringComparison.Ordinal)) continue;
			string absolute = ProjectSettings.GlobalizePath(path);
			if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(absolute);
		}
	}

	private void ReopenNewMissionDialog(string message)
	{
		_newMissionErrorLabel.Text = message;
		_newMissionErrorLabel.AddThemeColorOverride("font_color", new Color(1f, .42f, .42f));
		CallDeferred(MethodName.PopupNewMissionDialogAfterError);
	}

	private void PopupNewMissionDialogAfterError() => _newMissionDialog.PopupCentered(new Vector2I(820, 720));

	private static string NormalizeMissionId(string value)
	{
		string normalized = string.Concat((value ?? string.Empty).Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_'));
		while (normalized.Contains("__", StringComparison.Ordinal)) normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
		return normalized.Trim('_');
	}

	private void SwitchMission(long index)
	{
		if (index < 0 || index >= _templates.Count) return;
		SaveActiveConversation();
		if (_store.IsDirty) SaveDocument(true);
		LoadMission(_templates[(int)index]);
	}

	private void OpenV2()
	{
		SaveActiveConversation();
		if (_store.IsDirty) SaveDocument(true);
		GetTree().ChangeSceneToFile(LegacyWorkbenchPath);
	}

	private void OpenOverworldPlacement()
	{
		SaveActiveConversation();
		if (_store.IsDirty) SaveDocument(true);
		GetTree().ChangeSceneToFile(OverworldRegistryPath);
	}

	private void ExitV3()
	{
		SaveActiveConversation();
		if (_store.IsDirty) SaveDocument(true);
		GetTree().Quit();
	}

	private void LoadMission(MissionTemplate template)
	{
		if (template == null) return;
		_activeTemplate = template;
		string path = MissionDocumentSerializer.GetDocumentResourcePath(template.MissionId);
		bool sourceExists = FileAccess.FileExists(path);
		string loadError = string.Empty;
		MissionDocument document = sourceExists
			? MissionDocumentSerializer.Load(path, out loadError)
			: ImportLegacy(template);
		if (document == null)
		{
			SetStatus(sourceExists ? loadError : "The mission could not be imported.", true);
			return;
		}
		_suppressDocumentSignals = true;
		_store.SetDocument(document);
		_suppressDocumentSignals = false;
		int index = _templates.IndexOf(template);
		if (index >= 0) _missionOption.Select(index);
		RefreshAllViews();
		FrameDocument();
		RunFriendlyValidation(false);
		_saveStateLabel.Text = "All changes saved";
		if (!document.Authoring.WelcomeCompleted) ShowWelcomeGuide();
	}

	private MissionDocument ImportLegacy(MissionTemplate template)
	{
		MissionImportResult result = LegacyMissionImporter.Import(template);
		if (!result.Success)
		{
			SetStatus(string.Join(" ", result.Errors), true);
			return null;
		}
		MissionDocumentSerializer.Save(result.Document, MissionDocumentSerializer.GetDocumentResourcePath(template.MissionId), out _);
		return result.Document;
	}

	private void OnDocumentChanged()
	{
		if (_suppressDocumentSignals) return;
		RefreshMap();
		RefreshDetailsSummary();
		RefreshEventTimeline();
		RefreshTestPage();
		RefreshUndoButtons();
		if (_store.IsDirty)
		{
			_saveStateLabel.Text = "Saving draft…";
			_autosaveCountdown = 0.8d;
			_validationCountdown = 0.25d;
		}
	}

	private void OnSelectionChanged()
	{
		RefreshSelectionVisuals();
		RefreshFriendlyInspector();
	}

	private void MarkAuthoringChanged(string message = "Draft updated")
	{
		if (_store.Document == null) return;
		_store.NotifyExternalChange();
		SetStatus(message);
	}

	private void SaveDocument(bool automatic = false)
	{
		_autosaveCountdown = -1d;
		if (_store.Document == null) return;
		ApplyAuthoringRulesToDocument();
		_store.RefreshDerivedData();
		string path = MissionDocumentSerializer.GetDocumentResourcePath(_store.Document.MissionId);
		if (!MissionDocumentSerializer.Save(_store.Document, path, out string error))
		{
			_saveStateLabel.Text = "Draft not saved";
			SetStatus(error, true);
			return;
		}
		SyncActiveTemplateFromDocument();
		_store.MarkSaved();
		_saveStateLabel.Text = "All changes saved";
		if (automatic) RunFriendlyValidation(false);
		if (!automatic) SetStatus("Draft saved.");
	}

	private void SyncActiveTemplateFromDocument()
	{
		if (_activeTemplate == null || _store.Document == null) return;
		MissionDocument document = _store.Document;
		_activeTemplate.Title = document.Metadata.Title;
		_activeTemplate.Description = document.Metadata.Description;
		_activeTemplate.MissionScenePath = document.Metadata.MissionScenePath;
		_activeTemplate.DefaultReturnScenePath = document.Metadata.DefaultReturnScenePath;
		_activeTemplate.RecommendedOfficerCount = document.Metadata.RecommendedOfficerCount;
		_activeTemplate.SourceNodeType = document.Metadata.SourceNodeType;
		_activeTemplate.ObjectiveText = document.Metadata.ObjectiveText;
		_activeTemplate.PromptText = document.Metadata.PromptText;
		_activeTemplate.DefaultDialogueId = document.Metadata.DefaultDialogueId;
		MissionOutcomeData primary = document.Outcomes.FirstOrDefault();
		if (primary != null)
		{
			_activeTemplate.PrimaryOutcomeId = primary.Id;
			_activeTemplate.PrimaryActionText = primary.ActionText;
			_activeTemplate.PrimaryOutcomeRequiredFlags = ToGodotArray(primary.RequiredFlags);
			_activeTemplate.PrimaryOutcomeBlockedFlags = ToGodotArray(primary.BlockedFlags);
		}
		MissionOutcomeData secondary = document.Outcomes.Skip(1).FirstOrDefault();
		if (secondary != null)
		{
			_activeTemplate.SecondaryOutcomeId = secondary.Id;
			_activeTemplate.SecondaryActionText = secondary.ActionText;
			_activeTemplate.SecondaryOutcomeRequiredFlags = ToGodotArray(secondary.RequiredFlags);
			_activeTemplate.SecondaryOutcomeBlockedFlags = ToGodotArray(secondary.BlockedFlags);
		}
		if (!string.IsNullOrWhiteSpace(_activeTemplate.ResourcePath))
		{
			Error save = ResourceSaver.Save(_activeTemplate, _activeTemplate.ResourcePath);
			if (save != Error.Ok) SetStatus($"Draft saved, but the mission registry entry could not be updated ({save}).", true);
		}
	}

	private static Godot.Collections.Array<string> ToGodotArray(IEnumerable<string> values)
	{
		Godot.Collections.Array<string> result = new();
		foreach (string value in values ?? Enumerable.Empty<string>()) if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
		return result;
	}

	private void TestMission()
	{
		if (_store.Document == null) return;
		SaveDocument(true);
		List<MissionValidationIssue> issues = RunFriendlyValidation(true);
		if (issues.Any(issue => issue.Severity == MissionValidationSeverity.Error))
		{
			SetStatus("Test blocked. Choose a red problem to fix it.", true);
			SetPage(WorkspacePage.Test);
			return;
		}
		string output = $"user://mission_workbench_v3/compiled/{_store.Document.LayoutName}.json";
		MissionCompileResult result = MissionDocumentCompiler.Compile(_store.Document, output);
		if (!result.Success) { SetStatus("The test build could not be created.", true); return; }
		MissionManager manager = GetNodeOrNull<MissionManager>("/root/MissionManager");
		MissionRuntimeState state = manager?.PrepareMission(_store.Document.MissionId, WorkbenchScenePath, "Mission Workbench v3 playtest");
		if (state == null) { SetStatus("The mission could not be prepared for testing.", true); return; }
		MissionWorkbenchPlaytestSession.Prepare(_store.Document.MissionId, output);
		GetTree().ChangeSceneToFile(_store.Document.Metadata.MissionScenePath);
	}

	private void PublishMission()
	{
		if (_store.Document == null || _activeTemplate == null) return;
		SaveDocument(true);
		List<MissionValidationIssue> issues = RunFriendlyValidation(true);
		if (issues.Any(issue => issue.Severity == MissionValidationSeverity.Error))
		{
			SetStatus("Publish blocked. Fix the red problems first.", true);
			SetPage(WorkspacePage.Test);
			return;
		}
		MissionCompileResult result = MissionDocumentCompiler.Compile(_store.Document, _activeTemplate.LayoutResourcePath);
		SetStatus(result.Success ? "Mission published and ready for the game." : "The mission could not be published.", !result.Success);
	}

	private List<MissionValidationIssue> RunFriendlyValidation(bool focusPanel)
	{
		_validationCountdown = -1d;
		List<MissionValidationIssue> issues = MissionDocumentValidator.Validate(_store.Document);
		issues.AddRange(ValidateAuthoringRules());
		ShowFriendlyProblems(issues);
		if (focusPanel) RefreshTestPage();
		return issues;
	}

	private List<MissionValidationIssue> ValidateAuthoringRules()
	{
		List<MissionValidationIssue> issues = new();
		MissionDocument document = _store.Document;
		if (document == null) return issues;
		HashSet<string> ids = document.Elements.Select(element => element.Id).ToHashSet();
		foreach (MissionRuleData rule in document.Authoring.Rules.Where(rule => rule != null && rule.Enabled))
		{
			if (string.IsNullOrWhiteSpace(rule.SourceElementId) || !ids.Contains(rule.SourceElementId))
				issues.Add(FriendlyIssue("event.source", "Choose what starts this event.", rule.SourceElementId));
			if (RuleNeedsTarget(rule.ActionType) && (string.IsNullOrWhiteSpace(rule.TargetElementId) || !ids.Contains(rule.TargetElementId)))
				issues.Add(FriendlyIssue("event.target", $"Choose what should receive the action ‘{rule.ActionType}’.", rule.SourceElementId));
			if (rule.ActionType is "Start conversation" or "Show message" or "Change objective" && string.IsNullOrWhiteSpace(rule.Value))
				issues.Add(FriendlyIssue("event.value", $"Add the text or conversation for ‘{rule.ActionType}’.", rule.SourceElementId));
		}
		return issues;
	}

	private static MissionValidationIssue FriendlyIssue(string id, string message, string elementId = "") => new()
	{
		RuleId = id, Severity = MissionValidationSeverity.Error, Message = message, ElementId = elementId
	};

	private void ApplyAuthoringRulesToDocument()
	{
		MissionDocument document = _store.Document;
		if (document == null) return;
		foreach (MissionMapElement element in document.Elements)
		{
			element.Logic ??= new MissionElementLogic();
			element.Logic.RequiredFlag = RemoveGeneratedFlags(element.Logic.RequiredFlag);
			element.Logic.SetFlag = RemoveGeneratedFlags(element.Logic.SetFlag);
		}
		foreach (MissionOutcomeData outcome in document.Outcomes)
			outcome.RequiredFlags.RemoveAll(flag => flag.StartsWith("v3_event_", StringComparison.Ordinal));

		foreach (MissionRuleData rule in document.Authoring.Rules.Where(rule => rule != null && rule.Enabled))
		{
			MissionMapElement source = document.Elements.FirstOrDefault(element => element.Id == rule.SourceElementId);
			MissionMapElement target = document.Elements.FirstOrDefault(element => element.Id == rule.TargetElementId);
			if (source == null) continue;
			string flag = RuleFlag(rule);
			source.Logic.TriggerMode = rule.WhenType switch
			{
				"Player enters" => "enter",
				"Player uses" or "Player talks to" or "Player collects" => "interact",
				_ => source.Logic.TriggerMode
			};
			source.Logic.OneShot = true;
			source.Logic.SetFlag = AppendFlag(source.Logic.SetFlag, flag);
			switch (rule.ActionType)
			{
				case "Open door":
				case "Unlock object":
				case "Enable object":
				case "Spawn enemies":
					if (target != null) target.Logic.RequiredFlag = AppendFlag(target.Logic.RequiredFlag, flag);
					break;
				case "Start conversation":
					source.Logic.TargetId = rule.Value.Trim();
					if (!document.DialogueConversationIds.Contains(source.Logic.TargetId)) document.DialogueConversationIds.Add(source.Logic.TargetId);
					break;
				case "Complete mission":
					MissionOutcomeData outcome = document.Outcomes.FirstOrDefault();
					if (outcome != null && !outcome.RequiredFlags.Contains(flag)) outcome.RequiredFlags.Add(flag);
					break;
				case "Change objective":
					source.Logic.Notes = $"Objective: {rule.Value.Trim()}";
					break;
				case "Show message":
					source.Logic.Notes = $"Message: {rule.Value.Trim()}";
					break;
			}
		}
	}

	private static string RuleFlag(MissionRuleData rule) => $"v3_event_{NormalizeId(rule.Id)}";
	private static string RemoveGeneratedFlags(string value) => string.Join(", ", SplitFlags(value).Where(flag => !flag.StartsWith("v3_event_", StringComparison.Ordinal)));
	private static string AppendFlag(string value, string flag) => string.Join(", ", SplitFlags(value).Append(flag).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct());
	private static IEnumerable<string> SplitFlags(string value) => string.IsNullOrWhiteSpace(value) ? Enumerable.Empty<string>() : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	private static string NormalizeId(string value) => string.Concat((value ?? string.Empty).ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_'));
	private static bool RuleNeedsTarget(string action) => action is "Open door" or "Unlock object" or "Enable object" or "Spawn enemies";

	private void SetPage(WorkspacePage page)
	{
		_page = page;
		_detailsPage.Visible = page == WorkspacePage.Details;
		_eventsPage.Visible = page == WorkspacePage.Events;
		_conversationPage.Visible = page == WorkspacePage.Conversations;
		_testPage.Visible = page == WorkspacePage.Test;
		_mapChrome.Visible = page == WorkspacePage.Map;
		_mapBlocker.Visible = page != WorkspacePage.Map;
		for (int i = 0; i < _pageButtons.Length; i++) _pageButtons[i].ButtonPressed = i == (int)page;
		if (page == WorkspacePage.Details) RefreshDetailsPage();
		if (page == WorkspacePage.Events) RefreshEventTimeline();
		if (page == WorkspacePage.Conversations) RefreshConversationWorkspace();
		if (page == WorkspacePage.Test) RefreshTestPage();
	}

	private void SetStatus(string message, bool error = false)
	{
		_statusLabel.Text = message;
		_statusLabel.AddThemeColorOverride("font_color", error ? new Color(1f, .42f, .42f) : new Color(.65f, .95f, 1f));
	}

	private void RefreshAllViews()
	{
		RefreshMap();
		RefreshFriendlyInspector();
		RefreshDetailsPage();
		RefreshEventTimeline();
		RefreshConversationWorkspace();
		RefreshTestPage();
		RefreshUndoButtons();
	}

	private static StyleBoxFlat PanelStyle(Color border, Color? background = null) => new()
	{
		BgColor = background ?? new Color(.035f, .05f, .08f, .97f), BorderColor = border,
		BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
		CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
		ContentMarginLeft = 12, ContentMarginTop = 10, ContentMarginRight = 12, ContentMarginBottom = 10
	};
}
