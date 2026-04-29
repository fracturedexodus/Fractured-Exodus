using Godot;

public class LongRangeScanTargetingResult
{
	public bool IsTargeting;
	public string Message = string.Empty;
}

public class LongRangeScanExecutionResult
{
	public bool Allowed;
	public string FailureMessage = string.Empty;
	public float EnergyCost;
}

public class LongRangeScanService
{
	private readonly GlobalData _globalData;

	public LongRangeScanService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public LongRangeScanTargetingResult ToggleTargeting(bool isFleetMoving, bool currentlyTargeting)
	{
		if (isFleetMoving)
		{
			return new LongRangeScanTargetingResult
			{
				IsTargeting = currentlyTargeting
			};
		}

		bool newState = !currentlyTargeting;
		return new LongRangeScanTargetingResult
		{
			IsTargeting = newState,
			Message = newState
				? "Awaiting Deep Scan Coordinates... (Left-Click to Scan, Right-Click to Cancel)"
				: string.Empty
		};
	}

	public LongRangeScanTargetingResult CancelTargeting()
	{
		return new LongRangeScanTargetingResult
		{
			IsTargeting = false,
			Message = "Long Range Scan Cancelled."
		};
	}

	public LongRangeScanExecutionResult ExecuteScan(Vector2I targetHex)
	{
		LongRangeScanExecutionResult result = new LongRangeScanExecutionResult
		{
			Allowed = false,
			EnergyCost = 5f
		};

		if (_globalData == null || !_globalData.ExploredSystems.ContainsKey(_globalData.SavedSystem))
		{
			result.FailureMessage = "*** SCAN FAILED: NO ACTIVE SYSTEM DATA ***";
			return result;
		}

		if (_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() < result.EnergyCost)
		{
			result.FailureMessage = $"*** SCAN FAILED: INSUFFICIENT {GameConstants.ResourceKeys.EnergyCores.ToUpper()} ({result.EnergyCost:0.#} Req) ***";
			return result;
		}

		_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores] =
			_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() - result.EnergyCost;

		SystemData system = _globalData.ExploredSystems[_globalData.SavedSystem];
		for (int q = -10; q <= 10; q++)
		{
			int r1 = Mathf.Max(-10, -q - 10);
			int r2 = Mathf.Min(10, -q + 10);
			for (int r = r1; r <= r2; r++)
			{
				Vector2I revealedHex = new Vector2I(targetHex.X + q, targetHex.Y + r);
				if (!system.RadarRevealedHexes.Contains(revealedHex))
				{
					system.RadarRevealedHexes.Add(revealedHex);
				}
			}
		}

		result.Allowed = true;
		return result;
	}
}
