namespace Forge.Domain.Areas;

public sealed record AreaLoadingProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool AsyncStreaming { get; }
    public bool PrefetchDependencies { get; }
    public bool CancellationEnabled { get; }
    public int CacheBudgetMegabytes { get; }
    public int MaximumConcurrentLoads { get; }
    public int ChunkRadius { get; }
    public int TargetLoadMilliseconds { get; }
    public IReadOnlyList<string> RequiredResources { get; }
    public string Provenance { get; }

    public AreaLoadingProfile(Guid id, int schemaVersion, bool asyncStreaming, bool prefetchDependencies,
        bool cancellationEnabled, int cacheBudgetMegabytes, int maximumConcurrentLoads, int chunkRadius,
        int targetLoadMilliseconds, IReadOnlyList<string> requiredResources, string provenance)
    {
        ArgumentNullException.ThrowIfNull(requiredResources);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || cacheBudgetMegabytes is < 16 or > 65536 ||
            maximumConcurrentLoads is < 1 or > 32 || chunkRadius is < 0 or > 32 || targetLoadMilliseconds is < 1 or > 600000)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Area loading profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        AsyncStreaming = asyncStreaming;
        PrefetchDependencies = prefetchDependencies;
        CancellationEnabled = cancellationEnabled;
        CacheBudgetMegabytes = cacheBudgetMegabytes;
        MaximumConcurrentLoads = maximumConcurrentLoads;
        ChunkRadius = chunkRadius;
        TargetLoadMilliseconds = targetLoadMilliseconds;
        RequiredResources = requiredResources.Select(WeatherStateProfile.NormalizeOptionalResource)
            .Where(resource => resource.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Provenance = provenance.Trim();
    }
}
