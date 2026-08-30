using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Application.WorldGeneration;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldGeneration;

namespace Forge.Application.Tests.WorldGeneration;

public sealed class WorldGenerationWorkflowTests
{
    private static readonly Guid ProductionSourceAreaId =
        Guid.Parse("f0500000-0000-4000-8000-000000000090");

    [Fact]
    public void SpatialEvidenceFingerprintMatchesTheSharedGodotMicrounitVector()
    {
        float[] heights = [0, .25f, -.5f, 1];
        AreaEnvironmentOccupancyCell[] cells =
        [
            new("vegetation", "biomes", 8, -4, 12),
            new("scatter", "scatter", -2, 4, 3),
        ];

        Assert.Equal("4f223fa87aa1abead044938b4adaac046206f3b8638ec742c2b9a3ec09b7569a",
            WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
                64, 64, 2, 2, 10, heights, TerrainCellDiagonal.Main));
        Assert.Equal("ce30d56f66e0176741ec618333b01dd9114c07967afb3aba7c3ea0f8b3a008db",
            WorldGenerationSpatialEvidenceFingerprint.ComputeEnvironment(64, 64, 4, cells));
    }

    [Fact]
    public void DefaultRecipeIdentityAndFingerprintAreDeterministic()
    {
        var areaId = Guid.Parse("f0500000-0000-4000-8000-000000000099");

        var first = WorldGenerationRecipe.CreateDefault(areaId, "Deterministic valley", 917);
        var second = WorldGenerationRecipe.CreateDefault(areaId, "Deterministic valley", 917);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Rivers.Select(item => item.Id), second.Rivers.Select(item => item.Id));
        Assert.Equal(first.Biomes.Select(item => item.Id), second.Biomes.Select(item => item.Id));
        Assert.Equal(first.Fingerprint(), second.Fingerprint());
    }

    [Fact]
    public void RecipeFingerprintIsStableAcrossInputCollectionOrdering()
    {
        var areaId = Guid.Parse("f0500000-0000-4000-8000-000000000001");
        var first = Recipe(areaId, reverse: false);
        var second = Recipe(areaId, reverse: true);

        Assert.Equal(first.Fingerprint(), second.Fingerprint());
        Assert.Empty(WorldGenerationRecipeValidator.Validate(first));
        Assert.Equal(64, first.Fingerprint().Length);
    }

    [Fact]
    public void RecipeFingerprintPreservesStringCollectionBoundaries()
    {
        var source = Recipe(Guid.Parse("f0500000-0000-4000-8000-000000000001"));
        var joined = Copy(source, biomes:
            [source.Biomes[0] with { AssetTags = ["tree,rock"] }, .. source.Biomes.Skip(1)]);
        var separate = Copy(source, biomes:
            [source.Biomes[0] with { AssetTags = ["tree", "rock"] }, .. source.Biomes.Skip(1)]);

        Assert.NotEqual(joined.Fingerprint(), separate.Fingerprint());
    }

    [Fact]
    public void PlannerBuildsDeterministicDependencyOrderedBackendPlan()
    {
        var recipe = Recipe(Guid.Parse("f0500000-0000-4000-8000-000000000001"));
        var transactionId = Guid.Parse("f0500000-0000-4000-8000-000000000030");
        var planId = Guid.Parse("f0500000-0000-4000-8000-000000000031");

        var first = new WorldGenerationPlanner().Build(recipe, 7, new(2), transactionId, planId);
        var second = new WorldGenerationPlanner().Build(recipe, 7, new(2), transactionId, planId);

        Assert.True(first.ReadyToApply);
        Assert.Equal(first.RecipeFingerprint, second.RecipeFingerprint);
        Assert.Equal(
            first.Steps.Select(item => (item.Order, item.Stage, item.Backend, item.Operation)),
            second.Steps.Select(item => (item.Order, item.Stage, item.Backend, item.Operation)));
        Assert.Equal(Enum.GetValues<WorldGenerationStage>(), first.Steps.Select(item => item.Stage));
        Assert.Equal(WorldGenerationBackend.Terrain3D, first.Steps[0].Backend);
        Assert.Equal(WorldGenerationBackend.PcgGodot,
            first.Steps.Single(item => item.Stage == WorldGenerationStage.Settlements).Backend);
        Assert.Equal("32", first.Steps.Single(item => item.Stage == WorldGenerationStage.Settlements)
            .Parameters["buildingCount"]);
        Assert.Equal(WorldGenerationBackend.GodotNative,
            first.Steps.Single(item => item.Stage == WorldGenerationStage.Validation).Backend);
        Assert.Empty(first.Steps[0].DependsOn);
        Assert.All(first.Steps.Skip(1), step => Assert.Equal([step.Order - 1], step.DependsOn));
        Assert.Equal(first.PlanFingerprint, second.PlanFingerprint);
        Assert.Equal(first.PlanFingerprint, WorldGenerationExecutionPlanFingerprint.Compute(first));
    }

    [Fact]
    public void PlannerSupportsDeterministicPartialRegeneration()
    {
        var recipe = Recipe(Guid.Parse("f0500000-0000-4000-8000-000000000001"));
        var planner = new WorldGenerationPlanner();
        var partial = planner.Build(recipe, 7, new(2), Guid.NewGuid(), Guid.NewGuid(),
            [WorldGenerationStage.Validation, WorldGenerationStage.Terrain, WorldGenerationStage.Terrain]);
        var equivalent = planner.Build(recipe, 7, new(2), Guid.NewGuid(), Guid.NewGuid(),
            [WorldGenerationStage.Terrain, WorldGenerationStage.Validation]);
        var complete = planner.Build(recipe, 7, new(2), Guid.NewGuid(), Guid.NewGuid());

        Assert.True(partial.ReadyToApply);
        Assert.Equal([WorldGenerationStage.Terrain, WorldGenerationStage.Validation],
            partial.Steps.Select(item => item.Stage));
        Assert.Empty(partial.Steps[0].DependsOn);
        Assert.Equal([0], partial.Steps[1].DependsOn);
        Assert.Equal(partial.PlanFingerprint, equivalent.PlanFingerprint);
        Assert.NotEqual(partial.PlanFingerprint, complete.PlanFingerprint);
    }

    [Fact]
    public void PlanFingerprintMatchesGodotProtocolVector()
    {
        var step = new WorldGenerationExecutionStep(
            0,
            WorldGenerationStage.Terrain,
            WorldGenerationBackend.GodotNative,
            "godotnative.terrain",
            new Dictionary<string, string>(),
            []);

        var fingerprint = WorldGenerationExecutionPlanFingerprint.Compute(
            3,
            new(2),
            Guid.Parse("f0540000-0000-4000-8000-000000000001"),
            Guid.Parse("f0540000-0000-4000-8000-000000000002"),
            20260826,
            new string('a', 64),
            true,
            [step]);

        Assert.Equal("b32a985db8eb0dadb5befde7dce8191753e4a73853cd992696ce21c8dd37186b",
            fingerprint);
    }

    [Fact]
    public void RecipeValidationRejectsNonFiniteUnsafeAndIncompatibleInputs()
    {
        var source = Recipe(Guid.Parse("f0500000-0000-4000-8000-000000000001"));
        var invalid = Copy(source,
            bounds: new(double.NaN, source.Bounds.DepthMeters, source.Bounds.MaxElevationMeters),
            backends: new Dictionary<WorldGenerationStage, WorldGenerationBackend>
            {
                [WorldGenerationStage.Gameplay] = WorldGenerationBackend.Terrain3D,
            },
            regeneration: new(true, ["../ManualCastle"]));

        var diagnostics = WorldGenerationRecipeValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == "world_generation.bounds_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world_generation.backend_stage_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world_generation.protected_path_invalid");
    }

    [Fact]
    public async Task OrchestratorUsesNativeAdapterWhenRequestedBackendCannotExecute()
    {
        var recipe = Recipe(Guid.Parse("f0500000-0000-4000-8000-000000000001"));
        var plan = new WorldGenerationPlanner().Build(recipe, 7, new(2), Guid.NewGuid(), Guid.NewGuid(),
            [WorldGenerationStage.Terrain]);
        var requested = new RecordingAdapter(WorldGenerationBackend.Terrain3D, canExecute: false);
        var fallback = new RecordingAdapter(WorldGenerationBackend.GodotNative, canExecute: true);
        var context = new WorldGenerationExecutionContext("/workspace", recipe, plan, true,
            new MemoryArtifactSink());

        var result = await new WorldGenerationOrchestrator([requested, fallback]).ExecuteAsync(context);

        Assert.True(result.Succeeded);
        Assert.Empty(requested.Executed);
        Assert.Single(fallback.Executed);
        Assert.Equal(WorldGenerationStage.Terrain, fallback.Executed[0].Stage);
    }

    [Fact]
    public void RecipeMutationRequiresMatchingDryRunThenSupportsUndoRedo()
    {
        var context = new EntityMutationTestContext();
        var recipe = Recipe(context.AreaId.Value);
        var catalog = new WorldGenerationRecipeCatalog(
            Guid.Parse("f0500000-0000-4000-8000-000000000040"),
            WorldGenerationRecipeCatalog.CurrentSchemaVersion,
            [recipe],
            "test:world-generation");
        var history = new WorldGenerationRecipeCommandHistory();
        var handler = new SetWorldGenerationRecipesHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var transactionId = Guid.Parse("f0500000-0000-4000-8000-000000000041");
        var planId = Guid.Parse("f0500000-0000-4000-8000-000000000042");
        SetWorldGenerationRecipesCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            planId,
            history.State.Revision,
            catalog,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);

        var refused = handler.Handle(history.State, Command(false), context.State.Project);
        var preview = handler.Handle(history.State, Command(true), context.State.Project);
        var applied = handler.Handle(history.State, Command(false), context.State.Project);

        Assert.Equal(SetWorldGenerationRecipesHandler.PlanNotPreviewedCode, Assert.Single(refused.Diagnostics).Code);
        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        history.Record(applied);
        Assert.Single(history.State.Catalog.Recipes);
        Assert.True(history.Undo());
        Assert.Empty(history.State.Catalog.Recipes);
        Assert.True(history.Redo());
        Assert.Equal(recipe.Fingerprint(), Assert.Single(history.State.Catalog.Recipes).Fingerprint());
    }

    [Fact]
    public void CanonicalPromotionRequiresSuccessfulPreviewAndReferencesNeutralScene()
    {
        var context = new EntityMutationTestContext();
        var state = WithProductionSource(context.State);
        var recipe = Recipe(context.AreaId.Value);
        var recipeState = new WorldGenerationRecipeState(
            new WorldGenerationRecipeCatalog(
                Guid.Parse("f0500000-0000-4000-8000-000000000050"),
                WorldGenerationRecipeCatalog.CurrentSchemaVersion,
                [recipe],
                "test:promotion"),
            new WorldGenerationRecipeRevision(4));
        var plan = new WorldGenerationPlanner().Build(
            recipe,
            state.Revision.Value,
            recipeState.Revision,
            Guid.Parse("f0500000-0000-4000-8000-000000000051"),
            Guid.Parse("f0500000-0000-4000-8000-000000000052"));
        var job = WorldGenerationJob.Queue(plan, recipe, EntityMutationTestContext.Timestamp)
            .WithStatus(WorldGenerationJobStatus.Succeeded, EntityMutationTestContext.Timestamp.AddSeconds(1));
        var report = PromotionReport(job);
        var handler = new PromoteWorldGenerationHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var transactionId = Guid.Parse("f0500000-0000-4000-8000-000000000053");
        var promotionPlanId = Guid.Parse("f0500000-0000-4000-8000-000000000054");
        var canonicalPath = PromoteWorldGenerationHandler.CanonicalScenePath(recipe.AreaId);
        PromoteWorldGenerationCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            promotionPlanId,
            state.Revision,
            recipeState.Revision,
            job.PlanId,
            report,
            $"world/generation/artifacts/{job.PlanId:N}/derived-world.tscn",
            canonicalPath,
            null,
            "human-forward-plus:test",
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);

        var refused = handler.Handle(state, recipeState, job, Command(false));
        var preview = handler.Handle(state, recipeState, job, Command(true));
        var applied = handler.Handle(state, recipeState, job, Command(false));

        Assert.Equal(PromoteWorldGenerationHandler.PlanNotPreviewedCode,
            Assert.Single(refused.Command.Diagnostics).Code);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.NotNull(preview.Command.Patch);
        Assert.NotNull(preview.Command.Inverse);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        Assert.Equal(state.Revision.Next(), applied.Command.NewRevision);
        Assert.Contains(canonicalPath,
            applied.Command.State.Project.Areas.Single(item => item.Id == context.AreaId)
                .World.Loading.RequiredResources);
        var promotedWorld = applied.Command.State.Project.Areas.Single(item => item.Id == context.AreaId).World;
        Assert.NotNull(promotedWorld.PlacementSurface);
        Assert.True(promotedWorld.Terrain.Enabled);
        Assert.Equal(3, promotedWorld.Biomes.Count);
        Assert.Equal(2, promotedWorld.Features.Count(feature => feature.Kind == TerrainFeatureKind.Road));
        Assert.All(promotedWorld.Features.Where(feature => feature.Kind == TerrainFeatureKind.Road), feature =>
        {
            Assert.Equal("assets/road.png", feature.MaterialResource);
            Assert.All(feature.ControlPoints, point =>
                Assert.Equal(promotedWorld.GroundHeightAt(point.X, point.Z) + .06f, point.Y, 4));
        });
        Assert.Equal(1, applied.Promotion!.ProductionVisuals!.TexturedTerrainLayerCount);
        Assert.Equal(["canopy", "shrub", "ground-cover"],
            applied.Promotion.ProductionVisuals.VegetationStrata);
        Assert.Equal(0, applied.Promotion.ProductionVisuals.SpatialQuality.CameraSpawnCount);
        Assert.Equal(0, applied.Promotion.ProductionVisuals.SpatialQuality.GroundedFoundationCount);
        Assert.Equal(2, applied.Promotion.ProductionVisuals.ProductionFeatureCount);
        Assert.Equal(2, applied.Promotion.ProductionVisuals.TexturedRoadCount);
        Assert.Equal(3.5f, promotedWorld.GroundHeightAt(1024, -1024), 4);
        Assert.Equal(24, promotedWorld.EnvironmentOccupancy!.CountNear("vegetation", 0, 0, 64));
        Assert.Equal(report.ContentFingerprint, applied.Promotion.ContentFingerprint);
        Assert.Equal(report.TerrainSurface!.Fingerprint, applied.Promotion.TerrainSurface!.Fingerprint);
        Assert.Equal(report.EnvironmentOccupancy!.Fingerprint,
            applied.Promotion.EnvironmentOccupancy!.Fingerprint);
        Assert.Equal(recipe.Seed, applied.Promotion.Seed);
        Assert.Equal("godot-flow", applied.Promotion.Providers
            .Single(item => item.Stage == WorldGenerationStage.Biomes).ProviderKey);
        Assert.True(applied.Command.Audit.Applied);
    }

    [Fact]
    public void CanonicalPromotionRejectsStaleFailedOrUnacceptedEvidence()
    {
        var context = new EntityMutationTestContext();
        var recipe = Recipe(context.AreaId.Value);
        var recipeState = new WorldGenerationRecipeState(
            new WorldGenerationRecipeCatalog(Guid.NewGuid(), 1, [recipe], "test:promotion"),
            new WorldGenerationRecipeRevision(2));
        var plan = new WorldGenerationPlanner().Build(recipe, context.State.Revision.Value,
            recipeState.Revision, Guid.NewGuid(), Guid.NewGuid());
        var queued = WorldGenerationJob.Queue(plan, recipe, EntityMutationTestContext.Timestamp);
        var report = PromotionReport(queued);
        var handler = new PromoteWorldGenerationHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        PromoteWorldGenerationCommand Command(WorldGenerationJob job, WorldGenerationValidationReport evidence,
            ProjectRevision revision, string acceptance = "human-forward-plus:test") => new(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), revision, recipeState.Revision,
            job.PlanId, evidence, $"world/generation/artifacts/{job.PlanId:N}/derived-world.tscn",
            PromoteWorldGenerationHandler.CanonicalScenePath(recipe.AreaId), null, acceptance,
            AssistantPermissionLevel.ApplyApprovedPlan, true);

        var unfinished = handler.Handle(context.State, recipeState, queued,
            Command(queued, report, context.State.Revision));
        var stale = handler.Handle(context.State, recipeState,
            queued.WithStatus(WorldGenerationJobStatus.Succeeded, EntityMutationTestContext.Timestamp.AddSeconds(1)),
            Command(queued, report, context.State.Revision.Next()));
        var unaccepted = handler.Handle(context.State, recipeState,
            queued.WithStatus(WorldGenerationJobStatus.Succeeded, EntityMutationTestContext.Timestamp.AddSeconds(1)),
            Command(queued, report, context.State.Revision, string.Empty));
        var automatedOnly = handler.Handle(context.State, recipeState,
            queued.WithStatus(WorldGenerationJobStatus.Succeeded, EntityMutationTestContext.Timestamp.AddSeconds(1)),
            Command(queued, report, context.State.Revision, "automated:test"));

        Assert.Equal(PromoteWorldGenerationHandler.JobNotSucceededCode,
            Assert.Single(unfinished.Command.Diagnostics).Code);
        Assert.Equal(PromoteWorldGenerationHandler.RevisionConflictCode,
            Assert.Single(stale.Command.Diagnostics).Code);
        Assert.Equal(PromoteWorldGenerationHandler.AcceptanceRequiredCode,
            Assert.Single(unaccepted.Command.Diagnostics).Code);
        Assert.Equal(PromoteWorldGenerationHandler.AcceptanceRequiredCode,
            Assert.Single(automatedOnly.Command.Diagnostics).Code);
        Assert.All([unfinished, stale, unaccepted, automatedOnly],
            result => Assert.Same(context.State, result.Command.State));
    }

    [Fact]
    public void CanonicalPromotionRejectsReportsWithoutCanonicalPlacementAndEnvironmentEvidence()
    {
        var context = new EntityMutationTestContext();
        var recipe = Recipe(context.AreaId.Value);
        var recipeState = new WorldGenerationRecipeState(
            new WorldGenerationRecipeCatalog(Guid.NewGuid(), 1, [recipe], "test:promotion-evidence"),
            new WorldGenerationRecipeRevision(2));
        var plan = new WorldGenerationPlanner().Build(recipe, context.State.Revision.Value,
            recipeState.Revision, Guid.NewGuid(), Guid.NewGuid());
        var job = WorldGenerationJob.Queue(plan, recipe, EntityMutationTestContext.Timestamp)
            .WithStatus(WorldGenerationJobStatus.Succeeded, EntityMutationTestContext.Timestamp.AddSeconds(1));
        var invalid = PromotionReport(job) with { TerrainSurface = null, EnvironmentOccupancy = null };
        var command = new PromoteWorldGenerationCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), context.State.Revision,
            recipeState.Revision, job.PlanId, invalid,
            $"world/generation/artifacts/{job.PlanId:N}/derived-world.tscn",
            PromoteWorldGenerationHandler.CanonicalScenePath(recipe.AreaId), null, "human-forward-plus:test",
            AssistantPermissionLevel.ApplyApprovedPlan, true);

        var result = new PromoteWorldGenerationHandler(new FixedClock(EntityMutationTestContext.Timestamp))
            .Handle(context.State, recipeState, job, command);

        Assert.False(result.Succeeded);
        Assert.Equal(PromoteWorldGenerationHandler.EvidenceInvalidCode,
            Assert.Single(result.Command.Diagnostics).Code);
        Assert.Same(context.State, result.Command.State);
    }

    [Fact]
    public void CanonicalPromotionRejectsTechnicalPreviewEvenWhenTechnicalEvidencePasses()
    {
        var context = new EntityMutationTestContext();
        var recipe = Copy(
            Recipe(context.AreaId.Value),
            artDirection: new WorldGenerationArtDirectionProfile(
                WorldGenerationVisualTier.TechnicalPreview,
                null,
                false,
                3));
        var recipeState = new WorldGenerationRecipeState(
            new WorldGenerationRecipeCatalog(Guid.NewGuid(), 1, [recipe], "test:technical-preview"),
            new WorldGenerationRecipeRevision(2));
        var plan = new WorldGenerationPlanner().Build(recipe, context.State.Revision.Value,
            recipeState.Revision, Guid.NewGuid(), Guid.NewGuid());
        var job = WorldGenerationJob.Queue(plan, recipe, EntityMutationTestContext.Timestamp)
            .WithStatus(WorldGenerationJobStatus.Succeeded, EntityMutationTestContext.Timestamp.AddSeconds(1));
        var command = new PromoteWorldGenerationCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), context.State.Revision,
            recipeState.Revision, job.PlanId, PromotionReport(job),
            $"world/generation/artifacts/{job.PlanId:N}/derived-world.tscn",
            PromoteWorldGenerationHandler.CanonicalScenePath(recipe.AreaId), null, "human-forward-plus:test",
            AssistantPermissionLevel.ApplyApprovedPlan, true);

        var result = new PromoteWorldGenerationHandler(new FixedClock(EntityMutationTestContext.Timestamp))
            .Handle(context.State, recipeState, job, command);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Command.Diagnostics);
        Assert.Equal(PromoteWorldGenerationHandler.EvidenceInvalidCode, diagnostic.Code);
        Assert.Contains(WorldGenerationProductionArtDirection.ProductionTierRequiredCode, diagnostic.Message);
    }

    [Fact]
    public void CanonicalPromotionRejectsPlayerSpawnBesideAbruptTerrainRelief()
    {
        var context = new EntityMutationTestContext();
        var state = WithProductionSource(WithTaggedTargetEntity(
            context.State,
            ["player", "world-generation"],
            new Vector3Value(0, 1, 0)));
        var recipe = Recipe(context.AreaId.Value);
        var recipeState = new WorldGenerationRecipeState(
            new WorldGenerationRecipeCatalog(Guid.NewGuid(), 1, [recipe], "test:camera-spawn-quality"),
            new WorldGenerationRecipeRevision(2));
        var plan = new WorldGenerationPlanner().Build(recipe, state.Revision.Value,
            recipeState.Revision, Guid.NewGuid(), Guid.NewGuid());
        var job = WorldGenerationJob.Queue(plan, recipe, EntityMutationTestContext.Timestamp)
            .WithStatus(WorldGenerationJobStatus.Succeeded, EntityMutationTestContext.Timestamp.AddSeconds(1));
        float[] heights = [-1, 1, -1, 1];
        var surface = new WorldGenerationTerrainSurfaceEvidence(
            WorldGenerationTerrainSurfaceEvidence.CurrentSchemaVersion,
            4096,
            4096,
            2,
            2,
            640,
            heights,
            TerrainCellDiagonal.Main,
            WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
                4096, 4096, 2, 2, 640, heights, TerrainCellDiagonal.Main));
        var command = new PromoteWorldGenerationCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), state.Revision,
            recipeState.Revision, job.PlanId, PromotionReport(job) with { TerrainSurface = surface },
            $"world/generation/artifacts/{job.PlanId:N}/derived-world.tscn",
            PromoteWorldGenerationHandler.CanonicalScenePath(recipe.AreaId), null,
            "human-forward-plus:test", AssistantPermissionLevel.ApplyApprovedPlan, true);

        var result = new PromoteWorldGenerationHandler(new FixedClock(EntityMutationTestContext.Timestamp))
            .Handle(state, recipeState, job, command);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Command.Diagnostics);
        Assert.Equal(PromoteWorldGenerationHandler.EvidenceInvalidCode, diagnostic.Code);
        Assert.Contains(WorldGenerationProductionArtDirection.CameraSpawnUnsafeCode, diagnostic.Message);
    }

    [Fact]
    public void CanonicalPromotionRejectsFloatingGeneratedFoundations()
    {
        var context = new EntityMutationTestContext();
        var state = WithProductionSource(WithTaggedTargetEntity(
            context.State,
            ["assistant-generated", "foundation", "village-building"],
            new Vector3Value(0, 10, 0)));
        var recipe = Recipe(context.AreaId.Value);
        var recipeState = new WorldGenerationRecipeState(
            new WorldGenerationRecipeCatalog(Guid.NewGuid(), 1, [recipe], "test:foundation-quality"),
            new WorldGenerationRecipeRevision(2));
        var plan = new WorldGenerationPlanner().Build(recipe, state.Revision.Value,
            recipeState.Revision, Guid.NewGuid(), Guid.NewGuid());
        var job = WorldGenerationJob.Queue(plan, recipe, EntityMutationTestContext.Timestamp)
            .WithStatus(WorldGenerationJobStatus.Succeeded, EntityMutationTestContext.Timestamp.AddSeconds(1));
        var command = new PromoteWorldGenerationCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), state.Revision,
            recipeState.Revision, job.PlanId, PromotionReport(job),
            $"world/generation/artifacts/{job.PlanId:N}/derived-world.tscn",
            PromoteWorldGenerationHandler.CanonicalScenePath(recipe.AreaId), null,
            "human-forward-plus:test", AssistantPermissionLevel.ApplyApprovedPlan, true);

        var result = new PromoteWorldGenerationHandler(new FixedClock(EntityMutationTestContext.Timestamp))
            .Handle(state, recipeState, job, command);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Command.Diagnostics);
        Assert.Equal(PromoteWorldGenerationHandler.EvidenceInvalidCode, diagnostic.Code);
        Assert.Contains(WorldGenerationProductionArtDirection.FoundationFloatingCode, diagnostic.Message);
    }

    [Fact]
    public void ProductionArtDirectionAdaptsSmallStructureDriftButStillRejectsItWhenDisabled()
    {
        var context = new EntityMutationTestContext();
        var state = WithProductionSource(WithTaggedTargetEntity(
            context.State,
            ["assistant-generated", "foundation", "village-building"],
            new Vector3Value(0, 1.5f, 0)));
        const int samples = 129;
        var heights = Enumerable.Repeat(0f, samples * samples).ToArray();
        var surface = new WorldGenerationTerrainSurfaceEvidence(
            WorldGenerationTerrainSurfaceEvidence.CurrentSchemaVersion,
            512,
            512,
            samples,
            samples,
            10,
            heights,
            TerrainCellDiagonal.Main,
            WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
                512, 512, samples, samples, 10, heights, TerrainCellDiagonal.Main));
        var target = state.Project.Areas.Single(area => area.Id == context.AreaId);
        var adaptiveRecipe = Recipe(context.AreaId.Value);

        var adaptive = WorldGenerationProductionArtDirection.Materialize(
            state.Project,
            target,
            adaptiveRecipe,
            surface,
            "test:structure-adaptation");
        var strict = WorldGenerationProductionArtDirection.Materialize(
            state.Project,
            target,
            Copy(
                adaptiveRecipe,
                terrain: adaptiveRecipe.Terrain with { AdaptToStructures = false }),
            surface,
            "test:structure-adaptation-disabled");

        Assert.True(adaptive.Succeeded, string.Join(Environment.NewLine, adaptive.Diagnostics));
        Assert.NotNull(adaptive.Terrain);
        Assert.Contains(adaptive.Terrain.Heights, height => height > 0);
        Assert.InRange(adaptive.Evidence!.SpatialQuality.MaximumFoundationOffsetMeters, 0, .001f);
        Assert.Empty(WorldGenerationProductionArtDirection.ValidateSpatialQuality(
            target,
            surface,
            adaptToStructures: true));
        Assert.False(strict.Succeeded);
        Assert.Contains(strict.Diagnostics,
            diagnostic => diagnostic.StartsWith(
                WorldGenerationProductionArtDirection.FoundationFloatingCode,
                StringComparison.Ordinal));
        Assert.Contains(WorldGenerationProductionArtDirection.ValidateSpatialQuality(
                target,
                surface,
                adaptToStructures: false),
            diagnostic => diagnostic.StartsWith(
                WorldGenerationProductionArtDirection.FoundationFloatingCode,
                StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionArtDirectionLocalizesGroundCoverAroundTheAuthoredSettlement()
    {
        var context = new EntityMutationTestContext();
        var state = WithProductionSource(WithTaggedTargetEntity(
            context.State,
            ["assistant-generated", "foundation", "village-building"],
            new Vector3Value(30, 0, -40)));
        const int samples = 129;
        var heights = Enumerable.Repeat(0f, samples * samples).ToArray();
        var surface = new WorldGenerationTerrainSurfaceEvidence(
            WorldGenerationTerrainSurfaceEvidence.CurrentSchemaVersion,
            512,
            512,
            samples,
            samples,
            10,
            heights,
            TerrainCellDiagonal.Main,
            WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
                512, 512, samples, samples, 10, heights, TerrainCellDiagonal.Main));
        var target = state.Project.Areas.Single(area => area.Id == context.AreaId);

        var result = WorldGenerationProductionArtDirection.Materialize(
            state.Project,
            target,
            Recipe(context.AreaId.Value),
            surface,
            "test:localized-vegetation");

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        var groundCover = Assert.Single(result.Biomes,
            biome => biome.Tags.Contains("ground-cover"));
        Assert.Contains("focus-center:30,-40", groundCover.Tags);
        Assert.Contains("focus-radius:72", groundCover.Tags);
        Assert.DoesNotContain(result.Biomes.Single(biome => biome.Tags.Contains("tree")).Tags,
            tag => tag.StartsWith("focus-", StringComparison.Ordinal));
        Assert.NotNull(result.Terrain);
        Assert.Equal(
            (result.Terrain.SamplesX - 1) * (result.Terrain.SamplesZ - 1),
            result.Terrain.MaterialPaint.Count);
        Assert.Equal(result.Terrain.MaterialPaint.Count, result.Evidence!.PaintedTerrainCellCount);
        var buildingGuides = result.Features
            .Where(feature => feature.Kind == TerrainFeatureKind.Building &&
                              feature.MaterialResource == "builtin/invisible-scatter-exclusion")
            .ToArray();
        Assert.Equal(2, buildingGuides.Length);
        Assert.Contains(buildingGuides, feature => feature.CompatibleBiomeTags.Count == 0);
        Assert.Contains(buildingGuides, feature => feature.CompatibleBiomeTags.SequenceEqual(["tree"]));
    }

    [Fact]
    public void ProductionArtDirectionRejectsRunOnlyAnimationAtWalkingPatrolSpeed()
    {
        var areaId = AreaId.From(Guid.Parse("f0500000-0000-4000-8000-0000000000a0"));
        var routeId = Guid.Parse("f0500000-0000-4000-8000-0000000000a1");
        var entity = new EntityInstance(
            EntityId.From(Guid.Parse("f0500000-0000-4000-8000-0000000000a2")),
            BlueprintId.From(Guid.Parse("f0500000-0000-4000-8000-0000000000a3")),
            TransformValue.Identity,
            ["npc"],
            new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion,
            [
                new("runtime.animationProfile", EntityPropertyValueKind.String, "authored-clips"),
                new("runtime.animationRun", EntityPropertyValueKind.String, "jog_fwd_loop"),
                new("runtime.patrolRoute", EntityPropertyValueKind.String, routeId.ToString("D")),
            ]));
        var route = new PatrolRoute(
            routeId,
            1,
            "test:locomotion-quality",
            PatrolTraversalPolicy.Loop,
            [
                new(Guid.NewGuid(), new Vector3Value(0, 0, 0), 0, 0, 1.4, null, null),
                new(Guid.NewGuid(), new Vector3Value(8, 0, 0), 0, 0, 1.4, null, null),
            ]);
        var area = new AreaDefinition(
            areaId,
            "locomotion-quality",
            "Locomotion quality",
            [entity],
            AreaKind.Exterior,
            new AreaDimensions(64, 64));
        var waypoints = new WaypointAuthoringProfile(
            Guid.NewGuid(),
            WaypointAuthoringProfile.CurrentSchemaVersion,
            "test:locomotion-quality",
            areaId,
            true,
            [],
            [route]);
        area = area.WithNavigation(area.Navigation.WithWaypoints(waypoints));

        var diagnostic = Assert.Single(WorldGenerationProductionArtDirection.ValidatePatrolLocomotion(area));

        Assert.StartsWith(WorldGenerationProductionArtDirection.PatrolLocomotionMismatchCode, diagnostic);
        Assert.Contains("1.4 m/s", diagnostic);
    }

    private static WorldGenerationValidationReport PromotionReport(WorldGenerationJob job)
    {
        var providers = job.Plan.Steps.Select(step => new WorldGenerationProviderEvidence(
            step.Stage,
            step.Backend,
            step.Backend switch
            {
                WorldGenerationBackend.Terrain3D => "terrain3d",
                WorldGenerationBackend.PcgGodot => "godot-flow",
                _ => "openrpg-native",
            },
            step.Backend switch
            {
                WorldGenerationBackend.Terrain3D => "v1.0.2-stable",
                WorldGenerationBackend.PcgGodot => "284231f060099a8943eb56567c21732b7cd678d3",
                _ => "1.0.0",
            },
            new string(step.Order % 2 == 0 ? 'a' : 'b', 64))).ToArray();
        const int samples = 257;
        var heights = Enumerable.Range(0, samples)
            .SelectMany(z => Enumerable.Range(0, samples).Select(x =>
            {
                var tx = x / (float)(samples - 1);
                var tz = z / (float)(samples - 1);
                return tx >= tz
                    ? (.2f * (tx - tz)) + tz
                    : (.4f * (tz - tx)) + tx;
            }))
            .ToArray();
        AreaEnvironmentOccupancyCell[] cells = [new("vegetation", "biomes", 0, 0, 24)];
        return new WorldGenerationValidationReport(
            WorldGenerationValidationReport.CurrentSchemaVersion,
            job.JobId,
            job.PlanId,
            job.Recipe.Id,
            job.Recipe.AreaId,
            job.ProjectRevision,
            job.RecipeRevision,
            job.RecipeFingerprint,
            job.Plan.PlanFingerprint,
            new string('c', 64),
            new string('d', 64),
            new string('e', 64),
            true,
            true,
            true,
            true,
            providers,
            [],
            new WorldGenerationTerrainSurfaceEvidence(
                WorldGenerationTerrainSurfaceEvidence.CurrentSchemaVersion,
                4096,
                4096,
                samples,
                samples,
                10,
                heights,
                TerrainCellDiagonal.Main,
                WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
                    4096, 4096, samples, samples, 10, heights, TerrainCellDiagonal.Main)),
            new WorldGenerationEnvironmentOccupancyEvidence(
                WorldGenerationEnvironmentOccupancyEvidence.CurrentSchemaVersion,
                4096,
                4096,
                32,
                cells,
                WorldGenerationSpatialEvidenceFingerprint.ComputeEnvironment(4096, 4096, 32, cells)));
    }

    private static WorldGenerationRecipe Recipe(Guid areaId, bool reverse = false)
    {
        var rivers = new[]
        {
            new RiverGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000010"), "River A", 14, 3, "north-south"),
            new RiverGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000011"), "River B", 8, 2, "tributary"),
        };
        var biomes = new[]
        {
            new BiomeGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000012"),
                "temperate-forest", "west", .75, ["tree", "rock"]),
            new BiomeGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000013"),
                "meadow", "east", .35, ["grass", "flower"]),
        };
        var roads = new[]
        {
            new RoadGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000014"),
                "main", "south-gate", "village", 5),
            new RoadGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000015"),
                "path", "village", "castle", 2),
        };
        var settlements = new[]
        {
            new SettlementGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000016"),
                "village", "northern", "near:river", 24, false, ["tavern", "blacksmith"]),
            new SettlementGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000017"),
                "castle", "stone", "highest-nearby-hill", 8, true, ["keep"]),
        };
        var scatter = new[]
        {
            new ScatterGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000018"),
                "forest-props", "biome:temperate-forest", .65, 2.5),
            new ScatterGenerationRecipe(Guid.Parse("f0500000-0000-4000-8000-000000000019"),
                "road-props", "near:roads", .2, 4),
        };
        if (reverse)
        {
            Array.Reverse(rivers);
            Array.Reverse(biomes);
            Array.Reverse(roads);
            Array.Reverse(settlements);
            Array.Reverse(scatter);
        }
        return new WorldGenerationRecipe(
            Guid.Parse("f0500000-0000-4000-8000-000000000002"),
            WorldGenerationRecipe.CurrentSchemaVersion,
            areaId,
            "Northern valley",
            20260826,
            new(4096, 4096, 640),
            new("temperate-valley", .58, 3, true),
            rivers,
            biomes,
            roads,
            settlements,
            scatter,
            new(["wolves"], ["deer"], ["northern-loot"], true),
            new(true, true, .5),
            new(256, 2500, true, true),
            WorldGenerationRecipe.DefaultBackends(),
            true,
            "test:world-generation-v1",
            artDirection: new WorldGenerationArtDirectionProfile(
                WorldGenerationVisualTier.Production,
                ProductionSourceAreaId,
                true,
                3));
    }

    private static WorldGenerationRecipe Copy(
        WorldGenerationRecipe source,
        WorldGenerationBounds? bounds = null,
        TerrainGenerationRecipe? terrain = null,
        IReadOnlyList<BiomeGenerationRecipe>? biomes = null,
        IReadOnlyDictionary<WorldGenerationStage, WorldGenerationBackend>? backends = null,
        WorldGenerationRegenerationPolicy? regeneration = null,
        WorldGenerationArtDirectionProfile? artDirection = null) => new(
        source.Id,
        source.SchemaVersion,
        source.AreaId,
        source.Title,
        source.Seed,
        bounds ?? source.Bounds,
        terrain ?? source.Terrain,
        source.Rivers,
        biomes ?? source.Biomes,
        source.Roads,
        source.Settlements,
        source.Scatter,
        source.Gameplay,
        source.Navigation,
        source.Quality,
        backends ?? source.Backends,
        source.ApprovedForGeneration,
        source.Provenance,
        regeneration ?? source.Regeneration,
        artDirection ?? source.ArtDirection);

    private static ProjectState WithProductionSource(ProjectState state)
    {
        var sourceId = AreaId.From(ProductionSourceAreaId);
        var source = new AreaDefinition(
            sourceId,
            "production-art-source",
            "Production art source",
            [],
            AreaKind.Exterior,
            new AreaDimensions(512, 512));
        var sourceWorld = source.World;
        var materialId = Guid.Parse("f0500000-0000-4000-8000-000000000091");
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
            new BiomeLayerDefinition(Guid.Parse("f0500000-0000-4000-8000-000000000092"),
                "Canopy", materialId, -512, 512, 0, 30, 0, 1, .2f, "asset:tree",
                ["tree"], [], 30, "test:production-source"),
            new BiomeLayerDefinition(Guid.Parse("f0500000-0000-4000-8000-000000000093"),
                "Shrubs", materialId, -512, 512, 0, 30, 0, 1, .3f, "asset:shrub",
                ["shrub"], [], 20, "test:production-source"),
            new BiomeLayerDefinition(Guid.Parse("f0500000-0000-4000-8000-000000000094"),
                "Meadow grass", materialId, -512, 512, 0, 30, 0, 1, .6f, "asset:grass",
                ["ground-cover"], [], 10, "test:production-source"),
        };
        var features = new[]
        {
            new TerrainFeatureDefinition(
                Guid.Parse("f0500000-0000-4000-8000-000000000095"),
                TerrainFeatureDefinition.CurrentSchemaVersion,
                "Production road",
                TerrainFeatureKind.Road,
                [new Vector3Value(-48, .06f, -192), new Vector3Value(18, .06f, 0),
                    new Vector3Value(-24, .06f, 192)],
                4,
                .06f,
                0,
                TerrainFeatureCrossingPolicy.Allow,
                ["road"],
                "assets/road.png",
                "test:production-source"),
        };
        source = source.WithWorld(new AreaWorldProfile(
            sourceWorld.Id,
            sourceWorld.SchemaVersion,
            terrain,
            biomes,
            features,
            sourceWorld.VolumetricNavigation,
            sourceWorld.FlightRoutes,
            sourceWorld.Loading,
            "test:production-source"));
        var project = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            [.. state.Project.Areas, source],
            state.Project.Dialogues,
            state.Project.Quests);
        return new ProjectState(project, state.Revision);
    }

    private static ProjectState WithTaggedTargetEntity(
        ProjectState state,
        IReadOnlyList<string> tags,
        Vector3Value position)
    {
        var area = Assert.Single(state.Project.Areas);
        var source = Assert.Single(area.Entities);
        var entity = new EntityInstance(
            source.Id,
            source.BlueprintId,
            new TransformValue(position, source.Transform.Rotation, source.Transform.Scale),
            tags,
            source.Overrides,
            source.BlueprintVersionRequirement);
        var updatedArea = area.WithEntities([entity]);
        var project = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            [updatedArea],
            state.Project.Dialogues,
            state.Project.Quests);
        return new ProjectState(project, state.Revision);
    }

    private sealed class RecordingAdapter : IWorldGenerationAdapter
    {
        private readonly bool _canExecute;

        public RecordingAdapter(WorldGenerationBackend backend, bool canExecute)
        {
            Backend = backend;
            _canExecute = canExecute;
        }

        public WorldGenerationBackend Backend { get; }
        public List<WorldGenerationExecutionStep> Executed { get; } = [];

        public bool CanExecute(WorldGenerationExecutionStep step) => _canExecute;

        public ValueTask<WorldGenerationStepResult> ExecuteAsync(
            WorldGenerationExecutionContext context,
            WorldGenerationExecutionStep step,
            CancellationToken cancellationToken = default)
        {
            Executed.Add(step);
            return ValueTask.FromResult(new WorldGenerationStepResult(
                true, step.Stage, Backend, [], []));
        }
    }

    private sealed class MemoryArtifactSink : IWorldGenerationArtifactSink
    {
        public ValueTask WriteTextAsync(
            string relativePath,
            string content,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
