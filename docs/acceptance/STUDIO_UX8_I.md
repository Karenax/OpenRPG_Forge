# Acceptation Studio — UX8-I Compatibilité et migrations

État local au 22 août 2026 : **fonctionnel et qualifié** pour deux routes UX8. UX8 atteint ainsi
**38/51** routes accessibles dans Studio.

La page `production` réunit deux autorités sans les confondre : le contrat versionné du workspace
est modifié par `SetWorkspaceBuildProfileHandler`, tandis que les manifests d’assets restent en
lecture seule et sont résolus par `AssetPlatformParityValidator` puis
`AssetPackageManifestResolver`. Aucune mesure matérielle ni migration implicite n’est revendiquée.

## Routes couvertes

<a id="orf-proj-025"></a>
- **ORF-PROJ-025 — Compatibilité et migrations** : runtime minimal, format projet cible,
  migrations automatiques et caractère bloquant de la compatibilité runtime sont édités sans
  exposer l’identité technique du profil. L’aperçu construit le manifeste runtime correspondant et
  exécute `WorkspaceBuildCollaborationService.MigrateWorkspace` en dry-run vers le format courant.

<a id="orf-cont-025"></a>
- **ORF-CONT-025 — Compatibilité de plateforme** : l’utilisateur sélectionne un manifeste humain et
  Windows, Linux, macOS, Android ou iOS. L’analyse choisit une variante exacte ou le repli `Any`,
  contrôle les rôles requis, le budget, les fichiers présents, leurs SHA-256 et les métadonnées de
  shader/échelle/densité quand elles existent. Les manifests restent la propriété transactionnelle
  d’UX3-G et ne sont pas réécrits ici.

## Transaction, migration et préservation

- sélection explicite du module, de l’asset et de la plateforme ;
- empreinte du profil source complet et empreinte binaire typée du brouillon approuvé ;
- dry-run puis re-dry-run de `SetWorkspaceBuildProfileHandler` avant application ;
- migration strictement dry-run avant application du profil, avec format source/cible et documents
  concernés visibles ;
- undo, redo, sauvegarde puis réouverture d’un workspace isolé ;
- gouvernance, collaboration, layout, CLI, déterminisme, cache et toutes les autres queues du profil
  préservés exactement ;
- manifeste de preuve avec variante `Any` + variante Windows/Linux, fichiers réels et hashes réels ;
- projet, historique, révision, journal, dirty, sélection et approbation source inchangés ;
- répertoire temporaire supprimé et contrôlé avant PASS.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **64/64** — Domain 8, Application 12, ModuleFormat 44 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX8_I_PASS` avec routes, sélection, compatibilité,
  gate runtime, migration, plateforme, hashes, dry-run, commande, gardes stale, undo/redo,
  persistance, voisins, nettoyage, UI humaine, source inchangée et surface restaurée à `true` ;
- capture Forward+ **1920×1080** inspectée : Forgotten Forest réel, deux routes nommées, plan de
  migration 1.28.0 → 1.30.0, variante Windows exacte, budget, rôles, chemins humains, diagnostics
  et six actions visibles sans UUID ni clipping ;
- portail incrémental : **PASS en 423,41 s** — build 1,44 s, tests 1,83 + 2,21 + 7,96 s,
  smoke 401,62 s, capture 7,43 s et matrice 0,86 s ;
- matrice Studio après promotion : **618 `functional` / 99 `planned`**, dont UX8 **38/51**.
- graphe Roslyn final : **11 133 nœuds / 85 504 relations**, frais, pleinement déterministe et sans
  diagnostic.

Artefacts :

- `artifacts/verification/studio-increment/ux8-i-compatibility-migrations-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-i-compatibility-migrations.summary.json`.

## Limites honnêtes

La migration est planifiée sur le vrai workspace mais n’est pas appliquée pendant la preuve UI ;
le smoke exerce l’application du profil uniquement dans une session jetable. Le contrôle plateforme
lit les fichiers et hashes réels, mais ne lance aucun GPU, appareil mobile, import runtime, benchmark
ou certification de pilote. Une plateforme non déclarée reste signalée comme incompatible ; Studio
ne fabrique pas de variante de secours.

Le portail conserve `full_gate=false` et `graph_check=false` ; le graphe est régénéré ensuite par
l’outil Roslyn officiel. Cette preuve locale ne remplace pas `scripts/Verify.ps1` ni une release
publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~WorkspaceBuildProfileValidatorTests|' +
  'FullyQualifiedName~AssetExternalEditTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileCommandTests|' +
  'FullyQualifiedName~AssetPackageManifestTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileStoreTests|' +
  'FullyQualifiedName~WorkspaceBuildCollaborationServiceTests|' +
  'FullyQualifiedName~ModulePackageTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page production `
  -Evidence ux8-i-compatibility-migrations `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts/verification/studio-increment/ux8-i-compatibility-migrations-forward-plus-fhd.png'
```
