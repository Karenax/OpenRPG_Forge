# R093 — Placeable operations acceptance

## Scope

`R093 / FC-PLACEABLE-P1-01` closes exactly five P1 functions:

- `ORF-PLC-004` — container;
- `ORF-PLC-007` — initial animation state;
- `ORF-PLC-008` — destructibility;
- `ORF-PLC-009` — plot protection;
- `ORF-PLC-012` — locking.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/10_PLACEABLE.md` at lines 284–358,
509–583, 584–658, 659–733 and 884–958.

## Dependency reduction

ITEM remains an immutable inventory, loot-table or credential reference. GAMEPLAY contributes typed
damage and repair inputs without creating a second combat model. SCRIPT contributes versioned event
bindings and typed handles only. DM supplies explicit permissions and an audited override; MULTI is
limited to authored replication flags and does not introduce a parallel network protocol.

## Audited gap matrix

| Source requirement | Present before R093 | Real limit closed by R093 | Evidence |
|---|---|---|---|
| Container defines capacity, mass, ownership, access, persistence, loot, UI, replication and referenced inventory | Pure RPG Kit `ContainerComponent` and interaction service | No persisted project profile, authoring form or complete runtime projection | `PlaceableContainerProfile`, Studio section, adapter tests and accessible runtime panel |
| Initial visual state supports idle/open/closed/broken/active/custom, transitions and preview | Pure visual state machine service | No canonical authored state machine or transition preview across project/UI/runtime | `PlaceableVisualStateMachineProfile`, preview API and runtime smoke |
| Damageable placeable supports health, armor, resistances, visual stages, destruction, debris and repair | Pure `DamageableComponent` service | No persisted damage profile or functional engine representation | `PlaceableDamageableProfile`, adapter damage/repair tests and runtime nodes |
| Plot protection applies permissions, player feedback and audited GM override | Pure `ProtectionPolicy` authorization service | No canonical policy or auditable project/runtime path | `PlaceableProtectionProfile`, rejection/override tests and `PlaceableProtectionOverride` binding |
| Lock supports states, credentials, skill, code, hacking, relock, jam and permissions | Pure `LockComponent` security service | No persisted lock contract or end-to-end author/runtime controls | `PlaceableLockProfile`, adapter security tests and focusable runtime controls |

## Shared acceptance

- `PlaceableWorldProfile.Operations` owns stable UUIDs, schema and provenance for container, visual
  state machine, damageable, protection and lock data; legacy documents retain an optional null state.
- `SetWorldFeatureCatalogHandler` remains the sole mutation boundary with dry-run, apply, rejection,
  semantic patch, inverse, audit and project-level Undo/Redo.
- `WorldFeatureCatalogValidator` reports stable diagnostics below
  `module/worldFeatures/placeables/{uuid}/operations` and rejects missing required profiles, invalid
  components and identity conflicts.
- ForgeScript reads the same typed projection exposed by filtered MCP reads; MCP retains its existing
  dry-run/apply/revert transaction contract.
- Runtime adaptation reuses `PlaceableInteractionService` and `PlaceableSecurityService`; a missing
  optional profile is ignored and a missing required profile returns
  `WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING`.

## Delivered bounded changes

- Added one persisted placeable operations profile without duplicating interaction, damage,
  authorization or lock algorithms already present in the RPG Kit.
- Extended the World Studio placeable tab with complete container, six-state visual machine,
  destructibility, protection and lock sections plus empty/error/summary/preview states.
- Added one runtime adapter and one actual accessible panel with focusable open/close, damage, repair
  and unlock controls, an inventory list, visual state, health and feedback.
- Updated Crypt of Ashes with a persistent 24-slot party container, six visual states, damage/debris/
  repair, audited plot protection and a credential/skill/code/hacking/relock/jam lock profile.

## Reproducible evidence

- Targeted additions: 1 Application test and 5 RPG Kit tests; affected ModuleFormat, MCP and sample
  tests pass.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R093_PLACEABLE_OPERATIONS_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R093_PLACEABLE_OPERATIONS_PASS`.
- Visual marker: `OPENRPG_FORGE_R093_PLACEABLE_OPERATIONS_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r093-placeable-operations-ui.png`, inspected at native
  1540×940; the canonical container form and required-runtime toggle are readable without overlap,
  while the semantic preview proves the six visual states, transitions, damage, protection and lock.
- The integrated Toolset smoke reaches `OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS`, proving its generated
  playtest snapshot validates and starts in the separate runtime.
- The consolidated suite executes 1,653 .NET tests and 39 Python tests. The integrated portal preserves
  both exact R093 markers, restores the sample project byte-for-byte and ends with
  `OPENRPG_FORGE_VERIFICATION=PASS` before R094 begins.

## Non-objectives

- remaining placeable functions reserved for R094 and later lots;
- P2, AV7–AV10, commit or push.
