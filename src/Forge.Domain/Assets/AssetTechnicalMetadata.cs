namespace Forge.Domain.Assets;

public sealed record AssetDimensions(double Width, double Height, double Depth)
{
    public AssetDimensions Normalize()
    {
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || !double.IsFinite(Depth) ||
            Width <= 0 || Height <= 0 || Depth <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "Asset dimensions must be finite positive metres.");
        return this;
    }
}

public sealed record AssetTechnicalMetadata
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public AssetDimensions? Dimensions { get; }
    public string Style { get; }
    public string Skeleton { get; }
    public IReadOnlyList<string> Materials { get; }
    public long? TriangleCount { get; }
    public IReadOnlyList<string> Variants { get; }
    public IReadOnlyList<string> Animations { get; }
    public IReadOnlyList<string> Compatibility { get; }

    public AssetTechnicalMetadata(
        int schemaVersion = CurrentSchemaVersion,
        AssetDimensions? dimensions = null,
        string? style = null,
        string? skeleton = null,
        IReadOnlyList<string>? materials = null,
        long? triangleCount = null,
        IReadOnlyList<string>? variants = null,
        IReadOnlyList<string>? animations = null,
        IReadOnlyList<string>? compatibility = null)
    {
        if (schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Unsupported asset technical metadata schema.");
        if (triangleCount is < 0)
            throw new ArgumentOutOfRangeException(nameof(triangleCount), "Triangle count cannot be negative.");
        SchemaVersion = schemaVersion;
        Dimensions = dimensions?.Normalize();
        Style = NormalizeToken(style);
        Skeleton = NormalizeToken(skeleton);
        Materials = NormalizeTokens(materials);
        TriangleCount = triangleCount;
        Variants = NormalizeTokens(variants);
        Animations = NormalizeTokens(animations);
        Compatibility = NormalizeTokens(compatibility);
    }

    private static string NormalizeToken(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static IReadOnlyList<string> NormalizeTokens(IReadOnlyList<string>? values) => (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
