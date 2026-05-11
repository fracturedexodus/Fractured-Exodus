using Godot;

public partial class MissionDoor2D : Node2D
{
	private Sprite2D _frameSprite;
	private Sprite2D _leftPanel;
	private Sprite2D _rightPanel;
	private Vector2 _closedLeftPosition;
	private Vector2 _closedRightPosition;
	private Vector2 _openLeftPosition;
	private Vector2 _openRightPosition;
	private bool _isConfigured;

	public string DoorId { get; private set; } = string.Empty;
	public Vector2I Cell { get; private set; } = Vector2I.Zero;
	public string OrientationSuffix { get; private set; } = "nw";
	public bool IsOpen { get; private set; }

	public void Configure(Texture2D sourceTexture, Vector2 scale, Vector2 worldPosition, string doorId, Vector2I cell, bool startsOpen)
	{
		DoorId = doorId ?? string.Empty;
		Cell = cell;
		Position = worldPosition;
		OrientationSuffix = ResolveOrientationSuffix(sourceTexture?.ResourcePath ?? string.Empty);
		BuildPanels(sourceTexture, scale);
		_isConfigured = true;
		SetOpen(startsOpen, false);
	}

	public void SetOpen(bool open, bool animated)
	{
		IsOpen = open;
		if (!_isConfigured)
		{
			return;
		}

		Vector2 leftTarget = open ? _openLeftPosition : _closedLeftPosition;
		Vector2 rightTarget = open ? _openRightPosition : _closedRightPosition;

		if (!animated)
		{
			_leftPanel.Position = leftTarget;
			_rightPanel.Position = rightTarget;
			return;
		}

		Tween tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out);
		tween.SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(_leftPanel, "position", leftTarget, 0.26f);
		tween.Parallel().TweenProperty(_rightPanel, "position", rightTarget, 0.26f);
	}

	public void SetVisualModulate(Color color)
	{
		if (_frameSprite != null)
		{
			_frameSprite.Modulate = color;
		}

		if (_leftPanel != null)
		{
			_leftPanel.Modulate = color;
		}

		if (_rightPanel != null)
		{
			_rightPanel.Modulate = color;
		}
	}

	private void BuildPanels(Texture2D sourceTexture, Vector2 scale)
	{
		foreach (Node child in GetChildren())
		{
			child.QueueFree();
		}

		if (TryBuildLayeredDoor(sourceTexture, scale))
		{
			return;
		}

		BuildLegacyPanels(sourceTexture, scale);
	}

	private bool TryBuildLayeredDoor(Texture2D sourceTexture, Vector2 scale)
	{
		string sourcePath = sourceTexture?.ResourcePath ?? string.Empty;
		if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith("_closed.png"))
		{
			return false;
		}

		string framePath = sourcePath.Replace("_closed.png", "_frame.png");
		string leftPath = sourcePath.Replace("_closed.png", "_leaf_left.png");
		string rightPath = sourcePath.Replace("_closed.png", "_leaf_right.png");

		Texture2D frameTexture = GD.Load<Texture2D>(framePath);
		Texture2D leftTexture = GD.Load<Texture2D>(leftPath);
		Texture2D rightTexture = GD.Load<Texture2D>(rightPath);
		if (frameTexture == null || leftTexture == null || rightTexture == null)
		{
			return false;
		}

		_frameSprite = new Sprite2D
		{
			Texture = frameTexture,
			Centered = true,
			Scale = scale
		};
		_leftPanel = new Sprite2D
		{
			Texture = leftTexture,
			Centered = true,
			Scale = scale
		};
		_rightPanel = new Sprite2D
		{
			Texture = rightTexture,
			Centered = true,
			Scale = scale
		};

		AddChild(_frameSprite);
		AddChild(_leftPanel);
		AddChild(_rightPanel);

		_closedLeftPosition = Vector2.Zero;
		_closedRightPosition = Vector2.Zero;

		// Door slide direction should follow the wall face diagonal, not simply north/south.
		// NW and SE share one diagonal; NE and SW share the opposite.
		bool opensRightUp = sourcePath.Contains("_nw_") || sourcePath.Contains("_se_");
		float slideX = 11f * scale.X;
		float slideY = 5f * scale.Y;
		_openLeftPosition = opensRightUp
			? new Vector2(-slideX, slideY)
			: new Vector2(-slideX, -slideY);
		_openRightPosition = opensRightUp
			? new Vector2(slideX, -slideY)
			: new Vector2(slideX, slideY);
		return true;
	}

	private void BuildLegacyPanels(Texture2D sourceTexture, Vector2 scale)
	{
		int fullWidth = (int)sourceTexture.GetWidth();
		int fullHeight = (int)sourceTexture.GetHeight();
		int halfWidth = fullWidth / 2;

		AtlasTexture leftAtlas = new AtlasTexture
		{
			Atlas = sourceTexture,
			Region = new Rect2(0, 0, halfWidth, fullHeight)
		};
		AtlasTexture rightAtlas = new AtlasTexture
		{
			Atlas = sourceTexture,
			Region = new Rect2(halfWidth, 0, fullWidth - halfWidth, fullHeight)
		};

		_leftPanel = new Sprite2D
		{
			Texture = leftAtlas,
			Centered = true,
			Scale = scale
		};
		_rightPanel = new Sprite2D
		{
			Texture = rightAtlas,
			Centered = true,
			Scale = scale
		};

		AddChild(_leftPanel);
		AddChild(_rightPanel);

		float leftCenterX = ((halfWidth * 0.5f) - (fullWidth * 0.5f)) * scale.X;
		float rightCenterX = (((fullWidth - halfWidth) * 0.5f) + halfWidth - (fullWidth * 0.5f)) * scale.X;
		_closedLeftPosition = new Vector2(leftCenterX, 0f);
		_closedRightPosition = new Vector2(rightCenterX, 0f);

		float slideDistance = halfWidth * scale.X * 0.48f;
		_openLeftPosition = _closedLeftPosition + new Vector2(-slideDistance, 0f);
		_openRightPosition = _closedRightPosition + new Vector2(slideDistance, 0f);
	}

	private static string ResolveOrientationSuffix(string sourcePath)
	{
		if (sourcePath.Contains("_ne_"))
		{
			return "ne";
		}

		if (sourcePath.Contains("_se_"))
		{
			return "se";
		}

		if (sourcePath.Contains("_sw_"))
		{
			return "sw";
		}

		return "nw";
	}
}
