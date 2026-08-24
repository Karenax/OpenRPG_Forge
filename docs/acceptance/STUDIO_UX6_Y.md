# Studio UX6-Y — météo runtime et streaming de zone

Qualification Studio des routes **ORF-GAME-018** et **ORF-EE-023** sur la page `runtime`.
Cette preuve complète les surfaces d’auteur voisines sans les dupliquer : UX2‑ENV‑C reste
propriétaire des états météo et UX1‑C de la présentation de chargement.

## ORF-GAME-018

La surface « Météo runtime » permet, après sélection explicite d’une zone, de choisir l’état initial
par libellé humain et d’éditer l’autorité serveur, le seed déterministe, la persistance et la
réplication. Elle reconstruit uniquement `AreaEnvironmentProfile.Weather` en conservant les états,
transitions, ressources, effets, vent, eau, réverbération, audio, rendu et métadonnées d’auteur.

L’aperçu exécute `WeatherRuntimeStateMachine`, vérifie le refus sans autorité et construit l’événement
typé `weather.changed` via `WeatherForgeScriptDispatchService`. Il n’envoie aucun paquet et
n’exécute aucun ForgeScript. La réplication signée reste qualifiée par son test Network dédié.

## ORF-EE-023

La surface « Streaming de zone » édite `AreaWorldProfile.Loading` : asynchronisme, préchargement des
dépendances, propagation de l’annulation, budget cache, concurrence, rayon de chunks, cible de temps
et ressources relatives sûres. Le terrain, les biomes, les features, la navigation volumétrique,
les routes aériennes et la présentation UX1‑C restent inchangés.

L’aperçu exécute `AreaStreamingCoordinator` sur un chargeur mémoire déterministe : dépendances,
progression, réutilisation du cache, politique d’annulation et `AreaStreamingBenchmarkGate` sont
réellement traversés. Aucune ressource ni scène Godot n’est chargée par Studio.

## Transaction et préservation

- deux commandes explicites : `SetAreaEnvironment` puis `SetAreaWorldProfile` ;
- deux dry-runs purs avant mutation ;
- garde sur révision, empreinte SHA-256 du projet et empreinte binaire typée du brouillon ;
- rollback de la première commande si la seconde est refusée ;
- undo/redo dans l’ordre inverse/direct, puis save/reopen canonique ;
- preuve UI dans un workspace temporaire isolé ;
- projet, historique, dirty state, sélection, approbations et surface source restaurés exactement ;
- aucune UUID affichée dans les libellés ou diagnostics humains.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- Domain : **7/7** (`AreaClimateProfileTests`, `AreaWorldProfileTests`) ;
- Application : **12/12** (machine météo, événement typé, streaming, commandes Environment/World) ;
- Network : **2/2** (`WeatherReplicationServiceTests`) ;
- ModuleFormat : **12/12** (`RuntimeSaveStoreTests`, `AreaJsonSerializerTests`) ;
- smoke Studio complet : `OPENRPG_FORGE_STUDIO_UX6_Y_PASS` et shell Forgotten Forest PASS ;
- captures Forward+ 1920×1080 inspectées :
  `artifacts/verification/studio-ux6-y-runtime-weather-1920x1080.png` et
  `artifacts/verification/studio-ux6-y-area-loading-1920x1080.png` ;
- portail incrémental final : **163,69 s**, build, 33 tests, smoke, capture FHD et matrice ;
  `full_gate=false` reste explicite pour ce lot Studio-only de deux routes ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-y-runtime-operations.summary.json`.

## Limites honnêtes

Studio simule les opérations sur copies. Il n’applique pas la météo à un viewport, ne publie pas
d’enveloppe réseau, n’exécute pas de ForgeScript, ne charge pas de chunk ou de scène Godot et ne
mesure pas un stockage ou un réseau réel. Le runtime, le transport et le chargeur de plateforme
restent les autorités live.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.Network.Tests/Forge.Network.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~AreaClimateProfileTests|' +
  'FullyQualifiedName~AreaWorldProfileTests|' +
  'FullyQualifiedName~WeatherRuntimeStateMachineTests|' +
  'FullyQualifiedName~WeatherForgeScriptDispatchServiceTests|' +
  'FullyQualifiedName~AreaStreamingCoordinatorTests|' +
  'FullyQualifiedName~SetAreaEnvironmentHandlerTests|' +
  'FullyQualifiedName~SetAreaWorldProfileHandlerTests|' +
  'FullyQualifiedName~WeatherReplicationServiceTests|' +
  'FullyQualifiedName~RuntimeSaveStoreTests|' +
  'FullyQualifiedName~AreaJsonSerializerTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime `
  -Evidence ux6-y-runtime-operations -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-ux6-y-runtime-weather-1920x1080.png
```
