using Godot;
using System.Collections.Generic;

public class CampaignRewardService
{
	private readonly GlobalData _globalData;
	private readonly OfficerService _officerService;

	public CampaignRewardService(GlobalData globalData)
	{
		_globalData = globalData;
		_officerService = globalData != null ? new OfficerService(globalData) : null;
	}

	public void ApplyReward(MissionReward reward, string officerId = "")
	{
		if (_globalData == null || reward == null)
		{
			return;
		}

		ApplyResourceReward(reward);
		ApplyFleetItems(reward.FleetItemIds);
		ApplyOfficerItems(reward.OfficerItemIds, officerId);
		UnlockCodexEntries(reward.CodexEntryIds);
	}

	public void UnlockCodexEntries(IEnumerable<string> entryIds)
	{
		if (_globalData?.UnlockedCodexEntryIDs == null || entryIds == null)
		{
			return;
		}

		foreach (string entryId in entryIds)
		{
			if (!string.IsNullOrWhiteSpace(entryId) && !_globalData.UnlockedCodexEntryIDs.Contains(entryId))
			{
				_globalData.UnlockedCodexEntryIDs.Add(entryId);
			}
		}
	}

	private void ApplyResourceReward(MissionReward reward)
	{
		if (_globalData?.FleetResources == null)
		{
			return;
		}

		_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] =
			_globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle() + reward.RawMaterials;
		_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores] =
			_globalData.FleetResources[GameConstants.ResourceKeys.EnergyCores].AsSingle() + reward.EnergyCores;
		_globalData.FleetResources[GameConstants.ResourceKeys.AncientTech] =
			_globalData.FleetResources[GameConstants.ResourceKeys.AncientTech].AsSingle() + reward.AncientTech;
	}

	private void ApplyFleetItems(IEnumerable<string> itemIds)
	{
		if (itemIds == null)
		{
			return;
		}

		foreach (string itemId in itemIds)
		{
			if (string.IsNullOrWhiteSpace(itemId))
			{
				continue;
			}

			if (_globalData.MasterEquipmentDB != null && _globalData.MasterEquipmentDB.ContainsKey(itemId))
			{
				_globalData.UnequippedInventory ??= new List<string>();
				_globalData.UnequippedInventory.Add(itemId);
				continue;
			}

			_globalData.FleetCargoItemIDs ??= new List<string>();
			_globalData.FleetCargoItemIDs.Add(itemId);
			CampaignItemDefinition item = CampaignItemRegistry.GetItem(itemId);
			if (!string.IsNullOrWhiteSpace(item?.CodexEntryId))
			{
				UnlockCodexEntries(new[] { item.CodexEntryId });
			}
		}
	}

	private void ApplyOfficerItems(IEnumerable<string> itemIds, string officerId)
	{
		if (itemIds == null || string.IsNullOrWhiteSpace(officerId))
		{
			return;
		}

		OfficerState officer = _officerService?.GetOfficerById(officerId);
		if (officer == null)
		{
			return;
		}

		officer.PersonalInventoryItemIDs ??= new List<string>();
		foreach (string itemId in itemIds)
		{
			if (string.IsNullOrWhiteSpace(itemId))
			{
				continue;
			}

			officer.PersonalInventoryItemIDs.Add(itemId);
			CampaignItemDefinition item = CampaignItemRegistry.GetItem(itemId);
			if (!string.IsNullOrWhiteSpace(item?.CodexEntryId))
			{
				UnlockCodexEntries(new[] { item.CodexEntryId });
			}
		}

		OfficerMissionLoadoutService.ApplyCampaignItemUnlocks(officer);
	}
}
