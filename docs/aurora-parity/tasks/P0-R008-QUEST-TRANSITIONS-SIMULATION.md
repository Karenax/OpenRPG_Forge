# P0 R008 — transitions, simulation et validation narrative de quête

Lot stable : `R008` / `FC-QUEST-P0-02` — trois fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-QST-007` — Transitions d’état.
- `ORF-QST-022` — Simulation de quête.
- `ORF-QST-023` — Validation narrative.
- Source immuable relue intégralement pour ces trois sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/08_QUEST.md`.
- Dépendances réévaluées : la commande transactionnelle, ForgeScript, MCP, le graphe de dialogue et
  le socle QA sont présents. Le lot réutilise `QuestDefinition`, `SetQuestCommand` et l’atelier R007 ;
  il n’ouvre aucune fonction P1/P2 de récompense, objectif, assistant ou test généré.

## Matrice d’audit avant implémentation

| Fonction | Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|---|
| QST-007 | TransitionGraph canonique | UUID, états source/cible, conditions et actions | schéma, provenance, ordre, événement, priorité, idempotence et version ForgeScript absents | roundtrip exact et tests domaine |
| QST-007 | Auteur, commande, undo/redo | `SetQuestCommand`, dry-run, historique projet et atelier direct | aucun éditeur visible des transitions | onglet transition, dry-run/apply/undo/redo |
| QST-007 | Validation structurée | références et accessibilité | identité, binding, ordre, type, idempotence et conflit non contrôlés | diagnostics code + chemin + UUID |
| QST-022 | State explorer | création/avance d’instance R007 | le bouton suit arbitrairement le premier arc et n’injecte aucun événement | scénario persisté, événement injecté, trace et effets |
| QST-022 | Deadlocks et échecs | arrêt silencieux sur absence d’arc | aucun statut explicite ni diagnostic | résultat déterministe Completed/Deadlocked/Rejected |
| QST-022 | Interfaces/API | panneau de simulation, handle et ressource MCP de journal | aucune lecture de scénario/trace ni simulation MCP bornée | UI, ForgeScript et MCP en lecture/dry-run |
| QST-023 | Références/états inatteignables | `QuestValidator` couvre les références et l’accessibilité | pas de linter narratif dédié | diagnostics narratifs agrégés |
| QST-023 | Deadlocks/cycles/conflits | non couvert | cycles sans sortie, état non terminal bloqué, transitions ambiguës | tests nominaux et branches d’échec |
| QST-023 | Récompenses/objectifs | actions et journal typés | récompenses répétables involontaires et objectifs impossibles non signalés | diagnostics stables et localisés |

## Contrat minimal de fermeture

- enrichir `QuestTransition` de manière rétrocompatible avec identité/version/provenance, ordre stable,
  binding d’événement, priorité, idempotence et version d’API ForgeScript ;
- conserver conditions/actions typées et résolution déterministe par priorité, ordre puis UUID ;
- persister un profil de simulation identifié avec événements, variables et garde de nombre d’étapes ;
- exécuter le scénario en sandbox sans modifier le projet, produire trace, actions, variables,
  transitions consommées, état final et deadlock explicite ;
- ajouter un linter narratif aux diagnostics de quête pour états bloqués, cycles sans sortie, transitions
  concurrentes, objectifs impossibles et récompenses répétées ;
- étendre l’atelier existant avec édition de transition, mini-graphe, injection d’événement, trace et linter ;
- réutiliser `SetQuestCommand` pour UI et MCP, avec dry-run et diff sémantique ;
- exposer lecture typée et simulation bornée via ForgeScript/MCP ;
- prouver sauvegarde/rechargement, refus invalide, undo/redo, nominal et échec headless.

## Risques et tests exacts

- compatibilité JSON : anciennes transitions migrées et nouveau profil roundtrip exact ;
- domaine : ordre/priorité, événement, idempotence, conflit, deadlock, cycle et récompense répétée ;
- application : scénario nominal, condition refusée, événement inconnu, limite d’étapes et état terminal ;
- commande : édition UI et API identique, dry-run/apply/undo/redo ;
- MCP/ForgeScript : filtres, trace bornée et absence de mutation directe ;
- interface : smoke headless et capture visible inspectée ;
- intégration : marqueurs Toolset/Runtime, tracker, backlog, documentation, graphe et
  `scripts/Verify.ps1` avant puis après promotion limitée aux trois IDs.

## Non-objectifs

- objectifs explicites P1, récompenses auteur P1, tests générés P1 et génération IA ;
- politiques de répétition/partage/notifications déjà portées par un autre lot ;
- P1/P2, AV7–AV10, publication Git ou nouvelle voie de mutation.

## Résultat livré

- transitions canoniques v1 avec UUID, provenance, ordre stable, binding d’événement typé, priorité,
  idempotence, clé de déduplication, conditions/actions typées et version d’API ForgeScript ;
- profil de simulation identifié, versionné et persistant avec variables initiales, événements ordonnés
  et garde d’étapes ; moteur sandbox déterministe avec effets, variables, refus, deadlock, état final,
  audit avant/après et clés d’idempotence ;
- linter narratif couvrant références, accessibilité, états non terminaux bloqués, cycles sans sortie,
  transitions concurrentes, objectifs impossibles et récompenses répétables non protégées ;
- atelier unique enrichi d’un onglet de transitions/scénario, mini-graphe annoté, State Explorer et
  diagnostics localisés, en conservant `SetQuestCommand`, dry-run, apply, undo et redo ;
- lectures ForgeScript typées, ressource MCP enrichie, simulation MCP bornée et mutation complète
  toujours disponible exclusivement en dry-run avec diff sémantique.

## Preuves exécutées

- tests ciblés : Domain quête 26/26, Application quête 11/11, ModuleFormat quête 11/11 et MCP quête 4/4 ;
- solution complète : 1 067 tests .NET, 6 tests Python généraux et 33 tests pipeline assets ;
- roundtrip enrichi exact et migration déterministe de l’ancienne transition et du profil absent ;
- capture visible inspectée : `docs/aurora-parity/evidence/r008-quest-transition-ui.png` (1450×910),
  trois arcs annotés, binding `dialogue.accept`, priorité 90, idempotence `PerEvent`, trace à trois
  événements et état final `Completed` ;
- marqueurs `OPENRPG_FORGE_TOOLSET_R008_QUEST_PASS` et `OPENRPG_FORGE_RUNTIME_R008_QUEST_PASS`
  observés sans erreur ;
- portail pré-promotion : 27 projets sans avertissement, documentation et graphe frais (5 379 nœuds,
  31 804 relations, zéro diagnostic), prototypes 3D et exports verts,
  `OPENRPG_FORGE_VERIFICATION=PASS` ;
- promotion limitée aux trois IDs R008 ; aucune ligne P1/P2 ou AV7–AV10 modifiée.
- portail post-promotion : tracker à 244 `Tested`, 54 `Researched`, 42 lots P0 ouverts, autorités
  fraîches et `OPENRPG_FORGE_VERIFICATION=PASS` sur le même total de 1 067 tests .NET.
