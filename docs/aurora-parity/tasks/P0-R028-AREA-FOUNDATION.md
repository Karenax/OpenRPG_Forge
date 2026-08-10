# P0 R028 — fondation de zone et kits modulaires

Lot stable : `R028` / `FC-AREA-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-AREA-001` — Assistant de création de zone.
- `ORF-AREA-002` — Document de zone indépendant.
- `ORF-AREA-003` — Construction par tuiles.
- `ORF-AREA-008` — Rotation et variantes de tuile.
- `ORF-AREA-022` — Événements d’entrée/sortie.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/03_AREA.md`.
- Dépendances BUILD, CONTENT, PALETTE et SCRIPT réévaluées : build canonique et incrémental,
  packages/ressources typés, catalogue de blueprints et événements ForgeScript sûrs sont déjà
  `Tested`. Le lot ne rouvre aucun de ces domaines.

## Matrice d’audit avant implémentation

| Fonction | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Assistant | Nom, type, dimensions, vide/jouable, dry-run, commande et historique existent dans `AreaWorkspaceWindow`/`CreateAreaHandler` | Kit, terrain, éclairage, navigation, streaming, biome, graine et budget ne forment pas un profil canonique prévisualisable | Profil UUID v1 validé, assistant sans JSON, aperçu, apply/undo/redo et reload exact |
| Document indépendant | JSON de zone séparé, UUID, schéma, environnement, instances, portails, navigation, monde et politiques runtime existent | Provenance/document metadata, dépendances explicites, volumes et contrat de fondation ne sont pas réunis ni validés comme document complet | Métadonnées stables, profil de fondation embarqué, round-trip déterministe et diagnostic localisé |
| Tuiles | Placement d’entités sur grille et transformations transactionnelles existent | Pas de modèle canonique pour kit, sockets, règles de raccord, groupes, variantes et pinceaux | Kit modulaire typé, validateur de raccord, service de prévisualisation et commande atomique |
| Rotation/variantes | Rotation 90° d’entité existe | Pas de politique libre/discrète par kit, symétrie, variante sémantique ou aperçu de raccord | Rotation validée selon kit, miroir explicite, variante référencée et aperçu déterministe |
| Entrée/sortie | `RuntimeLifecycleCoordinator` émet déjà `AreaEntered`/`AreaExited` avec acteur, transition, cause, contexte, autorité, binding, schéma et provenance | Scripts configurables requis/optionnels et validation de leur présence ne sont pas attachés au document de zone | Bindings canoniques, diagnostic avant build, dispatch runtime ordonné et contrat ForgeScript/MCP |
| Surfaces | Commandes Area existantes, undo/redo et écran spécialisé sont présents | Pas de surface unique couvrant la fondation ni de lecture/mutation MCP filtrée | UI visible avec états vide/erreur, commande dry-run, ForgeScript par handle et MCP plan/apply |

## Contrat minimal de fermeture

- ajouter à chaque zone un profil de fondation UUID v1, à provenance explicite, couvrant création,
  document, kit modulaire et bindings `area.entered`/`area.exited` ;
- conserver le format JSON de zone indépendant et compléter son modèle sans dépendance Godot ;
- fournir grille, sockets typés, règles, groupes, pinceaux, variantes, rotation libre/discrète,
  symétrie et aperçu de raccord déterministe ;
- valider identité, schéma, références, chemins de scripts, transforms, géométrie de grille et règles
  avant application/build avec code, chemin et UUID ;
- exposer le même contrat via commande transactionnelle, UI spécialisée, ForgeScript sûr et MCP
  plan/apply filtré ;
- préserver les zones legacy avec un profil par défaut stable et des bindings optionnels.

## Fichiers probables

- `src/Forge.Domain/Areas/` pour les modèles, défauts et validation de fondation/kit ;
- `src/Forge.Application/Areas/` pour commandes, historique, aperçu et surface ForgeScript ;
- `src/Forge.ModuleFormat/Serialization/` et validation canonique pour persistance/preflight ;
- `apps/Forge.Toolset/addons/openrpg_forge/AreaWorkspaceWindow.cs`, fenêtre spécialisée et plugin ;
- `src/Forge.Mcp/Projects/`, hôte MCP, runtime lifecycle et tests associés ;
- tracker, preuves visuelles/headless, backlog, documentation et graphe.

## Risques et tests exacts

- UUID vide/dupliqué, schéma futur, provenance absente et profil legacy manquant ;
- kit/blueprint/variante/socket/groupe/pinceau manquant, règle ambiguë et placement hors grille ;
- angle interdit, symétrie interdite, transform non finie et raccord incompatible ;
- script requis absent, chemin non sûr, ordre sortie→entrée et contexte acteur/transition/autorité ;
- dry-run sans mutation, apply atomique, undo/redo, second save identique et migration stable ;
- UI inspectée à résolution reproductible, MCP structuré, smoke Toolset/runtime et portail global.

## Non-objectifs

- Terrain P1/P2, navmesh triangulé massif, streaming monde ouvert complet, nouveaux assets AV7–AV10,
  P1/P2, édition concurrente multi-utilisateur, commit ou push Git.

## Fermeture et preuves

Les cinq critères source sont fermés individuellement :

- le profil `AreaFoundationProfile` UUID v1 porte assistant, kit, volumes, dépendances, provenance et
  bindings lifecycle tout en fournissant un défaut legacy stable ;
- `AreaFoundationValidator` refuse identités ou références invalides, cellules/entités dupliquées,
  rotations ou miroirs incompatibles, géométrie non finie et chemins non confinés ;
- `ModularTilePlacementResolver` projette grille, rotation, variante sémantique et miroir dans
  `ProjectPreview3D` et `GameRoot`, donc le contrat n'est pas seulement persisté mais consommé en 3D ;
- `SetAreaFoundationHandler`, l'UI Aurora, ForgeScript typé et MCP filtré partagent le même modèle,
  avec aperçu sans écriture, apply, revert et undo/redo ;
- `RuntimeLifecycleCoordinator` sélectionne les bindings de la zone quittée puis entrée et transmet
  acteur, transition, cause, anciens/nouveaux contextes et autorité.

Preuves ciblées vertes : 14 tests domaine/lifecycle, 6 application, 7 ModuleFormat et 1 MCP ; builds
Toolset et Runtime sans avertissement ; marqueurs
`OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_PASS` et
`OPENRPG_FORGE_RUNTIME_R028_AREA_FOUNDATION_PASS` sans erreur moteur. La capture Aurora 1280×840
inspectée est `docs/aurora-parity/evidence/r028-area-foundation-ui-final.png`.

Le tracker compte désormais 326 fonctions `Tested`, dont les cinq IDs du lot ; tracker, backlog et
graphe déterministe ont été régénérés. Le graphe contient 5 942 nœuds, 36 026 relations et zéro
diagnostic.

Portail intégré final : 27 projets construits sans avertissement ni erreur, 1 241 tests .NET,
6 tests Python généraux, 33 tests Python asset-pipeline, smokes Toolset/runtime/packaging/réseau/MCP
et `OPENRPG_FORGE_VERIFICATION=PASS`.
