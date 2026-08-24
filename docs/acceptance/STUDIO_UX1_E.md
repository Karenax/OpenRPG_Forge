# Acceptation Studio — UX1-E Blueprint de placeable

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-001`. Ce lot ouvre dans
`Forge.Studio` une voie d’auteur humaine vers le `PlaceableWorldProfile.FoundationPolicy` canonique.
Il ne qualifie pas à lui seul les fonctions placeable voisines ; `ORF-PLC-002` est désormais
qualifiée séparément par `STUDIO_UX1_F.md`. Les 66 fenêtres historiques, les distributions
Windows/Linux du Studio et une release publique restent hors de ce lot.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Domain 1/1, Application 1/1, RPG Kit 3/3, ModuleFormat 1/1 et MCP 1/1, soit 7/7 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1E_PASS preview=true dry_run=true composition=true
  asset_reference=true instance_selection=true neighbor_preserved=true
  other_catalog_preserved=true runtime=true undo_redo=true persistence=true invalid_blocked=true
  human_no_uuid=true ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux blueprints placeable et trois
  instances, conserve un profil voisin, prévisualise, applique, annule, rétablit, sauvegarde et
  rouvre la fiche canonique ;
- le smoke UI applique puis annule une modification depuis l’overlay réel de Crypte des Cendres et
  restaure exactement le projet, la zone active, la sélection et l’état enregistré ;
- journal headless : `artifacts/verification/studio-shell/ux1-e/headless-smoke.log` ;
- journaux Forward+ : `blueprint-fhd.log`, `interaction-qhd.log` et `preview-4k.log` dans
  `artifacts/verification/studio-shell/ux1-e/` ;
- captures Forward+ inspectées : `blueprint-fhd.png` en 1920×1080, `interaction-qhd.png` en
  2560×1440 et `preview-4k.png` en 3840×2160.

Les trois captures gardent le blueprint, l’état vide, le visuel, le formulaire défilant, l’aperçu
runtime et toutes les actions visibles. L’overlay emploie une échelle de 100 % en Full HD, 125 % en
QHD et 175 % en 4K. La capture QHD montre le rôle **Objet utilisable** et ses réglages ; la capture
4K montre l’aperçu statique. Une correction de contrainte de hauteur a été inspectée en Full HD afin
que les boutons restent accessibles dans l’unique fenêtre Studio.

### ORF-PLC-001

La page **Objets & interactions** expose **Blueprint de décor** dans son en-tête. `Ctrl+K` fournit
aussi l’action **Configurer un blueprint de décor**. La colonne de gauche liste uniquement les
blueprints placeable par nom, annonce le nombre d’instances et distingue clairement l’absence ou la
présence d’une fiche canonique. Les assets 3D sont triés par usage réel puis par nom humain ; le nom
de variante du manifeste est préféré et le dossier importé sert de repli lisible. Aucun UUID, hash,
JSON ou chemin interne n’est demandé à l’auteur.

Le formulaire compose l’identité, le visuel, la collision et la navigation, le rôle statique ou
utilisable, les composants santé/conteneur/verrou/piège/physique, les liaisons de scripts, les
surcharges explicites et une transformation indépendante de Godot. Position, rotation, échelle,
pivot, pas de grille et alignement de surface sont numériques. Les données avancées qui ne sont pas
encore affichées — autres localisations, détails du visuel inchangé, couche physique, verbes et
conditions existants, parent, animation de transformation, contrat OnUsed et validation runtime —
restent conservées lors d’une édition.

**Prévisualiser** reconstruit un `PlaceableWorldProfile` et un `WorldFeatureCatalog` en préservant
toutes les autres familles, puis appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`.
`WorldFeatureRuntimeResolver.ResolvePlaceableFoundation` calcule la pose alignée sur le pas de
grille et alimente la carte d’aperçu. Le bouton **Voir les instances en 3D** ferme la fiche et
synchronise les instances réelles de la zone active avec la sélection et le gizmo du viewport.

**Appliquer au module** exécute le même handler sur la révision courante et inscrit son résultat dans
`ProjectCommandHistory`. Le profil placeable voisin, portes, triggers, rencontres, routes,
marchands, compagnons et scripts du catalogue restent inchangés. Undo/redo restaurent
successivement les deux états ; `Ctrl+S`, fermeture et réouverture conservent l’identité stable, la
provenance, l’asset, les composants, les scripts, les surcharges et la transformation exacte.

Une collision sans géométrie positive, une navigation incohérente, un rôle invalide, un décor non
instancié/non regroupé, une interaction incomplète, une transformation hors limites ou une liaison
de script vide est refusée avant application avec un message localisé. Lorsque **Exiger cette
fiche au runtime** est inactif, une extension absente garde le fallback compatible ; lorsqu’il est
actif, le runtime produit `WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING`.

La lecture filtrée et typée ainsi que le dry-run agent restent ceux du contrat MCP canonique déjà
testé. L’interface n’écrit jamais directement `project.json` et ne crée donc pas de voie de mutation
parallèle.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-001` ; le décor statique possède maintenant sa preuve séparée
  UX1-F, tandis que les contrôles utilisable, identité/apparence et transformation ne valent pas
  encore preuve complète de `ORF-PLC-003`, `ORF-PLC-005` ou `ORF-PLC-006` ;
- l’import de nouveaux modèles reste le rôle de la bibliothèque d’assets ; cette fiche choisit parmi
  les manifestes déjà gouvernés du module ;
- les opérations détaillées de conteneur, santé, verrou et piège, le cycle OnUsed avancé, l’édition
  séparée icône/portrait, le parentage et l’animation de transformation seront qualifiés dans leurs
  routes dédiées ;
- l’application reste volontairement en mémoire jusqu’à l’enregistrement explicite ;
- les 717 routes, la parité des 66 fenêtres, l’export du Studio et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~R046_PlaceableFoundation"
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~R046_PlaceableValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~PlaceableFoundationTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore --nologo `
  --filter "FullyQualifiedName~Placeable_foundation_resource"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-e-blueprint -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-e-blueprint -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-e/blueprint-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-e-interaction -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-e/interaction-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-e-preview -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-e/preview-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-E, 49 fonctions disposent d’une preuve Studio spécifique ; les 668 autres lignes
restent explicitement `planned`.
