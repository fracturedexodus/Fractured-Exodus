using Godot;
using System.Collections.Generic;

public class HighlightPresenterService
{
	public void RenderHighlights(
		Node2D highlightLayer,
		IEnumerable<Vector2I> selectedHexes,
		bool inCombat,
		Vector2I? activeSelectionHex,
		IEnumerable<Vector2I> reachableHexes,
		float hexSize)
	{
		if (highlightLayer == null) return;

		ClearHighlights(highlightLayer);

		foreach (Vector2I hex in selectedHexes)
		{
			CreateHighlightPolygon(highlightLayer, hex, new Color(1f, 0.8f, 0f, 0.6f), hexSize);
		}

		if (!inCombat || !activeSelectionHex.HasValue) return;

		foreach (Vector2I hex in reachableHexes)
		{
			if (hex == activeSelectionHex.Value) continue;
			CreateHighlightPolygon(highlightLayer, hex, new Color(0f, 1f, 0.3f, 0.4f), hexSize);
		}
	}

	private static void ClearHighlights(Node2D highlightLayer)
	{
		foreach (Node child in highlightLayer.GetChildren())
		{
			child.QueueFree();
		}
	}

	private static void CreateHighlightPolygon(Node2D highlightLayer, Vector2I hexCoord, Color color, float hexSize)
	{
		Polygon2D poly = new Polygon2D();
		Vector2[] points = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float angleDeg = 60 * i - 30;
			float angleRad = Mathf.DegToRad(angleDeg);
			points[i] = new Vector2(hexSize * Mathf.Cos(angleRad), hexSize * Mathf.Sin(angleRad));
		}

		poly.Polygon = points;
		poly.Color = color;
		poly.Position = HexMath.HexToPixel(hexCoord, hexSize);
		highlightLayer.AddChild(poly);
	}
}
