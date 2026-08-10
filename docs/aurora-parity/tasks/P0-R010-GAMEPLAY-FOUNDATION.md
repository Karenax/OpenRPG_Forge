# P0 R010 — fondations gameplay jouables

Lot stable : `R010` / `FC-GAMEPLAY-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-GAME-001` — Déplacement au sol.
- `ORF-GAME-010` — Système d’interaction.
- `ORF-GAME-012` — Caméras configurables.
- `ORF-GAME-013` — Contrôles et input mapping.
- `ORF-GAME-020` — Sauvegarde et chargement.
- Source immuable relue intégralement pour ces cinq sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/19_GAMEPLAY.md`.
- Dépendances réévaluées : AREA, DOOR, EE, QA et SHELL disposent déjà de contrats canoniques et
  de tests ; les placeables et le contenu sont utilisables au runtime. Le lot consolide leur
  consommation par un profil gameplay P0 sans ouvrir les fonctions P1/P2 voisines.

## Matrice d’audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| GAME-001 | `PlayerController`, collisions, marche/course/saut et runtime de capacités étendues | aucun `GroundedMovementProfile` canonique couvrant accélération, pente, marche, saut, accroupissement, root motion et prédiction | profil versionné, validation, simulation et runtime piloté par données |
| GAME-010 | interaction jouable, portes/objets/dialogues et roue d’actions | portée, focus, ciblage, verbes et maintien restent codés en dur ou dispersés | verbes typés, focus déterministe, hold et résolution de cible |
| GAME-012 | rig caméra runtime et caméra éditeur persistée | aucun catalogue module des modes isométrique/orbite/épaule/première personne/top-down/libre/véhicule/cinématique | profils caméra, sélection, validation et application runtime |
| GAME-013 | actions moteur et raccourcis inscrits au démarrage, keymap éditeur séparée | aucun mapping runtime canonique multi-device, contextes, glyphes, accessibilité et profil local joueur | actions/bindings versionnés, rebind validé et résolution par contexte |
| GAME-020 | `.orpsave` atomique, migration 1.1/1.2→1.3, validation et restauration jouable | pas de politique auteur pour chunks/autosave/rollback/checksum/dépendances ni enveloppe vérifiée/rotation | politique canonique, checksum, chunks déterministes, autosaves et rollback |

## Contrat minimal de fermeture

- étendre le profil gameplay module existant, sans document parallèle, avec cinq sous-contrats P0
  identifiés, versionnés, sourcés et rétrocompatibles ;
- valider champs, UUID, versions, clés, références entre actions/bindings/contextes, caméra par
  défaut et bornes numériques avec diagnostics code/chemin stables ;
- conserver l’unique commande `SetGameplayControlProfileCommand` pour dry-run/apply/undo/redo,
  l’éditeur spécialisé et le plan MCP existants ;
- fournir des services runtime purs pour déplacement, focus/verbes/hold, caméra et input ;
- piloter le runtime visible avec la configuration canonique, sans accès direct au fichier depuis
  ForgeScript ;
- enrichir la sauvegarde avec manifeste versionné, checksum SHA-256, chunks incrémentaux,
  dépendances de module, autosave rotatif, rollback et migration explicite ;
- prouver roundtrip, refus localisé, parité UI/commande, historique et démarrage jouable.

## Risques et tests exacts

- rétrocompatibilité JSON : anciens profils sans `foundation` et nouveaux profils exacts ;
- domaine : identités, doublons, bornes, références caméra/input/verbes et politiques de save ;
- application : simulation déterministe, focus, hold, caméra, rebind et lecture typée ;
- sauvegarde : checksum corrompu, chunks identiques réutilisés, dépendance absente, autosave/rollback ;
- commande/MCP : read, dry-run, diff, apply, persistence, revert et historique local ;
- interface/runtime : cinq panneaux P0, états explicites, marqueurs outil/runtime et capture visible ;
- intégration : tracker, backlog, docs, graphe et portails pré/post-promotion.

## Non-objectifs

- vol, projectiles, physique générale, ciblage de combat, temps de monde, checkpoint/respawn, HUD,
  replay ou toute autre fonction GAME P1/P2/P3 ;
- AV7–AV10, commit ou push Git.

## Résultat livré

- `GameplayFoundationProfile` étend le profil module canonique avec déplacement, interaction,
  catalogue de huit caméras, actions/bindings multi-device et politique de sauvegarde versionnée ;
- `GameplayFoundationValidator` émet des diagnostics stables sur identités, bornes et références ;
- la transaction existante `SetGameplayControlProfileCommand` conserve dry-run/apply/undo/redo et
  persiste exactement le nouveau contrat sans casser les anciens documents JSON ;
- les services application couvrent accélération, focus/verbes/hold, sélection caméra, résolution
  des bindings/rebind et lectures ForgeScript typées ; le runtime consomme le profil canonique ;
- `RuntimeSaveArchiveStore` fournit manifeste, chunks réutilisés, SHA-256, dépendances, rotation,
  rollback et migrations tout en conservant la sauvegarde historique `.orpsave` ;
- l'outil auteur présente cinq panneaux P0, valide avant mutation et publie le même contrat via MCP.

## Preuves exécutées

- capture inspectée : `docs/aurora-parity/evidence/r010-gameplay-foundation-ui.png` (1260×820),
  thème sombre, cinq panneaux, états explicites et valeurs canoniques exactes ;
- marqueurs intégrés : `OPENRPG_FORGE_TOOLSET_R010_GAMEPLAY_PASS` et
  `OPENRPG_FORGE_RUNTIME_R010_GAMEPLAY_PASS` ;
- portail pré-promotion : compilation sans avertissement, 1 085 tests .NET, 6 tests Python généraux,
  33 tests Python asset, documentation déterministe, tracker/backlog et graphe frais, smokes outil,
  runtime, MCP et sauvegarde, puis `OPENRPG_FORGE_VERIFICATION=PASS` ;
- promotion limitée à `ORF-GAME-001`, `ORF-GAME-010`, `ORF-GAME-012`, `ORF-GAME-013` et
  `ORF-GAME-020`. Le portail post-promotion doit rester vert avant activation de R011.
