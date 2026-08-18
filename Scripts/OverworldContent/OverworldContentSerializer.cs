using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileAccess = Godot.FileAccess;

public static class OverworldContentSerializer
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		WriteIndented = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
	};

	public static OverworldContentDefinition Load(string resourcePath, out string error)
	{
		error = string.Empty;
		using FileAccess file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			error = $"Could not open overworld content definition {resourcePath}.";
			return null;
		}

		try
		{
			OverworldContentDefinition definition = JsonSerializer.Deserialize<OverworldContentDefinition>(file.GetAsText(), Options);
			if (definition == null)
			{
				error = $"Overworld content definition {resourcePath} is empty.";
				return null;
			}
			if (definition.SchemaVersion != OverworldContentDefinition.CurrentSchemaVersion)
			{
				error = $"Overworld content definition {resourcePath} uses schema {definition.SchemaVersion}; expected {OverworldContentDefinition.CurrentSchemaVersion}.";
				return null;
			}

			Normalize(definition);
			definition.SourcePath = resourcePath;
			return definition;
		}
		catch (JsonException exception)
		{
			error = $"Overworld content JSON is invalid: {exception.Message}";
			return null;
		}
	}

	public static bool Save(OverworldContentDefinition definition, string resourcePath, out string error)
	{
		error = string.Empty;
		if (definition == null)
		{
			error = "No overworld content definition was supplied.";
			return false;
		}

		Normalize(definition);
		string directory = Path.GetDirectoryName(ProjectSettings.GlobalizePath(resourcePath));
		if (string.IsNullOrWhiteSpace(directory))
		{
			error = $"Definition path has no directory: {resourcePath}.";
			return false;
		}
		DirAccess.MakeDirRecursiveAbsolute(directory);
		using FileAccess file = FileAccess.Open(ProjectSettings.GlobalizePath(resourcePath), FileAccess.ModeFlags.Write);
		if (file == null)
		{
			error = $"Could not write overworld content definition {resourcePath}.";
			return false;
		}

		file.StoreString(Serialize(definition));
		definition.SourcePath = resourcePath;
		return true;
	}

	public static string Serialize(OverworldContentDefinition definition)
	{
		OverworldContentDefinition clone = JsonSerializer.Deserialize<OverworldContentDefinition>(JsonSerializer.Serialize(definition, Options), Options);
		Normalize(clone);
		clone.Placement.Regions.Sort(StringComparer.Ordinal);
		clone.Placement.RequiredFlags.Sort(StringComparer.Ordinal);
		clone.Placement.BlockedFlags.Sort(StringComparer.Ordinal);
		return JsonSerializer.Serialize(clone, Options) + "\n";
	}

	public static void Normalize(OverworldContentDefinition definition)
	{
		if (definition == null) return;
		definition.SchemaVersion = OverworldContentDefinition.CurrentSchemaVersion;
		definition.ContentId = definition.ContentId?.Trim() ?? string.Empty;
		definition.DisplayName = definition.DisplayName?.Trim() ?? string.Empty;
		definition.ContentReference = definition.ContentReference?.Trim() ?? string.Empty;
		definition.Placement ??= new OverworldPlacementRule();
		definition.Placement.Regions ??= new System.Collections.Generic.List<string>();
		definition.Placement.RequiredFlags ??= new System.Collections.Generic.List<string>();
		definition.Placement.BlockedFlags ??= new System.Collections.Generic.List<string>();
		definition.Placement.PreferredSpriteContains = definition.Placement.PreferredSpriteContains?.Trim() ?? string.Empty;
	}
}
