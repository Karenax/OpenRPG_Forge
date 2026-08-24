# Acceptation Studio — UX8-K Import/export en masse

État local au 22 août 2026 : **fonctionnel et qualifié** pour la route UX8-K. La vague atteint
ainsi **41/51** routes accessibles dans Studio.

La page `export` expose un atelier humain unique pour préparer un import ou un export de contenu.
Le transfert de fichiers est planifié et exécuté par `ContentBatchTransferService`, puis la mutation
du catalogue passe exclusivement par `SetContentWorkspaceHandler`. Les deux autorités restent
coordonnées par la surface : un échec de commande restaure les fichiers, et undo/redo compensent à
la fois le projet et le transfert sans inventer une transaction distribuée.

## Route couverte

<a id="orf-build-007"></a>
- **ORF-BUILD-007 — Import/export en masse** : sélection explicite du module et du sens du
  transfert, chemins relatifs confinés, glob, extensions, stratégie de collision, provenance et
  licence SPDX. L’aperçu liste les fichiers, tailles, SHA-256, diagnostics et diff du catalogue
  avant tout consentement ou écriture.

## Sécurité, transaction et préservation

- source et destination obligatoirement relatives au workspace, distinctes et non imbriquées ;
- refus des liens symboliques/réparse points, des sorties de périmètre et des collisions interdites ;
- budget interactif borné à 32 Mio, staging avec SHA-256 et stratégie `preserve-both` déterministe ;
- consentement humain explicite après un dry-run réussi ;
- révision, empreinte complète du projet et empreinte binaire typée du brouillon approuvé vérifiées
  à nouveau au moment de l’application ;
- transfert réel puis `SetContentWorkspaceHandler`, avec rollback des fichiers si la commande échoue ;
- undo/redo coordonnés, sauvegarde atomique et réouverture du workspace temporaire ;
- packages, overrides, ressources voisines et toutes les racines hors contenu préservés ;
- projet, historique, révision, journal, état dirty, sélection et approbation source inchangés ;
- répertoire de preuve temporaire supprimé avant PASS.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **51/51** — Domain 2, Application 1 et ModuleFormat 48 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX8_K_PASS` avec route, sélection, consentement,
  import, export, collision, confinement, dry-run, commande, gardes stale, undo/redo, persistance,
  voisins, nettoyage, transaction UI, surface humaine, source inchangée et surface restaurée à
  `true` ;
- capture Forward+ **1920×1080** corrigée puis inspectée : Crypte des Cendres réel, sélecteur
  import/export, paramètres, garanties, consentement, aperçu, diff, fichiers, diagnostics et cinq
  actions visibles sans UUID ni clipping ;
- portail incrémental final : **PASS en 423,12 s** — build 1,26 s, tests 1,65 + 2,74 + 4,62 s,
  smoke 403,58 s, capture 8,41 s et matrice 0,81 s ;
- matrice Studio après promotion : **621 `functional` / 96 `planned`**, dont UX8 **41/51** ;
- graphe Roslyn final régénéré par l’outil officiel, frais, déterministe et sans diagnostic.

Artefacts :

- `artifacts/verification/studio-increment/ux8-k-bulk-transfer-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-k-bulk-transfer.summary.json`.

## Limites honnêtes

Le transfert reste local et borné au workspace. La surface ne télécharge rien, ne surveille aucun
dossier, ne synchronise aucun dépôt distant et ne publie aucun package. Elle ne promet pas une
atomicité distribuée contre une panne du processus ou du système entre les deux autorités ; elle
fournit compensation et rollback dans la session contrôlée. La preuve UI travaille uniquement dans
un workspace jetable et ne modifie pas le module source.

Le portail conserve `full_gate=false` et `graph_check=false` ; le contrôle Roslyn est exécuté
séparément après la documentation. Cette qualification locale ne remplace pas `scripts/Verify.ps1`
ni une release publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ContentWorkspaceTests|' +
  'FullyQualifiedName~SetContentWorkspaceHandlerTests|' +
  'FullyQualifiedName~ContentWorkflowTests|' +
  'FullyQualifiedName~ProjectJsonSerializerTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page export `
  -Evidence ux8-k-bulk-transfer `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts/verification/studio-increment/ux8-k-bulk-transfer-forward-plus-fhd.png'
```
