# 8-Player Tabletop — Testing

How to validate the 8-player layer. [Design.md](Design.md) describes the
behavior these checks verify.

## Test rig: 8 simultaneous players

Validated recipe (2026-08-21): 4 MPPM editor players + 4 windowed Windows
builds.

### Editor players (1–4)

1. Open the project in Unity 6000.5.9f1 with Multiplayer Play Mode enabled.
2. Activate virtual players in the Multiplayer Play Mode window (tags
   `Player2`–`Player4`; the main editor is Player 1).
3. Enter Play mode; each editor player hosts/joins through the lobby UI.

> **Known template quirk (not fixed by design):** a virtual player that
> enters Play Mode in its first seconds after booting can lose a race
> against the editor's Unity Cloud binding: `XRINetworkGameManager.Awake`
> checks `CloudProjectSettings.projectBound` once, logs "Project has not
> been linked to Unity Cloud", and permanently skips authentication —
> the lobby list then stays silently empty on that player. This is
> pristine-template behavior (same code in ReferenceV2). Workaround:
> let freshly launched virtual players sit for a few seconds before
> entering Play Mode, or exit and re-enter Play Mode — on re-entry the
> binding is ready and the join list populates normally.
>
> A second flavor of the same race: a player can log
> "Checking for AuthenticationService.Instance before initialized"
> (ServicesInitializationException) when Play Mode starts before Unity
> Services core init finishes in that editor process; that player never
> authenticates and cannot join until Play Mode is re-entered. Also
> pristine-template behavior; it is probabilistic per player (e.g. 2 of
> 3 virtual players join, one does not). Same workaround. Note MPPM
> intentionally syncs Play Mode across all players, so re-entering play
> restarts every player.

### Windows build players (5–8)

1. Build a Windows standalone. The build must include the XR Interaction
   Simulator: Project Settings → XR Plug-in Management → XR Interaction
   Toolkit → "Use XR Interaction Simulator in scenes" ON and **"Instantiate In
   Editor Only" OFF**, or the instances will have no mouse/keyboard input.
   (Turning that checkbox off is a local testing change — do not commit it.)
2. Launch with [Launch-Players.ps1](Launch-Players.ps1):

   ```powershell
   .\Launch-Players.ps1 -Exe "E:\Builds\MRMTTT\MRMTTT.exe" -Count 4
   ```

   Each instance gets its own auth profile (`PlayerArg:<name>`) and a window
   title (`Player5`…), tiled on screen.
3. Inside each window press **Tab** to enter FPS/head mode so the mouse is the
   pointer.

## Checklists

### Expansion / collapse (config: `TableLayout_4or8`, MPPM 4 + builds 4)

- [ ] Players 1–4: behavior identical to the stock template (scale 1, square
      table, seats filled in order 0,1,2,3).
- [ ] 5th player joins → all four seated players get the vignette fade, table
      scales ×2, every player stays at their own seat.
- [ ] After expansion, each player's action bar ("Navigation Menu") and
      manipulation handles sit at the doubled offset, correctly oriented.
- [ ] The host applies the expansion to itself (host is not skipped).
- [ ] No `Player with id N not found` errors on any client.
- [ ] Players 6–8 join without re-fading already-seated players.
- [ ] A late joiner arriving after expansion sees the ×2 table and gets its UI
      placed (no `Invalid current seat: -1`).
- [ ] Seat switching works at 8 seats, including via spectator.
- [ ] Any player leaving keeps the table at 8 (collapse policy `Never`).
- [ ] Leaving an expanded session and starting a new one → table back at
      scale 1 with 4 seats.

### Dynamic shapes (config: `TableLayout_Dynamic`)

- [ ] 3 players → triangle; 4 → square; 5/6/7 → pentagon/hexagon/heptagon with
      seats and hover rings on the vertices and the rim in 3..7 segments;
      8 → octagon.

### Collapse policies (each: 6 players, player 3 leaves)

- [ ] `Never` → hexagon-sized table keeps the hole.
- [ ] `WhenRemainingFit` → hole remains until occupancy fits the smaller
      layout without moving anyone, then the table shrinks.
- [ ] `Immediate` → player 6 moves into seat 2 and the table becomes a
      pentagon, with the fade.

### Stock behavior (config: `TableLayout_Default`)

- [ ] A 5th player is refused a seat / spectates; nothing changes (template
      behavior preserved).

### Colors / palette (Phase 2)

- [ ] 8 players: rim segments follow seat order with correct colors.
- [ ] Black and white segments have contrast outlines (white-on-black,
      black-on-white); the glow color is never invisible on its segment.
- [ ] Seat hover visuals are visible for all 8 seats (including black/white).
- [ ] Avatar == seat color on all peers, including the local player's shirt on
      the host.
- [ ] A late joiner sees the host's current palette.
- [ ] Host `SetSeatColor` replicates to all peers.
- [ ] Active seat starts at the host's seat; `SetActiveSeat(k)` moves the
      animated highlight on all peers.
- [ ] 4-player visuals are identical to the stock template.

### Template parity (after every phase, against the pristine V2 reference)

Reference: `E:\src\Unity\6000.5\MRMTTT ReferenceV2` (not a git repo).

- [ ] 4 players: scale 1, seats 0–3 at radius 0.75 with yaws 0/180/270/90,
      fill order 0,1,2,3.
- [ ] Action bar and manipulation handles identical to template.
- [ ] `TableSeatHover - 1..4.mat`, `TableGridMaterial.mat`,
      `LocalPlayerUIArea.prefab`, `XRINetworkPlayer.cs` byte-identical to the
      template (until a phase deliberately touches them).
- [ ] `TableTop.prefab` differs only by `Seat (4)`–`Seat (7)`.
- [ ] CoachingUI `ResetToSeatDefault` works.
- [ ] Slingshot color ownership works.
- [ ] Host leave/rejoin is clean.

### Shader (Phase 5)

- [ ] In the editor (not playing) the table is identical to the template.
- [ ] `_SeatColors = 0` → output pixel-identical to the template.
- [ ] 4-player rim shows seat colors; 8-player shows 8 segments + outlines +
      highlight.
- [ ] Quest frame time measured before/after; compiled variant count unchanged.

## Automated tests

EditMode tests live in `Assets/Tests/EditMode/` and cover the pure helpers:
`TableLayoutConfig.TargetSeatCount` (all three collapse policies) and
`ScaleFor`, preset vs regular-polygon yaws, `SeatGeometry.AngularOrder`,
`ContrastColor.For`, and palette normalization.

Run: Test Runner window → EditMode, or batchmode:

```powershell
& "D:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testPlatform EditMode -testResults .\Logs\editmode-results.xml
```
