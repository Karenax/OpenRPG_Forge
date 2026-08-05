using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.EngineAdapter.Rendering;

public sealed record AreaClimatePlan(
    WindProfile Wind,
    WindZoneProfile? ActiveWindZone,
    WeatherStateProfile Weather,
    IReadOnlyList<WaterBodyProfile> WaterBodies,
    WaterQualityTier EffectiveWaterQuality,
    AreaReverbProfile Reverb,
    ReverbZoneProfile? ActiveReverbZone,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AreaClimatePlanBuilder
{
    public const string WaterQualityDowngradedCode = "environment.water.quality_downgraded";

    public static AreaClimatePlan Build(
        AreaEnvironmentProfile profile,
        Guid weatherStateId,
        Vector3Value listenerPosition,
        string platform)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        var weather = profile.Weather.States.SingleOrDefault(state => state.Id == weatherStateId)
            ?? throw new ArgumentException("The climate plan references an unknown weather state.", nameof(weatherStateId));
        var activeWind = profile.Wind.LocalZones
            .Where(zone => HorizontalDistance(zone.Center, listenerPosition) <= zone.Radius)
            .OrderBy(zone => zone.Radius)
            .FirstOrDefault();
        var activeReverb = profile.Reverb.Zones
            .Where(zone => Contains(zone, listenerPosition))
            .OrderBy(zone => zone.Size.X * zone.Size.Y * zone.Size.Z)
            .FirstOrDefault();
        var quality = profile.Water.Bodies.Count == 0
            ? WaterQualityTier.Low
            : profile.Water.Bodies.Max(body => body.Quality);
        var diagnostics = new List<DomainDiagnostic>();
        if (string.Equals(platform, "mobile", StringComparison.OrdinalIgnoreCase) && quality > WaterQualityTier.Medium)
        {
            quality = WaterQualityTier.Medium;
            diagnostics.Add(new(
                WaterQualityDowngradedCode,
                "Water quality was reduced to the mobile platform budget.",
                "environment/water/quality"));
        }

        return new(
            profile.Wind,
            activeWind,
            weather,
            profile.Water.Bodies,
            quality,
            profile.Reverb,
            activeReverb,
            diagnostics);
    }

    private static float HorizontalDistance(Vector3Value left, Vector3Value right)
    {
        var x = left.X - right.X;
        var z = left.Z - right.Z;
        return MathF.Sqrt((x * x) + (z * z));
    }

    private static bool Contains(ReverbZoneProfile zone, Vector3Value point) =>
        MathF.Abs(point.X - zone.Center.X) <= zone.Size.X / 2 &&
        MathF.Abs(point.Y - zone.Center.Y) <= zone.Size.Y / 2 &&
        MathF.Abs(point.Z - zone.Center.Z) <= zone.Size.Z / 2;
}
