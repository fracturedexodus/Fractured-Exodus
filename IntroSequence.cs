using Godot;
using System.Threading.Tasks;

public partial class IntroSequence : Control
{
	private const string MainMenuScenePath = "res://main_menu.tscn";
	private const string LogoPath = "res://Assets/Branding/LarsMoonGamingLogo.png";
	private const string VideoPath = "res://Assets/Intro/gameintro.ogv";
	private const double LogoHoldSeconds = 5.0;
	private const float CrossfadeSeconds = 1.0f;

	private TextureRect _logoRect;
	private VideoStreamPlayer _videoPlayer;
	private bool _videoStarted;
	private bool _leavingScene;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildBackground();
		BuildVideoPlayer();
		BuildLogo();
		_ = RunIntroSequenceAsync();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_leavingScene || !_videoStarted)
		{
			return;
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			GoToMainMenu();
		}
	}

	private void BuildBackground()
	{
		ColorRect blackBackdrop = new ColorRect
		{
			Color = Colors.Black
		};
		blackBackdrop.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(blackBackdrop);

		Control starLayer = new Control();
		starLayer.SetAnchorsPreset(LayoutPreset.FullRect);
		starLayer.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(starLayer);

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < 220; i++)
		{
			float size = rng.RandfRange(1.5f, 4.5f);
			ColorRect star = new ColorRect
			{
				Color = new Color(0.8f + rng.RandfRange(0.0f, 0.2f), 0.85f + rng.RandfRange(0.0f, 0.15f), 1f, rng.RandfRange(0.35f, 0.95f)),
				CustomMinimumSize = new Vector2(size, size),
				MouseFilter = MouseFilterEnum.Ignore
			};
			star.Position = new Vector2(rng.RandfRange(0f, 1920f), rng.RandfRange(0f, 1080f));
			starLayer.AddChild(star);
		}
	}

	private void BuildVideoPlayer()
	{
		_videoPlayer = new VideoStreamPlayer
		{
			Visible = false,
			Expand = true,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, 0f),
			Stream = GD.Load<VideoStream>(VideoPath)
		};
		_videoPlayer.SetAnchorsPreset(LayoutPreset.FullRect);
		_videoPlayer.Finished += OnVideoFinished;
		AddChild(_videoPlayer);
	}

	private void BuildLogo()
	{
		_logoRect = new TextureRect
		{
			Texture = GD.Load<Texture2D>(LogoPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(900f, 450f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		_logoRect.AnchorLeft = 0.5f;
		_logoRect.AnchorTop = 0.5f;
		_logoRect.AnchorRight = 0.5f;
		_logoRect.AnchorBottom = 0.5f;
		_logoRect.OffsetLeft = -450f;
		_logoRect.OffsetTop = -225f;
		_logoRect.OffsetRight = 450f;
		_logoRect.OffsetBottom = 225f;
		AddChild(_logoRect);
	}

	private async Task RunIntroSequenceAsync()
	{
		await ToSignal(GetTree().CreateTimer(LogoHoldSeconds), SceneTreeTimer.SignalName.Timeout);
		if (_leavingScene)
		{
			return;
		}

		_videoStarted = true;
		_videoPlayer.Visible = true;
		_videoPlayer.Play();

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(_logoRect, "modulate:a", 0f, CrossfadeSeconds);
		tween.TweenProperty(_videoPlayer, "modulate:a", 1f, CrossfadeSeconds);
		await ToSignal(tween, Tween.SignalName.Finished);
		_logoRect.Visible = false;
	}

	private void OnVideoFinished()
	{
		GoToMainMenu();
	}

	private void GoToMainMenu()
	{
		if (_leavingScene)
		{
			return;
		}

		_leavingScene = true;
		_videoPlayer?.Stop();
		SceneTransition transitioner = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transitioner != null)
		{
			transitioner.ChangeScene(MainMenuScenePath);
		}
		else
		{
			GetTree().ChangeSceneToFile(MainMenuScenePath);
		}
	}
}
