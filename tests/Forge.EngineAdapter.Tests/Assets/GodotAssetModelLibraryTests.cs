using Forge.Domain.Assets;
using Forge.EngineAdapter.Assets;

namespace Forge.EngineAdapter.Tests.Assets;

public sealed class GodotAssetModelLibraryTests
{
    [Fact]
    public void Instantiate_RejectsModelWhoseHashDiffersFromManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "model-library-tests", Guid.NewGuid().ToString("N"));
        var relative = "assets/imported/test/model.glb";
        var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, [0x67, 0x6c, 0x54, 0x46]);
        try
        {
            var assetId = AssetCatalogItemId.ForExternalId("test:tampered-model");
            var chunk = new AssetManifestChunk(
                Guid.NewGuid(),
                AssetChunkRole.Model,
                true,
                relative,
                new string('a', 64),
                4,
                4,
                AssetChunkCompression.None,
                [AssetTargetPlatform.Any]);
            var manifest = new AssetPackageManifest(
                AssetPackageManifest.CurrentSchemaVersion,
                AssetPackageManifestId.ForAsset(assetId),
                assetId,
                "test",
                "CC0-1.0",
                new string('b', 64),
                new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Any] = 1024 },
                [chunk],
                null,
                null,
                null,
                null);
            var library = new GodotAssetModelLibrary(root, [manifest]);

            var exception = Assert.Throws<InvalidDataException>(() => library.Instantiate(assetId.ToString()));

            Assert.Contains("hash", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
