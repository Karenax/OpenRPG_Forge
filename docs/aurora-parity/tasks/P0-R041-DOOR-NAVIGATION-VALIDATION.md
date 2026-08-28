# P0 R041 — navigation dynamique et validation de porte

Lot stable : `R041` / `FC-DOOR-P0-02` — deux fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-DOOR-019` — navigation dynamique.
- `ORF-DOOR-021` — validation de porte.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/11_DOOR.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R041 |
|---|---|---|
| obstacle et lien dynamiques | service RPG Kit isolé et navigation d'aire incrémentale | politique persistante du profil projet et synchronisation runtime sur état/progression d'animation |
| visualisation | overlays de navigation d'aire | option auteur explicite et état obstacle/lien lisible dans le viewport/runtime |
| validation exhaustive | validation R040 des états, transitions, interaction, portail et liens | cohérence collision/navigation, IDs obstacle/lien, seuils, références d'états et boucles avec codes/chemins stables |
| auteur | onglet Portes R040 | section navigation dédiée, seuils non arrondis, aperçu et commande transactionnelle |

Les règles clé et piège restent des extensions P1 facultatives : leur absence n'est pas une erreur P0. Les bindings
ForgeScript présents sont néanmoins validés par le catalogue d'événements.

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| obstacle/link synchronisés | `DoorWorldProfile.NavigationPolicy` et `SynchronizeDoorNavigation` | `R041_DoorNavigationSynchronizesObstacleLinkAnimationAndVisualization` |
| seuils d'animation | `OpeningPassableAt` et `ClosingBlockedAt` bornés 0–1 | test Domain sur ouverture/fermeture précoce et tardive |
| visualisation | chaîne obstacle/link et option `VisualizeInEditor` | capture inspectée `evidence/r041-door-navigation-ui.png` et smoke Toolset R041 |
| blocage testé | collision des états bloquants et lien des états praticables | test Domain et marker `blocking=tested` |
| validation avant application | diagnostics `world.door.navigation_invalid`, `navigation_state_mismatch`, `transition_loop` | `R041_DoorValidationReportsNavigationCollisionAndTransitionLoopBeforeApply` et dry-run Toolset rejeté |
| persistance | navigation sérialisée dans `FoundationPolicy` | `WorldFeaturePersistenceTests` vérifie IDs et option de visualisation après rechargement |
| projet canonique | catalogue matérialisé par la migration ponctuelle `CryptWorldFeatureMigration` | suites Application/ModuleFormat et portail intégré |

## Non-objectifs

- implémenter clé, piège ou fonctions de porte P1/P2 ;
- ouvrir R042 avant le portail vert ;
- ajouter AV7–AV10, commit ou push.

## Validation finale

- tests ciblés Domain, Application et ModuleFormat verts ;
- capture 1540×940 inspectée, IDs et seuils `.75/.25` visibles ;
- smoke `OPENRPG_FORGE_TOOLSET_R041_DOOR_NAVIGATION_VALIDATION_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R042.
