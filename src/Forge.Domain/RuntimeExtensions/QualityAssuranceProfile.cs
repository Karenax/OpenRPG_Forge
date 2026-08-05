using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public enum AreaBenchmarkKind
{
    Interior,
    Exterior,
    Stress,
}

public enum QualityTraceSurface
{
    Editor,
    Runtime,
    Server,
}

public sealed record NetworkTestSuiteProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public int ClientCount { get; }
    public IReadOnlyList<int> LatencyTicks { get; }
    public IReadOnlyList<int> LossPercentages { get; }
    public bool TestReconnect { get; }
    public bool TestContentMismatch { get; }
    public bool TestAuthorityAttacks { get; }
    public string Provenance { get; }

    public NetworkTestSuiteProfile(Guid id, int schemaVersion, int clientCount,
        IReadOnlyList<int> latencyTicks, IReadOnlyList<int> lossPercentages, bool testReconnect,
        bool testContentMismatch, bool testAuthorityAttacks, string provenance)
    {
        ArgumentNullException.ThrowIfNull(latencyTicks);
        ArgumentNullException.ThrowIfNull(lossPercentages);
        var latencies = latencyTicks.Distinct().Order().ToArray();
        var losses = lossPercentages.Distinct().Order().ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || clientCount is < 2 or > 64 ||
            latencies.Length == 0 || latencies.Any(value => value is < 0 or > 600) ||
            losses.Length == 0 || losses.Any(value => value is < 0 or > 95))
            throw new ArgumentOutOfRangeException(nameof(id), "Network QA requires schema v1 and bounded adverse conditions.");
        Id = id;
        SchemaVersion = schemaVersion;
        ClientCount = clientCount;
        LatencyTicks = latencies;
        LossPercentages = losses;
        TestReconnect = testReconnect;
        TestContentMismatch = testContentMismatch;
        TestAuthorityAttacks = testAuthorityAttacks;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record VisualRegressionProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<string> Resolutions { get; }
    public IReadOnlyList<string> CameraFixtures { get; }
    public IReadOnlyList<RenderQualityTier> QualityTiers { get; }
    public double PixelTolerance { get; }
    public string ReviewDirectory { get; }
    public string Provenance { get; }

    public VisualRegressionProfile(Guid id, int schemaVersion, IReadOnlyList<string> resolutions,
        IReadOnlyList<string> cameraFixtures, IReadOnlyList<RenderQualityTier> qualityTiers,
        double pixelTolerance, string reviewDirectory, string provenance)
    {
        ArgumentNullException.ThrowIfNull(resolutions);
        ArgumentNullException.ThrowIfNull(cameraFixtures);
        ArgumentNullException.ThrowIfNull(qualityTiers);
        var sizes = resolutions.Select(NormalizeResolution).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var cameras = cameraFixtures.Select(item => DataDrivenRuleSchema.NormalizeKey(item, nameof(cameraFixtures)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var tiers = qualityTiers.Distinct().Order().ToArray();
        var output = SafeDirectory(reviewDirectory, nameof(reviewDirectory));
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || sizes.Length == 0 || cameras.Length == 0 ||
            tiers.Length == 0 || !double.IsFinite(pixelTolerance) || pixelTolerance is < 0 or > .25)
            throw new ArgumentOutOfRangeException(nameof(id), "Visual QA requires fixtures, quality tiers and a bounded tolerance.");
        Id = id;
        SchemaVersion = schemaVersion;
        Resolutions = sizes;
        CameraFixtures = cameras;
        QualityTiers = tiers;
        PixelTolerance = pixelTolerance;
        ReviewDirectory = output;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static (int Width, int Height) ParseResolution(string value)
    {
        var normalized = NormalizeResolution(value);
        var parts = normalized.Split('x');
        return (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string NormalizeResolution(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Trim().ToLowerInvariant().Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height) ||
            width is < 640 or > 7680 || height is < 360 or > 4320)
            throw new ArgumentException("Visual resolutions must use WIDTHxHEIGHT within supported bounds.", nameof(value));
        return $"{width}x{height}";
    }

    internal static string SafeDirectory(string value, string parameter)
    {
        var normalized = DataDrivenRuleSchema.RequireText(value, parameter).Replace('\\', '/').Trim('/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException("Quality artifact directories must be safe relative paths.", parameter);
        return normalized;
    }
}

public sealed record AreaBenchmarkSuiteProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<AreaBenchmarkKind> Scenarios { get; }
    public int Iterations { get; }
    public int SampleFrames { get; }
    public string Provenance { get; }

    public AreaBenchmarkSuiteProfile(Guid id, int schemaVersion, IReadOnlyList<AreaBenchmarkKind> scenarios,
        int iterations, int sampleFrames, string provenance)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        var ordered = scenarios.Distinct().Order().ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || ordered.Length == 0 ||
            iterations is < 1 or > 50 || sampleFrames is < 30 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(id), "Area benchmarks require bounded scenarios and samples.");
        Id = id;
        SchemaVersion = schemaVersion;
        Scenarios = ordered;
        Iterations = iterations;
        SampleFrames = sampleFrames;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record PlatformPerformanceBudget
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Platform { get; }
    public double MaximumFrameMilliseconds { get; }
    public int MaximumMemoryMegabytes { get; }
    public int MaximumTextureMegabytes { get; }
    public int MaximumDrawCalls { get; }
    public int MaximumDynamicLights { get; }
    public double MaximumScriptMilliseconds { get; }
    public int MaximumNetworkKilobitsPerSecond { get; }
    public double WarningThreshold { get; }
    public bool GateBuild { get; }
    public string Provenance { get; }

    public PlatformPerformanceBudget(Guid id, int schemaVersion, string platform,
        double maximumFrameMilliseconds, int maximumMemoryMegabytes, int maximumTextureMegabytes,
        int maximumDrawCalls, int maximumDynamicLights, double maximumScriptMilliseconds,
        int maximumNetworkKilobitsPerSecond, double warningThreshold, bool gateBuild, string provenance)
    {
        var key = DataDrivenRuleSchema.NormalizeKey(platform, nameof(platform));
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            !double.IsFinite(maximumFrameMilliseconds) || maximumFrameMilliseconds is < 1 or > 1000 ||
            maximumMemoryMegabytes is < 64 or > 1_048_576 || maximumTextureMegabytes is < 16 or > 1_048_576 ||
            maximumDrawCalls is < 10 or > 1_000_000 || maximumDynamicLights is < 0 or > 4096 ||
            !double.IsFinite(maximumScriptMilliseconds) || maximumScriptMilliseconds is < .01 or > 1000 ||
            maximumNetworkKilobitsPerSecond is < 1 or > 10_000_000 || warningThreshold is < .5 or > 1)
            throw new ArgumentOutOfRangeException(nameof(id), "Platform performance budgets are outside supported bounds.");
        Id = id;
        SchemaVersion = schemaVersion;
        Platform = key;
        MaximumFrameMilliseconds = maximumFrameMilliseconds;
        MaximumMemoryMegabytes = maximumMemoryMegabytes;
        MaximumTextureMegabytes = maximumTextureMegabytes;
        MaximumDrawCalls = maximumDrawCalls;
        MaximumDynamicLights = maximumDynamicLights;
        MaximumScriptMilliseconds = maximumScriptMilliseconds;
        MaximumNetworkKilobitsPerSecond = maximumNetworkKilobitsPerSecond;
        WarningThreshold = warningThreshold;
        GateBuild = gateBuild;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record IntegratedProfilerProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<QualityTraceSurface> Surfaces { get; }
    public int MaximumCaptureSeconds { get; }
    public bool IncludeScriptSpans { get; }
    public bool IncludeDatabaseSpans { get; }
    public bool IncludeNetworkSpans { get; }
    public bool OneClickCapture { get; }
    public string OutputDirectory { get; }
    public string Provenance { get; }

    public IntegratedProfilerProfile(Guid id, int schemaVersion, IReadOnlyList<QualityTraceSurface> surfaces,
        int maximumCaptureSeconds, bool includeScriptSpans, bool includeDatabaseSpans,
        bool includeNetworkSpans, bool oneClickCapture, string outputDirectory, string provenance)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        var ordered = surfaces.Distinct().Order().ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || ordered.Length == 0 ||
            maximumCaptureSeconds is < 1 or > 3600)
            throw new ArgumentOutOfRangeException(nameof(id), "Integrated profiling requires bounded surfaces and duration.");
        Id = id;
        SchemaVersion = schemaVersion;
        Surfaces = ordered;
        MaximumCaptureSeconds = maximumCaptureSeconds;
        IncludeScriptSpans = includeScriptSpans;
        IncludeDatabaseSpans = includeDatabaseSpans;
        IncludeNetworkSpans = includeNetworkSpans;
        OneClickCapture = oneClickCapture;
        OutputDirectory = VisualRegressionProfile.SafeDirectory(outputDirectory, nameof(outputDirectory));
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }
}

public sealed record QualityAssuranceProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public NetworkTestSuiteProfile Network { get; }
    public VisualRegressionProfile Visual { get; }
    public AreaBenchmarkSuiteProfile Benchmarks { get; }
    public IReadOnlyList<PlatformPerformanceBudget> PlatformBudgets { get; }
    public IntegratedProfilerProfile Profiler { get; }
    public AccessibilityQualityProfile Accessibility { get; }
    public SecurityFuzzProfile SecurityFuzz { get; }
    public string Provenance { get; }

    public QualityAssuranceProfile(Guid id, int schemaVersion, NetworkTestSuiteProfile network,
        VisualRegressionProfile visual, AreaBenchmarkSuiteProfile benchmarks,
        IReadOnlyList<PlatformPerformanceBudget> platformBudgets, IntegratedProfilerProfile profiler,
        string provenance, AccessibilityQualityProfile? accessibility = null,
        SecurityFuzzProfile? securityFuzz = null)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(benchmarks);
        ArgumentNullException.ThrowIfNull(platformBudgets);
        ArgumentNullException.ThrowIfNull(profiler);
        var access = accessibility ?? AccessibilityQualityProfile.CreateDefault(id);
        var fuzz = securityFuzz ?? SecurityFuzzProfile.CreateDefault(id);
        var budgets = platformBudgets.OrderBy(item => item.Platform, StringComparer.Ordinal).ToArray();
        var ids = budgets.Select(item => item.Id).Append(id).Append(network.Id).Append(visual.Id)
            .Append(benchmarks.Id).Append(profiler.Id).Append(access.Id).Append(fuzz.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || budgets.Length == 0 ||
            budgets.Select(item => item.Platform).Distinct(StringComparer.Ordinal).Count() != budgets.Length ||
            ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Quality assurance requires schema v1, unique platforms and stable UUIDs.");
        Id = id;
        SchemaVersion = schemaVersion;
        Network = network;
        Visual = visual;
        Benchmarks = benchmarks;
        PlatformBudgets = budgets;
        Profiler = profiler;
        Accessibility = access;
        SecurityFuzz = fuzz;
        Provenance = DataDrivenRuleSchema.RequireText(provenance, nameof(provenance));
    }

    public static QualityAssuranceProfile CreateDefault(Guid ownerId)
    {
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(ownerId, $"quality-assurance:{key}");
        return new QualityAssuranceProfile(Stable("root"), 1,
            new NetworkTestSuiteProfile(Stable("network"), 1, 3, [0, 3, 8], [0, 10, 25], true, true, true,
                "migration-default"),
            new VisualRegressionProfile(Stable("visual"), 1, ["1280x720", "1600x900"],
                ["overview", "three-quarter"], [RenderQualityTier.Medium, RenderQualityTier.High], .02,
                ".forge/quality/visual-reviews", "migration-default"),
            new AreaBenchmarkSuiteProfile(Stable("benchmarks"), 1, Enum.GetValues<AreaBenchmarkKind>(), 3, 600,
                "migration-default"),
            [
                new PlatformPerformanceBudget(Stable("budget-desktop"), 1, "desktop", 16.67, 4096, 2048,
                    12_000, 32, 4, 4096, .8, true, "migration-default"),
                new PlatformPerformanceBudget(Stable("budget-mobile"), 1, "mobile", 33.33, 1536, 768,
                    4_000, 8, 3, 2048, .75, false, "migration-default"),
                new PlatformPerformanceBudget(Stable("budget-server"), 1, "dedicated-server", 50, 2048, 64,
                    100, 0, 8, 16_384, .8, true, "migration-default"),
            ],
            new IntegratedProfilerProfile(Stable("profiler"), 1, Enum.GetValues<QualityTraceSurface>(), 60,
                true, true, true, true, ".forge/quality/traces", "migration-default"),
            "migration-default");
    }
}
