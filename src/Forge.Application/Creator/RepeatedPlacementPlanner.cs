using Forge.Domain.Spatial;
using Forge.Domain.Authoring;

namespace Forge.Application.Creator;

public enum PlacementDistribution
{
    Line,
    Grid,
    DeterministicScatter,
}

public sealed record RepeatedPlacementRequest(
    Vector3Value Origin,
    int Count,
    float Spacing,
    PlacementDistribution Distribution,
    int Columns = 4,
    int Seed = 0);

public static class RepeatedPlacementPlanner
{
    public static IReadOnlyList<Vector3Value> Plan(RepeatedPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Count is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(request.Count));
        if (!float.IsFinite(request.Spacing) || request.Spacing <= 0f)
            throw new ArgumentOutOfRangeException(nameof(request.Spacing));
        if (request.Columns < 1) throw new ArgumentOutOfRangeException(nameof(request.Columns));

        return Enumerable.Range(0, request.Count).Select(index => request.Distribution switch
        {
            PlacementDistribution.Line => Offset(request.Origin, index * request.Spacing, 0f),
            PlacementDistribution.Grid => Offset(request.Origin,
                index % request.Columns * request.Spacing,
                index / request.Columns * request.Spacing),
            PlacementDistribution.DeterministicScatter => Scatter(request, index),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Distribution)),
        }).ToArray();
    }

    private static Vector3Value Scatter(RepeatedPlacementRequest request, int index)
    {
        var random = new Random(HashCode.Combine(request.Seed, index));
        var radius = request.Spacing * MathF.Sqrt(index);
        var angle = (float)(random.NextDouble() * Math.Tau);
        return Offset(request.Origin, MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
    }

    private static Vector3Value Offset(Vector3Value origin, float x, float z) =>
        new(origin.X + x, origin.Y, origin.Z + z);

    public static RepeatedPlacementPlan Plan(
        EditorRepeatedPlacementProfile profile,
        Vector3Value origin,
        int count,
        Func<Vector3Value, PlacementSurfaceSample>? sampleSurface = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Enabled) throw new InvalidOperationException("Repeated placement is disabled by the editor profile.");
        var distribution = profile.Distribution switch
        {
            EditorPlacementDistribution.Line => PlacementDistribution.Line,
            EditorPlacementDistribution.Grid => PlacementDistribution.Grid,
            _ => PlacementDistribution.DeterministicScatter,
        };
        var positions = Plan(new RepeatedPlacementRequest(origin, count, profile.Spacing, distribution,
            profile.Columns, profile.Seed));
        var poses = positions.Select(position => Align(position, profile, sampleSurface)).ToArray();
        return new(profile.Id, EditorRepeatedPlacementProfile.CurrentSchemaVersion, profile.Provenance,
            profile.BrushMode, profile.ContinuousStroke, poses);
    }

    public static RepeatedPlacementPlan PlanBrushStroke(
        EditorRepeatedPlacementProfile profile,
        IReadOnlyList<Vector3Value> samples,
        Func<Vector3Value, PlacementSurfaceSample>? sampleSurface = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(samples);
        if (!profile.Enabled || !profile.BrushMode)
            throw new InvalidOperationException("Brush placement is disabled by the editor profile.");
        if (samples.Count == 0) throw new ArgumentException("A brush stroke requires at least one sample.", nameof(samples));
        var accepted = new List<Vector3Value> { samples[0] };
        foreach (var sample in samples.Skip(1))
        {
            var previous = accepted[^1];
            var distance = MathF.Sqrt(MathF.Pow(sample.X - previous.X, 2) +
                                      MathF.Pow(sample.Y - previous.Y, 2) +
                                      MathF.Pow(sample.Z - previous.Z, 2));
            if (distance >= profile.Spacing) accepted.Add(sample);
        }
        var poses = accepted.Select(position => Align(position, profile, sampleSurface)).ToArray();
        return new(profile.Id, EditorRepeatedPlacementProfile.CurrentSchemaVersion, profile.Provenance,
            true, profile.ContinuousStroke, poses);
    }

    private static RepeatedPlacementPose Align(Vector3Value position, EditorRepeatedPlacementProfile profile,
        Func<Vector3Value, PlacementSurfaceSample>? sampleSurface)
    {
        var sample = sampleSurface?.Invoke(position) ?? PlacementSurfaceSample.Grid(position);
        return profile.Alignment switch
        {
            EditorPlacementAlignment.SurfaceNormal => new(
                new Vector3Value(position.X, sample.Position.Y + profile.SurfaceOffset, position.Z),
                sample.Normal, profile.Alignment),
            EditorPlacementAlignment.Socket => new(
                new Vector3Value(sample.Position.X, sample.Position.Y + profile.SurfaceOffset, sample.Position.Z),
                sample.Normal, profile.Alignment),
            _ => new(new Vector3Value(position.X, position.Y + profile.SurfaceOffset, position.Z),
                new Vector3Value(0, 1, 0), profile.Alignment),
        };
    }
}

public sealed record PlacementSurfaceSample(Vector3Value Position, Vector3Value Normal)
{
    public static PlacementSurfaceSample Grid(Vector3Value position) => new(position, new Vector3Value(0, 1, 0));
}

public sealed record RepeatedPlacementPose(Vector3Value Position, Vector3Value SurfaceNormal,
    EditorPlacementAlignment Alignment);

public sealed record RepeatedPlacementPlan(Guid ProfileId, int SchemaVersion, string Provenance,
    bool BrushMode, bool ContinuousStroke, IReadOnlyList<RepeatedPlacementPose> Poses);
