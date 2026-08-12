# R094 — Placeable security and events acceptance

## Scope

`R094 / FC-PLACEABLE-P1-02` closes exactly five P1 functions:

- `ORF-PLC-014` — trap;
- `ORF-PLC-015` — initial treasure;
- `ORF-PLC-016` — OnOpen and OnClose;
- `ORF-PLC-017` — OnLock and OnUnlock;
- `ORF-PLC-019` — OnDisturbed.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/10_PLACEABLE.md` at lines 1034–1108,
1109–1183, 1184–1261, 1262–1339 and 1418–1495.

## Dependency reduction

TRIGGER contributes an immutable trigger reference and SCRIPT contributes versioned typed bindings;
neither owns placeable state. ITEM remains a referenced `ItemLootTablePolicy`, with deterministic rolls
performed by the existing RPG Kit. Lock state changes reuse the operations profile closed in R093 and
the existing security service instead of introducing a second lock implementation.

## Audited gap matrix

| Source requirement | Present before R094 | Real limit closed by R094 | Evidence |
|---|---|---|---|
| Trap carries trigger, payload, detection/disarm difficulty, reset, owner, VFX, friendly policy and one-shot state | Pure `TrapComponent` and `PlaceableSecurityService` | No canonical persisted profile or complete authoring/runtime path | `PlaceableTrapProfile`, adapter tests, Studio form and runtime trap controls |
| Initial treasure uses a loot table, init/open/reset timing, deterministic seed and saved generation state | Pure `LootSpawner` service | No project-owned table, persisted result contract or visible runtime results | `PlaceableLootSpawnerProfile`, seeded tests and accessible loot list |
| Open/close events preserve actor, result, previous state and authority | Pure container event factory | No versioned bindings carried from authoring through ForgeScript/runtime | `PlaceableContainerStateEventsProfile` and runtime event log |
| Lock changes expose controlled before/after events with method, credential, actor and veto | Pure `PlaceableEventService.ChangeLockState` | No canonical bindings, controlled-veto policy or full project projection | `PlaceableLockStateEventsProfile`, veto/change tests and runtime controls |
| Content disturbance emits one transactional delta with cause, revision and actor | Pure `ContentsChanged` algorithm | No authored binding or end-to-end persistence/UI/runtime proof | `PlaceableContentsChangedEventProfile`, adapter/MCP tests and runtime event node |

## Shared acceptance

- `PlaceableWorldProfile.SecurityEvents` owns stable UUIDs, schema and provenance for all five
  contracts; legacy documents preserve an optional null state and required runtime use reports
  `WORLD_PLACEABLE_SECURITY_EVENTS_REQUIRED_MISSING`.
- `SetWorldFeatureCatalogHandler` remains the sole mutation boundary with dry-run, apply, rejection,
  semantic patch, inverse, audit and project-level Undo/Redo.
- `WorldFeatureCatalogValidator` reports stable paths below
  `module/worldFeatures/placeables/{uuid}/securityEvents` for missing prerequisites, references,
  payload flags, schema/provenance and identity conflicts.
- ForgeScript and filtered MCP reads project the same typed profile and event bindings; MCP mutation
  remains dry-run by default.
- Runtime adaptation reuses `PlaceableSecurityService`, `PlaceableEventService` and the R093 lock
  adapter. No gameplay algorithm is duplicated in engine nodes.

## Delivered bounded changes

- Added one persisted security/events profile containing a trap, deterministic loot spawner and the
  three versioned event policies.
- Extended World Studio with complete trap, treasure, open/close, lock/unlock and disturbed sections,
  including semantic summary, validation feedback and transaction preview.
- Added one runtime adapter and one actual accessible panel with focusable detect, disarm, trigger,
  reset, spawn, open, close, unlock and disturb controls, plus loot and event lists.
- Updated Crypt of Ashes with a one-shot ember trap, conditional seeded treasure, saved generated
  state and the five typed ForgeScript bindings.

## Reproducible evidence

- Targeted results: 5/5 RPG Kit adapter tests, 21/21 affected catalog workflow tests and 1/1 MCP
  filtered/dry-run test.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R094_PLACEABLE_SECURITY_EVENTS_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R094_PLACEABLE_SECURITY_EVENTS_PASS`.
- Visual marker: `OPENRPG_FORGE_R094_PLACEABLE_SECURITY_EVENTS_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r094-placeable-security-events-ui.png`, inspected at
  native 1540×940. Trap controls and the treasure entry are readable without overlap; the semantic
  preview exposes all five canonical contracts and their payload fields.
- The integrated portal preserves both exact R094 engine markers, validates persistence and runs the
  consolidated .NET and Python suites before R095 may begin.

## Non-objectives

- the three remaining placeable functions reserved for R095;
- P2, AV7–AV10, commit or push.
