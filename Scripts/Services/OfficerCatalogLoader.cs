using Godot;
using System.Collections.Generic;

public static class OfficerCatalogLoader
{
	private const string CatalogPath = "res://Data/officer_templates.json";

	public static Dictionary<string, OfficerTemplate> LoadCatalog()
	{
		if (!FileAccess.FileExists(CatalogPath))
		{
			GD.PrintErr($"Officer catalog not found at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		using FileAccess file = FileAccess.Open(CatalogPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr($"Failed to open officer catalog at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		Json json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok)
		{
			GD.PrintErr($"Failed to parse officer catalog at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		Godot.Collections.Array officerArray = (Godot.Collections.Array)json.Data;
		Dictionary<string, OfficerTemplate> catalog = new Dictionary<string, OfficerTemplate>();
		foreach (Variant officerVariant in officerArray)
		{
			Godot.Collections.Dictionary officerDict = (Godot.Collections.Dictionary)officerVariant;
			OfficerTemplate template = ParseOfficer(officerDict);
			if (!string.IsNullOrEmpty(template.OfficerID))
			{
				catalog[template.OfficerID] = template;
			}
		}

		if (catalog.Count == 0)
		{
			GD.PrintErr($"Officer catalog at {CatalogPath} contained no valid officers. Using fallback catalog.");
			return CreateFallbackCatalog();
		}

		return catalog;
	}

	private static OfficerTemplate ParseOfficer(Godot.Collections.Dictionary officerDict)
	{
		return new OfficerTemplate
		{
			OfficerID = officerDict.ContainsKey("OfficerID") ? (string)officerDict["OfficerID"] : string.Empty,
			ShipName = officerDict.ContainsKey("ShipName") ? (string)officerDict["ShipName"] : string.Empty,
			Name = officerDict.ContainsKey("Name") ? (string)officerDict["Name"] : string.Empty,
			PortraitPath = officerDict.ContainsKey("PortraitPath") ? (string)officerDict["PortraitPath"] : string.Empty,
			Biography = officerDict.ContainsKey("Biography") ? (string)officerDict["Biography"] : string.Empty,
			Ideology = officerDict.ContainsKey("Ideology") ? (string)officerDict["Ideology"] : string.Empty,
			Archetype = officerDict.ContainsKey("Archetype") ? (string)officerDict["Archetype"] : string.Empty,
			Specialty = officerDict.ContainsKey("Specialty") ? (string)officerDict["Specialty"] : string.Empty,
			Flaw = officerDict.ContainsKey("Flaw") ? (string)officerDict["Flaw"] : string.Empty,
			StartingApproval = officerDict.ContainsKey("StartingApproval") ? (int)officerDict["StartingApproval"] : 0,
			PersonalQuestID = officerDict.ContainsKey("PersonalQuestID") ? (string)officerDict["PersonalQuestID"] : string.Empty,
			CombatAbilityID = officerDict.ContainsKey("CombatAbilityID") ? (string)officerDict["CombatAbilityID"] : string.Empty
		};
	}

	private static Dictionary<string, OfficerTemplate> CreateFallbackCatalog()
	{
		return new Dictionary<string, OfficerTemplate>
		{
			{
				"preset_aether_skimmer",
				new OfficerTemplate
				{
					OfficerID = "preset_aether_skimmer",
					ShipName = "The Aether Skimmer",
					Name = "Lyra Voss",
					PortraitPath = "res://Assets/Officers/LyraVoss.png",
					Biography = "A daring pathfinder who still believes the fastest route through collapse is forward.",
					Ideology = "Expansionist",
					Archetype = "Survivor",
					Specialty = "Engine Routing",
					Flaw = "Reckless",
					StartingApproval = 5,
					PersonalQuestID = "officer_lyra_voss",
					CombatAbilityID = "EvasiveBurn"
				}
			},
			{
				"preset_valkyrie_wing",
				new OfficerTemplate
				{
					OfficerID = "preset_valkyrie_wing",
					ShipName = "The Valkyrie Wing",
					Name = "Cassian Rook",
					PortraitPath = "res://Assets/Officers/CassianRook.png",
					Biography = "An escort ace who measures success by how many civilians make it through alive.",
					Ideology = "Humanitarian",
					Archetype = "Pragmatist",
					Specialty = "Tactical Command",
					Flaw = "Rigid",
					StartingApproval = 0,
					PersonalQuestID = "officer_cassian_rook",
					CombatAbilityID = "CoordinatedStrike"
				}
			},
			{
				"preset_genesis_ark",
				new OfficerTemplate
				{
					OfficerID = "preset_genesis_ark",
					ShipName = "The Genesis Ark",
					Name = "Mara Ilyan",
					PortraitPath = "res://Assets/Officers/MaraIlyan.png",
					Biography = "A colony steward who carries memory, law, and hope for the refugees in her care.",
					Ideology = "Humanitarian",
					Archetype = "Idealist",
					Specialty = "Morale Support",
					Flaw = "Secretive",
					StartingApproval = 10,
					PersonalQuestID = "officer_mara_ilyan",
					CombatAbilityID = "HoldFast"
				}
			},
			{
				"preset_panacea_spire",
				new OfficerTemplate
				{
					OfficerID = "preset_panacea_spire",
					ShipName = "The Panacea Spire",
					Name = "Dr. Soren Vale",
					PortraitPath = "res://Assets/Officers/SorenVale.png",
					Biography = "A triage director balancing impossible care decisions after one quarantine broke his world.",
					Ideology = "Humanitarian",
					Archetype = "Scholar",
					Specialty = "Medical Triage",
					Flaw = "Fearful of AI",
					StartingApproval = 5,
					PersonalQuestID = "officer_soren_vale",
					CombatAbilityID = "EmergencyTriage"
				}
			},
			{
				"preset_relic_harvester",
				new OfficerTemplate
				{
					OfficerID = "preset_relic_harvester",
					ShipName = "The Relic Harvester",
					Name = "Tamsin Kreel",
					PortraitPath = "res://Assets/Officers/TamsinKreel.png",
					Biography = "A salvager-scholar convinced that one buried relic could still rewrite the fleet's fate.",
					Ideology = "Techno-Reclamation",
					Archetype = "Scholar",
					Specialty = "Salvage Efficiency",
					Flaw = "Obsessive",
					StartingApproval = 0,
					PersonalQuestID = "officer_tamsin_kreel",
					CombatAbilityID = "WeakpointScan"
				}
			},
			{
				"preset_neptune_forge",
				new OfficerTemplate
				{
					OfficerID = "preset_neptune_forge",
					ShipName = "The Neptune Forge",
					Name = "Brakk Tenor",
					PortraitPath = "res://Assets/Officers/BrakkTenor.png",
					Biography = "An industrial foreman who trusts steel, fuel, and hard choices more than anyone outside the fleet.",
					Ideology = "Isolationist",
					Archetype = "Pragmatist",
					Specialty = "Shield Tuning",
					Flaw = "Vengeful",
					StartingApproval = 0,
					PersonalQuestID = "officer_brakk_tenor",
					CombatAbilityID = "ForgePlating"
				}
			},
			{
				"preset_aegis_bastion",
				new OfficerTemplate
				{
					OfficerID = "preset_aegis_bastion",
					ShipName = "The Aegis Bastion",
					Name = "Seraph Nox",
					PortraitPath = "res://Assets/Officers/SeraphNox.png",
					Biography = "A fortress captain who sees the Shattering as the final indictment of unbound machine power.",
					Ideology = "AntiAI",
					Archetype = "Zealot",
					Specialty = "Missile Control",
					Flaw = "Rigid",
					StartingApproval = -5,
					PersonalQuestID = "officer_seraph_nox",
					CombatAbilityID = "MissileLock"
				}
			}
		};
	}
}
