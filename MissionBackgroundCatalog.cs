using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class MissionBackgroundDefinition
{
	public string Id { get; }
	public string DisplayName { get; }
	public string BackdropTexturePath { get; }
	public string FeatureTexturePath { get; }
	public float FeatureScale { get; }
	public Vector2 FeatureOffset { get; }
	public Color FeatureModulate { get; }

	public MissionBackgroundDefinition(
		string id,
		string displayName,
		string backdropTexturePath,
		string featureTexturePath,
		float featureScale,
		Vector2 featureOffset,
		Color featureModulate)
	{
		Id = id;
		DisplayName = displayName;
		BackdropTexturePath = backdropTexturePath;
		FeatureTexturePath = featureTexturePath;
		FeatureScale = featureScale;
		FeatureOffset = featureOffset;
		FeatureModulate = featureModulate;
	}
}

public static class MissionBackgroundCatalog
{
	public const string DefaultId = "none";
	private const string CatalogPath = "res://Data/mission_backgrounds.json";

	private static readonly List<MissionBackgroundDefinition> Definitions = LoadDefinitions();

	public static IReadOnlyList<MissionBackgroundDefinition> All => Definitions;

	public static MissionBackgroundDefinition GetById(string id)
	{
		return Definitions.FirstOrDefault(def => def.Id == id) ?? Definitions[0];
	}

	private static List<MissionBackgroundDefinition> LoadDefinitions()
	{
		if (!FileAccess.FileExists(CatalogPath))
		{
			GD.PrintErr($"Mission background catalog not found at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackDefinitions();
		}

		using FileAccess file = FileAccess.Open(CatalogPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr($"Failed to open mission background catalog at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackDefinitions();
		}

		Json json = new Json();
		if (json.Parse(file.GetAsText()) != Error.Ok)
		{
			GD.PrintErr($"Failed to parse mission background catalog at {CatalogPath}. Using fallback catalog.");
			return CreateFallbackDefinitions();
		}

		if (json.Data.VariantType != Variant.Type.Array)
		{
			GD.PrintErr($"Mission background catalog at {CatalogPath} is not an array. Using fallback catalog.");
			return CreateFallbackDefinitions();
		}

		Godot.Collections.Array backgroundArray = (Godot.Collections.Array)json.Data;
		List<MissionBackgroundDefinition> catalog = new List<MissionBackgroundDefinition>();
		foreach (Variant backgroundVariant in backgroundArray)
		{
			Godot.Collections.Dictionary backgroundDict = (Godot.Collections.Dictionary)backgroundVariant;
			MissionBackgroundDefinition definition = ParseDefinition(backgroundDict);
			if (!string.IsNullOrEmpty(definition.Id))
			{
				catalog.Add(definition);
			}
		}

		if (catalog.Count == 0)
		{
			GD.PrintErr($"Mission background catalog at {CatalogPath} contained no valid entries. Using fallback catalog.");
			return CreateFallbackDefinitions();
		}

		if (!catalog.Any(def => def.Id == DefaultId))
		{
			catalog.Insert(0, CreateNoneDefinition());
		}

		return catalog;
	}

	private static MissionBackgroundDefinition ParseDefinition(Godot.Collections.Dictionary backgroundDict)
	{
		string id = backgroundDict.ContainsKey("Id") ? (string)backgroundDict["Id"] : string.Empty;
		string displayName = backgroundDict.ContainsKey("DisplayName") ? (string)backgroundDict["DisplayName"] : id;
		string backdropTexturePath = backgroundDict.ContainsKey("BackdropTexturePath") ? (string)backgroundDict["BackdropTexturePath"] : string.Empty;
		string featureTexturePath = backgroundDict.ContainsKey("FeatureTexturePath") ? (string)backgroundDict["FeatureTexturePath"] : string.Empty;
		float featureScale = backgroundDict.ContainsKey("FeatureScale") ? backgroundDict["FeatureScale"].AsSingle() : 1f;
		float offsetX = backgroundDict.ContainsKey("FeatureOffsetX") ? backgroundDict["FeatureOffsetX"].AsSingle() : 0f;
		float offsetY = backgroundDict.ContainsKey("FeatureOffsetY") ? backgroundDict["FeatureOffsetY"].AsSingle() : 0f;
		float modulateR = backgroundDict.ContainsKey("FeatureModulateR") ? backgroundDict["FeatureModulateR"].AsSingle() : 1f;
		float modulateG = backgroundDict.ContainsKey("FeatureModulateG") ? backgroundDict["FeatureModulateG"].AsSingle() : 1f;
		float modulateB = backgroundDict.ContainsKey("FeatureModulateB") ? backgroundDict["FeatureModulateB"].AsSingle() : 1f;
		float modulateA = backgroundDict.ContainsKey("FeatureModulateA") ? backgroundDict["FeatureModulateA"].AsSingle() : 0f;

		return new MissionBackgroundDefinition(
			id,
			displayName,
			backdropTexturePath,
			featureTexturePath,
			featureScale,
			new Vector2(offsetX, offsetY),
			new Color(modulateR, modulateG, modulateB, modulateA));
	}

	private static List<MissionBackgroundDefinition> CreateFallbackDefinitions()
	{
		Color planetTint = new Color(0.95f, 0.98f, 1f, 0.20f);
		Color outpostTint = new Color(0.95f, 0.98f, 1f, 0.24f);

		return new List<MissionBackgroundDefinition>
		{
			CreateNoneDefinition(),
			new MissionBackgroundDefinition("space_stars", "Space - Stars", "res://Assets/Backgrounds/space_bg.png", string.Empty, 1f, Vector2.Zero, Colors.Transparent),
			new MissionBackgroundDefinition("planet_terra", "Planet - Terra", "res://Assets/Backgrounds/space_bg.png", "res://Planets/terra_planet.png", 1.95f, new Vector2(0f, -250f), planetTint),
			new MissionBackgroundDefinition("planet_arid", "Planet - Arid", "res://Assets/Backgrounds/space_bg.png", "res://Planets/arid_planet.png", 1.90f, new Vector2(0f, -248f), planetTint),
			new MissionBackgroundDefinition("planet_ocean", "Planet - Ocean", "res://Assets/Backgrounds/space_bg.png", "res://Planets/ocean_planet.png", 1.92f, new Vector2(0f, -248f), planetTint),
			new MissionBackgroundDefinition("planet_toxic", "Planet - Toxic", "res://Assets/Backgrounds/space_bg.png", "res://Planets/toxic_planet.png", 1.94f, new Vector2(0f, -248f), planetTint),
			new MissionBackgroundDefinition("planet_frozen", "Planet - Frozen", "res://Assets/Backgrounds/space_bg.png", "res://Planets/frozen_planet.png", 1.90f, new Vector2(0f, -250f), planetTint),
			new MissionBackgroundDefinition("planet_lava", "Planet - Lava", "res://Assets/Backgrounds/space_bg.png", "res://Planets/lava_planet.png", 1.95f, new Vector2(0f, -250f), planetTint),
			new MissionBackgroundDefinition("outpost_black_market", "Outpost - Black Market", "res://Assets/Backgrounds/space_bg.png", "res://Assets/Outposts/BlackMarketAsteroidExchangeSprite.png", 1.28f, new Vector2(0f, -180f), outpostTint),
			new MissionBackgroundDefinition("outpost_scrappers", "Outpost - Scrapper's Furnace", "res://Assets/Backgrounds/space_bg.png", "res://Assets/Outposts/ScrappersFurnaceHubSprite.png", 1.28f, new Vector2(0f, -180f), outpostTint),
			new MissionBackgroundDefinition("outpost_verdant", "Outpost - Verdant Pact", "res://Assets/Backgrounds/space_bg.png", "res://Assets/Outposts/VerdantPactBiosphereOutpostSprite.png", 1.28f, new Vector2(0f, -180f), outpostTint)
		};
	}

	private static MissionBackgroundDefinition CreateNoneDefinition()
	{
		return new MissionBackgroundDefinition(DefaultId, "No Background", string.Empty, string.Empty, 1f, Vector2.Zero, Colors.Transparent);
	}
}
