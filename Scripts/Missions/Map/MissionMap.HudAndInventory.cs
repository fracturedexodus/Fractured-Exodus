using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class MissionMap
{
	private void RefreshCombatHud()
	{
		if (_missionUi == null)
		{
			return;
		}

		bool showCombatHud = _combatActive && !_missionGameOver;
		_missionUi.SetCombatHudVisible(showCombatHud);
		if (!showCombatHud)
		{
			_missionUi.SetCombatEndTurnEnabled(false, false);
			_missionUi.SetCombatActionPanel(string.Empty, string.Empty, Array.Empty<MissionCombatActionOption>(), Array.Empty<MissionCombatWeaponOption>(), false);
			_missionUi.SetExplorationControlPanel(
				_explorationPartyMovementEnabled
					? "Move orders will guide the whole away team together."
					: "Move orders and interactions apply to the selected unit only.",
				_explorationPartyMovementEnabled,
				BuildExplorationOfficerOptions(),
				!_missionGameOver);
			_missionUi.SetPlayerCombatInfo(null);
			_missionUi.SetEnemyCombatInfo(null);
			_missionUi.SetExplorationSelectionInfo(GetExplorationDetailSummaries(), !_missionGameOver && !_explorationPartyMovementEnabled);
			_missionUi.SetCombatTurnLabel("MISSION COMBAT");
			_missionUi.SetCombatInitiative(Array.Empty<MissionCombatantSummary>(), -1);
			RefreshMissionPrompt();
			return;
		}

		MissionCombatTurnEntry activeEntry = GetActiveCombatTurnEntry();
		string turnLabel = activeEntry == null
			? $"ROUND {_combatRound}"
			: activeEntry.IsOfficer
				? $"ROUND {_combatRound} - {activeEntry.Officer.OfficerName.ToUpperInvariant()} TURN"
				: $"ROUND {_combatRound} - {activeEntry.Enemy.DisplayName.ToUpperInvariant()} TURN";
		_missionUi.SetCombatTurnLabel(turnLabel);
		_missionUi.SetCombatInitiative(_combatQueue
			.Select(entry => entry.IsOfficer ? BuildOfficerSummary(entry.Officer) : BuildEnemySummary(entry.Enemy))
			.ToList(), _combatActiveIndex);

		MissionNpcPawn playerSurvivor = GetActiveCombatEscortSurvivor() ?? GetSelectedEscortSurvivor();
		OfficerPawn playerOfficer = playerSurvivor == null ? (GetActiveCombatOfficer() ?? GetSelectedOfficer()) : null;
		Vector2I playerCell = playerOfficer?.CurrentCell ?? playerSurvivor?.CurrentCell ?? Vector2I.Zero;
		MissionNpcPawn enemyFocus = GetActiveCombatEnemy() ?? (_focusedEnemy != null && !_focusedEnemy.IsDead ? _focusedEnemy : GetClosestVisibleEnemy(playerCell));
		_missionUi.SetPlayerCombatInfo(playerOfficer != null ? BuildOfficerSummary(playerOfficer) : BuildEnemySummary(playerSurvivor));
		_missionUi.SetEnemyCombatInfo(BuildEnemySummary(enemyFocus));
		bool canPlayerEndTurn = _combatActive && !_enemyTurnInProgress && (playerOfficer != null || playerSurvivor != null);
		_missionUi.SetCombatEndTurnEnabled(canPlayerEndTurn, _combatActive);
		bool showActionPanel = playerOfficer != null && playerOfficer == GetActiveCombatOfficer() && !_enemyTurnInProgress;
		_missionUi.SetCombatActionPanel(
			playerOfficer != null ? $"{playerOfficer.OfficerName} Commands" : string.Empty,
			playerOfficer == null
				? string.Empty
				: $"{playerOfficer.WeaponName} ready. {GetCombatActionPromptText(playerOfficer)} Swapping weapons costs {CombatAttackActionCost} AP.",
			showActionPanel ? BuildCombatActionOptions(playerOfficer) : Array.Empty<MissionCombatActionOption>(),
			showActionPanel ? BuildCombatWeaponOptions(playerOfficer) : Array.Empty<MissionCombatWeaponOption>(),
			showActionPanel);
		_missionUi.SetExplorationControlPanel(string.Empty, _explorationPartyMovementEnabled, Array.Empty<MissionExplorationOfficerOption>(), false);
		RefreshMissionPrompt();
	}

	private MissionCombatantSummary BuildOfficerSummary(OfficerPawn officer)
	{
		if (officer == null)
		{
			return null;
		}

		Texture2D icon = LoadPortraitTexture(officer.PortraitPath);
		return new MissionCombatantSummary
		{
			DisplayName = officer.OfficerName,
			Subtitle = $"{officer.Specialty} | {officer.ShipName}",
			WeaponName = officer.WeaponName,
			ShieldName = officer.ShieldName,
			InventoryText = BuildOfficerInventoryText(officer),
			Icon = icon,
			CurrentHP = officer.CurrentHP,
			MaxHP = officer.MaxHP,
			CurrentShields = officer.CurrentShields,
			MaxShields = officer.MaxShields,
			CurrentAP = officer.CurrentActions,
			MaxAP = officer.MaxActions,
			AttackRange = officer.AttackRange,
			AttackMinDamage = officer.AttackMinDamage,
			AttackMaxDamage = officer.AttackDamage,
			Notes = BuildCombatantNotes(officer.InitiativeBonus, officer.ShieldRechargePerTurn, officer.BonusShieldDamage, officer.ShieldPiercingDamage, officer.WeaponStatusEffectId, officer.WeaponStatusEffectChance, officer.ActiveStatusEffectId)
		};
	}

	private MissionOfficerInventoryPanelData BuildOfficerInventoryPanelData(OfficerPawn officer)
	{
		if (officer == null || !TryGetOfficerState(officer, out OfficerState officerState))
		{
			return null;
		}

		OfficerMissionLoadoutService.EnsureOfficerLoadout(officerState);
		List<MissionInventoryEntry> loadoutEntries = new List<MissionInventoryEntry>
		{
			new MissionInventoryEntry
			{
				Title = officer.WeaponName,
				Detail = BuildWeaponDetailText(MissionEquipmentRegistry.GetWeapon(officerState.EquippedMissionWeaponId), officer),
				Highlighted = true
			},
			new MissionInventoryEntry
			{
				Title = officer.ShieldName,
				Detail = BuildShieldDetailText(MissionEquipmentRegistry.GetShield(officerState.EquippedMissionShieldId), officer),
				Highlighted = true
			},
			new MissionInventoryEntry
			{
				Title = string.IsNullOrWhiteSpace(officer.CombatAbilityId) ? "Mission Discipline" : officer.CombatAbilityId,
				Detail = string.IsNullOrWhiteSpace(officer.ActiveStatusEffectId)
					? "Status stable. No active impairments."
					: $"Current condition: {officer.ActiveStatusEffectId.Replace('_', ' ')}."
			}
		};

		List<MissionInventoryEntry> weaponEntries = BuildWeaponInventoryEntries(officerState, officer);
		List<MissionInventoryEntry> shieldEntries = BuildShieldInventoryEntries(officerState, officer);
		List<MissionInventoryEntry> itemEntries = BuildItemInventoryEntries(officerState);
		int carriedItems = (officerState.PersonalInventoryItemIDs ?? new List<string>()).Count;
		string footerText = "Click a locker entry to equip it. Right-click another portrait to switch dossiers.";
		if (weaponEntries.Count <= 1 && shieldEntries.Count <= 1)
		{
			footerText = carriedItems == 0
				? "No extra salvage is stowed on this operative yet."
				: $"Field pack holds {carriedItems} item{(carriedItems == 1 ? string.Empty : "s")}.";
		}

		return new MissionOfficerInventoryPanelData
		{
			OfficerId = $"officer:{officer.OfficerID}",
			DisplayName = officer.OfficerName,
			ShipName = officer.ShipName,
			Specialty = officer.Specialty,
			Portrait = LoadPortraitTexture(officer.PortraitPath),
			SummaryText = BuildOfficerInventorySummaryText(officerState, officer),
			VitalStatsText = BuildOfficerVitalStatsText(officerState, officer),
			FooterText = footerText,
			LoadoutEntries = loadoutEntries,
			WeaponEntries = weaponEntries,
			ShieldEntries = shieldEntries,
			ItemEntries = itemEntries
		};
	}

	private string BuildOfficerInventoryText(OfficerPawn officer)
	{
		if (officer == null || !TryGetOfficerState(officer, out OfficerState officerState))
		{
			return string.Empty;
		}

		List<string> sections = new List<string>();
		OfficerMissionLoadoutService.EnsureOfficerLoadout(officerState);
		string equippedWeaponId = officerState.EquippedMissionWeaponId;
		string equippedShieldId = officerState.EquippedMissionShieldId;

		List<string> weaponLines = BuildNamedInventoryLines(
			OfficerMissionLoadoutService.GetOwnedWeaponIds(officerState),
			equippedWeaponId,
			weaponId => MissionEquipmentRegistry.GetWeapon(weaponId)?.DisplayName);
		if (weaponLines.Count == 0 && !string.IsNullOrWhiteSpace(officer.WeaponName))
		{
			weaponLines.Add($"- {officer.WeaponName} (equipped)");
		}

		List<string> shieldLines = BuildNamedInventoryLines(
			OfficerMissionLoadoutService.GetOwnedShieldIds(officerState),
			equippedShieldId,
			shieldId => MissionEquipmentRegistry.GetShield(shieldId)?.DisplayName);
		if (shieldLines.Count == 0 && !string.IsNullOrWhiteSpace(officer.ShieldName))
		{
			shieldLines.Add($"- {officer.ShieldName} (equipped)");
		}

		List<string> itemLines = BuildStackedInventoryLines(
			officerState.PersonalInventoryItemIDs,
			itemId => CampaignItemRegistry.GetItem(itemId)?.DisplayName);

		AppendInventorySection(sections, "WEAPONS", weaponLines);
		AppendInventorySection(sections, "SHIELDS", shieldLines);
		AppendInventorySection(sections, "ITEMS", itemLines);
		return string.Join("\n\n", sections);
	}

	private static List<string> BuildNamedInventoryLines(IEnumerable<string> ids, string equippedId, Func<string, string> resolveName)
	{
		List<string> lines = new List<string>();
		HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (string id in ids ?? Enumerable.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id))
			{
				continue;
			}

			string displayName = resolveName?.Invoke(id);
			if (string.IsNullOrWhiteSpace(displayName))
			{
				displayName = id;
			}

			bool equipped = !string.IsNullOrWhiteSpace(equippedId) && string.Equals(id, equippedId, StringComparison.Ordinal);
			lines.Add(equipped ? $"- {displayName} (equipped)" : $"- {displayName}");
		}

		return lines;
	}

	private static List<string> BuildStackedInventoryLines(IEnumerable<string> ids, Func<string, string> resolveName)
	{
		return (ids ?? Enumerable.Empty<string>())
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.GroupBy(id => id, StringComparer.Ordinal)
			.Select(group =>
			{
				string displayName = resolveName?.Invoke(group.Key);
				if (string.IsNullOrWhiteSpace(displayName))
				{
					displayName = group.Key;
				}

				return group.Count() > 1
					? $"- {displayName} x{group.Count()}"
					: $"- {displayName}";
			})
			.OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static void AppendInventorySection(List<string> sections, string title, List<string> lines)
	{
		if (sections == null || string.IsNullOrWhiteSpace(title) || lines == null || lines.Count == 0)
		{
			return;
		}

		sections.Add($"{title}\n{string.Join("\n", lines)}");
	}

	private static List<MissionInventoryEntry> BuildWeaponInventoryEntries(OfficerState officerState, OfficerPawn officer)
	{
		List<MissionInventoryEntry> entries = OfficerMissionLoadoutService.GetOwnedWeaponIds(officerState)
			.Where(weaponId => !string.IsNullOrWhiteSpace(weaponId))
			.Distinct(StringComparer.Ordinal)
			.Select(weaponId =>
			{
				MissionWeaponDefinition definition = MissionEquipmentRegistry.GetWeapon(weaponId);
				bool isEquipped = string.Equals(weaponId, officerState.EquippedMissionWeaponId, StringComparison.Ordinal);
				return new MissionInventoryEntry
				{
					EntryId = weaponId,
					Title = definition?.DisplayName ?? weaponId,
					Detail = BuildWeaponDetailText(definition, officer),
					Highlighted = isEquipped,
					CanActivate = true,
					ActionText = isEquipped ? "EQUIPPED" : "EQUIP"
				};
			})
			.OrderByDescending(entry => entry.Highlighted)
			.ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (entries.Count == 0 && !string.IsNullOrWhiteSpace(officer?.WeaponName))
		{
			entries.Add(new MissionInventoryEntry
			{
				EntryId = officer.WeaponId,
				Title = officer.WeaponName,
				Detail = BuildWeaponDetailText(null, officer),
				Highlighted = true,
				CanActivate = true,
				ActionText = "EQUIPPED"
			});
		}

		return entries;
	}

	private static List<MissionInventoryEntry> BuildShieldInventoryEntries(OfficerState officerState, OfficerPawn officer)
	{
		List<MissionInventoryEntry> entries = OfficerMissionLoadoutService.GetOwnedShieldIds(officerState)
			.Where(shieldId => !string.IsNullOrWhiteSpace(shieldId))
			.Distinct(StringComparer.Ordinal)
			.Select(shieldId =>
			{
				MissionShieldDefinition definition = MissionEquipmentRegistry.GetShield(shieldId);
				bool isEquipped = string.Equals(shieldId, officerState.EquippedMissionShieldId, StringComparison.Ordinal);
				return new MissionInventoryEntry
				{
					EntryId = shieldId,
					Title = definition?.DisplayName ?? shieldId,
					Detail = BuildShieldDetailText(definition, officer),
					Highlighted = isEquipped,
					CanActivate = true,
					ActionText = isEquipped ? "EQUIPPED" : "EQUIP"
				};
			})
			.OrderByDescending(entry => entry.Highlighted)
			.ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (entries.Count == 0 && !string.IsNullOrWhiteSpace(officer?.ShieldName))
		{
			entries.Add(new MissionInventoryEntry
			{
				EntryId = officer.ShieldName,
				Title = officer.ShieldName,
				Detail = BuildShieldDetailText(null, officer),
				Highlighted = true,
				CanActivate = true,
				ActionText = "EQUIPPED"
			});
		}

		return entries;
	}

	private static List<MissionInventoryEntry> BuildItemInventoryEntries(OfficerState officerState)
	{
		return (officerState?.PersonalInventoryItemIDs ?? new List<string>())
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.GroupBy(itemId => itemId, StringComparer.Ordinal)
			.Select(group =>
			{
				CampaignItemDefinition definition = CampaignItemRegistry.GetItem(group.Key);
				return new MissionInventoryEntry
				{
					Title = definition?.DisplayName ?? group.Key,
					Detail = BuildItemDetailText(definition),
					QuantityText = group.Count() > 1 ? $"x{group.Count()}" : string.Empty
				};
			})
			.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string BuildOfficerInventorySummaryText(OfficerState officerState, OfficerPawn officer)
	{
		List<string> notes = new List<string>();
		if (!string.IsNullOrWhiteSpace(officerState?.Biography))
		{
			notes.Add(officerState.Biography.Trim());
		}
		else
		{
			notes.Add("No formal dossier has been written for this operative yet.");
		}

		if (!string.IsNullOrWhiteSpace(officerState?.Archetype) || !string.IsNullOrWhiteSpace(officerState?.Ideology))
		{
			notes.Add($"Alignment: {officerState.Archetype} with {officerState.Ideology} leanings.");
		}

		if (!string.IsNullOrWhiteSpace(officerState?.Flaw))
		{
			notes.Add($"Watchpoint: {officerState.Flaw}.");
		}

		if (!string.IsNullOrWhiteSpace(officer?.CombatAbilityId))
		{
			notes.Add($"Combat discipline: {officer.CombatAbilityId}.");
		}

		return string.Join("\n\n", notes.Where(note => !string.IsNullOrWhiteSpace(note)));
	}

	private static string BuildOfficerVitalStatsText(OfficerState officerState, OfficerPawn officer)
	{
		List<string> lines = new List<string>
		{
			$"HP        {officer.CurrentHP}/{officer.MaxHP}",
			$"SHIELDS   {officer.CurrentShields}/{officer.MaxShields}",
			$"ACTIONS   {officer.CurrentActions}/{officer.MaxActions}",
			$"RANGE     {officer.AttackRange}",
			$"DAMAGE    {officer.AttackMinDamage}-{officer.AttackDamage}",
			$"INIT      +{officer.InitiativeBonus}",
			$"APPROVAL  {officerState?.Approval ?? 0}",
			$"STRESS    {officerState?.Stress ?? 0}"
		};

		if (officer.ShieldRechargePerTurn > 0)
		{
			lines.Add($"RECHARGE  +{officer.ShieldRechargePerTurn}/turn");
		}

		return string.Join("\n", lines);
	}

	private static string BuildWeaponDetailText(MissionWeaponDefinition definition, OfficerPawn officer = null, MissionNpcPawn survivor = null)
	{
		List<string> parts = new List<string>();
		if (definition != null)
		{
			parts.Add(definition.IsMelee ? "Melee" : $"Range {MissionGridRules.ScaleAuthoredUnit(definition.AttackRange)}");
			parts.Add($"DMG {definition.MinDamage}-{definition.MaxDamage}");
			if (definition.BonusShieldDamage > 0)
			{
				parts.Add($"+{definition.BonusShieldDamage} vs shields");
			}

			if (definition.ShieldPiercingDamage > 0)
			{
				parts.Add($"{definition.ShieldPiercingDamage} pierce");
			}

			if (!string.IsNullOrWhiteSpace(definition.StatusEffectId))
			{
				int chancePercent = Mathf.RoundToInt(definition.StatusEffectChance * 100f);
				parts.Add($"{definition.StatusEffectId.Replace('_', ' ')} {chancePercent}%");
			}

			if (!string.IsNullOrWhiteSpace(definition.Description))
			{
				parts.Add(definition.Description);
			}

			return string.Join(" | ", parts);
		}

		if (officer != null)
		{
			parts.Add(officer.UsesMeleeWeapon ? "Melee" : $"Range {officer.AttackRange}");
			parts.Add($"DMG {officer.AttackMinDamage}-{officer.AttackDamage}");
			if (officer.BonusShieldDamage > 0)
			{
				parts.Add($"+{officer.BonusShieldDamage} vs shields");
			}

			if (officer.ShieldPiercingDamage > 0)
			{
				parts.Add($"{officer.ShieldPiercingDamage} pierce");
			}
		}
		else if (survivor != null)
		{
			parts.Add(survivor.UsesMeleeWeapon ? "Melee" : $"Range {survivor.AttackRange}");
			parts.Add($"DMG {survivor.AttackMinDamage}-{survivor.AttackDamage}");
			if (survivor.BonusShieldDamage > 0)
			{
				parts.Add($"+{survivor.BonusShieldDamage} vs shields");
			}

			if (survivor.ShieldPiercingDamage > 0)
			{
				parts.Add($"{survivor.ShieldPiercingDamage} pierce");
			}
		}

		return string.Join(" | ", parts);
	}

	private static string BuildShieldDetailText(MissionShieldDefinition definition, OfficerPawn officer = null, MissionNpcPawn survivor = null)
	{
		List<string> parts = new List<string>();
		if (definition != null)
		{
			parts.Add($"+{definition.CapacityBonus} capacity");
			parts.Add($"+{definition.RechargePerTurn}/turn");
			if (!string.IsNullOrWhiteSpace(definition.Description))
			{
				parts.Add(definition.Description);
			}

			return string.Join(" | ", parts);
		}

		if (officer != null)
		{
			parts.Add($"Capacity {officer.MaxShields}");
			parts.Add($"+{officer.ShieldRechargePerTurn}/turn");
		}
		else if (survivor != null)
		{
			parts.Add($"Capacity {survivor.MaxShields}");
			parts.Add($"+{survivor.ShieldRechargePerTurn}/turn");
		}

		return string.Join(" | ", parts);
	}

	private static string BuildItemDetailText(CampaignItemDefinition definition)
	{
		if (definition == null)
		{
			return "Recovered mission salvage.";
		}

		List<string> parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(definition.Category))
		{
			parts.Add(definition.Category);
		}

		if (!string.IsNullOrWhiteSpace(definition.Description))
		{
			parts.Add(definition.Description);
		}

		return parts.Count == 0 ? "Recovered mission salvage." : string.Join(" | ", parts);
	}

	private MissionOfficerInventoryPanelData BuildSurvivorInventoryPanelData(MissionNpcPawn survivor)
	{
		if (!IsActiveEscortSurvivor(survivor))
		{
			return null;
		}

		survivor.GetOwnedWeaponIds();
		survivor.GetOwnedShieldIds();
		List<MissionInventoryEntry> loadoutEntries = new List<MissionInventoryEntry>
		{
			new MissionInventoryEntry
			{
				Title = survivor.WeaponName,
				Detail = BuildWeaponDetailText(MissionEquipmentRegistry.GetWeapon(survivor.EquippedMissionWeaponId), null, survivor),
				Highlighted = true
			},
			new MissionInventoryEntry
			{
				Title = survivor.ShieldName,
				Detail = BuildShieldDetailText(MissionEquipmentRegistry.GetShield(survivor.EquippedMissionShieldId), null, survivor),
				Highlighted = true
			},
			new MissionInventoryEntry
			{
				Title = "Escort Status",
				Detail = string.IsNullOrWhiteSpace(survivor.ActiveStatusEffectId)
					? "Able to move, scavenge, and defend the evac route."
					: $"Current condition: {survivor.ActiveStatusEffectId.Replace('_', ' ')}."
			}
		};

		List<MissionInventoryEntry> weaponEntries = BuildSurvivorWeaponInventoryEntries(survivor);
		List<MissionInventoryEntry> shieldEntries = BuildSurvivorShieldInventoryEntries(survivor);
		List<MissionInventoryEntry> itemEntries = BuildSurvivorItemInventoryEntries(survivor);
		int carriedItems = survivor.PersonalInventoryItemIDs.Count;
		string footerText = "Click a locker entry to equip it. Right-click another portrait to switch dossiers.";
		if (weaponEntries.Count <= 1 && shieldEntries.Count <= 1)
		{
			footerText = carriedItems == 0
				? "This survivor is carrying no extra salvage."
				: $"Field pack holds {carriedItems} item{(carriedItems == 1 ? string.Empty : "s")}.";
		}

		return new MissionOfficerInventoryPanelData
		{
			OfficerId = $"survivor:{survivor.NpcId}",
			DisplayName = survivor.DisplayName,
			ShipName = CampaignText.RemnantsLabel,
			Specialty = "Rescued Survivor",
			Portrait = LoadPortraitTexture(survivor.PortraitPath),
			SummaryText = BuildSurvivorInventorySummaryText(survivor),
			VitalStatsText = BuildSurvivorVitalStatsText(survivor),
			FooterText = footerText,
			LoadoutEntries = loadoutEntries,
			WeaponEntries = weaponEntries,
			ShieldEntries = shieldEntries,
			ItemEntries = itemEntries
		};
	}

	private static List<MissionInventoryEntry> BuildSurvivorWeaponInventoryEntries(MissionNpcPawn survivor)
	{
		return survivor.GetOwnedWeaponIds()
			.Where(weaponId => !string.IsNullOrWhiteSpace(weaponId))
			.Distinct(StringComparer.Ordinal)
			.Select(weaponId =>
			{
				MissionWeaponDefinition definition = MissionEquipmentRegistry.GetWeapon(weaponId);
				bool isEquipped = string.Equals(weaponId, survivor.EquippedMissionWeaponId, StringComparison.Ordinal);
				return new MissionInventoryEntry
				{
					EntryId = weaponId,
					Title = definition?.DisplayName ?? weaponId,
					Detail = BuildWeaponDetailText(definition, null, survivor),
					Highlighted = isEquipped,
					CanActivate = true,
					ActionText = isEquipped ? "EQUIPPED" : "EQUIP"
				};
			})
			.OrderByDescending(entry => entry.Highlighted)
			.ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static List<MissionInventoryEntry> BuildSurvivorShieldInventoryEntries(MissionNpcPawn survivor)
	{
		return survivor.GetOwnedShieldIds()
			.Where(shieldId => !string.IsNullOrWhiteSpace(shieldId))
			.Distinct(StringComparer.Ordinal)
			.Select(shieldId =>
			{
				MissionShieldDefinition definition = MissionEquipmentRegistry.GetShield(shieldId);
				bool isEquipped = string.Equals(shieldId, survivor.EquippedMissionShieldId, StringComparison.Ordinal);
				return new MissionInventoryEntry
				{
					EntryId = shieldId,
					Title = definition?.DisplayName ?? shieldId,
					Detail = BuildShieldDetailText(definition, null, survivor),
					Highlighted = isEquipped,
					CanActivate = true,
					ActionText = isEquipped ? "EQUIPPED" : "EQUIP"
				};
			})
			.OrderByDescending(entry => entry.Highlighted)
			.ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static List<MissionInventoryEntry> BuildSurvivorItemInventoryEntries(MissionNpcPawn survivor)
	{
		return (survivor?.PersonalInventoryItemIDs ?? new List<string>())
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.GroupBy(itemId => itemId, StringComparer.Ordinal)
			.Select(group =>
			{
				CampaignItemDefinition definition = CampaignItemRegistry.GetItem(group.Key);
				return new MissionInventoryEntry
				{
					Title = definition?.DisplayName ?? group.Key,
					Detail = BuildItemDetailText(definition),
					QuantityText = group.Count() > 1 ? $"x{group.Count()}" : string.Empty
				};
			})
			.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string BuildSurvivorInventorySummaryText(MissionNpcPawn survivor)
	{
		List<string> notes = new List<string>();
		if (!string.IsNullOrWhiteSpace(survivor?.Description))
		{
			notes.Add(survivor.Description.Trim());
		}

		if (!string.IsNullOrWhiteSpace(survivor?.Notes))
		{
			notes.Add(survivor.Notes.Trim());
		}

		notes.Add("Recovered survivors can scavenge the battlefield and carry away-team equipment during extraction.");
		return string.Join("\n\n", notes.Where(note => !string.IsNullOrWhiteSpace(note)));
	}

	private static string BuildSurvivorVitalStatsText(MissionNpcPawn survivor)
	{
		List<string> lines = new List<string>
		{
			$"HP        {survivor.CurrentHP}/{survivor.MaxHP}",
			$"SHIELDS   {survivor.CurrentShields}/{survivor.MaxShields}",
			$"ACTIONS   {survivor.CurrentActions}/{survivor.MaxActions}",
			$"RANGE     {survivor.AttackRange}",
			$"DAMAGE    {survivor.AttackMinDamage}-{survivor.AttackDamage}",
			$"INIT      +{survivor.InitiativeBonus}"
		};

		if (survivor.ShieldRechargePerTurn > 0)
		{
			lines.Add($"RECHARGE  +{survivor.ShieldRechargePerTurn}/turn");
		}

		return string.Join("\n", lines);
	}

	private MissionCombatantSummary BuildEnemySummary(MissionNpcPawn enemy)
	{
		if (enemy == null)
		{
			return null;
		}

		Texture2D icon = LoadPortraitTexture(enemy.PortraitPath);
		if (icon == null && enemy.GetNodeOrNull<Sprite2D>("Sprite2D") is Sprite2D sprite)
		{
			icon = sprite.Texture;
		}

		return new MissionCombatantSummary
		{
			DisplayName = enemy.DisplayName,
			Subtitle = enemy.IsHostile ? "Hostile Contact" : IsEscortSurvivor(enemy) ? $"Escort {CampaignText.RemnantsLabel.TrimEnd('s')}" : "Mission Contact",
			WeaponName = enemy.WeaponName,
			ShieldName = enemy.ShieldName,
			InventoryText = BuildSurvivorInventoryText(enemy),
			Icon = icon,
			CurrentHP = enemy.CurrentHP,
			MaxHP = enemy.MaxHP,
			CurrentShields = enemy.CurrentShields,
			MaxShields = enemy.MaxShields,
			CurrentAP = enemy.CurrentActions,
			MaxAP = enemy.MaxActions,
			AttackRange = enemy.AttackRange,
			AttackMinDamage = enemy.AttackMinDamage,
			AttackMaxDamage = enemy.AttackDamage,
			Notes = enemy.IsHostile || IsEscortSurvivor(enemy)
				? BuildCombatantNotes(enemy.InitiativeBonus, enemy.ShieldRechargePerTurn, enemy.BonusShieldDamage, enemy.ShieldPiercingDamage, enemy.WeaponStatusEffectId, enemy.WeaponStatusEffectChance, enemy.ActiveStatusEffectId)
				: "Non-hostile contact"
		};
	}

	private static string BuildSurvivorInventoryText(MissionNpcPawn npc)
	{
		if (npc == null)
		{
			return string.Empty;
		}

		List<string> sections = new List<string>();
		List<string> weaponLines = BuildNamedInventoryLines(
			npc.GetOwnedWeaponIds(),
			npc.EquippedMissionWeaponId,
			weaponId => MissionEquipmentRegistry.GetWeapon(weaponId)?.DisplayName);
		List<string> shieldLines = BuildNamedInventoryLines(
			npc.GetOwnedShieldIds(),
			npc.EquippedMissionShieldId,
			shieldId => MissionEquipmentRegistry.GetShield(shieldId)?.DisplayName);
		List<string> itemLines = BuildStackedInventoryLines(
			npc.PersonalInventoryItemIDs,
			itemId => CampaignItemRegistry.GetItem(itemId)?.DisplayName);
		AppendInventorySection(sections, "WEAPONS", weaponLines);
		AppendInventorySection(sections, "SHIELDS", shieldLines);
		AppendInventorySection(sections, "ITEMS", itemLines);
		if (sections.Count == 0)
		{
			return string.Empty;
		}

		return string.Join("\n\n", sections);
	}

	private Texture2D LoadPortraitTexture(string resourcePath)
	{
		if (string.IsNullOrWhiteSpace(resourcePath) || !ResourceLoader.Exists(resourcePath))
		{
			return null;
		}

		if (_portraitTextureCache.TryGetValue(resourcePath, out Texture2D cachedTexture))
		{
			return cachedTexture;
		}

		Texture2D loadedTexture = ResourceLoader.Load<Texture2D>(resourcePath, string.Empty, ResourceLoader.CacheMode.IgnoreDeep);
		if (loadedTexture != null)
		{
			_portraitTextureCache[resourcePath] = loadedTexture;
		}

		return loadedTexture;
	}

	private static string BuildCombatantNotes(int initiativeBonus, int shieldRechargePerTurn, int bonusShieldDamage, int shieldPiercingDamage, string statusEffectId, float statusEffectChance, string activeStatusEffectId)
	{
		List<string> notes = new List<string>
		{
			$"Initiative bonus: +{initiativeBonus}",
			$"Shield recharge: +{shieldRechargePerTurn}/turn"
		};

		if (bonusShieldDamage > 0)
		{
			notes.Add($"Shield break: +{bonusShieldDamage}");
		}

		if (shieldPiercingDamage > 0)
		{
			notes.Add($"Piercing: +{shieldPiercingDamage}");
		}

		if (!string.IsNullOrWhiteSpace(statusEffectId) && statusEffectChance > 0f)
		{
			notes.Add($"Status: {statusEffectId} {(int)(statusEffectChance * 100f)}%");
		}

		if (!string.IsNullOrWhiteSpace(activeStatusEffectId))
		{
			notes.Add($"Afflicted: {activeStatusEffectId}");
		}

		return string.Join("\n", notes);
	}

	private static string BuildActionResultMessage(string actorName, string statusMessage, string fallbackMessage)
	{
		if (!string.IsNullOrWhiteSpace(statusMessage))
		{
			return string.IsNullOrWhiteSpace(actorName)
				? statusMessage.Trim()
				: $"{actorName}: {statusMessage.Trim()}";
		}

		return fallbackMessage;
	}
}

