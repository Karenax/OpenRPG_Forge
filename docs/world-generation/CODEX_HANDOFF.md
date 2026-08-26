# Handoff Codex — WorldGeneration qualité

## État en une minute

Le socle WorldGeneration a été fusionné dans `main` au commit :

```text
b0a519b1566c4c5dded9db7fe38c0cfc6d99399d
```

Le travail de qualification et d'amélioration visuelle continue sur :

```text
feat/worldgen-quality-qualification
```

Le système fournit déjà recettes versionnées, dry-run, plans déterministes, jobs persistants, audit,
annulation, régénération partielle, adaptateurs Terrain3D/Graph PCG et fallback natif. Il ne fournit
pas encore un monde artistique de production : les graphes PCG attendus manquent, la qualification
réelle des addons sous Godot 4.7.1 reste à faire et la promotion canonique n'existe pas encore.

La première tranche de cette branche conserve l'enum sérialisé `PcgGodot`, mais distingue le
fournisseur concret par `providerKey` :

```text
terrain3d
godot-flow
gf-pcgodot
openrpg-native
```

Cette distinction est nécessaire pour comparer les implémentations sans casser les modules existants.

## Mission suivante

Lire et exécuter :

```text
docs/world-generation/CODEX_EXECUTION_PLAN.md
```

Le prochain lot prioritaire est la qualification réelle de Terrain3D et Godot Flow dans une fixture
Godot 4.7.1, suivie de la carte de référence et de son harness de mesure. Ne commence pas par intégrer
un nouvel addon.

## Documents de décision

- `docs/world-generation/CODEX_EXECUTION_PLAN.md`
- `docs/world-generation/QUALITY_BENCHMARK.md`
- `docs/world-generation/BACKEND_QUALIFICATION.md`
- `docs/world-generation/README.md`
- `docs/ADR/ADR-0050-governed-world-generation-orchestration.md`

## Fichiers importants

| Zone | Fichiers |
|---|---|
| Domaine | `src/Forge.Domain/WorldGeneration/WorldGenerationRecipe.cs`, `WorldGenerationRecipeCatalog.cs` |
| Application | `src/Forge.Application/WorldGeneration/` |
| Persistance | `src/Forge.ModuleFormat/Serialization/WorldGeneration*`, `src/Forge.ModuleFormat/Workspaces/WorldGeneration*` |
| Adaptateurs | `src/Forge.EngineAdapter/WorldGeneration/` |
| Identité fournisseurs | `WorldGenerationProviderKeys.cs`, `WorldGenerationBackendCapabilityProbe.cs` |
| MCP | `src/Forge.Mcp/Projects/ForgeProjectAgentService.WorldGeneration.cs`, `apps/Forge.McpHost/ForgeWorldGenerationMcpTools.cs` |
| Godot | `integrations/godot/addons/openrpg_world_generation/` |
| Registre graphes | `integrations/godot/addons/openrpg_world_generation/graph_registry.json` |
| Dépendances | `third_party/world-generation.dependencies.json`, `scripts/Install-WorldGenerationDependencies.ps1` |
| Tests | `tests/*/WorldGeneration/`, `tests/Godot/WorldGenerationNativeSmoke.gd` |

## Invariants à préserver

- Domain et Application ne référencent jamais Godot, Terrain3D ou un fournisseur Graph PCG.
- Le MCP orchestre des contrats bornés ; aucun GDScript arbitraire n'est accepté.
- Toute mutation reste previewée, révisionnée, permissionnée et auditée.
- Recipe et plan fingerprints restent canoniques et vérifiés des deux côtés du bridge.
- Les fichiers sont confinés au workspace.
- Le fallback natif reste disponible pour les contrats et la CI.
- Les retouches protégées ne sont pas remplacées.
- `AreaWorldProfile` reste canonique jusqu'à une promotion explicite.
- Un seul fournisseur possède une couche de production.
- Aucun gain visuel n'est annoncé sans benchmark et captures normalisées.

## Tests ciblés

```powershell
dotnet restore OpenRPGForge.sln --locked-mode
dotnet test tests/Forge.EngineAdapter.Tests/Forge.EngineAdapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
```

Puis les gates indiqués par `AGENTS.md`, le formatage, le build complet, les tests complets et le graphe
d'architecture.

## Limites à ne pas masquer

1. Les graphes artistiques de production ne sont pas livrés.
2. Les étapes natives hors terrain restent principalement des ancres déterministes.
3. Terrain3D et Godot Flow n'ont pas encore été qualifiés ensemble dans une CI réelle sous 4.7.1.
4. Le worker ne promeut pas automatiquement la scène vers le module canonique.
5. Navigation et reachability ne sont pas encore prouvées sur la carte de référence complète.
6. Les fournisseurs concurrents ne sont pas encore comparés avec les mêmes assets et la même seed.

Ne marque aucune limite comme résolue sur la seule base d'un document, d'un marker de scène ou d'un
test de présence de plugin.
