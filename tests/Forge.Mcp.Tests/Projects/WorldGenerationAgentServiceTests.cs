using Forge.Application.WorldGeneration;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.WorldGeneration;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class WorldGenerationAgentServiceTests
{
    private static readonly Guid ProductionSourceAreaId =
        Guid.Parse("f0530000-0000-4000-8000-000000000090");

    [Fact]
    public void RecipePlanApplyExecutionQueueCancelAndRevertAreGovernedAndPersistent()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var source = workspace.Project.GetWorldGenerationRecipes();
        var recipe = WorldGenerationRecipe.CreateDefault(project.Areas[0].Id.Value, "MCP valley", 20260826)
            .WithApproval(true, "test:mcp-approved");
        var catalog = new WorldGenerationRecipeCatalog(
            Guid.Parse("f0530000-0000-4000-8000-000000000001"), 1, [recipe], "test:mcp");

        var planned = workspace.Project.PlanWorldGenerationRecipes(new(
            1,
            source.RecipeRevision,
            source.ProjectRevision,
            catalog,
            AssistantPermissionLevel.ApplyApprovedPlan));
        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.False(planned.Applied);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory, WorldGenerationRecipeStore.RelativePath)));

        var applied = workspace.Project.ApplyWorldGenerationRecipePlan(planned.PlanId!);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.True(File.Exists(Path.Combine(workspace.RootDirectory, WorldGenerationRecipeStore.RelativePath)));

        var preview = workspace.Project.PreviewWorldGenerationExecution(new(
            1,
            source.ProjectRevision,
            applied.NewRecipeRevision!.Value,
            recipe.Id.ToString("D")));
        Assert.True(preview.Succeeded, string.Join(" | ", preview.Diagnostics.Select(item => item.Message)));
        Assert.True(preview.Plan!.ReadyToApply);
        Assert.False(preview.Queued);

        var queued = workspace.Project.QueueWorldGenerationExecutionPlan(preview.Plan.PlanId.ToString("D"));
        Assert.True(queued.Succeeded);
        Assert.True(queued.Queued);
        Assert.Equal(WorldGenerationJobStatus.Queued, queued.JobStatus);
        var job = Assert.Single(workspace.Project.GetWorldGenerationJobs());
        Assert.Equal(preview.Plan.PlanId.ToString("D"), job.PlanId);
        Assert.Equal(preview.Plan.PlanFingerprint, job.PlanFingerprint);
        Assert.Equal("queued", Assert.Single(job.Audit).Action);

        var reopened = ForgeProjectAgentService.Open(workspace.RootDirectory).Service!;
        Assert.Equal(applied.NewRecipeRevision.Value,
            reopened.GetWorldGenerationRecipes().RecipeRevision);
        var reopenedJob = Assert.Single(reopened.GetWorldGenerationJobs());
        Assert.Equal(job.JobId, reopenedJob.JobId);
        Assert.Equal(job.PlanFingerprint, reopenedJob.PlanFingerprint);
        var idempotentQueue = reopened.QueueWorldGenerationExecutionPlan(preview.Plan.PlanId.ToString("D"));
        Assert.True(idempotentQueue.Succeeded);
        Assert.True(idempotentQueue.Queued);
        Assert.Equal(WorldGenerationJobStatus.Queued, idempotentQueue.JobStatus);

        var cancelled = workspace.Project.CancelWorldGenerationJob(preview.Plan.PlanId.ToString("D"));
        Assert.True(cancelled.Succeeded);
        Assert.Equal(WorldGenerationJobStatus.Cancelled, cancelled.JobStatus);

        var reverted = workspace.Project.RevertWorldGenerationRecipePlan(planned.PlanId!);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.Empty(workspace.Project.GetWorldGenerationRecipes().Catalog.Recipes);
    }

    [Fact]
    public void SuccessfulJobCanBePreviewedPromotedReopenedAndReverted()
    {
        using var workspace = new TestWorkspace(AddProductionSource);
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var source = workspace.Project.GetWorldGenerationRecipes();
        var draft = WorldGenerationRecipe.CreateDefault(project.Areas[0].Id.Value,
            "Canonical MCP valley", 20260827);
        var recipe = new WorldGenerationRecipe(
                draft.Id, draft.SchemaVersion, draft.AreaId, draft.Title, draft.Seed, draft.Bounds,
                draft.Terrain, draft.Rivers, draft.Biomes, draft.Roads, draft.Settlements, draft.Scatter,
                draft.Gameplay, draft.Navigation, draft.Quality, draft.Backends,
                draft.ApprovedForGeneration, draft.Provenance, draft.Regeneration,
                new WorldGenerationArtDirectionProfile(
                    WorldGenerationVisualTier.Production, ProductionSourceAreaId, true, 3))
            .WithApproval(true, "test:mcp-promotion-approved");
        var catalog = new WorldGenerationRecipeCatalog(
            Guid.Parse("f0530000-0000-4000-8000-000000000011"), 1, [recipe],
            "test:mcp-promotion");
        var recipePlan = workspace.Project.PlanWorldGenerationRecipes(new(
            1, source.RecipeRevision, source.ProjectRevision, catalog,
            AssistantPermissionLevel.ApplyApprovedPlan));
        var recipeApplied = workspace.Project.ApplyWorldGenerationRecipePlan(recipePlan.PlanId!);
        var execution = workspace.Project.PreviewWorldGenerationExecution(new(
            1, source.ProjectRevision, recipeApplied.NewRecipeRevision!.Value, recipe.Id.ToString("D")));
        Assert.True(workspace.Project.QueueWorldGenerationExecutionPlan(
            execution.Plan!.PlanId.ToString("D")).Succeeded);
        var job = WorldGenerationJobStore.Load(workspace.RootDirectory, execution.Plan.PlanId).Job!
            .WithStatus(WorldGenerationJobStatus.Succeeded, DateTimeOffset.UtcNow);
        Assert.True(WorldGenerationJobStore.Save(workspace.RootDirectory, job).Succeeded);

        var scene = "[gd_scene format=3]\n\n[node name=\"OpenRPGBakedWorld\" type=\"Node3D\"]\n";
        var sceneRelative = WorldGenerationPromotionStore.DerivedSceneRelativePath(job.PlanId);
        var scenePath = Path.Combine(workspace.RootDirectory,
            sceneRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);
        File.WriteAllText(scenePath, scene);
        var sceneSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(scene))).ToLowerInvariant();
        var providers = job.Plan.Steps.Select(step => new WorldGenerationProviderEvidence(
            step.Stage, step.Backend,
            step.Backend == WorldGenerationBackend.PcgGodot ? "godot-flow" :
                step.Backend == WorldGenerationBackend.Terrain3D ? "terrain3d" : "openrpg-native",
            "test-version", new string('a', 64))).ToArray();
        const int surfaceSamplesPerAxis = 257;
        var surfaceHeights = new float[surfaceSamplesPerAxis * surfaceSamplesPerAxis];
        AreaEnvironmentOccupancyCell[] occupancyCells = [new("vegetation", "biomes", 0, 0, 24)];
        var terrainSurface = new WorldGenerationTerrainSurfaceEvidence(
            1, 2048, 2048, surfaceSamplesPerAxis, surfaceSamplesPerAxis, 10, surfaceHeights,
            TerrainCellDiagonal.Main,
            WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
                2048, 2048, surfaceSamplesPerAxis, surfaceSamplesPerAxis, 10, surfaceHeights,
                TerrainCellDiagonal.Main));
        var environmentOccupancy = new WorldGenerationEnvironmentOccupancyEvidence(
            1, 2048, 2048, 4, occupancyCells,
            WorldGenerationSpatialEvidenceFingerprint.ComputeEnvironment(2048, 2048, 4, occupancyCells));
        var report = new WorldGenerationValidationReport(
            1, job.JobId, job.PlanId, recipe.Id, recipe.AreaId, job.ProjectRevision, job.RecipeRevision,
            job.RecipeFingerprint, job.Plan.PlanFingerprint, sceneSha, new string('b', 64),
            new string('c', 64), true, true, true, true, providers, [],
            terrainSurface, environmentOccupancy);
        Assert.True(WorldGenerationPromotionStore.SaveValidationReport(
            workspace.RootDirectory, report).Succeeded);

        var planned = workspace.Project.PlanWorldGenerationPromotion(new(
            1,
            source.ProjectRevision,
            recipeApplied.NewRecipeRevision.Value,
            job.PlanId.ToString("D"),
            null,
            "human-forward-plus:mcp-test",
            AssistantPermissionLevel.ApplyApprovedPlan));
        Assert.True(planned.Succeeded,
            string.Join(" | ", planned.Diagnostics.Select(item => item.Message)));
        Assert.False(planned.Applied);
        Assert.NotNull(planned.Diff);
        Assert.NotNull(planned.Inverse);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory,
            planned.CanonicalSceneRelativePath!.Replace('/', Path.DirectorySeparatorChar))));

        var applied = workspace.Project.ApplyWorldGenerationPromotion(planned.PromotionPlanId!);

        Assert.True(applied.Succeeded,
            string.Join(" | ", applied.Diagnostics.Select(item => item.Message)));
        Assert.True(applied.Applied);
        Assert.Equal(source.ProjectRevision + 1, applied.NewProjectRevision);
        var loaded = workspace.Project.GetCanonicalWorldGenerationPromotion(recipe.AreaId.ToString("D"));
        Assert.True(loaded.Succeeded);
        Assert.Equal(sceneSha, loaded.Promotion!.CanonicalSceneSha256);
        var reopenedProject = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.Contains(applied.CanonicalSceneRelativePath,
            reopenedProject.Areas.Single(item => item.Id.Value == recipe.AreaId)
                .World.Loading.RequiredResources);
        var reopenedWorld = reopenedProject.Areas.Single(item => item.Id.Value == recipe.AreaId).World;
        Assert.Equal(terrainSurface.Fingerprint, reopenedWorld.PlacementSurface!.SourceFingerprint);
        Assert.Equal(environmentOccupancy.Fingerprint,
            reopenedWorld.EnvironmentOccupancy!.SourceFingerprint);
        var reopenedService = ForgeProjectAgentService.Open(workspace.RootDirectory).Service!;
        var unboundedVegetation = reopenedService.FindTerrainPlacementCandidates(new(
            1, recipe.AreaId.ToString("D"), 8, 8, 100, 100, 32, 8, .35f,
            "vegetation", 1, 64, 0, 3));
        Assert.False(unboundedVegetation.Succeeded);
        Assert.Equal("assistant.terrain_candidates.vegetation_density_missing",
            Assert.Single(unboundedVegetation.Diagnostics).Code);
        var placement = reopenedService.FindTerrainPlacementCandidates(new(
            1, recipe.AreaId.ToString("D"), 8, 8, 100, 100, 32, 8, .35f,
            "vegetation", 0, 64, 0, 3, 0));
        Assert.True(placement.Succeeded,
            string.Join(" | ", placement.Diagnostics.Select(item => item.Message)));
        Assert.Equal(terrainSurface.Fingerprint, placement.SurfaceFingerprint);
        Assert.Equal(environmentOccupancy.Fingerprint, placement.EnvironmentFingerprint);
        Assert.NotEmpty(placement.Candidates);
        Assert.All(placement.Candidates, item => Assert.Equal(0, item.FootprintEnvironmentInstances));

        var reverted = workspace.Project.RevertWorldGenerationPromotion(planned.PromotionPlanId!);

        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        Assert.False(File.Exists(Path.Combine(workspace.RootDirectory,
            applied.CanonicalSceneRelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        var reopenedAfterRevert = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        Assert.DoesNotContain(applied.CanonicalSceneRelativePath,
            reopenedAfterRevert.Areas.Single(item => item.Id.Value == recipe.AreaId)
                .World.Loading.RequiredResources);
    }

    private static ProjectDefinition AddProductionSource(ProjectDefinition project)
    {
        var target = project.Areas[0];
        var generatedDimensions = new AreaDimensions(2048, 2048);
        target = new AreaDefinition(
            target.Id,
            target.Slug,
            target.DisplayName,
            target.Entities,
            target.Kind,
            generatedDimensions,
            target.Portals,
            target.SchemaVersion,
            target.Navigation,
            target.Environment,
            target.Authoring,
            AreaWorldProfile.CreateDefault(target.Id, target.Kind, generatedDimensions),
            target.RuntimePolicies,
            target.Foundation,
            target.Playtest);
        var sourceId = AreaId.From(ProductionSourceAreaId);
        var source = new AreaDefinition(
            sourceId,
            "production-art-source",
            "Production art source",
            [],
            AreaKind.Exterior,
            new AreaDimensions(512, 512));
        var sourceWorld = source.World;
        var materialId = Guid.Parse("f0530000-0000-4000-8000-000000000091");
        var terrain = new TerrainDefinition(
            sourceWorld.Terrain.Id,
            TerrainDefinition.CurrentSchemaVersion,
            TerrainProviderKind.Heightmap,
            512,
            512,
            2,
            2,
            2,
            7,
            20,
            [0, 0, 0, 0],
            [new TerrainMaterialLayer(materialId, "Meadow", "#5f8345", "assets/ground.png", 4)],
            [],
            string.Empty,
            string.Empty,
            string.Empty,
            "test:production-source");
        var biomes = new[]
        {
            new BiomeLayerDefinition(Guid.Parse("f0530000-0000-4000-8000-000000000092"),
                "Canopy", materialId, -512, 512, 0, 30, 0, 1, .2f, "asset:tree",
                ["tree"], [], 30, "test:production-source"),
            new BiomeLayerDefinition(Guid.Parse("f0530000-0000-4000-8000-000000000093"),
                "Shrubs", materialId, -512, 512, 0, 30, 0, 1, .3f, "asset:shrub",
                ["shrub"], [], 20, "test:production-source"),
            new BiomeLayerDefinition(Guid.Parse("f0530000-0000-4000-8000-000000000094"),
                "Meadow grass", materialId, -512, 512, 0, 30, 0, 1, .6f, "asset:grass",
                ["ground-cover"], [], 10, "test:production-source"),
        };
        source = source.WithWorld(new AreaWorldProfile(
            sourceWorld.Id,
            sourceWorld.SchemaVersion,
            terrain,
            biomes,
            sourceWorld.Features,
            sourceWorld.VolumetricNavigation,
            sourceWorld.FlightRoutes,
            sourceWorld.Loading,
            "test:production-source"));
        return new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints,
            [target, .. project.Areas.Skip(1), source],
            project.Dialogues,
            project.Quests);
    }
}
