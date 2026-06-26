using System;
using System.Collections.Generic;
using System.Linq;

public static class OfficerMissionLoadoutService
{
	public static void EnsureOfficerLoadout(OfficerState officer)
	{
		if (officer == null)
		{
			return;
		}

		officer.OwnedMissionWeaponIds ??= new List<string>();
		officer.OwnedMissionShieldIds ??= new List<string>();
		ApplyCampaignItemUnlocks(officer);
		officer.OwnedMissionWeaponIds = SanitizeOwnedIds(officer.OwnedMissionWeaponIds, MissionEquipmentRegistry.GetWeapon);
		officer.OwnedMissionShieldIds = SanitizeOwnedIds(officer.OwnedMissionShieldIds, MissionEquipmentRegistry.GetShield);

		officer.EquippedMissionWeaponId = ResolveEquippedId(
			officer.EquippedMissionWeaponId,
			officer.OwnedMissionWeaponIds,
			MissionEquipmentRegistry.GetDefaultWeaponIdForSpecialty(officer.Specialty),
			MissionEquipmentRegistry.GetWeapon);
		officer.EquippedMissionShieldId = ResolveEquippedId(
			officer.EquippedMissionShieldId,
			officer.OwnedMissionShieldIds,
			MissionEquipmentRegistry.GetDefaultShieldIdForSpecialty(officer.Specialty),
			MissionEquipmentRegistry.GetShield);

		if (!string.IsNullOrWhiteSpace(officer.EquippedMissionWeaponId) && !officer.OwnedMissionWeaponIds.Contains(officer.EquippedMissionWeaponId))
		{
			officer.OwnedMissionWeaponIds.Add(officer.EquippedMissionWeaponId);
		}

		if (!string.IsNullOrWhiteSpace(officer.EquippedMissionShieldId) && !officer.OwnedMissionShieldIds.Contains(officer.EquippedMissionShieldId))
		{
			officer.OwnedMissionShieldIds.Add(officer.EquippedMissionShieldId);
		}
	}

	public static IReadOnlyList<string> ApplyCampaignItemUnlocks(OfficerState officer)
	{
		if (officer == null)
		{
			return Array.Empty<string>();
		}

		officer.PersonalInventoryItemIDs ??= new List<string>();
		officer.OwnedMissionWeaponIds ??= new List<string>();
		officer.OwnedMissionShieldIds ??= new List<string>();

		List<string> unlockedDisplayNames = new List<string>();
		List<string> remainingItems = new List<string>();
		foreach (string itemId in officer.PersonalInventoryItemIDs)
		{
			if (string.IsNullOrWhiteSpace(itemId))
			{
				continue;
			}

			CampaignItemDefinition item = CampaignItemRegistry.GetItem(itemId);
			bool itemProvidesEquipment = false;
			if (!string.IsNullOrWhiteSpace(item?.MissionWeaponId) && MissionEquipmentRegistry.GetWeapon(item.MissionWeaponId) != null)
			{
				itemProvidesEquipment = true;
				if (!officer.OwnedMissionWeaponIds.Contains(item.MissionWeaponId))
				{
					officer.OwnedMissionWeaponIds.Add(item.MissionWeaponId);
					unlockedDisplayNames.Add(MissionEquipmentRegistry.GetWeapon(item.MissionWeaponId)?.DisplayName ?? item.MissionWeaponId);
				}
			}

			if (!string.IsNullOrWhiteSpace(item?.MissionShieldId) && MissionEquipmentRegistry.GetShield(item.MissionShieldId) != null)
			{
				itemProvidesEquipment = true;
				if (!officer.OwnedMissionShieldIds.Contains(item.MissionShieldId))
				{
					officer.OwnedMissionShieldIds.Add(item.MissionShieldId);
					unlockedDisplayNames.Add(MissionEquipmentRegistry.GetShield(item.MissionShieldId)?.DisplayName ?? item.MissionShieldId);
				}
			}

			if (!(item?.ConsumeOnUnlock == true && itemProvidesEquipment))
			{
				remainingItems.Add(itemId);
			}
		}

		if (remainingItems.Count != officer.PersonalInventoryItemIDs.Count)
		{
			officer.PersonalInventoryItemIDs = remainingItems;
		}

		return unlockedDisplayNames
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct(StringComparer.Ordinal)
			.ToList();
	}

	public static IReadOnlyList<string> GetOwnedWeaponIds(OfficerState officer)
	{
		EnsureOfficerLoadout(officer);
		return officer?.OwnedMissionWeaponIds?.ToList() ?? new List<string>();
	}

	public static IReadOnlyList<string> GetOwnedShieldIds(OfficerState officer)
	{
		EnsureOfficerLoadout(officer);
		return officer?.OwnedMissionShieldIds?.ToList() ?? new List<string>();
	}

	public static MissionWeaponDefinition GetEquippedWeapon(OfficerState officer)
	{
		EnsureOfficerLoadout(officer);
		return MissionEquipmentRegistry.GetWeapon(officer?.EquippedMissionWeaponId);
	}

	public static MissionShieldDefinition GetEquippedShield(OfficerState officer)
	{
		EnsureOfficerLoadout(officer);
		return MissionEquipmentRegistry.GetShield(officer?.EquippedMissionShieldId);
	}

	public static bool EquipWeapon(OfficerState officer, string weaponId)
	{
		if (officer == null || string.IsNullOrWhiteSpace(weaponId) || MissionEquipmentRegistry.GetWeapon(weaponId) == null)
		{
			return false;
		}

		EnsureOfficerLoadout(officer);
		if (!officer.OwnedMissionWeaponIds.Contains(weaponId))
		{
			return false;
		}

		officer.EquippedMissionWeaponId = weaponId;
		return true;
	}

	public static bool EquipShield(OfficerState officer, string shieldId)
	{
		if (officer == null || string.IsNullOrWhiteSpace(shieldId) || MissionEquipmentRegistry.GetShield(shieldId) == null)
		{
			return false;
		}

		EnsureOfficerLoadout(officer);
		if (!officer.OwnedMissionShieldIds.Contains(shieldId))
		{
			return false;
		}

		officer.EquippedMissionShieldId = shieldId;
		return true;
	}

	private static List<string> SanitizeOwnedIds<TDefinition>(IEnumerable<string> ids, Func<string, TDefinition> resolver)
		where TDefinition : class
	{
		HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
		List<string> sanitizedIds = new List<string>();
		foreach (string id in ids ?? Enumerable.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id) || resolver(id) == null)
			{
				continue;
			}

			sanitizedIds.Add(id);
		}

		return sanitizedIds;
	}

	private static string ResolveEquippedId<TDefinition>(
		string equippedId,
		IReadOnlyList<string> ownedIds,
		string defaultId,
		Func<string, TDefinition> resolver)
		where TDefinition : class
	{
		if (!string.IsNullOrWhiteSpace(equippedId) && resolver(equippedId) != null)
		{
			return equippedId;
		}

		string ownedFallback = ownedIds?.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id) && resolver(id) != null);
		if (!string.IsNullOrWhiteSpace(ownedFallback))
		{
			return ownedFallback;
		}

		return resolver(defaultId) != null ? defaultId : string.Empty;
	}
}
