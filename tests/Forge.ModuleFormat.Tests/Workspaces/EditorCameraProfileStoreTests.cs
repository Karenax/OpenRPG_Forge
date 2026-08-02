using Forge.Domain.Authoring;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class EditorCameraProfileStoreTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-camera-tests",
        Guid.NewGuid().ToString("N"));
    private static readonly ProjectId ProjectId =
        ProjectId.From(Guid.Parse("ca200000-0000-4000-8000-000000000001"));

    public EditorCameraProfileStoreTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
    }

    [Fact]
    public void MissingProfileReturnsStableProjectDefault()
    {
        var result = EditorCameraProfileStore.LoadOrCreate(_temporaryRoot, ProjectId);

        Assert.True(result.UsedDefault);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(ProjectId.Value, result.Profile.Id.Value);
        Assert.Equal(EditorCameraProjection.Perspective, result.Profile.Pose.Projection);
    }

    [Fact]
    public void SaveAndReloadRoundTripProfileOutsideModuleDocument()
    {
        var initial = EditorCameraProfileStore.LoadOrCreate(_temporaryRoot, ProjectId).Profile;
        var bookmark = new EditorCameraBookmark(
            EditorCameraBookmarkId.From(Guid.Parse("cb200000-0000-4000-8000-000000000001")),
            "Boss",
            initial.Pose);
        var profile = new EditorCameraProfile(
            initial.Id,
            initial.SchemaVersion,
            2.5f,
            0.4f,
            new EditorCameraPose(
                new Vector3Value(10, 1, -4),
                90,
                30,
                12,
                EditorCameraProjection.Orthographic,
                50,
                15),
            [bookmark]);

        var save = EditorCameraProfileStore.Save(_temporaryRoot, profile);
        var restored = EditorCameraProfileStore.LoadOrCreate(_temporaryRoot, ProjectId);

        Assert.True(save.Succeeded);
        Assert.False(restored.UsedDefault);
        Assert.Empty(restored.Diagnostics);
        Assert.Equal(2.5f, restored.Profile.NavigationSpeed);
        Assert.Equal(profile.Pose, restored.Profile.Pose);
        Assert.Equal("Boss", Assert.Single(restored.Profile.Bookmarks).Name);
        Assert.True(File.Exists(Path.Combine(_temporaryRoot, ".forge", "editor-camera.json")));
    }

    [Fact]
    public void InvalidProfileReturnsDefaultWithStableDiagnostic()
    {
        var folder = Path.Combine(_temporaryRoot, ".forge");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "editor-camera.json"), "{ invalid }");

        var result = EditorCameraProfileStore.LoadOrCreate(_temporaryRoot, ProjectId);

        Assert.True(result.UsedDefault);
        Assert.Equal(EditorCameraProfileStore.ReadFailedCode, Assert.Single(result.Diagnostics).Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }
}
