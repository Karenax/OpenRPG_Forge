# R088 — Item creation acceptance

## Scope

`R088 / FC-ITEM-P1-01` closes exactly five P1 functions:

- `ORF-ITEM-001` — item creation wizard;
- `ORF-ITEM-005` — customizable appearance;
- `ORF-ITEM-006` — weight and encumbrance;
- `ORF-ITEM-008` — stacks;
- `ORF-ITEM-009` — charges and uses.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/09_ITEM.md` at lines 64–138,
364–438, 439–513, 589–663 and 664–738.

## Dependency reduction

CONTENT is reduced to governed asset identifiers, package provenance, SPDX license and content hash.
MULTI is reduced to a revision-checked atomic stack operation and an explicit authoritative replication
event. PALETTE supplies the existing project item blueprint selected by the wizard. SCRIPT exposes a
typed read view and validated command mutation only; this lot adds no direct file or engine-node access.

## Audited gap matrix

| Source requirement | Present before R088 | Real limit closed by R088 | Evidence |
|---|---|---|---|
| Wizard for archetype, appearance, slots, quality, level, property, provenance and generation rules | Transactional RPG Kit creation request and R044 item foundation editor | Generation rules and the complete P1 composition were not canonical, visible together or runtime-readable | Canonical creation profile, live Studio form, command and typed view |
| Visual definition independent of rules | Isolated RPG Kit record with meshes, materials, sockets, icon, VFX and equipped preview | Visual data was not persisted in the project item foundation or available to Toolset/MCP/runtime | Appearance record, round trip, preview card and adapter |
| Mass, volume, footprint and stack contribution | Tested RPG Kit policy calculations | Inventory policy was not authorable or stored in the canonical project | Canonical policy, live fields and contribution tests |
| Maximum, compatibility key, atomic merge/split and replication | Revision-checked RPG Kit stack operations | Stack policy was not connected to an authored blueprint or required/optional runtime resolution | Canonical policy, adapter and Toolset/runtime smokes |
| Generic charges, rest uses, cooldown, durability, consumable and recharge | Tested RPG Kit usage resource | Usage state and policy were not authorable, persisted, script-readable or agent-readable | Canonical policy, typed views, round trip and consumption test |

## Shared acceptance

- `ItemFoundationProfile.Creation` owns stable profile/appearance UUIDs, schema, provenance, package
  manifest, SPDX license, SHA-256 and the five bounded capability records; legacy documents keep an
  optional null state.
- `SetItemFoundationHandler` remains the sole Toolset mutation path with dry-run, apply, rejection,
  semantic patch, inverse, audit and project-level Undo/Redo.
- `ItemFoundationValidator` localizes malformed wizard sections, bindings, ranges, stack keys and
  usage values below `blueprints/{uuid}/itemFoundation/creation`.
- ForgeScript reads a typed creation view; MCP returns a filtered read projection while mutation stays
  dry-run.
- Runtime adaptation reuses `ItemInventoryService`; an absent optional creation profile is ignored and
  a required one emits `item.creation.required_missing`.

## Delivered bounded changes

- Added one canonical P1 creation profile without replacing the existing item foundation or advanced
  item profiles.
- Extended the existing item Studio with generation, appearance, inventory, stack and usage sections,
  live rule summary and an equipped-preview card.
- Added a pure RPG Kit adapter from canonical data to the existing mass, stack and usage runtime.
- Added typed ForgeScript and MCP projections and five explicit runtime capability nodes.

## Reproducible evidence

- Targeted additions: 3 Domain tests and 2 RPG Kit tests; affected Domain, RPG Kit, Application,
  ModuleFormat and MCP suites pass.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R088_ITEM_CREATION_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R088_ITEM_CREATION_PASS`.
- Visual marker: `OPENRPG_FORGE_R088_ITEM_CREATION_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r088-item-creation-ui.png`, inspected at native 1440×920
  resolution; the eight authoring/summary sections, six appearance bindings, inventory footprint,
  stack policy, usage resource and five transaction buttons are readable without overlap.
- Consolidated suite: 1,627 .NET tests and 39 Python tests; the integrated portal preserves the exact
  Toolset/runtime R088 markers, restores the sample project byte-for-byte and ends with
  `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectives

- item identification, ownership, plot flags, transfer/equipment rules or later item lots;
- P2, AV7–AV10, commit or push.
