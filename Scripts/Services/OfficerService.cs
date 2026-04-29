using Godot;
using System.Collections.Generic;
using System.Linq;

public enum OfficerApprovalEventType
{
	ScanPlanet,
	SalvagePlanet,
	RepairShip,
	RepairFleet,
	DistressSignalBroadcast,
	DistressSignalRescue,
	DistressSignalAmbush,
	PurchaseEquipment,
	EquipItem,
	SellAncientTech
}

public class OfficerApprovalContext
{
	public string ActingShipName { get; set; } = string.Empty;
	public string ItemName { get; set; } = string.Empty;
	public string ItemCategory { get; set; } = string.Empty;
}

public class OfficerApprovalChange
{
	public OfficerState Officer { get; set; }
	public int Delta { get; set; }
	public int OldApproval { get; set; }
	public int NewApproval { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string QueuedDowntimeEventId { get; set; } = string.Empty;
}

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
	private const int MinApproval = -100;
	private const int MaxApproval = 100;
	private const int SupportThreshold = 20;
	private const int LoyaltyThreshold = 50;
	private const int ConflictThreshold = -20;
	private const int BreakThreshold = -50;

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

	public List<OfficerState> GetActiveFleetOfficers()
	{
		List<OfficerState> officers = new List<OfficerState>();
		if (_globalData?.ShipOfficers == null)
		{
			return officers;
		}

		HashSet<string> seenShipNames = new HashSet<string>();
		foreach (string shipName in _globalData.SelectedPlayerFleet ?? new List<string>())
		{
			if (_globalData.ShipOfficers.TryGetValue(shipName, out OfficerState officer) && officer != null)
			{
				officers.Add(officer);
				seenShipNames.Add(shipName);
			}
		}

		foreach (KeyValuePair<string, OfficerState> kvp in _globalData.ShipOfficers)
		{
			if (kvp.Value != null && !seenShipNames.Contains(kvp.Key))
			{
				officers.Add(kvp.Value);
			}
		}

		return officers;
	}

	public List<OfficerApprovalChange> ApplyApprovalEvent(OfficerApprovalEventType eventType, OfficerApprovalContext context = null)
	{
		List<OfficerApprovalChange> changes = new List<OfficerApprovalChange>();
		foreach (OfficerState officer in GetActiveFleetOfficers())
		{
			officer.Flags ??= new List<string>();
			officer.CompletedScenes ??= new List<string>();

			int delta = CalculateApprovalDelta(officer, eventType, context);
			if (delta == 0)
			{
				continue;
			}

			int oldApproval = officer.Approval;
			officer.Approval = Mathf.Clamp(officer.Approval + delta, MinApproval, MaxApproval);
			delta = officer.Approval - oldApproval;
			if (delta == 0)
			{
				continue;
			}

			string downtimeEventId = QueueApprovalDowntimeEvent(officer, oldApproval, officer.Approval);
			changes.Add(new OfficerApprovalChange
			{
				Officer = officer,
				Delta = delta,
				OldApproval = oldApproval,
				NewApproval = officer.Approval,
				Reason = GetApprovalReason(eventType, context),
				QueuedDowntimeEventId = downtimeEventId
			});
		}

		return changes;
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

	private int CalculateApprovalDelta(OfficerState officer, OfficerApprovalEventType eventType, OfficerApprovalContext context)
	{
		if (officer == null)
		{
			return 0;
		}

		int delta = eventType switch
		{
			OfficerApprovalEventType.ScanPlanet => CalculateScanDelta(officer, context),
			OfficerApprovalEventType.SalvagePlanet => CalculateSalvageDelta(officer, context),
			OfficerApprovalEventType.RepairShip => CalculateRepairShipDelta(officer, context),
			OfficerApprovalEventType.RepairFleet => CalculateRepairFleetDelta(officer),
			OfficerApprovalEventType.DistressSignalBroadcast => CalculateDistressBroadcastDelta(officer),
			OfficerApprovalEventType.DistressSignalRescue => CalculateDistressRescueDelta(officer),
			OfficerApprovalEventType.DistressSignalAmbush => CalculateDistressAmbushDelta(officer),
			OfficerApprovalEventType.PurchaseEquipment => CalculatePurchaseDelta(officer, context),
			OfficerApprovalEventType.EquipItem => CalculateEquipDelta(officer, context),
			OfficerApprovalEventType.SellAncientTech => CalculateSellAncientTechDelta(officer),
			_ => 0
		};

		return Mathf.Clamp(delta, -3, 3);
	}

	private int CalculateScanDelta(OfficerState officer, OfficerApprovalContext context)
	{
		int delta = 0;
		if (officer.Archetype == "Scholar") delta += 2;
		if (officer.Ideology == "Expansionist") delta += 1;
		if (officer.Ideology == "TechnoReclamation") delta += 1;
		if (officer.Specialty == "Salvage Efficiency") delta += 1;
		if (officer.ShipName == context?.ActingShipName) delta += 1;
		return delta;
	}

	private int CalculateSalvageDelta(OfficerState officer, OfficerApprovalContext context)
	{
		int delta = 0;
		if (officer.Archetype == "Pragmatist") delta += 1;
		if (officer.Archetype == "Survivor") delta += 1;
		if (officer.Ideology == "TechnoReclamation") delta += 2;
		if (officer.Specialty == "Salvage Efficiency") delta += 2;
		if (officer.ShipName == context?.ActingShipName) delta += 1;
		return delta;
	}

	private int CalculateRepairShipDelta(OfficerState officer, OfficerApprovalContext context)
	{
		int delta = 0;
		if (officer.Ideology == "Humanitarian") delta += 1;
		if (officer.Archetype == "Idealist") delta += 1;
		if (officer.Specialty == "Medical Triage") delta += 2;
		if (officer.Specialty == "Shield Tuning") delta += 1;
		if (officer.Specialty == "Morale Support") delta += 1;
		if (officer.ShipName == context?.ActingShipName) delta += 2;
		return delta;
	}

	private int CalculateRepairFleetDelta(OfficerState officer)
	{
		int delta = 0;
		if (officer.Ideology == "Humanitarian") delta += 1;
		if (officer.Archetype == "Idealist") delta += 1;
		if (officer.Specialty == "Medical Triage") delta += 2;
		if (officer.Specialty == "Shield Tuning") delta += 1;
		if (officer.Specialty == "Morale Support") delta += 1;
		return delta;
	}

	private int CalculateDistressBroadcastDelta(OfficerState officer)
	{
		int delta = 0;
		if (officer.Ideology == "Humanitarian") delta += 2;
		if (officer.Archetype == "Idealist") delta += 1;
		if (officer.Archetype == "Survivor") delta += 1;
		if (officer.Specialty == "Morale Support") delta += 1;
		if (officer.Ideology == "Isolationist") delta -= 1;
		return delta;
	}

	private int CalculateDistressRescueDelta(OfficerState officer)
	{
		int delta = 0;
		if (officer.Ideology == "Humanitarian") delta += 2;
		if (officer.Archetype == "Idealist") delta += 1;
		if (officer.Archetype == "Survivor") delta += 1;
		if (officer.Specialty == "Morale Support") delta += 1;
		if (officer.Ideology == "Isolationist") delta -= 1;
		return delta;
	}

	private int CalculateDistressAmbushDelta(OfficerState officer)
	{
		int delta = 0;
		if (officer.Ideology == "Isolationist") delta += 2;
		if (officer.Archetype == "Pragmatist") delta += 1;
		if (officer.Flaw == "Vengeful") delta += 1;
		if (officer.Ideology == "Humanitarian") delta -= 1;
		return delta;
	}

	private int CalculatePurchaseDelta(OfficerState officer, OfficerApprovalContext context)
	{
		int delta = 0;
		if (officer.Archetype == "Pragmatist") delta += 1;
		if (officer.Ideology == "TechnoReclamation") delta += 1;
		if (CategorySupportsProtection(context?.ItemCategory) && officer.Ideology == "Humanitarian") delta += 1;
		if (CategoryMatchesSpecialty(officer, context?.ItemCategory)) delta += 1;
		return delta;
	}

	private int CalculateEquipDelta(OfficerState officer, OfficerApprovalContext context)
	{
		int delta = 0;
		if (officer.ShipName == context?.ActingShipName) delta += 2;
		if (CategoryMatchesSpecialty(officer, context?.ItemCategory)) delta += 1;
		if (CategorySupportsProtection(context?.ItemCategory) && officer.Ideology == "Humanitarian") delta += 1;
		return delta;
	}

	private int CalculateSellAncientTechDelta(OfficerState officer)
	{
		int delta = 0;
		if (officer.Ideology == "AntiAI") delta += 2;
		if (officer.Ideology == "Isolationist") delta += 1;
		if (officer.Ideology == "TechnoReclamation") delta -= 2;
		if (officer.Archetype == "Scholar") delta -= 1;
		return delta;
	}

	private bool CategorySupportsProtection(string itemCategory)
	{
		return itemCategory == GameConstants.EquipmentCategories.Armor ||
			itemCategory == GameConstants.EquipmentCategories.Shield;
	}

	private bool CategoryMatchesSpecialty(OfficerState officer, string itemCategory)
	{
		if (officer == null || string.IsNullOrEmpty(itemCategory))
		{
			return false;
		}

		return (itemCategory == GameConstants.EquipmentCategories.Weapon && officer.Specialty == "Tactical Command") ||
			(itemCategory == GameConstants.EquipmentCategories.Shield && officer.Specialty == "Shield Tuning") ||
			(itemCategory == GameConstants.EquipmentCategories.Armor && officer.Specialty == "Medical Triage") ||
			(itemCategory == GameConstants.EquipmentCategories.Missile && officer.Specialty == "Missile Control");
	}

	private string GetApprovalReason(OfficerApprovalEventType eventType, OfficerApprovalContext context)
	{
		return eventType switch
		{
			OfficerApprovalEventType.ScanPlanet => "the sensor sweep",
			OfficerApprovalEventType.SalvagePlanet => "the salvage operation",
			OfficerApprovalEventType.RepairShip => "the field repairs",
			OfficerApprovalEventType.RepairFleet => "the fleet-wide repairs",
			OfficerApprovalEventType.DistressSignalBroadcast => "broadcasting the distress signal",
			OfficerApprovalEventType.DistressSignalRescue => "accepting outside aid",
			OfficerApprovalEventType.DistressSignalAmbush => "how the distress signal played out",
			OfficerApprovalEventType.PurchaseEquipment => $"purchasing {context?.ItemName ?? "new equipment"}",
			OfficerApprovalEventType.EquipItem => $"upgrading {context?.ActingShipName ?? "the fleet"}",
			OfficerApprovalEventType.SellAncientTech => "selling ancient tech",
			_ => "recent command decisions"
		};
	}

	private string QueueApprovalDowntimeEvent(OfficerState officer, int oldApproval, int newApproval)
	{
		if (officer == null)
		{
			return string.Empty;
		}

		string eventId = TryQueueThresholdEvent(officer, oldApproval, newApproval, SupportThreshold, "support");
		if (!string.IsNullOrEmpty(eventId)) return eventId;

		eventId = TryQueueThresholdEvent(officer, oldApproval, newApproval, LoyaltyThreshold, "loyalty");
		if (!string.IsNullOrEmpty(eventId)) return eventId;

		eventId = TryQueueThresholdEvent(officer, oldApproval, newApproval, ConflictThreshold, "conflict");
		if (!string.IsNullOrEmpty(eventId)) return eventId;

		return TryQueueThresholdEvent(officer, oldApproval, newApproval, BreakThreshold, "break");
	}

	private string TryQueueThresholdEvent(OfficerState officer, int oldApproval, int newApproval, int threshold, string eventSuffix)
	{
		string flagId = $"approval_{eventSuffix}";
		bool crossed = threshold > 0
			? oldApproval < threshold && newApproval >= threshold
			: oldApproval > threshold && newApproval <= threshold;

		if (!crossed || officer.Flags.Contains(flagId))
		{
			return string.Empty;
		}

		officer.Flags.Add(flagId);
		string eventId = $"officer_{eventSuffix}_{officer.OfficerID}";
		if (_globalData != null && !_globalData.PendingDowntimeEvents.Contains(eventId))
		{
			_globalData.PendingDowntimeEvents.Add(eventId);
		}

		return eventId;
	}

	private string Sanitize(string value)
	{
		return value.ToLowerInvariant().Replace(" ", "_").Replace("-", "_").Replace("'", string.Empty);
	}
}
