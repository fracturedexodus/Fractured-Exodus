using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileAccess = Godot.FileAccess;

public static class MissionDocumentCompiler
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

	public static MissionCompileResult Compile(MissionDocument document, string outputResourcePath)
	{
		MissionCompileResult result = new MissionCompileResult
		{
			OutputResourcePath = outputResourcePath,
			Issues = MissionDocumentValidator.Validate(document)
		};
		if (result.Issues.Any(issue => issue.Severity == MissionValidationSeverity.Error))
		{
			return result;
		}
		if (string.IsNullOrWhiteSpace(outputResourcePath))
		{
			result.Issues.Add(OutputError("No runtime layout output path was supplied."));
			return result;
		}

		List<Dictionary<string, object>> entries = new List<Dictionary<string, object>>
		{
			new Dictionary<string, object>
			{
				["item_type"] = "background",
				["background_id"] = document.Environment.BackgroundId
			}
		};

		foreach (MissionMapElement element in document.Elements.OrderBy(element => element.SourceOrder).ThenBy(element => element.Id, StringComparer.Ordinal))
		{
			Dictionary<string, object> entry = new Dictionary<string, object>
			{
				["item_type"] = GetLegacyItemType(element.Kind),
				["layer"] = GetLegacyLayer(element),
				["column"] = element.Column,
				["row"] = element.Row,
				["offset_x"] = element.OffsetX,
				["offset_y"] = element.OffsetY,
				["rotation_degrees"] = element.RotationDegrees,
				["flip_h"] = element.FlipH,
				["flip_v"] = element.FlipV
			};

			if (element.Kind == MissionMapElementKind.Marker) entry["marker_id"] = element.MarkerId;
			else entry["tile_id"] = element.TileId;
			entry["logic_role"] = element.Logic?.Role ?? string.Empty;
			entry["logic_label"] = element.Logic?.Label ?? string.Empty;
			entry["logic_target_id"] = element.Logic?.TargetId ?? string.Empty;
			entry["npc_definition_path"] = element.NpcDefinitionPath ?? string.Empty;
			entry["logic_npc_portrait"] = element.Logic?.NpcPortraitPath ?? string.Empty;
			entry["logic_required_flag"] = element.Logic?.RequiredFlag ?? string.Empty;
			entry["logic_set_flag"] = element.Logic?.SetFlag ?? string.Empty;
			entry["logic_trigger_mode"] = element.Logic?.TriggerMode ?? "none";
			entry["logic_once"] = element.Logic?.OneShot ?? false;
			entry["logic_notes"] = element.Logic?.Notes ?? string.Empty;
			entry["prop_definition_path"] = element.PropDefinitionPath ?? string.Empty;
			entries.Add(entry);
		}

		result.Content = JsonSerializer.Serialize(entries, JsonOptions) + "\n";
		string absoluteDirectory = Path.GetDirectoryName(ProjectSettings.GlobalizePath(outputResourcePath));
		if (string.IsNullOrWhiteSpace(absoluteDirectory))
		{
			result.Issues.Add(OutputError($"Runtime layout path has no directory: {outputResourcePath}."));
			return result;
		}
		DirAccess.MakeDirRecursiveAbsolute(absoluteDirectory);
		using FileAccess file = FileAccess.Open(ProjectSettings.GlobalizePath(outputResourcePath), FileAccess.ModeFlags.Write);
		if (file == null)
		{
			result.Issues.Add(OutputError($"Could not write runtime layout {outputResourcePath}."));
			return result;
		}
		file.StoreString(result.Content);
		result.Success = true;
		return result;
	}

	private static MissionValidationIssue OutputError(string message)
	{
		return new MissionValidationIssue { RuleId = "compile.output", Severity = MissionValidationSeverity.Error, Message = message };
	}

	public static string GetPlaytestOutputPath(MissionDocument document)
	{
		return $"user://mission_workbench_v2/compiled/{document.LayoutName}.json";
	}

	private static string GetLegacyItemType(MissionMapElementKind kind) => kind switch
	{
		MissionMapElementKind.Marker => "marker",
		MissionMapElementKind.PlacedProp => "placed_prop",
		_ => "tile"
	};

	private static string GetLegacyLayer(MissionMapElement element)
	{
		if (element.Kind == MissionMapElementKind.Marker) return "marker";
		if (element.Kind == MissionMapElementKind.PlacedProp) return "prop";
		return MissionTileCatalog.TryGetById(element.TileId, out MissionTileDefinition definition)
			? definition.Category.ToString().ToLowerInvariant()
			: "prop";
	}
}
