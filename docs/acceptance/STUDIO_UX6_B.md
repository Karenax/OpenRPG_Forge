# Acceptation Studio — UX6-B Fondations du gameplay

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour quatre routes de la page
`runtime`. UX6 atteint **8/84** ; ce lot reste un portail incrémental (`full_gate=false`).

La surface **Fondations du gameplay** expose quatre workflows humains : Déplacement, Interactions,
Caméras et Entrées. Tous reconstruisent uniquement `GameplayFoundationProfile` et passent ensemble
par la commande canonique `SetGameplayControlProfileHandler`. La politique de sauvegarde, propriété
d’UX7, ainsi que tous les profils gameplay voisins sont réinjectés sans édition.

<a id="orf-game-001"></a>
## ORF-GAME-001 — Déplacement au sol

Marche, course, accélération, décélération, pente, hauteur de marche, saut, accroupissement, root
motion et prédiction configurée sont éditables directement. L’aperçu exécute
`GameplayFoundationRuntime.Step` sur un état copié et rapporte vitesse, saut et politique de
prédiction sans déplacer d’acteur.

<a id="orf-game-010"></a>
## ORF-GAME-010 — Système d’interaction

Le ciblage, la portée, le cône de vue, l’invite et la collection complète des verbes sont éditables
par sélection humaine. Chaque verbe conserve son identité stable cachée et expose clé, libellé,
commande, maintien, tags de cible et autorité. `ResolveFocus` prouve le filtrage déterministe sur une
cible temporaire, sans interaction live.

<a id="orf-game-012"></a>
## ORF-GAME-012 — Caméras configurables

Le catalogue complet permet d’ajouter, retirer et modifier les huit modes canoniques, le profil par
défaut, champ de vue, distances, inclinaison, focus, collision et alignement du personnage.
`ResolveCamera` résout le profil demandé, mais aucun nœud `Camera3D` n’est créé ou déplacé.

<a id="orf-game-013"></a>
## ORF-GAME-013 — Contrôles et input mapping

Contextes, actions et liaisons clavier/souris ou manette sont des collections complètes avec
ajout/retrait explicite. Les actions accessibles exigent une liaison ; rebinding, glyphes, liaison
principale et réglages locaux par joueur sont persistés. `ResolveInput` vérifie une liaison copiée ;
Studio ne reconstruit pas l’`InputMap` Godot live.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- portail incrémental : **134,00 s**, 8/8 tests ciblés — Domain 3, Application 4,
  ModuleFormat 1 — puis smoke global, capture Forward+ FHD et matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_B_PASS` : sélection explicite, données réelles Forgotten Forest,
  quatre invalidités, dry-run sans mutation, quatre résolveurs purs, source et approbation obsolètes
  bloquées, commande canonique, undo/redo, round-trip, sauvegarde et voisins préservés, transaction
  UI isolée, ressources libérées, source et surface restaurées ;
- capture inspectée :
  `artifacts/verification/studio-shell/ux6-b/ux6-b-gameplay-foundation-fhd.png`, 1920×1080,
  Godot 4.7.1 .NET, Vulkan Forward+, Radeon RX 7800 XT, 278 assets gouvernés et zéro fallback ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-b-gameplay-foundation.summary.json` ;
- graphe Roslyn régénéré et contrôlé : 10 355 nœuds, 76 368 relations, zéro diagnostic.

La première capture a révélé des décimales binaires brutes dans les champs `float`. L’affichage a
été borné à six décimales sans modifier la valeur canonique, puis la build, les tests, le smoke, le
portail et la capture ont tous été rejoués. La capture finale montre les quatre onglets, le footer
complet, le diff, l’aperçu et les diagnostics sans clipping ni UUID.

## Préservation et limites honnêtes

La preuve copie le vrai projet Forgotten Forest dans un workspace temporaire, compare projet,
historique, révision, journal, dirty, zone active et approbation avant/après, puis supprime l’isolat.
`Foundation.Save`, Swimming, Climbing, RadialMenu, TimeControl, CombatModes, Simulation, Session,
les six zones, blueprints, dialogues et quêtes restent structurellement préservés.

Les aperçus sont des décisions pures. Ce lot ne prouve ni déplacement d’acteur, collision physique,
caméra live, rebinding de l’InputMap, prediction réseau, paquet natif, ForgeScript ou moteur de jeu.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~GameplayFoundationProfileTests|' +
  'FullyQualifiedName~GameplayFoundationRuntimeTests|' +
  'FullyQualifiedName~SetGameplayControlProfileHandlerTests|' +
  'FullyQualifiedName~GameplayControlPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime -Evidence ux6-b-gameplay-foundation `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux6-b/ux6-b-gameplay-foundation-fhd.png
```
