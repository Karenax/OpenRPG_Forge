# Acceptation Studio — UX6-D Simulation gameplay

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour quatre routes de la page
`runtime`. UX6 atteint **17/84** ; le portail de ce lot reste incrémental (`full_gate=false`).

La surface **Simulation gameplay** expose quatre workflows humains dans une transaction canonique
`SetGameplayControlProfileHandler`. Elle remplace uniquement `Flight`, `Projectiles`, `Physics` et
`Targeting`. Le temps du monde, la fondation UX6-B, les capacités UX6-C, la session et les autres
racines du projet sont réinjectés sans édition.

<a id="orf-game-002"></a>
## ORF-GAME-002 — Vol libre

Activation, exigence runtime, vitesse, accélération, altitude, inclinaison, portance, atterrissage,
navigation volumétrique, sonde et contexte d’entrée sont directement éditables. L’aperçu appelle
`GameplaySimulation.FlyStep` dans un volume copié ; aucun acteur, navigation 3D, animation, IK,
collision ou caméra n’est instancié.

<a id="orf-game-008"></a>
## ORF-GAME-008 — Projectiles

Mode balistique ou guidé, vitesse, gravité, durée, guidage, rayon, autorité serveur, prédiction,
VFX, masque de collision, révision et audit sont exposés. `StepProjectile` résout une étape et une
collision déterministes sur un état temporaire, sans projectile natif, VFX, physique Godot ni
transport réseau.

<a id="orf-game-009"></a>
## ORF-GAME-009 — Physique 3D

Le pas fixe et les collections complètes de corps et liaisons sont éditables avec identités stables
cachées. Corps, modes, masse, amortissement, gravité, trigger, interaction, limites, politique
réseau et exigence runtime sont conservés ; les liaisons exposent type, deux corps, force de rupture
et exclusion de collision. `SimulatePhysics` ne fait évoluer que des états copiés.

<a id="orf-game-014"></a>
## ORF-GAME-014 — Ciblage

Modes de ciblage, portée, rayon, angle, nombre maximal, ligne de vue, action d’entrée, couleur et
tags requis/bloqués sont éditables. `PreviewTargeting` sélectionne un candidat synthétique borné,
sans raycast, caméra, acteur, pathfinding ou réseau live.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- portail incrémental : **126,38 s**, **11/11** tests ciblés — Domain 3, Application 7,
  ModuleFormat 1 — puis smoke global, capture Forward+ FHD et contrôle de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_D_PASS` : sélection explicite, vrai profil Crypte des Cendres,
  quatre invalidités, dry-run sans mutation, quatre résolutions pures, source et approbation
  obsolètes bloquées, commande canonique, undo/redo, round-trip, temps du monde, queues gameplay,
  identités et voisins préservés, transaction UI isolée, ressources libérées, source et surface
  restaurées ;
- capture inspectée :
  `artifacts/verification/studio-increment/ux6-d-gameplay-simulation-forward-plus-fhd.png`,
  1920×1080, Godot 4.7.1 .NET, Vulkan Forward+, quatre onglets et footer complet sans clipping ni
  UUID ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-d-gameplay-simulation.summary.json` ;
- graphe Roslyn régénéré et contrôlé : **10 396 nœuds**, **76 794 relations**, zéro diagnostic.

Le jalon UX6-A à UX6-D regroupe désormais quatre lots et dix-sept routes. Le portail transversal
`scripts/Verify.ps1` a donc été rejoué intégralement après le portail local : **2 035/2 035** tests
.NET, **39/39** tests Python, smokes Studio, Toolset, Runtime, Forward+, Crypte des Cendres, Blender,
MCP, réseau, sauvegarde/reprise et distribution, puis `OPENRPG_FORGE_VERIFICATION=PASS`. Le premier
lancement complet s’était arrêté uniquement sur le contrôle whitespace des fichiers UX6 ; le
formateur du projet a été appliqué à ces seuls fichiers et le portail a été repris depuis le début.

## Préservation et limites honnêtes

La preuve copie le vrai projet Crypte des Cendres dans un workspace temporaire, compare projet,
historique, révision, journal, dirty, zone active et approbation avant/après, puis supprime l’isolat.
Les identités des profils, corps et liaisons restent stables. `WorldTime`, `Foundation`, `Swimming`,
`Climbing`, `RadialMenu`, `TimeControl`, `CombatModes`, `Session`, les zones, blueprints, dialogues et
quêtes sont préservés.

Les aperçus sont des calculs déterministes en mémoire. Ce lot ne prouve ni acteur, `RigidBody3D`,
collision Godot, projectile natif, VFX, caméra, pathfinding, ForgeScript ou transport réseau live.
Les valeurs non finies, enums hors contrat et références de liaison invalides sont bloquées avant
approbation ; aucune valeur valide n’est normalisée silencieusement.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~GameplaySimulationProfileTests|' +
  'FullyQualifiedName~GameplaySimulationTests|' +
  'FullyQualifiedName~GameplaySimulationScriptServiceTests|' +
  'FullyQualifiedName~SetGameplayControlProfileHandlerTests|' +
  'FullyQualifiedName~GameplayControlPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime -Evidence ux6-d-gameplay-simulation `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-d-gameplay-simulation-forward-plus-fhd.png
```
