using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class AmbientEventRegistry
{
	private const string EventDirectory = "res://Data/AmbientEvents";
	private static readonly Dictionary<string, AmbientEventDefinition> Events = new Dictionary<string, AmbientEventDefinition>(StringComparer.OrdinalIgnoreCase);
	private static bool _loaded;

	public static AmbientEventDefinition GetEvent(string eventId)
	{
		EnsureLoaded();
		return !string.IsNullOrWhiteSpace(eventId) && Events.TryGetValue(eventId, out AmbientEventDefinition definition)
			? definition
			: null;
	}

	public static IReadOnlyCollection<AmbientEventDefinition> GetEvents()
	{
		EnsureLoaded();
		return Events.Values;
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;
		List<string> paths = new List<string>();
		CollectEventPaths(EventDirectory, paths);
		if (paths.Count == 0)
		{
			GD.PushWarning($"No ambient event definitions found under: {EventDirectory}");
			return;
		}

		foreach (string path in paths)
		{
			LoadFile(path);
		}
	}

	private static void CollectEventPaths(string directoryPath, List<string> paths)
	{
		DirAccess directory = DirAccess.Open(directoryPath);
		if (directory == null) return;
		directory.ListDirBegin();
		for (string entry = directory.GetNext(); !string.IsNullOrEmpty(entry); entry = directory.GetNext())
		{
			if (entry is "." or "..") continue;
			string path = $"{directoryPath.TrimEnd('/')}/{entry}";
			if (directory.CurrentIsDir()) CollectEventPaths(path, paths);
			else if (entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
		}
		directory.ListDirEnd();
		paths.Sort(StringComparer.Ordinal);
	}

	private static void LoadFile(string path)
	{
		try
		{
			string json = FileAccess.GetFileAsString(path);
			AmbientEventDefinition definition = JsonSerializer.Deserialize<AmbientEventDefinition>(json, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
			});

			if (definition == null || string.IsNullOrWhiteSpace(definition.EventId))
			{
				GD.PushWarning($"Ambient event has no event_id: {path}");
				return;
			}
			if (!ValidateDefinition(definition, path))
			{
				return;
			}

			Events[definition.EventId] = definition;
		}
		catch (Exception exception)
		{
			GD.PushError($"Unable to load ambient event {path}: {exception.Message}");
		}
	}

	private static bool ValidateDefinition(AmbientEventDefinition definition, string path)
	{
		if (definition.Nodes == null || definition.Nodes.Count == 0 || definition.GetNode(definition.StartNodeId) == null)
		{
			GD.PushError($"Ambient event {path} has no valid start node '{definition.StartNodeId}'.");
			return false;
		}

		var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (AmbientEventNode node in definition.Nodes)
		{
			if (node == null || string.IsNullOrWhiteSpace(node.NodeId) || !nodeIds.Add(node.NodeId))
			{
				GD.PushError($"Ambient event {path} contains an empty or duplicate node id.");
				return false;
			}
		}

		foreach (AmbientEventNode node in definition.Nodes)
		{
			foreach (AmbientEventChoice choice in node.Choices ?? new List<AmbientEventChoice>())
			{
				if (choice == null || string.IsNullOrWhiteSpace(choice.ChoiceId))
				{
					GD.PushError($"Ambient event {path}, node {node.NodeId}, contains a choice without an id.");
					return false;
				}

				if (!string.IsNullOrWhiteSpace(choice.NextNodeId) && !nodeIds.Contains(choice.NextNodeId))
				{
					GD.PushError($"Ambient event {path}, choice {choice.ChoiceId}, references missing node {choice.NextNodeId}.");
					return false;
				}
			}
		}

		return true;
	}
}
