# Handoff Codex — WorldGeneration qualité

## État en une minute

Le socle WorldGeneration a été fusionné dans `main` au commit :

```text
b0a519b1566c4c5dded9db7fe38c0cfc6d99399d
```

La première tranche de qualification des fournisseurs a été intégrée dans `main` au commit :

```text
f68a8547d7a1856ccac5891206d702425da20597
```

Le système fournit déjà recettes versionnées, dry-run, plans déterministes, jobs persistants, audit,
annulation, régénération partielle, adaptateurs Terrain3D/Graph PCG et fallback natif.

Dans l'arbre de travail courant fondé sur `537897b21e35453b90661ccc990b0fdd8a1b027a`, la tranche
suivante est implémentée mais pas publiée : installateur/reçu schema 2, qualification réelle Windows
de Terrain3D avec Godot Flow et GF-PCGODOT sous Godot 4.7.1, proxy technique 2 km, comparaison isolée,
preuve de navigation/collision/sauvegarde/réouverture et promotion canonique MCP atomique/réversible.
Elle ne fournit toujours pas un monde artistique de production : les graphes et assets finalisés
manquent, et les gates Linux/export/revue humaine restent ouvertes.

Cette tranche conserve l'enum sérialisé `PcgGodot`, mais distingue le
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

Exécuter le job addon Linux configuré dans `.github/workflows/ci.yml`, qualifier les exports
provider-free Windows/Linux, puis construire un vrai module extérieur artistique avant de décider le
fournisseur Graph PCG. Ne pas intégrer ProtonScatter ou Road Generator avant d'avoir mesuré une
limite concrète du fournisseur baseline.

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
| Promotion | `src/Forge.Application/WorldGeneration/PromoteWorldGeneration.cs`, `src/Forge.ModuleFormat/Workspaces/WorldGenerationPromotionStore.cs` |
| Qualification | `scripts/Invoke-WorldGenerationAddonQualification.ps1`, `scripts/Compare-WorldGenerationAddonQualifications.ps1` |
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
- Une promotion initiale exige un plan complet ; un remplacement exige le SHA-256 canonique observé.
- La scène canonique ne conserve aucune référence à Terrain3D ou au fournisseur Graph PCG.
- Un seul fournisseur possède une couche de production.
- Aucun gain visuel n'est annoncé sans benchmark et captures normalisées.

## Tests ciblés

```powershell
dotnet restore OpenRPGForge.sln --locked-mode
dotnet test tests/Forge.EngineAdapter.Tests/Forge.EngineAdapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
./scripts/Test-WorldGenerationDependencyInstaller.ps1
./scripts/Invoke-WorldGenerationAddonQualification.ps1 -GraphProvider godot-flow -QualificationProfile reference-2km -RunId <run-id>
./scripts/Invoke-WorldGenerationAddonQualification.ps1 -GraphProvider gf-pcgodot -QualificationProfile reference-2km -RunId <run-id>
./scripts/Compare-WorldGenerationAddonQualifications.ps1 -BaselineRun <godot-flow-run> -CandidateRun <gf-pcgodot-run> -QualificationProfile reference-2km
```

Puis les gates indiqués par `AGENTS.md`, le formatage, le build complet, les tests complets et le graphe
d'architecture.

## Limites à ne pas masquer

1. Les graphes artistiques de production ne sont pas livrés.
2. Les étapes natives hors terrain restent principalement des ancres déterministes.
3. La qualification réelle Windows est locale ; le job Linux est configuré mais n'a pas encore été
   exécuté avec succès sur GitHub dans cette tranche.
4. Les exports provider-free Windows/Linux ne sont pas qualifiés.
5. La navigation du proxy technique atteint 131 points, mais aucun vrai module artistique n'a encore
   prouvé toutes les entrées et zones héroïques du benchmark.
6. Les fournisseurs concurrents ont une parité exacte sur le proxy et les cinq captures techniques ;
   scoring visuel aveugle et effort de correction manuelle n'ont pas été mesurés.
7. La promotion canonique est volontairement explicite et ne remplace pas la revue humaine.

Ne marque aucune limite comme résolue sur la seule base d'un document, d'un marker de scène ou d'un
test de présence de plugin.
