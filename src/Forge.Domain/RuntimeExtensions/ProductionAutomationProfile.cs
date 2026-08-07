using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public sealed record ProductionAutomationProfile
{
    public const int CurrentSchemaVersion = 1;

    public sealed record MediaGenerationSettings(Guid Id, bool Enabled, IReadOnlyList<string> MediaKinds,
        IReadOnlyList<string> Providers, bool RequireRightsMetadata, bool RequireHumanReview,
        bool StageBeforeImport, int MaximumJobs, long MaximumBytesPerJob, bool ModuleIndependentFallback);

    public sealed record SoakTestSettings(Guid Id, bool Enabled, int DurationMinutes, int VirtualPlayers,
        int SampleIntervalSeconds, int MaximumMemoryGrowthMegabytes, int MaximumHandleGrowth,
        double MaximumP95LatencyMilliseconds, bool AssertDatabaseHealth);

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public MediaGenerationSettings MediaGeneration { get; }
    public SoakTestSettings SoakTests { get; }
    public string Provenance { get; }

    public ProductionAutomationProfile(Guid id, int schemaVersion, MediaGenerationSettings mediaGeneration,
        SoakTestSettings soakTests, string provenance)
    {
        ArgumentNullException.ThrowIfNull(mediaGeneration);
        ArgumentNullException.ThrowIfNull(soakTests);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || mediaGeneration.Id == Guid.Empty ||
            mediaGeneration.MaximumJobs is < 1 or > 256 || mediaGeneration.MaximumBytesPerJob is < 1_024 or > 4_294_967_296 ||
            mediaGeneration.MediaKinds.Any(kind => kind is not ("image" or "audio" or "vfx")) ||
            mediaGeneration.Providers.Any(string.IsNullOrWhiteSpace) || mediaGeneration.Enabled &&
            (!mediaGeneration.RequireRightsMetadata || !mediaGeneration.RequireHumanReview || !mediaGeneration.StageBeforeImport))
            throw new ArgumentOutOfRangeException(nameof(id), "Media generation must be governed, staged and bounded.");
        if (soakTests.Id == Guid.Empty || soakTests.DurationMinutes is < 1 or > 10_080 ||
            soakTests.VirtualPlayers is < 1 or > 100_000 || soakTests.SampleIntervalSeconds is < 1 or > 3_600 ||
            soakTests.MaximumMemoryGrowthMegabytes is < 0 or > 1_048_576 || soakTests.MaximumHandleGrowth is < 0 or > 10_000_000 ||
            !double.IsFinite(soakTests.MaximumP95LatencyMilliseconds) || soakTests.MaximumP95LatencyMilliseconds is < 1 or > 60_000)
            throw new ArgumentException("Soak tests require bounded duration, workload and health assertions.", nameof(soakTests));
        if (new[] { id, mediaGeneration.Id, soakTests.Id }.Distinct().Count() != 3)
            throw new ArgumentException("Production automation UUIDs must be unique.");

        Id = id;
        SchemaVersion = schemaVersion;
        MediaGeneration = mediaGeneration with
        {
            MediaKinds = mediaGeneration.MediaKinds.Select(Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            Providers = mediaGeneration.Providers.Select(Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        };
        SoakTests = soakTests;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static ProductionAutomationProfile CreateDefault(Guid ownerId)
    {
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(ownerId, $"post-launch:production:{key}");
        return new ProductionAutomationProfile(Stable("root"), 1,
            new MediaGenerationSettings(Stable("media"), false, ["image", "audio", "vfx"], [],
                true, true, true, 8, 256 * 1024 * 1024, true),
            new SoakTestSettings(Stable("soak"), false, 60, 32, 10, 128, 100, 250, true),
            "migration-default");
    }

    public IReadOnlyList<Guid> AllIds() => [Id, MediaGeneration.Id, SoakTests.Id];

    private static string Normalize(string value) => DataDrivenRuleSchema.NormalizeKey(value, nameof(value));
}
