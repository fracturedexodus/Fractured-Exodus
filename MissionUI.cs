using Godot;
using System.Collections.Generic;

public sealed class MissionExtractionOption
{
	public string OutcomeId { get; init; } = string.Empty;
	public string DisplayText { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
}

public sealed class MissionCombatantSummary
{
	public string DisplayName { get; init; } = string.Empty;
	public string Subtitle { get; init; } = string.Empty;
	public string WeaponName { get; init; } = string.Empty;
	public string ShieldName { get; init; } = string.Empty;
	public Texture2D Icon { get; init; }
	public int CurrentHP { get; init; }
	public int MaxHP { get; init; }
	public int CurrentShields { get; init; }
	public int MaxShields { get; init; }
	public int CurrentAP { get; init; }
	public int MaxAP { get; init; }
	public int AttackRange { get; init; }
	public int AttackMinDamage { get; init; }
	public int AttackMaxDamage { get; init; }
	public string Notes { get; init; } = string.Empty;
}

public partial class MissionUI : CanvasLayer
{
	private static readonly Color HudPanelBackground = new Color(0.04f, 0.05f, 0.08f, 0.74f);
	private static readonly Color HudPanelBorder = new Color(0.22f, 0.28f, 0.36f, 0.62f);
	private static readonly Color HudAccentBorder = new Color(0.24f, 0.64f, 0.78f, 0.82f);
	private const string ActionLogReadyHeader = "[color=gray]--- ACTION LOG READY ---[/color]";

	private const float ExplorationSelectionSingleWidth = 372f;
	private const float ExplorationSelectionDoubleWidth = 732f;
	private const float ExplorationCardWidth = 348f;
	private const float ExplorationCardHeight = 286f;

	[Signal]
	public delegate void ExtractionOutcomeChosenEventHandler(string outcomeId);

	[Signal]
	public delegate void CombatEndTurnPressedEventHandler();

	[Signal]
	public delegate void MissionSaveConfirmedEventHandler(string saveName);

	[Signal]
	public delegate void StoryEventConfirmedEventHandler();

	[Signal]
	public delegate void ConfirmationAcceptedEventHandler();

	[Signal]
	public delegate void ConfirmationCancelledEventHandler();

	public Label TitleLabel { get; private set; }
	public Label ObjectiveLabel { get; private set; }
	public Label SelectedOfficerLabel { get; private set; }
	public Label PromptLabel { get; private set; }
	public bool IsExtractionPromptVisible => _extractionPromptPanel?.Visible ?? false;
	public bool IsStoryEventVisible => _storyEventPanel?.Visible ?? false;
	public bool IsConfirmationVisible => _confirmationPromptPanel?.Visible ?? false;
	public bool IsMissionSavePromptVisible => _savePromptPanel?.Visible ?? false;

	private Control _uiRoot;
	private PanelContainer _topLeftPanel;
	private PanelContainer _legacyActionPanel;
	private PanelContainer _extractionPromptPanel;
	private Label _extractionPromptTitleLabel;
	private Label _extractionPromptBodyLabel;
	private VBoxContainer _extractionPromptButtonStack;
	private VBoxContainer _initiativeRoot;
	private Label _combatTurnLabel;
	private HBoxContainer _combatInitiativeRow;
	private Button _combatEndTurnButton;
	private PanelContainer _playerCombatInfoPanel;
	private TextureRect _playerCombatIcon;
	private Label _playerCombatHeaderLabel;
	private Label _playerCombatInfoLabel;
	private PanelContainer _enemyCombatInfoPanel;
	private TextureRect _enemyCombatIcon;
	private Label _enemyCombatHeaderLabel;
	private Label _enemyCombatInfoLabel;
	private PanelContainer _explorationSelectionPanel;
	private PanelContainer _explorationPrimaryInfoPanel;
	private TextureRect _explorationPrimaryIcon;
	private Label _explorationPrimaryHeaderLabel;
	private Label _explorationPrimaryInfoLabel;
	private PanelContainer _explorationSecondaryInfoPanel;
	private TextureRect _explorationSecondaryIcon;
	private Label _explorationSecondaryHeaderLabel;
	private Label _explorationSecondaryInfoLabel;
	private PanelContainer _combatLogPanel;
	private RichTextLabel _combatLogText;
	private readonly List<string> _combatLogEntries = new List<string>();
	private PanelContainer _actionLogPanel;
	private RichTextLabel _actionLogText;
	private readonly List<string> _actionLogEntries = new List<string>();
	private PanelContainer _hoverSummaryPanel;
	private Label _hoverSummaryLabel;
	private PanelContainer _storyEventPanel;
	private TextureRect _storyEventImage;
	private RichTextLabel _storyEventDescription;
	private Button _storyEventConfirmButton;
	private PanelContainer _confirmationPromptPanel;
	private Label _confirmationPromptTitleLabel;
	private Label _confirmationPromptBodyLabel;
	private Button _confirmationPromptConfirmButton;
	private Button _confirmationPromptCancelButton;
	private PanelContainer _savePromptPanel;
	private LineEdit _saveNameLineEdit;
	private Label _savePromptStatusLabel;
	private ColorRect _gameOverPanel;
	private Label _gameOverLabel;
	private Button _gameOverReturnButton;

	public Button GameOverReturnButton => _gameOverReturnButton;

	public override void _Ready()
	{
		_uiRoot = GetNode<Control>("UIRoot");
		_topLeftPanel = GetNodeOrNull<PanelContainer>("UIRoot/TopLeftPanel");
		TitleLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/TitleLabel");
		ObjectiveLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/ObjectiveLabel");
		SelectedOfficerLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/SelectedOfficerLabel");
		PromptLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/PromptLabel");
		if (_topLeftPanel != null)
		{
			_topLeftPanel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
			_topLeftPanel.AnchorLeft = 0.5f;
			_topLeftPanel.AnchorRight = 0.5f;
			_topLeftPanel.OffsetLeft = -280f;
			_topLeftPanel.OffsetTop = 20f;
			_topLeftPanel.OffsetRight = 280f;
			_topLeftPanel.OffsetBottom = 156f;
			_topLeftPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());
		}
		if (SelectedOfficerLabel != null)
		{
			SelectedOfficerLabel.Visible = false;
		}
		if (PromptLabel != null)
		{
			PromptLabel.Visible = false;
		}
		_legacyActionPanel = GetNodeOrNull<PanelContainer>("UIRoot/BottomRightPanel");
		if (_legacyActionPanel != null)
		{
			_legacyActionPanel.Visible = false;
		}

		BuildExtractionPrompt();
		BuildCombatHud();
		BuildExplorationSelectionHud();
		BuildActionLog();
		BuildHoverSummary();
		BuildStoryEventPanel();
		BuildConfirmationPrompt();
		BuildMissionSavePrompt();
		BuildGameOverPanel();
		ApplyBattlemapLabelStyling();
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

	public void SetCombatHudVisible(bool visible)
	{
		if (_initiativeRoot != null)
		{
			_initiativeRoot.Visible = visible;
		}

		if (_playerCombatInfoPanel != null)
		{
			_playerCombatInfoPanel.Visible = visible;
		}

		if (_enemyCombatInfoPanel != null)
		{
			_enemyCombatInfoPanel.Visible = visible;
		}

		if (_combatLogPanel != null)
		{
			_combatLogPanel.Visible = false;
		}

		if (_explorationSelectionPanel != null && visible)
		{
			_explorationSelectionPanel.Visible = false;
		}
	}

	public void SetCombatTurnLabel(string text)
	{
		if (_combatTurnLabel != null)
		{
			_combatTurnLabel.Text = text;
		}
	}

	public void SetCombatInitiative(IReadOnlyList<MissionCombatantSummary> queue, int activeIndex)
	{
		if (_combatInitiativeRow == null)
		{
			return;
		}

		foreach (Node child in _combatInitiativeRow.GetChildren())
		{
			child.QueueFree();
		}

		StyleBoxFlat defaultStyle = CreateCombatSquareStyle(new Color(0.08f, 0.08f, 0.08f, 0.96f), new Color(0.32f, 0.32f, 0.32f, 1f));
		StyleBoxFlat activeStyle = CreateCombatSquareStyle(new Color(0.08f, 0.12f, 0.10f, 0.98f), new Color(0.30f, 1.00f, 0.55f, 1f));
		for (int i = 0; i < (queue?.Count ?? 0); i++)
		{
			MissionCombatantSummary summary = queue[i];
			if (summary == null)
			{
				continue;
			}

			PanelContainer square = new PanelContainer
			{
				CustomMinimumSize = new Vector2(82f, 82f),
			TooltipText = $"{summary.DisplayName}\nHP {summary.CurrentHP}/{summary.MaxHP} | SHD {summary.CurrentShields}/{summary.MaxShields} | AP {summary.CurrentAP}/{summary.MaxAP}\n{summary.WeaponName} | {summary.ShieldName}"
			};
			square.AddThemeStyleboxOverride("panel", (i == activeIndex ? activeStyle : defaultStyle).Duplicate() as StyleBoxFlat);
			VBoxContainer content = new VBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Center
			};
			TextureRect icon = new TextureRect
			{
				CustomMinimumSize = new Vector2(52f, 40f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Texture = summary.Icon
			};
			content.AddChild(icon);
			Label label = new Label
			{
				Text = summary.DisplayName,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			label.AddThemeFontSizeOverride("font_size", 11);
			content.AddChild(label);
			square.AddChild(content);
			_combatInitiativeRow.AddChild(square);
		}
	}

	public void SetPlayerCombatInfo(MissionCombatantSummary summary)
	{
		UpdateCombatInfoPanel(summary, _playerCombatInfoPanel, _playerCombatIcon, _playerCombatHeaderLabel, _playerCombatInfoLabel, "OFFICER");
	}

	public void SetEnemyCombatInfo(MissionCombatantSummary summary)
	{
		UpdateCombatInfoPanel(summary, _enemyCombatInfoPanel, _enemyCombatIcon, _enemyCombatHeaderLabel, _enemyCombatInfoLabel, "ENEMY");
	}

	public void SetExplorationSelectionInfo(IReadOnlyList<MissionCombatantSummary> summaries, bool visible)
	{
		if (_explorationSelectionPanel == null)
		{
			return;
		}

		if (!visible || summaries == null || summaries.Count == 0)
		{
			_explorationSelectionPanel.Visible = false;
			return;
		}

		_explorationSelectionPanel.Visible = true;
		UpdateExplorationSelectionPanelWidth(Mathf.Clamp(summaries.Count, 1, 2));
		UpdateCombatInfoPanel(
			summaries.Count > 0 ? summaries[0] : null,
			_explorationPrimaryInfoPanel,
			_explorationPrimaryIcon,
			_explorationPrimaryHeaderLabel,
			_explorationPrimaryInfoLabel,
			"OFFICER");
		UpdateCombatInfoPanel(
			summaries.Count > 1 ? summaries[1] : null,
			_explorationSecondaryInfoPanel,
			_explorationSecondaryIcon,
			_explorationSecondaryHeaderLabel,
			_explorationSecondaryInfoLabel,
			"OFFICER");
	}

	public void SetCombatEndTurnEnabled(bool enabled, bool visible = true)
	{
		if (_combatEndTurnButton == null)
		{
			return;
		}

		_combatEndTurnButton.Visible = visible;
		_combatEndTurnButton.Disabled = !enabled;
	}

	public void ShowMissionGameOver()
	{
		if (_gameOverPanel == null)
		{
			return;
		}

		_gameOverLabel.Text = "AWAY TEAM LOST";
		_gameOverPanel.Visible = true;
	}

	public void AppendCombatLog(string message)
	{
		if (_combatLogText == null || string.IsNullOrWhiteSpace(message))
		{
			return;
		}

		_combatLogEntries.Add(message.Trim());
		while (_combatLogEntries.Count > 8)
		{
			_combatLogEntries.RemoveAt(0);
		}

		_combatLogText.Text = string.Join("\n", _combatLogEntries);
		_combatLogText.ScrollToLine(_combatLogEntries.Count);
	}

	public void AppendActionLog(string message)
	{
		if (_actionLogText == null || string.IsNullOrWhiteSpace(message))
		{
			return;
		}

		_actionLogEntries.Add(message.Trim());
		while (_actionLogEntries.Count > 12)
		{
			_actionLogEntries.RemoveAt(0);
		}

		string body = string.Join("\n\n", _actionLogEntries);
		_actionLogText.Text = string.IsNullOrWhiteSpace(body)
			? ActionLogReadyHeader
			: $"{ActionLogReadyHeader}\n\n{body}";
		CallDeferred(nameof(ScrollActionLogToBottom));
	}

	public void ClearCombatLog()
	{
		_combatLogEntries.Clear();
		if (_combatLogText != null)
		{
			_combatLogText.Text = string.Empty;
		}
	}

	public void ClearActionLog()
	{
		_actionLogEntries.Clear();
		if (_actionLogText != null)
		{
			_actionLogText.Text = ActionLogReadyHeader;
			CallDeferred(nameof(ScrollActionLogToBottom));
		}
	}

	public void ShowHoverSummary(MissionCombatantSummary summary, Vector2 screenPosition)
	{
		if (_hoverSummaryPanel == null || _hoverSummaryLabel == null || summary == null)
		{
			return;
		}

		_hoverSummaryLabel.Text = $"{summary.DisplayName}\n{summary.Subtitle}\nWEAPON: {summary.WeaponName}\nSHIELD: {summary.ShieldName}\nHP: {summary.CurrentHP}/{summary.MaxHP}\nSHIELDS: {summary.CurrentShields}/{summary.MaxShields}\nAP: {summary.CurrentAP}/{summary.MaxAP}\nRANGE: {summary.AttackRange} | DMG: {summary.AttackMinDamage}-{summary.AttackMaxDamage}" + (string.IsNullOrWhiteSpace(summary.Notes) ? string.Empty : $"\n{summary.Notes}");
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 desiredPosition = screenPosition + new Vector2(34f, -24f);
		float maxX = Mathf.Max(12f, viewportSize.X - _hoverSummaryPanel.Size.X - 12f);
		float maxY = Mathf.Max(12f, viewportSize.Y - _hoverSummaryPanel.Size.Y - 12f);
		_hoverSummaryPanel.Position = new Vector2(
			Mathf.Clamp(desiredPosition.X, 12f, maxX),
			Mathf.Clamp(desiredPosition.Y, 12f, maxY));
		_hoverSummaryPanel.Visible = true;
	}

	public void HideHoverSummary()
	{
		if (_hoverSummaryPanel != null)
		{
			_hoverSummaryPanel.Visible = false;
		}
	}

	public void ShowStoryEvent(string title, string description, string imagePath, string confirmButtonText)
	{
		if (_storyEventPanel == null || _storyEventDescription == null || _storyEventConfirmButton == null || _storyEventImage == null)
		{
			return;
		}

		if (_storyEventPanel.GetNodeOrNull<Label>("TitleLabel") is Label titleLabel)
		{
			titleLabel.Text = string.IsNullOrWhiteSpace(title) ? "MISSION EVENT" : title.ToUpperInvariant();
		}

		_storyEventDescription.Text = string.IsNullOrWhiteSpace(description)
			? string.Empty
			: description;
		_storyEventImage.Texture = !string.IsNullOrWhiteSpace(imagePath) && ResourceLoader.Exists(imagePath)
			? GD.Load<Texture2D>(imagePath)
			: null;
		_storyEventConfirmButton.Text = string.IsNullOrWhiteSpace(confirmButtonText) ? "CONTINUE" : confirmButtonText;
		_storyEventPanel.Visible = true;
	}

	public void HideStoryEvent()
	{
		if (_storyEventPanel != null)
		{
			_storyEventPanel.Visible = false;
		}
	}

	public void ShowConfirmationPrompt(string title, string body, string confirmText, string cancelText)
	{
		if (_confirmationPromptPanel == null || _confirmationPromptTitleLabel == null || _confirmationPromptBodyLabel == null)
		{
			return;
		}

		_confirmationPromptTitleLabel.Text = string.IsNullOrWhiteSpace(title) ? "CONFIRM ACTION" : title.ToUpperInvariant();
		_confirmationPromptBodyLabel.Text = body ?? string.Empty;
		if (_confirmationPromptConfirmButton != null)
		{
			_confirmationPromptConfirmButton.Text = string.IsNullOrWhiteSpace(confirmText) ? "CONFIRM" : confirmText.ToUpperInvariant();
		}

		if (_confirmationPromptCancelButton != null)
		{
			_confirmationPromptCancelButton.Text = string.IsNullOrWhiteSpace(cancelText) ? "CANCEL" : cancelText.ToUpperInvariant();
		}

		_confirmationPromptPanel.Visible = true;
	}

	public void HideConfirmationPrompt()
	{
		if (_confirmationPromptPanel != null)
		{
			_confirmationPromptPanel.Visible = false;
		}
	}

	public void ShowMissionSavePrompt(string suggestedName)
	{
		if (_savePromptPanel == null || _saveNameLineEdit == null || _savePromptStatusLabel == null)
		{
			return;
		}

		_savePromptStatusLabel.Text = string.Empty;
		_saveNameLineEdit.Text = string.IsNullOrWhiteSpace(suggestedName) ? "Mission Save" : suggestedName.Trim();
		_savePromptPanel.Visible = true;
		_saveNameLineEdit.GrabFocus();
		_saveNameLineEdit.SelectAll();
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
		_extractionPromptPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle(0.88f, true));
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

	private void BuildCombatHud()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_initiativeRoot = new VBoxContainer
		{
			Visible = false
		};
		_initiativeRoot.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_initiativeRoot.OffsetLeft = 20f;
		_initiativeRoot.OffsetTop = 20f;
		_initiativeRoot.OffsetRight = 440f;
		_initiativeRoot.OffsetBottom = 210f;
		_initiativeRoot.AddThemeConstantOverride("separation", 8);
		_uiRoot.AddChild(_initiativeRoot);

		_combatTurnLabel = new Label
		{
			Text = "MISSION COMBAT",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		_combatTurnLabel.AddThemeFontSizeOverride("font_size", 22);
		_initiativeRoot.AddChild(_combatTurnLabel);

		_combatInitiativeRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin
		};
		_combatInitiativeRow.AddThemeConstantOverride("separation", 8);
		_initiativeRoot.AddChild(_combatInitiativeRow);

		_combatEndTurnButton = new Button
		{
			Text = "END TURN",
			CustomMinimumSize = new Vector2(180f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		_combatEndTurnButton.Pressed += () => EmitSignal(SignalName.CombatEndTurnPressed);
		_initiativeRoot.AddChild(_combatEndTurnButton);

		_playerCombatInfoPanel = BuildCombatInfoPanel(new Vector2(20f, 760f), out _playerCombatIcon, out _playerCombatHeaderLabel, out _playerCombatInfoLabel);
		_enemyCombatInfoPanel = BuildCombatInfoPanel(new Vector2(1500f, 760f), out _enemyCombatIcon, out _enemyCombatHeaderLabel, out _enemyCombatInfoLabel);
		_playerCombatInfoPanel.Visible = false;
		_enemyCombatInfoPanel.Visible = false;
		_uiRoot.AddChild(_playerCombatInfoPanel);
		_uiRoot.AddChild(_enemyCombatInfoPanel);
	}

	private void BuildExplorationSelectionHud()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_explorationSelectionPanel = new PanelContainer
		{
			Visible = false
		};
		_explorationSelectionPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		_explorationSelectionPanel.OffsetLeft = 20f;
		_explorationSelectionPanel.OffsetTop = -360f;
		_explorationSelectionPanel.OffsetRight = 20f + ExplorationSelectionDoubleWidth;
		_explorationSelectionPanel.OffsetBottom = -58f;
		_explorationSelectionPanel.AddThemeStyleboxOverride("panel", CreateTransparentPanelStyle());
		_uiRoot.AddChild(_explorationSelectionPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		_explorationSelectionPanel.AddChild(margin);

		HBoxContainer row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin
		};
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		_explorationPrimaryInfoPanel = BuildCombatInfoPanel(
			Vector2.Zero,
			out _explorationPrimaryIcon,
			out _explorationPrimaryHeaderLabel,
			out _explorationPrimaryInfoLabel,
			new Vector2(ExplorationCardWidth, ExplorationCardHeight),
			new Vector2(156f, 132f));
		_explorationSecondaryInfoPanel = BuildCombatInfoPanel(
			Vector2.Zero,
			out _explorationSecondaryIcon,
			out _explorationSecondaryHeaderLabel,
			out _explorationSecondaryInfoLabel,
			new Vector2(ExplorationCardWidth, ExplorationCardHeight),
			new Vector2(156f, 132f));
		_explorationPrimaryInfoPanel.Position = Vector2.Zero;
		_explorationSecondaryInfoPanel.Position = Vector2.Zero;
		_explorationPrimaryInfoPanel.Visible = false;
		_explorationSecondaryInfoPanel.Visible = false;
		_explorationPrimaryInfoPanel.AddThemeStyleboxOverride("panel", CreateTransparentPanelStyle());
		_explorationSecondaryInfoPanel.AddThemeStyleboxOverride("panel", CreateTransparentPanelStyle());
		row.AddChild(_explorationPrimaryInfoPanel);
		row.AddChild(_explorationSecondaryInfoPanel);
	}

	private void BuildCombatLog()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_combatLogPanel = new PanelContainer
		{
			Visible = false
		};
		_combatLogPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		_combatLogPanel.OffsetLeft = 20f;
		_combatLogPanel.OffsetTop = -280f;
		_combatLogPanel.OffsetRight = 370f;
		_combatLogPanel.OffsetBottom = -20f;
		_combatLogPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());
		_uiRoot.AddChild(_combatLogPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		_combatLogPanel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 8);
		margin.AddChild(content);

		Label title = new Label
		{
			Text = "COMBAT LOG",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(title);

		_combatLogText = new RichTextLabel
		{
			FitContent = false,
			ScrollActive = true,
			SelectionEnabled = false,
			CustomMinimumSize = new Vector2(0f, 132f),
			BbcodeEnabled = false
		};
		content.AddChild(_combatLogText);
	}

	private void BuildActionLog()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_actionLogPanel = new PanelContainer();
		_actionLogPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_actionLogPanel.OffsetLeft = -370f;
		_actionLogPanel.OffsetTop = 20f;
		_actionLogPanel.OffsetRight = -20f;
		_actionLogPanel.OffsetBottom = 270f;
		_actionLogPanel.AddThemeStyleboxOverride("panel", CreateBattlemapActionLogStyle());
		_uiRoot.AddChild(_actionLogPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		_actionLogPanel.AddChild(margin);

		_actionLogText = new RichTextLabel
		{
			FitContent = false,
			ScrollActive = true,
			ScrollFollowing = true,
			SelectionEnabled = false,
			CustomMinimumSize = new Vector2(320f, 220f),
			BbcodeEnabled = true
		};
		margin.AddChild(_actionLogText);
		_actionLogText.Text = ActionLogReadyHeader;
	}

	private void BuildHoverSummary()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_hoverSummaryPanel = new PanelContainer
		{
			Visible = false,
			Size = new Vector2(260f, 170f)
		};
		_hoverSummaryPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle(0.88f, true));
		_uiRoot.AddChild(_hoverSummaryPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		_hoverSummaryPanel.AddChild(margin);

		_hoverSummaryLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		margin.AddChild(_hoverSummaryLabel);
	}

	private void BuildStoryEventPanel()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_storyEventPanel = new PanelContainer
		{
			Visible = false
		};
		_storyEventPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_storyEventPanel.OffsetLeft = -624f;
		_storyEventPanel.OffsetTop = -420f;
		_storyEventPanel.OffsetRight = 624f;
		_storyEventPanel.OffsetBottom = 420f;
		_uiRoot.AddChild(_storyEventPanel);

		_storyEventPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle(0.94f, true));

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		_storyEventPanel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		Label titleLabel = new Label
		{
			Name = "TitleLabel",
			Text = "MISSION EVENT",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 26);
		titleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.96f, 1f));
		content.AddChild(titleLabel);

		_storyEventImage = new TextureRect
		{
			CustomMinimumSize = new Vector2(0f, 504f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		content.AddChild(_storyEventImage);

		_storyEventDescription = new RichTextLabel
		{
			BbcodeEnabled = false,
			FitContent = true,
			ScrollActive = true,
			CustomMinimumSize = new Vector2(0f, 120f)
		};
		content.AddChild(_storyEventDescription);

		_storyEventConfirmButton = new Button
		{
			Text = "CONTINUE",
			CustomMinimumSize = new Vector2(0f, 48f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_storyEventConfirmButton.Pressed += () => EmitSignal(SignalName.StoryEventConfirmed);
		content.AddChild(_storyEventConfirmButton);
	}

	private void BuildConfirmationPrompt()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_confirmationPromptPanel = new PanelContainer
		{
			Visible = false
		};
		_confirmationPromptPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_confirmationPromptPanel.OffsetLeft = -280f;
		_confirmationPromptPanel.OffsetTop = -140f;
		_confirmationPromptPanel.OffsetRight = 280f;
		_confirmationPromptPanel.OffsetBottom = 140f;
		_uiRoot.AddChild(_confirmationPromptPanel);

		_confirmationPromptPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle(0.94f, true));

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		_confirmationPromptPanel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		_confirmationPromptTitleLabel = new Label
		{
			Text = "CONFIRM ACTION",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_confirmationPromptTitleLabel.AddThemeFontSizeOverride("font_size", 24);
		_confirmationPromptTitleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.96f, 1f));
		content.AddChild(_confirmationPromptTitleLabel);

		_confirmationPromptBodyLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_confirmationPromptBodyLabel);

		HBoxContainer buttons = new HBoxContainer();
		buttons.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttons);

		_confirmationPromptCancelButton = new Button
		{
			Text = "CANCEL",
			CustomMinimumSize = new Vector2(0f, 46f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_confirmationPromptCancelButton.Pressed += () => EmitSignal(SignalName.ConfirmationCancelled);
		buttons.AddChild(_confirmationPromptCancelButton);

		_confirmationPromptConfirmButton = new Button
		{
			Text = "CONFIRM",
			CustomMinimumSize = new Vector2(0f, 46f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_confirmationPromptConfirmButton.Pressed += () => EmitSignal(SignalName.ConfirmationAccepted);
		buttons.AddChild(_confirmationPromptConfirmButton);
	}

	private void BuildMissionSavePrompt()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_savePromptPanel = new PanelContainer
		{
			Visible = false
		};
		_savePromptPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_savePromptPanel.OffsetLeft = -280f;
		_savePromptPanel.OffsetTop = -130f;
		_savePromptPanel.OffsetRight = 280f;
		_savePromptPanel.OffsetBottom = 130f;
		_savePromptPanel.AddThemeStyleboxOverride("panel", CreateActionLogStyle());
		_uiRoot.AddChild(_savePromptPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		_savePromptPanel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		Label title = new Label
		{
			Text = "NAME SAVE FILE",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		content.AddChild(title);

		_saveNameLineEdit = new LineEdit
		{
			PlaceholderText = "Black Site Relay Save",
			CustomMinimumSize = new Vector2(0f, 42f)
		};
		_saveNameLineEdit.TextSubmitted += _ => ConfirmMissionSavePrompt();
		content.AddChild(_saveNameLineEdit);

		_savePromptStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_savePromptStatusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.55f));
		content.AddChild(_savePromptStatusLabel);

		HBoxContainer buttons = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttons.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttons);

		Button cancelButton = new Button
		{
			Text = "CANCEL",
			CustomMinimumSize = new Vector2(180f, 42f)
		};
		cancelButton.Pressed += HideMissionSavePrompt;
		buttons.AddChild(cancelButton);

		Button saveButton = new Button
		{
			Text = "SAVE",
			CustomMinimumSize = new Vector2(180f, 42f)
		};
		saveButton.Pressed += ConfirmMissionSavePrompt;
		buttons.AddChild(saveButton);
	}

	private void BuildGameOverPanel()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_gameOverPanel = new ColorRect
		{
			Visible = false,
			Color = new Color(0f, 0f, 0f, 0.42f)
		};
		_gameOverPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_uiRoot.AddChild(_gameOverPanel);

		VBoxContainer content = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		content.SetAnchorsPreset(Control.LayoutPreset.Center);
		content.OffsetLeft = -180f;
		content.OffsetTop = -70f;
		content.OffsetRight = 180f;
		content.OffsetBottom = 70f;
		content.AddThemeConstantOverride("separation", 12);
		_gameOverPanel.AddChild(content);

		_gameOverLabel = new Label
		{
			Text = "AWAY TEAM LOST",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_gameOverLabel.AddThemeFontSizeOverride("font_size", 28);
		content.AddChild(_gameOverLabel);

		_gameOverReturnButton = new Button
		{
			Text = "RETURN TO MAIN MENU",
			CustomMinimumSize = new Vector2(240f, 46f)
		};
		content.AddChild(_gameOverReturnButton);
	}

	private PanelContainer BuildCombatInfoPanel(Vector2 position, out TextureRect iconRect, out Label headerLabel, out Label infoLabel, Vector2? sizeOverride = null, Vector2? iconSizeOverride = null)
	{
		PanelContainer panel = new PanelContainer
		{
			Position = position,
			Size = sizeOverride ?? new Vector2(392f, 262f)
		};
		panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 8);
		content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		margin.AddChild(content);

		headerLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		headerLabel.AddThemeFontSizeOverride("font_size", 17);
		content.AddChild(headerLabel);

		iconRect = new TextureRect
		{
			CustomMinimumSize = iconSizeOverride ?? new Vector2(120f, 88f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		content.AddChild(iconRect);

		infoLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.Off,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		infoLabel.AddThemeFontSizeOverride("font_size", 15);
		content.AddChild(infoLabel);
		return panel;
	}

	private void UpdateCombatInfoPanel(MissionCombatantSummary summary, PanelContainer panel, TextureRect iconRect, Label headerLabel, Label infoLabel, string emptyTitle)
	{
		if (panel == null || iconRect == null || headerLabel == null || infoLabel == null)
		{
			return;
		}

		if (summary == null)
		{
			panel.Visible = false;
			return;
		}

		panel.Visible = true;
		iconRect.Texture = summary.Icon;
		headerLabel.Text = $"== {summary.DisplayName.ToUpperInvariant()} ==";
		infoLabel.Text = $"{emptyTitle}: {summary.DisplayName}\nWEAPON: {summary.WeaponName}\nSHIELD: {summary.ShieldName}\nHP: {summary.CurrentHP}/{summary.MaxHP}\nSHIELDS: {summary.CurrentShields}/{summary.MaxShields}\nAP: {summary.CurrentAP}/{summary.MaxAP}\nRANGE: {summary.AttackRange} | DMG: {summary.AttackMinDamage}-{summary.AttackMaxDamage}";
	}

	private void UpdateExplorationSelectionPanelWidth(int visibleCardCount)
	{
		if (_explorationSelectionPanel == null)
		{
			return;
		}

		float targetWidth = visibleCardCount > 1
			? ExplorationSelectionDoubleWidth
			: ExplorationSelectionSingleWidth;
		_explorationSelectionPanel.OffsetRight = _explorationSelectionPanel.OffsetLeft + targetWidth;
	}

	private static StyleBoxFlat CreateCombatSquareStyle(Color backgroundColor, Color borderColor)
	{
		return new StyleBoxFlat
		{
			BgColor = backgroundColor,
			BorderColor = borderColor,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8
		};
	}

	private static StyleBoxEmpty CreateTransparentPanelStyle()
	{
		return new StyleBoxEmpty();
	}

	private void ApplyBattlemapLabelStyling()
	{
		if (TitleLabel != null)
		{
			TitleLabel.AddThemeFontSizeOverride("font_size", 28);
			TitleLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 1f, 0.98f));
			TitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		}

		if (ObjectiveLabel != null)
		{
			ObjectiveLabel.AddThemeFontSizeOverride("font_size", 16);
			ObjectiveLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 0.98f, 0.96f));
			ObjectiveLabel.HorizontalAlignment = HorizontalAlignment.Center;
		}
	}

	private void ConfirmMissionSavePrompt()
	{
		string saveName = _saveNameLineEdit?.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(saveName))
		{
			if (_savePromptStatusLabel != null)
			{
				_savePromptStatusLabel.Text = "Enter a name before saving.";
			}
			_saveNameLineEdit?.GrabFocus();
			return;
		}

		EmitSignal(SignalName.MissionSaveConfirmed, saveName);
		HideMissionSavePrompt();
	}

	public void HideMissionSavePrompt()
	{
		if (_savePromptPanel != null)
		{
			_savePromptPanel.Visible = false;
		}
	}

	private void ScrollActionLogToBottom()
	{
		if (_actionLogText == null)
		{
			return;
		}

		int lastLine = Mathf.Max(0, _actionLogText.GetLineCount() - 1);
		_actionLogText.ScrollToLine(lastLine);
	}

	private static StyleBoxFlat CreateActionLogStyle()
	{
		return CreateHudPanelStyle(0.86f, true);
	}

	private static StyleBoxFlat CreateLogPanelStyle()
	{
		return CreateHudPanelStyle(0.78f, false);
	}

	private static StyleBoxFlat CreateBattlemapActionLogStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0f, 0f, 0f, 0.36f),
			BorderWidthLeft = 0,
			BorderWidthTop = 0,
			BorderWidthRight = 0,
			BorderWidthBottom = 0,
			CornerRadiusTopLeft = 0,
			CornerRadiusTopRight = 0,
			CornerRadiusBottomRight = 0,
			CornerRadiusBottomLeft = 0
		};
	}

	private static StyleBoxFlat CreateHudPanelStyle(float alphaOverride = -1f, bool useAccentBorder = false)
	{
		Color background = HudPanelBackground;
		if (alphaOverride >= 0f)
		{
			background.A = alphaOverride;
		}

		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = useAccentBorder ? HudAccentBorder : HudPanelBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		};
	}
}
