using Godot;
using System.Collections.Generic;

// ==========================================
// DATA CONTAINERS
// ==========================================
public class PlanetData 
{
	public string Name { get; set; }
	
	// Updated properties to match your new SystemView generation!
	public int TypeIndex { get; set; }
	public float Distance { get; set; }
	public float Speed { get; set; }
	public string Habitability { get; set; }
	public string Resources { get; set; }
	public Vector2 Position { get; set; } 
	
	// --- NEW: VISUAL PERSISTENCE PROPERTIES ---
	public float Scale { get; set; } 
	public float StartingAngle { get; set; } 
}

public class SystemData 
{
	public string SystemName { get; set; }
	public Vector2 StarPosition { get; set; }
	public List<PlanetData> Planets { get; set; } = new List<PlanetData>();
}

public class StarMapData
{
	public string SystemName { get; set; }
	public Vector2 MapPosition { get; set; }
	public int PlanetCount { get; set; }
	public float StarScale { get; set; }
	public Color StarColor { get; set; }
}

// ==========================================
// THE SINGLETON
// ==========================================
public partial class GlobalData : Node
{
	// --- 1. Current UI Selection ---
	public string SavedSystem { get; set; } = "";
	public string SavedPlanet { get; set; } = "";
	public string SavedType { get; set; } = "";

	// --- 2. System Memory ---
	// This dictionary remembers every system the player visits.
	public Dictionary<string, SystemData> ExploredSystems { get; set; } = new Dictionary<string, SystemData>();

	// --- 3. Galactic Map State ---
	public List<StarMapData> CurrentSectorStars { get; set; } = new List<StarMapData>();

	public override void _Ready()
	{
		GD.Print("GlobalData Singleton Initialized successfully.");
	}
}
