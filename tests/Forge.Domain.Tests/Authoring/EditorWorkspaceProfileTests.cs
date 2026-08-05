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
        Assert.All(profiles, profile => Assert.Equal(EditorWorkspaceProfile.CurrentSchemaVersion, profile.SchemaVersion));
        Assert.All(profiles, profile => Assert.Equal("Noto Sans", profile.PrimaryFontFamily));
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

    [Fact]
    public void Profile_ValidatesScalesFontsAndBoundedPersonalCollections()
    {
        var profile = EditorWorkspaceProfile.CreateDefault(EditorRole.Artist);
        var ids = Enumerable.Range(1, 80)
            .Select(value => BlueprintId.From(new Guid(value, 0, 0, new byte[8])))
            .ToArray();
        var configured = new EditorWorkspaceProfile(
            profile.Id, profile.Name, profile.Role, profile.DisplayFilter, profile.RepeatPlacement,
            [], null, null, profile.ContextualHelpEnabled, "test", uiScale: 1.25, textScale: 1.4,
            primaryFontFamily: "Atkinson Hyperlegible", fallbackFontFamilies: ["Noto Sans", "Arial"],
            personalFavoriteBlueprintIds: ids, recentBlueprintIds: ids);

        Assert.Equal(1.25, configured.UiScale);
        Assert.Equal(1.4, configured.TextScale);
        Assert.Equal(64, configured.PersonalFavoriteBlueprintIds.Count);
        Assert.Equal(24, configured.RecentBlueprintIds.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditorWorkspaceProfile(
            profile.Id, profile.Name, profile.Role, profile.DisplayFilter, false, [], null, null, true, "test",
            uiScale: 3));
    }
}
