using Godot;
using System;
using System.Linq;

public partial class MissionWorkbenchV2
{
	private void BuildUi()
	{
		CanvasLayer backgroundLayer = new CanvasLayer { Name = "WorkbenchBackdrop", Layer = -10 };
		AddChild(backgroundLayer);
		_uiLayer = new CanvasLayer { Name = "WorkbenchUI", Layer = 10 };
		AddChild(_uiLayer);

		ColorRect backdrop = new ColorRect { Color = new Color(0.018f, 0.025f, 0.045f, 1f), MouseFilter = Control.MouseFilterEnum.Ignore };
		backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		backgroundLayer.AddChild(backdrop);

		BuildTopBar();
		BuildPalettePanel();
		BuildInspectorPanel();
		BuildProblemsPanel();
		BuildModeViews();
		BuildDialogs();
	}

	private void BuildTopBar()
	{
		PanelContainer panel = CreateAnchoredPanel(0f, 0f, 1f, 0f, 8f, 8f, -8f, 66f, new Color(0.25f, 0.88f, 1f));
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		panel.AddChild(row);

		Label title = new Label { Text = "MISSION WORKBENCH v2" };
		title.AddThemeFontSizeOverride("font_size", 20);
		row.AddChild(title);

		_missionOption = new OptionButton { CustomMinimumSize = new Vector2(220f, 38f) };
		_missionOption.ItemSelected += index => { if (index >= 0 && index < _templates.Count) LoadMission(_templates[(int)index]); };
		row.AddChild(_missionOption);

		_modeOption = new OptionButton { CustomMinimumSize = new Vector2(120f, 38f) };
		_modeOption.AddItem("Map");
		_modeOption.AddItem("Flow");
		_modeOption.AddItem("Dialogue");
		_modeOption.ItemSelected += index => SetMode((WorkbenchMode)index);
		row.AddChild(_modeOption);

		row.AddChild(BuildButton("Save Source", SaveDocument));
		_undoButton = BuildButton("Undo", _store.Undo);
		_redoButton = BuildButton("Redo", _store.Redo);
		row.AddChild(_undoButton);
		row.AddChild(_redoButton);
		row.AddChild(BuildButton("Validate", ValidateDocument));
		row.AddChild(BuildButton("Compile", CompileDocument));
		row.AddChild(BuildButton("Playtest", PlaytestDocument));
		row.AddChild(BuildButton("Legacy Builder", () => GetTree().ChangeSceneToFile(LegacyBuilderScenePath)));
		row.AddChild(BuildButton("Overworld Registry", () => GetTree().ChangeSceneToFile(OverworldRegistryScenePath)));
		row.AddChild(BuildButton("Manual", ShowUserManual));
		row.AddChild(BuildButton("Exit", RequestExit));
		RefreshToolbar();
	}

	private void BuildDialogs()
	{
		_manualDialog = new AcceptDialog
		{
			Title = "Mission Workbench v2 — User Manual",
			OkButtonText = "Close",
			MinSize = new Vector2I(920, 700)
		};
		_manualText = new TextEdit
		{
			Editable = false,
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			CustomMinimumSize = new Vector2(880f, 620f)
		};
		_manualDialog.AddChild(_manualText);
		AddChild(_manualDialog);

		_exitConfirmation = new ConfirmationDialog
		{
			Title = "Exit Mission Workbench v2?",
			DialogText = "This mission has unsaved source changes. Exit and discard them?",
			OkButtonText = "Exit Without Saving",
			CancelButtonText = "Keep Working"
		};
		_exitConfirmation.Confirmed += ExitWorkbench;
		AddChild(_exitConfirmation);
	}

	private void ShowUserManual()
	{
		using FileAccess file = FileAccess.Open(UserManualPath, FileAccess.ModeFlags.Read);
		_manualText.Text = file?.GetAsText()
			?? "The user manual could not be loaded. See " + UserManualPath + ".";
		_manualText.SetCaretLine(0);
		_manualText.SetVScroll(0);
		_manualDialog.PopupCentered(new Vector2I(1000, 780));
	}

	private void RequestExit()
	{
		if (_store.IsDirty || _dialogueHasUnsavedChanges)
		{
			_exitConfirmation.DialogText = _store.IsDirty && _dialogueHasUnsavedChanges
				? "This mission has unsaved source and dialogue changes. Exit and discard them?"
				: _dialogueHasUnsavedChanges
					? "This mission has unsaved dialogue changes. Exit and discard them?"
					: "This mission has unsaved source changes. Exit and discard them?";
			_exitConfirmation.PopupCentered();
			return;
		}

		ExitWorkbench();
	}

	private void ExitWorkbench()
	{
		GetTree().Quit();
	}

	private void BuildPalettePanel()
	{
		PanelContainer panel = CreateAnchoredPanel(0f, 0f, 0f, 1f, 8f, 74f, 292f, -190f, new Color(0.28f, 0.72f, 0.98f));
		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 8);
		panel.AddChild(content);
		Label title = new Label { Text = "ASSET PALETTE" };
		title.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(title);
		Label hint = new Label { Text = "Choose an asset, then click the map.\nSelect mode lets you move existing objects.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		content.AddChild(hint);
		_paletteList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Single };
		_paletteList.ItemSelected += index => _placementAsset = _paletteList.GetItemMetadata((int)index).AsString();
		content.AddChild(_paletteList);
		Button selectButton = BuildButton("Selection Tool (Esc)", () => { _placementAsset = string.Empty; _paletteList.DeselectAll(); SetStatus("Selection tool active."); });
		content.AddChild(selectButton);
	}

	private void BuildInspectorPanel()
	{
		PanelContainer panel = CreateAnchoredPanel(1f, 0f, 1f, 1f, -372f, 74f, -8f, -190f, new Color(0.78f, 0.48f, 1f));
		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 8);
		panel.AddChild(content);
		Label title = new Label { Text = "CONTEXT INSPECTOR" };
		title.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(title);
		_selectionLabel = new Label { Text = "Nothing selected", AutowrapMode = TextServer.AutowrapMode.WordSmart };
		content.AddChild(_selectionLabel);
		ScrollContainer scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		content.AddChild(scroll);
		_inspectorFields = new VBoxContainer();
		_inspectorFields.AddThemeConstantOverride("separation", 7);
		scroll.AddChild(_inspectorFields);
	}

	private void BuildProblemsPanel()
	{
		PanelContainer panel = CreateAnchoredPanel(0f, 1f, 1f, 1f, 8f, -180f, -8f, -8f, new Color(1f, 0.58f, 0.28f));
		VBoxContainer content = new VBoxContainer();
		panel.AddChild(content);
		HBoxContainer header = new HBoxContainer();
		content.AddChild(header);
		Label title = new Label { Text = "PROBLEMS / OUTPUT", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		header.AddChild(title);
		_statusLabel = new Label { Text = "Ready.", HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		header.AddChild(_statusLabel);
		_problemsList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		_problemsList.ItemActivated += index => FocusValidationIssue((int)index);
		content.AddChild(_problemsList);
	}

	private void BuildModeViews()
	{
		_mapUiBlocker = new ColorRect
		{
			Color = new Color(0.018f, 0.025f, 0.045f, 0.98f),
			MouseFilter = Control.MouseFilterEnum.Stop,
			Visible = false
		};
		_mapUiBlocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_mapUiBlocker.OffsetLeft = 300f;
		_mapUiBlocker.OffsetTop = 74f;
		_mapUiBlocker.OffsetRight = -380f;
		_mapUiBlocker.OffsetBottom = -190f;
		_uiLayer.AddChild(_mapUiBlocker);

		_flowGraph = new GraphEdit { Visible = false, MinimapEnabled = true, ShowGrid = true };
		_flowGraph.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_flowGraph.OffsetLeft = 300f;
		_flowGraph.OffsetTop = 74f;
		_flowGraph.OffsetRight = -380f;
		_flowGraph.OffsetBottom = -190f;
		_uiLayer.AddChild(_flowGraph);

		_dialogueView = new PanelContainer { Visible = false };
		_dialogueView.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_dialogueView.OffsetLeft = 300f;
		_dialogueView.OffsetTop = 74f;
		_dialogueView.OffsetRight = -380f;
		_dialogueView.OffsetBottom = -190f;
		_dialogueView.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.45f, 0.85f, 0.72f)));
		_uiLayer.AddChild(_dialogueView);
		HBoxContainer dialogueColumns = new HBoxContainer();
		dialogueColumns.AddThemeConstantOverride("separation", 10);
		_dialogueView.AddChild(dialogueColumns);
		_dialogueConversationList = new ItemList { CustomMinimumSize = new Vector2(250f, 0f) };
		_dialogueConversationList.ItemSelected += OnDialogueConversationSelected;
		dialogueColumns.AddChild(_dialogueConversationList);
		_dialogueNodeList = new ItemList { CustomMinimumSize = new Vector2(260f, 0f) };
		_dialogueNodeList.ItemSelected += OnDialogueNodeSelected;
		dialogueColumns.AddChild(_dialogueNodeList);
		VBoxContainer dialogueEditor = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		dialogueEditor.AddChild(new Label { Text = "SPEAKER" });
		_dialogueSpeakerEdit = new LineEdit();
		_dialogueSpeakerEdit.TextChanged += _ => { if (_activeDialogueData != null) _dialogueHasUnsavedChanges = true; };
		dialogueEditor.AddChild(_dialogueSpeakerEdit);
		dialogueEditor.AddChild(new Label { Text = "DIALOGUE TEXT" });
		_dialogueTextEdit = new TextEdit { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		_dialogueTextEdit.TextChanged += () => { if (_activeDialogueData != null) _dialogueHasUnsavedChanges = true; };
		dialogueEditor.AddChild(_dialogueTextEdit);
		HBoxContainer dialogueButtons = new HBoxContainer();
		dialogueButtons.AddChild(BuildButton("Add Node", AddDialogueNode));
		dialogueButtons.AddChild(BuildButton("Delete Node", DeleteDialogueNode));
		dialogueButtons.AddChild(BuildButton("Save Dialogue", SaveDialogueNode));
		dialogueEditor.AddChild(dialogueButtons);
		dialogueColumns.AddChild(dialogueEditor);
	}

	private void BuildPalette()
	{
		_paletteList.Clear();
		AddPaletteHeader("— FLOOR —");
		foreach (MissionTileDefinition definition in MissionTileCatalog.FloorTiles.Where(definition => definition.VisibleInPalette)) AddPaletteItem(definition.DisplayName, $"tile:{definition.Id}");
		AddPaletteHeader("— WALL —");
		foreach (MissionTileDefinition definition in MissionTileCatalog.WallTiles.Where(definition => definition.VisibleInPalette)) AddPaletteItem(definition.DisplayName, $"tile:{definition.Id}");
		AddPaletteHeader("— PROP TILE —");
		foreach (MissionTileDefinition definition in MissionTileCatalog.PropTiles.Where(definition => definition.VisibleInPalette)) AddPaletteItem(definition.DisplayName, $"tile:{definition.Id}");
		AddPaletteHeader("— MARKERS —");
		foreach (MissionMarkerDefinition definition in MissionMarkerCatalog.All) AddPaletteItem(definition.DisplayName, $"marker:{definition.Id}");
	}

	private void AddPaletteHeader(string text)
	{
		int index = _paletteList.AddItem(text);
		_paletteList.SetItemDisabled(index, true);
	}

	private void AddPaletteItem(string text, string metadata)
	{
		int index = _paletteList.AddItem(text);
		_paletteList.SetItemMetadata(index, metadata);
	}

	private void ShowValidation(System.Collections.Generic.List<MissionValidationIssue> issues)
	{
		_visibleIssues.Clear();
		_visibleIssues.AddRange(issues ?? new System.Collections.Generic.List<MissionValidationIssue>());
		_problemsList.Clear();
		foreach (MissionValidationIssue issue in _visibleIssues)
		{
			string prefix = issue.Severity switch { MissionValidationSeverity.Error => "ERROR", MissionValidationSeverity.Warning => "WARN", _ => "INFO" };
			_problemsList.AddItem($"[{prefix}] {issue.Message}");
		}
		if (_visibleIssues.Count == 0) _problemsList.AddItem("No validation issues.");
	}

	private void FocusValidationIssue(int index)
	{
		if (index < 0 || index >= _visibleIssues.Count) return;
		MissionValidationIssue issue = _visibleIssues[index];
		if (string.IsNullOrWhiteSpace(issue.ElementId)) return;
		_store.Select(issue.ElementId);
		SetMode(WorkbenchMode.Map);
		MissionMapElement element = _store.GetSelectedElement();
		if (element != null) _camera.Position = IsoGridHelper.GridToWorld(element.Column, element.Row, MissionFloorTextureFactory.TileSize, new Vector2(0f, -20f));
	}

	private void RefreshFlowGraph()
	{
		if (_flowGraph == null || _store.Document == null) return;
		foreach (Node child in _flowGraph.GetChildren()) if (child is GraphNode) child.QueueFree();
		foreach (MissionFlowNodeData flow in _store.Document.FlowNodes)
		{
			GraphNode node = new GraphNode
			{
				Name = flow.Id,
				Title = $"{flow.Kind}: {flow.Label}",
				PositionOffset = new Vector2(flow.CanvasX, flow.CanvasY),
				CustomMinimumSize = new Vector2(240f, 130f)
			};
			node.GuiInput += input =>
			{
				if (input is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && !string.IsNullOrWhiteSpace(flow.MapElementId))
				{
					_store.Select(flow.MapElementId);
				}
			};
			node.AddChild(new Label { Text = $"Trigger: {flow.TriggerMode}\nTarget: {flow.TargetId}\nRequires: {string.Join(", ", flow.RequiredFlags)}\nSets: {string.Join(", ", flow.SetFlags)}" });
			_flowGraph.AddChild(node);
		}
		foreach (MissionOutcomeData outcome in _store.Document.Outcomes)
		{
			GraphNode node = new GraphNode
			{
				Name = $"outcome_{outcome.Id}", Title = $"Outcome: {outcome.ActionText}",
				PositionOffset = new Vector2(1120f, _store.Document.Outcomes.IndexOf(outcome) * 190f), CustomMinimumSize = new Vector2(260f, 120f)
			};
			node.AddChild(new Label { Text = $"ID: {outcome.Id}\nRequires: {string.Join(", ", outcome.RequiredFlags)}\nBlocks: {string.Join(", ", outcome.BlockedFlags)}" });
			_flowGraph.AddChild(node);
		}
	}

	private void CaptureFlowNodePositions()
	{
		if (_flowGraph == null || _store.Document == null) return;
		foreach (MissionFlowNodeData flow in _store.Document.FlowNodes)
		{
			GraphNode node = _flowGraph.GetNodeOrNull<GraphNode>(flow.Id);
			if (node == null) continue;
			flow.CanvasX = node.PositionOffset.X;
			flow.CanvasY = node.PositionOffset.Y;
		}
	}

	private void RefreshDialogueView()
	{
		_dialogueConversationList.Clear();
		_dialogueNodeList.Clear();
		_activeDialogueData = null;
		_activeDialogueNodeId = string.Empty;
		_dialogueHasUnsavedChanges = false;
		_dialogueSpeakerEdit.Text = string.Empty;
		_dialogueTextEdit.Text = string.Empty;
		foreach (string id in _store.Document?.DialogueConversationIds ?? new System.Collections.Generic.List<string>()) _dialogueConversationList.AddItem(id);
	}

	private void OnDialogueConversationSelected(long index)
	{
		_dialogueNodeList.Clear();
		_dialogueSpeakerEdit.Text = string.Empty;
		_dialogueTextEdit.Text = string.Empty;
		if (index < 0 || index >= _dialogueConversationList.ItemCount) return;
		_activeDialogueData = DialogueRegistry.LoadConversationData(_dialogueConversationList.GetItemText((int)index));
		foreach (DialogueNode node in _activeDialogueData?.Nodes ?? new System.Collections.Generic.List<DialogueNode>())
		{
			int item = _dialogueNodeList.AddItem($"{node.Id} — {node.SpeakerName}");
			_dialogueNodeList.SetItemMetadata(item, node.Id);
		}
		_dialogueHasUnsavedChanges = false;
	}

	private void OnDialogueNodeSelected(long index)
	{
		int conversationIndex = _dialogueConversationList.GetSelectedItems().FirstOrDefault(-1);
		if (conversationIndex < 0 || index < 0) return;
		string nodeId = _dialogueNodeList.GetItemMetadata((int)index).AsString();
		DialogueNode node = _activeDialogueData?.Nodes?.FirstOrDefault(candidate => candidate.Id == nodeId);
		if (node == null) return;
		_activeDialogueNodeId = node.Id;
		_dialogueSpeakerEdit.Text = node.SpeakerName;
		_dialogueTextEdit.Text = node.Text;
		_dialogueHasUnsavedChanges = false;
	}

	private void AddDialogueNode()
	{
		if (_activeDialogueData == null) return;
		string baseId = "node";
		int suffix = 1;
		while (_activeDialogueData.Nodes.Any(node => node.Id == $"{baseId}_{suffix}")) suffix++;
		DialogueNode node = new DialogueNode { Id = $"{baseId}_{suffix}", SpeakerName = "Speaker", Text = "New dialogue line." };
		_activeDialogueData.Nodes.Add(node);
		_activeDialogueNodeId = node.Id;
		ReloadActiveDialogueNodeList();
		_dialogueHasUnsavedChanges = true;
	}

	private void DeleteDialogueNode()
	{
		DialogueNode node = _activeDialogueData?.Nodes?.FirstOrDefault(candidate => candidate.Id == _activeDialogueNodeId);
		if (node == null || _activeDialogueData.Nodes.Count <= 1) return;
		_activeDialogueData.Nodes.Remove(node);
		_activeDialogueNodeId = _activeDialogueData.Nodes[0].Id;
		ReloadActiveDialogueNodeList();
		_dialogueHasUnsavedChanges = true;
	}

	private void SaveDialogueNode()
	{
		DialogueNode node = _activeDialogueData?.Nodes?.FirstOrDefault(candidate => candidate.Id == _activeDialogueNodeId);
		if (node == null) return;
		node.SpeakerName = _dialogueSpeakerEdit.Text.Trim();
		node.Text = _dialogueTextEdit.Text;
		if (DialogueRegistry.SaveConversationData(_activeDialogueData))
		{
			_dialogueHasUnsavedChanges = false;
			SetStatus($"Saved dialogue {_activeDialogueData.ConversationId}.");
		}
		else SetStatus("Dialogue save failed.", true);
	}

	private void ReloadActiveDialogueNodeList()
	{
		_dialogueNodeList.Clear();
		foreach (DialogueNode node in _activeDialogueData?.Nodes ?? new System.Collections.Generic.List<DialogueNode>())
		{
			int item = _dialogueNodeList.AddItem($"{node.Id} — {node.SpeakerName}");
			_dialogueNodeList.SetItemMetadata(item, node.Id);
			if (node.Id == _activeDialogueNodeId) _dialogueNodeList.Select(item);
		}
		DialogueNode active = _activeDialogueData?.Nodes?.FirstOrDefault(node => node.Id == _activeDialogueNodeId);
		_dialogueSpeakerEdit.Text = active?.SpeakerName ?? string.Empty;
		_dialogueTextEdit.Text = active?.Text ?? string.Empty;
	}

	private PanelContainer CreateAnchoredPanel(float leftAnchor, float topAnchor, float rightAnchor, float bottomAnchor, float left, float top, float right, float bottom, Color border)
	{
		PanelContainer panel = new PanelContainer
		{
			AnchorLeft = leftAnchor, AnchorTop = topAnchor, AnchorRight = rightAnchor, AnchorBottom = bottomAnchor,
			OffsetLeft = left, OffsetTop = top, OffsetRight = right, OffsetBottom = bottom
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(border));
		_uiLayer.AddChild(panel);
		return panel;
	}

	private static Button BuildButton(string text, Action action)
	{
		Button button = new Button { Text = text, CustomMinimumSize = new Vector2(92f, 36f) };
		button.Pressed += () => action?.Invoke();
		return button;
	}
}
