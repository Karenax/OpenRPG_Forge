using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Quality;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Network.Testing;

namespace Forge.ModuleFormat.Quality;

public sealed record WorkspacePerformanceLabOptions(bool WriteArtifacts)
{
    public static WorkspacePerformanceLabOptions Default { get; } = new(true);
}

public sealed record WorkspacePerformanceLabReport(
    bool Passed,
    string Fingerprint,
    AdverseNetworkQualityReport Network,
    AreaBenchmarkQualityReport Benchmarks,
    VisualReviewPlan VisualReviews,
    IReadOnlyList<QualityTraceSpan> TraceSpans,
    string? PerformanceReportPath,
    string? VisualManifestPath,
    string? TracePath);

public sealed class WorkspacePerformanceLabService
{
    public const string PerformanceReportRelativePath = ".forge/quality/performance-report.json";
    public const string VisualManifestFileName = "visual-review-manifest.json";
    public const string TraceFileName = "quality-trace.json";

    public WorkspacePerformanceLabReport Run(string workspaceRoot, ProjectDefinition project,
        WorkspacePerformanceLabOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(project);
        options ??= WorkspacePerformanceLabOptions.Default;
        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var profile = project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance;
        var profiler = new IntegratedQualityProfiler();
        var inventory = profiler.Capture("workspace.inventory", QualityTraceSurface.Editor,
            "database", () => Inventory(root));
        var visual = profiler.Capture("visual.review.plan", QualityTraceSurface.Editor,
            "render", () => PerformanceQualityService.PlanVisualReviews(profile.Visual));
        var network = profiler.Capture("network.adverse-suite", QualityTraceSurface.Server,
            "network", () => AdverseNetworkQualityService.Run(profile.Network));
        _ = profiler.Capture("forgescript.profile", QualityTraceSurface.Runtime,
            "script", () => inventory.ScriptCount,
            new Dictionary<string, string> { ["scripts"] = inventory.ScriptCount.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        var benchmarks = profiler.Capture("runtime.area-benchmarks", QualityTraceSurface.Runtime,
            "runtime", () => PerformanceQualityService.Benchmark(project, profile, inventory.PackageBytes,
                inventory.ScriptCount));
        var spans = profiler.Snapshot();
        var requiredSurfaces = profile.Profiler.Surfaces.All(surface => spans.Any(span => span.Surface == surface));
        var requiredCategories = (!profile.Profiler.IncludeScriptSpans || spans.Any(span => span.Category == "script")) &&
            (!profile.Profiler.IncludeDatabaseSpans || spans.Any(span => span.Category == "database")) &&
            (!profile.Profiler.IncludeNetworkSpans || spans.Any(span => span.Category == "network"));
        var passed = network.Passed && benchmarks.Passed && visual.Cases.Count > 0 &&
            spans.All(span => !span.Failed) && requiredSurfaces && requiredCategories;
        var fingerprint = Fingerprint(new
        {
            passed,
            networkFingerprint = network.Fingerprint,
            benchmarkFingerprint = benchmarks.Fingerprint,
            visualFingerprint = visual.Fingerprint,
            surfaces = spans.Select(span => span.Surface).Distinct().Order(),
            categories = spans.Select(span => span.Category).Distinct().Order(StringComparer.Ordinal),
            inventory,
        });

        string? reportPath = null;
        string? visualPath = null;
        string? tracePath = null;
        if (options.WriteArtifacts)
        {
            reportPath = WriteJson(root, PerformanceReportRelativePath, new
            {
                schemaVersion = 1,
                passed,
                fingerprint,
                network,
                benchmarks,
                visualReviews = visual,
                traceSummary = new
                {
                    spans = spans.Count,
                    surfaces = spans.Select(span => span.Surface).Distinct().Order(),
                    categories = spans.Select(span => span.Category).Distinct().Order(StringComparer.Ordinal),
                },
            });
            visualPath = WriteJson(root, $"{profile.Visual.ReviewDirectory}/{VisualManifestFileName}", visual);
            tracePath = WriteJson(root, $"{profile.Profiler.OutputDirectory}/{TraceFileName}", new
            {
                schemaVersion = 1,
                displayTimeUnit = "ms",
                traceEvents = spans.Select(span => new
                {
                    name = span.Name,
                    cat = span.Category,
                    ph = "X",
                    ts = span.StartedMicroseconds,
                    dur = span.DurationMicroseconds,
                    pid = 1,
                    tid = span.Surface.ToString().ToLowerInvariant(),
                    args = span.Metadata,
                }),
            });
        }

        return new(passed, fingerprint, network, benchmarks, visual, spans, reportPath, visualPath, tracePath);
    }

    private static WorkspaceInventory Inventory(string root)
    {
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith(".forge/", StringComparison.Ordinal))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return new WorkspaceInventory(files.Sum(path => new FileInfo(path).Length),
            files.Count(path => string.Equals(Path.GetExtension(path), ".forge", StringComparison.OrdinalIgnoreCase)),
            files.Length);
    }

    private static string WriteJson(string root, string relativePath, object value)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var output = Path.GetFullPath(Path.Combine(root, normalized));
        if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Quality artifact path escapes the workspace.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporary = output + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }) + Environment.NewLine, new UTF8Encoding(false));
            File.Move(temporary, output, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return relativePath.Replace('\\', '/');
    }

    private static string Fingerprint(object value) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        })));

    private sealed record WorkspaceInventory(long PackageBytes, int ScriptCount, int FileCount);
}
