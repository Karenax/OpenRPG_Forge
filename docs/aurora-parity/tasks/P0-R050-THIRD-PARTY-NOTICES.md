# P0 R050 — Notices tierces et garde-fou de licences

Lot stable : `R050` / `FC-BUILD-P0-04` — une fonction, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-BUILD-023` — notices tierces automatisées.
- Source immuable relue intégralement : section correspondante de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/25_BUILD.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R050 |
|---|---|---|
| modèle canonique | dépendances tierces à UUID, schéma, provenance et SPDX | agrégation étendue aux tables, packages, sources et assets gouvernés |
| licence obligatoire | validation du module, des blueprints et dépendances | package ou ressource à licence inconnue bloqué pour Shipping avant compilation |
| NOTICE reproductible | module, blueprints et dépendances triés | document complet déterministe recalculé depuis le projet canonique |
| contrôle du package | NOTICE présent dans `.orpmod` | divergence entre NOTICE et projet rejetée à la validation |
| interface | diagnostics de conformité existants | aperçu réel du NOTICE avec états vide, conforme et erreur |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| agrégation | `DistributionNoticeBuilder` | test d'ordre stable et présence tables, packages, sources et assets |
| garde-fou SPDX | `DistributionComplianceValidator`, préflight `ModulePackager.Export` | tests des codes localisés et absence d'archive écrite |
| package | `ModulePackageValidator` | test de divergence du NOTICE recalculé |
| interface | `DistributionProfilesWindow` | capture inspectée `evidence/r050-third-party-notices-ui.png` |
| commande et historique | `SetModuleDistributionHandler`, `ProjectCommandHistory` | tests dry-run, apply, rejet, undo et redo |
| persistance | sérialisation du profil et du registre | round-trip des cibles et approbations tierces |
| CLI et MCP | `forge package export`, `BuildModule` | export structuré, borné, sans shell arbitraire |

## Non-objectifs

- découverte distante de licences ou workflow juridique multi-rôle ;
- fonctions P1/P2, AV7–AV10, commit ou push.

## Validation finale

- suites Domain, Application, RpgKit.Domain, ModuleFormat et MCP vertes ;
- capture 1280×820 inspectée : aperçu NOTICE lisible et diagnostics sans chevauchement ;
- smoke `OPENRPG_FORGE_TOOLSET_R050_THIRD_PARTY_NOTICES_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré complet.
