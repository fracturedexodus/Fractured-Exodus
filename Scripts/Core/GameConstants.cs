public static class GameConstants
{
	public static class EntityTypes
	{
		public const string Planet = "Planet";
		public const string BasePlanetPlayerStart = "Base Planet (Player Start)";
		public const string CelestialBody = "Celestial Body";
		public const string PlayerFleet = "Player Fleet";
		public const string EnemyFleet = "Enemy Fleet";
		public const string StarGate = "StarGate";
		public const string Outpost = "Outpost";
	}

	public static class EquipmentCategories
	{
		public const string Weapon = "Weapon";
		public const string Shield = "Shield";
		public const string Armor = "Armor";
		public const string Missile = "Missile";
	}

	public static class ResourceKeys
	{
		public const string RawMaterials = "Raw Materials";
		public const string EnergyCores = "Energy Cores";
		public const string AncientTech = "Ancient Tech";
	}

	public static class ItemPrefixes
	{
		public const string Weapon = "WPN_";
		public const string Shield = "SHLD_";
		public const string Armor = "ARMR_";
		public const string Missile = "MSL_";
	}

	public static class StandardEquipment
	{
		public const string WeaponId = "WPN_MARK1_LASER";
		public const string ShieldId = "SHLD_STANDARD";
		public const string ArmorId = "ARMR_STANDARD";
		public const int MinSaleRaw = 50;
		public const int MaxSaleRaw = 150;
		public const int AncientTechSaleRaw = 150;
	}
}
