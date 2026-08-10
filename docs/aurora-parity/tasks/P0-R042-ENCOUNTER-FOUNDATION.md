# P0 R042 — fondation de rencontre

Lot stable : `R042` / `FC-ENCOUNTER-P0-01` — trois fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-ENC-002` — volume d'activation.
- `ORF-ENC-003` — liste de créatures candidates.
- `ORF-ENC-020` — validation de rencontre.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/13_ENCOUNTER.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R042 |
|---|---|---|
| volume 2D/3D | modèle et assistant RPG Kit isolés | volume persistant du profil projet, quatre formes, filtres, ligne de vue, menace, script et visualisation |
| pool de créatures | `SpawnCandidate` RPG Kit | candidats du document monde avec blueprint typé, poids, rôle, coût, conditions, variantes et maximum vivant |
| résolution runtime | cycles/respawn G13-T/U | activation nominale et branches filtre, faction, ligne de vue, distance et pool épuisé |
| validation | validation générique rencontre | géométrie, références de blueprints, pool, budget, ancres, récompenses et cap réseau avec codes stables |
| auteur/agents | onglet Rencontres sans volume/pool | sections dédiées, aperçu, commande dry-run/apply, undo/redo, ForgeScript/MCP génériques et round-trip |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| volume 2D/3D et filtres | `EncounterWorldProfile.ActivationVolume` | capture inspectée `evidence/r042-encounter-foundation-ui.png` et round-trip ModuleFormat |
| candidats pondérés | `SpawnCandidate` et `FoundationPolicy` | `R042_EncounterActivationFiltersAndSelectsBoundedCandidatePool` |
| chemin nominal/échec | `ActivateEncounter` | test Domain : activation, absence de ligne de vue et pool épuisé |
| validation complète | `WorldFeatureCatalogValidator` | `R042_EncounterValidationRejectsGeometryPoolReferencesBudgetAnchorsRewardsAndCap` |
| transaction auteur | `WorldFeatureStudioWindow` et `SetWorldFeatureCatalogHandler` | smoke Toolset R042 avec dry-run invalide rejeté |
| projet canonique | `CryptWorldFeatureAuthoringService` | profil Crypte avec volume, candidat, budget, ancre, récompense et cap réseau |
| ForgeScript/MCP | script d'activation et ressource/plan monde filtrés | suites Application/MCP existantes et portail intégré |

## Non-objectifs

- ouvrir difficulté, vagues, respawn ou autres fonctions P1/P2 ;
- ajouter P1/P2, AV7–AV10, commit ou push.

## Validation finale

- tests ciblés Domain, Application et ModuleFormat verts ;
- capture 1540×940 inspectée, volume 2D/3D et paramètres d'activation lisibles ;
- smoke `OPENRPG_FORGE_TOOLSET_R042_ENCOUNTER_FOUNDATION_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R043.
