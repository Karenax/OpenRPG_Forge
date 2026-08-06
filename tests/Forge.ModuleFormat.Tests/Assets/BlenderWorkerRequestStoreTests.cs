using System.Text.Json;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class BlenderWorkerRequestStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-worker-request", Guid.NewGuid().ToString("N"));
    private static readonly AssetCatalogItemId AssetId = AssetCatalogItemId.From(
        Guid.Parse("c1400000-0000-4000-8000-000000000001"));

    public BlenderWorkerRequestStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void WritesDeterministicTypedRequestWithoutExecutableCode()
    {
        var configuration = Configuration();

        var first = BlenderWorkerRequestStore.Save(_root, configuration);
        var path = Path.Combine(_root, first.RequestRelativePath!.Replace('/', Path.DirectorySeparatorChar));
        var firstJson = File.ReadAllText(path);
        var second = BlenderWorkerRequestStore.Save(_root, configuration);
        var secondJson = File.ReadAllText(path);
        using var document = JsonDocument.Parse(secondJson);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("normalize_model", document.RootElement.GetProperty("operations")[1].GetString());
        Assert.Contains("generate_thumbnail", secondJson, StringComparison.Ordinal);
        Assert.Contains("audit_quality", secondJson, StringComparison.Ordinal);
        Assert.False(secondJson.Contains("script", StringComparison.OrdinalIgnoreCase));
        Assert.False(secondJson.Contains("python", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
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
            new string('e', 64),
            "normalized/test/tree.obj",
            "assets/imported/tree/model.glb",
            AssetExternalTool.Blender,
            [
                AssetExternalOperation.ValidateModel,
                AssetExternalOperation.NormalizeModel,
                AssetExternalOperation.ExportGlb,
                AssetExternalOperation.GenerateThumbnail,
                AssetExternalOperation.AuditQuality,
            ],
            true,
            [AssetTargetPlatform.Windows]);
}
