using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class LegacyMissionImporter
{
	public static MissionImportResult Import(MissionTemplate template)
	{
		MissionImportResult result = new MissionImportResult();
		if (template == null)
		{
			result.Errors.Add("Mission template is missing.");
			return result;
		}
		if (string.IsNullOrWhiteSpace(template.LayoutResourcePath))
		{
			result.Errors.Add($"Mission {template.MissionId} has no layout path.");
			return result;
		}

		using FileAccess file = FileAccess.Open(template.LayoutResourcePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			result.Errors.Add($"Could not open legacy layout {template.LayoutResourcePath}.");
			return result;
		}

		try
		{
			using JsonDocument json = JsonDocument.Parse(file.GetAsText());
			if (json.RootElement.ValueKind != JsonValueKind.Array)
			{
				result.Errors.Add("Legacy mission layout root must be an array.");
				return result;
			}

			MissionDocument document = BuildDocument(template);
			int sourceOrder = 0;
			foreach (JsonElement entry in json.RootElement.EnumerateArray())
			{
				if (entry.ValueKind != JsonValueKind.Object)
				{
					result.Errors.Add($"Legacy layout entry {sourceOrder} is not an object.");
					sourceOrder++;
					continue;
				}

				string itemType = GetString(entry, "item_type", "tile");
				if (itemType == "background")
				{
					document.Environment.BackgroundId = GetString(entry, "background_id", MissionBackgroundCatalog.DefaultId);
					sourceOrder++;
					continue;
				}

				MissionMapElement element = ParseElement(entry, itemType, sourceOrder);
				document.Elements.Add(element);
				MissionFlowNodeData flowNode = BuildFlowNode(element, document.FlowNodes.Count);
				if (flowNode != null)
				{
					document.FlowNodes.Add(flowNode);
				}

				if (element.MarkerId == "trigger_dialogue" && !string.IsNullOrWhiteSpace(element.Logic.TargetId))
				{
					document.DialogueConversationIds.Add(element.Logic.TargetId);
				}
				sourceOrder++;
			}

			MissionDocumentReferenceResolver.RefreshDialogueIds(document);
			result.Document = document;
			return result;
		}
		catch (JsonException exception)
		{
			result.Errors.Add($"Legacy mission JSON is invalid: {exception.Message}");
			return result;
		}
	}

	private static MissionDocument BuildDocument(MissionTemplate template)
	{
		MissionDocument document = new MissionDocument
		{
			MissionId = template.MissionId,
			LayoutName = System.IO.Path.GetFileNameWithoutExtension(template.LayoutResourcePath),
			Metadata = new MissionDocumentMetadata
			{
				Title = template.Title,
				Description = template.Description,
				MissionScenePath = template.MissionScenePath,
				DefaultReturnScenePath = template.DefaultReturnScenePath,
				RecommendedOfficerCount = template.RecommendedOfficerCount,
				SourceNodeType = template.SourceNodeType,
				ObjectiveText = template.ObjectiveText,
				PromptText = template.PromptText,
				DefaultDialogueId = template.DefaultDialogueId,
				InteractionKeys = template.InteractionKeys?.ToList() ?? new List<string>(),
				IsEnabled = template.IsEnabled
			}
		};

		AddOutcome(document, template.PrimaryOutcomeId, template.PrimaryActionText, template.PrimaryOutcomeRequiredFlags, template.PrimaryOutcomeBlockedFlags);
		AddOutcome(document, template.SecondaryOutcomeId, template.SecondaryActionText, template.SecondaryOutcomeRequiredFlags, template.SecondaryOutcomeBlockedFlags);
		if (!string.IsNullOrWhiteSpace(template.DefaultDialogueId))
		{
			document.DialogueConversationIds.Add(template.DefaultDialogueId);
		}
		return document;
	}

	private static void AddOutcome(
		MissionDocument document,
		string id,
		string actionText,
		Godot.Collections.Array<string> requiredFlags,
		Godot.Collections.Array<string> blockedFlags)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return;
		}

		document.Outcomes.Add(new MissionOutcomeData
		{
			Id = id,
			ActionText = actionText,
			RequiredFlags = requiredFlags?.ToList() ?? new List<string>(),
			BlockedFlags = blockedFlags?.ToList() ?? new List<string>()
		});
	}

	private static MissionMapElement ParseElement(JsonElement entry, string itemType, int sourceOrder)
	{
		MissionMapElementKind kind = itemType switch
		{
			"marker" => MissionMapElementKind.Marker,
			"placed_prop" => MissionMapElementKind.PlacedProp,
			_ => MissionMapElementKind.Tile
		};
		string tileId = GetString(entry, "tile_id");
		string markerId = GetString(entry, "marker_id");
		int column = GetInt(entry, "column");
		int row = GetInt(entry, "row");
		string targetId = GetString(entry, "logic_target_id");
		string identity = $"{kind}|{tileId}|{markerId}|{column}|{row}|{targetId}|{sourceOrder}";

		return new MissionMapElement
		{
			Id = $"map_{BuildStableSuffix(identity)}",
			SourceOrder = sourceOrder,
			Kind = kind,
			TileId = tileId,
			MarkerId = markerId,
			Column = column,
			Row = row,
			OffsetX = GetFloat(entry, "offset_x"),
			OffsetY = GetFloat(entry, "offset_y"),
			RotationDegrees = GetFloat(entry, "rotation_degrees"),
			FlipH = GetBool(entry, "flip_h"),
			FlipV = GetBool(entry, "flip_v"),
			PropDefinitionPath = GetString(entry, "prop_definition_path"),
			NpcDefinitionPath = GetString(entry, "npc_definition_path"),
			Logic = new MissionElementLogic
			{
				Role = GetString(entry, "logic_role", kind == MissionMapElementKind.Marker ? "marker" : string.Empty),
				Label = GetString(entry, "logic_label"),
				TargetId = targetId,
				NpcPortraitPath = GetString(entry, "logic_npc_portrait"),
				RequiredFlag = GetString(entry, "logic_required_flag"),
				SetFlag = GetString(entry, "logic_set_flag"),
				TriggerMode = GetString(entry, "logic_trigger_mode", "none"),
				OneShot = GetBool(entry, "logic_once"),
				Notes = GetString(entry, "logic_notes")
			}
		};
	}

	private static MissionFlowNodeData BuildFlowNode(MissionMapElement element, int index)
	{
		bool isSpawn = element.Kind == MissionMapElementKind.Marker
			&& (element.MarkerId.StartsWith("spawn_") || element.MarkerId == "npc_spawn" || element.MarkerId == "hostile_spawn");
		bool hasLogic = isSpawn
			|| element.Kind == MissionMapElementKind.Marker
			|| !string.IsNullOrWhiteSpace(element.Logic.Role)
			|| !string.IsNullOrWhiteSpace(element.Logic.SetFlag)
			|| !string.IsNullOrWhiteSpace(element.Logic.RequiredFlag);
		if (!hasLogic)
		{
			return null;
		}

		MissionFlowNodeKind kind = isSpawn
			? MissionFlowNodeKind.Spawn
			: element.MarkerId.StartsWith("objective_")
				? MissionFlowNodeKind.Objective
				: element.Logic.TriggerMode == "interact"
					? MissionFlowNodeKind.Interaction
					: MissionFlowNodeKind.Trigger;
		return new MissionFlowNodeData
		{
			Id = $"flow_{element.Id}",
			Kind = kind,
			MapElementId = element.Id,
			Label = string.IsNullOrWhiteSpace(element.Logic.Label)
				? !string.IsNullOrWhiteSpace(element.MarkerId) ? element.MarkerId : element.TileId
				: element.Logic.Label,
			TriggerMode = element.Logic.TriggerMode,
			RequiredFlags = SplitFlags(element.Logic.RequiredFlag),
			SetFlags = SplitFlags(element.Logic.SetFlag),
			TargetId = element.Logic.TargetId,
			CanvasX = (index % 4) * 280f,
			CanvasY = (index / 4) * 190f
		};
	}

	private static List<string> SplitFlags(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new List<string>()
			: value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
	}

	private static string BuildStableSuffix(string identity)
	{
		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
		return Convert.ToHexString(hash).Substring(0, 12).ToLowerInvariant();
	}

	private static string GetString(JsonElement element, string name, string fallback = "")
	{
		return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? fallback
			: fallback;
	}

	private static int GetInt(JsonElement element, string name)
	{
		return element.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result) ? result : 0;
	}

	private static float GetFloat(JsonElement element, string name)
	{
		return element.TryGetProperty(name, out JsonElement value) && value.TryGetSingle(out float result) ? result : 0f;
	}

	private static bool GetBool(JsonElement element, string name)
	{
		return element.TryGetProperty(name, out JsonElement value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) && value.GetBoolean();
	}
}
