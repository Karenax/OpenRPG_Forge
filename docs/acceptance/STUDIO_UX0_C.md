# Acceptation Studio — UX0-C Manipuler la scène sans quitter le Studio

État local au 15 août 2026 : **fonctionnel et qualifié** pour les cinq identifiants de ce lot.
Cette acceptation porte sur les routes aujourd’hui actives dans le Studio autonome. Elle ne promeut
aucune page encore planifiée, ne qualifie pas les 66 fenêtres historiques et ne vaut pas release.

## Preuves communes

- build : `dotnet build apps/Forge.Studio/Forge.Studio.csproj --configuration Debug --no-restore`,
  sans avertissement ni erreur ;
- tests ciblés : Domain 9/9, Application 12/12 et ModuleFormat 5/5, soit 26/26 ;
- smoke Godot 4.7.1 .NET :
  `OPENRPG_FORGE_STUDIO_UX0C_PASS modes=6 navigation_overlay=true collision_overlay=true
  camera_views=6 camera_undo=true camera_roundtrip=true selection=3 lock=true translate=true
  rotate=true scale=true alignment=true project_undo=true workspace_undo=true invalid_blocked=true
  windows=1 persisted=false` ;
- rendu Forward+ réel de Clairval : 291 entités, 278 assets gouvernés et zéro fallback ; la vue
  Navigation, la multisélection et le gizmo d’échelle ont été inspectés en Full HD, et la vue
  Navigation en QHD ;
- captures de la surcouche intégrée : modes, caméra, sélection et transformation sans seconde
  fenêtre, sans UUID, hash ou chemin technique dans le parcours normal ;
- préférences préservées : les SHA-256 de `editor-camera.json`, `editor-profile.json` et
  `editor-profiles.json` sont identiques avant/après ; `editor-keymap.json` reste absent ;
- portail intégré : `scripts/Verify.ps1` terminé par `OPENRPG_FORGE_VERIFICATION=PASS`, avec
  1 912 tests .NET, 39 tests Python, zéro paquet vulnérable et les smokes Studio UX0-A/B/C inclus ;
- journaux : `artifacts/verification/studio-ux0c/studio-ux0c-smoke.log` et
  `artifacts/verification/studio-ux0c/studio-ux0c-forwardplus.log` ;
- captures : `artifacts/verification/studio-ux0c/ux0-c-modes-fhd.png`,
  `ux0-c-modes-qhd.png`, `ux0-c-camera-fhd.png`, `ux0-c-selection-fhd.png`,
  `ux0-c-transform-fhd.png`, `ux0-c-transform-qhd.png`, `ux0-c-scale-gizmo-fhd.png`,
  `ux0-c-world-navigation-fhd.png` et `ux0-c-world-navigation-qhd.png`.

Le smoke headless utilise volontairement le repli GL et ses modèles de secours ; il ne sert pas de
preuve d’asset. La preuve spatiale qualifiante est le journal Forward+ avec `fallback_assets=0`.

### ORF-SHELL-003

La vue 3D principale construit la zone canonique et propose six modes explicites : **Édition**,
**Simulation**, **Éclairage**, **Navigation**, **Collisions** et **Performance**. Chaque mode change
réellement la présentation : gizmos, lumière de confort, grille dimensionnée par la zone, volumes de
collision ou coût des diagnostics. Le choix suit
`StudioViewportToolsOverlay` → `ConfigureViewportMode` → `EditorWorkspaceProfilePlanner` →
`EditorWorkspaceProfileCommandHistory`, puis n’est stocké atomiquement que sur demande.

### ORF-SHELL-006

Le clic sélectionne un objet ; `Ctrl`/`Maj` ajoute ou retire des objets, un objet actif reste défini et
la sélection propre à chaque zone est restaurée. L’auteur peut tout cadrer, cadrer la sélection,
verrouiller la sélection et inspecter les tags et transformations communes. Les éditions communes
passent par les mêmes commandes Application que l’édition d’un objet ; une sélection invalide ou un
objet disparu est refusé avant toute mutation.

### ORF-SHELL-007

Orbite, translation latérale, zoom adaptatif, focus, pilotage, vitesse, sensibilité, sept raccourcis,
six vues perspective/orthographiques et bookmarks sont accessibles par noms humains. La route est
`StudioWorldViewport` → `NavigateEditorCameraCommand` → `NavigateEditorCameraHandler` →
`EditorCameraProfile`. La pose exacte, y compris projection et taille orthographique, est restaurée
et annulable ; `EditorCameraProfileStore` n’écrit que via **Enregistrer la caméra**.

### ORF-SHELL-011

Les outils Translation, Rotation et Échelle disposent de gizmos visibles, des repères local/global,
des pivots individuel/objet actif/centre, des snaps, d’une saisie numérique et des alignements. La
prévisualisation est sans effet ; l’application envoie une unique `ManipulateEntitiesCommand` pour
toute la sélection. `ManipulateEntitiesHandler` calcule le résultat canonique, notamment la rotation
locale dans le repère de l’objet actif, puis l’historique restaure le projet complet.

### ORF-SHELL-013

Toutes les mutations actuellement activées dans le Studio passent par une commande et un historique
canonique : projet pour les entités, workspace pour modes et profils, caméra pour navigation et
bookmarks, keymap pour les raccourcis. Le smoke prouve undo/redo sur les trois contextes UX0-C et
refuse une commande invalide. Les pages encore planifiées ne présentent aucune mutation active ;
elles ne peuvent donc pas contourner ce contrat. La sauvegarde du module et celle des préférences
restent des actions explicites séparées.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --filter `
  'FullyQualifiedName~EditorCameraProfileTests|FullyQualifiedName~EditorWorkspaceProfileTests'
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --filter `
  'FullyQualifiedName~EditorCameraNavigationTests|FullyQualifiedName~ManipulateEntitiesHandlerTests|FullyQualifiedName~EditorWorkspaceProfilePlannerTests'
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --filter `
  'FullyQualifiedName~EditorCameraProfileStoreTests|FullyQualifiedName~EditorWorkspaceProfileStoreTests'
& .\scripts\Open-Studio.ps1 -Headless -Smoke -Evidence ux0-c -Wait
& .\scripts\Open-Studio.ps1 -Resolution 1920x1080 -Evidence ux0-c-world-navigation `
  -Capture artifacts/verification/studio-ux0c/ux0-c-world-navigation-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Resolution 2560x1440 -Evidence ux0-c-world-navigation `
  -Capture artifacts/verification/studio-ux0c/ux0-c-world-navigation-qhd.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A et UX0-B, quinze fonctions disposent désormais d’une preuve spécifique ; les 702 autres
lignes restent explicitement `planned`.
