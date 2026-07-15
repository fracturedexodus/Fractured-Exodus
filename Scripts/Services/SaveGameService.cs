using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using File = System.IO.File;
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
	public bool IsRecovered { get; set; }
	public string RecoverySourcePath { get; set; } = string.Empty;
}

public class SaveGameService
{
	private const string NamedSavesDirectoryPath = "user://saves";
	private readonly string _manualSavePath;
	private readonly string _autoSavePath;
	public string LastError { get; private set; } = string.Empty;

	public SaveGameService(string manualSavePath = "user://savegame.json", string autoSavePath = "user://autosave.json")
	{
		_manualSavePath = manualSavePath;
		_autoSavePath = autoSavePath;
	}

	public bool Save(GlobalData globalData, bool autoSave = false)
	{
		if (globalData == null)
		{
			LastError = "Cannot save without campaign data.";
			return false;
		}

		string targetPath = autoSave ? _autoSavePath : _manualSavePath;
		string displayName = autoSave ? "Autosave" : "Quicksave";
		return WriteSaveFile(globalData, targetPath, displayName);
	}

	public bool SaveNamed(GlobalData globalData, string saveName)
	{
		if (globalData == null || string.IsNullOrWhiteSpace(saveName))
		{
			LastError = "A named save requires campaign data and a non-empty name.";
			return false;
		}

		EnsureNamedSaveDirectory();
		string slotId = BuildSlotId(saveName);
		string targetPath = $"{NamedSavesDirectoryPath}/{slotId}.json";
		return WriteSaveFile(globalData, targetPath, saveName.Trim());
	}

	public bool Load(GlobalData globalData, string slotId = "")
	{
		LastError = string.Empty;
		if (globalData == null)
		{
			LastError = "Cannot load without campaign data.";
			return false;
		}

		if (!string.IsNullOrWhiteSpace(slotId))
		{
			SaveGameSlotInfo match = GetAvailableSaves().FirstOrDefault(save => string.Equals(save.SlotId, slotId, StringComparison.OrdinalIgnoreCase));
			if (match == null)
			{
				LastError = $"Save slot was not found: {slotId}";
				return false;
			}

			return TryLoadFromPath(globalData, match.FilePath);
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

		if (HasAnySaveCandidate(_manualSavePath))
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
			IEnumerable<string> namedSaveFiles = namedSavesDirectory
				.GetFiles()
				.Select(NormalizeSaveCandidateFileName)
				.Where(file => !string.IsNullOrWhiteSpace(file))
				.Distinct(StringComparer.OrdinalIgnoreCase);
			foreach (string fileName in namedSaveFiles)
			{
				SaveGameSlotInfo namedSave = BuildSaveSlotInfo($"{NamedSavesDirectoryPath}/{fileName}", isLegacySave: false, isAutoSave: false);
				if (namedSave != null)
				{
					saves.Add(namedSave);
				}
			}
		}

		if (HasAnySaveCandidate(_autoSavePath))
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

	public bool DeleteSlot(string slotId)
	{
		if (string.IsNullOrWhiteSpace(slotId))
		{
			return false;
		}

		SaveGameSlotInfo match = GetAvailableSaves().FirstOrDefault(save => string.Equals(save.SlotId, slotId, StringComparison.OrdinalIgnoreCase));
		if (match == null || string.IsNullOrWhiteSpace(match.FilePath))
		{
			return false;
		}

		DeleteIfExists(match.FilePath);
		DeleteIfExists(GetBackupPath(match.FilePath));
		DeleteIfExists(GetTempPath(match.FilePath));
		DeleteIfExists(GetBackupStagingPath(match.FilePath));
		return !FileAccess.FileExists(match.FilePath)
			&& !FileAccess.FileExists(GetBackupPath(match.FilePath))
			&& !FileAccess.FileExists(GetTempPath(match.FilePath));
	}

	public void DeleteSave()
	{
		DeleteIfExists(_manualSavePath);
		DeleteIfExists(GetBackupPath(_manualSavePath));
		DeleteIfExists(GetTempPath(_manualSavePath));
		DeleteIfExists(GetBackupStagingPath(_manualSavePath));
		DeleteIfExists(_autoSavePath);
		DeleteIfExists(GetBackupPath(_autoSavePath));
		DeleteIfExists(GetTempPath(_autoSavePath));
		DeleteIfExists(GetBackupStagingPath(_autoSavePath));
	}

	private bool WriteSaveFile(GlobalData globalData, string targetPath, string displayName)
	{
		LastError = string.Empty;
		string tempPath = GetTempPath(targetPath);
		try
		{
			CampaignSaveData saveData = CampaignSaveData.FromRuntime(globalData);
			saveData.SaveDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Campaign Save" : displayName.Trim();
			saveData.SavedAtUtc = DateTime.UtcNow.ToString("O");
			string jsonString = Json.Stringify(saveData.ToVariantDictionary());

			DeleteIfExists(tempPath);
			using (FileAccess file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
			{
				if (file == null)
				{
					return Fail($"Could not open temporary save file for writing: {tempPath}");
				}

				file.StoreString(jsonString);
				file.Flush();
			}

			if (!TryReadSaveData(tempPath, out CampaignSaveData verifiedSave, out string validationError)
				|| verifiedSave == null)
			{
				return Fail($"Temporary save validation failed: {validationError}");
			}

			if (!CommitValidatedSave(tempPath, targetPath, out string commitError))
			{
				return Fail($"Could not commit save: {commitError}");
			}

			GD.Print($"Save successfully written and verified: {targetPath}");
			return true;
		}
		catch (Exception exception)
		{
			return Fail($"Unexpected save failure for {targetPath}: {exception.Message}");
		}
	}

	private bool CommitValidatedSave(string tempPath, string targetPath, out string error)
	{
		error = string.Empty;
		string absoluteTempPath = ProjectSettings.GlobalizePath(tempPath);
		string absoluteTargetPath = ProjectSettings.GlobalizePath(targetPath);
		string backupPath = GetBackupPath(targetPath);
		string absoluteBackupPath = ProjectSettings.GlobalizePath(backupPath);
		string backupStagingPath = GetBackupStagingPath(targetPath);
		string absoluteBackupStagingPath = ProjectSettings.GlobalizePath(backupStagingPath);

		try
		{
			if (!File.Exists(absoluteTargetPath))
			{
				File.Move(absoluteTempPath, absoluteTargetPath);
				return true;
			}

			bool targetIsValid = TryReadSaveData(targetPath, out _, out _);
			if (!targetIsValid)
			{
				GD.PushWarning($"Replacing invalid primary save while preserving its existing backup: {targetPath}");
				File.Move(absoluteTempPath, absoluteTargetPath, overwrite: true);
				return true;
			}

			if (File.Exists(absoluteBackupStagingPath))
			{
				File.Delete(absoluteBackupStagingPath);
			}

			try
			{
				File.Replace(absoluteTempPath, absoluteTargetPath, absoluteBackupStagingPath, ignoreMetadataErrors: true);
			}
			catch (PlatformNotSupportedException)
			{
				File.Copy(absoluteTargetPath, absoluteBackupStagingPath, overwrite: true);
				File.Move(absoluteTempPath, absoluteTargetPath, overwrite: true);
			}

			if (File.Exists(absoluteBackupStagingPath))
			{
				File.Move(absoluteBackupStagingPath, absoluteBackupPath, overwrite: true);
			}

			return true;
		}
		catch (Exception exception)
		{
			error = exception.Message;
			return false;
		}
	}

	private bool Fail(string message)
	{
		LastError = message;
		GD.PrintErr(message);
		return false;
	}

	private static void DeleteIfExists(string path)
	{
		if (FileAccess.FileExists(path))
		{
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
		}
	}

	private bool TryLoadFromPath(GlobalData globalData, string path)
	{
		if (!TryReadSaveDataWithRecovery(path, out CampaignSaveData saveData, out string recoveryPath, out string error))
		{
			LastError = $"Unable to load save {path}: {error}";
			GD.PrintErr(LastError);
			return false;
		}

		saveData.ApplyTo(globalData);
		LastError = string.Empty;
		if (!string.Equals(recoveryPath, path, StringComparison.Ordinal))
		{
			GD.PushWarning($"Loaded recovery save {recoveryPath} because the primary save {path} was unavailable or invalid.");
		}
		return true;
	}

	private static bool TryReadSaveDataWithRecovery(
		string path,
		out CampaignSaveData saveData,
		out string recoveryPath,
		out string error)
	{
		saveData = null;
		recoveryPath = string.Empty;
		List<string> errors = new List<string>();
		foreach (string candidatePath in new[] { path, GetTempPath(path), GetBackupPath(path) })
		{
			if (TryReadSaveData(candidatePath, out saveData, out string candidateError))
			{
				recoveryPath = candidatePath;
				error = string.Empty;
				return true;
			}

			if (!string.IsNullOrWhiteSpace(candidateError))
			{
				errors.Add(candidateError);
			}
		}

		error = errors.Count == 0 ? "No primary, temporary, or backup save exists." : string.Join(" | ", errors);
		return false;
	}

	private static bool TryReadSaveData(string path, out CampaignSaveData saveData, out string error)
	{
		saveData = null;
		error = string.Empty;
		if (!FileAccess.FileExists(path))
		{
			error = $"Save file does not exist: {path}";
			return false;
		}

		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			error = $"Save file could not be opened: {path}";
			return false;
		}

		try
		{
			Json json = new Json();
			Error parseResult = json.Parse(file.GetAsText());
			if (parseResult != Error.Ok || json.Data.VariantType != Variant.Type.Dictionary)
			{
				error = $"Save JSON is invalid at {path}: {json.GetErrorMessage()}";
				return false;
			}

			Godot.Collections.Dictionary saveDictionary = (Godot.Collections.Dictionary)json.Data;
			if (!TryMigrateSaveDictionary(saveDictionary, out error))
			{
				error = $"Save migration failed at {path}: {error}";
				return false;
			}

			saveData = CampaignSaveData.FromVariantDictionary(saveDictionary);
			if (!ValidateSaveData(saveData, out error))
			{
				error = $"Save validation failed at {path}: {error}";
				saveData = null;
				return false;
			}

			return true;
		}
		catch (Exception exception)
		{
			error = $"Save data could not be read at {path}: {exception.Message}";
			saveData = null;
			return false;
		}
	}

	private static bool TryMigrateSaveDictionary(Godot.Collections.Dictionary saveDictionary, out string error)
	{
		error = string.Empty;
		int version = saveDictionary.ContainsKey("SaveSchemaVersion")
			? ((Variant)saveDictionary["SaveSchemaVersion"]).AsInt32()
			: 0;

		if (version < 0)
		{
			error = $"Invalid negative schema version {version}.";
			return false;
		}

		if (version > CampaignSaveData.CurrentSchemaVersion)
		{
			error = $"Save schema {version} is newer than supported schema {CampaignSaveData.CurrentSchemaVersion}.";
			return false;
		}

		while (version < CampaignSaveData.CurrentSchemaVersion)
		{
			switch (version)
			{
				case 0:
					saveDictionary["SaveSchemaVersion"] = 1;
					version = 1;
					break;
				default:
					error = $"No migration exists from schema version {version}.";
					return false;
			}
		}

		return true;
	}

	private static bool ValidateSaveData(CampaignSaveData saveData, out string error)
	{
		error = string.Empty;
		if (saveData == null)
		{
			error = "Save data is empty.";
			return false;
		}

		if (saveData.SaveSchemaVersion != CampaignSaveData.CurrentSchemaVersion)
		{
			error = $"Expected schema {CampaignSaveData.CurrentSchemaVersion}, found {saveData.SaveSchemaVersion}.";
			return false;
		}

		if (saveData.CurrentTurn < 1)
		{
			error = $"CurrentTurn must be at least 1, found {saveData.CurrentTurn}.";
			return false;
		}

		if (!string.IsNullOrWhiteSpace(saveData.LastSavedScenePath)
			&& !saveData.LastSavedScenePath.StartsWith("res://", StringComparison.Ordinal))
		{
			error = $"Invalid scene path: {saveData.LastSavedScenePath}";
			return false;
		}

		return true;
	}

	private static SaveGameSlotInfo BuildSaveSlotInfo(string path, bool isLegacySave, bool isAutoSave)
	{
		if (!TryReadSaveDataWithRecovery(path, out CampaignSaveData saveData, out string recoveryPath, out _))
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
			IsLegacySave = isLegacySave,
			IsRecovered = !string.Equals(recoveryPath, path, StringComparison.Ordinal),
			RecoverySourcePath = recoveryPath
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

		string sortPath = !string.IsNullOrWhiteSpace(save.RecoverySourcePath)
			? save.RecoverySourcePath
			: save.FilePath;
		return (long)FileAccess.GetModifiedTime(sortPath);
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

	private static string GetTempPath(string path) => path + ".tmp";
	private static string GetBackupPath(string path) => path + ".bak";
	private static string GetBackupStagingPath(string path) => path + ".bak.next";

	private static bool HasAnySaveCandidate(string path)
	{
		return FileAccess.FileExists(path)
			|| FileAccess.FileExists(GetTempPath(path))
			|| FileAccess.FileExists(GetBackupPath(path));
	}

	private static string NormalizeSaveCandidateFileName(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return string.Empty;
		}

		if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
		{
			return fileName;
		}

		if (fileName.EndsWith(".json.tmp", StringComparison.OrdinalIgnoreCase)
			|| fileName.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase))
		{
			return fileName.Substring(0, fileName.Length - 4);
		}

		return string.Empty;
	}
}
