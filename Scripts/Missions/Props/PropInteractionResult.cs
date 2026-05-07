using System.Collections.Generic;

public class PropInteractionResult
{
	public bool Success { get; set; }
	public string FailureMessage { get; set; } = string.Empty;
	public string StatusMessage { get; set; } = string.Empty;
	public string DialogueId { get; set; } = string.Empty;
	public string MissionEventId { get; set; } = string.Empty;
	public bool ConsumeProp { get; set; }
	public MissionReward Reward { get; set; } = new MissionReward();
	public List<string> FlagsToSet { get; set; } = new List<string>();
	public List<string> DoorIdsToToggle { get; set; } = new List<string>();

	public static PropInteractionResult Blocked(string failureMessage)
	{
		return new PropInteractionResult
		{
			Success = false,
			FailureMessage = failureMessage ?? string.Empty
		};
	}

	public static PropInteractionResult Completed(string statusMessage = "")
	{
		return new PropInteractionResult
		{
			Success = true,
			StatusMessage = statusMessage ?? string.Empty
		};
	}
}
