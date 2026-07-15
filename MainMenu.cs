using Godot;
using System;
using System.Collections.Generic;

public partial class MainMenu : Control
{
	private Button _loadGameButton;
	private Control _loadOverlay;
	private ItemList _saveList;
	private Label _saveDetailsLabel;
	private Label _loadStatusLabel;
	private Button _loadSelectedButton;
	private Button _deleteSelectedButton;
	private CenterContainer _deleteConfirmWrapper;
	private Label _deleteConfirmLabel;
	private string _pendingDeleteSlotId = string.Empty;
	private string _pendingDeleteDisplayName = string.Empty;
	private readonly List<SaveGameSlotInfo> _availableSaves = new List<SaveGameSlotInfo>();

	public override void _Ready()
	{
		AudioStreamPlayer menuMusic = GetNodeOrNull<AudioStreamPlayer>("MenuMusic");
		if (menuMusic?.Stream is AudioStreamMP3 mp3Stream)
		{
			mp3Stream.Loop = true;
		}

		_loadGameButton = GetNodeOrNull<Button>("MenuButtons/LoadGame");
		BuildLoadGameOverlay();
	}

	public void _on_launch_fleet_pressed()
	{
		GlobalData globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (globalData != null)
		{
			globalData.ResetForNewGame();
		}

		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene("res://galactic_map.tscn");
			return;
		}

		GetTree().ChangeSceneToFile("res://galactic_map.tscn");
	}

	public void _on_abandon_run_pressed()
	{
		GetTree().Quit();
	}

	public void _on_load_game_pressed()
	{
		GlobalData globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (globalData == null)
		{
			return;
		}

		_availableSaves.Clear();
		_availableSaves.AddRange(globalData.GetAvailableSaveGames());
		if (_availableSaves.Count == 0)
		{
			ShowNoSaveFeedback();
			return;
		}

		RefreshLoadOverlay();
		_loadOverlay.Visible = true;
	}

	private void BuildLoadGameOverlay()
	{
		ColorRect overlay = new ColorRect
		{
			Visible = false,
			Color = new Color(0f, 0f, 0f, 0.72f),
			MouseFilter = MouseFilterEnum.Stop
		};
		overlay.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(overlay);
		_loadOverlay = overlay;

		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		overlay.AddChild(center);

		PanelContainer panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(720f, 560f)
		};
		StyleBoxFlat panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.10f, 0.96f),
			BorderColor = new Color(0.29f, 0.93f, 1f, 0.85f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 22,
			ContentMarginTop = 18,
			ContentMarginRight = 22,
			ContentMarginBottom = 18,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		center.AddChild(panel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		panel.AddChild(content);

		Label title = new Label
		{
			Text = "LOAD GAME",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		content.AddChild(title);

		Label subtitle = new Label
		{
			Text = "Select a save file to resume your campaign.",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		content.AddChild(subtitle);

		_saveList = new ItemList
		{
			CustomMinimumSize = new Vector2(0f, 250f),
			SelectMode = ItemList.SelectModeEnum.Single
		};
		_saveList.ItemSelected += OnSaveListItemSelected;
		_saveList.ItemActivated += OnSaveListItemActivated;
		content.AddChild(_saveList);

		_saveDetailsLabel = new Label
		{
			CustomMinimumSize = new Vector2(0f, 108f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_saveDetailsLabel);

		_loadStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_loadStatusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f));
		content.AddChild(_loadStatusLabel);

		HBoxContainer buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttonRow);

		Button cancelButton = new Button
		{
			Text = "CANCEL",
			CustomMinimumSize = new Vector2(180f, 46f)
		};
		cancelButton.Pressed += HideLoadOverlay;
		buttonRow.AddChild(cancelButton);

		_deleteSelectedButton = new Button
		{
			Text = "DELETE",
			CustomMinimumSize = new Vector2(180f, 46f),
			Disabled = true
		};
		_deleteSelectedButton.Pressed += PromptDeleteSelectedSave;
		buttonRow.AddChild(_deleteSelectedButton);

		_loadSelectedButton = new Button
		{
			Text = "LOAD SELECTED",
			CustomMinimumSize = new Vector2(220f, 46f),
			Disabled = true
		};
		_loadSelectedButton.Pressed += LoadSelectedSave;
		buttonRow.AddChild(_loadSelectedButton);

		BuildDeleteConfirmationOverlay(overlay);
	}

	private void RefreshLoadOverlay()
	{
		if (_saveList == null)
		{
			return;
		}

		_saveList.Clear();
		_loadStatusLabel.Text = string.Empty;
		_saveDetailsLabel.Text = string.Empty;
		_loadSelectedButton.Disabled = _availableSaves.Count == 0;
		_deleteSelectedButton.Disabled = _availableSaves.Count == 0;

		for (int i = 0; i < _availableSaves.Count; i++)
		{
			_saveList.AddItem(BuildSaveListLabel(_availableSaves[i]));
		}

		if (_availableSaves.Count > 0)
		{
			_saveList.Select(0);
			UpdateSaveDetails(0);
		}
		else
		{
			_loadStatusLabel.Text = "No save files found.";
		}
	}

	private void OnSaveListItemSelected(long index)
	{
		UpdateSaveDetails((int)index);
	}

	private void OnSaveListItemActivated(long index)
	{
		UpdateSaveDetails((int)index);
		LoadSelectedSave();
	}

	private void UpdateSaveDetails(int index)
	{
		if (index < 0 || index >= _availableSaves.Count)
		{
			_saveDetailsLabel.Text = string.Empty;
			_loadSelectedButton.Disabled = true;
			_deleteSelectedButton.Disabled = true;
			return;
		}

		SaveGameSlotInfo save = _availableSaves[index];
		string locationText = !string.IsNullOrWhiteSpace(save.CurrentMissionTitle)
			? $"Mission: {save.CurrentMissionTitle}"
			: !string.IsNullOrWhiteSpace(save.SavedSystem)
				? $"System: {save.SavedSystem}{(string.IsNullOrWhiteSpace(save.SavedPlanet) ? string.Empty : $" | Planet: {save.SavedPlanet}")}"
				: "Location: Unknown";

		_saveDetailsLabel.Text = $"{locationText}\nTurn: {save.CurrentTurn}\nSaved: {FormatSaveTimestamp(save.SavedAtUtc)}";
		_loadSelectedButton.Disabled = false;
		_deleteSelectedButton.Disabled = false;
	}

	private void LoadSelectedSave()
	{
		if (_saveList == null)
		{
			return;
		}

		int[] selectedItems = _saveList.GetSelectedItems();
		if (selectedItems.Length == 0)
		{
			_loadStatusLabel.Text = "Select a save first.";
			return;
		}

		int selectedIndex = selectedItems[0];
		if (selectedIndex < 0 || selectedIndex >= _availableSaves.Count)
		{
			_loadStatusLabel.Text = "That save could not be found.";
			return;
		}

		SaveGameSlotInfo selectedSave = _availableSaves[selectedIndex];
		GlobalData globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (globalData == null || !globalData.LoadGame(selectedSave.SlotId))
		{
			_loadStatusLabel.Text = "Unable to load that save.";
			return;
		}

		string scenePath = ResolveLoadedScenePath(globalData, selectedSave);
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(scenePath);
			return;
		}

		GetTree().ChangeSceneToFile(scenePath);
	}

	private void HideLoadOverlay()
	{
		HideDeleteConfirmation();
		if (_loadOverlay != null)
		{
			_loadOverlay.Visible = false;
		}
	}

	private void BuildDeleteConfirmationOverlay(Control overlay)
	{
		_deleteConfirmWrapper = new CenterContainer
		{
			Visible = false
		};
		_deleteConfirmWrapper.SetAnchorsPreset(LayoutPreset.FullRect);
		overlay.AddChild(_deleteConfirmWrapper);

		PanelContainer panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(520f, 220f)
		};
		StyleBoxFlat panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.10f, 0.98f),
			BorderColor = new Color(1f, 0.40f, 0.40f, 0.90f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 20,
			ContentMarginTop = 18,
			ContentMarginRight = 20,
			ContentMarginBottom = 18,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		_deleteConfirmWrapper.AddChild(panel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		panel.AddChild(content);

		Label title = new Label
		{
			Text = "DELETE SAVE",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		content.AddChild(title);

		_deleteConfirmLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		content.AddChild(_deleteConfirmLabel);

		HBoxContainer buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttonRow);

		Button cancelButton = new Button
		{
			Text = "CANCEL",
			CustomMinimumSize = new Vector2(180f, 46f)
		};
		cancelButton.Pressed += HideDeleteConfirmation;
		buttonRow.AddChild(cancelButton);

		Button deleteButton = new Button
		{
			Text = "DELETE SAVE",
			CustomMinimumSize = new Vector2(200f, 46f)
		};
		deleteButton.Pressed += ConfirmDeleteSelectedSave;
		buttonRow.AddChild(deleteButton);
	}

	private void PromptDeleteSelectedSave()
	{
		if (_saveList == null || _deleteConfirmWrapper == null || _deleteConfirmLabel == null)
		{
			return;
		}

		int[] selectedItems = _saveList.GetSelectedItems();
		if (selectedItems.Length == 0)
		{
			_loadStatusLabel.Text = "Select a save first.";
			return;
		}

		int selectedIndex = selectedItems[0];
		if (selectedIndex < 0 || selectedIndex >= _availableSaves.Count)
		{
			_loadStatusLabel.Text = "That save could not be found.";
			return;
		}

		SaveGameSlotInfo selectedSave = _availableSaves[selectedIndex];
		_pendingDeleteSlotId = selectedSave.SlotId;
		_pendingDeleteDisplayName = BuildSaveListLabel(selectedSave);
		_deleteConfirmLabel.Text = $"Delete {_pendingDeleteDisplayName}?\nThis cannot be undone.";
		_deleteConfirmWrapper.Visible = true;
	}

	private void HideDeleteConfirmation()
	{
		_pendingDeleteSlotId = string.Empty;
		_pendingDeleteDisplayName = string.Empty;
		if (_deleteConfirmWrapper != null)
		{
			_deleteConfirmWrapper.Visible = false;
		}
	}

	private void ConfirmDeleteSelectedSave()
	{
		GlobalData globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (globalData == null || string.IsNullOrWhiteSpace(_pendingDeleteSlotId))
		{
			HideDeleteConfirmation();
			return;
		}

		string deletedSaveName = _pendingDeleteDisplayName;
		bool deleted = globalData.DeleteSaveGame(_pendingDeleteSlotId);
		HideDeleteConfirmation();
		if (!deleted)
		{
			_loadStatusLabel.Text = "Unable to delete that save.";
			return;
		}

		_availableSaves.Clear();
		_availableSaves.AddRange(globalData.GetAvailableSaveGames());
		RefreshLoadOverlay();
		_loadStatusLabel.Text = $"Deleted {deletedSaveName}.";
	}

	private void ShowNoSaveFeedback()
	{
		if (_loadGameButton == null)
		{
			return;
		}

		_loadGameButton.Text = "NO SAVE FOUND";
		_loadGameButton.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
		GetTree().CreateTimer(2.0f).Timeout += () =>
		{
			if (_loadGameButton == null)
			{
				return;
			}

			_loadGameButton.Text = "LOAD GAME";
			_loadGameButton.RemoveThemeColorOverride("font_color");
		};
	}

	private static string ResolveLoadedScenePath(GlobalData globalData, SaveGameSlotInfo selectedSave)
	{
		if (!string.IsNullOrWhiteSpace(selectedSave?.LastSavedScenePath))
		{
			return selectedSave.LastSavedScenePath;
		}

		if (!string.IsNullOrWhiteSpace(globalData?.LastSavedScenePath))
		{
			return globalData.LastSavedScenePath;
		}

		if (!string.IsNullOrWhiteSpace(globalData?.CurrentMissionScenePath))
		{
			return globalData.CurrentMissionScenePath;
		}

		if (globalData?.CurrentSectorStars?.Count > 0)
		{
			return "res://galactic_map.tscn";
		}

		return "res://exploration_battle.tscn";
	}

	private static string FormatSaveTimestamp(string savedAtUtc)
	{
		if (DateTime.TryParse(savedAtUtc, out DateTime parsed))
		{
			return parsed.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
		}

		return "Unknown";
	}

	private static string BuildSaveListLabel(SaveGameSlotInfo save)
	{
		if (save == null)
		{
			return string.Empty;
		}

		string label = save.DisplayName;
		if (save.IsAutoSave)
		{
			label += " [AUTOSAVE]";
		}
		else if (save.IsLegacySave)
		{
			label += " [QUICKSAVE]";
		}
		if (save.IsRecovered)
		{
			label += " [RECOVERED]";
		}

		return label;
	}
}
