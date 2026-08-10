# P0 R009 — ancres, départs et destinations de transition

Lot stable : `R009` / `FC-WAYPOINT-P0-01` — quatre fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-WPT-001` — Waypoint nommé.
- `ORF-WPT-005` — Point de départ.
- `ORF-WPT-006` — Destination de transition.
- `ORF-WPT-017` — Validation.
- Source immuable relue intégralement pour ces quatre sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/14_WAYPOINT.md`.
- Dépendances réévaluées : AREA/PROJECT possèdent documents, commandes et historique ; DOOR/TRIGGER
  possèdent déjà une destination typée minimale ; QA et navigation sont opérationnels. Le lot ajoute
  le contrat d’ancre partagé sans ouvrir routes de patrouille, map notes ou ancres audio/VFX P1/P2.

## Matrice d’audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| WPT-001 | `WaypointProfile` et `GenericLocator` isolés, position/tag/requête | aucun `AnchorDefinition/Instance` dans le document de zone, pas de slug/localisation/scope/transform complet/blueprint/overrides | modèle intégré, roundtrip, UI et commande |
| WPT-005 | blueprint `PlayerSpawn`, création de module et diagnostic de spawn | aucun profil clearance/priorité/équipe/orientation/fallback ni résolution déterministe | profil de départ, résolution runtime nominale/fallback/refus |
| WPT-006 | `PortalLink` entity→entity et `PortalComponent` RPG Kit | aucune destination vers ancre cross-zone avec orientation, vélocité et preload validés | destination typée, résolution projet et transfert runtime |
| WPT-017 | validation navigation/portails partielle et codes stables | pas d’audit commun IDs/slugs, définition/instance, bounds, clearance/nav, destination/fallback et clé localisée | diagnostics code/chemin/UUID et panneau auteur |

## Contrat minimal de fermeture

- intégrer un `AnchorCatalog` versionné à `AreaNavigationDefinition`, sans collection projet parallèle ;
- séparer définition réutilisable et instance transformée, toutes deux identifiées, sourcées et ordonnées ;
- porter type, tags, scope, libellé localisé, `BlueprintId`/`AssetId` et overrides explicites ;
- ajouter un profil de spawn avec clearance, priorité, équipe, orientation et fallback typé ;
- ajouter une destination cross-zone avec area/ancre cibles, orientation, vélocité et preload ;
- valider identités, doublons, références, transform/bounds, clearance/navigation, fallbacks,
  destinations cross-zone et localisation avec codes/chemins stables ;
- éditer et prévisualiser dans la fenêtre Navigation existante via `SetAreaNavigationCommand`, avec
  dry-run, apply, undo/redo et overlay/gizmo visible ;
- exposer lecture ForgeScript typée, ressource MCP filtrée et mutation MCP en dry-run ;
- fournir résolution runtime optionnelle sûre et diagnostic si un catalogue requis est invalide.

## Risques et tests exacts

- migration/roundtrip : navigation historique sans catalogue et catalogue enrichi exact ;
- domaine : identité, ordre, références, bounds, localisation, clearance, fallback et cross-zone ;
- application/runtime : sélection de spawn par équipe/priorité, fallback, voyage et politiques ;
- commande : dry-run/apply/undo/redo et sauvegarde workspace ;
- ForgeScript/MCP : handles, filtre borné, preview avec diff et aucune écriture directe ;
- interface : états vide/erreur, édition, preview 3D/overlay, smoke headless et capture inspectée ;
- intégration : marqueurs Toolset/Runtime, tracker, backlog, docs, graphe et portails pré/post-promotion.

## Non-objectifs

- apparence avancée, map notes, patrouilles, routes aériennes/véhicules, caméra, audio/VFX, groupes ;
- modification des contrats P1/P2 de portes et triggers au-delà de leur résolution vers l’ancre P0 ;
- P1/P2, AV7–AV10, commit ou push Git.

## Résultat livré

- catalogue d'ancres v1 intégré au document de navigation de chaque zone, rétrocompatible avec les
  zones historiques et cloné avec remappage déterministe des UUID et références locales ;
- définition localisée et instance transformée séparées avec slug, type, scope, tags, blueprint,
  asset, ordre, provenance, exigence runtime et overrides ;
- profils de départ avec clearance, équipe, priorité, orientation, exigence de navigation et repli,
  plus résolution déterministe directe ou fallback ;
- destinations inter-zones vers UUID d'ancre avec politiques d'orientation, de vélocité et de
  préchargement ;
- validation projet des identités, doublons, localisations, références, bornes, clearance,
  navigation, fallback, destination et catalogue requis vide ;
- onglet visible dans la fenêtre Navigation, commande unique avec dry-run/apply/undo/redo, lecture
  ForgeScript typée et outils MCP filtrés/dry-run.

## Preuves exécutées

- tests ciblés : 6 domaine, 2 application, 3 ModuleFormat et 1 MCP, tous verts ;
- solution complète : 1 075 tests .NET, 6 tests Python généraux et 33 tests pipeline assets ;
- capture visible inspectée : `docs/aurora-parity/evidence/r009-waypoint-anchor-ui.png` (1240×790),
  trois types d'ancres, identité/transform, départ et destination visibles ;
- marqueurs `OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_PASS` et
  `OPENRPG_FORGE_RUNTIME_R009_WAYPOINT_PASS` observés sans erreur ;
- portail pré-promotion vert, graphe déterministe à 5 412 nœuds et 32 148 relations, zéro
  diagnostic, puis promotion limitée aux quatre IDs R009 ;
- portail post-promotion final relancé sur l'état livré, y compris le diagnostic non bloquant des
  doublons de définition ;
- aucun P1/P2, AV7–AV10, commit ou push.
