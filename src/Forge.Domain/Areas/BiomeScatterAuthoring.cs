using System.Globalization;
using System.Text.RegularExpressions;

namespace Forge.Domain.Areas;

public enum BiomeScatterPatchField
{
    Density,
    Resource,
    Altitude,
    Slope,
    Moisture,
    Scale,
    Tint,
    Focus,
    ExclusionTags,
    Seed,
}

public sealed record BiomeScatterRange(float Minimum, float Maximum);

public sealed record BiomeScatterTint(string Color, float Strength);

public sealed record BiomeScatterFocus(float X, float Z, float Radius);

public sealed record BiomeScatterSettings(
    float? Scale,
    BiomeScatterTint? Tint,
    BiomeScatterFocus? Focus,
    int? Seed);

public sealed record BiomeScatterPatch(
    Guid BiomeId,
    IReadOnlyList<BiomeScatterPatchField> Fields,
    float? Density = null,
    string? Resource = null,
    BiomeScatterRange? Altitude = null,
    BiomeScatterRange? Slope = null,
    BiomeScatterRange? Moisture = null,
    float? Scale = null,
    BiomeScatterTint? Tint = null,
    BiomeScatterFocus? Focus = null,
    IReadOnlyList<string>? ExclusionTags = null,
    int? Seed = null);

public static partial class BiomeScatterAuthoring
{
    private const string ScalePrefix = "scale:";
    private const string TintColorPrefix = "foliage-tint:";
    private const string TintStrengthPrefix = "foliage-tint-strength:";
    private const string FocusCenterPrefix = "focus-center:";
    private const string FocusRadiusPrefix = "focus-radius:";
    private const string SeedPrefix = "scatter-seed:";

    public static BiomeScatterSettings Read(BiomeLayerDefinition biome)
    {
        ArgumentNullException.ThrowIfNull(biome);
        var scale = ReadFloat(biome.Tags, ScalePrefix);
        var tintColor = ReadString(biome.Tags, TintColorPrefix);
        var tintStrength = ReadFloat(biome.Tags, TintStrengthPrefix);
        var focusCenter = ReadPair(biome.Tags, FocusCenterPrefix);
        var focusRadius = ReadFloat(biome.Tags, FocusRadiusPrefix);
        var seed = ReadInt(biome.Tags, SeedPrefix);
        return new BiomeScatterSettings(
            scale,
            tintColor is not null && tintStrength is not null
                ? new BiomeScatterTint(tintColor, tintStrength.Value)
                : null,
            focusCenter is not null && focusRadius is not null
                ? new BiomeScatterFocus(focusCenter.Value.X, focusCenter.Value.Z, focusRadius.Value)
                : null,
            seed);
    }

    public static AreaWorldProfile Apply(AreaWorldProfile source, IReadOnlyList<BiomeScatterPatch> patches)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(patches);
        if (patches.Count == 0) throw new ArgumentException("At least one biome patch is required.", nameof(patches));
        if (patches.Select(item => item.BiomeId).Distinct().Count() != patches.Count)
            throw new ArgumentException("A biome can only be patched once per command.", nameof(patches));

        var byId = patches.ToDictionary(item => item.BiomeId);
        var missing = byId.Keys.Where(id => source.Biomes.All(item => item.Id != id)).ToArray();
        if (missing.Length > 0)
            throw new ArgumentException($"Biome '{missing[0]:D}' does not exist.", nameof(patches));

        var biomes = source.Biomes.Select(biome =>
            byId.TryGetValue(biome.Id, out var patch) ? Apply(biome, patch) : biome).ToArray();
        return new AreaWorldProfile(source.Id, source.SchemaVersion, source.Terrain, biomes, source.Features,
            source.VolumetricNavigation, source.FlightRoutes, source.Loading, source.Provenance);
    }

    private static BiomeLayerDefinition Apply(BiomeLayerDefinition source, BiomeScatterPatch patch)
    {
        if (patch.BiomeId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(patch.BiomeId));
        ArgumentNullException.ThrowIfNull(patch.Fields);
        if (patch.Fields.Count == 0 || patch.Fields.Distinct().Count() != patch.Fields.Count)
            throw new ArgumentException("Biome patch fields must be non-empty and unique.", nameof(patch.Fields));

        var fields = patch.Fields.ToHashSet();
        var density = fields.Contains(BiomeScatterPatchField.Density)
            ? Required(patch.Density, nameof(patch.Density))
            : source.ScatterDensity;
        var resource = fields.Contains(BiomeScatterPatchField.Resource)
            ? patch.Resource ?? throw new ArgumentNullException(nameof(patch.Resource))
            : source.ScatterResource;
        var altitude = fields.Contains(BiomeScatterPatchField.Altitude)
            ? RequiredRange(patch.Altitude, nameof(patch.Altitude), null, null)
            : new BiomeScatterRange(source.MinimumAltitude, source.MaximumAltitude);
        var slope = fields.Contains(BiomeScatterPatchField.Slope)
            ? RequiredRange(patch.Slope, nameof(patch.Slope), 0, 90)
            : new BiomeScatterRange(source.MinimumSlopeDegrees, source.MaximumSlopeDegrees);
        var moisture = fields.Contains(BiomeScatterPatchField.Moisture)
            ? RequiredRange(patch.Moisture, nameof(patch.Moisture), 0, 1)
            : new BiomeScatterRange(source.MinimumMoisture, source.MaximumMoisture);
        if (density is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(patch.Density));

        var tags = source.Tags.ToList();
        if (fields.Contains(BiomeScatterPatchField.Scale))
        {
            if (patch.Scale is { } scale && (!float.IsFinite(scale) || scale is < .1f or > 16f))
                throw new ArgumentOutOfRangeException(nameof(patch.Scale));
            ReplaceTags(tags, [ScalePrefix], patch.Scale is { } value ? [FormatTag(ScalePrefix, value)] : []);
        }
        if (fields.Contains(BiomeScatterPatchField.Tint))
        {
            var tint = patch.Tint;
            if (tint is not null &&
                (!HexColor().IsMatch(tint.Color) || !float.IsFinite(tint.Strength) || tint.Strength is < 0 or > 1))
                throw new ArgumentOutOfRangeException(nameof(patch.Tint));
            ReplaceTags(tags, [TintColorPrefix, TintStrengthPrefix],
                tint is null
                    ? []
                    : [$"{TintColorPrefix}{tint.Color.ToLowerInvariant()}", FormatTag(TintStrengthPrefix, tint.Strength)]);
        }
        if (fields.Contains(BiomeScatterPatchField.Focus))
        {
            var focus = patch.Focus;
            if (focus is not null &&
                (!float.IsFinite(focus.X) || !float.IsFinite(focus.Z) || !float.IsFinite(focus.Radius) ||
                 focus.Radius <= 0))
                throw new ArgumentOutOfRangeException(nameof(patch.Focus));
            ReplaceTags(tags, [FocusCenterPrefix, FocusRadiusPrefix],
                focus is null
                    ? []
                    : [$"{FocusCenterPrefix}{Format(focus.X)},{Format(focus.Z)}", FormatTag(FocusRadiusPrefix, focus.Radius)]);
        }
        if (fields.Contains(BiomeScatterPatchField.Seed))
        {
            ReplaceTags(tags, [SeedPrefix], patch.Seed is { } seed
                ? [$"{SeedPrefix}{seed.ToString(CultureInfo.InvariantCulture)}"]
                : []);
        }

        var exclusions = fields.Contains(BiomeScatterPatchField.ExclusionTags)
            ? patch.ExclusionTags ?? throw new ArgumentNullException(nameof(patch.ExclusionTags))
            : source.ExclusionTags;
        return new BiomeLayerDefinition(source.Id, source.Name, source.MaterialLayerId,
            altitude.Minimum, altitude.Maximum, slope.Minimum, slope.Maximum, moisture.Minimum, moisture.Maximum,
            density, resource, tags, exclusions, source.Priority, source.Provenance);
    }

    private static float Required(float? value, string name)
    {
        if (value is null || !float.IsFinite(value.Value)) throw new ArgumentOutOfRangeException(name);
        return value.Value;
    }

    private static BiomeScatterRange RequiredRange(BiomeScatterRange? value, string name, float? minimum,
        float? maximum)
    {
        if (value is null || !float.IsFinite(value.Minimum) || !float.IsFinite(value.Maximum) ||
            value.Maximum < value.Minimum || (minimum is not null && value.Minimum < minimum) ||
            (maximum is not null && value.Maximum > maximum))
            throw new ArgumentOutOfRangeException(name);
        return value;
    }

    private static void ReplaceTags(List<string> tags, IReadOnlyList<string> prefixes,
        IReadOnlyList<string> replacements)
    {
        tags.RemoveAll(tag => prefixes.Any(prefix => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        tags.AddRange(replacements);
    }

    private static string FormatTag(string prefix, float value) => $"{prefix}{Format(value)}";

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string? ReadString(IReadOnlyList<string> tags, string prefix) => tags
        .FirstOrDefault(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];

    private static float? ReadFloat(IReadOnlyList<string> tags, string prefix) =>
        float.TryParse(ReadString(tags, prefix), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
        float.IsFinite(value)
            ? value
            : null;

    private static int? ReadInt(IReadOnlyList<string> tags, string prefix) =>
        int.TryParse(ReadString(tags, prefix), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static (float X, float Z)? ReadPair(IReadOnlyList<string> tags, string prefix)
    {
        var parts = ReadString(tags, prefix)?.Split(',', StringSplitOptions.TrimEntries);
        return parts is { Length: 2 } &&
               float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
               float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) &&
               float.IsFinite(x) && float.IsFinite(z)
            ? (x, z)
            : null;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}
