namespace Forge.Domain.Navigation;

public sealed record NavigationPathResult(
    bool Succeeded,
    IReadOnlyList<NavigationCell> Cells,
    float TotalCost,
    int VisitedCellCount,
    NavigationPathFailure Failure,
    NavigationCell RequestedStart,
    NavigationCell RequestedGoal,
    NavigationCell? ResolvedStart,
    NavigationCell? ResolvedGoal,
    bool RecoveredStart,
    bool RecoveredGoal)
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion => CurrentSchemaVersion;
}
