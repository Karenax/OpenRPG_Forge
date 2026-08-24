using Forge.Application.Projects;
using Forge.Domain.Blueprints;

namespace Forge.Application.Tests.Projects;

public sealed class SampleModuleAuthoringServiceTests
{
    [Fact]
    public void Creates_sample_module_only_through_revisioned_authoring_commands()
    {
        var result = SampleModuleAuthoringService.Create(); var project = result.State.Project; Assert.Equal("module-de-demonstration", project.Module.Slug); Assert.Equal("zone-de-demonstration", Assert.Single(project.Areas).Slug); Assert.Equal(8, project.Blueprints.Count); Assert.Equal(8, Assert.Single(project.Areas).Entities.Count); Assert.Equal(17, result.Patches.Count); Assert.Single(project.Dialogues); Assert.Single(project.Quests); Assert.Contains(project.Blueprints, item => item.Kind == BlueprintKind.Trigger); Assert.All(project.Blueprints.Where(item => item.Kind == BlueprintKind.Creature), item => Assert.NotNull(item.Combat)); Assert.Equal(16, result.State.Revision.Value);
    }
}
