using Godot;

[GlobalClass]
public partial class MissionShieldDefinition : Resource
{
	[Export] public string ShieldId { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
	[Export] public int CapacityBonus { get; set; } = 0;
	[Export] public int RechargePerTurn { get; set; } = 0;
}
