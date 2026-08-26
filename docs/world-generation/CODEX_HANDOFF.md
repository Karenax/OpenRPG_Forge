# Handoff Codex — WorldGeneration

## État en une minute

Le système fournit une orchestration MCP gouvernée et indépendante du moteur : recettes versionnées,
dry-run et application transactionnelle, previews déterministes, plans à empreinte stable, sélection
partielle d'étapes, jobs JSON persistants, audit, annulation et fallback Godot natif. Terrain3D et
Godot Flow sont facultatifs, épinglés et installés par script.

Le fallback testé sous Godot 4.7.1 produit un terrain déterministe avec maillage et collision. Les
autres étapes natives restent surtout des conteneurs/ancres. Les graphes PCG artistiques ne sont pas
livrés et le worker ne sauvegarde pas encore la scène générée dans le modèle canonique du module.

## Fichiers importants

| Zone | Fichiers |
|---|---|
| Domaine | `src/Forge.Domain/WorldGeneration/WorldGenerationRecipe.cs`, `WorldGenerationRecipeCatalog.cs` |
| Application | `src/Forge.Application/WorldGeneration/WorldGenerationContracts.cs`, `WorldGenerationPlanner.cs`, `SetWorldGenerationRecipes.cs`, `WorldGenerationJob.cs` |
| Persistance | `src/Forge.ModuleFormat/Serialization/WorldGeneration*JsonSerializer.cs`, `src/Forge.ModuleFormat/Workspaces/WorldGeneration*Store.cs`, `WorldGenerationWorkspacePath.cs` |
| Adaptateurs | `src/Forge.EngineAdapter/WorldGeneration/` |
| MCP | `src/Forge.Mcp/Projects/ForgeProjectAgentService.WorldGeneration.cs`, `WorldGenerationContracts.cs`, `apps/Forge.McpHost/ForgeWorldGenerationMcpTools.cs` |
| Godot | `integrations/godot/addons/openrpg_world_generation/` |
| Dépendances | `third_party/world-generation.dependencies.json`, `scripts/Install-WorldGenerationDependencies.ps1` |
| Décision | `docs/ADR/ADR-0050-governed-world-generation-orchestration.md` |

## Invariants à préserver

- Domain et Application ne référencent jamais Godot, Terrain3D ou Godot Flow.
- Le MCP orchestre ; il ne reçoit ni n'exécute du GDScript libre.
- Toute mutation de recette passe par preview/diff, permission, révisions et application du plan.
- Une exécution n'est mise en file qu'à partir d'un plan persisté et encore cohérent avec la recette.
- Recipe et plan fingerprints sont des SHA-256 minuscules ; l'empreinte du plan est vérifiée en C#
  et dans le worker GDScript.
- Les opérations backend sont allowlistées des deux côtés du bridge.
- Les fichiers restent sous le workspace ; traversées, chemins absolus, liens et jonctions sont refusés.
- Le backend natif reste exécutable sans addon externe.
- Les retouches humaines protégées ne sont pas remplacées par une régénération.
- `AreaWorldProfile` reste canonique : une scène générée est dérivée tant qu'aucune commande de
  promotion explicite n'existe.

## Tests ciblés

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj -c Release --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj -c Release --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.EngineAdapter.Tests/Forge.EngineAdapter.Tests.csproj -c Release --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj -c Release --filter FullyQualifiedName~WorldGeneration

$godot = (Get-Command godot* | Where-Object Source -Like '*console.exe' | Select-Object -First 1).Source
& $godot --headless --check-only --script integrations/godot/addons/openrpg_world_generation/openrpg_world_generation_backend.gd
& $godot --headless --check-only --script integrations/godot/addons/openrpg_world_generation/world_generation_job_runner.gd
& $godot --headless --script tests/Godot/WorldGenerationNativeSmoke.gd

./scripts/Install-WorldGenerationDependencies.ps1 -GodotProjectPath ./apps/Forge.Toolset -WhatIf
```

Puis appliquer la vérification ciblée issue du graphe, régénérer le graphe si une source indexée a
changé, et terminer par les gates demandés dans `AGENTS.md` et `PROJECT_CONTEXT.md`.

## Scénarios couverts

- identité et empreinte de recette déterministes ;
- empreinte du plan indépendante des GUID de suivi ;
- régénération partielle triée et chaînée ;
- rejet de nombres non finis, chemins de nœud dangereux et couples backend/étape incompatibles ;
- fallback Application vers l'adaptateur natif ;
- persistance de la révision de recette, du plan, du job et de l'audit ;
- rejet d'un plan altéré ;
- réouverture du service MCP avec recette et job conservés ;
- protocole C#/GDScript partagé, rejet d'opération altérée, maillage/collision et fallback sous Godot.

## Limites et prochaines étapes

1. Ajouter des graphes PCG génériques ou propres à un module avec fixtures et budget de performance.
2. Concevoir une commande explicite qui promeut une génération validée vers les contrats canoniques
   sans écraser `AreaWorldProfile` ni les retouches humaines.
3. Sauvegarder atomiquement la scène dérivée et les reçus d'artefacts après succès du worker.
4. Remplacer les ancres natives par des générateurs complets pour hydrologie, routes, bâtiments,
   gameplay et navigation, avec collision et reachability réelles.
5. Qualifier Terrain3D `v1.0.2-stable` et Godot Flow au commit épinglé dans un projet Godot 4.7.1
   jetable, puis sur une vraie zone extérieure.
6. Persister l'inverse des plans de mutation de recette si un revert après redémarrage devient requis.

Ne marquer aucune de ces limites comme terminée sur la seule base d'un plan ou d'un marker de scène.
