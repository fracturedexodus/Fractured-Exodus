public class CampaignItemDefinition
{
	public string ItemId { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public bool Stackable { get; set; } = true;
	public string CodexEntryId { get; set; } = string.Empty;
	public string MissionWeaponId { get; set; } = string.Empty;
	public string MissionShieldId { get; set; } = string.Empty;
	public bool ConsumeOnUnlock { get; set; }
}
