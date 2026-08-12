# R095 — Placeable composition and events acceptance

## Scope

`R095 / FC-PLACEABLE-P1-03` closes exactly three P1 functions:

- `ORF-PLC-020` — OnDamaged and OnDeath;
- `ORF-PLC-025` — sockets and assembly;
- `ORF-PLC-026` — multi-verb interaction.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/10_PLACEABLE.md` at lines 1496–1570,
1886–1960 and 1961–2035.

## Dependency reduction

SCRIPT contributes versioned event bindings without owning health. PALETTE contributes an immutable
child `BlueprintId` and optional prefab reference without creating a second asset catalog. GAMEPLAY
contributes contextual tags only. Damage payloads, attachment snap and verb ordering reuse the pure
RPG Kit services already present before R095.

## Audited gap matrix

| Source requirement | Present before R095 | Real limit closed by R095 | Evidence |
|---|---|---|---|
| DamageApplied and EntityDestroyed share a uniform entity payload | Pure `PlaceableEventService.DamageEvent` | No canonical bindings, payload policy or end-to-end author/runtime path | `PlaceableDamageEventsProfile`, adapter tests and runtime damage node |
| Typed attachment sockets support local transform, tags, snap, capacity, child blueprint and composed prefab | Pure `Attach` algorithm with patch/inverse | No persisted socket/attachment composition, reference validation or visible runtime representation | socket/attachment profiles, validator, snapped engine nodes and panel |
| Inspect, Use, Open, Talk, Take, Push, Climb and custom verbs are context-filtered and ordered | Pure `ResolveVerbs` algorithm | No complete authored verb catalog or project/ForgeScript/MCP/runtime projection | `PlaceableMultiVerbProfile`, contextual tests, Studio form and runtime verb list |

## Shared acceptance

- `PlaceableWorldProfile.CompositionEvents` owns stable UUIDs, schema, provenance, transforms,
  blueprint references and explicit bindings; legacy documents preserve an optional null state.
- Missing optional data is ignored; required runtime use reports
  `WORLD_PLACEABLE_COMPOSITION_EVENTS_REQUIRED_MISSING`.
- `SetWorldFeatureCatalogHandler` remains the sole mutation boundary with dry-run, semantic patch,
  inverse, rejection, audit and project-level Undo/Redo.
- `WorldFeatureCatalogValidator` localizes identity, damage payload, socket, attachment, capacity,
  blueprint and verb diagnostics below `module/worldFeatures/placeables/{uuid}/compositionEvents`.
- ForgeScript and filtered MCP reads expose the same typed profile. Runtime nodes are projections and
  never become the source of truth.

## Delivered bounded changes

- Added one canonical composition/events profile with uniform damage events, typed sockets,
  reversible attachments and eight contextual verbs.
- Extended World Studio with editable damage payload, socket geometry/capacity, child blueprint,
  composed prefab, standard/custom verb, context and priority fields.
- Added one adapter and an accessible runtime panel with focusable damage, destroy, assemble and verb
  actions plus socket and verb lists.
- Updated Crypt of Ashes with a snapped lever prefab and the complete standard plus custom verb set.

## Reproducible evidence

- Targeted results: 4/4 RPG Kit adapter tests, 1/1 validation test, 1/1 MCP filtered/dry-run test and
  1/1 sample persistence/validation test.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R095_PLACEABLE_COMPOSITION_EVENTS_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R095_PLACEABLE_COMPOSITION_EVENTS_PASS`.
- Visual marker: `OPENRPG_FORGE_R095_PLACEABLE_COMPOSITION_EVENTS_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r095-placeable-composition-events-ui.png`, inspected at
  native 1540×940. Uniform damage bindings and payload flags plus the socket section are readable
  without overlap; the semantic preview lists the snapped assembly and all eight verbs.
- The integrated portal preserves both exact R095 engine markers, validates byte-stable persistence
  and executes the consolidated .NET and Python suites before R096 may begin.

## Non-objectives

- placeable P2 functions;
- P2, AV7–AV10, commit or push.
