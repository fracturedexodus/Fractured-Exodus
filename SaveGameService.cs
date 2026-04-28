using Godot;

public class SaveGameService
{
	private readonly string _savePath;

	public SaveGameService(string savePath = "user://savegame.json")
	{
		_savePath = savePath;
	}

	public void Save(GlobalData globalData)
	{
		CampaignSaveData saveData = CampaignSaveData.FromRuntime(globalData);
		string jsonString = Json.Stringify(saveData.ToVariantDictionary());
		using FileAccess file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Write);
		if (file != null)
		{
			file.StoreString(jsonString);
			GD.Print("Game successfully saved to: " + _savePath);
		}
	}

	public bool Load(GlobalData globalData)
	{
		if (!FileAccess.FileExists(_savePath)) return false;

		using FileAccess file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Read);
		var json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok) return false;

		CampaignSaveData.FromVariantDictionary((Godot.Collections.Dictionary)json.Data).ApplyTo(globalData);
		return true;
	}

	public void DeleteSave()
	{
		if (FileAccess.FileExists(_savePath)) DirAccess.RemoveAbsolute(_savePath);
	}
}
