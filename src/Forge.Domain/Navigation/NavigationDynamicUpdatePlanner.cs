namespace Forge.Domain.Navigation;

public static class NavigationDynamicUpdatePlanner
{
    public static NavigationDynamicUpdatePlan Plan(
        NavigationSurface surface,
        IReadOnlySet<NavigationCell> previousObstacles,
        IReadOnlySet<NavigationCell> nextObstacles,
        IReadOnlyDictionary<NavigationCell, float>? previousCosts = null,
        IReadOnlyDictionary<NavigationCell, float>? nextCosts = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(previousObstacles);
        ArgumentNullException.ThrowIfNull(nextObstacles);
        previousCosts ??= new Dictionary<NavigationCell, float>();
        nextCosts ??= new Dictionary<NavigationCell, float>();
        var changed = SymmetricDifference(previousObstacles, nextObstacles)
            .Concat(previousCosts.Keys.Concat(nextCosts.Keys).Distinct().Where(cell =>
                previousCosts.GetValueOrDefault(cell, 1) != nextCosts.GetValueOrDefault(cell, 1)))
            .Where(cell => cell.X >= 0 && cell.X < surface.WidthCells && cell.Z >= 0 && cell.Z < surface.DepthCells)
            .Distinct()
            .Order()
            .ToArray();
        return new NavigationDynamicUpdatePlan(
            changed,
            Math.Max(0, surface.WalkableCells.Count - changed.Count(cell => surface.IsWalkable(cell))),
            changed.Length > Math.Max(256, surface.WalkableCells.Count / 3),
            surface.SourceFingerprint);
    }

    private static IEnumerable<NavigationCell> SymmetricDifference(
        IReadOnlySet<NavigationCell> left,
        IReadOnlySet<NavigationCell> right) => left.Where(cell => !right.Contains(cell))
            .Concat(right.Where(cell => !left.Contains(cell)));
}
