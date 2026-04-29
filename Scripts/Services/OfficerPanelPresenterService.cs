using Godot;

public class OfficerPanelPresenterService
{
	public void Reset(Label titleLabel, TextureRect portraitRect, Label detailsLabel)
	{
		if (titleLabel != null) titleLabel.Text = string.Empty;
		if (portraitRect != null) portraitRect.Texture = null;
		if (detailsLabel != null) detailsLabel.Text = string.Empty;
	}

	public bool Apply(GlobalData globalData, MapEntity ship, Label titleLabel, TextureRect portraitRect, Label detailsLabel)
	{
		if (globalData == null || ship == null || titleLabel == null || portraitRect == null || detailsLabel == null)
		{
			return false;
		}

		if (ship.Type != GameConstants.EntityTypes.PlayerFleet)
		{
			return false;
		}

		if (globalData.ShipOfficers == null || !globalData.ShipOfficers.TryGetValue(ship.Name, out OfficerState officer) || officer == null)
		{
			return false;
		}

		titleLabel.Text = $"== {officer.DisplayName.ToUpper()} ==";

		if (!string.IsNullOrEmpty(officer.PortraitPath))
		{
			portraitRect.Texture = GD.Load<Texture2D>(officer.PortraitPath);
		}
		else
		{
			portraitRect.Texture = null;
		}

		string officerType = officer.IsCustom ? "Custom Officer" : "Preset Officer";
		string biography = string.IsNullOrEmpty(officer.Biography) ? "No biography available." : officer.Biography;

		detailsLabel.Text =
			$"Assigned Ship: {ship.Name}\n" +
			$"Type: {officerType}\n" +
			$"Archetype: {officer.Archetype}\n" +
			$"Ideology: {officer.Ideology}\n" +
			$"Specialty: {officer.Specialty}\n" +
			$"Flaw: {officer.Flaw}\n" +
			$"Approval: {officer.Approval}\n" +
			$"Stress: {officer.Stress}\n\n" +
			$"{biography}";

		return true;
	}
}
