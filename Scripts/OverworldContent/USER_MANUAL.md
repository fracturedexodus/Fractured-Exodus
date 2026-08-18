# Overworld Content Registry User Manual

The Overworld Content Registry is the world-placement companion to Mission Workbench v2.

- **Mission Workbench v2** authors what happens inside a mission.
- **Overworld Content Registry** authors where and when missions or ambient events appear on the system overworld.

Open `res://overworld_content_registry.tscn` to review or edit placement definitions.

## Core workflow

1. Select a registered definition on the left.
2. Edit its placement fields.
3. Click **Validate**.
4. Resolve every error and review warnings.
5. Click **Save** or press **Ctrl+S**.
6. Test the campaign or run `res://overworld_content_registry_smoke.tscn` headlessly.

The registry reloads definitions from `res://Data/OverworldContent/`. Files use the suffix `.overworld.json` and schema version 1.

## Toolbar

- **New** creates an unsaved placement definition.
- **Save** validates and writes the selected definition.
- **Reload** discards unsaved edits and reloads all files.
- **Validate** checks the current in-memory registry.
- **Mission Workbench** opens Mission Workbench v2.
- **Manual** opens this guide.
- **Exit** closes the registry. Unsaved changes require confirmation.

Shortcuts: **Ctrl+S** saves and **Ctrl+Q** exits.

## Definition fields

### Identity and content

- **Content ID** is the unique stable ID of this placement definition. It is not the mission ID or ambient-event ID.
- **Display Name** is the author-facing name shown in this registry.
- **Content Type** is `Mission` or `AmbientEvent`.
- **Content Reference** connects placement to playable content:
  - Mission definitions use a mission template interaction key such as `planet:black_site_relay`.
  - Ambient events use an event ID such as `pilgrim_beacons`.
- **Enabled** controls whether runtime assignment considers the definition.
- **Priority** determines evaluation order. Higher numbers run first; ties are resolved by Content ID.

### Placement target

- **Planet** assigns a mission interaction key to an eligible planet.
- **Outpost** assigns a mission interaction key to an eligible outpost.
- **FreeHex** creates an ambient-event instance on an empty system-map hex.

Mission placement currently supports Planet and Outpost. Ambient events currently use FreeHex.

### Scope and probability

- **Campaign** permits one assignment across all explored systems.
- **System** permits one assignment in each eligible system.
- **Regions** is a comma-separated allowlist. Leave it empty to allow every region.
- **Spawn Chance** is a value from 0 to 1.
- **Guarantee First Eligible Placement** skips the chance roll until the first copy has been assigned somewhere in the campaign. Later systems use Spawn Chance.

Assignments are persistent and idempotent. Reloading a system does not create duplicates.

### Planet and outpost targeting

- **Avoid Starting Planet** prefers a different planet from the campaign's saved starting planet.
- **Preferred Outpost Sprite Contains** prefers an outpost whose sprite path contains the supplied text.
- **Allow Fallback Target** permits the first otherwise eligible node when no preferred target exists.

The runtime never replaces a different mission already assigned to a planet or outpost.

### Free-hex targeting

- **Minimum Free-Hex Radius** and **Maximum Free-Hex Radius** constrain distance from the system star.

Free-hex placement avoids occupied hexes and reserved outpost positions.

### Campaign flags

- **Required Story Flags** must all exist before placement is eligible.
- **Blocked Story Flags** prevent placement if any are present.

Enter flags as comma-separated stable IDs.

## Validation

The registry validates:

- schema version and unknown JSON properties;
- unique content IDs;
- known region names;
- spawn chance and radius ranges;
- required/blocked flag conflicts;
- mission interaction keys against enabled `MissionTemplate` resources;
- ambient-event IDs against `AmbientEventRegistry`;
- supported content type and world-node combinations;
- duplicate enabled placement references.

Definitions with errors are skipped by runtime assignment. Errors are also written to the Godot log once when assignment begins.

## Starter definitions

- **Black Site Relay placement:** campaign-unique planet mission, guaranteed at the first eligible system and avoiding the starting planet when possible.
- **Smuggler Exchange placement:** campaign-unique outpost mission, preferring a black-market asteroid exchange.
- **Pilgrim Beacons placement:** system-unique Luminous Verge ambient event, guaranteed once and then using a 35% chance in later eligible systems.

## Adding a mission to the overworld

1. Build and compile the mission in Mission Workbench v2.
2. Add an interaction key to its `MissionTemplate` resource.
3. Create a Mission entry in this registry.
4. Set Content Reference to that exact interaction key.
5. Choose Planet or Outpost placement.
6. Configure region, scope, chance, target preferences, and flags.
7. Validate and save.

No `MapSpawner.cs` change should be necessary.

## Adding an ambient event

1. Add the event JSON anywhere under `res://Data/AmbientEvents/`; `AmbientEventRegistry` scans the directory recursively.
2. Create an AmbientEvent registry entry.
3. Set Content Reference to the event's `event_id`.
4. Choose FreeHex placement.
5. Configure regions, scope, chance, radius, and campaign flags.
6. Validate and save.

## Save compatibility

Planet mission keys, outpost mission keys, and ambient-event instances already live in campaign save data. Registry placement only fills eligible unassigned locations. Existing assignments are preserved.

Legacy saves that use `IsBlackSiteRelaySite` without an interaction key are upgraded in memory to `planet:black_site_relay` during assignment.
