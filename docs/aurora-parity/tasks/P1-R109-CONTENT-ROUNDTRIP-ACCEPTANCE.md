# R109 — Acceptation contenu, roundtrip et plateformes

## Périmètre

`R109 / FC-CONTENT-P1-02` couvre exactement cinq fonctions P1 :

- `ORF-CONT-016` — animations ;
- `ORF-CONT-018` — tables de données 2DA-style ;
- `ORF-CONT-021` — roundtrip d’édition externe ;
- `ORF-CONT-022` — pont Blender ;
- `ORF-CONT-025` — compatibilité de plateforme.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/22_CONTENT.md`, lignes 1173–1244,
1321–1392, 1543–1614, 1617–1688 et 1839–1910.

## Matrice d’écarts auditée

| Exigence source | Présent avant R109 | Limite réelle | Fermeture/preuve |
|---|---|---|---|
| Clips, bibliothèque, retarget, root motion, événements, compression et aperçu | manifeste d’animation gouverné, contrôleur moteur et aperçus Toolset/runtime | aucune ; le retarget automatique de nouveaux rigs dépasse le contrat | validation manifeste, smoke R109 et aperçus existants |
| Tables typées, schémas, références, grilles, migrations et overrides | UUID de tables/lignes/colonnes, types, références, overrides, écran, commande et projet | aucune ; formules et index ne sont pas demandés | tests Content Workspace, persistance et marqueur R109 |
| Outil externe, watcher, reimport contrôlé et diff de métadonnées | configuration typée persistante, worker, CLI, UI, dry-run et undo/redo | `WatchForChanges` était déclaratif sans service de détection/diff | `AssetExternalEditWatchService` et tests déterministes |
| Pont Python Blender typé sans exécution arbitraire MCP | worker confiné, opérations allowlistées, hashes, Blender headless réel et GLB | aucune ; un outil MCP dédié n’est pas requis si aucun shell arbitraire n’est exposé | tests Python, request store et smokes Blender existants |
| Variantes par build, fallback et validation de parité | chunks plateforme/`Any`, résolveur et validateur de rôles requis | aucune ; benchmarks physiques ne font pas partie du contrat | test de fallback/parité et smoke R109 |

## Contrat fermé

- Le watcher confine le chemin source, utilise `FileSystemWatcher` et confirme par capture SHA-256
  déterministe avant de proposer un reimport.
- Le plan de reimport ne contient que l’outil, les opérations allowlistées, les plateformes, la sortie
  canonique et un diff de métadonnées ; un dry-run n’écrit aucun fichier.
- L’acquittement remplace la baseline après acceptation et supprime le changement déjà traité.
- Une source absente ou une configuration invalide produit un diagnostic stable et localisé.
- Animations, tables, worker Blender et variantes plateforme continuent d’utiliser leurs commandes,
  stores et surfaces Toolset existants.

## Preuves obtenues avant portail global

- 2/2 tests `AssetExternalEditWatchServiceTests` ;
- 441/441 tests Application ;
- build Toolset sans avertissement ni erreur ;
- `.artifacts/r109-toolset-smoke-8ad4a71ec8b74859a83fa41466845394.log` contient le marqueur exact
  `OPENRPG_FORGE_TOOLSET_R109_CONTENT_ROUNDTRIP_PASS`.

## Portail consolidé

- tracker/backlog régénérés : 646 `Tested`, 70 `Implemented`, 1 `Researched`, 162 lots clos ;
- documentation générée en mode `check` sans dérive ;
- graphe Roslyn déterministe : 7 672 nœuds, 49 290 relations, zéro diagnostic ;
- 1 760 tests .NET et 39 tests Python passent ;
- `artifacts/verification/toolset.log` contient le marqueur exact R109 ;
- `.artifacts/r109-full-verify-final.log` termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- retarget automatique de rigs arbitraires ou éditeur d’animation complet ;
- formules/index de tables non demandés par l’adaptation ;
- exécution de Python ou d’un shell arbitraire via MCP ;
- lots R110+, P2, Asset/Map AV7–AV10, commit ou push avant portail R109 vert.
