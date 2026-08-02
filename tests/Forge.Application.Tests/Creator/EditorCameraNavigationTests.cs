using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Tests.Entities;
using Forge.Application.Time;
using Forge.Domain.Authoring;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Creator;

public sealed class EditorCameraNavigationTests
{
    private static readonly EditorCameraProfileId ProfileId =
        EditorCameraProfileId.From(Guid.Parse("ca100000-0000-4000-8000-000000000001"));
    private static readonly EditorCameraBookmarkId BookmarkId =
        EditorCameraBookmarkId.From(Guid.Parse("cb100000-0000-4000-8000-000000000001"));
    private readonly NavigateEditorCameraHandler _handler =
        new(new FixedClock(EntityMutationTestContext.Timestamp));

    [Fact]
    public void OrbitPanAndAdaptiveZoomRemainInsideValidatedPose()
    {
        var state = Initial();
        var orbit = Apply(state, EditorCameraOperation.Orbit, x: 40, y: -20);
        var pan = Apply(orbit.State, EditorCameraOperation.Pan, x: 25, y: -10);
        var zoom = Apply(pan.State, EditorCameraOperation.Zoom, x: 3);

        Assert.True(orbit.Succeeded);
        Assert.NotEqual(state.Profile.Pose.YawDegrees, orbit.State.Profile.Pose.YawDegrees);
        Assert.NotEqual(orbit.State.Profile.Pose.Pivot, pan.State.Profile.Pose.Pivot);
        Assert.True(zoom.State.Profile.Pose.Distance < pan.State.Profile.Pose.Distance);
        Assert.Equal(EditorCameraProjection.Perspective, zoom.State.Profile.Pose.Projection);
    }

    [Fact]
    public void FocusAndCardinalViewProduceOrthographicAuthorPose()
    {
        var focus = _handler.Handle(
            Initial(),
            Command(Initial().Revision, EditorCameraOperation.Focus) with
            {
                Target = new Vector3Value(8, 2, -4),
                Radius = 3,
            });
        var top = Apply(focus.State, EditorCameraOperation.SetView, view: EditorCameraView.Top);

        Assert.Equal(new Vector3Value(8, 2, -4), focus.State.Profile.Pose.Pivot);
        Assert.Equal(7.5f, focus.State.Profile.Pose.Distance);
        Assert.Equal(EditorCameraProjection.Orthographic, top.State.Profile.Pose.Projection);
        Assert.Equal(89, top.State.Profile.Pose.PitchDegrees);
    }

    [Fact]
    public void DryRunAndInvalidSettingsDoNotMutateState()
    {
        var state = Initial();
        var preview = _handler.Handle(
            state,
            Command(state.Revision, EditorCameraOperation.SetSpeed, x: 2) with { DryRun = true });
        var invalid = _handler.Handle(
            state,
            Command(state.Revision, EditorCameraOperation.SetSpeed, x: 0));

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.False(preview.Audit.Applied);
        Assert.Equal(
            NavigateEditorCameraHandler.ValueInvalidCode,
            Assert.Single(invalid.Diagnostics).Code);
    }

    [Fact]
    public void BookmarkRecallAndDedicatedHistorySupportUndoRedo()
    {
        var history = new EditorCameraCommandHistory(Initial().Profile);
        var save = _handler.Handle(
            history.State,
            Command(history.State.Revision, EditorCameraOperation.SaveBookmark) with
            {
                BookmarkId = BookmarkId,
                BookmarkName = "Entrée de la crypte",
            });
        history.Record(save);
        var moved = _handler.Handle(
            history.State,
            Command(history.State.Revision, EditorCameraOperation.Pan, x: 100));
        history.Record(moved);
        var recall = _handler.Handle(
            history.State,
            Command(history.State.Revision, EditorCameraOperation.RecallBookmark) with
            {
                BookmarkId = BookmarkId,
            });
        history.Record(recall);

        Assert.Equal(Initial().Profile.Pose, history.State.Profile.Pose);
        Assert.True(history.Undo());
        Assert.Equal(moved.State.Profile.Pose, history.State.Profile.Pose);
        Assert.True(history.Redo());
        Assert.Equal(Initial().Profile.Pose, history.State.Profile.Pose);
    }

    [Fact]
    public void MissingFocusTargetAndBookmarkReturnStableDiagnostics()
    {
        var state = Initial();
        var focus = _handler.Handle(state, Command(state.Revision, EditorCameraOperation.Focus));
        var recall = _handler.Handle(state, Command(state.Revision, EditorCameraOperation.RecallBookmark));

        Assert.Equal(NavigateEditorCameraHandler.TargetMissingCode, Assert.Single(focus.Diagnostics).Code);
        Assert.Equal(NavigateEditorCameraHandler.BookmarkMissingCode, Assert.Single(recall.Diagnostics).Code);
    }

    private EditorCameraCommandResult Apply(
        EditorCameraState state,
        EditorCameraOperation operation,
        float x = 0,
        float y = 0,
        EditorCameraView view = EditorCameraView.Perspective) =>
        _handler.Handle(state, Command(state.Revision, operation, x, y, view));

    private static NavigateEditorCameraCommand Command(
        EditorCameraRevision revision,
        EditorCameraOperation operation,
        float x = 0,
        float y = 0,
        EditorCameraView view = EditorCameraView.Perspective) =>
        new(
            CommandId.From(Guid.NewGuid()),
            revision,
            operation,
            x,
            y,
            View: view);

    private static EditorCameraState Initial() =>
        new(EditorCameraProfile.CreateDefault(ProfileId), new EditorCameraRevision(0));
}
