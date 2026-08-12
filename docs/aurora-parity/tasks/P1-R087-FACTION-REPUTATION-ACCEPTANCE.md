# R087 — Faction reputation acceptance

## Scope

`R087 / FC-FACTION-P1-02` closes exactly four P1 functions:

- `ORF-FAC-007` — global reputation;
- `ORF-FAC-011` — semantic faction tags;
- `ORF-FAC-012` — matrix interface;
- `ORF-FAC-013` — player reaction.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/06_FACTION.md` at lines 466–535,
746–815, 816–885 and 886–955.

## Dependency reduction

SCRIPT reduces to typed reads and validated command mutation; no script receives file or engine-node
access. QA reduces to a reproducible matrix history, comparison and encounter simulation. DIALOGUE
supplies a bounded modifier and DM supplies the final explicit override; this lot adds no dialogue
graph or Live Director mutation path.

## Audited gap matrix

| Source requirement | Present before R087 | Real limit closed by R087 | Evidence |
|---|---|---|---|
| World/server/campaign/group/player reputation transactions | Reversible RPG Kit runtime state | Transactions were not persisted or authorable in the canonical project | Canonical profile, Studio rows, service and runtime tests |
| Semantic faction tags for rules and queries | Tags on definitions and a raw query helper | Tags were not validated as normalized identifiers and ForgeScript had no typed effective-tag query | Stable diagnostics and typed inherited-tag query |
| Editable heatmap, comparison, simulation, import/export and history | Directional relationship editor plus stateless service helpers | Captures and comparison history were not canonical, visible or agent-readable | Persisted snapshots, Studio preview, MCP projection and round trip |
| Reputation/disguise/crime/dialogue/zone/GM reaction | Runtime resolver accepted ad-hoc modifiers | The enablement and default reputation scope were not authorable or persisted | Canonical policy, stable order, Studio and runtime proof |

## Shared acceptance

- `FactionCatalog.Reputation` owns UUID, schema, provenance, transactions, heatmap history and the
  reaction policy; legacy documents keep an optional empty state.
- `SetFactionCatalogHandler` remains the sole Toolset mutation path with dry-run/apply/reject,
  semantic patch, audit and project-level undo/redo.
- `FactionValidator` localizes malformed tags, UUID collisions, scopes, references, values,
  snapshots and reaction policy fields below `module/factions`.
- ForgeScript reads through a typed profile handle; MCP filters transactions and heatmap relations
  to the selected faction IDs while mutation remains dry-run.
- Runtime resolution ignores an absent optional profile and returns one stable diagnostic when the
  profile is required.

## Delivered bounded changes

- Added a canonical reputation profile with all five required scopes, reversible transactions,
  two versioned heatmap captures and one persisted reaction policy.
- Reused `FactionReputationService` for effective scores, transaction inverse, tag queries,
  heatmap capture/compare/simulate/import/export and the stable reaction chain.
- Extended safe faction removal to migrate or prune global transactions and historical matrix
  references together with the existing canonical references.
- Extended the existing faction Studio with one `RÉPUTATION P1` tab and the existing transaction,
  undo/redo and persistence path; no parallel store was introduced.
- Added typed ForgeScript reads, filtered MCP projections and explicit runtime nodes for the
  reaction policy, transactions and heatmap history.

## Reproducible evidence

- Targeted additions: 3 Domain + 2 RPG Kit + 2 Application tests; affected Domain, RPG Kit,
  Application, ModuleFormat and MCP faction suites pass.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R087_FACTION_REPUTATION_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R087_FACTION_REPUTATION_PASS`.
- Visual marker: `OPENRPG_FORGE_R087_FACTION_REPUTATION_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r087-faction-reputation-ui.png`, inspected at native
  1540×940 resolution with five scopes, two history rows, seven reaction controls and the matrix,
  comparison and encounter-simulation summary readable without overlap.
- Consolidated suite: 1,622 .NET tests and 39 Python tests; the integrated portal preserves the
  exact Toolset/runtime R087 markers, restores the sample project byte-for-byte and ends with
  `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectives

- R088 or later lots before the R087 portal is green;
- faction inheritance, witnesses or dynamic diplomacy beyond their existing G13-G boundary;
- P2, AV7–AV10, commit or push.
