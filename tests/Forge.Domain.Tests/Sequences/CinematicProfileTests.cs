using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Sequences;

public sealed class CinematicProfileTests
{
    [Fact]
    public void Project_validator_accepts_owned_anchor_and_rejects_missing_or_out_of_bounds_references()
    {
        var areaId = AreaId.From(Guid.NewGuid());
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Cinematic", [],
            [new AreaDefinition(areaId, "stage", "Stage", [], dimensions: new AreaDimensions(20, 20))]);
        var area = project.Areas.Single();
        var anchor = Anchor(new Vector3Value(2, 3, 2));
        var areaCinematics = new AreaCinematicProfile(areaId.Value, 1, [anchor], "test");
        var areaPolicies = new AreaRuntimePolicyProfile(area.RuntimePolicies.Id, 1, [], area.RuntimePolicies.Rest,
            "test", areaCinematics);
        var sequence = Sequence(anchor.Id);
        var startup = new StartupFlowDefinition(Guid.NewGuid(), 1, StartupFlowKind.Sequence, sequence.Id,
            string.Empty, StartupFlowKind.None, null, string.Empty, true, "Passer", "test");
        var moduleCinematics = new ModuleCinematicProfile(project.Module.Id.Value, 1, startup, [sequence], "test");
        var defaults = project.Module.RuntimePolicies;
        var modulePolicies = new ModuleRuntimePolicyProfile(defaults.Id, 1, [], defaults.Rest,
            defaults.SequenceAbort, "test", moduleCinematics);
        project = new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(modulePolicies),
            project.Blueprints, [area.WithRuntimePolicies(areaPolicies)], project.Dialogues, project.Quests);

        Assert.Empty(ProjectValidator.Validate(project));

        var outside = Anchor(new Vector3Value(40, 3, 40));
        var invalidArea = new AreaCinematicProfile(areaId.Value, 1, [outside], "test");
        var invalidPolicies = new AreaRuntimePolicyProfile(areaPolicies.Id, 1, [], areaPolicies.Rest, "test",
            invalidArea);
        var invalidProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            [area.WithRuntimePolicies(invalidPolicies)], project.Dialogues, project.Quests);
        var diagnostics = ProjectValidator.Validate(invalidProject);
        Assert.Contains(diagnostics, item => item.Code == CinematicValidator.CameraAnchorBoundsCode);
        Assert.Contains(diagnostics, item => item.Code == CinematicValidator.MissingCameraAnchorCode);
    }

    [Fact]
    public void Transform_override_hierarchy_rejects_cycles()
    {
        var areaId = AreaId.From(Guid.NewGuid());
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.NewGuid()), "marker", "Marker",
            BlueprintKind.Placeable);
        var first = new EntityInstance(EntityId.From(Guid.NewGuid()), blueprint.Id, TransformValue.Identity);
        var second = new EntityInstance(EntityId.From(Guid.NewGuid()), blueprint.Id, TransformValue.Identity);
        var area = new AreaDefinition(areaId, "stage", "Stage", [first, second]);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Hierarchy", [blueprint], [area]);
        var overrides = new[]
        {
            Override(first.Id, second.Id),
            Override(second.Id, first.Id),
        };
        var defaults = project.Module.RuntimePolicies;
        var cinematics = new ModuleCinematicProfile(project.Module.Id.Value, 1,
            ModuleCinematicProfile.CreateDefault(project.Module.Id).Startup, [], "test", overrides);
        var policies = new ModuleRuntimePolicyProfile(defaults.Id, 1, [], defaults.Rest, defaults.SequenceAbort,
            "test", cinematics);
        project = new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(policies),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        Assert.Contains(ProjectValidator.Validate(project),
            item => item.Code == CinematicValidator.TransformHierarchyCode);
    }

    private static CameraAnchorDefinition Anchor(Vector3Value position) => new(Guid.NewGuid(), "Opening shot",
        new TransformValue(position, QuaternionValue.Identity, Vector3Value.One), Vector3Value.Zero,
        CameraFraming.Wide, 55, 2_000, .08f, ["opening"], "test");

    private static SequenceDefinition Sequence(Guid anchorId) => new(Guid.NewGuid(), 1, "opening", "Opening",
        2_000, true, false,
        [new SequenceTrackDefinition(Guid.NewGuid(), "Camera", SequenceTrackKind.Camera,
            SequenceTargetKind.CameraAnchor, anchorId, string.Empty,
            [new SequenceKeyframe(Guid.NewGuid(), 0, null, string.Empty, SequenceEasing.Linear)], "test")],
        [], "test");

    private static TransformOverrideComponent Override(EntityId entity, EntityId parent) => new(Guid.NewGuid(), 1,
        entity, parent, TransformValue.Identity, TransformValue.Identity, 1_000, SequenceEasing.Linear,
        TransformAuthority.Server, TransformSaveBehavior.Resume, true, "test");
}
