# Acceptation Studio — UX8-B Maintenance collaborative du build

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX8.
UX8 atteint ainsi **10/51** routes accessibles dans Studio.

La page `build` expose un second workbench à sélection explicite. Il copie le module ouvert dans
un workspace jetable puis exécute les cinq autorités R023 : cache, normalisation, migration,
références et diff. Les résultats sont navigables, les diagnostics restent accessibles à la
demande et l’utilisateur peut ouvrir la copie appliquée tant que la surface reste ouverte.

## Routes couvertes

<a id="orf-build-006"></a>
- **ORF-BUILD-006 — Cache jetable** : le cache explicite est inventorié par chemin, taille et
  SHA-256, son index est écrit dans l’isolat puis la purge confinée laisse les documents canoniques
  intacts. Tout est reconstructible depuis la source.

<a id="orf-build-008"></a>
- **ORF-BUILD-008 — Normalisation des documents** : le service prévisualise les JSON concernés,
  applique les écritures atomiques UTF-8 sans BOM/LF avec ordre et nombres stables, puis exige un
  second passage vide.

<a id="orf-build-009"></a>
- **ORF-BUILD-009 — Migrations** : le plan versionné est prévisualisé avant écriture, sauvegardé
  dans la copie, appliqué puis relu pour prouver son idempotence. Les downgrades non supportés ne
  sont jamais présentés comme disponibles.

<a id="orf-build-010"></a>
- **ORF-BUILD-010 — Références inverses** : le graphe de build est reconstruit depuis les fichiers
  canoniques et expose pour chaque nœud les dépendances avant et les dépendants inverses, avec
  identité et ordre stables.

<a id="orf-build-011"></a>
- **ORF-BUILD-011 — Diff sémantique** : un snapshot jetable produit des changements structurés
  `Added`, `Removed` ou `Modified` pour zones, graphes, entités, packages et scripts. Le texte
  visible remplace tous les UUID, y compris ceux dont la version n’est pas standard.

## Isolation, annulation et surface humaine

- les opérations s’appliquent sous `%TEMP%\openrpg-forge-studio-ux8-b`, jamais au workspace ouvert ;
- l’annulation prend effet entre deux opérations canoniques : l’opération synchrone en cours se
  termine, les suivantes ne démarrent pas et la copie est supprimée ;
- la preuve enrichit uniquement l’isolat avec un cache, un JSON non canonique, un projet legacy et
  une modification de script afin d’exercer les chemins d’application ; le parcours humain normal
  analyse exclusivement la copie fidèle du module sélectionné ;
- projet, historique, révision, journal, dirty, undo/redo, zone active, sélection, fichier projet
  et empreinte du workspace source restent identiques ;
- cinq cartes et cinq résultats sont visibles à FHD ; la copie et les journaux sont accessibles par
  actions explicites et les identifiants techniques sont masqués.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **25/25** — Domain 5, Application 6, ModuleFormat 12, MCP 2 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX8_B_PASS` avec `routes`, `selection`,
  `cache`, `normalization`, `migration`, `references`, `semantic_diff`, `cancel`, `navigation`,
  `isolated`, `ui`, `resources_released`, `source_unchanged` et `surface_restored` à `true` ;
- capture Forward+ **1920×1080** sur Forgotten Forest réel, inspectée après généralisation du filtre
  UUID : cinq cartes, résultats, détail, statut et quatre actions visibles sans clipping ;
- après smoke et capture : **0** isolat UX8-A ou UX8-B restant sous `%TEMP%`.
- portail incrémental complet : **PASS en 549,47 s** — build 1,33 s, tests Domain 1,49 s,
  Application 2,37 s, ModuleFormat 4,68 s et MCP 5,23 s, smoke 415,27 s, capture 13,99 s,
  matrice 0,86 s, génération du graphe 52,95 s et contrôle complet 51,25 s ;
- matrice Studio : **590 `functional` / 127 `planned`**, dont UX8 **10/51** ;
- graphe Roslyn : **11 043 nœuds / 84 530 relations / 0 diagnostic**, frais et déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux8-b-build-collaboration-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-b-build-collaboration.summary.json`.

## Limites honnêtes

Le workbench prépare et vérifie une copie : il ne remplace pas silencieusement le module source et
ne prétend pas fournir un undo multi-fichiers sur celui-ci. La copie peut être inspectée puis
fermée ; sa conservation définitive relève d’une action humaine hors de ce lot.

La normalisation et la migration n’exécutent ni script ni gameplay. Le graphe et le diff décrivent
les documents et ressources connus ; ils ne prouvent ni une installation, ni une signature, ni un
canal de release. Aucun shell arbitraire, réseau, commit ou publication Git n’est utilisé.

Le portail Studio demeure un incrément local avec `full_gate=false`; il ne remplace ni
`scripts/Verify.ps1` global ni une qualification de distribution.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~WorkspaceBuildProfileValidatorTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileCommandTests|' +
  'FullyQualifiedName~WorkspaceBuildCollaborationServiceTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileStoreTests|' +
  'FullyQualifiedName~BuildAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page build `
  -Evidence ux8-b-build-collaboration `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-b-build-collaboration-forward-plus-fhd.png' `
  -CheckGraph
```
