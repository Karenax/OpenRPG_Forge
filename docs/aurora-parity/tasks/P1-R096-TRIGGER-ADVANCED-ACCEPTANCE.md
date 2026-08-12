# R096 — Trigger advanced acceptance

## Scope

`R096 / FC-TRIGGER-P1-01` closes exactly four P1 functions:

- `ORF-TRG-004` — trap trigger;
- `ORF-TRG-011` — typed target filter;
- `ORF-TRG-017` — gameplay volumes;
- `ORF-TRG-019` — occupancy visualization.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/12_TRIGGER.md` at lines 280–350,
817–887, 1267–1337 and 1417–1487.

## Dependency reduction

SCRIPT contributes versioned trap and volume bindings. FACTION contributes stable faction IDs to the
query without duplicating the faction catalog. GAMEPLAY contributes typed effect parameters. QA
consumes the bounded overlay and trace. Detection, disarm, reset, target matching, ten-volume effects
and debug projection reuse `TriggerAdvancedService`, which already existed before R096.

## Audited gap matrix

| Source requirement | Present before R096 | Real limit closed by R096 | Evidence |
|---|---|---|---|
| Trap detection, disarm, payload, reset, ownership and GM visualization | Pure RPG Kit `TrapVolume` service and unit test | No canonical authoring, validation, persistence, script/MCP or engine path | `TriggerAdvancedProfile`, adapter, Studio trap section and runtime `Area3D` |
| Typed query for players, tags, factions, components, owner, team and sandbox predicate | Pure `EntityQuery` matcher | Query was not owned by a Trigger document or exposed to author/runtime | target-filter profile, validator, adapter and exact-match tests |
| Damage, Heal, Gravity, Water, Climb, NoBuild, Camera, Audio, Weather and Checkpoint | Pure ten-entry library | No persisted complete library, editable parameters or typed bindings | ten canonical volume profiles, Studio fields, ForgeScript/MCP and ten engine areas |
| Occupants, last fires, cooldowns, filters and event trace | Pure debug projection | No persisted display policy, trace bound or accessible runtime view | occupancy profile, bounded adapter, panel and visual proof |

## Shared acceptance

- `TriggerWorldProfile.Advanced` owns stable UUIDs, schema, provenance, bindings and explicit policies;
  geometry, transform, blueprint and overrides remain owned by the canonical foundation.
- Missing optional data is ignored; required runtime use reports
  `WORLD_TRIGGER_ADVANCED_REQUIRED_MISSING`.
- `SetWorldFeatureCatalogHandler` remains the sole mutation boundary with dry-run, semantic diff,
  rejection, audit and project-level Undo/Redo.
- `WorldFeatureCatalogValidator` localizes trap, query, complete volume library, debug policy and UUID
  diagnostics below `module/worldFeatures/triggers/{uuid}/advanced`.
- ForgeScript and filtered MCP reads expose the same typed profile. Runtime nodes are projections and
  never become the source of truth.

## Delivered bounded changes

- Added one canonical advanced Trigger profile and one adapter to the existing RPG Kit services.
- Extended World Studio with explicit trap, target-query, ten-volume and occupancy-debug controls.
- Added an accessible runtime panel with focusable detect, disarm, reset and apply actions, a complete
  volume list and an occupancy/event trace.
- Updated Crypt of Ashes with a trap, exact target query, all ten gameplay volumes and bounded debug
  overlay; project serialization remains byte-stable after round-trip.

## Reproducible evidence

- Targeted results: 4/4 adapter tests, 1/1 canonical command/validation test, 1/1 filtered MCP test and
  4/4 selected sample persistence tests.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R096_TRIGGER_ADVANCED_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R096_TRIGGER_ADVANCED_PASS`.
- Visual marker: `OPENRPG_FORGE_R096_TRIGGER_ADVANCED_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r096-trigger-advanced-ui.png`, inspected at native
  1540×940. The trap authoring controls are readable without overlap; the semantic preview displays
  trap payload/difficulties, the complete typed query, 10/10 volume kinds and all five debug facets.
- The integrated portal preserves both exact R096 engine markers, validates byte-stable persistence
  and executes the consolidated .NET and Python suites before R097 may begin.

## Non-objectives

- Trigger P2/P3 functions;
- P2, AV7–AV10, commit or push.
