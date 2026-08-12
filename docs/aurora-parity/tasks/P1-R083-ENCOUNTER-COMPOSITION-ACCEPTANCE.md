# R083 — Encounter composition acceptance

## Scope

`R083 / FC-ENCOUNTER-P1-01` closes exactly five P1 functions:

- `ORF-ENC-001` — encounter wizard;
- `ORF-ENC-004` — encounter difficulty;
- `ORF-ENC-005` — minimum and maximum counts;
- `ORF-ENC-006` — challenge/party-driven selection;
- `ORF-ENC-007` — one-shot or continuous activation.

Source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/13_ENCOUNTER.md` at lines 52–120,
271–339, 344–412, 417–485 and 490–558.

## Dependency reduction

CREATURE reduces to the already canonical candidate blueprint IDs and RPG Kit encounter roles.
MULTI reduces to the authoritative network actor cap. QA reduces to deterministic seeded previews
and stable diagnostics. SCRIPT reduces to an optional activation binding. R083 does not open manual
spawn points, player restrictions, lifecycle events, waves/phases, statistical production analysis
or P2 encounter functions.

## Audited gap matrix

| Source requirement | Present before R083 | Real limit to close | Required evidence |
|---|---|---|---|
| Wizard for role, theme, zone, difficulty, groups, budget, activation and spawn policy | Isolated `EncounterAuthoringService` plus tests | Not stored or authorable on canonical `EncounterWorldProfile` | Canonical composition tail, UI, command and round trip |
| Ruleset difficulty with party estimator and multipliers | Isolated `EncounterDifficultyProfile` | No canonical provider identity or project validation | Typed policy, validator, adapter and preview |
| Per-group minimum/maximum under budget/server cap | Isolated `CountRange` | Not persisted or surfaced in the Toolset | Group profile, invalid dry-run and summary |
| Data-driven party assessment and composition preview | Seeded RPG Kit preview | No canonical selection policy, script/MCP handle or runtime representation | Adapter, deterministic preview and bounded projections |
| Seven activation modes | RPG Kit enum and validation | Not attached to canonical encounter authoring/runtime | Optional policy, interval/script validation and runtime node |

## Acceptance matrix

| Shared acceptance criterion | Proof required for closure |
|---|---|
| Author all five functions without generated-file edits | Dedicated composition sections in the encounter tab, empty/error state and preview |
| Save, close and reload preserves canonical data | JSON/session and governed-sample round trips |
| Invalid configuration is refused before build | Stable diagnostics for identity, UUIDs, refs, budgets, counts, provider and activation |
| UI and command converge | Existing `SetWorldFeatureCatalogHandler` dry-run/apply path |
| Undo/redo restore canonical documents | Existing Toolset command history smoke |
| Runtime optional/required behavior is safe | Resolver plus nominal/failure headless smoke |
| Runtime and agents stay bounded | Typed ForgeScript handle, filtered MCP read/dry-run and five explicit runtime nodes |

## Delivered bounded changes

- Added one optional `EncounterCompositionProfile` tail to `EncounterWorldProfile`.
- Reused the existing RPG Kit authoring and composition services through one adapter, including the
  explicit translation from canonical candidate IDs to RPG Kit creature IDs.
- Extended the existing encounter tab and shared world-feature command; no parallel store was added.
- Added typed/generic script and MCP projections, governed sample data and five explicit runtime nodes.
- Added stable diagnostics for identity, UUIDs, references, budgets, counts, provider and activation.
- Promoted only the five R083 tracker rows after targeted tests, visual inspection and both engine
  smokes passed.

## Reproducible evidence

- `dotnet test` targeted: 4 Domain + 3 RPG Kit + 2 Application + 1 ModuleFormat + 1 MCP, all passed.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R083_ENCOUNTER_COMPOSITION_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R083_ENCOUNTER_COMPOSITION_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r083-encounter-composition-ui.png`, inspected with the
  difficulty, ruleset, party provider, deterministic budget and semantic summary visible.
- Integrated portal: `scripts/Verify.ps1`; final consolidated counts are recorded in `STATUS.md`.

## Non-objectives

- `R084` or later P1 lots;
- P2 and AV7–AV10;
- new ruleset formulas, renderer, engine fork or networking protocol;
- commit or push.
