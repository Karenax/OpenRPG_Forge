namespace Forge.Domain.Navigation;

public sealed record NavigationBakePlan(
    bool RequiresFullBake,
    IReadOnlyList<NavigationCell> ChangedCells,
    int ReusedCellCount);
