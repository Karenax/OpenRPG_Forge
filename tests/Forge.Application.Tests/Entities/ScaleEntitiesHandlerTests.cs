using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Entities;

public sealed class ScaleEntitiesHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Handle_AppliesAbsoluteScaleWithDryRunAndHistory()
    {
        var context = new EntityMutationTestContext();
        var entity = Assert.Single(Assert.Single(context.State.Project.Areas).Entities);
        var handler = new ScaleEntitiesHandler(new FixedClock(Timestamp));
        var command = Command(context, [entity.Id], new Vector3Value(1.5f, 2, 0.75f));

        var preview = handler.Handle(context.State, command with { DryRun = true });
        var applied = handler.Handle(context.State, command);
        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Same(context.State, preview.State);
        Assert.Equal(new Vector3Value(1.5f, 2, 0.75f),
            Assert.Single(Assert.Single(applied.State.Project.Areas).Entities).Transform.Scale);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(entity.Transform.Scale, Assert.Single(Assert.Single(history.State.Project.Areas).Entities).Transform.Scale);
        Assert.True(history.Redo().Succeeded);
    }

    [Fact]
    public void Handle_MultipliesMultipleEntitiesAtomically()
    {
        var context = new EntityMutationTestContext();
        var original = Assert.Single(Assert.Single(context.State.Project.Areas).Entities);
        var secondId = EntityId.From(Guid.Parse("94745b72-62a1-4de6-a9a9-9ef43924646f"));
        var second = new EntityInstance(secondId, original.BlueprintId,
            new TransformValue(new Vector3Value(-4, 0, 4), original.Transform.Rotation, new Vector3Value(2, 1, 1)),
            ["decor"]);
        var area = Assert.Single(context.State.Project.Areas).WithEntities([original, second]);
        var project = new Forge.Domain.Projects.ProjectDefinition(
            context.State.Project.Id, context.State.Project.Title, context.State.Project.Module,
            context.State.Project.Blueprints, [area], context.State.Project.Dialogues, context.State.Project.Quests);
        var state = new ProjectState(project, context.State.Revision);
        var handler = new ScaleEntitiesHandler(new FixedClock(Timestamp));

        var result = handler.Handle(state, new ScaleEntitiesCommand(
            CommandId.From(Guid.NewGuid()), state.Revision, area.Id, [original.Id, second.Id],
            new Vector3Value(2, 2, 2), Multiply: true, DryRun: false));

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        Assert.Equal(new Vector3Value(2, 2, 2), result.State.Project.Areas[0].Entities[0].Transform.Scale);
        Assert.Equal(new Vector3Value(4, 2, 2), result.State.Project.Areas[0].Entities[1].Transform.Scale);
    }

    [Fact]
    public void HandleChangesCanonicalCollisionFootprintAndNavigationBakeFingerprint()
    {
        var context = new EntityMutationTestContext();
        var area = Assert.Single(context.State.Project.Areas);
        var entity = Assert.Single(area.Entities);
        var agent = area.Navigation.AgentProfiles[0];
        var before = NavigationSurfaceBuilder.Build(context.State.Project, area, agent);
        var handler = new ScaleEntitiesHandler(new FixedClock(Timestamp));

        var result = handler.Handle(context.State, Command(context, [entity.Id], new Vector3Value(4, 1, 4)));
        var updatedArea = Assert.Single(result.State.Project.Areas);
        var after = NavigationSurfaceBuilder.Build(result.State.Project, updatedArea, agent);

        Assert.True(result.Succeeded);
        Assert.NotEqual(before.SourceFingerprint, after.SourceFingerprint);
        Assert.True(after.WalkableCells.Count < before.WalkableCells.Count);
        Assert.Equal(new Vector3Value(4, 1, 4), Assert.Single(updatedArea.Entities).Transform.Scale);
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(101f)]
    public void Handle_RejectsScaleOutsideConstraints(float value)
    {
        var context = new EntityMutationTestContext();
        var entity = Assert.Single(Assert.Single(context.State.Project.Areas).Entities);
        var handler = new ScaleEntitiesHandler(new FixedClock(Timestamp));

        var result = handler.Handle(context.State, Command(context, [entity.Id], new Vector3Value(value, 1, 1)));

        Assert.False(result.Succeeded);
        Assert.Equal(ScaleEntitiesHandler.InvalidScaleCode, Assert.Single(result.Diagnostics).Code);
    }

    private static ScaleEntitiesCommand Command(
        EntityMutationTestContext context,
        IReadOnlyList<EntityId> ids,
        Vector3Value scale) => new(
            CommandId.From(Guid.Parse("86eec837-e9a6-4771-a813-dc18b67cef31")),
            context.State.Revision,
            Assert.Single(context.State.Project.Areas).Id,
            ids,
            scale,
            Multiply: false,
            DryRun: false);
}
