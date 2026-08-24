# CyberCity prototype asset source

The PNG files in `Tiles/` are a deliberately small runtime selection copied from
the locally purchased **PVGames Cyber City Core Tiles** pack.

- Original local library: `Assets/cybercitycoretiles/`
- Prototype scene: `res://cybercity_prototype.tscn`
- Usage: in-project commercial or non-commercial game content
- Restriction: do not redistribute the original or edited source assets as an
  asset pack

The full source library is intentionally ignored by Git and Godot-facing project
work. Add future runtime selections to this curated folder instead of referencing
the complete source library directly.

## Black Site Relay selection

The mission reconstruction adds a ground material, two bed styles, two terminals,
an archive generator, two bulkhead orientations, and a ceiling light. These are
used to distinguish the survivor, archive, medical/evac, and insertion wings while
the map topology remains a direct transcription of
`Data/MissionLayouts/black_site_relay_builder.json`.
