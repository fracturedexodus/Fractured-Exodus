using Godot;
using System.Collections.Generic;
using System.Linq;

public class InventoryStack
{
	public string ItemID { get; set; }
	public EquipmentData Item { get; set; }
	public int Count { get; set; }
}

public class InventoryReport
{
	public List<string> Lines { get; set; } = new List<string>();
}

public class FleetInventoryService
{
	public const string DefaultWeaponName = "Mark I Laser";
	public const string DefaultHullName = "Standard Hull";
	public const string DefaultShieldName = "Standard Shields";

	private readonly GlobalData _globalData;

	public FleetInventoryService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public IEnumerable<EquipmentData> GetShopItems()
	{
		if (_globalData?.MasterEquipmentDB == null)
		{
			return Enumerable.Empty<EquipmentData>();
		}

		return _globalData.MasterEquipmentDB.Values
			.Where(item => item != null && !IsStandardIssueItem(item.ItemID))
			.OrderBy(item => item.Category)
			.ThenBy(item => item.Name);
	}

	public EquipmentData GetEquipment(string itemID)
	{
		if (_globalData?.MasterEquipmentDB == null || string.IsNullOrEmpty(itemID) || !_globalData.MasterEquipmentDB.ContainsKey(itemID))
		{
			return null;
		}

		return _globalData.MasterEquipmentDB[itemID];
	}

	public bool CanAfford(string itemID)
	{
		EquipmentData item = GetEquipment(itemID);
		if (item == null || _globalData?.FleetResources == null)
		{
			return false;
		}

		float tech = _globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle();
		float raw = _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle();
		return tech >= item.CostTech && raw >= item.CostRaw;
	}

	public bool BuyItem(string itemID)
	{
		if (!CanAfford(itemID))
		{
			return false;
		}

		EquipmentData item = _globalData.MasterEquipmentDB[itemID];
		float tech = _globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle();
		float raw = _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle();

		_globalData.FleetResources[GameConstants.ResourceKeys.AncientTech] = tech - item.CostTech;
		_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] = raw - item.CostRaw;
		_globalData.UnequippedInventory.Add(itemID);
		return true;
	}

	public ShipLoadout GetOrCreateLoadout(string shipName)
	{
		if (_globalData == null || string.IsNullOrEmpty(shipName))
		{
			return null;
		}

		if (!_globalData.FleetLoadouts.ContainsKey(shipName))
		{
			_globalData.FleetLoadouts[shipName] = new ShipLoadout();
		}

		return _globalData.FleetLoadouts[shipName];
	}

	public string GetEquippedItemName(string itemID)
	{
		EquipmentData item = GetEquipment(itemID);
		return item?.Name ?? "None";
	}

	public string GetActiveWeaponName(string shipName)
	{
		ShipLoadout loadout = GetLoadout(shipName);
		if (loadout == null || string.IsNullOrEmpty(loadout.WeaponID))
		{
			return DefaultWeaponName;
		}

		EquipmentData item = GetEquipment(loadout.WeaponID);
		return item?.Name ?? DefaultWeaponName;
	}

	public string GetActiveShieldName(string shipName)
	{
		ShipLoadout loadout = GetLoadout(shipName);
		if (loadout == null || string.IsNullOrEmpty(loadout.ShieldID))
		{
			return DefaultShieldName;
		}

		EquipmentData item = GetEquipment(loadout.ShieldID);
		return item?.Name ?? DefaultShieldName;
	}

	public string GetActiveHullName(string shipName)
	{
		ShipLoadout loadout = GetLoadout(shipName);
		if (loadout == null || string.IsNullOrEmpty(loadout.ArmorID))
		{
			return DefaultHullName;
		}

		EquipmentData item = GetEquipment(loadout.ArmorID);
		return item?.Name ?? DefaultHullName;
	}

	public List<InventoryStack> GetGroupedInventory()
	{
		if (_globalData?.UnequippedInventory == null)
		{
			return new List<InventoryStack>();
		}

		return _globalData.UnequippedInventory
			.GroupBy(itemID => itemID)
			.Select(group => new InventoryStack
			{
				ItemID = group.Key,
				Item = GetEquipment(group.Key),
				Count = group.Count()
			})
			.Where(stack => stack.Item != null)
			.OrderBy(stack => stack.Item.Category)
			.ThenBy(stack => stack.Item.Name)
			.ToList();
	}

	public List<InventoryStack> GetGroupedEquippableInventory()
	{
		return GetGroupedInventory()
			.Where(stack => !IsStandardIssueItem(stack.ItemID))
			.ToList();
	}

	public List<InventoryStack> GetGroupedSellableInventory()
	{
		return GetGroupedInventory()
			.Where(stack => IsStandardIssueItem(stack.ItemID))
			.ToList();
	}

	public InventoryReport BuildInventoryReport()
	{
		InventoryReport report = new InventoryReport();
		if (_globalData == null)
		{
			return report;
		}

		report.Lines.Add("[color=yellow]--- FLEET INVENTORY ---[/color]");

		if (_globalData.FleetResources != null)
		{
			foreach (KeyValuePair<string, Variant> kvp in _globalData.FleetResources)
			{
				report.Lines.Add($"- {kvp.Key}: {kvp.Value.AsSingle():0.##}");
			}
		}

		int weaponCount = _globalData.UnequippedInventory?.Count(id => id.StartsWith(GameConstants.ItemPrefixes.Weapon)) ?? 0;
		int shieldCount = _globalData.UnequippedInventory?.Count(id => id.StartsWith(GameConstants.ItemPrefixes.Shield)) ?? 0;
		int armorCount = _globalData.UnequippedInventory?.Count(id => id.StartsWith(GameConstants.ItemPrefixes.Armor)) ?? 0;

		report.Lines.Add(string.Empty);
		report.Lines.Add("[color=cyan]--- UNEQUIPPED UPGRADES ---[/color]");
		report.Lines.Add($"- Weapons: {weaponCount}");
		report.Lines.Add($"- Shields: {shieldCount}");
		report.Lines.Add($"- Armor: {armorCount}");

		return report;
	}

	public int GetStandardIssueSaleValue(string outpostName)
	{
		string seed = $"{_globalData?.SavedSystem ?? string.Empty}:{outpostName ?? string.Empty}";
		int hash = 17;
		foreach (char c in seed)
		{
			hash = (hash * 31) + c;
		}

		int range = GameConstants.StandardEquipment.MaxSaleRaw - GameConstants.StandardEquipment.MinSaleRaw + 1;
		int normalized = Mathf.Abs(hash % range);
		return GameConstants.StandardEquipment.MinSaleRaw + normalized;
	}

	public int GetAncientTechUnitCount()
	{
		if (_globalData?.FleetResources == null)
		{
			return 0;
		}

		return Mathf.FloorToInt(_globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle());
	}

	public bool SellAncientTech()
	{
		if (_globalData?.FleetResources == null || GetAncientTechUnitCount() <= 0)
		{
			return false;
		}

		float tech = _globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle();
		float raw = _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle();
		_globalData.FleetResources[GameConstants.ResourceKeys.AncientTech] = Mathf.Max(0f, tech - 1f);
		_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] = raw + GameConstants.StandardEquipment.AncientTechSaleRaw;
		return true;
	}

	public bool SellInventoryItem(string itemID, int rawValue)
	{
		if (_globalData?.UnequippedInventory == null || _globalData?.FleetResources == null)
		{
			return false;
		}

		if (string.IsNullOrEmpty(itemID) || !_globalData.UnequippedInventory.Contains(itemID))
		{
			return false;
		}

		_globalData.UnequippedInventory.Remove(itemID);
		float raw = _globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle();
		_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] = raw + rawValue;
		return true;
	}

	public bool EquipItem(string shipName, string itemID)
	{
		if (_globalData?.UnequippedInventory == null || !_globalData.UnequippedInventory.Contains(itemID))
		{
			return false;
		}

		EquipmentData itemToEquip = GetEquipment(itemID);
		ShipLoadout loadout = GetOrCreateLoadout(shipName);
		if (itemToEquip == null || loadout == null)
		{
			return false;
		}

		string oldItemID = "";
		if (itemToEquip.Category == GameConstants.EquipmentCategories.Weapon)
		{
			oldItemID = loadout.WeaponID;
			loadout.WeaponID = itemID;
		}
		else if (itemToEquip.Category == GameConstants.EquipmentCategories.Shield)
		{
			oldItemID = loadout.ShieldID;
			loadout.ShieldID = itemID;
		}
		else if (itemToEquip.Category == GameConstants.EquipmentCategories.Armor)
		{
			oldItemID = loadout.ArmorID;
			loadout.ArmorID = itemID;
		}
		else
		{
			return false;
		}

		_globalData.UnequippedInventory.Remove(itemID);
		if (string.IsNullOrEmpty(oldItemID))
		{
			oldItemID = GetStandardIssueItemId(itemToEquip.Category);
		}

		if (!string.IsNullOrEmpty(oldItemID))
		{
			_globalData.UnequippedInventory.Add(oldItemID);
		}

		return true;
	}

	public void ApplyLoadoutStats(MapEntity ship)
	{
		ApplyLoadoutStats(_globalData, ship);
	}

	public static void ApplyLoadoutStats(GlobalData globalData, MapEntity ship)
	{
		if (globalData == null || ship == null || ship.Type != GameConstants.EntityTypes.PlayerFleet)
		{
			return;
		}

		(int baseHp, int baseShields) = Database.GetShipCombatStats(ship.Name);
		(int baseRange, int baseDamage) = Database.GetShipWeaponStats(ship.Name);

		ShipLoadout loadout = null;
		if (globalData.FleetLoadouts.ContainsKey(ship.Name))
		{
			loadout = globalData.FleetLoadouts[ship.Name];
		}

		int hpBonus = GetLoadoutBonus(globalData, loadout?.ArmorID, GameConstants.EquipmentCategories.Armor);
		int shieldBonus = GetLoadoutBonus(globalData, loadout?.ShieldID, GameConstants.EquipmentCategories.Shield);
		int weaponBonus = GetLoadoutBonus(globalData, loadout?.WeaponID, GameConstants.EquipmentCategories.Weapon);

		int newMaxHp = baseHp + hpBonus;
		int newMaxShields = baseShields + shieldBonus;
		int newAttackDamage = baseDamage + weaponBonus;

		int hpDelta = newMaxHp - ship.MaxHP;
		int shieldDelta = newMaxShields - ship.MaxShields;

		ship.MaxHP = newMaxHp;
		ship.MaxShields = newMaxShields;
		ship.AttackRange = baseRange;
		ship.AttackDamage = newAttackDamage;
		ship.CurrentHP = Mathf.Clamp(ship.CurrentHP + hpDelta, 0, ship.MaxHP);
		ship.CurrentShields = Mathf.Clamp(ship.CurrentShields + shieldDelta, 0, ship.MaxShields);
	}

	private static int GetLoadoutBonus(GlobalData globalData, string itemID, string category)
	{
		if (globalData?.MasterEquipmentDB == null || string.IsNullOrEmpty(itemID) || !globalData.MasterEquipmentDB.ContainsKey(itemID))
		{
			return 0;
		}

		EquipmentData item = globalData.MasterEquipmentDB[itemID];
		return item.Category == category ? item.BonusStat : 0;
	}

	private static bool IsStandardIssueItem(string itemID)
	{
		return itemID == GameConstants.StandardEquipment.WeaponId
			|| itemID == GameConstants.StandardEquipment.ShieldId
			|| itemID == GameConstants.StandardEquipment.ArmorId;
	}

	private static string GetStandardIssueItemId(string category)
	{
		if (category == GameConstants.EquipmentCategories.Weapon)
		{
			return GameConstants.StandardEquipment.WeaponId;
		}

		if (category == GameConstants.EquipmentCategories.Shield)
		{
			return GameConstants.StandardEquipment.ShieldId;
		}

		if (category == GameConstants.EquipmentCategories.Armor)
		{
			return GameConstants.StandardEquipment.ArmorId;
		}

		return string.Empty;
	}

	private ShipLoadout GetLoadout(string shipName)
	{
		if (_globalData == null || string.IsNullOrEmpty(shipName) || _globalData.FleetLoadouts == null)
		{
			return null;
		}

		return _globalData.FleetLoadouts.ContainsKey(shipName) ? _globalData.FleetLoadouts[shipName] : null;
	}
}
