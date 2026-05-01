using Godot;
using System.Collections.Generic;
using System.Linq;

public class MissionService
{
	private readonly GlobalData _globalData;
	private readonly OfficerService _officerService;

	private static readonly Dictionary<string, MissionDefinition> MissionDefinitions = new Dictionary<string, MissionDefinition>
	{
		{
			"black_site_relay",
			new MissionDefinition
			{
				MissionID = "black_site_relay",
				ScenePath = "res://black_site_relay.tscn",
				Title = "Black Site Relay",
				Description = "Investigate a failing Custodian relay and decide whether to save the survivors or secure the archive core.",
				DefaultReturnScenePath = "res://exploration_battle.tscn",
				RecommendedOfficerCount = 2
			}
		}
	};

	public MissionService(GlobalData globalData)
	{
		_globalData = globalData;
		_officerService = globalData != null ? new OfficerService(globalData) : null;
	}

	public MissionDefinition GetDefinition(string missionId)
	{
		if (string.IsNullOrEmpty(missionId))
		{
			return null;
		}

		return MissionDefinitions.TryGetValue(missionId, out MissionDefinition definition) ? definition : null;
	}

	public MissionRuntimeState PrepareMission(string missionId, string returnScenePath = "", string sourceEncounterName = "")
	{
		if (_globalData == null)
		{
			return null;
		}

		MissionDefinition definition = GetDefinition(missionId);
		if (definition == null)
		{
			return null;
		}

		List<string> participatingShips = (_globalData.SelectedPlayerFleet ?? new List<string>())
			.Where(shipName => _officerService?.GetOfficerForShip(shipName) != null)
			.Take(definition.RecommendedOfficerCount)
			.ToList();

		List<string> participatingOfficerIds = participatingShips
			.Select(shipName => _officerService?.GetOfficerForShip(shipName)?.OfficerID ?? string.Empty)
			.Where(officerId => !string.IsNullOrEmpty(officerId))
			.ToList();

		MissionRuntimeState state = new MissionRuntimeState
		{
			MissionID = definition.MissionID,
			MissionTitle = definition.Title,
			ReturnScenePath = string.IsNullOrEmpty(returnScenePath) ? definition.DefaultReturnScenePath : returnScenePath,
			SourceSystem = _globalData.SavedSystem,
			SourceEncounterName = sourceEncounterName,
			ParticipatingShipNames = participatingShips,
			ParticipatingOfficerIDs = participatingOfficerIds
		};

		_globalData.SetCurrentMissionState(state);
		return state;
	}

	public MissionRuntimeState GetCurrentMissionState()
	{
		return _globalData?.GetCurrentMissionState();
	}

	public void ApplyOutcome(MissionOutcome outcome)
	{
		if (_globalData == null || outcome == null || string.IsNullOrEmpty(outcome.MissionID))
		{
			return;
		}

		_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] =
			_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle() + outcome.Reward.RawMaterials;
		_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores] =
			_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() + outcome.Reward.EnergyCores;
		_globalData.FleetResources[GameConstants.ResourceKeys.AncientTech] =
			_globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle() + outcome.Reward.AncientTech;

		_officerService?.ApplyDirectApprovalChanges(outcome.ApprovalChanges);

		foreach (string flag in outcome.FlagsToSet ?? new List<string>())
		{
			if (!_globalData.StoryFlags.Contains(flag))
			{
				_globalData.StoryFlags.Add(flag);
			}
		}

		if (!_globalData.CompletedMissionIDs.Contains(outcome.MissionID))
		{
			_globalData.CompletedMissionIDs.Add(outcome.MissionID);
		}

		_globalData.MissionOutcomes[outcome.MissionID] = outcome.OutcomeID;
		_globalData.ClearCurrentMissionState();
	}

	public void ReturnToMissionSource(Node caller)
	{
		if (caller == null || _globalData == null)
		{
			return;
		}

		string returnScenePath = string.IsNullOrEmpty(_globalData.MissionReturnScenePath)
			? "res://exploration_battle.tscn"
			: _globalData.MissionReturnScenePath;

		SceneTransition transitioner = caller.GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(returnScenePath);
		}
		else
		{
			caller.GetTree().ChangeSceneToFile(returnScenePath);
		}
	}
}
