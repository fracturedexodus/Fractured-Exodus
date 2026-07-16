using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class AmbientEventChoiceState
{
	public bool IsAvailable { get; set; }
	public string RequirementText { get; set; } = string.Empty;
}

public class AmbientEventResolution
{
	public bool Applied { get; set; }
	public bool EventCompleted { get; set; }
	public string ResultText { get; set; } = string.Empty;
	public string OfficerDecisionTag { get; set; } = string.Empty;
	public string FailureReason { get; set; } = string.Empty;
}

public class AmbientEventService
{
	private readonly GlobalData _globalData;

	public AmbientEventService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public string GetCurrentRegion()
	{
		if (_globalData?.CurrentSectorStars == null || string.IsNullOrWhiteSpace(_globalData.SavedSystem))
		{
			return string.Empty;
		}

		return _globalData.CurrentSectorStars
			.FirstOrDefault(star => star != null && star.SystemName == _globalData.SavedSystem)?.Region ?? string.Empty;
	}

	public AmbientEventInstanceData GetInstance(string instanceId)
	{
		if (_globalData == null
			|| string.IsNullOrWhiteSpace(_globalData.SavedSystem)
			|| !_globalData.ExploredSystems.TryGetValue(_globalData.SavedSystem, out SystemData system))
		{
			return null;
		}

		return system.AmbientEvents?.FirstOrDefault(instance => instance != null && instance.InstanceId == instanceId);
	}

	public AmbientEventChoiceState GetChoiceState(AmbientEventChoice choice, MapEntity actingShip)
	{
		if (choice == null)
		{
			return Unavailable("Choice data is unavailable.");
		}

		if (actingShip == null || actingShip.Type != GameConstants.EntityTypes.PlayerFleet)
		{
			return Unavailable("A fleet ship must answer the signal.");
		}

		if (choice.ActionCost > 0 && actingShip.CurrentActions < choice.ActionCost)
		{
			return Unavailable($"Requires {choice.ActionCost} ship action.");
		}

		if (choice.RequiredActingShips?.Count > 0 && !choice.RequiredActingShips.Contains(actingShip.Name))
		{
			return Unavailable($"Requires: {string.Join(" or ", choice.RequiredActingShips)}.");
		}

		foreach (KeyValuePair<string, float> cost in choice.ResourceCosts ?? new Dictionary<string, float>())
		{
			float available = GetResource(cost.Key);
			if (available < cost.Value)
			{
				return Unavailable($"Requires {cost.Value:0.##} {cost.Key}.");
			}
		}

		return new AmbientEventChoiceState { IsAvailable = true };
	}

	public AmbientEventResolution ApplyChoice(AmbientEventInstanceData instance, AmbientEventChoice choice, MapEntity actingShip)
	{
		if (instance == null || instance.IsResolved)
		{
			return Failed("This signal has already been resolved.");
		}

		AmbientEventChoiceState choiceState = GetChoiceState(choice, actingShip);
		if (!choiceState.IsAvailable)
		{
			return Failed(choiceState.RequirementText);
		}

		actingShip.CurrentActions = Math.Max(0, actingShip.CurrentActions - choice.ActionCost);
		foreach (KeyValuePair<string, float> cost in choice.ResourceCosts ?? new Dictionary<string, float>())
		{
			ChangeResource(cost.Key, -cost.Value);
		}

		AmbientEventEffects effects = choice.Effects ?? new AmbientEventEffects();
		foreach (KeyValuePair<string, float> change in effects.ResourceChanges ?? new Dictionary<string, float>())
		{
			ChangeResource(change.Key, change.Value);
		}

		_globalData.RegionalCounters ??= new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> change in effects.RegionalCounterChanges ?? new Dictionary<string, int>())
		{
			_globalData.RegionalCounters.TryGetValue(change.Key, out int currentValue);
			_globalData.RegionalCounters[change.Key] = currentValue + change.Value;
		}

		_globalData.StoryFlags ??= new List<string>();
		foreach (string flag in effects.SetFlags ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(flag) && !_globalData.StoryFlags.Contains(flag))
			{
				_globalData.StoryFlags.Add(flag);
			}
		}

		if (!string.IsNullOrWhiteSpace(choice.NextNodeId))
		{
			instance.CurrentNodeId = choice.NextNodeId;
		}

		if (choice.CompleteEvent)
		{
			instance.IsResolved = true;
			instance.ResolutionId = choice.ChoiceId;
		}

		return new AmbientEventResolution
		{
			Applied = true,
			EventCompleted = choice.CompleteEvent,
			ResultText = effects.ResultText,
			OfficerDecisionTag = effects.OfficerDecisionTag
		};
	}

	private float GetResource(string resourceKey)
	{
		return _globalData?.FleetResources != null && _globalData.FleetResources.ContainsKey(resourceKey)
			? _globalData.FleetResources[resourceKey].AsSingle()
			: 0f;
	}

	private void ChangeResource(string resourceKey, float delta)
	{
		if (_globalData?.FleetResources == null || string.IsNullOrWhiteSpace(resourceKey))
		{
			return;
		}

		_globalData.FleetResources[resourceKey] = Math.Max(0f, GetResource(resourceKey) + delta);
	}

	private static AmbientEventChoiceState Unavailable(string reason)
	{
		return new AmbientEventChoiceState { IsAvailable = false, RequirementText = reason };
	}

	private static AmbientEventResolution Failed(string reason)
	{
		return new AmbientEventResolution { FailureReason = reason };
	}
}

