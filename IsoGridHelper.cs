using Godot;

public static class IsoGridHelper
{
	public static Vector2 GridToWorld(int column, int row, Vector2 tileStep, Vector2 origin)
	{
		float halfWidth = tileStep.X * 0.5f;
		float halfHeight = tileStep.Y * 0.5f;
		return origin + new Vector2((column - row) * halfWidth, (column + row) * halfHeight);
	}

	public static Vector2I WorldToGrid(Vector2 worldPosition, Vector2 tileStep, Vector2 origin)
	{
		Vector2 local = worldPosition - origin;
		float halfWidth = tileStep.X * 0.5f;
		float halfHeight = tileStep.Y * 0.5f;
		float a = local.X / halfWidth;
		float b = local.Y / halfHeight;
		int column = Mathf.RoundToInt((a + b) * 0.5f);
		int row = Mathf.RoundToInt((b - a) * 0.5f);
		return new Vector2I(column, row);
	}
}
