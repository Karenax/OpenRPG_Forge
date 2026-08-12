using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.EngineAdapter.Rendering;

public sealed record TerrainProviderRuntimePlan(
    TerrainProviderKind Provider,
    string Resource,
    bool UsesExternalResource,
    bool SupportsSculpt,
    bool SupportsMaterialPaint,
    string DiagnosticCode);

public sealed record TerrainFeatureSplinePlan(
    Guid FeatureId,
    TerrainFeatureKind Kind,
    IReadOnlyList<Vector3Value> Samples,
    IReadOnlyList<Guid> Intersections);

public sealed record WindMaterialBinding(
    IReadOnlyDictionary<string, float> ShaderGlobals,
    IReadOnlyList<string> MaterialResources,
    string AudioCue,
    float GameplayForce);

public sealed record WeatherEffectDispatchPlan(
    string ScriptEvent,
    string PrecipitationResource,
    string AudioCue,
    string GameplayEffect,
    float SurfaceWetness,
    bool ServerAuthoritative,
    bool Replicated);

public sealed record SkyTransitionPlan(
    SkyProfile From,
    SkyProfile To,
    double DurationSeconds,
    double Progress,
    double EnergyMultiplier,
    double CloudCoverage,
    string DiagnosticCode);

public sealed record WaterProductionPlan(
    Guid WaterId,
    WaterBodyKind Kind,
    IReadOnlyList<Vector3Value> SurfaceVertices,
    IReadOnlyDictionary<string, float> ShaderParameters,
    Vector3Value FlowVelocity,
    float BuoyancyAcceleration,
    float NavigationCost,
    bool SwimmingEnabled,
    string AudioCue,
    string DiagnosticCode);

public static class AreaProductionPlanBuilder
{
    public static TerrainProviderRuntimePlan Terrain(TerrainDefinition terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var resource = terrain.Provider switch
        {
            TerrainProviderKind.Heightmap => terrain.HeightmapResource,
            TerrainProviderKind.Voxel => terrain.VoxelResource,
            TerrainProviderKind.Mesh => terrain.MeshResource,
            _ => string.Empty,
        };
        var external = terrain.Provider is TerrainProviderKind.Voxel or TerrainProviderKind.Mesh;
        var available = !external || !string.IsNullOrWhiteSpace(resource);
        return new(terrain.Provider, resource, external && available,
            terrain.Provider is TerrainProviderKind.Flat or TerrainProviderKind.Heightmap or TerrainProviderKind.Voxel,
            terrain.Provider is not TerrainProviderKind.None and not TerrainProviderKind.Mesh,
            available ? "area.terrain.provider_ready" : "area.terrain.provider_resource_missing");
    }

    public static IReadOnlyList<TerrainFeatureSplinePlan> Features(
        IReadOnlyList<TerrainFeatureDefinition> features,
        float sampleSpacing = 2)
    {
        ArgumentNullException.ThrowIfNull(features);
        if (!float.IsFinite(sampleSpacing) || sampleSpacing is < .25f or > 32)
            throw new ArgumentOutOfRangeException(nameof(sampleSpacing));
        var sampled = features.ToDictionary(feature => feature.Id,
            feature => Sample(feature.ControlPoints, sampleSpacing));
        return features.OrderBy(feature => feature.Id).Select(feature => new TerrainFeatureSplinePlan(
            feature.Id, feature.Kind, sampled[feature.Id], features.Where(other => other.Id != feature.Id &&
                Intersects(sampled[feature.Id], sampled[other.Id], (feature.Width + other.Width) / 2))
                .Select(other => other.Id).Order().ToArray())).ToArray();
    }

    public static WindMaterialBinding Wind(
        WindProfile wind,
        WindZoneProfile? activeZone,
        IReadOnlyList<string> materialResources)
    {
        ArgumentNullException.ThrowIfNull(wind);
        ArgumentNullException.ThrowIfNull(materialResources);
        var directionX = activeZone?.DirectionX ?? wind.DirectionX;
        var directionZ = activeZone?.DirectionZ ?? wind.DirectionZ;
        var speed = activeZone?.Speed ?? wind.Speed;
        var gust = activeZone?.GustMultiplier ?? wind.GustStrength;
        var materials = wind.InfluencesVegetation
            ? materialResources.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray()
            : [];
        return new(new Dictionary<string, float>
        {
            ["wind_direction_x"] = directionX,
            ["wind_direction_z"] = directionZ,
            ["wind_speed"] = speed,
            ["wind_gust"] = gust,
        }, materials, wind.AudioCue, wind.GameplayForce);
    }

    public static WeatherEffectDispatchPlan Weather(WeatherProfile profile, WeatherStateProfile state)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        if (profile.States.All(item => item.Id != state.Id))
            throw new ArgumentException("Weather state does not belong to the profile.", nameof(state));
        return new($"weather.changed:{state.Id:D}", state.PrecipitationResource, state.AudioCue,
            state.GameplayEffect, state.SurfaceWetness, profile.ServerAuthoritative, profile.ReplicateState);
    }

    public static SkyTransitionPlan Sky(SkyProfile from, SkyProfile to, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        var duration = Math.Max(0, to.TransitionSeconds);
        var progress = duration == 0 ? 1 : Math.Clamp(elapsedSeconds / duration, 0, 1);
        return new(from, to, duration, progress,
            Lerp(from.EnergyMultiplier, to.EnergyMultiplier, progress),
            Lerp(from.CloudCoverage, to.CloudCoverage, progress),
            to.Mode == SkyProfileMode.Panorama && string.IsNullOrWhiteSpace(to.PanoramaResource)
                ? "area.sky.panorama_missing" : "area.sky.transition_ready");
    }

    public static WaterProductionPlan Water(WaterBodyProfile body, WaterQualityTier effectiveQuality,
        float submergedFraction)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!float.IsFinite(submergedFraction) || submergedFraction is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(submergedFraction));
        var halfX = body.Size.X / 2;
        var halfZ = body.Size.Z / 2;
        var segments = body.Kind == WaterBodyKind.River ? 8 : 1;
        var vertices = new List<Vector3Value>((segments + 1) * 2);
        for (var index = 0; index <= segments; index++)
        {
            var amount = index / (float)segments;
            var z = body.Center.Z - halfZ + (body.Size.Z * amount);
            var meander = body.Kind == WaterBodyKind.River ? MathF.Sin(amount * MathF.PI * 2) * halfX * .2f : 0;
            vertices.Add(new(body.Center.X - halfX + meander, body.Center.Y, z));
            vertices.Add(new(body.Center.X + halfX + meander, body.Center.Y, z));
        }
        var quality = (float)effectiveQuality;
        return new(body.Id, body.Kind, vertices, new Dictionary<string, float>
        {
            ["water_depth"] = body.MaximumDepth,
            ["water_flow_speed"] = body.FlowSpeed,
            ["water_quality"] = quality,
            ["water_foam"] = effectiveQuality >= WaterQualityTier.High ? 1 : 0,
            ["water_refraction"] = effectiveQuality >= WaterQualityTier.Medium ? 1 : 0,
        }, new(body.FlowDirectionX * body.FlowSpeed, 0, body.FlowDirectionZ * body.FlowSpeed),
            body.Buoyancy * submergedFraction, body.NavigationCost, body.SwimmingEnabled, body.AudioCue,
            string.IsNullOrWhiteSpace(body.MaterialResource)
                ? "area.water.procedural_material" : "area.water.material_ready");
    }

    private static IReadOnlyList<Vector3Value> Sample(IReadOnlyList<Vector3Value> points, float spacing)
    {
        var result = new List<Vector3Value> { points[0] };
        for (var index = 1; index < points.Count; index++)
        {
            var from = points[index - 1];
            var to = points[index];
            var distance = Distance(from, to);
            var steps = Math.Max(1, (int)MathF.Ceiling(distance / spacing));
            for (var step = 1; step <= steps; step++)
            {
                var amount = step / (float)steps;
                result.Add(new(from.X + ((to.X - from.X) * amount),
                    from.Y + ((to.Y - from.Y) * amount), from.Z + ((to.Z - from.Z) * amount)));
            }
        }
        return result;
    }

    private static bool Intersects(IReadOnlyList<Vector3Value> left, IReadOnlyList<Vector3Value> right, float radius) =>
        left.Any(a => right.Any(b => Distance(a, b) <= radius));

    private static float Distance(Vector3Value a, Vector3Value b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        var z = a.Z - b.Z;
        return MathF.Sqrt((x * x) + (y * y) + (z * z));
    }

    private static double Lerp(double from, double to, double amount) => from + ((to - from) * amount);
}
