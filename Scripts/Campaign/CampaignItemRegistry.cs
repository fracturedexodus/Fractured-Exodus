using Godot;
using System.Collections.Generic;
using System.Linq;

public static class CampaignItemRegistry
{
	private const string ItemDirectory = "res://Data/Campaign/Items";
	private static readonly Dictionary<string, CampaignItemDefinition> ItemsById = new Dictionary<string, CampaignItemDefinition>();
	private static bool _isLoaded;

	public static IReadOnlyDictionary<string, CampaignItemDefinition> GetAll()
	{
		EnsureLoaded();
		return ItemsById;
	}

	public static CampaignItemDefinition GetItem(string itemId)
	{
		EnsureLoaded();
		if (string.IsNullOrWhiteSpace(itemId))
		{
			return null;
		}

		return ItemsById.TryGetValue(itemId, out CampaignItemDefinition item) ? item : null;
	}

	public static void Reload()
	{
		_isLoaded = false;
		ItemsById.Clear();
		EnsureLoaded();
	}

	private static void EnsureLoaded()
	{
		if (_isLoaded)
		{
			return;
		}

		_isLoaded = true;
		ItemsById.Clear();
		if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(ItemDirectory)))
		{
			return;
		}

		foreach (string file in DirAccess.GetFilesAt(ItemDirectory).Where(file => file.EndsWith(".json")))
		{
			LoadItemFile($"{ItemDirectory}/{file}");
		}
	}

	private static void LoadItemFile(string resourcePath)
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
		string itemId = dict.TryGetValue("item_id", out Variant itemIdVariant) ? itemIdVariant.AsString() : string.Empty;
		if (string.IsNullOrWhiteSpace(itemId))
		{
			return;
		}

		ItemsById[itemId] = new CampaignItemDefinition
		{
			ItemId = itemId,
			DisplayName = dict.TryGetValue("display_name", out Variant displayNameVariant) ? displayNameVariant.AsString() : itemId,
			Description = dict.TryGetValue("description", out Variant descriptionVariant) ? descriptionVariant.AsString() : string.Empty,
			Category = dict.TryGetValue("category", out Variant categoryVariant) ? categoryVariant.AsString() : "Mission Loot",
			Stackable = !dict.TryGetValue("stackable", out Variant stackableVariant) || stackableVariant.AsBool(),
			CodexEntryId = dict.TryGetValue("codex_entry_id", out Variant codexVariant) ? codexVariant.AsString() : string.Empty
		};
	}
}
