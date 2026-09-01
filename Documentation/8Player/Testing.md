# 8-Player Tabletop — Testing

How to validate the 8-player layer. [Design.md](Design.md) describes the
behavior these checks verify.

## Open findings (observed during validation, not yet fixed)

- **Squelch toggle — fixed in `91b2def`, still unverified; parked**
  (status 2026-08-26). Original finding (08-25): squelching another
  player via their seat card's mic button worked, but clicking again
  did not unsquelch. Suspects: `TableTopSeatButton` stacking the mute
  button's onClick listener on every `AssignPlayerToSeat`, and/or the
  `squelched` bindable round-trip. `91b2def` made it state-driven and
  reapplies on participant refetch, but landed during the stale-assembly
  evening so no session has exercised it. On 08-26 the user confirmed
  the squelched icon (red circle) toggles on and off correctly across
  repeated clicks — evidence the state-driven fix works and the
  listener-stacking bug is gone. The audio half remains unverified: a
  clean check is impractical on a single machine (all clients share
  speakers/mic — echo masks who is muted). Parked by user decision; verify properly
  when players are on separate audio endpoints (e.g. the headset +
  desktop rig). Retest recipe: mic click → squelched icon on + silence;
  click again → audio back; repeat; then seat-swap and rejoin that
  player and confirm one click still equals one toggle.
- **Corner seat cards (5-8) do not respond to clicks** — *root cause
  found and fixed 2026-08-26, awaiting retest.* Not a hover/raycast
  problem at all. Each card's join button is
  `-SeatButton N/Text Poke Button/Button Front/Hover Object/Unoccupied/
  Open Seat/Confirm Button`. All eight already carried a correct
  `RequestSeat` persistent call with the correct seat index argument
  (0-7, set in `Player Menu UI.prefab`), but the four corner cards had
  **no `m_Target` override anywhere** — not in the scene, not in
  `Player Menu UI.prefab` — so the call resolved against a null target
  and silently no-opped. Fixed by pointing all four at the scene's
  `NetworkTableTopManager`; the scene now carries explicit `m_Target`
  overrides for all eight.

  Consequences (both applied 2026-08-26): the Phase 4 `SeatButtonSpawner`
  is **not needed** — the cards already exist and `NetworkTableTopManager`
  already shows/hides them with `SetActive(i < seatCount)`.
  `TableTopSeatButton.RepairSeatWiring()` (the interim runtime patch) has
  been **deleted**, and `SeatButtonLayout.EnsureButtonsAreVisible()` (which
  force-activated all 8 cards in `Start()`, racing the seat-count gating)
  has been **removed** — `SeatButtonLayout` now only sizes the scroll
  content, and full deletion of that component remains Phase 3. Wiring was
  verified live via `unity command eval_file`: all 8 cards call
  `RequestSeat(seatID)` on `NetworkTableTopManager` with matching indices.
- **Action bar clips through the doubled table** (2026-08-25): the
  billboard scales its x/z offsets with the table but not y, and the
  x2 table is taller, so at many camera heights the bar renders under
  or intersecting the tabletop; players must lean far out to see it.
  Needs a design decision (raise bar with table height vs reposition)
  — user decision 2026-08-26: evaluate IN VR on the headset, not in a
  desktop build. Separately, the bar's baseline z-fighting with the
  4-player tabletop was fixed 2026-08-26: Navigation Menu raised from
  y=0 to y=0.005 (scene override on the LocalPlayerUIArea instance).

  **RESOLVED 2026-08-26 — it was never a position bug.** Quest + editor
  symptom: bar visually "under the table" appearing/disappearing around
  joins and moves, on host and clients alike. Live snapshot during the
  broken state proved the transforms healthy (bar world y=+0.006,
  tabletop Plane surface at y=-0.0001, billboard cache intact, scale 1):
  the bar was geometrically ABOVE the surface while rendering UNDER it.
  Root cause: the migration's `TableGridMaterialColor` variant lost the
  stock material's explicit render-queue pin — stock `TableGridMaterial`
  draws at queue **2446** (before UI, ZWrite on), the color variant fell
  to the shader default **3000**, the same transparent queue as the
  world-space canvas. Same-queue transparents sort by camera distance,
  so the tabletop and the UI swapped draw order depending on head
  position — which also retro-explains the original "z-fighting"
  report. Fix, second attempt: pinning only `renderQueue = 2446` did
  NOT hold — the material's `_QueueControl` was 0 (Auto), so the
  Android/Windows platform-switch reimport normalized the queue back to
  3000 before the next builds were made (which is why the first rebuild
  still showed the bug). Durable fix (2026-08-26 late): match the stock
  material's full queue policy on `TableGridMaterialColor.mat` —
  `_QueueControl: 1` (user override), `_QueueOffset: -4`,
  `m_CustomRenderQueue: 2446` — verified to survive a forced reimport.
  **CONFIRMED FIXED 2026-08-27.** Controlled A/B in one session: editor
  host (queue 2446 live) kept the bar fully visible through a VR join
  while the headset on the previous queue-3000 APK showed it overlapped
  — same session, same events, material queue the only variable. After
  rebuilding, validated live with 5 players including the 4->8
  expansion at the new 1.5x scale: UI visible throughout on all peers.
  The redundant 5mm Navigation Menu lift was reverted to y=0 the same
  day (template parity restored).
- **Rotation bar bent at the unscaled corners — fixed 2026-08-27,
  awaiting retest.** At 1.5x the handle capsule's position scaled but
  `TableHandleController`'s bend/slide anchors stayed authored
  (±0.425), so the bar bent where the 1x corners used to be.
  Fix: anchors and the grip offsets under both handle centers follow
  `tableScaleChanged` (±0.6375 at 1.5x). Retest: expand to 5+, slide a
  hand along the rotation bar — it should bend only at the actual
  corners; grips sit at the bar ends; collapse back to 1x restores the
  authored span. (Separately: 8-seat scale pinned to 1.5 in TableLayout_4or8,
  user decision — spacing-preserving value would be ~1.85.)
- **Table manipulation gizmo vs table scale — fixed 2026-08-26,
  awaiting retest.** Three defects: (1) the free-move manipulator's
  post-move reset used its Awake-cached scale-1 local position, snapping
  the gizmo back to the 4-player radius; (2) the rotation manipulator's
  radius lives on its `Handles` child (a grandchild of
  TableManipulationOffset), which SeatBillboard's direct-child scaling
  never reached — and TableHandleController re-copies the visible
  capsule onto those centers every frame; (3) `Table Move Visual` (the
  grab preview outline) kept its 4-player footprint scale and, on the
  free-move manipulator, its unscaled offset back to the table center.
  Fix: `TableSeatSystem.tableScaleChanged` event + `TableManipulator.
  ApplyTableScale` (scales direct-child z offsets and the visual's
  uniform scale; both manipulators host the script) + scale-aware
  post-move reset. Retest at 8 seats: gizmo sits at the doubled edge,
  survives a table move, outline is double-size and centered.
- **Runtime logging is very sparse after the noise cleanup**: silent
  no-ops are hard to diagnose during validation. Consider an optional
  verbose-validation logging toggle rather than restoring blanket
  Debug.Log noise.

## Test rig: 8 simultaneous players

Validated recipe (2026-08-21): 4 MPPM editor players + 4 windowed Windows
builds.

### Editor players (1–4)

1. Open the project in Unity 6000.5.9f1 with Multiplayer Play Mode enabled.
2. Activate virtual players in the Multiplayer Play Mode window (tags
   `Player2`–`Player4`; the main editor is Player 1).
3. Enter Play mode; each editor player hosts/joins through the lobby UI.

> **Known template quirk (stock, verified against ReferenceV2):**
> **builds never see editor-hosted lobbies in the join list.** Every
> lobby is stamped with an editor flag; `LobbyManager.CheckForLobbyFilter`
> hides editor lobbies whenever `s_HideEditorInLobbies` is false, and
> `Awake` force-sets it false in builds — so the browser and quick-join
> both exclude editor rooms on devices/standalone. Editors DO see
> build-hosted lobbies, which is why device-hosted sessions never hit
> this. Workaround for editor-hosts-device-joins: join by room code
> (`JoinLobbyByCode` has no filter) — the code is on the host's table
> card.
>
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
>
> History: these races are a **V2 regression**. Template V2 rewrote the
> authentication/MPPM path (command-line parsing instead of the MPPM
> tags API, auth wrapped in a failure-swallowing try/catch); V1 did not
> exhibit this under heavy multi-player restart testing.

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

Validated 2026-08-27 on the current binaries (editor host + Quest + Windows
builds) unless noted.

- [x] Players 1–4: behavior identical to the stock template (scale 1, square
      table, seats filled in order 0,1,2,3).
- [x] 5th player joins → all four seated players get the vignette fade, table
      scales, every player stays at their own seat.
- [x] After expansion, each player's action bar ("Navigation Menu") and
      manipulation handles sit at the scaled offset, correctly oriented.
- [x] The host applies the expansion to itself (host is not skipped).
- [x] No `Player with id N not found` errors on any client.
- [x] Players 6–8 join without re-fading already-seated players.
- [x] Seat switching works at 8 seats, including via spectator.
- [x] Any player leaving keeps the table at 8 (collapse policy `Never`).
- [x] Leaving an expanded session and starting a new one → table back at
      scale 1 with 4 seats.
- [ ] A late joiner arriving after expansion sees the scaled table and gets its
      UI placed (no `Invalid current seat: -1`).
- [ ] Rotation-bar bend anchors sit at the scaled corners (fix 2026-08-27,
      needs the next build round).

### Generalisation: the layout engine beyond 4-or-8

The checks above only exercise the Pegs-and-Jokers shape. These validation
configs exist to prove the *template mechanism* generalises. Regenerate them
with `-executeMethod MRTTT.EditorTools.CreateTableLayoutAssets.Create`; swap
the config on `NetworkTableTopManager` in the scene to test each.

> **The config is baked into each build.** Every peer reads its *own* local
> config for seat geometry and scale, so all players in a session must run the
> same one. The Windows test rig therefore keeps one build directory per
> config, each with eight shortcuts (`MRMTTT.exe - a..h`, one auth profile
> each) so a single folder can host up to eight clients:
>
> | Build dir | Config |
> |---|---|
> | `Builds/Win1` | `TableLayout_Odd` |
> | `Builds/Win2` | `TableLayout_Immediate` |
> | `Builds/Win3` | `TableLayout_6Max` |
> | `Builds/Win4` | `TableLayout_Dynamic` |
>
> These are layout-logic checks, not VR checks — the simulator in the Windows
> build is sufficient. Keep the Quest build on the shipping `TableLayout_4or8`.

#### `TableLayout_Dynamic` — {3,4,5,6,7,8}, `WhenRemainingFit`, fully derived

No presets at all: every count uses regular-polygon yaws and the
constant-spacing scale formula. Exercises the no-preset path end to end.

- [ ] 3 players → triangle; 4 → square; 5/6/7 → pentagon/hexagon/heptagon with
      seats and hover rings on the vertices and the rim in 3..7 segments;
      8 → octagon.
- [ ] Seat 0 still faces yaw 0 at every count (derived order starts at 0).
- [ ] Table scale grows smoothly across counts (no jump at a preset boundary).

#### `TableLayout_6Max` — {4,6}, `WhenRemainingFit`, derived scale

Partial growth: a maximum that is neither 4 nor 8, and no pinned scale.

- [ ] 5th player → table grows to a hexagon (6 seats), derived scale ≈1.29.
- [ ] A 7th player is refused a seat / spectates (6 is the maximum).
- [ ] Players leave until the remainder fits 4 seats without moving anyone →
      table collapses to the square.

#### `TableLayout_Immediate` — {4,6,8}, `Immediate`

Multi-step growth, and the only policy that relocates seated players.

- [ ] Growth steps 4 → 6 → 8 as players join (not straight to 8).
- [ ] A player leaving triggers an immediate shrink, displaced players moved to
      the lowest free seats, with the fade.
- [ ] Nobody is stranded in a seat index beyond the new count.

#### `TableLayout_Odd` — {3,5,7}, `Never`, minimum seat count 3

The important one: proves nothing hard-codes 4. `MinimumSeatCount()` and
several call sites fall back to 4 when the config is missing.

- [ ] A fresh session starts as a **triangle** (3 seats), not a square.
- [ ] Growth 3 → 5 → 7; an 8th player is refused.
- [ ] Session-end reset returns to 3 seats, not 4.
- [ ] Seat cards show 3/5/7 entries and the scroll content sizes to match.

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
