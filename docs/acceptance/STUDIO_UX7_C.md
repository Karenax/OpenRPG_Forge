# Acceptation Studio — UX7-C Suites qualité spécialisées

État local au 21 août 2026 : **fonctionnel et qualifié** pour cinq routes UX7.
UX7 atteint ainsi **19/43** routes accessibles dans Studio.

La page `quality` expose une surface plein écran à sélection explicite du module. Elle lance cinq
suites dans une copie temporaire bornée, accepte une annulation réelle entre chaque cas et chaque
famille, puis rend les verdicts navigables. La réussite technique du runner est distincte du verdict
du contenu : un dialogue ou une quête à corriger reste visible sans transformer un run terminé en
faux succès fonctionnel.

## Routes couvertes

<a id="orf-qa-009"></a>
- **ORF-QA-009 — Tests ForgeScript** : les sources `.forge` réelles sont compilées et exécutées par
  `ForgeScriptSandboxTestService` avec les quotas canoniques et un monde mock gouverné.
<a id="orf-qa-011"></a>
- **ORF-QA-011 — Tests de dialogue** : `NarrativeQualityService.TestDialogue` couvre nœuds, choix,
  branches, conditions et raisons d’arrêt déterministes.
<a id="orf-qa-012"></a>
- **ORF-QA-012 — Tests de quête** : `NarrativeQualityService.TestQuest` couvre états, transitions,
  portées joueur/groupe/monde et égalité après reprise de sauvegarde.
<a id="orf-qa-013"></a>
- **ORF-QA-013 — Tests de rencontre** : `EncounterQualityService.Simulate` exécute les profils et
  seeds canoniques, puis vérifie sélection, navigation et nettoyage.
<a id="orf-qa-014"></a>
- **ORF-QA-014 — Tests réseau** : `AdverseNetworkQualityService.Run` couvre latence, perte,
  convergence, reconnexion, contenu incompatible et tentative d’autorité interdite.

## Isolation, annulation et navigation

- Le projet et ses scripts sont copiés dans un workspace temporaire ; le rapport JSON est écrit
  uniquement dans cet isolat puis supprimé avec lui.
- La sélection est liée à une empreinte SHA-256 du projet sérialisé et de chaque source `.forge` ;
  un changement de source invalide le run.
- Le `CancellationToken` est contrôlé entre chaque script, dialogue, quête et famille ; le smoke
  annule après ForgeScript et vérifie la libération de l’isolat.
- Les cinq résultats conservent route, verdict, nombres de cas, résumé, détails et empreinte
  déterministe ; le smoke visite réellement les cinq entrées de la liste.
- Projet, historique, révision, journal, état dirty, undo/redo, zone active et sélection de la
  session source restent identiques ; aucun identifiant UUID n’est affiché dans la surface humaine.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **13/13** — Scripting 2, Application 5, Network 1, ModuleFormat 5 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_C_PASS` avec cinq routes, annulation,
  navigation, déterminisme, isolation, nettoyage, source et surface restaurées ;
- approbations candidates et shell global : PASS ;
- portail incrémental : **284,70 s** ;
- matrice Studio avant promotion : **717 lignes**, **36 pages**, 552 fonctionnelles / 165 planifiées,
  PASS ;
- capture Forward+ **1920×1080** sur Forgotten Forest réel : 2 scripts, 1 dialogue, 1 quête,
  36 simulations de rencontre et 9 scénarios réseau ; cinq résultats et actions visibles, sans
  clipping bloquant.

Artefacts :

- `artifacts/verification/studio-increment/ux7-c-quality-suites.summary.json` ;
- `artifacts/verification/studio-increment/ux7-c-quality-suites-forward-plus-fhd.png` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limites honnêtes

ForgeScript s’exécute dans un monde mock, pas dans le monde de jeu. Les dialogues et quêtes sont des
parcours de graphes en mémoire. La rencontre sélectionne des entités et vérifie navigation/nettoyage,
mais n’instancie aucun acteur moteur. Le réseau utilise un harnais déterministe en mémoire : aucun
socket, pair distant ni serveur externe n’est ouvert. Le portail a `full_gate=false` et ne revendique
ni `Verify.ps1` global ni contrôle de graphe pour cet incrément.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Scripting.Tests\Forge.Scripting.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptSandboxTestServiceTests|' +
  'FullyQualifiedName~ProjectQualityServicesTests|' +
  'FullyQualifiedName~AdverseNetworkQualityServiceTests|' +
  'FullyQualifiedName~WorkspaceQualityLabServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page quality `
  -Evidence ux7-c-quality-suites `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux7-c-quality-suites-forward-plus-fhd.png'
```
