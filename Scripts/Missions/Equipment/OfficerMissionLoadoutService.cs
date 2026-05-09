using System.Collections.Generic;

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

		if (string.IsNullOrWhiteSpace(officer.EquippedMissionWeaponId))
		{
			officer.EquippedMissionWeaponId = MissionEquipmentRegistry.GetDefaultWeaponIdForSpecialty(officer.Specialty);
		}

		if (string.IsNullOrWhiteSpace(officer.EquippedMissionShieldId))
		{
			officer.EquippedMissionShieldId = MissionEquipmentRegistry.GetDefaultShieldIdForSpecialty(officer.Specialty);
		}

		if (!string.IsNullOrWhiteSpace(officer.EquippedMissionWeaponId) && !officer.OwnedMissionWeaponIds.Contains(officer.EquippedMissionWeaponId))
		{
			officer.OwnedMissionWeaponIds.Add(officer.EquippedMissionWeaponId);
		}

		if (!string.IsNullOrWhiteSpace(officer.EquippedMissionShieldId) && !officer.OwnedMissionShieldIds.Contains(officer.EquippedMissionShieldId))
		{
			officer.OwnedMissionShieldIds.Add(officer.EquippedMissionShieldId);
		}
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
}
