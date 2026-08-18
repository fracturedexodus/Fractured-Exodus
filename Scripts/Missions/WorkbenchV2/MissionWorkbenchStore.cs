using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public interface IMissionWorkbenchCommand
{
	string Name { get; }
	void Execute(MissionDocument document);
	void Undo(MissionDocument document);
}

public sealed class MissionWorkbenchStore
{
	private readonly Stack<IMissionWorkbenchCommand> _undo = new Stack<IMissionWorkbenchCommand>();
	private readonly Stack<IMissionWorkbenchCommand> _redo = new Stack<IMissionWorkbenchCommand>();

	public MissionDocument Document { get; private set; }
	public string SelectedElementId { get; private set; } = string.Empty;
	public bool IsDirty { get; private set; }
	public int Revision { get; private set; }
	public bool CanUndo => _undo.Count > 0;
	public bool CanRedo => _redo.Count > 0;
	public string UndoLabel => CanUndo ? _undo.Peek().Name : string.Empty;
	public string RedoLabel => CanRedo ? _redo.Peek().Name : string.Empty;

	public event Action DocumentChanged;
	public event Action SelectionChanged;

	public void SetDocument(MissionDocument document)
	{
		Document = document;
		MissionDocumentSerializer.Normalize(Document);
		MissionDocumentReferenceResolver.RefreshDialogueIds(Document);
		SynchronizeFlowNodes();
		_undo.Clear();
		_redo.Clear();
		SelectedElementId = string.Empty;
		IsDirty = false;
		Revision++;
		DocumentChanged?.Invoke();
		SelectionChanged?.Invoke();
	}

	public void Execute(IMissionWorkbenchCommand command)
	{
		if (Document == null || command == null) return;
		command.Execute(Document);
		_undo.Push(command);
		_redo.Clear();
		SynchronizeFlowNodes();
		MarkChanged();
	}

	public void Undo()
	{
		if (Document == null || _undo.Count == 0) return;
		IMissionWorkbenchCommand command = _undo.Pop();
		command.Undo(Document);
		_redo.Push(command);
		SynchronizeFlowNodes();
		EnsureSelectionExists();
		MarkChanged();
	}

	public void Redo()
	{
		if (Document == null || _redo.Count == 0) return;
		IMissionWorkbenchCommand command = _redo.Pop();
		command.Execute(Document);
		_undo.Push(command);
		SynchronizeFlowNodes();
		MarkChanged();
	}

	public void Select(string elementId)
	{
		string normalized = Document?.Elements?.Any(element => element.Id == elementId) == true ? elementId : string.Empty;
		if (SelectedElementId == normalized) return;
		SelectedElementId = normalized;
		SelectionChanged?.Invoke();
	}

	public MissionMapElement GetSelectedElement()
	{
		return Document?.Elements?.FirstOrDefault(element => element.Id == SelectedElementId);
	}

	public void MarkSaved()
	{
		IsDirty = false;
		DocumentChanged?.Invoke();
	}

	private void MarkChanged()
	{
		IsDirty = true;
		Revision++;
		DocumentChanged?.Invoke();
	}

	private void EnsureSelectionExists()
	{
		if (!string.IsNullOrWhiteSpace(SelectedElementId)
			&& Document.Elements.All(element => element.Id != SelectedElementId))
		{
			SelectedElementId = string.Empty;
			SelectionChanged?.Invoke();
		}
	}

	private void SynchronizeFlowNodes()
	{
		HashSet<string> elementIds = Document.Elements.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
		Document.FlowNodes.RemoveAll(node => !string.IsNullOrWhiteSpace(node.MapElementId) && !elementIds.Contains(node.MapElementId));
		foreach (MissionMapElement element in Document.Elements)
		{
			MissionFlowNodeData existing = Document.FlowNodes.FirstOrDefault(node => node.MapElementId == element.Id);
			bool needsNode = element.Kind == MissionMapElementKind.Marker || !string.IsNullOrWhiteSpace(element.Logic?.Role);
			if (!needsNode)
			{
				if (existing != null) Document.FlowNodes.Remove(existing);
				continue;
			}

			if (existing == null)
			{
				existing = new MissionFlowNodeData
				{
					Id = $"flow_{element.Id}",
					MapElementId = element.Id,
					CanvasX = (Document.FlowNodes.Count % 4) * 280f,
					CanvasY = (Document.FlowNodes.Count / 4) * 190f
				};
				Document.FlowNodes.Add(existing);
			}

			existing.Kind = ResolveFlowKind(element);
			existing.Label = string.IsNullOrWhiteSpace(element.Logic?.Label)
				? !string.IsNullOrWhiteSpace(element.MarkerId) ? element.MarkerId : element.TileId
				: element.Logic.Label;
			existing.TriggerMode = element.Logic?.TriggerMode ?? "none";
			existing.TargetId = element.Logic?.TargetId ?? string.Empty;
			existing.RequiredFlags = SplitFlags(element.Logic?.RequiredFlag);
			existing.SetFlags = SplitFlags(element.Logic?.SetFlag);
		}
	}

	private static MissionFlowNodeKind ResolveFlowKind(MissionMapElement element)
	{
		if (element.MarkerId.StartsWith("spawn_") || element.MarkerId is "npc_spawn" or "hostile_spawn") return MissionFlowNodeKind.Spawn;
		if (element.MarkerId.StartsWith("objective_")) return MissionFlowNodeKind.Objective;
		if (element.Logic?.TriggerMode == "interact") return MissionFlowNodeKind.Interaction;
		return MissionFlowNodeKind.Trigger;
	}

	private static List<string> SplitFlags(string value)
	{
		return string.IsNullOrWhiteSpace(value)
			? new List<string>()
			: value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
	}
}

public sealed class AddMissionElementCommand : IMissionWorkbenchCommand
{
	private readonly MissionMapElement _element;
	public string Name => "Place element";
	public AddMissionElementCommand(MissionMapElement element) => _element = MissionElementCloner.Clone(element);
	public void Execute(MissionDocument document) => document.Elements.Add(MissionElementCloner.Clone(_element));
	public void Undo(MissionDocument document) => document.Elements.RemoveAll(element => element.Id == _element.Id);
}

public sealed class RemoveMissionElementCommand : IMissionWorkbenchCommand
{
	private readonly MissionMapElement _element;
	private int _index;
	public string Name => "Delete element";
	public RemoveMissionElementCommand(MissionMapElement element) => _element = MissionElementCloner.Clone(element);
	public void Execute(MissionDocument document)
	{
		_index = document.Elements.FindIndex(element => element.Id == _element.Id);
		if (_index >= 0) document.Elements.RemoveAt(_index);
	}
	public void Undo(MissionDocument document) => document.Elements.Insert(Math.Clamp(_index, 0, document.Elements.Count), MissionElementCloner.Clone(_element));
}

public sealed class MoveMissionElementCommand : IMissionWorkbenchCommand
{
	private readonly string _elementId;
	private readonly int _fromColumn;
	private readonly int _fromRow;
	private readonly int _toColumn;
	private readonly int _toRow;
	public string Name => "Move element";
	public MoveMissionElementCommand(string elementId, int fromColumn, int fromRow, int toColumn, int toRow)
	{
		_elementId = elementId;
		_fromColumn = fromColumn;
		_fromRow = fromRow;
		_toColumn = toColumn;
		_toRow = toRow;
	}
	public void Execute(MissionDocument document) => SetCell(document, _toColumn, _toRow);
	public void Undo(MissionDocument document) => SetCell(document, _fromColumn, _fromRow);
	private void SetCell(MissionDocument document, int column, int row)
	{
		MissionMapElement element = document.Elements.FirstOrDefault(candidate => candidate.Id == _elementId);
		if (element == null) return;
		element.Column = column;
		element.Row = row;
	}
}

public sealed class ReplaceMissionElementCommand : IMissionWorkbenchCommand
{
	private readonly MissionMapElement _before;
	private readonly MissionMapElement _after;
	public string Name => "Edit element";
	public ReplaceMissionElementCommand(MissionMapElement before, MissionMapElement after)
	{
		_before = MissionElementCloner.Clone(before);
		_after = MissionElementCloner.Clone(after);
	}
	public void Execute(MissionDocument document) => Replace(document, _after);
	public void Undo(MissionDocument document) => Replace(document, _before);
	private static void Replace(MissionDocument document, MissionMapElement replacement)
	{
		int index = document.Elements.FindIndex(element => element.Id == replacement.Id);
		if (index >= 0) document.Elements[index] = MissionElementCloner.Clone(replacement);
	}
}

public static class MissionElementCloner
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = false };
	public static MissionMapElement Clone(MissionMapElement element)
	{
		return element == null ? null : JsonSerializer.Deserialize<MissionMapElement>(JsonSerializer.Serialize(element, Options), Options);
	}
}
