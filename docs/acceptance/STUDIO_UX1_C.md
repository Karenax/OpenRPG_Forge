# Acceptation Studio — UX1-C Écran de chargement

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-AREA-028`. Ce lot ajoute une
voie d’auteur humaine à la présentation de chargement canonique déjà disponible dans le domaine et
le runtime. Il ne qualifie pas les 66 fenêtres historiques, les distributions Windows/Linux ni les
66 autres routes UX1.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Domain 3/3, Application 7/7 et ModuleFormat 17/17, soit 27/27 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1C_PASS preview=true dry_run=true image_video=true
  reduced_motion=true accessible_fallback=true real_streaming=true minimum_duration=true
  other_experience_preserved=true world_profile_preserved=true neighbor_preserved=true
  undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true ui=true
  ui_transaction=true project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux zones, une image, une vidéo et deux
  ressources de streaming, prévisualise les variantes normale et mouvements réduits, applique,
  annule, rétablit, charge réellement les deux ressources, sauvegarde puis rouvre ;
- le smoke UI exécute le même parcours dans le projet courant en mémoire, puis restaure exactement
  le projet, la zone active et l’état enregistré sans écrire Crypte des Cendres ;
- journal headless : `artifacts/verification/studio-shell/ux1-c/headless-smoke.log` ;
- journaux Forward+ : `forwardplus-fhd.log`, `forwardplus-qhd.log` et `forwardplus-4k.log` dans
  `artifacts/verification/studio-shell/ux1-c/`, sous Godot 4.7.1 Mono, Vulkan et
  AMD Radeon RX 7800 XT, sans avertissement, erreur ni échec ;
- captures Forward+ inspectées : `loading-fhd.png` en 1920×1080, `reduced-qhd.png` en 2560×1440 et
  `fallback-4k.png` en 3840×2160.

Les trois captures gardent les champs, l’aperçu joueur, le résumé, les actions et le statut visibles.
L’overlay emploie une échelle de 100 % en Full HD, 125 % en QHD et 175 % en 4K. La capture QHD
montre explicitement la variante sans mouvement ; la capture 4K montre l’état vide sans média et son
repli textuel, sans fenêtre native supplémentaire.

### ORF-AREA-028

La page **Zones & scène** expose **Chargement** près des opérations de zone. `Ctrl+K` fournit aussi
l’action **Configurer l’écran de chargement**. Le formulaire présente un titre, une image de repli,
une vidéo, des astuces françaises et anglaises, une description accessible, la progression, le
préchargement et une durée minimale. Les médias sont choisis par noms humains parmi les ressources
déjà importées : le parcours normal ne demande ni chemin, UUID, hash ou JSON. Les localisations non
affichées restent conservées lors de l’édition.

**Prévisualiser** construit un nouvel `AreaLoadingPresentation`, conserve les autres cues, émetteurs,
voix et patrouilles de l’`AreaExperienceProfile`, puis appelle `SetAreaExperienceProfileHandler` avec
`SetAreaExperienceProfileCommand.DryRun`. `AreaExperienceRuntimeService.ResolveLoading` produit les
deux rendus : vidéo prioritaire en mode normal, image de repli en mouvements réduits, ou texte
accessible si aucun média utilisable n’est disponible. Le résumé annonce les astuces, la jauge, la
durée minimale et le nombre de ressources réellement déclarées par le profil de streaming.

**Appliquer** revalide la révision, rejoue le dry-run, exécute le même handler et inscrit le résultat
dans `ProjectCommandHistory`. Le monde, le profil de streaming, les autres données d’expérience et
les zones voisines restent inchangés. Undo/redo restaurent successivement les deux états ; `Ctrl+S`,
fermeture et réouverture conservent exactement médias, textes, options, durée et localisations.

Le runtime tient désormais compte de `PrefetchRequiredResources` : lorsqu’il est actif,
`AreaStreamingCoordinator` alimente la progression avec les ressources canoniques de la zone ;
lorsqu’il est désactivé, la présentation se termine sans lancer ce préchargement. Le fallback reste
toujours annoncé et `MinimumVisibleMilliseconds` reste appliqué par le runtime.

## Limites honnêtes

- le Studio sélectionne seulement les images et vidéos déjà présentes dans le workspace ; leur
  import gouverné reste le rôle de la bibliothèque d’assets ;
- le lecteur Studio affiche l’image réellement et représente une vidéo par un état de prélecture ;
  le décodage et la lecture vidéo restent exécutés par `RuntimeLoadingOverlay` ;
- les ressources à charger appartiennent au profil de streaming de zone, affiché ici en lecture
  humaine ; leur édition détaillée arrivera avec les outils terrain/streaming suivants ;
- l’application reste volontairement en mémoire jusqu’à l’enregistrement explicite ;
- les 717 routes, la parité des 66 fenêtres, l’export du Studio et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~AreaExperienceProfileTests"
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~SetAreaExperienceProfileHandlerTests|FullyQualifiedName~AreaExperienceRuntimeServiceTests|FullyQualifiedName~AreaStreamingCoordinatorTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~AreaExperiencePersistenceTests|FullyQualifiedName~ProjectSessionTests"
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-c-loading -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-c-loading -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-c/loading-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-c-reduced -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-c/reduced-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-c-fallback -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-c/fallback-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-C, 47 fonctions disposent d’une preuve Studio spécifique ; les 670 autres lignes
restent explicitement `planned`.
