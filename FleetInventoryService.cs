using Godot;
using System.Collections.Generic;
using System.Linq;

public class InventoryStack
{
	public string ItemID { get; set; }
	public EquipmentData Item { get; set; }
	public int Count { get; set; }
}

public class FleetInventoryService
{
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
}
