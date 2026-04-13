using Godot;
using System.Collections.Generic;

public partial class DialogueUI : CanvasLayer
{
	[Export] public Label SpeakerNameLabel;
	[Export] public RichTextLabel DialogueTextDisplay;
	[Export] public VBoxContainer OptionsContainer;

	private GlobalData _globalData;
	private string _currentNpcId;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		Visible = false; // Hide it until needed
	}

	// Call this from BattleMap.cs to start a conversation!
	public void StartConversation(string npcId)
	{
		_currentNpcId = npcId;
		Visible = true;
		LoadDialogueNode("Start"); // Every conversation begins at the "Start" node
	}

	private void LoadDialogueNode(string nodeId)
	{
		DialogueNode nodeData = QuestManager.GetDialogue(_currentNpcId, nodeId);

		if (nodeData == null)
		{
			EndConversation();
			return;
		}

		// Update the visual text
		SpeakerNameLabel.Text = nodeData.SpeakerName;
		DialogueTextDisplay.Text = nodeData.Text;

		// Check if this node triggers a quest
		if (!string.IsNullOrEmpty(nodeData.QuestToTrigger))
		{
			QuestManager.AcceptQuest(_globalData, nodeData.QuestToTrigger);
		}

		// Clear out the old buttons
		foreach (Node child in OptionsContainer.GetChildren())
		{
			child.QueueFree();
		}

		// Create new buttons for the player's options
		if (nodeData.Options != null)
		{
			foreach (var option in nodeData.Options)
			{
				Button optionBtn = new Button();
				optionBtn.Text = option.Key;
				
				// When clicked, load the next node (or end if it says "End")
				optionBtn.Pressed += () => 
				{
					if (option.Value == "End") EndConversation();
					else LoadDialogueNode(option.Value);
				};
				
				OptionsContainer.AddChild(optionBtn);
			}
		}
	}

	private void EndConversation()
	{
		Visible = false;
		// Optional: Trigger an event here to tell BattleMap.cs to unpause the game
	}
}
