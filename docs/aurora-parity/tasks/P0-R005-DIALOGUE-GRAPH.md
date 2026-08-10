# P0 R005 — graphe de dialogue, rôles, branches, liens et ordre

Lot stable : `R005` / `FC-DIALOGUE-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-DLG-001` — Fichier/graphe de conversation.
- `ORF-DLG-002` — Nœuds PNJ et joueur.
- `ORF-DLG-003` — Nombre de branches non limité.
- `ORF-DLG-004` — Liens vers un nœud existant.
- `ORF-DLG-005` — Réordonnancement des réponses.
- Source immuable relue intégralement pour ces cinq sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/07_DIALOGUE.md`.
- Dépendances réévaluées : BUILD et QA sont couvertes par R001–R003. Les services de dialogue,
  commandes transactionnelles, sérialisation JSON, runtime, MCP et écran spécialisé existent déjà.

## Audit avant implémentation

| Fonction | Présent | Limite réelle à fermer |
|---|---|---|
| DLG-001 | `DialogueDefinition`, JSON déterministe, UUID, localisation, commande, validation, runtime et éditeur | schéma/provenance/métadonnées/participants/entrées/arêtes explicites et vue graphe avec mini-carte absents |
| DLG-002 | locuteur par blueprint et narrateur implicite | aucun `NodeRole` explicite couvrant SpeakerLine, PlayerChoice, Narration, System, ActionOnly, Subgraph et End |
| DLG-003 | listes sans plafond, parcours et couverture bornée | aucune pagination auteur, aucun groupement de sous-graphe et aucun diagnostic de cycle accidentel |
| DLG-004 | `TargetNodeId` permet déjà la réutilisation d'un nœud | aucun type d'arête `Reference`, aucun compte d'entrées multiples ni signal visuel sûr |
| DLG-005 | ordre de liste déterministe au runtime | aucune priorité explicite, aucun drag-drop/boutons de déplacement ni diff de réordonnancement lisible |

## Contrat minimal de fermeture

- enrichir le modèle canonique existant sans créer un second graphe concurrent ;
- conserver la compatibilité des dialogues existants grâce à des valeurs de migration déterministes ;
- exposer rôles, sous-graphe visuel, type de lien, priorité et ordre dans l'éditeur spécialisé ;
- paginer l'inventaire des nœuds et afficher une mini-carte textuelle avec entrées multiples ;
- refuser les cycles de flux accidentels, tout en permettant un lien de référence explicitement choisi ;
- faire du réordonnancement une mutation du brouillon qui aboutit à la même `SetDialogueCommand` ;
- étendre la ressource MCP filtrée et conserver plan/apply/revert ;
- fournir un handle ForgeScript de lecture sûr et un runtime optionnel sans dépendance moteur dans le domaine ;
- ne pas ouvrir DLG-006, DLG-009, DLG-011, DLG-021, DLG-029 ni une fonction P1/P2.

## Matrice des 30 critères d'acceptation

| Fonction | Configuration sans généré | Save/close/reload | Invalide localisé | UI = commande | Undo/redo | Headless nominal/échec |
|---|---|---|---|---|---|---|
| DLG-001 | écran Dialogue | JSON exact | identité, schéma, références | `SetDialogueCommand` | historique projet | domaine + Toolset |
| DLG-002 | sélecteur de rôle | JSON exact | rôle et locuteur | même commande | historique projet | domaine + runtime |
| DLG-003 | pagination et groupe | JSON exact | cycle de flux | même commande | historique projet | graphe profond + cycle |
| DLG-004 | type Référence | JSON exact | cible absente | même commande | historique projet | entrées multiples + cible absente |
| DLG-005 | drag-drop et boutons | JSON exact | priorité négative | même commande | historique projet | ordre runtime + diff |

## Non-objectifs

- localisation avancée DLG-006, conditions DLG-009, actions DLG-011, mode test DLG-021 et linter
  complet DLG-029, qui appartiennent à des lots P0 ultérieurs ;
- contrats paramétrés de graphes imbriqués DLG-026, édition collaborative DLG-031 ou génération IA
  DLG-032 ;
- présentation voix/caméra/interjections déjà livrée par G13-H ;
- P1/P2, AV7–AV10, publication Git ou nouvelle voie de mutation.

## Résultat livré

- document canonique v1 enrichi avec provenance, métadonnées, participants, points d'entrée,
  nœuds typés et arêtes explicites, sans second graphe concurrent ;
- migration déterministe des dialogues historiques et roundtrip agrégé/workspace exact ;
- sept rôles explicites, sous-graphes, arêtes `Flow`/`Reference`, priorités et ordre stable ;
- protection contre les cycles de flux accidentels, avec références intentionnelles autorisées ;
- éditeur 1440×950 avec pagination par 25, minicarte, compte d'entrées, type de lien, priorité,
  boutons de déplacement et glisser-déposer ;
- lecture ForgeScript par handle typé, ressource MCP filtrée/bornée et plan MCP enrichi conservant
  dry-run/apply/revert ;
- runtime facultatif sûr et diagnostic explicite pour un graphe requis invalide.

## Preuves exécutées

- Domain dialogue : 20/20 ; Application dialogue : 13/13 ; ModuleFormat dialogue/workspace : 20/20 ;
  MCP dialogue : 7/7 ; test stdio officiel : 1/1 avec délai de réponse borné à 15 secondes ;
- graphe profond : 513 nœuds et 512 arêtes, cycles de flux refusés, références intentionnelles acceptées ;
- capture visible inspectée : `docs/aurora-parity/evidence/r005-dialogue-graph-ui.png` (1440×950) ;
- marqueurs `OPENRPG_FORGE_TOOLSET_R005_DIALOGUE_PASS` et
  `OPENRPG_FORGE_RUNTIME_R005_DIALOGUE_PASS` intégrés à `scripts/Verify.ps1` ;
- portail pré-promotion : 1 034 tests .NET, 6 tests Python généraux, 33 tests pipeline assets,
  documentation/tracker/backlog/graphe frais, smokes Toolset/Runtime/MCP et prototypes 3D verts,
  `OPENRPG_FORGE_VERIFICATION=PASS` ;
- promotion limitée à `ORF-DLG-001`…`ORF-DLG-005` : 231 `Tested`, 167 P0 non closes,
  45 lots P0 ouverts.
