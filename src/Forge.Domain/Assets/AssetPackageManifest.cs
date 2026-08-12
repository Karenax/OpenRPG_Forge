using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Assets;

public sealed record AssetPackageManifestId
{
    public Guid Value { get; }

    private AssetPackageManifestId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An asset package manifest ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static AssetPackageManifestId From(Guid value) => new(value);

    public static AssetPackageManifestId Parse(string value) => From(Guid.Parse(value));

    public static AssetPackageManifestId ForAsset(AssetCatalogItemId assetId)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:asset-manifest:{assetId}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return From(new Guid(bytes[..16]));
    }

    public override string ToString() => Value.ToString("D");
}

public enum AssetChunkRole
{
    Model,
    Texture,
    Collision,
    Animation,
    Vfx,
    Lod,
    Navigation,
    Font,
}

public enum AssetChunkCompression
{
    None,
    Brotli,
    Zstandard,
}

public enum AssetTargetPlatform
{
    Any,
    Windows,
    Linux,
    MacOS,
    Android,
    IOS,
}

public enum AssetCollisionGeneration
{
    Embedded,
    ConvexDecomposition,
    Primitive,
    Manual,
}

public enum AssetNavigationGeneration
{
    Generated,
    Embedded,
    Manual,
}

public enum AssetAnimationCompression
{
    Lossless,
    Balanced,
    Aggressive,
}

public sealed record AssetManifestChunk(
    Guid Id,
    AssetChunkRole Role,
    bool Required,
    string RelativePath,
    string ContentSha256,
    long UncompressedBytes,
    long StoredBytes,
    AssetChunkCompression Compression,
    IReadOnlyList<AssetTargetPlatform> Platforms);

public sealed record AssetCollisionManifest(
    bool Required,
    Guid? ChunkId,
    AssetCollisionGeneration Generation,
    IReadOnlyList<string> MaterialTags,
    IReadOnlyList<AssetTargetPlatform> CookPlatforms);

public sealed record AssetNavigationManifest(
    bool Required,
    Guid? ChunkId,
    AssetNavigationGeneration Generation,
    IReadOnlyList<string> MaterialTags,
    IReadOnlyList<AssetTargetPlatform> CookPlatforms);

public sealed record AssetFontFallbackManifest(
    string Family,
    Guid ChunkId);

public sealed record AssetFontManifest(
    bool Required,
    string PrimaryFamily,
    Guid? PrimaryChunkId,
    IReadOnlyList<AssetFontFallbackManifest> Fallbacks,
    IReadOnlyList<string> ShapingScripts,
    IReadOnlyList<string> LocalizationTestLocales,
    IReadOnlyList<int> DpiBuckets,
    decimal MinimumTextScale,
    decimal MaximumTextScale);

public sealed record AssetAnimationClipManifest(
    string Name,
    decimal DurationSeconds,
    bool RootMotion,
    AssetAnimationCompression Compression,
    IReadOnlyList<string> Events);

public sealed record AssetAnimationManifest(
    bool Required,
    Guid? ChunkId,
    string Rig,
    string? RetargetMap,
    IReadOnlyList<AssetAnimationClipManifest> Clips);

public sealed record AssetVfxManifest(
    bool Required,
    Guid? ChunkId,
    string Provider,
    int MaximumParticles,
    int MaximumDrawCalls,
    decimal CullDistance);

public sealed record AssetLodLevelManifest(
    int Level,
    Guid? ChunkId,
    decimal ScreenRatio,
    int MaximumTriangles);

public sealed record AssetLodManifest(
    bool Required,
    decimal CrossFadeSeconds,
    IReadOnlyList<AssetLodLevelManifest> Levels);

public sealed record AssetModelDimensions(
    decimal ExtentXMeters,
    decimal ExtentYMeters,
    decimal ExtentZMeters);

public sealed record AssetModelMetrics(
    int BaseMeshCount,
    int VertexCount,
    int TriangleCount,
    int MaterialCount,
    int TextureCount,
    int LodMeshCount,
    int CollisionMeshCount,
    int QualityScore);

public sealed record AssetVariantManifest(
    string Key,
    string DisplayName,
    Guid ModelChunkId,
    IReadOnlyList<AssetTargetPlatform> Platforms,
    IReadOnlyList<string> Tags);

public sealed record AssetCompatibilityManifest(
    string ShaderFamily,
    ArtisticRealism Realism,
    decimal MetersPerUnit,
    int TargetTexelDensity,
    IReadOnlyList<AssetTargetPlatform> Platforms,
    bool RuntimeImportVerified);

public sealed record AssetModelMetadata(
    AssetModelDimensions Dimensions,
    AssetModelMetrics Metrics,
    IReadOnlyList<string> Styles,
    IReadOnlyList<AssetVariantManifest> Variants,
    AssetCompatibilityManifest Compatibility,
    string PreviewRelativePath,
    string PreviewContentSha256,
    long PreviewBytes,
    string QualityReportRelativePath,
    string QualityReportContentSha256);

public sealed record AssetPackageManifest(
    int SchemaVersion,
    AssetPackageManifestId Id,
    AssetCatalogItemId AssetId,
    string ModificationProvenance,
    string LicenseSpdx,
    string SourceContentSha256,
    IReadOnlyDictionary<AssetTargetPlatform, long> PlatformBudgets,
    IReadOnlyList<AssetManifestChunk> Chunks,
    AssetCollisionManifest? Collision,
    AssetAnimationManifest? Animation,
    AssetVfxManifest? Vfx,
    AssetLodManifest? Lod,
    AssetModelMetadata? ModelMetadata = null,
    AssetNavigationManifest? Navigation = null,
    AssetFontManifest? Font = null)
{
    public const int CurrentSchemaVersion = 1;
}
