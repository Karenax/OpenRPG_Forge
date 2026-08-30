using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public sealed record AreaWorldProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public TerrainDefinition Terrain { get; }
    public IReadOnlyList<BiomeLayerDefinition> Biomes { get; }
    public IReadOnlyList<TerrainFeatureDefinition> Features { get; }
    public VolumetricNavigationProfile VolumetricNavigation { get; }
    public IReadOnlyList<FlightRouteDefinition> FlightRoutes { get; }
    public AreaLoadingProfile Loading { get; }
    public string Provenance { get; }
    public TerrainPlacementSurfaceDefinition? PlacementSurface { get; }
    public AreaEnvironmentOccupancyProfile? EnvironmentOccupancy { get; }

    public AreaWorldProfile(Guid id, int schemaVersion, TerrainDefinition terrain,
        IReadOnlyList<BiomeLayerDefinition> biomes, IReadOnlyList<TerrainFeatureDefinition> features,
        VolumetricNavigationProfile volumetricNavigation, IReadOnlyList<FlightRouteDefinition> flightRoutes,
        AreaLoadingProfile loading, string provenance,
        TerrainPlacementSurfaceDefinition? placementSurface = null,
        AreaEnvironmentOccupancyProfile? environmentOccupancy = null)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(biomes);
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(volumetricNavigation);
        ArgumentNullException.ThrowIfNull(flightRoutes);
        ArgumentNullException.ThrowIfNull(loading);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Area world profile schema is invalid.");
        }

        var biomeValues = biomes.OrderByDescending(biome => biome.Priority).ThenBy(biome => biome.Id).ToArray();
        var featureValues = features.OrderBy(feature => feature.Id).ToArray();
        var routeValues = flightRoutes.OrderBy(route => route.Id).ToArray();
        if (biomeValues.Select(biome => biome.Id).Distinct().Count() != biomeValues.Length ||
            featureValues.Select(feature => feature.Id).Distinct().Count() != featureValues.Length ||
            routeValues.Select(route => route.Id).Distinct().Count() != routeValues.Length)
        {
            throw new ArgumentException("Area world collection IDs must be unique.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Terrain = terrain;
        Biomes = biomeValues;
        Features = featureValues;
        VolumetricNavigation = volumetricNavigation;
        FlightRoutes = routeValues;
        Loading = loading;
        Provenance = provenance.Trim();
        PlacementSurface = placementSurface;
        EnvironmentOccupancy = environmentOccupancy;
    }

    public float GroundHeightAt(float worldX, float worldZ) => PlacementSurface is { } surface
        ? surface.HeightAt(worldX, worldZ)
        : Terrain.Enabled ? Terrain.HeightAt(worldX, worldZ) : 0;

    public static AreaWorldProfile CreateDefault(AreaId areaId, AreaKind kind, AreaDimensions dimensions)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentNullException.ThrowIfNull(dimensions);
        var soilId = StableGuid(areaId, "terrain:material:soil");
        var grassId = StableGuid(areaId, "terrain:material:grass");
        var rockId = StableGuid(areaId, "terrain:material:rock");
        var enabled = kind == AreaKind.Exterior;
        const int samples = 17;
        var heights = new float[samples * samples];
        if (enabled)
        {
            for (var z = 0; z < samples; z++)
            {
                for (var x = 0; x < samples; x++)
                {
                    var nx = (x / (samples - 1f)) - 0.5f;
                    var nz = (z / (samples - 1f)) - 0.5f;
                    heights[(z * samples) + x] = Math.Clamp(
                        (MathF.Sin((nx * 5.2f) + 0.7f) + MathF.Cos((nz * 4.3f) - 0.4f)) * 0.12f +
                        MathF.Sin((nx + nz) * 8f) * 0.04f,
                        -1,
                        1);
                }
            }
        }

        var layers = enabled
            ? new TerrainMaterialLayer[]
            {
                new(soilId, "Terre", "#70533a", string.Empty, 4),
                new(grassId, "Prairie", "#5f8345", string.Empty, 5),
                new(rockId, "Roche", "#777b82", string.Empty, 3),
            }
            : [];
        var terrain = new TerrainDefinition(StableGuid(areaId, "terrain"), TerrainDefinition.CurrentSchemaVersion,
            enabled ? TerrainProviderKind.Heightmap : TerrainProviderKind.None,
            dimensions.Width, dimensions.Depth, samples, samples, 16, 2026, enabled ? 8 : 0, heights,
            layers, [], string.Empty, string.Empty, string.Empty, "migration-default");
        var biomes = enabled
            ? new[]
            {
                new BiomeLayerDefinition(StableGuid(areaId, "biome:meadow"), "Prairie", grassId,
                    -512, 512, 0, 38, 0, 1, 0.45f, string.Empty, ["meadow", "temperate"], ["water", "road"], 20,
                    "migration-default"),
                new BiomeLayerDefinition(StableGuid(areaId, "biome:rock"), "Roche", rockId,
                    -512, 512, 32, 90, 0, 1, 0.08f, string.Empty, ["rock", "cliff"], ["water"], 30,
                    "migration-default"),
            }
            : [];
        var volume = new VolumetricNavigationProfile(StableGuid(areaId, "volumetric-navigation"),
            VolumetricNavigationProfile.CurrentSchemaVersion, enabled, enabled ? VolumetricNavigationMode.Flight : VolumetricNavigationMode.None,
            4, new Vector3Value(0, 12, 0), new Vector3Value(dimensions.Width, 24, dimensions.Depth), 1,
            [], [], false, "migration-default");
        var loading = new AreaLoadingProfile(StableGuid(areaId, "loading"), AreaLoadingProfile.CurrentSchemaVersion,
            true, true, true, 512, 4, 2, 2000, [], "migration-default");
        return new AreaWorldProfile(StableGuid(areaId, "world"), CurrentSchemaVersion, terrain, biomes, [], volume,
            [], loading, "migration-default");
    }

    public static AreaWorldProfile CloneForArea(AreaWorldProfile source, AreaId destinationAreaId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destinationAreaId);
        var materialMap = source.Terrain.MaterialLayers.Select((layer, index) => (layer.Id, NewId: StableGuid(destinationAreaId, $"terrain:material:{index}")))
            .ToDictionary(item => item.Id, item => item.NewId);
        var terrain = new TerrainDefinition(StableGuid(destinationAreaId, "terrain"), source.Terrain.SchemaVersion,
            source.Terrain.Provider, source.Terrain.Width, source.Terrain.Depth, source.Terrain.SamplesX,
            source.Terrain.SamplesZ, source.Terrain.ChunkSize, source.Terrain.Seed, source.Terrain.HeightScale,
            source.Terrain.Heights,
            source.Terrain.MaterialLayers.Select((layer, index) => new TerrainMaterialLayer(
                materialMap[layer.Id], layer.Name, layer.AlbedoColor, layer.MaterialResource, layer.UvScale)).ToArray(),
            source.Terrain.Holes.Select((hole, index) => new TerrainHoleDefinition(
                StableGuid(destinationAreaId, $"terrain:hole:{index}"), hole.Name, hole.StartX, hole.StartZ, hole.Width, hole.Depth, "area-clone")).ToArray(),
            source.Terrain.HeightmapResource, source.Terrain.VoxelResource, source.Terrain.MeshResource, "area-clone",
            source.Terrain.MaterialPaint.Select(cell => new TerrainMaterialPaintCell(
                cell.X, cell.Z, materialMap[cell.MaterialLayerId])).ToArray());
        var biomes = source.Biomes.Select((biome, index) => new BiomeLayerDefinition(
            StableGuid(destinationAreaId, $"biome:{index}"), biome.Name, materialMap[biome.MaterialLayerId],
            biome.MinimumAltitude, biome.MaximumAltitude, biome.MinimumSlopeDegrees, biome.MaximumSlopeDegrees,
            biome.MinimumMoisture, biome.MaximumMoisture, biome.ScatterDensity, biome.ScatterResource,
            biome.Tags, biome.ExclusionTags, biome.Priority, "area-clone")).ToArray();
        var features = source.Features.Select((feature, index) => new TerrainFeatureDefinition(
            StableGuid(destinationAreaId, $"feature:{index}"), feature.SchemaVersion, feature.Name, feature.Kind,
            feature.ControlPoints, feature.Width, feature.Height, feature.BankDegrees, feature.CrossingPolicy,
            feature.CompatibleBiomeTags, feature.MaterialResource, "area-clone")).ToArray();
        var volume = new VolumetricNavigationProfile(StableGuid(destinationAreaId, "volumetric-navigation"),
            source.VolumetricNavigation.SchemaVersion, source.VolumetricNavigation.Enabled, source.VolumetricNavigation.Modes,
            source.VolumetricNavigation.CellSize, source.VolumetricNavigation.Center, source.VolumetricNavigation.Size,
            source.VolumetricNavigation.AvoidanceRadius,
            source.VolumetricNavigation.Obstacles.Select((item, index) => new NavigationVolumeDefinition(
                StableGuid(destinationAreaId, $"volume:obstacle:{index}"), item.Name, item.Center, item.Size, item.CostMultiplier, "area-clone")).ToArray(),
            source.VolumetricNavigation.CostVolumes.Select((item, index) => new NavigationVolumeDefinition(
                StableGuid(destinationAreaId, $"volume:cost:{index}"), item.Name, item.Center, item.Size, item.CostMultiplier, "area-clone")).ToArray(),
            source.VolumetricNavigation.DebugVisible, "area-clone");
        var routes = source.FlightRoutes.Select((route, routeIndex) => new FlightRouteDefinition(
            StableGuid(destinationAreaId, $"flight-route:{routeIndex}"), route.SchemaVersion, route.Name,
            route.Nodes.Select((node, nodeIndex) => new FlightRouteNode(
                StableGuid(destinationAreaId, $"flight-route:{routeIndex}:node:{nodeIndex}"), node.Name, node.Position, node.Speed, node.BankDegrees)).ToArray(),
            route.Curve, route.ObstaclePolicy, route.Closed, route.DebugVisible, "area-clone")).ToArray();
        var loading = new AreaLoadingProfile(StableGuid(destinationAreaId, "loading"), source.Loading.SchemaVersion,
            source.Loading.AsyncStreaming, source.Loading.PrefetchDependencies, source.Loading.CancellationEnabled,
            source.Loading.CacheBudgetMegabytes, source.Loading.MaximumConcurrentLoads, source.Loading.ChunkRadius,
            source.Loading.TargetLoadMilliseconds, source.Loading.RequiredResources, "area-clone");
        var placementSurface = source.PlacementSurface is null
            ? null
            : new TerrainPlacementSurfaceDefinition(StableGuid(destinationAreaId, "placement-surface"),
                source.PlacementSurface.SchemaVersion, source.PlacementSurface.Width,
                source.PlacementSurface.Depth, source.PlacementSurface.SamplesX,
                source.PlacementSurface.SamplesZ, source.PlacementSurface.HeightScale,
                source.PlacementSurface.Heights, source.PlacementSurface.CellDiagonal,
                source.PlacementSurface.SourceFingerprint, "area-clone");
        var occupancy = source.EnvironmentOccupancy is null
            ? null
            : new AreaEnvironmentOccupancyProfile(StableGuid(destinationAreaId, "environment-occupancy"),
                source.EnvironmentOccupancy.SchemaVersion, source.EnvironmentOccupancy.Width,
                source.EnvironmentOccupancy.Depth, source.EnvironmentOccupancy.CellSizeMeters,
                source.EnvironmentOccupancy.Cells, source.EnvironmentOccupancy.SourceFingerprint, "area-clone");
        return new AreaWorldProfile(StableGuid(destinationAreaId, "world"), source.SchemaVersion, terrain, biomes,
            features, volume, routes, loading, "area-clone", placementSurface, occupancy);
    }

    public static AreaWorldProfile ResizeForArea(AreaWorldProfile source, AreaDimensions dimensions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dimensions);
        var terrain = new TerrainDefinition(source.Terrain.Id, source.Terrain.SchemaVersion, source.Terrain.Provider,
            dimensions.Width, dimensions.Depth, source.Terrain.SamplesX, source.Terrain.SamplesZ,
            source.Terrain.ChunkSize, source.Terrain.Seed, source.Terrain.HeightScale, source.Terrain.Heights,
            source.Terrain.MaterialLayers, source.Terrain.Holes, source.Terrain.HeightmapResource,
            source.Terrain.VoxelResource, source.Terrain.MeshResource, "area-resize", source.Terrain.MaterialPaint);
        var volume = new VolumetricNavigationProfile(source.VolumetricNavigation.Id,
            source.VolumetricNavigation.SchemaVersion, source.VolumetricNavigation.Enabled,
            source.VolumetricNavigation.Modes, source.VolumetricNavigation.CellSize,
            source.VolumetricNavigation.Center,
            new Vector3Value(dimensions.Width, source.VolumetricNavigation.Size.Y, dimensions.Depth),
            source.VolumetricNavigation.AvoidanceRadius, source.VolumetricNavigation.Obstacles,
            source.VolumetricNavigation.CostVolumes, source.VolumetricNavigation.DebugVisible, "area-resize");
        return new AreaWorldProfile(source.Id, source.SchemaVersion, terrain, source.Biomes, source.Features, volume,
            source.FlightRoutes, source.Loading, "area-resize");
    }

    internal static Guid StableGuid(AreaId areaId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId}:{discriminator}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}
