namespace Forge.Domain.Assets;

public enum AssetProcessingKind
{
    Texture,
    Model,
}

public enum TextureColorSpace
{
    Srgb,
    Linear,
}

public enum TextureCompressionProfile
{
    Lossless,
    Desktop,
    Mobile,
    Universal,
}

public enum PbrTextureChannel
{
    BaseColor,
    Normal,
    Roughness,
    Metallic,
    AmbientOcclusion,
    Emissive,
    Height,
}

public sealed record TextureProcessingOptions(
    TextureColorSpace ColorSpace,
    TextureCompressionProfile Compression,
    int MaximumDimension,
    decimal TexelDensity,
    IReadOnlyDictionary<PbrTextureChannel, string> Channels);

public sealed record ModelProcessingOptions(
    bool ImportRig,
    bool ImportAnimations,
    bool GenerateCollisions,
    IReadOnlyList<string> Sockets,
    IReadOnlyList<decimal> LodRatios);

public sealed record AssetProcessingProfile
{
    public const int CurrentSchemaVersion = 1;

    public AssetCatalogItemId AssetId { get; }

    public int SchemaVersion { get; }

    public AssetProcessingKind Kind { get; }

    public string SourceFormat { get; }

    public string CanonicalFormat { get; }

    public string LicenseSpdx { get; }

    public string ContentSha256 { get; }

    public string Provenance { get; }

    public long MaximumOutputBytes { get; }

    public TextureProcessingOptions? Texture { get; }

    public ModelProcessingOptions? Model { get; }

    public AssetProcessingProfile(
        AssetCatalogItemId assetId,
        int schemaVersion,
        AssetProcessingKind kind,
        string sourceFormat,
        string canonicalFormat,
        string licenseSpdx,
        string contentSha256,
        string provenance,
        long maximumOutputBytes,
        TextureProcessingOptions? texture = null,
        ModelProcessingOptions? model = null)
    {
        ArgumentNullException.ThrowIfNull(assetId);
        AssetId = assetId;
        SchemaVersion = schemaVersion;
        Kind = kind;
        SourceFormat = NormalizeFormat(sourceFormat);
        CanonicalFormat = NormalizeFormat(canonicalFormat);
        LicenseSpdx = licenseSpdx?.Trim() ?? string.Empty;
        ContentSha256 = contentSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        Provenance = provenance?.Trim() ?? string.Empty;
        MaximumOutputBytes = maximumOutputBytes;
        Texture = texture is null
            ? null
            : texture with
            {
                Channels = (texture.Channels ?? new Dictionary<PbrTextureChannel, string>())
                    .OrderBy(item => item.Key)
                    .ToDictionary(item => item.Key, item => item.Value.Replace('\\', '/').Trim()),
            };
        Model = model is null
            ? null
            : model with
            {
                Sockets = (model.Sockets ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                LodRatios = (model.LodRatios ?? []).OrderDescending().ToArray(),
            };
    }

    private static string NormalizeFormat(string? format) =>
        (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
}
