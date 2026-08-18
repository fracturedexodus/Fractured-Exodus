using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class OverworldContentRegistrySmoke : Node
{
	public override void _Ready()
	{
		OverworldContentRegistry.Reload();
		List<OverworldContentIssue> errors = OverworldContentRegistry.GetIssues().Where(issue => issue.Severity == OverworldContentIssueSeverity.Error).ToList();
		if (errors.Count > 0) Fail(1, string.Join(" | ", errors.Select(issue => issue.Message)));
		if (OverworldContentRegistry.GetIssues().Count > 0) Fail(11, "Starter registry definitions should validate without warnings.");
		if (OverworldContentRegistry.GetDefinitions().Count != 3) Fail(2, "Expected three starter registry definitions.");

		GlobalData campaign = BuildCampaign();
		SystemData system = campaign.ExploredSystems[campaign.SavedSystem];
		Random rng = new Random(101);
		OverworldContentAssignmentService.AssignNodeContent(campaign, system, rng);
		if (system.Planets.Count(planet => planet.MissionInteractionKey == "planet:black_site_relay") != 1) Fail(3, "Black Site was not assigned exactly once.");
		if (system.Planets.First(planet => planet.MissionInteractionKey == "planet:black_site_relay").Name == campaign.SavedPlanet) Fail(4, "Black Site ignored avoid-starting-planet placement.");
		OutpostData exchange = system.Outposts.FirstOrDefault(outpost => outpost.MissionInteractionKey == "outpost:smuggler_exchange");
		if (exchange == null || !exchange.SpritePath.Contains("BlackMarketAsteroidExchangeSprite")) Fail(5, "Smuggler Exchange did not prefer the black-market outpost.");

		Dictionary<Vector2I, Node2D> grid = BuildGrid(20);
		Dictionary<Vector2I, MapEntity> contents = new Dictionary<Vector2I, MapEntity> { [Vector2I.Zero] = new MapEntity { Type = GameConstants.EntityTypes.CelestialBody } };
		OverworldContentAssignmentService.AssignFreeHexContent(campaign, system, rng, 20, grid, contents);
		AmbientEventInstanceData beacon = system.AmbientEvents.SingleOrDefault(instance => instance.EventId == "pilgrim_beacons");
		if (beacon == null) Fail(6, "Guaranteed first Pilgrim Beacons event was not assigned.");
		if (HexMath.HexDistance(Vector2I.Zero, beacon.HexPosition) < 7 || HexMath.HexDistance(Vector2I.Zero, beacon.HexPosition) > 14) Fail(7, "Ambient event was placed outside its configured radius.");
		if (system.Outposts.Any(outpost => outpost.HexPosition == beacon.HexPosition)) Fail(8, "Ambient event overlapped an outpost.");

		OverworldContentAssignmentService.AssignNodeContent(campaign, system, rng);
		OverworldContentAssignmentService.AssignFreeHexContent(campaign, system, rng, 20, grid, contents);
		if (system.Planets.Count(planet => !string.IsNullOrWhiteSpace(planet.MissionInteractionKey)) != 1
			|| system.Outposts.Count(outpost => !string.IsNullOrWhiteSpace(outpost.MissionInteractionKey)) != 1
			|| system.AmbientEvents.Count(instance => instance.EventId == "pilgrim_beacons") != 1) Fail(9, "Assignment was not idempotent.");

		GlobalData legacyCampaign = new GlobalData();
		SystemData legacySystem = new SystemData { SystemName = "Legacy" };
		legacySystem.Planets.Add(new PlanetData { Name = "Legacy Relay", IsBlackSiteRelaySite = true });
		legacyCampaign.ExploredSystems[legacySystem.SystemName] = legacySystem;
		legacyCampaign.SavedSystem = legacySystem.SystemName;
		OverworldContentAssignmentService.AssignNodeContent(legacyCampaign, legacySystem, new Random(2));
		if (legacySystem.Planets[0].MissionInteractionKey != "planet:black_site_relay") Fail(10, "Legacy Black Site marker was not migrated.");

		campaign.Free();
		legacyCampaign.Free();
		GD.Print("OVERWORLD_CONTENT_REGISTRY_SMOKE_OK definitions=3 missions=2 ambient_events=1");
		GetTree().Quit(0);
	}

	private static GlobalData BuildCampaign()
	{
		GlobalData campaign = new GlobalData { SavedSystem = "Verge Test", SavedPlanet = "Verge Test Prime" };
		campaign.CurrentSectorStars.Add(new StarMapData { SystemName = campaign.SavedSystem, Region = "Luminous Verge" });
		SystemData system = new SystemData { SystemName = campaign.SavedSystem };
		system.Planets.Add(new PlanetData { Name = campaign.SavedPlanet });
		system.Planets.Add(new PlanetData { Name = "Verge Test Secundus" });
		system.Outposts.Add(new OutpostData { Name = "Scrapper Hub", HexPosition = new Vector2I(8, 0), SpritePath = "res://Assets/Outposts/ScrappersFurnaceHubSprite.png" });
		system.Outposts.Add(new OutpostData { Name = "Black Market", HexPosition = new Vector2I(9, -1), SpritePath = "res://Assets/Outposts/BlackMarketAsteroidExchangeSprite.png" });
		campaign.ExploredSystems[system.SystemName] = system;
		return campaign;
	}

	private static Dictionary<Vector2I, Node2D> BuildGrid(int radius)
	{
		Dictionary<Vector2I, Node2D> grid = new Dictionary<Vector2I, Node2D>();
		for (int q = -radius; q <= radius; q++)
		{
			int minimumR = Math.Max(-radius, -q - radius);
			int maximumR = Math.Min(radius, -q + radius);
			for (int r = minimumR; r <= maximumR; r++) grid[new Vector2I(q, r)] = null;
		}
		return grid;
	}

	private void Fail(int code, string message)
	{
		GD.PushError($"OVERWORLD_CONTENT_REGISTRY_SMOKE_FAIL {message}");
		GetTree().Quit(code);
		throw new InvalidOperationException(message);
	}
}
