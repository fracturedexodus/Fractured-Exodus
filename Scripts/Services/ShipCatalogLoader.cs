using Godot;
using System.Collections.Generic;

public class ShipDefinition
{
	public string Name { get; set; } = string.Empty;
	public string SpritePath { get; set; } = string.Empty;
	public string BlueprintPath { get; set; } = string.Empty;
	public string MovementSoundPath { get; set; } = string.Empty;
	public int BaseActions { get; set; }
	public int MaxHP { get; set; }
	public int MaxShields { get; set; }
	public int AttackRange { get; set; }
	public int AttackDamage { get; set; }
	public int InitiativeBonus { get; set; }
	public float RotationOffset { get; set; }
	public bool IsEnemyShip { get; set; }
}

public static class ShipCatalogLoader
{
	private const string CatalogPath = "res://Data/ship_catalog.json";

	public static Dictionary<string, ShipDefinition> LoadCatalog()
	{
		if (!FileAccess.FileExists(CatalogPath))
		{
			GD.PrintErr($"Ship catalog not found at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		using FileAccess file = FileAccess.Open(CatalogPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr($"Failed to open ship catalog at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		Json json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok)
		{
			GD.PrintErr($"Failed to parse ship catalog at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		Godot.Collections.Array shipArray = (Godot.Collections.Array)json.Data;
		Dictionary<string, ShipDefinition> catalog = new Dictionary<string, ShipDefinition>();
		foreach (Variant shipVariant in shipArray)
		{
			Godot.Collections.Dictionary shipDict = (Godot.Collections.Dictionary)shipVariant;
			ShipDefinition definition = ParseShip(shipDict);
			if (!string.IsNullOrEmpty(definition.Name))
			{
				catalog[definition.Name] = definition;
			}
		}

		if (catalog.Count == 0)
		{
			GD.PrintErr($"Ship catalog at {CatalogPath} contained no valid ships. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		return catalog;
	}

	private static ShipDefinition ParseShip(Godot.Collections.Dictionary shipDict)
	{
		return new ShipDefinition
		{
			Name = shipDict.ContainsKey("Name") ? (string)shipDict["Name"] : string.Empty,
			SpritePath = shipDict.ContainsKey("SpritePath") ? (string)shipDict["SpritePath"] : string.Empty,
			BlueprintPath = shipDict.ContainsKey("BlueprintPath") ? (string)shipDict["BlueprintPath"] : string.Empty,
			MovementSoundPath = shipDict.ContainsKey("MovementSoundPath") ? (string)shipDict["MovementSoundPath"] : string.Empty,
			BaseActions = shipDict.ContainsKey("BaseActions") ? (int)shipDict["BaseActions"] : 0,
			MaxHP = shipDict.ContainsKey("MaxHP") ? (int)shipDict["MaxHP"] : 0,
			MaxShields = shipDict.ContainsKey("MaxShields") ? (int)shipDict["MaxShields"] : 0,
			AttackRange = shipDict.ContainsKey("AttackRange") ? (int)shipDict["AttackRange"] : 0,
			AttackDamage = shipDict.ContainsKey("AttackDamage") ? (int)shipDict["AttackDamage"] : 0,
			InitiativeBonus = shipDict.ContainsKey("InitiativeBonus") ? (int)shipDict["InitiativeBonus"] : 0,
			RotationOffset = shipDict.ContainsKey("RotationOffset") ? shipDict["RotationOffset"].AsSingle() : 0f,
			IsEnemyShip = shipDict.ContainsKey("IsEnemyShip") && (bool)shipDict["IsEnemyShip"]
		};
	}

	private static Dictionary<string, ShipDefinition> CreateFallbackCatalog()
	{
		return new Dictionary<string, ShipDefinition>
		{
			{
				"The Aether Skimmer",
				new ShipDefinition
				{
					Name = "The Aether Skimmer",
					SpritePath = "res://Ships/AetherSkimmerSprite.png",
					BlueprintPath = "res://Ships/AetherSkimmer.png",
					MovementSoundPath = "res://Sounds/AetherSkimmer.wav",
					BaseActions = 5,
					MaxHP = 20,
					MaxShields = 10,
					AttackRange = 1,
					AttackDamage = 20,
					InitiativeBonus = 5,
					RotationOffset = 0f,
					IsEnemyShip = false
				}
			},
			{
				"The Valkyrie Wing",
				new ShipDefinition
				{
					Name = "The Valkyrie Wing",
					SpritePath = "res://Ships/ValkyrieWingSprite.png",
					BlueprintPath = "res://Ships/ValkyrieWing.png",
					MovementSoundPath = "res://Sounds/ValkyrieWing.wav",
					BaseActions = 4,
					MaxHP = 30,
					MaxShields = 20,
					AttackRange = 2,
					AttackDamage = 15,
					InitiativeBonus = 3,
					RotationOffset = Mathf.Pi / 2f,
					IsEnemyShip = false
				}
			},
			{
				"The Genesis Ark",
				new ShipDefinition
				{
					Name = "The Genesis Ark",
					SpritePath = "res://Ships/GenesisArkSprite.png",
					BlueprintPath = "res://Ships/GenesisArk.png",
					MovementSoundPath = "res://Sounds/GenesisArk.wav",
					BaseActions = 3,
					MaxHP = 50,
					MaxShields = 30,
					AttackRange = 3,
					AttackDamage = 20,
					InitiativeBonus = 0,
					RotationOffset = Mathf.Pi / 2f,
					IsEnemyShip = false
				}
			},
			{
				"The Panacea Spire",
				new ShipDefinition
				{
					Name = "The Panacea Spire",
					SpritePath = "res://Ships/PanaceaSpireSprite.png",
					BlueprintPath = "res://Ships/PanaceaSpire.png",
					MovementSoundPath = "res://Sounds/PanaceaSpire.wav",
					BaseActions = 3,
					MaxHP = 40,
					MaxShields = 40,
					AttackRange = 2,
					AttackDamage = 15,
					InitiativeBonus = 0,
					RotationOffset = Mathf.Pi / 2f,
					IsEnemyShip = false
				}
			},
			{
				"The Relic Harvester",
				new ShipDefinition
				{
					Name = "The Relic Harvester",
					SpritePath = "res://Ships/RelicHarvesterSprite.png",
					BlueprintPath = "res://Ships/RelicHarvester.png",
					MovementSoundPath = "res://Sounds/RelicHarvester.mp3",
					BaseActions = 3,
					MaxHP = 50,
					MaxShields = 20,
					AttackRange = 1,
					AttackDamage = 35,
					InitiativeBonus = 0,
					RotationOffset = Mathf.Pi / 2f,
					IsEnemyShip = false
				}
			},
			{
				"The Neptune Forge",
				new ShipDefinition
				{
					Name = "The Neptune Forge",
					SpritePath = "res://Ships/NeptuneForgeSprite.png",
					BlueprintPath = "res://Ships/NeptuneForge.png",
					MovementSoundPath = "res://Sounds/NeptuneForge.wav",
					BaseActions = 2,
					MaxHP = 80,
					MaxShields = 20,
					AttackRange = 2,
					AttackDamage = 30,
					InitiativeBonus = -2,
					RotationOffset = Mathf.Pi,
					IsEnemyShip = false
				}
			},
			{
				"The Aegis Bastion",
				new ShipDefinition
				{
					Name = "The Aegis Bastion",
					SpritePath = "res://Ships/AegisBastionSprite.png",
					BlueprintPath = "res://Ships/AegisBastion.png",
					MovementSoundPath = "res://Sounds/HeavyThrusters.wav",
					BaseActions = 2,
					MaxHP = 100,
					MaxShields = 50,
					AttackRange = 2,
					AttackDamage = 25,
					InitiativeBonus = -2,
					RotationOffset = Mathf.Pi / 2f,
					IsEnemyShip = false
				}
			},
			{
				"Scrap-Stick Subversion Drone",
				new ShipDefinition
				{
					Name = "Scrap-Stick Subversion Drone",
					SpritePath = "res://EnemyShips/ScrapStickSubversionDroneSprite.png",
					BlueprintPath = "res://EnemyShips/ScraptickSubversionDrone.png",
					MovementSoundPath = string.Empty,
					BaseActions = 5,
					MaxHP = 20,
					MaxShields = 10,
					AttackRange = 1,
					AttackDamage = 20,
					InitiativeBonus = 5,
					RotationOffset = Mathf.Pi,
					IsEnemyShip = true
				}
			},
			{
				"Aether Censor Obelisk",
				new ShipDefinition
				{
					Name = "Aether Censor Obelisk",
					SpritePath = "res://EnemyShips/AetherCensorObeliskSprite.png",
					BlueprintPath = "res://EnemyShips/AetherCensorObelisk.png",
					MovementSoundPath = string.Empty,
					BaseActions = 4,
					MaxHP = 30,
					MaxShields = 20,
					AttackRange = 2,
					AttackDamage = 15,
					InitiativeBonus = 3,
					RotationOffset = 0f,
					IsEnemyShip = true
				}
			},
			{
				"Custodian Logic Barge",
				new ShipDefinition
				{
					Name = "Custodian Logic Barge",
					SpritePath = "res://EnemyShips/CustodianLogicBargeSprite.png",
					BlueprintPath = "res://EnemyShips/CustodianLogicBarge.png",
					MovementSoundPath = string.Empty,
					BaseActions = 3,
					MaxHP = 50,
					MaxShields = 30,
					AttackRange = 3,
					AttackDamage = 20,
					InitiativeBonus = 0,
					RotationOffset = 0f,
					IsEnemyShip = true
				}
			},
			{
				"Ignis Repurposed Terraformer",
				new ShipDefinition
				{
					Name = "Ignis Repurposed Terraformer",
					SpritePath = "res://EnemyShips/IgnisRepurposedTerraformerSprite.png",
					BlueprintPath = "res://EnemyShips/IgnisRepurposedTerraformer.png",
					MovementSoundPath = string.Empty,
					BaseActions = 2,
					MaxHP = 80,
					MaxShields = 20,
					AttackRange = 2,
					AttackDamage = 30,
					InitiativeBonus = -2,
					RotationOffset = 0f,
					IsEnemyShip = true
				}
			},
			{
				"Reformatter Dreadnought",
				new ShipDefinition
				{
					Name = "Reformatter Dreadnought",
					SpritePath = "res://EnemyShips/ReformatterDreadnoughtSprite.png",
					BlueprintPath = "res://EnemyShips/ReformatterDreadnought.png",
					MovementSoundPath = string.Empty,
					BaseActions = 2,
					MaxHP = 100,
					MaxShields = 50,
					AttackRange = 2,
					AttackDamage = 25,
					InitiativeBonus = -2,
					RotationOffset = Mathf.Pi,
					IsEnemyShip = true
				}
			}
		};
	}
}
