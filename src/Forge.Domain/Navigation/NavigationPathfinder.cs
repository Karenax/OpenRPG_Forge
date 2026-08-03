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

    public static NavigationPathResult FindPath(NavigationSurface surface, NavigationPathQuery query)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(query);
        var offMesh = surface.OffMeshEdges
            .GroupBy(edge => edge.From)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.To).Order().ToArray());
        bool Available(NavigationCell cell) => surface.IsWalkable(cell) && !query.DynamicObstacles.Contains(cell);

        var start = ResolveEndpoint(query.Start);
        if (start is null)
        {
            return Failed(NavigationPathFailure.StartBlocked, null, null, 0);
        }

        var goal = ResolveEndpoint(query.Goal);
        if (goal is null)
        {
            return Failed(NavigationPathFailure.GoalBlocked, start, null, 0);
        }

        var frontier = new PriorityQueue<NavigationCell, (float Score, float Cost, int Z, int X)>();
        var cameFrom = new Dictionary<NavigationCell, NavigationCell>();
        var costs = new Dictionary<NavigationCell, float> { [start.Value] = 0 };
        frontier.Enqueue(start.Value, (Heuristic(start.Value, goal.Value), 0, start.Value.Z, start.Value.X));
        var visited = 0;
        while (frontier.TryDequeue(out var current, out _))
        {
            visited++;
            if (visited > query.MaximumVisitedCells)
            {
                return Failed(NavigationPathFailure.SearchBudgetExceeded, start, goal, visited);
            }

            if (current == goal.Value)
            {
                var path = new List<NavigationCell> { current };
                while (cameFrom.TryGetValue(current, out var previous))
                {
                    path.Add(previous);
                    current = previous;
                }

                path.Reverse();
                return new NavigationPathResult(
                    true,
                    path,
                    costs[goal.Value],
                    visited,
                    NavigationPathFailure.None,
                    query.Start,
                    query.Goal,
                    start,
                    goal,
                    start != query.Start,
                    goal != query.Goal);
            }

            foreach (var next in Neighbors(current))
            {
                if (!Available(next))
                {
                    continue;
                }

                var stepCost = query.CostMultipliers.GetValueOrDefault(next, 1);
                var candidateCost = costs[current] + stepCost;
                if (costs.TryGetValue(next, out var knownCost) && candidateCost >= knownCost)
                {
                    continue;
                }

                costs[next] = candidateCost;
                cameFrom[next] = current;
                frontier.Enqueue(next, (
                    candidateCost + Heuristic(next, goal.Value),
                    candidateCost,
                    next.Z,
                    next.X));
            }
        }

        return Failed(NavigationPathFailure.Unreachable, start, goal, visited);

        NavigationCell? ResolveEndpoint(NavigationCell requested)
        {
            if (Available(requested))
            {
                return requested;
            }

            if (!query.RecoverBlockedEndpoints)
            {
                return null;
            }

            return surface.WalkableCells
                .Where(Available)
                .Select(cell => (Cell: cell, Distance: Heuristic(cell, requested)))
                .Where(candidate => candidate.Distance <= query.RecoveryRadius)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Cell)
                .Select(candidate => (NavigationCell?)candidate.Cell)
                .FirstOrDefault();
        }

        IEnumerable<NavigationCell> Neighbors(NavigationCell current)
        {
            foreach (var direction in Directions)
            {
                yield return new NavigationCell(current.X + direction.X, current.Z + direction.Z);
            }

            if (offMesh.TryGetValue(current, out var linked))
            {
                foreach (var target in linked)
                {
                    yield return target;
                }
            }
        }

        NavigationPathResult Failed(
            NavigationPathFailure failure,
            NavigationCell? resolvedStart,
            NavigationCell? resolvedGoal,
            int visitedCount) => new(
                false,
                [],
                float.PositiveInfinity,
                visitedCount,
                failure,
                query.Start,
                query.Goal,
                resolvedStart,
                resolvedGoal,
                resolvedStart is not null && resolvedStart != query.Start,
                resolvedGoal is not null && resolvedGoal != query.Goal);
    }

    private static int Heuristic(NavigationCell left, NavigationCell right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Z - right.Z);
}
