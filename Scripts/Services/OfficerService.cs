using Godot;
using System.Collections.Generic;
using System.Linq;

public class CustomOfficerRequest
{
	public string ShipName { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string PortraitPath { get; set; } = string.Empty;
	public string Archetype { get; set; } = string.Empty;
	public string Ideology { get; set; } = string.Empty;
	public string Specialty { get; set; } = string.Empty;
	public string Flaw { get; set; } = string.Empty;
	public string BiographySeed { get; set; } = string.Empty;
}

public class OfficerService
{
	private readonly GlobalData _globalData;

	public static readonly string[] Archetypes = { "Idealist", "Pragmatist", "Scholar", "Survivor", "Zealot" };
	public static readonly string[] Ideologies = { "Humanitarian", "Expansionist", "AntiAI", "TechnoReclamation", "Isolationist" };
	public static readonly string[] Specialties = { "Tactical Command", "Shield Tuning", "Salvage Efficiency", "Medical Triage", "Missile Control", "Engine Routing", "Morale Support" };
	public static readonly string[] Flaws = { "Reckless", "Rigid", "Secretive", "Vengeful", "Fearful of AI" };
	public static readonly string[] BiographySeeds = { "Refugee Convoy Veteran", "Former Colony Administrator", "Ex-Custodian Technician", "Salvage Freebooter", "Medical Responder", "Faithful Pilgrim", "Black Market Defector" };

	public OfficerService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public OfficerTemplate GetPresetTemplateForShip(string shipName)
	{
		return _globalData.MasterOfficerDB.Values.FirstOrDefault(template => template.ShipName == shipName);
	}

	public List<string> GetPortraitOptions()
	{
		return _globalData.MasterOfficerDB.Values
			.Select(template => template.PortraitPath)
			.Where(path => !string.IsNullOrEmpty(path))
			.Distinct()
			.ToList();
	}

	public OfficerState CreatePresetOfficer(string shipName)
	{
		OfficerTemplate template = GetPresetTemplateForShip(shipName);
		if (template == null)
		{
			return new OfficerState
			{
				OfficerID = $"preset_missing_{Sanitize(shipName)}",
				TemplateOfficerID = string.Empty,
				ShipName = shipName,
				DisplayName = "Unassigned Officer",
				IsCustom = false,
				PortraitPath = string.Empty,
				Biography = "No preset officer template found for this ship.",
				Ideology = "Humanitarian",
				Archetype = "Survivor",
				Specialty = "Tactical Command",
				Flaw = "Rigid",
				Approval = 0,
				CombatAbilityID = "TacticalCommand"
			};
		}

		return new OfficerState
		{
			OfficerID = template.OfficerID,
			TemplateOfficerID = template.OfficerID,
			ShipName = shipName,
			DisplayName = template.Name,
			IsCustom = false,
			PortraitPath = template.PortraitPath,
			Biography = template.Biography,
			Ideology = template.Ideology,
			Archetype = template.Archetype,
			Specialty = template.Specialty,
			Flaw = template.Flaw,
			Approval = template.StartingApproval,
			Stress = 0,
			CombatAbilityID = template.CombatAbilityID,
			PersonalQuestID = template.PersonalQuestID
		};
	}

	public OfficerState CreateCustomOfficer(CustomOfficerRequest request)
	{
		string shipName = request.ShipName ?? string.Empty;
		string displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? $"Officer {shipName}" : request.DisplayName.Trim();
		string portraitPath = !string.IsNullOrEmpty(request.PortraitPath) ? request.PortraitPath : GetPresetTemplateForShip(shipName)?.PortraitPath ?? string.Empty;
		string specialty = string.IsNullOrEmpty(request.Specialty) ? Specialties[0] : request.Specialty;

		return new OfficerState
		{
			OfficerID = $"custom_{Sanitize(shipName)}",
			TemplateOfficerID = string.Empty,
			ShipName = shipName,
			DisplayName = displayName,
			IsCustom = true,
			PortraitPath = portraitPath,
			Biography = BuildCustomBiography(request),
			BiographySeed = string.IsNullOrEmpty(request.BiographySeed) ? BiographySeeds[0] : request.BiographySeed,
			Ideology = string.IsNullOrEmpty(request.Ideology) ? Ideologies[0] : request.Ideology,
			Archetype = string.IsNullOrEmpty(request.Archetype) ? Archetypes[0] : request.Archetype,
			Specialty = specialty,
			Flaw = string.IsNullOrEmpty(request.Flaw) ? Flaws[0] : request.Flaw,
			Approval = 0,
			Stress = 0,
			CombatAbilityID = GetAbilityForSpecialty(specialty),
			PersonalQuestID = string.Empty
		};
	}

	public void AssignOfficerToShip(string shipName, OfficerState officer)
	{
		if (string.IsNullOrEmpty(shipName) || officer == null)
		{
			return;
		}

		officer.ShipName = shipName;
		_globalData.ShipOfficers[shipName] = officer;
	}

	public OfficerState GetOfficerForShip(string shipName)
	{
		return _globalData.ShipOfficers.TryGetValue(shipName, out OfficerState officer) ? officer : null;
	}

	private string BuildCustomBiography(CustomOfficerRequest request)
	{
		string seed = string.IsNullOrEmpty(request.BiographySeed) ? BiographySeeds[0] : request.BiographySeed;
		string archetype = string.IsNullOrEmpty(request.Archetype) ? Archetypes[0] : request.Archetype;
		string ideology = string.IsNullOrEmpty(request.Ideology) ? Ideologies[0] : request.Ideology;
		string flaw = string.IsNullOrEmpty(request.Flaw) ? Flaws[0] : request.Flaw;
		return $"{seed}. A {archetype.ToLower()} officer aligned with {ideology} ideals, but burdened by a {flaw.ToLower()} streak.";
	}

	private string GetAbilityForSpecialty(string specialty)
	{
		return specialty switch
		{
			"Tactical Command" => "CoordinatedStrike",
			"Shield Tuning" => "ForgePlating",
			"Salvage Efficiency" => "WeakpointScan",
			"Medical Triage" => "EmergencyTriage",
			"Missile Control" => "MissileLock",
			"Engine Routing" => "EvasiveBurn",
			"Morale Support" => "HoldFast",
			_ => "TacticalCommand"
		};
	}

	private string Sanitize(string value)
	{
		return value.ToLowerInvariant().Replace(" ", "_").Replace("-", "_").Replace("'", string.Empty);
	}
}
