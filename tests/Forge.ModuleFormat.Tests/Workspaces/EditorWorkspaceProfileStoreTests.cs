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
                true, [area], area, null, false, "user-profile");

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
            Assert.True(File.Exists(Path.Combine(root, EditorWorkspaceProfileStore.RelativePath)));
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

        var migrated = EditorWorkspaceProfileJsonSerializer.Deserialize(legacy.ToJsonString());

        Assert.Equal(EditorWorkspaceProfile.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(1, migrated.UiScale);
        Assert.Equal(1, migrated.TextScale);
        Assert.Equal("Noto Sans", migrated.PrimaryFontFamily);
        Assert.Equal(["Segoe UI", "Arial"], migrated.FallbackFontFamilies);
        Assert.Empty(migrated.PersonalFavoriteBlueprintIds);
        Assert.Empty(migrated.RecentBlueprintIds);
    }
}
