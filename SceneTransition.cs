using Godot;
using System;

public partial class SceneTransition : CanvasLayer
{
	private AnimationPlayer _animPlayer;
	private bool _isTransitioning;

	public override void _Ready()
	{
		_animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
	}

	// Notice the 'async' keyword! This allows the script to pause and wait for the animation.
	public async void ChangeScene(string targetScenePath)
	{
		if (_isTransitioning || string.IsNullOrWhiteSpace(targetScenePath))
		{
			return;
		}

		_isTransitioning = true;

		try
		{
			// 1. Play the fade to black animation
			_animPlayer.Play("fade");
			
			// 2. Wait exactly until the animation is completely finished
			await ToSignal(_animPlayer, AnimationPlayer.SignalName.AnimationFinished);

			// 3. Swap the scene in the background
			GetTree().ChangeSceneToFile(targetScenePath);

			// 4. Play the fade animation in reverse to reveal the new scene!
			_animPlayer.PlayBackwards("fade");
		}
		finally
		{
			_isTransitioning = false;
		}
	}
}
