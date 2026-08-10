# P0 R047 — OnUsed et validation de placeable

Lot stable : `R047` / `FC-PLACEABLE-P0-02` — deux fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-PLC-018` — événement OnUsed typé.
- `ORF-PLC-027` — validation complète de placeable.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/10_PLACEABLE.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R047 |
|---|---|---|
| InteractionExecuted | événement typé dans le RPG Kit isolé | politique projet UUID/schéma/provenance/API/binding et exécution runtime avec verbe, acteur, point cible et résultat |
| cycle de vie script | binding OnUsed générique | scripts avant/après gouvernés et absence optionnelle/requise explicite |
| collision/navigation/animation | collision et transform R046 | animation du transform résolue dans une allowlist et cohérence navigation/collision conservée |
| locks/traps/inventory/physics | booléens de composants | références uniques requises dès que le composant est actif |
| scripts/budgets | routes script persistantes | budget scripts et draw calls vérifié contre le document canonique |
| auteur/agents | commande monde et MCP générique | écran visible, rejet pré-build, handle filtré et plan dry-run |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| événement OnUsed | `OnUsedPolicy`, `ExecutePlaceableUse` | test runtime verbe/acteur/cible/résultat/API/scripts |
| validation exhaustive | `RuntimeValidationPolicy`, `ValidatePlaceableRuntimeContracts` | diagnostics animation, locks, traps, inventory, physique et budgets |
| interface | section `ONUSED ET VALIDATION` | capture inspectée `evidence/r047-placeable-events-validation-ui.png` |
| transaction | `SetWorldFeatureCatalogHandler` | smoke dry-run/apply/rejet et undo/redo |
| ForgeScript/MCP | propriétés `onUsed`, `forgeScriptApi`, `validation` | test ressource filtrée et dry-run sans révision |
| persistance | catalogue monde | round-trip de l’événement et des références de validation |
| runtime | `PlaceableInteractionExecuted` | nominal et diagnostic OnUsed requis manquant |

## Non-objectifs

- événements Placeable P1/P2 autres que OnUsed, destructibilité avancée ou inventaire détaillé ;
- P1/P2, AV7–AV10, commit ou push.

## Validation finale

- suites Domain, Application, RpgKit.Domain, ModuleFormat et MCP vertes ;
- capture 1540×940 inspectée : API, binding, scripts et références visibles sans chevauchement ;
- smoke `OPENRPG_FORGE_TOOLSET_R047_PLACEABLE_EVENTS_VALIDATION_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R048.
