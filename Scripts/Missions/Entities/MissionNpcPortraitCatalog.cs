using Godot;
using System.Collections.Generic;
using System.Linq;

public static class MissionNpcPortraitCatalog
{
	private static readonly string[] PortraitPaths =
	{
		"res://Assets/NPCs/NPC1.png",
		"res://Assets/NPCs/NPC2.png",
		"res://Assets/NPCs/NPC3.png",
		"res://Assets/NPCs/NPC4.png",
		"res://Assets/NPCs/NPC5.png",
		"res://Assets/NPCs/NPC6.png",
		"res://Assets/NPCs/NPC7.png",
		"res://Assets/NPCs/NPC8.png",
		"res://Assets/NPCs/NPC9.png",
		"res://Assets/NPCs/NPC10.png",
		"res://Assets/NPCs/NPC11.png",
		"res://Assets/NPCs/NPC12.png",
		"res://Assets/NPCs/NPC13.png"
	};

	public static IReadOnlyList<string> GetAllPortraitPaths()
	{
		return PortraitPaths;
	}

	public static bool IsNpcPortraitPath(string portraitPath)
	{
		return !string.IsNullOrWhiteSpace(portraitPath)
			&& PortraitPaths.Contains(portraitPath);
	}

	public static string GetNextUnusedPortraitPath(IEnumerable<string> usedPortraitPaths)
	{
		HashSet<string> used = new HashSet<string>(
			(usedPortraitPaths ?? Enumerable.Empty<string>())
				.Where(path => !string.IsNullOrWhiteSpace(path)));
		return PortraitPaths.FirstOrDefault(path => ResourceLoader.Exists(path) && !used.Contains(path)) ?? string.Empty;
	}
}
