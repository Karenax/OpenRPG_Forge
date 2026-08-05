namespace Forge.Domain.Content;

public sealed record DataTableMergeResult(
    DataTableDefinition Merged,
    IReadOnlyList<DataTableMergeConflict> Conflicts);
