using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class AmbientEventRegistry
{
	private const string EventDirectory = "res://Data/AmbientEvents/LuminousVerge";
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
		DirAccess directory = DirAccess.Open(EventDirectory);
		if (directory == null)
		{
			GD.PushWarning($"Ambient event directory not found: {EventDirectory}");
			return;
		}

		directory.ListDirBegin();
		for (string fileName = directory.GetNext(); !string.IsNullOrEmpty(fileName); fileName = directory.GetNext())
		{
			if (directory.CurrentIsDir() || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			LoadFile($"{EventDirectory}/{fileName}");
		}
		directory.ListDirEnd();
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
