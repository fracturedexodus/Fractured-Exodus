using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap
{
	private void BuildPauseMenuUI()
	{
		CanvasLayer pauseLayer = new CanvasLayer { Layer = 176 };
		AddChild(pauseLayer);

		_pauseMenuWrapper = new CenterContainer();
		_pauseMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_pauseMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_pauseMenuWrapper.Visible = false;
		pauseLayer.AddChild(_pauseMenuWrapper);

		PanelContainer pausePanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(440f, 320f)
		};
		pausePanel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
		_pauseMenuWrapper.AddChild(pausePanel);

		VBoxContainer content = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		content.AddThemeConstantOverride("separation", 12);
		pausePanel.AddChild(content);

		Label title = new Label
		{
			Text = "GAME MENU",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 26);
		content.AddChild(title);

		content.AddChild(BuildPauseMenuButton("SAVE GAME", OpenPauseSavePrompt));
		content.AddChild(BuildPauseMenuButton("LOAD GAME", ShowLoadGameMenu));
		content.AddChild(BuildPauseMenuButton("RETURN TO GAME", HidePauseMenus));
		content.AddChild(BuildPauseMenuButton("RETURN TO MAIN MENU", ReturnToMainMenuFromPause));
	}

	private void BuildLoadGameMenuUI()
	{
		CanvasLayer loadLayer = new CanvasLayer { Layer = 177 };
		AddChild(loadLayer);

		_loadMenuWrapper = new CenterContainer();
		_loadMenuWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_loadMenuWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_loadMenuWrapper.Visible = false;
		loadLayer.AddChild(_loadMenuWrapper);

		PanelContainer loadPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(720f, 560f)
		};
		loadPanel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
		_loadMenuWrapper.AddChild(loadPanel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		loadPanel.AddChild(content);

		Label title = new Label
		{
			Text = "LOAD GAME",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 28);
		content.AddChild(title);

		_loadSaveList = new ItemList
		{
			CustomMinimumSize = new Vector2(0f, 250f),
			SelectMode = ItemList.SelectModeEnum.Single
		};
		_loadSaveList.ItemSelected += index => UpdateLoadGameSelection((int)index);
		_loadSaveList.ItemActivated += index =>
		{
			UpdateLoadGameSelection((int)index);
			LoadSelectedPauseSave();
		};
		content.AddChild(_loadSaveList);

		_loadSaveDetailsLabel = new Label
		{
			CustomMinimumSize = new Vector2(0f, 108f),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		content.AddChild(_loadSaveDetailsLabel);

		_loadSaveStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_loadSaveStatusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f));
		content.AddChild(_loadSaveStatusLabel);

		HBoxContainer buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttonRow);

		buttonRow.AddChild(BuildPauseMenuButton("BACK", ShowPauseMenu, 180f));

		_deleteSelectedSaveButton = BuildPauseMenuButton("DELETE", PromptDeleteSelectedPauseSave, 180f);
		_deleteSelectedSaveButton.Disabled = true;
		buttonRow.AddChild(_deleteSelectedSaveButton);

		_loadSelectedSaveButton = BuildPauseMenuButton("LOAD SELECTED", LoadSelectedPauseSave, 220f);
		_loadSelectedSaveButton.Disabled = true;
		buttonRow.AddChild(_loadSelectedSaveButton);

		BuildDeleteSaveConfirmationUI(loadLayer);
	}

	private void OnMissionSaveConfirmed(string saveName)
	{
		if (_globalData == null)
		{
			return;
		}

		_globalData.CurrentMissionSaveState = BuildCurrentMissionSaveState();
		bool saved = _globalData.SaveNamedGame(saveName, ResolveMissionScenePath());
		AppendActionLog(saved
			? $"Mission saved as {saveName}."
			: $"Save failed: {_globalData.LastSaveError}");
	}

	private void QuickSaveMission()
	{
		if (_globalData == null)
		{
			return;
		}

		_globalData.CurrentMissionSaveState = BuildCurrentMissionSaveState();
		bool saved = _globalData.SaveGame(false, ResolveMissionScenePath());
		AppendActionLog(saved ? "Mission quicksaved." : $"Quicksave failed: {_globalData.LastSaveError}");
	}

	private void QuickLoadMission()
	{
		if (_globalData == null)
		{
			return;
		}

		SaveGameSlotInfo quicksave = _globalData
			.GetAvailableSaveGames()
			.FirstOrDefault(save => save != null && save.IsLegacySave);
		if (quicksave == null)
		{
			AppendActionLog("No quicksave found.");
			return;
		}

		if (!_globalData.LoadGame(quicksave.SlotId))
		{
			AppendActionLog("Quickload failed.");
			return;
		}

		HidePauseMenus();
		_missionUi?.HideMissionSavePrompt();
		string scenePath = ResolveLoadedScenePath(_globalData, quicksave);
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(scenePath);
			return;
		}

		GetTree().ChangeSceneToFile(scenePath);
	}

	private void ShowPauseMenu()
	{
		HideDeleteSaveConfirmation();
		if (_pauseMenuWrapper == null)
		{
			return;
		}

		_pauseMenuWrapper.Visible = true;
		if (_loadMenuWrapper != null)
		{
			_loadMenuWrapper.Visible = false;
		}
	}

	private void HidePauseMenus()
	{
		HideDeleteSaveConfirmation();
		if (_pauseMenuWrapper != null)
		{
			_pauseMenuWrapper.Visible = false;
		}

		if (_loadMenuWrapper != null)
		{
			_loadMenuWrapper.Visible = false;
		}
	}

	private void TogglePauseMenu()
	{
		if (_deleteSaveConfirmWrapper?.Visible == true)
		{
			HideDeleteSaveConfirmation();
			return;
		}

		if (_loadMenuWrapper?.Visible == true)
		{
			ShowPauseMenu();
			return;
		}

		if (_missionUi?.IsMissionSavePromptVisible == true)
		{
			_missionUi.HideMissionSavePrompt();
			return;
		}

		if (_pauseMenuWrapper == null)
		{
			return;
		}

		_pauseMenuWrapper.Visible = !_pauseMenuWrapper.Visible;
	}

	private void OpenPauseSavePrompt()
	{
		HidePauseMenus();
		_missionUi?.ShowMissionSavePrompt($"{(_missionState?.MissionTitle ?? "Mission").Trim()} Save");
	}

	private void ShowLoadGameMenu()
	{
		if (_globalData == null || _loadMenuWrapper == null || _loadSaveList == null)
		{
			return;
		}

		_availableSaveGames.Clear();
		_availableSaveGames.AddRange(_globalData.GetAvailableSaveGames());
		_loadSaveList.Clear();
		_loadSaveDetailsLabel.Text = string.Empty;
		_loadSaveStatusLabel.Text = string.Empty;
		_loadSelectedSaveButton.Disabled = _availableSaveGames.Count == 0;
		_deleteSelectedSaveButton.Disabled = _availableSaveGames.Count == 0;

		for (int i = 0; i < _availableSaveGames.Count; i++)
		{
			_loadSaveList.AddItem(BuildSaveListLabel(_availableSaveGames[i]));
		}

		_pauseMenuWrapper.Visible = false;
		_loadMenuWrapper.Visible = true;
		if (_availableSaveGames.Count > 0)
		{
			_loadSaveList.Select(0);
			UpdateLoadGameSelection(0);
		}
		else
		{
			_loadSaveStatusLabel.Text = "No save files found.";
		}
	}

	private void UpdateLoadGameSelection(int index)
	{
		if (index < 0 || index >= _availableSaveGames.Count)
		{
			_loadSaveDetailsLabel.Text = string.Empty;
			_loadSelectedSaveButton.Disabled = true;
			_deleteSelectedSaveButton.Disabled = true;
			return;
		}

		SaveGameSlotInfo save = _availableSaveGames[index];
		string locationText = !string.IsNullOrWhiteSpace(save.CurrentMissionTitle)
			? $"Mission: {save.CurrentMissionTitle}"
			: !string.IsNullOrWhiteSpace(save.SavedSystem)
				? $"System: {save.SavedSystem}{(string.IsNullOrWhiteSpace(save.SavedPlanet) ? string.Empty : $" | Planet: {save.SavedPlanet}")}"
				: "Location: Unknown";
		_loadSaveDetailsLabel.Text = $"{locationText}\nTurn: {save.CurrentTurn}\nSaved: {FormatSaveTimestamp(save.SavedAtUtc)}";
		_loadSelectedSaveButton.Disabled = false;
		_deleteSelectedSaveButton.Disabled = false;
	}

	private void LoadSelectedPauseSave()
	{
		if (_globalData == null || _loadSaveList == null)
		{
			return;
		}

		int[] selectedItems = _loadSaveList.GetSelectedItems();
		if (selectedItems.Length == 0)
		{
			_loadSaveStatusLabel.Text = "Select a save first.";
			return;
		}

		int selectedIndex = selectedItems[0];
		if (selectedIndex < 0 || selectedIndex >= _availableSaveGames.Count)
		{
			_loadSaveStatusLabel.Text = "That save could not be found.";
			return;
		}

		SaveGameSlotInfo selectedSave = _availableSaveGames[selectedIndex];
		if (!_globalData.LoadGame(selectedSave.SlotId))
		{
			_loadSaveStatusLabel.Text = "Unable to load that save.";
			return;
		}

		string scenePath = ResolveLoadedScenePath(_globalData, selectedSave);
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(scenePath);
			return;
		}

		GetTree().ChangeSceneToFile(scenePath);
	}

	private void BuildDeleteSaveConfirmationUI(CanvasLayer loadLayer)
	{
		_deleteSaveConfirmWrapper = new CenterContainer();
		_deleteSaveConfirmWrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_deleteSaveConfirmWrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		_deleteSaveConfirmWrapper.Visible = false;
		loadLayer.AddChild(_deleteSaveConfirmWrapper);

		PanelContainer confirmPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(520f, 220f)
		};
		confirmPanel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
		_deleteSaveConfirmWrapper.AddChild(confirmPanel);

		VBoxContainer content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 14);
		confirmPanel.AddChild(content);

		Label title = new Label
		{
			Text = "DELETE SAVE",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		content.AddChild(title);

		_deleteSaveConfirmLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		content.AddChild(_deleteSaveConfirmLabel);

		HBoxContainer buttonRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		buttonRow.AddThemeConstantOverride("separation", 12);
		content.AddChild(buttonRow);

		buttonRow.AddChild(BuildPauseMenuButton("CANCEL", HideDeleteSaveConfirmation, 180f));
		buttonRow.AddChild(BuildPauseMenuButton("DELETE SAVE", ConfirmDeleteSelectedPauseSave, 200f));
	}

	private void PromptDeleteSelectedPauseSave()
	{
		if (_loadSaveList == null || _deleteSaveConfirmWrapper == null || _deleteSaveConfirmLabel == null)
		{
			return;
		}

		int[] selectedItems = _loadSaveList.GetSelectedItems();
		if (selectedItems.Length == 0)
		{
			_loadSaveStatusLabel.Text = "Select a save first.";
			return;
		}

		int selectedIndex = selectedItems[0];
		if (selectedIndex < 0 || selectedIndex >= _availableSaveGames.Count)
		{
			_loadSaveStatusLabel.Text = "That save could not be found.";
			return;
		}

		SaveGameSlotInfo selectedSave = _availableSaveGames[selectedIndex];
		_pendingDeleteSaveSlotId = selectedSave.SlotId;
		_pendingDeleteSaveDisplayName = BuildSaveListLabel(selectedSave);
		_deleteSaveConfirmLabel.Text = $"Delete {_pendingDeleteSaveDisplayName}?\nThis cannot be undone.";
		_deleteSaveConfirmWrapper.Visible = true;
	}

	private void HideDeleteSaveConfirmation()
	{
		_pendingDeleteSaveSlotId = string.Empty;
		_pendingDeleteSaveDisplayName = string.Empty;
		if (_deleteSaveConfirmWrapper != null)
		{
			_deleteSaveConfirmWrapper.Visible = false;
		}
	}

	private void ConfirmDeleteSelectedPauseSave()
	{
		if (_globalData == null || string.IsNullOrWhiteSpace(_pendingDeleteSaveSlotId))
		{
			HideDeleteSaveConfirmation();
			return;
		}

		string deletedSaveName = _pendingDeleteSaveDisplayName;
		bool deleted = _globalData.DeleteSaveGame(_pendingDeleteSaveSlotId);
		HideDeleteSaveConfirmation();
		if (!deleted)
		{
			_loadSaveStatusLabel.Text = "Unable to delete that save.";
			return;
		}

		ShowLoadGameMenu();
		_loadSaveStatusLabel.Text = $"Deleted {deletedSaveName}.";
	}

	private MissionRuntimeSaveData BuildCurrentMissionSaveState()
	{
		return new MissionRuntimeSaveData
		{
			MissionId = GetActiveMissionId(),
			ScenePath = ResolveMissionScenePath(),
			CombatRound = _combatRound,
			CombatActive = _combatActive,
			CombatActiveIndex = _combatActiveIndex,
			FocusedEnemyId = _focusedEnemy?.NpcId ?? string.Empty,
			SelectedOfficerIndex = _selectedOfficerIndex,
			SelectedOfficerIds = _selectedOfficerIds.ToList(),
			SelectedEscortSurvivorPrimaryId = _selectedEscortSurvivorId,
			SelectedEscortSurvivorIds = _selectedEscortSurvivorIds.ToList(),
			ConsumedTriggerKeys = _consumedTriggerKeys.ToList(),
			EngagedEnemyIds = _engagedEnemyIds.ToList(),
			ExploredCells = _exploredCells.Select(Vector2ISaveData.FromVector2I).ToList(),
			Officers = _officerPawns
				.Where(pawn => pawn != null && !string.IsNullOrWhiteSpace(pawn.OfficerID))
				.Select(pawn => new MissionActorSaveData
				{
					ActorId = pawn.OfficerID,
					Cell = Vector2ISaveData.FromVector2I(pawn.CurrentCell),
					CurrentHP = pawn.CurrentHP,
					CurrentShields = pawn.CurrentShields,
					CurrentActions = pawn.CurrentActions,
					ActiveStatusEffectId = pawn.ActiveStatusEffectId,
					IsDead = pawn.IsDead
				})
				.ToList(),
			Npcs = _missionNpcs
				.Where(npc => npc != null && !string.IsNullOrWhiteSpace(npc.NpcId))
				.Select(npc => new MissionActorSaveData
				{
					ActorId = npc.NpcId,
					DefinitionPath = npc.DefinitionResourcePath,
					Cell = Vector2ISaveData.FromVector2I(npc.CurrentCell),
					CurrentHP = npc.CurrentHP,
				CurrentShields = npc.CurrentShields,
				CurrentActions = npc.CurrentActions,
				ActiveStatusEffectId = npc.ActiveStatusEffectId,
				PersonalInventoryItemIDs = npc.PersonalInventoryItemIDs.ToList(),
				EquippedMissionWeaponId = npc.EquippedMissionWeaponId,
				EquippedMissionShieldId = npc.EquippedMissionShieldId,
				OwnedMissionWeaponIds = npc.OwnedMissionWeaponIds.ToList(),
				OwnedMissionShieldIds = npc.OwnedMissionShieldIds.ToList(),
				IsDead = npc.IsDead,
				IsConsumed = npc.IsConsumed,
				IsExtracted = npc.IsExtracted
			})
				.ToList(),
			Props = _missionPropsByCell.Values
				.Where(prop => prop != null && !string.IsNullOrWhiteSpace(prop.PropInstanceId))
				.Distinct()
				.Select(prop => new MissionPropSaveData
				{
					PropInstanceId = prop.PropInstanceId,
					Cell = Vector2ISaveData.FromVector2I(GetPropCell(prop)),
					IsConsumed = prop.IsConsumed,
					RewardOfficerItemIds = GetLootDropRewardItemIds(prop).ToList()
				})
				.ToList(),
			Doors = (_roomBuilder?.GetDoorIds() ?? Enumerable.Empty<string>())
				.Where(doorId => !string.IsNullOrWhiteSpace(doorId))
				.Select(doorId => new MissionDoorSaveData
				{
					DoorId = doorId,
					IsOpen = _roomBuilder.IsDoorOpen(doorId)
				})
				.ToList(),
			CombatQueue = _combatQueue
				.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.CombatantId))
				.Select(entry => new MissionCombatTurnSaveData
				{
					CombatantId = entry.CombatantId,
					IsOfficer = entry.IsOfficer
				})
				.ToList()
		};
	}

	private void RestoreSavedMissionStateIfAvailable()
	{
		MissionRuntimeSaveData saveState = _globalData?.CurrentMissionSaveState;
		if (saveState == null || !IsCompatibleMissionSave(saveState))
		{
			return;
		}

		RestoreDoorStates(saveState);
		RestorePropStates(saveState);
		RestoreOfficerStates(saveState);
		EnsureSavedMissionNpcsExist(saveState);
		RestoreNpcStates(saveState);
		RestoreMissionCollections(saveState);
		RestoreMissionSelection(saveState);
		RestoreMissionCombatState(saveState);
		_pendingInteractionKey = string.Empty;
		_pendingInteractionOfficerId = string.Empty;
		_pendingDoorId = string.Empty;
		_pendingPropInstanceId = string.Empty;
		_pendingNpcId = string.Empty;
		_pendingCombatMoveOfficerId = string.Empty;
		_pendingCombatAttackEnemyId = string.Empty;
		_pendingCombatMoveCost = 0;
		_enemyTurnInProgress = false;
		_pendingStoryProp = null;
		_pendingStoryResult = null;
		_pendingStoryContext = null;
		_pendingMedicalBedProp = null;
		_pendingMedicalBedOfficerId = string.Empty;
		UpdateMovementGridVisibility();
	}

	private bool IsCompatibleMissionSave(MissionRuntimeSaveData saveState)
	{
		return saveState != null
			&& !string.IsNullOrWhiteSpace(saveState.MissionId)
			&& string.Equals(saveState.MissionId, GetActiveMissionId(), StringComparison.Ordinal);
	}

	private void RestoreDoorStates(MissionRuntimeSaveData saveState)
	{
		if (_roomBuilder == null)
		{
			return;
		}

		foreach (MissionDoorSaveData doorState in saveState.Doors ?? new List<MissionDoorSaveData>())
		{
			if (doorState == null || string.IsNullOrWhiteSpace(doorState.DoorId))
			{
				continue;
			}

			_roomBuilder.TrySetDoorOpen(doorState.DoorId, doorState.IsOpen, false);
		}
	}

	private void RestorePropStates(MissionRuntimeSaveData saveState)
	{
		foreach (MissionPropSaveData propState in saveState.Props ?? new List<MissionPropSaveData>())
		{
			if (propState == null || string.IsNullOrWhiteSpace(propState.PropInstanceId))
			{
				continue;
			}

			MissionProp prop = _missionPropsByCell.Values.FirstOrDefault(candidate => candidate != null && candidate.PropInstanceId == propState.PropInstanceId);
			if (prop == null && IsEnemyLootDropPropInstanceId(propState.PropInstanceId) && propState.Cell != null)
			{
				prop = SpawnEnemyLootDropProp(propState.Cell.ToVector2I(), propState.RewardOfficerItemIds, propState.PropInstanceId);
			}

			prop?.ApplySavedConsumptionState(propState.IsConsumed);
		}
	}

	private void RestoreOfficerStates(MissionRuntimeSaveData saveState)
	{
		foreach (MissionActorSaveData officerState in saveState.Officers ?? new List<MissionActorSaveData>())
		{
			if (officerState == null || string.IsNullOrWhiteSpace(officerState.ActorId) || officerState.Cell == null)
			{
				continue;
			}

			OfficerPawn pawn = _officerPawns.FirstOrDefault(candidate => candidate != null && candidate.OfficerID == officerState.ActorId);
			if (pawn == null)
			{
				continue;
			}

			Vector2I cell = officerState.Cell.ToVector2I();
			pawn.SetGridCell(cell, GetMovementCellGlobalPosition(cell));
			pawn.ApplySavedRuntimeState(
				officerState.CurrentHP,
				officerState.CurrentShields,
				officerState.CurrentActions,
				officerState.ActiveStatusEffectId,
				officerState.IsDead);
		}
	}

	private void RestoreNpcStates(MissionRuntimeSaveData saveState)
	{
		foreach (MissionActorSaveData npcState in saveState.Npcs ?? new List<MissionActorSaveData>())
		{
			if (npcState == null || string.IsNullOrWhiteSpace(npcState.ActorId) || npcState.Cell == null)
			{
				continue;
			}

			MissionNpcPawn npc = _missionNpcs.FirstOrDefault(candidate => candidate != null && candidate.NpcId == npcState.ActorId);
			if (npc == null)
			{
				continue;
			}

			Vector2I cell = npcState.Cell.ToVector2I();
			npc.SetGridCell(cell, GetMovementCellGlobalPosition(cell));
			npc.ApplySavedRuntimeState(
				npcState.CurrentHP,
				npcState.CurrentShields,
				npcState.CurrentActions,
				npcState.ActiveStatusEffectId,
				npcState.IsDead,
				npcState.IsConsumed,
				npcState.IsExtracted,
				npcState.PersonalInventoryItemIDs,
				npcState.OwnedMissionWeaponIds,
				npcState.OwnedMissionShieldIds,
				npcState.EquippedMissionWeaponId,
				npcState.EquippedMissionShieldId);
		}

		ReindexMissionNpcCells();
	}

	private void EnsureSavedMissionNpcsExist(MissionRuntimeSaveData saveState)
	{
		foreach (MissionActorSaveData npcState in saveState?.Npcs ?? new List<MissionActorSaveData>())
		{
			if (npcState == null
				|| string.IsNullOrWhiteSpace(npcState.ActorId)
				|| string.IsNullOrWhiteSpace(npcState.DefinitionPath)
				|| _missionNpcs.Any(candidate => candidate != null && candidate.NpcId == npcState.ActorId))
			{
				continue;
			}

			MissionNpcDefinition definition = GD.Load<MissionNpcDefinition>(npcState.DefinitionPath);
			if (definition == null)
			{
				continue;
			}

			MissionNpcPawn spawnedNpc = SpawnRuntimeMissionNpc(definition, npcState.Cell?.ToVector2I() ?? Vector2I.Zero);
			if (spawnedNpc == null)
			{
				continue;
			}

			if (IsEscortSurvivorId(spawnedNpc.NpcId))
			{
				_escortSurvivorIds.Add(spawnedNpc.NpcId);
				if (npcState.IsExtracted)
				{
					_extractedSurvivorIds.Add(spawnedNpc.NpcId);
				}
			}
		}
	}

	private void RestoreMissionCollections(MissionRuntimeSaveData saveState)
	{
		_escortSurvivorIds.Clear();
		_extractedSurvivorIds.Clear();
		foreach (MissionActorSaveData npcState in saveState.Npcs ?? new List<MissionActorSaveData>())
		{
			if (npcState == null || !IsEscortSurvivorId(npcState.ActorId))
			{
				continue;
			}

			_escortSurvivorIds.Add(npcState.ActorId);
			if (npcState.IsExtracted)
			{
				_extractedSurvivorIds.Add(npcState.ActorId);
			}
		}

		_consumedTriggerKeys.Clear();
		foreach (string key in saveState.ConsumedTriggerKeys ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(key))
			{
				_consumedTriggerKeys.Add(key);
			}
		}

		_engagedEnemyIds.Clear();
		foreach (string enemyId in saveState.EngagedEnemyIds ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(enemyId))
			{
				_engagedEnemyIds.Add(enemyId);
			}
		}

		_exploredCells.Clear();
		foreach (Vector2ISaveData exploredCell in saveState.ExploredCells ?? new List<Vector2ISaveData>())
		{
			if (exploredCell != null)
			{
				_exploredCells.Add(exploredCell.ToVector2I());
			}
		}
	}

	private void RestoreMissionSelection(MissionRuntimeSaveData saveState)
	{
		_selectedOfficerIndex = Mathf.Clamp(saveState.SelectedOfficerIndex, 0, Mathf.Max(0, _officerPawns.Count - 1));
		_selectedOfficerIds.Clear();
		_selectedEscortSurvivorIds.Clear();
		foreach (string officerId in saveState.SelectedOfficerIds ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(officerId)
				&& _officerPawns.Any(pawn => pawn != null && !pawn.IsDead && pawn.OfficerID == officerId))
			{
				_selectedOfficerIds.Add(officerId);
			}
		}

		foreach (string survivorId in saveState.SelectedEscortSurvivorIds ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(survivorId)
				&& GetAliveEscortSurvivors().Any(survivor => survivor != null && survivor.NpcId == survivorId))
			{
				_selectedEscortSurvivorIds.Add(survivorId);
			}
		}

		_selectedEscortSurvivorId = !string.IsNullOrWhiteSpace(saveState.SelectedEscortSurvivorPrimaryId)
			&& _selectedEscortSurvivorIds.Contains(saveState.SelectedEscortSurvivorPrimaryId)
			? saveState.SelectedEscortSurvivorPrimaryId
			: string.Empty;
		_explorationPartyMovementEnabled = (_selectedOfficerIds.Count + _selectedEscortSurvivorIds.Count) > 1;
		NormalizeFriendlySelectionState();
		UpdateOfficerSelectionVisuals();
	}

	private void RestoreMissionCombatState(MissionRuntimeSaveData saveState)
	{
		_combatRound = Mathf.Max(1, saveState.CombatRound);
		_combatActive = saveState.CombatActive && _engagedEnemyIds.Count > 0 && GetAliveOfficers().Any();
		_focusedEnemy = _missionNpcs.FirstOrDefault(npc => npc != null && npc.NpcId == saveState.FocusedEnemyId && !npc.IsDead);
		_combatQueue.Clear();
		foreach (MissionCombatTurnSaveData turnState in saveState.CombatQueue ?? new List<MissionCombatTurnSaveData>())
		{
			if (turnState == null || string.IsNullOrWhiteSpace(turnState.CombatantId))
			{
				continue;
			}

			if (turnState.IsOfficer)
			{
				OfficerPawn officer = _officerPawns.FirstOrDefault(candidate => candidate != null && candidate.OfficerID == turnState.CombatantId && !candidate.IsDead);
				if (officer != null)
				{
					_combatQueue.Add(new MissionCombatTurnEntry
					{
						CombatantId = officer.OfficerID,
						IsOfficer = true,
						InitiativeScore = 0,
						Officer = officer
					});
				}
				continue;
			}

			MissionNpcPawn enemy = _missionNpcs.FirstOrDefault(candidate => candidate != null && candidate.NpcId == turnState.CombatantId && !candidate.IsDead && !candidate.IsExtracted);
			if (enemy != null)
			{
				_combatQueue.Add(new MissionCombatTurnEntry
				{
					CombatantId = enemy.NpcId,
					IsOfficer = false,
					InitiativeScore = 0,
					Enemy = enemy
				});
			}
		}

		if (_combatActive && _combatQueue.Count == 0)
		{
			RebuildCombatQueue();
		}

		_combatActiveIndex = _combatActive
			? Mathf.Clamp(saveState.CombatActiveIndex, -1, _combatQueue.Count - 1)
			: -1;
		if (!_combatActive)
		{
			_combatQueue.Clear();
		}
	}

	private string ResolveMissionScenePath()
	{
		if (!string.IsNullOrWhiteSpace(_missionState?.ScenePath))
		{
			return _missionState.ScenePath;
		}

		if (GetTree()?.CurrentScene != null && !string.IsNullOrWhiteSpace(GetTree().CurrentScene.SceneFilePath))
		{
			return GetTree().CurrentScene.SceneFilePath;
		}

		return "res://black_site_relay.tscn";
	}
}

