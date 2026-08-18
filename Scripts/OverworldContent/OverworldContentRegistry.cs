using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class OverworldContentRegistry
{
	public const string DefinitionDirectory = "res://Data/OverworldContent";

	private static readonly Dictionary<string, OverworldContentDefinition> Definitions = new Dictionary<string, OverworldContentDefinition>(StringComparer.OrdinalIgnoreCase);
	private static readonly List<OverworldContentIssue> Issues = new List<OverworldContentIssue>();
	private static bool _loaded;

	public static IReadOnlyList<OverworldContentDefinition> GetDefinitions()
	{
		EnsureLoaded();
		return Definitions.Values.OrderByDescending(definition => definition.Priority).ThenBy(definition => definition.ContentId, StringComparer.Ordinal).ToList();
	}

	public static OverworldContentDefinition GetDefinition(string contentId)
	{
		EnsureLoaded();
		return !string.IsNullOrWhiteSpace(contentId) && Definitions.TryGetValue(contentId, out OverworldContentDefinition definition) ? definition : null;
	}

	public static IReadOnlyList<OverworldContentIssue> GetIssues()
	{
		EnsureLoaded();
		return Issues.ToList();
	}

	public static void Reload()
	{
		_loaded = false;
		Definitions.Clear();
		Issues.Clear();
		EnsureLoaded();
	}

	private static void EnsureLoaded()
	{
		if (_loaded) return;
		_loaded = true;
		Definitions.Clear();
		Issues.Clear();

		List<string> paths = new List<string>();
		CollectDefinitionPaths(DefinitionDirectory, paths);
		foreach (string path in paths.OrderBy(path => path, StringComparer.Ordinal))
		{
			OverworldContentDefinition definition = OverworldContentSerializer.Load(path, out string error);
			if (definition == null)
			{
				Issues.Add(new OverworldContentIssue { Severity = OverworldContentIssueSeverity.Error, RuleId = "registry.load", Message = error, SourcePath = path });
				continue;
			}
			if (Definitions.ContainsKey(definition.ContentId))
			{
				Issues.Add(new OverworldContentIssue { Severity = OverworldContentIssueSeverity.Error, RuleId = "registry.duplicate", ContentId = definition.ContentId, Message = $"Content ID '{definition.ContentId}' is duplicated.", SourcePath = path });
				continue;
			}
			Definitions[definition.ContentId] = definition;
		}

		Issues.AddRange(OverworldContentValidator.Validate(Definitions.Values));
	}

	private static void CollectDefinitionPaths(string directoryPath, List<string> paths)
	{
		DirAccess directory = DirAccess.Open(directoryPath);
		if (directory == null)
		{
			Issues.Add(new OverworldContentIssue { Severity = OverworldContentIssueSeverity.Error, RuleId = "registry.directory", Message = $"Overworld content directory not found: {directoryPath}." });
			return;
		}

		directory.ListDirBegin();
		for (string entry = directory.GetNext(); !string.IsNullOrEmpty(entry); entry = directory.GetNext())
		{
			if (entry is "." or "..") continue;
			string path = $"{directoryPath.TrimEnd('/')}/{entry}";
			if (directory.CurrentIsDir()) CollectDefinitionPaths(path, paths);
			else if (entry.EndsWith(".overworld.json", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
		}
		directory.ListDirEnd();
	}
}
