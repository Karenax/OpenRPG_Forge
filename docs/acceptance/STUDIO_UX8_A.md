# Acceptation Studio — UX8-A Build réel et explicable

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX8.
UX8 atteint ainsi **5/51** routes accessibles dans Studio.

La page `build` expose une surface pleine taille à sélection explicite du module. Le Studio calcule
le graphe canonique, recopie le workspace dans un isolat, puis démarre réellement
`dotnet Forge.Cli.dll build` deux fois. Il compare les paquets octet par octet, relit l'état
incrémental et rend chaque verdict navigable. La source ouverte, son historique et Git restent
inchangés.

## Routes couvertes

<a id="orf-shell-019"></a>
- **ORF-SHELL-019 — Build du module** : un vrai paquet `.orpmod` est produit dans le dossier de
  sortie temporaire. L'utilisateur peut ouvrir ce dossier tant que la surface reste ouverte.

<a id="orf-ee-004"></a>
- **ORF-EE-004 — Optimisation Build Module** : `WorkspaceBuildGraphBuilder` et le matérialiseur
  parallèle exposent ressources, dépendances, tâches, pic de parallélisme, durée et empreinte
  d'ordre stable.

<a id="orf-build-003"></a>
- **ORF-BUILD-003 — Build CLI** : un processus enfant `dotnet` charge le vrai `Forge.Cli.dll`,
  exécute `build`, renvoie son JSON structuré et conserve stdout/stderr dans des journaux affichés
  uniquement à la demande.

<a id="orf-build-004"></a>
- **ORF-BUILD-004 — Build déterministe** : chaque invocation active la vérification reproductible
  du CLI ; les deux sorties distinctes sont aussi comparées octet par octet et par SHA-256.

<a id="orf-build-005"></a>
- **ORF-BUILD-005 — Build incrémental** : le premier passage reconstruit le graphe complet, le
  second reconstruit zéro ressource, réutilise toutes les ressources et relit
  `.forge/build-graph.json`.

## Isolation, annulation et surface humaine

- `ProjectSession.Create` écrit une copie sous `%TEMP%\openrpg-forge-studio-ux8-a`, tandis que les
  paquets sont placés dans un dossier frère du workspace copié pour ne jamais polluer son graphe.
- L'annulation démarre réellement Forge.Cli, annule l'attente, tue tout l'arbre de processus et
  exige la suppression de l'isolat.
- Projet, historique, révision, journal, dirty, undo/redo, zone active, sélection, fichier projet et
  empreinte du workspace source restent identiques.
- Les cinq cartes expliquent le périmètre ; les cinq résultats ouvrent un détail humain. Le journal
  brut reste replié par défaut et son affichage remplace les UUID éventuels par un libellé protégé.
- La sortie d'un run humain reste disponible seulement jusqu'à la fermeture de la surface ; le
  smoke et la capture libèrent explicitement toute ressource temporaire.

## Preuves exécutées

- build `Forge.Studio` Debug, incluant `Forge.Cli` : **0 avertissement, 0 erreur** ;
- portail incrémental final : **624,28 s**, avec build Studio en 14,35 s, tests Domain en
  1,49 s, Application en 2,35 s, ModuleFormat en 8,40 s, smoke en 442,82 s, capture en
  49,25 s, matrice en 0,84 s, génération du graphe en 52,75 s et contrôle complet en 51,97 s ;
- tests .NET ciblés : **61/61** — Domain 5, Application 6, ModuleFormat 50 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX8_A_PASS` avec `routes`, `selection`,
  `package`, `optimized`, `cli`, `deterministic`, `incremental`, `cancel`, `navigation`, `isolated`,
  `artifacts`, `ui`, `resources_released`, `source_unchanged` et `surface_restored` à `true` ;
- capture Forward+ **1920×1080** sur Forgotten Forest réel, inspectée après correction de l'état du
  bouton `Ouvrir la sortie` : cinq cartes, cinq résultats, détail incrémental, journal à la demande,
  statut et quatre actions visibles sans clipping ;
- matrice Studio : **585** routes `functional` et **132** `planned`, dont UX8 à **5/51** ;
- graphe Roslyn : **11 032** nœuds, **84 434** relations et **0** diagnostic, puis contrôle
  déterministe complet réussi ;
- après smoke et capture : **0** isolat UX8-A restant sous `%TEMP%`.

Artefacts :

- `artifacts/verification/studio-increment/ux8-a-build-foundation-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-a-build-foundation.summary.json`.

## Limites honnêtes

Le paquet est une sortie locale de développement : ce lot ne signe, ne publie, ne téléverse et
n'installe rien. Il ne qualifie ni les canaux de release, ni le delta patching, ni le lanceur, ni
la distribution publique. Le binaire CLI est celui compilé depuis le même checkout grâce à une
dépendance de build sans référence d'assembly ; l'empaquetage autonome de ce binaire avec une
distribution finale relève des lots UX8 suivants.

Le portail Studio demeure un incrément local avec `full_gate=false`; il ne remplace ni
`scripts/Verify.ps1` global ni une qualification de distribution.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~WorkspaceBuildProfile|' +
  'FullyQualifiedName~WorkspaceBuildProfileCommandTests|' +
  'FullyQualifiedName~WorkspaceBuildGraphTests|' +
  'FullyQualifiedName~DeterministicBuildTaskSchedulerTests|' +
  'FullyQualifiedName~ModulePackageTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page build `
  -Evidence ux8-a-build-foundation `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-a-build-foundation-forward-plus-fhd.png' `
  -CheckGraph
```
