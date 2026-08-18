using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private void EnsureSelectionBox()
	{
		_selectionBox = GetNodeOrNull<SelectionBox>("SelectionBox");
		if (_selectionBox != null)
		{
			return;
		}

		_selectionBox = new SelectionBox
		{
			Name = "SelectionBox",
			ZIndex = 200
		};
		AddChild(_selectionBox);
	}

	private void SelectOfficer(int index)
	{
		if (_officerPawns.Count == 0)
		{
			return;
		}

		_selectedOfficerIndex = Mathf.Clamp(index, 0, _officerPawns.Count - 1);
		if (_officerPawns[_selectedOfficerIndex]?.IsDead == true)
		{
			int livingIndex = _officerPawns.FindIndex(pawn => pawn != null && !pawn.IsDead);
			if (livingIndex >= 0)
			{
				_selectedOfficerIndex = livingIndex;
			}
		}

		OfficerPawn selectedOfficer = _officerPawns[_selectedOfficerIndex];
		if (selectedOfficer == null || selectedOfficer.IsDead || string.IsNullOrWhiteSpace(selectedOfficer.OfficerID))
		{
			return;
		}

		SetSelectedFriendlyUnits(new[] { selectedOfficer }, null, selectedOfficer);
	}

	private void SelectEntireExplorationParty(object primaryUnit = null)
	{
		List<OfficerPawn> officers = GetAliveOfficers().ToList();
		List<MissionNpcPawn> survivors = GetAliveEscortSurvivors().ToList();
		if (officers.Count == 0 && survivors.Count == 0)
		{
			return;
		}

		object resolvedPrimary = primaryUnit switch
		{
			OfficerPawn officer when officers.Contains(officer) => officer,
			MissionNpcPawn survivor when survivors.Contains(survivor) => survivor,
			_ => GetPrimarySelectedFriendlyUnit() switch
			{
				OfficerPawn officer when officers.Contains(officer) => officer,
				MissionNpcPawn survivor when survivors.Contains(survivor) => survivor,
				_ => officers.Cast<object>().Concat(survivors).FirstOrDefault()
			}
		};

		SetSelectedFriendlyUnits(officers, survivors, resolvedPrimary);
	}

	private void SetExplorationMovementMode(bool partyModeEnabled, OfficerPawn preferredOfficer = null)
	{
		_explorationPartyMovementEnabled = partyModeEnabled;
		if (_combatActive)
		{
			RefreshCombatHud();
			return;
		}

		if (partyModeEnabled)
		{
			SelectEntireExplorationParty(preferredOfficer);
			return;
		}

		OfficerPawn officer = preferredOfficer ?? GetSelectedOfficer() ?? GetAliveOfficers().FirstOrDefault();
		if (officer == null)
		{
			return;
		}

		int officerIndex = _officerPawns.IndexOf(officer);
		if (officerIndex >= 0)
		{
			SelectOfficer(officerIndex);
		}
	}

	private void SetSelectedOfficers(IEnumerable<OfficerPawn> officers, OfficerPawn primaryOfficer = null)
	{
		SetSelectedFriendlyUnits(officers, null, primaryOfficer);
	}

	private void SetSelectedFriendlyUnits(IEnumerable<OfficerPawn> officers, IEnumerable<MissionNpcPawn> survivors, object primaryUnit = null)
	{
		List<OfficerPawn> selectedOfficers = officers?
			.Where(officer => officer != null && !officer.IsDead && !string.IsNullOrWhiteSpace(officer.OfficerID))
			.Distinct()
			.ToList() ?? new List<OfficerPawn>();
		List<MissionNpcPawn> selectedSurvivors = survivors?
			.Where(survivor => IsActiveEscortSurvivor(survivor) && !string.IsNullOrWhiteSpace(survivor.NpcId))
			.Distinct()
			.ToList() ?? new List<MissionNpcPawn>();
		if (selectedOfficers.Count == 0 && selectedSurvivors.Count == 0)
		{
			return;
		}

		_selectedOfficerIds.Clear();
		_selectedEscortSurvivorIds.Clear();
		foreach (OfficerPawn officer in selectedOfficers)
		{
			_selectedOfficerIds.Add(officer.OfficerID);
		}

		foreach (MissionNpcPawn survivor in selectedSurvivors)
		{
			_selectedEscortSurvivorIds.Add(survivor.NpcId);
		}

		_selectedEscortSurvivorId = string.Empty;
		switch (primaryUnit)
		{
			case OfficerPawn primaryOfficer when _selectedOfficerIds.Contains(primaryOfficer.OfficerID):
			{
				int primaryIndex = _officerPawns.IndexOf(primaryOfficer);
				if (primaryIndex >= 0)
				{
					_selectedOfficerIndex = primaryIndex;
				}

				break;
			}
			case MissionNpcPawn primarySurvivor when _selectedEscortSurvivorIds.Contains(primarySurvivor.NpcId):
				_selectedEscortSurvivorId = primarySurvivor.NpcId;
				break;
		}

		if (!_combatActive)
		{
			_explorationPartyMovementEnabled = (_selectedOfficerIds.Count + _selectedEscortSurvivorIds.Count) > 1;
		}

		NormalizeFriendlySelectionState();
		UpdateOfficerSelectionVisuals();
		UpdateSelectedOfficerDisplay();
		RefreshCombatHud();
	}

	private void NormalizeFriendlySelectionState()
	{
		_selectedOfficerIds.RemoveWhere(officerId => string.IsNullOrWhiteSpace(officerId)
			|| !_officerPawns.Any(pawn => pawn != null && !pawn.IsDead && pawn.OfficerID == officerId));
		_selectedEscortSurvivorIds.RemoveWhere(survivorId => string.IsNullOrWhiteSpace(survivorId)
			|| !GetAliveEscortSurvivors().Any(survivor => survivor != null && survivor.NpcId == survivorId));

		if (!string.IsNullOrWhiteSpace(_selectedEscortSurvivorId) && !_selectedEscortSurvivorIds.Contains(_selectedEscortSurvivorId))
		{
			_selectedEscortSurvivorId = string.Empty;
		}

		OfficerPawn indexedOfficer = GetSelectedOfficer();
		if (_selectedOfficerIds.Count == 0 && _selectedEscortSurvivorIds.Count == 0)
		{
			OfficerPawn fallbackOfficer = indexedOfficer ?? _officerPawns.FirstOrDefault(pawn => pawn != null && !pawn.IsDead);
			if (fallbackOfficer != null)
			{
				_selectedOfficerIndex = _officerPawns.IndexOf(fallbackOfficer);
				_selectedOfficerIds.Add(fallbackOfficer.OfficerID);
				return;
			}

			MissionNpcPawn fallbackSurvivor = GetAliveEscortSurvivors().FirstOrDefault();
			if (fallbackSurvivor != null)
			{
				_selectedEscortSurvivorIds.Add(fallbackSurvivor.NpcId);
				_selectedEscortSurvivorId = fallbackSurvivor.NpcId;
			}

			return;
		}

		if (!string.IsNullOrWhiteSpace(_selectedEscortSurvivorId))
		{
			return;
		}

		OfficerPawn selectedOfficer = indexedOfficer;
		if (selectedOfficer != null && _selectedOfficerIds.Contains(selectedOfficer.OfficerID))
		{
			return;
		}

		OfficerPawn firstSelectedOfficer = _officerPawns.FirstOrDefault(pawn => pawn != null && !pawn.IsDead && _selectedOfficerIds.Contains(pawn.OfficerID));
		if (firstSelectedOfficer != null)
		{
			_selectedOfficerIndex = _officerPawns.IndexOf(firstSelectedOfficer);
			return;
		}

		MissionNpcPawn firstSelectedSurvivor = GetAliveEscortSurvivors()
			.FirstOrDefault(survivor => !string.IsNullOrWhiteSpace(survivor.NpcId) && _selectedEscortSurvivorIds.Contains(survivor.NpcId));
		if (firstSelectedSurvivor != null)
		{
			_selectedEscortSurvivorId = firstSelectedSurvivor.NpcId;
		}
	}

	private void UpdateOfficerSelectionVisuals()
	{
		for (int i = 0; i < _officerPawns.Count; i++)
		{
			OfficerPawn officer = _officerPawns[i];
			if (officer != null)
			{
				bool isSelected = !officer.IsDead
					&& !string.IsNullOrWhiteSpace(officer.OfficerID)
					&& _selectedOfficerIds.Contains(officer.OfficerID);
				officer.SetSelected(isSelected);
			}
		}

		foreach (MissionNpcPawn npc in _missionNpcs.Where(candidate => candidate != null))
		{
			bool isSelected = IsActiveEscortSurvivor(npc)
				&& !string.IsNullOrWhiteSpace(npc.NpcId)
				&& _selectedEscortSurvivorIds.Contains(npc.NpcId);
			npc.SetSelected(isSelected);
		}
	}

	private void CycleOfficerSelection()
	{
		List<object> controllableUnits = GetControllableUnitsForSelection();
		if (controllableUnits.Count == 0)
		{
			return;
		}

		int currentIndex = GetCurrentSelectionIndex(controllableUnits);
		int nextIndex = (currentIndex + 1 + controllableUnits.Count) % controllableUnits.Count;
		SelectFriendlyUnit(controllableUnits[nextIndex]);
	}

	private OfficerPawn GetSelectedOfficer()
	{
		if (_selectedOfficerIndex < 0 || _selectedOfficerIndex >= _officerPawns.Count)
		{
			return null;
		}

		OfficerPawn pawn = _officerPawns[_selectedOfficerIndex];
		return pawn != null && !pawn.IsDead ? pawn : null;
	}

	private List<OfficerPawn> GetSelectedOfficers()
	{
		List<OfficerPawn> selected = _officerPawns
			.Where(pawn => pawn != null && !pawn.IsDead && !string.IsNullOrWhiteSpace(pawn.OfficerID) && _selectedOfficerIds.Contains(pawn.OfficerID))
			.ToList();
		if (selected.Count > 0)
		{
			return selected;
		}

		OfficerPawn activeOfficer = GetSelectedOfficer();
		if (activeOfficer == null || _selectedEscortSurvivorIds.Count > 0)
		{
			return new List<OfficerPawn>();
		}

		return new List<OfficerPawn> { activeOfficer };
	}

	private void SelectEscortSurvivor(MissionNpcPawn survivor)
	{
		if (!IsActiveEscortSurvivor(survivor) || string.IsNullOrWhiteSpace(survivor.NpcId))
		{
			return;
		}

		SetSelectedFriendlyUnits(null, new[] { survivor }, survivor);
	}

	private MissionNpcPawn GetSelectedEscortSurvivor()
	{
		if (string.IsNullOrWhiteSpace(_selectedEscortSurvivorId))
		{
			return null;
		}

		MissionNpcPawn survivor = _missionNpcs.FirstOrDefault(candidate => candidate != null && candidate.NpcId == _selectedEscortSurvivorId);
		return IsActiveEscortSurvivor(survivor) ? survivor : null;
	}

	private List<MissionNpcPawn> GetSelectedEscortSurvivors()
	{
		List<MissionNpcPawn> selected = GetAliveEscortSurvivors()
			.Where(survivor => !string.IsNullOrWhiteSpace(survivor.NpcId) && _selectedEscortSurvivorIds.Contains(survivor.NpcId))
			.ToList();
		if (selected.Count > 0)
		{
			return selected;
		}

		MissionNpcPawn primarySurvivor = GetSelectedEscortSurvivor();
		return primarySurvivor != null ? new List<MissionNpcPawn> { primarySurvivor } : new List<MissionNpcPawn>();
	}

	private object GetPrimarySelectedFriendlyUnit()
	{
		MissionNpcPawn selectedSurvivor = GetSelectedEscortSurvivor();
		if (selectedSurvivor != null)
		{
			return selectedSurvivor;
		}

		return GetSelectedOfficer();
	}

	private List<object> GetSelectedFriendlyUnits()
	{
		List<object> selectedUnits = GetControllableUnitsForSelection()
			.Where(unit => unit switch
			{
				OfficerPawn officer => !string.IsNullOrWhiteSpace(officer.OfficerID) && _selectedOfficerIds.Contains(officer.OfficerID),
				MissionNpcPawn survivor => !string.IsNullOrWhiteSpace(survivor.NpcId) && _selectedEscortSurvivorIds.Contains(survivor.NpcId),
				_ => false
			})
			.ToList();
		object primaryUnit = GetPrimarySelectedFriendlyUnit();
		if (primaryUnit != null && selectedUnits.Remove(primaryUnit))
		{
			selectedUnits.Insert(0, primaryUnit);
		}

		if (selectedUnits.Count > 0)
		{
			return selectedUnits;
		}

		return primaryUnit != null ? new List<object> { primaryUnit } : new List<object>();
	}

	private List<MissionCombatantSummary> GetSelectedFriendlySummaries()
	{
		return GetSelectedFriendlyUnits()
			.Select(unit => unit switch
			{
				OfficerPawn officer => BuildOfficerSummary(officer),
				MissionNpcPawn survivor => BuildEnemySummary(survivor),
				_ => null
			})
			.Where(summary => summary != null)
			.ToList();
	}

	private List<MissionCombatantSummary> GetExplorationDetailSummaries()
	{
		if (_explorationPartyMovementEnabled)
		{
			return new List<MissionCombatantSummary>();
		}

		object primaryUnit = GetPrimarySelectedFriendlyUnit();
		MissionCombatantSummary summary = primaryUnit switch
		{
			OfficerPawn officer => BuildOfficerSummary(officer),
			MissionNpcPawn survivor => BuildEnemySummary(survivor),
			_ => null
		};

		return summary != null ? new List<MissionCombatantSummary> { summary } : new List<MissionCombatantSummary>();
	}

	private List<MissionExplorationOfficerOption> BuildExplorationOfficerOptions()
	{
		object primaryUnit = GetPrimarySelectedFriendlyUnit();
		List<MissionExplorationOfficerOption> options = GetAliveOfficers()
			.Select(officer => new MissionExplorationOfficerOption
			{
				OfficerId = $"officer:{officer.OfficerID}",
				DisplayName = officer.OfficerName,
				Subtitle = officer.Specialty,
				Icon = LoadPortraitTexture(officer.PortraitPath),
				Selected = officer == primaryUnit,
				CanInspectInventory = true
			})
			.ToList();

		options.AddRange(GetAliveEscortSurvivors()
			.OrderBy(survivor => survivor.DisplayName)
			.Select(survivor => new MissionExplorationOfficerOption
			{
				OfficerId = $"survivor:{survivor.NpcId}",
				DisplayName = survivor.DisplayName,
				Subtitle = "Rescued Survivor",
				Icon = LoadPortraitTexture(survivor.PortraitPath),
				Selected = survivor == primaryUnit,
				CanInspectInventory = true
			}));

		return options
			.ToList();
	}

	private List<object> GetControllableUnitsForSelection()
	{
		List<object> units = new List<object>();
		units.AddRange(GetAliveOfficers().Cast<object>());
		units.AddRange(GetAliveEscortSurvivors().OrderBy(npc => npc.DisplayName).Cast<object>());
		return units;
	}

	private int GetCurrentSelectionIndex(List<object> units)
	{
		object selectedUnit = GetPrimarySelectedFriendlyUnit();
		return selectedUnit != null ? units.IndexOf(selectedUnit) : -1;
	}

	private void SelectFriendlyUnit(object unit)
	{
		switch (unit)
		{
			case OfficerPawn officer:
			{
				int officerIndex = _officerPawns.IndexOf(officer);
				if (officerIndex >= 0)
				{
					SelectOfficer(officerIndex);
				}

				break;
			}
			case MissionNpcPawn survivor:
				SelectEscortSurvivor(survivor);
				break;
		}
	}

	private object ResolveExplorationUnit(string unitId)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			return null;
		}

		if (unitId.StartsWith("officer:", StringComparison.Ordinal))
		{
			string resolvedOfficerId = unitId["officer:".Length..];
			return _officerPawns.FirstOrDefault(candidate =>
				candidate != null
				&& !candidate.IsDead
				&& string.Equals(candidate.OfficerID, resolvedOfficerId, StringComparison.Ordinal));
		}

		if (unitId.StartsWith("survivor:", StringComparison.Ordinal))
		{
			string survivorNpcId = unitId["survivor:".Length..];
			return _missionNpcs.FirstOrDefault(candidate =>
				candidate != null
				&& string.Equals(candidate.NpcId, survivorNpcId, StringComparison.Ordinal)
				&& IsActiveEscortSurvivor(candidate));
		}

		return _officerPawns.FirstOrDefault(candidate =>
			candidate != null
			&& !candidate.IsDead
			&& string.Equals(candidate.OfficerID, unitId, StringComparison.Ordinal));
	}

	private void UpdateSelectedOfficerDisplay()
	{
		if (_missionUi == null)
		{
			return;
		}

		_missionUi.SetExplorationControlPanel(
			_explorationPartyMovementEnabled
				? "Move orders will guide the whole away team together."
				: "Move orders and interactions apply to the selected unit only.",
			_explorationPartyMovementEnabled,
			BuildExplorationOfficerOptions(),
			!_combatActive && !_missionGameOver);

		List<object> selectedUnits = GetSelectedFriendlyUnits();
		_missionUi.SetExplorationSelectionInfo(GetExplorationDetailSummaries(), !_combatActive && !_missionGameOver && !_explorationPartyMovementEnabled);
		if (selectedUnits.Count == 0)
		{
			_missionUi.SetExplorationSelectionInfo(Array.Empty<MissionCombatantSummary>(), false);
			return;
		}

		object primaryUnit = selectedUnits[0];
		if (selectedUnits.Count > 1)
		{
			switch (primaryUnit)
			{
				case OfficerPawn officer:
					_missionUi.SetSelectedOfficer(
						$"{officer.OfficerName} (+{selectedUnits.Count - 1})",
						$"{selectedUnits.Count} UNITS SELECTED",
						officer.Specialty);
					return;
				case MissionNpcPawn survivor:
					_missionUi.SetSelectedOfficer(
						$"{survivor.DisplayName} (+{selectedUnits.Count - 1})",
						$"{selectedUnits.Count} UNITS SELECTED",
						"GUIDE TO EVAC");
					return;
			}
		}

		switch (primaryUnit)
		{
			case MissionNpcPawn survivor:
				_missionUi.SetSelectedOfficer(survivor.DisplayName, $"ESCORT {CampaignText.RemnantsLabel.ToUpperInvariant()}", "GUIDE TO EVAC");
				return;
			case OfficerPawn officer:
				_missionUi.SetSelectedOfficer(officer.OfficerName, officer.ShipName, officer.Specialty);
				return;
		}

		_missionUi.SetExplorationSelectionInfo(Array.Empty<MissionCombatantSummary>(), false);
	}

	private MissionOutcome BuildOutcome(string outcomeId)
	{
		string missionId = GetActiveMissionId();
		MissionOutcome outcome = new MissionOutcome
		{
			MissionID = missionId,
			OutcomeID = outcomeId,
			IsSuccess = true
		};
		outcome.FallenOfficerShipNames = _officerPawns
			.Where(pawn => pawn != null && pawn.IsDead)
			.Select(pawn => pawn.ShipName)
			.Where(shipName => !string.IsNullOrWhiteSpace(shipName))
			.Distinct()
			.ToList();

		List<OfficerState> officers = (_missionState?.ParticipatingShipNames ?? new List<string>())
			.Select(shipName => _globalData?.ShipOfficers != null && _globalData.ShipOfficers.TryGetValue(shipName, out OfficerState officer) ? officer : null)
			.Where(officer => officer != null)
			.ToList();

		if (missionId == "outpost_smuggler_exchange")
		{
			if (outcomeId == "deal_cut")
			{
				outcome.Reward.RawMaterials = 35;
				outcome.Reward.EnergyCores = 1;
				outcome.Reward.AncientTech = 2;
				outcome.FlagsToSet.Add("smuggler_exchange_deal_cut");
				outcome.Reward.CodexEntryIds.Add("broker_contract_terms");

				foreach (OfficerState officer in officers)
				{
					int delta = 0;
					if (officer.Ideology == "TechnoReclamation") delta += 1;
					if (officer.Archetype == "Pragmatist") delta += 1;
					if (officer.Archetype == "Scholar") delta += 1;
					if (officer.Ideology == "Humanitarian") delta -= 1;
					if (delta != 0) outcome.ApprovalChanges[officer.OfficerID] = delta;
				}
			}
			else
			{
				outcome.Reward.RawMaterials = 90;
				outcome.Reward.EnergyCores = 2;
				outcome.FlagsToSet.Add("smuggler_exchange_contraband_seized");
				outcome.Reward.CodexEntryIds.Add("smuggler_seizure_report");

				foreach (OfficerState officer in officers)
				{
					int delta = 0;
					if (officer.Archetype == "Pragmatist") delta += 1;
					if (officer.Specialty == "Security") delta += 1;
					if (officer.Ideology == "Humanitarian") delta -= 1;
					if (officer.Archetype == "Idealist") delta -= 1;
					if (delta != 0) outcome.ApprovalChanges[officer.OfficerID] = delta;
				}
			}

			return outcome;
		}

		if (outcomeId == "survivors_saved")
		{
			outcome.Reward.RawMaterials = 70;
			outcome.Reward.EnergyCores = 1;
			outcome.PopulationSaved = GetExtractedEscortSurvivorCount();
			outcome.RescuedRemnants = BuildRescuedRemnantRecords();
			outcome.FlagsToSet.Add("relay_survivors_saved");
			outcome.Reward.CodexEntryIds.Add("relay_survivor_registry");

			foreach (OfficerState officer in officers)
			{
				int delta = 0;
				if (officer.Ideology == "Humanitarian") delta += 2;
				if (officer.Archetype == "Idealist") delta += 1;
				if (officer.Specialty == "Medical Triage") delta += 1;
				if (officer.Ideology == "TechnoReclamation") delta -= 2;
				if (officer.Archetype == "Scholar") delta -= 1;
				if (delta != 0) outcome.ApprovalChanges[officer.OfficerID] = delta;
			}
		}
		else
		{
			outcome.Reward.EnergyCores = 2;
			outcome.Reward.AncientTech = 2;
			outcome.FlagsToSet.Add("relay_archive_secured");
			outcome.Reward.FleetItemIds.Add("custodian_archive_shard");
			outcome.Reward.CodexEntryIds.Add("custodian_archive_shard");

			foreach (OfficerState officer in officers)
			{
				int delta = 0;
				if (officer.Ideology == "TechnoReclamation") delta += 2;
				if (officer.Archetype == "Scholar") delta += 1;
				if (officer.Archetype == "Pragmatist") delta += 1;
				if (officer.Ideology == "Humanitarian") delta -= 2;
				if (officer.Archetype == "Idealist") delta -= 1;
				if (delta != 0) outcome.ApprovalChanges[officer.OfficerID] = delta;
			}
		}

		return outcome;
	}

	private List<RemnantRecord> BuildRescuedRemnantRecords()
	{
		string missionId = GetActiveMissionId();
		string missionTitle = !string.IsNullOrWhiteSpace(_missionTemplate?.Title)
			? _missionTemplate.Title
			: _missionState?.MissionTitle ?? missionId;
		return _missionNpcs
			.Where(npc => npc != null
				&& IsEscortSurvivor(npc)
				&& !string.IsNullOrWhiteSpace(npc.NpcId)
				&& _extractedSurvivorIds.Contains(npc.NpcId))
			.OrderBy(npc => npc.DisplayName)
			.Select(npc => new RemnantRecord
			{
				RecordId = $"{missionId}:{npc.NpcId}",
				DisplayName = npc.DisplayName,
				Description = string.IsNullOrWhiteSpace(npc.Description) ? "Recovered civilian from a completed away mission." : npc.Description,
				Notes = npc.Notes,
				MissionId = missionId,
				MissionTitle = missionTitle,
				RescuedOnTurn = Mathf.Max(1, _globalData?.CurrentTurn ?? 1),
				PortraitPath = npc.PortraitPath,
				DefinitionPath = npc.DefinitionResourcePath,
				PersonalInventoryItemIDs = npc.PersonalInventoryItemIDs.ToList()
			})
			.ToList();
	}

	private string GetActiveMissionId()
	{
		return string.IsNullOrEmpty(_missionState?.MissionID) ? DefaultMissionId : _missionState.MissionID;
	}

	private void ApplyMissionTemplateToRoomBuilder()
	{
		if (_roomBuilder == null)
		{
			return;
		}

		if (MissionWorkbenchPlaytestSession.TryConsumeLayoutOverride(GetActiveMissionId(), out string playtestLayoutPath))
		{
			_roomBuilder.LayoutResourcePath = playtestLayoutPath;
			return;
		}

		_roomBuilder.LayoutResourcePath = _missionTemplate?.LayoutResourcePath?.Trim() ?? string.Empty;
	}

	private string GetPrimaryOutcomeId()
	{
		return string.IsNullOrWhiteSpace(_missionTemplate?.PrimaryOutcomeId) ? "survivors_saved" : _missionTemplate.PrimaryOutcomeId;
	}

	private string GetSecondaryOutcomeId()
	{
		return string.IsNullOrWhiteSpace(_missionTemplate?.SecondaryOutcomeId) ? "archive_secured" : _missionTemplate.SecondaryOutcomeId;
	}

	private string GetMissionObjectiveText()
	{
		return string.IsNullOrWhiteSpace(_missionTemplate?.ObjectiveText)
			? "OBJECTIVE: Investigate the relay, assess the survivors, and decide what to save."
			: _missionTemplate.ObjectiveText;
	}

	private string GetBaseMissionPromptText()
	{
		string prompt = string.IsNullOrWhiteSpace(_missionTemplate?.PromptText)
			? "Controls: left click an officer to select, left click a floor cell to move, WASD to step the selected officer, TAB or 1-2 to switch officers, middle mouse drag or screen-edge hover to pan, mouse wheel or +/- to zoom."
			: _missionTemplate.PromptText;
		if (GetAliveEscortSurvivors().Any())
		{
			prompt += $" Rescued {CampaignText.RemnantsLabel.ToLowerInvariant()} can be selected and moved toward evac just like the away team.";
		}

		return prompt;
	}

	private string GetMissionPromptText()
	{
		string basePrompt = GetBaseMissionPromptText();
		if (_combatActive)
		{
			return $"{basePrompt} Combat is active: {GetCombatActionPromptText(GetActiveCombatOfficer())} Use END TURN when that unit is done.";
		}

		List<MissionExtractionOption> extractionOptions = GetAvailableExtractionOptions();
		bool allOfficersOnEvac = AreAllOfficersOnEvacZone();
		if (!allOfficersOnEvac)
		{
			return $"{basePrompt} Complete a valid mission path, then rally every surviving officer on the evac zone to extract.";
		}

		if (extractionOptions.Count == 0)
		{
			return $"{basePrompt} Your officers are assembled at evac, but no mission outcome is ready yet.";
		}

		if (extractionOptions.Count == 1)
		{
			return $"{basePrompt} All officers are on the evac zone. Extraction is ready for {extractionOptions[0].DisplayText.ToUpper()}.";
		}

		return $"{basePrompt} All officers are on the evac zone. Multiple extraction outcomes are available; choose how the mission resolves.";
	}

	private void RefreshMissionPrompt()
	{
		if (_missionUi?.PromptLabel != null)
		{
			_missionUi.PromptLabel.Text = GetMissionPromptText();
		}
	}

	private bool IsOutcomeReady(string outcomeId)
	{
		if (outcomeId == GetPrimaryOutcomeId())
		{
			return AreRequiredFlagsSatisfied(_missionTemplate?.PrimaryOutcomeRequiredFlags)
				&& GetExtractedEscortSurvivorCount() > 0
				&& !GetAliveEscortSurvivors().Any()
				&& AreBlockedFlagsClear(_missionTemplate?.PrimaryOutcomeBlockedFlags);
		}

		if (outcomeId == GetSecondaryOutcomeId())
		{
			return AreRequiredFlagsSatisfied(_missionTemplate?.SecondaryOutcomeRequiredFlags)
				&& AreBlockedFlagsClear(_missionTemplate?.SecondaryOutcomeBlockedFlags);
		}

		return false;
	}

	private string GetOutcomeDisplayName(string outcomeId)
	{
		if (outcomeId == GetPrimaryOutcomeId())
		{
			return string.IsNullOrWhiteSpace(_missionTemplate?.PrimaryActionText) ? "SAVE SURVIVORS" : _missionTemplate.PrimaryActionText;
		}

		if (outcomeId == GetSecondaryOutcomeId())
		{
			return string.IsNullOrWhiteSpace(_missionTemplate?.SecondaryActionText) ? "SECURE ARCHIVE" : _missionTemplate.SecondaryActionText;
		}

		return outcomeId;
	}

	private void OnExtractionOutcomeChosen(string outcomeId)
	{
		if (string.IsNullOrWhiteSpace(outcomeId) || !IsOutcomeReady(outcomeId) || !AreAllOfficersOnEvacZone())
		{
			return;
		}

		CompleteMission(BuildOutcome(outcomeId));
	}

	private void CompleteMission(MissionOutcome outcome)
	{
		_missionService?.ApplyOutcome(outcome);
		_missionService?.ReturnToMissionSource(this);
	}

	private void ReturnWithoutOutcome()
	{
		_globalData?.ClearCurrentMissionState();
		_missionService?.ReturnToMissionSource(this);
	}
}
