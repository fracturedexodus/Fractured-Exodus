using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class OfficerAssignmentMenu : Control
{
	private GlobalData _globalData;
	private OfficerService _officerService;
	private List<string> _fleetShips = new List<string>();
	private List<string> _portraitOptions = new List<string>();
	private int _currentShipIndex = 0;
	private bool _useCustomMode = false;

	private Label _shipLabel;
	private Label _statusLabel;
	private TextureRect _shipPreview;
	private TextureRect _portraitPreview;
	private Label _detailsLabel;
	private Button _presetModeButton;
	private Button _customModeButton;
	private VBoxContainer _presetPanel;
	private VBoxContainer _customPanel;
	private LineEdit _nameEdit;
	private OptionButton _portraitOption;
	private OptionButton _archetypeOption;
	private OptionButton _ideologyOption;
	private OptionButton _specialtyOption;
	private OptionButton _flawOption;
	private OptionButton _bioSeedOption;
	private Label _messageLabel;
	private Button _launchButton;

	public override void _Ready()
	{
		_globalData = GetNode<GlobalData>("/root/GlobalData");
		_officerService = new OfficerService(_globalData);
		_fleetShips = (_globalData.SelectedPlayerFleet ?? new List<string>()).ToList();
		_portraitOptions = _officerService.GetPortraitOptions();

		if (_fleetShips.Count == 0)
		{
			GoBack();
			return;
		}

		BuildUi();
		RefreshCurrentShip();
	}

	private void BuildUi()
	{
		PanelContainer rootPanel = new PanelContainer();
		rootPanel.AnchorRight = 1.0f;
		rootPanel.AnchorBottom = 1.0f;
		AddChild(rootPanel);

		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 48);
		margin.AddThemeConstantOverride("margin_top", 32);
		margin.AddThemeConstantOverride("margin_right", 48);
		margin.AddThemeConstantOverride("margin_bottom", 32);
		rootPanel.AddChild(margin);

		VBoxContainer root = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 18);
		margin.AddChild(root);

		Label title = new Label
		{
			Text = "ASSIGN OFFICERS",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 36);
		root.AddChild(title);

		_statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		root.AddChild(_statusLabel);

		HSplitContainer content = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		content.SplitOffset = 860;
		root.AddChild(content);

		VBoxContainer left = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(720, 0),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		left.AddThemeConstantOverride("separation", 12);
		content.AddChild(left);

		_shipLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_shipLabel.AddThemeFontSizeOverride("font_size", 30);
		left.AddChild(_shipLabel);

		HBoxContainer previewRow = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		previewRow.AddThemeConstantOverride("separation", 18);
		left.AddChild(previewRow);

		VBoxContainer shipPreviewColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		shipPreviewColumn.AddThemeConstantOverride("separation", 8);
		previewRow.AddChild(shipPreviewColumn);

		Label shipPreviewLabel = new Label
		{
			Text = "SHIP",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		shipPreviewColumn.AddChild(shipPreviewLabel);

		_shipPreview = new TextureRect
		{
			CustomMinimumSize = new Vector2(350, 420),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		shipPreviewColumn.AddChild(_shipPreview);

		VBoxContainer officerPreviewColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		officerPreviewColumn.AddThemeConstantOverride("separation", 8);
		previewRow.AddChild(officerPreviewColumn);

		Label officerPreviewLabel = new Label
		{
			Text = "OFFICER",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		officerPreviewColumn.AddChild(officerPreviewLabel);

		_portraitPreview = new TextureRect
		{
			CustomMinimumSize = new Vector2(350, 420),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		officerPreviewColumn.AddChild(_portraitPreview);

		_detailsLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		left.AddChild(_detailsLabel);

		ScrollContainer rightScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(520, 0),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		content.AddChild(rightScroll);

		VBoxContainer right = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		right.AddThemeConstantOverride("separation", 12);
		rightScroll.AddChild(right);

		HBoxContainer modeButtons = new HBoxContainer();
		modeButtons.AddThemeConstantOverride("separation", 12);
		right.AddChild(modeButtons);

		_presetModeButton = new Button { Text = "Use Preset Officer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_presetModeButton.Pressed += () =>
		{
			_useCustomMode = false;
			RefreshCurrentShip(true);
		};
		modeButtons.AddChild(_presetModeButton);

		_customModeButton = new Button { Text = "Create Custom Officer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_customModeButton.Pressed += () =>
		{
			_useCustomMode = true;
			RefreshCurrentShip(true);
		};
		modeButtons.AddChild(_customModeButton);

		_presetPanel = new VBoxContainer();
		_presetPanel.AddThemeConstantOverride("separation", 8);
		right.AddChild(_presetPanel);

		Label presetIntro = new Label
		{
			Text = "Preset officers bring authored backstory, fixed ideology, and richer personal hooks.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_presetPanel.AddChild(presetIntro);

		_customPanel = new VBoxContainer();
		_customPanel.AddThemeConstantOverride("separation", 8);
		right.AddChild(_customPanel);

		_nameEdit = AddLabeledLineEdit(_customPanel, "Officer Name");
		_portraitOption = AddLabeledOption(_customPanel, "Portrait");
		_archetypeOption = AddLabeledOption(_customPanel, "Archetype");
		_ideologyOption = AddLabeledOption(_customPanel, "Ideology");
		_specialtyOption = AddLabeledOption(_customPanel, "Specialty");
		_flawOption = AddLabeledOption(_customPanel, "Flaw");
		_bioSeedOption = AddLabeledOption(_customPanel, "Origin");

		PopulateOptionButton(_portraitOption, _portraitOptions.Select(GetPortraitLabel));
		PopulateOptionButton(_archetypeOption, OfficerService.Archetypes);
		PopulateOptionButton(_ideologyOption, OfficerService.Ideologies);
		PopulateOptionButton(_specialtyOption, OfficerService.Specialties);
		PopulateOptionButton(_flawOption, OfficerService.Flaws);
		PopulateOptionButton(_bioSeedOption, OfficerService.BiographySeeds);

		_portraitOption.ItemSelected += _ => UpdateCustomPortraitPreview();

		_messageLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(0.8f, 0.95f, 1f)
		};
		right.AddChild(_messageLabel);

		HBoxContainer footerButtons = new HBoxContainer();
		footerButtons.AddThemeConstantOverride("separation", 12);
		root.AddChild(footerButtons);

		Button backButton = new Button { Text = "Back to Fleet Builder", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		backButton.Pressed += () =>
		{
			SaveCurrentOfficer();
			GoBack();
		};
		footerButtons.AddChild(backButton);

		Button prevButton = new Button { Text = "Previous Ship", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		prevButton.Pressed += () => StepShip(-1);
		footerButtons.AddChild(prevButton);

		Button nextButton = new Button { Text = "Next Ship", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		nextButton.Pressed += () => StepShip(1);
		footerButtons.AddChild(nextButton);

		Button saveButton = new Button { Text = "Save Officer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		saveButton.Pressed += () =>
		{
			SaveCurrentOfficer();
			SetMessage("Officer saved for this ship.");
			RefreshCurrentShip(true);
		};
		footerButtons.AddChild(saveButton);

		_launchButton = new Button { Text = "Begin Exodus", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_launchButton.Pressed += OnLaunchPressed;
		footerButtons.AddChild(_launchButton);
	}

	private LineEdit AddLabeledLineEdit(VBoxContainer parent, string labelText)
	{
		Label label = new Label { Text = labelText };
		parent.AddChild(label);
		LineEdit lineEdit = new LineEdit();
		parent.AddChild(lineEdit);
		return lineEdit;
	}

	private OptionButton AddLabeledOption(VBoxContainer parent, string labelText)
	{
		Label label = new Label { Text = labelText };
		parent.AddChild(label);
		OptionButton option = new OptionButton();
		parent.AddChild(option);
		return option;
	}

	private void PopulateOptionButton(OptionButton option, IEnumerable<string> values)
	{
		option.Clear();
		foreach (string value in values)
		{
			option.AddItem(value);
		}
	}

	private void RefreshCurrentShip(bool preserveMode = false)
	{
		string shipName = _fleetShips[_currentShipIndex];
		OfficerTemplate template = _officerService.GetPresetTemplateForShip(shipName);
		OfficerState assigned = _officerService.GetOfficerForShip(shipName);

		if (!preserveMode)
		{
			_useCustomMode = assigned != null && assigned.IsCustom;
		}

		_shipLabel.Text = $"{shipName}\nOfficer {_currentShipIndex + 1}/{_fleetShips.Count}";
		_statusLabel.Text = $"Assigned Officers: {GetAssignedCount()}/{_fleetShips.Count}";
		_presetPanel.Visible = !_useCustomMode;
		_customPanel.Visible = _useCustomMode;
		_presetModeButton.Disabled = !_useCustomMode;
		_customModeButton.Disabled = _useCustomMode;

		if (!_useCustomMode)
		{
			OfficerState preview = assigned != null && !assigned.IsCustom ? assigned : _officerService.CreatePresetOfficer(shipName);
			ApplyOfficerPreview(preview, template);
			_messageLabel.Text = "Preset is the default if you leave a ship unchanged.";
		}
		else
		{
			OfficerState preview = assigned != null && assigned.IsCustom ? assigned : BuildDefaultCustomOfficer(shipName, template);
			LoadCustomState(preview);
			ApplyOfficerPreview(preview, template);
			_messageLabel.Text = "Custom officers use modular traits and shared dialogue logic.";
		}

		_launchButton.Text = GetAssignedCount() >= _fleetShips.Count ? "Begin Exodus" : "Begin Exodus (Unassigned ships use presets)";
	}

	private OfficerState BuildDefaultCustomOfficer(string shipName, OfficerTemplate template)
	{
		string portraitPath = !string.IsNullOrEmpty(template?.PortraitPath)
			? template.PortraitPath
			: (_portraitOptions.Count > 0 ? _portraitOptions[0] : string.Empty);
		return _officerService.CreateCustomOfficer(new CustomOfficerRequest
		{
			ShipName = shipName,
			DisplayName = $"Cmdr. {shipName.Replace("The ", string.Empty)}",
			PortraitPath = portraitPath,
			Archetype = OfficerService.Archetypes[0],
			Ideology = OfficerService.Ideologies[0],
			Specialty = template?.Specialty ?? OfficerService.Specialties[0],
			Flaw = OfficerService.Flaws[0],
			BiographySeed = OfficerService.BiographySeeds[0]
		});
	}

	private void ApplyOfficerPreview(OfficerState officer, OfficerTemplate template)
	{
		string shipPreviewPath = Database.GetShipBlueprintPath(officer.ShipName);
		if (string.IsNullOrEmpty(shipPreviewPath))
		{
			shipPreviewPath = Database.GetShipTexturePath(officer.ShipName);
		}

		if (!string.IsNullOrEmpty(shipPreviewPath))
		{
			_shipPreview.Texture = GD.Load<Texture2D>(shipPreviewPath);
		}
		else
		{
			_shipPreview.Texture = null;
		}

		if (!string.IsNullOrEmpty(officer.PortraitPath))
		{
			_portraitPreview.Texture = GD.Load<Texture2D>(officer.PortraitPath);
		}
		else
		{
			_portraitPreview.Texture = null;
		}

		string templateNote = officer.IsCustom ? "Custom Officer" : "Preset Officer";
		string bio = !string.IsNullOrEmpty(officer.Biography) ? officer.Biography : template?.Biography ?? "No biography available.";
		_detailsLabel.Text =
			$"{templateNote}\n\n" +
			$"Name: {officer.DisplayName}\n" +
			$"Archetype: {officer.Archetype}\n" +
			$"Ideology: {officer.Ideology}\n" +
			$"Specialty: {officer.Specialty}\n" +
			$"Flaw: {officer.Flaw}\n" +
			$"Ability: {officer.CombatAbilityID}\n" +
			$"Approval: {officer.Approval}\n\n" +
			$"{bio}";
	}

	private void LoadCustomState(OfficerState officer)
	{
		_nameEdit.Text = officer.DisplayName;
		SetOptionButtonSelection(_portraitOption, GetPortraitLabel(officer.PortraitPath));
		SetOptionButtonSelection(_archetypeOption, officer.Archetype);
		SetOptionButtonSelection(_ideologyOption, officer.Ideology);
		SetOptionButtonSelection(_specialtyOption, officer.Specialty);
		SetOptionButtonSelection(_flawOption, officer.Flaw);
		SetOptionButtonSelection(_bioSeedOption, string.IsNullOrEmpty(officer.BiographySeed) ? OfficerService.BiographySeeds[0] : officer.BiographySeed);
		UpdateCustomPortraitPreview();
	}

	private void SetOptionButtonSelection(OptionButton option, string value)
	{
		for (int i = 0; i < option.ItemCount; i++)
		{
			if (option.GetItemText(i) == value)
			{
				option.Select(i);
				return;
			}
		}

		if (option.ItemCount > 0)
		{
			option.Select(0);
		}
	}

	private void UpdateCustomPortraitPreview()
	{
		string portraitPath = GetSelectedPortraitPath();
		if (!string.IsNullOrEmpty(portraitPath))
		{
			_portraitPreview.Texture = GD.Load<Texture2D>(portraitPath);
		}
	}

	private void StepShip(int direction)
	{
		SaveCurrentOfficer();
		_currentShipIndex += direction;
		if (_currentShipIndex < 0)
		{
			_currentShipIndex = _fleetShips.Count - 1;
		}
		else if (_currentShipIndex >= _fleetShips.Count)
		{
			_currentShipIndex = 0;
		}
		SetMessage(string.Empty);
		RefreshCurrentShip();
	}

	private void SaveCurrentOfficer()
	{
		string shipName = _fleetShips[_currentShipIndex];
		if (_useCustomMode)
		{
			OfficerState customOfficer = _officerService.CreateCustomOfficer(new CustomOfficerRequest
			{
				ShipName = shipName,
				DisplayName = _nameEdit.Text,
				PortraitPath = GetSelectedPortraitPath(),
				Archetype = _archetypeOption.GetItemText(_archetypeOption.Selected),
				Ideology = _ideologyOption.GetItemText(_ideologyOption.Selected),
				Specialty = _specialtyOption.GetItemText(_specialtyOption.Selected),
				Flaw = _flawOption.GetItemText(_flawOption.Selected),
				BiographySeed = _bioSeedOption.GetItemText(_bioSeedOption.Selected)
			});
			_officerService.AssignOfficerToShip(shipName, customOfficer);
		}
		else
		{
			_officerService.AssignOfficerToShip(shipName, _officerService.CreatePresetOfficer(shipName));
		}
	}

	private void EnsureAllShipsAssigned()
	{
		foreach (string shipName in _fleetShips)
		{
			if (_officerService.GetOfficerForShip(shipName) == null)
			{
				_officerService.AssignOfficerToShip(shipName, _officerService.CreatePresetOfficer(shipName));
			}
		}
	}

	private void OnLaunchPressed()
	{
		SaveCurrentOfficer();
		EnsureAllShipsAssigned();
		SetMessage("Officer assignments locked in. Launching fleet.");
		SceneTransition transitioner = GetNode<SceneTransition>("/root/SceneTransition");
		transitioner.ChangeScene("res://exploration_battle.tscn");
	}

	private void GoBack()
	{
		SceneTransition transitioner = GetNode<SceneTransition>("/root/SceneTransition");
		transitioner.ChangeScene("res://fleet_selection.tscn");
	}

	private int GetAssignedCount()
	{
		return _fleetShips.Count(ship => _officerService.GetOfficerForShip(ship) != null);
	}

	private string GetSelectedPortraitPath()
	{
		if (_portraitOptions.Count == 0 || _portraitOption.Selected < 0 || _portraitOption.Selected >= _portraitOptions.Count)
		{
			return string.Empty;
		}

		return _portraitOptions[_portraitOption.Selected];
	}

	private string GetPortraitLabel(string portraitPath)
	{
		if (string.IsNullOrEmpty(portraitPath))
		{
			return "Default Portrait";
		}

		string fileName = portraitPath.Split('/').LastOrDefault() ?? portraitPath;
		return fileName.Replace(".png", string.Empty).Replace(".jpg", string.Empty);
	}

	private void SetMessage(string message)
	{
		_messageLabel.Text = message;
	}
}
