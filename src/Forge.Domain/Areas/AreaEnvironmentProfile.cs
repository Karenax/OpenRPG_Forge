namespace Forge.Domain.Areas;

public enum AreaEnvironmentClass { Interior, Exterior, Natural, Artificial, Underground }
public enum DayNightMode { Clock, AlwaysDay, AlwaysNight }
public enum FogMode { Disabled, Distance, Volumetric }

public sealed record AreaLightingProfile
{
    public DayNightMode Mode { get; }
    public float DayLengthMinutes { get; }
    public float AmbientIntensity { get; }
    public float DirectionalIntensity { get; }
    public float ColorTemperatureKelvin { get; }
    public bool Hdr { get; }
    public bool Shadows { get; }
    public AreaLightingProfile(DayNightMode mode, float dayLengthMinutes, float ambientIntensity,
        float directionalIntensity, float colorTemperatureKelvin, bool hdr, bool shadows)
    {
        if (!float.IsFinite(dayLengthMinutes) || dayLengthMinutes <= 0 || !float.IsFinite(ambientIntensity) || ambientIntensity < 0 ||
            !float.IsFinite(directionalIntensity) || directionalIntensity < 0 || colorTemperatureKelvin is < 1000 or > 20000)
            throw new ArgumentOutOfRangeException(nameof(dayLengthMinutes));
        Mode = mode; DayLengthMinutes = dayLengthMinutes; AmbientIntensity = ambientIntensity;
        DirectionalIntensity = directionalIntensity; ColorTemperatureKelvin = colorTemperatureKelvin; Hdr = hdr; Shadows = shadows;
    }
}

public sealed record AreaFogProfile
{
    public FogMode Mode { get; }
    public float Density { get; }
    public float StartDistance { get; }
    public float EndDistance { get; }
    public float Height { get; }
    public AreaFogProfile(FogMode mode, float density, float startDistance, float endDistance, float height)
    {
        if (density is < 0 or > 1 || startDistance < 0 || endDistance < startDistance || !float.IsFinite(height))
            throw new ArgumentOutOfRangeException(nameof(density));
        Mode = mode; Density = density; StartDistance = startDistance; EndDistance = endDistance; Height = height;
    }
}

public sealed record AreaAudioProfile
{
    public string DayMusic { get; }
    public string NightMusic { get; }
    public string CombatMusic { get; }
    public string Ambience { get; }
    public string ReverbPreset { get; }
    public float TransitionSeconds { get; }
    public AreaAudioProfile(string dayMusic, string nightMusic, string combatMusic, string ambience, string reverbPreset, float transitionSeconds)
    {
        ArgumentNullException.ThrowIfNull(dayMusic); ArgumentNullException.ThrowIfNull(nightMusic);
        ArgumentNullException.ThrowIfNull(combatMusic); ArgumentNullException.ThrowIfNull(ambience); ArgumentException.ThrowIfNullOrWhiteSpace(reverbPreset);
        if (!float.IsFinite(transitionSeconds) || transitionSeconds < 0) throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
        DayMusic = dayMusic.Trim(); NightMusic = nightMusic.Trim(); CombatMusic = combatMusic.Trim(); Ambience = ambience.Trim();
        ReverbPreset = reverbPreset.Trim(); TransitionSeconds = transitionSeconds;
    }
}

public sealed record AreaEnvironmentProfile
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; }
    public AreaEnvironmentClass Classification { get; }
    public IReadOnlyList<string> Tags { get; }
    public AreaLightingProfile Lighting { get; }
    public AreaFogProfile Fog { get; }
    public AreaAudioProfile Audio { get; }
    public AreaEnvironmentProfile(int schemaVersion, AreaEnvironmentClass classification, IReadOnlyList<string> tags,
        AreaLightingProfile lighting, AreaFogProfile fog, AreaAudioProfile audio)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(tags); ArgumentNullException.ThrowIfNull(lighting); ArgumentNullException.ThrowIfNull(fog); ArgumentNullException.ThrowIfNull(audio);
        SchemaVersion = schemaVersion; Classification = classification; Tags = tags.Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Lighting = lighting; Fog = fog; Audio = audio;
    }
    public static AreaEnvironmentProfile CreateDefault(AreaKind kind) => new(CurrentSchemaVersion,
        kind == AreaKind.Interior ? AreaEnvironmentClass.Interior : AreaEnvironmentClass.Exterior,
        [], new(DayNightMode.Clock, 24, 0.35f, 1f, 6500, true, true),
        new(FogMode.Disabled, 0, 20, 100, 0), new("", "", "", "", "neutral", 2));
}
