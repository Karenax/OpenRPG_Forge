namespace Forge.ModuleFormat.Distribution;

public sealed record ModuleDeltaManifest(
    string FormatVersion,
    string BasePackageSha256,
    string TargetPackageSha256,
    long TargetBytes,
    int ChunkSizeBytes,
    bool AllowResume,
    bool DeduplicateByHash,
    bool AllowRollback,
    int RetainedVersions,
    IReadOnlyList<ModuleDeltaChunk> Chunks);
