# Acceptation Studio — UX6-C Capacités gameplay

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour cinq routes de la page
`runtime`. UX6 atteint **13/84** ; ce lot reste un portail incrémental (`full_gate=false`).

La surface **Capacités gameplay** expose cinq workflows humains dans une transaction canonique
`SetGameplayControlProfileHandler`. Elle remplace uniquement `Swimming`, `Climbing`, `RadialMenu`,
`TimeControl` et `CombatModes`. La fondation UX6-B, la sauvegarde UX7, la simulation, la session et
les autres racines du projet sont réinjectées sans édition.

<a id="orf-game-003"></a>
## ORF-GAME-003 — Nage et plongée

Activation, caractère requis, flottabilité, accroche de surface, vitesses, influence du courant,
souffle, tags de volumes et trois animations sont directement éditables. L’aperçu appelle
`MovementCapabilityRuntime.Resolve` sur un contexte aquatique copié et rapporte plongée, vitesse et
souffle sans instancier de volume d’eau ni déplacer d’acteur.

<a id="orf-game-004"></a>
## ORF-GAME-004 — Escalade

Vitesse, endurance, drain, portée de rebord, cinématique inverse configurée, liens authorés,
protection de chute, tags de surface et animations sont exposés. Le même résolveur consomme une
endurance temporaire ; aucun lien, IK, collision ou acteur live n’est exécuté.

<a id="orf-game-011"></a>
## ORF-GAME-011 — Menu radial

Entrée, zone morte, profondeur, manette, clavier, liste de repli et lecteur d’écran sont éditables.
La collection complète des actions conserve ses identités cachées et permet ajout, retrait,
réordonnancement, parentage, type, commande, cible, tags et libellé accessible. `RadialActionResolver`
prouve la racine et un sous-menu sans ouvrir de roue native.

<a id="orf-game-015"></a>
## ORF-GAME-015 — Pause tactique

Pause, ralenti, planification, tour par tour, limite d’actions, politique multijoueur, fraction de
vote et permissions sont éditables. `TimeControlCoordinator.Request` évalue une demande locale sur
une session copiée ; aucune horloge moteur, file d’acteurs ou session réseau n’est modifiée.

<a id="orf-game-016"></a>
## ORF-GAME-016 — Modes de combat

Le catalogue complet des fournisseurs permet ajout, retrait, sélection du défaut, clé, nom humain,
temps réel/avec pause/tour par tour/personnalisé, fournisseur personnalisé, contrats d’événement et
autorité serveur. Le dry-run global exige les mêmes contrats pour tous les fournisseurs. L’aperçu
résout uniquement le fournisseur par défaut ; aucun combat, événement ou réseau live n’est lancé.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- portail incrémental final : **133,93 s**, **7/7** tests ciblés — Domain 2, Application 4,
  ModuleFormat 1 — puis smoke global, capture Forward+ FHD et contrôle de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_C_PASS` : sélection explicite, vrai profil Crypte des Cendres,
  cinq invalidités, dry-run sans mutation, cinq résolutions pures, source et approbation obsolètes
  bloquées, commande canonique, undo/redo, round-trip, fondation et voisins préservés, transaction UI
  isolée, ressources libérées, source et surface restaurées ;
- capture inspectée :
  `artifacts/verification/studio-shell/ux6-c/ux6-c-gameplay-capabilities-fhd.png`, 1920×1080,
  Godot 4.7.1 .NET, Vulkan Forward+, cinq onglets et footer complet sans clipping ni UUID ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-c-gameplay-capabilities.summary.json` ;
- graphe Roslyn régénéré et contrôlé : **10 376 nœuds**, **76 585 relations**, zéro diagnostic.

La première capture était fonctionnelle mais montrait quatre noms d’enums techniques en anglais.
Les libellés de mouvement, temps, pause multijoueur et combat ont été humanisés en français, puis
la build, les 7 tests, le smoke, le portail et la capture ont tous été rejoués. La capture finale
conserve les valeurs canoniques et ne masque aucun diagnostic.

## Préservation et limites honnêtes

La preuve copie le vrai projet Crypte des Cendres dans un workspace temporaire, compare projet,
historique, révision, journal, dirty, zone active et approbation avant/après, puis supprime l’isolat.
Les identités des cinq profils, actions radiales et fournisseurs restent stables. `Foundation`,
`Simulation`, `Session`, les zones, blueprints, dialogues et quêtes sont préservés.

Les aperçus sont des décisions pures. Ce lot ne prouve ni acteur, volume d’eau, lien d’escalade,
cinématique inverse, roue native, horloge moteur, combat, événement, ForgeScript ou transport réseau
live. Les valeurs non finies et enums hors contrat sont bloqués explicitement par la surface Studio.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~GameplayControlProfileTests|' +
  'FullyQualifiedName~GameplayControlRuntimeTests|' +
  'FullyQualifiedName~SetGameplayControlProfileHandlerTests|' +
  'FullyQualifiedName~GameplayControlPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime -Evidence ux6-c-gameplay-capabilities `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux6-c/ux6-c-gameplay-capabilities-fhd.png
```
