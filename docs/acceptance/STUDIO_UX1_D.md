# Acceptation Studio — UX1-D Calques et groupes d’édition

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-AREA-035`. Ce lot rend la
hiérarchie de calques Aurora directement manipulable dans `Forge.Studio`, sans exposer UUID, JSON,
hash ou chemin interne. Il ne qualifie ni les autres routes UX1, ni les 66 fenêtres historiques, ni
les distributions Windows/Linux du Studio.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Domain 3/3, Application 6/6, ModuleFormat 2/2 et MCP 1/1, soit 12/12 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1D_PASS preview=true dry_run=true hierarchy=true
  visibility_lock=true ownership=true group_selection=true partial_export=true drag_reparent=true
  entity_assignment=true other_authoring_preserved=true neighbor_preserved=true runtime=true
  undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true ui=true
  ui_transaction=true project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire de deux zones, construit quatre calques
  imbriqués, déplace un enfant, affecte trois objets, vérifie les états hérités, sélectionne un
  groupe, prévisualise l’export, applique, annule, rétablit, sauvegarde puis rouvre ;
- le smoke UI exécute déplacement, sélection de groupe et aperçu depuis l’overlay réel, puis restaure
  exactement le projet, la zone active et la sélection, sans écrire Forgotten Forest ;
- journal headless : `artifacts/verification/studio-shell/ux1-d/headless-smoke.log` ;
- journaux Forward+ : `tree-fhd.log`, `locked-qhd.log` et `export-4k.log` dans
  `artifacts/verification/studio-shell/ux1-d/` ;
- captures Forward+ inspectées : `tree-fhd.png` en 1920×1080, `locked-qhd.png` en 2560×1440 et
  `export-4k.png` en 3840×2160.

Les trois captures conservent l’arbre, les propriétés humaines, les actions principales, l’aperçu
canonique et le statut visibles. La capture QHD montre explicitement un verrou hérité ; la capture 4K
montre l’export partiel de trois calques, deux objets et un modèle, dans l’unique fenêtre Studio.

### ORF-AREA-035

La page **Zones & scène** expose **Calques** près des opérations de zone. `Ctrl+K` fournit aussi
l’action **Organiser les calques de la zone**. L’overlay présente un arbre lisible avec profondeur,
visibilité effective, verrouillage effectif et propriétaire. Un calque se déplace par
glisser-déposer sur son nouveau groupe ; le sélecteur **Groupe parent** fournit la même opération au
clavier. Les objets sont proposés par noms et positions humaines, puis affectés au calque choisi.

**Sélectionner dans la vue 3D** synchronise tous les objets visibles du groupe avec la sélection
centrale. L’option d’inclusion des objets masqués reste explicite. **Prévisualiser** résout la
hiérarchie avec `AreaLayerHierarchy`, calcule la sélection avec `AreaLayerAuthoringService.SelectGroup`,
calcule l’export gouverné avec `PreviewPartialExport`, puis appelle `SetAreaAuthoringHandler` en
dry-run. Le résumé nomme les calques, objets et modèles exportés ; les cycles, doublons, objets déjà
possédés et modifications d’un groupe verrouillé sont refusés avec un message humain.

**Appliquer** revalide la révision, rejoue le dry-run, exécute le même handler et inscrit le résultat
dans `ProjectCommandHistory`. Les événements utilisateur, la carte, le profil d’expérience, le monde,
les politiques runtime et les zones voisines restent inchangés. Undo/redo restaurent successivement
les deux états ; `Ctrl+S`, fermeture et réouverture conservent hiérarchie, propriétaires, objets,
visibilité, verrouillage, export, schéma et provenance.

Lorsque **Exiger ces métadonnées pendant le jeu** est actif, `AreaLayerRuntimeResolver` refuse une
zone dépourvue de profil valide. Lorsque l’option est inactive, une ancienne zone sans calque garde
le fallback compatible. Ce réglage ne simule pas la sélection ou l’export : il persiste le contrat
runtime canonique de la zone.

## Limites honnêtes

- l’overlay organise les objets déjà présents dans la zone ; leur création et leur placement restent
  assurés par la palette et la vue 3D ;
- l’export partiel est ici un aperçu gouverné des calques et dépendances ; l’écriture du paquet reste
  le rôle du parcours Publier ;
- un calque verrouillé, ou verrouillé par un parent, doit être déverrouillé avant déplacement ou
  réaffectation ;
- l’application reste volontairement en mémoire jusqu’à l’enregistrement explicite ;
- les 717 routes, la parité des 66 fenêtres, l’export du Studio et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --configuration Debug --no-build `
  --filter "FullyQualifiedName~AreaLayerAuthoringTests"
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --configuration Debug --no-build `
  --filter "FullyQualifiedName~SetAreaAuthoringHandlerTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --configuration Debug --no-build `
  --filter "FullyQualifiedName~R056"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --configuration Debug --no-build `
  --filter "FullyQualifiedName~R056AreaLayers"
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-d-tree -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-d-tree -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-d/tree-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-d-locked -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-d/locked-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-d-export -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-d/export-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-D, 48 fonctions disposent d’une preuve Studio spécifique ; les 669 autres lignes
restent explicitement `planned`.
