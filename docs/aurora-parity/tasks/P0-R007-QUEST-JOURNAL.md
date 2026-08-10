# P0 R007 — journal, états et progression de quête

Lot stable : `R007` / `FC-QUEST-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-QST-001` — Catégorie de journal.
- `ORF-QST-002` — Entrées numériques du journal.
- `ORF-QST-003` — État courant par joueur.
- `ORF-QST-004` — États terminé et échoué.
- `ORF-QST-005` — Texte d’entrée localisé.
- Source immuable relue intégralement pour ces cinq sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/08_QUEST.md`.
- Dépendances réévaluées : BUILD, CONTENT et MULTI sont fermés ; les politiques de journal,
  répétition et partage existantes sont réutilisées. Le lot n’ouvre aucun objectif, récompense,
  assistant, marqueur, ressource ou étape appartenant aux lots quête suivants.

## Audit avant implémentation

| Fonction | Présent | Limite réelle à fermer |
|---|---|---|
| QST-001 | `QuestDefinition` possède UUID, slug et nom d’affichage | titre/description localisés, catégorie, priorité, propriétaire, schéma et provenance absents |
| QST-002 | graphe à états UUID et transitions typées | aucun slug d’état, ordre d’affichage indépendant, index numérique historique ni migration explicite |
| QST-003 | `QuestStateMachine` conserve un état courant unique | aucune instance identifiée par joueur/groupe/monde, histoire, variables ou horodatages de simulation |
| QST-004 | booléen terminal et interdiction de transition sortante | aucune distinction terminé/échoué/abandonné/personnalisé ni règle de réactivation explicite |
| QST-005 | entrée journal clé/fallback | absence d’identité, schéma, provenance, résumé/détails, objectifs localisés et liens contextuels typés |

## Contrat minimal de fermeture

- enrichir le modèle `Forge.Domain.Quests` existant sans introduire un troisième modèle concurrent ;
- conserver la lecture exacte des anciens JSON par des valeurs de migration déterministes ;
- identifier, versionner et sourcer quête, états, entrées et liens, avec ordre stable ;
- exprimer les fins `Completed`, `Failed`, `Abandoned` et `Custom`, et leur règle de réactivation ;
- fournir une instance par portée `Player`, `Party` ou `World`, compatible avec la politique de partage,
  contenant état, historique, variables, dates et révision ;
- éditer visiblement ces données sans fichier généré et via le même `SetQuestCommand` que les API ;
- exposer une lecture ForgeScript par handle typé, une ressource MCP filtrée et un dry-run MCP avec diff ;
- produire des diagnostics à codes et chemins stables pour schéma, provenance, localisation, UUID,
  ordre, index historique, fins, réactivation, liens et références ;
- conserver mutation, undo/redo et sauvegarde atomiques dans l’historique projet ;
- garder le runtime optionnel sûr et diagnostiquer explicitement toute quête requise invalide.

## Matrice des 30 critères d’acceptation

| Fonction | Configuration sans généré | Save/close/reload | Invalide localisé | UI = commande | Undo/redo | Headless nominal/échec |
|---|---|---|---|---|---|---|
| QST-001 | formulaire quête direct | JSON exact | catégorie, priorité, propriétaire, schéma | `SetQuestCommand` | historique projet | définition valide/invalide |
| QST-002 | liste ordonnée d’états | UUID, ordre et index exacts | doublons, ordre et index | même commande | historique projet | migration et parcours multi-entrant |
| QST-003 | panneau de simulation par portée | instance sérialisable exacte | portée, propriétaire, politique | lecture du brouillon canonique | définition annulable | création, transition et refus |
| QST-004 | type final + réactivation | type/règle exacts | combinaison terminale | même commande | historique projet | terminé/échoué/réactivation |
| QST-005 | résumé/détails/objectifs/liens | texte localisé exact | clé, UUID, type et référence | même commande | historique projet | résolution/fallback et lien invalide |

## Risques et tests exacts

- compatibilité JSON : roundtrip enrichi, agrégat historique et workspace ;
- domaine : migration numérique, ordre stable, fins, réactivation, liens, validation et régression
  d’accessibilité avec plusieurs liens entrants ;
- instance : portées autorisées, historique/variables/dates, transition nominale et refus atomique ;
- commande : dry-run, apply, sauvegarde, undo et redo ;
- lecture agent/script : handle typé, filtres bornés, dry-run et diff MCP ;
- interface : smoke Toolset headless et capture visible inspectée ;
- intégration : marqueurs Toolset/Runtime, documentation, tracker, backlog, graphe puis
  `scripts/Verify.ps1` avant promotion et après promotion limitée aux cinq IDs.

## Non-objectifs

- objectifs QST-006 à QST-011, déclencheurs QST-012 à QST-016, récompenses et chaînes ultérieures ;
- assistant de quête, marqueurs de carte, ressources de mise en scène ou édition générale de localisation ;
- P1/P2, AV7–AV10, publication Git ou nouvelle voie de mutation.

## Résultat livré

- définition de quête canonique v1 avec UUID, provenance, titre et description localisés, catégorie,
  priorité, propriétaire, caractère requis au runtime et politique d’instance ;
- états versionnés avec slug, ordre d’affichage indépendant, index numérique historique migré,
  fins `Completed`, `Failed`, `Abandoned` ou `Custom` et réactivation explicite ;
- entrée de journal identifiée avec résumé, détails, objectifs localisés et liens contextuels typés,
  dont les références projet sont résolues avec diagnostic stable ;
- instance isolée par joueur, groupe ou monde avec propriétaire, autorité, état, historique, variables,
  dates de simulation et révision, transitions et réactivation atomiques ;
- atelier direct 1450×910 avec création de brouillon, catalogue, formulaire d’identité/portée,
  éditeur d’état/journal défilable, mini-graphe, validation, simulation, dry-run, apply et undo/redo ;
- handle ForgeScript en lecture seule, ressource MCP filtrée/bornée et dry-run MCP réutilisant
  `SetQuestCommand`, sans accès fichier ou nœud moteur ;
- correction du parcours d’accessibilité des quêtes en présence de plusieurs liens entrants.

## Preuves exécutées

- tests ciblés : Domain quête et référence projet 21/21, Application quête 9/9,
  ModuleFormat quête 11/11 et MCP quête 4/4 ;
- solution complète : 1 059 tests .NET, 6 tests Python généraux et 33 tests pipeline assets ;
- roundtrip JSON agrégé et workspace exact, plus migration déterministe de l’ancienne forme ;
- capture visible inspectée : `docs/aurora-parity/evidence/r007-quest-journal-ui.png` (1450×910),
  état final `Completed`, réactivation `Manual`, mini-graphe et instance à quatre événements visibles ;
- marqueurs `OPENRPG_FORGE_TOOLSET_R007_QUEST_PASS` et
  `OPENRPG_FORGE_RUNTIME_R007_QUEST_PASS` intégrés à `scripts/Verify.ps1` et observés sans erreur ;
- portail pré-promotion puis post-promotion : 27 projets sans avertissement, documentation,
  tracker, backlog et graphe frais, modules et prototypes 3D verts,
  `OPENRPG_FORGE_VERIFICATION=PASS` ;
- promotion limitée aux cinq IDs R007 : 241 fonctions `Tested`, 157 P0 non closes et 43 lots P0
  ouverts ; aucune ligne P1/P2 ou AV7–AV10 modifiée.
