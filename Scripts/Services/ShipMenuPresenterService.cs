using Godot;

public class ShipMenuPresenterService
{
	public void ResetMenu(BattleUI ui)
	{
		if (ui == null) return;

		if (ui.BtnLongRange != null) ui.BtnLongRange.Visible = false;
		if (ui.BtnScan != null) ui.BtnScan.Visible = false;
		if (ui.BtnSalvage != null) ui.BtnSalvage.Visible = false;
		if (ui.BtnRepair != null) ui.BtnRepair.Visible = false;
		if (ui.CodexButton != null) ui.CodexButton.Visible = false;
		if (ui.WeaponNameLabel != null) ui.WeaponNameLabel.Visible = false;
		if (ui.HullNameLabel != null) ui.HullNameLabel.Visible = false;
		if (ui.ShieldNameLabel != null) ui.ShieldNameLabel.Visible = false;
		if (ui.MissileNameLabel != null) ui.MissileNameLabel.Visible = false;
	}

	public void ApplyMenuState(BattleUI ui, MapEntity ship, ShipMenuState menuState, string weaponName, string hullName, string shieldName, string missileName)
	{
		if (ui == null || ship == null || menuState == null) return;

		ui.ShipMenuTitle.Text = menuState.Title;

		if (!string.IsNullOrEmpty(menuState.ImagePath))
		{
			Texture2D texture = GD.Load<Texture2D>(menuState.ImagePath);
			if (texture != null)
			{
				ui.ShipImageDisplay.Texture = texture;
			}
		}

		ui.ShipImageDisplay.Modulate = new Color(1f, menuState.HpPercent, menuState.HpPercent);
		ui.HpBar.MaxValue = ship.MaxHP;
		ui.HpBar.Value = ship.CurrentHP;
		ui.HpLabel.Text = $"HULL INTEGRITY: {ship.CurrentHP}/{ship.MaxHP}";
		ui.ShieldBar.MaxValue = ship.MaxShields;
		ui.ShieldBar.Value = ship.CurrentShields;
		ui.ShieldLabel.Text = $"SHIELD CAPACITORS: {ship.CurrentShields}/{ship.MaxShields}";
		ui.ShipMenuDetails.Text = menuState.DetailsText;

		ui.WeaponNameLabel.Text = $"WEAPON: {weaponName}";
		ui.HullNameLabel.Text = $"HULL: {hullName}";
		ui.ShieldNameLabel.Text = $"SHIELD: {shieldName}";
		ui.MissileNameLabel.Text = $"MISSILE: {missileName}";
		ui.WeaponNameLabel.Visible = true;
		ui.HullNameLabel.Visible = true;
		ui.ShieldNameLabel.Visible = true;
		ui.MissileNameLabel.Visible = true;

		ui.BtnRepair.Visible = menuState.IsPlayerShip;
		ui.BtnRepair.Disabled = !menuState.CanRepair;
		ui.CodexButton.Visible = menuState.IsPlayerShip;

		if (menuState.ShowLongRange && ui.BtnLongRange != null)
		{
			ui.BtnLongRange.Visible = true;
			ui.BtnLongRange.Disabled = menuState.DisableLongRange;
		}

		if (menuState.ShowScan && ui.BtnScan != null)
		{
			ui.BtnScan.Visible = true;
			ui.BtnScan.Disabled = menuState.DisableScan;
			ui.BtnScan.Text = menuState.ScanText;
		}

		if (menuState.ShowSalvage && ui.BtnSalvage != null)
		{
			ui.BtnSalvage.Visible = true;
			ui.BtnSalvage.Disabled = menuState.DisableSalvage;
			ui.BtnSalvage.Text = menuState.SalvageText;
		}
	}
}
