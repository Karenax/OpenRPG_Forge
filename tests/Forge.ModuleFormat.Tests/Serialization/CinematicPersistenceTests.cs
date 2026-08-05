using System.Text.Json.Nodes;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class CinematicPersistenceTests
{
    [Fact]
    public void Project_round_trip_preserves_startup_sequence_tracks_and_camera_anchors()
    {
        var source = WithCinematics(TestProjectFactory.CreateValid());
        var json = ProjectJsonSerializer.Serialize(source);
        var restored = ProjectJsonSerializer.Deserialize(json);

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal(StartupFlowKind.Sequence, restored.Module.RuntimePolicies.Cinematics.Startup.Kind);
        Assert.Single(restored.Module.RuntimePolicies.Cinematics.Sequences);
        Assert.Single(restored.Module.RuntimePolicies.Cinematics.TransformOverrides);
        Assert.Single(restored.Module.RuntimePolicies.Cinematics.Tweens);
        Assert.Single(restored.Areas.Single().RuntimePolicies.Cinematics.CameraAnchors);
    }

    [Fact]
    public void Pre_cinematic_project_and_area_migrate_to_stable_empty_profiles()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(TestProjectFactory.CreateValid()))!.AsObject();
        document["formatVersion"] = ProjectJsonSerializer.PreCinematicFormatVersion;
        document["project"]!["module"]!["runtimePolicies"]!.AsObject().Remove("cinematics");
        foreach (var area in document["project"]!["areas"]!.AsArray())
        {
            area!["runtimePolicies"]!.AsObject().Remove("cinematics");
        }

        var first = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        Assert.Equal(first.Module.RuntimePolicies.Cinematics.Id, second.Module.RuntimePolicies.Cinematics.Id);
        Assert.Empty(first.Module.RuntimePolicies.Cinematics.Sequences);
        Assert.Empty(first.Areas.Single().RuntimePolicies.Cinematics.CameraAnchors);
    }

    private static ProjectDefinition WithCinematics(ProjectDefinition project)
    {
        var area = project.Areas.Single();
        var anchor = new CameraAnchorDefinition(Guid.NewGuid(), "Opening",
            new TransformValue(new Vector3Value(1, 4, 5), QuaternionValue.Identity, Vector3Value.One),
            Vector3Value.Zero, CameraFraming.Wide, 55, 2_000, .08f, ["intro"], "test");
        var areaCinematics = new AreaCinematicProfile(area.Id.Value, 1, [anchor], "test");
        var areaPolicies = new AreaRuntimePolicyProfile(area.RuntimePolicies.Id, 1, area.RuntimePolicies.Heartbeats,
            area.RuntimePolicies.Rest, "test", areaCinematics);
        var sequence = new SequenceDefinition(Guid.NewGuid(), 1, "opening", "Opening", 2_000, true, false,
            [new SequenceTrackDefinition(Guid.NewGuid(), "Camera", SequenceTrackKind.Camera,
                SequenceTargetKind.CameraAnchor, anchor.Id, string.Empty,
                [new SequenceKeyframe(Guid.NewGuid(), 0, null, string.Empty, SequenceEasing.Linear)], "test")],
            [], "test");
        var startup = new StartupFlowDefinition(Guid.NewGuid(), 1, StartupFlowKind.Sequence, sequence.Id,
            string.Empty, StartupFlowKind.None, null, string.Empty, true, "Passer", "test");
        var moduleCinematics = new ModuleCinematicProfile(project.Module.Id.Value, 1, startup, [sequence], "test");
        var entity = area.Entities.First();
        var destination = new TransformValue(new Vector3Value(entity.Transform.Position.X + 1,
            entity.Transform.Position.Y, entity.Transform.Position.Z), entity.Transform.Rotation,
            entity.Transform.Scale);
        var transformOverride = new TransformOverrideComponent(Guid.NewGuid(), 1, entity.Id, null,
            entity.Transform, destination, 2_000, SequenceEasing.SmoothStep, TransformAuthority.Server,
            TransformSaveBehavior.CommitFinal, true, "test");
        var tween = new AnimationTweenDefinition(Guid.NewGuid(), 1, entity.Id, AnimationChannelKind.State,
            "stance", null, null, AnimationTweenValue.State("idle"), AnimationTweenValue.State("alert"), 2_000,
            SequenceEasing.Linear, TransformAuthority.Owner, TransformSaveBehavior.Resume, false, "test");
        moduleCinematics = new ModuleCinematicProfile(project.Module.Id.Value, 1, startup, [sequence], "test",
            [transformOverride], [tween]);
        var defaults = project.Module.RuntimePolicies;
        var modulePolicies = new ModuleRuntimePolicyProfile(defaults.Id, 1, defaults.Heartbeats, defaults.Rest,
            defaults.SequenceAbort, "test", moduleCinematics);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(modulePolicies),
            project.Blueprints, [area.WithRuntimePolicies(areaPolicies)], project.Dialogues, project.Quests);
    }
}
