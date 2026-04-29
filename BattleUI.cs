using Godot;
using System;

public partial class BattleUI : CanvasLayer
{
	[ExportGroup("Main HUD")]
	[Export] public Label TurnLabel;
	[Export] public Label InventoryDisplay;
	[Export] public Button EndTurnButton;
	[Export] public Button SaveGameButton;
	[Export] public Button RepairFleetButton;
	[Export] public Button InventoryButton;
	[Export] public Button MainMenuButton;
	
	[ExportGroup("Combat Controls")]
	[Export] public Button AttackButton;
	[Export] public Button MissileButton;
	[Export] public Button JumpButton;
	[Export] public HBoxContainer InitiativeUI;
	
	[ExportGroup("Information Panels")]
	[Export] public PanelContainer InfoPanel;
	[Export] public Label InfoLabel;
	[Export] public PanelContainer CombatLogPanel;
	[Export] public RichTextLabel CombatLogText;
	[Export] public ColorRect GameOverPanel;
	[Export] public Label GameOverLabel;
	[Export] public Button GameOverReturnButton;

	[ExportGroup("Ship Terminal")]
	[Export] public PanelContainer ShipMenuPanel;
	[Export] public Label ShipMenuTitle;
	[Export] public TextureRect ShipImageDisplay;
	[Export] public ProgressBar HpBar;
	[Export] public Label HpLabel;
	[Export] public ProgressBar ShieldBar;
	[Export] public Label ShieldLabel;
	[Export] public Label ShipMenuDetails;
	
	[ExportSubgroup("Terminal Buttons")]
	[Export] public Label WeaponNameLabel;
	[Export] public Label HullNameLabel;
	[Export] public Label ShieldNameLabel;
	[Export] public Label MissileNameLabel;
	[Export] public Button BtnRepair;
	[Export] public Button BtnScan;
	[Export] public Button BtnSalvage;
	[Export] public Button BtnLongRange;
	[Export] public Button CodexButton;
	[Export] public Button CloseMenuButton;
}
