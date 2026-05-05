using Godot;
using System.Collections.Generic;

public partial class DialogueUI : CanvasLayer
{
	[Signal]
	public delegate void ConversationEndedEventHandler();

	[Export] public Label SpeakerNameLabel;
	[Export] public RichTextLabel DialogueTextDisplay;
	[Export] public VBoxContainer OptionsContainer;
	[Export] public TextureRect OfficerPortraitRect;
	[Export] public Label OfficerNameLabel;
	[Export] public TextureRect NpcPortraitRect;
	[Export] public Label NpcNameLabel;

	private GlobalData _globalData;
	private string _currentNpcId;
	private string _currentOfficerName = "Officer";
	private string _currentOfficerPortraitPath = string.Empty;
	private string _currentNpcPortraitPath = string.Empty;
	public bool IsConversationOpen => Visible;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		Visible = false; // Hide it until needed
	}

	// Call this from BattleMap.cs to start a conversation!
	public void StartConversation(string npcId)
	{
		StartConversation(npcId, "Officer", string.Empty, string.Empty);
	}

	public void StartConversation(string npcId, string officerName, string officerPortraitPath, string npcPortraitPath)
	{
		_currentNpcId = npcId;
		_currentOfficerName = string.IsNullOrEmpty(officerName) ? "Officer" : officerName;
		_currentOfficerPortraitPath = officerPortraitPath ?? string.Empty;
		_currentNpcPortraitPath = npcPortraitPath ?? string.Empty;
		ApplyPortraits();
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
		if (NpcNameLabel != null)
		{
			NpcNameLabel.Text = nodeData.SpeakerName;
		}

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
		EmitSignal(SignalName.ConversationEnded);
	}

	private void ApplyPortraits()
	{
		if (OfficerNameLabel != null)
		{
			OfficerNameLabel.Text = _currentOfficerName;
		}

		if (OfficerPortraitRect != null)
		{
			OfficerPortraitRect.Texture = string.IsNullOrEmpty(_currentOfficerPortraitPath)
				? null
				: GD.Load<Texture2D>(_currentOfficerPortraitPath);
		}

		if (NpcPortraitRect != null)
		{
			NpcPortraitRect.Texture = string.IsNullOrEmpty(_currentNpcPortraitPath)
				? null
				: GD.Load<Texture2D>(_currentNpcPortraitPath);
		}

		if (NpcNameLabel != null && string.IsNullOrEmpty(NpcNameLabel.Text))
		{
			NpcNameLabel.Text = "Contact";
		}
	}
}
