using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Assistant;

public sealed class TerrainAwareAssistedAreaBuildTests
{
    [Fact]
    public void ExteriorBuildUsesPromotedSurfaceEvenWhenRenderableTerrainIsDisabled()
    {
        var context = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12));
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var transaction = Guid.NewGuid();
        var execution = Guid.NewGuid();
        BuildAssistedAreaPlanCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transaction, execution, context.State.Revision, context.Plan.Id,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        Assert.True(handler.Handle(context.State, Command(true), context.Catalog).Succeeded);
        var applied = handler.Handle(context.State, Command(false), context.Catalog);

        Assert.True(applied.Succeeded, string.Join(" | ", applied.Diagnostics.Select(item => item.Message)));
        var entity = applied.Command.State.Project.Areas.Single().Entities.Single(item =>
            item.Tags.Contains("building", StringComparer.Ordinal));
        Assert.Equal(5, entity.Transform.Position.Y, 4);
    }

    [Fact]
    public void ExteriorPlayerSpawnUsesTheRuntimeCapsuleCenterAnchor()
    {
        var context = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12),
            blueprintKind: BlueprintKind.PlayerSpawn);
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var transaction = Guid.NewGuid();
        var execution = Guid.NewGuid();
        BuildAssistedAreaPlanCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transaction, execution, context.State.Revision, context.Plan.Id,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        Assert.True(handler.Handle(context.State, Command(true), context.Catalog).Succeeded);
        var applied = handler.Handle(context.State, Command(false), context.Catalog);

        Assert.True(applied.Succeeded, string.Join(" | ", applied.Diagnostics.Select(item => item.Message)));
        var spawn = applied.Command.State.Project.Areas.Single().Entities.Single();
        Assert.Equal(5 + TerrainPlacementGrounding.PlayerSpawnCapsuleCenterOffsetMeters,
            spawn.Transform.Position.Y, 4);
    }

    [Fact]
    public void ExteriorBuildRejectsExplicitHeightAndUnsafeStructureFootprints()
    {
        var explicitContext = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12), explicitTransform: true);
        var slopedContext = Context(Surface([0, 1, 0, 1]), Occupancy(12));
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        BuildAssistedAreaPlanCommand Command(BuildContext context) => new(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), context.State.Revision,
            context.Plan.Id, AssistantPermissionLevel.ApplyApprovedPlan, true);

        var explicitResult = handler.Handle(explicitContext.State, Command(explicitContext), explicitContext.Catalog);
        var slopedResult = handler.Handle(slopedContext.State, Command(slopedContext), slopedContext.Catalog);

        Assert.Equal(AssistedAreaPlanProjectValidator.ExplicitExteriorPlacementCode,
            Assert.Single(explicitResult.Diagnostics).Code);
        Assert.Equal(BuildAssistedAreaPlanHandler.TerrainFootprintUnfitCode,
            Assert.Single(slopedResult.Diagnostics).Code);
        Assert.Same(slopedContext.State, slopedResult.Command.State);
    }

    [Fact]
    public void RequiredVegetationIsAValidatedBuildIntentInsteadOfAnImplicitGuess()
    {
        var sparse = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(2), requireVegetation: true);
        var lush = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12), requireVegetation: true);
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        BuildAssistedAreaPlanCommand Command(BuildContext context) => new(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), context.State.Revision,
            context.Plan.Id, AssistantPermissionLevel.ApplyApprovedPlan, true);

        var sparseResult = handler.Handle(sparse.State, Command(sparse), sparse.Catalog);
        var lushResult = handler.Handle(lush.State, Command(lush), lush.Catalog);

        Assert.Equal(BuildAssistedAreaPlanHandler.VegetationIntentUnsatisfiedCode,
            Assert.Single(sparseResult.Diagnostics).Code);
        Assert.True(lushResult.Succeeded, string.Join(" | ", lushResult.Diagnostics.Select(item => item.Message)));
    }

    [Fact]
    public void RequiredVegetationIsValidatedAroundEveryContentNode()
    {
        var occupancy = new AreaEnvironmentOccupancyProfile(
            Guid.Parse("d1590000-0000-4000-8000-000000000006"), 1, 64, 64, 4,
            [new("vegetation", "biomes", -16, 0, 12), new("vegetation", "biomes", 0, 0, 12)],
            new string('b', 64), "test:distributed-occupancy");
        var context = Context(Surface([.5f, .5f, .5f, .5f]), occupancy,
            requireVegetation: true, semanticRoleTag: "social", addSecondContentNode: true);
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var command = new BuildAssistedAreaPlanCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), context.State.Revision,
            context.Plan.Id, AssistantPermissionLevel.ApplyApprovedPlan, true);

        var result = handler.Handle(context.State, command, context.Catalog);

        Assert.Equal(BuildAssistedAreaPlanHandler.VegetationIntentUnsatisfiedCode,
            Assert.Single(result.Diagnostics).Code);
        Assert.Contains("Second content", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VegetationInsideAStructureFootprintIsRejectedBeforeConstruction()
    {
        var context = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12, 0, 0), requireVegetation: true);
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var command = new BuildAssistedAreaPlanCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), context.State.Revision,
            context.Plan.Id, AssistantPermissionLevel.ApplyApprovedPlan, true);

        var result = handler.Handle(context.State, command, context.Catalog);

        Assert.Equal(BuildAssistedAreaPlanHandler.VegetationFootprintConflictCode,
            Assert.Single(result.Diagnostics).Code);
        Assert.Same(context.State, result.Command.State);
    }

    [Fact]
    public void VegetationInsideAnUntaggedPrefabFootprintIsRejectedBeforeConstruction()
    {
        var context = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12, 0, 0),
            semanticRoleTag: "social", prefabComposition: true);
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var command = new BuildAssistedAreaPlanCommand(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), context.State.Revision,
            context.Plan.Id, AssistantPermissionLevel.ApplyApprovedPlan, true);

        var result = handler.Handle(context.State, command, context.Catalog);

        Assert.Equal(BuildAssistedAreaPlanHandler.VegetationFootprintConflictCode,
            Assert.Single(result.Diagnostics).Code);
        Assert.Same(context.State, result.Command.State);
    }

    [Fact]
    public void SemanticRolesRejectFurnitureAsVillagersAndIncompleteVillageHouses()
    {
        var placeable = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12),
            semanticRoleTag: "villager", blueprintKind: BlueprintKind.Placeable);
        var creature = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12),
            semanticRoleTag: "villager", blueprintKind: BlueprintKind.Creature);
        var incompleteHouse = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(12),
            semanticRoleTag: "village-house", blueprintKind: BlueprintKind.Placeable);

        Assert.Contains(AssistedAreaPlanProjectValidator.Validate(placeable.Catalog, placeable.State.Project),
            item => item.Code == AssistedAreaPlanProjectValidator.SemanticRoleMismatchCode);
        Assert.DoesNotContain(AssistedAreaPlanProjectValidator.Validate(creature.Catalog, creature.State.Project),
            item => item.Code == AssistedAreaPlanProjectValidator.SemanticRoleMismatchCode);
        Assert.Contains(AssistedAreaPlanProjectValidator.Validate(incompleteHouse.Catalog, incompleteHouse.State.Project),
            item => item.Code == AssistedAreaPlanProjectValidator.SemanticRoleMismatchCode);
    }

    [Fact]
    public void CandidateSearchFindsFlatLushGroundWithoutPuttingVegetationInsideTheFootprint()
    {
        var context = Context(Surface([.5f, .5f, .5f, .5f]), Occupancy(20, 16, 0));
        var area = context.State.Project.Areas.Single();

        var candidates = TerrainPlacementCandidateFinder.Find(
            area, 4, 4, 0, 0, 20, 4, .35f, "vegetation", 10, 18, 0, 5, 100);

        var best = Assert.Single(candidates, item => item.X == 0 && item.Z == 0);
        Assert.Equal(20, best.NearbyEnvironmentInstances);
        Assert.Equal(0, best.FootprintEnvironmentInstances);
        Assert.Equal(5, best.GroundHeight, 4);
        Assert.True(best.NearbyEnvironmentDensityPerHectare >= 100);
    }

    private static BuildContext Context(
        TerrainPlacementSurfaceDefinition surface,
        AreaEnvironmentOccupancyProfile occupancy,
        bool explicitTransform = false,
        bool requireVegetation = false,
        string? semanticRoleTag = null,
        BlueprintKind blueprintKind = BlueprintKind.Placeable,
        bool prefabComposition = false,
        bool addSecondContentNode = false)
    {
        var areaId = AreaId.From(Guid.Parse("d1590000-0000-4000-8000-000000000001"));
        var blueprintId = BlueprintId.From(Guid.Parse("d1590000-0000-4000-8000-000000000002"));
        var area = new AreaDefinition(areaId, "terrain-aware-build", "Terrain aware build", [],
            AreaKind.Exterior, new AreaDimensions(64, 64));
        var disabledTerrain = new TerrainDefinition(area.World.Terrain.Id, 1, TerrainProviderKind.None,
            64, 64, 2, 2, 2, 1, 0, [0, 0, 0, 0], [], [], string.Empty, string.Empty,
            string.Empty, "test:generated-scene-owned-terrain");
        var world = new AreaWorldProfile(area.World.Id, area.World.SchemaVersion, disabledTerrain,
            area.World.Biomes, area.World.Features, area.World.VolumetricNavigation, area.World.FlightRoutes,
            area.World.Loading, "test:promoted-world", surface, occupancy);
        area = area.WithWorld(world);
        var childBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("d1590000-0000-4000-8000-000000000007")),
            "prefab-child", "Prefab child", BlueprintKind.Placeable);
        var prefab = prefabComposition
            ? new PrefabCompositionDefinition(
                Guid.Parse("d1590000-0000-4000-8000-000000000008"),
                PrefabCompositionDefinition.CurrentSchemaVersion,
                "test:terrain-aware-prefab",
                [new PrefabCompositionNode(
                    Guid.Parse("d1590000-0000-4000-8000-000000000009"),
                    childBlueprint.Id,
                    null,
                    null,
                    TransformValue.Identity,
                    ["foundation"])],
                [], [], [])
            : null;
        var authoring = prefab is null
            ? null
            : new BlueprintAuthoringMetadata(
                BlueprintAuthoringMetadata.CurrentSchemaVersion,
                "1.0.0",
                "test.terrain.prefab",
                "content/prefab.json",
                prefabComposition: prefab);
        var blueprint = new BlueprintDefinition(blueprintId, "content", "Content", blueprintKind,
            authoring: authoring);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("d1590000-0000-4000-8000-000000000003")),
            "Terrain aware project", prefabComposition ? [blueprint, childBlueprint] : [blueprint], [area]);
        var nodes = new List<AssistedAreaPlanNode>
        {
            new AssistedAreaPlanNode(Guid.Parse("d1590000-0000-4000-8000-000000000011"), 1,
                AssistedAreaPlanNodeKind.Entry, "Entry", -18, 0, 4, 4, [], ["entry"]),
            new AssistedAreaPlanNode(Guid.Parse("d1590000-0000-4000-8000-000000000012"), 1,
                AssistedAreaPlanNodeKind.Social, semanticRoleTag is null ? "House" : "Villager",
                addSecondContentNode ? -16 : 0, 0, 8, 8,
                [blueprintId.Value], [semanticRoleTag ?? "building"],
                explicitTransform ? AssistedAreaPlacementMode.ExplicitTransform : AssistedAreaPlacementMode.TerrainSurface,
                new TransformValue(new Vector3Value(0, 99, 0), QuaternionValue.Identity, Vector3Value.One)),
        };
        if (addSecondContentNode)
            nodes.Add(new AssistedAreaPlanNode(Guid.Parse("d1590000-0000-4000-8000-000000000015"), 1,
                AssistedAreaPlanNodeKind.Social, "Second content", 16, 0, 8, 8,
                [blueprintId.Value], ["social"], AssistedAreaPlacementMode.TerrainSurface));
        nodes.Add(new AssistedAreaPlanNode(Guid.Parse("d1590000-0000-4000-8000-000000000013"), 1,
            AssistedAreaPlanNodeKind.Objective, "Square", 10, 0, 4, 4, [], ["square"]));
        nodes.Add(new AssistedAreaPlanNode(Guid.Parse("d1590000-0000-4000-8000-000000000014"), 1,
            AssistedAreaPlanNodeKind.Exit, "Exit", 18, 0, 4, 4, [], ["exit"]));
        var links = Enumerable.Range(0, nodes.Count - 1).Select(index => new AssistedAreaPlanLink(
            Guid.Parse($"d1590000-0000-4000-8000-{index + 20:000000000000}"), 1,
            nodes[index].Id, nodes[index + 1].Id, "path", true)).ToArray();
        var intent = new AssistedAreaEnvironmentIntent(
            AssistedAreaEnvironmentIntent.CurrentSchemaVersion,
            requireVegetation ? AssistedAreaVegetationIntent.RequirePresence : AssistedAreaVegetationIntent.PreserveExisting,
            requireVegetation ? 10 : 0,
            12,
            .35f,
            "test:explicit-environment-intent",
            requireVegetation ? 24 : 0);
        var plan = new AssistedAreaPlanDefinition(
            Guid.Parse("d1590000-0000-4000-8000-000000000004"), 1, areaId.Value,
            "Terrain-aware village", AssistedAreaArchetype.Village, 159, 48, 32, [blueprintId.Value],
            ["Preserve the authored environment."], ["Build one grounded house."], nodes, links,
            [$"project:{project.Id}"], true, 0, "offline-ci", "test:terrain-aware-build", intent);
        return new(new ProjectState(project, new ProjectRevision(0)), plan,
            new AssistedAreaPlanCatalog(Guid.NewGuid(), 1, [plan], "test:terrain-aware-build"));
    }

    private static TerrainPlacementSurfaceDefinition Surface(IReadOnlyList<float> heights) => new(
        Guid.Parse("d1590000-0000-4000-8000-000000000005"), 1, 64, 64, 2, 2, 10, heights,
        TerrainCellDiagonal.Main, new string('a', 64), "test:surface");

    private static AreaEnvironmentOccupancyProfile Occupancy(
        int vegetationCount,
        float x = 10,
        float z = 0) => new(
        Guid.Parse("d1590000-0000-4000-8000-000000000006"), 1, 64, 64, 4,
        [new("vegetation", "biomes", x, z, vegetationCount)], new string('b', 64), "test:occupancy");

    private sealed record BuildContext(
        ProjectState State,
        AssistedAreaPlanDefinition Plan,
        AssistedAreaPlanCatalog Catalog);
}
