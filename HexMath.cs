using Godot;
using System;

public static class HexMath
{
	// The 6 directions to neighboring hexes
	public static readonly Vector2I[] Directions = new Vector2I[] {
		new Vector2I(1, 0), new Vector2I(1, -1), new Vector2I(0, -1), 
		new Vector2I(-1, 0), new Vector2I(-1, 1), new Vector2I(0, 1)
	};

	public static int HexDistance(Vector2I a, Vector2I b) 
	{ 
		return (Mathf.Abs(a.X - b.X) + Mathf.Abs(a.X + a.Y - b.X - b.Y) + Mathf.Abs(a.Y - b.Y)) / 2; 
	}

	public static Vector2 HexToPixel(Vector2I hex, float hexSize)
	{
		float x = hexSize * Mathf.Sqrt(3) * (hex.X + hex.Y / 2f);
		float y = hexSize * 3f / 2f * hex.Y;
		return new Vector2(x, y);
	}

	public static Vector2I PixelToHex(Vector2 pt, float hexSize)
	{
		float q = (Mathf.Sqrt(3f) / 3f * pt.X - 1f / 3f * pt.Y) / hexSize;
		float r = (2f / 3f * pt.Y) / hexSize;
		return AxialRound(q, r);
	}

	public static Vector2I AxialRound(float q, float r)
	{
		float s = -q - r;
		int rq = Mathf.RoundToInt(q);
		int rr = Mathf.RoundToInt(r);
		int rs = Mathf.RoundToInt(s);
		float qDiff = Mathf.Abs(rq - q);
		float rDiff = Mathf.Abs(rr - r);
		float sDiff = Mathf.Abs(rs - s);
		if (qDiff > rDiff && qDiff > sDiff) rq = -rr - rs;
		else if (rDiff > sDiff) rr = -rq - rs;
		return new Vector2I(rq, rr);
	}
}
