using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionProp : Area2D, IInteractable
{
	[Signal] public delegate void InteractionCommittedEventHandler(MissionProp prop);

	[Export] public PropDefinition Definition { get; set; }
	[Export] public string PropInstanceId { get; set; } = string.Empty;
	[Export] public NodePath VisualSpritePath { get; set; } = new NodePath("Sprite2D");

	public bool IsConsumed { get; private set; }

	private Sprite2D _visualSprite;

	public override void _Ready()
	{
		_visualSprite = GetNodeOrNull<Sprite2D>(VisualSpritePath);
		ResolvePropInstanceId();
		ApplyDefinitionVisual();
	}

	public virtual bool CanInteract(PropInteractionContext context)
	{
		return string.IsNullOrEmpty(GetBlockedReason(context));
	}

	public virtual PropInteractionResult Interact(PropInteractionContext context)
	{
		string blockedReason = GetBlockedReason(context);
		if (!string.IsNullOrEmpty(blockedReason))
		{
			return PropInteractionResult.Blocked(blockedReason);
		}

		PropInteractionResult result = BuildInteractionResult(context) ?? PropInteractionResult.Blocked("Interaction returned no result.");
		if (!result.Success)
		{
			return result;
		}

		ApplyDefinitionDefaults(result, context);
		return result;
	}

	public virtual void CommitInteractionResult(PropInteractionResult result, PropInteractionContext context)
	{
		if (result == null || !result.Success)
		{
			return;
		}

		ApplyGlobalFlags(result, context?.GlobalData);
		ApplyReward(result, context?.GlobalData);

		if (result.ConsumeProp)
		{
			IsConsumed = true;
			if (Definition?.HideWhenConsumed == true)
			{
				Visible = false;
				Monitoring = false;
				Monitorable = false;
			}
			else
			{
				Modulate = new Color(1f, 1f, 1f, 0.4f);
			}
		}

		EmitSignal(SignalName.InteractionCommitted, this);
	}

	protected virtual PropInteractionResult BuildInteractionResult(PropInteractionContext context)
	{
		return PropInteractionResult.Completed();
	}

	protected virtual string GetBlockedReason(PropInteractionContext context)
	{
		if (Definition == null)
		{
			return "No prop definition assigned.";
		}

		if (IsConsumed)
		{
			return $"{GetDisplayName()} has already been used.";
		}

		if (context?.Officer == null)
		{
			return "No officer is available to interact.";
		}

		if (!string.IsNullOrWhiteSpace(Definition.RequiredOfficerSpecialty)
			&& !string.Equals(context.Officer.Specialty, Definition.RequiredOfficerSpecialty, System.StringComparison.OrdinalIgnoreCase))
		{
			return $"{GetDisplayName()} requires {Definition.RequiredOfficerSpecialty}.";
		}

		if (Definition.RequiredFlags == null || Definition.RequiredFlags.Count == 0)
		{
			return string.Empty;
		}

		HashSet<string> activeFlags = context.GlobalData?.StoryFlags != null
			? context.GlobalData.StoryFlags.ToHashSet()
			: new HashSet<string>();
		foreach (string requiredFlag in Definition.RequiredFlags)
		{
			if (!string.IsNullOrWhiteSpace(requiredFlag) && !activeFlags.Contains(requiredFlag))
			{
				return $"{GetDisplayName()} is not ready yet.";
			}
		}

		return string.Empty;
	}

	protected string GetDisplayName()
	{
		return string.IsNullOrWhiteSpace(Definition?.DisplayName) ? Name : Definition.DisplayName;
	}

	private void ResolvePropInstanceId()
	{
		if (!string.IsNullOrWhiteSpace(PropInstanceId))
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(Definition?.PropId))
		{
			PropInstanceId = Definition.PropId;
			return;
		}

		PropInstanceId = Name;
	}

	private void ApplyDefinitionVisual()
	{
		if (_visualSprite == null || Definition == null || string.IsNullOrWhiteSpace(Definition.SpriteTexturePath))
		{
			return;
		}

		_visualSprite.Texture = GD.Load<Texture2D>(Definition.SpriteTexturePath);
	}

	private void ApplyDefinitionDefaults(PropInteractionResult result, PropInteractionContext context)
	{
		if (Definition == null)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(result.DialogueId))
		{
			result.DialogueId = Definition.DialogueId;
		}

		if (string.IsNullOrWhiteSpace(result.MissionEventId))
		{
			result.MissionEventId = Definition.MissionEventId;
		}

		if (string.IsNullOrWhiteSpace(result.StatusMessage))
		{
			result.StatusMessage = Definition.SuccessMessage;
		}

		if (result.Reward != null)
		{
			result.Reward.RawMaterials += Definition.RewardRawMaterials;
			result.Reward.EnergyCores += Definition.RewardEnergyCores;
			result.Reward.AncientTech += Definition.RewardAncientTech;
		}

		if (Definition.SetFlags != null)
		{
			foreach (string flag in Definition.SetFlags)
			{
				if (!string.IsNullOrWhiteSpace(flag) && !result.FlagsToSet.Contains(flag))
				{
					result.FlagsToSet.Add(flag);
				}
			}
		}

		if (Definition.DoorTargetIds != null)
		{
			foreach (string doorId in Definition.DoorTargetIds)
			{
				if (!string.IsNullOrWhiteSpace(doorId) && !result.DoorIdsToToggle.Contains(doorId))
				{
					result.DoorIdsToToggle.Add(doorId);
				}
			}
		}

		if (Definition.OneShot)
		{
			result.ConsumeProp = true;
		}
	}

	private void ApplyGlobalFlags(PropInteractionResult result, GlobalData globalData)
	{
		if (globalData?.StoryFlags == null || result.FlagsToSet == null)
		{
			return;
		}

		foreach (string flag in result.FlagsToSet)
		{
			if (!string.IsNullOrWhiteSpace(flag) && !globalData.StoryFlags.Contains(flag))
			{
				globalData.StoryFlags.Add(flag);
			}
		}
	}

	private void ApplyReward(PropInteractionResult result, GlobalData globalData)
	{
		if (globalData?.FleetResources == null || result.Reward == null)
		{
			return;
		}

		globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] =
			globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle() + result.Reward.RawMaterials;
		globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores] =
			globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() + result.Reward.EnergyCores;
		globalData.FleetResources[GameConstants.ResourceKeys.AncientTech] =
			globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle() + result.Reward.AncientTech;
	}
}
