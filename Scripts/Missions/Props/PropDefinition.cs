using Godot;

[GlobalClass]
public partial class PropDefinition : Resource
{
	[Export] public string PropId { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
	[Export] public PropInteractionType InteractionType { get; set; } = PropInteractionType.Custom;
	[Export(PropertyHint.File, "*.tscn")] public string ScenePath { get; set; } = string.Empty;
	[Export(PropertyHint.File, "*.png,*.jpg,*.jpeg,*.webp,*.svg")] public string SpriteTexturePath { get; set; } = string.Empty;
	[Export(PropertyHint.Range, "0.05,3.0,0.01")] public float VisualScaleMultiplier { get; set; } = PropVisualSizing.DefaultVisualScaleMultiplier;
	[Export] public int InteractionRange { get; set; } = 1;
	[Export] public bool OneShot { get; set; } = false;
	[Export] public bool HideWhenConsumed { get; set; } = true;
	[Export] public string RequiredOfficerSpecialty { get; set; } = string.Empty;
	[Export] public Godot.Collections.Array<string> RequiredFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export] public Godot.Collections.Array<string> BlockedFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export] public Godot.Collections.Array<string> SetFlags { get; set; } = new Godot.Collections.Array<string>();
	[Export] public string DialogueId { get; set; } = string.Empty;
	[Export] public string MissionEventId { get; set; } = string.Empty;
	[Export] public Godot.Collections.Array<string> DoorTargetIds { get; set; } = new Godot.Collections.Array<string>();
	[Export] public string SuccessMessage { get; set; } = string.Empty;
	[Export] public int RewardRawMaterials { get; set; } = 0;
	[Export] public int RewardEnergyCores { get; set; } = 0;
	[Export] public int RewardAncientTech { get; set; } = 0;
	[Export] public Godot.Collections.Array<string> RewardFleetItemIds { get; set; } = new Godot.Collections.Array<string>();
	[Export] public Godot.Collections.Array<string> RewardOfficerItemIds { get; set; } = new Godot.Collections.Array<string>();
	[Export] public Godot.Collections.Array<string> RewardCodexEntryIds { get; set; } = new Godot.Collections.Array<string>();
}
