using Godot;
using System;

public partial class AmbientEventPanel : CanvasLayer
{
	public event Action<AmbientEventChoice> ChoiceSelected;
	public event Action CloseRequested;

	private CenterContainer _wrapper;
	private Label _titleLabel;
	private Label _speakerLabel;
	private RichTextLabel _bodyLabel;
	private Label _statusLabel;
	private VBoxContainer _choiceList;
	private Button _closeButton;
	private AmbientEventDefinition _definition;
	private AmbientEventInstanceData _instance;
	private AmbientEventService _service;
	private MapEntity _actingShip;

	public bool IsOpen => _wrapper?.Visible == true;

	public override void _Ready()
	{
		Layer = 185;
		BuildUi();
	}

	public void Open(AmbientEventDefinition definition, AmbientEventInstanceData instance, AmbientEventService service, MapEntity actingShip)
	{
		_definition = definition;
		_instance = instance;
		_service = service;
		_actingShip = actingShip;
		_statusLabel.Text = string.Empty;
		_closeButton.Text = "LEAVE SIGNAL OPEN";
		_wrapper.Visible = true;
		RenderCurrentNode();
	}

	public void Close()
	{
		if (_wrapper != null)
		{
			_wrapper.Visible = false;
		}
	}

	public void ShowChoiceFailure(string reason)
	{
		_statusLabel.Text = $"[Unavailable] {reason}";
		_statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.48f, 0.48f));
	}

	public void ShowBranchResult(string resultText)
	{
		RenderCurrentNode();
		_statusLabel.Text = resultText ?? string.Empty;
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.92f, 1f));
	}

	public void ShowCompletedResult(string resultText)
	{
		_titleLabel.Text = _definition?.Title?.ToUpperInvariant() ?? "AMBIENT EVENT";
		_speakerLabel.Text = "OUTCOME";
		_bodyLabel.Text = resultText ?? string.Empty;
		_statusLabel.Text = "The signal has been resolved.";
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.55f, 1f, 0.72f));
		ClearChoices();
		_closeButton.Text = "CLOSE";
	}

	private void BuildUi()
	{
		_wrapper = new CenterContainer();
		_wrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_wrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_wrapper.Visible = false;
		AddChild(_wrapper);

		PanelContainer panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(820f, 650f)
		};
		StyleBoxFlat style = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.035f, 0.09f, 0.98f),
			BorderColor = new Color(0.68f, 0.34f, 1f, 0.9f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 28,
			ContentMarginTop = 24,
			ContentMarginRight = 28,
			ContentMarginBottom = 24,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10
		};
		panel.AddThemeStyleboxOverride("panel", style);
		_wrapper.AddChild(panel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		panel.AddChild(content);

		_titleLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_titleLabel.AddThemeFontSizeOverride("font_size", 27);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.62f, 1f));
		content.AddChild(_titleLabel);

		_speakerLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_speakerLabel.AddThemeFontSizeOverride("font_size", 17);
		_speakerLabel.AddThemeColorOverride("font_color", new Color(0.66f, 0.88f, 1f));
		content.AddChild(_speakerLabel);

		_bodyLabel = new RichTextLabel
		{
			CustomMinimumSize = new Vector2(760f, 190f),
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_bodyLabel);

		_statusLabel = new Label
		{
			CustomMinimumSize = new Vector2(0f, 34f),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_statusLabel);

		ScrollContainer choiceScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(760f, 285f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		content.AddChild(choiceScroll);

		_choiceList = new VBoxContainer();
		_choiceList.AddThemeConstantOverride("separation", 9);
		choiceScroll.AddChild(_choiceList);

		HBoxContainer footer = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		content.AddChild(footer);
		_closeButton = BuildCloseButton("LEAVE SIGNAL OPEN");
		footer.AddChild(_closeButton);
	}

	private void RenderCurrentNode()
	{
		AmbientEventNode node = _definition?.GetNode(_instance?.CurrentNodeId ?? _definition?.StartNodeId);
		if (node == null)
		{
			ShowChoiceFailure("Event branch data could not be found.");
			return;
		}

		_titleLabel.Text = _definition.Title.ToUpperInvariant();
		_speakerLabel.Text = node.SpeakerName;
		_bodyLabel.Text = node.Text;
		_statusLabel.Text = string.Empty;
		ClearChoices();

		foreach (AmbientEventChoice choice in node.Choices)
		{
			AmbientEventChoiceState choiceState = _service.GetChoiceState(choice, _actingShip);
			Button button = new Button
			{
				Text = choice.Text,
				Disabled = !choiceState.IsAvailable,
				TooltipText = choiceState.IsAvailable ? string.Empty : choiceState.RequirementText,
				CustomMinimumSize = new Vector2(740f, 48f),
				TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
			};
			button.AddThemeColorOverride("font_color", new Color(0.9f, 0.94f, 1f));
			button.AddThemeColorOverride("font_disabled_color", new Color(0.48f, 0.5f, 0.58f));
			button.Pressed += () =>
			{
				DisableChoiceButtons();
				ChoiceSelected?.Invoke(choice);
			};
			_choiceList.AddChild(button);

			if (!choiceState.IsAvailable)
			{
				Label requirement = new Label
				{
					Text = choiceState.RequirementText,
					HorizontalAlignment = HorizontalAlignment.Center
				};
				requirement.AddThemeColorOverride("font_color", new Color(0.9f, 0.48f, 0.48f));
				_choiceList.AddChild(requirement);
			}
		}
	}

	private void ClearChoices()
	{
		foreach (Node child in _choiceList.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void DisableChoiceButtons()
	{
		foreach (Node child in _choiceList.GetChildren())
		{
			if (child is Button button)
			{
				button.Disabled = true;
			}
		}
	}

	private Button BuildCloseButton(string text = "CLOSE")
	{
		Button button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(210f, 42f)
		};
		button.Pressed += () => CloseRequested?.Invoke();
		return button;
	}
}
