using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record AreaEnvironmentScriptSnapshot(
    string AreaId,
    Guid ProfileId,
    int SchemaVersion,
    string Provenance,
    string Classification,
    string ClassificationKey,
    string ClassificationExtensionPack,
    IReadOnlyList<string> DrivenSubsystems,
    DayNightMode DayNightMode,
    float DayLengthMinutes,
    string LightingPreset,
    int CurvePointCount,
    int TimedEventCount,
    int ReflectionProbeCount,
    int LightingVolumeCount,
    FogMode FogMode,
    int FogVolumeCount,
    IReadOnlyDictionary<string, AreaFogQualityTier> FogQualityProfiles,
    bool RequiredAtRuntime);

public sealed record AreaEnvironmentTimePreview(
    float Hour,
    float SunIntensity,
    float MoonIntensity,
    float SunElevationDegrees,
    IReadOnlyList<string> EventsAtHour);

public static class AreaEnvironmentScriptService
{
    public static AreaEnvironmentScriptSnapshot Read(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var environment = area.Environment;
        var authoring = environment.Authoring;
        var classification = authoring.Classification;
        var driven = new List<string>();
        if (classification.DrivesRendering) driven.Add("rendering");
        if (classification.DrivesAudio) driven.Add("audio");
        if (classification.DrivesNavigation) driven.Add("navigation");
        if (classification.DrivesWeather) driven.Add("weather");
        if (classification.DrivesCamera) driven.Add("camera");
        if (classification.DrivesGameplay) driven.Add("gameplay");
        return new(area.Id.ToString(), authoring.Id, authoring.SchemaVersion, authoring.Provenance,
            environment.Classification.ToString(), classification.Key, classification.ExtensionPack, driven,
            environment.Lighting.Mode, environment.Lighting.DayLengthMinutes, authoring.Lighting.PresetKey,
            authoring.Lighting.Curves.Count, authoring.Lighting.Events.Count, authoring.Lighting.Probes.Count,
            authoring.Lighting.VolumeOverrides.Count, environment.Fog.Mode, authoring.Fog.LocalVolumes.Count,
            authoring.Fog.QualityProfiles, authoring.RequiredAtRuntime);
    }

    public static AreaEnvironmentTimePreview PreviewTime(AreaDefinition area, float hour)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (!float.IsFinite(hour)) throw new ArgumentOutOfRangeException(nameof(hour));
        var normalized = ((hour % 24) + 24) % 24;
        var curves = area.Environment.Authoring.Lighting.Curves.OrderBy(item => item.Hour).ToArray();
        var lower = curves.LastOrDefault(item => item.Hour <= normalized) ?? curves[0];
        var upper = curves.FirstOrDefault(item => item.Hour >= normalized) ?? curves[^1];
        var span = Math.Max(.0001f, upper.Hour - lower.Hour);
        var amount = Math.Clamp((normalized - lower.Hour) / span, 0, 1);
        var events = area.Environment.Authoring.Lighting.Events
            .Where(item => Math.Abs(item.Hour - normalized) < .01f)
            .OrderBy(item => item.EventName, StringComparer.Ordinal)
            .Select(item => item.EventName).ToArray();
        return new(normalized,
            Lerp(lower.SunIntensity, upper.SunIntensity, amount),
            Lerp(lower.MoonIntensity, upper.MoonIntensity, amount),
            Lerp(lower.SunElevationDegrees, upper.SunElevationDegrees, amount), events);
    }

    private static float Lerp(float first, float second, float amount) => first + (second - first) * amount;
}
