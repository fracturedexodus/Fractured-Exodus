using Godot;
using System.Collections.Generic;
using System.Linq;

public static class CodexIntelRegistry
{
	private const string EntryDirectory = "res://Data/Codex/Entries";
	private static readonly Dictionary<string, CodexIntelEntry> EntriesById = new Dictionary<string, CodexIntelEntry>();
	private static bool _isLoaded;

	public static IReadOnlyDictionary<string, CodexIntelEntry> GetAll()
	{
		EnsureLoaded();
		return EntriesById;
	}

	public static CodexIntelEntry GetEntry(string entryId)
	{
		EnsureLoaded();
		if (string.IsNullOrWhiteSpace(entryId))
		{
			return null;
		}

		return EntriesById.TryGetValue(entryId, out CodexIntelEntry entry) ? entry : null;
	}

	public static void Reload()
	{
		_isLoaded = false;
		EntriesById.Clear();
		EnsureLoaded();
	}

	private static void EnsureLoaded()
	{
		if (_isLoaded)
		{
			return;
		}

		_isLoaded = true;
		EntriesById.Clear();
		if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(EntryDirectory)))
		{
			return;
		}

		foreach (string file in DirAccess.GetFilesAt(EntryDirectory).Where(file => file.EndsWith(".json")))
		{
			LoadEntryFile($"{EntryDirectory}/{file}");
		}
	}

	private static void LoadEntryFile(string resourcePath)
	{
		using FileAccess file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			return;
		}

		Godot.Collections.Dictionary dict = parsed.AsGodotDictionary();
		string entryId = dict.TryGetValue("entry_id", out Variant entryIdVariant) ? entryIdVariant.AsString() : string.Empty;
		if (string.IsNullOrWhiteSpace(entryId))
		{
			return;
		}

		EntriesById[entryId] = new CodexIntelEntry
		{
			EntryId = entryId,
			Title = dict.TryGetValue("title", out Variant titleVariant) ? titleVariant.AsString() : entryId,
			Category = dict.TryGetValue("category", out Variant categoryVariant) ? categoryVariant.AsString() : "Mission Intel",
			Summary = dict.TryGetValue("summary", out Variant summaryVariant) ? summaryVariant.AsString() : string.Empty,
			DetailText = dict.TryGetValue("detail_text", out Variant detailVariant) ? detailVariant.AsString() : string.Empty,
			ImagePath = dict.TryGetValue("image_path", out Variant imageVariant) ? imageVariant.AsString() : string.Empty
		};
	}
}
