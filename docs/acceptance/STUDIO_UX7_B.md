# Acceptation Studio — UX7-B Laboratoire qualité isolé

État local au 21 août 2026 : **fonctionnel et qualifié** pour quatre routes UX7.
UX7 atteint ainsi **14/43** routes accessibles dans Studio.

La page `quality` expose un laboratoire plein écran à sélection explicite. Un run travaille dans un
workspace temporaire, produit quatre résultats navigables, accepte une annulation réelle entre les
familles et libère ensuite ses fichiers. Le profil qualité reste l'autorité canonique : aperçu sec,
approbation liée à la révision et aux empreintes, application, undo/redo et save/reopen passent par
`SetQualityFoundationHandler` et `QualityFoundationProfileStore`. Les politiques Licences et
Validation voisines sont conservées après leur normalisation canonique de persistance.

## Routes couvertes

<a id="orf-qa-008"></a>
- **ORF-QA-008 — Tests unitaires du domaine** : le harness intégré valide le profil, exerce un cas négatif et observe les diagnostics du projet sans mutation.
<a id="orf-qa-010"></a>
- **ORF-QA-010 — Tests headless runtime** : `GameplaySessionServices.ReplayHeadless` rejoue 54 frames déterministes et vérifie événements, seeds et empreintes d'état.
<a id="orf-qa-015"></a>
- **ORF-QA-015 — Tests de sauvegarde** : de vrais fichiers `.orpsave` temporaires couvrent round-trip, migration, écriture partielle, fichier absent, rollback et octets déterministes.
<a id="orf-qa-020"></a>
- **ORF-QA-020 — Logs structurés** : un échantillon JSON en mémoire conserve sévérité, catégorie, source et transaction, avec identité joueur masquée ou hachée.

## Isolation, annulation et navigation

- Le module et le profil sont copiés dans un workspace temporaire borné.
- L'annulation utilise un `CancellationToken` réel et est observée après la famille Domaine.
- Chaque résultat porte sa route, son statut, sa durée, son résumé et ses détails ; une sélection ouvre
  le détail correspondant et synchronise l'onglet de configuration.
- Le run, l'aperçu et la transaction UI prouvent séparément leur absence de mutation de la source.
- Projet, historique, révision, journal, état dirty, sélection et approbation de la session source sont
  restaurés exactement ; le workspace temporaire et ses rapports sont supprimés.
- Les identifiants techniques ne sont pas affichés dans la surface humaine.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **25/25** — Domain 5, Application 9, ModuleFormat 11 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_B_PASS`, approbations candidates et shell global PASS ;
- portail incrémental : **245,20 s** ;
- matrice Studio : **717 lignes**, 36 pages, PASS avant promotion ;
- capture réelle Forward+ **1920×1080**, inspectée après deux passes correctives : les quatre résultats,
  leur détail, le statut et les sept actions du footer sont visibles, sans clipping bloquant.

Artefacts :

- `artifacts/verification/studio-increment/ux7-b-quality-lab.summary.json` ;
- `artifacts/verification/studio-increment/ux7-b-quality-lab-forward-plus-fhd.png` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limites honnêtes

Le run intégré n'est pas le runner xUnit du portail : les 25 tests .NET restent une preuve séparée.
Studio ne lance aucun runtime Godot de jeu, acteur, réseau, script, playback ou journal joueur. Le
replay headless et le log sont en mémoire ; les sauvegardes utilisent de vrais fichiers mais dans
l'isolat supprimé. Le portail incrémental a `full_gate=false` : le `Verify.ps1` global n'est pas
revendiqué pour ce lot.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~QualityFoundationProfileTests|' +
  'FullyQualifiedName~GameplaySessionProfileTests|' +
  'FullyQualifiedName~QualityFoundation|' +
  'FullyQualifiedName~GameplaySessionServicesTests|' +
  'FullyQualifiedName~QualityFoundationProfileStoreTests|' +
  'FullyQualifiedName~RuntimeSaveStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page quality `
  -Evidence ux7-b-quality-lab `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux7-b-quality-lab-forward-plus-fhd.png'
```
