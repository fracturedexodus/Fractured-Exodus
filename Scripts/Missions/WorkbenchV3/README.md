# Mission Workbench v3

Run `res://mission_workbench_v3.tscn` to author missions through the beginner-facing workflow.

## Author workflow

1. **Mission Details** — name the mission, write the briefing and objective, and choose the environment.
2. **Build Map** — place thumbnail assets, paint floors, draw rectangles, or stamp complete rooms.
3. **Mission Events** — create readable When → Then rules. Workbench generates internal IDs and flags.
4. **Conversations** — write dialogue moments, player responses, branches, event conditions, and effects.
5. **Test & Publish** — resolve friendly live checks, test a temporary draft, then publish the runtime layout.

Draft source and conversations autosave. **Test Mission** validates and launches a temporary compiled layout. **Publish Mission** validates and writes the registered runtime layout.

## Creating a completely new mission

Use **+ New Mission** in the top toolbar. Enter a name, briefing, objective, starter layout, and officer count. Workbench creates and saves:

- `Data/Missions/Templates/<mission-id>.tres` — the registered mission template;
- `Data/Missions/Workbench/<mission-id>.mission.json` — the editable source;
- `Data/MissionLayouts/<mission-id>_builder.json` — the initial runtime layout;
- `mission:<mission-id>` — the stable interaction key used by overworld placement.

Open **Edit Overworld Placement**, create a placement definition, and choose the mission from **Choose Registered Mission**. Creation never overwrites an existing mission or file.

## Compatibility

Workbench v3 uses the existing `MissionDocument`, validator, compiler, and playtest handoff. Beginner-facing rules are stored in `MissionDocument.Authoring` and translated into the established runtime element logic during save, test, and publish. Existing schema-v2 source files load with an empty authoring section and remain usable in Workbench v2.

Technical placement and resource fields remain available under **Advanced placement** in the selected-object editor for recovery work.

## Verification

Build the C# solution, then run the scene headlessly:

```powershell
dotnet build Fractured_Exodus.sln --no-restore
Godot.exe --headless --path . --quit-after 8 mission_workbench_v3.tscn
Godot.exe --headless --path . mission_workbench_v3_new_mission_smoke.tscn
```

The new-mission smoke creates a uniquely named temporary mission, verifies template/source/layout persistence and overworld validation, then removes only those temporary files.
