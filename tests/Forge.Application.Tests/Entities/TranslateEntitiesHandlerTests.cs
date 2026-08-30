using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Entities;

public sealed class TranslateEntitiesHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HandleTranslatesMultipleEntitiesAtomicallyWithDryRunAndHistory()
    {
        var context = new EntityMutationTestContext();
        var original = Assert.Single(Assert.Single(context.State.Project.Areas).Entities);
        var second = new EntityInstance(
            EntityId.From(Guid.Parse("c1a17800-0000-4000-8000-000000000001")),
            original.BlueprintId,
            new TransformValue(new Vector3Value(-4, 2, 7), original.Transform.Rotation,
                new Vector3Value(2, 1, 1)),
            ["translation-test"]);
        var area = Assert.Single(context.State.Project.Areas).WithEntities([original, second]);
        var project = new Forge.Domain.Projects.ProjectDefinition(
            context.State.Project.Id, context.State.Project.Title, context.State.Project.Module,
            context.State.Project.Blueprints, [area], context.State.Project.Dialogues, context.State.Project.Quests);
        var state = new ProjectState(project, context.State.Revision);
        var handler = new TranslateEntitiesHandler(new FixedClock(Timestamp));
        var command = new TranslateEntitiesCommand(
            CommandId.From(Guid.Parse("c1a17800-0000-4000-8000-000000000002")),
            state.Revision,
            area.Id,
            [original.Id, second.Id],
            new Vector3Value(1, 0, 1),
            DryRun: false);

        var preview = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(preview.Succeeded, string.Join(" | ", preview.Diagnostics.Select(item => item.Message)));
        Assert.Same(state, preview.State);
        Assert.True(applied.Succeeded, string.Join(" | ", applied.Diagnostics.Select(item => item.Message)));
        Assert.Equal(original.Transform.Position.X + 1,
            applied.State.Project.Areas[0].Entities[0].Transform.Position.X);
        Assert.Equal(second.Transform.Position.Z + 1,
            applied.State.Project.Areas[0].Entities[1].Transform.Position.Z);
        Assert.Equal(original.Transform.Rotation,
            applied.State.Project.Areas[0].Entities[0].Transform.Rotation);
        Assert.Equal(second.Transform.Scale,
            applied.State.Project.Areas[0].Entities[1].Transform.Scale);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(original.Transform.Position, history.State.Project.Areas[0].Entities[0].Transform.Position);
        Assert.True(history.Redo().Succeeded);
    }

    [Fact]
    public void HandleRejectsDeltaOutsideLimit()
    {
        var context = new EntityMutationTestContext();
        var entity = Assert.Single(Assert.Single(context.State.Project.Areas).Entities);
        var result = new TranslateEntitiesHandler(new FixedClock(Timestamp)).Handle(
            context.State,
            new TranslateEntitiesCommand(
                CommandId.From(Guid.NewGuid()),
                context.State.Revision,
                context.State.Project.Areas[0].Id,
                [entity.Id],
                new Vector3Value(TranslateEntitiesHandler.MaximumComponent + 1, 0, 0),
                DryRun: false));

        Assert.False(result.Succeeded);
        Assert.Equal(TranslateEntitiesHandler.InvalidDeltaCode, Assert.Single(result.Diagnostics).Code);
    }
}
