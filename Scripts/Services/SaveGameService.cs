using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;

public sealed class SaveGameSlotInfo
{
	public string SlotId { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string FilePath { get; set; } = string.Empty;
	public string LastSavedScenePath { get; set; } = string.Empty;
	public string SavedAtUtc { get; set; } = string.Empty;
	public string SavedSystem { get; set; } = string.Empty;
	public string SavedPlanet { get; set; } = string.Empty;
	public string CurrentMissionTitle { get; set; } = string.Empty;
	public int CurrentTurn { get; set; } = 1;
	public bool IsAutoSave { get; set; }
	public bool IsLegacySave { get; set; }
}

public class SaveGameService
{
	private const string NamedSavesDirectoryPath = "user://saves";
	private readonly string _manualSavePath;
	private readonly string _autoSavePath;

	public SaveGameService(string manualSavePath = "user://savegame.json", string autoSavePath = "user://autosave.json")
	{
		_manualSavePath = manualSavePath;
		_autoSavePath = autoSavePath;
	}

	public void Save(GlobalData globalData, bool autoSave = false)
	{
		if (globalData == null)
		{
			return;
		}

		string targetPath = autoSave ? _autoSavePath : _manualSavePath;
		string displayName = autoSave ? "Autosave" : "Quicksave";
		WriteSaveFile(globalData, targetPath, displayName);
	}

	public void SaveNamed(GlobalData globalData, string saveName)
	{
		if (globalData == null || string.IsNullOrWhiteSpace(saveName))
		{
			return;
		}

		EnsureNamedSaveDirectory();
		string slotId = BuildSlotId(saveName);
		string targetPath = $"{NamedSavesDirectoryPath}/{slotId}.json";
		WriteSaveFile(globalData, targetPath, saveName.Trim());
	}

	public bool Load(GlobalData globalData, string slotId = "")
	{
		if (globalData == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(slotId))
		{
			SaveGameSlotInfo match = GetAvailableSaves().FirstOrDefault(save => string.Equals(save.SlotId, slotId, StringComparison.OrdinalIgnoreCase));
			return match != null && TryLoadFromPath(globalData, match.FilePath);
		}

		if (TryLoadFromPath(globalData, _manualSavePath))
		{
			return true;
		}

		return TryLoadFromPath(globalData, _autoSavePath);
	}

	public List<SaveGameSlotInfo> GetAvailableSaves()
	{
		List<SaveGameSlotInfo> saves = new List<SaveGameSlotInfo>();

		if (FileAccess.FileExists(_manualSavePath))
		{
			SaveGameSlotInfo legacySave = BuildSaveSlotInfo(_manualSavePath, isLegacySave: true, isAutoSave: false);
			if (legacySave != null)
			{
				saves.Add(legacySave);
			}
		}

		DirAccess namedSavesDirectory = DirAccess.Open(NamedSavesDirectoryPath);
		if (namedSavesDirectory != null)
		{
			foreach (string fileName in namedSavesDirectory.GetFiles().Where(file => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
			{
				SaveGameSlotInfo namedSave = BuildSaveSlotInfo($"{NamedSavesDirectoryPath}/{fileName}", isLegacySave: false, isAutoSave: false);
				if (namedSave != null)
				{
					saves.Add(namedSave);
				}
			}
		}

		if (FileAccess.FileExists(_autoSavePath))
		{
			SaveGameSlotInfo autoSave = BuildSaveSlotInfo(_autoSavePath, isLegacySave: false, isAutoSave: true);
			if (autoSave != null)
			{
				saves.Add(autoSave);
			}
		}

		return saves
			.OrderByDescending(GetSaveSortStamp)
			.ThenBy(save => save.DisplayName, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public void DeleteSave()
	{
		DeleteIfExists(_manualSavePath);
		DeleteIfExists(_autoSavePath);
	}

	private void WriteSaveFile(GlobalData globalData, string targetPath, string displayName)
	{
		CampaignSaveData saveData = CampaignSaveData.FromRuntime(globalData);
		saveData.SaveDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Campaign Save" : displayName.Trim();
		saveData.SavedAtUtc = DateTime.UtcNow.ToString("O");
		string jsonString = Json.Stringify(saveData.ToVariantDictionary());
		using FileAccess file = FileAccess.Open(targetPath, FileAccess.ModeFlags.Write);
		if (file != null)
		{
			file.StoreString(jsonString);
			GD.Print($"Save successfully written to: {targetPath}");
		}
	}

	private static void DeleteIfExists(string path)
	{
		if (FileAccess.FileExists(path))
		{
			DirAccess.RemoveAbsolute(path);
		}
	}

	private static bool TryLoadFromPath(GlobalData globalData, string path)
	{
		CampaignSaveData saveData = ReadSaveData(path);
		if (saveData == null)
		{
			return false;
		}

		saveData.ApplyTo(globalData);
		return true;
	}

	private static CampaignSaveData ReadSaveData(string path)
	{
		if (!FileAccess.FileExists(path))
		{
			return null;
		}

		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return null;
		}

		Json json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok || json.Data.VariantType != Variant.Type.Dictionary)
		{
			return null;
		}

		return CampaignSaveData.FromVariantDictionary((Godot.Collections.Dictionary)json.Data);
	}

	private static SaveGameSlotInfo BuildSaveSlotInfo(string path, bool isLegacySave, bool isAutoSave)
	{
		CampaignSaveData saveData = ReadSaveData(path);
		if (saveData == null)
		{
			return null;
		}

		string slotId = isAutoSave
			? "autosave"
			: isLegacySave
				? "quicksave"
				: Path.GetFileNameWithoutExtension(path);

		return new SaveGameSlotInfo
		{
			SlotId = slotId,
			DisplayName = ResolveDisplayName(saveData, isLegacySave, isAutoSave),
			FilePath = path,
			LastSavedScenePath = saveData.LastSavedScenePath ?? string.Empty,
			SavedAtUtc = saveData.SavedAtUtc ?? string.Empty,
			SavedSystem = saveData.SavedSystem ?? string.Empty,
			SavedPlanet = saveData.SavedPlanet ?? string.Empty,
			CurrentMissionTitle = saveData.CurrentMissionTitle ?? string.Empty,
			CurrentTurn = saveData.CurrentTurn,
			IsAutoSave = isAutoSave,
			IsLegacySave = isLegacySave
		};
	}

	private static string ResolveDisplayName(CampaignSaveData saveData, bool isLegacySave, bool isAutoSave)
	{
		if (!string.IsNullOrWhiteSpace(saveData?.SaveDisplayName))
		{
			return saveData.SaveDisplayName.Trim();
		}

		if (isAutoSave)
		{
			return "Autosave";
		}

		return isLegacySave ? "Quicksave" : "Campaign Save";
	}

	private static long GetSaveSortStamp(SaveGameSlotInfo save)
	{
		if (save == null)
		{
			return long.MinValue;
		}

		if (DateTime.TryParse(save.SavedAtUtc, out DateTime parsedUtc))
		{
			return parsedUtc.ToUniversalTime().Ticks;
		}

		return (long)FileAccess.GetModifiedTime(save.FilePath);
	}

	private static string BuildSlotId(string saveName)
	{
		string normalized = new string((saveName ?? string.Empty)
			.Trim()
			.ToLowerInvariant()
			.Select(character => char.IsLetterOrDigit(character)
				? character
				: character == ' ' || character == '-' || character == '_'
					? '-'
					: '\0')
			.Where(character => character != '\0')
			.ToArray());

		while (normalized.Contains("--", StringComparison.Ordinal))
		{
			normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
		}

		normalized = normalized.Trim('-');
		return string.IsNullOrWhiteSpace(normalized) ? "save-slot" : normalized;
	}

	private static void EnsureNamedSaveDirectory()
	{
		string absoluteDirectoryPath = ProjectSettings.GlobalizePath(NamedSavesDirectoryPath);
		if (!DirAccess.DirExistsAbsolute(absoluteDirectoryPath))
		{
			DirAccess.MakeDirRecursiveAbsolute(absoluteDirectoryPath);
		}
	}
}
