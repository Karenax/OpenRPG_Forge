namespace Forge.Application.Commands;

public sealed record CommandHistoryJournalEntry(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string CommandName,
    DateTimeOffset Timestamp,
    long BaseRevision,
    long AppliedRevision,
    ProjectPatch Patch,
    ProjectPatch Inverse,
    bool IsApplied)
{
    public const int CurrentSchemaVersion = 1;
}
