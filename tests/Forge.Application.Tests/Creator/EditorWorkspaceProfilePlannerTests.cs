using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;

namespace Forge.Application.Tests.Creator;

public sealed class EditorWorkspaceProfilePlannerTests
{
    [Fact]
    public void TabsFavoritesAndRecentsRemainBoundedAndDeterministic()
    {
        var first = AreaId.From(Guid.Parse("f1000000-0000-4000-8000-000000000001"));
        var second = AreaId.From(Guid.Parse("f1000000-0000-4000-8000-000000000002"));
        var blueprint = BlueprintId.From(Guid.Parse("f1000000-0000-4000-8000-000000000003"));
        var profile = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, first);

        profile = EditorWorkspaceProfilePlanner.OpenArea(profile, second);
        profile = EditorWorkspaceProfilePlanner.RecordRecent(profile, blueprint);
        profile = EditorWorkspaceProfilePlanner.TogglePersonalFavorite(profile, blueprint);
        profile = EditorWorkspaceProfilePlanner.ConfigureAccessibility(
            profile, 1.25, 1.5, "Atkinson Hyperlegible", ["Noto Sans", "Segoe UI"]);

        Assert.Equal([first, second], profile.OpenAreaTabs);
        Assert.Equal(second, profile.ActiveAreaId);
        Assert.Equal([blueprint], profile.RecentBlueprintIds);
        Assert.Equal([blueprint], profile.PersonalFavoriteBlueprintIds);
        Assert.Equal(1.25, profile.UiScale);
        Assert.Equal(1.5, profile.TextScale);
        Assert.Equal("Atkinson Hyperlegible", profile.PrimaryFontFamily);

        var reactivated = EditorWorkspaceProfilePlanner.OpenArea(profile, first);
        Assert.Equal([first, second], reactivated.OpenAreaTabs);
        Assert.Equal(first, reactivated.ActiveAreaId);

        var closed = EditorWorkspaceProfilePlanner.CloseArea(profile, second);
        Assert.Equal([first], closed.OpenAreaTabs);
        Assert.Equal(first, closed.ActiveAreaId);

        var reconciled = EditorWorkspaceProfilePlanner.ReconcileAreas(profile, [first], first);
        Assert.Equal([first], reconciled.OpenAreaTabs);
        Assert.Equal(first, reconciled.ActiveAreaId);
        Assert.Null(reconciled.ComparisonAreaId);

        var empty = EditorWorkspaceProfilePlanner.CloseArea(EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, first), first);
        Assert.Empty(empty.OpenAreaTabs);
        Assert.Null(empty.ActiveAreaId);
    }
}
