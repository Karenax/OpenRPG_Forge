# R089 — Item gameplay acceptance

## Scope

`R089 / FC-ITEM-P1-02` closes exactly five P1 functions:

- `ORF-ITEM-013` — plot item;
- `ORF-ITEM-014` — undroppable and transfer policy;
- `ORF-ITEM-015` — data-driven magical properties;
- `ORF-ITEM-016` — unique item power;
- `ORF-ITEM-018` — equipment and slots.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/09_ITEM.md` at lines 964–1038,
1039–1113, 1114–1188, 1189–1263 and 1339–1413.

## Dependency reduction

CREATURE is reduced to stable rig and equipment-slot identifiers. DM is reduced to an explicit,
audited override decision; it does not bypass the command boundary. MULTI is reduced to authoritative
transfer permissions and deterministic policy decisions. SCRIPT exposes typed read projections and
named hooks/events only, without file, process or native engine access.

## Audited gap matrix

| Source requirement | Present before R089 | Real limit closed by R089 | Evidence |
|---|---|---|---|
| Plot-item protection against destruction, sale, drop, theft, consumption and cleanup, with audited GM override | Pure RPG Kit critical policy | Protection was neither authorable nor persisted on the canonical item blueprint | Canonical critical profile, Studio section, command, round trip and runtime decisions |
| Per-destination transfer policy and permissions | Pure RPG Kit transfer algorithm | No project document, Toolset editor, ForgeScript/MCP projection or required/optional resolution | Eight canonical destinations, permission list, typed projections and adapter |
| Data-driven modifiers with parameters, stacking, conditions, UI summary and hooks | Pure RPG Kit modifier resolution | Modifier definitions were not authored or governed with the item | Versioned modifier policy, live resolved preview and runtime test |
| Unique ability binding with targeting, costs, charges, cooldown and typed events | Pure RPG Kit activation algorithm | Ability binding was not persisted, previewable or script-readable | Canonical binding, Studio fields, activation smoke and typed event names |
| Ruleset/rig slots, multi-slot, layers, conflicts and equipment preview | Pure RPG Kit equipment preview | Slot definitions were detached from the project and unavailable to creators | Canonical slot catalog, live conflict preview and five runtime nodes |

## Shared acceptance

- `ItemFoundationProfile.Gameplay` owns a stable UUID, schema, provenance, package manifest, SPDX
  license and SHA-256; legacy documents retain an optional null state.
- `SetItemFoundationHandler` remains the sole Toolset mutation path with dry-run, apply, rejection,
  semantic patch, inverse, audit and project-level Undo/Redo.
- `ItemFoundationValidator` localizes malformed governance, destination maps, modifiers, abilities and
  equipment references below `blueprints/{uuid}/itemFoundation/gameplay`.
- The R088 creation editor preserves an already authored gameplay profile, so editing one item facet
  cannot erase another.
- ForgeScript reads a typed gameplay view; MCP returns a filtered read projection while mutation stays
  dry-run.
- Runtime adaptation reuses `ItemInventoryService` and `ItemGameplayService`; an absent optional profile
  is ignored and a required one emits `item.gameplay.required_missing`.

## Delivered bounded changes

- Added one canonical P1 gameplay profile without replacing the existing item foundation, creation or
  advanced-item profiles.
- Added a dedicated Studio reachable from RPG Kit authoring, with five live sections, empty/error states,
  conflict preview and the standard transaction controls.
- Added a pure adapter from canonical data to the existing critical, transfer, modifier, ability and
  equipment services.
- Added typed ForgeScript and MCP projections plus five explicit runtime capability nodes.

## Reproducible evidence

- Targeted additions: 3 Domain tests and 2 RPG Kit tests; affected Application, ModuleFormat and MCP
  tests also pass.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R089_ITEM_GAMEPLAY_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R089_ITEM_GAMEPLAY_PASS`.
- Visual marker: `OPENRPG_FORGE_R089_ITEM_GAMEPLAY_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r089-item-gameplay-ui.png`, inspected at native 1440×920
  resolution; the protections, eight transfer destinations, modifier resolution, unique ability,
  equipment conflicts and transaction buttons are readable without overlap.
- Consolidated suite: 1,632 .NET tests and 39 Python tests; the integrated portal preserves both exact
  R089 markers, restores the sample project byte-for-byte and ends with
  `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectives

- loot, crafting, shops, identification and later item lots;
- P2, AV7–AV10, commit or push.
