# Acceptation Studio — UX1-B Adapter et copier une zone

État local au 16 août 2026 : **fonctionnel et qualifié** pour les deux identifiants de ce lot.
Cette acceptation poursuit UX1 avec deux opérations de document accessibles dans la fenêtre unique
du Studio. Elle ne qualifie pas les 67 autres routes UX1, les 66 fenêtres historiques, les
distributions Windows/Linux ni une publication Git.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests canoniques ciblés : Application 14/14 et ModuleFormat 19/19, soit 33/33 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1B_PASS resize_preview=true reposition_losses=true
  removal_preview=true resize_dry_run=true resize_undo_redo=true resize_persistence=true
  neighbor_preserved=true clone_preview=true full_clone=true template_clone=true
  portal_remap=true new_identities=true clone_undo_redo=true independent_document=true
  clone_persistence=true invalid_blocked=true human_no_uuid=true ui=true ui_transaction=true
  project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire de deux zones, place un objet en bordure et deux
  transitions, compare les politiques de retrait/replacement, applique/annule/rétablit les deux
  opérations, sauvegarde, rouvre et vérifie les documents indépendants et les identités remappées ;
- le smoke UI applique aussi un agrandissement et une copie-gabarit au projet courant en mémoire,
  annule, rétablit puis restaure exactement le projet et la zone active sans écrire Forgotten Forest ;
- journal headless : `artifacts/verification/studio-shell/ux1-b/headless-smoke.log` ;
- journaux Forward+ : `forwardplus-fhd.log`, `forwardplus-qhd.log` et `forwardplus-4k.log` dans
  `artifacts/verification/studio-shell/ux1-b/`, sous Godot 4.7.1 Mono, Vulkan et
  AMD Radeon RX 7800 XT, sans avertissement, erreur ni échec ;
- captures Forward+ inspectées : `resize-fhd.png` en 1920×1080, `copy-qhd.png` en 2560×1440 et
  `resize-4k.png` en 3840×2160.

Les trois captures finales gardent la zone source, les deux parcours, tous les champs, le résumé
d’impact, les actions et le statut visibles ensemble. L’overlay applique la même échelle responsive
que l’assistant de création : 100 % en Full HD, 125 % en QHD et 175 % en 4K, sans troncature ni
fenêtre native supplémentaire.

### ORF-AREA-011

La page **Zones & scène** expose **Dimensions & copie** près de **Nouvelle zone**. `Ctrl+K` propose
également **Redimensionner la zone active**. Le parcours **Dimensions** demande largeur, profondeur,
point fixe et traitement des objets hors limites avec des libellés humains. Il ne montre ni UUID,
hash, chemin de fichier ou JSON.

**Prévisualiser** appelle d’abord `AreaGeometryPlanner.Resize`, puis `EditAreaGeometryHandler` avec
`EditAreaGeometryCommand.DryRun`. Le résumé annonce les dimensions avant/après, le décalage lié à
l’ancrage, le nombre d’objets conservés, replacés ou retirés et la liste nommée des objets concernés.
Le choix recommandé les replace à l’intérieur ; l’autre choix les retire explicitement.

**Appliquer** reconstruit le plan sur la révision courante, repasse par le dry-run canonique, exécute
le même handler puis inscrit la mutation dans `ProjectCommandHistory`. Terrain, fondation,
environnement, auteur et zones voisines restent préservés. Le smoke couvre refus de dimensions
invalides, undo/redo, sauvegarde et réouverture avec la position corrigée.

### ORF-AREA-012

Le parcours **Copier** demande seulement le nom visible, **Copie complète** ou **Gabarit vide**, puis
la conservation ou le retrait des transitions externes. `Ctrl+K` fournit aussi une action directe
**Copier la zone active**. L’aperçu passe par `DuplicateAreaHandler` en dry-run et construit un
candidat non enregistré afin d’annoncer objets, transitions internes/externes, terrain, navigation,
environnement et références de modèles partagées.

L’application revalide la révision, utilise le même handler, ouvre la nouvelle zone dans un onglet et
laisse la sauvegarde explicite. Une copie complète remappe les identités des objets et transitions,
les destinations internes, la navigation, l’environnement et le monde ; les blueprints restent des
références partagées. Un gabarit garde les profils de construction sans reprendre les objets ni les
transitions. La source et les voisines restent inchangées, et la nouvelle zone possède exactement un
document référencé après sauvegarde/réouverture.

## Limites honnêtes

- UX1-B couvre dimensions et copie ; édition de terrain, calques, navigation et environnement reste
  `planned` dans les incréments suivants ;
- l’application reste volontairement non enregistrée jusqu’à `Ctrl+S` ; fermer sans sauver relève
  encore du parcours général de récupération prévu en UX7 ;
- le redimensionnement repositionne les objets par bornage dans le rectangle de zone ; il ne prétend
  pas encore résoudre automatiquement la composition artistique ou la navigation reconstruite ;
- les 717 routes, la parité des 66 fenêtres, l’export du Studio et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~AreaGeometryPlannerTests|FullyQualifiedName~SemanticClipboardWorkflowTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~AreaJsonSerializerTests|FullyQualifiedName~ProjectSessionTests"
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-b-resize -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-b-resize -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-b/resize-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-b-copy -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-b/copy-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux1-b-resize -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-b/resize-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-B, 46 fonctions disposent désormais d’une preuve Studio spécifique ; les
671 autres lignes restent explicitement `planned`.
