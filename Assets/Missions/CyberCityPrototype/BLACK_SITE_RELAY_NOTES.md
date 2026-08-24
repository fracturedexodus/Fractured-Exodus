# Black Site Relay prototype mapping

The playable prototype in `res://cybercity_prototype.tscn` recreates the live
mission layout referenced by `res://black_site_relay.tscn` and
`res://Data/Missions/Templates/black_site_relay.tres`.

- 111 source floor cells across the full 18 by 20 coordinate bounds
- all 8 bulkhead door cells
- officer insertion cells at `(7, 19)` and `(8, 19)`
- survivor pods at `(0, 13)` and archive core at `(10, 0)`
- medical beds at `(15, 7)`, `(15, 8)`, and `(15, 9)`
- evac at `(16, 8)` and the dialogue uplink at `(5, 18)`
- Custodian Sentry, Scavenger Brute, Scavenger Raider, and relay ambusher spawns

The prototype preserves the mission's mutually exclusive survivor/archive route
choice in its HUD. Mission-state interactions and combat AI remain presentation
stubs; movement, directional animation, attack preview, selection, and zoom are
live.
