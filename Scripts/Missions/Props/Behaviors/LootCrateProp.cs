public partial class LootCrateProp : MissionProp
{
	protected override PropInteractionResult BuildInteractionResult(PropInteractionContext context)
	{
		return new PropInteractionResult
		{
			Success = true,
			StatusMessage = string.IsNullOrWhiteSpace(Definition?.SuccessMessage)
				? $"{GetDisplayName()} secured."
				: Definition.SuccessMessage,
			ConsumeProp = true
		};
	}
}
