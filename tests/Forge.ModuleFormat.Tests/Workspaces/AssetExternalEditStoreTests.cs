using Forge.Domain.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssetExternalEditStoreTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-external-edit-tests",
        Guid.NewGuid().ToString("N"));
    private static readonly AssetCatalogItemId AssetId = AssetCatalogItemId.From(
        Guid.Parse("c1300000-0000-4000-8000-000000000001"));

    public AssetExternalEditStoreTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
    }

    [Fact]
    public void MissingDocumentReturnsEmptyConfiguration()
    {
        var result = AssetExternalEditStore.Load(_temporaryRoot);

        Assert.True(result.Succeeded);
        Assert.True(result.FileMissing);
        Assert.Empty(result.Configurations);
    }

    [Fact]
    public void SaveAndReloadAreDeterministic()
    {
        var saved = AssetExternalEditStore.Save(_temporaryRoot, [Configuration()]);
        var path = Path.Combine(_temporaryRoot, "assets", "external-edits.json");
        var first = File.ReadAllText(path);
        var restored = AssetExternalEditStore.Load(_temporaryRoot);
        var savedAgain = AssetExternalEditStore.Save(_temporaryRoot, restored.Configurations);
        var second = File.ReadAllText(path);

        Assert.True(saved.Succeeded);
        Assert.True(savedAgain.Succeeded);
        Assert.True(restored.Succeeded);
        Assert.Equal(first, second);
        var expected = Configuration();
        var actual = Assert.Single(restored.Configurations);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.AssetId, actual.AssetId);
        Assert.Equal(expected.ManifestId, actual.ManifestId);
        Assert.Equal(expected.Operations, actual.Operations);
        Assert.Equal(expected.TargetPlatforms, actual.TargetPlatforms);
        Assert.Equal(expected.SourceContentSha256, actual.SourceContentSha256);
    }

    [Fact]
    public void InvalidDocumentReturnsStableDiagnostic()
    {
        var directory = Path.Combine(_temporaryRoot, "assets");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "external-edits.json"), "{ invalid }");

        var result = AssetExternalEditStore.Load(_temporaryRoot);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Configurations);
        Assert.Equal(AssetExternalEditStore.ReadFailedCode, Assert.Single(result.Diagnostics).Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private static AssetExternalEditConfiguration Configuration() =>
        new(
            AssetExternalEditConfiguration.CurrentSchemaVersion,
            AssetExternalEditId.ForAsset(AssetId),
            AssetId,
            AssetPackageManifestId.ForAsset(AssetId),
            "author:test",
            "CC0-1.0",
            new string('d', 64),
            "normalized/test/tree.obj",
            "assets/imported/tree/model.glb",
            AssetExternalTool.Blender,
            [AssetExternalOperation.ValidateModel, AssetExternalOperation.ExportGlb],
            true,
            [AssetTargetPlatform.Linux, AssetTargetPlatform.Windows]);
}
