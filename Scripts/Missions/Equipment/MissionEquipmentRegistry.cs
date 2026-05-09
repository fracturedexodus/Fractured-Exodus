using Godot;
using System.Collections.Generic;
using System.Linq;

public static class MissionEquipmentRegistry
{
	private const string WeaponDirectory = "res://Data/Missions/Equipment/Weapons";
	private const string ShieldDirectory = "res://Data/Missions/Equipment/Shields";

	private static readonly Dictionary<string, MissionWeaponDefinition> Weapons = new Dictionary<string, MissionWeaponDefinition>();
	private static readonly Dictionary<string, MissionShieldDefinition> Shields = new Dictionary<string, MissionShieldDefinition>();
	private static bool _isLoaded;

	public static MissionWeaponDefinition GetWeapon(string weaponId)
	{
		EnsureLoaded();
		return string.IsNullOrWhiteSpace(weaponId) || !Weapons.TryGetValue(weaponId, out MissionWeaponDefinition weapon)
			? null
			: weapon;
	}

	public static MissionShieldDefinition GetShield(string shieldId)
	{
		EnsureLoaded();
		return string.IsNullOrWhiteSpace(shieldId) || !Shields.TryGetValue(shieldId, out MissionShieldDefinition shield)
			? null
			: shield;
	}

	public static IReadOnlyList<MissionWeaponDefinition> GetAllWeapons()
	{
		EnsureLoaded();
		return Weapons.Values
			.Where(weapon => weapon != null)
			.OrderBy(weapon => weapon.DisplayName)
			.ToList();
	}

	public static IReadOnlyList<MissionShieldDefinition> GetAllShields()
	{
		EnsureLoaded();
		return Shields.Values
			.Where(shield => shield != null)
			.OrderBy(shield => shield.DisplayName)
			.ToList();
	}

	public static string GetDefaultWeaponIdForSpecialty(string specialty)
	{
		return specialty switch
		{
			"Medical Triage" => "shock_baton",
			"Morale Support" => "shock_baton",
			"Salvage Efficiency" => "cutting_rig",
			"Engine Routing" => "cutting_rig",
			"Missile Control" => "heavy_sidearm",
			"Tactical Command" => "pulse_carbine",
			"Shield Tuning" => "defense_pistol",
			_ => "sidearm"
		};
	}

	public static string GetDefaultShieldIdForSpecialty(string specialty)
	{
		return specialty switch
		{
			"Shield Tuning" => "tuned_barrier",
			"Medical Triage" => "medic_screen",
			_ => "field_aegis"
		};
	}

	public static void Reload()
	{
		_isLoaded = false;
		Weapons.Clear();
		Shields.Clear();
		EnsureLoaded();
	}

	private static void EnsureLoaded()
	{
		if (_isLoaded)
		{
			return;
		}

		_isLoaded = true;
		Weapons.Clear();
		Shields.Clear();
		LoadWeapons();
		LoadShields();
	}

	private static void LoadWeapons()
	{
		if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(WeaponDirectory)))
		{
			return;
		}

		foreach (string file in DirAccess.GetFilesAt(WeaponDirectory).Where(file => file.EndsWith(".tres")))
		{
			MissionWeaponDefinition weapon = GD.Load<MissionWeaponDefinition>($"{WeaponDirectory}/{file}");
			if (weapon == null || string.IsNullOrWhiteSpace(weapon.WeaponId))
			{
				continue;
			}

			Weapons[weapon.WeaponId] = weapon;
		}
	}

	private static void LoadShields()
	{
		if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(ShieldDirectory)))
		{
			return;
		}

		foreach (string file in DirAccess.GetFilesAt(ShieldDirectory).Where(file => file.EndsWith(".tres")))
		{
			MissionShieldDefinition shield = GD.Load<MissionShieldDefinition>($"{ShieldDirectory}/{file}");
			if (shield == null || string.IsNullOrWhiteSpace(shield.ShieldId))
			{
				continue;
			}

			Shields[shield.ShieldId] = shield;
		}
	}
}
