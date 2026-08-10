# P0 R034 — opérations de contenu et playtest ciblé

Lot stable : `R034` / `FC-SHELL-P0-03` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-SHELL-014` — Copier, couper, coller et dupliquer.
- `ORF-SHELL-015` — Recherche globale.
- `ORF-SHELL-016` — Find references.
- `ORF-SHELL-017` — Renommage sûr.
- `ORF-SHELL-018` — Aperçu et mode test.
- Source immuable relue intégralement : sections 904–1228 de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/01_SHELL.md`.

## Audit avant fermeture

| Fonction | Déjà présent | Limite réelle à fermer |
|---|---|---|
| Clipboard | payloads entité/zone, nouveaux UUID, clone de zone et remap de portails, aperçu, dry-run, undo/redo | payload sans UUID/provenance, copie limitée à une entité dans l'UI, absence de coupe atomique et d'option instance/copie |
| Recherche globale | index versionné des données et graphes projet, texte accent-insensible, filtres type/tag, diagnostics et UI vide/erreur | scripts et assets gouvernés absents du même index et profil de recherche non persistant |
| Find references | index inverse déterministe pour agrégats, entités, portails, dialogues et quêtes avec chemins cliquables | pas de références génériques vers scripts, assets, textes localisés, variables et états via leurs valeurs canoniques |
| Renommage sûr | aperçu obligatoire, nom/slug des agrégats, UUID stable, validation, dry-run, patch inverse et historique | chemins de ressources auteur et références textuelles hors agrégats non migrés transactionnellement |
| Aperçu/test | Play From Here configuré/persisté, snapshots temporaires, quest injections, dialogue sandbox et headless avec retour éditeur | cibles explicites quête/dialogue et mode headless non réunis dans le profil et le manifeste de lancement |

## Contrat minimal de fermeture

- versionner le clipboard et ajouter multi-sélection, coupe atomique et modes instance/copie avec remap ;
- indexer scripts/assets et leurs références canoniques dans la même recherche structurée persistée ;
- permettre un renommage de ressource avec aperçu, migration de références, dry-run et rollback ;
- étendre le profil Play From Here à une cible monde, quête ou dialogue et au mode headless ;
- prouver configuration UI, persistance, erreurs localisées, égalité UI/commande, undo/redo, smoke Toolset
  et portail global.

## Non-objectifs

- build/export de R035, R036+, P1/P2, AV7–AV10, commit ou push Git.

## Matrice de fermeture

| Fonction | Critères fermés | Preuves reproductibles |
|---|---|---|
| `ORF-SHELL-014` | payload UUID/schéma/provenance, sélection multiple, couper, coller, dupliquer, mode instance/copie indépendante, remap, dry-run et historique | `SemanticClipboardWorkflowTests`, UI `main_panel.tscn`, marqueurs `G706` et `R034` |
| `ORF-SHELL-015` | texte structuré sur données/graphes/scripts/assets/textes, filtres, sources et limite persistées | `CreatorSearchAndRenameTests`, `CreatorSearchAndResourceRenameTests`, `.forge/editor-search.json`, marqueur `R034` |
| `ORF-SHELL-016` | usages inverses d’agrégats, variables, états, textes localisés et chemins de ressources, affichés et navigables | `CreatorReferenceIndex`, `CreatorWorkspaceResourceCatalog`, `CreatorSearchAndRenameTests`, marqueur `R034` |
| `ORF-SHELL-017` | nom/slug et chemin auteur, aperçu, conservation UUID, migration atomique, dérive refusée, rollback, undo/redo | `WorkspaceResourceRenameService`, `WorkspaceResourceIdentityStore`, `CreatorSearchAndResourceRenameTests`, marqueur `R034` |
| `ORF-SHELL-018` | cible monde/quête/dialogue, point de départ, profil, headless, snapshot temporaire et source inchangée | `AreaPlaytestProfile`, `PlaytestLaunchManifest`, `PlaytestSnapshotStoreTests`, marqueurs runtime et `R034` |

## Validation de palier

- compilation : 27 projets, zéro avertissement et zéro erreur ;
- suites complètes : Domain 320, Application 339, ModuleFormat 262 ;
- portail intégré : 1 296 tests .NET, 6 tests Python généraux, 33 tests Python asset, tous les smokes
  Toolset/runtime/export et `OPENRPG_FORGE_VERIFICATION=PASS` ;
- graphe Roslyn frais et déterministe : 6 070 nœuds, 37 289 relations, zéro diagnostic ;
- aucun P1/P2, AV7–AV10, commit ou push ouvert par ce lot.
