# R099 — Script runtime tools acceptance

## Scope

`R099 / FC-SCRIPT-P1-03` closes exactly five P1 functions:

- `ORF-SCR-019` — sound and music API;
- `ORF-SCR-020` — time API;
- `ORF-SCR-023` — custom events;
- `ORF-SCR-032` — hot reload;
- `ORF-SCR-033` — debugger.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/18_SCRIPT.md` at lines 1441–1515,
1517–1591, 1745–1819, 2433–2507 and 2509–2583.

## Dependency reduction

AUDIO owns the canonical resources, buses and engine playback. PROJECT owns the world clock. BUILD
owns compilation and dependency graphs. QA owns diagnostics, while MULTI owns remote authority. R099
does not duplicate those catalogs: it exposes their bounded script-tool projection through
`ForgeScriptRuntimeTools`, one application adapter, the existing native sandbox, MCP, the ForgeScript
editor and the runtime.

## Audited gap matrix

| Source requirement | Present before R099 | Real limit closed by R099 | Evidence |
|---|---|---|---|
| Audio cues, state machine, fades, snapshots, listener policies and completion | Pure audio helpers and API catalogue entries | Incomplete validation/revision, no native implementation and no shared editor/runtime projection | audio state/service tests, native `audio_*` calls, adapter and UI |
| World clock, timers, schedules, simulation time, calendar and deterministic delay | Clock advance and due-list helpers | No identified scheduling/cancellation, duplicate protection or native timer/calendar/delay path | `Schedule`, `Due`, native `world_*`/`timer_*`, adapter tests |
| Namespaced event bus with schema, version, subscription and tracing | Mutable schema/subscriber dictionaries | Weak namespace/type/conflict validation and no native event handles | event service tests, native `custom_event`, MCP and runtime |
| Handler-boundary reload, state migration, dependency invalidation and restart fallback | One compact reload function | Module identity/API metadata checks, explicit deferred/restart rollback and visible runtime proof were absent | reload tests, adapter, native request and UI |
| Breakpoints, step, stack, locals, watches, events, entity inspect and remote permissions | Break/step snapshot helper | No breakpoints, remote-admin policy, watch/entity helpers, native debug session or accessible projection | debugger tests, native `debug_*`, adapter and panel |

## Shared acceptance

- `ForgeScriptSourceDocument` supplies stable UUID, schema, provenance, API version, event bindings and
  structured diagnostics to all five tools.
- `ForgeScriptSourceHistory` remains the only source/configuration mutation boundary, with preview,
  apply, rejection, Undo/Redo and byte-stable persistence.
- Missing optional tooling is ignored safely; required use returns
  `FORGESCRIPT_RUNTIME_TOOL_REQUIRED_MISSING`; stale source handles are rejected.
- Audio, clock, timer, event, reload and debug references are opaque generation-checked handles; all
  calls remain in the same instruction/memory/time/recursion-limited native sandbox.
- MCP exposes the same versioned API and compile/diagnostic/test surface without file, network,
  process, engine-object or unrestricted .NET access.

## Delivered bounded changes

- Completed audio state, listener, snapshot, fade and completion semantics with validation/revision.
- Completed deterministic world clock, timer, schedule, calendar, delay and cancellation semantics.
- Hardened the namespaced typed event bus, reload rollback and remote debugger permissions.
- Added native calls/handles for all five surfaces and rebuilt the pinned AngelScript bridge.
- Added one application adapter, five visible Toolset summaries and one accessible runtime panel; no
  parallel source editor or engine service was introduced.

## Reproducible evidence

- Targeted results: 9/9 runtime-tool tests, 5/5 adapter tests, 1/1 native sandbox test and 7/7 MCP
  agent tests.
- Native bridge marker: `OPENRPG_FORGE_FORGESCRIPT_NATIVE_BUILD_PASS`.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R099_SCRIPT_RUNTIME_TOOLS_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R099_SCRIPT_RUNTIME_TOOLS_PASS`.
- Visual marker: `OPENRPG_FORGE_R099_SCRIPT_RUNTIME_TOOLS_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r099-script-runtime-tools-ui.png`, inspected at 1680×1000.
  Audio, time, custom event, hot reload and debugger states are all readable without overlap.
- Integrated gate transcript: `.artifacts/r099-full-verify-final.log`; this gate must preserve both
  exact R099 engine markers, quota abort/recovery, generated documentation, the Roslyn graph and all
  consolidated test suites before R100 begins.

## Non-objectives

- `FC-SCRIPT-P1-04` and later P1 lots;
- all P2 functions and Asset/Map AV7–AV10;
- commit or push.
