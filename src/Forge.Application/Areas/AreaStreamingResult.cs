namespace Forge.Application.Areas;

public sealed record AreaStreamingResult(IReadOnlyList<AreaChunkPayload> Resources, int LoadedResources,
    int CacheHits, long LoadedBytes, TimeSpan Elapsed, bool TargetMet);
