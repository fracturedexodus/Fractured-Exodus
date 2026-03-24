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
		// Based on your scene tree: these are direct children of SystemWindow
		_systemNameLabel = GetNode<Label>("SystemName");
		_planetCountLabel = GetNode<Label>("PlanetCount");
		_startSystemButton = GetNode<Button>("StartSystemButton");

		// Wire up the button in code
		_startSystemButton.Pressed += OnStartSystemButtonPressed;
	}

	// Called by GalacticMap.cs to pass star data into this window
	public void SetupWindow(string systemName, int planetCount)
	{
		_currentSystemName = systemName;
		_currentPlanetCount = planetCount;

		_systemNameLabel.Text = $"System: {systemName}";
		_planetCountLabel.Text = $"Planets: {planetCount}";
	}

	private void OnStartSystemButtonPressed()
	{
		GD.Print($"Transitioning to: {_currentSystemName}");

		// 1. Load the new system view scene
		// Make sure this file name matches exactly in your FileSystem (snake_case)
		PackedScene systemViewScene = GD.Load<PackedScene>("res://system_view.tscn");
		
		if (systemViewScene == null)
		{
			GD.PrintErr("Error: Could not find system_view.tscn. Check your file path!");
			return;
		}

		Node newSystemScene = systemViewScene.Instantiate();

		// 2. Add to root first so it can access the Viewport/Screen size
		GetTree().Root.AddChild(newSystemScene);
		
		// 3. Set it as the current active scene
		GetTree().CurrentScene = newSystemScene;

		// 4. Initialize with our saved data
		newSystemScene.Call("InitializeSystem", _currentSystemName, _currentPlanetCount);

		// 5. THE MAP REMOVER
		// Since SystemWindow is usually a child of GalacticMap, 
		// finding the Map and freeing it will clean up everything.
		Node galacticMap = GetTree().Root.GetNodeOrNull("GalacticMap");
		if (galacticMap != null)
		{
			galacticMap.QueueFree();
		}
		else
		{
			// Fallback: If the map isn't named "GalacticMap", just free the window
			this.QueueFree();
		}
	}
}
