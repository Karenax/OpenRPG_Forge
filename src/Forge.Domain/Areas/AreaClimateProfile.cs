using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public enum WeatherKind
{
    Clear,
    Overcast,
    Rain,
    Storm,
    Snow,
    Fog,
}

public enum WaterBodyKind
{
    Plane,
    River,
    Volume,
}

public enum WaterQualityTier
{
    Low,
    Medium,
    High,
    Ultra,
}

public sealed record WindZoneProfile
{
    public Guid Id { get; }
    public Vector3Value Center { get; }
    public float Radius { get; }
    public float DirectionX { get; }
    public float DirectionZ { get; }
    public float Speed { get; }
    public float GustMultiplier { get; }

    public WindZoneProfile(Guid id, Vector3Value center, float radius, float directionX, float directionZ,
        float speed, float gustMultiplier)
    {
        if (id == Guid.Empty || radius <= 0 || !float.IsFinite(radius) ||
            !float.IsFinite(directionX) || !float.IsFinite(directionZ) ||
            !float.IsFinite(speed) || speed < 0 || gustMultiplier is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        Id = id;
        Center = center;
        Radius = radius;
        (DirectionX, DirectionZ) = NormalizeDirection(directionX, directionZ);
        Speed = speed;
        GustMultiplier = gustMultiplier;
    }

    internal static (float X, float Z) NormalizeDirection(float x, float z)
    {
        var length = MathF.Sqrt((x * x) + (z * z));
        return length <= 0.0001f ? (0, 0) : (x / length, z / length);
    }
}

public sealed record WindProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public float DirectionX { get; }
    public float DirectionZ { get; }
    public float Speed { get; }
    public float GustStrength { get; }
    public float GustIntervalSeconds { get; }
    public bool InfluencesVegetation { get; }
    public bool InfluencesParticles { get; }
    public string AudioCue { get; }
    public float GameplayForce { get; }
    public IReadOnlyList<WindZoneProfile> LocalZones { get; }

    public WindProfile(int schemaVersion, float directionX, float directionZ, float speed,
        float gustStrength, float gustIntervalSeconds, bool influencesVegetation,
        bool influencesParticles, string audioCue, float gameplayForce,
        IReadOnlyList<WindZoneProfile> localZones)
    {
        if (schemaVersion != CurrentSchemaVersion || !float.IsFinite(speed) || speed < 0 ||
            gustStrength is < 0 or > 8 || gustIntervalSeconds is < 0.1f or > 3600 ||
            gameplayForce is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        ArgumentNullException.ThrowIfNull(audioCue);
        ArgumentNullException.ThrowIfNull(localZones);
        if (localZones.Select(zone => zone.Id).Distinct().Count() != localZones.Count)
        {
            throw new ArgumentException("Wind zones require unique IDs.", nameof(localZones));
        }

        SchemaVersion = schemaVersion;
        (DirectionX, DirectionZ) = WindZoneProfile.NormalizeDirection(directionX, directionZ);
        Speed = speed;
        GustStrength = gustStrength;
        GustIntervalSeconds = gustIntervalSeconds;
        InfluencesVegetation = influencesVegetation;
        InfluencesParticles = influencesParticles;
        AudioCue = audioCue.Trim();
        GameplayForce = gameplayForce;
        LocalZones = localZones.OrderBy(zone => zone.Id).ToArray();
    }

    public static WindProfile CreateDefault(AreaEnvironmentClass classification) =>
        classification is AreaEnvironmentClass.Exterior or AreaEnvironmentClass.Natural
            ? new(CurrentSchemaVersion, 0.8f, 0.3f, 3, 0.35f, 8, true, true, "wind.light", 0, [])
            : new(CurrentSchemaVersion, 0, 0, 0, 0, 8, false, false, string.Empty, 0, []);
}

public sealed record PrecipitationVisualProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public float DirectionX { get; }
    public float DirectionZ { get; }
    public float WindInfluence { get; }
    public float SpreadDegrees { get; }
    public float MinimumVelocity { get; }
    public float MaximumVelocity { get; }
    public float GravityY { get; }
    public float ParticlesPerIntensity { get; }
    public int MinimumParticles { get; }
    public float ParticleWidth { get; }
    public float ParticleLength { get; }
    public float LifetimeSeconds { get; }
    public float Randomness { get; }
    public string Color { get; }

    public PrecipitationVisualProfile(
        int schemaVersion,
        float directionX,
        float directionZ,
        float windInfluence,
        float spreadDegrees,
        float minimumVelocity,
        float maximumVelocity,
        float gravityY,
        float particlesPerIntensity,
        int minimumParticles,
        float particleWidth,
        float particleLength,
        float lifetimeSeconds,
        float randomness,
        string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (schemaVersion != CurrentSchemaVersion || !float.IsFinite(directionX) || !float.IsFinite(directionZ) ||
            directionX is < -4 or > 4 || directionZ is < -4 or > 4 ||
            !float.IsFinite(windInfluence) || windInfluence is < 0 or > 4 ||
            !float.IsFinite(spreadDegrees) || spreadDegrees is < 0 or > 180 ||
            !float.IsFinite(minimumVelocity) || minimumVelocity is < 0 or > 100 ||
            !float.IsFinite(maximumVelocity) || maximumVelocity < minimumVelocity || maximumVelocity > 100 ||
            !float.IsFinite(gravityY) || gravityY is < -100 or > 100 ||
            !float.IsFinite(particlesPerIntensity) || particlesPerIntensity is < 0 or > 100_000 ||
            minimumParticles is < 0 or > 100_000 ||
            !float.IsFinite(particleWidth) || particleWidth is < .001f or > 10 ||
            !float.IsFinite(particleLength) || particleLength is < .001f or > 20 ||
            !float.IsFinite(lifetimeSeconds) || lifetimeSeconds is < .1f or > 120 ||
            !float.IsFinite(randomness) || randomness is < 0 or > 1 || !IsHexColor(color))
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        SchemaVersion = schemaVersion;
        DirectionX = directionX;
        DirectionZ = directionZ;
        WindInfluence = windInfluence;
        SpreadDegrees = spreadDegrees;
        MinimumVelocity = minimumVelocity;
        MaximumVelocity = maximumVelocity;
        GravityY = gravityY;
        ParticlesPerIntensity = particlesPerIntensity;
        MinimumParticles = minimumParticles;
        ParticleWidth = particleWidth;
        ParticleLength = particleLength;
        LifetimeSeconds = lifetimeSeconds;
        Randomness = randomness;
        Color = color.ToLowerInvariant();
    }

    public static PrecipitationVisualProfile CreateDefault(WeatherKind kind) => kind == WeatherKind.Snow
        ? new(CurrentSchemaVersion, 0, 0, .12f, 7, 2, 4, -.4f, 1_600, 64,
            .12f, .12f, 9, .82f, "#ebf5ffd9")
        : new(CurrentSchemaVersion, 0, 0, 0, 0, 16, 22, -12, 3_000, 128,
            .025f, .56f, 2.8f, .82f, "#a8d1ff99");

    private static bool IsHexColor(string value) =>
        value.Length is 7 or 9 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
}

public sealed record WeatherStateProfile
{
    public Guid Id { get; }
    public string Name { get; }
    public WeatherKind Kind { get; }
    public float Intensity { get; }
    public float Humidity { get; }
    public float SurfaceWetness { get; }
    public bool Lightning { get; }
    public string PrecipitationResource { get; }
    public string AudioCue { get; }
    public string GameplayEffect { get; }
    public float MinimumDurationSeconds { get; }
    public float MaximumDurationSeconds { get; }
    public float TransitionSeconds { get; }
    public PrecipitationVisualProfile Precipitation { get; }
    public SurfaceWetnessMaterialProfile WetnessMaterial { get; }

    public WeatherStateProfile(Guid id, string name, WeatherKind kind, float intensity, float humidity,
        float surfaceWetness, bool lightning, string precipitationResource, string audioCue,
        string gameplayEffect, float minimumDurationSeconds, float maximumDurationSeconds,
        float transitionSeconds, PrecipitationVisualProfile? precipitation = null,
        SurfaceWetnessMaterialProfile? wetnessMaterial = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(precipitationResource);
        ArgumentNullException.ThrowIfNull(audioCue);
        ArgumentNullException.ThrowIfNull(gameplayEffect);
        if (id == Guid.Empty || intensity is < 0 or > 1 || humidity is < 0 or > 1 ||
            surfaceWetness is < 0 or > 1 || minimumDurationSeconds < 0 ||
            maximumDurationSeconds < minimumDurationSeconds || transitionSeconds is < 0 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(intensity));
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
        Intensity = intensity;
        Humidity = humidity;
        SurfaceWetness = surfaceWetness;
        Lightning = lightning;
        PrecipitationResource = NormalizeOptionalResource(precipitationResource);
        AudioCue = audioCue.Trim();
        GameplayEffect = gameplayEffect.Trim();
        MinimumDurationSeconds = minimumDurationSeconds;
        MaximumDurationSeconds = maximumDurationSeconds;
        TransitionSeconds = transitionSeconds;
        Precipitation = precipitation ?? PrecipitationVisualProfile.CreateDefault(kind);
        WetnessMaterial = wetnessMaterial ?? SurfaceWetnessMaterialProfile.CreateDefault();
    }

    public static string NormalizeOptionalResource(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        if (string.IsNullOrEmpty(normalized)) return string.Empty;
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Environment resources must use safe relative paths.", nameof(value));
        }

        return normalized;
    }
}

public sealed record SurfaceWetnessMaterialProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public float AlbedoDarkening { get; }
    public float RoughnessReduction { get; }
    public float SpecularIncrease { get; }

    public SurfaceWetnessMaterialProfile(
        int schemaVersion,
        float albedoDarkening,
        float roughnessReduction,
        float specularIncrease)
    {
        if (schemaVersion != CurrentSchemaVersion ||
            !float.IsFinite(albedoDarkening) || albedoDarkening is < 0 or > .35f ||
            !float.IsFinite(roughnessReduction) || roughnessReduction is < 0 or > .8f ||
            !float.IsFinite(specularIncrease) || specularIncrease is < 0 or > .6f)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        SchemaVersion = schemaVersion;
        AlbedoDarkening = albedoDarkening;
        RoughnessReduction = roughnessReduction;
        SpecularIncrease = specularIncrease;
    }

    public static SurfaceWetnessMaterialProfile CreateDefault() =>
        new(CurrentSchemaVersion, .14f, .36f, .2f);
}

public sealed record WeatherProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public Guid InitialStateId { get; }
    public IReadOnlyList<WeatherStateProfile> States { get; }
    public bool ServerAuthoritative { get; }
    public int DeterministicSeed { get; }
    public bool PersistState { get; }
    public bool ReplicateState { get; }

    public WeatherProfile(int schemaVersion, Guid initialStateId, IReadOnlyList<WeatherStateProfile> states,
        bool serverAuthoritative, int deterministicSeed, bool persistState, bool replicateState)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count == 0 || states.Select(state => state.Id).Distinct().Count() != states.Count ||
            states.All(state => state.Id != initialStateId))
        {
            throw new ArgumentException("Weather states require unique IDs and a valid initial state.", nameof(states));
        }

        SchemaVersion = schemaVersion;
        InitialStateId = initialStateId;
        States = states.OrderBy(state => state.Id).ToArray();
        ServerAuthoritative = serverAuthoritative;
        DeterministicSeed = deterministicSeed;
        PersistState = persistState;
        ReplicateState = replicateState;
    }

    public static WeatherProfile CreateDefault(AreaEnvironmentClass classification)
    {
        var clearId = Guid.Parse("6f550000-0000-4000-8000-000000000001");
        var clear = new WeatherStateProfile(clearId, "Clair", WeatherKind.Clear, 0, 0.35f, 0,
            false, string.Empty, string.Empty, string.Empty, 600, 1200, 8);
        if (classification is not AreaEnvironmentClass.Exterior and not AreaEnvironmentClass.Natural)
        {
            return new(CurrentSchemaVersion, clearId, [clear], true, 2026, true, true);
        }

        var rain = new WeatherStateProfile(
            Guid.Parse("6f550000-0000-4000-8000-000000000002"),
            "Pluie", WeatherKind.Rain, 0.55f, 0.85f, 0.7f, false,
            string.Empty, "weather.rain", "surface.wet", 180, 420, 12);
        return new(CurrentSchemaVersion, clearId, [clear, rain], true, 2026, true, true);
    }
}

public sealed record WaterBodyProfile
{
    public Guid Id { get; }
    public string Name { get; }
    public WaterBodyKind Kind { get; }
    public Vector3Value Center { get; }
    public Vector3Value Size { get; }
    public float MaximumDepth { get; }
    public float FlowDirectionX { get; }
    public float FlowDirectionZ { get; }
    public float FlowSpeed { get; }
    public float Buoyancy { get; }
    public float NavigationCost { get; }
    public bool SwimmingEnabled { get; }
    public WaterQualityTier Quality { get; }
    public string MaterialResource { get; }
    public string AudioCue { get; }

    public WaterBodyProfile(Guid id, string name, WaterBodyKind kind, Vector3Value center,
        Vector3Value size, float maximumDepth, float flowDirectionX, float flowDirectionZ,
        float flowSpeed, float buoyancy, float navigationCost, bool swimmingEnabled,
        WaterQualityTier quality, string materialResource, string audioCue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(materialResource);
        ArgumentNullException.ThrowIfNull(audioCue);
        if (id == Guid.Empty || size.X <= 0 || size.Y <= 0 || size.Z <= 0 || maximumDepth <= 0 ||
            flowSpeed is < 0 or > 100 || buoyancy is < 0 or > 10 || navigationCost is < 0.1f or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
        Center = center;
        Size = size;
        MaximumDepth = maximumDepth;
        (FlowDirectionX, FlowDirectionZ) = WindZoneProfile.NormalizeDirection(flowDirectionX, flowDirectionZ);
        FlowSpeed = flowSpeed;
        Buoyancy = buoyancy;
        NavigationCost = navigationCost;
        SwimmingEnabled = swimmingEnabled;
        Quality = quality;
        MaterialResource = WeatherStateProfile.NormalizeOptionalResource(materialResource);
        AudioCue = audioCue.Trim();
    }
}

public sealed record WaterProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public IReadOnlyList<WaterBodyProfile> Bodies { get; }

    public WaterProfile(int schemaVersion, IReadOnlyList<WaterBodyProfile> bodies)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(bodies);
        if (bodies.Select(body => body.Id).Distinct().Count() != bodies.Count)
        {
            throw new ArgumentException("Water bodies require unique IDs.", nameof(bodies));
        }

        SchemaVersion = schemaVersion;
        Bodies = bodies.OrderBy(body => body.Id).ToArray();
    }

    public static WaterProfile Empty { get; } = new(CurrentSchemaVersion, []);
}

public sealed record ReverbZoneProfile
{
    public Guid Id { get; }
    public string Name { get; }
    public Vector3Value Center { get; }
    public Vector3Value Size { get; }
    public string Preset { get; }
    public float Wet { get; }
    public float Dry { get; }
    public float BlendSeconds { get; }
    public string ImpulseResource { get; }
    public string LicenseSpdx { get; }
    public string ContentSha256 { get; }
    public string Provenance { get; }

    public ReverbZoneProfile(Guid id, string name, Vector3Value center, Vector3Value size, string preset,
        float wet, float dry, float blendSeconds, string impulseResource, string licenseSpdx,
        string contentSha256, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(preset);
        ArgumentNullException.ThrowIfNull(impulseResource);
        ArgumentNullException.ThrowIfNull(licenseSpdx);
        ArgumentNullException.ThrowIfNull(contentSha256);
        ArgumentNullException.ThrowIfNull(provenance);
        if (id == Guid.Empty || size.X <= 0 || size.Y <= 0 || size.Z <= 0 || wet is < 0 or > 1 ||
            dry is < 0 or > 1 || blendSeconds is < 0 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var impulse = WeatherStateProfile.NormalizeOptionalResource(impulseResource);
        if (!string.IsNullOrEmpty(impulse) &&
            (string.IsNullOrWhiteSpace(licenseSpdx) || string.IsNullOrWhiteSpace(provenance) ||
             contentSha256.Length != 64 || !contentSha256.All(Uri.IsHexDigit)))
        {
            throw new ArgumentException("Impulse responses require SPDX, provenance and SHA-256 evidence.");
        }

        Id = id;
        Name = name.Trim();
        Center = center;
        Size = size;
        Preset = preset.Trim().ToLowerInvariant();
        Wet = wet;
        Dry = dry;
        BlendSeconds = blendSeconds;
        ImpulseResource = impulse;
        LicenseSpdx = licenseSpdx.Trim();
        ContentSha256 = contentSha256.Trim().ToLowerInvariant();
        Provenance = provenance.Trim();
    }
}

public sealed record AreaReverbProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public string DefaultPreset { get; }
    public IReadOnlyList<ReverbZoneProfile> Zones { get; }

    public AreaReverbProfile(int schemaVersion, string defaultPreset, IReadOnlyList<ReverbZoneProfile> zones)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultPreset);
        ArgumentNullException.ThrowIfNull(zones);
        if (zones.Select(zone => zone.Id).Distinct().Count() != zones.Count)
        {
            throw new ArgumentException("Reverb zones require unique IDs.", nameof(zones));
        }

        SchemaVersion = schemaVersion;
        DefaultPreset = defaultPreset.Trim().ToLowerInvariant();
        Zones = zones.OrderBy(zone => zone.Id).ToArray();
    }

    public static AreaReverbProfile CreateDefault(string preset) =>
        new(CurrentSchemaVersion, string.IsNullOrWhiteSpace(preset) ? "neutral" : preset, []);
}
