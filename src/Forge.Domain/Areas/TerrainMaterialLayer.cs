namespace Forge.Domain.Areas;

public sealed record TerrainMaterialLayer
{
    public Guid Id { get; }
    public string Name { get; }
    public string AlbedoColor { get; }
    public string MaterialResource { get; }
    public float UvScale { get; }

    public TerrainMaterialLayer(Guid id, string name, string albedoColor, string materialResource, float uvScale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(albedoColor);
        ArgumentNullException.ThrowIfNull(materialResource);
        if (id == Guid.Empty || !IsColor(albedoColor) || !float.IsFinite(uvScale) || uvScale is < 0.01f or > 1024f)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Terrain material layer values are invalid.");
        }

        Id = id;
        Name = name.Trim();
        AlbedoColor = albedoColor.Trim().ToLowerInvariant();
        MaterialResource = WeatherStateProfile.NormalizeOptionalResource(materialResource);
        UvScale = uvScale;
    }

    private static bool IsColor(string value) =>
        value.Length is 7 or 9 && value[0] == '#' && value.AsSpan(1).ToArray().All(Uri.IsHexDigit);
}
