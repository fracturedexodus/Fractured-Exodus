using Godot;

public static class Database
{
	public static string[] EnemyShipTypes = new string[] {
		"Aether Censor Obelisk", "Custodian Logic Barge", "Ignis Repurposed Terraformer",
		"Reformatter Dreadnought", "Scrap-Stick Subversion Drone"
	};

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
		switch (shipName)
		{
			case "The Relic Harvester": return "res://Ships/RelicHarvesterSprite.png";
			case "The Panacea Spire": return "res://Ships/PanaceaSpireSprite.png";
			case "The Neptune Forge": return "res://Ships/NeptuneForgeSprite.png";
			case "The Genesis Ark": return "res://Ships/GenesisArkSprite.png";
			case "The Valkyrie Wing": return "res://Ships/ValkyrieWingSprite.png";
			case "The Aegis Bastion": return "res://Ships/AegisBastionSprite.png";
			case "The Aether Skimmer": return "res://Ships/AetherSkimmerSprite.png";
			
			case "Aether Censor Obelisk": return "res://EnemyShips/AetherCensorObeliskSprite.png";
			case "Custodian Logic Barge": return "res://EnemyShips/CustodianLogicBargeSprite.png";
			case "Ignis Repurposed Terraformer": return "res://EnemyShips/IgnisRepurposedTerraformerSprite.png";
			case "Reformatter Dreadnought": return "res://EnemyShips/ReformatterDreadnoughtSprite.png";
			case "Scrap-Stick Subversion Drone": return "res://EnemyShips/ScrapStickSubversionDroneSprite.png";
			default: return "res://icon.svg"; 
		}
	}

	public static string GetShipMovementSoundPath(string shipName)
	{
		switch (shipName)
		{
			case "The Valkyrie Wing": return "res://Sounds/ValkyrieWing.wav";
			case "The Aegis Bastion": return "res://Sounds/HeavyThrusters.wav";
			case "The Panacea Spire": return "res://Sounds/PanaceaSpire.wav";
			case "The Aether Skimmer": return "res://Sounds/AetherSkimmer.wav";
			case "The Genesis Ark": return "res://Sounds/GenesisArk.wav";
			case "The Neptune Forge": return "res://Sounds/NeptuneForge.wav";
			case "The Relic Harvester": return "res://Sounds/RelicHarvester.mp3";
			default: return ""; 
		}
	}

	public static int GetShipBaseActions(string shipName)
	{
		switch (shipName)
		{
			case "The Aether Skimmer": return 5;
			case "The Valkyrie Wing": return 4;
			case "The Genesis Ark": return 3;
			case "The Panacea Spire": return 3;
			case "The Relic Harvester": return 3;
			case "The Neptune Forge": return 2;
			case "The Aegis Bastion": return 2;
			
			case "Scrap-Stick Subversion Drone": return 5; 
			case "Aether Censor Obelisk": return 4; 
			case "Custodian Logic Barge": return 3; 
			case "Ignis Repurposed Terraformer": return 2; 
			case "Reformatter Dreadnought": return 2; 
			default: return 3; 
		}
	}

	public static (int hp, int shields) GetShipCombatStats(string shipName)
	{
		switch (shipName)
		{
			case "The Aegis Bastion": return (100, 50);   
			case "The Neptune Forge": return (80, 20);    
			case "The Genesis Ark": return (50, 30);      
			case "The Panacea Spire": return (40, 40);    
			case "The Relic Harvester": return (50, 20);  
			case "The Valkyrie Wing": return (30, 20);    
			case "The Aether Skimmer": return (20, 10);   
			
			case "Reformatter Dreadnought": return (100, 50); 
			case "Ignis Repurposed Terraformer": return (80, 20); 
			case "Custodian Logic Barge": return (50, 30); 
			case "Aether Censor Obelisk": return (30, 20); 
			case "Scrap-Stick Subversion Drone": return (20, 10); 
			default: return (50, 25); 
		}
	}

	public static (int range, int damage) GetShipWeaponStats(string shipName)
	{
		switch (shipName)
		{
			case "The Aegis Bastion": return (2, 25);   
			case "The Neptune Forge": return (2, 30);    
			case "The Genesis Ark": return (3, 20);      
			case "The Panacea Spire": return (2, 15);    
			case "The Relic Harvester": return (1, 35);  
			case "The Valkyrie Wing": return (2, 15);    
			case "The Aether Skimmer": return (1, 20);   
			
			case "Reformatter Dreadnought": return (2, 25); 
			case "Ignis Repurposed Terraformer": return (2, 30); 
			case "Custodian Logic Barge": return (3, 20); 
			case "Aether Censor Obelisk": return (2, 15); 
			case "Scrap-Stick Subversion Drone": return (1, 20); 
			default: return (2, 20); 
		}
	}

	public static int GetShipInitiativeBonus(string shipName)
	{
		switch (shipName)
		{
			case "The Aether Skimmer":
			case "Scrap-Stick Subversion Drone": 
				return 5; 
			
			case "The Valkyrie Wing":
			case "Aether Censor Obelisk": 
				return 3; 

			case "The Genesis Ark":
			case "The Panacea Spire":
			case "The Relic Harvester":
			case "Custodian Logic Barge": 
				return 0; 
			
			case "The Neptune Forge":
			case "Ignis Repurposed Terraformer":
			case "The Aegis Bastion":
			case "Reformatter Dreadnought": 
				return -2; 
				
			default: return 0; 
		}
	}

	public static float GetShipRotationOffset(string shipName)
	{
		switch (shipName)
		{
			case "The Genesis Ark":
			case "The Panacea Spire":
			case "The Relic Harvester":
			case "The Valkyrie Wing":
			case "The Aegis Bastion":
				return Mathf.Pi / 2f; 
				
			case "The Neptune Forge":
			case "Scrap-Stick Subversion Drone":
			case "Reformatter Dreadnought":
				return Mathf.Pi; 

			case "The Aether Skimmer":
			case "Aether Censor Obelisk":
			case "Custodian Logic Barge":
			case "Ignis Repurposed Terraformer":
				return 0f; 

			default:
				return 0f; 
		}
	}
}
