# Overworld Content Registry

Open `res://overworld_content_registry.tscn` to author data-driven overworld placement. Definitions live under `res://Data/OverworldContent/` with the suffix `.overworld.json`.

Runtime flow:

1. `OverworldContentRegistry` strictly loads and validates placement definitions.
2. `OverworldContentAssignmentService` evaluates region, flags, scope, probability, and target rules in stable priority order.
3. `MapSpawner` copies mission keys onto planets/outposts and creates persistent ambient-event instances on free hexes.
4. Existing save DTOs persist the resulting assignments.

Run `res://overworld_content_registry_smoke.tscn` headlessly to verify registry references, deterministic placement constraints, campaign uniqueness, idempotency, and legacy Black Site migration.

See `USER_MANUAL.md` for authoring instructions.
