namespace Forge.Domain.Areas;

public enum AreaEnvironmentClass { Interior, Exterior, Natural, Artificial, Underground }
public enum DayNightMode { Clock, AlwaysDay, AlwaysNight }
public enum FogMode { Disabled, Distance, Volumetric }
public enum AudioOrderMode { Sequential, DeterministicRandom }
public enum AreaSoundMode { Global2D, Spatial3D }
public enum AudioInstancePolicy { Single, Restart, Overlap }
public enum AudioAuthority { Local, Server }
public enum MusicState { Exploration, Tension, Combat, Boss, Victory }

public sealed record MusicStateEntry(MusicState State, string Track, IReadOnlyList<string> Stems,
    float TransitionSeconds, float CooldownSeconds);
public sealed record AudioMixBus(Guid Id, string Name, Guid? ParentId, float Volume, string Snapshot,
    string Platform, float DynamicRange, bool EnhanceImportantSounds);
public sealed record LocalizedSoundCaption(string Locale, string Text);
public sealed record SoundCaption(string Resource, IReadOnlyList<LocalizedSoundCaption> Localizations,
    bool ShowDirection, string Importance);
public sealed record AreaAudioOrchestration(IReadOnlyList<MusicStateEntry> MusicStates,
    IReadOnlyList<AudioMixBus> MixBuses, IReadOnlyList<SoundCaption> Captions)
{
    public static AreaAudioOrchestration Empty { get; } = new([], [], []);
}

public sealed record AudioSpatialProfile(AreaSoundMode Mode, float InnerRadius, float OuterRadius,
    float AirAbsorption, string AttenuationCurve);

public sealed record AudioRuntimeControl(bool InitiallyActive, AudioInstancePolicy InstancePolicy,
    AudioAuthority Authority, float DefaultFadeSeconds, string StartedEvent, string StoppedEvent);

public sealed record AdaptiveAmbienceLayer(string Resource, float StartHour, float EndHour,
    string Weather, string LocationTag, float MinimumDanger, bool IndoorOnly, float Volume);

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
    public IReadOnlyList<string> Playlist { get; }
    public AudioOrderMode OrderMode { get; }
    public float IntervalSeconds { get; }
    public float IntervalVariationSeconds { get; }
    public float RelativeVolume { get; }
    public AudioSpatialProfile Spatial { get; }
    public AudioRuntimeControl RuntimeControl { get; }
    public IReadOnlyList<AdaptiveAmbienceLayer> AdaptiveLayers { get; }
    public AreaAudioOrchestration Orchestration { get; }
    public AreaAudioProfile(string dayMusic, string nightMusic, string combatMusic, string ambience, string reverbPreset,
        float transitionSeconds, IReadOnlyList<string>? playlist = null, AudioOrderMode orderMode = AudioOrderMode.Sequential,
        float intervalSeconds = 0, float intervalVariationSeconds = 0, float relativeVolume = 1,
        AudioSpatialProfile? spatial = null, AudioRuntimeControl? runtimeControl = null,
        IReadOnlyList<AdaptiveAmbienceLayer>? adaptiveLayers = null,
        AreaAudioOrchestration? orchestration = null)
    {
        ArgumentNullException.ThrowIfNull(dayMusic); ArgumentNullException.ThrowIfNull(nightMusic);
        ArgumentNullException.ThrowIfNull(combatMusic); ArgumentNullException.ThrowIfNull(ambience); ArgumentException.ThrowIfNullOrWhiteSpace(reverbPreset);
        if (!float.IsFinite(transitionSeconds) || transitionSeconds < 0 || intervalSeconds < 0 ||
            intervalVariationSeconds < 0 || relativeVolume is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
        DayMusic = dayMusic.Trim(); NightMusic = nightMusic.Trim(); CombatMusic = combatMusic.Trim(); Ambience = ambience.Trim();
        ReverbPreset = reverbPreset.Trim(); TransitionSeconds = transitionSeconds;
        Playlist = (playlist ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        OrderMode = orderMode; IntervalSeconds = intervalSeconds; IntervalVariationSeconds = intervalVariationSeconds;
        RelativeVolume = relativeVolume;
        Spatial = spatial ?? new(AreaSoundMode.Global2D, 0, 0, 0, "linear");
        RuntimeControl = runtimeControl ?? new(true, AudioInstancePolicy.Single, AudioAuthority.Local, 1, "audio.started", "audio.stopped");
        AdaptiveLayers = (adaptiveLayers ?? []).ToArray();
        Orchestration = orchestration ?? AreaAudioOrchestration.Empty;
        if (Spatial.InnerRadius < 0 || Spatial.OuterRadius < Spatial.InnerRadius || Spatial.AirAbsorption is < 0 or > 1 ||
            string.IsNullOrWhiteSpace(Spatial.AttenuationCurve) || AdaptiveLayers.Any(layer =>
                layer.StartHour is < 0 or >= 24 || layer.EndHour is < 0 or >= 24 || layer.MinimumDanger is < 0 or > 1 || layer.Volume is < 0 or > 4))
            throw new ArgumentException("Audio spatial or adaptive profile is invalid.");
        if (Orchestration.MusicStates.Select(item => item.State).Distinct().Count() != Orchestration.MusicStates.Count ||
            Orchestration.MixBuses.Select(item => item.Id).Distinct().Count() != Orchestration.MixBuses.Count ||
            Orchestration.Captions.Select(item => item.Resource).Distinct(StringComparer.Ordinal).Count() != Orchestration.Captions.Count)
            throw new ArgumentException("Audio orchestration entries must be unique.");
        var busIds = Orchestration.MixBuses.Select(item => item.Id).ToHashSet();
        if (Orchestration.MixBuses.Any(bus => bus.ParentId is not null && (!busIds.Contains(bus.ParentId.Value) || bus.ParentId == bus.Id)) ||
            Orchestration.MixBuses.Any(bus => bus.Volume is < 0 or > 4 || bus.DynamicRange is < 0 or > 1) ||
            Orchestration.MusicStates.Any(state => state.TransitionSeconds < 0 || state.CooldownSeconds < 0))
            throw new ArgumentException("Audio orchestration hierarchy or ranges are invalid.");
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
