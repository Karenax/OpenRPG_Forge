using Forge.Domain.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssetPackageManifestStoreTests
{
    [Fact]
    public void MissingStoreIsAnEmptyValidState()
    {
        using var fixture = new TemporaryWorkspace();

        var result = AssetPackageManifestStore.Load(fixture.Root);

        Assert.True(result.Succeeded);
        Assert.True(result.FileMissing);
        Assert.Empty(result.Manifests);
    }

    [Fact]
    public void StoreRoundTripIsCanonicalAndStable()
    {
        using var fixture = new TemporaryWorkspace();
        var manifest = Manifest();

        Assert.True(AssetPackageManifestStore.Save(fixture.Root, [manifest]).Succeeded);
        var first = File.ReadAllText(Path.Combine(fixture.Root, AssetPackageManifestStore.RelativePath));
        var loaded = AssetPackageManifestStore.Load(fixture.Root);
        Assert.True(AssetPackageManifestStore.Save(fixture.Root, loaded.Manifests).Succeeded);
        var second = File.ReadAllText(Path.Combine(fixture.Root, AssetPackageManifestStore.RelativePath));

        Assert.True(loaded.Succeeded);
        Assert.Equal(manifest.Id, Assert.Single(loaded.Manifests).Id);
        Assert.Equal(first, second);
    }

    [Fact]
    public void InvalidDocumentReturnsStableDiagnostic()
    {
        using var fixture = new TemporaryWorkspace();
        var path = Path.Combine(fixture.Root, AssetPackageManifestStore.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"formatVersion\":\"99\",\"manifests\":[]}");

        var result = AssetPackageManifestStore.Load(fixture.Root);

        Assert.False(result.Succeeded);
        Assert.Equal(AssetPackageManifestStore.ReadFailedCode, Assert.Single(result.Diagnostics).Code);
    }

    private static AssetPackageManifest Manifest()
    {
        var assetId = AssetCatalogItemId.ForExternalId("test:manifest");
        var chunkId = Guid.Parse("b1100000-0000-4000-8000-000000000012");
        return new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(assetId),
            assetId,
            "author:test",
            "CC0-1.0",
            new string('a', 64),
            new Dictionary<AssetTargetPlatform, long>
            {
                [AssetTargetPlatform.Windows] = 4096,
            },
            [new AssetManifestChunk(
                chunkId,
                AssetChunkRole.Model,
                true,
                "assets/imported/test/model.glb",
                new string('b', 64),
                1024,
                1024,
                AssetChunkCompression.None,
                [AssetTargetPlatform.Windows])],
            new AssetCollisionManifest(
                true,
                chunkId,
                AssetCollisionGeneration.Embedded,
                ["default"],
                [AssetTargetPlatform.Windows]),
            null,
            null,
            new AssetLodManifest(false, 0.1m, [new(0, chunkId, 1m, 1000)]));
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"openrpg-manifest-{Guid.NewGuid():N}");

        public TemporaryWorkspace() => Directory.CreateDirectory(Root);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
