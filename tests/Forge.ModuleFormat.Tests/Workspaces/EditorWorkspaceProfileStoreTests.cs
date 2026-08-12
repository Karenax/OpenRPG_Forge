using System.Text.Json.Nodes;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class EditorWorkspaceProfileStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsOutsideCanonicalProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "editor-profile-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var area = AreaId.From(Guid.Parse("529a5fb8-a263-45fd-9ae7-2da595bf3a35"));
            var profile = new EditorWorkspaceProfile(
                EditorWorkspaceProfileId.From(Guid.Parse("cc42b58d-faf7-4a22-b4bb-c70965a439af")),
                "Artiste extérieur", EditorRole.Artist,
                new EditorDisplayFilter([BlueprintKind.Placeable], ["nature"], ghostHidden: true),
                true, [area], area, null, false, "user-profile",
                shell: new EditorShellConfiguration(
                    Guid.Parse("36000000-0000-4000-8000-000000000001"), 1, "test",
                    new EditorShellLayout(Guid.Parse("36000000-0000-4000-8000-000000000002"), 1, "test", 300, 340, 160),
                    EditorExplorerGrouping.ValidationStatus, EditorViewportMode.Performance,
                    new EditorPaletteFilter([BlueprintKind.Placeable], ["nature"], ["stylized"],
                        ["CC0-1.0"], ["desktop"], ["forest"], true)));

            EditorWorkspaceProfileStore.Save(root, profile);
            var restored = EditorWorkspaceProfileStore.TryLoad(root);

            Assert.NotNull(restored);
            Assert.Equal(profile.Id, restored.Id);
            Assert.Equal(profile.DisplayFilter.VisibleKinds, restored.DisplayFilter.VisibleKinds);
            Assert.Equal(profile.DisplayFilter.RequiredTags, restored.DisplayFilter.RequiredTags);
            Assert.Equal(profile.DisplayFilter.Author, restored.DisplayFilter.Author);
            Assert.Equal(profile.DisplayFilter.State, restored.DisplayFilter.State);
            Assert.Equal(profile.DisplayFilter.GhostHidden, restored.DisplayFilter.GhostHidden);
            Assert.True(restored.RepeatPlacement);
            Assert.Equal(EditorWorkspaceProfile.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.Equal(1, restored.UiScale);
            Assert.Equal(EditorExplorerGrouping.ValidationStatus, restored.Shell.ExplorerGrouping);
            Assert.Equal(EditorViewportMode.Performance, restored.Shell.ViewportMode);
            Assert.Equal(300, restored.Shell.Layout.ExplorerWidth);
            Assert.Equal(["forest"], restored.Shell.PaletteFilter.Tags);
            Assert.True(restored.Shell.PaletteFilter.FavoritesOnly);
            Assert.True(restored.Shell.DirectManipulation.Selection.MultiSelectionEnabled);
            Assert.Equal(EditorCoordinateSpace.World, restored.Shell.DirectManipulation.Transform.CoordinateSpace);
            Assert.True(restored.RepeatedPlacementProfile.BrushMode);
            Assert.True(restored.Help.SourcedAssistantEnabled);
            var catalog = EditorWorkspaceProfileStore.TryLoadCatalog(root);
            Assert.NotNull(catalog);
            Assert.Equal(7, catalog.Profiles.Count);
            Assert.Equal(7, catalog.Profiles.Select(candidate => candidate.Role).Distinct().Count());
            Assert.Equal(profile.Id, catalog.ActiveProfileId);
            Assert.True(File.Exists(Path.Combine(root, EditorWorkspaceProfileStore.RelativePath)));
            Assert.True(File.Exists(Path.Combine(root, EditorWorkspaceProfileStore.CatalogRelativePath)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Deserialize_MigratesSchemaOneToScalableDefaults()
    {
        var area = AreaId.From(Guid.Parse("529a5fb8-a263-45fd-9ae7-2da595bf3a35"));
        var current = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, area);
        var legacy = JsonNode.Parse(EditorWorkspaceProfileJsonSerializer.Serialize(current))!.AsObject();
        legacy["schemaVersion"] = 1;
        legacy.Remove("uiScale");
        legacy.Remove("textScale");
        legacy.Remove("primaryFontFamily");
        legacy.Remove("fallbackFontFamilies");
        legacy.Remove("personalFavoriteBlueprintIds");
        legacy.Remove("recentBlueprintIds");
        legacy.Remove("shell");

        var migrated = EditorWorkspaceProfileJsonSerializer.Deserialize(legacy.ToJsonString());

        Assert.Equal(EditorWorkspaceProfile.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(1, migrated.UiScale);
        Assert.Equal(1, migrated.TextScale);
        Assert.Equal("Noto Sans", migrated.PrimaryFontFamily);
        Assert.Equal(["Segoe UI", "Arial"], migrated.FallbackFontFamilies);
        Assert.Empty(migrated.PersonalFavoriteBlueprintIds);
        Assert.Empty(migrated.RecentBlueprintIds);
        Assert.Equal(EditorExplorerGrouping.Area, migrated.Shell.ExplorerGrouping);
        Assert.Equal(EditorViewportMode.Editing, migrated.Shell.ViewportMode);
    }
}
