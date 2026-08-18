# Mission Workbench v2

Open `res://mission_workbench_v2.tscn` to author missions. The legacy builder remains at `res://mission_scene_builder.tscn` and links back to v2.

## Data flow

1. Legacy layout JSON and `MissionTemplate` resources are imported once.
2. The versioned source document is saved under `res://Data/Missions/Workbench/<mission>.mission.json`.
3. **Save Source** writes only the v2 document.
4. **Validate** reports structural and semantic problems. Activating a problem focuses its map element.
5. **Compile** writes the deterministic runtime layout configured by the mission template. Compilation stops on errors.
6. **Playtest** compiles to `user://mission_workbench_v2/compiled/` and launches `MissionMap` with a one-use layout override.

## Map controls

- Choose an asset and left-click to place it.
- Activate the selection tool or press `Esc`, then click or drag an element.
- Arrow keys nudge the selection.
- `Delete` removes the selection.
- `Ctrl+D` duplicates the selection.
- `Ctrl+Z` / `Ctrl+Y` undo and redo.
- `Ctrl+S` saves the source document.
- Middle mouse pans and the wheel zooms.

## Verification

Run `res://mission_workbench_v2_migrate.tscn` headlessly to verify every registered mission. It protects existing v2 source files, checks source serialization, exercises undo/redo, compiles to `user://`, and semantically re-imports the result.

Run `res://mission_workbench_v2_playtest_smoke.tscn` to verify the compiled-draft handoff through the strict runtime loader.
