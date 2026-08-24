# Acceptation Studio — UX6-E Production gameplay

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour quatre routes Gameplay.
UX6 atteint **21/84** ; le portail de ce lot reste incrémental (`full_gate=false`).

La surface **Production gameplay** expose quatre workflows humains dans une transaction canonique
`SetRuntimeExtensionsHandler`. Elle remplace uniquement `PostLaunch.Gameplay.ZeroGravity`,
`Vehicles`, `Mounts` et `Achievements`. Les packs gouvernés, le directeur live, le réseau,
l’expérience Creator, la production, les outils modernes, la préparation opérationnelle et toutes
les autres racines du projet restent inchangés.

<a id="orf-game-005"></a>
## ORF-GAME-005 — Apesanteur

Activation, accélération des propulseurs, vitesse maximale, traînée, assistance d’orientation et
conservation de l’élan sont directement éditables avec leurs bornes canoniques. L’aperçu appelle
`PostLaunchCapabilityResolver.ResolveMobility` sur une demande copiée ; aucune gravité, physique,
collision ou locomotion Godot n’est exécutée.

<a id="orf-game-006"></a>
## ORF-GAME-006 — Véhicules

Activation, nombre de places, mode cinématique/physique, intégrité, possession et réplication
autoritaire sont exposés sans identifiant technique. La résolution accepte ou refuse une demande
de places en mémoire ; aucun véhicule, siège, contrôleur, acteur ou transport réseau n’est créé.

<a id="orf-game-007"></a>
## ORF-GAME-007 — Montures

Activation, combat, inventaire, persistance du cavalier et trois politiques de descente sont
éditables. La décision de mobilité traite une demande explicite sur une copie ; aucune monture,
animation, inventaire ou position sûre n’est matérialisé dans le moteur.

<a id="orf-game-028"></a>
## ORF-GAME-028 — Succès et jalons

La collection complète est éditable par sélection, ajout et retrait avec identités stables cachées,
clé, événement, seuil, clé plateforme optionnelle et politique de télémétrie. L’aperçu appelle
`EvaluateAchievement` ; il calcule une décision locale sans appel de plateforme, déverrouillage
persistant ni émission de télémétrie.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- portail incrémental : **136,22 s**, **13/13** tests ciblés — Domain 2, Application 4,
  ModuleFormat 7 — puis smoke global, capture Forward+ FHD et contrôle de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_E_PASS` : sélection explicite, vrai profil Forgotten Forest,
  quatre invalidités, dry-run sans mutation, résolutions pures, source et approbation obsolètes
  bloquées, commande canonique, undo/redo, round-trip, profils voisins et identités préservés,
  transaction UI isolée, ressources libérées, source et surface restaurées ;
- capture inspectée :
  `artifacts/verification/studio-increment/ux6-e-gameplay-production-forward-plus-fhd.png`,
  1920×1080, Godot 4.7.1 .NET, Vulkan Forward+, quatre onglets, diagnostics et footer complets sans
  clipping ni UUID ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-e-gameplay-production.summary.json`.

## Préservation et limites honnêtes

La preuve copie le vrai projet Forgotten Forest dans un workspace temporaire, compare projet,
historique, révision, journal, dirty, zone active et approbation avant/après, puis supprime l’isolat.
Les identités du profil, des trois capacités et des succès existants restent stables. Le constructeur
canonique rematérialise l’allowlist des packs gouvernés ; la preuve compare donc ses six champs et
sa liste ordonnée, puis compare le projet final sérialisé au candidat exact du handler.

Les aperçus sont des décisions déterministes en mémoire. Ce lot ne prouve ni gravité moteur,
véhicule, monture, plateforme de succès, télémétrie, physique, acteur, ForgeScript ou réseau live.
Les valeurs non finies, modes hors contrat, politiques de descente invalides et doublons de succès
sont bloqués avant approbation ; aucune valeur valide n’est normalisée silencieusement.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~PostLaunchCapabilityResolverTests.G13AC|' +
  'FullyQualifiedName~PostLaunchCapabilityResolverTests.G13AD|' +
  'FullyQualifiedName~PostLaunchRuntimeServiceTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime -Evidence ux6-e-gameplay-production `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-e-gameplay-production-forward-plus-fhd.png
```
