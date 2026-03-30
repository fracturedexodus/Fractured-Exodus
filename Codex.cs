using Godot;
using System;

public partial class Codex : Control
{
	public override void _Ready()
	{
		GD.Print("Codex system online.");

		// 1. Find the button in your scene tree
		Button returnBtn = GetNodeOrNull<Button>("ReturnButton");

		// 2. Wire the button's "Pressed" signal to our method below
		if (returnBtn != null)
		{
			returnBtn.Pressed += OnReturnPressed;
			GD.Print("Return button successfully connected!");
		}
		else
		{
			GD.PrintErr("Could not find the ReturnButton! Check the exact name and path in your Scene Tree.");
		}
	}

	// 3. This is the method the compiler was looking for!
	private void OnReturnPressed()
	{
		GD.Print("Exiting Codex, returning to Map...");
		
		// Grab the GlobalData singleton
		GlobalData data = GetNodeOrNull<GlobalData>("/root/GlobalData");
		
		// Tell it to load the exact state of the map we saved right before opening the Codex
		if (data != null)
		{
			data.LoadGame();
		}
		
		// Load the BattleMap back onto the screen
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null) 
		{
			transitioner.ChangeScene("res://exploration_battle.tscn");
		}
		else 
		{
			GetTree().ChangeSceneToFile("res://exploration_battle.tscn");
		}
	}
}
