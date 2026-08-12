# R082 — Door lock event acceptance

## Scope

`R082 / FC-DOOR-P1-02` closes exactly one P1 function:

- `ORF-DOOR-013` — `OnLock` and `OnUnlock`.

The source criterion was read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/11_DOOR.md` at lines 962–1035.
The required adaptation is a canonical `DoorLocked`/`DoorUnlocked` lifecycle carrying method,
actor and credential, with stable identity, schema/provenance, engine-independent transform and
asset inherited from the R081 access/event profile, explicit overrides, transactional authoring,
stable diagnostics, optional-safe/required-explicit runtime behavior, typed ForgeScript reads,
filtered MCP reads with dry-run mutation, persistence and undo/redo.

## Dependency reduction

The SCRIPT dependency reduces to the existing typed event-binding and bounded-handle contracts.
The RPG Kit already owns `DoorLockEvent` and the terminal transition guard. R082 does not reopen
lock/key resolution, door opening/closing, damage, P2 functions or a networking protocol.

## Audited gap

| Source requirement | Present before R082 | Real limit to close | Required evidence |
|---|---|---|---|
| `DoorLocked`/`DoorUnlocked` after a terminal lock-state transition | Isolated `DoorEventService.CompleteLockTransition` and one unit test | No canonical binding, authoring path, runtime representation, script/MCP projection or persistence proof | Optional canonical tail, validation, adapter, UI, round trip, runtime node and exact markers |

## Acceptance matrix

| Shared acceptance criterion | Proof required for closure |
|---|---|
| Author both bindings without generated-file edits | Dedicated lock-event section in the existing door tab, with empty/error state and preview |
| Save, close and reload exactly preserves canonical data | JSON/session and governed-sample round trips |
| Reject invalid configuration before build | Stable diagnostic for identity, schema, API and exact bindings |
| UI and command mutation converge | Existing `SetWorldFeatureCatalogHandler` dry-run/apply path |
| Undo and redo restore canonical documents | Existing Toolset command history smoke |
| Runtime optional/required behavior is safe | Dedicated resolver and runtime smoke |
| Runtime and agents stay bounded | Typed ForgeScript view, filtered MCP read/dry-run and one explicit runtime node |

## Delivered bounded changes

- Add one optional `DoorLockEventPolicy` tail to `DoorAccessEventProfile`.
- Reuse `DoorEventService.CompleteLockTransition` through the R081 RPG Kit adapter.
- Extend the existing door tab, canonical command, script/MCP projections and governed sample.
- Add focused validation, persistence and optional/required tests.
- Add one runtime node, one visual capture and exact Toolset/runtime R082 markers.
- Promote only `ORF-DOOR-013` after the complete portal passes.

All six bounded changes above are delivered. The optional lock-event tail is UUID-bearing and
versioned, validates exact terminal bindings, survives project/session persistence, uses the
existing world-feature command and history, is visible in the door tab and semantic summary,
projects through typed ForgeScript and filtered MCP reads, reuses the existing RPG Kit transition
guard and creates one explicit runtime lifecycle node.

## Targeted validation

- Domain lock-event policy and optional/required resolution: 2/2 tests passed.
- RPG Kit adapter plus invalid transition guards: 2/2 focused tests passed.
- World-feature command, typed/generic script projections and invalid dry-run: 1/1 test passed.
- Module-format project/session and governed-sample round trips: 2/2 tests passed.
- MCP filtered/searchable projection and dry-run plan: 1/1 test passed.
- Solution Debug build: 27 projects, 0 warning and 0 error.
- Toolset smoke:
  `OPENRPG_FORGE_TOOLSET_R082_DOOR_LOCK_EVENTS_PASS`.
- Runtime smoke:
  `OPENRPG_FORGE_RUNTIME_R082_DOOR_LOCK_EVENTS_PASS`.
- Visual evidence: `docs/aurora-parity/evidence/r082-door-lock-events-ui.png`, inspected with both
  exact bindings, the required-runtime flag, semantic payload summary and transaction controls
  visible together without overlap.

## Integrated closure

The integrated `scripts/Verify.ps1` portal passed after the targeted evidence. It built all 27
projects with zero warning and zero error, executed 1,581 .NET tests and 39 Python tests, retained
the exact R082 Toolset/runtime markers, and validated documentation, tracker/backlog freshness,
exports, 3D maps, asset delivery, MCP, network and save/resume before ending with
`OPENRPG_FORGE_VERIFICATION=PASS`. The Roslyn graph is fresh and deterministic at 7,103 nodes and
45,406 relations. The console transcript is retained at
`.artifacts/r082-full-verify-console.log`.

Only `ORF-DOOR-013` was promoted to `Tested`. R082 is closed; R083 is the next eligible lot.

## Non-objectives

- `R083`, encounter or later P1 lots;
- P2 and AV7–AV10;
- a new ruleset, engine fork, renderer or networking protocol;
- commit or push.
