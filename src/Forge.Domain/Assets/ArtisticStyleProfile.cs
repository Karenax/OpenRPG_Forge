namespace Forge.Domain.Assets;

public sealed record ArtisticStyleProfile
{
    public const int CurrentSchemaVersion = 1;

    public ArtisticStyleProfileId Id { get; }

    public int SchemaVersion { get; }

    public double MetersPerUnit { get; }

    public int TargetTexelDensity { get; }

    public double ScaleTolerancePercent { get; }

    public double TexelTolerancePercent { get; }

    public string ShaderFamily { get; }

    public string Era { get; }

    public ArtisticRealism Realism { get; }

    public IReadOnlyList<string> Palette { get; }

    public IReadOnlyList<ArtisticAssetStyle> Assets { get; }

    public string Provenance { get; }

    public ArtisticStyleProfile(
        ArtisticStyleProfileId id,
        double metersPerUnit,
        int targetTexelDensity,
        double scaleTolerancePercent,
        double texelTolerancePercent,
        string shaderFamily,
        string era,
        ArtisticRealism realism,
        IReadOnlyList<string> palette,
        IReadOnlyList<ArtisticAssetStyle> assets,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (metersPerUnit is < 0.001 or > 1000) throw new ArgumentOutOfRangeException(nameof(metersPerUnit));
        if (targetTexelDensity is < 16 or > 8192) throw new ArgumentOutOfRangeException(nameof(targetTexelDensity));
        if (scaleTolerancePercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(scaleTolerancePercent));
        if (texelTolerancePercent is < 0 or > 400) throw new ArgumentOutOfRangeException(nameof(texelTolerancePercent));
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(era);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        var materializedAssets = assets.OrderBy(asset => asset.BlueprintId.ToString(), StringComparer.Ordinal).ToArray();
        if (materializedAssets.Select(asset => asset.BlueprintId).Distinct().Count() != materializedAssets.Length)
        {
            throw new ArgumentException("Artistic asset blueprint IDs must be unique.", nameof(assets));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        MetersPerUnit = metersPerUnit;
        TargetTexelDensity = targetTexelDensity;
        ScaleTolerancePercent = scaleTolerancePercent;
        TexelTolerancePercent = texelTolerancePercent;
        ShaderFamily = shaderFamily.Trim().ToLowerInvariant();
        Era = era.Trim().ToLowerInvariant();
        Realism = realism;
        Palette = palette.Select(value => value.Trim().ToLowerInvariant()).Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(12).ToArray();
        Assets = materializedAssets;
        Provenance = provenance.Trim();
    }

    public static ArtisticStyleProfile CreateDefault(Guid moduleId) =>
        new(
            ArtisticStyleProfileId.From(moduleId),
            metersPerUnit: 1,
            targetTexelDensity: 512,
            scaleTolerancePercent: 20,
            texelTolerancePercent: 50,
            shaderFamily: "pbr-metallic-roughness",
            era: "original-fantasy",
            ArtisticRealism.StylizedRealism,
            ["earth", "ember", "stone", "vegetation"],
            [],
            "openrpg-forge-default");
}
