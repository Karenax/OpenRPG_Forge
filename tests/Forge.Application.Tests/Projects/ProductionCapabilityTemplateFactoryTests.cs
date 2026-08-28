using Forge.Application.Projects;
using Forge.Application.Tests.Entities;

namespace Forge.Application.Tests.Projects;

public sealed class ProductionCapabilityTemplateFactoryTests
{
    [Fact]
    public void Creates_deterministic_enabled_profile_for_all_ten_slices()
    {
        var project = new PlaceEntityTestContext().State.Project;
        var first = ProductionCapabilityTemplateFactory.Create(project);
        var second = ProductionCapabilityTemplateFactory.Create(project);

        Assert.Equal(first.PostLaunch.Id, second.PostLaunch.Id);
        Assert.True(first.PostLaunch.Gameplay.ZeroGravity.Enabled);
        Assert.Equal($"{project.Module.Slug}-completed",
            Assert.Single(first.PostLaunch.Gameplay.Achievements).Key);
        Assert.True(first.PostLaunch.LiveDirector.SessionRecording.Enabled);
        Assert.Equal($"{project.Module.Slug}-default", Assert.Single(first.PostLaunch.Network.Shards).Key);
        Assert.True(first.PostLaunch.CreatorExperience.DynamicAreas.Enabled);
        Assert.True(first.PostLaunch.Production.MediaGeneration.Enabled);
        Assert.True(first.PostLaunch.Production.SoakTests.Enabled);
    }
}
