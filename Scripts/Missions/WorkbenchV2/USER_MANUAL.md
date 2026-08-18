# Mission Workbench v2 User Manual

Mission Workbench v2 is the mission-authoring tool for Fractured Exodus. It lets you edit the physical map, review mission flow, maintain dialogue text, validate the mission, compile it into the runtime layout, and launch a playtest.

## Opening and exiting

Open `res://mission_workbench_v2.tscn` in Godot and run the scene. You can also open it from the legacy Mission Scene Builder with **Open Workbench v2**.

Use **Exit** in the upper-right corner or press **Ctrl+Q** to close the Workbench. If the mission source or open dialogue editor has unsaved changes, the Workbench asks before discarding them.

## Screen layout

The top toolbar contains the mission selector, mode selector, document commands, testing tools, manual, and exit control.

- **Asset Palette** on the left contains floor, wall, prop-tile, and marker assets.
- The center is the active Map, Flow, or Dialogue workspace.
- **Context Inspector** on the right edits the selected map element.
- **Problems / Output** at the bottom reports validation, compilation, and save results.

## Recommended workflow

1. Select the mission from the top toolbar.
2. Choose **Map** and build or adjust the layout.
3. Configure each important element in the Context Inspector.
4. Use **Flow** to review triggers, objectives, spawns, and outcomes.
5. Use **Dialogue** to review or edit referenced conversation text.
6. Click **Validate** and resolve every error. Review warnings intentionally.
7. Click **Save Source**.
8. Click **Playtest** to test a compiled draft without replacing the live layout.
9. When the mission is ready, click **Compile** to publish its runtime layout.

## Map mode

### Placing elements

Select an asset in the left palette, then left-click a cell in the center map. Newly placed doors receive a generated target ID. Newly placed markers receive appropriate starter logic that can be refined in the inspector.

Use **Selection Tool** or press **Esc** when you want to stop placing assets.

### Selecting and moving

- Left-click an existing element to select it.
- Drag a selected element to another cell.
- Use the arrow keys for one-cell nudges.
- If several elements share a cell, markers and props are selected before walls and floors.

### Duplicating and deleting

- **Ctrl+D** duplicates the selection one cell diagonally from its original location.
- **Delete** removes the selected element.
- Right-click an element to select and delete it in one action.

All placement, movement, inspector edits, duplication, and deletion participate in undo/redo.

### Camera controls

- Drag with the middle mouse button to pan.
- Use the mouse wheel to zoom.

## Context Inspector

Select a map element to edit its authored data. Changes are not applied until you click **Apply Changes**.

- **Column / Row**: logical map cell.
- **Offset X / Offset Y**: visual adjustment inside the cell.
- **Rotation**: sprite rotation in degrees.
- **Label**: author-facing or runtime display label.
- **Target ID**: dialogue, door, NPC, loot preset, or other logic target.
- **Logic Role**: semantic role used by mission flow and runtime behavior.
- **Required Flags**: comma-separated flags that gate the interaction.
- **Set Flags**: comma-separated flags produced by the interaction.
- **Prop Definition**: `res://` path to a `PropDefinition` resource.
- **NPC Definition**: `res://` path to a `MissionNpcDefinition` resource.
- **Notes**: authoring notes.
- **Trigger Mode**: `none`, `enter`, or `interact`.
- **One Shot**: prevents repeat activation when supported by the runtime behavior.

Keep target IDs stable after other mission logic begins referring to them. Door IDs must be unique within a mission.

## Flow mode

Flow mode presents a mission-level view of authored map logic.

- Spawn, trigger, interaction, and objective nodes are generated from map elements.
- Outcome nodes show the flags required or blocked by each result.
- Click a map-backed node to select its element in the inspector.
- Drag nodes into a readable arrangement. Their positions are stored when you save the source document.

Flow nodes mirror map logic. Edit their functional values through the selected element's Context Inspector.

## Dialogue mode

Dialogue mode lists conversations referenced by mission metadata, map targets, props, and NPC definitions.

1. Select a conversation in the first column.
2. Select a node in the second column.
3. Edit the speaker and dialogue text.
4. Click **Save Dialogue**.

**Add Node** creates a basic new node. **Delete Node** removes the selected node, but the final remaining node cannot be deleted.

Dialogue is stored in the shared conversation JSON files, not inside the mission source document. The current editor changes node speaker/text and basic node membership. Dialogue options, branches, requirements, and flag effects should still be maintained carefully in the conversation JSON when those fields need to change.

## Save, validate, compile, and playtest

### Save Source

**Save Source** writes the editable v2 mission document to:

`res://Data/Missions/Workbench/<mission-id>.mission.json`

Shortcut: **Ctrl+S**.

Saving the source does not change the runtime layout.

### Validate

**Validate** checks structural and semantic requirements, including:

- valid background, tile, marker, prop, NPC, and dialogue references;
- unique stable element and door IDs;
- required officer spawn markers;
- floor availability and approximate objective/evacuation reachability;
- NPC alignment for friendly and hostile spawns;
- elements placed away from floor cells;
- required outcome or interaction flags that nothing appears to produce.

Double-click a problem associated with a map element to select and center that element. Errors block compilation. Warnings do not block compilation, but should be reviewed.

### Playtest

**Playtest** validates and compiles a temporary draft under `user://mission_workbench_v2/compiled/`, then launches the real MissionMap using that draft once.

Playtest does not replace the mission's published runtime layout. Save the source first if you want the authored document to retain your changes.

### Compile

**Compile** validates the current document and writes deterministic legacy-format JSON to the layout path configured by the mission template. This updates the runtime layout used by the game.

Use Compile only when you intend to publish the current Workbench document. Validation errors prevent the write.

## Undo and redo

- **Ctrl+Z**: undo.
- **Ctrl+Y** or **Ctrl+Shift+Z**: redo.

The toolbar describes the next undo or redo operation. Saving does not erase the command history, but loading another mission does.

## Legacy Builder

**Legacy Builder** opens the original Mission Scene Builder. It remains available for comparison and recovery while v2 is adopted.

Save your v2 source before switching builders. The two tools use different authoring formats until v2 is compiled.

## Overworld Content Registry

Use **Overworld Registry** when you need to control where and when the mission appears on the system map. Mission Workbench owns the interior layout and mission logic; the Overworld Content Registry owns planet/outpost assignment, region eligibility, campaign flags, uniqueness, and spawn probability.

## Troubleshooting

### Compilation is blocked

Run **Validate** and resolve all entries marked `ERROR`. Double-click element-specific errors to locate the responsible map object.

### A dialogue conversation is missing

Confirm that the target ID matches a file and `conversation_id` under `res://Data/Dialogue/Conversations/`.

### An objective is unreachable

Confirm there is a connected four-directional chain of floor cells from `spawn_a` to the objective or evacuation marker. The validator intentionally uses a conservative floor-connectivity check.

### A required flag is never produced

Check map-element **Set Flags**, referenced prop/NPC definitions, and dialogue node or option effects. If the flag comes from unsupported custom runtime code, document the exception before accepting the warning.

### Changes appear in Playtest but not the normal game

Playtest uses a temporary compiled draft. Click **Compile** when you are ready to update the published runtime layout.

## Keyboard reference

| Shortcut | Action |
|---|---|
| Ctrl+S | Save source document |
| Ctrl+Z | Undo |
| Ctrl+Y / Ctrl+Shift+Z | Redo |
| Ctrl+D | Duplicate selected map element |
| Delete | Delete selected map element |
| Arrow keys | Nudge selected map element |
| Esc | Activate selection tool |
| Ctrl+Q | Exit Workbench |
