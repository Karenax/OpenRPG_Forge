# R080 — Creature event and AI acceptance

## Scope

`R080 / FC-CREATURE-P1-04` closes exactly five P1 functions:

- `ORF-CRE-035` — `OnPerception`;
- `ORF-CRE-037` — `OnPhysicalAttacked`;
- `ORF-CRE-038` — `OnSpellCastAt` generalized to abilities;
- `ORF-CRE-040` — `OnBlocked`;
- `ORF-CRE-046` — profile-driven artificial intelligence.

Source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/05_CREATURE.md` at lines 2645–2724,
2805–2882, 2883–2960, 3039–3117 and 3508–3592. The shared contract requires canonical,
versioned data with stable UUIDs, provenance, engine-independent transform and asset identity;
transactional authoring, localized diagnostics, optional-safe/required-explicit runtime behavior,
typed ForgeScript reads, filtered MCP reads with dry-run semantic mutation, persistence and undo/redo.

## Dependency reduction

The SCRIPT, AREA and QA dependencies reduce to existing typed event bindings, navigation status,
bounded action identifiers, blackboard values and the already tested RPG Kit event/AI services.
R080 does not open later creature ambient-animation work, area AI authoring, P2 behavior or a new
network protocol.

## Audited gap matrix

| Source requirement | Present before R080 | Real limit to close | Required evidence |
|---|---|---|---|
| `StimulusDetected` / `StimulusLost` with sense, confidence, position and memory | Typed RPG Kit diff service and tests | No canonical creature binding, authoring path or runtime/script/agent projection | Domain event tests, UI proof, round-trip, runtime nodes |
| `AttackDeclared` / `AttackResolved` with attacker, weapon, target, result and network context | Typed RPG Kit lifecycle and tests | Not attached to the creature blueprint or persisted | Binding validation, simulation, command and adapter proof |
| `AbilityTargeted` / `AbilityImpacted` with source, target context, tags, result and magnitude | Typed RPG Kit lifecycle and tests | No canonical `OnSpellCastAt` authoring or bounded runtime projection | Event simulation and exact smoke marker |
| `MovementBlocked` with obstacle, cause, navigation status, position and recovery | Deterministic RPG Kit recovery selection | No canonical event binding or visible recovery policy | Validation, preview, adapter and runtime node proof |
| Interchangeable state machine, behavior tree, utility and script controller with inspectable blackboard | Isolated RPG Kit profile, decision service and tests | No canonical UUID-bearing controller, persistence, author UI, command, ForgeScript or MCP | Controller tests, UI simulation, round-trip and bounded projections |

## Acceptance matrix

| Shared acceptance criterion | Proof required for closure |
|---|---|
| Author all five functions without hand-editing generated files | Dedicated five-section creator window with empty/error states and visible simulation |
| Save, close and reload exactly preserves canonical data | JSON round-trip test and governed sample reload smoke |
| Invalid configuration is refused before build with a localized diagnostic | Domain, project and command-handler tests for UUIDs, bindings, blackboard and ranges |
| UI mutation and domain command produce the same result | Window uses the existing `SetCreatureFoundationHandler` for dry-run and apply |
| Undo then redo restore old and new documents | Shared command history exercised by the Toolset smoke |
| Runtime optional/required behavior is safe | Resolver tests and runtime smoke marker |
| Runtime and agent access stay bounded | Typed ForgeScript handle, filtered MCP resource, dry-run mutation and explicit runtime nodes |

## Delivered bounded changes

- Add one optional `CreatureEventAiCompositionProfile` tail to `CreatureFoundationProfile`.
- Model four typed event configurations and one interchangeable controller with a UUID-bearing,
  typed, inspectable blackboard and bounded action/recovery lists.
- Reuse the existing foundation command, serializer and undo/redo path; do not add parallel storage.
- Adapt the canonical profile to the existing RPG Kit event and AI services.
- Add one Toolset window, one runtime representation and the script/MCP projections.
- Add a governed sample and reproducible tests/capture, then promote only these five tracker rows.

All six bounded changes above are delivered. The canonical tail is persisted on
`CreatureFoundationProfile`, validated both locally and across project blueprints, projected through
typed ForgeScript and filtered MCP reads, adapted to the existing RPG Kit services and represented by
real runtime nodes. The dedicated Toolset window uses the shared foundation command for dry-run,
apply, undo and redo.

## Targeted validation

- Domain event/AI profile: 4/4 tests passed.
- RPG Kit event/AI adapter and pre-existing interaction services: 9/9 tests passed.
- Foundation command and cross-blueprint UUID validation: 9/9 tests passed.
- Module-format project round trips: 45/45 tests passed.
- MCP filtered creature-foundation projection: 1/1 test passed.
- Solution Debug build: 27 projects, 0 warning and 0 error.
- Toolset smoke:
  `OPENRPG_FORGE_TOOLSET_R080_CREATURE_EVENT_AI_PASS`.
- Runtime smoke:
  `OPENRPG_FORGE_RUNTIME_R080_CREATURE_EVENT_AI_PASS`.
- Visual evidence: `docs/aurora-parity/evidence/r080-creature-event-ai-ui.png`, 1500×930,
  inspected with all four event cycles, the Utility controller, recovery order and deterministic
  `attack · score 0,9` simulation visible without overlap.

## Integrated closure

The integrated `scripts/Verify.ps1` portal passed after the targeted evidence. It built all 27
projects with zero warning and zero error, executed 1,566 .NET tests and 39 Python tests, retained
the exact R080 Toolset/runtime markers, and validated documentation, tracker/backlog freshness,
exports, 3D maps, asset delivery, MCP, network and save/resume before ending with
`OPENRPG_FORGE_VERIFICATION=PASS`. The Roslyn graph is fresh and deterministic at 7,073 nodes and
45,178 relations. The console transcript is retained at
`.artifacts/r080-full-verify-console.log`.

Only `ORF-CRE-035`, `ORF-CRE-037`, `ORF-CRE-038`, `ORF-CRE-040` and `ORF-CRE-046` were promoted to
`Tested`. R080 is closed; R081 is the next eligible lot.

## Non-objectives

- `R081` door functions or later P1 lots;
- creature ambient animations and every P2 function;
- AV7–AV10;
- a new ruleset, engine fork, renderer or networking protocol;
- commit or push.
