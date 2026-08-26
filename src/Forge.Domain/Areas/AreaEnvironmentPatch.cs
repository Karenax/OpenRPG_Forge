namespace Forge.Domain.Areas;

public enum AreaEnvironmentPatchField
{
    Classification,
    Tags,
    Lighting,
    Fog,
    Rendering,
    Sky,
    Wind,
    Precipitation,
    Water,
    Reverb,
    Audio,
    Authoring,
    RequireAuthoringRuntimeMetadata,
}

public sealed record AreaEnvironmentPatch(
    IReadOnlyList<AreaEnvironmentPatchField> Fields,
    AreaLightingProfile? Lighting = null,
    AreaRenderingProfile? Rendering = null,
    WeatherProfile? Precipitation = null,
    AreaAudioProfile? Audio = null,
    AreaEnvironmentClass? Classification = null,
    IReadOnlyList<string>? Tags = null,
    AreaFogProfile? Fog = null,
    SkyProfile? Sky = null,
    WindProfile? Wind = null,
    WaterProfile? Water = null,
    AreaReverbProfile? Reverb = null,
    AreaEnvironmentAuthoringProfile? Authoring = null,
    bool? RequireAuthoringRuntimeMetadata = null)
{
    public IReadOnlyList<string> RelativePaths => Fields.Select(field => field switch
    {
        AreaEnvironmentPatchField.Classification => "environment/classification",
        AreaEnvironmentPatchField.Tags => "environment/tags",
        AreaEnvironmentPatchField.Lighting => "environment/lighting",
        AreaEnvironmentPatchField.Fog => "environment/fog",
        AreaEnvironmentPatchField.Rendering => "environment/rendering",
        AreaEnvironmentPatchField.Sky => "environment/sky",
        AreaEnvironmentPatchField.Wind => "environment/wind",
        AreaEnvironmentPatchField.Precipitation => "environment/precipitation",
        AreaEnvironmentPatchField.Water => "environment/water",
        AreaEnvironmentPatchField.Reverb => "environment/reverb",
        AreaEnvironmentPatchField.Audio => "environment/audio",
        AreaEnvironmentPatchField.Authoring => "environment/authoring",
        AreaEnvironmentPatchField.RequireAuthoringRuntimeMetadata =>
            "environment/require-authoring-runtime-metadata",
        _ => throw new ArgumentOutOfRangeException(nameof(Fields)),
    }).ToArray();

    public static AreaEnvironmentProfile Apply(AreaEnvironmentProfile source, AreaEnvironmentPatch patch)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(patch.Fields);
        if (patch.Fields.Count == 0 || patch.Fields.Distinct().Count() != patch.Fields.Count)
            throw new ArgumentException("Environment patch fields must be non-empty and unique.", nameof(patch));
        var fields = patch.Fields.ToHashSet();
        var classification = fields.Contains(AreaEnvironmentPatchField.Classification)
            ? patch.Classification ?? throw new ArgumentNullException(nameof(patch.Classification))
            : source.Classification;
        var tags = fields.Contains(AreaEnvironmentPatchField.Tags)
            ? patch.Tags ?? throw new ArgumentNullException(nameof(patch.Tags))
            : source.Tags;
        var lighting = fields.Contains(AreaEnvironmentPatchField.Lighting)
            ? patch.Lighting ?? throw new ArgumentNullException(nameof(patch.Lighting))
            : source.Lighting;
        var rendering = fields.Contains(AreaEnvironmentPatchField.Rendering)
            ? patch.Rendering ?? throw new ArgumentNullException(nameof(patch.Rendering))
            : source.Rendering;
        var fog = fields.Contains(AreaEnvironmentPatchField.Fog)
            ? patch.Fog ?? throw new ArgumentNullException(nameof(patch.Fog))
            : source.Fog;
        var sky = fields.Contains(AreaEnvironmentPatchField.Sky)
            ? patch.Sky ?? throw new ArgumentNullException(nameof(patch.Sky))
            : source.Sky;
        var wind = fields.Contains(AreaEnvironmentPatchField.Wind)
            ? patch.Wind ?? throw new ArgumentNullException(nameof(patch.Wind))
            : source.Wind;
        var precipitation = fields.Contains(AreaEnvironmentPatchField.Precipitation)
            ? patch.Precipitation ?? throw new ArgumentNullException(nameof(patch.Precipitation))
            : source.Weather;
        var audio = fields.Contains(AreaEnvironmentPatchField.Audio)
            ? patch.Audio ?? throw new ArgumentNullException(nameof(patch.Audio))
            : source.Audio;
        var water = fields.Contains(AreaEnvironmentPatchField.Water)
            ? patch.Water ?? throw new ArgumentNullException(nameof(patch.Water))
            : source.Water;
        var reverb = fields.Contains(AreaEnvironmentPatchField.Reverb)
            ? patch.Reverb ?? throw new ArgumentNullException(nameof(patch.Reverb))
            : source.Reverb;
        var authoring = fields.Contains(AreaEnvironmentPatchField.Authoring)
            ? patch.Authoring ?? throw new ArgumentNullException(nameof(patch.Authoring))
            : source.Authoring;
        var requireRuntimeMetadata = fields.Contains(AreaEnvironmentPatchField.RequireAuthoringRuntimeMetadata)
            ? patch.RequireAuthoringRuntimeMetadata ??
              throw new ArgumentNullException(nameof(patch.RequireAuthoringRuntimeMetadata))
            : source.RequireAuthoringRuntimeMetadata;
        return new AreaEnvironmentProfile(source.SchemaVersion, classification, tags, lighting,
            fog, audio, rendering, sky, wind, precipitation, water, reverb, authoring, requireRuntimeMetadata);
    }

    public static AreaEnvironmentPatch Between(AreaEnvironmentProfile source, AreaEnvironmentProfile candidate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);
        var fields = new List<AreaEnvironmentPatchField>();
        if (source.Classification != candidate.Classification) fields.Add(AreaEnvironmentPatchField.Classification);
        if (!source.Tags.SequenceEqual(candidate.Tags, StringComparer.Ordinal)) fields.Add(AreaEnvironmentPatchField.Tags);
        if (source.Lighting != candidate.Lighting) fields.Add(AreaEnvironmentPatchField.Lighting);
        if (source.Fog != candidate.Fog) fields.Add(AreaEnvironmentPatchField.Fog);
        if (source.Rendering != candidate.Rendering) fields.Add(AreaEnvironmentPatchField.Rendering);
        if (source.Sky != candidate.Sky) fields.Add(AreaEnvironmentPatchField.Sky);
        if (source.Wind != candidate.Wind) fields.Add(AreaEnvironmentPatchField.Wind);
        if (source.Weather != candidate.Weather) fields.Add(AreaEnvironmentPatchField.Precipitation);
        if (source.Water != candidate.Water) fields.Add(AreaEnvironmentPatchField.Water);
        if (source.Reverb != candidate.Reverb) fields.Add(AreaEnvironmentPatchField.Reverb);
        if (source.Audio != candidate.Audio) fields.Add(AreaEnvironmentPatchField.Audio);
        if (source.Authoring != candidate.Authoring) fields.Add(AreaEnvironmentPatchField.Authoring);
        if (source.RequireAuthoringRuntimeMetadata != candidate.RequireAuthoringRuntimeMetadata)
            fields.Add(AreaEnvironmentPatchField.RequireAuthoringRuntimeMetadata);
        return new(fields, candidate.Lighting, candidate.Rendering, candidate.Weather, candidate.Audio,
            candidate.Classification, candidate.Tags, candidate.Fog, candidate.Sky, candidate.Wind,
            candidate.Water, candidate.Reverb, candidate.Authoring, candidate.RequireAuthoringRuntimeMetadata);
    }
}
