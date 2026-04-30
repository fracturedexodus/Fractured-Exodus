using Godot;

public static class IsoGridHelper
{
	public static Vector2 GridToWorld(int column, int row, Vector2 tileStep, Vector2 origin)
	{
		float halfWidth = tileStep.X * 0.5f;
		float halfHeight = tileStep.Y * 0.5f;
		return origin + new Vector2((column - row) * halfWidth, (column + row) * halfHeight);
	}
}
