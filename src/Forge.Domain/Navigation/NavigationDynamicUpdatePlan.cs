namespace Forge.Domain.Navigation;

public sealed record NavigationDynamicUpdatePlan(
    IReadOnlyList<NavigationCell> ChangedCells,
    int ReusedCellCount,
    bool RequiresFullSearchReset,
    string SourceFingerprint)
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion => CurrentSchemaVersion;
}
