public sealed class CombatDamageResult
{
	public int IncomingDamage { get; init; }
	public int ShieldDamage { get; init; }
	public int HealthDamage { get; init; }
	public int RemainingShields { get; init; }
	public int RemainingHealth { get; init; }
	public bool WasFatal { get; init; }
}
