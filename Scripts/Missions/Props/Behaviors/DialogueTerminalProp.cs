public partial class DialogueTerminalProp : MissionProp
{
	protected override PropInteractionResult BuildInteractionResult(PropInteractionContext context)
	{
		string dialogueId = !string.IsNullOrWhiteSpace(Definition?.DialogueId)
			? Definition.DialogueId
			: context?.MissionTemplate?.DefaultDialogueId ?? string.Empty;
		if (string.IsNullOrWhiteSpace(dialogueId))
		{
			return PropInteractionResult.Blocked($"{GetDisplayName()} has no dialogue assigned.");
		}

		return new PropInteractionResult
		{
			Success = true,
			DialogueId = dialogueId,
			StatusMessage = string.IsNullOrWhiteSpace(Definition?.SuccessMessage)
				? $"{GetDisplayName()} is online."
				: Definition.SuccessMessage
		};
	}
}
