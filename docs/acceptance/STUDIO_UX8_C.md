# Acceptation Studio — UX8-C Merge et gouvernance du dépôt

État local au 22 août 2026 : **fonctionnel et qualifié** pour quatre routes UX8.
UX8 atteint ainsi **14/51** routes accessibles dans Studio.

La page `build` expose un troisième workbench à sélection explicite. Le premier onglet calcule un
merge assisté en mémoire sur deux projections du module réel ; le second édite le profil canonique
de gouvernance du workspace par aperçu sec puis commande transactionnelle. Aucun processus agent,
runner CI distant, shell arbitraire ou changement Git n'est déclenché.

## Routes couvertes

<a id="orf-build-012"></a>
- **ORF-BUILD-012 — Merge assisté** : `DataTableThreeWayMerger` et
  `UuidGraphThreeWayMerger` produisent un plan inspectable pour les listes et graphes. Les politiques
  manuel, notre variante, variante reçue et conservation des deux sont explicites ; cette dernière
  crée des identités déterministes et remappe les relations sans écrire le projet ouvert.

<a id="orf-build-013"></a>
- **ORF-BUILD-013 — Worktrees et agents** : préfixe de branche, manifeste de propriété, chemins du
  workspace et quatre contrôles d'intégration sont exposés par libellés humains. Le dépôt local est
  inspecté lorsqu'il est disponible, mais Studio ne crée ni worktree, ni branche, ni agent.

<a id="orf-build-014"></a>
- **ORF-BUILD-014 — CI** : workflow versionné et sept exigences — schémas, code managé, code natif,
  ForgeScript, tests, module témoin et licences — sont validés par le profil et ses preuves locales.
  La surface ne contacte aucun service distant et ne prétend pas exécuter la CI.

<a id="orf-build-015"></a>
- **ORF-BUILD-015 — Fixtures de référence** : le catalogue versionné présente les fixtures Tiny,
  Vertical, Stress, Malicious et Migration ainsi que leur verdict attendu. Les chemins sont bornés
  au workspace et les preuves sont validées par l'autorité ModuleFormat.

## Transaction, isolation et surface humaine

- le merge reste un résultat en mémoire et ne possède volontairement aucune action `Appliquer` ;
- le profil de gouvernance est la seule écriture : `SetWorkspaceBuildProfileHandler` effectue le
  dry-run, vérifie la révision et les empreintes source/brouillon, puis alimente historique,
  undo/redo et sauvegarde ;
- la preuve transactionnelle opère sous `%TEMP%\openrpg-forge-studio-ux8-c`, dans une copie du
  module et du profil de build ; la source, son historique, sa révision, son journal, son état dirty,
  sa sélection et son approbation restent inchangés ;
- les UUID sont remplacés par des libellés protégés et les quatre cartes de route restent visibles
  à FHD ; deux captures complémentaires montrent le plan de merge puis la gouvernance et ses cinq
  actions transactionnelles ;
- tout isolat est supprimé après smoke et capture.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **23/23** — Domain 9, Application 6, ModuleFormat 8 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX8_C_PASS` avec `routes`, `selection`,
  `merge_conflict`, `preserve_both`, `worktrees`, `ci`, `fixtures`, `dry_run`, `stale_guard`,
  `undo_redo`, `persistence`, `neighbors`, `ui`, `human`, `resources_released`,
  `source_unchanged` et `surface_restored` à `true` ;
- captures Forward+ **1920×1080** sur Crypte des Cendres réel, inspectées : la vue Merge montre les
  deux variantes, les conflits liste/graphe et le remappage ; la vue Gouvernance montre le profil,
  les sept contrôles CI, les cinq fixtures, le verdict sec et les cinq actions sans clipping ;
- portail incrémental complet : **PASS en 538,11 s** — build 1,31 s, tests Domain 1,52 s,
  Application 2,31 s et ModuleFormat 4,00 s, smoke 413,05 s, capture 9,17 s, matrice 0,85 s,
  génération du graphe 53,50 s et contrôle complet 52,33 s ;
- matrice Studio : **594 `functional` / 123 `planned`**, dont UX8 **14/51** ;
- graphe Roslyn : **11 056 nœuds / 84 680 relations / 0 diagnostic**, frais et déterministe ;
- après smoke et captures : **0** isolat UX8-C restant sous `%TEMP%`.

Artefacts :

- `artifacts/verification/studio-increment/ux8-c-assisted-merge-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-c-build-governance-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-c-build-governance.summary.json`.

## Limites honnêtes

Le merge est un plan d'aide à la décision, pas un merge Git ni une écriture multi-fichiers. La
commande persistante ne modifie que le profil de gouvernance du workspace. Les worktrees, agents et
jobs CI décrits restent des conventions vérifiées ; ils ne sont ni créés ni exécutés par Studio.
Les fixtures prouvent la résolution locale attendue, pas une publication, une signature ou un canal
de distribution.

Le portail Studio demeure un incrément local avec `full_gate=false`; il ne remplace ni
`scripts/Verify.ps1` global ni une qualification de distribution.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~UuidGraphThreeWayMergerTests|' +
  'FullyQualifiedName~ContentWorkspaceTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileValidatorTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileCommandTests|' +
  'FullyQualifiedName~BuildGovernanceEvidenceTests|' +
  'FullyQualifiedName~WorkspaceBuildProfileStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page build `
  -Evidence ux8-c-build-governance `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-c-build-governance-forward-plus-fhd.png' `
  -CheckGraph
```
