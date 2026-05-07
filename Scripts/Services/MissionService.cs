using Godot;

public class MissionService
{
	private readonly GlobalData _globalData;
	private MissionManager _missionManager;

	public MissionService(GlobalData globalData)
	{
		_globalData = globalData;
		_missionManager = ResolveMissionManager();
	}

	public MissionDefinition GetDefinition(string missionId)
	{
		return ResolveMissionManager()?.GetDefinition(missionId);
	}

	public MissionTemplate GetTemplate(string missionId)
	{
		return ResolveMissionManager()?.GetTemplate(missionId);
	}

	public MissionRuntimeState PrepareMission(string missionId, string returnScenePath = "", string sourceEncounterName = "")
	{
		return ResolveMissionManager()?.PrepareMission(missionId, returnScenePath, sourceEncounterName);
	}

	public MissionRuntimeState GetCurrentMissionState()
	{
		return ResolveMissionManager()?.GetCurrentMissionState() ?? _globalData?.GetCurrentMissionState();
	}

	public void ApplyOutcome(MissionOutcome outcome)
	{
		ResolveMissionManager()?.ApplyOutcome(outcome);
	}

	public void ReturnToMissionSource(Node caller)
	{
		ResolveMissionManager()?.ReturnToMissionSource(caller);
	}

	public MissionRuntimeState PrepareMissionFromInteraction(string interactionKey, string returnScenePath = "", string sourceEncounterName = "", string sourceNodeType = "", string sourceNodeId = "")
	{
		return ResolveMissionManager()?.PrepareMissionFromInteraction(interactionKey, returnScenePath, sourceEncounterName, sourceNodeType, sourceNodeId);
	}

	public bool LaunchMissionFromInteraction(Node caller, string interactionKey, string returnScenePath = "", string sourceEncounterName = "", string sourceNodeType = "", string sourceNodeId = "")
	{
		return ResolveMissionManager()?.LaunchMissionFromInteraction(caller, interactionKey, returnScenePath, sourceEncounterName, sourceNodeType, sourceNodeId) == true;
	}

	private MissionManager ResolveMissionManager()
	{
		if (_missionManager != null && GodotObject.IsInstanceValid(_missionManager))
		{
			return _missionManager;
		}

		SceneTree tree = Engine.GetMainLoop() as SceneTree;
		_missionManager = tree?.Root?.GetNodeOrNull<MissionManager>("/root/MissionManager");
		return _missionManager;
	}
}
