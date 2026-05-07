public partial class DoorControlProp : MissionProp
{
	protected override PropInteractionResult BuildInteractionResult(PropInteractionContext context)
	{
		if (Definition?.DoorTargetIds == null || Definition.DoorTargetIds.Count == 0)
		{
			return PropInteractionResult.Blocked($"{GetDisplayName()} has no linked doors.");
		}

		PropInteractionResult result = PropInteractionResult.Completed(
			string.IsNullOrWhiteSpace(Definition.SuccessMessage)
				? $"{GetDisplayName()} rerouted access permissions."
				: Definition.SuccessMessage);
		foreach (string doorId in Definition.DoorTargetIds)
		{
			if (!string.IsNullOrWhiteSpace(doorId))
			{
				result.DoorIdsToToggle.Add(doorId);
			}
		}

		return result;
	}
}
