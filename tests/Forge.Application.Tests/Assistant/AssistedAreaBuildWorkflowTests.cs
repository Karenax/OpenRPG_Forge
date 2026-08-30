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

public sealed class AssistedAreaBuildWorkflowTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApprovedPlanRequiresExactPreviewAndBuildsOneAtomicUndoableBatch()
    {
        var context = Context(approved: true);
        var history = new ProjectCommandHistory(new ProjectState(context.Project, new ProjectRevision(0)));
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(Timestamp));
        var transaction = Guid.NewGuid();
        var executionPlan = Guid.NewGuid();
        BuildAssistedAreaPlanCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transaction, executionPlan, history.State.Revision,
            context.Plan.Id, AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        var refused = handler.Handle(history.State, Command(false), context.Catalog);
        var preview = handler.Handle(history.State, Command(true), context.Catalog);
        var applied = handler.Handle(history.State, Command(false), context.Catalog);

        Assert.Equal(BuildAssistedAreaPlanHandler.PlanNotPreviewedCode, refused.Diagnostics[0].Code);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Command.Audit.Applied);
        Assert.Equal(2, preview.EntityIds.Count);
        Assert.True(preview.LightingPass);
        Assert.Contains("seed:preserved", preview.SemanticDiff, StringComparison.Ordinal);
        Assert.Equal(context.Project, preview.Command.State.Project);
        Assert.True(applied.Succeeded);
        Assert.Equal(new ProjectRevision(1), applied.Command.NewRevision);
        Assert.Equal(context.Project.Areas[0].Entities.Count + 2,
            applied.Command.State.Project.Areas[0].Entities.Count);
        Assert.All(applied.Command.State.Project.Areas[0].Entities.Except(context.Project.Areas[0].Entities),
            item => Assert.Contains($"assistant-plan:{context.Plan.Id:D}", item.Tags));

        history.Record(applied.Command);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(context.Project.Areas[0].Entities.Count, history.State.Project.Areas[0].Entities.Count);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(context.Project.Areas[0].Entities.Count + 2, history.State.Project.Areas[0].Entities.Count);
    }

    [Fact]
    public void DraftOrInsufficientPermissionCannotBuild()
    {
        var draft = Context(approved: false);
        var state = new ProjectState(draft.Project, new ProjectRevision(0));
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(Timestamp));
        BuildAssistedAreaPlanCommand Command(AssistantPermissionLevel permission) => new(
            CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), state.Revision,
            draft.Plan.Id, permission, true);

        var unapproved = handler.Handle(state, Command(AssistantPermissionLevel.ApplyApprovedPlan), draft.Catalog);
        var approvedContext = Context(approved: true);
        var insufficient = handler.Handle(
            new ProjectState(approvedContext.Project, new ProjectRevision(0)),
            Command(AssistantPermissionLevel.Configure) with { AssistedAreaPlanId = approvedContext.Plan.Id },
            approvedContext.Catalog);

        Assert.Equal(BuildAssistedAreaPlanHandler.ApprovalRequiredCode, unapproved.Diagnostics[0].Code);
        Assert.Equal(BuildAssistedAreaPlanHandler.PermissionDeniedCode, insufficient.Diagnostics[0].Code);
    }

    [Fact]
    public void BuiltPlanCannotBeAppliedTwice()
    {
        var context = Context(approved: true);
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(Timestamp));
        var transaction = Guid.NewGuid();
        var executionPlan = Guid.NewGuid();
        var state = new ProjectState(context.Project, new ProjectRevision(0));
        BuildAssistedAreaPlanCommand Command(ProjectRevision revision, bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transaction, executionPlan, revision, context.Plan.Id,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);
        Assert.True(handler.Handle(state, Command(state.Revision, true), context.Catalog).Succeeded);
        var applied = handler.Handle(state, Command(state.Revision, false), context.Catalog);

        var repeated = handler.Handle(applied.Command.State,
            Command(applied.Command.State.Revision, true) with
            {
                TransactionId = Guid.NewGuid(),
                ExecutionPlanId = Guid.NewGuid(),
            }, context.Catalog);

        Assert.Equal(BuildAssistedAreaPlanHandler.AlreadyBuiltCode, repeated.Diagnostics[0].Code);
    }

    [Fact]
    public void TerrainPlacementProjectsHeightAndPreservesRotationScale()
    {
        var context = Context(approved: true, exterior: true, authoredTransform: true);
        var state = new ProjectState(context.Project, new ProjectRevision(0));
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(Timestamp));
        var transaction = Guid.NewGuid();
        var execution = Guid.NewGuid();
        BuildAssistedAreaPlanCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transaction, execution, state.Revision,
            context.Plan.Id, AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        Assert.True(handler.Handle(state, Command(true), context.Catalog).Succeeded);
        var applied = handler.Handle(state, Command(false), context.Catalog);
        var node = context.Plan.Nodes.Single(item => item.Label == "Hall");
        var placed = applied.Command.State.Project.Areas[0].Entities.Single(item =>
            item.Tags.Contains($"assistant-node:{node.Id:D}", StringComparer.Ordinal));
        var expectedY = context.Project.Areas[0].World.Terrain.HeightAt(node.X, node.Z) + 0.35f;

        Assert.True(applied.Succeeded);
        Assert.Equal(expectedY, placed.Transform.Position.Y, 4);
        Assert.NotEqual(0, placed.Transform.Position.Y);
        Assert.Equal(new QuaternionValue(0, 0.70710677f, 0, 0.70710677f), placed.Transform.Rotation);
        Assert.Equal(new Vector3Value(1.5f, 2f, 0.75f), placed.Transform.Scale);
    }

    [Fact]
    public void ApprovedPlanExpandsPrefabCompositionWithPerNodeVisualsAndProvenance()
    {
        var source = Context(approved: true);
        var child = source.Project.Blueprints.Single(item => item.Kind == BlueprintKind.Placeable);
        var holderId = BlueprintId.From(Guid.Parse("b1250000-0000-4000-8000-000000000021"));
        var rootId = Guid.Parse("b1250000-0000-4000-8000-000000000022");
        var childId = Guid.Parse("b1250000-0000-4000-8000-000000000023");
        var roofId = Guid.Parse("b1250000-0000-4000-8000-000000000030");
        var composition = new PrefabCompositionDefinition(
            Guid.Parse("b1250000-0000-4000-8000-000000000024"), 1, "test:assisted-house",
            [
                new PrefabCompositionNode(rootId, child.Id, null, null,
                    new TransformValue(new Vector3Value(-1, 0, 0), QuaternionValue.Identity, Vector3Value.One),
                    ["foundation", "house-piece", "nonblocking"],
                    new EntityPropertyOverrides(1,
                        [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String,
                            "asset:foundation")])),
                new PrefabCompositionNode(childId, child.Id, rootId, null,
                    new TransformValue(new Vector3Value(2, 0, 0), QuaternionValue.Identity, Vector3Value.One),
                    ["house-piece", "nonblocking", "wall"],
                    new EntityPropertyOverrides(1,
                        [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String,
                            "asset:wall")])),
                new PrefabCompositionNode(roofId, child.Id, rootId, null,
                    new TransformValue(new Vector3Value(0, 3, 0), QuaternionValue.Identity, Vector3Value.One),
                    ["house-piece", "nonblocking", "roof"],
                    new EntityPropertyOverrides(1,
                        [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String,
                            "asset:roof")]))
            ], ["modular-house"], [], []);
        var holder = new BlueprintDefinition(holderId, "medieval-house-prefab", "Medieval house prefab",
            BlueprintKind.Placeable,
            authoring: new BlueprintAuthoringMetadata(1, "1.0.0", "test.assisted.house",
                "content/blueprints/medieval-house-prefab.json", prefabComposition: composition));
        var project = new ProjectDefinition(source.Project.Id, source.Project.Title, source.Project.Module,
            [.. source.Project.Blueprints, holder], source.Project.Areas, source.Project.Dialogues,
            source.Project.Quests);
        var nodes = new[]
        {
            new AssistedAreaPlanNode(Guid.Parse("b1250000-0000-4000-8000-000000000025"), 1,
                AssistedAreaPlanNodeKind.Entry, "Entry", -12, 0, 6, 6, [], ["entry"]),
            new AssistedAreaPlanNode(Guid.Parse("b1250000-0000-4000-8000-000000000026"), 1,
                AssistedAreaPlanNodeKind.Social, "House", -4, 0, 8, 8, [holderId.Value], ["village-house"]),
            new AssistedAreaPlanNode(Guid.Parse("b1250000-0000-4000-8000-000000000027"), 1,
                AssistedAreaPlanNodeKind.Objective, "Square", 4, 0, 6, 6, [], ["square"]),
            new AssistedAreaPlanNode(Guid.Parse("b1250000-0000-4000-8000-000000000028"), 1,
                AssistedAreaPlanNodeKind.Exit, "Exit", 12, 0, 6, 6, [], ["exit"]),
        };
        var links = Enumerable.Range(1, nodes.Length - 1).Select(index =>
            new AssistedAreaPlanLink(Guid.Parse($"b1250000-0000-4000-8000-{index:000000000000}"), 1,
                nodes[index - 1].Id, nodes[index].Id, "path", true)).ToArray();
        var plan = new AssistedAreaPlanDefinition(
            Guid.Parse("b1250000-0000-4000-8000-000000000029"), 1,
            project.Areas.Single().Id.Value, "Prefab village", AssistedAreaArchetype.Village, 2026, 48, 32,
            [holderId.Value], ["Keep navigation."], ["Build a complete house."], nodes, links,
            [$"project:{project.Id}"], true, 0, "offline-ci", "test:assisted-prefab");
        var catalog = new AssistedAreaPlanCatalog(Guid.NewGuid(), 1, [plan], "test:assisted-prefab");
        var state = new ProjectState(project, new ProjectRevision(0));
        var handler = new BuildAssistedAreaPlanHandler(new FixedClock(Timestamp));
        var transaction = Guid.NewGuid();
        var execution = Guid.NewGuid();
        BuildAssistedAreaPlanCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transaction, execution, state.Revision, plan.Id,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        var preview = handler.Handle(state, Command(true), catalog);
        var applied = handler.Handle(state, Command(false), catalog);

        Assert.True(preview.Succeeded, string.Join(" | ", preview.Diagnostics.Select(item => item.Message)));
        Assert.Equal(3, preview.EntityIds.Count);
        Assert.True(applied.Succeeded, string.Join(" | ", applied.Diagnostics.Select(item => item.Message)));
        var added = applied.Command.State.Project.Areas.Single().Entities
            .Except(project.Areas.Single().Entities).ToArray();
        Assert.Equal(3, added.Length);
        Assert.DoesNotContain(added, item => item.BlueprintId == holderId);
        Assert.All(added, item =>
        {
            Assert.Contains("house-piece", item.Tags);
            Assert.Contains("village-house", item.Tags);
            Assert.Contains($"assistant-plan:{plan.Id:D}", item.Tags);
            Assert.Contains(item.Overrides.Properties, property => property.Key == "visual.asset");
        });
    }

    private static BuildContext Context(bool approved, bool exterior = false, bool authoredTransform = false)
    {
        var areaId = AreaId.From(Guid.Parse("b1250000-0000-4000-8000-000000000001"));
        var spawnId = BlueprintId.From(Guid.Parse("b1250000-0000-4000-8000-000000000002"));
        var kitId = BlueprintId.From(Guid.Parse("b1250000-0000-4000-8000-000000000003"));
        var spawn = new EntityInstance(
            EntityId.From(Guid.Parse("b1250000-0000-4000-8000-000000000004")),
            spawnId,
            new TransformValue(new Vector3Value(0, 0.8f, 0), QuaternionValue.Identity, Vector3Value.One),
            ["player"]);
        var area = new AreaDefinition(areaId, "build-test", "Build test", [spawn],
            exterior ? AreaKind.Exterior : AreaKind.Interior,
            new AreaDimensions(64, 64));
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("b1250000-0000-4000-8000-000000000005")),
            "Assisted build",
            [
                new BlueprintDefinition(spawnId, "spawn", "Spawn", BlueprintKind.PlayerSpawn),
                new BlueprintDefinition(kitId, "stone-kit", "Stone kit", BlueprintKind.Placeable),
            ],
            [area]);
        var nodes = new[]
        {
            new AssistedAreaPlanNode(Guid.NewGuid(), 1, AssistedAreaPlanNodeKind.Entry, "Entry", -12, 0, 6, 6, [], ["entry"]),
            new AssistedAreaPlanNode(Guid.NewGuid(), 1, AssistedAreaPlanNodeKind.Exploration, "Hall", -4, 0, 6, 6,
                [kitId.Value], ["hall"],
                placementMode: AssistedAreaPlacementMode.TerrainSurface,
                placementTransform: authoredTransform
                    ? new TransformValue(
                        new Vector3Value(-4, 99, 0),
                        new QuaternionValue(0, 0.70710677f, 0, 0.70710677f),
                        new Vector3Value(1.5f, 2f, 0.75f))
                    : null,
                terrainOffset: authoredTransform ? 0.35f : 0),
            new AssistedAreaPlanNode(Guid.NewGuid(), 1, AssistedAreaPlanNodeKind.Objective, "Objective", 4, 0, 6, 6, [kitId.Value], ["objective"]),
            new AssistedAreaPlanNode(Guid.NewGuid(), 1, AssistedAreaPlanNodeKind.Exit, "Exit", 12, 0, 6, 6, [], ["exit"]),
        };
        var links = Enumerable.Range(1, nodes.Length - 1).Select(index =>
            new AssistedAreaPlanLink(Guid.NewGuid(), 1, nodes[index - 1].Id, nodes[index].Id, "door", true)).ToArray();
        var plan = new AssistedAreaPlanDefinition(
            Guid.Parse("b1250000-0000-4000-8000-000000000006"), 1, areaId.Value, "Approved crypt",
            AssistedAreaArchetype.Crypt, 125, 48, 32, [kitId.Value], ["Keep navigation."], ["Reach objective."],
            nodes, links, [$"project:{project.Id}"], approved, 0, "offline-ci", "test:g12-d");
        return new(project, plan, new AssistedAreaPlanCatalog(Guid.NewGuid(), 1, [plan], "test:g12-d"));
    }

    private sealed record BuildContext(
        ProjectDefinition Project,
        AssistedAreaPlanDefinition Plan,
        AssistedAreaPlanCatalog Catalog);
}
