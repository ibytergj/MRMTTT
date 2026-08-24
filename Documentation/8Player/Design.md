# 8-Player Tabletop — Design

This document describes the design of the 8-player extension to Unity's MR
Multiplayer Tabletop Template V2. It records the decisions the implementation
follows; [Testing.md](Testing.md) records how to validate them.

The layer's goals, in priority order:

1. Correct behavior with up to 8 networked players (join, seat, expand,
   collapse, leave, late-join).
2. A minimal, reviewable diff against the pristine V2 template, suitable for an
   upstream PR to Unity. New functionality lives in additive files; touched
   template files each need a one-line justification.
3. A clean, game-agnostic layer that downstream games (e.g. Pegs and Jokers)
   configure through assets, not code edits.

## Decisions

| Topic | Decision |
|---|---|
| Table sizing | Fully dynamic, config-driven. The table supports any seat count 3–8 (shape, rim segments, seats, seat hover visuals, and UI all follow), growing with occupancy. A per-game `TableLayoutConfig` asset owns the behavior. The template's default asset is `{4}`, so stock behavior is unchanged. |
| Seat geometry | Authored presets for 4 and 8 seats keep the established orderings (see the seat table below). Counts without a preset use a regular polygon at `i·360/N`, clockwise. Seat positions are always **derived** from the seat's yaw (`-forward × radius`), never hand-placed. Angular order for the rim is derived from the seat transforms; there are no hand-written remap tables in C# or shader code. |
| Color model | **Player color == seat color.** Each seat starts with a default rim color from the palette; avatar, name tag, seat button, and rim always agree. The per-seat palette is networked (host-authoritative, replicated), so late joiners see current state. |
| Palette | A `PlayerColorPalette` ScriptableObject (8 colors). The template default keeps the template pastels for seats 1–4 and adds 4 distinct colors (including black and white). Games ship their own asset; the host can edit colors at game start. |
| Black / white | Supported via a contrast-adaptive outline on each rim segment and seat hover visual (black segment → white border, white segment → black border, chosen by luminance). Glow color is derived by the same rule. There is no "white == unassigned" sentinel. |
| Active player | The seat with the animated highlight texture. Starts as the **host's seat**; games move it with the server API `SetActiveSeat(int)`. |
| Shader | The per-seat rim is integrated into the template's own `VirtualSurfaceShader` behind a `_SeatColors` Boolean material property (default off → pixel-identical to the template). The rim HLSL lives in a real `.hlsl` include file. |

## Seat geometry

Base radius 0.75 m. Seat *i*'s local position is derived from its yaw:
`localRotation = Euler(0, yaw, 0)`, `localPosition = -forward × radius`.

Preset yaws (degrees, clockwise), keeping the template's opposite-pair fill
order for the first four seats:

| Seat | 4-seat preset | 8-seat preset |
|---|---|---|
| 0 | 0 | 0 |
| 1 | 180 | 180 |
| 2 | 270 | 270 |
| 3 | 90 | 90 |
| 4 | — | 45 |
| 5 | — | 135 |
| 6 | — | 225 |
| 7 | — | 315 |

See [8PlayerSeatingDiagram.svg](8PlayerSeatingDiagram.svg).

Counts without a preset place seat *i* at yaw `i·360/N` clockwise.

Table scale per count = `radius(N)/radius(4)`, from the constant-seat-spacing
formula `sin(π/4)/sin(π/N)` — unless the preset pins a scale. The 8-seat preset
pins ×2 (the formula gives ×1.85) to preserve the established look. Seats 4–7
use a per-seat distance override (≈0.99 m) so they sit near the corners of the
scaled table.

## TableLayoutConfig (per-game contract)

`Scripts/Table/TableLayoutConfig.cs`, a ScriptableObject:

- `int[] supportedSeatCounts` — sorted, each 3–8.
- `CollapsePolicy collapsePolicy` — see below.
- `List<SeatLayoutPreset> presets` — `seatCount`, `float[] yaws`, optional `scale`.
- `float baseRadius = 0.75`.
- `ScaleFor(n)` — preset override, else the constant-seat-spacing formula.
- `TargetSeatCount(occupiedMask, current)` — pure function implementing growth
  and all three collapse policies (EditMode-tested).

Growth is always immediate on join: the layout becomes the smallest supported
count ≥ occupancy. Shrinking is governed by `CollapsePolicy`:

- `Never` — the table never shrinks mid-session; a vacated seat stays open for
  the next joiner. Layout resets only when the session ends.
- `WhenRemainingFit` — shrink to the smallest supported count only when every
  remaining player already sits at an index below it (nobody has to move);
  otherwise keep the hole.
- `Immediate` — on every leave, shrink to the smallest supported count; the
  server first moves any player seated at an index ≥ the new count to the
  lowest free seat, then applies the layout.

Shipped assets (`Assets/MRTabletopAssets/Settings/`):

| Asset | supportedSeatCounts | collapsePolicy | Purpose |
|---|---|---|---|
| `TableLayout_Default` | `{4}` | `Never` | Template default — stock behavior |
| `TableLayout_4or8` | `{4, 8}` | `Never` | Pegs and Jokers (4↔8 latch) |
| `TableLayout_Dynamic` | `{3..8}` | `WhenRemainingFit` | Demos / testing |

`NetworkTableTopManager` references one asset; a game swaps the asset to change
behavior and seat positions without touching code.

## Layout data flow

Single replicated layout state: `NetworkVariable<int> m_SeatCount` on
`NetworkTableTopManager`.

- **Server**, after every occupancy change (assign, leave, disconnect), runs
  `ServerUpdateSeatLayout()`: `target = config.TargetSeatCount(...)`; on
  collapse it first re-seats players at index ≥ target via the normal
  `ServerAssignSeat` path, then sets `m_SeatCount.Value = target`. Seat
  requests for an index ≥ the current count are rejected.
- **Everyone (host included)** reacts to `m_SeatCount.OnValueChanged` with
  `ApplySeatLayout(count, animate: true)`: `TableTop.SetSeatLayout(count)`
  (seat yaws/positions, active seats, `_ShapeSides`) and
  `TableSeatSystem.SetTableScale(config.ScaleFor(count))`, wrapped in the
  vignette fade when the layout actually changes. The method is absolute and
  idempotent — applying the same layout twice is a no-op.
- **Late joiners** apply `m_SeatCount.Value` in `OnNetworkSpawn` **before**
  requesting a seat, following the template's late-joiner idiom (apply current
  state → subscribe → request).
- **Local UI** (action bar / "Navigation Menu", manipulation handles) is never
  positioned by the layout engine. It follows the template's own hook:
  `TableSeatSystem.m_OnSeatChanged` → `SeatBillboard`, which reads the yaw from
  the seat transform and scales its cached base offsets by the table scale.
  `SetTableScale` re-invokes `m_OnSeatChanged` so billboards re-place after an
  expansion.
- **Session end** (`OnNetworkDespawn`) resets scale to 1 and the layout to the
  smallest supported count.

The table root stays pinned at the world origin (template convention: table
manipulation moves the XR Origin, never the table). `SetTableScale` keeps each
player's offset relative to their seat across the scale change.

## Palette and active seat

`PlayerColorManager` is an in-scene NetworkBehaviour **with a NetworkObject**:

- `NetworkList<Color> m_Palette` — per-seat colors, seeded by the host from the
  `PlayerColorPalette` asset; replicated to all peers and late joiners.
- `NetworkVariable<int> m_ActiveSeat` — the animated-highlight seat; initialized
  to the host's seat at session start.
- API: `GetPlayerColor(seat)`, `GetAllPlayerColors()`, `ActiveSeat`,
  `SetActiveSeat(int)` (server), `SetSeatColor(int, Color)` (server),
  `RequestSeatColorRpc(seat, color)` (seated player; the server rejects colors
  held by another occupied seat), `GetContrastColor(seat)`.

Consumers pull from the palette: seat buttons (`TableTopSeatButton` →
`LocalPlayerColor`), avatars/name tags (`SeatMap`), the rim updater
(`SeatColorRimUpdater`, angular order derived via `SeatGeometry`), and the seat
hover visuals (`SeatHoverVisual`, MaterialPropertyBlock + contrast outline).

## Color picker protocol (API shaped now; UI is follow-on work)

Server-arbitrated, per-seat colors:

1. A seated player picks a color. If it is **free** (not assigned to any other
   occupied seat) → `RequestSeatColorRpc(seat, color)` → the server applies it
   to that seat → the palette replicates. Done.
2. If **another player holds it** → `RequestColorTransferRpc(fromSeat, color)`
   → the server records one pending request per color and relays
   `ColorTransferRequestedRpc` to the holder only.
3. The holder answers `RespondColorTransferRpc(requestId, accepted)`.
   **Deny** → the server sends `ColorTransferDeniedRpc` to the requester.
   **Accept** → the server assigns the color to the requester's seat, gives the
   releasing seat the next free *default* palette color, replicates both, and
   notifies both players.
4. The releasing player may then pick any free color, or request a held one —
   same process again.

Rules: requests time out server-side (default 30 s → denied); a player can have
one outstanding request; leaving a seat cancels its pending requests; the host
is never special.

Phase-2 code provides `m_Palette`, `RequestSeatColorRpc`, and a
`PendingColorRequest` struct plus the three transfer RPC signatures with server
arbitration and timeout. The picker UI and notification panel are out of scope.

## Per-game override summary

A game consuming this layer overrides behavior entirely through assets:

- **Seat counts / collapse behavior / geometry** — its own `TableLayoutConfig`.
- **Colors** — its own `PlayerColorPalette`.
- **Active player** — calls `SetActiveSeat(int)` from its game mode.

No template or 8-player-layer script needs editing.

## New files (additive)

`SeatGeometry.cs`, `ContrastColor.cs`, `TableLayoutConfig.cs`,
`PlayerColorPalette.cs`, `SeatHoverVisual.cs`, `SeatButtonSpawner.cs`,
`SeatColorRimUpdater.cs`, `SeatColorRim.hlsl`, the three
`TableLayout_*.asset`s, `DefaultPlayerColorPalette.asset`, and the EditMode
tests under `Assets/Tests/EditMode/`.

## History

The original C# dynamic seat placement was deleted in commit `21b53bc`
(2025-10-29); the shader's N-gon support survived. This design re-adds the C#
side properly. Earlier planning and investigation documents live on branch
`8Player` under `Assets/MRTabletopAssets/Planning Documentation/`.
