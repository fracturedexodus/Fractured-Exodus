public sealed class MissionAttackProfile
{
	public string WeaponId { get; init; } = string.Empty;
	public string WeaponName { get; init; } = "Unarmed";
	public bool IsMelee { get; init; }
	public int Range { get; init; } = 1;
	public int MinDamage { get; init; } = 1;
	public int MaxDamage { get; init; } = 1;
	public int BonusShieldDamage { get; init; }
	public int ShieldPiercingDamage { get; init; }
	public string StatusEffectId { get; init; } = string.Empty;
	public float StatusEffectChance { get; init; }
}
