using Forge.Domain.Blueprints;

namespace Forge.Domain.Assets;

public sealed record ArtisticAssetStyle
{
    public BlueprintId BlueprintId { get; }

    public double MetersPerUnit { get; }

    public int TexelDensity { get; }

    public string ShaderFamily { get; }

    public string Era { get; }

    public ArtisticRealism Realism { get; }

    public IReadOnlyList<string> Palette { get; }

    public string Provenance { get; }

    public ArtisticAssetStyle(
        BlueprintId blueprintId,
        double metersPerUnit,
        int texelDensity,
        string shaderFamily,
        string era,
        ArtisticRealism realism,
        IReadOnlyList<string> palette,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        if (metersPerUnit is < 0.001 or > 1000) throw new ArgumentOutOfRangeException(nameof(metersPerUnit));
        if (texelDensity is < 16 or > 8192) throw new ArgumentOutOfRangeException(nameof(texelDensity));
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(era);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        BlueprintId = blueprintId;
        MetersPerUnit = metersPerUnit;
        TexelDensity = texelDensity;
        ShaderFamily = shaderFamily.Trim().ToLowerInvariant();
        Era = era.Trim().ToLowerInvariant();
        Realism = realism;
        Palette = palette.Select(Normalize).Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(12).ToArray();
        Provenance = provenance.Trim();
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
