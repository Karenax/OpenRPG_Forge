# P0 R049 — Validation exhaustive de trigger

Lot stable : `R049` / `FC-TRIGGER-P0-02` — une fonction, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-TRG-020` — validation de trigger.
- Source immuable relue intégralement : section correspondante de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/12_TRIGGER.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R049 |
|---|---|---|
| géométrie | contrôle isolé RPG Kit | auto-intersection du polygone et des trous rejetée sur le document canonique |
| filters | filtres canoniques R048 | contradiction tags requis/exclus diagnostiquée avant mutation/build |
| actions/target | actions typées R048 | toutes les cibles résolues dans un contexte versionné et persistant |
| portal | portail typé R048 | destination présente dans l'ensemble de références autorisées |
| cooldown | valeur canonique R048 | valeur finie et positive ou nulle contrôlée au même point de validation |
| scripts | API et bindings R048 | scripts actions, événements et interaction résolus dans l'allowlist |
| accessibilité | layers canoniques R048 | intersection obligatoire avec les layers accessibles |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| validation canonique | `RuntimeValidationPolicy`, `ValidateTriggerRuntimeContracts` | test des sept catégories et codes stables |
| contexte runtime | `ResolveTriggerValidation` | tests nominal, fallback optionnel et diagnostic requis |
| interface | section `VALIDATION PRE-BUILD` | capture inspectée `evidence/r049-trigger-validation-ui.png` |
| transaction | `SetWorldFeatureCatalogHandler` | invalidité rejetée avant application sur le même document canonique |
| ForgeScript/MCP | propriété `validation` | ressource trigger filtrée et plan dry-run |
| persistance | catalogue monde | round-trip destinations, targets, scripts, layers et exigence runtime |

## Non-objectifs

- triggers P1/P2, volumes de gameplay avancés ou pièges ;
- AV7–AV10, commit ou push.

## Validation finale

- suites Domain, Application, RpgKit.Domain, ModuleFormat et MCP vertes ;
- capture 1540×940 inspectée : section validation visible sans chevauchement ;
- smoke `OPENRPG_FORGE_TOOLSET_R049_TRIGGER_VALIDATION_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R050.
