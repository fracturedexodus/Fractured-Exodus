using Godot;
using System.Collections.Generic;

public static class Database
{
	private static readonly Dictionary<string, ShipDefinition> ShipCatalog = ShipCatalogLoader.LoadCatalog();
	private static readonly string[] CachedEnemyShipTypes = BuildEnemyShipTypes();

	public static string[] EnemyShipTypes => CachedEnemyShipTypes;

	public static string[] ShipParts = new string[] {
		"Port Thrusters", "Starboard Hull", "Main Bridge", "Weapon Arrays",
		"Shield Generators", "Aft Cargo Bay", "Navigational Sensors", "Life Support Systems"
	};

	public static string[] MissTexts = new string[] {
		"Shot went wide!", "Evasive maneuvers successful!",
		"Glanced harmlessly off the deflectors!", "Missed by a hair!"
	};

	public static string GetShipTexturePath(string shipName)
	{
		ShipDefinition definition = GetShipDefinition(shipName);
		return definition?.SpritePath ?? "res://Assets/UI/icon.svg";
	}

	public static string GetShipBlueprintPath(string shipName)
	{
		ShipDefinition definition = GetShipDefinition(shipName);
		return definition?.BlueprintPath ?? string.Empty;
	}

	public static string GetShipMovementSoundPath(string shipName)
	{
		ShipDefinition definition = GetShipDefinition(shipName);
		return definition?.MovementSoundPath ?? string.Empty;
	}

	public static int GetShipBaseActions(string shipName)
	{
		ShipDefinition definition = GetShipDefinition(shipName);
		return definition?.BaseActions ?? 3;
	}

	public static (int hp, int shields) GetShipCombatStats(string shipName)
	{
		ShipDefinition definition = GetShipDefinition(shipName);
		return definition == null ? (50, 25) : (definition.MaxHP, definition.MaxShields);
	}

	public static (int range, int damage) GetShipWeaponStats(string shipName)
	{
		ShipDefinition definition = GetShipDefinition(shipName);
		return definition == null ? (2, 20) : (definition.AttackRange, definition.AttackDamage);
	}

	public static int GetShipInitiativeBonus(string shipName)
	{
		ShipDefinition definition = GetShipDefinition(shipName);
		return definition?.InitiativeBonus ?? 0;
	}

	public static float GetShipRotationOffset(string shipName)
	{
		ShipDefinition definition = GetShipDefinition(shipName);
		return definition?.RotationOffset ?? 0f;
	}

	private static ShipDefinition GetShipDefinition(string shipName)
	{
		if (string.IsNullOrEmpty(shipName) || !ShipCatalog.ContainsKey(shipName))
		{
			return null;
		}

		return ShipCatalog[shipName];
	}

	private static string[] BuildEnemyShipTypes()
	{
		List<string> enemyNames = new List<string>();
		foreach (KeyValuePair<string, ShipDefinition> kvp in ShipCatalog)
		{
			if (kvp.Value != null && kvp.Value.IsEnemyShip)
			{
				enemyNames.Add(kvp.Key);
			}
		}

		return enemyNames.ToArray();
	}
}
