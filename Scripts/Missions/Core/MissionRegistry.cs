using Godot;
using System;
using System.Collections.Generic;

public class MissionRegistry
{
	private readonly string _templateDirectoryPath;

	public MissionRegistry(string templateDirectoryPath = "res://Data/Missions/Templates")
	{
		_templateDirectoryPath = templateDirectoryPath;
	}

	public List<MissionTemplate> LoadTemplates()
	{
		var templates = new List<MissionTemplate>();
		var resourcePaths = new List<string>();
		CollectTemplatePaths(_templateDirectoryPath, resourcePaths);

		foreach (string resourcePath in resourcePaths)
		{
			Resource resource = ResourceLoader.Load(resourcePath);
			if (resource is MissionTemplate template)
			{
				templates.Add(template);
				continue;
			}

			GD.PrintErr($"MissionRegistry skipped non-mission resource at {resourcePath}.");
		}

		return templates;
	}

	private void CollectTemplatePaths(string directoryPath, List<string> resourcePaths)
	{
		DirAccess dir = DirAccess.Open(directoryPath);
		if (dir == null)
		{
			return;
		}

		dir.ListDirBegin();
		while (true)
		{
			string entryName = dir.GetNext();
			if (string.IsNullOrEmpty(entryName))
			{
				break;
			}

			if (entryName == "." || entryName == "..")
			{
				continue;
			}

			string entryPath = $"{directoryPath.TrimEnd('/')}/{entryName}";
			if (dir.CurrentIsDir())
			{
				CollectTemplatePaths(entryPath, resourcePaths);
				continue;
			}

			if (entryName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)
				|| entryName.EndsWith(".res", StringComparison.OrdinalIgnoreCase))
			{
				resourcePaths.Add(entryPath);
			}
		}

		dir.ListDirEnd();
	}
}
