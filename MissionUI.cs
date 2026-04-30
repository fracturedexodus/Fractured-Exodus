using Godot;

public partial class MissionUI : CanvasLayer
{
	public Label TitleLabel { get; private set; }
	public Label ObjectiveLabel { get; private set; }
	public Label SelectedOfficerLabel { get; private set; }
	public Label PromptLabel { get; private set; }
	public Button SaveSurvivorsButton { get; private set; }
	public Button SecureArchiveButton { get; private set; }
	public Button ReturnButton { get; private set; }

	public override void _Ready()
	{
		TitleLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/TitleLabel");
		ObjectiveLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/ObjectiveLabel");
		SelectedOfficerLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/SelectedOfficerLabel");
		PromptLabel = GetNode<Label>("UIRoot/TopLeftPanel/Margin/Content/PromptLabel");
		SaveSurvivorsButton = GetNode<Button>("UIRoot/BottomRightPanel/Margin/ButtonStack/SaveSurvivorsButton");
		SecureArchiveButton = GetNode<Button>("UIRoot/BottomRightPanel/Margin/ButtonStack/SecureArchiveButton");
		ReturnButton = GetNode<Button>("UIRoot/BottomRightPanel/Margin/ButtonStack/ReturnButton");
	}

	public void SetMissionText(string title, string objective, string prompt)
	{
		if (TitleLabel != null) TitleLabel.Text = title;
		if (ObjectiveLabel != null) ObjectiveLabel.Text = objective;
		if (PromptLabel != null) PromptLabel.Text = prompt;
	}

	public void SetSelectedOfficer(string officerName, string shipName, string specialty)
	{
		if (SelectedOfficerLabel == null)
		{
			return;
		}

		SelectedOfficerLabel.Text = $"ACTIVE OFFICER: {officerName}\nSHIP: {shipName}\nSPECIALTY: {specialty}";
	}
}
