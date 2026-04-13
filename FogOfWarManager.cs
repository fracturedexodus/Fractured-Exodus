using Godot;
using System.Collections.Generic;

public class FogOfWarManager
{
	private BattleMap _map;
	private Dictionary<Vector2I, Polygon2D> _fogTiles = new Dictionary<Vector2I, Polygon2D>();
	private HashSet<Vector2I> _exploredHexes = new HashSet<Vector2I>();
	private HashSet<Vector2I> _currentlyVisible = new HashSet<Vector2I>();

	public FogOfWarManager(BattleMap map)
	{
		_map = map;
	}

	public void GenerateFog(Node2D fogLayer, IEnumerable<Vector2I> mapHexes, float hexSize)
	{
		foreach (Vector2I hex in mapHexes)
		{
			Polygon2D poly = new Polygon2D();
			Vector2[] points = new Vector2[6];
			for (int i = 0; i < 6; i++)
			{
				float angle_deg = 60 * i - 30;
				float angle_rad = Mathf.DegToRad(angle_deg);
				// Make fog polygon slightly larger to overlap perfectly and prevent visual seams
				points[i] = new Vector2(hexSize * 1.05f * Mathf.Cos(angle_rad), hexSize * 1.05f * Mathf.Sin(angle_rad));
			}
			poly.Polygon = points;
			poly.Color = new Color(0.05f, 0.05f, 0.1f, 0.98f); // Deep space dark blue/black
			poly.Position = HexMath.HexToPixel(hex, hexSize);
			fogLayer.AddChild(poly);
			_fogTiles[hex] = poly;
		}
	}

	// --- NEW: Save/Load Methods ---
	public HashSet<Vector2I> GetExploredHexes() => _exploredHexes;

	public void SetExploredHexes(List<Vector2I> savedHexes)
	{
		if (savedHexes == null) return;
		foreach (var hex in savedHexes)
		{
			_exploredHexes.Add(hex);
		}
	}

	public void UpdateVisibility()
	{
		_currentlyVisible.Clear();
		List<Vector2I> playerPositions = new List<Vector2I>();

		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type == "Player Fleet") playerPositions.Add(kvp.Key);
		}

		int visionRange = _map.ScanningRange;

		// 1. Calculate currently visible hexes based on player ship positions
		foreach (Vector2I pPos in playerPositions)
		{
			for (int q = -visionRange; q <= visionRange; q++)
			{
				int r1 = Mathf.Max(-visionRange, -q - visionRange);
				int r2 = Mathf.Min(visionRange, -q + visionRange);
				for (int r = r1; r <= r2; r++)
				{
					Vector2I visibleHex = new Vector2I(pPos.X + q, pPos.Y + r);
					_currentlyVisible.Add(visibleHex);
					_exploredHexes.Add(visibleHex);
				}
			}
		}

		// 2. Update visual fog tiles
		foreach (var kvp in _fogTiles)
		{
			if (_currentlyVisible.Contains(kvp.Key))
			{
				kvp.Value.Visible = false; // Completely clear when looking directly at it
			}
			else if (_exploredHexes.Contains(kvp.Key))
			{
				kvp.Value.Visible = true;
				// Brighter "remembered" area
				kvp.Value.Color = new Color(0.05f, 0.05f, 0.1f, 0.25f); 
			}
			else
			{
				kvp.Value.Visible = true;
				kvp.Value.Color = new Color(0.05f, 0.05f, 0.1f, 0.98f); // Pitch black for unexplored
			}
		}

		// 3. Apply visibility rules to entities and hazards
		UpdateEntityVisibility();
	}

	private void UpdateEntityVisibility()
	{
		// Hide/Show Ships and Planets
		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type != "Player Fleet" && GodotObject.IsInstanceValid(kvp.Value.VisualSprite))
			{
				if (kvp.Value.Type == "Enemy Fleet")
				{
					// Enemies are only visible if CURRENTLY in vision
					kvp.Value.VisualSprite.Visible = _currentlyVisible.Contains(kvp.Key);
				}
				else
				{
					// Planets, Gates, and Stars stay visible once EXPLORED
					kvp.Value.VisualSprite.Visible = _exploredHexes.Contains(kvp.Key);
				}
			}
		}

		// Hide/Show Asteroids
		foreach (Node child in _map.EnvironmentLayer.GetChildren())
		{
			if (child is Polygon2D rock)
			{
				Vector2I hex = HexMath.PixelToHex(rock.GlobalPosition, _map.HexSize);
				rock.Visible = _exploredHexes.Contains(hex);
			}
		}

		// Hide/Show Radiation Clouds
		foreach (Node child in _map.RadiationLayer.GetChildren())
		{
			if (child is Polygon2D rad)
			{
				// Use GlobalPosition or Position depending on how your layers are structured
				Vector2I hex = HexMath.PixelToHex(rad.GlobalPosition, _map.HexSize);
				rad.Visible = _exploredHexes.Contains(hex); 
			}
		}
	}
}
