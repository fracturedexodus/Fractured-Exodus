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
	public Texture2D Icon { get; init; }
	public int CurrentHP { get; init; }
	public int MaxHP { get; init; }
	public int CurrentAP { get; init; }
	public int MaxAP { get; init; }
	public int AttackRange { get; init; }
	public int AttackDamage { get; init; }
}

public partial class MissionUI : CanvasLayer
{
	[Signal]
	public delegate void ExtractionOutcomeChosenEventHandler(string outcomeId);

	[Signal]
	public delegate void CombatEndTurnPressedEventHandler();

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
	private VBoxContainer _initiativeRoot;
	private Label _combatTurnLabel;
	private HBoxContainer _combatInitiativeRow;
	private Button _combatEndTurnButton;
	private PanelContainer _playerCombatInfoPanel;
	private TextureRect _playerCombatIcon;
	private Label _playerCombatInfoLabel;
	private PanelContainer _enemyCombatInfoPanel;
	private TextureRect _enemyCombatIcon;
	private Label _enemyCombatInfoLabel;
	private ColorRect _gameOverPanel;
	private Label _gameOverLabel;
	private Button _gameOverReturnButton;

	public Button GameOverReturnButton => _gameOverReturnButton;

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
		BuildCombatHud();
		BuildGameOverPanel();
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
				TooltipText = $"{summary.DisplayName}\nHP {summary.CurrentHP}/{summary.MaxHP} | AP {summary.CurrentAP}/{summary.MaxAP}"
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
		UpdateCombatInfoPanel(summary, _playerCombatInfoPanel, _playerCombatIcon, _playerCombatInfoLabel, "OFFICER");
	}

	public void SetEnemyCombatInfo(MissionCombatantSummary summary)
	{
		UpdateCombatInfoPanel(summary, _enemyCombatInfoPanel, _enemyCombatIcon, _enemyCombatInfoLabel, "ENEMY");
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
		_initiativeRoot.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_initiativeRoot.AnchorLeft = 0.5f;
		_initiativeRoot.AnchorRight = 0.5f;
		_initiativeRoot.OffsetLeft = -360f;
		_initiativeRoot.OffsetTop = 18f;
		_initiativeRoot.OffsetRight = 360f;
		_initiativeRoot.OffsetBottom = 182f;
		_initiativeRoot.AddThemeConstantOverride("separation", 8);
		_uiRoot.AddChild(_initiativeRoot);

		_combatTurnLabel = new Label
		{
			Text = "MISSION COMBAT",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_combatTurnLabel.AddThemeFontSizeOverride("font_size", 22);
		_initiativeRoot.AddChild(_combatTurnLabel);

		_combatInitiativeRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		_combatInitiativeRow.AddThemeConstantOverride("separation", 8);
		_initiativeRoot.AddChild(_combatInitiativeRow);

		_combatEndTurnButton = new Button
		{
			Text = "END TURN",
			CustomMinimumSize = new Vector2(180f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		_combatEndTurnButton.Pressed += () => EmitSignal(SignalName.CombatEndTurnPressed);
		_initiativeRoot.AddChild(_combatEndTurnButton);

		_playerCombatInfoPanel = BuildCombatInfoPanel(new Vector2(20f, 760f), out _playerCombatIcon, out _playerCombatInfoLabel);
		_enemyCombatInfoPanel = BuildCombatInfoPanel(new Vector2(1500f, 760f), out _enemyCombatIcon, out _enemyCombatInfoLabel);
		_playerCombatInfoPanel.Visible = false;
		_enemyCombatInfoPanel.Visible = false;
		_uiRoot.AddChild(_playerCombatInfoPanel);
		_uiRoot.AddChild(_enemyCombatInfoPanel);
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

	private PanelContainer BuildCombatInfoPanel(Vector2 position, out TextureRect iconRect, out Label infoLabel)
	{
		PanelContainer panel = new PanelContainer
		{
			Position = position,
			Size = new Vector2(392f, 262f)
		};

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 10);
		margin.AddChild(content);

		iconRect = new TextureRect
		{
			CustomMinimumSize = new Vector2(120f, 88f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		content.AddChild(iconRect);

		infoLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(infoLabel);
		return panel;
	}

	private void UpdateCombatInfoPanel(MissionCombatantSummary summary, PanelContainer panel, TextureRect iconRect, Label infoLabel, string emptyTitle)
	{
		if (panel == null || iconRect == null || infoLabel == null)
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
		infoLabel.Text = $"{emptyTitle}: {summary.DisplayName}\n{summary.Subtitle}\nWEAPON: {summary.WeaponName}\nHP: {summary.CurrentHP}/{summary.MaxHP}\nAP: {summary.CurrentAP}/{summary.MaxAP}\nRANGE: {summary.AttackRange} | DMG: 1-{summary.AttackDamage}";
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
}
