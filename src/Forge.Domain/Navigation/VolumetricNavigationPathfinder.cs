using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public static class VolumetricNavigationPathfinder
{
    private static readonly VolumetricCell[] Directions =
    [
        new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
    ];

    public static VolumetricPathResult FindPath(VolumetricNavigationGraph graph, Vector3Value start,
        Vector3Value destination, int maximumVisitedCells = 100_000)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (maximumVisitedCells <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumVisitedCells));
        }

        var startCell = graph.CellAt(start);
        var destinationCell = graph.CellAt(destination);
        if (!graph.IsPassable(startCell) || !graph.IsPassable(destinationCell))
        {
            return new VolumetricPathResult(false, [], 0, 0);
        }

        var frontier = new PriorityQueue<VolumetricCell, float>();
        var cameFrom = new Dictionary<VolumetricCell, VolumetricCell>();
        var costs = new Dictionary<VolumetricCell, float> { [startCell] = 0 };
        frontier.Enqueue(startCell, Heuristic(startCell, destinationCell));
        var visited = 0;
        while (frontier.TryDequeue(out var current, out _) && visited < maximumVisitedCells)
        {
            visited++;
            if (current == destinationCell)
            {
                var cells = Reconstruct(cameFrom, startCell, destinationCell);
                return new VolumetricPathResult(true, cells.Select(graph.CenterOf).ToArray(), visited, costs[current]);
            }

            foreach (var direction in Directions)
            {
                var next = new VolumetricCell(current.X + direction.X, current.Y + direction.Y, current.Z + direction.Z);
                if (!graph.IsPassable(next))
                {
                    continue;
                }

                var nextCost = costs[current] + graph.CostAt(next);
                if (costs.TryGetValue(next, out var known) && nextCost >= known)
                {
                    continue;
                }

                costs[next] = nextCost;
                cameFrom[next] = current;
                frontier.Enqueue(next, nextCost + Heuristic(next, destinationCell));
            }
        }

        return new VolumetricPathResult(false, [], visited, 0);
    }

    private static float Heuristic(VolumetricCell left, VolumetricCell right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y) + Math.Abs(left.Z - right.Z);

    private static IReadOnlyList<VolumetricCell> Reconstruct(IReadOnlyDictionary<VolumetricCell, VolumetricCell> cameFrom,
        VolumetricCell start, VolumetricCell destination)
    {
        var path = new List<VolumetricCell> { destination };
        var current = destination;
        while (current != start)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
