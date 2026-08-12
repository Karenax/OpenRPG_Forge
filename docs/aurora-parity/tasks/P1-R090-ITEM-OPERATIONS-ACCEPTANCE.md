# R090 — Item operations acceptance

## Scope

`R090 / FC-ITEM-P1-03` closes exactly three P1 functions:

- `ORF-ITEM-024` — loot tables;
- `ORF-ITEM-030` — inventory inspector;
- `ORF-ITEM-031` — item lifecycle events.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/09_ITEM.md` at lines 1789–1863,
2239–2313 and 2314–2388.

## Dependency reduction

QA is reduced to deterministic seeded rolls, statistical preview and localized validation. SHELL is
reduced to the existing command/Undo/Redo boundary and a dedicated Studio; no parallel scene mutation
is introduced. SCRIPT is reduced to API 1.0 event bindings and a typed read view without direct file,
process or native engine access.

## Audited gap matrix

| Source requirement | Present before R090 | Real limit closed by R090 | Evidence |
|---|---|---|---|
| Weighted loot groups, conditions, quantities, quality, seed, uniqueness and statistical preview | Tested pure `ItemGameplayService` algorithms | Loot tables were detached from item blueprints and unavailable to creators, ForgeScript, MCP and runtime | Canonical table, Studio preview, adapter and seeded smoke |
| Drag-drop containers/slots, filters, validation, visual preview, comparison and multi-edit | Tested pure `InventoryAuthoringService` | Inventory documents were not authorable or persisted with the item, and no product UI exposed the complete workflow | Canonical inspector document, live multi-edit preview, filters and round trip |
| Nine item lifecycle events | Typed enum and pure emitter | Handlers, authority and ForgeScript API version were not canonical bindings | Nine versioned bindings, typed view, MCP projection and runtime events |

## Shared acceptance

- `ItemFoundationProfile.Operations` owns stable profile/table/inspector/binding UUIDs, schema,
  provenance, package manifest, SPDX license and SHA-256; legacy documents retain an optional null state.
- `SetItemFoundationHandler` remains the sole project mutation path with dry-run, apply, rejection,
  semantic patch, inverse, audit and project-level Undo/Redo.
- `ItemFoundationValidator` localizes invalid governance, loot entries, placements and lifecycle bindings
  below `blueprints/{uuid}/itemFoundation/operations`.
- The R088 foundation editor preserves R089/R090 facets when editing creation data.
- ForgeScript reads one typed operations view; MCP returns the same filtered projection while mutation
  stays dry-run.
- Runtime adaptation reuses `ItemGameplayService` and `InventoryAuthoringService`; an absent optional
  profile is ignored and a required one emits `item.operations.required_missing`.

## Delivered bounded changes

- Added one canonical operations profile without replacing earlier item or advanced-item documents.
- Added a dedicated Studio reachable from RPG Kit authoring, with loot, inventory and lifecycle sections,
  empty/error states, seeded roll, live filters and multi-edit comparison.
- Added a pure adapter to the existing loot, inventory-authoring and event services.
- Added typed ForgeScript/MCP projections and three explicit runtime capability nodes.

## Reproducible evidence

- Targeted additions: 3 Domain tests and 2 RPG Kit tests; affected Application, ModuleFormat and MCP
  tests pass.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R090_ITEM_OPERATIONS_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R090_ITEM_OPERATIONS_PASS`.
- Visual marker: `OPENRPG_FORGE_R090_ITEM_OPERATIONS_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r090-item-operations-ui.png`, inspected at native 1440×920;
  two loot entries, deterministic roll, two placements, multi-edit preview and all nine events are readable
  without overlap.
- The consolidated suite executes 1,637 .NET tests and 39 Python tests. The integrated portal preserves
  both exact R090 markers, restores the sample project byte-for-byte and ends with
  `OPENRPG_FORGE_VERIFICATION=PASS` before R091 begins.

## Non-objectives

- merchants, placeables and later P1 lots;
- P2, AV7–AV10, commit or push.
