using Godot;
using System;
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
	public string InventoryText { get; init; } = string.Empty;
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

public sealed class MissionInteractionMenuOption
{
	public string ActionId { get; init; } = string.Empty;
	public string Label { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public bool Disabled { get; init; }
}

public sealed class MissionCombatActionOption
{
	public string ActionId { get; init; } = string.Empty;
	public string Label { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public bool Disabled { get; init; }
	public bool Selected { get; init; }
}

public sealed class MissionCombatWeaponOption
{
	public string WeaponId { get; init; } = string.Empty;
	public string Label { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public bool Disabled { get; init; }
	public bool Equipped { get; init; }
}

public sealed class MissionExplorationOfficerOption
{
	public string OfficerId { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	public string Subtitle { get; init; } = string.Empty;
	public Texture2D Icon { get; init; }
	public bool Selected { get; init; }
	public bool CanInspectInventory { get; init; }
}

public sealed class MissionInventoryEntry
{
	public string EntryId { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string Detail { get; init; } = string.Empty;
	public string QuantityText { get; init; } = string.Empty;
	public bool Highlighted { get; init; }
	public bool CanActivate { get; init; }
	public string ActionText { get; init; } = string.Empty;
}

public sealed class MissionOfficerInventoryPanelData
{
	public string OfficerId { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	public string ShipName { get; init; } = string.Empty;
	public string Specialty { get; init; } = string.Empty;
	public Texture2D Portrait { get; init; }
	public string SummaryText { get; init; } = string.Empty;
	public string VitalStatsText { get; init; } = string.Empty;
	public string FooterText { get; init; } = string.Empty;
	public IReadOnlyList<MissionInventoryEntry> LoadoutEntries { get; init; } = Array.Empty<MissionInventoryEntry>();
	public IReadOnlyList<MissionInventoryEntry> WeaponEntries { get; init; } = Array.Empty<MissionInventoryEntry>();
	public IReadOnlyList<MissionInventoryEntry> ShieldEntries { get; init; } = Array.Empty<MissionInventoryEntry>();
	public IReadOnlyList<MissionInventoryEntry> ItemEntries { get; init; } = Array.Empty<MissionInventoryEntry>();
}

public sealed class MissionInfoPanelRefs
{
	public PanelContainer Panel { get; init; }
	public TextureRect Icon { get; init; }
	public Label Header { get; init; }
	public Label Info { get; init; }
	public Label Inventory { get; init; }
}

public partial class MissionUI : CanvasLayer
{
	private static readonly Color HudPanelBackground = new Color(0.04f, 0.05f, 0.08f, 0.74f);
	private static readonly Color HudPanelBorder = new Color(0.22f, 0.28f, 0.36f, 0.62f);
	private static readonly Color HudAccentBorder = new Color(0.24f, 0.64f, 0.78f, 0.82f);
	private const string ActionLogReadyHeader = "[color=gray]--- ACTION LOG READY ---[/color]";

	private const float ExplorationSelectionMinimumWidth = 564f;
	private const float ExplorationSelectionMaximumWidth = 1812f;
	private const float ExplorationCardSpacing = 12f;
	private const float ExplorationCardWidth = 540f;
	private const float ExplorationCardHeight = 340f;
	private const string ExtractionOutcomeMetaKey = "mission_ui_extraction_outcome_id";
	private const string InteractionActionMetaKey = "mission_ui_interaction_action_id";

	[Signal]
	public delegate void ExtractionOutcomeChosenEventHandler(string outcomeId);

	[Signal]
	public delegate void CombatEndTurnPressedEventHandler();

	[Signal]
	public delegate void CombatActionChosenEventHandler(string actionId);

	[Signal]
	public delegate void CombatWeaponSwapRequestedEventHandler(string weaponId);

	[Signal]
	public delegate void ExplorationControlModeChosenEventHandler(string modeId);

	[Signal]
	public delegate void ExplorationOfficerChosenEventHandler(string officerId);

	[Signal]
	public delegate void ExplorationOfficerInventoryRequestedEventHandler(string officerId);

	[Signal]
	public delegate void ExplorationUnitFocusRequestedEventHandler(string unitId);

	[Signal]
	public delegate void OfficerInventoryWeaponEquipRequestedEventHandler(string officerId, string weaponId);

	[Signal]
	public delegate void OfficerInventoryShieldEquipRequestedEventHandler(string officerId, string shieldId);

	[Signal]
	public delegate void MissionSaveConfirmedEventHandler(string saveName);

	[Signal]
	public delegate void StoryEventConfirmedEventHandler();

	[Signal]
	public delegate void ConfirmationAcceptedEventHandler();

	[Signal]
	public delegate void ConfirmationCancelledEventHandler();

	[Signal]
	public delegate void InteractionMenuOptionChosenEventHandler(string actionId);

	public Label TitleLabel { get; private set; }
	public Label ObjectiveLabel { get; private set; }
	public Label SelectedOfficerLabel { get; private set; }
	public Label PromptLabel { get; private set; }
	public bool IsExtractionPromptVisible => _extractionPromptPanel?.Visible ?? false;
	public bool IsStoryEventVisible => _storyEventPanel?.Visible ?? false;
	public bool IsConfirmationVisible => _confirmationPromptPanel?.Visible ?? false;
	public bool IsMissionSavePromptVisible => _savePromptPanel?.Visible ?? false;
	public bool IsInteractionMenuVisible => _interactionMenuPanel?.Visible ?? false;
	public bool IsOfficerInventoryVisible => _officerInventoryOverlay?.Visible ?? false;

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
	private PanelContainer _combatActionPanel;
	private Label _combatActionTitleLabel;
	private Label _combatActionStatusLabel;
	private HBoxContainer _combatActionButtonRow;
	private VBoxContainer _combatWeaponButtonStack;
	private PanelContainer _explorationControlPanel;
	private Label _explorationControlStatusLabel;
	private Button _explorationSingleModeButton;
	private Button _explorationPartyModeButton;
	private VBoxContainer _explorationOfficerButtonStack;
	private PanelContainer _explorationSelectionPanel;
	private ScrollContainer _explorationSelectionScroll;
	private HBoxContainer _explorationSelectionRow;
	private readonly List<MissionInfoPanelRefs> _explorationInfoCards = new List<MissionInfoPanelRefs>();
	private PanelContainer _combatLogPanel;
	private RichTextLabel _combatLogText;
	private readonly List<string> _combatLogEntries = new List<string>();
	private PanelContainer _actionLogPanel;
	private RichTextLabel _actionLogText;
	private readonly List<string> _actionLogEntries = new List<string>();
	private PanelContainer _hoverSummaryPanel;
	private Label _hoverSummaryLabel;
	private PanelContainer _interactionMenuPanel;
	private Label _interactionMenuTitleLabel;
	private Label _interactionMenuBodyLabel;
	private VBoxContainer _interactionMenuButtonStack;
	private PanelContainer _storyEventPanel;
	private Label _storyEventTitleLabel;
	private TextureRect _storyEventImage;
	private RichTextLabel _storyEventDescription;
	private Button _storyEventConfirmButton;
	private Texture2D _storyEventRuntimeTexture;
	private PanelContainer _confirmationPromptPanel;
	private Label _confirmationPromptTitleLabel;
	private Label _confirmationPromptBodyLabel;
	private Button _confirmationPromptConfirmButton;
	private Button _confirmationPromptCancelButton;
	private PanelContainer _savePromptPanel;
	private LineEdit _saveNameLineEdit;
	private Label _savePromptStatusLabel;
	private Button _savePromptCancelButton;
	private Button _savePromptConfirmButton;
	private ColorRect _officerInventoryOverlay;
	private PanelContainer _officerInventoryPanel;
	private TextureRect _officerInventoryPortrait;
	private Label _officerInventoryNameLabel;
	private Label _officerInventoryRoleLabel;
	private Label _officerInventoryStatsLabel;
	private Label _officerInventorySummaryLabel;
	private Label _officerInventoryFooterLabel;
	private Button _officerInventoryCloseButton;
	private VBoxContainer _officerInventoryLoadoutStack;
	private VBoxContainer _officerInventoryWeaponStack;
	private VBoxContainer _officerInventoryShieldStack;
	private VBoxContainer _officerInventoryItemStack;
	private string _activeOfficerInventoryId = string.Empty;
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
		BuildCombatActionPanel();
		BuildExplorationControlPanel();
		BuildExplorationSelectionHud();
		BuildActionLog();
		BuildHoverSummary();
		BuildInteractionMenu();
		BuildStoryEventPanel();
		BuildConfirmationPrompt();
		BuildMissionSavePrompt();
		BuildOfficerInventoryPanel();
		BuildGameOverPanel();
		ApplyBattlemapLabelStyling();
	}

	public override void _ExitTree()
	{
		ClearExtractionPromptButtons();
		ClearExplorationOfficerButtons();
		ClearInteractionMenuButtons();
		HideStoryEvent();

		if (_combatEndTurnButton != null)
		{
			_combatEndTurnButton.Pressed -= OnCombatEndTurnButtonPressed;
		}

		ClearCombatActionButtons();
		ClearCombatWeaponButtons();

		if (_storyEventConfirmButton != null)
		{
			_storyEventConfirmButton.Pressed -= OnStoryEventConfirmButtonPressed;
		}

		if (_confirmationPromptCancelButton != null)
		{
			_confirmationPromptCancelButton.Pressed -= OnConfirmationCancelledButtonPressed;
		}

		if (_confirmationPromptConfirmButton != null)
		{
			_confirmationPromptConfirmButton.Pressed -= OnConfirmationAcceptedButtonPressed;
		}

		if (_saveNameLineEdit != null)
		{
			_saveNameLineEdit.TextSubmitted -= OnSaveNameSubmitted;
		}

		if (_savePromptCancelButton != null)
		{
			_savePromptCancelButton.Pressed -= HideMissionSavePrompt;
		}

		if (_savePromptConfirmButton != null)
		{
			_savePromptConfirmButton.Pressed -= ConfirmMissionSavePrompt;
		}

		if (_officerInventoryCloseButton != null)
		{
			_officerInventoryCloseButton.Pressed -= HideOfficerInventory;
		}
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

		SelectedOfficerLabel.Text = $"ACTIVE UNIT: {officerName}\nGROUP: {shipName}\nROLE: {specialty}";
	}

	public void ShowExtractionPrompt(string title, string body, IReadOnlyList<MissionExtractionOption> options)
	{
		if (_extractionPromptPanel == null || _extractionPromptTitleLabel == null || _extractionPromptBodyLabel == null || _extractionPromptButtonStack == null)
		{
			return;
		}

		_extractionPromptTitleLabel.Text = title;
		_extractionPromptBodyLabel.Text = body;

		ClearExtractionPromptButtons();

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

		if (_combatActionPanel != null && !visible)
		{
			_combatActionPanel.Visible = false;
		}

		if (_explorationControlPanel != null && visible)
		{
			_explorationControlPanel.Visible = false;
		}

		if (_combatLogPanel != null)
		{
			_combatLogPanel.Visible = false;
		}

		if (_explorationSelectionPanel != null && visible)
		{
			_explorationSelectionPanel.Visible = false;
		}

		if (visible)
		{
			HideOfficerInventory();
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
		UpdateCombatInfoPanel(summary, _playerCombatInfoPanel, _playerCombatIcon, _playerCombatHeaderLabel, _playerCombatInfoLabel, null, "UNIT");
	}

	public void SetEnemyCombatInfo(MissionCombatantSummary summary)
	{
		UpdateCombatInfoPanel(summary, _enemyCombatInfoPanel, _enemyCombatIcon, _enemyCombatHeaderLabel, _enemyCombatInfoLabel, null, "ENEMY");
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
		EnsureExplorationSelectionCards(summaries.Count);
		UpdateExplorationSelectionPanelWidth(summaries.Count);
		for (int i = 0; i < _explorationInfoCards.Count; i++)
		{
			MissionInfoPanelRefs card = _explorationInfoCards[i];
			UpdateCombatInfoPanel(
				i < summaries.Count ? summaries[i] : null,
				card.Panel,
				card.Icon,
				card.Header,
				card.Info,
				card.Inventory,
				"UNIT");
		}
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

	public void SetCombatActionPanel(
		string title,
		string status,
		IReadOnlyList<MissionCombatActionOption> actions,
		IReadOnlyList<MissionCombatWeaponOption> weapons,
		bool visible)
	{
		if (_combatActionPanel == null || _combatActionTitleLabel == null || _combatActionStatusLabel == null || _combatActionButtonRow == null || _combatWeaponButtonStack == null)
		{
			return;
		}

		_combatActionPanel.Visible = visible;
		if (!visible)
		{
			ClearCombatActionButtons();
			ClearCombatWeaponButtons();
			return;
		}

		_combatActionTitleLabel.Text = string.IsNullOrWhiteSpace(title) ? "TACTICAL OPTIONS" : title.ToUpperInvariant();
		_combatActionStatusLabel.Text = status ?? string.Empty;

		ClearCombatActionButtons();
		foreach (MissionCombatActionOption option in actions ?? new List<MissionCombatActionOption>())
		{
			if (option == null || string.IsNullOrWhiteSpace(option.ActionId))
			{
				continue;
			}

			string prefix = option.Selected ? "> " : string.Empty;
			Button button = new Button
			{
				Text = $"{prefix}{(string.IsNullOrWhiteSpace(option.Label) ? option.ActionId : option.Label).ToUpperInvariant()}",
				TooltipText = option.Description,
				Disabled = option.Disabled,
				CustomMinimumSize = new Vector2(0f, 42f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			string actionId = option.ActionId;
			button.Pressed += () => EmitSignal(SignalName.CombatActionChosen, actionId);
			_combatActionButtonRow.AddChild(button);
		}

		ClearCombatWeaponButtons();
		foreach (MissionCombatWeaponOption option in weapons ?? new List<MissionCombatWeaponOption>())
		{
			if (option == null || string.IsNullOrWhiteSpace(option.WeaponId))
			{
				continue;
			}

			string suffix = option.Equipped ? " [EQUIPPED]" : string.Empty;
			Button button = new Button
			{
				Text = $"{(string.IsNullOrWhiteSpace(option.Label) ? option.WeaponId : option.Label).ToUpperInvariant()}{suffix}",
				TooltipText = option.Description,
				Disabled = option.Disabled,
				CustomMinimumSize = new Vector2(0f, 40f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			string weaponId = option.WeaponId;
			button.Pressed += () => EmitSignal(SignalName.CombatWeaponSwapRequested, weaponId);
			_combatWeaponButtonStack.AddChild(button);
		}
	}

	public void SetExplorationControlPanel(
		string status,
		bool partyModeEnabled,
		IReadOnlyList<MissionExplorationOfficerOption> officers,
		bool visible)
	{
		if (_explorationControlPanel == null || _explorationControlStatusLabel == null || _explorationSingleModeButton == null || _explorationPartyModeButton == null || _explorationOfficerButtonStack == null)
		{
			return;
		}

		_explorationControlPanel.Visible = visible;
		if (!visible)
		{
			ClearExplorationOfficerButtons();
			return;
		}

		_explorationControlStatusLabel.Text = status ?? string.Empty;
		ApplyExplorationModeButtonState(_explorationSingleModeButton, !partyModeEnabled);
		ApplyExplorationModeButtonState(_explorationPartyModeButton, partyModeEnabled);

		ClearExplorationOfficerButtons();
		foreach (MissionExplorationOfficerOption officer in officers ?? new List<MissionExplorationOfficerOption>())
		{
			if (officer == null || string.IsNullOrWhiteSpace(officer.OfficerId))
			{
				continue;
			}

			Button button = new Button
			{
				Text = string.IsNullOrWhiteSpace(officer.Subtitle)
					? officer.DisplayName
					: $"{officer.DisplayName}\n{officer.Subtitle}",
				TooltipText = officer.CanInspectInventory
					? $"{officer.DisplayName}\nRight-click to inspect inventory."
					: officer.DisplayName,
				Icon = officer.Icon,
				ExpandIcon = true,
				IconAlignment = HorizontalAlignment.Left,
				VerticalIconAlignment = VerticalAlignment.Center,
				Alignment = HorizontalAlignment.Left,
				CustomMinimumSize = new Vector2(0f, 74f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			button.AddThemeFontSizeOverride("font_size", 13);
			ApplyExplorationOfficerButtonState(button, officer.Selected);
			string officerId = officer.OfficerId;
			bool canInspectInventory = officer.CanInspectInventory;
			button.Pressed += () => EmitSignal(SignalName.ExplorationOfficerChosen, officerId);
			button.GuiInput += @event =>
			{
				if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
				{
					return;
				}

				if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.DoubleClick)
				{
					EmitSignal(SignalName.ExplorationUnitFocusRequested, officerId);
					button.AcceptEvent();
					return;
				}

				if (canInspectInventory && mouseButton.ButtonIndex == MouseButton.Right)
				{
					EmitSignal(SignalName.ExplorationOfficerInventoryRequested, officerId);
					button.AcceptEvent();
				}
			};
			_explorationOfficerButtonStack.AddChild(button);
		}
	}

	public void ShowOfficerInventory(MissionOfficerInventoryPanelData data)
	{
		if (_officerInventoryOverlay == null
			|| _officerInventoryPortrait == null
			|| _officerInventoryNameLabel == null
			|| _officerInventoryRoleLabel == null
			|| _officerInventoryStatsLabel == null
			|| _officerInventorySummaryLabel == null
			|| _officerInventoryFooterLabel == null
			|| data == null)
		{
			return;
		}

		_officerInventoryPortrait.Texture = data.Portrait;
		_activeOfficerInventoryId = data.OfficerId ?? string.Empty;
		_officerInventoryNameLabel.Text = string.IsNullOrWhiteSpace(data.DisplayName)
			? "AWAY TEAM OPERATIVE"
			: data.DisplayName.ToUpperInvariant();
		_officerInventoryRoleLabel.Text = $"{data.Specialty.ToUpperInvariant()}  |  {data.ShipName.ToUpperInvariant()}";
		_officerInventoryStatsLabel.Text = data.VitalStatsText ?? string.Empty;
		_officerInventorySummaryLabel.Text = data.SummaryText ?? string.Empty;
		_officerInventoryFooterLabel.Text = data.FooterText ?? "Right-click another portrait to inspect a different operative.";

		PopulateInventoryEntryStack(_officerInventoryLoadoutStack, data.LoadoutEntries, "No active loadout data.");
		PopulateInteractiveInventoryEntryStack(
			_officerInventoryWeaponStack,
			data.WeaponEntries,
			"No owned weapons.",
			weaponId => EmitSignal(SignalName.OfficerInventoryWeaponEquipRequested, _activeOfficerInventoryId, weaponId));
		PopulateInteractiveInventoryEntryStack(
			_officerInventoryShieldStack,
			data.ShieldEntries,
			"No owned shields.",
			shieldId => EmitSignal(SignalName.OfficerInventoryShieldEquipRequested, _activeOfficerInventoryId, shieldId));
		PopulateInventoryItemStack(data.ItemEntries);
		_officerInventoryOverlay.Visible = true;
	}

	public void HideOfficerInventory()
	{
		if (_officerInventoryOverlay != null)
		{
			_officerInventoryOverlay.Visible = false;
		}

		_activeOfficerInventoryId = string.Empty;
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

	public void ShowInteractionMenu(string title, string body, Vector2 screenPosition, IReadOnlyList<MissionInteractionMenuOption> options)
	{
		if (_interactionMenuPanel == null || _interactionMenuTitleLabel == null || _interactionMenuBodyLabel == null || _interactionMenuButtonStack == null)
		{
			return;
		}

		ClearInteractionMenuButtons();

		_interactionMenuTitleLabel.Text = string.IsNullOrWhiteSpace(title) ? "INTERACT" : title.ToUpperInvariant();
		_interactionMenuBodyLabel.Text = body ?? string.Empty;

		foreach (MissionInteractionMenuOption option in options ?? new List<MissionInteractionMenuOption>())
		{
			if (option == null || string.IsNullOrWhiteSpace(option.ActionId))
			{
				continue;
			}

			Button button = new Button
			{
				Text = string.IsNullOrWhiteSpace(option.Label) ? option.ActionId.ToUpperInvariant() : option.Label.ToUpperInvariant(),
				TooltipText = option.Description,
				Disabled = option.Disabled,
				CustomMinimumSize = new Vector2(0f, 42f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			string actionId = option.ActionId;
			button.Pressed += () => EmitSignal(SignalName.InteractionMenuOptionChosen, actionId);
			_interactionMenuButtonStack.AddChild(button);
		}

		Vector2 minimumSize = _interactionMenuPanel.GetCombinedMinimumSize();
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 desiredPosition = screenPosition - new Vector2(8f, 8f);
		float maxX = Mathf.Max(12f, viewportSize.X - minimumSize.X - 12f);
		float maxY = Mathf.Max(12f, viewportSize.Y - minimumSize.Y - 12f);
		_interactionMenuPanel.Position = new Vector2(
			Mathf.Clamp(desiredPosition.X, 12f, maxX),
			Mathf.Clamp(desiredPosition.Y, 12f, maxY));
		_interactionMenuPanel.Visible = _interactionMenuButtonStack.GetChildCount() > 0;
	}

	public void HideInteractionMenu()
	{
		if (_interactionMenuPanel != null)
		{
			_interactionMenuPanel.Visible = false;
		}
	}

	public bool IsMouseOverInteractionMenu()
	{
		return _interactionMenuPanel != null
			&& _interactionMenuPanel.Visible
			&& _interactionMenuPanel.GetGlobalRect().HasPoint(GetViewport().GetMousePosition());
	}

	public void ShowStoryEvent(string title, string description, string imagePath, string confirmButtonText)
	{
		if (_storyEventPanel == null || _storyEventDescription == null || _storyEventConfirmButton == null || _storyEventImage == null)
		{
			return;
		}

		if (_storyEventTitleLabel != null)
		{
			_storyEventTitleLabel.Text = string.IsNullOrWhiteSpace(title) ? "MISSION EVENT" : title.ToUpperInvariant();
		}

		_storyEventDescription.Text = string.IsNullOrWhiteSpace(description)
			? string.Empty
			: description;
		_storyEventRuntimeTexture = LoadTextureWithoutCache(imagePath);
		_storyEventImage.Texture = _storyEventRuntimeTexture;
		_storyEventConfirmButton.Text = string.IsNullOrWhiteSpace(confirmButtonText) ? "CONTINUE" : confirmButtonText;
		_storyEventPanel.Visible = true;
	}

	public void HideStoryEvent()
	{
		if (_storyEventPanel != null)
		{
			_storyEventPanel.Visible = false;
		}

		if (_storyEventImage != null)
		{
			_storyEventImage.Texture = null;
		}

		_storyEventRuntimeTexture = null;
	}

	private static Texture2D LoadTextureWithoutCache(string resourcePath)
	{
		if (string.IsNullOrWhiteSpace(resourcePath))
		{
			return null;
		}

		return ResourceLoader.Exists(resourcePath)
			? ResourceLoader.Load<Texture2D>(resourcePath, string.Empty, ResourceLoader.CacheMode.IgnoreDeep)
			: null;
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
		_combatEndTurnButton.Pressed += OnCombatEndTurnButtonPressed;
		_initiativeRoot.AddChild(_combatEndTurnButton);

		_playerCombatInfoPanel = BuildCombatInfoPanel(new Vector2(20f, 760f), out _playerCombatIcon, out _playerCombatHeaderLabel, out _playerCombatInfoLabel, out _);
		_enemyCombatInfoPanel = BuildCombatInfoPanel(new Vector2(1500f, 760f), out _enemyCombatIcon, out _enemyCombatHeaderLabel, out _enemyCombatInfoLabel, out _);
		_playerCombatInfoPanel.Visible = false;
		_enemyCombatInfoPanel.Visible = false;
		_uiRoot.AddChild(_playerCombatInfoPanel);
		_uiRoot.AddChild(_enemyCombatInfoPanel);
	}

	private void BuildCombatActionPanel()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_combatActionPanel = new PanelContainer
		{
			Visible = false
		};
		_combatActionPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_combatActionPanel.OffsetLeft = -396f;
		_combatActionPanel.OffsetTop = 290f;
		_combatActionPanel.OffsetRight = -46f;
		_combatActionPanel.OffsetBottom = 610f;
		_combatActionPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());
		_uiRoot.AddChild(_combatActionPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		_combatActionPanel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);

		_combatActionTitleLabel = new Label
		{
			Text = "TACTICAL OPTIONS"
		};
		_combatActionTitleLabel.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(_combatActionTitleLabel);

		_combatActionStatusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_combatActionStatusLabel);

		_combatActionButtonRow = new HBoxContainer();
		_combatActionButtonRow.AddThemeConstantOverride("separation", 8);
		content.AddChild(_combatActionButtonRow);

		Label inventoryLabel = new Label
		{
			Text = "WEAPONS"
		};
		inventoryLabel.AddThemeFontSizeOverride("font_size", 14);
		content.AddChild(inventoryLabel);

		_combatWeaponButtonStack = new VBoxContainer();
		_combatWeaponButtonStack.AddThemeConstantOverride("separation", 8);
		content.AddChild(_combatWeaponButtonStack);
	}

	private void BuildExplorationControlPanel()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_explorationControlPanel = new PanelContainer
		{
			Visible = false
		};
		_explorationControlPanel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_explorationControlPanel.OffsetLeft = 20f;
		_explorationControlPanel.OffsetTop = 20f;
		_explorationControlPanel.OffsetRight = 280f;
		_explorationControlPanel.OffsetBottom = 560f;
		_explorationControlPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle(0.84f, true));
		_uiRoot.AddChild(_explorationControlPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		_explorationControlPanel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);

		Label titleLabel = new Label
		{
			Text = "EXPLORATION CONTROL"
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 18);
		content.AddChild(titleLabel);

		_explorationControlStatusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_explorationControlStatusLabel);

		HBoxContainer modeRow = new HBoxContainer();
		modeRow.AddThemeConstantOverride("separation", 8);
		content.AddChild(modeRow);

		_explorationSingleModeButton = new Button
		{
			Text = "SINGLE",
			CustomMinimumSize = new Vector2(0f, 38f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_explorationSingleModeButton.Pressed += () => EmitSignal(SignalName.ExplorationControlModeChosen, "single");
		modeRow.AddChild(_explorationSingleModeButton);

		_explorationPartyModeButton = new Button
		{
			Text = "PARTY",
			CustomMinimumSize = new Vector2(0f, 38f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_explorationPartyModeButton.Pressed += () => EmitSignal(SignalName.ExplorationControlModeChosen, "party");
		modeRow.AddChild(_explorationPartyModeButton);

		_explorationOfficerButtonStack = new VBoxContainer();
		_explorationOfficerButtonStack.AddThemeConstantOverride("separation", 8);
		content.AddChild(_explorationOfficerButtonStack);
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
		_explorationSelectionPanel.OffsetTop = -430f;
		_explorationSelectionPanel.OffsetRight = 20f + ExplorationSelectionMaximumWidth;
		_explorationSelectionPanel.OffsetBottom = -58f;
		_explorationSelectionPanel.AddThemeStyleboxOverride("panel", CreateTransparentPanelStyle());
		_uiRoot.AddChild(_explorationSelectionPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		_explorationSelectionPanel.AddChild(margin);

		_explorationSelectionScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
			VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		margin.AddChild(_explorationSelectionScroll);

		_explorationSelectionRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin
		};
		_explorationSelectionRow.AddThemeConstantOverride("separation", (int)ExplorationCardSpacing);
		_explorationSelectionScroll.AddChild(_explorationSelectionRow);
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

	private void BuildInteractionMenu()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_interactionMenuPanel = new PanelContainer
		{
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop,
			CustomMinimumSize = new Vector2(312f, 0f)
		};
		_interactionMenuPanel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle(0.92f, true));
		_uiRoot.AddChild(_interactionMenuPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		_interactionMenuPanel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);

		_interactionMenuTitleLabel = new Label
		{
			Text = "INTERACT"
		};
		_interactionMenuTitleLabel.AddThemeFontSizeOverride("font_size", 18);
		_interactionMenuTitleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.96f, 1f));
		content.AddChild(_interactionMenuTitleLabel);

		_interactionMenuBodyLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_interactionMenuBodyLabel);

		_interactionMenuButtonStack = new VBoxContainer();
		_interactionMenuButtonStack.AddThemeConstantOverride("separation", 8);
		content.AddChild(_interactionMenuButtonStack);
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

		_storyEventTitleLabel = new Label
		{
			Name = "TitleLabel",
			Text = "MISSION EVENT",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_storyEventTitleLabel.AddThemeFontSizeOverride("font_size", 26);
		_storyEventTitleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.96f, 1f));
		content.AddChild(_storyEventTitleLabel);

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
		_storyEventConfirmButton.Pressed += OnStoryEventConfirmButtonPressed;
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
		_confirmationPromptCancelButton.Pressed += OnConfirmationCancelledButtonPressed;
		buttons.AddChild(_confirmationPromptCancelButton);

		_confirmationPromptConfirmButton = new Button
		{
			Text = "CONFIRM",
			CustomMinimumSize = new Vector2(0f, 46f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_confirmationPromptConfirmButton.Pressed += OnConfirmationAcceptedButtonPressed;
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
		_saveNameLineEdit.TextSubmitted += OnSaveNameSubmitted;
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

		_savePromptCancelButton = new Button
		{
			Text = "CANCEL",
			CustomMinimumSize = new Vector2(180f, 42f)
		};
		_savePromptCancelButton.Pressed += HideMissionSavePrompt;
		buttons.AddChild(_savePromptCancelButton);

		_savePromptConfirmButton = new Button
		{
			Text = "SAVE",
			CustomMinimumSize = new Vector2(180f, 42f)
		};
		_savePromptConfirmButton.Pressed += ConfirmMissionSavePrompt;
		buttons.AddChild(_savePromptConfirmButton);
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

	private void BuildOfficerInventoryPanel()
	{
		if (_uiRoot == null)
		{
			return;
		}

		_officerInventoryOverlay = new ColorRect
		{
			Visible = false,
			Color = new Color(0.04f, 0.02f, 0.01f, 0.80f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_officerInventoryOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_officerInventoryOverlay.GuiInput += OnOfficerInventoryOverlayGuiInput;
		_uiRoot.AddChild(_officerInventoryOverlay);

		_officerInventoryPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(1180f, 760f)
		};
		_officerInventoryPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_officerInventoryPanel.OffsetLeft = -590f;
		_officerInventoryPanel.OffsetTop = -380f;
		_officerInventoryPanel.OffsetRight = 590f;
		_officerInventoryPanel.OffsetBottom = 380f;
		_officerInventoryPanel.AddThemeStyleboxOverride("panel", CreateInventoryWindowStyle());
		_officerInventoryOverlay.AddChild(_officerInventoryPanel);

		MarginContainer outerMargin = new MarginContainer();
		outerMargin.AddThemeConstantOverride("margin_left", 20);
		outerMargin.AddThemeConstantOverride("margin_top", 20);
		outerMargin.AddThemeConstantOverride("margin_right", 20);
		outerMargin.AddThemeConstantOverride("margin_bottom", 20);
		_officerInventoryPanel.AddChild(outerMargin);

		VBoxContainer layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 16);
		outerMargin.AddChild(layout);

		HBoxContainer headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 14);
		layout.AddChild(headerRow);

		VBoxContainer rail = new VBoxContainer();
		rail.CustomMinimumSize = new Vector2(76f, 0f);
		rail.AddThemeConstantOverride("separation", 10);
		headerRow.AddChild(rail);
		rail.AddChild(BuildInventoryRailTag("LOADOUT", true));
		rail.AddChild(BuildInventoryRailTag("STATUS", false));
		rail.AddChild(BuildInventoryRailTag("PACK", false));

		VBoxContainer mainColumn = new VBoxContainer();
		mainColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		mainColumn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		mainColumn.AddThemeConstantOverride("separation", 14);
		headerRow.AddChild(mainColumn);

		HBoxContainer titleRow = new HBoxContainer();
		titleRow.Alignment = BoxContainer.AlignmentMode.Center;
		titleRow.AddThemeConstantOverride("separation", 12);
		mainColumn.AddChild(titleRow);

		Control leftSpacer = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		titleRow.AddChild(leftSpacer);

		PanelContainer titlePlaque = new PanelContainer
		{
			CustomMinimumSize = new Vector2(460f, 74f)
		};
		titlePlaque.AddThemeStyleboxOverride("panel", CreateInventoryBannerStyle());
		titleRow.AddChild(titlePlaque);

		Label titleLabel = new Label
		{
			Text = "INVENTORY",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		titleLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		titleLabel.AddThemeFontSizeOverride("font_size", 34);
		titleLabel.AddThemeColorOverride("font_color", new Color(0.93f, 0.90f, 0.84f));
		titlePlaque.AddChild(titleLabel);

		Control rightSpacer = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		titleRow.AddChild(rightSpacer);

		_officerInventoryCloseButton = new Button
		{
			Text = "CLOSE",
			CustomMinimumSize = new Vector2(120f, 44f)
		};
		_officerInventoryCloseButton.AddThemeStyleboxOverride("normal", CreateInventoryButtonStyle());
		_officerInventoryCloseButton.AddThemeStyleboxOverride("hover", CreateInventoryButtonStyle(true));
		_officerInventoryCloseButton.AddThemeStyleboxOverride("pressed", CreateInventoryButtonStyle(true));
		_officerInventoryCloseButton.Pressed += HideOfficerInventory;
		titleRow.AddChild(_officerInventoryCloseButton);

		PanelContainer identityPanel = new PanelContainer();
		identityPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle(true));
		mainColumn.AddChild(identityPanel);

		MarginContainer identityMargin = new MarginContainer();
		identityMargin.AddThemeConstantOverride("margin_left", 18);
		identityMargin.AddThemeConstantOverride("margin_top", 14);
		identityMargin.AddThemeConstantOverride("margin_right", 18);
		identityMargin.AddThemeConstantOverride("margin_bottom", 14);
		identityPanel.AddChild(identityMargin);

		VBoxContainer identityContent = new VBoxContainer();
		identityContent.AddThemeConstantOverride("separation", 6);
		identityMargin.AddChild(identityContent);

		_officerInventoryNameLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_officerInventoryNameLabel.AddThemeFontSizeOverride("font_size", 30);
		identityContent.AddChild(_officerInventoryNameLabel);

		_officerInventoryRoleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_officerInventoryRoleLabel.AddThemeFontSizeOverride("font_size", 16);
		_officerInventoryRoleLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.78f, 0.62f));
		identityContent.AddChild(_officerInventoryRoleLabel);

		HBoxContainer bodyRow = new HBoxContainer();
		bodyRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		bodyRow.AddThemeConstantOverride("separation", 14);
		mainColumn.AddChild(bodyRow);

		PanelContainer portraitPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(270f, 0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		portraitPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		bodyRow.AddChild(portraitPanel);

		MarginContainer portraitMargin = new MarginContainer();
		portraitMargin.AddThemeConstantOverride("margin_left", 14);
		portraitMargin.AddThemeConstantOverride("margin_top", 14);
		portraitMargin.AddThemeConstantOverride("margin_right", 14);
		portraitMargin.AddThemeConstantOverride("margin_bottom", 14);
		portraitPanel.AddChild(portraitMargin);

		VBoxContainer portraitContent = new VBoxContainer();
		portraitContent.AddThemeConstantOverride("separation", 12);
		portraitMargin.AddChild(portraitContent);

		_officerInventoryPortrait = new TextureRect
		{
			CustomMinimumSize = new Vector2(0f, 250f),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
		};
		portraitContent.AddChild(_officerInventoryPortrait);

		_officerInventoryStatsLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_officerInventoryStatsLabel.AddThemeFontSizeOverride("font_size", 15);
		portraitContent.AddChild(_officerInventoryStatsLabel);

		VBoxContainer centerColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		centerColumn.AddThemeConstantOverride("separation", 14);
		bodyRow.AddChild(centerColumn);

		PanelContainer summaryPanel = new PanelContainer();
		summaryPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		centerColumn.AddChild(summaryPanel);

		MarginContainer summaryMargin = new MarginContainer();
		summaryMargin.AddThemeConstantOverride("margin_left", 14);
		summaryMargin.AddThemeConstantOverride("margin_top", 14);
		summaryMargin.AddThemeConstantOverride("margin_right", 14);
		summaryMargin.AddThemeConstantOverride("margin_bottom", 14);
		summaryPanel.AddChild(summaryMargin);

		VBoxContainer summaryContent = new VBoxContainer();
		summaryContent.AddThemeConstantOverride("separation", 10);
		summaryMargin.AddChild(summaryContent);
		summaryContent.AddChild(BuildInventorySectionHeader("OPERATIVE DOSSIER"));

		_officerInventorySummaryLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_officerInventorySummaryLabel.AddThemeFontSizeOverride("font_size", 15);
		summaryContent.AddChild(_officerInventorySummaryLabel);

		PanelContainer loadoutPanel = BuildInventorySectionCard("ACTIVE GEAR", out _officerInventoryLoadoutStack);
		loadoutPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		centerColumn.AddChild(loadoutPanel);

		VBoxContainer rightColumn = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(360f, 0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		rightColumn.AddThemeConstantOverride("separation", 14);
		bodyRow.AddChild(rightColumn);

		rightColumn.AddChild(BuildInventorySectionCard("WEAPONS LOCKER", out _officerInventoryWeaponStack));
		rightColumn.AddChild(BuildInventorySectionCard("SHIELD RACK", out _officerInventoryShieldStack));

		PanelContainer itemPanel = new PanelContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		itemPanel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());
		rightColumn.AddChild(itemPanel);

		MarginContainer itemMargin = new MarginContainer();
		itemMargin.AddThemeConstantOverride("margin_left", 14);
		itemMargin.AddThemeConstantOverride("margin_top", 14);
		itemMargin.AddThemeConstantOverride("margin_right", 14);
		itemMargin.AddThemeConstantOverride("margin_bottom", 14);
		itemPanel.AddChild(itemMargin);

		VBoxContainer itemContent = new VBoxContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		itemContent.AddThemeConstantOverride("separation", 10);
		itemMargin.AddChild(itemContent);
		itemContent.AddChild(BuildInventorySectionHeader("FIELD PACK"));

		_officerInventoryItemStack = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		_officerInventoryItemStack.AddThemeConstantOverride("separation", 8);
		itemContent.AddChild(_officerInventoryItemStack);

		_officerInventoryFooterLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_officerInventoryFooterLabel.AddThemeFontSizeOverride("font_size", 14);
		_officerInventoryFooterLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.76f, 0.65f));
		mainColumn.AddChild(_officerInventoryFooterLabel);
	}

	private PanelContainer BuildCombatInfoPanel(Vector2 position, out TextureRect iconRect, out Label headerLabel, out Label infoLabel, out Label inventoryLabel, Vector2? sizeOverride = null, Vector2? iconSizeOverride = null)
	{
		Vector2 panelSize = sizeOverride ?? new Vector2(392f, 262f);
		PanelContainer panel = new PanelContainer
		{
			Position = position,
			Size = panelSize,
			CustomMinimumSize = panelSize,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
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

		HBoxContainer bodyRow = new HBoxContainer();
		bodyRow.AddThemeConstantOverride("separation", 12);
		bodyRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		bodyRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		content.AddChild(bodyRow);

		iconRect = null;

		infoLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(150f, 0f)
		};
		infoLabel.AddThemeFontSizeOverride("font_size", 15);
		bodyRow.AddChild(infoLabel);

		inventoryLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			Visible = false,
			CustomMinimumSize = new Vector2(160f, 0f)
		};
		inventoryLabel.AddThemeFontSizeOverride("font_size", 14);
		bodyRow.AddChild(inventoryLabel);
		return panel;
	}

	private void UpdateCombatInfoPanel(MissionCombatantSummary summary, PanelContainer panel, TextureRect iconRect, Label headerLabel, Label infoLabel, Label inventoryLabel, string emptyTitle)
	{
		if (panel == null || headerLabel == null || infoLabel == null)
		{
			return;
		}

		if (summary == null)
		{
			panel.Visible = false;
			return;
		}

		panel.Visible = true;
		headerLabel.Text = $"== {summary.DisplayName.ToUpperInvariant()} ==";
		string subtitleLine = string.IsNullOrWhiteSpace(summary.Subtitle) ? string.Empty : $"{summary.Subtitle}\n";
		infoLabel.Text = $"{emptyTitle}: {summary.DisplayName}\n{subtitleLine}WEAPON: {summary.WeaponName}\nSHIELD: {summary.ShieldName}\nHP: {summary.CurrentHP}/{summary.MaxHP}\nSHIELDS: {summary.CurrentShields}/{summary.MaxShields}\nAP: {summary.CurrentAP}/{summary.MaxAP}\nRANGE: {summary.AttackRange} | DMG: {summary.AttackMinDamage}-{summary.AttackMaxDamage}";
		if (inventoryLabel != null)
		{
			inventoryLabel.Visible = !string.IsNullOrWhiteSpace(summary.InventoryText);
			inventoryLabel.Text = summary.InventoryText;
		}
	}

	private void UpdateExplorationSelectionPanelWidth(int visibleCardCount)
	{
		if (_explorationSelectionPanel == null)
		{
			return;
		}

		float contentWidth = (visibleCardCount * ExplorationCardWidth) + (Mathf.Max(0, visibleCardCount - 1) * ExplorationCardSpacing) + 24f;
		float targetWidth = Mathf.Clamp(contentWidth, ExplorationSelectionMinimumWidth, ExplorationSelectionMaximumWidth);
		_explorationSelectionPanel.OffsetRight = _explorationSelectionPanel.OffsetLeft + targetWidth;
	}

	private void EnsureExplorationSelectionCards(int requiredCount)
	{
		if (_explorationSelectionRow == null)
		{
			return;
		}

		while (_explorationInfoCards.Count < requiredCount)
		{
			PanelContainer panel = BuildCombatInfoPanel(
				Vector2.Zero,
				out TextureRect icon,
				out Label header,
				out Label info,
				out Label inventory,
				new Vector2(ExplorationCardWidth, ExplorationCardHeight),
				new Vector2(156f, 132f));
			panel.Position = Vector2.Zero;
			panel.Visible = false;
			panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle(0.88f, true));
			_explorationSelectionRow.AddChild(panel);
			_explorationInfoCards.Add(new MissionInfoPanelRefs
			{
				Panel = panel,
				Icon = icon,
				Header = header,
				Info = info,
				Inventory = inventory
			});
		}
	}

	private void ApplyExplorationModeButtonState(Button button, bool selected)
	{
		if (button == null)
		{
			return;
		}

		StyleBoxFlat style = CreateCombatSquareStyle(
			selected ? new Color(0.08f, 0.14f, 0.11f, 0.96f) : new Color(0.09f, 0.09f, 0.10f, 0.92f),
			selected ? new Color(0.34f, 0.98f, 0.60f, 1f) : new Color(0.32f, 0.36f, 0.42f, 1f));
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", style);
		button.AddThemeStyleboxOverride("pressed", style);
		button.AddThemeStyleboxOverride("focus", style);
		button.AddThemeColorOverride("font_color", selected ? new Color(0.92f, 1f, 0.96f) : Colors.White);
	}

	private void ApplyExplorationOfficerButtonState(Button button, bool selected)
	{
		if (button == null)
		{
			return;
		}

		StyleBoxFlat style = CreateCombatSquareStyle(
			selected ? new Color(0.10f, 0.14f, 0.18f, 0.96f) : new Color(0.08f, 0.08f, 0.09f, 0.90f),
			selected ? new Color(0.40f, 0.92f, 1.00f, 1f) : new Color(0.26f, 0.30f, 0.36f, 1f));
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", style);
		button.AddThemeStyleboxOverride("pressed", style);
		button.AddThemeStyleboxOverride("focus", style);
		button.AddThemeColorOverride("font_color", Colors.White);
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

	private PanelContainer BuildInventoryRailTag(string text, bool active)
	{
		PanelContainer tag = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 70f)
		};
		tag.AddThemeStyleboxOverride("panel", CreateInventoryRailStyle(active));

		Label label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", active ? new Color(0.96f, 0.90f, 0.80f) : new Color(0.72f, 0.68f, 0.62f));
		tag.AddChild(label);
		return tag;
	}

	private PanelContainer BuildInventorySectionCard(string title, out VBoxContainer body)
	{
		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", CreateInventorySectionStyle());

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);
		content.AddChild(BuildInventorySectionHeader(title));

		body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 8);
		content.AddChild(body);
		return panel;
	}

	private Label BuildInventorySectionHeader(string title)
	{
		Label header = new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		header.AddThemeFontSizeOverride("font_size", 17);
		header.AddThemeColorOverride("font_color", new Color(0.92f, 0.86f, 0.74f));
		return header;
	}

	private void PopulateInventoryEntryStack(VBoxContainer container, IReadOnlyList<MissionInventoryEntry> entries, string emptyText)
	{
		if (container == null)
		{
			return;
		}

		ClearContainerChildren(container);
		if (entries == null || entries.Count == 0)
		{
			container.AddChild(BuildInventoryPlaceholder(emptyText));
			return;
		}

		foreach (MissionInventoryEntry entry in entries)
		{
			if (entry == null)
			{
				continue;
			}

			container.AddChild(BuildInventoryEntryCard(entry));
		}
	}

	private void PopulateInteractiveInventoryEntryStack(
		VBoxContainer container,
		IReadOnlyList<MissionInventoryEntry> entries,
		string emptyText,
		Action<string> onActivate)
	{
		if (container == null)
		{
			return;
		}

		ClearContainerChildren(container);
		if (entries == null || entries.Count == 0)
		{
			container.AddChild(BuildInventoryPlaceholder(emptyText));
			return;
		}

		foreach (MissionInventoryEntry entry in entries)
		{
			if (entry == null)
			{
				continue;
			}

			container.AddChild(BuildInventoryEntryCard(entry, onActivate));
		}
	}

	private void PopulateInventoryItemStack(IReadOnlyList<MissionInventoryEntry> entries)
	{
		if (_officerInventoryItemStack == null)
		{
			return;
		}

		ClearContainerChildren(_officerInventoryItemStack);
		int visibleCount = 0;
		foreach (MissionInventoryEntry entry in entries ?? Array.Empty<MissionInventoryEntry>())
		{
			if (entry == null)
			{
				continue;
			}

			_officerInventoryItemStack.AddChild(BuildInventoryItemCell(entry));
			visibleCount++;
		}

		if (visibleCount == 0)
		{
			_officerInventoryItemStack.AddChild(BuildInventoryPlaceholder("No recovered mission salvage."));
			return;
		}

		for (int slotIndex = visibleCount; slotIndex < 4; slotIndex++)
		{
			_officerInventoryItemStack.AddChild(BuildInventoryEmptyCell());
		}
	}

	private Control BuildInventoryEntryCard(MissionInventoryEntry entry, Action<string> onActivate = null)
	{
		PanelContainer card = new PanelContainer
		{
			TooltipText = entry.Detail,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 76f)
		};
		card.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(entry.Highlighted, false));

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		card.AddChild(margin);

		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		margin.AddChild(row);

		VBoxContainer textColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		textColumn.AddThemeConstantOverride("separation", 2);
		row.AddChild(textColumn);

		Label title = new Label
		{
			Text = entry.Title
		};
		title.AddThemeFontSizeOverride("font_size", 15);
		title.AddThemeColorOverride("font_color", entry.Highlighted ? new Color(0.98f, 0.92f, 0.82f) : Colors.White);
		textColumn.AddChild(title);

		if (!string.IsNullOrWhiteSpace(entry.Detail))
		{
			Label detail = new Label
			{
				Text = entry.Detail,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			detail.AddThemeFontSizeOverride("font_size", 12);
			detail.AddThemeColorOverride("font_color", new Color(0.83f, 0.79f, 0.72f));
			textColumn.AddChild(detail);
		}

		if (!string.IsNullOrWhiteSpace(entry.QuantityText))
		{
			Label quantity = new Label
			{
				Text = entry.QuantityText,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center
			};
			quantity.CustomMinimumSize = new Vector2(40f, 0f);
			quantity.AddThemeFontSizeOverride("font_size", 14);
			quantity.AddThemeColorOverride("font_color", new Color(0.94f, 0.83f, 0.56f));
			row.AddChild(quantity);
		}

		if (entry.CanActivate && !string.IsNullOrWhiteSpace(entry.EntryId))
		{
			Button actionButton = new Button
			{
				Text = string.IsNullOrWhiteSpace(entry.ActionText) ? "EQUIP" : entry.ActionText,
				Disabled = entry.Highlighted,
				CustomMinimumSize = new Vector2(88f, 34f)
			};
			actionButton.AddThemeStyleboxOverride("normal", CreateInventoryButtonStyle());
			actionButton.AddThemeStyleboxOverride("hover", CreateInventoryButtonStyle(true));
			actionButton.AddThemeStyleboxOverride("pressed", CreateInventoryButtonStyle(true));
			actionButton.AddThemeStyleboxOverride("disabled", CreateInventoryButtonStyle());
			actionButton.AddThemeFontSizeOverride("font_size", 12);
			string entryId = entry.EntryId;
			actionButton.Pressed += () => onActivate?.Invoke(entryId);
			row.AddChild(actionButton);
		}

		return card;
	}

	private Control BuildInventoryPlaceholder(string text)
	{
		PanelContainer card = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		card.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(false, true));

		Label label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", new Color(0.68f, 0.64f, 0.60f));
		card.CustomMinimumSize = new Vector2(0f, 64f);
		card.AddChild(label);
		return card;
	}

	private Control BuildInventoryItemCell(MissionInventoryEntry entry)
	{
		PanelContainer cell = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 92f),
			TooltipText = entry.Detail,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		cell.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(entry.Highlighted, false));

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		cell.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 4);
		margin.AddChild(content);

		Label title = new Label
		{
			Text = entry.Title,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		title.AddThemeFontSizeOverride("font_size", 14);
		title.AddThemeColorOverride("font_color", Colors.White);
		content.AddChild(title);

		Label detail = new Label
		{
			Text = string.IsNullOrWhiteSpace(entry.Detail) ? "Mission salvage slot." : entry.Detail,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		detail.AddThemeFontSizeOverride("font_size", 11);
		detail.AddThemeColorOverride("font_color", new Color(0.82f, 0.77f, 0.70f));
		content.AddChild(detail);

		if (!string.IsNullOrWhiteSpace(entry.QuantityText))
		{
			Label quantity = new Label
			{
				Text = entry.QuantityText,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			quantity.AddThemeFontSizeOverride("font_size", 13);
			quantity.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.52f));
			content.AddChild(quantity);
		}

		return cell;
	}

	private Control BuildInventoryEmptyCell()
	{
		PanelContainer cell = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0f, 56f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		cell.AddThemeStyleboxOverride("panel", CreateInventorySlotStyle(false, true));
		return cell;
	}

	private static void ClearContainerChildren(Node container)
	{
		if (container == null)
		{
			return;
		}

		foreach (Node child in container.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void OnOfficerInventoryOverlayGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton
			|| !mouseButton.Pressed
			|| mouseButton.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		HideOfficerInventory();
		_officerInventoryOverlay.AcceptEvent();
	}

	private static StyleBoxFlat CreateInventoryWindowStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.17f, 0.13f, 0.11f, 0.98f),
			BorderColor = new Color(0.55f, 0.50f, 0.44f, 1f),
			BorderWidthLeft = 5,
			BorderWidthTop = 5,
			BorderWidthRight = 5,
			BorderWidthBottom = 5,
			CornerRadiusTopLeft = 16,
			CornerRadiusTopRight = 16,
			CornerRadiusBottomRight = 16,
			CornerRadiusBottomLeft = 16,
			ShadowColor = new Color(0f, 0f, 0f, 0.42f),
			ShadowSize = 12,
			ContentMarginLeft = 4f,
			ContentMarginTop = 4f,
			ContentMarginRight = 4f,
			ContentMarginBottom = 4f
		};
	}

	private static StyleBoxFlat CreateInventoryBannerStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.31f, 0.24f, 0.18f, 0.98f),
			BorderColor = new Color(0.68f, 0.60f, 0.45f, 1f),
			BorderWidthLeft = 4,
			BorderWidthTop = 4,
			BorderWidthRight = 4,
			BorderWidthBottom = 4,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12
		};
	}

	private static StyleBoxFlat CreateInventorySectionStyle(bool highlighted = false)
	{
		return new StyleBoxFlat
		{
			BgColor = highlighted ? new Color(0.25f, 0.20f, 0.17f, 0.96f) : new Color(0.14f, 0.11f, 0.09f, 0.96f),
			BorderColor = highlighted ? new Color(0.72f, 0.60f, 0.42f, 1f) : new Color(0.44f, 0.40f, 0.36f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
	}

	private static StyleBoxFlat CreateInventorySlotStyle(bool highlighted, bool empty)
	{
		return new StyleBoxFlat
		{
			BgColor = empty
				? new Color(0.09f, 0.08f, 0.07f, 0.82f)
				: highlighted
					? new Color(0.34f, 0.23f, 0.14f, 0.94f)
					: new Color(0.18f, 0.15f, 0.12f, 0.94f),
			BorderColor = empty
				? new Color(0.24f, 0.22f, 0.20f, 0.82f)
				: highlighted
					? new Color(0.86f, 0.70f, 0.42f, 1f)
					: new Color(0.46f, 0.40f, 0.34f, 1f),
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

	private static StyleBoxFlat CreateInventoryRailStyle(bool active)
	{
		return new StyleBoxFlat
		{
			BgColor = active ? new Color(0.30f, 0.22f, 0.14f, 0.96f) : new Color(0.11f, 0.09f, 0.08f, 0.92f),
			BorderColor = active ? new Color(0.82f, 0.68f, 0.40f, 1f) : new Color(0.36f, 0.33f, 0.30f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
	}

	private static StyleBoxFlat CreateInventoryButtonStyle(bool hover = false)
	{
		return new StyleBoxFlat
		{
			BgColor = hover ? new Color(0.39f, 0.26f, 0.16f, 0.98f) : new Color(0.24f, 0.18f, 0.12f, 0.96f),
			BorderColor = hover ? new Color(0.90f, 0.74f, 0.44f, 1f) : new Color(0.62f, 0.50f, 0.30f, 1f),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
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

	private void ClearExtractionPromptButtons()
	{
		if (_extractionPromptButtonStack == null)
		{
			return;
		}

		foreach (Node child in _extractionPromptButtonStack.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void ClearCombatActionButtons()
	{
		if (_combatActionButtonRow == null)
		{
			return;
		}

		foreach (Node child in _combatActionButtonRow.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void ClearExplorationOfficerButtons()
	{
		if (_explorationOfficerButtonStack == null)
		{
			return;
		}

		foreach (Node child in _explorationOfficerButtonStack.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void ClearCombatWeaponButtons()
	{
		if (_combatWeaponButtonStack == null)
		{
			return;
		}

		foreach (Node child in _combatWeaponButtonStack.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void ClearInteractionMenuButtons()
	{
		if (_interactionMenuButtonStack == null)
		{
			return;
		}

		foreach (Node child in _interactionMenuButtonStack.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void OnCombatEndTurnButtonPressed()
	{
		EmitSignal(SignalName.CombatEndTurnPressed);
	}

	private void OnStoryEventConfirmButtonPressed()
	{
		EmitSignal(SignalName.StoryEventConfirmed);
	}

	private void OnConfirmationAcceptedButtonPressed()
	{
		EmitSignal(SignalName.ConfirmationAccepted);
	}

	private void OnConfirmationCancelledButtonPressed()
	{
		EmitSignal(SignalName.ConfirmationCancelled);
	}

	private void OnSaveNameSubmitted(string _text)
	{
		ConfirmMissionSavePrompt();
	}
}
