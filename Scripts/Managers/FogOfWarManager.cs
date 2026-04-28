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
				points[i] = new Vector2(hexSize * 1.05f * Mathf.Cos(angle_rad), hexSize * 1.05f * Mathf.Sin(angle_rad));
			}
			poly.Polygon = points;
			poly.Color = new Color(0.05f, 0.05f, 0.1f, 0.98f); 
			poly.Position = HexMath.HexToPixel(hex, hexSize);
			fogLayer.AddChild(poly);
			_fogTiles[hex] = poly;
		}
	}

	public HashSet<Vector2I> GetExploredHexes() => _exploredHexes;

	public void SetExploredHexes(List<Vector2I> savedHexes)
	{
		if (savedHexes == null) return;
		foreach (var hex in savedHexes) _exploredHexes.Add(hex);
	}

	public void UpdateVisibility()
	{
		_currentlyVisible.Clear();
		List<Vector2I> playerPositions = new List<Vector2I>();

		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet && GodotObject.IsInstanceValid(kvp.Value.VisualSprite))
			{
				Vector2I visualHex = HexMath.PixelToHex(kvp.Value.VisualSprite.Position, _map.HexSize);
				playerPositions.Add(visualHex);
			}
		}

		int visionRange = _map.ScanningRange;
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

		// --- NEW: Inject Radar Scan Vision ---
		SystemData sys = null;
		if (_map._globalData != null && !string.IsNullOrEmpty(_map._globalData.SavedSystem) && _map._globalData.ExploredSystems.ContainsKey(_map._globalData.SavedSystem))
		{
			sys = _map._globalData.ExploredSystems[_map._globalData.SavedSystem];
			foreach (Vector2I radarHex in sys.RadarRevealedHexes)
			{
				_currentlyVisible.Add(radarHex); // Acts like a camera!
				_exploredHexes.Add(radarHex);
			}
		}

		Color exploredColor = new Color(0.05f, 0.05f, 0.1f, 0.25f);
		Color unexploredColor = new Color(0.05f, 0.05f, 0.1f, 0.98f);

		foreach (var kvp in _fogTiles)
		{
			if (_currentlyVisible.Contains(kvp.Key))
			{
				if (kvp.Value.Visible) kvp.Value.Visible = false; 
			}
			else if (_exploredHexes.Contains(kvp.Key)) 
			{ 
				if (!kvp.Value.Visible) kvp.Value.Visible = true; 
				if (kvp.Value.Color != exploredColor) kvp.Value.Color = exploredColor; 
			}
			else 
			{ 
				if (!kvp.Value.Visible) kvp.Value.Visible = true; 
				if (kvp.Value.Color != unexploredColor) kvp.Value.Color = unexploredColor; 
			}
		}
		UpdateEntityVisibility();
	}

	private void UpdateEntityVisibility()
	{
		foreach (var kvp in _map.HexContents)
		{
			if (kvp.Value.Type != GameConstants.EntityTypes.PlayerFleet && GodotObject.IsInstanceValid(kvp.Value.VisualSprite))
			{
				bool shouldBeVisible = kvp.Value.Type == GameConstants.EntityTypes.EnemyFleet ? _currentlyVisible.Contains(kvp.Key) : _exploredHexes.Contains(kvp.Key);
				if (kvp.Value.VisualSprite.Visible != shouldBeVisible) kvp.Value.VisualSprite.Visible = shouldBeVisible;
			}
		}
		
		foreach (Node child in _map.EnvironmentLayer.GetChildren()) 
		{
			if (child is Polygon2D rock) 
			{
				bool vis = _exploredHexes.Contains(HexMath.PixelToHex(rock.GlobalPosition, _map.HexSize));
				if (rock.Visible != vis) rock.Visible = vis;
			}
		}
		
		foreach (Node child in _map.RadiationLayer.GetChildren()) 
		{
			if (child is Polygon2D rad) 
			{
				bool vis = _exploredHexes.Contains(HexMath.PixelToHex(rad.Position, _map.HexSize));
				if (rad.Visible != vis) rad.Visible = vis;
			}
		}
	}
}
