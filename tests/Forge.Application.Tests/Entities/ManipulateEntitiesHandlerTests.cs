using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Entities;

public sealed class ManipulateEntitiesHandlerTests
{
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("d3300000-0000-4000-8000-000000000001"));
    private static readonly BlueprintId BlueprintId = BlueprintId.From(Guid.Parse("d3300000-0000-4000-8000-000000000002"));
    private static readonly EntityId FirstId = EntityId.From(Guid.Parse("d3300000-0000-4000-8000-000000000003"));
    private static readonly EntityId SecondId = EntityId.From(Guid.Parse("d3300000-0000-4000-8000-000000000004"));
    private readonly ManipulateEntitiesHandler _handler = new(new FixedClock(EntityMutationTestContext.Timestamp));

    [Fact]
    public void MultiTranslationIsAtomicDryRunnableAndUndoable()
    {
        var state = Initial();
        var command = Command(state.Revision, EditorTransformTool.Translate, new Vector3Value(1, 0, -1));
        var preview = _handler.Handle(state, command with { DryRun = true });
        var applied = _handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.False(preview.Audit.Applied);
        Assert.Equal(new Vector3Value(1, 0, -1), applied.State.Project.Areas[0].Entities[0].Transform.Position);
        Assert.Equal(new Vector3Value(3, 0, 1), applied.State.Project.Areas[0].Entities[1].Transform.Position);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(state.Project, history.State.Project);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(applied.State.Project, history.State.Project);
    }

    [Fact]
    public void RotationScaleAndAlignmentSupportGroupPivotAndSnapping()
    {
        var state = Initial();
        var rotated = _handler.Handle(state, Command(state.Revision, EditorTransformTool.Rotate,
            new Vector3Value(0, 44, 0)) with
        { RotationSnapDegrees = 15 });
        var scaled = _handler.Handle(rotated.State, Command(rotated.State.Revision,
            EditorTransformTool.Scale, new Vector3Value(1.25f, 1.25f, 1.25f)) with
        { ScaleSnap = .25f });
        var aligned = _handler.Handle(scaled.State, Command(scaled.State.Revision,
            EditorTransformTool.Translate, Vector3Value.Zero) with
        { Alignment = EditorTransformAlignment.MinimumX });

        Assert.True(rotated.Succeeded);
        Assert.All(rotated.State.Project.Areas[0].Entities, entity => Assert.NotEqual(QuaternionValue.Identity, entity.Transform.Rotation));
        Assert.True(scaled.Succeeded);
        Assert.All(scaled.State.Project.Areas[0].Entities, entity => Assert.Equal(new Vector3Value(1.25f, 1.25f, 1.25f), entity.Transform.Scale));
        Assert.True(aligned.Succeeded);
        Assert.Single(aligned.State.Project.Areas[0].Entities.Select(entity => entity.Transform.Position.X).Distinct());
    }

    [Fact]
    public void InvalidSelectionAndRevisionReturnStableDiagnosticsWithoutPartialMutation()
    {
        var state = Initial();
        var empty = _handler.Handle(state, Command(state.Revision, EditorTransformTool.Translate, Vector3Value.One) with { EntityIds = [] });
        var stale = _handler.Handle(state, Command(new ProjectRevision(99), EditorTransformTool.Translate, Vector3Value.One));

        Assert.Equal(ManipulateEntitiesHandler.EmptySelectionCode, Assert.Single(empty.Diagnostics).Code);
        Assert.Equal(ManipulateEntitiesHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
        Assert.Same(state, empty.State);
        Assert.Same(state, stale.State);
    }

    private static ManipulateEntitiesCommand Command(ProjectRevision revision, EditorTransformTool tool, Vector3Value value) => new(
        CommandId.From(Guid.NewGuid()), ManipulateEntitiesHandler.CurrentSchemaVersion, "direct-manipulation-test",
        revision, AreaId, [FirstId, SecondId], tool, value, EditorCoordinateSpace.World,
        EditorPivotMode.SelectionCenter, EditorTransformAlignment.None, 1, 15, .05f, false);

    private static ProjectState Initial()
    {
        var blueprint = new BlueprintDefinition(BlueprintId, "pillar", "Pillar", BlueprintKind.Placeable);
        var entities = new[]
        {
            new EntityInstance(FirstId, BlueprintId, TransformValue.Identity, ["stone"]),
            new EntityInstance(SecondId, BlueprintId,
                new TransformValue(new Vector3Value(2, 0, 2), QuaternionValue.Identity, Vector3Value.One), ["stone"]),
        };
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("d3300000-0000-4000-8000-000000000005")),
            "Direct manipulation", [blueprint], [new AreaDefinition(AreaId, "room", "Room", entities)]);
        return new ProjectState(project, new ProjectRevision(5));
    }
}
