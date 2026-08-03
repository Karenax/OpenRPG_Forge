using Forge.Domain.Spatial;

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
}
