using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Application.WorldGeneration;
using Forge.Domain.Assistant;
using Forge.Domain.WorldGeneration;

namespace Forge.Application.Tests.WorldGeneration;

public sealed class WorldGenerationWorkflowTests
{
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
        Assert.Equal(WorldGenerationBackend.GodotNative,
            first.Steps.Single(item => item.Stage == WorldGenerationStage.Validation).Backend);
        Assert.Empty(first.Steps[0].DependsOn);
        Assert.All(first.Steps.Skip(1), step => Assert.Equal([step.Order - 1], step.DependsOn));
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
            "test:world-generation-v1");
    }
}
