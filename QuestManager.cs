using Godot;
using System.Collections.Generic;
using System.Linq;

public static class QuestManager
{
	// A master catalog of every quest in the game
	public static Dictionary<string, QuestData> QuestDatabase = new Dictionary<string, QuestData>
	{
		{
			"Quest_Ancient_Cache", new QuestData
			{
				QuestID = "Quest_Ancient_Cache",
				Title = "The Miner's Secret",
				Description = "A stranded miner gave us coordinates to an untouched Ancient Tech cache. We should investigate the system.",
				TargetSystem = "SECTOR-9999" // We can randomize this later!
			}
		}
	};

	// Logic for accepting a quest
	public static void AcceptQuest(GlobalData globalData, string questId)
	{
		if (globalData == null || string.IsNullOrEmpty(questId)) return;

		// Don't give them the quest if they already have it or finished it
		if (globalData.ActiveQuests.Any(q => q.QuestID == questId)) return;
		
		bool hasFinished = false;
		foreach(var finishedId in globalData.CompletedQuestIDs)
		{
			if ((string)finishedId == questId) hasFinished = true;
		}
		if (hasFinished) return;

		if (QuestDatabase.ContainsKey(questId))
		{
			// Give them a copy of the quest
			globalData.ActiveQuests.Add(QuestDatabase[questId]);
			GD.Print($"QUEST ACCEPTED: {QuestDatabase[questId].Title}");
		}
	}
}
