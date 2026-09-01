# 8-Player Plan — Continuation Handoff

State as of 2026-08-25 (branch `8Player-V2`, HEAD `4557117`). The approved
implementation plan lives at
`C:\Users\Blender\.claude\plans\hazy-pondering-dewdrop.md`; decisions and
architecture are in [Design.md](Design.md), test recipes and open findings in
[Testing.md](Testing.md).

## Editor state: healthy (verified 08-25 23:40)

A cold restart resolved the stale-assembly incident from earlier in the
evening (`Library/ScriptAssemblies/MRTTT.dll` 23:40 > newest source 20:37 —
verified). Historical note for the rules below: an in-editor build through
the MCP bridge had imported a pending external script edit and compiled
fresh *player* assemblies while the *editor* assemblies never recompiled;
the asset pipeline then reported no changes (`compile time=0ms`) through
every refresh and play-mode domain reload until the restart. Symptom was the
Navigation Menu at X=0.522 (old both-axes scaling) instead of X=0.261.

Standing rules learned the hard way:
- **User rule (verbatim): "no builds and changes without Unity Open, even if
  you're using the CLI."** Script changes and builds happen with the editor
  open and aware of them; no headless side-channel operations that leave an
  open editor out of sync.
- Never edit C# while a play/MPPM session is active (recompile kills it).
- After any file edit, confirm the editor imported and recompiled it:
  `Library/ScriptAssemblies` newer than the newest source edit — before any
  play test or build.
- Builds through the open editor (Build menu), not via the MCP bridge into a
  live editor mid-session (that is what wedged the pipeline).

## Phase status vs the approved plan

- **Phase 0 (triage/docs/tests): DONE.** Docs in `Documentation/8Player/`,
  29 EditMode tests green (`Assets/Tests/EditMode/`), pure helpers
  (`SeatGeometry`, `TableLayoutConfig`, `ContrastColor`, `PlayerColorPalette`).
- **Phase 1 (layout engine): DONE, runtime-validated to 5 players.**
  Replicated `m_SeatCount`, `ApplySeatLayout` on every peer, seats owned by
  `TableTop.prefab` (derived positions, corner distance override 0.9925),
  scene wired to `TableLayout_4or8`, growth verified live (Bubba, seat 4,
  yaw 45, x2 table, palette across Relay to a standalone build).
- **Phase 2 (colors): CORE DONE.** `PlayerColorManager` is a real networked
  singleton (NetworkObject added in scene), host-seeded palette asset,
  active seat = host seat, transfer-protocol RPCs with timeout,
  `SeatColorRimUpdater` (renamed via GUID-preserving move), `SeatHoverVisual`
  on all 8 rings (palette + contrast outline; corner materials 5-8 deleted).
  REMAINING: `XRINetworkPlayer`/`XRAvatarVisuals` template reconciliation
  (note: squelch fix intentionally modified XRINetworkPlayer — reconcile,
  don't blindly revert), `SeatColorRim.hlsl` on the forked graph, angular-
  order color push (must land TOGETHER with shader-side remap removal).
- **Phase 3 (noise/dead code): NOT STARTED.** CS0618 Find* fixes,
  SeatButtonLayout deletion, Scripts/Shaders folder cleanup.
- **Phase 4 (PR hygiene): PARTIALLY REVISED (2026-08-26).** SeatButtonSpawner
  is **dropped** (user decision): the corner cards' real defect was a missing
  `m_Target` on their RequestSeat persistent calls — fixed by the user in the
  scene, verified live (all 8 cards call `RequestSeat(seatID)` on
  `NetworkTableTopManager`). The interim `RepairSeatWiring()` self-repair and
  `SeatButtonLayout.EnsureButtonsAreVisible()` have been deleted accordingly.
  Remaining: hover visuals into prefab, delete EightPlayerSceneMigration.cs +
  CreateTableLayoutAssets.cs + EightPlayerWiring.cs, revert incidental churn,
  full diff review vs 8cca7cd.
- **Phase 5 (shader integration): NOT STARTED.**

## Validated live (4-then-5-player MPPM+build sessions, 08-25)

4-player baseline identical to template; colors == seats on all peers incl.
through seat switch and rejoin (new client id); active seat stays host's;
4->8 growth with nobody moving; palette reached a standalone build; consoles
clean (player-ID noise eliminated); session-end reset to 4 seats/scale 1
(host leave). Seat-switch server logic at 8 verified via direct RequestSeat.

## Committed fixes AWAITING RETEST (blocked on the stale-assembly unwedge)

- Squelch toggle (state-driven, reapplies on participant refetch) `91b2def`
- SeatBillboard full pose: rotate to seat + scale radial-only; lateral stays
  authored (user-tuned expectation X=0.261) `91b2def` + `4557117`
- Seat cards 5-8 (the cards for the corner seats, indices 4-7): runtime
  self-repair of dead onClick + hover refs `91b2def`
- Seat cards gated by current seat count (cards 1-4 shown until expansion,
  all 8 after) `4557117`
- SeatHoverVisual contrast outlines (black ring gets white edge) `91b2def`
- Windows build `Builds/Win1` (20:46) already contains ALL of the above —
  only the editors are stale.

## Open findings (see Testing.md "Open findings")

- Action bar can clip through the doubled table at some camera heights
  (y offset intentionally not scaled yet; needs a design decision).
- Runtime logging very sparse; consider a verbose-validation toggle.
- MPPM auth races (V2 template regression, user chose NOT to fix; workaround
  documented in Testing.md).
- Template quirks observed: no spectator UI button exists (API only);
  active-seat highlight stays on a vacated seat by design until
  SetActiveSeat is called.

## Local-only uncommitted files (keep out of commits)

- `Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset`
  (simulator-in-builds toggle for the Windows-build test rig)
- `Packages/manifest.json` + `Packages/packages-lock.json`
  (com.anklebreaker.unity-mcp git package — strip before any PR, like Coplay)

## Next steps in order

1. Rerun the 5-player retest of the awaiting-retest list, then 6-8 players
   (no re-fade), non-host leave at 8 (table must hold — `Never` policy),
   late joiner after expansion.
2. Finish Phase 2 remainder (shader work last, per plan).
3. Phase 3 cleanup, Phase 4 hygiene (SeatButtonSpawner replaces the interim
   self-repair), Phase 5 shader integration with parity checks.
4. Longer term: merge V2-upgrade into master, re-add Coplay, then migrate the
   template layer INTO PegsAndJokersMR (in place; MRMTTT stays game-agnostic).
