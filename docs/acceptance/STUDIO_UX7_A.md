# Acceptation Studio — UX7-A Validation canonique

État local au 21 août 2026 : **fonctionnel et qualifié** pour 10 routes UX7.

La page `validation` expose un atelier unique, lisible par un humain, qui agrège les autorités
existantes sans les recopier : validation canonique du workspace, préparation de zone, validation
audio, qualité de localisation, conformité de distribution et diagnostics Creator cliquables.
La zone est toujours sélectionnée explicitement. Chaque message conserve son code et son chemin
canonique en mémoire, mais l’interface affiche une cible et un texte français sans UUID.

## Routes couvertes

<a id="orf-qa-001"></a>
- **ORF-QA-001 — Validation de schéma** : les documents JSON du workspace sont relus avec leurs versions et les erreurs sont localisées.
<a id="orf-qa-002"></a>
- **ORF-QA-002 — Validation de références** : les identités et références typées sont résolues par le validateur canonique.
<a id="orf-qa-003"></a>
- **ORF-QA-003 — Validation de graphes** : les graphes de dialogues et quêtes signalent cibles absentes et nœuds incohérents.
<a id="orf-qa-004"></a>
- **ORF-QA-004 — Validation géométrique** : volumes, échelles, raccords et placements hors limites sont diagnostiqués.
<a id="orf-qa-005"></a>
- **ORF-QA-005 — Validation navigation** : points d’apparition, accessibilité et liens sont analysés pour les profils agents.
<a id="orf-qa-006"></a>
- **ORF-QA-006 — Validation de localisation** : conflits, traductions absentes et problèmes de qualité sont regroupés.
<a id="orf-qa-007"></a>
- **ORF-QA-007 — Validation de licence** : licences, provenance et approbations sont vérifiées pour la cible Shipping.
<a id="orf-area-037"></a>
- **ORF-AREA-037 — Validation de zone** : les neuf contrôles de préparation sont exécutés sur la zone choisie.
<a id="orf-aud-020"></a>
- **ORF-AUD-020 — Validation audio** : chemins, existence, taille et empreinte des ressources audio sont contrôlés.
<a id="orf-qa-021"></a>
- **ORF-QA-021 — Diagnostics cliquables** : une cible humaine peut être ouverte dans Studio ; les corrections disponibles exigent un aperçu lié au diagnostic courant.

## Transaction et préservation

- Le rapport est en lecture seule et porte une empreinte de la révision, de la zone et des résultats.
- Une correction n’est applicable qu’après un aperçu sec canonique.
- Une approbation restaurée après disparition du diagnostic est refusée comme périmée.
- L’application passe par `ApplyDiagnosticQuickFixHandler`, puis par l’historique canonique.
- Undo, redo, save et reopen sont prouvés sur un workspace temporaire isolé.
- Projet, historique, révision, journal, état dirty, sélection et approbation de la session source restent inchangés.
- Le workspace temporaire est supprimé et la page/surface source est restaurée.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **33/33** — Domain 12, Application 11, ModuleFormat 10 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_A_PASS`, approbations candidates et shell global PASS ;
- portail incrémental : **300,73 s** ;
- matrice Studio : **717 lignes**, 36 pages, PASS avant promotion ;
- capture Forward+ réelle **1920×1080**, inspectée après la passe corrective humaine : dix familles, détail, cible, aperçu et footer entièrement visibles, sans clipping bloquant ni UUID.

Artefacts :

- `artifacts/verification/studio-increment/ux7-a-canonical-validation.summary.json` ;
- `artifacts/verification/studio-increment/ux7-a-canonical-validation-forward-plus-fhd.png` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limites honnêtes

Cette surface valide et prépare. Elle ne lance aucun build ou packaging, n’exécute aucun runtime,
script, réseau ou playback audio, et ne transforme pas un avertissement en conformité automatique.
Une correction automatique ne couvre que les quick-fixes déjà définis par le domaine ; les autres
messages doivent être ouverts et corrigés dans leur surface d’authoring. Le portail incrémental a
`full_gate=false` : le `Verify.ps1` global n’est pas revendiqué pour ce lot.

## Reproduction

```powershell
dotnet build apps/Forge.Studio/Forge.Studio.csproj -c Debug --no-restore

.\scripts\Verify-StudioIncrement.ps1 `
  -Page validation `
  -Evidence ux7-a-canonical-validation `
  -TestProject @(
    'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj',
    'tests\Forge.Application.Tests\Forge.Application.Tests.csproj',
    'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  ) `
  -TestFilterByProject @{
    'Forge.Domain.Tests' = 'FullyQualifiedName~DistributionComplianceValidatorTests|FullyQualifiedName~ActionableDiagnosticTests|FullyQualifiedName~LocalizationCatalogTests'
    'Forge.Application.Tests' = 'FullyQualifiedName~ApplyDiagnosticQuickFixHandlerTests|FullyQualifiedName~ProjectQualityServicesTests|FullyQualifiedName~LocalizationWorkflowTests|FullyQualifiedName~WorkspaceQualityLabServiceTests|FullyQualifiedName~WorkspaceAudioValidatorTests'
    'Forge.ModuleFormat.Tests' = 'FullyQualifiedName~QualityFoundationProfileStoreTests'
  } `
  -Capture 'artifacts\verification\studio-increment\ux7-a-canonical-validation-forward-plus-fhd.png'
```
