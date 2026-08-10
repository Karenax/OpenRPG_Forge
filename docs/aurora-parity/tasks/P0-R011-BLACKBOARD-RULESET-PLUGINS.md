# P0 R011 — blackboards et plugins de règles

Lot stable : `R011` / `FC-GAMEPLAY-P0-02` — deux fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-GAME-022` — Variables et blackboards.
- `ORF-GAME-023` — Système de règles plugin.
- Source immuable relue intégralement pour les deux sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/19_GAMEPLAY.md`.
- Dépendances réévaluées : SCRIPT possède les handles/services typés et CONTENT possède le catalogue
  canonique. Les commandes module/règles, l'historique et la persistance existent déjà.

## Matrice d’audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| GAME-022 | définitions UUID typées, scopes session/save/joueur/groupe/serveur, éditeur spécialisé, commande atomique, blackboard runtime et sauvegarde | aucune politique de réplication ni vue debug gouvernée, aucun handle ForgeScript dédié, aucune lecture MCP filtrée ni plan variables dry-run | schéma enrichi rétrocompatible, réplication déterministe, vue debug, service script et MCP read/plan |
| GAME-023 | profils de progression/difficulté/mutateurs, UI règles, commande/MCP dry-run/apply/revert, lecture ForgeScript | `rulesetId` reste une chaîne : aucun package canonique ne déclare attributs, effets, capacités, inventaires, progression et UI ni ne gère extension optionnelle/requise | catalogue de packages versionné, validation, résolution runtime, édition visible et lecture MCP filtrée |

## Contrat minimal de fermeture

- enrichir `ProjectVariableDefinition` sans document parallèle avec réplication, visibilité debug et
  exigence runtime, en conservant le JSON historique ;
- valider identités, schémas, clés, doublons, scopes et politiques avec code/chemin/UUID stables ;
- fournir un service blackboard typé pour lecture, mutation validée, delta de réplication et snapshot
  debug sans accès fichier ou nœud moteur ;
- exposer variables par handle ForgeScript et ressource MCP filtrée, avec plan dry-run/apply/revert
  fondé sur `SetModuleConfigurationCommand` ;
- ajouter au `ModuleRulesProfile` un catalogue de plugins data-driven déclarant attributs, effets,
  capacités, inventaires, progression et panneaux UI ;
- résoudre au runtime l'extension active, ignorer une extension optionnelle absente et diagnostiquer
  une extension requise absente ;
- réutiliser `SetRulesProfilesCommand`, l'historique, la persistance et le plan MCP existants ;
- rendre les deux capacités visibles dans les écrans spécialisés avec états vide/erreur explicites.

## Risques et tests exacts

- rétrocompatibilité JSON des variables et profils de règles antérieurs ;
- domaine : UUID/schéma, doublons, clé active, six registres et références ;
- application : normalisation typée, mutation refusée sans état partiel, delta owner/party/all/server,
  debug masqué et résolution plugin optionnelle/requise ;
- commande/MCP : read filtré, dry-run, diff, apply, persistence, revert et concurrence ;
- interface/runtime : édition des politiques et packages, undo/redo, marqueurs outil/runtime et capture ;
- intégration : tracker, backlog, docs, graphe et portails pré/post-promotion.

## Non-objectifs

- difficulté, mutateurs, ciblage, HUD ou toute autre fonction GAME P1/P2/P3 ;
- chargement de code natif arbitraire ou téléchargement de plugins ; le P0 livre le contrat data-driven
  et sa résolution sûre, pas une nouvelle frontière d'exécution ;
- AV7–AV10, commit ou push Git.

## Résultat livré

- `ProjectVariableDefinition` porte réplication, visibilité debug et exigence runtime tout en gardant
  les anciens JSON compatibles ; `ProjectVariableValidator` localise identités, doublons et politiques ;
- `ProjectBlackboardScriptService` fournit handles typés, lecture/mutation normalisée, révision,
  snapshots debug filtrés et deltas owner/party/client/server ; le runtime utilise ce service ;
- l'écran module expose type, scope, réplication, debug et exigence runtime par la commande canonique ;
- MCP fournit recherche ID/texte/scope et plan dry-run/apply/revert des variables ;
- `RulesetPluginCatalog` enregistre les six familles exigées, avec package actif, version, contenu,
  provenance, extension optionnelle ou requise et diagnostics stables ;
- l'écran règles édite le catalogue sans code natif arbitraire, la commande/MCP existante conserve
  dry-run/apply/revert, et le runtime résout le package actif ou émet le diagnostic requis.

## Preuves exécutées

- captures inspectées : `docs/aurora-parity/evidence/r011-blackboard-ui.png` (1440×840) et
  `docs/aurora-parity/evidence/r011-ruleset-plugin-ui.png` (1260×820), sans panneau tronqué et avec
  les politiques/régistres visibles ;
- tests ciblés : 9 domaine, 11 application, 4 persistance et 2 MCP ; formatage sans dérive ;
- marqueurs intégrés : `OPENRPG_FORGE_TOOLSET_R011_GAMEPLAY_SYSTEMS_PASS` et
  `OPENRPG_FORGE_RUNTIME_R011_GAMEPLAY_SYSTEMS_PASS` ;
- portail pré-promotion : compilation sans avertissement, 1 093 tests .NET, 6 tests Python généraux,
  33 tests Python asset, documentation/tracker/backlog/graphe frais et tous les smokes intégrés,
  puis `OPENRPG_FORGE_VERIFICATION=PASS` ;
- promotion limitée à `ORF-GAME-022` et `ORF-GAME-023`. Le portail post-promotion doit rester vert
  avant activation de R012.
