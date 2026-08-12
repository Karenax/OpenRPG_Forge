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
        Assert.All(profiles, profile => Assert.Equal(EditorExplorerGrouping.Area, profile.Shell.ExplorerGrouping));
        Assert.All(profiles, profile => Assert.Equal(EditorViewportMode.Editing, profile.Shell.ViewportMode));
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

    [Fact]
    public void ShellConfigurationValidatesLayoutAndNormalizesContextualPalette()
    {
        var profile = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder);
        var layout = new EditorShellLayout(
            Guid.Parse("34000000-0000-4000-8000-000000000001"), 1, "test",
            260, 320, 180, assistantVisible: false);
        var filter = new EditorPaletteFilter(
            [BlueprintKind.Placeable], [" Crypt-Stone ", "crypt-stone"], ["Gothic"],
            ["CC0-1.0"], ["Medieval"], ["Dungeon", "Stone"], true);
        var shell = new EditorShellConfiguration(
            Guid.Parse("34000000-0000-4000-8000-000000000002"), 1, "test",
            layout, EditorExplorerGrouping.Dependency, EditorViewportMode.Navigation, filter);
        var configured = new EditorWorkspaceProfile(
            profile.Id, profile.Name, profile.Role, profile.DisplayFilter, profile.RepeatPlacement,
            [], null, null, true, "test", shell: shell);

        Assert.Equal(EditorExplorerGrouping.Dependency, configured.Shell.ExplorerGrouping);
        Assert.Equal(EditorViewportMode.Navigation, configured.Shell.ViewportMode);
        Assert.Equal(["crypt-stone"], configured.Shell.PaletteFilter.Kits);
        Assert.Equal(["dungeon", "stone"], configured.Shell.PaletteFilter.Tags);
        Assert.True(configured.Shell.PaletteFilter.FavoritesOnly);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditorShellLayout(
            Guid.NewGuid(), 1, "test", 20, 320, 180));
        Assert.Throws<ArgumentException>(() => new EditorShellLayout(
            Guid.NewGuid(), 1, "test", 260, 320, 180,
            false, false, false, false, false));
        Assert.Throws<ArgumentException>(() => new EditorPaletteFilter([]));
    }

    [Fact]
    public void DirectManipulationDefaultsAreStableVersionedAndValidated()
    {
        var profile = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder);
        var direct = profile.Shell.DirectManipulation;

        Assert.NotEqual(Guid.Empty, direct.Id);
        Assert.Equal(1, direct.SchemaVersion);
        Assert.True(direct.Selection.MultiSelectionEnabled);
        Assert.True(direct.Selection.EditCommonPropertiesOnly);
        Assert.Equal(EditorTransformTool.Translate, direct.Transform.Tool);
        Assert.Equal(EditorCoordinateSpace.World, direct.Transform.CoordinateSpace);
        Assert.Equal(EditorPivotMode.SelectionCenter, direct.Transform.PivotMode);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditorTransformToolConfiguration(
            Guid.NewGuid(), 1, "test", translationSnap: 0));
        Assert.Throws<ArgumentException>(() => new EditorSelectionConfiguration(
            Guid.Empty, 1, "test"));
    }

    [Fact]
    public void ProductivityProfilesAreVersionedRoleBoundAndSourced()
    {
        var profile = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder);

        Assert.NotEqual(Guid.Empty, profile.DisplayFilter.Id);
        Assert.True(profile.RepeatedPlacementProfile.BrushMode);
        Assert.Equal(EditorPlacementAlignment.GridPlane, profile.RepeatedPlacementProfile.Alignment);
        Assert.True(profile.MultiArea.ComparisonReadOnly);
        Assert.True(profile.Help.SourcedAssistantEnabled);
        Assert.True(profile.RolePolicy.Allows(EditorPermission.EditAreas));
        Assert.Contains(EditorPanel.Palette, profile.RolePolicy.VisiblePanels);
        Assert.All(EditorContextualHelpCatalog.Topics, topic => Assert.NotEmpty(topic.Citations));
        Assert.Throws<ArgumentException>(() => new EditorRolePolicy(Guid.NewGuid(), 1, "test",
            EditorRole.Builder, [EditorPanel.Viewport], EditorPermission.ViewProject));
        Assert.Throws<ArgumentException>(() => new EditorHelpTopic(Guid.NewGuid(), 1, "test", "x", "X",
            "assisted", [], [], [], [], true).Validate());
        Assert.Empty(EditorProductivityRuntimeResolver.Resolve(null, false).Diagnostics);
        Assert.Equal(EditorProductivityRuntimeResolver.RequiredMissingCode,
            Assert.Single(EditorProductivityRuntimeResolver.Resolve(null, true).Diagnostics).Code);
        Assert.Equal(EditorRole.Builder, EditorProductivityRuntimeResolver.Resolve(profile, true).Handle!.Role);
    }
}
