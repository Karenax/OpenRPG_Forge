namespace Forge.ModuleFormat.Distribution;

public sealed record ModuleDeltaBuildResult(
    string DeltaPath,
    ModuleDeltaManifest Manifest,
    int IncludedChunks,
    int ReusedChunks,
    long IncludedBytes);
