using Forge.Application.Sequences;
using Forge.Domain.Entities;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Sequences;

public sealed class SequencePlayerTests
{
    [Fact]
    public void Timeline_interpolates_transform_emits_signals_and_can_be_skipped()
    {
        var entityId = Guid.NewGuid();
        var transformTrack = new SequenceTrackDefinition(Guid.NewGuid(), "Move", SequenceTrackKind.Transform,
            SequenceTargetKind.Entity, entityId, string.Empty,
            [Key(0, Transform(0), string.Empty), Key(1_000, Transform(10), string.Empty)], "test");
        var signalTrack = new SequenceTrackDefinition(Guid.NewGuid(), "Cue", SequenceTrackKind.Signal,
            SequenceTargetKind.Blackboard, null, string.Empty, [Key(400, null, "door.open")], "test");
        var sequence = new SequenceDefinition(Guid.NewGuid(), 1, "intro", "Intro", 1_000, true, false,
            [transformTrack, signalTrack], [], "test");

        var evaluation = SequencePlayer.Advance(sequence, SequencePlaybackState.Start(sequence.Id), 500, true,
            _ => false);
        Assert.Equal(5, evaluation.Transforms[entityId].Position.X, 3);
        Assert.Contains(evaluation.Signals, item => item.Name == "door.open");
        var skipped = SequencePlayer.Skip(sequence, evaluation.State);
        Assert.True(skipped.State.Skipped);
        Assert.True(skipped.State.Completed);
    }

    [Fact]
    public void Synchronized_timeline_and_server_transform_override_enforce_authority()
    {
        var sequence = new SequenceDefinition(Guid.NewGuid(), 1, "sync", "Sync", 1_000, false, true, [], [],
            "test");
        var denied = SequencePlayer.Advance(sequence, SequencePlaybackState.Start(sequence.Id), 100, false,
            _ => true);
        Assert.Equal(0, denied.State.TimeMilliseconds);
        Assert.Contains(denied.Diagnostics, item => item.Code == SequencePlayer.AuthorityCode);

        var entityId = EntityId.From(Guid.NewGuid());
        var component = new TransformOverrideComponent(Guid.NewGuid(), 1, entityId, null, Transform(0),
            Transform(8), 800, SequenceEasing.SmoothStep, TransformAuthority.Server,
            TransformSaveBehavior.CommitFinal, true, "test");
        var state = TransformOverrideRuntime.Start(component);
        Assert.Equal(state, TransformOverrideRuntime.Advance(component, state, 400, false));
        var advanced = TransformOverrideRuntime.Advance(component, state, 400, true);
        Assert.Equal(4, advanced.Transform.Position.X, 3);
        Assert.True(TransformOverrideRuntime.Cancel(advanced).Cancelled);
    }

    [Fact]
    public void Opposite_quaternion_representations_remain_stable_at_midpoint()
    {
        var from = new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One);
        var to = new TransformValue(Vector3Value.Zero, new QuaternionValue(0, 0, 0, -1), Vector3Value.One);
        var sampled = TransformInterpolator.Sample(from, to, .5, SequenceEasing.Linear);
        Assert.Equal(1, Math.Abs(sampled.Rotation.W), 5);
    }

    [Fact]
    public void Generic_tweens_cover_material_state_easing_cancellation_and_authority()
    {
        var entityId = EntityId.From(Guid.NewGuid());
        var material = new AnimationTweenDefinition(Guid.NewGuid(), 1, entityId,
            AnimationChannelKind.MaterialParameter, "emission", null, null,
            AnimationTweenValue.Color(0, 0, 0, 1), AnimationTweenValue.Color(1, .5f, .25f, 1), 1_000,
            SequenceEasing.Linear, TransformAuthority.Server, TransformSaveBehavior.Resume, true, "test");
        var denied = AnimationTweenRuntime.Advance(material, AnimationTweenRuntime.Start(material), 500, false);
        Assert.Equal(0, denied.ElapsedMilliseconds);
        var sampled = AnimationTweenRuntime.Advance(material, denied, 500, true);
        Assert.Equal(.5f, sampled.Value.X, 3);
        Assert.True(AnimationTweenRuntime.Cancel(sampled).Cancelled);

        var state = new AnimationTweenDefinition(Guid.NewGuid(), 1, entityId, AnimationChannelKind.State,
            "stance", null, null, AnimationTweenValue.State("idle"), AnimationTweenValue.State("alert"), 100,
            SequenceEasing.Linear, TransformAuthority.Owner, TransformSaveBehavior.CommitFinal, false, "test");
        var completed = AnimationTweenRuntime.Advance(state, AnimationTweenRuntime.Start(state), 100, true);
        Assert.Equal("alert", completed.Value.Text);
        Assert.True(completed.Completed);
    }

    [Fact]
    public void Startup_flow_uses_accessible_fallback_when_primary_resource_is_missing()
    {
        var startup = new StartupFlowDefinition(Guid.NewGuid(), 1, StartupFlowKind.Video, null,
            "assets/video/missing.ogv", StartupFlowKind.Menu, null, string.Empty, true, "Passer", "test");
        var profile = new ModuleCinematicProfile(Guid.NewGuid(), 1, startup, [], "test");
        var resolution = StartupFlowResolver.Resolve(profile, _ => false);
        Assert.Equal(StartupFlowKind.Menu, resolution.Kind);
        Assert.True(resolution.UsedFallback);
        Assert.True(resolution.Skippable);
        Assert.Equal("Passer", resolution.SkipLabel);
        Assert.Contains(resolution.Diagnostics, item => item.Code == StartupFlowResolver.MissingCode);
    }

    private static SequenceKeyframe Key(int time, TransformValue? transform, string value) =>
        new(Guid.NewGuid(), time, transform, value, SequenceEasing.Linear);

    private static TransformValue Transform(float x) => new(new Vector3Value(x, 0, 0),
        QuaternionValue.Identity, Vector3Value.One);
}
