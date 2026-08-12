# R092 — Merchant experience acceptance

## Scope

`R092 / FC-MERCHANT-P1-02` closes exactly three P1 functions:

- `ORF-MER-009` — infinite supply;
- `ORF-MER-011` — `OnOpen` and `OnClose` store sessions;
- `ORF-MER-017` — configurable store interface.

The source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/16_MERCHANT.md` at lines 627–698,
771–842 and 1206–1277.

## Dependency reduction

ITEM remains an immutable blueprint/variant reference. DIALOGUE and FACTION contribute existing
vendor, interaction and pricing inputs without creating another conversation or reputation model.
SCRIPT is reduced to typed session bindings and a versioned API; UI configuration is project data,
not executable engine state. NETWORK is represented only by the already-defined supply policy enum;
R092 does not introduce a server inventory protocol.

## Audited gap matrix

| Source requirement | Present before R092 | Real limit closed by R092 | Evidence |
|---|---|---|---|
| Infinite supply allows unbounded purchases without decrementing the authored quantity | Pure RPG Kit `SupplyPolicy.Infinite` transaction semantics | The project sample, authored profile and runtime path still demonstrated finite stock | Infinite canonical sample, 50-unit transaction and stable-quantity assertions |
| `StoreSessionOpened/Closed` carry customer, vendor, price context and transaction summary through typed bindings | Pure `MerchantExperienceService.Open/Close` records | No persisted lifecycle/API contract or project/runtime projection | `MerchantSessionLifecycleProfile`, adapter, Studio, ForgeScript/MCP and runtime markers |
| Store UI provides keyboard/gamepad navigation, comparison, filters, cart, confirmation and accessibility | Pure `StoreUiConfiguration` validation | No canonical configuration or functional author/runtime interface | `MerchantStoreUiProfile`, Studio form and `MerchantStorePanel` |

## Shared acceptance

- `MerchantWorldProfile.Experience` owns three stable UUIDs, schema, provenance, typed open/close
  bindings, complete payload flags, configurable columns and accessibility requirements; legacy
  documents keep an optional null state.
- `SetWorldFeatureCatalogHandler` remains the sole mutation boundary with dry-run, apply, rejection,
  semantic patch, inverse, audit and project-level Undo/Redo.
- `WorldFeatureCatalogValidator` reports stable diagnostics below
  `module/worldFeatures/merchants/{uuid}/experience` and rejects missing required profiles, invalid
  session bindings/payloads, invalid store UI and identity conflicts.
- ForgeScript reads the same typed projection exposed by filtered MCP reads; MCP retains its existing
  dry-run/apply/revert transaction contract.
- Runtime adaptation reuses `MerchantService` and `MerchantExperienceService`; a missing optional
  profile is ignored and a missing required one returns
  `WORLD_MERCHANT_EXPERIENCE_REQUIRED_MISSING`.

## Delivered bounded changes

- Added one persisted merchant experience profile without duplicating the R091 store, pricing or
  restriction models.
- Extended the World Studio merchant tab with session API/bindings/payload and complete accessible UI
  configuration plus empty/error/summary states.
- Added one runtime adapter over the existing RPG Kit session/UI services and one actual accessible
  store panel with focusable filter, list, compare, cart and confirmation controls.
- Updated Crypt of Ashes to demonstrate infinite stock, versioned events and a four-column accessible
  store interface.

## Reproducible evidence

- Targeted additions: 1 Application test and 4 RPG Kit tests; affected ModuleFormat, MCP and sample
  tests pass.
- Toolset marker: `OPENRPG_FORGE_TOOLSET_R092_MERCHANT_EXPERIENCE_PASS`.
- Runtime marker: `OPENRPG_FORGE_RUNTIME_R092_MERCHANT_EXPERIENCE_PASS`.
- Visual marker: `OPENRPG_FORGE_R092_MERCHANT_EXPERIENCE_UI_CAPTURE_PASS`.
- Visual proof: `docs/aurora-parity/evidence/r092-merchant-experience-ui.png`, inspected at native
  1440×920; session heading, API, open/close bindings, complete payload and the first UI controls are
  readable without overlap, while the semantic summary proves all accessibility and column options.
- The integrated Toolset smoke reaches `OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS`, proving its generated
  playtest snapshot validates and starts in the separate runtime.
- The consolidated suite executes 1,648 .NET tests and 39 Python tests. The integrated portal preserves
  both exact R092 markers, restores the sample project byte-for-byte and ends with
  `OPENRPG_FORGE_VERIFICATION=PASS` before R093 begins.

## Non-objectives

- placeable functions reserved for R093 and later lots;
- P2, AV7–AV10, commit or push.
