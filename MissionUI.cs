using Godot;
using System.Collections.Generic;

public sealed class MissionExtractionOption
{
	public string OutcomeId { get; init; } = string.Empty;
	public string DisplayText { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
}

public partial class MissionUI : CanvasLayer
{
	[Signal]
	public delegate void ExtractionOutcomeChosenEventHandler(string outcomeId);

	public Label TitleLabel { get; private set; }
	public Label ObjectiveLabel { get; private set; }
	public Label SelectedOfficerLabel { get; private set; }
	public Label PromptLabel { get; private set; }
	public bool IsExtractionPromptVisible => _extractionPromptPanel?.Visible ?? false;

	private Control _uiRoot;
	private PanelContainer _legacyActionPanel;
	private PanelContainer _extractionPromptPanel;
	private Label _extractionPromptTitleLabel;
	private Label _extractionPromptBodyLabel;
	private VBoxContainer _extractionPromptButtonStack;

	public override void _Ready()
	{
		_uiRoot = GetNode<Control>("UIRoot");
		TitleLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/TitleLabel");
		ObjectiveLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/ObjectiveLabel");
		SelectedOfficerLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/SelectedOfficerLabel");
		PromptLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/PromptLabel");
		_legacyActionPanel = GetNodeOrNull<PanelContainer>("UIRoot/BottomRightPanel");
		if (_legacyActionPanel != null)
		{
			_legacyActionPanel.Visible = false;
		}

		BuildExtractionPrompt();
	}

	public void SetMissionText(string title, string objective, string prompt)
	{
		if (TitleLabel != null) TitleLabel.Text = title;
		if (ObjectiveLabel != null) ObjectiveLabel.Text = objective;
		if (PromptLabel != null) PromptLabel.Text = prompt;
	}

	public void SetSelectedOfficer(string officerName, string shipName, string specialty)
	{
		if (SelectedOfficerLabel == null)
		{
			return;
		}

		SelectedOfficerLabel.Text = $"ACTIVE OFFICER: {officerName}\nSHIP: {shipName}\nSPECIALTY: {specialty}";
	}

	public void ShowExtractionPrompt(string title, string body, IReadOnlyList<MissionExtractionOption> options)
	{
		if (_extractionPromptPanel == null || _extractionPromptTitleLabel == null || _extractionPromptBodyLabel == null || _extractionPromptButtonStack == null)
		{
			return;
		}

		_extractionPromptTitleLabel.Text = title;
		_extractionPromptBodyLabel.Text = body;

		foreach (Node child in _extractionPromptButtonStack.GetChildren())
		{
			child.QueueFree();
		}

		foreach (MissionExtractionOption option in options ?? new List<MissionExtractionOption>())
		{
			if (option == null || string.IsNullOrWhiteSpace(option.OutcomeId))
			{
				continue;
			}

			Button button = new Button
			{
				Text = option.DisplayText,
				TooltipText = option.Description,
				CustomMinimumSize = new Vector2(0f, 46f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			string outcomeId = option.OutcomeId;
			button.Pressed += () => EmitSignal(SignalName.ExtractionOutcomeChosen, outcomeId);
			_extractionPromptButtonStack.AddChild(button);
		}

		_extractionPromptPanel.Visible = _extractionPromptButtonStack.GetChildCount() > 0;
	}

	public void HideExtractionPrompt()
	{
		if (_extractionPromptPanel != null)
		{
			_extractionPromptPanel.Visible = false;
		}
	}

	private void BuildExtractionPrompt()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_extractionPromptPanel = new PanelContainer
		{
			Name = "ExtractionPromptPanel",
			Visible = false,
			OffsetLeft = 610f,
			OffsetTop = 740f,
			OffsetRight = 1310f,
			OffsetBottom = 1040f
		};
		_uiRoot.AddChild(_extractionPromptPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		_extractionPromptPanel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		_extractionPromptTitleLabel = new Label
		{
			Text = "EXTRACTION READY"
		};
		_extractionPromptTitleLabel.AddThemeFontSizeOverride("font_size", 24);
		content.AddChild(_extractionPromptTitleLabel);

		_extractionPromptBodyLabel = new Label
		{
			Text = "All officers are on the evac zone.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_extractionPromptBodyLabel);

		_extractionPromptButtonStack = new VBoxContainer();
		_extractionPromptButtonStack.AddThemeConstantOverride("separation", 10);
		content.AddChild(_extractionPromptButtonStack);
	}
}
