using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Domain.Validation;

public static class AreaWorldValidator
{
    public const string TerrainDimensionsCode = "area.world.terrain_dimensions_mismatch";
    public const string BiomeMaterialCode = "area.world.biome_material_missing";
    public const string FeatureBoundsCode = "area.world.feature_out_of_bounds";
    public const string FeatureBiomeCode = "area.world.feature_biome_missing";
    public const string CrossingBridgeCode = "area.world.crossing_bridge_missing";
    public const string FlightRouteBoundsCode = "area.world.flight_route_out_of_bounds";
    public const string LoadingResourceCode = "area.world.loading_resource_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area, AreaWorldProfile world)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(world);
        var diagnostics = new List<DomainDiagnostic>();
        var path = $"areas/{area.Id}/world";
        if (world.Terrain.Width != area.Dimensions.Width || world.Terrain.Depth != area.Dimensions.Depth)
        {
            diagnostics.Add(new DomainDiagnostic(TerrainDimensionsCode,
                "Terrain dimensions must match the area dimensions.", $"{path}/terrain"));
        }

        var materials = world.Terrain.MaterialLayers.Select(layer => layer.Id).ToHashSet();
        foreach (var biome in world.Biomes.Where(biome => !materials.Contains(biome.MaterialLayerId)))
        {
            diagnostics.Add(new DomainDiagnostic(BiomeMaterialCode,
                $"Biome '{biome.Name}' references a missing terrain material.", $"{path}/biomes/{biome.Id}/materialLayerId"));
        }

        var biomeTags = world.Biomes.SelectMany(biome => biome.Tags).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in world.Features)
        {
            if (feature.ControlPoints.Any(point => !Contains(area, point)))
            {
                diagnostics.Add(new DomainDiagnostic(FeatureBoundsCode,
                    $"Feature '{feature.Name}' leaves the area bounds.", $"{path}/features/{feature.Id}/controlPoints"));
            }

            var missingTag = feature.CompatibleBiomeTags.FirstOrDefault(tag => !biomeTags.Contains(tag));
            if (missingTag is not null)
            {
                diagnostics.Add(new DomainDiagnostic(FeatureBiomeCode,
                    $"Feature '{feature.Name}' references missing biome tag '{missingTag}'.",
                    $"{path}/features/{feature.Id}/compatibleBiomeTags"));
            }
        }

        var roads = world.Features.Where(feature => feature.Kind == TerrainFeatureKind.Road).ToArray();
        var rivers = world.Features.Where(feature => feature.Kind == TerrainFeatureKind.River).ToArray();
        var bridges = world.Features.Where(feature => feature.Kind == TerrainFeatureKind.Bridge).ToArray();
        foreach (var road in roads)
        {
            foreach (var river in rivers.Where(river => Crosses(road, river)))
            {
                if ((road.CrossingPolicy == TerrainFeatureCrossingPolicy.RequiresBridge ||
                     river.CrossingPolicy == TerrainFeatureCrossingPolicy.RequiresBridge) &&
                    !bridges.Any(bridge => Crosses(bridge, road) && Crosses(bridge, river)))
                {
                    diagnostics.Add(new DomainDiagnostic(CrossingBridgeCode,
                        $"Road '{road.Name}' crosses river '{river.Name}' without a compatible bridge.",
                        $"{path}/features/{road.Id}/crossingPolicy"));
                }
            }
        }

        foreach (var route in world.FlightRoutes.Where(route => route.Nodes.Any(node => !ContainsVolume(world.VolumetricNavigation, node.Position))))
        {
            diagnostics.Add(new DomainDiagnostic(FlightRouteBoundsCode,
                $"Flight route '{route.Name}' leaves the volumetric navigation bounds.", $"{path}/flightRoutes/{route.Id}/nodes"));
        }

        return diagnostics;
    }

    private static bool Contains(AreaDefinition area, Vector3Value point) =>
        point.X >= -area.Dimensions.Width / 2f && point.X <= area.Dimensions.Width / 2f &&
        point.Z >= -area.Dimensions.Depth / 2f && point.Z <= area.Dimensions.Depth / 2f;

    private static bool ContainsVolume(VolumetricNavigationProfile profile, Vector3Value point) =>
        point.X >= profile.Center.X - profile.Size.X / 2f && point.X <= profile.Center.X + profile.Size.X / 2f &&
        point.Y >= profile.Center.Y - profile.Size.Y / 2f && point.Y <= profile.Center.Y + profile.Size.Y / 2f &&
        point.Z >= profile.Center.Z - profile.Size.Z / 2f && point.Z <= profile.Center.Z + profile.Size.Z / 2f;

    private static bool Crosses(TerrainFeatureDefinition first, TerrainFeatureDefinition second)
    {
        for (var left = 0; left < first.ControlPoints.Count - 1; left++)
        {
            for (var right = 0; right < second.ControlPoints.Count - 1; right++)
            {
                if (SegmentsCross(first.ControlPoints[left], first.ControlPoints[left + 1],
                        second.ControlPoints[right], second.ControlPoints[right + 1]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsCross(Vector3Value a, Vector3Value b, Vector3Value c, Vector3Value d)
    {
        static float Orientation(Vector3Value p, Vector3Value q, Vector3Value r) =>
            ((q.Z - p.Z) * (r.X - q.X)) - ((q.X - p.X) * (r.Z - q.Z));
        var first = Orientation(a, b, c);
        var second = Orientation(a, b, d);
        var third = Orientation(c, d, a);
        var fourth = Orientation(c, d, b);
        return first * second < 0 && third * fourth < 0;
    }
}
