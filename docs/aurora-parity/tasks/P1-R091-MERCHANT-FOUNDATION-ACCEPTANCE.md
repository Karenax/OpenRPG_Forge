# R091 — Merchant foundation acceptance

## Scope

`R091 / FC-MERCHANT-P1-01` closes exactly five P1 functions:

- `ORF-MER-001` — independent store object;
- `ORF-MER-002` — Store Setup Wizard;
- `ORF-MER-003` — store inventory;
- `ORF-MER-004` — markup and markdown;
- `ORF-MER-008` — whitelist and blacklist restrictions.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/16_MERCHANT.md` at lines 51–122,
123–194, 195–266, 267–338 and 555–626.

## Dependency reduction

DIALOGUE is reduced to existing dialogue UUID and choice bindings. FACTION remains an existing
pricing/restriction input and receives no parallel economy model. ITEM contributes immutable blueprint
references and item views. PALETTE contributes only resolved vendor/item references. SCRIPT is reduced
to the existing typed world-feature view and one controlled custom-rule binding; no file, process,
network or native-engine access is introduced.

## Audited gap matrix

| Source requirement | Present before R091 | Real limit closed by R091 | Evidence |
|---|---|---|---|
| Store independent from actors and reusable by vendors, interfaces and channels | Pure RPG Kit `StoreDefinition` | No persisted project-owned store nor complete creator/runtime projection | `MerchantFoundationProfile`, Studio, project round trip and runtime nodes |
| Wizard creates store, vendor, dialogue choice, interaction and tests | Deterministic pure `MerchantService.Preview` | Wizard intent and fixture were not canonical or authorable | Persisted `MerchantStoreWizardProfile`, preview adapter and invalid dry-run proof |
| Finite/infinite stock with quantity, variant, supplier, restock, order and hidden conditions | Pure stock records and transaction stock | Full supply metadata was detached from the project | Canonical stock entry, UI summary, finite/infinite runtime proof |
| Buy/sell curves with currency, reputation, scarcity, condition and negotiation | Pure `MerchantExperienceService.Price` | Curves and factors were not persisted or editable | Canonical pricing policy, interpolation adapter and bounded price tests |
| Whitelist/blacklist over type, tags, quality, provenance, condition, owner and custom rule | Pure `MerchantExperienceService.Matches` | Query and controlled binding were not persisted or projected | Canonical query, accepted/blocked tests, ForgeScript/MCP fields |

## Shared acceptance

- `MerchantWorldProfile.Foundation` owns stable UUIDs, schema, provenance, localized identity, vendors,
  wizard, stock, pricing and restrictions; legacy documents keep an optional null state.
- `SetWorldFeatureCatalogHandler` remains the sole mutation boundary with dry-run, apply, rejection,
  semantic patch, inverse, audit and project-level Undo/Redo.
- `WorldFeatureCatalogValidator` reports stable diagnostics below
  `module/worldFeatures/merchants/{uuid}/foundation` for references, wizard, stock, pricing and query.
- ForgeScript reads the typed merchant projection; MCP returns the same filtered view and retains its
  existing preview/apply/revert transaction contract.
- Runtime adaptation reuses `MerchantService` and `MerchantExperienceService`; a missing optional
  foundation is ignored and a missing required one returns
  `WORLD_MERCHANT_FOUNDATION_REQUIRED_MISSING`.

## Delivered bounded changes

- Added one canonical merchant foundation without replacing the existing R045 atomic transaction model.
- Extended the World Studio merchant tab with store, wizard, stock, price curves and query sections plus
  empty/error/summary states.
- Added one adapter to the existing RPG Kit store, pricing and query algorithms.
- Added a complete authored foundation to the Crypt of Ashes sample and five explicit runtime nodes.

## Reproducible evidence

- Targeted additions: 1 Application test, 4 RPG Kit tests and 1 ModuleFormat round-trip test; affected
  MCP tests pass.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R091_MERCHANT_FOUNDATION_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R091_MERCHANT_FOUNDATION_PASS`.
- Visual marker: `OPENRPG_FORGE_R091_MERCHANT_FOUNDATION_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r091-merchant-foundation-ui.png`, inspected at native
  1440×920; store, wizard and stock form are readable while the semantic summary shows inventory,
  curves and all restriction facets without overlap.
- The integrated Toolset smoke reaches `OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS`, proving its generated
  playtest snapshot validates and starts in the separate runtime.
- The consolidated suite executes 1,643 .NET tests and 39 Python tests. The integrated portal preserves
  both exact R091 markers, restores the sample project byte-for-byte and ends with
  `OPENRPG_FORGE_VERIFICATION=PASS` before R092 begins.

## Non-objectives

- `ORF-MER-009`, `ORF-MER-011` and `ORF-MER-017`, reserved for R092;
- P2, AV7–AV10, commit or push.
