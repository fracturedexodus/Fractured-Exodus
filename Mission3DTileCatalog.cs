using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class Mission3DTileDefinition
{
	public string Id { get; }
	public string DisplayName { get; }
	public MissionTileCategory Category { get; }
	public Vector3 DefaultOffset { get; }

	public Mission3DTileDefinition(string id, string displayName, MissionTileCategory category, Vector3 defaultOffset)
	{
		Id = id;
		DisplayName = displayName;
		Category = category;
		DefaultOffset = defaultOffset;
	}
}

public static class Mission3DTileCatalog
{
	private static readonly List<Mission3DTileDefinition> Definitions = new List<Mission3DTileDefinition>
	{
		new Mission3DTileDefinition("floor_panel", "Floor Panel", MissionTileCategory.Floor, Vector3.Zero),
		new Mission3DTileDefinition("floor_hazard", "Floor Hazard", MissionTileCategory.Floor, Vector3.Zero),
		new Mission3DTileDefinition("wall_north", "Wall North", MissionTileCategory.Wall, new Vector3(0f, 1.5f, -1.85f)),
		new Mission3DTileDefinition("wall_west", "Wall West", MissionTileCategory.Wall, new Vector3(-1.85f, 1.5f, 0f)),
		new Mission3DTileDefinition("wall_corner_nw", "Wall Corner NW", MissionTileCategory.Wall, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("door_north", "Door North", MissionTileCategory.Wall, new Vector3(0f, 0f, -1.85f)),
		new Mission3DTileDefinition("console_terminal", "Console Terminal", MissionTileCategory.Prop, new Vector3(0f, 0.5f, 0f)),
		new Mission3DTileDefinition("crate", "Crate", MissionTileCategory.Prop, new Vector3(0f, 0.7f, 0f)),
		new Mission3DTileDefinition("medical_bed", "Medical Bed", MissionTileCategory.Prop, new Vector3(0f, 0.45f, 0f)),
		new Mission3DTileDefinition("archive_core", "Archive Core", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("pipe_bundle", "Pipe Bundle", MissionTileCategory.Prop, new Vector3(0f, 0.2f, 0f)),
		new Mission3DTileDefinition("debris", "Debris", MissionTileCategory.Prop, new Vector3(0f, 0.2f, 0f))
	};

	private static StandardMaterial3D _steelMaterial;
	private static StandardMaterial3D _darkSteelMaterial;
	private static StandardMaterial3D _accentCyanMaterial;
	private static StandardMaterial3D _accentAmberMaterial;
	private static StandardMaterial3D _warningMaterial;
	private static StandardMaterial3D _crateMaterial;
	private static StandardMaterial3D _bedMaterial;
	private static StandardMaterial3D _glassMaterial;
	private static StandardMaterial3D _debrisMaterial;
	private static StandardMaterial3D _outlineMaterial;

	public static IReadOnlyList<Mission3DTileDefinition> All => Definitions;

	public static bool TryGetById(string id, out Mission3DTileDefinition definition)
	{
		definition = Definitions.FirstOrDefault(def => def.Id == id);
		return definition != null;
	}

	public static Node3D CreateTileNode(Mission3DTileDefinition definition)
	{
		EnsureMaterials();

		Node3D root = new Node3D
		{
			Name = definition.Id
		};

		Node3D visuals = new Node3D { Name = "Visuals" };
		root.AddChild(visuals);

		switch (definition.Id)
		{
			case "floor_panel":
				BuildFloor(visuals, false);
				break;
			case "floor_hazard":
				BuildFloor(visuals, true);
				break;
			case "wall_north":
				AddMesh(visuals, CreateBox(new Vector3(4.0f, 3.0f, 0.35f), _darkSteelMaterial), Vector3.Zero);
				AddMesh(visuals, CreateBox(new Vector3(4.0f, 0.2f, 0.4f), _accentCyanMaterial), new Vector3(0f, 0.3f, 0f), new Vector3(1f, 1f, 0.25f));
				break;
			case "wall_west":
				AddMesh(visuals, CreateBox(new Vector3(0.35f, 3.0f, 4.0f), _darkSteelMaterial), Vector3.Zero);
				AddMesh(visuals, CreateBox(new Vector3(0.4f, 0.2f, 4.0f), _accentCyanMaterial), new Vector3(0f, 0.3f, 0f), new Vector3(0.25f, 1f, 1f));
				break;
			case "wall_corner_nw":
				AddMesh(visuals, CreateBox(new Vector3(4.0f, 3.0f, 0.35f), _darkSteelMaterial), new Vector3(0f, 1.5f, -1.85f));
				AddMesh(visuals, CreateBox(new Vector3(0.35f, 3.0f, 4.0f), _darkSteelMaterial), new Vector3(-1.85f, 1.5f, 0f));
				break;
			case "door_north":
				BuildDoor(visuals);
				break;
			case "console_terminal":
				BuildConsole(visuals);
				break;
			case "crate":
				BuildCrate(visuals);
				break;
			case "medical_bed":
				BuildMedicalBed(visuals);
				break;
			case "archive_core":
				BuildArchiveCore(visuals);
				break;
			case "pipe_bundle":
				BuildPipeBundle(visuals);
				break;
			case "debris":
				BuildDebris(visuals);
				break;
		}

		StaticBody3D body = new StaticBody3D { Name = "Body" };
		CollisionShape3D collider = new CollisionShape3D
		{
			Name = "Collider",
			Shape = new BoxShape3D
			{
				Size = new Vector3(4.0f, 3.6f, 4.0f)
			}
		};
		body.AddChild(collider);
		root.AddChild(body);

		Node3D outline = new Node3D
		{
			Name = "SelectionOutline",
			Visible = false
		};
		AddMesh(outline, CreateBox(new Vector3(4.15f, 0.08f, 4.15f), _outlineMaterial), new Vector3(0f, 0.05f, 0f));
		root.AddChild(outline);

		return root;
	}

	private static void BuildFloor(Node3D parent, bool hazard)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 0.22f, 4.0f), _steelMaterial), Vector3.Zero);
		if (hazard)
		{
			AddMesh(parent, CreateBox(new Vector3(3.2f, 0.04f, 0.3f), _warningMaterial), new Vector3(0f, 0.14f, 0f));
			AddMesh(parent, CreateBox(new Vector3(0.3f, 0.04f, 3.2f), _warningMaterial), new Vector3(0f, 0.14f, 0f));
		}
		else
		{
			AddMesh(parent, CreateBox(new Vector3(3.0f, 0.04f, 0.18f), _accentCyanMaterial), new Vector3(0f, 0.14f, 0f), new Vector3(1f, 1f, 0.5f));
		}
	}

	private static void BuildDoor(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 0.3f, 0.45f), _steelMaterial), new Vector3(0f, 3.05f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.55f, 3.1f, 0.45f), _darkSteelMaterial), new Vector3(-1.72f, 1.55f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.55f, 3.1f, 0.45f), _darkSteelMaterial), new Vector3(1.72f, 1.55f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.45f, 2.6f, 0.25f), _steelMaterial), new Vector3(-0.78f, 1.3f, 0.05f));
		AddMesh(parent, CreateBox(new Vector3(1.45f, 2.6f, 0.25f), _steelMaterial), new Vector3(0.78f, 1.3f, -0.05f));
		AddMesh(parent, CreateBox(new Vector3(0.14f, 1.8f, 0.12f), _accentCyanMaterial), new Vector3(0f, 1.6f, 0.14f));
	}

	private static void BuildConsole(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(2.4f, 0.9f, 1.3f), _darkSteelMaterial), new Vector3(0f, 0.45f, 0.2f));
		AddMesh(parent, CreateBox(new Vector3(1.8f, 0.15f, 0.8f), _accentCyanMaterial), new Vector3(0f, 0.97f, -0.08f));
		AddMesh(parent, CreateBox(new Vector3(1.8f, 1.0f, 0.18f), _steelMaterial), new Vector3(0f, 1.35f, -0.52f), new Vector3(1f, 1f, 0.4f));
	}

	private static void BuildCrate(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(1.8f, 1.4f, 1.8f), _crateMaterial), new Vector3(0f, 0.7f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.2f, 0.08f, 1.2f), _accentCyanMaterial), new Vector3(0f, 1.22f, 0f));
	}

	private static void BuildMedicalBed(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(2.4f, 0.45f, 1.4f), _bedMaterial), new Vector3(-0.4f, 0.45f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.3f, 0.95f, 1.4f), _darkSteelMaterial), new Vector3(-1.55f, 0.7f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.85f, 1.6f, 0.85f), _darkSteelMaterial), new Vector3(1.15f, 0.8f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.6f, 0.8f, 0.08f), _accentCyanMaterial), new Vector3(1.15f, 1.25f, -0.45f));
	}

	private static void BuildArchiveCore(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(2.2f, 0.55f, 2.2f), _darkSteelMaterial), new Vector3(0f, 0.28f, 0f));
		AddMesh(parent, CreateCylinder(0.62f, 1.8f, _glassMaterial), new Vector3(0f, 1.25f, 0f));
		AddMesh(parent, CreateCylinder(0.16f, 1.2f, _accentCyanMaterial), new Vector3(0f, 1.35f, 0f));
	}

	private static void BuildPipeBundle(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(0.18f, 2.4f, _steelMaterial), new Vector3(-0.6f, 0.18f, 0f), new Vector3(1f, 1f, 1f), new Vector3(0f, 0f, 90f));
		AddMesh(parent, CreateCylinder(0.18f, 1.8f, _steelMaterial), new Vector3(0.9f, 0.18f, 0.4f), new Vector3(1f, 1f, 1f), new Vector3(0f, 0f, 90f));
		AddMesh(parent, CreateCylinder(0.18f, 1.6f, _steelMaterial), new Vector3(0.2f, 0.95f, -0.7f));
	}

	private static void BuildDebris(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(1.8f, 1.0f, 1.2f), _debrisMaterial), new Vector3(-0.3f, 0.5f, 0.2f), new Vector3(1f, 1f, 1f), new Vector3(0f, 18f, -12f));
		AddMesh(parent, CreateBox(new Vector3(1.2f, 0.55f, 1.8f), _darkSteelMaterial), new Vector3(0.9f, 0.28f, -0.3f), new Vector3(1f, 1f, 1f), new Vector3(10f, -20f, 0f));
	}

	private static MeshInstance3D CreateBox(Vector3 size, Material material)
	{
		BoxMesh mesh = new BoxMesh { Size = size };
		return new MeshInstance3D
		{
			Mesh = mesh,
			MaterialOverride = material
		};
	}

	private static MeshInstance3D CreateCylinder(float radius, float height, Material material)
	{
		CylinderMesh mesh = new CylinderMesh
		{
			TopRadius = radius,
			BottomRadius = radius,
			Height = height,
			RadialSegments = 18
		};
		return new MeshInstance3D
		{
			Mesh = mesh,
			MaterialOverride = material
		};
	}

	private static void AddMesh(Node3D parent, MeshInstance3D mesh, Vector3 position, Vector3? scale = null, Vector3? rotationDegrees = null)
	{
		mesh.Position = position;
		mesh.Scale = scale ?? Vector3.One;
		mesh.RotationDegrees = rotationDegrees ?? Vector3.Zero;
		parent.AddChild(mesh);
	}

	private static void EnsureMaterials()
	{
		_steelMaterial ??= CreateMaterial(new Color(0.20f, 0.24f, 0.29f), 0.15f, 0.72f);
		_darkSteelMaterial ??= CreateMaterial(new Color(0.12f, 0.15f, 0.19f), 0.18f, 0.85f);
		_accentCyanMaterial ??= CreateEmissiveMaterial(new Color(0.20f, 0.90f, 1.00f), 1.8f);
		_accentAmberMaterial ??= CreateEmissiveMaterial(new Color(0.95f, 0.65f, 0.18f), 1.4f);
		_warningMaterial ??= CreateEmissiveMaterial(new Color(0.95f, 0.72f, 0.15f), 0.9f);
		_crateMaterial ??= CreateMaterial(new Color(0.18f, 0.22f, 0.26f), 0.12f, 0.8f);
		_bedMaterial ??= CreateMaterial(new Color(0.62f, 0.68f, 0.72f), 0.04f, 0.42f);
		_glassMaterial ??= CreateEmissiveMaterial(new Color(0.18f, 0.82f, 0.94f), 2.1f, 0.45f);
		_debrisMaterial ??= CreateMaterial(new Color(0.25f, 0.18f, 0.12f), 0.05f, 0.95f);
		_outlineMaterial ??= CreateEmissiveMaterial(new Color(0.35f, 0.96f, 1.00f), 2.0f, 0.35f);
	}

	private static StandardMaterial3D CreateMaterial(Color albedo, float metallic, float roughness)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = albedo,
			Metallic = metallic,
			Roughness = roughness
		};
	}

	private static StandardMaterial3D CreateEmissiveMaterial(Color emissiveColor, float energy, float alpha = 1f)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = new Color(emissiveColor.R, emissiveColor.G, emissiveColor.B, alpha),
			EmissionEnabled = true,
			Emission = emissiveColor,
			EmissionEnergyMultiplier = energy,
			Transparency = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = alpha < 1f
		};
	}
}
