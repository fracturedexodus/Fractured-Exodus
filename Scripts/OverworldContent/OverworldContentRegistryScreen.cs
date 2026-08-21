using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class OverworldContentRegistryScreen : Control
{
	private const string ManualPath = "res://Scripts/OverworldContent/USER_MANUAL.md";
	private readonly List<OverworldContentDefinition> _definitions = new List<OverworldContentDefinition>();
	private ItemList _definitionList;
	private ItemList _issuesList;
	private Label _status;
	private Label _sourcePath;
	private LineEdit _contentId;
	private LineEdit _displayName;
	private OptionButton _contentType;
	private LineEdit _contentReference;
	private OptionButton _missionReferenceOption;
	private CheckBox _enabled;
	private LineEdit _priority;
	private OptionButton _nodeType;
	private OptionButton _uniqueScope;
	private LineEdit _regions;
	private LineEdit _spawnChance;
	private CheckBox _guaranteeFirst;
	private CheckBox _avoidStartingPlanet;
	private LineEdit _preferredSprite;
	private CheckBox _allowFallback;
	private LineEdit _minimumRadius;
	private LineEdit _maximumRadius;
	private LineEdit _requiredFlags;
	private LineEdit _blockedFlags;
	private AcceptDialog _manualDialog;
	private TextEdit _manualText;
	private ConfirmationDialog _exitDialog;
	private OverworldContentDefinition _current;
	private bool _dirty;
	private bool _loadingForm;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		BuildUi();
		ReloadRegistry();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
		if (key.CtrlPressed && key.Keycode == Key.S) { SaveCurrent(); GetViewport().SetInputAsHandled(); }
		if (key.CtrlPressed && key.Keycode == Key.Q) { RequestExit(); GetViewport().SetInputAsHandled(); }
	}

	private void BuildUi()
	{
		ColorRect background = new ColorRect { Color = new Color(0.015f, 0.022f, 0.04f) };
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(background);

		PanelContainer top = CreatePanel(0f, 0f, 1f, 0f, 8f, 8f, -8f, 66f, new Color(0.35f, 0.92f, 0.76f));
		HBoxContainer toolbar = new HBoxContainer();
		toolbar.AddThemeConstantOverride("separation", 8);
		top.AddChild(toolbar);
		Label title = new Label { Text = "OVERWORLD CONTENT REGISTRY", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		title.AddThemeFontSizeOverride("font_size", 21);
		toolbar.AddChild(title);
		toolbar.AddChild(Button("New", NewDefinition));
		toolbar.AddChild(Button("Save", SaveCurrent));
		toolbar.AddChild(Button("Reload", ReloadRegistry));
		toolbar.AddChild(Button("Validate", ValidateCurrent));
		toolbar.AddChild(Button("Mission Workbench", OpenMissionWorkbench));
		toolbar.AddChild(Button("Manual", ShowManual));
		toolbar.AddChild(Button("Exit", RequestExit));

		PanelContainer listPanel = CreatePanel(0f, 0f, 0f, 1f, 8f, 74f, 420f, -210f, new Color(0.3f, 0.7f, 1f));
		VBoxContainer listColumn = new VBoxContainer();
		listPanel.AddChild(listColumn);
		listColumn.AddChild(Heading("REGISTERED CONTENT"));
		listColumn.AddChild(new Label { Text = "Placement definitions loaded from Data/OverworldContent", AutowrapMode = TextServer.AutowrapMode.WordSmart });
		_definitionList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
		_definitionList.ItemSelected += SelectDefinition;
		listColumn.AddChild(_definitionList);

		PanelContainer editorPanel = CreatePanel(0f, 0f, 1f, 1f, 428f, 74f, -8f, -210f, new Color(0.75f, 0.5f, 1f));
		VBoxContainer editorColumn = new VBoxContainer();
		editorPanel.AddChild(editorColumn);
		HBoxContainer editorHeader = new HBoxContainer();
		editorHeader.AddChild(Heading("PLACEMENT DEFINITION"));
		_sourcePath = new Label { HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		editorHeader.AddChild(_sourcePath);
		editorColumn.AddChild(editorHeader);
		ScrollContainer scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		editorColumn.AddChild(scroll);
		GridContainer form = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		form.AddThemeConstantOverride("h_separation", 14);
		form.AddThemeConstantOverride("v_separation", 8);
		scroll.AddChild(form);

		_contentId = AddLine(form, "Content ID");
		_displayName = AddLine(form, "Display Name");
		_contentType = AddEnum<OverworldContentType>(form, "Content Type");
		_contentReference = AddLine(form, "Content Reference");
		form.AddChild(new Label { Text = "Choose Registered Mission" });
		_missionReferenceOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		form.AddChild(_missionReferenceOption);
		PopulateMissionReferenceOptions();
		_missionReferenceOption.ItemSelected += SelectMissionReference;
		_enabled = AddCheck(form, "Enabled");
		_priority = AddLine(form, "Priority");
		_nodeType = AddEnum<OverworldPlacementNodeType>(form, "World Node Type");
		_uniqueScope = AddEnum<OverworldUniqueScope>(form, "Unique Scope");
		_regions = AddLine(form, "Regions (comma-separated)");
		_spawnChance = AddLine(form, "Spawn Chance (0–1)");
		_guaranteeFirst = AddCheck(form, "Guarantee First Eligible Placement");
		_avoidStartingPlanet = AddCheck(form, "Avoid Starting Planet");
		_preferredSprite = AddLine(form, "Preferred Outpost Sprite Contains");
		_allowFallback = AddCheck(form, "Allow Fallback Target");
		_minimumRadius = AddLine(form, "Minimum Free-Hex Radius");
		_maximumRadius = AddLine(form, "Maximum Free-Hex Radius");
		_requiredFlags = AddLine(form, "Required Story Flags");
		_blockedFlags = AddLine(form, "Blocked Story Flags");
		ConnectDirtySignals();

		PanelContainer outputPanel = CreatePanel(0f, 1f, 1f, 1f, 8f, -202f, -8f, -8f, new Color(1f, 0.58f, 0.28f));
		VBoxContainer output = new VBoxContainer();
		outputPanel.AddChild(output);
		HBoxContainer outputHeader = new HBoxContainer();
		outputHeader.AddChild(Heading("VALIDATION / OUTPUT"));
		_status = new Label { Text = "Ready.", HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		outputHeader.AddChild(_status);
		output.AddChild(outputHeader);
		_issuesList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
		output.AddChild(_issuesList);

		BuildDialogs();
	}

	private void BuildDialogs()
	{
		_manualDialog = new AcceptDialog { Title = "Overworld Content Registry — User Manual", OkButtonText = "Close", MinSize = new Vector2I(920, 700) };
		_manualText = new TextEdit { Editable = false, WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(880f, 620f) };
		_manualDialog.AddChild(_manualText);
		AddChild(_manualDialog);

		_exitDialog = new ConfirmationDialog { Title = "Exit Overworld Content Registry?", DialogText = "This definition has unsaved changes. Exit and discard them?", OkButtonText = "Exit Without Saving", CancelButtonText = "Keep Working" };
		_exitDialog.Confirmed += () => GetTree().Quit();
		AddChild(_exitDialog);
	}

	private void ReloadRegistry()
	{
		OverworldContentRegistry.Reload();
		PopulateMissionReferenceOptions();
		_definitions.Clear();
		_definitions.AddRange(OverworldContentRegistry.GetDefinitions());
		RefreshDefinitionList();
		ShowIssues(OverworldContentRegistry.GetIssues());
		_dirty = false;
		if (_definitions.Count > 0)
		{
			_definitionList.Select(0);
			LoadForm(_definitions[0]);
		}
		else LoadForm(null);
		SetStatus($"Loaded {_definitions.Count} overworld content definitions.");
	}

	private void RefreshDefinitionList()
	{
		_definitionList.Clear();
		foreach (OverworldContentDefinition definition in _definitions)
		{
			string state = definition.Enabled ? string.Empty : " [DISABLED]";
			_definitionList.AddItem($"[{definition.ContentType}] {definition.DisplayName}{state}");
		}
	}

	private void SelectDefinition(long index)
	{
		if (index < 0 || index >= _definitions.Count) return;
		if (_dirty)
		{
			SetStatus("Save or Reload before switching definitions.", true);
			int currentIndex = _definitions.IndexOf(_current);
			if (currentIndex >= 0) _definitionList.Select(currentIndex);
			return;
		}
		LoadForm(_definitions[(int)index]);
	}

	private void NewDefinition()
	{
		if (_dirty) { SetStatus("Save or Reload before creating a definition.", true); return; }
		OverworldContentDefinition definition = new OverworldContentDefinition
		{
			ContentId = $"new_content_{DateTime.UtcNow:yyyyMMddHHmmss}", DisplayName = "New overworld content", ContentType = OverworldContentType.Mission,
			Placement = new OverworldPlacementRule { NodeType = OverworldPlacementNodeType.Planet, UniqueScope = OverworldUniqueScope.Campaign }
		};
		_definitions.Add(definition);
		RefreshDefinitionList();
		_definitionList.Select(_definitions.Count - 1);
		LoadForm(definition);
		_dirty = true;
		SetStatus("New definition created. Complete the fields and Save.");
	}

	private void LoadForm(OverworldContentDefinition definition)
	{
		_loadingForm = true;
		_current = definition;
		_sourcePath.Text = definition?.SourcePath ?? "Unsaved definition";
		_contentId.Text = definition?.ContentId ?? string.Empty;
		_displayName.Text = definition?.DisplayName ?? string.Empty;
		_contentType.Select((int)(definition?.ContentType ?? OverworldContentType.Mission));
		_contentReference.Text = definition?.ContentReference ?? string.Empty;
		RefreshMissionReferenceSelection();
		_enabled.ButtonPressed = definition?.Enabled ?? true;
		_priority.Text = (definition?.Priority ?? 0).ToString();
		OverworldPlacementRule placement = definition?.Placement ?? new OverworldPlacementRule();
		_nodeType.Select((int)placement.NodeType);
		_uniqueScope.Select((int)placement.UniqueScope);
		_regions.Text = string.Join(", ", placement.Regions);
		_spawnChance.Text = placement.SpawnChance.ToString("0.###");
		_guaranteeFirst.ButtonPressed = placement.GuaranteeFirstPlacement;
		_avoidStartingPlanet.ButtonPressed = placement.AvoidStartingPlanet;
		_preferredSprite.Text = placement.PreferredSpriteContains;
		_allowFallback.ButtonPressed = placement.AllowFallbackTarget;
		_minimumRadius.Text = placement.MinimumRadius.ToString();
		_maximumRadius.Text = placement.MaximumRadius.ToString();
		_requiredFlags.Text = string.Join(", ", placement.RequiredFlags);
		_blockedFlags.Text = string.Join(", ", placement.BlockedFlags);
		_loadingForm = false;
		_dirty = false;
	}

	private void ApplyForm()
	{
		if (_current == null) return;
		_current.ContentId = _contentId.Text.Trim();
		_current.DisplayName = _displayName.Text.Trim();
		_current.ContentType = (OverworldContentType)_contentType.Selected;
		_current.ContentReference = _contentReference.Text.Trim();
		_current.Enabled = _enabled.ButtonPressed;
		if (int.TryParse(_priority.Text, out int priority)) _current.Priority = priority;
		_current.Placement ??= new OverworldPlacementRule();
		_current.Placement.NodeType = (OverworldPlacementNodeType)_nodeType.Selected;
		_current.Placement.UniqueScope = (OverworldUniqueScope)_uniqueScope.Selected;
		_current.Placement.Regions = Split(_regions.Text);
		if (float.TryParse(_spawnChance.Text, out float chance)) _current.Placement.SpawnChance = chance;
		_current.Placement.GuaranteeFirstPlacement = _guaranteeFirst.ButtonPressed;
		_current.Placement.AvoidStartingPlanet = _avoidStartingPlanet.ButtonPressed;
		_current.Placement.PreferredSpriteContains = _preferredSprite.Text.Trim();
		_current.Placement.AllowFallbackTarget = _allowFallback.ButtonPressed;
		if (int.TryParse(_minimumRadius.Text, out int minimumRadius)) _current.Placement.MinimumRadius = minimumRadius;
		if (int.TryParse(_maximumRadius.Text, out int maximumRadius)) _current.Placement.MaximumRadius = maximumRadius;
		_current.Placement.RequiredFlags = Split(_requiredFlags.Text);
		_current.Placement.BlockedFlags = Split(_blockedFlags.Text);
	}

	private void SaveCurrent()
	{
		if (_current == null) return;
		ApplyForm();
		List<OverworldContentIssue> issues = OverworldContentValidator.Validate(_definitions);
		ShowIssues(issues);
		if (issues.Any(issue => issue.Severity == OverworldContentIssueSeverity.Error && (string.IsNullOrWhiteSpace(issue.ContentId) || issue.ContentId == _current.ContentId)))
		{
			SetStatus("Save blocked by validation errors.", true);
			return;
		}
		string path = string.IsNullOrWhiteSpace(_current.SourcePath)
			? $"{OverworldContentRegistry.DefinitionDirectory}/{NormalizeFileId(_current.ContentId)}.overworld.json"
			: _current.SourcePath;
		string selectedId = _current.ContentId;
		if (!OverworldContentSerializer.Save(_current, path, out string error)) { SetStatus(error, true); return; }
		OverworldContentRegistry.Reload();
		_definitions.Clear();
		_definitions.AddRange(OverworldContentRegistry.GetDefinitions());
		RefreshDefinitionList();
		int index = _definitions.FindIndex(definition => definition.ContentId == selectedId);
		if (index >= 0) { _definitionList.Select(index); LoadForm(_definitions[index]); }
		ShowIssues(OverworldContentRegistry.GetIssues());
		SetStatus($"Saved {selectedId} to {path}.");
	}

	private void ValidateCurrent()
	{
		ApplyForm();
		List<OverworldContentIssue> issues = OverworldContentValidator.Validate(_definitions);
		ShowIssues(issues);
		int errorCount = issues.Count(issue => issue.Severity == OverworldContentIssueSeverity.Error);
		SetStatus(issues.Count == 0 ? "Validation passed." : $"Validation: {errorCount} errors, {issues.Count - errorCount} warnings/info.", errorCount > 0);
	}

	private void OpenMissionWorkbench()
	{
		if (_dirty) { SetStatus("Save or Reload before leaving the registry.", true); return; }
		GetTree().ChangeSceneToFile("res://mission_workbench_v3.tscn");
	}

	private void ShowManual()
	{
		using FileAccess file = FileAccess.Open(ManualPath, FileAccess.ModeFlags.Read);
		_manualText.Text = file?.GetAsText() ?? $"Manual could not be loaded: {ManualPath}";
		_manualText.SetCaretLine(0);
		_manualText.SetVScroll(0);
		_manualDialog.PopupCentered(new Vector2I(1000, 780));
	}

	private void RequestExit()
	{
		if (_dirty) _exitDialog.PopupCentered();
		else GetTree().Quit();
	}

	private void ShowIssues(IEnumerable<OverworldContentIssue> issues)
	{
		_issuesList.Clear();
		foreach (OverworldContentIssue issue in issues ?? Enumerable.Empty<OverworldContentIssue>())
		{
			_issuesList.AddItem($"[{issue.Severity.ToString().ToUpperInvariant()}] {issue.ContentId} {issue.Message}".Trim());
		}
		if (_issuesList.ItemCount == 0) _issuesList.AddItem("No validation issues.");
	}

	private void ConnectDirtySignals()
	{
		foreach (LineEdit edit in new[] { _contentId, _displayName, _contentReference, _priority, _regions, _spawnChance, _preferredSprite, _minimumRadius, _maximumRadius, _requiredFlags, _blockedFlags }) edit.TextChanged += _ => MarkDirty();
		foreach (OptionButton option in new[] { _contentType, _nodeType, _uniqueScope }) option.ItemSelected += _ => { RefreshMissionReferenceAvailability(); MarkDirty(); };
		foreach (CheckBox check in new[] { _enabled, _guaranteeFirst, _avoidStartingPlanet, _allowFallback }) check.Toggled += _ => MarkDirty();
	}

	private void PopulateMissionReferenceOptions()
	{
		if (_missionReferenceOption == null) return;
		_missionReferenceOption.Clear();
		_missionReferenceOption.AddItem("Choose a mission…");
		_missionReferenceOption.SetItemMetadata(0, string.Empty);
		foreach (MissionTemplate template in new MissionRegistry().LoadTemplates().Where(template => template != null && template.IsEnabled).OrderBy(template => template.Title))
		{
			foreach (string key in template.InteractionKeys ?? new Godot.Collections.Array<string>())
			{
				if (string.IsNullOrWhiteSpace(key)) continue;
				int item = _missionReferenceOption.ItemCount;
				_missionReferenceOption.AddItem($"{template.Title} — {key}");
				_missionReferenceOption.SetItemMetadata(item, key.Trim());
			}
		}
		RefreshMissionReferenceAvailability();
	}

	private void SelectMissionReference(long index)
	{
		if (_loadingForm || index <= 0 || index >= _missionReferenceOption.ItemCount) return;
		_contentReference.Text = _missionReferenceOption.GetItemMetadata((int)index).AsString();
		MarkDirty();
	}

	private void RefreshMissionReferenceSelection()
	{
		if (_missionReferenceOption == null) return;
		int selected = 0;
		for (int i = 1; i < _missionReferenceOption.ItemCount; i++)
		{
			if (string.Equals(_missionReferenceOption.GetItemMetadata(i).AsString(), _contentReference.Text.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				selected = i;
				break;
			}
		}
		_missionReferenceOption.Select(selected);
		RefreshMissionReferenceAvailability();
	}

	private void RefreshMissionReferenceAvailability()
	{
		if (_missionReferenceOption != null && _contentType != null) _missionReferenceOption.Disabled = (OverworldContentType)_contentType.Selected != OverworldContentType.Mission;
	}

	private void MarkDirty()
	{
		if (_loadingForm || _current == null) return;
		_dirty = true;
		SetStatus("Unsaved changes.");
	}

	private void SetStatus(string message, bool error = false)
	{
		_status.Text = message;
		_status.AddThemeColorOverride("font_color", error ? new Color(1f, 0.4f, 0.4f) : new Color(0.65f, 0.95f, 1f));
	}

	private PanelContainer CreatePanel(float anchorLeft, float anchorTop, float anchorRight, float anchorBottom, float left, float top, float right, float bottom, Color border)
	{
		PanelContainer panel = new PanelContainer { AnchorLeft = anchorLeft, AnchorTop = anchorTop, AnchorRight = anchorRight, AnchorBottom = anchorBottom, OffsetLeft = left, OffsetTop = top, OffsetRight = right, OffsetBottom = bottom };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.03f, 0.045f, 0.07f, 0.98f), BorderColor = border, BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, ContentMarginLeft = 10, ContentMarginTop = 10, ContentMarginRight = 10, ContentMarginBottom = 10 });
		AddChild(panel);
		return panel;
	}

	private static Label Heading(string text)
	{
		Label label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", 18);
		return label;
	}

	private static Button Button(string text, Action action)
	{
		Button button = new Button { Text = text, CustomMinimumSize = new Vector2(96f, 36f) };
		button.Pressed += () => action?.Invoke();
		return button;
	}

	private static LineEdit AddLine(GridContainer form, string label)
	{
		form.AddChild(new Label { Text = label });
		LineEdit edit = new LineEdit { CustomMinimumSize = new Vector2(600f, 36f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		form.AddChild(edit);
		return edit;
	}

	private static CheckBox AddCheck(GridContainer form, string label)
	{
		form.AddChild(new Label { Text = label });
		CheckBox check = new CheckBox();
		form.AddChild(check);
		return check;
	}

	private static OptionButton AddEnum<T>(GridContainer form, string label) where T : struct, Enum
	{
		form.AddChild(new Label { Text = label });
		OptionButton option = new OptionButton { CustomMinimumSize = new Vector2(300f, 36f) };
		foreach (string name in Enum.GetNames<T>()) option.AddItem(name);
		form.AddChild(option);
		return option;
	}

	private static List<string> Split(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? new List<string>() : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
	}

	private static string NormalizeFileId(string value)
	{
		string normalized = string.IsNullOrWhiteSpace(value) ? "untitled_content" : value.Trim().ToLowerInvariant();
		return string.Concat(normalized.Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_'));
	}
}
