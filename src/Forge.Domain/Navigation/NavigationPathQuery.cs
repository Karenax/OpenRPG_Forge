namespace Forge.Domain.Navigation;

public sealed record NavigationPathQuery
{
    public NavigationCell Start { get; }

    public NavigationCell Goal { get; }

    public IReadOnlySet<NavigationCell> DynamicObstacles { get; }

    public IReadOnlyDictionary<NavigationCell, float> CostMultipliers { get; }

    public bool RecoverBlockedEndpoints { get; }

    public int RecoveryRadius { get; }

    public int MaximumVisitedCells { get; }

    public NavigationPathQuery(
        NavigationCell start,
        NavigationCell goal,
        IReadOnlySet<NavigationCell>? dynamicObstacles = null,
        IReadOnlyDictionary<NavigationCell, float>? costMultipliers = null,
        bool recoverBlockedEndpoints = true,
        int recoveryRadius = 4,
        int maximumVisitedCells = 100_000)
    {
        if (recoveryRadius < 0 || maximumVisitedCells <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryRadius));
        }

        var costs = costMultipliers ?? new Dictionary<NavigationCell, float>();
        if (costs.Any(pair => !float.IsFinite(pair.Value) || pair.Value < 1))
        {
            throw new ArgumentException("Navigation cost multipliers must be finite and at least one.", nameof(costMultipliers));
        }

        Start = start;
        Goal = goal;
        DynamicObstacles = dynamicObstacles ?? new HashSet<NavigationCell>();
        CostMultipliers = costs;
        RecoverBlockedEndpoints = recoverBlockedEndpoints;
        RecoveryRadius = recoveryRadius;
        MaximumVisitedCells = maximumVisitedCells;
    }
}
