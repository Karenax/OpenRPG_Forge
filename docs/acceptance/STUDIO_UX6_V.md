# Acceptation Forge Studio — UX6-V

Date de qualification : 21 août 2026.

UX6-V rend `ORF-EE-028` directement accessible sur la page Runtime par la surface
**Pathfinding amélioré**. La configuration persistante reste séparée du laboratoire A* copié et
des plans d’adaptation moteur afin de ne pas présenter une simulation Studio comme un déplacement
réel dans Godot.

## ORF-EE-028

### Configuration persistante

- sélection explicite d’une zone et d’un profil d’agent par libellés humains, sans UUID visible ;
- pas de cellule, rayon, hauteur, ouverture de porte, liaison hors surface et vol éditables ;
- identités, ancres, waypoints, autres agents, liaisons, environnement et zones voisines préservés ;
- aperçu `DryRun` puis application par `SetNavigationTopologyHandler` ;
- garde sur révision, empreinte SHA-256 du projet et empreinte binaire préfixée du brouillon ;
- undo, redo, sauvegarde, réouverture et comparaison du projet canonique complet.

### Laboratoire A* et adaptation moteur

L’aperçu construit une vraie `NavigationSurface` puis exécute `NavigationPathfinder` sur une copie :

- départ et arrivée, portes ouvertes ou fermées, obstacle dynamique et multiplicateur de coût ;
- détour autour d’un obstacle, évitement d’une cellule coûteuse et récupération d’une extrémité
  bloquée ;
- refus déterministe lorsque le budget de recherche est dépassé ;
- plan de mise à jour partielle, plan de rebake et `SurfaceNavigationPlanBuilder` réellement calculés ;
- grille lisible distinguant surface, route, départ, arrivée, obstacle et coût local.

Aucun `NavigationAgent3D`, acteur, évitement local, mouvement, serveur ou transport réseau n’est
instancié ou exécuté par Studio. L’adaptateur Godot et l’autorité runtime restent responsables de
l’application au monde de jeu.

## Transaction et préservation

La preuve UI ouvre un workspace temporaire dérivé du module réel et ajoute une zone laboratoire
uniquement dans cet isolat. Preview, apply, undo, redo et save y sont exécutés. Projet, historique,
révision, journal, état dirty, zone, sélection et approbations de la session source sont comparés et
restaurés ; le répertoire temporaire doit être libéré pour que le smoke passe.

## Validation obtenue

- build Studio Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **10/10** — Domain 4, Application 2, EngineAdapter 1 et ModuleFormat 3 ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX6_V_PASS`, avec sélection explicite, invalidité,
  dry-run, surface, plan moteur, obstacle, coût, récupération, budget, mise à jour partielle,
  révision et approbation obsolètes, commande canonique, undo/redo, round-trip, voisins, texte
  humain, transaction UI, libération des ressources, source inchangée et surface restaurée ;
- portail incrémental : **144,21 s**, build, tests, smoke global, capture Forward+ FHD et contrôle
  des 717 lignes de matrice ; `full_gate=false` reste explicite ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-v-pathfinding.summary.json` ;
- capture Forward+ 1920×1080 inspectée :
  `artifacts/verification/studio-increment/ux6-v-pathfinding-forward-plus-fhd.png`.

La sélection, les trois onglets, la grille A*, les résultats, le diff humain et les cinq actions
transactionnelles sont visibles et lisibles sans clipping bloquant.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.EngineAdapter.Tests/Forge.EngineAdapter.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~NavigationPathfinderTests|' +
  'FullyQualifiedName~SetNavigationTopologyHandlerTests|' +
  'FullyQualifiedName~SurfaceNavigationPlanBuilderTests|' +
  'FullyQualifiedName~NavigationPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime `
  -Evidence ux6-v-pathfinding -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-v-pathfinding-forward-plus-fhd.png
```
