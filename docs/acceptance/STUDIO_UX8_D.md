# Acceptation Studio — UX8-D État du dépôt et décisions

État local au 22 août 2026 : **fonctionnel et qualifié** pour quatre routes UX8.
UX8 atteint ainsi **18/51** routes accessibles dans Studio.

La page `production` expose un inspecteur à sélection explicite du module. Il lit le profil de build
ouvert et remonte jusqu'au dépôt local, sans lancer Git ni générateur. Les quatre résultats sont
navigables, les preuves absentes restent annoncées comme absentes, le journal est replié par défaut
et l'inspection respecte une annulation coopérative.

## Routes couvertes

<a id="orf-build-001"></a>
- **ORF-BUILD-001 — Dépôt Git canonique** : les garanties du profil — dépôt requis, textes
  versionnables, fichiers générés explicites et assets reproductibles — sont croisées avec la
  présence locale du dépôt, du fichier d'exclusion et du guide, sans commande Git.

<a id="orf-build-002"></a>
- **ORF-BUILD-002 — Structure de projet standard** : les huit emplacements `assets`, `cache`,
  `data`, `docs`, `localization`, `packages`, `scripts` et `tests` sont affichés par libellés humains.
  La surface distingue une configuration valide d'un dossier réellement matérialisé dans le
  checkout et ne crée rien pour masquer une absence.

<a id="orf-build-018"></a>
- **ORF-BUILD-018 — Versioning sémantique** : versions Core, kits, package et schémas sont séparées,
  avec règles de compatibilité et changelog. La validation canonique interdit les valeurs non SemVer
  ou les règles incomplètes.

<a id="orf-build-019"></a>
- **ORF-BUILD-019 — Décisions d'architecture** : dossier, modèle, statuts formels, liens de tests,
  benchmarks pour décisions de performance et remplacement par décision supersédante sont affichés,
  avec inventaire local des documents Markdown en lecture seule.

## Lecture seule, annulation et surface humaine

- `WorkspaceBuildProfileValidator` et `WorkspaceBuildGovernanceValidator` restent les autorités ;
- le dépôt est trouvé depuis le workspace ouvert, le répertoire courant ou le binaire, sans chemin
  absolu ni UUID affiché ;
- l'annulation est vérifiée par `CancellationToken` entre chaque autorité et ne laisse aucune tâche
  ni écriture en attente ;
- projet, historique, révision, journal, dirty, zone active et sélection restent identiques ;
- la première capture FHD a révélé un libellé de détail réduit à une colonne puis un footer masqué ;
  les deux défauts ont été corrigés avant la capture acceptée.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **19/19** — Domain 5, Application 6, ModuleFormat 8 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX8_D_PASS` avec `routes`, `selection`, `git`,
  `layout`, `semver`, `adrs`, `cancel`, `logs`, `navigation`, `ui`, `human`, `source_unchanged` et
  `surface_restored` à `true` ;
- capture Forward+ **1920×1080** sur Crypte des Cendres réel, inspectée après deux corrections : quatre
  cartes, quatre résultats, détail SemVer, trois actions, statut et limites visibles sans clipping ;
- portail incrémental complet : **PASS en 535,79 s** — build 1,33 s, tests Domain 1,49 s,
  Application 2,32 s et ModuleFormat 3,99 s, smoke 412,90 s, capture 7,50 s, matrice 0,87 s,
  génération du graphe 52,49 s et contrôle complet 52,85 s ;
- matrice Studio : **598 `functional` / 119 `planned`**, dont UX8 **18/51** ;
- graphe Roslyn : **11 066 nœuds / 84 755 relations / 0 diagnostic**, frais et déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux8-d-repository-readiness-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-d-repository-readiness.summary.json`.

## Limites honnêtes

La surface inspecte et explique l'état local ; elle ne crée ni dépôt, dossier, version, changelog ou
ADR. Elle ne lance ni Git, ni génération documentaire, ni test global. `CONFORME` signifie que le
contrat persistant est valide et que les preuves locales requises pour la route sont disponibles ;
les emplacements standards non matérialisés restent explicitement décrits comme configurés.

Le portail Studio demeure un incrément local avec `full_gate=false`; il ne remplace ni
`scripts/Verify.ps1` global ni une qualification de distribution.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~WorkspaceBuildProfileValidatorTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileCommandTests|' +
  'FullyQualifiedName~BuildGovernanceEvidenceTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page production `
  -Evidence ux8-d-repository-readiness `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-d-repository-readiness-forward-plus-fhd.png' `
  -CheckGraph
```
