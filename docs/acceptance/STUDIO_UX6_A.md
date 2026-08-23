# Acceptation Studio — UX6-A Règles et temps du monde

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour quatre routes de la page
`rules`. UX6 est ouverte à **4/84** ; ce lot n’est pas un portail transversal complet
(`full_gate=false`).

La surface **Règles & temps du monde** expose quatre workflows humains : Difficultés, Mutateurs,
Plugins de règles et Temps du monde. Elle utilise deux transactions explicites parce que les règles
et l’horloge appartiennent à deux agrégats canoniques différents. Appliquer l’un invalide
l’approbation de l’autre ; l’utilisateur recharge et prévisualise alors le second bloc.

<a id="orf-proj-023"></a>
## ORF-PROJ-023 — Configuration de difficulté

La configuration du module sélectionne une difficulté réelle, conserve ses identités et édite ses
multiplicateurs, restrictions de sauvegarde, tir ami et aides d’accessibilité. Le contexte de zone
requis par `SetRulesProfilesHandler` est sélectionné par nom ; ses règles UX2 sont réinjectées sans
édition.

<a id="orf-game-024"></a>
## ORF-GAME-024 — Difficulté et mutateurs

Les difficultés et mutateurs sont des collections complètes : ajout/retrait explicite, sélection de
la difficulté active, scalaires et tags. Le dry-run résout la difficulté effective avec
`RulesRuntimeService.ResolveDifficulty`, puis l’application passe par `SetRulesProfilesHandler`,
son historique, undo/redo et save/reopen.

<a id="orf-game-023"></a>
## ORF-GAME-023 — Système de règles plugin

Chaque package conserve clé, version, package de contenu et six registres déclaratifs. L’aperçu
appelle `RulesetPluginRuntimeService.Resolve` avec le contenu déclaré présent puis absent afin de
montrer les politiques optionnelle et requise. Studio ne charge aucune DLL, aucun code natif et
n’exécute aucune extension.

<a id="orf-game-017"></a>
## ORF-GAME-017 — Temps de monde

Calendrier, minute totale, échelle, pause, échelles par zone, domaines de pause et horaires sont
édités comme collections ordonnées. Le dry-run passe par `SetGameplayControlProfileHandler` et
`GameplaySimulationScriptService.AdvanceWorld` sur une copie en mémoire. Les horaires éligibles sont
rapportés, mais aucun événement, ForgeScript, nœud Godot, réseau ou sauvegarde live n’est exécuté.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- portail incrémental : **163,60 s**, 21/21 tests ciblés — Domain 6, Application 11,
  ModuleFormat 4 — puis smoke global, capture Forward+ FHD et matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_A_PASS` : sélection explicite, données réelles du module,
  invalidités, deux dry-runs, aperçus déterministes, source et approbation obsolètes bloquées, deux
  commandes canoniques, undo/redo, round-trip, voisins et tails préservés, transaction UI isolée,
  ressources libérées, source et surface restaurées ;
- capture inspectée :
  `artifacts/verification/studio-shell/ux6-a/ux6-a-rules-fhd.png`, 1920×1080, Godot 4.7.1 .NET,
  Vulkan Forward+, Radeon RX 7800 XT, 278 assets gouvernés et zéro fallback ;
- résumé machine : `artifacts/verification/studio-increment/ux6-a-rules.summary.json`.
- graphe Roslyn régénéré et contrôlé : 10 333 nœuds, 76 153 relations, zéro diagnostic.

La capture montre les quatre onglets, une scrollbar explicite, les deux paires Aperçu/Appliquer,
Annuler, Rétablir et Enregistrer sans clipping. Le résumé canonique a été humanisé après la première
inspection ; la seconde capture ne montre plus le texte technique du handler.

## Préservation et limites honnêtes

La preuve copie le vrai projet Forgotten Forest dans un workspace temporaire, compare projet,
historique, révision, journal, dirty, zone active et approbations avant/après, puis supprime l’isolat.
`ProgressionPolicy`, les règles de zone UX2 et tous les profils gameplay hors `Simulation.WorldTime`
restent préservés.

L’aperçu plugin simule seulement la disponibilité déclarée du contenu ; il ne constitue pas un
inventaire d’installation. L’aperçu temps avance une horloge copiée et ne distribue aucun événement.
Ce lot ne prouve donc ni package natif chargé, ni gameplay live, ni moteur, ni réseau, ni ForgeScript.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~RulesProfileTests|' +
  'FullyQualifiedName~GameplaySimulationProfileTests|' +
  'FullyQualifiedName~RulesRuntimeServiceTests|' +
  'FullyQualifiedName~SetRulesProfilesHandlerTests|' +
  'FullyQualifiedName~GameplaySimulationTests|' +
  'FullyQualifiedName~GameplaySimulationScriptServiceTests|' +
  'FullyQualifiedName~SetGameplayControlProfileHandlerTests|' +
  'FullyQualifiedName~RulesProfilePersistenceTests|' +
  'FullyQualifiedName~GameplayControlPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page rules -Evidence ux6-a-rules `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux6-a/ux6-a-rules-fhd.png
```
