using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;

namespace Forge.Domain.Tests.Authoring;

public sealed class EditorWorkspaceProfileTests
{
    [Fact]
    public void Defaults_AreStableForAllSevenRoles()
    {
        var area = AreaId.From(Guid.Parse("4fb58935-3660-4719-b92b-778f1dd8051e"));
        var profiles = Enum.GetValues<EditorRole>().Select(role => EditorWorkspaceProfile.CreateDefault(role, area)).ToArray();

        Assert.Equal(7, profiles.Length);
        Assert.Equal(7, profiles.Select(profile => profile.Id).Distinct().Count());
        Assert.All(profiles, profile => Assert.Equal(area, profile.ActiveAreaId));
        Assert.True(profiles.Single(profile => profile.Role == EditorRole.Beginner).ContextualHelpEnabled);
        Assert.True(profiles.Single(profile => profile.Role == EditorRole.Artist).RepeatPlacement);
    }

    [Fact]
    public void Profile_ValidatesTabsAndNormalizesFilters()
    {
        var area = AreaId.From(Guid.Parse("4fb58935-3660-4719-b92b-778f1dd8051e"));
        var other = AreaId.From(Guid.Parse("690ff4e1-563f-49bb-aa08-e6683f0a4b1b"));
        var filter = new EditorDisplayFilter(
            [BlueprintKind.Creature],
            [" Boss ", "boss", "Hostile"],
            "Karen",
            "draft",
            true,
            true);
        var profile = new EditorWorkspaceProfile(
            EditorWorkspaceProfileId.From(Guid.NewGuid()), "Combat", EditorRole.Builder, filter,
            true, [area, other, area], area, other, true, "test");

        Assert.Equal(["boss", "hostile"], profile.DisplayFilter.RequiredTags);
        Assert.Equal(2, profile.OpenAreaTabs.Count);
        Assert.Throws<ArgumentException>(() => new EditorWorkspaceProfile(
            profile.Id, profile.Name, profile.Role, filter, false, [area], other, null, true, "test"));
    }
}
