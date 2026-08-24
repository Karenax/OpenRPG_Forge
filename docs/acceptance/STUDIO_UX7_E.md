# Acceptation Studio — UX7-E Gouvernance, accessibilité et sécurité

État local au 21 août 2026 : **fonctionnel et qualifié** pour neuf routes UX7.
UX7 atteint ainsi **31/43** routes accessibles dans Studio.

La page `quality` expose un laboratoire plein écran à sélection explicite du module. Chaque exécution
travaille dans une copie temporaire bornée du workspace, rend neuf résultats navigables, conserve les
non-conformités comme verdicts lisibles et détruit l'isolat. Aucun fixture hostile n'est lancé comme jeu.

## Routes couvertes

<a id="orf-qa-022"></a>
- **ORF-QA-022 — Rapport de santé du projet** : `ProjectHealthReportService` regroupe les diagnostics,
  tests, performance, licences, migration et couverture par domaine sans transformer un contenu dégradé
  en erreur technique du runner.
<a id="orf-qa-023"></a>
- **ORF-QA-023 — Accessibilité de l'éditeur** : `AccessibilityGovernanceService` contrôle sept familles
  éditeur : clavier, labels, statuts non chromatiques, raccourcis, contraste, échelle et polices.
<a id="orf-qa-024"></a>
- **ORF-QA-024 — Accessibilité du runtime** : le même service contrôle sept familles runtime :
  sous-titres, captions, texte, contraste, input, mouvement réduit et difficulté.
<a id="orf-qa-025"></a>
- **ORF-QA-025 — Security fuzzing** : `WorkspaceGovernanceLabService` exécute 112 charges déterministes
  sur sept frontières et confine le rapport dans `.forge/quality/governance-report.json` de l'isolat.
<a id="orf-qa-026"></a>
- **ORF-QA-026 — Malicious module fixtures** : le catalogue et ses preuves versionnées couvrent sept
  familles ; trois sondes hostiles en mémoire vérifient tableau surdimensionné, injection et asset invalide.
<a id="orf-qa-029"></a>
- **ORF-QA-029 — Coverage Aurora-parity** : les 717 lignes du tracker sont relues dans l’isolat ; les
  identifiants, statuts gouvernés et preuves obligatoires des routes `Tested` sont vérifiés et navigables.
<a id="orf-qa-030"></a>
- **ORF-QA-030 — Acceptance gates** : les 717 affectations de la matrice, les gates configurées G0–G13
  et le programme P0 exigeant audit, tests ciblés, documentation, preuve et gate globale sont inspectés.
<a id="orf-mp-036"></a>
- **ORF-MP-036 — Tests réseau déterministes** : `DeterministicNetworkTestRunner` simule trois clients,
  latence, perte, duplication, réordonnancement, trois replays et six cycles puis vérifie la convergence
  de l’état autoritaire et une empreinte stable.
<a id="orf-qa-027"></a>
- **ORF-QA-027 — Soak tests** : `DeterministicSoakTestService` accepte une campagne positive et rejette
  une campagne négative sur durée, mémoire, handles, santé base, latence p95 et joueurs virtuels.

## Isolation, annulation et navigation

- La sélection est liée à une empreinte SHA-256 du projet, du workspace et des sources d'autorité des
  fixtures, preuves, tracker et gates.
- L'annulation est exercée après le rapport de santé ; les analyses suivantes ne sont pas produites et la
  copie temporaire est libérée.
- Deux runs complets rendent la même empreinte fonctionnelle. Les 112 charges, sept frontières, sept
  familles de fixtures, 717 routes, 717 affectations, replays réseau et échantillons soak sont comptés.
- Projet, historique, révision, journal, dirty, undo/redo, zone active, sélection, fichiers source et
  approbations restent identiques. Le rapport de gouvernance est résolu sous la racine temporaire.
- Les workbenches UX7-B à UX7-E sont mutuellement exclusifs et le smoke restaure page, surface et
  approbations préexistantes.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **42/42** — Network 9, Domain 16, Application 6, ModuleFormat 11 ;
- audit Python du tracker : **5/5** ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_E_PASS` avec tous les sous-invariants à `true` ;
- approbations candidates, evidence et shell global : PASS ;
- portail incrémental final : **288,36 s** — build 1,14 s, tests 2,17/1,47/2,51/3,95 s,
  smoke 265,37 s, capture 10,85 s et matrice 0,83 s ;
- matrice avant promotion : **717 lignes**, **36 pages**, 560 fonctionnelles / 157 planifiées, PASS ;
- capture Forward+ **1920×1080** sur Crypte des Cendres réel : module, neuf cartes, neuf résultats,
  campagne soak, couverture, gates, détail défilable et actions entièrement lisibles, sans clipping.

Artefacts :

- `artifacts/verification/studio-increment/ux7-e-governance-lab.summary.json` ;
- `artifacts/verification/studio-increment/ux7-e-governance-lab-forward-plus-fhd.png` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limites honnêtes

Le rapport de santé décrit un verdict de contenu : son éventuelle non-conformité n'est pas un crash du
runner. Les contrôles d'accessibilité qualifient les politiques et configurations ; ils ne certifient pas
un matériel ou un logiciel d'assistance réel. Le fuzzing utilise des charges bornées et déterministes,
sans attaquant ni réseau réel. Les fixtures malveillantes sont lues et sondées en mémoire ; aucune n'est
lancée comme module jouable. Le réseau déterministe est un harness en mémoire sans socket ni pair distant.
Le soak évalue des échantillons simulés sans attente murale de 60 minutes ni charge matérielle réelle ;
le profil auteur dormant de Crypte des Cendres reste inchangé. Le portail a `full_gate=false` et ne
revendique ni `Verify.ps1` global ni contrôle du graphe.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~NetworkAssuranceProfileTests|' +
  'FullyQualifiedName~ProductionNetworkTests|' +
  'FullyQualifiedName~PostLaunchCapabilityResolverTests|' +
  'FullyQualifiedName~ModuleRuntimeExtensionProfileTests|' +
  'FullyQualifiedName~QualityAssuranceProfileTests|' +
  'FullyQualifiedName~PostLaunchRuntimeServiceTests|' +
  'FullyQualifiedName~ProjectHealthReportServiceTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests|' +
  'FullyQualifiedName~WorkspaceGovernanceLabServiceTests|' +
  'FullyQualifiedName~WorkspaceQualityAcceptanceEvidenceValidatorTests|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page quality `
  -Evidence ux7-e-governance-lab `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux7-e-governance-lab-forward-plus-fhd.png'

python -m pytest tests\test_aurora_tracker.py -q
```
