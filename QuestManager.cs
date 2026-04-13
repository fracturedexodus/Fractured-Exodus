using Godot;
using System.Collections.Generic;
using System.Linq;

// Represents one "screen" of conversation
public class DialogueNode
{
	public string SpeakerName { get; set; }
	public string Text { get; set; }
	public Dictionary<string, string> Options { get; set; } // "What player says" -> "Next Node ID"
	public string QuestToTrigger { get; set; } // Triggers when this node is read
}

public static class QuestManager
{
	// This dictionary holds ALL the dialogue in your game. 
	// The Key is the specific NPC or Faction you are talking to.
	public static Dictionary<string, Dictionary<string, DialogueNode>> Dialogues = new Dictionary<string, Dictionary<string, DialogueNode>>
	{
		{
			// Example NPC: A stranded miner you find while scanning
			"Stranded_Miner", new Dictionary<string, DialogueNode>
			{
				{
					"Start", new DialogueNode 
					{ 
						SpeakerName = "Unknown Signal", 
						Text = "Mayday... is anyone out there? Our reactor is dead. We need 50 Energy Cores or we're going to freeze.", 
						Options = new Dictionary<string, string> 
						{ 
							{ "We can spare the cores. (Give 50 Energy Cores)", "Give_Cores" },
							{ "Who are you?", "Ask_Info" },
							{ "We can't help you. (Leave)", "End" } 
						} 
					}
				},
				{
					"Ask_Info", new DialogueNode 
					{ 
						SpeakerName = "Stranded Miner", 
						Text = "We're an independent surveying crew. Please, the temperature is dropping rapidly.", 
						Options = new Dictionary<string, string> 
						{ 
							{ "Fine, take the cores.", "Give_Cores" },
							{ "Sorry, we need them for our own survival. (Leave)", "End" } 
						} 
					}
				},
				{
					"Give_Cores", new DialogueNode 
					{ 
						SpeakerName = "Stranded Miner", 
						Text = "Thank you! You saved our lives. Here, we found these coordinates before our ship died. It's an untouched Ancient Tech cache.", 
						Options = new Dictionary<string, string> 
						{ 
							{ "Upload coordinates to the ship's nav-computer. (End)", "End" } 
						},
						QuestToTrigger = "Quest_Ancient_Cache" // This hands the player a quest!
					}
				}
			}
		}
	};

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

	// Helper function to pull the dialogue node
	public static DialogueNode GetDialogue(string npcId, string nodeId)
	{
		if (Dialogues.ContainsKey(npcId) && Dialogues[npcId].ContainsKey(nodeId))
		{
			return Dialogues[npcId][nodeId];
		}
		return null;
	}

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
