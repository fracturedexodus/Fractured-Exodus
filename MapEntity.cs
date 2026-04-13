using Godot;

public class MapEntity
{
	public string Name;
	public string Type;
	public string Details;
	
	public int MaxActions;
	public int CurrentActions; 
	
	public int AttackRange;
	public int AttackDamage;
	
	public int MaxHP;
	public int CurrentHP;
	public int MaxShields;
	public int CurrentShields;

	public int InitiativeBonus;
	public int CurrentInitiativeRoll;
	public bool IsDead = false;

	public Sprite2D VisualSprite; 
	public float BaseRotationOffset; 
}
