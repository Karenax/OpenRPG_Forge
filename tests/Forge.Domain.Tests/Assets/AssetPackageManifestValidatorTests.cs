using Forge.Domain.Assets;

namespace Forge.Domain.Tests.Assets;

public sealed class AssetPackageManifestValidatorTests
{
    [Fact]
    public void ValidManifestCoversChunksCollisionAnimationVfxAndLod()
    {
        var manifest = ValidManifest();

        var diagnostics = AssetPackageManifestValidator.Validate(manifest);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void RequiredExtensionMustResolveToChunk()
    {
        var manifest = ValidManifest() with
        {
            Vfx = new AssetVfxManifest(true, null, "forge.vfxgraph", 512, 8, 40m),
        };

        var diagnostic = Assert.Single(AssetPackageManifestValidator.Validate(manifest));

        Assert.Equal(AssetPackageManifestValidator.ReferenceMissingCode, diagnostic.Code);
        Assert.EndsWith("/vfx/chunkId", diagnostic.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformBudgetAndLodThresholdsAreValidated()
    {
        var manifest = ValidManifest() with
        {
            PlatformBudgets = new Dictionary<AssetTargetPlatform, long>
            {
                [AssetTargetPlatform.Windows] = 512,
            },
            Lod = new AssetLodManifest(
                false,
                0.1m,
                [
                    new(0, ChunkId, 0.5m, 1000),
                    new(2, null, 0.8m, 500),
                ]),
        };

        var codes = AssetPackageManifestValidator.Validate(manifest).Select(item => item.Code).ToArray();

        Assert.Contains(AssetPackageManifestValidator.PlatformBudgetExceededCode, codes);
        Assert.Contains(AssetPackageManifestValidator.LodInvalidCode, codes);
    }

    internal static readonly AssetCatalogItemId AssetId =
        AssetCatalogItemId.From(Guid.Parse("b1100000-0000-4000-8000-000000000001"));
    internal static readonly Guid ChunkId = Guid.Parse("b1100000-0000-4000-8000-000000000002");

    internal static AssetPackageManifest ValidManifest() =>
        new(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(AssetId),
            AssetId,
            "author:test",
            "CC0-1.0",
            new string('a', 64),
            new Dictionary<AssetTargetPlatform, long>
            {
                [AssetTargetPlatform.Windows] = 4 * 1024 * 1024,
            },
            [new AssetManifestChunk(
                ChunkId,
                AssetChunkRole.Model,
                true,
                "assets/imported/tree/model.glb",
                new string('b', 64),
                2048,
                2048,
                AssetChunkCompression.None,
                [AssetTargetPlatform.Windows])],
            new AssetCollisionManifest(
                true,
                ChunkId,
                AssetCollisionGeneration.Embedded,
                ["stone"],
                [AssetTargetPlatform.Windows]),
            new AssetAnimationManifest(
                false,
                ChunkId,
                "humanoid",
                null,
                [new("idle", 1m, false, AssetAnimationCompression.Balanced, ["footstep"])]),
            new AssetVfxManifest(false, null, "forge.vfxgraph", 512, 8, 40m),
            new AssetLodManifest(
                false,
                0.1m,
                [new(0, ChunkId, 1m, 1000), new(1, null, 0.5m, 500)]));
}
