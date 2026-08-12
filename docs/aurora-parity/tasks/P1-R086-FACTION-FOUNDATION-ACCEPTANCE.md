# R086 — Faction foundation acceptance

## Scope

`R086 / FC-FACTION-P1-01` closes exactly five P1 functions:

- `ORF-FAC-001` — faction creation, archive and safe removal;
- `ORF-FAC-003` — directional reputation matrix;
- `ORF-FAC-004` — asymmetric relations;
- `ORF-FAC-005` — ruleset disposition bands;
- `ORF-FAC-006` — personal reputation overlays.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/06_FACTION.md` at lines 46–115,
186–255, 256–325, 326–395 and 396–465.

## Dependency reduction

BUILD reduces to canonical validation before project serialization. GAMEPLAY reduces to bounded
disposition bands and a resolved label; R086 adds no combat AI. MULTI reduces to typed actor,
player or group subjects and persisted expiration/cause; authoritative global transactions remain
outside this lot.

## Audited gap matrix

| Source requirement | Present before R086 | Real limit closed by R086 | Evidence |
|---|---|---|---|
| Create/archive/remove with member migration | `FactionAuthoringService` and full catalog command | Advanced memberships and canonical references were not migrated; archive was not authorable in UI | Safe removal tests and Studio field |
| Directional matrix, presets and heatmap | Canonical relationships and RPG heatmap service | No visible heatmap/band editing and MCP omitted matrix data | Studio preview, filtered MCP projection |
| Independent A→B and B→A with explanation | Independent relationship records | No typed foundation script read or dedicated runtime proof | Typed handles and runtime nodes |
| Configurable disposition bands and overrides | Canonical bands and resolver | Bands were preserved but not authorable or fully validated | Studio fields and stable diagnostics |
| Actor/player/group personal reputation | Runtime-only RPG record | Not stored in the project, UI, MCP or engine scene | Canonical tail, round trip and runtime node |

## Shared acceptance

- The existing `SetFactionCatalogHandler` remains the only Toolset mutation path and supports
  dry-run/apply/reject with semantic patch, audit and project-level undo/redo.
- UUID, schema, provenance, references, values, unique directional pairs, bands and personal
  overlays have stable validation codes and document paths.
- JSON workspace round trip preserves the canonical catalog exactly.
- ForgeScript reads faction definitions and relations only through typed handles; MCP reads filtered
  matrix/band/personal projections and mutation remains dry-run.
- Empty optional foundations resolve safely and required empty foundations return one diagnostic.

## Delivered bounded changes

- Extended the canonical catalog with persisted actor/player/group personal-reputation overlays and
  an explicit required-runtime policy while preserving legacy empty documents.
- Completed stable validation for duplicate faction UUIDs, directional pairs, band IDs/bounds and
  personal identity, references, deltas and causes.
- Extended safe removal to migrate legacy and advanced memberships and clean or migrate every
  dependent canonical faction reference.
- Extended the existing faction window and catalog command with archive, bands, personal overlays
  and a directional heatmap; no parallel store or mutation path was introduced.
- Added typed definition handles, filtered MCP matrix/band/personal projections and explicit engine
  nodes for definitions, relationships and personal overrides.

## Reproducible evidence

- Targeted additions: 3 Domain + 2 RPG Kit + 2 Application tests; the affected faction suites and
  existing ModuleFormat/MCP coverage all pass.
- Consolidated suite: 1,615 .NET tests and 39 Python tests.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R086_FACTION_FOUNDATION_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R086_FACTION_FOUNDATION_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r086-faction-foundation-ui.png`, inspected at native
  resolution with archive state, both directional scores, three bands, the player override and the
  heatmap summary readable together without overlap.
- The first canonical save normalized the already-added P1 defaults; a second complete portal
  proved byte-stable project restoration and ended with `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectives

- R087 or later lots before the R086 portal is green;
- global reputation transactions, final matrix workflow, player reaction rules or later faction UI;
- P2 hierarchy extensions, AV7–AV10, commit or push.
