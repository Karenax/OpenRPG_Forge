# R098 — Script world APIs acceptance

## Scope

`R098 / FC-SCRIPT-P1-02` closes exactly five P1 functions:

- `ORF-SCR-012` — inventory API;
- `ORF-SCR-013` — journal and quest API;
- `ORF-SCR-014` — dialogue API;
- `ORF-SCR-015` — faction and reputation API;
- `ORF-SCR-016` — perception API.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/18_SCRIPT.md` at lines 909–983,
985–1059, 1061–1135, 1137–1211 and 1213–1287.

## Dependency reduction

AREA and CREATURE provide opaque world/entity identities. ITEM contributes inventory semantics,
QUEST owns canonical quest documents, DIALOGUE owns canonical graphs and participant roles, and
FACTION owns canonical faction definitions. R098 does not duplicate those catalogs. It exposes one
typed, authoritative scripting projection through `ScriptWorldService`, one application adapter, the
existing native AngelScript sandbox, the ForgeScript editor, MCP and the runtime.

## Audited gap matrix

| Source requirement | Present before R098 | Real limit closed by R098 | Evidence |
|---|---|---|---|
| Inventory transactions, ownership, queries, capacity, rollback, audit and revision | A pure transfer helper | No query result, robust validation, semantic diff, native handle or shared runtime projection | `InventoryResult`, `QueryInventory`, native `inventory_transfer`, adapter and rollback tests |
| Journal/quest transitions, objectives, history, cause, instance scope and revision | A minimal status replacement | Identity, authority, scope/cause validation and objective/history projection were incomplete | `QuestState`, `TransitionQuest`, native `quest_transition` and scoped tests |
| Dialogue graph, participants, variables, result, completion and safe interruption | A minimal session record | Stable participant/variable ordering, duplicate/stale handle protection and native lifecycle were absent | `DialogueRequest`, session lifecycle, native `dialogue_*` and adapter tests |
| Faction membership and reputation with cause, scope, authority, bounds and revision | A minimal membership mutation | Scoped query, bounded reputation and shared versioned scripting path were absent | `FactionTransaction`, `QueryFaction`, native `faction_reputation` and tests |
| Perception by sense, LOS, range, stimulus, confidence, seed and trace | A simple distance check | Supported-sense validation, signal/LOS model, deterministic confidence, stimuli and full trace were incomplete | `PerceptionQuery`, native `perception_query`, deterministic replay tests and UI |

## Shared acceptance

- Every mutation carries an identity, authority and cause or produces a deterministic trace; rejected
  inventory changes retain the original immutable state.
- `ForgeScriptWorldApiRuntimeAdapter` rejects stale handles, treats missing optional script data as a
  safe fallback and emits `FORGESCRIPT_WORLD_API_REQUIRED_MISSING` for required data.
- `InventoryResultRef`, `QuestResultRef`, `DialogueSessionRef`, `FactionResultRef` and
  `PerceptionResultRef` remain opaque native handles inside the same quota-limited sandbox.
- `ForgeScriptSourceHistory` remains the only source mutation boundary, with preview, apply,
  rejection, Undo/Redo and canonical source persistence.
- MCP exposes the same versioned signatures and compiles/tests them without file, network, process,
  engine-object or unrestricted .NET access.

## Delivered bounded changes

- Completed authoritative inventory, quest, dialogue, faction and perception semantics in the RPG
  Kit world service, including queries, audit, revision and deterministic trace data.
- Added six native calls, five opaque handle kinds and rebuilt the pinned AngelScript bridge.
- Added one shared application adapter with immutable runtime projection and stale-handle protection.
- Extended the existing ForgeScript editor with all five world summaries and versioned source calls;
  no parallel source editor was created.
- Added an accessible runtime panel with empty, bounded-error, console and five focusable command
  states over the same adapter.

## Reproducible evidence

- Targeted results: 11/11 RPG Kit world/adapter tests, 1/1 native sandbox test and 6/6 MCP agent tests.
- Native bridge marker: `OPENRPG_FORGE_FORGESCRIPT_NATIVE_BUILD_PASS`.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R098_SCRIPT_WORLD_APIS_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R098_SCRIPT_WORLD_APIS_PASS`.
- Visual marker: `OPENRPG_FORGE_R098_SCRIPT_WORLD_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r098-script-world-apis-ui.png`, inspected at 1680×1000.
  The five world APIs, their live state, the versioned source editor, sandbox console and clickable
  diagnostics are readable without overlap.
- Integrated gate transcript: `.artifacts/r098-full-verify-final.log`; this gate must preserve both
  exact R098 engine markers, the native bridge, generated documentation, the Roslyn graph and all
  consolidated test suites before R099 begins.

## Non-objectives

- `FC-SCRIPT-P1-03` and later P1 lots;
- all P2 functions and Asset/Map AV7–AV10;
- commit or push.
