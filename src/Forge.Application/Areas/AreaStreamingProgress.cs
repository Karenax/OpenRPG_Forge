namespace Forge.Application.Areas;

public sealed record AreaStreamingProgress(int CompletedResources, int TotalResources, string CurrentResource,
    int CacheHits, long LoadedBytes)
{
    public float Fraction => TotalResources == 0 ? 1 : CompletedResources / (float)TotalResources;
}
