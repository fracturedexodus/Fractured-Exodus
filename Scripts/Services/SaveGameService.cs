using Godot;

public class SaveGameService
{
	private readonly string _manualSavePath;
	private readonly string _autoSavePath;

	public SaveGameService(string manualSavePath = "user://savegame.json", string autoSavePath = "user://autosave.json")
	{
		_manualSavePath = manualSavePath;
		_autoSavePath = autoSavePath;
	}

	public void Save(GlobalData globalData, bool autoSave = false)
	{
		CampaignSaveData saveData = CampaignSaveData.FromRuntime(globalData);
		string jsonString = Json.Stringify(saveData.ToVariantDictionary());
		string targetPath = autoSave ? _autoSavePath : _manualSavePath;
		using FileAccess file = FileAccess.Open(targetPath, FileAccess.ModeFlags.Write);
		if (file != null)
		{
			file.StoreString(jsonString);
			GD.Print($"{(autoSave ? "Auto-save" : "Manual save")} successfully written to: {targetPath}");
		}
	}

	public bool Load(GlobalData globalData)
	{
		if (TryLoadFromPath(globalData, _manualSavePath))
		{
			return true;
		}

		return TryLoadFromPath(globalData, _autoSavePath);
	}

	public void DeleteSave()
	{
		DeleteIfExists(_manualSavePath);
		DeleteIfExists(_autoSavePath);
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
		if (!FileAccess.FileExists(path))
		{
			return false;
		}

		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		var json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok)
		{
			return false;
		}

		CampaignSaveData.FromVariantDictionary((Godot.Collections.Dictionary)json.Data).ApplyTo(globalData);
		return true;
	}
}
