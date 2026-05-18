using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionProp : Area2D, IInteractable
{
	[Signal] public delegate void InteractionCommittedEventHandler(MissionProp prop);

	[Export] public PropDefinition Definition { get; set; }
	[Export] public string PropInstanceId { get; set; } = string.Empty;
	[Export] public NodePath VisualSpritePath { get; set; } = new NodePath("Sprite2D");
	[Export] public float PlacementRotationDegrees { get; set; }
	[Export] public bool PlacementFlipH { get; set; }
	[Export] public bool PlacementFlipV { get; set; }

	public bool IsConsumed { get; private set; }

	private Sprite2D _visualSprite;
	private bool _isFogVisible = true;
	private bool _isCoverOccluded;

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
		ApplyReward(result, context);

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

	public void SetFogVisibility(bool isVisible)
	{
		_isFogVisible = isVisible;
		if (IsConsumed && Definition?.HideWhenConsumed == true)
		{
			Visible = false;
			Monitoring = false;
			Monitorable = false;
			return;
		}

		bool shouldBeVisible = isVisible && !_isCoverOccluded;
		Visible = shouldBeVisible;
		Monitoring = shouldBeVisible;
		Monitorable = shouldBeVisible;
	}

	public void SetCoverOccluded(bool occluded)
	{
		_isCoverOccluded = occluded;
		if (IsConsumed && Definition?.HideWhenConsumed == true)
		{
			Visible = false;
			Monitoring = false;
			Monitorable = false;
			return;
		}

		bool shouldBeVisible = _isFogVisible && !_isCoverOccluded;
		Visible = shouldBeVisible;
		Monitoring = shouldBeVisible;
		Monitorable = shouldBeVisible;
	}

	public void ApplySavedConsumptionState(bool isConsumed)
	{
		IsConsumed = isConsumed;
		if (!IsConsumed)
		{
			Modulate = Colors.White;
			SetFogVisibility(_isFogVisible);
			return;
		}

		if (Definition?.HideWhenConsumed == true)
		{
			Visible = false;
			Monitoring = false;
			Monitorable = false;
			return;
		}

		Modulate = new Color(1f, 1f, 1f, 0.4f);
		SetFogVisibility(_isFogVisible);
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

		HashSet<string> activeFlags = context.GlobalData?.StoryFlags != null
			? context.GlobalData.StoryFlags.ToHashSet()
			: new HashSet<string>();
		foreach (string blockedFlag in Definition.BlockedFlags ?? new Godot.Collections.Array<string>())
		{
			if (!string.IsNullOrWhiteSpace(blockedFlag) && activeFlags.Contains(blockedFlag))
			{
				return $"{GetDisplayName()} is no longer available.";
			}
		}

		if (Definition.RequiredFlags == null || Definition.RequiredFlags.Count == 0)
		{
			return string.Empty;
		}

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

		Texture2D texture = GD.Load<Texture2D>(Definition.SpriteTexturePath);
		_visualSprite.Texture = texture;
		_visualSprite.Scale = PropVisualSizing.GetScale(texture, Definition.VisualScaleMultiplier);
		_visualSprite.FlipH = PlacementFlipH;
		_visualSprite.FlipV = PlacementFlipV;
		RotationDegrees = PlacementRotationDegrees;
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
			if (Definition.RewardFleetItemIds != null)
			{
				foreach (string itemId in Definition.RewardFleetItemIds)
				{
					if (!string.IsNullOrWhiteSpace(itemId) && !result.Reward.FleetItemIds.Contains(itemId))
					{
						result.Reward.FleetItemIds.Add(itemId);
					}
				}
			}
			if (Definition.RewardOfficerItemIds != null)
			{
				foreach (string itemId in Definition.RewardOfficerItemIds)
				{
					if (!string.IsNullOrWhiteSpace(itemId) && !result.Reward.OfficerItemIds.Contains(itemId))
					{
						result.Reward.OfficerItemIds.Add(itemId);
					}
				}
			}
			if (Definition.RewardCodexEntryIds != null)
			{
				foreach (string entryId in Definition.RewardCodexEntryIds)
				{
					if (!string.IsNullOrWhiteSpace(entryId) && !result.Reward.CodexEntryIds.Contains(entryId))
					{
						result.Reward.CodexEntryIds.Add(entryId);
					}
				}
			}
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

	private void ApplyReward(PropInteractionResult result, PropInteractionContext context)
	{
		if (context?.GlobalData == null || result.Reward == null)
		{
			return;
		}

		new CampaignRewardService(context.GlobalData).ApplyReward(result.Reward, context.Officer?.OfficerID ?? string.Empty);
	}
}
