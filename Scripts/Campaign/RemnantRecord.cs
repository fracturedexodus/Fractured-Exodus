using Godot;

public class RemnantRecord
{
	public string RecordId { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Notes { get; set; } = string.Empty;
	public string MissionId { get; set; } = string.Empty;
	public string MissionTitle { get; set; } = string.Empty;
	public int RescuedOnTurn { get; set; }
	public string PortraitPath { get; set; } = string.Empty;
	public string DefinitionPath { get; set; } = string.Empty;
	public System.Collections.Generic.List<string> PersonalInventoryItemIDs { get; set; } = new System.Collections.Generic.List<string>();
	public OfficerStateSaveData StoredOfficerState { get; set; }

	public RemnantRecord Clone()
	{
		return new RemnantRecord
		{
			RecordId = RecordId,
			DisplayName = DisplayName,
			Description = Description,
			Notes = Notes,
			MissionId = MissionId,
			MissionTitle = MissionTitle,
			RescuedOnTurn = RescuedOnTurn,
			PortraitPath = PortraitPath,
			DefinitionPath = DefinitionPath,
			PersonalInventoryItemIDs = new System.Collections.Generic.List<string>(PersonalInventoryItemIDs ?? new System.Collections.Generic.List<string>()),
			StoredOfficerState = CloneStoredOfficerState(StoredOfficerState)
		};
	}

	public Godot.Collections.Dictionary<string, Variant> ToVariantDictionary()
	{
		return new Godot.Collections.Dictionary<string, Variant>
		{
			{ "RecordId", RecordId },
			{ "DisplayName", DisplayName },
			{ "Description", Description },
			{ "Notes", Notes },
			{ "MissionId", MissionId },
			{ "MissionTitle", MissionTitle },
			{ "RescuedOnTurn", RescuedOnTurn },
			{ "PortraitPath", PortraitPath },
			{ "DefinitionPath", DefinitionPath },
			{ "PersonalInventoryItemIDs", CampaignSaveData.ToVariantArray(PersonalInventoryItemIDs ?? new System.Collections.Generic.List<string>()) },
			{ "StoredOfficerState", StoredOfficerState?.ToVariantDictionary() ?? new Godot.Collections.Dictionary<string, Variant>() }
		};
	}

	public static RemnantRecord FromVariantDictionary(Godot.Collections.Dictionary dict)
	{
		return new RemnantRecord
		{
			RecordId = dict.ContainsKey("RecordId") ? (string)dict["RecordId"] : string.Empty,
			DisplayName = dict.ContainsKey("DisplayName") ? (string)dict["DisplayName"] : string.Empty,
			Description = dict.ContainsKey("Description") ? (string)dict["Description"] : string.Empty,
			Notes = dict.ContainsKey("Notes") ? (string)dict["Notes"] : string.Empty,
			MissionId = dict.ContainsKey("MissionId") ? (string)dict["MissionId"] : string.Empty,
			MissionTitle = dict.ContainsKey("MissionTitle") ? (string)dict["MissionTitle"] : string.Empty,
			RescuedOnTurn = dict.ContainsKey("RescuedOnTurn") ? (int)dict["RescuedOnTurn"] : 0,
			PortraitPath = dict.ContainsKey("PortraitPath") ? (string)dict["PortraitPath"] : string.Empty,
			DefinitionPath = dict.ContainsKey("DefinitionPath") ? (string)dict["DefinitionPath"] : string.Empty,
			PersonalInventoryItemIDs = CampaignSaveData.FromStringArray(dict.ContainsKey("PersonalInventoryItemIDs") ? (Godot.Collections.Array)dict["PersonalInventoryItemIDs"] : new Godot.Collections.Array()),
			StoredOfficerState = dict.ContainsKey("StoredOfficerState")
				&& dict["StoredOfficerState"].VariantType == Variant.Type.Dictionary
				&& ((Godot.Collections.Dictionary)dict["StoredOfficerState"]).Count > 0
					? OfficerStateSaveData.FromVariantDictionary((Godot.Collections.Dictionary)dict["StoredOfficerState"])
					: null
		};
	}

	private static OfficerStateSaveData CloneStoredOfficerState(OfficerStateSaveData officerState)
	{
		if (officerState == null)
		{
			return null;
		}

		return OfficerStateSaveData.FromRuntime(officerState.ToRuntime());
	}
}
