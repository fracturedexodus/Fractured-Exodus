using Godot;
using System;

public partial class SystemWindow : Control
{
	// UI References
	private Label _systemNameLabel;
	private Label _planetCountLabel;
	private Button _startSystemButton;

	// Data storage for the transition
	private string _currentSystemName = "";
	private int _currentPlanetCount = 0;

	public override void _Ready()
	{
		_systemNameLabel = GetNode<Label>("SystemName");
		_planetCountLabel = GetNode<Label>("PlanetCount");
		_startSystemButton = GetNode<Button>("StartSystemButton");

		_startSystemButton.Pressed += OnStartSystemButtonPressed;
	}

	public void SetupWindow(string systemName, int planetCount)
	{
		_currentSystemName = systemName;
		_currentPlanetCount = planetCount;

		_systemNameLabel.Text = $"System: {systemName}";
		_planetCountLabel.Text = $"Planets: {planetCount}";
	}

	// --- UPDATED: Added 'async' to allow for the fade timing ---
	private async void OnStartSystemButtonPressed()
	{
		GD.Print($"Transitioning to: {_currentSystemName}");

		// 1. Access the Transitioner Autoload
		var transitioner = GetNode<SceneTransition>("/root/SceneTransition");
		var animPlayer = transitioner.GetNode<AnimationPlayer>("AnimationPlayer");

		// 2. Start the Fade to Black
		animPlayer.Play("fade");
		await ToSignal(animPlayer, AnimationPlayer.SignalName.AnimationFinished);

		// 3. --- EVERYTHING BELOW HAPPENS WHILE THE SCREEN IS BLACK ---

		// Load and Instantiate the new scene
		PackedScene systemViewScene = GD.Load<PackedScene>("res://system_view.tscn");
		if (systemViewScene == null)
		{
			GD.PrintErr("Error: Could not find system_view.tscn!");
			animPlayer.PlayBackwards("fade"); // Fade back so the user isn't stuck in the dark
			return;
		}

		Node newSystemScene = systemViewScene.Instantiate();
		GetTree().Root.AddChild(newSystemScene);
		GetTree().CurrentScene = newSystemScene;

		// Initialize with our saved data
		newSystemScene.Call("InitializeSystem", _currentSystemName, _currentPlanetCount);

		// Clean up the old map
		Node galacticMap = GetTree().Root.GetNodeOrNull("GalacticMap");
		if (galacticMap != null)
		{
			galacticMap.QueueFree();
		}
		else
		{
			this.QueueFree();
		}

		// 4. Fade back in to reveal the new system!
		animPlayer.PlayBackwards("fade");
	}
}
