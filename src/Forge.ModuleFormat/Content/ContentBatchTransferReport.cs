namespace Forge.ModuleFormat.Content;

public sealed record ContentBatchTransferReport(
    int SchemaVersion,
    ContentBatchOperation Operation,
    bool DryRun,
    bool Valid,
    IReadOnlyList<ContentBatchTransferEntry> Entries,
    IReadOnlyList<string> Diagnostics)
{
    public int Added => Entries.Count(entry => entry.Status == ContentBatchEntryStatus.Added);

    public int Replaced => Entries.Count(entry => entry.Status == ContentBatchEntryStatus.Replaced);

    public int Skipped => Entries.Count(entry => entry.Status == ContentBatchEntryStatus.Skipped);

    public int PreservedBoth => Entries.Count(entry => entry.Status == ContentBatchEntryStatus.PreservedBoth);

    public long TotalBytes => Entries.Sum(entry => entry.Bytes);
}
