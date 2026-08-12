using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Areas;

public sealed record AreaClassificationCapabilityProfile(
    AreaEnvironmentClass BuiltIn,
    string Key,
    string ExtensionPack,
    bool DrivesRendering,
    bool DrivesAudio,
    bool DrivesNavigation,
    bool DrivesWeather,
    bool DrivesCamera,
    bool DrivesGameplay);

public sealed record AreaDayNightCurvePoint(
    float Hour,
    float SunIntensity,
    float MoonIntensity,
    float SunElevationDegrees);

public sealed record AreaTimedEnvironmentEvent(
    Guid Id,
    float Hour,
    string EventName,
    string ForgeScriptPath,
    bool Required);

public sealed record AreaEnvironmentVolumeOverride(
    Guid Id,
    string Name,
    TransformValue Transform,
    string LightingPresetKey,
    FogMode FogMode,
    float BlendDistanceMeters,
    IReadOnlyDictionary<string, string> Overrides);

public sealed record AreaReflectionProbeProfile(
    Guid Id,
    string Name,
    TransformValue Transform,
    string AssetId,
    BlueprintId? BlueprintId,
    float RadiusMeters,
    bool Realtime,
    IReadOnlyDictionary<string, string> Overrides);

public sealed record AreaPhysicalLightingProfile(
    string PresetKey,
    string AmbientColor,
    string DirectionalColor,
    bool SimpleControls,
    IReadOnlyList<AreaDayNightCurvePoint> Curves,
    IReadOnlyList<AreaTimedEnvironmentEvent> Events,
    IReadOnlyList<AreaReflectionProbeProfile> Probes,
    IReadOnlyList<AreaEnvironmentVolumeOverride> VolumeOverrides);

public enum AreaFogQualityTier
{
    Low,
    Medium,
    High,
    Ultra,
}

public sealed record AreaFogNoiseProfile(
    string AssetId,
    float ScaleMeters,
    float SpeedX,
    float SpeedZ,
    float Contrast);

public sealed record AreaLocalFogVolume(
    Guid Id,
    string Name,
    TransformValue Transform,
    FogMode Mode,
    float Density,
    float HeightFalloff,
    string Color,
    float BlendDistanceMeters,
    IReadOnlyDictionary<string, string> Overrides);

public sealed record AreaFogAuthoringProfile(
    string DayColor,
    string NightColor,
    AreaFogNoiseProfile? Noise,
    IReadOnlyList<AreaLocalFogVolume> LocalVolumes,
    IReadOnlyDictionary<string, AreaFogQualityTier> QualityProfiles);

public sealed record AreaEnvironmentAuthoringProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public AreaClassificationCapabilityProfile Classification { get; }
    public AreaPhysicalLightingProfile Lighting { get; }
    public AreaFogAuthoringProfile Fog { get; }
    public bool RequiredAtRuntime { get; }

    public AreaEnvironmentAuthoringProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        AreaClassificationCapabilityProfile classification,
        AreaPhysicalLightingProfile lighting,
        AreaFogAuthoringProfile fog,
        bool requiredAtRuntime)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        Classification = classification ?? throw new ArgumentNullException(nameof(classification));
        Lighting = lighting ?? throw new ArgumentNullException(nameof(lighting));
        Fog = fog ?? throw new ArgumentNullException(nameof(fog));
        RequiredAtRuntime = requiredAtRuntime;
    }

    public static AreaEnvironmentAuthoringProfile CreateDefault(AreaId areaId, AreaEnvironmentClass classification) =>
        new(
            StableGuid(areaId, "environment-authoring"),
            CurrentSchemaVersion,
            "migration-default",
            new(classification, classification.ToString().ToLowerInvariant(), string.Empty,
                true, true, true, true, true, true),
            new("physical-neutral", "#596273", "#fff1d6", true,
                [new(0, 0, .25f, -45), new(6, .25f, .1f, 0), new(12, 1, 0, 60),
                    new(18, .25f, .1f, 0), new(24, 0, .25f, -45)],
                [], [], []),
            new("#b8c4d8", "#25324a", null, [],
                new Dictionary<string, AreaFogQualityTier>(StringComparer.Ordinal)
                {
                    ["desktop"] = AreaFogQualityTier.High,
                    ["low"] = AreaFogQualityTier.Low,
                }),
            false);

    public static AreaEnvironmentAuthoringProfile CloneForArea(
        AreaEnvironmentAuthoringProfile source,
        AreaId destinationAreaId) =>
        new(
            StableGuid(destinationAreaId, "environment-authoring"),
            source.SchemaVersion,
            "area-clone",
            source.Classification,
            source.Lighting with
            {
                Events = source.Lighting.Events.Select(item => item with
                {
                    Id = StableGuid(destinationAreaId, $"environment-event:{item.Id:D}"),
                }).ToArray(),
                Probes = source.Lighting.Probes.Select(item => item with
                {
                    Id = StableGuid(destinationAreaId, $"environment-probe:{item.Id:D}"),
                }).ToArray(),
                VolumeOverrides = source.Lighting.VolumeOverrides.Select(item => item with
                {
                    Id = StableGuid(destinationAreaId, $"environment-volume:{item.Id:D}"),
                }).ToArray(),
            },
            source.Fog with
            {
                LocalVolumes = source.Fog.LocalVolumes.Select(item => item with
                {
                    Id = StableGuid(destinationAreaId, $"fog-volume:{item.Id:D}"),
                }).ToArray(),
            },
            source.RequiredAtRuntime);

    internal static Guid StableGuid(AreaId areaId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId}:{discriminator}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public static class AreaEnvironmentAuthoringValidator
{
    public const string IdentityCode = "area.environment.identity_invalid";
    public const string SchemaCode = "area.environment.schema_unsupported";
    public const string ClassificationCode = "area.environment.classification_invalid";
    public const string LightingCode = "area.environment.lighting_invalid";
    public const string EventCode = "area.environment.event_invalid";
    public const string ProbeCode = "area.environment.probe_invalid";
    public const string VolumeCode = "area.environment.volume_invalid";
    public const string FogCode = "area.environment.fog_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area, AreaEnvironmentProfile environment)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(environment);
        var profile = environment.Authoring;
        var root = $"areas/{area.Id}/environment/authoring";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityCode, "Environment UUID and provenance are required.", root));
        if (profile.SchemaVersion != AreaEnvironmentAuthoringProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaCode, "Environment authoring schema version is unsupported.",
                $"{root}/schemaVersion"));

        var classification = profile.Classification;
        if (classification.BuiltIn != environment.Classification || string.IsNullOrWhiteSpace(classification.Key) ||
            classification.Key.Contains(' ') ||
            !new[] { classification.DrivesRendering, classification.DrivesAudio,
                classification.DrivesNavigation, classification.DrivesWeather,
                classification.DrivesCamera, classification.DrivesGameplay }.Any(item => item))
            diagnostics.Add(new(ClassificationCode,
                "Classification key must match the built-in class and drive at least one subsystem.",
                $"{root}/classification"));

        var lighting = profile.Lighting;
        if (string.IsNullOrWhiteSpace(lighting.PresetKey) || !Color(lighting.AmbientColor) ||
            !Color(lighting.DirectionalColor) || lighting.Curves.Count < 2 ||
            lighting.Curves.Any(item => !Finite(item.Hour, item.SunIntensity, item.MoonIntensity,
                item.SunElevationDegrees) || item.Hour is < 0 or > 24 || item.SunIntensity < 0 ||
                item.MoonIntensity < 0 || item.SunElevationDegrees is < -90 or > 90) ||
            lighting.Curves.Select(item => item.Hour).Distinct().Count() != lighting.Curves.Count)
            diagnostics.Add(new(LightingCode,
                "Physical lighting preset, colors and ordered finite sun/moon curves are required.",
                $"{root}/lighting"));

        var childIds = lighting.Events.Select(item => item.Id)
            .Concat(lighting.Probes.Select(item => item.Id))
            .Concat(lighting.VolumeOverrides.Select(item => item.Id))
            .Concat(profile.Fog.LocalVolumes.Select(item => item.Id)).ToArray();
        if (childIds.Any(item => item == Guid.Empty) || childIds.Distinct().Count() != childIds.Length)
            diagnostics.Add(new(IdentityCode, "Environment child UUIDs must be non-empty and unique.", root));

        if (lighting.Events.Any(item => item.Hour is < 0 or >= 24 || !float.IsFinite(item.Hour) ||
                string.IsNullOrWhiteSpace(item.EventName) || string.IsNullOrWhiteSpace(item.ForgeScriptPath) ||
                !SafePath(item.ForgeScriptPath)))
            diagnostics.Add(new(EventCode, "Timed events require a valid hour, event name and safe ForgeScript path.",
                $"{root}/lighting/events"));
        if (lighting.Probes.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.AssetId) && item.BlueprintId is null ||
                !FiniteTransform(item.Transform) || !float.IsFinite(item.RadiusMeters) || item.RadiusMeters <= 0 ||
                item.Overrides.Keys.Any(string.IsNullOrWhiteSpace)))
            diagnostics.Add(new(ProbeCode,
                "Reflection probes require identity, finite transform, asset or blueprint and positive radius.",
                $"{root}/lighting/probes"));
        if (lighting.VolumeOverrides.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.LightingPresetKey) || !FiniteTransform(item.Transform) ||
                !float.IsFinite(item.BlendDistanceMeters) || item.BlendDistanceMeters < 0 ||
                item.Overrides.Keys.Any(string.IsNullOrWhiteSpace)))
            diagnostics.Add(new(VolumeCode,
                "Lighting volume overrides require finite transforms, presets, blend and typed overrides.",
                $"{root}/lighting/volumeOverrides"));

        var fog = profile.Fog;
        if (!Color(fog.DayColor) || !Color(fog.NightColor) || fog.QualityProfiles.Count == 0 ||
            fog.QualityProfiles.Keys.Any(string.IsNullOrWhiteSpace) || fog.Noise is { } noise &&
            (string.IsNullOrWhiteSpace(noise.AssetId) || !Finite(noise.ScaleMeters, noise.SpeedX, noise.SpeedZ,
                noise.Contrast) || noise.ScaleMeters <= 0 || noise.Contrast is < 0 or > 4) ||
            fog.LocalVolumes.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                !FiniteTransform(item.Transform) || !Finite(item.Density, item.HeightFalloff,
                    item.BlendDistanceMeters) || item.Density is < 0 or > 1 || item.HeightFalloff < 0 ||
                item.BlendDistanceMeters < 0 || !Color(item.Color) ||
                item.Overrides.Keys.Any(string.IsNullOrWhiteSpace)))
            diagnostics.Add(new(FogCode,
                "Fog requires day/night colors, quality profiles and valid noise/local volumes.",
                $"{root}/fog"));

        return diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ToArray();
    }

    private static bool SafePath(string value) => !Path.IsPathRooted(value) &&
        !value.Replace('\\', '/').Split('/').Contains("..", StringComparer.Ordinal);

    private static bool Color(string value) => value.Length == 7 && value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);

    private static bool Finite(params float[] values) => values.All(float.IsFinite);

    private static bool FiniteTransform(TransformValue value) =>
        Finite(value.Position.X, value.Position.Y, value.Position.Z, value.Rotation.X, value.Rotation.Y,
            value.Rotation.Z, value.Rotation.W, value.Scale.X, value.Scale.Y, value.Scale.Z);
}

public sealed record AreaEnvironmentRuntimeHandle(
    Guid Id,
    string ClassificationKey,
    DayNightMode DayNightMode,
    string LightingPresetKey,
    int CurvePoints,
    int TimedEvents,
    int ReflectionProbes,
    int LightingVolumes,
    FogMode FogMode,
    int FogVolumes,
    int FogQualityProfiles);

public static class AreaEnvironmentRuntimeResolver
{
    public const string RequiredMissingCode = "runtime.area_environment.required_missing";

    public static (AreaEnvironmentRuntimeHandle? Handle, IReadOnlyList<DomainDiagnostic> Diagnostics) Resolve(
        AreaId areaId,
        AreaEnvironmentProfile? environment,
        bool required)
    {
        if (environment is null || required &&
            string.Equals(environment.Authoring.Provenance, "migration-default", StringComparison.Ordinal))
            return required
                ? (null, [new(RequiredMissingCode, "Required area environment metadata is missing.",
                    $"areas/{areaId}/environment")])
                : (null, []);
        var authoring = environment.Authoring;
        return (new(authoring.Id, authoring.Classification.Key, environment.Lighting.Mode,
            authoring.Lighting.PresetKey, authoring.Lighting.Curves.Count, authoring.Lighting.Events.Count,
            authoring.Lighting.Probes.Count, authoring.Lighting.VolumeOverrides.Count,
            environment.Fog.Mode, authoring.Fog.LocalVolumes.Count, authoring.Fog.QualityProfiles.Count), []);
    }
}
