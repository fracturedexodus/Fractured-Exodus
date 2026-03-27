using Godot;
using System;

public partial class MainMenu : Control
{
	// This is the method connected to your Launch Fleet button
	public void _on_launch_fleet_pressed()
	{
		// --- NEW: Wipe the old game data before starting! ---
		GlobalData globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		if (globalData != null)
		{
			globalData.ResetForNewGame();
		}

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

	// ==========================================
	// LOAD GAME LOGIC
	// ==========================================
	public void _on_load_game_pressed()
	{
		// 1. Grab the GlobalData suitcase
		GlobalData globalData = GetNodeOrNull<GlobalData>("/root/GlobalData");
		
		if (globalData != null)
		{
			// 2. Tell it to load the file. It returns 'true' if successful, 'false' if no save exists!
			bool loadSuccessful = globalData.LoadGame();

			if (loadSuccessful)
			{
				GD.Print("Save loaded successfully! Warping to Battle Map...");
				
				// 3. Jump straight into the action using your custom SceneTransition fader!
				SceneTransition transitioner = GetNode<SceneTransition>("/root/SceneTransition");
				transitioner.ChangeScene("res://exploration_battle.tscn");
			}
			else
			{
				GD.Print("No save file found.");
				
				// 4. Optional: Visual feedback if they click Load but have no save file.
				// NOTE: If your button is inside a container (like a VBoxContainer), update the path below!
				// Example: GetNodeOrNull<Button>("VBoxContainer/LoadGameButton");
				Button loadButton = GetNodeOrNull<Button>("LoadGameButton");
				
				if (loadButton != null)
				{
					loadButton.Text = "NO SAVE FOUND";
					loadButton.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f)); // Flash red

					// Reset the button text after 2 seconds
					GetTree().CreateTimer(2.0f).Timeout += () => 
					{
						loadButton.Text = "LOAD GAME";
						loadButton.RemoveThemeColorOverride("font_color");
					};
				}
			}
		}
	}
}
