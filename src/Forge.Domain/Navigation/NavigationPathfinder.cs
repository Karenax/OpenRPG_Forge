namespace Forge.Domain.Navigation;

public static class NavigationPathfinder
{
    private static readonly NavigationCell[] Directions =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    public static IReadOnlySet<NavigationCell> ReachableFrom(
        NavigationSurface surface,
        IEnumerable<NavigationCell> starts)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(starts);
        var visited = new HashSet<NavigationCell>();
        var queue = new Queue<NavigationCell>();
        foreach (var start in starts.Where(surface.IsWalkable).Distinct())
        {
            visited.Add(start);
            queue.Enqueue(start);
        }

        var offMesh = surface.OffMeshEdges
            .GroupBy(edge => edge.From)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.To).ToArray());
        while (queue.TryDequeue(out var cell))
        {
            foreach (var direction in Directions)
            {
                Visit(new NavigationCell(cell.X + direction.X, cell.Z + direction.Z));
            }

            if (offMesh.TryGetValue(cell, out var linked))
            {
                foreach (var target in linked) Visit(target);
            }
        }

        return visited;

        void Visit(NavigationCell candidate)
        {
            if (surface.IsWalkable(candidate) && visited.Add(candidate))
            {
                queue.Enqueue(candidate);
            }
        }
    }
}
