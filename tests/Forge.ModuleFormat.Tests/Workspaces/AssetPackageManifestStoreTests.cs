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
        var reloaded = Assert.Single(loaded.Manifests);
        Assert.Equal(manifest.Id, reloaded.Id);
        Assert.Equal(2400, reloaded.ModelMetadata!.Metrics.TriangleCount);
        Assert.Equal(["forest", "stylized"], reloaded.ModelMetadata.Styles);
        Assert.Equal("default", Assert.Single(reloaded.ModelMetadata.Variants).Key);
        Assert.Equal(AssetNavigationGeneration.Manual, reloaded.Navigation!.Generation);
        Assert.Equal(["Arab", "Latn"], reloaded.Font!.ShapingScripts);
        Assert.Equal([96, 144, 192], reloaded.Font.DpiBuckets);
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
        var collisionChunkId = Guid.Parse("b1100000-0000-4000-8000-000000000013");
        var navigationChunkId = Guid.Parse("b1100000-0000-4000-8000-000000000014");
        var primaryFontChunkId = Guid.Parse("b1100000-0000-4000-8000-000000000015");
        var fallbackFontChunkId = Guid.Parse("b1100000-0000-4000-8000-000000000016");
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
                [AssetTargetPlatform.Windows]),
            new(collisionChunkId, AssetChunkRole.Collision, true, "assets/imported/test/collision.mesh",
                new string('e', 64), 256, 256, AssetChunkCompression.None, [AssetTargetPlatform.Windows]),
            new(navigationChunkId, AssetChunkRole.Navigation, true, "assets/imported/test/navigation.navmesh",
                new string('f', 64), 256, 256, AssetChunkCompression.None, [AssetTargetPlatform.Windows]),
            new(primaryFontChunkId, AssetChunkRole.Font, true, "assets/fonts/atkinson.ttf",
                new string('1', 64), 256, 256, AssetChunkCompression.None, [AssetTargetPlatform.Windows]),
            new(fallbackFontChunkId, AssetChunkRole.Font, true, "assets/fonts/noto.ttf",
                new string('2', 64), 256, 256, AssetChunkCompression.None, [AssetTargetPlatform.Windows])],
            new AssetCollisionManifest(
                true,
                collisionChunkId,
                AssetCollisionGeneration.Embedded,
                ["default"],
                [AssetTargetPlatform.Windows]),
            null,
            null,
            new AssetLodManifest(false, 0.1m, [new(0, chunkId, 1m, 1000)]),
            new AssetModelMetadata(
                new AssetModelDimensions(1.2m, 3.4m, 1.1m),
                new AssetModelMetrics(1, 1200, 2400, 2, 4, 0, 1, 94),
                ["stylized", "forest"],
                [new("default", "Tree", chunkId, [AssetTargetPlatform.Windows], ["forest"])],
                new AssetCompatibilityManifest(
                    "pbr-metallic-roughness",
                    ArtisticRealism.Stylized,
                    1m,
                    1024,
                    [AssetTargetPlatform.Windows],
                    false),
                "assets/imported/test/model.preview.png",
                new string('c', 64),
                2048,
                "assets/imported/test/model.quality.json",
                new string('d', 64)),
            new AssetNavigationManifest(true, navigationChunkId, AssetNavigationGeneration.Manual,
                ["walkable", "blocked"], [AssetTargetPlatform.Windows]),
            new AssetFontManifest(true, "Atkinson Hyperlegible", primaryFontChunkId,
                [new("Noto Sans", fallbackFontChunkId)], ["Latn", "Arab"], ["fr-FR", "ar"],
                [96, 144, 192], 0.8m, 2m));
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"openrpg-manifest-{Guid.NewGuid():N}");

        public TemporaryWorkspace() => Directory.CreateDirectory(Root);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
