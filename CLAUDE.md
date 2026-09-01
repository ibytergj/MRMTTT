# MRMTTT — agent instructions

Unity 6000.5.9f1 project (editor at `D:\Program Files\Unity\Hub\Editor\6000.5.9f1`).
Target platform: Meta Quest (Android, OpenXR). Batchmode compiles run under
Standalone and prove compilation only, not Android settings.

## Unity tooling: use the `unity` CLI, not MCP servers

The **`unity` CLI is the primary and near-always-sufficient interface** to the
editor. It is installed with Unity Hub (`C:\Users\Blender\AppData\Local\Unity\bin\unity.exe`)
and is **not** the same thing as `Unity.exe -batchmode`: via the
`com.unity.pipeline` package it attaches to the **already-running** editor over
local HTTP (port 7800), so an open editor is a connection point, not an obstacle.

Docs: <https://docs.unity.com/en-us/unity-cli/unity-cli> — read them; do not
infer capabilities or limitations from `--help` output or from memory.

The official **`unity-cli` skill** is installed per-user
(`~/.claude/skills/unity-cli/`, via `unity skill install claude-code`) —
invoke it for CLI work; it carries the full command reference. After a CLI
upgrade, run `unity skill refresh`. Ignore the older `unity-mcp-skill` (it
belongs to the MCP-for-Unity package and teaches the disallowed workflow).

Ground rules:

- Always pass `--project-path "E:\src\Unity\6000.5\MRMTTT"` (multiple editors
  and clones may be open).
- `unity status` — connected editors. `unity command` — lists all editor
  commands (~130) with parameter schemas. **Check that list before declaring
  anything impossible.**
- Common commands: `find_gameobjects`, `get_scene_hierarchy`,
  `get_component_properties`, `get_serialized_fields`, `set_serialized_field`,
  `set_component_properties`, `console`, `editor_status`, `menu`, `search`,
  `recompile` / `recompile_status`, `run_tests`, `build` / `build_status`.
- Arbitrary C# against the live editor: `unity command eval --code ...` or
  `eval_file --file <path>`. The file may live **outside** `Assets/` (no
  import, no domain reload — safe while a play/MPPM session is running).
  Gotchas: the code is a *method body* — `using` directives fail, fully
  qualify types, avoid bare `Object`, end with `return`; pass
  `--timeout 60000` for scene sweeps (default 5000 ms times out).
- `get_serialized_fields` returns `{"unsupported":"Generic"}` for composite
  nodes (e.g. a UnityEvent persistent call) — read leaf property paths, or use
  `eval` with `SerializedObject`.
- MPPM virtual players are **not** exposed over the pipeline; only the main
  editor is.
- MCP servers (unityMCP, mcp-for-unity, aura-unity, coplay-mcp) are **not**
  to be used for editor work. AnkleBreaker is kept for exactly one purpose:
  the pipeline only exposes the main editor, while AnkleBreaker's plugin runs
  inside each MPPM virtual player and can read their consoles/state live
  during multi-player test sessions. Use it for that, nothing else; for all
  main-editor work every AnkleBreaker call has a direct CLI equivalent.
  (Without it, virtual-player diagnosis falls back to `Library/VP/*/Logs`.)
- **HzOSDevMCP** is the exception and the tool of choice for Quest *device*
  work: APK install/launch, logcat, device screenshots, perfetto, Meta docs.
- Use `Unity.exe -batchmode` only for genuinely headless runs on a project no
  editor has locked.

## Safety rules (learned the hard way)

- **Never create or edit `.meta` files.** Unity generates them; commit what
  Unity writes.
- **No builds or changes without Unity open** (user rule, verbatim): the open
  editor must be aware of every change. After editing any script, confirm the
  editor recompiled it (`Library/ScriptAssemblies` newer than the newest
  source edit) before testing or building. Builds go through the open editor,
  never headless behind its back.
- **Never enter/exit Play Mode without asking the user.**
- **Never edit C# under `Assets/` while a play/MPPM test session is active** —
  the recompile's domain reload destroys every player's session state.
  Diagnose live (eval/read-only commands are fine); batch code fixes for
  between sessions and say so.
- Verify builds/recompiles through status, console evidence, and artifacts —
  command completion alone is not success.

## Project context

- Branch layout, migration direction, and 8-player plan status:
  `Documentation/8Player/Handoff.md` (start here), `Design.md`, `Testing.md`.
- This repo must stay a clean, game-agnostic template (possible upstream PR to
  Unity). Keep game-specific content out; strip dev artifacts
  (`Assets/Editor/EightPlayerSceneMigration.cs` etc.) before any PR.
- Local-only, never commit: `XRDeviceSimulatorSettings.asset` changes,
  `Packages/manifest.json` / `packages-lock.json` (AnkleBreaker git package).
