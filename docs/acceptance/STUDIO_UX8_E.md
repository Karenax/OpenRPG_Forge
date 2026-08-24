# Acceptation Studio — UX8-E Preuves de production

État local au 22 août 2026 : **fonctionnel et qualifié** pour quatre routes UX8.
UX8 atteint ainsi **22/51** routes accessibles dans Studio.

La page `production` expose un inspecteur annulable à sélection explicite du module. Il exécute les
autorités canoniques en lecture seule, conserve les journaux repliés à la demande et ne transforme
jamais un prérequis absent en réussite. La seule écriture de preuve est une archive de crash créée
dans un dossier temporaire borné, inspectée puis supprimée pendant la même opération.

## Routes couvertes

<a id="orf-build-020"></a>
- **ORF-BUILD-020 — Documentation générée** : `ForgeDocumentationGenerator` reconstruit les six
  catalogues en mémoire avec `checkOnly: true`, compare leur contenu suivi et affiche chaque écart
  sans écrire dans `docs/generated`.

<a id="orf-build-021"></a>
- **ORF-BUILD-021 — Task manifests** : `OperationalReadinessValidator` contrôle schéma, portée,
  chemins interdits, acceptations, responsable et dépendances des manifestes persistés. Aucun agent
  n'est créé ou exécuté par cette surface.

<a id="orf-build-024"></a>
- **ORF-BUILD-024 — Release channels** : canal courant, transition suivante, cibles, plateformes,
  signature et politique delta sont affichés. `ModuleReleasePromotionService.Preview` refuse
  honnêtement la promotion lorsque l'adaptateur de publication distante n'est pas configuré ; aucun
  bundle, signature, envoi ou changement de canal n'est tenté.

<a id="orf-build-025"></a>
- **ORF-BUILD-025 — Crash artifacts** : `CrashArtifactBuilder` crée réellement une archive ZIP
  temporaire, assainit un secret témoin, vérifie quotas, version et empreinte package, puis la
  supprime. Aucun adaptateur de dump, processus ou mémoire native n'est collecté.

## Annulation, journaux et préservation

- la sélection du module est explicite et les quatre résultats restent navigables au clavier ;
- un `CancellationToken` est contrôlé entre les quatre autorités et la preuve d'annulation ne laisse
  aucune tâche ni ressource temporaire active ;
- les journaux techniques sont repliés par défaut et ne révèlent ni chemin absolu ni UUID ;
- projet, historique, révision, journal, dirty, zone active et sélection restent identiques ;
- le smoke compte les dossiers `openrpg-forge-studio-ux8e-*` avant et après la preuve et exige leur
  suppression complète.

## Preuves exécutées

- moteur lancé : exécutable officiel **Godot 4.7.1 Mono**, avec l'assembly Studio reconstruite ;
- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **30/30** — Domain 14, ModuleFormat 16 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX8_E_PASS` avec `routes`, `selection`,
  `documentation`, `tasks`, `release_channels`, `crash_artifacts`, `cancel`, `logs`, `navigation`,
  `resources_released`, `ui`, `human`, `source_unchanged` et `surface_restored` à `true` ;
- capture Forward+ **1920×1080** sur Forgotten Forest réel, inspectée : quatre cartes et quatre
  résultats lisibles, détail de promotion bloquée, trois actions, statut et frontière honnête
  visibles sans clipping ;
- portail incrémental complet : **PASS en 531,26 s** — build 1,40 s, tests Domain 2,39 s et
  ModuleFormat 4,58 s, smoke 410,55 s, capture 7,92 s, matrice 0,83 s, génération du graphe 51,47 s
  et contrôle complet 52,04 s ;
- matrice Studio : **602 `functional` / 115 `planned`**, dont UX8 **22/51** ;
- graphe Roslyn : **11 076 nœuds / 84 866 relations / 0 diagnostic**, frais et déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux8-e-production-evidence-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-e-production-evidence.summary.json`.

## Limites honnêtes

La documentation est comparée mais jamais régénérée par cette surface. Les task manifests sont
validés mais aucun agent n'est distribué. Le canal de release est prévisualisé sans publisher,
signature, upload ou publication. L'archive de crash ne contient aucun dump natif et n'est jamais
conservée : elle prouve uniquement la politique d'assainissement et de quotas dans un isolat local.

Le portail Studio demeure un incrément local avec `full_gate=false`; il ne remplace ni
`scripts/Verify.ps1` global ni une qualification de distribution signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~OperationalReadinessProfileTests|' +
  'FullyQualifiedName~ModuleReleaseConfigurationTests|' +
  'FullyQualifiedName~DistributionComplianceValidatorTests|' +
  'FullyQualifiedName~ForgeDocumentationGeneratorTests|' +
  'FullyQualifiedName~CrashArtifactBuilderTests|' +
  'FullyQualifiedName~ModuleDistributionPipelineTests|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page production `
  -Evidence ux8-e-production-evidence `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-e-production-evidence-forward-plus-fhd.png' `
  -CheckGraph
```
