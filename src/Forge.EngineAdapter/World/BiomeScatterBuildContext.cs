using Forge.Domain.Areas;

namespace Forge.EngineAdapter.World;

public sealed record BiomeScatterBuildDiagnostics(
    int PreparedExclusionSegments,
    long ExclusionQueries,
    long CandidateSegmentChecks,
    long LinearCandidateSegmentChecks);

public sealed record BiomeScatterBuildResult(
    IReadOnlyList<BiomeScatterPlacement> Placements,
    BiomeScatterBuildDiagnostics Diagnostics);

public sealed class BiomeScatterBuildContext
{
    private readonly AreaWorldProfile _world;
    private readonly IReadOnlyDictionary<(int X, int Z), Guid> _materialPaint;
    private readonly IReadOnlyList<BiomeScatterPreparedFeature> _features;

    internal BiomeScatterBuildContext(AreaWorldProfile world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _materialPaint = world.Terrain.MaterialPaint.ToDictionary(
            cell => (cell.X, cell.Z),
            cell => cell.MaterialLayerId);
        _features = world.Features.Select(BiomeScatterPreparedFeature.Create).ToArray();
    }

    public int MaterialPaintPreparationPasses => 1;

    public BiomeScatterBuildResult Build(BiomeLayerDefinition biome)
    {
        ArgumentNullException.ThrowIfNull(biome);
        return BiomeScatterPlanBuilder.BuildPrepared(_world, biome, _materialPaint, _features);
    }
}

internal sealed record BiomeScatterPreparedFeature(
    string KindTag,
    bool IsInvisibleExclusionGuide,
    IReadOnlySet<string> CompatibleBiomeTags,
    float Width,
    IReadOnlyList<Forge.Domain.Spatial.Vector3Value> ControlPoints)
{
    internal static BiomeScatterPreparedFeature Create(TerrainFeatureDefinition feature) => new(
        feature.Kind.ToString().ToLowerInvariant(),
        string.Equals(
            feature.MaterialResource,
            "builtin/invisible-scatter-exclusion",
            StringComparison.OrdinalIgnoreCase),
        feature.CompatibleBiomeTags.ToHashSet(StringComparer.OrdinalIgnoreCase),
        feature.Width,
        feature.ControlPoints);
}

internal sealed class BiomeScatterExclusionIndex
{
    private const float CellSize = 32f;
    private readonly IReadOnlyDictionary<(int X, int Z), IReadOnlyList<Segment>> _cells;

    private BiomeScatterExclusionIndex(
        int segmentCount,
        IReadOnlyDictionary<(int X, int Z), IReadOnlyList<Segment>> cells)
    {
        SegmentCount = segmentCount;
        _cells = cells;
    }

    internal int SegmentCount { get; }

    internal static BiomeScatterExclusionIndex Create(
        IReadOnlyList<BiomeScatterPreparedFeature> features,
        BiomeLayerDefinition biome)
    {
        var cells = new Dictionary<(int X, int Z), List<Segment>>();
        var ordinal = 0;
        var edgeClearance = biome.Tags.Contains("lush-meadow", StringComparer.OrdinalIgnoreCase)
            ? .45f
            : 1.5f;
        foreach (var feature in features)
        {
            var excludedByKind = biome.ExclusionTags.Contains(feature.KindTag, StringComparer.OrdinalIgnoreCase);
            var excludedByGuide = feature.IsInvisibleExclusionGuide &&
                                  (feature.CompatibleBiomeTags.Count == 0 ||
                                   feature.CompatibleBiomeTags.Any(tag =>
                                       biome.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase) ||
                                       biome.ExclusionTags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
            if (!excludedByKind && !excludedByGuide) continue;

            var radius = feature.Width * .5f + edgeClearance;
            for (var index = 0; index < feature.ControlPoints.Count - 1; index++)
            {
                var a = feature.ControlPoints[index];
                var b = feature.ControlPoints[index + 1];
                var segment = new Segment(ordinal++, a.X, a.Z, b.X, b.Z, radius);
                var minimumCellX = CellAt(MathF.Min(a.X, b.X) - radius);
                var maximumCellX = CellAt(MathF.Max(a.X, b.X) + radius);
                var minimumCellZ = CellAt(MathF.Min(a.Z, b.Z) - radius);
                var maximumCellZ = CellAt(MathF.Max(a.Z, b.Z) + radius);
                for (var cellZ = minimumCellZ; cellZ <= maximumCellZ; cellZ++)
                {
                    for (var cellX = minimumCellX; cellX <= maximumCellX; cellX++)
                    {
                        if (!cells.TryGetValue((cellX, cellZ), out var segments))
                        {
                            segments = [];
                            cells[(cellX, cellZ)] = segments;
                        }
                        segments.Add(segment);
                    }
                }
            }
        }

        return new BiomeScatterExclusionIndex(
            ordinal,
            cells.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<Segment>)pair.Value.ToArray()));
    }

    internal bool IsExcluded(float x, float z, out int candidateChecks, out int linearChecks)
    {
        candidateChecks = 0;
        linearChecks = SegmentCount;
        if (SegmentCount == 0 || !_cells.TryGetValue((CellAt(x), CellAt(z)), out var segments))
            return false;

        foreach (var segment in segments)
        {
            candidateChecks++;
            if (DistanceToSegment(x, z, segment) > segment.Radius) continue;
            linearChecks = segment.Ordinal + 1;
            return true;
        }
        return false;
    }

    private static int CellAt(float coordinate) => (int)MathF.Floor(coordinate / CellSize);

    private static float DistanceToSegment(float px, float pz, Segment segment)
    {
        var dx = segment.Bx - segment.Ax;
        var dz = segment.Bz - segment.Az;
        var length = dx * dx + dz * dz;
        if (length <= .0001f)
            return MathF.Sqrt((px - segment.Ax) * (px - segment.Ax) +
                              (pz - segment.Az) * (pz - segment.Az));
        var t = Math.Clamp(((px - segment.Ax) * dx + (pz - segment.Az) * dz) / length, 0, 1);
        var x = segment.Ax + t * dx;
        var z = segment.Az + t * dz;
        return MathF.Sqrt((px - x) * (px - x) + (pz - z) * (pz - z));
    }

    private sealed record Segment(int Ordinal, float Ax, float Az, float Bx, float Bz, float Radius);
}
