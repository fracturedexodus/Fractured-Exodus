using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionManager : Node
{
	[Signal] public delegate void MissionTemplatesReloadedEventHandler();
	[Signal] public delegate void MissionStartedEventHandler(string missionId, string scenePath);
	[Signal] public delegate void MissionCompletedEventHandler(string missionId, string outcomeId);

	private const string BuiltInTemplatePathPrefix = "builtin://missions/";

	private readonly Dictionary<string, MissionTemplate> _templatesById = new Dictionary<string, MissionTemplate>();
	private readonly Dictionary<string, MissionTemplate> _templatesByInteractionKey = new Dictionary<string, MissionTemplate>();
	private MissionRegistry _missionRegistry;
	private GlobalData _globalData;
	private OfficerService _officerService;

	public override void _Ready()
	{
		_globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		_officerService = _globalData != null ? new OfficerService(_globalData) : null;
		_missionRegistry = new MissionRegistry();
		ReloadTemplates();
	}

	public void ReloadTemplates()
	{
		_templatesById.Clear();
		_templatesByInteractionKey.Clear();

		foreach (MissionTemplate template in _missionRegistry.LoadTemplates())
		{
			RegisterTemplate(template);
		}

		EnsureBuiltInTemplates();
		EmitSignal(SignalName.MissionTemplatesReloaded);
		GD.Print($"MissionManager loaded {_templatesById.Count} mission templates.");
	}

	public void RegisterTemplate(MissionTemplate template)
	{
		if (template == null || string.IsNullOrWhiteSpace(template.MissionId) || !template.IsEnabled)
		{
			return;
		}

		_templatesById[template.MissionId] = template;

		foreach (string rawKey in template.InteractionKeys ?? new Godot.Collections.Array<string>())
		{
			string normalizedKey = NormalizeKey(rawKey);
			if (!string.IsNullOrEmpty(normalizedKey))
			{
				_templatesByInteractionKey[normalizedKey] = template;
			}
		}
	}

	public MissionTemplate GetTemplate(string missionId)
	{
		if (string.IsNullOrWhiteSpace(missionId))
		{
			return null;
		}

		return _templatesById.TryGetValue(missionId, out MissionTemplate template) ? template : null;
	}

	public MissionDefinition GetDefinition(string missionId)
	{
		return GetTemplate(missionId)?.ToDefinition();
	}

	public bool TryGetTemplateForInteraction(string interactionKey, out MissionTemplate template)
	{
		string normalizedKey = NormalizeKey(interactionKey);
		if (string.IsNullOrEmpty(normalizedKey))
		{
			template = null;
			return false;
		}

		return _templatesByInteractionKey.TryGetValue(normalizedKey, out template);
	}

	public MissionRuntimeState PrepareMission(
		string missionId,
		string returnScenePath = "",
		string sourceEncounterName = "",
		string sourceNodeType = "",
		string sourceNodeId = "",
		string sourceInteractionKey = "")
	{
		MissionTemplate template = GetTemplate(missionId);
		if (template == null || _globalData == null)
		{
			return null;
		}

		MissionRuntimeState state = BuildMissionState(
			template,
			returnScenePath,
			sourceEncounterName,
			sourceNodeType,
			sourceNodeId,
			sourceInteractionKey);

		_globalData.SetCurrentMissionState(state);
		EmitSignal(SignalName.MissionStarted, state.MissionID, state.ScenePath);
		return state;
	}

	public MissionRuntimeState PrepareMissionFromInteraction(
		string interactionKey,
		string returnScenePath = "",
		string sourceEncounterName = "",
		string sourceNodeType = "",
		string sourceNodeId = "")
	{
		if (!TryGetTemplateForInteraction(interactionKey, out MissionTemplate template))
		{
			return null;
		}

		string resolvedNodeType = string.IsNullOrWhiteSpace(sourceNodeType) ? template.SourceNodeType : sourceNodeType;
		return PrepareMission(
			template.MissionId,
			returnScenePath,
			sourceEncounterName,
			resolvedNodeType,
			sourceNodeId,
			interactionKey);
	}

	public bool LaunchMission(
		Node caller,
		string missionId,
		string returnScenePath = "",
		string sourceEncounterName = "",
		string sourceNodeType = "",
		string sourceNodeId = "",
		string sourceInteractionKey = "")
	{
		MissionRuntimeState state = PrepareMission(
			missionId,
			returnScenePath,
			sourceEncounterName,
			sourceNodeType,
			sourceNodeId,
			sourceInteractionKey);

		if (state == null || string.IsNullOrWhiteSpace(state.ScenePath))
		{
			return false;
		}

		ChangeScene(caller, state.ScenePath);
		return true;
	}

	public bool LaunchMissionFromInteraction(
		Node caller,
		string interactionKey,
		string returnScenePath = "",
		string sourceEncounterName = "",
		string sourceNodeType = "",
		string sourceNodeId = "")
	{
		MissionRuntimeState state = PrepareMissionFromInteraction(
			interactionKey,
			returnScenePath,
			sourceEncounterName,
			sourceNodeType,
			sourceNodeId);

		if (state == null || string.IsNullOrWhiteSpace(state.ScenePath))
		{
			return false;
		}

		ChangeScene(caller, state.ScenePath);
		return true;
	}

	public MissionRuntimeState GetCurrentMissionState()
	{
		return _globalData?.GetCurrentMissionState();
	}

	public bool HasCompletedMission(string missionId)
	{
		return !string.IsNullOrWhiteSpace(missionId)
			&& _globalData?.CompletedMissionIDs?.Contains(missionId) == true;
	}

	public string GetOutcomeId(string missionId)
	{
		if (string.IsNullOrWhiteSpace(missionId) || _globalData?.MissionOutcomes == null)
		{
			return string.Empty;
		}

		return _globalData.MissionOutcomes.TryGetValue(missionId, out string outcomeId) ? outcomeId : string.Empty;
	}

	public void ApplyOutcome(MissionOutcome outcome)
	{
		if (_globalData == null || outcome == null || string.IsNullOrWhiteSpace(outcome.MissionID))
		{
			return;
		}

		new CampaignRewardService(_globalData).ApplyReward(outcome.Reward);
		if (outcome.PopulationSaved > 0)
		{
			float existingPopulation = _globalData.FleetResources.ContainsKey(GameConstants.ResourceKeys.Population)
				? _globalData.FleetResources[GameConstants.ResourceKeys.Population].AsSingle()
				: 0f;
			_globalData.FleetResources[GameConstants.ResourceKeys.Population] = existingPopulation + outcome.PopulationSaved;
		}

		if (_globalData.RescuedRemnants == null)
		{
			_globalData.RescuedRemnants = new List<RemnantRecord>();
		}

		foreach (RemnantRecord remnant in outcome.RescuedRemnants ?? new List<RemnantRecord>())
		{
			if (remnant == null || string.IsNullOrWhiteSpace(remnant.RecordId))
			{
				continue;
			}

			if (_globalData.RescuedRemnants.Any(existing => existing != null && existing.RecordId == remnant.RecordId))
			{
				continue;
			}

			_globalData.RescuedRemnants.Add(remnant.Clone());
		}

		_officerService?.ApplyDirectApprovalChanges(outcome.ApprovalChanges);

		foreach (string shipName in outcome.FallenOfficerShipNames ?? new List<string>())
		{
			if (string.IsNullOrWhiteSpace(shipName))
			{
				continue;
			}

			_globalData.ShipOfficers?.Remove(shipName);
			if (_globalData.PendingOfficerReplacementShipNames != null
				&& !_globalData.PendingOfficerReplacementShipNames.Contains(shipName))
			{
				_globalData.PendingOfficerReplacementShipNames.Add(shipName);
			}
		}

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
		EmitSignal(SignalName.MissionCompleted, outcome.MissionID, outcome.OutcomeID);
	}

	public void ReturnToMissionSource(Node caller)
	{
		if (caller == null || _globalData == null)
		{
			return;
		}

		string returnScenePath = string.IsNullOrWhiteSpace(_globalData.MissionReturnScenePath)
			? "res://exploration_battle.tscn"
			: _globalData.MissionReturnScenePath;
		ChangeScene(caller, returnScenePath);
	}

	private MissionRuntimeState BuildMissionState(
		MissionTemplate template,
		string returnScenePath,
		string sourceEncounterName,
		string sourceNodeType,
		string sourceNodeId,
		string sourceInteractionKey)
	{
		List<string> participatingShips = SelectParticipatingShips(template);
		List<string> participatingOfficerIds = participatingShips
			.Select(shipName => _officerService?.GetOfficerForShip(shipName)?.OfficerID ?? string.Empty)
			.Where(officerId => !string.IsNullOrWhiteSpace(officerId))
			.ToList();

		return new MissionRuntimeState
		{
			MissionID = template.MissionId,
			MissionTitle = template.Title,
			ScenePath = template.MissionScenePath,
			TemplateResourcePath = string.IsNullOrWhiteSpace(template.ResourcePath)
				? $"{BuiltInTemplatePathPrefix}{template.MissionId}"
				: template.ResourcePath,
			ReturnScenePath = string.IsNullOrWhiteSpace(returnScenePath) ? template.DefaultReturnScenePath : returnScenePath,
			SourceSystem = _globalData?.SavedSystem ?? string.Empty,
			SourceEncounterName = sourceEncounterName,
			SourceNodeType = sourceNodeType,
			SourceNodeID = sourceNodeId,
			SourceInteractionKey = NormalizeKey(sourceInteractionKey),
			ParticipatingShipNames = participatingShips,
			ParticipatingOfficerIDs = participatingOfficerIds
		};
	}

	private List<string> SelectParticipatingShips(MissionTemplate template)
	{
		List<string> explicitlySelectedShips = (_globalData?.SelectedMissionOfficerShipNames ?? new List<string>())
			.Where(shipName => !string.IsNullOrWhiteSpace(shipName))
			.Distinct()
			.ToList();
		if (explicitlySelectedShips.Count > 0)
		{
			return explicitlySelectedShips
				.Where(shipName => _officerService?.GetOfficerForShip(shipName) != null)
				.Take(Mathf.Max(1, template.RecommendedOfficerCount))
				.ToList();
		}

		List<string> selectedFleet = (_globalData?.SelectedPlayerFleet ?? new List<string>())
			.Where(shipName => !string.IsNullOrWhiteSpace(shipName))
			.ToList();

		List<string> crewedShips = selectedFleet
			.Where(shipName => _officerService?.GetOfficerForShip(shipName) != null)
			.Take(template.RecommendedOfficerCount)
			.ToList();

		if (crewedShips.Count > 0)
		{
			return crewedShips;
		}

		return selectedFleet.Take(template.RecommendedOfficerCount).ToList();
	}

	private void EnsureBuiltInTemplates()
	{
		if (_templatesById.ContainsKey("black_site_relay"))
		{
			return;
		}

		var builtInTemplate = new MissionTemplate
		{
			MissionId = "black_site_relay",
			Title = "Black Site Relay",
			Description = "Investigate a failing Custodian relay and decide whether to save the survivors or secure the archive core.",
			MissionScenePath = "res://black_site_relay.tscn",
			LayoutResourcePath = "res://Data/MissionLayouts/black_site_relay_builder.json",
			DefaultReturnScenePath = "res://exploration_battle.tscn",
			RecommendedOfficerCount = 2,
			SourceNodeType = "Planet",
			ObjectiveText = "OBJECTIVE: Investigate the relay, assess the survivors, and decide what to save.",
			PromptText = "Controls: left click an officer to select, left click a floor tile to move, TAB or 1-2 to switch officers, middle mouse drag or WASD to pan, mouse wheel or +/- to zoom. Use the relay uplink to assess the crisis, complete either the survivor shelter or archive core objective, then rally the whole team at evac to extract.",
			PrimaryActionText = "SAVE SURVIVORS",
			PrimaryOutcomeId = "survivors_saved",
			PrimaryOutcomeRequiredFlags = new Godot.Collections.Array<string>
			{
				"relay_survivor_objective_complete"
			},
			PrimaryOutcomeBlockedFlags = new Godot.Collections.Array<string>
			{
				"relay_archive_objective_complete"
			},
			SecondaryActionText = "SECURE ARCHIVE",
			SecondaryOutcomeId = "archive_secured",
			SecondaryOutcomeRequiredFlags = new Godot.Collections.Array<string>
			{
				"relay_archive_objective_complete"
			},
			SecondaryOutcomeBlockedFlags = new Godot.Collections.Array<string>
			{
				"relay_survivor_objective_complete"
			},
			DefaultDialogueId = "trigger_dialogue",
			InteractionKeys = new Godot.Collections.Array<string>
			{
				"black_site_relay",
				"planet:black_site_relay"
			}
		};

		RegisterTemplate(builtInTemplate);
	}

	private void ChangeScene(Node caller, string scenePath)
	{
		SceneTransition transitioner = caller.GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(scenePath);
		}
		else
		{
			caller.GetTree().ChangeSceneToFile(scenePath);
		}
	}

	private static string NormalizeKey(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
	}
}
