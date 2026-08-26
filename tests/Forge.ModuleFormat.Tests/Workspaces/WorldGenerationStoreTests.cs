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
}
