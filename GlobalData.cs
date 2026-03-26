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
	public string Region { get; set; }
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

	// --- NEW: BASE PLANET & FLEET SELECTION ---
	// Stores the specific planet the player clicked on to use as a starting base
	public string SelectedBasePlanetType { get; set; } = ""; 
	
	// Stores the eventual hex coordinate of that planet so the battle map knows where to spawn the fleet
	public Vector2 SelectedBasePlanetHexCoords { get; set; } = new Vector2(0, 0);

	// A list to hold the names/IDs of the ships the player chose in the Fleet Selection screen
	public List<string> SelectedPlayerFleet { get; set; } = new List<string>();

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
