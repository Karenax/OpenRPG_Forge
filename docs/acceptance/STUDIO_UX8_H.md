# Acceptation Studio — UX8-H Distribution et livraison

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX8.
UX8 atteint ainsi **36/51** routes accessibles dans Studio.

La page `export` expose les cinq cibles du module, les plateformes de livraison, le packaging
Windows, la politique de signature publique et les mises à jour delta. La configuration passe par
`SetModuleDistributionHandler` ; le contrôle optionnel des artefacts réutilise les builders de
distribution réels dans une copie temporaire. Studio ne demande et ne conserve aucune clé privée.

## Routes couvertes

<a id="orf-proj-024"></a>
- **ORF-PROJ-024 — Paramètres par cible** : Éditeur, Client joueur, Serveur dédié, Tests et
  Livraison conservent leur identité stable, activation, données auteur, symboles, optimisation,
  politique de licences, nom de sortie et profil de secrets nommé. Les cinq profils sont édités
  individuellement sans écraser leurs voisins.

<a id="orf-build-016"></a>
- **ORF-BUILD-016 — Build multi-plateforme** : Windows x64, Linux x64 et macOS Apple Silicon sont
  sélectionnés explicitement. `ModuleReleaseBundleBuilder` produit deux fois chaque bundle dans
  l’isolat et la preuve exige des octets déterministes ainsi que les scripts de cycle de vie attendus.

<a id="orf-build-017"></a>
- **ORF-BUILD-017 — Packaging Windows** : archive portable, installateur ou les deux, détection des
  prérequis, journal d’installation, désinstallateur et prérequis déclarés alimentent la politique
  canonique. Le profil Stable est refusé si les garanties Windows requises manquent.

<a id="orf-cont-028"></a>
- **ORF-CONT-028 — Signatures** : modes désactivé, optionnel ou requis, profil public, empreintes
  SHA-256 de confiance et empreintes révoquées sont authorés sans secret. Le smoke crée une paire
  P-256 éphémère uniquement dans son répertoire temporaire, signe puis vérifie un package réel.

<a id="orf-cont-029"></a>
- **ORF-CONT-029 — Delta updates** : taille de blocs, reprise, déduplication, rollback et nombre de
  versions conservées sont éditables. La preuve exécute création, application puis rollback du delta
  et compare le package reconstruit ; une politique Stable incomplète reste bloquée.

## Transaction, artefacts et préservation

- sélection explicite du module et de la cible ; deux colonnes défilables et footer d’actions fixe ;
- empreintes binaires typées du brouillon, plus empreintes du projet source et du candidat ;
- approbation liée à la révision, à la source, au brouillon et au candidat exacts ;
- dry-run puis re-dry-run de `SetModuleDistributionHandler` avant application ;
- undo, redo, sauvegarde puis réouverture du workspace isolé ;
- dépendances et profil de lanceur de `ModuleDistributionConfiguration` préservés exactement ;
- projet, historique, révision, journal, dirty, sélection et approbation source inchangés ;
- build et bundles optionnels dans `openrpg-forge-studio-ux8-h-*`, puis suppression vérifiée ;
- sur la vraie source, les diagnostics de provenance/licence restent visibles et bloquants. La
  capture de preuve désactive ces gates uniquement dans son brouillon dry-run, jamais dans le module.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **22/22** — Domain 10, Application 2, ModuleFormat 7, Scripting 3 ;
- le test Scripting verrouille réellement le document cible sous Windows, puis prouve le remplacement
  atomique à fichier temporaire unique et la reprise bornée sans résidu `.tmp` ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX6_P_PASS` avec `apply_undo_redo=true`, puis
  `OPENRPG_FORGE_STUDIO_UX8_H_PASS` avec routes, cibles, plateformes, Windows, signature, delta,
  bundles, dry-run, commande, refus Stable, gardes stale, undo/redo, persistance, voisins, UI humaine,
  source inchangée et surface restaurée à `true` ;
- capture Forward+ **1920×1080** inspectée : cinq routes nommées, deux scrolls explicites, plan et
  diagnostics lisibles, sept actions visibles, aucun UUID, secret ou clipping bloquant ;
- portail incrémental : **PASS en 422,07 s** — build 1,19 s, tests 1,51 + 2,45 + 4,20 + 1,52 s,
  smoke 402,79 s, capture 7,53 s et matrice 0,82 s ;
- matrice Studio après promotion : **616 `functional` / 101 `planned`**, dont UX8 **36/51** ;
- graphe Roslyn final : **11 120 nœuds / 85 368 relations**, frais, pleinement déterministe et sans
  diagnostic.

Artefacts :

- `artifacts/verification/studio-increment/ux8-h-distribution-release-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-h-distribution-release.summary.json`.

## Limites honnêtes

La vérification de bundles construit réellement dans une copie temporaire, mais ne publie, ne
téléverse et n’installe rien. L’installateur Windows est un artefact de distribution contrôlé ; il
n’est pas exécuté sur la machine pendant ce portail. Une signature requise reste la responsabilité
de la CI ou de l’opérateur qui détient la clé privée externe. Studio ne certifie ni magasin, ni
notarisation, ni disponibilité réseau, ni canal public de mise à jour.

Le portail Studio conserve `full_gate=false` et `graph_check=false` ; le graphe est vérifié ensuite
par l’outil Roslyn officiel. Cette preuve locale ne remplace pas `scripts/Verify.ps1` ni une release
publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Scripting.Tests\Forge.Scripting.Tests.csproj'
)
$filter = 'FullyQualifiedName~ModuleReleaseConfigurationTests|' +
  'FullyQualifiedName~DistributionComplianceValidatorTests|' +
  'FullyQualifiedName~SetModuleDistributionHandlerTests|' +
  'FullyQualifiedName~ModuleDistributionPipelineTests|' +
  'FullyQualifiedName~ForgeScriptInteropProfileTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page export `
  -Evidence ux8-h-distribution-release `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-h-distribution-release-forward-plus-fhd.png'
```
