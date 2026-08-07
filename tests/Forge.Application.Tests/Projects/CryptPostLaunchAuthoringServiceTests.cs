using Forge.Application.Projects;
using Forge.Application.Tests.Entities;

namespace Forge.Application.Tests.Projects;

public sealed class CryptPostLaunchAuthoringServiceTests
{
    [Fact]
    public void Creates_deterministic_enabled_profile_for_all_ten_slices()
    {
        var project = new PlaceEntityTestContext().State.Project;
        var first = CryptPostLaunchAuthoringService.Create(project);
        var second = CryptPostLaunchAuthoringService.Create(project);

        Assert.Equal(first.PostLaunch.Id, second.PostLaunch.Id);
        Assert.True(first.PostLaunch.Gameplay.ZeroGravity.Enabled);
        Assert.Single(first.PostLaunch.Gameplay.Achievements);
        Assert.True(first.PostLaunch.LiveDirector.SessionRecording.Enabled);
        Assert.Single(first.PostLaunch.Network.Shards);
        Assert.True(first.PostLaunch.CreatorExperience.DynamicAreas.Enabled);
        Assert.True(first.PostLaunch.Production.MediaGeneration.Enabled);
        Assert.True(first.PostLaunch.Production.SoakTests.Enabled);
    }
}
