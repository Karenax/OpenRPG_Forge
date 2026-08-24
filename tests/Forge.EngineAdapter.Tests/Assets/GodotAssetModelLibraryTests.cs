using System.Security.Cryptography;
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

    [Fact]
    public void PrepareRuntimeImportPackage_RejectsMissingRequiredDependency()
    {
        var root = CreateRoot();
        var modelRelative = "assets/imported/test/model.glb";
        var modelBytes = new byte[] { 0x67, 0x6c, 0x54, 0x46 };
        WriteChunk(root, modelRelative, modelBytes);
        try
        {
            var assetId = AssetCatalogItemId.ForExternalId("test:missing-texture");
            var modelChunk = CreateChunk(AssetChunkRole.Model, modelRelative, modelBytes);
            var textureChunk = CreateChunk(
                AssetChunkRole.Texture,
                "assets/imported/test/Textures/colormap.png",
                [0x89, 0x50, 0x4e, 0x47]);
            var manifest = CreateManifest(assetId, modelChunk, textureChunk);
            var library = new GodotAssetModelLibrary(root, [manifest]);

            var exception = Assert.Throws<FileNotFoundException>(
                () => library.PrepareRuntimeImportPackage(manifest, modelChunk));

            Assert.Contains("dependency", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PrepareRuntimeImportPackage_RejectsDependencyWhoseHashDiffersFromManifest()
    {
        var root = CreateRoot();
        var modelRelative = "assets/imported/test/model.glb";
        var textureRelative = "assets/imported/test/Textures/colormap.png";
        var modelBytes = new byte[] { 0x67, 0x6c, 0x54, 0x46 };
        WriteChunk(root, modelRelative, modelBytes);
        WriteChunk(root, textureRelative, [0x00, 0x01, 0x02, 0x03]);
        try
        {
            var assetId = AssetCatalogItemId.ForExternalId("test:tampered-texture");
            var modelChunk = CreateChunk(AssetChunkRole.Model, modelRelative, modelBytes);
            var textureChunk = CreateChunk(
                AssetChunkRole.Texture,
                textureRelative,
                [0x89, 0x50, 0x4e, 0x47]);
            var manifest = CreateManifest(assetId, modelChunk, textureChunk);
            var library = new GodotAssetModelLibrary(root, [manifest]);

            var exception = Assert.Throws<InvalidDataException>(
                () => library.PrepareRuntimeImportPackage(manifest, modelChunk));

            Assert.Contains("hash", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot() => Path.Combine(
        Path.GetTempPath(),
        "OpenRPG_Forge",
        "model-library-tests",
        Guid.NewGuid().ToString("N"));

    private static string WriteChunk(string root, string relative, byte[] bytes)
    {
        var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, bytes);
        return absolute;
    }

    private static AssetManifestChunk CreateChunk(AssetChunkRole role, string relative, byte[] bytes) => new(
        Guid.NewGuid(),
        role,
        true,
        relative,
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
        bytes.LongLength,
        bytes.LongLength,
        AssetChunkCompression.None,
        [AssetTargetPlatform.Any]);

    private static AssetPackageManifest CreateManifest(
        AssetCatalogItemId assetId,
        params AssetManifestChunk[] chunks) => new(
        AssetPackageManifest.CurrentSchemaVersion,
        AssetPackageManifestId.ForAsset(assetId),
        assetId,
        "test",
        "CC0-1.0",
        new string('b', 64),
        new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Any] = 1024 },
        chunks,
        null,
        null,
        null,
        null);
}
