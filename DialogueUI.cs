using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class DialogueUI : CanvasLayer
{
	[Signal]
	public delegate void ConversationEndedEventHandler();
	[Signal]
	public delegate void DialogueStateChangedEventHandler();

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
		DialogueNode nodeData = DialogueRegistry.GetDialogue(_currentNpcId, nodeId);

		if (nodeData == null || !AreFlagConditionsSatisfied(nodeData.RequiredFlags, nodeData.BlockedFlags))
		{
			EndConversation();
			return;
		}

		ApplyDialogueEffects(nodeData.SetFlags, nodeData.QuestToTrigger);

		// Update the visual text
		SpeakerNameLabel.Text = nodeData.SpeakerName;
		DialogueTextDisplay.Text = nodeData.Text;
		if (NpcNameLabel != null)
		{
			NpcNameLabel.Text = nodeData.SpeakerName;
		}

		// Clear out the old buttons
		foreach (Node child in OptionsContainer.GetChildren())
		{
			child.QueueFree();
		}

		// Create new buttons for the player's options
		if (nodeData.Options != null)
		{
			List<DialogueOption> availableOptions = nodeData.Options
				.Where(option => option != null && AreFlagConditionsSatisfied(option.RequiredFlags, option.BlockedFlags))
				.ToList();
			foreach (DialogueOption option in availableOptions)
			{
				Button optionBtn = new Button();
				optionBtn.Text = option.Text;
				
				// When clicked, load the next node (or end if it says "End")
				optionBtn.Pressed += () => 
				{
					ApplyDialogueEffects(option.SetFlags, option.QuestToTrigger);
					if (option.NextNodeId == "End") EndConversation();
					else LoadDialogueNode(option.NextNodeId);
				};
				
				OptionsContainer.AddChild(optionBtn);
			}

			if (availableOptions.Count == 0)
			{
				Button closeButton = new Button
				{
					Text = "End Conversation"
				};
				closeButton.Pressed += EndConversation;
				OptionsContainer.AddChild(closeButton);
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

	private bool AreFlagConditionsSatisfied(System.Collections.Generic.IEnumerable<string> requiredFlags, System.Collections.Generic.IEnumerable<string> blockedFlags)
	{
		if (_globalData?.StoryFlags == null)
		{
			return !(requiredFlags?.Any() ?? false);
		}

		foreach (string requiredFlag in requiredFlags ?? Enumerable.Empty<string>())
		{
			if (!string.IsNullOrWhiteSpace(requiredFlag) && !_globalData.StoryFlags.Contains(requiredFlag))
			{
				return false;
			}
		}

		foreach (string blockedFlag in blockedFlags ?? Enumerable.Empty<string>())
		{
			if (!string.IsNullOrWhiteSpace(blockedFlag) && _globalData.StoryFlags.Contains(blockedFlag))
			{
				return false;
			}
		}

		return true;
	}

	private void ApplyDialogueEffects(System.Collections.Generic.IEnumerable<string> setFlags, string questToTrigger)
	{
		bool changedState = false;
		if (_globalData?.StoryFlags != null)
		{
			foreach (string flag in setFlags ?? Enumerable.Empty<string>())
			{
				if (!string.IsNullOrWhiteSpace(flag) && !_globalData.StoryFlags.Contains(flag))
				{
					_globalData.StoryFlags.Add(flag);
					changedState = true;
				}
			}
		}

		if (!string.IsNullOrEmpty(questToTrigger))
		{
			QuestManager.AcceptQuest(_globalData, questToTrigger);
			changedState = true;
		}

		if (changedState)
		{
			EmitSignal(SignalName.DialogueStateChanged);
		}
	}
}
