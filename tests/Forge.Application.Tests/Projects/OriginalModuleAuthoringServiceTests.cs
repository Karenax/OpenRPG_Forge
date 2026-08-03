using Forge.Application.Projects;
using Forge.Domain.Blueprints;

namespace Forge.Application.Tests.Projects;

public sealed class OriginalModuleAuthoringServiceTests
{
    [Fact]
    public void Creates_original_module_only_through_revisioned_authoring_commands()
    {
        var result = OriginalModuleAuthoringService.CreateVeilleeDesEclats(); var project = result.State.Project; Assert.Equal("la-veillee-des-eclats", project.Module.Slug); Assert.Equal("parvis-des-astres", Assert.Single(project.Areas).Slug); Assert.Equal(8, project.Blueprints.Count); Assert.Equal(8, Assert.Single(project.Areas).Entities.Count); Assert.Equal(15, result.Patches.Count); Assert.Single(project.Dialogues); Assert.Single(project.Quests); Assert.Contains(project.Blueprints, item => item.Kind == BlueprintKind.Trigger); Assert.Equal(14, result.State.Revision.Value);
    }
}
