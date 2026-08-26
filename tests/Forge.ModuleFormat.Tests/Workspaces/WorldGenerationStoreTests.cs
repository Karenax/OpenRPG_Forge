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
            Assert.True(WorldGenerationRecipeStore.Save(root, catalog).Succeeded);
            var loadedCatalog = WorldGenerationRecipeStore.Load(root);
            Assert.True(loadedCatalog.Succeeded);
            Assert.Equal(recipe.Fingerprint(), Assert.Single(loadedCatalog.Catalog.Recipes).Fingerprint());

            var plan = new WorldGenerationPlanner().Build(recipe, 3, new(2),
                Guid.Parse("f0510000-0000-4000-8000-000000000003"),
                Guid.Parse("f0510000-0000-4000-8000-000000000004"));
            Assert.True(WorldGenerationExecutionPlanStore.Save(root, plan).Succeeded);
            Assert.Equal(plan, WorldGenerationExecutionPlanStore.Load(root, plan.PlanId).Plan);

            var job = WorldGenerationJob.Queue(plan, recipe, DateTimeOffset.UnixEpoch);
            Assert.True(WorldGenerationJobStore.Save(root, job).Succeeded);
            Assert.Equal(WorldGenerationJobStatus.Queued,
                WorldGenerationJobStore.Load(root, plan.PlanId).Job!.Status);

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
}
