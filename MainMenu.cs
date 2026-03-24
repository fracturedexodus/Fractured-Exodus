using Godot;
using System;

public partial class MainMenu : Control
{
	// This is the method connected to your Launch Fleet button
	public void _on_launch_fleet_pressed()
	{
		// 1. Grab the global transitioner node we made
		SceneTransition transitioner = GetNode<SceneTransition>("/root/SceneTransition");
		
		// 2. Tell it to fade into the Galactic Map!
		transitioner.ChangeScene("res://galactic_map.tscn");
	}

	// This method is connected to your Abandon Run/Quit button
	public void _on_abandon_run_pressed()
	{
		// This closes the game completely
		GetTree().Quit();
	}
}
