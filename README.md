# Fractured Exodus

`Fractured Exodus` is a Godot 4 C# tactical space game focused on fleet exploration, system travel, ship upgrades, outpost trading, and turn-based combat.

## Overview

The player commands a small fleet moving between star systems through ancient stargates. Moment-to-moment play mixes:

- exploration across a hex map
- scanning and salvaging planets
- buying, equipping, and selling ship gear at outposts
- fleet management and repairs
- tactical combat against hostile fleets

The codebase is currently being cleaned up from a prototype-style root layout into a more structured Godot project while preserving existing gameplay behavior.

## Tech

- Godot `4.6`
- C#
- .NET `8.0`

Project entry and engine settings live in [project.godot](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/project.godot:1).

## Main Game Files

- [main_menu.tscn](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/main_menu.tscn:1): main menu scene
- [galactic_map.tscn](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/galactic_map.tscn:1): sector / star map scene
- [exploration_battle.tscn](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/exploration_battle.tscn:1): exploration and combat scene
- [BattleMap.cs](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/BattleMap.cs:1): primary gameplay scene controller
- [GlobalData.cs](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/GlobalData.cs:1): campaign state singleton

## Project Layout

- [Scripts/Core](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Scripts/Core): shared constants and lightweight data helpers
- [Scripts/Managers](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Scripts/Managers): combat and map-side managers
- [Scripts/Services](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Scripts/Services): gameplay services such as inventory, jumps, save/load, exploration actions, and distress events
- [Data](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Data): data-driven content such as the equipment catalog
- [Assets](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Assets): organized UI, background, outpost, and source-art assets
- [Ships](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Ships): player ship art and related resources
- [EnemyShips](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/EnemyShips): enemy ship art
- [Planets](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Planets): planet textures
- [Sounds](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Sounds): music and sound effects

## Running The Project

1. Open the folder in Godot 4.6 or newer.
2. Let Godot reimport assets if prompted.
3. Run the main scene configured in `project.godot`.

## Current Notes

- Save/load has been split into a dedicated save service plus DTO-based save schema.
- Equipment definitions are loaded from [equipment_catalog.json](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/Data/equipment_catalog.json:1).
- Several gameplay systems have been extracted from `BattleMap` into focused services, but `BattleMap.cs` is still the largest remaining gameplay hotspot.

## License

See [LICENSE](/C:/Users/rembe/.codex/worktrees/c449/fractured-exodus/LICENSE:1).
