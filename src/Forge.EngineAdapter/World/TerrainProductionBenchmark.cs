namespace Forge.EngineAdapter.World;

public sealed record TerrainProductionBenchmarkSample(
    string ModuleSlug,
    float WidthMeters,
    float DepthMeters,
    int HeightSamples,
    int TerrainVertices,
    int TerrainTriangles,
    int CollisionShapes,
    int ScatterLayers,
    int ScatterInstances,
    int NavigationRegions,
    int NavigationPolygons,
    int NavigationBlockedTriangles,
    int NavigationLinks,
    int NavigationPathPoints,
    int LodAssetCount,
    int LodLevelCount,
    int CullingGroupCount,
    long GovernedAssetBytes,
    long ManagedAllocatedBytes,
    long ManagedHeapDeltaBytes,
    long ProcessWorkingSetDeltaBytes,
    double ReconstructionMilliseconds,
    double CollisionMilliseconds,
    double NavigationMilliseconds,
    double SculptureMilliseconds,
    double NavigationPathMilliseconds,
    bool SculptureSucceeded);

public sealed record TerrainProductionBenchmarkBudget(
    int MinimumHeightSamples,
    int MinimumTerrainVertices,
    int MinimumCollisionShapes,
    int MinimumNavigationPolygons,
    int MinimumLodAssets,
    int MinimumLodLevels,
    int MinimumCullingGroups,
    long MaximumManagedAllocatedBytes,
    long MaximumManagedHeapDeltaBytes,
    long MaximumProcessWorkingSetDeltaBytes,
    double MaximumReconstructionMilliseconds,
    double MaximumCollisionMilliseconds,
    double MaximumNavigationMilliseconds,
    double MaximumSculptureMilliseconds,
    double MaximumNavigationPathMilliseconds)
{
    public static TerrainProductionBenchmarkBudget RepresentativeExterior128 { get; } = new(
        MinimumHeightSamples: 33 * 33,
        MinimumTerrainVertices: 33 * 33,
        MinimumCollisionShapes: 2,
        MinimumNavigationPolygons: 1_500,
        MinimumLodAssets: 22,
        MinimumLodLevels: 66,
        MinimumCullingGroups: 3,
        MaximumManagedAllocatedBytes: 256L * 1024 * 1024,
        MaximumManagedHeapDeltaBytes: 256L * 1024 * 1024,
        MaximumProcessWorkingSetDeltaBytes: 512L * 1024 * 1024,
        MaximumReconstructionMilliseconds: 5_000,
        MaximumCollisionMilliseconds: 2_500,
        MaximumNavigationMilliseconds: 2_500,
        MaximumSculptureMilliseconds: 500,
        MaximumNavigationPathMilliseconds: 5_000);
}

public sealed record TerrainProductionBenchmarkCheck(
    string Key,
    double Value,
    double Budget,
    string Comparison,
    string Unit,
    bool Passed);

public sealed record TerrainProductionBenchmarkReport(
    int SchemaVersion,
    DateTimeOffset MeasuredAtUtc,
    TerrainProductionBenchmarkSample Sample,
    TerrainProductionBenchmarkBudget Budget,
    IReadOnlyList<TerrainProductionBenchmarkCheck> Checks,
    bool Passed);

public static class TerrainProductionBenchmark
{
    public const int CurrentSchemaVersion = 1;

    public static TerrainProductionBenchmarkReport Evaluate(
        TerrainProductionBenchmarkSample sample,
        TerrainProductionBenchmarkBudget? budget = null,
        DateTimeOffset? measuredAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(sample);
        budget ??= TerrainProductionBenchmarkBudget.RepresentativeExterior128;
        var checks = new List<TerrainProductionBenchmarkCheck>
        {
            Minimum("height-samples", sample.HeightSamples, budget.MinimumHeightSamples, "count"),
            Minimum("terrain-vertices", sample.TerrainVertices, budget.MinimumTerrainVertices, "count"),
            Minimum("collision-shapes", sample.CollisionShapes, budget.MinimumCollisionShapes, "count"),
            Minimum("navigation-polygons", sample.NavigationPolygons, budget.MinimumNavigationPolygons, "count"),
            Minimum("lod-assets", sample.LodAssetCount, budget.MinimumLodAssets, "count"),
            Minimum("lod-levels", sample.LodLevelCount, budget.MinimumLodLevels, "count"),
            Minimum("culling-groups", sample.CullingGroupCount, budget.MinimumCullingGroups, "count"),
            Maximum("managed-allocated", sample.ManagedAllocatedBytes, budget.MaximumManagedAllocatedBytes, "bytes"),
            Maximum("managed-heap-delta", sample.ManagedHeapDeltaBytes, budget.MaximumManagedHeapDeltaBytes, "bytes"),
            Maximum("working-set-delta", sample.ProcessWorkingSetDeltaBytes,
                budget.MaximumProcessWorkingSetDeltaBytes, "bytes"),
            Maximum("reconstruction", sample.ReconstructionMilliseconds,
                budget.MaximumReconstructionMilliseconds, "ms"),
            Maximum("collisions", sample.CollisionMilliseconds, budget.MaximumCollisionMilliseconds, "ms"),
            Maximum("navigation", sample.NavigationMilliseconds, budget.MaximumNavigationMilliseconds, "ms"),
            Maximum("sculpture", sample.SculptureMilliseconds, budget.MaximumSculptureMilliseconds, "ms"),
            Maximum("navigation-path", sample.NavigationPathMilliseconds,
                budget.MaximumNavigationPathMilliseconds, "ms"),
            new("sculpture-command", sample.SculptureSucceeded ? 1 : 0, 1, "==", "bool",
                sample.SculptureSucceeded),
        };
        return new TerrainProductionBenchmarkReport(
            CurrentSchemaVersion,
            measuredAtUtc ?? DateTimeOffset.UtcNow,
            sample,
            budget,
            checks,
            checks.All(check => check.Passed));
    }

    private static TerrainProductionBenchmarkCheck Minimum(string key, double value, double budget, string unit) =>
        new(key, value, budget, ">=", unit, value >= budget);

    private static TerrainProductionBenchmarkCheck Maximum(string key, double value, double budget, string unit) =>
        new(key, value, budget, "<=", unit, value <= budget);
}
