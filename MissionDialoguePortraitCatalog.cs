using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed class MissionDialoguePortraitDefinition
{
	public string DisplayName { get; }
	public string TexturePath { get; }

	public MissionDialoguePortraitDefinition(string displayName, string texturePath)
	{
		DisplayName = displayName;
		TexturePath = texturePath;
	}
}

public static class MissionDialoguePortraitCatalog
{
	private static readonly string[] SearchRoots =
	{
		"res://Assets/Officers",
		"res://Assets/NPCs",
		"res://Assets/DialoguePortraits"
	};

	private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

	private static readonly List<MissionDialoguePortraitDefinition> Definitions = BuildDefinitions();

	public static IReadOnlyList<MissionDialoguePortraitDefinition> All => Definitions;

	public static string GetDisplayName(string texturePath)
	{
		if (string.IsNullOrEmpty(texturePath))
		{
			return "None";
		}

		MissionDialoguePortraitDefinition definition = Definitions.FirstOrDefault(item => item.TexturePath == texturePath);
		return definition?.DisplayName ?? HumanizeName(Path.GetFileNameWithoutExtension(texturePath));
	}

	private static List<MissionDialoguePortraitDefinition> BuildDefinitions()
	{
		List<MissionDialoguePortraitDefinition> definitions = new List<MissionDialoguePortraitDefinition>
		{
			new MissionDialoguePortraitDefinition("None", string.Empty)
		};

		HashSet<string> seenPaths = new HashSet<string>();
		foreach (string root in SearchRoots)
		{
			string absoluteRoot = ProjectSettings.GlobalizePath(root);
			if (!Directory.Exists(absoluteRoot))
			{
				continue;
			}

			foreach (string filePath in Directory.GetFiles(absoluteRoot, "*.*", SearchOption.AllDirectories))
			{
				string extension = Path.GetExtension(filePath);
				if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension.ToLowerInvariant()))
				{
					continue;
				}

				string resourcePath = ProjectSettings.LocalizePath(filePath).Replace("\\", "/");
				if (!seenPaths.Add(resourcePath))
				{
					continue;
				}

				definitions.Add(new MissionDialoguePortraitDefinition(
					HumanizeName(Path.GetFileNameWithoutExtension(resourcePath)),
					resourcePath));
			}
		}

		return definitions
			.OrderBy(def => string.IsNullOrEmpty(def.TexturePath) ? string.Empty : def.DisplayName)
			.ToList();
	}

	private static string HumanizeName(string rawName)
	{
		if (string.IsNullOrEmpty(rawName))
		{
			return "Portrait";
		}

		string spaced = rawName.Replace("_", " ");
		List<char> chars = new List<char>(spaced.Length + 8);
		for (int i = 0; i < spaced.Length; i++)
		{
			char current = spaced[i];
			if (i > 0 && char.IsUpper(current) && char.IsLetterOrDigit(spaced[i - 1]) && spaced[i - 1] != ' ')
			{
				chars.Add(' ');
			}
			chars.Add(current);
		}

		string[] words = new string(chars.ToArray())
			.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < words.Length; i++)
		{
			string lower = words[i].ToLowerInvariant();
			words[i] = char.ToUpperInvariant(lower[0]) + lower.Substring(1);
		}

		return string.Join(" ", words);
	}
}
