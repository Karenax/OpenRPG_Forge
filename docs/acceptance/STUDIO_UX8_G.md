# Acceptation Studio — UX8-G Packages de contenu

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX8.
UX8 atteint ainsi **31/51** routes accessibles dans Studio.

La page `packages` expose le catalogue de packages du module ouvert, son ordre de priorité, ses
dépendances et sa compilation canonique. Crypte des Cendres possède actuellement un catalogue vide :
la surface produit conserve cet état et n’ajoute rien implicitement. Les deux packages visibles dans
la preuve sont un brouillon local ; la transaction complète est exercée uniquement dans un workspace
temporaire isolé, sans modifier la source.

## Routes couvertes

<a id="orf-proj-020"></a>
- **ORF-PROJ-020 — Contenu personnalisé ordonné** : le catalogue complet est ordonné par priorité
  puis namespace selon la politique de conflit choisie. L’aperçu montre explicitement la ressource
  gagnante avant toute application et préserve les sources/assets gérés par UX3-H.

<a id="orf-cont-001"></a>
- **ORF-CONT-001 — Package de contenu versionné** : namespace, version SemVer, licence SPDX,
  provenance, hash canonique, priorité et ressources scellées sont édités avec identités stables.
  Le hash est recalculé à partir du contenu complet sans masquer les tails protégés.

<a id="orf-cont-002"></a>
- **ORF-CONT-002 — Ordre de priorité** : `ContentPackageResolver` applique la politique canonique et
  l’aperçu affiche l’ordre résolu ainsi que le package propriétaire de chaque ressource logique.

<a id="orf-cont-003"></a>
- **ORF-CONT-003 — Dépendances** : dépendances Required, Optional et Peer, contraintes SemVer et
  provenance sont éditables. Les références obligatoires absentes et les cycles restent bloquants
  avec diagnostic Code + chemin ; le cas invalide ne sérialise ni ne modifie le candidat.

<a id="orf-cont-007"></a>
- **ORF-CONT-007 — Compilation de package** : `ContentPackageCompiler` valide et résout le catalogue,
  puis produit lock, manifeste et ressources en mémoire. Aucun package disque n’est écrit.

## Transaction et préservation

- sélection explicite du package et de la dépendance, avec créations et suppressions distinctes ;
- empreintes binaires typées et exhaustives du brouillon, plus empreintes projet source/candidat ;
- approbation liée à la révision, à la source et au brouillon exacts ;
- dry-run puis re-dry-run de `SetContentWorkspaceHandler` avant application ;
- patch, inverse et audit non appliqué pendant la prévisualisation ;
- undo, redo, sauvegarde puis réouverture du workspace isolé ;
- batch, tables, merge policy, sources, assets, autres packages, module et voisins projet préservés ;
- projet, historique, révision, journal, dirty, sélection et approbation source inchangés ;
- workspace temporaire `openrpg-forge-studio-ux8-g-*` supprimé et surface restaurée.

## Preuves exécutées

- moteur lancé : exécutable officiel **Godot 4.7.1 Mono**, backend Vulkan Forward+ sur AMD Radeon
  RX 7800 XT ;
- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **47/47** — Domain 6, Application 3, ModuleFormat 38 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX8_G_PASS` avec routes, sélection, version,
  priorité, dépendances, compilation, dry-run, commande, gardes stale, undo/redo, persistance,
  voisins, libération, UI humaine, source inchangée et restauration de surface à `true` ;
- capture Forward+ **1920×1080** corrigée puis inspectée : deux colonnes défilables, aperçu, lock,
  diagnostics et cinq actions visibles sans clipping ni UUID ;
- portail incrémental complet : **PASS en 544,55 s** — build 2,43 s, tests Domain 1,60 s,
  Application 2,55 s et ModuleFormat 7,75 s, smoke 414,82 s, capture 8,23 s, matrice 0,89 s,
  génération du graphe 52,49 s et contrôle complet 53,73 s ;
- matrice Studio après promotion : **611 `functional` / 106 `planned`**, dont UX8 **31/51** ;
- graphe Roslyn final régénéré à **11 105 nœuds / 85 168 relations**, frais, déterministe et sans
  diagnostic.

Artefacts :

- `artifacts/verification/studio-increment/ux8-g-content-packages-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-g-content-packages.summary.json`.

## Limites honnêtes

Studio compile ici un plan, un lock et un manifeste en mémoire. Il n’installe, ne télécharge, ne
signe et ne publie aucun package ; il n’exécute aucun script et ne modifie aucune source ou asset
géré par UX3-H. Une dépendance déclarée ne prouve pas qu’un fournisseur externe a été audité.

Le portail Studio demeure un incrément local avec `full_gate=false`; il ne remplace ni
`scripts/Verify.ps1` global ni une qualification de distribution signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ContentPackageCatalogTests|' +
  'FullyQualifiedName~ContentResourceRegistryTests|' +
  'FullyQualifiedName~SetContentWorkspaceHandlerTests|' +
  'FullyQualifiedName~ContentPackageScriptServiceTests|' +
  'FullyQualifiedName~ContentPackageCompilerTests|' +
  'FullyQualifiedName~ModulePackageTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page packages `
  -Evidence ux8-g-content-packages `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-g-content-packages-forward-plus-fhd.png' `
  -CheckGraph
```
