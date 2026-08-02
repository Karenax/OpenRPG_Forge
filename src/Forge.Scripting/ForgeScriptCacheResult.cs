namespace Forge.Scripting;

public sealed record ForgeScriptCacheResult(
    ForgeScriptCompileResult Compilation,
    bool CacheHit,
    string CacheKey,
    ForgeScriptCacheMetadata? Metadata);
