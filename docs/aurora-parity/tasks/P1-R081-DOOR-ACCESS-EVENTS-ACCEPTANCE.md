# R081 — Door access and event acceptance

## Scope

`R081 / FC-DOOR-P1-01` closes exactly five P1 functions:

- `ORF-DOOR-004` — lock and key;
- `ORF-DOOR-005` — failed opening;
- `ORF-DOOR-006` — door trap;
- `ORF-DOOR-011` — `OnAreaTransitionClick`;
- `ORF-DOOR-012` — `OnOpen` and `OnClose`.

Source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/11_DOOR.md` at lines 278–501 and
806–957. The shared contract requires canonical UUID-bearing data, schema/provenance, an
engine-independent transform and blueprint/asset identity, explicit overrides, transactional
authoring, stable localized diagnostics, optional-safe/required-explicit runtime behavior, typed
ForgeScript reads, filtered MCP reads with dry-run mutation, persistence and undo/redo.

## Dependency reduction

ITEM and PLACEABLE reduce to the already tested shared credential, skill-check, lock and trap
services. SCRIPT reduces to typed event bindings and bounded handles. AUDIO reduces to governed
sound asset identity. TRIGGER reduces to the existing side/event trap dispatch contract. R081 does
not open door destruction/loading, `OnLock`/`OnUnlock`, P2 door functions or a new network protocol.

## Audited gap matrix

| Source requirement | Present before R081 | Real limit to close | Required evidence |
|---|---|---|---|
| Shared lock with credentials, skill checks, relock and consumption | Isolated RPG Kit service and tests | Not stored on the canonical door profile or authorable | Domain profile, adapter, UI and round trip |
| `DoorOpenRejected` with reason, localization, sound, animation and script | Isolated RPG Kit event factory | No canonical rejection policy, validation or projection | Rejection simulation, diagnostics and runtime node |
| Detectable/disarmable trap attached to sides/events with payload, reset and ownership | Isolated RPG Kit trap binding | Not persisted or authorable on a door | Trap validation, adapter and visible preview |
| `PortalUseRequested` with actor, destination, cause, conditions and veto | Isolated RPG Kit event service | No canonical event binding or blueprint/runtime representation | Typed request/completion simulation and marker |
| `DoorOpened`/`DoorClosed` after terminal transitions | Isolated terminal-state guard | No canonical bindings, typed handle or runtime nodes | Terminal-event tests and runtime proof |

## Acceptance matrix

| Shared acceptance criterion | Proof required for closure |
|---|---|
| Author all five functions without editing generated files | Door tab with dedicated access/event sections, empty/error state and preview |
| Save, close and reload exactly preserves canonical data | JSON/session round trip and governed sample reload |
| Invalid configuration is rejected before build | Stable diagnostics for UUIDs, references, localization, ranges and bindings |
| UI and command mutation converge | Existing `SetWorldFeatureCatalogHandler` dry-run/apply path |
| Undo and redo restore canonical documents | Existing Toolset command history smoke |
| Runtime optional/required behavior is safe | Resolver and runtime smoke |
| Runtime and agents stay bounded | Typed ForgeScript handle, filtered MCP resource and dry-run semantic mutation |

## Delivered bounded changes

- Add one optional `DoorAccessEventProfile` tail to `DoorWorldProfile`.
- Reuse shared RPG Kit lock/trap/event services through one adapter.
- Extend the existing door authoring tab and shared world-feature command; no parallel storage.
- Add typed script/MCP projections, governed sample data and explicit runtime nodes.
- Add targeted tests, a reproducible UI capture and exact Toolset/runtime smoke markers.
- Promote only the five R081 tracker rows after the complete portal passes.

All six bounded changes above are delivered. `DoorWorldProfile` now carries the optional canonical
access/event tail, whose nested UUID-bearing lock, rejection, trap, portal and terminal-state
profiles are validated locally and across the project catalog. The same document is persisted,
edited through the existing transactional world-feature command, projected through typed
ForgeScript and filtered MCP reads, adapted to the pre-existing RPG Kit services and represented by
real runtime nodes. No parallel door store or engine-specific domain type was introduced.

## Targeted validation

- Domain access/event profile: 4/4 tests passed.
- RPG Kit adapter plus existing lock, trap and door-event services: 10/10 tests passed.
- World-feature command and cross-door UUID validation: 2/2 focused tests passed.
- Module-format world-feature and governed-sample round trips: 6/6 tests passed.
- MCP filtered world-feature projection: 1/1 focused test passed.
- Solution Debug build: 27 projects, 0 warning and 0 error.
- Toolset smoke:
  `OPENRPG_FORGE_TOOLSET_R081_DOOR_ACCESS_EVENTS_PASS`.
- Runtime smoke:
  `OPENRPG_FORGE_RUNTIME_R081_DOOR_ACCESS_EVENTS_PASS`.
- Visual evidence: `docs/aurora-parity/evidence/r081-door-access-events-ui.png`, inspected with the
  canonical asset/transform, required-runtime flag, lock state, credential and semantic summary for
  rejection, trap, portal request/completion and terminal state visible without overlap.

## Integrated closure

The integrated `scripts/Verify.ps1` portal passed after the targeted evidence. It built all 27
projects with zero warning and zero error, executed 1,576 .NET tests and 39 Python tests, retained
the exact R081 Toolset/runtime markers, and validated documentation, tracker/backlog freshness,
exports, 3D maps, asset delivery, MCP, network and save/resume before ending with
`OPENRPG_FORGE_VERIFICATION=PASS`. The Roslyn graph is fresh and deterministic at 7,099 nodes and
45,383 relations. The console transcript is retained at
`.artifacts/r081-full-verify-console.log`.

Only `ORF-DOOR-004`, `ORF-DOOR-005`, `ORF-DOOR-006`, `ORF-DOOR-011` and `ORF-DOOR-012` were promoted
to `Tested`. R081 is closed; R082 is the next eligible lot.

## Non-objectives

- `R082`, encounter or later P1 lots;
- P2 and AV7–AV10;
- a new renderer, engine fork, ruleset or networking protocol;
- commit or push.
