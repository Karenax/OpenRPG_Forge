# Acceptation Studio — UX0-H Régler le temps et les transitions globales

État local au 15 août 2026 : **fonctionnel et qualifié** pour les trois identifiants de ce lot.
Cette acceptation porte sur les routes actives du Studio autonome. Elle ne qualifie pas encore les
66 fenêtres historiques, les autres lignes de la matrice Studio, une distribution publique ou une
publication Git.

## Preuves communes

- builds `Forge.Studio` et `Forge.Runtime` : zéro avertissement et zéro erreur ;
- tests ciblés ajoutés : Domain 1/1, Application 2/2 et ModuleFormat 1/1, soit 4/4 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX0H_PASS sequence_abort=true world_clock=true progression=true
  runtime_execution=true missing_script_blocked=true invalid_values_blocked=true dry_run=true
  atomic_undo_redo=true persistence_roundtrip=true neighbors_preserved=true human_no_uuid=true
  ui=true ui_transaction=true cards=12 bindings=29 startup_flow_preserved=true
  project_restored=true windows=1` ;
- smoke runtime réel, sortie 0 :
  `OPENRPG_FORGE_RUNTIME_UX0H_WORLD_TRANSITIONS_PASS
  sequence_abort=restored-script-executed world_clock=calendar-zone-astronomy-save
  progression=curve-source-group-difficulty runtime=resolved` ;
- la commande Application unique `SetModuleRuntimePoliciesCommand` prévisualise puis applique les
  trois politiques. L’opération entre dans `ProjectCommandHistory`, puis undo et redo restaurent le
  document canonique complet ;
- un script d’abandon requis doit être relatif, borné au projet, présent sous `scripts` et contenir
  le handler choisi. Le cas absent est refusé avant mutation avec un message humain ;
- calendrier invalide, fuseau hors limites et courbe de progression non croissante sont refusés
  avant sauvegarde ;
- les profils non affichés sont préservés : cinématiques, extensions, gameplay, repos, difficultés,
  mutateurs, plugins de règles et signaux utilisateur. La séquence de démarrage reste inchangée ;
- le format projet `0.6.36` savait déjà persister ces profils canoniques : aucune migration de format
  ni réécriture du contenu de démonstration n’a été nécessaire ;
- journal headless : `artifacts/verification/studio-shell/ux0-h/headless-smoke.log` ;
- journal runtime : `artifacts/verification/studio-shell/ux0-h/runtime-smoke.log` ;
- journaux Forward+ : `forwardplus-fhd.log`, `forwardplus-qhd.log` et `forwardplus-4k.log` dans
  `artifacts/verification/studio-shell/ux0-h/`, tous sous Godot 4.7.1 et Vulkan sur
  AMD Radeon RX 7800 XT ;
- captures Forward+ inspectées : `abort-fhd.png` en 1920×1080, `time-qhd.png` en 2560×1440 et
  `progression-4k.png` en 3840×2160 dans le même dossier.

La première inspection Full HD a détecté une aide autowrappée réduite à une colonne d’un caractère.
Les aides UX0-H possèdent désormais une largeur minimale et les commandes de progression restent
groupées près de leur contexte sur les écrans larges. Le bureau QHD plafonnait aussi la hauteur d’une
fenêtre demandée en 4K ; le harness de preuve rend donc la même interface dans un `SubViewport`
Forward+ de taille exacte lorsque le système borne la fenêtre. Le journal contrôle la taille du PNG,
ce qui empêche de présenter une capture 3840×1421 comme une preuve UHD.

### ORF-PROJ-015

La carte **Abandon de cinématique** expose l’activation, les restaurations caméra, contrôles,
protection temporaire, verrous et transaction narrative, ainsi que le nombre maximal d’étapes à
annuler. Le script est choisi par son nom visible ; aucun chemin, handler technique, UUID ou hash
n’est demandé dans le parcours normal.

`SequenceAbortCoordinator` restaure un snapshot borné et refuse une séquence inactive, désactivée ou
un rollback narratif trop large. `GameRoot` publie ensuite la liaison configurée et compile/exécute le
ForgeScript résolu. Le smoke vérifie réellement restauration et exécution.

### ORF-PROJ-016

La carte **Temps et calendrier du monde** configure activation, caractère requis, pause, vitesse,
jours par mois, mois par année, heures par jour, minutes par heure et date initiale. Les repères aube,
lever/coucher du soleil, crépuscule, lever de lune et minuit sont activables et éditables. Chaque zone
existante apparaît par son nom humain avec un décalage en minutes ; une zone sans réglage reste
absente du dictionnaire canonique au lieu d’être silencieusement écrite à zéro.

`CustomWorldEventBus` avance le temps selon la vitesse, applique le fuseau de la zone et détecte les
événements astronomiques franchis. Le snapshot persiste minutes totales, pause et révision.

### ORF-PROJ-017

La carte **Échelle de progression** montre le ruleset courant par son nom, puis permet d’ajouter,
modifier ou retirer des paliers et des sources d’expérience. Partage au groupe, multiplicateur de
groupe et multiplicateur de difficulté sont explicites. Le niveau initial obligatoire est protégé et
aucune règle D&D n’est codée dans l’interface.

`RulesRuntimeService` résout l’expérience à partir de la courbe, de la source nommée, du groupe, de la
difficulté et du ruleset canonique. Le smoke atteint un palier configuré avec ces seules données.

## Limites honnêtes

- `ORF-PROJ-018`, film ou séquence de démarrage, n’est pas absorbé par cette page : il reste planifié
  sur **Cinématiques & caméras** dans UX5 ;
- la carte affiche le ruleset actuellement attaché au module et en édite l’échelle. La création et la
  sélection de profils de ruleset complets restent dans **Règles & difficulté** en UX6 ;
- les fuseaux couvrent les zones déjà présentes ; créer, renommer ou supprimer une zone reste le
  travail de la page **Zones & scène** ;
- la qualification des 717 routes, la parité des 66 fenêtres, l’export Windows/Linux et le retrait du
  plugin historique restent hors de ce lot.
- un projet Core minimal sans aucun ForgeScript émet
  `OPENRPG_FORGE_RUNTIME_UX0H_WORLD_TRANSITIONS_SKIPPED reason=no-forgescript-fixture` dans son smoke
  générique. Le smoke runtime principal conserve une fixture résolue et son marqueur `PASS` reste
  obligatoire dans `Verify.ps1`.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --filter `
  'FullyQualifiedName~Ux0HSequenceAbortBindingRequiresASafeServerScriptWhenMarkedRequired'
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --filter `
  'FullyQualifiedName~Ux0HWorldTransitions'
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --filter `
  'FullyQualifiedName~Ux0HProjectRoundTripPreservesAbortCalendarAndProgressionWithoutMovingStartupFlow'
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux0-h-time -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 1920x1080 -Evidence ux0-h-abort `
  -Capture artifacts/verification/studio-shell/ux0-h/abort-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 2560x1440 -Evidence ux0-h-time `
  -Capture artifacts/verification/studio-shell/ux0-h/time-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 3840x2160 -Evidence ux0-h-progression `
  -Capture artifacts/verification/studio-shell/ux0-h/progression-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX0-H, trente-sept fonctions disposent désormais d’une preuve Studio spécifique ; les
680 autres lignes restent explicitement `planned`.
