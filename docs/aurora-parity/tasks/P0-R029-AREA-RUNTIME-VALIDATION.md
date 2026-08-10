# P0 R029 — transitions, navigation, Play From Here et validation de zone

Lot stable : `R029` / `FC-AREA-P0-02` — quatre fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-AREA-030` — Transitions de zone.
- `ORF-AREA-031` — Navigation de surface.
- `ORF-AREA-036` — Play From Here.
- `ORF-AREA-037` — Validation de zone.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/03_AREA.md`.
- Dépendances DOOR, TRIGGER, WAYPOINT, GAMEPLAY et QA réévaluées : les contrats typés de portail,
  ancre, mouvement et validation canonique existent. Le lot borne leur intersection à la transition
  de zone et n'ouvre aucun lot P1/P2 ni les lots P0 dépendants encore non prouvés.

## Matrice d'audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Transitions | `PortalLink`, commande dry-run/apply, validation des références, persistance, bidirectionnalité et préchargement existent | Pas de schéma/provenance propres au portail, politique d'orientation, hooks sûrs ni exécution runtime de la transition/streaming | Profil de portail canonique, service runtime ordonné, UI/commande/historique/reload, ForgeScript et MCP filtré |
| Navigation de surface | Bake déterministe par zone, quatre tailles/vol, liens hors-mesh, portes, reachability, rebuild partiel, cache, overlay et adaptation Godot triangulée existent | Preuves dispersées ; lecture ForgeScript et mutation MCP ne couvrent pas encore le document de navigation complet | Snapshot typé, MCP read/plan/apply/revert, UI existante prouvée, runtime polygons/links et diagnostics |
| Play From Here | Snapshot canonique temporaire et lancement depuis un `PlayerSpawn` existent | Aucun démarrage caméra/waypoint, profil personnage/test ni état de quête injecté/persisté | Profil UUID v1 par zone, résolution déterministe des trois modes, manifeste de lancement, UI sans JSON, commande et historique |
| Validation de zone | Validation projet, géométrie, navigation, fondation, ancres, audio et politiques runtime existent | Pas de rapport unifié par zone couvrant explicitement raccords, collisions, navmesh, transitions, références, spawns, lumière, audio et budgets | Rapport stable filtré par zone, UI visible, préflight build, ForgeScript/MCP read-only, diagnostics code/path/UUID |

## Contrat minimal de fermeture

- compléter `PortalLink` sans casser les projets legacy et exécuter orientation, préchargement et hooks
  via un service runtime pur avant adaptation moteur ;
- conserver le bake/navigation existant, ajouter seulement les surfaces sûres manquantes et prouver
  les polygones, profils, liens, rebuild partiel et overlay ;
- ajouter un profil Play From Here canonique avec modes spawn/caméra/waypoint, personnage, profil de
  test et injections de quêtes, résolu dans un snapshot temporaire sans modifier le projet source ;
- agréger toutes les validations de zone dans un rapport déterministe filtrable, utilisé avant build ;
- fournir UI spécialisée avec états vide/erreur/aperçu, commandes atomiques, undo/redo, reload exact,
  ForgeScript par handle sûr et MCP filtré/dry-run lorsque la capacité est mutable ;
- couvrir explicitement extension optionnelle absente et extension requise invalide.

## Risques et tests exacts

- portail destination/source manquante, schéma futur, yaw non fini, hook non confiné, sens inverse ;
- orientation/position/vitesse incorrectes, streaming annulé ou ressource obligatoire absente ;
- waypoint/caméra/player blueprint invalide, état de quête inconnu, snapshot qui modifie le workspace ;
- navmesh trop dense, endpoint bloqué, objectif inaccessible et cache/rebuild non déterministe ;
- rapport de zone incomplet, doublons ou diagnostics sans chemin/UUID ;
- dry-run sans écriture, apply atomique, undo/redo, round-trip, UI inspectée, smokes Toolset/runtime,
  tests ciblés puis `scripts/Verify.ps1`.

## Non-objectifs

- streaming monde ouvert massif, navigation volumétrique P2, système complet de portes/triggers,
  P1/P2, AV7–AV10, commit ou push Git.

## Fermeture prouvée

Les quatre fonctions sont closes sans ouvrir leurs dépendances P1/P2. `PortalLink` porte désormais
schéma, provenance, orientation, politique de vitesse, préchargement et hooks confinés ; sa résolution
directe ou inverse alimente un cycle runtime load/enter/start/exit/enter. La navigation existante est
consommée sous forme de surface et polygones triangulés par quatre profils agents, liens hors-mesh,
portes, rebuild partiel, cache et overlay. Play From Here configure spawn, caméra ou waypoint,
personnage, profil et injections de quête dans un manifeste et un snapshot temporaires. Le rapport de
zone couvre les neuf catégories requises avec code, chemin et UUID stables.

Preuves ciblées : 3 tests Domain, 3 Application, 6 ModuleFormat et 1 MCP, builds Runtime/Toolset/MCP
sans avertissement, marqueurs `OPENRPG_FORGE_RUNTIME_R029_AREA_RUNTIME_VALIDATION_PASS` et
`OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_PASS`. La capture 1280×840 inspectée est
`docs/aurora-parity/evidence/r029-area-runtime-ui.png` ; elle montre le profil caméra, le snapshot sans
écriture source et l’état valide 9/9. Tracker et backlog régénérés : 330 `Tested`, 382 `Implemented`,
5 `Researched`, 21 lots P0 encore ouverts. Graphe déterministe : 5 982 nœuds, 36 506 relations,
zéro diagnostic. Portail intégré final : restauration verrouillée du prototype, audit de vulnérabilité
sur 27 projets sans paquet vulnérable, build Release sans avertissement ni erreur, 1 249 tests .NET,
6 tests Python généraux et 33 tests du pipeline d'assets, smokes Toolset/Runtime/MCP, packaging,
réseau et Live Director verts, puis `OPENRPG_FORGE_VERIFICATION=PASS`.
