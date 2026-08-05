# G11-L — Déplacements, roue d’actions, temps tactique et combat

## Périmètre

- `ORF-GAME-003` — nage et plongée avec flottabilité, courants et souffle ;
- `ORF-GAME-004` — escalade avec liens auteur, endurance, IK et garde-fous ;
- `ORF-GAME-011` — menu radial imbriqué, manette, clavier et repli accessible ;
- `ORF-GAME-015` — pause, ralenti, planification, tours et permissions multijoueur ;
- `ORF-GAME-016` — fournisseurs de combat partageant les mêmes événements.

## Résultat livré

- profil canonique `ModuleGameplayControlProfile` v1, propriétaire du module, composé des profils
  Swimming, Climbing, RadialMenu, TimeControl et CombatModes ; chaque agrégat porte UUID stable,
  schéma et provenance ;
- validation d’identité et d’équivalence des contrats d’événements entre fournisseurs de combat ;
- services Application déterministes pour résoudre sol/nage/plongée/escalade, construire les pages
  de roue imbriquées, autoriser la pause réseau, recueillir un vote et ordonner une file d’actions ;
- commande atomique avec dry-run, diff, inverse, audit, undo et redo ;
- fenêtre **Contrôles, actions & combat** accessible depuis Zones : réglages de nage, escalade,
  actions imbriquées, accessibilité, pause, ralenti, planification, permissions et fournisseur ;
- runtime joueur avec détection des volumes d’eau et surfaces marquées, souffle/endurance, roue
  visible sur `Tab`, pause/planification sur `P`, ralenti sur `T` et modes de combat sur `M` ;
- une reprise après planification exécute la file dans l’ordre canonique ; les contrôles du joueur et
  l’avancement climat/politiques/cinématiques restent réellement gelés pendant la pause ;
- API MCP gouvernée en lecture, plan dry-run/diff, application persistée et réversion ;
- formats projet `0.6.14` et workspace `1.8.0`, avec migration déterministe des trois workspaces
  fournis et profil par défaut complet pour les documents antérieurs.

## Preuves

- `GameplayControlProfileTests` couvre stabilité, complétude, identité et événements partagés ;
- `GameplayControlRuntimeTests` couvre nage, plongée, escalade, récupération, roue imbriquée,
  accessibilité, vote, planification et capacité de la file ;
- `SetGameplayControlProfileHandlerTests` couvre dry-run, transaction, undo et redo ;
- `GameplayControlPersistenceTests` couvre le round-trip JSON ;
- `ForgeProjectAgentServiceTests` couvre lecture, plan MCP, persistance et réversion ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_G11L_GAMEPLAY_PASS` sur la Crypte réelle.

## Limites explicites

- l’éditeur de roue emploie une liste structurée `clé | libellé | commande | parent` ; une surface
  graphique par glisser-déposer pourra améliorer l’ergonomie sans changer le contrat ;
- l’escalade runtime exige une surface explicitement marquée par un tag autorisé. Les animations et
  l’IK sont nommées dans le profil, mais le personnage capsule de démonstration n’embarque pas encore
  de squelette artistique auquel les appliquer ;
- la nage fonctionne dans les `WaterBodyProfile` réels. Les trois modules fournis ne placent pas
  encore de bassin dans leur carte principale ; le service, l’adapter de volume et la ressource
  joueur sont néanmoins testés séparément ;
- les votes et permissions sont déterministes dans le contrat Application. Leur transport entre
  clients repose sur Network Beta et devra être éprouvé dans un scénario multijoueur dédié ;
- ce lot ferme les mécanismes de jeu, pas la composition artistique finale de la carte 3D.

## Critère de passage

G11-L est fermé lorsque tests ciblés, smokes Toolset/runtime, migrations, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
