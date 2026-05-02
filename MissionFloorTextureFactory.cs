using Godot;
using System.Collections.Generic;

public static class MissionFloorTextureFactory
{
	public static readonly Vector2 TileSize = new Vector2(226f, 133f);
	private const int Width = 226;
	private const int Height = 133;
	private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
	private static readonly Dictionary<string, string> TexturePaths = new Dictionary<string, string>
	{
		{ "floor_standard", "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/floors/floor_standard_226.png" },
		{ "floor_grate", "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/floors/floor_grate_226.png" },
		{ "floor_hazard", "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/floors/floor_hazard_226.png" },
		{ "floor_panel", "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/floors/floor_vented_226.png" },
		{ "floor_plating", "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/floors/floor_plating_226.png" },
		{ "floor_compact_panel", "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/floors/floor_compact_panel_226.png" },
		{ "floor_glow", "res://Assets/Missions/BlackSiteRelay/GeminiSheetSet/floors/floor_relay_glow_226.png" }
	};

	public static Texture2D GetTexture(string tileId)
	{
		if (Cache.TryGetValue(tileId, out Texture2D texture))
		{
			return texture;
		}

		texture = LoadRenderedTexture(tileId);
		if (texture != null)
		{
			Cache[tileId] = texture;
			return texture;
		}

		texture = CreateFallbackTexture(tileId);
		Cache[tileId] = texture;
		return texture;
	}

	private static Texture2D LoadRenderedTexture(string tileId)
	{
		if (!TexturePaths.TryGetValue(tileId, out string path))
		{
			return null;
		}

		Texture2D texture = ResourceLoader.Load<Texture2D>(path);
		if (texture != null)
		{
			return texture;
		}

		string absolutePath = ProjectSettings.GlobalizePath(path);
		if (!FileAccess.FileExists(absolutePath))
		{
			return null;
		}

		Image image = new Image();
		if (image.Load(absolutePath) != Error.Ok)
		{
			return null;
		}

		return ImageTexture.CreateFromImage(image);
	}

	private static Texture2D CreateFallbackTexture(string tileId)
	{
		(Color baseColor, Color lineColor, Color accentColor) = GetPalette(tileId);
		Image image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
		Vector2 center = new Vector2(Width * 0.5f, Height * 0.5f);
		float halfWidth = Width * 0.5f;
		float halfHeight = Height * 0.5f;

		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				float normalizedDiamond = Mathf.Abs(x - center.X) / halfWidth + Mathf.Abs(y - center.Y) / halfHeight;
				if (normalizedDiamond > 1f)
				{
					continue;
				}

				Color color = baseColor;
				bool isEdge = normalizedDiamond > 0.95f;
				bool isMidline = Mathf.Abs(x - center.X) < 1.2f || Mathf.Abs(y - center.Y) < 1.0f;
				bool isAccent = tileId == "floor_glow" && Mathf.Abs(y - center.Y) < 1.4f && x > Width * 0.28f && x < Width * 0.72f;
				color = isAccent ? accentColor : (isEdge || isMidline ? lineColor : color);

				image.SetPixel(x, y, color);
			}
		}

		return ImageTexture.CreateFromImage(image);
	}

	private static (Color Base, Color Line, Color Accent) GetPalette(string tileId)
	{
		return tileId switch
		{
			"floor_grate" => (new Color(0.16f, 0.18f, 0.19f, 1f), new Color(0.37f, 0.43f, 0.45f, 1f), new Color(0.25f, 0.85f, 0.95f, 1f)),
			"floor_hazard" => (new Color(0.18f, 0.18f, 0.17f, 1f), new Color(0.38f, 0.38f, 0.34f, 1f), new Color(0.95f, 0.68f, 0.18f, 1f)),
			"floor_panel" => (new Color(0.17f, 0.20f, 0.22f, 1f), new Color(0.36f, 0.42f, 0.45f, 1f), new Color(0.25f, 0.86f, 0.98f, 1f)),
			"floor_plating" => (new Color(0.15f, 0.17f, 0.19f, 1f), new Color(0.33f, 0.37f, 0.41f, 1f), new Color(0.62f, 0.68f, 0.72f, 1f)),
			"floor_glow" => (new Color(0.12f, 0.17f, 0.20f, 1f), new Color(0.26f, 0.36f, 0.42f, 1f), new Color(0.20f, 0.92f, 1.00f, 1f)),
			_ => (new Color(0.18f, 0.20f, 0.21f, 1f), new Color(0.36f, 0.40f, 0.42f, 1f), new Color(0.30f, 0.78f, 0.88f, 1f))
		};
	}

}
