# R085 — Encounter statistical preview acceptance

## Scope

`R085 / FC-ENCOUNTER-P1-03` closes exactly `ORF-ENC-018` — statistical preview.
The complete source criterion was audited in
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/13_ENCOUNTER.md` at lines 1307–1375.

## Audited gap

`EncounterWaveService.Simulate` already runs deterministic consecutive seeds and aggregates
composition, cost, duration, reward and cost variance. The real remaining limit is that N, the base
seed and duration/reward estimators are not canonical, authorable, persisted, script/MCP-readable or
represented in the runtime. R085 reuses that service and adds no balance formula to the engine.

## Acceptance

- one optional versioned statistics profile with UUID, provenance, bounded run count, seed,
  estimator identities, coefficients, percentiles and explicit overrides;
- dedicated Studio preview section using the existing world-feature command and undo/redo;
- deterministic result with per-run composition/cost/duration/reward, variances and percentiles;
- stable invalid/optional/required diagnostics before build;
- JSON round trip, typed ForgeScript handle, filtered dry-run MCP and explicit runtime node;
- targeted tests, inspected visual proof, Toolset/runtime markers and integrated portal before the
  single tracker row is promoted.

## Delivered bounded changes

- Added one optional `EncounterStatisticsProfile` tail with stable UUID, schema, provenance, run
  count, base seed, typed estimator identities and coefficients, percentiles and overrides.
- Reused `EncounterWaveService.Simulate`; it now reports count, cost, duration and reward variances
  plus nearest-rank percentiles without moving ruleset formulas into the engine.
- Added the Studio statistical-preview section to the existing world-feature command and history,
  with the governed sample configured for 64 consecutive seeds starting at 840.
- Added one typed ForgeScript service, the filtered generic MCP projection, JSON round-trip checks
  and one explicit runtime statistics node.
- Added stable diagnostics for identity, bounded configuration, sorted unique percentiles and
  overrides, including optional-safe and required-diagnostic runtime resolution.

## Reproducible evidence

- Targeted tests: 3 Domain + 2 RPG adapter + 1 Application + 1 ModuleFormat + 1 MCP, all passed.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R085_ENCOUNTER_STATISTICS_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R085_ENCOUNTER_STATISTICS_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r085-encounter-statistics-ui.png`, inspected at native
  resolution with required policy, 64 runs, seed 840, both estimator identities and coefficients,
  percentile summary and semantic preview visible without overlap.
- Promoted only `ORF-ENC-018` after the integrated `scripts/Verify.ps1` portal passed with 1,608
  .NET tests, 39 Python tests and `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectives

- R086 or later lots before this portal is green, P2, AV7–AV10;
- ruleset-specific balance formulas, distributed simulation, graph editor, commit or push.
