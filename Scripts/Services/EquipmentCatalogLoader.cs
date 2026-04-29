using Godot;
using System.Collections.Generic;

public static class EquipmentCatalogLoader
{
	private const string CatalogPath = "res://Data/equipment_catalog.json";

	public static Dictionary<string, EquipmentData> LoadCatalog()
	{
		if (!FileAccess.FileExists(CatalogPath))
		{
			GD.PrintErr($"Equipment catalog not found at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		using FileAccess file = FileAccess.Open(CatalogPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr($"Failed to open equipment catalog at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		var json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok)
		{
			GD.PrintErr($"Failed to parse equipment catalog at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		var itemArray = (Godot.Collections.Array)json.Data;

		var catalog = new Dictionary<string, EquipmentData>();
		foreach (Variant itemVariant in itemArray)
		{
			var itemDict = (Godot.Collections.Dictionary)itemVariant;
			EquipmentData equipment = ParseEquipment(itemDict);
			if (!string.IsNullOrEmpty(equipment.ItemID))
			{
				catalog[equipment.ItemID] = equipment;
			}
		}

		if (catalog.Count == 0)
		{
			GD.PrintErr($"Equipment catalog at {CatalogPath} contained no valid items. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		return catalog;
	}

	private static EquipmentData ParseEquipment(Godot.Collections.Dictionary itemDict)
	{
		return new EquipmentData
		{
			ItemID = itemDict.ContainsKey("ItemID") ? (string)itemDict["ItemID"] : string.Empty,
			Name = itemDict.ContainsKey("Name") ? (string)itemDict["Name"] : string.Empty,
			Category = itemDict.ContainsKey("Category") ? (string)itemDict["Category"] : string.Empty,
			BonusStat = itemDict.ContainsKey("BonusStat") ? (int)itemDict["BonusStat"] : 0,
			CostTech = itemDict.ContainsKey("CostTech") ? itemDict["CostTech"].AsSingle() : 0f,
			CostRaw = itemDict.ContainsKey("CostRaw") ? itemDict["CostRaw"].AsSingle() : 0f,
			Description = itemDict.ContainsKey("Description") ? (string)itemDict["Description"] : string.Empty,
			MissileDamage = itemDict.ContainsKey("MissileDamage") ? (int)itemDict["MissileDamage"] : 0,
			MissileRange = itemDict.ContainsKey("MissileRange") ? (int)itemDict["MissileRange"] : 0,
			MissileAbility = itemDict.ContainsKey("MissileAbility") ? (string)itemDict["MissileAbility"] : string.Empty
		};
	}

	private static Dictionary<string, EquipmentData> CreateFallbackCatalog()
	{
		return new Dictionary<string, EquipmentData>
		{
			{
				GameConstants.StandardEquipment.WeaponId,
				new EquipmentData
				{
					ItemID = GameConstants.StandardEquipment.WeaponId,
					Name = FleetInventoryService.DefaultWeaponName,
					Category = GameConstants.EquipmentCategories.Weapon,
					BonusStat = 0,
					CostTech = 0,
					CostRaw = 0,
					Description = "Standard-issue fleet weaponry"
				}
			},
			{
				GameConstants.StandardEquipment.ShieldId,
				new EquipmentData
				{
					ItemID = GameConstants.StandardEquipment.ShieldId,
					Name = FleetInventoryService.DefaultShieldName,
					Category = GameConstants.EquipmentCategories.Shield,
					BonusStat = 0,
					CostTech = 0,
					CostRaw = 0,
					Description = "Standard-issue defensive shield array"
				}
			},
			{
				GameConstants.StandardEquipment.ArmorId,
				new EquipmentData
				{
					ItemID = GameConstants.StandardEquipment.ArmorId,
					Name = FleetInventoryService.DefaultHullName,
					Category = GameConstants.EquipmentCategories.Armor,
					BonusStat = 0,
					CostTech = 0,
					CostRaw = 0,
					Description = "Standard-issue reinforced hull plating"
				}
			},
			{
				"WPN_LASER_MK2",
				new EquipmentData
				{
					ItemID = "WPN_LASER_MK2",
					Name = "Mk II Pulse Laser",
					Category = GameConstants.EquipmentCategories.Weapon,
					BonusStat = 10,
					CostTech = 1,
					CostRaw = 100,
					Description = "+10 Max Attack Damage"
				}
			},
			{
				"WPN_RAILGUN",
				new EquipmentData
				{
					ItemID = "WPN_RAILGUN",
					Name = "Magnetic Railgun",
					Category = GameConstants.EquipmentCategories.Weapon,
					BonusStat = 25,
					CostTech = 3,
					CostRaw = 300,
					Description = "+25 Max Attack Damage"
				}
			},
			{
				"SHLD_DEFLECTOR",
				new EquipmentData
				{
					ItemID = "SHLD_DEFLECTOR",
					Name = "Ion Deflector",
					Category = GameConstants.EquipmentCategories.Shield,
					BonusStat = 50,
					CostTech = 1,
					CostRaw = 150,
					Description = "+50 Max Shields"
				}
			},
			{
				"SHLD_AEGIS",
				new EquipmentData
				{
					ItemID = "SHLD_AEGIS",
					Name = "Aegis Generator",
					Category = GameConstants.EquipmentCategories.Shield,
					BonusStat = 120,
					CostTech = 3,
					CostRaw = 400,
					Description = "+120 Max Shields"
				}
			},
			{
				"ARMR_TITANIUM",
				new EquipmentData
				{
					ItemID = "ARMR_TITANIUM",
					Name = "Titanium Plating",
					Category = GameConstants.EquipmentCategories.Armor,
					BonusStat = 100,
					CostTech = 1,
					CostRaw = 200,
					Description = "+100 Max Hull HP"
				}
			},
			{
				"ARMR_NEUTRONIUM",
				new EquipmentData
				{
					ItemID = "ARMR_NEUTRONIUM",
					Name = "Neutronium Hull",
					Category = GameConstants.EquipmentCategories.Armor,
					BonusStat = 300,
					CostTech = 4,
					CostRaw = 500,
					Description = "+300 Max Hull HP"
				}
			},
			{
				"MSL_HEATSEEKER",
				new EquipmentData
				{
					ItemID = "MSL_HEATSEEKER",
					Name = "Heatseeker Missile",
					Category = GameConstants.EquipmentCategories.Missile,
					BonusStat = 0,
					CostTech = 1,
					CostRaw = 150,
					Description = "Range 5 missile. Standard explosive impact.",
					MissileDamage = 35,
					MissileRange = 5,
					MissileAbility = "Standard"
				}
			},
			{
				"MSL_ION",
				new EquipmentData
				{
					ItemID = "MSL_ION",
					Name = "Ion Disruptor Missile",
					Category = GameConstants.EquipmentCategories.Missile,
					BonusStat = 0,
					CostTech = 2,
					CostRaw = 220,
					Description = "Range 5 missile. Heavy anti-shield payload.",
					MissileDamage = 45,
					MissileRange = 5,
					MissileAbility = "ShieldBreaker"
				}
			},
			{
				"MSL_BREACHER",
				new EquipmentData
				{
					ItemID = "MSL_BREACHER",
					Name = "Breacher Missile",
					Category = GameConstants.EquipmentCategories.Missile,
					BonusStat = 0,
					CostTech = 3,
					CostRaw = 280,
					Description = "Range 4 missile. Armor-penetrating warhead.",
					MissileDamage = 30,
					MissileRange = 4,
					MissileAbility = "ShieldPiercing"
				}
			}
		};
	}
}
