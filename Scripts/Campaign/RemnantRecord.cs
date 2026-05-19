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
			DefinitionPath = DefinitionPath
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
			{ "DefinitionPath", DefinitionPath }
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
			DefinitionPath = dict.ContainsKey("DefinitionPath") ? (string)dict["DefinitionPath"] : string.Empty
		};
	}
}
