using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class Mission3DTileDefinition
{
	public string Id { get; }
	public string DisplayName { get; }
	public MissionTileCategory Category { get; }
	public Vector3 DefaultOffset { get; }
	public string ScenePath { get; }
	public Vector3 SceneScale { get; }
	public Vector3 SceneRotationDegrees { get; }
	public bool UsesExternalScene => !string.IsNullOrEmpty(ScenePath);

	public Mission3DTileDefinition(
		string id,
		string displayName,
		MissionTileCategory category,
		Vector3 defaultOffset,
		string scenePath = "",
		Vector3? sceneScale = null,
		Vector3? sceneRotationDegrees = null)
	{
		Id = id;
		DisplayName = displayName;
		Category = category;
		DefaultOffset = defaultOffset;
		ScenePath = scenePath;
		SceneScale = sceneScale ?? Vector3.One;
		SceneRotationDegrees = sceneRotationDegrees ?? Vector3.Zero;
	}
}

public sealed class Mission3DSkinDefinition
{
	public string Id { get; }
	public string DisplayName { get; }

	public Mission3DSkinDefinition(string id, string displayName)
	{
		Id = id;
		DisplayName = displayName;
	}
}

public static class Mission3DTileCatalog
{
	private static readonly List<Mission3DTileDefinition> Definitions = new List<Mission3DTileDefinition>
	{
		new Mission3DTileDefinition("floor_panel", "Floor Panel", MissionTileCategory.Floor, Vector3.Zero),
		new Mission3DTileDefinition("floor_hazard", "Floor Hazard", MissionTileCategory.Floor, Vector3.Zero),
		new Mission3DTileDefinition("floor_aetherweb", "Aetherweb Floor", MissionTileCategory.Floor, Vector3.Zero),
		new Mission3DTileDefinition("floor_shattered_reach", "Shattered Reach Floor", MissionTileCategory.Floor, Vector3.Zero),
		new Mission3DTileDefinition("floor_ember_waste", "Ember Waste Floor", MissionTileCategory.Floor, Vector3.Zero),
		new Mission3DTileDefinition("floor_verdant_shroud", "Verdant Shroud Floor", MissionTileCategory.Floor, Vector3.Zero),
		new Mission3DTileDefinition("q_floor_basic", "Q Floor Basic", MissionTileCategory.Floor, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/map/bodies/floor_tile_basic.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_floor_panel", "Q Floor Panel", MissionTileCategory.Floor, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/map/bodies/floor_tile_basic_2.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_floor_empty", "Q Floor Empty", MissionTileCategory.Floor, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/map/bodies/floor_tile_empty.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_floor_double", "Q Floor Double", MissionTileCategory.Floor, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/map/bodies/floor_tile_double.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("wall_north", "Wall North", MissionTileCategory.Wall, Vector3.Zero),
		new Mission3DTileDefinition("wall_west", "Wall West", MissionTileCategory.Wall, Vector3.Zero),
		new Mission3DTileDefinition("wall_corner_nw", "Wall Corner NW", MissionTileCategory.Wall, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("door_north", "Door North", MissionTileCategory.Wall, Vector3.Zero),
		new Mission3DTileDefinition("wall_obsidian_belt", "Obsidian Belt Bulkhead", MissionTileCategory.Wall, Vector3.Zero),
		new Mission3DTileDefinition("wall_crystal_verge", "Luminous Verge Wall", MissionTileCategory.Wall, Vector3.Zero),
		new Mission3DTileDefinition("wall_rift_barrier", "Rift Barrier", MissionTileCategory.Wall, Vector3.Zero),
		new Mission3DTileDefinition("q_wall_panel", "Q Wall Panel", MissionTileCategory.Wall, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/map/bodies/wall_1.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_wall_window", "Q Window Wall", MissionTileCategory.Wall, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/map/bodies/window_wall_side_a.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_wall_door_single", "Q Single Door Wall", MissionTileCategory.Wall, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/map/bodies/door_single_wall_side_a.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_wall_door_double", "Q Double Door Wall", MissionTileCategory.Wall, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/map/bodies/door_double_wall_side_a.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_door_single", "Q Door Single", MissionTileCategory.Wall, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/doors/bodies/door_single.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_door_double_left", "Q Door Double Left", MissionTileCategory.Wall, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/doors/bodies/door_double_l.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_door_double_right", "Q Door Double Right", MissionTileCategory.Wall, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/doors/bodies/door_double_r.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("console_terminal", "Console Terminal", MissionTileCategory.Prop, Vector3.Zero),
		new Mission3DTileDefinition("crate", "Crate", MissionTileCategory.Prop, Vector3.Zero),
		new Mission3DTileDefinition("medical_bed", "Medical Bed", MissionTileCategory.Prop, Vector3.Zero),
		new Mission3DTileDefinition("archive_core", "Archive Core", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("pipe_bundle", "Pipe Bundle", MissionTileCategory.Prop, Vector3.Zero),
		new Mission3DTileDefinition("debris", "Debris", MissionTileCategory.Prop, Vector3.Zero),
		new Mission3DTileDefinition("aetherweb_relay_pylon", "Aetherweb Relay Pylon", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("nexus_wound_core", "Nexus Wound Core", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("thronevault_data_shrine", "Thronevault Data Shrine", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("luminous_myth_node", "Luminous Myth Node", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("ember_terraformer_core", "Ember Terraformer Core", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("obsidian_trade_beacon", "Obsidian Trade Beacon", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("verdant_biosphere_pod", "Verdant Biosphere Pod", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("echo_spiral_time_anchor", "Echo Spiral Time Anchor", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("far_silence_probe", "Far Silence Probe", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("distress_lighthouse", "Lighthouse Red Beacon", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("broken_stargate_arc", "Broken Stargate Arc", MissionTileCategory.Prop, new Vector3(0f, 0f, 0f)),
		new Mission3DTileDefinition("q_computer", "Q Computer", MissionTileCategory.Prop, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/props/bodies/props_computer.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_computer_small", "Q Computer Small", MissionTileCategory.Prop, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/props/bodies/props_computer_small.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_crate", "Q Crate", MissionTileCategory.Prop, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/props/bodies/props_crate.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_container", "Q Container", MissionTileCategory.Prop, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/props/bodies/props_container_full.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_pod", "Q Pod", MissionTileCategory.Prop, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/props/bodies/props_pod.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_shelf", "Q Shelf", MissionTileCategory.Prop, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/props/bodies/props_shelf.tscn", new Vector3(2f, 2f, 2f)),
		new Mission3DTileDefinition("q_teleporter", "Q Teleporter", MissionTileCategory.Prop, Vector3.Zero, "res://addons/quaternius-modular-scifi-pack/props/bodies/props_teleporter_1.tscn", new Vector3(2f, 2f, 2f))
	};

	private static readonly List<Mission3DSkinDefinition> SkinDefinitions = new List<Mission3DSkinDefinition>
	{
		new Mission3DSkinDefinition("default", "Default"),
		new Mission3DSkinDefinition("aetherweb", "Aetherweb"),
		new Mission3DSkinDefinition("obsidian_belt", "Obsidian Belt"),
		new Mission3DSkinDefinition("ember_wastes", "Ember Wastes"),
		new Mission3DSkinDefinition("verdant_shroud", "Verdant Shroud"),
		new Mission3DSkinDefinition("luminous_verge", "Luminous Verge"),
		new Mission3DSkinDefinition("echo_rift", "Echo Rift")
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
	private static StandardMaterial3D _voidMaterial;
	private static StandardMaterial3D _obsidianMaterial;
	private static StandardMaterial3D _crystalMaterial;
	private static StandardMaterial3D _riftMaterial;
	private static StandardMaterial3D _emberMaterial;
	private static StandardMaterial3D _verdantMaterial;
	private static StandardMaterial3D _bioglassMaterial;
	private static StandardMaterial3D _signalRedMaterial;
	private static StandardMaterial3D _goldMaterial;

	public static IReadOnlyList<Mission3DTileDefinition> All => Definitions;
	public static IReadOnlyList<Mission3DSkinDefinition> AllSkins => SkinDefinitions;

	public static bool TryGetById(string id, out Mission3DTileDefinition definition)
	{
		definition = Definitions.FirstOrDefault(def => def.Id == id);
		return definition != null;
	}

	public static Node3D CreateTileNode(Mission3DTileDefinition definition)
	{
		return CreateTileNode(definition, "default");
	}

	public static Node3D CreateTileNode(Mission3DTileDefinition definition, string skinId)
	{
		EnsureMaterials();
		string normalizedSkinId = NormalizeSkinId(skinId);

		Node3D root = new Node3D
		{
			Name = definition.Id
		};

		Node3D visuals = new Node3D { Name = "Visuals" };
		root.AddChild(visuals);

		if (definition.UsesExternalScene)
		{
			BuildExternalScene(visuals, definition);
		}
		else switch (definition.Id)
		{
			case "floor_panel":
				BuildFloor(visuals, false);
				break;
			case "floor_hazard":
				BuildFloor(visuals, true);
				break;
			case "floor_aetherweb":
				BuildAetherwebFloor(visuals);
				break;
			case "floor_shattered_reach":
				BuildShatteredReachFloor(visuals);
				break;
			case "floor_ember_waste":
				BuildEmberWasteFloor(visuals);
				break;
			case "floor_verdant_shroud":
				BuildVerdantShroudFloor(visuals);
				break;
			case "wall_north":
				AddMesh(visuals, CreateBox(new Vector3(4.0f, 3.0f, 0.35f), _darkSteelMaterial), new Vector3(0f, 1.5f, 0f));
				AddMesh(visuals, CreateBox(new Vector3(4.0f, 0.2f, 0.4f), _accentCyanMaterial), new Vector3(0f, 1.8f, 0f), new Vector3(1f, 1f, 0.25f));
				break;
			case "wall_west":
				AddMesh(visuals, CreateBox(new Vector3(0.35f, 3.0f, 4.0f), _darkSteelMaterial), new Vector3(0f, 1.5f, 0f));
				AddMesh(visuals, CreateBox(new Vector3(0.4f, 0.2f, 4.0f), _accentCyanMaterial), new Vector3(0f, 1.8f, 0f), new Vector3(0.25f, 1f, 1f));
				break;
			case "wall_corner_nw":
				AddMesh(visuals, CreateBox(new Vector3(4.0f, 3.0f, 0.35f), _darkSteelMaterial), new Vector3(0f, 1.5f, -1.85f));
				AddMesh(visuals, CreateBox(new Vector3(0.35f, 3.0f, 4.0f), _darkSteelMaterial), new Vector3(-1.85f, 1.5f, 0f));
				break;
			case "door_north":
				BuildDoor(visuals);
				break;
			case "wall_obsidian_belt":
				BuildObsidianBeltWall(visuals);
				break;
			case "wall_crystal_verge":
				BuildCrystalVergeWall(visuals);
				break;
			case "wall_rift_barrier":
				BuildRiftBarrier(visuals);
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
			case "aetherweb_relay_pylon":
				BuildAetherwebRelayPylon(visuals);
				break;
			case "nexus_wound_core":
				BuildNexusWoundCore(visuals);
				break;
			case "thronevault_data_shrine":
				BuildThronevaultDataShrine(visuals);
				break;
			case "luminous_myth_node":
				BuildLuminousMythNode(visuals);
				break;
			case "ember_terraformer_core":
				BuildEmberTerraformerCore(visuals);
				break;
			case "obsidian_trade_beacon":
				BuildObsidianTradeBeacon(visuals);
				break;
			case "verdant_biosphere_pod":
				BuildVerdantBiospherePod(visuals);
				break;
			case "echo_spiral_time_anchor":
				BuildEchoSpiralTimeAnchor(visuals);
				break;
			case "far_silence_probe":
				BuildFarSilenceProbe(visuals);
				break;
			case "distress_lighthouse":
				BuildDistressLighthouse(visuals);
				break;
			case "broken_stargate_arc":
				BuildBrokenStargateArc(visuals);
				break;
		}

		StaticBody3D body = new StaticBody3D { Name = "Body" };
		CollisionShape3D collider = new CollisionShape3D
		{
			Name = "Collider",
			Shape = new BoxShape3D { Size = GetSelectionColliderSize(definition) },
			Position = GetSelectionColliderOffset(definition)
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

		root.SetMeta("skin_id", normalizedSkinId);
		ApplySkin(root, normalizedSkinId);
		return root;
	}

	private static Vector3 GetSelectionColliderSize(Mission3DTileDefinition definition)
	{
		return definition.Category == MissionTileCategory.Floor
			? new Vector3(4.0f, 0.35f, 4.0f)
			: new Vector3(4.0f, 3.6f, 4.0f);
	}

	private static Vector3 GetSelectionColliderOffset(Mission3DTileDefinition definition)
	{
		return definition.Category == MissionTileCategory.Floor
			? new Vector3(0f, 0.18f, 0f)
			: Vector3.Zero;
	}

	private static void BuildExternalScene(Node3D parent, Mission3DTileDefinition definition)
	{
		PackedScene scene = ResourceLoader.Load<PackedScene>(definition.ScenePath);
		if (scene == null)
		{
			BuildMissingExternalSceneFallback(parent);
			return;
		}

		Node instance = scene.Instantiate();
		if (instance is not Node3D sceneRoot)
		{
			instance.QueueFree();
			BuildMissingExternalSceneFallback(parent);
			return;
		}

		sceneRoot.Name = "ExternalScene";
		sceneRoot.Scale = definition.SceneScale;
		sceneRoot.RotationDegrees = definition.SceneRotationDegrees;
		parent.AddChild(sceneRoot);
	}

	private static void BuildMissingExternalSceneFallback(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(2.6f, 0.3f, 2.6f), _darkSteelMaterial), new Vector3(0f, 0.15f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.8f, 1.8f, 1.8f), _warningMaterial), new Vector3(0f, 1.05f, 0f));
	}

	public static bool TryGetSkinById(string skinId, out Mission3DSkinDefinition skin)
	{
		skin = SkinDefinitions.FirstOrDefault(def => def.Id == skinId);
		return skin != null;
	}

	public static void ApplySkin(Node root, string skinId)
	{
		EnsureMaterials();
		string normalizedSkinId = NormalizeSkinId(skinId);
		ApplySkinRecursive(root, normalizedSkinId);
		if (root is Node3D tileRoot)
		{
			tileRoot.SetMeta("skin_id", normalizedSkinId);
		}
	}

	private static string NormalizeSkinId(string skinId)
	{
		return SkinDefinitions.Any(def => def.Id == skinId) ? skinId : "default";
	}

	private static void ApplySkinRecursive(Node node, string skinId)
	{
		if (node is MeshInstance3D mesh)
		{
			string role = mesh.GetMeta("material_role", "").AsString();
			if (!string.IsNullOrEmpty(role) && role != "outline")
			{
				mesh.MaterialOverride = CreateSkinMaterial(role, skinId);
			}
		}

		foreach (Node child in node.GetChildren())
		{
			ApplySkinRecursive(child, skinId);
		}
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

	private static void BuildAetherwebFloor(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 0.2f, 4.0f), _darkSteelMaterial), Vector3.Zero);
		AddMesh(parent, CreateBox(new Vector3(3.55f, 0.04f, 0.12f), _accentCyanMaterial), new Vector3(0f, 0.14f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.12f, 0.04f, 3.55f), _accentCyanMaterial), new Vector3(0f, 0.14f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.7f, 0.04f, 0.10f), _accentAmberMaterial), new Vector3(0.85f, 0.15f, 0.85f), Vector3.One, new Vector3(0f, 45f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.7f, 0.04f, 0.10f), _accentAmberMaterial), new Vector3(-0.85f, 0.15f, -0.85f), Vector3.One, new Vector3(0f, 45f, 0f));
	}

	private static void BuildShatteredReachFloor(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 0.18f, 4.0f), _debrisMaterial), Vector3.Zero);
		AddMesh(parent, CreateBox(new Vector3(1.55f, 0.06f, 1.2f), _steelMaterial), new Vector3(-0.9f, 0.14f, 0.55f), Vector3.One, new Vector3(0f, -18f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.25f, 0.06f, 1.55f), _darkSteelMaterial), new Vector3(0.85f, 0.15f, -0.55f), Vector3.One, new Vector3(0f, 24f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.16f, 0.05f, 2.8f), _riftMaterial), new Vector3(0.1f, 0.18f, 0f), Vector3.One, new Vector3(0f, -35f, 0f));
	}

	private static void BuildEmberWasteFloor(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 0.2f, 4.0f), _debrisMaterial), Vector3.Zero);
		AddMesh(parent, CreateBox(new Vector3(3.4f, 0.05f, 0.18f), _emberMaterial), new Vector3(0f, 0.15f, 0f), Vector3.One, new Vector3(0f, 16f, 0f));
		AddMesh(parent, CreateBox(new Vector3(2.4f, 0.05f, 0.16f), _emberMaterial), new Vector3(0.2f, 0.16f, -0.95f), Vector3.One, new Vector3(0f, -26f, 0f));
		AddMesh(parent, CreateCylinder(0.24f, 0.08f, _warningMaterial), new Vector3(-1.35f, 0.18f, 1.15f));
	}

	private static void BuildVerdantShroudFloor(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 0.18f, 4.0f), _steelMaterial), Vector3.Zero);
		AddMesh(parent, CreateBox(new Vector3(3.2f, 0.05f, 0.28f), _verdantMaterial), new Vector3(0f, 0.15f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.28f, 0.05f, 3.2f), _verdantMaterial), new Vector3(0f, 0.15f, 0f));
		AddMesh(parent, CreateSphere(0.22f, _verdantMaterial), new Vector3(-1.25f, 0.28f, -1.25f), new Vector3(1f, 0.45f, 1f));
		AddMesh(parent, CreateSphere(0.18f, _verdantMaterial), new Vector3(1.4f, 0.25f, 1.05f), new Vector3(1f, 0.4f, 1f));
	}

	private static void BuildObsidianBeltWall(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 3.0f, 0.38f), _obsidianMaterial), new Vector3(0f, 1.5f, 0f));
		AddMesh(parent, CreateBox(new Vector3(3.4f, 0.14f, 0.42f), _signalRedMaterial), new Vector3(0f, 2.15f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.22f, 2.4f, 0.44f), _goldMaterial), new Vector3(-1.45f, 1.2f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.22f, 2.4f, 0.44f), _goldMaterial), new Vector3(1.45f, 1.2f, 0f));
	}

	private static void BuildCrystalVergeWall(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 0.55f, 0.34f), _darkSteelMaterial), new Vector3(0f, 0.28f, 0f));
		for (int i = 0; i < 5; i++)
		{
			float x = -1.6f + (i * 0.8f);
			float height = 1.8f + ((i % 2) * 0.7f);
			AddMesh(parent, CreateCylinder(0.22f, height, _crystalMaterial, 0.06f), new Vector3(x, 0.15f + (height * 0.5f), 0f), Vector3.One, new Vector3(0f, 0f, i % 2 == 0 ? 8f : -8f));
		}
		AddMesh(parent, CreateBox(new Vector3(3.5f, 0.08f, 0.18f), _accentCyanMaterial), new Vector3(0f, 1.85f, 0.1f));
	}

	private static void BuildRiftBarrier(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(4.0f, 0.22f, 0.32f), _darkSteelMaterial), new Vector3(0f, 0.12f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.28f, 2.8f, 0.18f), _riftMaterial), new Vector3(-0.85f, 1.48f, 0f), Vector3.One, new Vector3(0f, 0f, -18f));
		AddMesh(parent, CreateBox(new Vector3(0.22f, 2.3f, 0.18f), _riftMaterial), new Vector3(0.15f, 1.32f, 0.05f), Vector3.One, new Vector3(0f, 0f, 12f));
		AddMesh(parent, CreateBox(new Vector3(0.18f, 2.0f, 0.18f), _riftMaterial), new Vector3(1.1f, 1.12f, 0f), Vector3.One, new Vector3(0f, 0f, -10f));
	}

	private static void BuildAetherwebRelayPylon(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(1.0f, 0.35f, _darkSteelMaterial), new Vector3(0f, 0.18f, 0f));
		AddMesh(parent, CreateCylinder(0.38f, 3.0f, _steelMaterial, 0.22f), new Vector3(0f, 1.65f, 0f));
		AddMesh(parent, CreateSphere(0.52f, _accentCyanMaterial), new Vector3(0f, 3.35f, 0f));
		AddMesh(parent, CreateBox(new Vector3(2.8f, 0.08f, 0.18f), _accentCyanMaterial), new Vector3(0f, 2.35f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.18f, 0.08f, 2.8f), _accentCyanMaterial), new Vector3(0f, 2.35f, 0f));
	}

	private static void BuildNexusWoundCore(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(1.25f, 0.28f, _obsidianMaterial), new Vector3(0f, 0.14f, 0f));
		AddMesh(parent, CreateSphere(0.76f, _voidMaterial), new Vector3(0f, 1.35f, 0f));
		AddMesh(parent, CreateCylinder(0.9f, 0.08f, _riftMaterial), new Vector3(0f, 1.35f, 0f), new Vector3(1f, 1f, 1f), new Vector3(90f, 0f, 0f));
		AddMesh(parent, CreateCylinder(0.65f, 0.08f, _accentCyanMaterial), new Vector3(0f, 1.35f, 0f), new Vector3(1f, 1f, 1f), new Vector3(0f, 0f, 90f));
	}

	private static void BuildThronevaultDataShrine(Node3D parent)
	{
		AddMesh(parent, CreateBox(new Vector3(2.6f, 0.35f, 2.0f), _goldMaterial), new Vector3(0f, 0.18f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.5f, 2.2f, 0.28f), _goldMaterial), new Vector3(0f, 1.25f, -0.72f));
		AddMesh(parent, CreateBox(new Vector3(0.18f, 1.65f, 0.2f), _accentAmberMaterial), new Vector3(-0.45f, 1.35f, -0.53f));
		AddMesh(parent, CreateBox(new Vector3(0.18f, 1.65f, 0.2f), _accentAmberMaterial), new Vector3(0f, 1.35f, -0.5f));
		AddMesh(parent, CreateBox(new Vector3(0.18f, 1.65f, 0.2f), _accentAmberMaterial), new Vector3(0.45f, 1.35f, -0.53f));
		AddMesh(parent, CreateSphere(0.38f, _glassMaterial), new Vector3(0f, 2.5f, -0.55f));
	}

	private static void BuildLuminousMythNode(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(0.85f, 0.24f, _crystalMaterial), new Vector3(0f, 0.12f, 0f));
		AddMesh(parent, CreateCylinder(0.36f, 2.8f, _crystalMaterial, 0.05f), new Vector3(0f, 1.55f, 0f), Vector3.One, new Vector3(0f, 0f, 6f));
		AddMesh(parent, CreateSphere(0.42f, _accentCyanMaterial), new Vector3(0f, 3.1f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.8f, 0.08f, 0.12f), _accentCyanMaterial), new Vector3(0f, 1.55f, 0f), Vector3.One, new Vector3(0f, 0f, 25f));
		AddMesh(parent, CreateBox(new Vector3(1.8f, 0.08f, 0.12f), _accentAmberMaterial), new Vector3(0f, 1.95f, 0f), Vector3.One, new Vector3(0f, 0f, -25f));
	}

	private static void BuildEmberTerraformerCore(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(1.05f, 0.35f, _darkSteelMaterial), new Vector3(0f, 0.18f, 0f));
		AddMesh(parent, CreateSphere(0.72f, _emberMaterial), new Vector3(0f, 1.25f, 0f));
		AddMesh(parent, CreateCylinder(0.18f, 2.2f, _steelMaterial), new Vector3(-0.9f, 1.1f, 0f), Vector3.One, new Vector3(0f, 0f, 22f));
		AddMesh(parent, CreateCylinder(0.18f, 2.2f, _steelMaterial), new Vector3(0.9f, 1.1f, 0f), Vector3.One, new Vector3(0f, 0f, -22f));
		AddMesh(parent, CreateBox(new Vector3(2.2f, 0.12f, 0.16f), _warningMaterial), new Vector3(0f, 2.2f, 0f));
	}

	private static void BuildObsidianTradeBeacon(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(0.75f, 0.25f, _obsidianMaterial), new Vector3(0f, 0.13f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.45f, 2.4f, 0.45f), _obsidianMaterial), new Vector3(0f, 1.3f, 0f));
		AddMesh(parent, CreateBox(new Vector3(2.4f, 0.16f, 0.16f), _goldMaterial), new Vector3(0f, 1.9f, 0f));
		AddMesh(parent, CreateBox(new Vector3(0.16f, 0.16f, 2.4f), _goldMaterial), new Vector3(0f, 1.55f, 0f));
		AddMesh(parent, CreateSphere(0.28f, _signalRedMaterial), new Vector3(0f, 2.65f, 0f));
	}

	private static void BuildVerdantBiospherePod(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(1.1f, 0.25f, _steelMaterial), new Vector3(0f, 0.13f, 0f));
		AddMesh(parent, CreateSphere(0.98f, _bioglassMaterial), new Vector3(0f, 1.2f, 0f), new Vector3(1f, 1.25f, 1f));
		AddMesh(parent, CreateSphere(0.42f, _verdantMaterial), new Vector3(-0.25f, 1.0f, 0.2f), new Vector3(1f, 0.7f, 1f));
		AddMesh(parent, CreateCylinder(0.08f, 1.1f, _verdantMaterial), new Vector3(0.28f, 1.0f, -0.22f), Vector3.One, new Vector3(0f, 0f, 18f));
		AddMesh(parent, CreateSphere(0.18f, _verdantMaterial), new Vector3(0.42f, 1.55f, -0.32f));
	}

	private static void BuildEchoSpiralTimeAnchor(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(0.9f, 0.28f, _darkSteelMaterial), new Vector3(0f, 0.14f, 0f));
		AddMesh(parent, CreateCylinder(0.16f, 2.7f, _steelMaterial), new Vector3(0f, 1.45f, 0f));
		AddMesh(parent, CreateCylinder(1.1f, 0.08f, _riftMaterial), new Vector3(0f, 1.55f, 0f), Vector3.One, new Vector3(90f, 0f, 0f));
		AddMesh(parent, CreateCylinder(0.78f, 0.08f, _accentCyanMaterial), new Vector3(0f, 1.95f, 0f), Vector3.One, new Vector3(90f, 35f, 0f));
		AddMesh(parent, CreateSphere(0.26f, _accentAmberMaterial), new Vector3(0f, 2.45f, 0f));
	}

	private static void BuildFarSilenceProbe(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(0.6f, 0.2f, _voidMaterial), new Vector3(0f, 0.1f, 0f));
		AddMesh(parent, CreateCylinder(0.22f, 2.1f, _obsidianMaterial, 0.12f), new Vector3(0f, 1.15f, 0f));
		AddMesh(parent, CreateSphere(0.36f, _glassMaterial), new Vector3(0f, 2.35f, 0f));
		AddMesh(parent, CreateBox(new Vector3(2.6f, 0.08f, 0.18f), _darkSteelMaterial), new Vector3(0f, 1.45f, 0f), Vector3.One, new Vector3(0f, 25f, 0f));
		AddMesh(parent, CreateBox(new Vector3(2.6f, 0.08f, 0.18f), _darkSteelMaterial), new Vector3(0f, 1.45f, 0f), Vector3.One, new Vector3(0f, -25f, 0f));
	}

	private static void BuildDistressLighthouse(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(0.7f, 0.25f, _darkSteelMaterial), new Vector3(0f, 0.13f, 0f));
		AddMesh(parent, CreateCylinder(0.2f, 2.6f, _steelMaterial), new Vector3(0f, 1.4f, 0f));
		AddMesh(parent, CreateBox(new Vector3(1.1f, 0.18f, 1.1f), _darkSteelMaterial), new Vector3(0f, 2.72f, 0f));
		AddMesh(parent, CreateSphere(0.42f, _signalRedMaterial), new Vector3(0f, 3.05f, 0f));
		AddMesh(parent, CreateCylinder(1.4f, 0.05f, _signalRedMaterial), new Vector3(0f, 3.05f, 0f), Vector3.One, new Vector3(90f, 0f, 0f));
	}

	private static void BuildBrokenStargateArc(Node3D parent)
	{
		AddMesh(parent, CreateCylinder(1.45f, 0.22f, _darkSteelMaterial), new Vector3(0f, 0.11f, 0f));
		AddMesh(parent, CreateCylinder(0.22f, 2.4f, _steelMaterial), new Vector3(-1.2f, 1.25f, 0f), Vector3.One, new Vector3(0f, 0f, -20f));
		AddMesh(parent, CreateCylinder(0.22f, 2.4f, _steelMaterial), new Vector3(1.2f, 1.25f, 0f), Vector3.One, new Vector3(0f, 0f, 20f));
		AddMesh(parent, CreateCylinder(0.22f, 2.35f, _steelMaterial), new Vector3(0f, 2.35f, 0f), Vector3.One, new Vector3(0f, 0f, 90f));
		AddMesh(parent, CreateBox(new Vector3(0.18f, 1.3f, 0.12f), _accentCyanMaterial), new Vector3(-0.52f, 1.65f, 0.02f), Vector3.One, new Vector3(0f, 0f, -35f));
		AddMesh(parent, CreateBox(new Vector3(0.14f, 1.0f, 0.12f), _riftMaterial), new Vector3(0.56f, 1.88f, 0.02f), Vector3.One, new Vector3(0f, 0f, 28f));
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

	private static MeshInstance3D CreateCylinder(float radius, float height, Material material, float? topRadius = null)
	{
		CylinderMesh mesh = new CylinderMesh
		{
			TopRadius = topRadius ?? radius,
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

	private static MeshInstance3D CreateSphere(float radius, Material material)
	{
		SphereMesh mesh = new SphereMesh
		{
			Radius = radius,
			Height = radius * 2f,
			RadialSegments = 24,
			Rings = 12
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
		mesh.SetMeta("material_role", GetMaterialRole(mesh.MaterialOverride));
		parent.AddChild(mesh);
	}

	private static string GetMaterialRole(Material material)
	{
		if (material == _steelMaterial) return "steel";
		if (material == _darkSteelMaterial) return "dark_steel";
		if (material == _accentCyanMaterial) return "accent";
		if (material == _accentAmberMaterial) return "secondary";
		if (material == _warningMaterial) return "warning";
		if (material == _crateMaterial) return "crate";
		if (material == _bedMaterial) return "bed";
		if (material == _glassMaterial) return "glass";
		if (material == _debrisMaterial) return "debris";
		if (material == _outlineMaterial) return "outline";
		if (material == _voidMaterial) return "void";
		if (material == _obsidianMaterial) return "obsidian";
		if (material == _crystalMaterial) return "crystal";
		if (material == _riftMaterial) return "rift";
		if (material == _emberMaterial) return "ember";
		if (material == _verdantMaterial) return "verdant";
		if (material == _bioglassMaterial) return "bioglass";
		if (material == _signalRedMaterial) return "signal";
		if (material == _goldMaterial) return "gold";
		return "steel";
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
		_voidMaterial ??= CreateEmissiveMaterial(new Color(0.05f, 0.03f, 0.10f), 0.8f);
		_obsidianMaterial ??= CreateMaterial(new Color(0.04f, 0.04f, 0.06f), 0.35f, 0.52f);
		_crystalMaterial ??= CreateEmissiveMaterial(new Color(0.58f, 0.86f, 1.00f), 1.5f, 0.62f);
		_riftMaterial ??= CreateEmissiveMaterial(new Color(0.62f, 0.22f, 1.00f), 2.0f, 0.72f);
		_emberMaterial ??= CreateEmissiveMaterial(new Color(1.00f, 0.28f, 0.08f), 2.2f);
		_verdantMaterial ??= CreateEmissiveMaterial(new Color(0.22f, 0.95f, 0.42f), 1.35f);
		_bioglassMaterial ??= CreateEmissiveMaterial(new Color(0.36f, 0.95f, 0.75f), 1.1f, 0.38f);
		_signalRedMaterial ??= CreateEmissiveMaterial(new Color(1.00f, 0.08f, 0.05f), 2.6f);
		_goldMaterial ??= CreateMaterial(new Color(0.78f, 0.58f, 0.24f), 0.4f, 0.35f);
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

	private static StandardMaterial3D CreateSkinMaterial(string role, string skinId)
	{
		if (skinId == "default")
		{
			return GetDefaultMaterial(role);
		}

		(Color baseColor, Color darkColor, Color accentColor, Color secondaryColor, Color warningColor, Color glassColor) = GetSkinColors(skinId);
		return role switch
		{
			"steel" => CreateMaterial(baseColor, 0.2f, 0.62f),
			"dark_steel" => CreateMaterial(darkColor, 0.25f, 0.78f),
			"accent" => CreateEmissiveMaterial(accentColor, 2.0f),
			"secondary" => CreateEmissiveMaterial(secondaryColor, 1.55f),
			"warning" => CreateEmissiveMaterial(warningColor, 1.4f),
			"crate" => CreateMaterial(Darken(baseColor, 0.72f), 0.14f, 0.82f),
			"bed" => CreateMaterial(Lighten(baseColor, 1.7f), 0.04f, 0.46f),
			"glass" => CreateEmissiveMaterial(glassColor, 1.8f, 0.45f),
			"debris" => CreateMaterial(Darken(secondaryColor, 0.42f), 0.06f, 0.95f),
			"void" => CreateEmissiveMaterial(Darken(darkColor, 0.5f), 0.85f),
			"obsidian" => CreateMaterial(Darken(darkColor, 0.55f), 0.38f, 0.5f),
			"crystal" => CreateEmissiveMaterial(glassColor, 1.7f, 0.62f),
			"rift" => CreateEmissiveMaterial(secondaryColor, 2.15f, 0.72f),
			"ember" => CreateEmissiveMaterial(warningColor, 2.35f),
			"verdant" => CreateEmissiveMaterial(accentColor, 1.45f),
			"bioglass" => CreateEmissiveMaterial(glassColor, 1.2f, 0.38f),
			"signal" => CreateEmissiveMaterial(warningColor, 2.75f),
			"gold" => CreateMaterial(secondaryColor, 0.42f, 0.34f),
			_ => CreateMaterial(baseColor, 0.2f, 0.62f)
		};
	}

	private static StandardMaterial3D GetDefaultMaterial(string role)
	{
		return role switch
		{
			"steel" => _steelMaterial,
			"dark_steel" => _darkSteelMaterial,
			"accent" => _accentCyanMaterial,
			"secondary" => _accentAmberMaterial,
			"warning" => _warningMaterial,
			"crate" => _crateMaterial,
			"bed" => _bedMaterial,
			"glass" => _glassMaterial,
			"debris" => _debrisMaterial,
			"void" => _voidMaterial,
			"obsidian" => _obsidianMaterial,
			"crystal" => _crystalMaterial,
			"rift" => _riftMaterial,
			"ember" => _emberMaterial,
			"verdant" => _verdantMaterial,
			"bioglass" => _bioglassMaterial,
			"signal" => _signalRedMaterial,
			"gold" => _goldMaterial,
			_ => _steelMaterial
		};
	}

	private static (Color Base, Color Dark, Color Accent, Color Secondary, Color Warning, Color Glass) GetSkinColors(string skinId)
	{
		return skinId switch
		{
			"aetherweb" => (new Color(0.16f, 0.23f, 0.31f), new Color(0.04f, 0.08f, 0.13f), new Color(0.16f, 0.92f, 1.00f), new Color(0.58f, 0.40f, 1.00f), new Color(0.96f, 0.68f, 0.20f), new Color(0.22f, 0.86f, 1.00f)),
			"obsidian_belt" => (new Color(0.10f, 0.10f, 0.13f), new Color(0.02f, 0.02f, 0.04f), new Color(0.95f, 0.08f, 0.06f), new Color(0.82f, 0.58f, 0.24f), new Color(1.00f, 0.18f, 0.08f), new Color(0.65f, 0.70f, 0.82f)),
			"ember_wastes" => (new Color(0.28f, 0.18f, 0.12f), new Color(0.08f, 0.04f, 0.03f), new Color(1.00f, 0.34f, 0.08f), new Color(0.95f, 0.64f, 0.14f), new Color(1.00f, 0.12f, 0.03f), new Color(1.00f, 0.55f, 0.20f)),
			"verdant_shroud" => (new Color(0.16f, 0.25f, 0.21f), new Color(0.03f, 0.10f, 0.07f), new Color(0.22f, 0.96f, 0.42f), new Color(0.10f, 0.80f, 0.68f), new Color(0.86f, 0.96f, 0.28f), new Color(0.45f, 1.00f, 0.72f)),
			"luminous_verge" => (new Color(0.30f, 0.34f, 0.48f), new Color(0.08f, 0.08f, 0.18f), new Color(0.64f, 0.92f, 1.00f), new Color(1.00f, 0.84f, 0.42f), new Color(0.90f, 0.96f, 1.00f), new Color(0.72f, 0.88f, 1.00f)),
			"echo_rift" => (new Color(0.18f, 0.12f, 0.28f), new Color(0.04f, 0.02f, 0.09f), new Color(0.96f, 0.22f, 1.00f), new Color(0.18f, 0.92f, 1.00f), new Color(1.00f, 0.18f, 0.42f), new Color(0.70f, 0.44f, 1.00f)),
			_ => (new Color(0.20f, 0.24f, 0.29f), new Color(0.12f, 0.15f, 0.19f), new Color(0.20f, 0.90f, 1.00f), new Color(0.95f, 0.65f, 0.18f), new Color(0.95f, 0.72f, 0.15f), new Color(0.18f, 0.82f, 0.94f))
		};
	}

	private static Color Darken(Color color, float amount)
	{
		return new Color(color.R * amount, color.G * amount, color.B * amount, color.A);
	}

	private static Color Lighten(Color color, float amount)
	{
		return new Color(Mathf.Min(color.R * amount, 1f), Mathf.Min(color.G * amount, 1f), Mathf.Min(color.B * amount, 1f), color.A);
	}
}
