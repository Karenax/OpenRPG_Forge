namespace Forge.Domain.Content;

public sealed record DataTableMergeConflict(
    Guid ConflictId,
    DataRowId RowId,
    DataRowDefinition? Base,
    DataRowDefinition? Ours,
    DataRowDefinition? Theirs,
    string Summary);
