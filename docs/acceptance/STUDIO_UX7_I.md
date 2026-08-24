# Acceptation Studio — UX7-I Observabilité Tracy et OpenTelemetry

État local au 22 août 2026 : **fonctionnel et qualifié** pour la dernière route UX7.
UX7 atteint ainsi **43/43** routes accessibles dans Studio.

La page `quality` expose une surface pleine taille à sélection explicite du module. Elle permet
d'authorer le profil canonique d'observabilité, de choisir un backend et de lancer une capture
annulable dans une copie temporaire du workspace. Le fallback OpenTelemetry produit réellement une
trace JSON confinée couvrant Editor, Runtime, Serveur et ForgeScript. Tracy natif reste honnêtement
conditionné à un Godot instrumenté et à un serveur de capture compatible : aucun artefact générique
n'est renommé en capture Tracy.

## Route couverte

<a id="orf-ee-029"></a>
- **ORF-EE-029 — Profiler Tracy** : `ObservabilitySettings` persiste activation, compatibilités
  Tracy/OpenTelemetry, durée maximale et inclusion ForgeScript. `SetRuntimeExtensionsHandler`
  fournit dry-run, révision, historique, undo/redo et round-trip. Le run OpenTelemetry construit un
  artefact réel sous `.forge/quality/observability/open-telemetry-trace.json` dans l'isolat, tandis
  que le choix Tracy natif renvoie le diagnostic stable `OBSERVABILITY_TRACY_RUNTIME_MISSING` si le
  moteur officiel n'est pas instrumenté.

## Contrat, capture et isolation

- Une observabilité active exige au moins un backend autorisé ; durées, spans scripts, backend et
  surfaces manquantes produisent des diagnostics stables avec chemin.
- La capture OpenTelemetry contient des spans structurés pour Editor, Runtime et Serveur, inclut le
  span ForgeScript demandé et calcule une empreinte déterministe du résultat.
- L'annulation précoce ne produit aucun artefact et libère sa racine temporaire.
- Projet, historique, révision, journal, dirty, undo/redo, zone active, sélection et approbation de
  la session source restent identiques avant et après les preuves.
- Les résultats Editor, Runtime, Serveur, artefact, diagnostic Tracy et annulation sont navigables
  dans la surface humaine, sans UUID exposé.
- La barre transactionnelle complète reste visible en Forward+ 1920×1080 après la passe corrective
  de layout.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **14/14** — Domain 11, ModuleFormat 3 ;
- smoke Godot global : marker `OPENRPG_FORGE_STUDIO_UX7_I_PASS`, avec transaction UI, capture,
  diagnostic Tracy, annulation, navigation, confinement, cleanup et source intacte à `true` ;
- capture Forward+ **1920×1080** sur Crypte des Cendres réel : sélection, configuration, aperçu sec,
  six résultats navigables, limite Tracy et sept actions transactionnelles lisibles sans clipping ;
- portail incrémental : **420,22 s** — build 1,10 s, tests 1,44/2,53 s, smoke 400,29 s, capture
  13,86 s et matrice 0,95 s ;
- matrice après promotion : **717 lignes**, **36 pages**, **624 fonctionnelles / 93 planifiées** ;
- graphe Roslyn régénéré en **51,56 s** puis contrôlé intégralement en **52,90 s** :
  **11 194 nœuds**, **86 220 relations**, **0 diagnostic**.

Artefacts :

- `artifacts/verification/studio-increment/ux7-i-observability-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux7-i-observability.summary.json` ;
- `artifacts/verification/studio-shell/studio-interactive.log`.

## Limite native explicite

La qualification porte sur le parcours d'observabilité demandé : configuration Tracy optionnelle,
fallback OpenTelemetry réellement exécuté et diagnostic d'environnement natif. Elle ne prétend pas
avoir construit Godot avec `profiler=tracy`, connecté un capture server Tracy, mesuré un GPU
constructeur ni qualifié une distribution. Le portail Studio demeure local avec `full_gate=false`
et ne remplace pas `scripts/Verify.ps1`.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~PostLaunchCapabilityResolverTests|' +
  'FullyQualifiedName~WorkspaceObservabilityCaptureServiceTests|' +
  'FullyQualifiedName~WorkspacePerformanceLabServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page quality `
  -Evidence ux7-i-observability `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux7-i-observability-forward-plus-fhd.png'
```
