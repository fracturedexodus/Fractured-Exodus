using Godot;

[GlobalClass]
public partial class MissionWeaponDefinition : Resource
{
	[Export] public string WeaponId { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
	[Export] public bool IsMelee { get; set; } = false;
	[Export] public int AttackRange { get; set; } = 1;
	[Export] public int MinDamage { get; set; } = 1;
	[Export] public int MaxDamage { get; set; } = 3;
	[Export] public int BonusShieldDamage { get; set; } = 0;
	[Export] public int ShieldPiercingDamage { get; set; } = 0;
	[Export] public string StatusEffectId { get; set; } = string.Empty;
	[Export(PropertyHint.Range, "0,1,0.01")] public float StatusEffectChance { get; set; } = 0f;
}
