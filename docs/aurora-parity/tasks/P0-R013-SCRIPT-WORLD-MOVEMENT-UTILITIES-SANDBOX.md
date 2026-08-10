# P0 R013 — monde, mouvement, utilitaires et sandbox ForgeScript

Lot stable : `R013` / `FC-SCRIPT-P0-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-SCR-009` — API zones et objets.
- `ORF-SCR-017` — API mouvement.
- `ORF-SCR-021` — API math et chaînes.
- `ORF-SCR-022` — Variables locales.
- `ORF-SCR-027` — Sandbox sans fichier/réseau.
- Source immuable relue intégralement pour les cinq sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/18_SCRIPT.md`.
- Dépendances réévaluées : AREA fournit zones, entités, transformations et commandes ; WAYPOINT et
  GAMEPLAY fournissent navigation et modes de mouvement ; R011 fournit le blackboard canonique typé,
  ses scopes, sa réplication, sa persistance et son handle sûr ; R012 fournit source `.as`, compilation,
  bindings, transactions, éditeur, MCP et sandbox à quotas.

## Matrice d’audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| SCR-009 | zones et entités canoniques, UUID, transformations, spawn par commande et requêtes spécialisées | le host natif n’expose que `health`, `spawn` et `unlock` ; aucune zone typée, requête bornée, transform, destruction ou portée | API auto-documentée, handles opaques, intents monde déterministes et validation des références |
| SCR-017 | navigation de surface/volume, waypoints, modes de créature, téléportation de portail et vélocité | aucune tâche ForgeScript move/face/follow/teleport, aucun identifiant de tâche ni événement d’achèvement | tâches bornées, modes, destination/vélocité, annulation et événements structurés |
| SCR-021 | types spatiaux canoniques, calcul déterministe .NET et chaînes UTF-8 AngelScript | aucun type vectoriel/quaternion exécutable, politique de math déterministe, formatage Unicode ou regex bornée dans l’allowlist | bibliothèque native sûre, limites explicites et tests nominaux/abusifs |
| SCR-022 | blackboard R011 avec schéma UUID, types, defaults, scopes, inspector, réplication, persistance et handle | le host natif ne propose qu’un événement chaîne `variable`; aucune lecture/écriture locale typée par objet | getters/setters typés, portée objet, diagnostics et pont vers le blackboard canonique |
| SCR-027 | VM n’enregistre qu’une allowlist, quotas instruction/temps/récursion/spawn et récupération après interruption | absence d’un manifeste de capacités et de tests négatifs explicites fichier/réseau/processus/pointeur/réflexion/moteur/.NET | manifeste versionné, rejet à la compilation et runtime sain après quota |

## Contrat minimal de fermeture

- conserver le domaine et les commandes existants comme autorité ; le host ForgeScript n’émet que des
  handles opaques et des intents structurés, jamais des objets moteur ni une mutation parallèle ;
- exposer zone courante, recherche bornée, position, spawn à une position, destruction et portées ;
- exposer des tâches move/face/follow/teleport, modes, vélocité, annulation, état et achèvement ;
- fournir vecteurs/quaternions, calculs finis déterministes, formatage invariant, Unicode et regex bornée ;
- fournir variables locales typées par entité, reliées au schéma/handle blackboard déjà canonique ;
- publier un manifeste d’allowlist versionné qui exclut explicitement fichier, réseau, processus, pointeurs,
  réflexion, Godot et .NET ; refuser ces symboles avant build avec diagnostics localisés ;
- réutiliser l’éditeur/console/diagnostics et les transactions R012 ; compile/test ne modifient aucune source ;
- prouver cas nominal, cas invalide, round-trip, équivalence commande/interface, undo/redo et cas abusif.

## Risques et tests exacts

- ABI du host natif et reconstruction reproductible avec AngelScript 2.38 ;
- ordre et format stables des événements monde/mouvement/variables ;
- handles nuls/inconnus, nombres non finis, chaînes excessives, regex invalide ou trop longue ;
- absence réelle des API interdites et récupération après quota ;
- compatibilité des scripts historiques et du blackboard R011 ;
- interface : catalogue visible, état vide/erreur, console, diagnostics cliquables et capture inspectée ;
- intégration : tracker, backlog, docs, graphe et portails pré/post-promotion.

## Non-objectifs

- combat, effets, IA, caméra, audio, temps, JSON, débogueur ou autres fonctions SCRIPT P1/P2/P3 ;
- accès direct au moteur, au système de fichiers, au réseau, aux processus ou aux bibliothèques natives ;
- AV7–AV10, commit ou push Git.

## Résultat livré

- `ForgeScriptCapabilityManifest` constitue l’allowlist v1 lisible par l’éditeur et MCP, borne requêtes,
  chaînes et regex, et refuse avant compilation fichier, réseau, processus, pointeur natif, réflexion,
  moteur et .NET avec code, chemin et UUID stables ;
- le host natif AngelScript expose handles `AreaRef`/`EntityRef`/`MovementTaskRef`, vecteurs et quaternions
  valeur, requête de zone, transform, spawn/destruction, tâches move/face/follow, téléportation, vélocité,
  annulation/achèvement, utilitaires invariants et variables locales typées ;
- `ForgeScriptWorldCommandService` traduit les événements opaques en mutations de `AreaDefinition` sans
  objet moteur : validation des références/catalogues, preview/apply, révision, atomicité et undo/redo ;
- les variables locales natives couvrent entier, nombre, chaîne, entité et vecteur ; le schéma, les defaults,
  scopes, l’inspecteur, la réplication et la persistance restent ceux du blackboard canonique livré en R011 ;
- `get_api` MCP publie capacités, interdictions, quotas et signatures ; compile/test refusent la même
  capability interdite avant d’appeler la VM ;
- l’éditeur ForgeScript affiche quatre groupes d’API P0 et la frontière de sécurité, tout en conservant
  source canonique, événements, console, diagnostics cliquables et transactions R012.

## Preuves ciblées exécutées

- host natif reconstruit depuis les sources : `OPENRPG_FORGE_FORGESCRIPT_NATIVE_BUILD_PASS` ;
- tests : 45 ForgeScript, 284 Application et 8 MCP ForgeScript ; solution compilée sans avertissement ni erreur ;
- marqueurs intégrés : `OPENRPG_FORGE_TOOLSET_R013_SCRIPT_WORLD_API_PASS` et
  `OPENRPG_FORGE_RUNTIME_R013_SCRIPT_WORLD_API_PASS`, plus smoke runtime complet réussi ;
- capture inspectée : `docs/aurora-parity/evidence/r013-forgescript-world-api-ui.png` (1440×880), catalogue
  lisible, source monde/mouvement visible, compilation réussie et zéro diagnostic ;
- portail pré-promotion : compilation sans avertissement, 1 114 tests .NET, 6 tests Python généraux,
  33 tests Python asset, documentation/tracker/backlog/graphe frais et tous les smokes intégrés, puis
  `OPENRPG_FORGE_VERIFICATION=PASS` ;
- promotion limitée à `ORF-SCR-009`, `ORF-SCR-017`, `ORF-SCR-021`, `ORF-SCR-022` et `ORF-SCR-027` ;
  le portail post-promotion doit rester vert avant activation de R014.
