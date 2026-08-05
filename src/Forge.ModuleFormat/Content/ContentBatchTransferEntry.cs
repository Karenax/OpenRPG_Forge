namespace Forge.ModuleFormat.Content;

public sealed record ContentBatchTransferEntry(
    string SourcePath,
    string TargetPath,
    string RelativePath,
    string ContentSha256,
    long Bytes,
    string Provenance,
    string LicenseSpdx,
    ContentBatchEntryStatus Status,
    string? CollisionPath = null);
