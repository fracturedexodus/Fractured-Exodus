using Godot;
using System.Collections.Generic;

public class AttackButtonState
{
	public bool IsVisible { get; set; }
	public bool ResetTargeting { get; set; }
	public string ButtonText { get; set; } = "ATTACK";
}

public class BattleActionButtonPresenterService
{
	public AttackButtonState BuildAttackButtonState(
		List<Vector2I> selectedHexes,
		Dictionary<Vector2I, MapEntity> hexContents,
		bool inCombat,
		MapEntity activeShip)
	{
		if (selectedHexes.Count == 1 && hexContents.ContainsKey(selectedHexes[0]))
		{
			MapEntity singleShip = hexContents[selectedHexes[0]];
			if (singleShip.Type == GameConstants.EntityTypes.PlayerFleet &&
				singleShip.CurrentActions > 0 &&
				(!inCombat || singleShip == activeShip))
			{
				return new AttackButtonState
				{
					IsVisible = true,
					ResetTargeting = false,
					ButtonText = "ATTACK"
				};
			}
		}

		return new AttackButtonState
		{
			IsVisible = false,
			ResetTargeting = true,
			ButtonText = "ATTACK"
		};
	}

	public void ApplyJumpButtonState(BattleUI ui, JumpButtonState state)
	{
		if (ui?.JumpButton == null || state == null) return;
		ui.JumpButton.Text = state.ButtonText;
		ui.JumpButton.Visible = state.IsVisible;
	}

	public void ApplyAttackButtonState(BattleUI ui, CombatManager combat, AttackButtonState state)
	{
		if (ui?.AttackButton == null || state == null) return;

		ui.AttackButton.Visible = state.IsVisible;
		ui.AttackButton.Text = state.ButtonText;

		if (state.ResetTargeting && combat != null)
		{
			combat.IsTargeting = false;
		}
	}
}
