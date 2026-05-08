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
		},
		{
			"trigger_dialogue", new Dictionary<string, DialogueNode>
			{
				{
					"Start", new DialogueNode
					{
						SpeakerName = "Relay Survivor",
						Text = "Hold there. If you're hearing this, then the relay still has visitors left in the galaxy. The archive vault is destabilizing and the shelter seals are failing. We do not have time for both.",
						Options = new Dictionary<string, string>
						{
							{ "Tell me about the survivors.", "Survivors" },
							{ "Tell me about the archive.", "Archive" },
							{ "We understand. We'll decide what to save.", "End" }
						}
					}
				},
				{
					"Survivors", new DialogueNode
					{
						SpeakerName = "Relay Survivor",
						Text = "Three cryo-pods still cycle in the shelter wing. If power drops again, they die with the station. Save them first and the archive will likely be lost to cascade failure.",
						Options = new Dictionary<string, string>
						{
							{ "Understood. Back to the relay schematic.", "Start" },
							{ "We'll move for the shelter.", "End" }
						}
					}
				},
				{
					"Archive", new DialogueNode
					{
						SpeakerName = "Relay Survivor",
						Text = "The archive core contains intact Custodian route-logic and pre-collapse relay logs. If you cut auxiliary power to stabilize it, the life-support reserves in the shelter will not hold much longer.",
						Options = new Dictionary<string, string>
						{
							{ "Understood. Show me the tradeoff again.", "Start" },
							{ "We'll secure the archive.", "End" }
						}
					}
				}
			}
		},
		{
			"smuggler_exchange_dialogue", new Dictionary<string, DialogueNode>
			{
				{
					"Start", new DialogueNode
					{
						SpeakerName = "Broker Veil",
						Text = "The exchange is bleeding pressure. Veil's uplink is still broadcasting and every patrol cutter in the corridor can smell the leak. If we want anything from this outpost, we need the broker's terms, the safehouse manifest, and whatever survived in the vault.",
						Options = new Dictionary<string, string>
						{
							{ "Review the broker's terms.", "Cache" },
							{ "Who is converging on the station?", "Threat" },
							{ "Understood. We'll work the outpost and choose our exit.", "End" }
						}
					}
				},
				{
					"Cache", new DialogueNode
					{
						SpeakerName = "Broker Veil",
						Text = "The vault holds route ledgers and contraband manifests. The medbay holds my wounded runners. Help the people and I can still broker your way out. Strip the vault and you'll leave rich, but every ghost on this station becomes your debt.",
						Options = new Dictionary<string, string>
						{
							{ "Back to the uplink summary.", "Start" },
							{ "We have enough. We'll make the call ourselves.", "End" }
						}
					}
				},
				{
					"Threat", new DialogueNode
					{
						SpeakerName = "Broker Veil",
						Text = "Patrol cutters, reclaim crews, and two licensed evidence burners. If they board first, they erase the vault, purge the medbay, and put our names in an open ledger for every hungry ship in the Reach.",
						Options = new Dictionary<string, string>
						{
							{ "Back to the uplink summary.", "Start" },
							{ "Then we move now.", "End" }
						}
					}
				}
			}
		},
		{
			"smuggler_exchange_intro", new Dictionary<string, DialogueNode>
			{
				{
					"Start", new DialogueNode
					{
						SpeakerName = "Broker Veil",
						Text = "You found the uplink before the patrols cut it. Good. The vault can be unsealed from the override console, but only if you understand the price: my wounded are boxed into the medbay, and every ledger in the vault can put a dozen routes to the torch.",
						Options = new Dictionary<string, string>
						{
							{ "What does the vault actually hold?", "Vault" },
							{ "Who is trapped in the medbay?", "Medbay" },
							{ "Enough. We'll move through the station and decide.", "End" }
						}
					}
				},
				{
					"Vault", new DialogueNode
					{
						SpeakerName = "Broker Veil",
						Text = "Route ledgers, hush-pay manifests, and keys to old embargo lanes. Take them and you'll own the corridor for a season. Lose them and the patrols own every ship that ever trusted me.",
						Options = new Dictionary<string, string>
						{
							{ "And the medbay?", "Medbay" },
							{ "Back to the uplink.", "Start" },
							{ "We'll handle the station from here.", "End" }
						}
					}
				},
				{
					"Medbay", new DialogueNode
					{
						SpeakerName = "Broker Veil",
						Text = "Couriers, cutters, a pair of civilians that happened to be buying medicine when the station sealed. They don't care about ledgers or contracts. They just need a ship willing to take them before the burners arrive.",
						Options = new Dictionary<string, string>
						{
							{ "Back to the uplink.", "Start" },
							{ "We know enough. Hold this channel.", "End" }
						}
					}
				}
			}
		},
		{
			"smuggler_exchange_survivors", new Dictionary<string, DialogueNode>
			{
				{
					"Start", new DialogueNode
					{
						SpeakerName = "Safehouse Medic",
						Text = "Three runners are stable enough to move, two civilians are in shock, and the station's auto-doc is cycling on stolen power. Give us a corridor and we'll reach your shuttle under our own legs.",
						Options = new Dictionary<string, string>
						{
							{ "You're coming with us. Be ready to move.", "End" },
							{ "What happens if we leave you here?", "Risk" }
						}
					}
				},
				{
					"Risk", new DialogueNode
					{
						SpeakerName = "Safehouse Medic",
						Text = "Patrol evidence teams don't take witnesses. They'll call it contamination control and vent this room into the black. If you're offering extraction, say it plain so I can keep these people breathing until the doors open.",
						Options = new Dictionary<string, string>
						{
							{ "Understood. Prepare them for evac.", "End" },
							{ "Hold for a few more minutes.", "Start" }
						}
					}
				}
			}
		},
		{
			"smuggler_exchange_patrols", new Dictionary<string, DialogueNode>
			{
				{
					"Start", new DialogueNode
					{
						SpeakerName = "Traffic Scrubber",
						Text = "Patrol feed decrypted. Two cutters are aligning for docking arms and a reclaimer tug is holding back to torch the vault after the sweep. You have one clean exit vector if you move before they finish triangulating Veil's relay.",
						Options = new Dictionary<string, string>
						{
							{ "Mark the clean vector and keep the feed running.", "End" },
							{ "Which section do they hit first?", "Impact" }
						}
					}
				},
				{
					"Impact", new DialogueNode
					{
						SpeakerName = "Traffic Scrubber",
						Text = "Boarders sweep the vault side first. The medbay dies second. If you were planning to strip contraband and run, this is the warning that makes it possible.",
						Options = new Dictionary<string, string>
						{
							{ "Good. Keep them blind a little longer.", "End" },
							{ "Back to the patrol feed.", "Start" }
						}
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
