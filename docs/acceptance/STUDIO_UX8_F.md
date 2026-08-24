# Acceptation Studio — UX8-F Gouvernance de la chaîne d’approvisionnement

État local au 22 août 2026 : **fonctionnel et qualifié** pour quatre routes UX8.
UX8 atteint ainsi **26/51** routes accessibles dans Studio.

La page `production` expose les dépendances, notices, crédits et frontières IP du module réellement
ouvert. Forgotten Forest ne déclare actuellement aucune dépendance et ne possède pas de registre de
gouvernance d’asset : ces deux états vides restent affichés tels quels, sans synthèse produit. Une
approbation riche est créée uniquement dans la session de preuve isolée afin d’exercer la transaction
complète sans modifier la source.

## Routes couvertes

<a id="orf-build-022"></a>
- **ORF-BUILD-022 — Dependency approval** : un formulaire humain édite nom, version sémantique,
  licence SPDX, responsable, justification, cadence, plan de retrait, source, cibles, état, risque,
  rôles, quorum et référence d’audit. `DistributionComplianceValidator` puis
  `SetModuleDistributionHandler` produisent le dry-run canonique lié à la révision et au brouillon ;
  l’application repasse la validation, rejoint l’historique et supporte undo/redo puis save/reopen.

<a id="orf-build-023"></a>
- **ORF-BUILD-023 — Third-party notices** : `DistributionNoticeBuilder` reconstruit deux fois la
  notice Shipping depuis le candidat approuvé, exige une égalité octet par octet et présente son
  SHA-256 avec le patch et l’inverse. Aucun package n’est construit ou publié.

<a id="orf-cont-030"></a>
- **ORF-CONT-030 — Credits and notices generation** : la notice canonique agrège les provenances du
  projet, les tables, packages, sources, assets et dépendances. L’inspecteur évalue le vrai module en
  lecture seule ; la fixture de preuve vérifie les sections attendues sans écrire de NOTICE source.

<a id="orf-cont-031"></a>
- **ORF-CONT-031 — IP boundary enforcement** : `AssetGovernanceStore` et le validateur canonique
  contrôlent licence, permission, frontière IP et compatibilité de cible. L’absence réelle du registre
  est annoncée comme telle ; un enregistrement gouverné n’existe que dans l’isolat de smoke.

## Transaction et préservation

- sélection de dépendance explicite, avec action distincte pour créer une nouvelle approbation ;
- empreintes binaires typées et préfixées par longueur pour la source, le brouillon et le candidat ;
- approbation liée à la révision, refus des sources ou approbations devenues obsolètes ;
- dry-run puis re-dry-run avant l’application de `set_module_distribution` ;
- patch et inverse canoniques `module/distribution`, audit non appliqué pendant le preview ;
- undo, redo, sauvegarde puis réouverture du projet isolé ;
- profils de cible, release, launcher, autres dépendances, module et voisins projet préservés ;
- projet, historique, révision, journal, dirty, sélection et approbation de la session source inchangés ;
- dossier temporaire `openrpg-forge-studio-ux8f-*` supprimé et ressource UI libérée.

## Preuves exécutées

- moteur lancé : exécutable officiel **Godot 4.7.1 Mono**, backend Vulkan Forward+ sur AMD Radeon
  RX 7800 XT ;
- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **20/20** — Domain 10, Application 2, ModuleFormat 8 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX8_F_PASS` avec routes, sélection,
  approbation, notices, crédits, frontière IP, annulation, journaux, dry-run, commande, gardes stale,
  undo/redo, persistance, voisins, navigation, libération, UI, surface humaine, source inchangée et
  restauration de surface à `true` ;
- capture Forward+ **1920×1080** sur Forgotten Forest réel, inspectée : formulaire complet, aperçu
  sec, patch/inverse, empreinte de notice et cinq actions visibles sans clipping ni UUID ;
- portail incrémental complet : **PASS en 534,72 s** — build 1,22 s, tests Domain 1,62 s,
  Application 2,60 s et ModuleFormat 4,21 s, smoke 410,37 s, capture 8,18 s, matrice 0,87 s,
  génération du graphe 52,66 s et contrôle complet 52,93 s ;
- matrice Studio après promotion : **606 `functional` / 111 `planned`**, dont UX8 **26/51** ;
- graphe Roslyn régénéré, frais, entièrement déterministe et sans diagnostic.

Artefacts :

- `artifacts/verification/studio-increment/ux8-f-supply-chain-evidence-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-f-supply-chain-evidence.summary.json`.

## Limites honnêtes

Studio enregistre la gouvernance du module et génère les notices en mémoire. Il ne télécharge,
n’audite ni ne publie aucune bibliothèque ; il n’écrit pas le registre d’assets, ne construit pas de
package, ne signe rien et n’envoie rien sur le réseau. Une référence d’audit est une donnée authorée,
pas la preuve qu’un audit externe a été exécuté.

Le portail Studio demeure un incrément local avec `full_gate=false`; il ne remplace ni
`scripts/Verify.ps1` global ni une qualification de distribution signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~DistributionComplianceValidatorTests|' +
  'FullyQualifiedName~AssetGovernanceTests|' +
  'FullyQualifiedName~SetModuleDistributionHandlerTests|' +
  'FullyQualifiedName~ShippingExport_EmitsCanonicalNoticeAndApprovedDependencyManifest|' +
  'FullyQualifiedName~R050_NoticeAggregatesDataTablesPackagesSourcesAndAssetsDeterministically|' +
  'FullyQualifiedName~R050_ValidatorRejectsNoticeDivergenceFromCanonicalProject|' +
  'FullyQualifiedName~ModuleManifestValidatorTests|' +
  'FullyQualifiedName~AssetGovernanceStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page production `
  -Evidence ux8-f-supply-chain-evidence `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-f-supply-chain-evidence-forward-plus-fhd.png' `
  -CheckGraph
```
