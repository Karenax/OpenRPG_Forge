namespace Forge.Domain.Navigation;

public static class NavigationBakePlanner
{
    public static NavigationBakePlan Plan(NavigationSurface? previous, NavigationSurface current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is null || previous.AreaId != current.AreaId ||
            previous.AgentProfileId != current.AgentProfileId ||
            previous.WidthCells != current.WidthCells || previous.DepthCells != current.DepthCells ||
            previous.CellSize != current.CellSize)
        {
            return new NavigationBakePlan(true, current.WalkableCells, 0);
        }

        if (string.Equals(previous.SourceFingerprint, current.SourceFingerprint, StringComparison.Ordinal))
        {
            return new NavigationBakePlan(false, [], current.WalkableCells.Count);
        }

        var before = previous.WalkableCells.ToHashSet();
        var after = current.WalkableCells.ToHashSet();
        var changed = before.SymmetricExcept(after).Order().ToArray();
        return new NavigationBakePlan(
            false,
            changed,
            after.Intersect(before).Count());
    }

    private static IEnumerable<T> SymmetricExcept<T>(this HashSet<T> left, HashSet<T> right)
    {
        var result = new HashSet<T>(left);
        result.SymmetricExceptWith(right);
        return result;
    }
}
