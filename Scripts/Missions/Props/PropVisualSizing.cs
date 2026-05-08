using Godot;

public static class PropVisualSizing
{
	public const float DefaultVisualScaleMultiplier = 0.60f;
	private const float DefaultTargetMaxDimension = 132f;

	public static Vector2 GetScale(Texture2D texture, float visualScaleMultiplier, float targetMaxDimension = DefaultTargetMaxDimension)
	{
		if (texture == null)
		{
			return Vector2.One;
		}

		Vector2 size = texture.GetSize();
		float maxDimension = Mathf.Max(size.X, size.Y);
		if (maxDimension <= 0f)
		{
			return Vector2.One;
		}

		float scale = maxDimension > targetMaxDimension ? targetMaxDimension / maxDimension : 1f;
		scale *= Mathf.Max(visualScaleMultiplier, 0.01f);
		return new Vector2(scale, scale);
	}
}
