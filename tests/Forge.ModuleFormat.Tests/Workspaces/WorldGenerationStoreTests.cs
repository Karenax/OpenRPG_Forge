using Forge.Application.WorldGeneration;
using Forge.Domain.WorldGeneration;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class WorldGenerationStoreTests
{
    [Fact]
    public async Task RecipePlanJobAndArtifactsRoundTripAtomically()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-worldgen-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var recipe = WorldGenerationRecipe.CreateDefault(
                    Guid.Parse("f0510000-0000-4000-8000-000000000001"), "Store test", 71)
                .WithApproval(true, "test:approved");
            var catalog = new WorldGenerationRecipeCatalog(
                Guid.Parse("f0510000-0000-4000-8000-000000000002"), 1, [recipe], "test:store");
            Assert.True(WorldGenerationRecipeStore.Save(root, catalog, new(7)).Succeeded);
            var loadedCatalog = WorldGenerationRecipeStore.Load(root);
            Assert.True(loadedCatalog.Succeeded);
            Assert.Equal(7, loadedCatalog.Revision.Value);
            Assert.Equal(recipe.Fingerprint(), Assert.Single(loadedCatalog.Catalog.Recipes).Fingerprint());

            var plan = new WorldGenerationPlanner().Build(recipe, 3, new(2),
                Guid.Parse("f0510000-0000-4000-8000-000000000003"),
                Guid.Parse("f0510000-0000-4000-8000-000000000004"));
            Assert.True(WorldGenerationExecutionPlanStore.Save(root, plan).Succeeded);
            var loadedPlan = WorldGenerationExecutionPlanStore.Load(root, plan.PlanId).Plan!;
            Assert.Equal(plan.PlanId, loadedPlan.PlanId);
            Assert.Equal(plan.PlanFingerprint, loadedPlan.PlanFingerprint);
            Assert.Equal(plan.Steps.Select(item => (item.Order, item.Stage, item.Backend, item.Operation)),
                loadedPlan.Steps.Select(item => (item.Order, item.Stage, item.Backend, item.Operation)));
            Assert.All(plan.Steps.Zip(loadedPlan.Steps), pair =>
            {
                Assert.Equal(pair.First.Parameters.OrderBy(item => item.Key),
                    pair.Second.Parameters.OrderBy(item => item.Key));
                Assert.Equal(pair.First.DependsOn, pair.Second.DependsOn);
            });
            Assert.Empty(loadedPlan.Diagnostics);

            var job = WorldGenerationJob.Queue(plan, recipe, DateTimeOffset.UnixEpoch);
            Assert.True(WorldGenerationJobStore.Save(root, job).Succeeded);
            var loadedJob = WorldGenerationJobStore.Load(root, plan.PlanId).Job!;
            Assert.Equal(WorldGenerationJobStatus.Queued, loadedJob.Status);
            Assert.Equal("queued", Assert.Single(loadedJob.Audit).Action);

            var completed = loadedJob.WithStatus(WorldGenerationJobStatus.Succeeded,
                DateTimeOffset.UnixEpoch.AddSeconds(1));
            Assert.True(WorldGenerationJobStore.Save(root, completed).Succeeded);
            Assert.Equal(["queued", "succeeded"],
                WorldGenerationJobStore.Load(root, plan.PlanId).Job!.Audit.Select(item => item.Action));

            var sink = new FileSystemWorldGenerationArtifactSink(root);
            await sink.WriteTextAsync("recipe/step.json", "{}");
            Assert.True(File.Exists(Path.Combine(root, "world", "generation", "artifacts", "recipe", "step.json")));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sink.WriteTextAsync("../escape.json", "{}").AsTask());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TamperedExecutionPlanIsRejectedOnLoad()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-worldgen-tamper-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var recipe = WorldGenerationRecipe.CreateDefault(
                    Guid.Parse("f0510000-0000-4000-8000-000000000011"), "Tamper test", 72)
                .WithApproval(true, "test:approved");
            var plan = new WorldGenerationPlanner().Build(recipe, 3, new(2),
                Guid.Parse("f0510000-0000-4000-8000-000000000012"),
                Guid.Parse("f0510000-0000-4000-8000-000000000013"));
            Assert.True(WorldGenerationExecutionPlanStore.Save(root, plan).Succeeded);
            var path = Path.Combine(root, WorldGenerationExecutionPlanStore.RelativeDirectory,
                $"{plan.PlanId:N}.json");
            var replacement = new string(plan.PlanFingerprint[0] == 'f' ? 'e' : 'f', 64);
            File.WriteAllText(path, File.ReadAllText(path).Replace(plan.PlanFingerprint, replacement,
                StringComparison.Ordinal));

            var loaded = WorldGenerationExecutionPlanStore.Load(root, plan.PlanId);

            Assert.False(loaded.Succeeded);
            Assert.Null(loaded.Plan);
            Assert.Equal(WorldGenerationExecutionPlanStore.ReadFailedCode, Assert.Single(loaded.Diagnostics).Code);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnapprovedExecutionPlanCannotBePromotedByEditingReadyFlag()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-worldgen-approval-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var recipe = WorldGenerationRecipe.CreateDefault(
                Guid.Parse("f0510000-0000-4000-8000-000000000021"), "Approval test", 73);
            var plan = new WorldGenerationPlanner().Build(recipe, 3, new(2),
                Guid.Parse("f0510000-0000-4000-8000-000000000022"),
                Guid.Parse("f0510000-0000-4000-8000-000000000023"));
            Assert.False(plan.ReadyToApply);
            Assert.True(WorldGenerationExecutionPlanStore.Save(root, plan).Succeeded);
            var path = Path.Combine(root, WorldGenerationExecutionPlanStore.RelativeDirectory,
                $"{plan.PlanId:N}.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace(
                "\"readyToApply\": false", "\"readyToApply\": true", StringComparison.Ordinal));

            var loaded = WorldGenerationExecutionPlanStore.Load(root, plan.PlanId);

            Assert.False(loaded.Succeeded);
            Assert.Null(loaded.Plan);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NeutralDerivedScenePromotionIsAtomicReopenableAndReversible()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-worldgen-promotion-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var recipe = WorldGenerationRecipe.CreateDefault(
                    Guid.Parse("f0510000-0000-4000-8000-000000000031"), "Promotion test", 74)
                .WithApproval(true, "test:approved");
            var plan = new WorldGenerationPlanner().Build(recipe, 0, new(3),
                Guid.Parse("f0510000-0000-4000-8000-000000000032"),
                Guid.Parse("f0510000-0000-4000-8000-000000000033"));
            var job = WorldGenerationJob.Queue(plan, recipe, DateTimeOffset.UnixEpoch)
                .WithStatus(WorldGenerationJobStatus.Succeeded, DateTimeOffset.UnixEpoch.AddSeconds(1));
            var scene = "[gd_scene format=3]\n\n[node name=\"OpenRPGBakedWorld\" type=\"Node3D\"]\n";
            var scenePath = Path.Combine(root,
                WorldGenerationPromotionStore.DerivedSceneRelativePath(plan.PlanId)
                    .Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);
            File.WriteAllText(scenePath, scene);
            var sceneSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(scene))).ToLowerInvariant();
            var providers = plan.Steps.Select(step => new WorldGenerationProviderEvidence(
                step.Stage, step.Backend,
                step.Backend == WorldGenerationBackend.PcgGodot ? "godot-flow" :
                    step.Backend == WorldGenerationBackend.Terrain3D ? "terrain3d" : "openrpg-native",
                "test-version", new string('a', 64))).ToArray();
            var report = new WorldGenerationValidationReport(
                1, job.JobId, job.PlanId, recipe.Id, recipe.AreaId, job.ProjectRevision, job.RecipeRevision,
                job.RecipeFingerprint, job.Plan.PlanFingerprint, sceneSha, new string('b', 64),
                new string('c', 64), true, true, true, true, providers, []);
            Assert.True(WorldGenerationPromotionStore.SaveValidationReport(root, report).Succeeded);
            var inspected = WorldGenerationPromotionStore.Inspect(root, job);
            Assert.True(inspected.Succeeded,
                string.Join(" | ", inspected.Diagnostics.Select(item => item.Message)));
            Assert.Equal(sceneSha, inspected.SceneSha256);

            var promotion = new WorldGenerationCanonicalPromotion(
                1, Guid.Parse("f0510000-0000-4000-8000-000000000034"),
                Guid.Parse("f0510000-0000-4000-8000-000000000035"),
                job.JobId, job.PlanId, recipe.Id, recipe.AreaId, 0, 1, 3,
                job.RecipeFingerprint, job.Plan.PlanFingerprint, recipe.Seed,
                WorldGenerationPromotionStore.DerivedSceneRelativePath(plan.PlanId),
                PromoteWorldGenerationHandler.CanonicalScenePath(recipe.AreaId),
                sceneSha, report.ContentFingerprint, report.AssetSelectionFingerprint,
                providers, recipe.Regeneration.ProtectedNodePaths, "human-review:test");
            var applied = WorldGenerationPromotionStore.Apply(root, promotion, expectedCanonicalSceneSha256: null);

            Assert.True(applied.Succeeded,
                string.Join(" | ", applied.Diagnostics.Select(item => item.Message)));
            Assert.Equal(2, applied.Patches.Count);
            var canonicalPath = Path.Combine(root,
                promotion.CanonicalSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(scene, File.ReadAllText(canonicalPath));
            var reopened = WorldGenerationPromotionStore.Load(root, recipe.AreaId);
            Assert.True(reopened.Succeeded);
            Assert.Equal(promotion.ContentFingerprint, reopened.Promotion!.ContentFingerprint);
            Assert.Equal(sceneSha, reopened.SceneSha256);
            var unacknowledged = WorldGenerationPromotionStore.ValidateCanonicalExpectation(
                root, recipe.AreaId, expectedCanonicalSceneSha256: null);
            var acknowledged = WorldGenerationPromotionStore.ValidateCanonicalExpectation(
                root, recipe.AreaId, sceneSha);
            Assert.False(unacknowledged.Succeeded);
            Assert.Equal(WorldGenerationPromotionStore.CanonicalConflictCode,
                Assert.Single(unacknowledged.Diagnostics).Code);
            Assert.True(acknowledged.Succeeded);
            Assert.Equal(sceneSha, acknowledged.CurrentSceneSha256);

            WorldGenerationPromotionStore.Restore(root, applied.Patches, useAfter: false);
            Assert.False(File.Exists(canonicalPath));
            Assert.True(WorldGenerationPromotionStore.Load(root, recipe.AreaId).FileMissing);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PromotionRejectsProviderCouplingTamperingAndUnacknowledgedOverwrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-worldgen-promotion-negative-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var planId = Guid.Parse("f0510000-0000-4000-8000-000000000043");
            var derivedRelative = WorldGenerationPromotionStore.DerivedSceneRelativePath(planId);
            var derivedPath = Path.Combine(root, derivedRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(derivedPath)!);
            File.WriteAllText(derivedPath,
                "[gd_scene format=3]\n[ext_resource path=\"res://addons/terrain_3d/terrain.gdextension\"]\n");

            var neutral = WorldGenerationPromotionStore.ValidateNeutralScene(root, derivedRelative);

            Assert.False(neutral.Succeeded);
            Assert.Equal(WorldGenerationPromotionStore.ProviderDependencyCode,
                Assert.Single(neutral.Diagnostics).Code);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
