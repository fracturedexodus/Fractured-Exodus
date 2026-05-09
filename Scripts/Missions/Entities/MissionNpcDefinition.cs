using Godot;

[GlobalClass]
public partial class MissionNpcDefinition : Resource
{
	[Export] public string NpcId { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = string.Empty;
	[Export(PropertyHint.File, "*.png,*.jpg,*.jpeg,*.webp,*.svg")] public string PortraitPath { get; set; } = string.Empty;
	[Export(PropertyHint.File, "*.png,*.jpg,*.jpeg,*.webp,*.svg")] public string SpriteTexturePath { get; set; } = string.Empty;
	[Export(PropertyHint.Range, "0.05,3.0,0.01")] public float VisualScaleMultiplier { get; set; } = 0.11f;
	[Export] public Color AccentColor { get; set; } = new Color(0.72f, 0.86f, 0.92f, 1f);
	[Export] public bool ShowNameLabel { get; set; } = true;
	[Export] public bool IsHostile { get; set; } = false;
	[Export] public int InteractionRange { get; set; } = 1;
	[Export] public bool OneShot { get; set; } = false;
	[Export] public int MaxHP { get; set; } = 10;
	[Export] public int MaxActions { get; set; } = 2;
	[Export] public int AttackRange { get; set; } = 1;
	[Export] public int AttackDamage { get; set; } = 3;
	[Export] public int InitiativeBonus { get; set; } = 0;
	[Export] public string WeaponName { get; set; } = "Claws";
	[Export] public Godot.Collections.Array<string> RequiredFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export] public Godot.Collections.Array<string> SetFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export] public string SuccessMessage { get; set; } = string.Empty;
	[Export] public string DefaultDialogueId { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string Notes { get; set; } = string.Empty;
}
