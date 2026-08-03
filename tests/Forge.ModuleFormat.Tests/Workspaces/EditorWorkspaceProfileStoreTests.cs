using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
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
            Assert.Equal(profile.DisplayFilter.RequiredTags, restored.DisplayFilter.RequiredTags);
            Assert.True(restored.RepeatPlacement);
            Assert.True(File.Exists(Path.Combine(root, EditorWorkspaceProfileStore.RelativePath)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
