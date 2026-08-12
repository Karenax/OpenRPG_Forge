# R084 — Encounter scenario acceptance

## Scope

`R084 / FC-ENCOUNTER-P1-02` closes exactly five P1 functions:

- `ORF-ENC-009` — manual spawn points;
- `ORF-ENC-011` — player restrictions;
- `ORF-ENC-012` — `OnEnter` and `OnExit`;
- `ORF-ENC-013` — `OnExhausted`;
- `ORF-ENC-015` — waves and phases.

Source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/13_ENCOUNTER.md` at lines 636–705,
782–850, 855–926, 931–1002 and 1083–1158.

## Dependency reduction

WAYPOINT/AREA reduce to canonical spawn-anchor IDs and finite transforms. MULTI reduces to a typed
activator query and authoritative network role. SCRIPT reduces to three versioned lifecycle
bindings. QUEST/AUDIO reduce to optional typed quest/objective and voice-cue references on a wave;
R084 adds no quest runner, audio mixer, network protocol or general graph editor.

## Audited gap matrix

| Source requirement | Present before R084 | Real limit to close | Required evidence |
|---|---|---|---|
| Manual anchors with transform, capacity, navigation, visibility, fallback and distribution | Isolated RPG Kit `SpawnAnchor` | Canonical encounter uses string IDs and identity transforms | Scenario tail, validation, UI, adapter and runtime nodes |
| Configurable player-only activation exclusions and authority | Isolated `ActivatorQuery` | Not persisted or exposed by the Toolset | Canonical activator policy and nominal/denied checks |
| Enter/exit events with activator and duration | Isolated `EncounterLifecycle` | No canonical API/bindings/scripts or project round trip | Versioned lifecycle policy and typed events |
| Exhausted event with cause, kills, escaped, waves and rewards | Isolated event only carries spawned count | Missing complete payload and bindings | Extended typed event, adapter and runtime smoke |
| Wave graph with conditions, delays, compositions, objectives, voice and transitions | Isolated phases/waves lack canonical UUID references and Toolset | Not attached to project, command, script/MCP or runtime | Typed phase/wave profiles, graph validation, preview and nodes |

## Acceptance matrix

| Shared acceptance criterion | Proof required for closure |
|---|---|
| Author all five functions without generated-file edits | Dedicated encounter scenario sections and graph summary |
| Save, close and reload preserves canonical data | JSON/session and governed-sample round trips |
| Invalid configuration is refused before build | Stable diagnostics for IDs, anchors, transforms, activator, lifecycle and graph refs |
| UI and command converge | Existing `SetWorldFeatureCatalogHandler` dry-run/apply path |
| Undo/redo restore canonical documents | Existing Toolset command history smoke |
| Runtime optional/required behavior is safe | Resolver plus nominal/denied/failure headless smoke |
| Runtime and agents stay bounded | Typed ForgeScript handle, filtered MCP read/dry-run and explicit runtime nodes |

## Delivered bounded changes

- Added one optional `EncounterScenarioProfile` tail to `EncounterWorldProfile`.
- Reused `EncounterCompositionService`, `EncounterLifecycle` and `EncounterWaveService` through the
  existing encounter adapter boundary.
- Extended the existing encounter tab and shared command; no parallel store was added.
- Added typed script/MCP projections, governed sample data and four explicit runtime groups for
  anchors, activator restrictions, lifecycle and the phase/wave graph.
- Extended `EncounterExhausted` compatibly with cause, kills, escaped actors, wave count and rewards.
- Added stable diagnostics for component UUIDs, transforms, canonical references, activation tags,
  versioned bindings and forward-only graph transitions.

## Reproducible evidence

- Targeted tests: 4 Domain + 3 RPG adapter + 2 Application + 1 ModuleFormat + 1 MCP, all passed.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R084_ENCOUNTER_SCENARIO_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R084_ENCOUNTER_SCENARIO_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r084-encounter-scenario-ui.png`, inspected with manual
  anchor capacity/navigation/visibility/fallback, all activator exclusions, server authority and the
  semantic lifecycle/wave summary visible without overlap.
- Promoted only the five R084 tracker rows after the integrated `scripts/Verify.ps1` portal passed.

## Non-objectives

- `R085` or later P1 lots until this portal is green;
- P2 and AV7–AV10;
- a universal graph editor, quest/audio subsystem rewrite, engine fork or network protocol;
- commit or push.
