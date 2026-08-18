using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchV2 : Node2D
{
	private const string LegacyBuilderScenePath = "res://mission_scene_builder.tscn";
	private const string WorkbenchScenePath = "res://mission_workbench_v2.tscn";
	private const string UserManualPath = "res://Scripts/Missions/WorkbenchV2/USER_MANUAL.md";
	private const float DefaultZoom = 0.62f;
	private const float MinZoom = 0.25f;
	private const float MaxZoom = 1.6f;

	private enum WorkbenchMode { Map, Flow, Dialogue }

	private readonly MissionWorkbenchStore _store = new MissionWorkbenchStore();
	private readonly List<MissionTemplate> _templates = new List<MissionTemplate>();
	private Camera2D _camera;
	private Node2D _world;
	private Node2D _floorLayer;
	private Node2D _wallLayer;
	private Node2D _propLayer;
	private Node2D _markerLayer;
	private CanvasLayer _uiLayer;
	private OptionButton _missionOption;
	private OptionButton _modeOption;
	private ItemList _paletteList;
	private VBoxContainer _inspectorFields;
	private Label _selectionLabel;
	private ItemList _problemsList;
	private Label _statusLabel;
	private Button _undoButton;
	private Button _redoButton;
	private Control _mapUiBlocker;
	private GraphEdit _flowGraph;
	private Control _dialogueView;
	private ItemList _dialogueConversationList;
	private ItemList _dialogueNodeList;
	private LineEdit _dialogueSpeakerEdit;
	private TextEdit _dialogueTextEdit;
	private AcceptDialog _manualDialog;
	private TextEdit _manualText;
	private ConfirmationDialog _exitConfirmation;
	private DialogueConversationData _activeDialogueData;
	private string _activeDialogueNodeId = string.Empty;
	private bool _dialogueHasUnsavedChanges;
	private MissionTemplate _activeTemplate;
	private WorkbenchMode _mode;
	private string _placementAsset = string.Empty;
	private readonly List<MissionValidationIssue> _visibleIssues = new List<MissionValidationIssue>();

	public override void _Ready()
	{
		_camera = GetNode<Camera2D>("Camera2D");
		_world = GetNode<Node2D>("World");
		_floorLayer = GetNode<Node2D>("World/FloorLayer");
		_wallLayer = GetNode<Node2D>("World/WallLayer");
		_propLayer = GetNode<Node2D>("World/PropLayer");
		_markerLayer = GetNode<Node2D>("World/MarkerLayer");
		_camera.Zoom = Vector2.One * DefaultZoom;
		BuildUi();
		BuildPalette();
		_store.DocumentChanged += OnDocumentChanged;
		_store.SelectionChanged += OnSelectionChanged;
		LoadTemplates();
		if (_templates.Count > 0) LoadMission(_templates[0]);
	}

	public override void _ExitTree()
	{
		_store.DocumentChanged -= OnDocumentChanged;
		_store.SelectionChanged -= OnSelectionChanged;
	}

	private void LoadTemplates()
	{
		_templates.Clear();
		_templates.AddRange(new MissionRegistry().LoadTemplates().Where(template => template != null && template.IsEnabled).OrderBy(template => template.Title));
		_missionOption.Clear();
		foreach (MissionTemplate template in _templates) _missionOption.AddItem(template.Title);
	}

	private void LoadMission(MissionTemplate template)
	{
		if (template == null) return;
		_activeTemplate = template;
		string documentPath = MissionDocumentSerializer.GetDocumentResourcePath(template.MissionId);
		bool sourceExists = FileAccess.FileExists(documentPath);
		string loadError = string.Empty;
		MissionDocument document = sourceExists ? MissionDocumentSerializer.Load(documentPath, out loadError) : null;
		if (sourceExists && document == null)
		{
			SetStatus(loadError, true);
			return;
		}
		if (!sourceExists)
		{
			MissionImportResult import = LegacyMissionImporter.Import(template);
			if (!import.Success)
			{
				SetStatus(string.Join(" ", import.Errors), true);
				return;
			}
			document = import.Document;
			if (!MissionDocumentSerializer.Save(document, documentPath, out string importSaveError))
			{
				SetStatus(importSaveError, true);
				return;
			}
			SetStatus($"Imported {template.Title} into Workbench v2.");
		}

		_store.SetDocument(document);
		int optionIndex = _templates.IndexOf(template);
		if (optionIndex >= 0) _missionOption.Select(optionIndex);
		FrameDocument();
	}

	private void SaveDocument()
	{
		if (_store.Document == null) return;
		CaptureFlowNodePositions();
		string path = MissionDocumentSerializer.GetDocumentResourcePath(_store.Document.MissionId);
		if (!MissionDocumentSerializer.Save(_store.Document, path, out string error))
		{
			SetStatus(error, true);
			return;
		}
		_store.MarkSaved();
		SetStatus($"Saved source document to {path}.");
	}

	private void CompileDocument()
	{
		if (_store.Document == null || _activeTemplate == null) return;
		MissionCompileResult result = MissionDocumentCompiler.Compile(_store.Document, _activeTemplate.LayoutResourcePath);
		ShowValidation(result.Issues);
		SetStatus(result.Success ? $"Compiled runtime layout to {result.OutputResourcePath}." : "Compile blocked by validation errors.", !result.Success);
	}

	private void PlaytestDocument()
	{
		if (_store.Document == null) return;
		string outputPath = MissionDocumentCompiler.GetPlaytestOutputPath(_store.Document);
		MissionCompileResult result = MissionDocumentCompiler.Compile(_store.Document, outputPath);
		ShowValidation(result.Issues);
		if (!result.Success)
		{
			SetStatus("Playtest blocked by validation errors.", true);
			return;
		}

		MissionManager manager = GetNodeOrNull<MissionManager>("/root/MissionManager");
		MissionRuntimeState state = manager?.PrepareMission(_store.Document.MissionId, WorkbenchScenePath, "Mission Workbench v2 playtest");
		if (state == null)
		{
			SetStatus("Could not prepare mission playtest.", true);
			return;
		}

		MissionWorkbenchPlaytestSession.Prepare(_store.Document.MissionId, outputPath);
		GetTree().ChangeSceneToFile(_store.Document.Metadata.MissionScenePath);
	}

	private void ValidateDocument()
	{
		List<MissionValidationIssue> issues = MissionDocumentValidator.Validate(_store.Document);
		ShowValidation(issues);
		int errors = issues.Count(issue => issue.Severity == MissionValidationSeverity.Error);
		SetStatus(issues.Count == 0 ? "Validation passed." : $"Validation: {errors} errors, {issues.Count - errors} warnings/info.", errors > 0);
	}

	private void OnDocumentChanged()
	{
		RefreshMap();
		RefreshFlowGraph();
		RefreshToolbar();
	}

	private void OnSelectionChanged()
	{
		RefreshSelectionVisuals();
		RefreshInspector();
	}

	private void SetMode(WorkbenchMode mode)
	{
		_mode = mode;
		_modeOption.Select((int)mode);
		_flowGraph.Visible = mode == WorkbenchMode.Flow;
		_dialogueView.Visible = mode == WorkbenchMode.Dialogue;
		_mapUiBlocker.Visible = mode != WorkbenchMode.Map;
		if (mode == WorkbenchMode.Flow) RefreshFlowGraph();
		if (mode == WorkbenchMode.Dialogue) RefreshDialogueView();
	}

	private void RefreshToolbar()
	{
		_undoButton.Disabled = !_store.CanUndo;
		_redoButton.Disabled = !_store.CanRedo;
		_undoButton.Text = _store.CanUndo ? $"Undo {_store.UndoLabel}" : "Undo";
		_redoButton.Text = _store.CanRedo ? $"Redo {_store.RedoLabel}" : "Redo";
	}

	private void SetStatus(string message, bool error = false)
	{
		_statusLabel.Text = message;
		_statusLabel.AddThemeColorOverride("font_color", error ? new Color(1f, 0.42f, 0.42f) : new Color(0.65f, 0.95f, 1f));
	}

	private static StyleBoxFlat CreatePanelStyle(Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.05f, 0.08f, 0.97f), BorderColor = border,
			BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
			ContentMarginLeft = 10, ContentMarginTop = 10, ContentMarginRight = 10, ContentMarginBottom = 10
		};
	}
}
