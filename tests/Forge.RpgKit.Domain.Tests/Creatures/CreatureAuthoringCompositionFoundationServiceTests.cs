using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.RpgKit.Application.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureAuthoringCompositionFoundationServiceTests
{
    [Fact]
    public void CanonicalFoundationBuildsValidatedRpgKitCreature()
    {
        var blueprintId = BlueprintId.From(Guid.NewGuid());
        var foundation = CreatureFoundationProfile.CreateDefault(Guid.NewGuid(), "ember-warden", "Gardien", "author:r077");
        foundation = new(foundation.Id, foundation.SchemaVersion, foundation.Provenance, true,
            foundation.Identity, foundation.DefaultTransform, foundation.Resources, foundation.Movement,
            foundation.References, foundation.Spawned, foundation.Overrides,
            CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r077"), true);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "R077",
            [new BlueprintDefinition(blueprintId, "ember-warden", "Gardien", BlueprintKind.Creature,
                creatureFoundation: foundation)],
            [new AreaDefinition(AreaId.From(Guid.NewGuid()), "lab", "Lab", [])]);

        var result = CreatureAuthoringCompositionFoundationService.Resolve(project, blueprintId);

        Assert.True(result.Succeeded);
        Assert.False(result.UsedFallback);
        Assert.Equal("species.human", result.Creature!.Species!.Id);
        Assert.Contains("guard", result.Creature.Animations!.CombatStances);
        Assert.Equal("mesh.guardian", result.Creature.Visual!.MeshId);
    }

    [Fact]
    public void MissingOptionalProfileIsIgnoredAndRequiredProfileIsDiagnosed()
    {
        var blueprintId = BlueprintId.From(Guid.NewGuid());
        var foundation = CreatureFoundationProfile.CreateDefault(Guid.NewGuid(), "ember-warden", "Gardien", "author:r077");
        var optionalProject = Project(blueprintId, foundation);
        Assert.True(CreatureAuthoringCompositionFoundationService.Resolve(optionalProject, blueprintId).Succeeded);
        Assert.True(CreatureAuthoringCompositionFoundationService.Resolve(optionalProject, blueprintId).UsedFallback);

        var required = new CreatureFoundationProfile(foundation.Id, foundation.SchemaVersion, foundation.Provenance,
            foundation.RequiredAtRuntime, foundation.Identity, foundation.DefaultTransform, foundation.Resources,
            foundation.Movement, foundation.References, foundation.Spawned, foundation.Overrides, null, true);
        var result = CreatureAuthoringCompositionFoundationService.Resolve(Project(blueprintId, required), blueprintId);
        Assert.False(result.Succeeded);
        Assert.Equal(CreatureAuthoringCompositionValidator.MissingRequiredCode, Assert.Single(result.Diagnostics).Code);
    }

    private static ProjectDefinition Project(BlueprintId blueprintId, CreatureFoundationProfile foundation) => new(
        ProjectId.From(Guid.NewGuid()), "R077",
        [new BlueprintDefinition(blueprintId, "ember-warden", "Gardien", BlueprintKind.Creature,
            creatureFoundation: foundation)],
        [new AreaDefinition(AreaId.From(Guid.NewGuid()), "lab", "Lab", [])]);
}
