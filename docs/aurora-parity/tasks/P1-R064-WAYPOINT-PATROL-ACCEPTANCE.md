# P1 R064 — Waypoints, notes de carte et patrouilles

## Périmètre

Lot canonique `R064 / FC-WAYPOINT-P1-01`, limité à cinq fonctions :

- `ORF-WPT-002` — Apparence d’éditeur ;
- `ORF-WPT-003` — Map note ;
- `ORF-WPT-004` — Map note activable ;
- `ORF-WPT-007` — Points de patrouille ordonnés ;
- `ORF-WPT-008` — Patrouille forward/reverse.

Le lot réutilise les ancres et portails validés en R009. Il n’ouvre ni fonction P2, ni AV7–AV10,
ni navigation multijoueur avancée.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/14_WAYPOINT.md` avant modification.

| Critère source | État avant R064 | Limite réelle fermée par R064 |
|---|---|---|
| Apparence dans l’éditeur | icône, couleur, couche, visibilité et taille existaient comme données isolées | profil canonique rattaché à la zone, validation bornée et aperçu graphique à taille écran constante |
| Note de carte | texte, icône, étage, catégorie et précision étaient modélisés | affichage HUD, suivi et politique de visibilité reliés au même waypoint persistant |
| Activation de note | portée, événement, animation et persistance étaient modélisés | révélation autoritaire joueur, groupe ou monde, déclenchée par événement et conservée dans l’état runtime |
| Patrouille ordonnée | points, attente, facing, vitesse, action, branche et poids existaient sans intégration d’auteur complète | route UUID ordonnée avec arêtes stables, éditeur, validation, commande atomique et acteur runtime |
| Forward/reverse | algorithmes loop, ping-pong, once, random, weighted et scripted existaient dans un service isolé | curseur runtime autoritaire, script typé et déplacement d’un acteur moteur avec attente, orientation et vitesse |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R064 |
|---|---|
| Domaine canonique avant représentation moteur | `WaypointAuthoringProfile`, `WaypointProfile`, `PatrolRoute` et `PatrolRouteEdge` restent indépendants de l’interface et du moteur |
| Identité et ordre déterministes | UUID, schéma, provenance, références typées, arêtes stables et ordre explicite validés |
| Validation structurée | diagnostics stables pour identité, zone, apparence, note, ancre, route, référence et budgets |
| Interface visible, vide et erreur | onglet `Waypoints & patrouilles`, formulaires, résumé, état et aperçu graphique de la route |
| UI et commande sans voie parallèle | `SetAreaNavigationHandler`, dry-run/apply/reject, undo/redo et round-trip exact |
| ForgeScript typé | handles sûrs `WaypointRef`, `PatrolRouteRef`, révélation et progression de patrouille |
| MCP filtré et transactionnel | lecture bornée par zone, UUID, nom ou couche, plus aperçu de mutation sans écriture |
| Runtime optionnel/requis | absence optionnelle ignorée et absence requise signalée par diagnostic stable |
| Runtime jouable | marqueur HUD visible et acteur moteur déplacé sur une route ping-pong autoritaire |

## Validation exécutée

- build solution : 27 projets, zéro avertissement et zéro erreur ;
- tests ciblés : Domain 3/3, Application 7/7, ModuleFormat 1/1 et MCP 1/1 ;
- contrat MCP stdio : 1/1 ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R064_WAYPOINT_PATROL_PASS` avec restauration T32 ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R064_WAYPOINT_PATROL_PASS` avec acteur moteur et HUD ;
- capture `docs/aurora-parity/evidence/r064-waypoint-patrol-ui.png`, 1440×820, inspectée après
  correction de la largeur : formulaires, route, aperçu et actions sont lisibles sans rognage ;
- tracker, backlog, documentation et graphe Roslyn régénérés avant le portail intégré ;
- portail `scripts/Verify.ps1` requis avant ouverture de R065.

## Décision

Les cinq limites `Partial` identifiées — intégration canonique, auteur, commande, runtime et preuve
visuelle — sont fermées dans ce seul lot. Les cinq fonctions passent à `Tested` uniquement avec la
preuve R064 ci-dessus et après succès du portail intégré ; aucune autre ligne P1 n’est promue.
