using Forge.Domain.Creatures;
using Forge.Mcp.Creatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class CreatureAuthoringMutationRedTests
{
    [Fact]
    public void CanonicalCreatureFacetsPlanApplyReopenAuditAndRevert()
    {
        using var workspace = new TestWorkspace();
        var opened = ProjectSession.Open(workspace.RootDirectory).Session!;
        var original = opened.Project.Blueprints.First(blueprint => blueprint.CreatureFoundation is not null);
        var foundation = CreatureFoundationProfile.CreateDefault(
            Guid.NewGuid(), original.Slug, "Personnage authored", "author:test");
        var defaultPresentation = CreaturePresentationProfile.CreateDefault(Guid.NewGuid(), "author:test");
        var presentation = new CreaturePresentationProfile(
            defaultPresentation.Id,
            CreaturePresentationProfile.CurrentSchemaVersion,
            "author:test",
            "nonbinary",
            "humanoid-medium",
            defaultPresentation.Pronouns,
            [new CreaturePresentationProfile.PortraitVariant(
                "neutral", "assets/portraits/authored-256.png", 256, 256, "CC0-1.0",
                new string('a', 64), "Portrait authored")],
            false,
            "rig.humanoid",
            [new CreaturePresentationProfile.BodySlot(
                "head", "asset.authored-helmet", "rig.humanoid", 1, ["hair"], "hide-covered")],
            defaultPresentation.MaterialColors,
            [],
            null);
        var progression = CreatureProgressionProfile.CreateDefault(Guid.NewGuid(), "author:test");
        var defaultRole = CreatureRoleBehaviorProfile.CreateDefault(Guid.NewGuid(), "author:test");
        var role = new CreatureRoleBehaviorProfile(
            defaultRole.Id, defaultRole.SchemaVersion, defaultRole.Provenance, defaultRole.RequiredAtRuntime,
            defaultRole.Role, defaultRole.Ambient,
            new CreatureRoleBehaviorProfile.ProgressionTemplate(
                Guid.NewGuid(), "progression.manual", 1, 1, false, true,
                [new CreatureRoleBehaviorProfile.TrackAllocation("adventurer", 1)], [], []),
            defaultRole.Overrides);
        var appliedPlans = new List<string>();
        Assert.True(role.AutoLevel.ManualLock);

        var staleFoundation = workspace.Project.PlanCreatureAuthoring(new CreatureAuthoringPlanRequest(
            1, workspace.Project.GetCurrentRevision(), original.Id.ToString(),
            CreatureAuthoringFacet.Foundation, Foundation: foundation));
        Assert.True(staleFoundation.Succeeded);

        Apply(CreatureAuthoringFacet.RoleBehavior, roleBehavior: role);
        var staleApply = workspace.Project.ApplyCreatureAuthoringPlan(staleFoundation.PlanId!);
        Assert.False(staleApply.Succeeded);
        Assert.Contains(staleApply.Diagnostics, item => item.Code == "command.revision_conflict");
        Apply(CreatureAuthoringFacet.Foundation, foundation: foundation);
        Apply(CreatureAuthoringFacet.Presentation, presentation: presentation);
        Apply(CreatureAuthoringFacet.Progression, progression: progression);

        var reopened = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(blueprint => blueprint.Id == original.Id);
        Assert.Equal(foundation.Id, reopened.CreatureFoundation!.Id);
        Assert.Equal(presentation.Id, reopened.CreaturePresentation!.Id);
        Assert.Equal(progression.Id, reopened.CreatureProgression!.Id);
        Assert.Equal(role.Id, reopened.CreatureRoleBehavior!.Id);
        Assert.Equal("Personnage authored", reopened.CreatureFoundation.Identity.Names["fr"]);
        Assert.Equal(2, reopened.CreatureFoundation.Resources.Count);
        Assert.Equal("assets/portraits/authored-256.png",
            reopened.CreaturePresentation.Portraits.Single().AssetPath);
        Assert.Equal("idle", reopened.CreatureRoleBehavior.Ambient.IdleSets.Single().AnimationIds.Single());
        Assert.Equal(1, reopened.CreatureProgression.Tracks.Single().Level);

        foreach (var planId in appliedPlans.AsEnumerable().Reverse())
        {
            var reverted = workspace.Project.RevertCreatureAuthoringPlan(planId);
            Assert.True(reverted.Succeeded,
                string.Join(" | ", reverted.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        }

        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(blueprint => blueprint.Id == original.Id);
        Assert.Equal(original.CreatureFoundation?.Id, restored.CreatureFoundation?.Id);
        Assert.Equal(original.CreaturePresentation?.Id, restored.CreaturePresentation?.Id);
        Assert.Equal(original.CreatureProgression?.Id, restored.CreatureProgression?.Id);
        Assert.Equal(original.CreatureRoleBehavior?.Id, restored.CreatureRoleBehavior?.Id);

        foreach (var planId in appliedPlans)
        {
            var redone = workspace.Project.RedoCreatureAuthoringPlan(planId);
            Assert.True(redone.Succeeded,
                string.Join(" | ", redone.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
            Assert.Contains(redone.Audit, entry => entry.Action == "redo" && entry.Succeeded);
        }
        var redoneProject = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Blueprints
            .Single(blueprint => blueprint.Id == original.Id);
        Assert.Equal(foundation.Id, redoneProject.CreatureFoundation!.Id);
        Assert.Equal(presentation.Id, redoneProject.CreaturePresentation!.Id);
        Assert.Equal(progression.Id, redoneProject.CreatureProgression!.Id);
        Assert.Equal(role.Id, redoneProject.CreatureRoleBehavior!.Id);

        void Apply(
            CreatureAuthoringFacet facet,
            CreatureFoundationProfile? foundation = null,
            CreaturePresentationProfile? presentation = null,
            CreatureProgressionProfile? progression = null,
            CreatureRoleBehaviorProfile? roleBehavior = null)
        {
            var revision = workspace.Project.GetCurrentRevision();
            var planned = workspace.Project.PlanCreatureAuthoring(new CreatureAuthoringPlanRequest(
                CreatureAuthoringPlanRequest.CurrentSchemaVersion,
                revision,
                original.Id.ToString(),
                facet,
                foundation,
                presentation,
                progression,
                roleBehavior));

            Assert.True(planned.Succeeded,
                string.Join(" | ", planned.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
            Assert.NotNull(planned.Diff);
            Assert.Equal(revision, workspace.Project.GetCurrentRevision());
            var applied = workspace.Project.ApplyCreatureAuthoringPlan(planned.PlanId!);
            Assert.True(applied.Succeeded,
                string.Join(" | ", applied.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
            Assert.Contains(applied.Audit, entry => entry.Action == "apply" && entry.Succeeded);
            appliedPlans.Add(planned.PlanId!);
        }
    }
}
