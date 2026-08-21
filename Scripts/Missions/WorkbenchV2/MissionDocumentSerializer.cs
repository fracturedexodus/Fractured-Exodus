using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileAccess = Godot.FileAccess;

public static class MissionDocumentSerializer
{
	public const string DocumentDirectory = "res://Data/Missions/Workbench";

	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	public static string GetDocumentResourcePath(string missionId)
	{
		return $"{DocumentDirectory}/{NormalizeFileId(missionId)}.mission.json";
	}

	public static MissionDocument Load(string resourcePath, out string error)
	{
		error = string.Empty;
		using FileAccess file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			error = $"Could not open mission document {resourcePath}.";
			return null;
		}

		try
		{
			MissionDocument document = JsonSerializer.Deserialize<MissionDocument>(file.GetAsText(), Options);
			if (document == null)
			{
				error = $"Mission document {resourcePath} was empty.";
				return null;
			}
			if (document.SchemaVersion != MissionDocument.CurrentSchemaVersion)
			{
				error = $"Mission document {resourcePath} uses schema {document.SchemaVersion}; expected {MissionDocument.CurrentSchemaVersion}.";
				return null;
			}

			Normalize(document);
			return document;
		}
		catch (JsonException exception)
		{
			error = $"Mission document JSON is invalid: {exception.Message}";
			return null;
		}
	}

	public static bool Save(MissionDocument document, string resourcePath, out string error)
	{
		error = string.Empty;
		if (document == null)
		{
			error = "No mission document was supplied.";
			return false;
		}

		Normalize(document);
		string absoluteDirectory = Path.GetDirectoryName(ProjectSettings.GlobalizePath(resourcePath));
		if (string.IsNullOrWhiteSpace(absoluteDirectory))
		{
			error = $"Mission document path has no directory: {resourcePath}";
			return false;
		}

		DirAccess.MakeDirRecursiveAbsolute(absoluteDirectory);
		using FileAccess file = FileAccess.Open(ProjectSettings.GlobalizePath(resourcePath), FileAccess.ModeFlags.Write);
		if (file == null)
		{
			error = $"Could not write mission document {resourcePath}.";
			return false;
		}

		file.StoreString(Serialize(document));
		return true;
	}

	public static string Serialize(MissionDocument document)
	{
		MissionDocument snapshot = Clone(document);
		Normalize(snapshot);
		snapshot.Elements = snapshot.Elements.OrderBy(element => element.SourceOrder).ThenBy(element => element.Id, StringComparer.Ordinal).ToList();
		snapshot.FlowNodes = snapshot.FlowNodes.OrderBy(node => node.Id, StringComparer.Ordinal).ToList();
		snapshot.DialogueConversationIds = snapshot.DialogueConversationIds.Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
		snapshot.Outcomes = snapshot.Outcomes.OrderBy(outcome => outcome.Id, StringComparer.Ordinal).ToList();
		return JsonSerializer.Serialize(snapshot, Options) + "\n";
	}

	public static MissionDocument Clone(MissionDocument document)
	{
		return JsonSerializer.Deserialize<MissionDocument>(JsonSerializer.Serialize(document, Options), Options) ?? new MissionDocument();
	}

	public static void Normalize(MissionDocument document)
	{
		document.SchemaVersion = MissionDocument.CurrentSchemaVersion;
		document.Metadata ??= new MissionDocumentMetadata();
		document.Environment ??= new MissionEnvironmentData();
		document.Elements ??= new List<MissionMapElement>();
		document.FlowNodes ??= new List<MissionFlowNodeData>();
		document.DialogueConversationIds ??= new List<string>();
		document.Outcomes ??= new List<MissionOutcomeData>();
		document.Authoring ??= new MissionAuthoringData();
		document.Authoring.Rules ??= new List<MissionRuleData>();
		foreach (MissionMapElement element in document.Elements.Where(element => element != null))
		{
			element.Logic ??= new MissionElementLogic();
		}
	}

	private static string NormalizeFileId(string value)
	{
		string normalized = string.IsNullOrWhiteSpace(value) ? "untitled_mission" : value.Trim().ToLowerInvariant();
		return string.Concat(normalized.Select(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_'));
	}
}
