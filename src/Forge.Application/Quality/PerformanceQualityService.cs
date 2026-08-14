using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.Quality;

public enum PerformanceBudgetStatus
{
    Passed,
    Warning,
    Failed,
}

public sealed record AreaBenchmarkMeasurement(
    AreaBenchmarkKind Kind,
    AreaId AreaId,
    string AreaSlug,
    int Iteration,
    int EntityCount,
    int UniqueAssets,
    int EstimatedDrawCalls,
    int EstimatedDynamicLights,
    double EstimatedFrameMilliseconds,
    double EstimatedMemoryMegabytes,
    double EstimatedTextureMegabytes,
    double EstimatedScriptMilliseconds,
    double EstimatedNetworkKilobitsPerSecond,
    double NavigationRebuildMilliseconds,
    long PackageBytes);

public sealed record PerformanceBudgetEvaluation(
    string Platform,
    AreaBenchmarkKind Scenario,
    PerformanceBudgetStatus Status,
    IReadOnlyList<string> Diagnostics);

public sealed record AreaBenchmarkQualityReport(
    bool Passed,
    string Fingerprint,
    IReadOnlyList<AreaBenchmarkMeasurement> Measurements,
    IReadOnlyList<PerformanceBudgetEvaluation> Budgets);

public sealed record VisualReviewCase(
    string Id,
    string Resolution,
    string CameraFixture,
    RenderQualityTier QualityTier,
    double PixelTolerance,
    string ReferencePath,
    string CandidatePath,
    string ReviewPath);

public sealed record VisualReviewPlan(
    int SchemaVersion,
    string Fingerprint,
    string ReviewDirectory,
    IReadOnlyList<VisualReviewCase> Cases);

public static class PerformanceQualityService
{
    public static AreaBenchmarkQualityReport Benchmark(
        ProjectDefinition project,
        QualityAssuranceProfile profile,
        long packageBytes,
        int scriptCount)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profile);
        if (packageBytes < 0 || scriptCount < 0) throw new ArgumentOutOfRangeException(nameof(packageBytes));
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);
        var measurements = new List<AreaBenchmarkMeasurement>();
        foreach (var kind in profile.Benchmarks.Scenarios)
        {
            var area = SelectRepresentativeArea(project, kind);
            var baseDraw = area.Entities.Sum(entity => blueprints.GetValueOrDefault(entity.BlueprintId)?.Kind switch
            {
                BlueprintKind.Creature => 4,
                BlueprintKind.Placeable or BlueprintKind.Door => 2,
                _ => 1,
            });
            var uniqueAssets = area.Entities.Select(entity => entity.BlueprintId).Distinct().Count();
            var scale = kind switch { AreaBenchmarkKind.Interior => 1, AreaBenchmarkKind.Exterior => 2, _ => 30 };
            for (var iteration = 0; iteration < profile.Benchmarks.Iterations; iteration++)
            {
                var started = Stopwatch.GetTimestamp();
                var agent = area.Navigation.AgentProfiles[0];
                _ = NavigationSurfaceBuilder.Build(project, area, agent, NavigationDoorState.AllOpen);
                var navMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var entityCount = area.Entities.Count * scale;
                var drawCalls = Math.Max(1, baseDraw * scale);
                var lights = kind == AreaBenchmarkKind.Interior ? 8 : kind == AreaBenchmarkKind.Exterior ? 16 : 32;
                var frame = 2.5 + drawCalls / 650d + lights / 40d;
                var memory = 72 + entityCount * .11 + uniqueAssets * scale * .35;
                var textures = Math.Max(16, uniqueAssets * scale * 2.5);
                var script = .15 + scriptCount * scale * .02;
                var network = Math.Max(1, entityCount * .45);
                measurements.Add(new(kind, area.Id, area.Slug, iteration, entityCount, uniqueAssets * scale,
                    drawCalls, lights, frame, memory, textures, script, network, navMilliseconds, packageBytes));
            }
        }

        var evaluations = profile.PlatformBudgets.SelectMany(budget => profile.Benchmarks.Scenarios.Select(kind =>
            Evaluate(budget, kind, measurements.Where(item => item.Kind == kind).ToArray()))).ToArray();
        var passed = evaluations.All(item => item.Status != PerformanceBudgetStatus.Failed);
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            passed,
            measurements = measurements.Select(item => new
            {
                item.Kind,
                item.AreaId,
                item.EntityCount,
                item.UniqueAssets,
                item.EstimatedDrawCalls,
                item.EstimatedDynamicLights,
                item.EstimatedFrameMilliseconds,
                item.EstimatedMemoryMegabytes,
                item.EstimatedTextureMegabytes,
                item.EstimatedScriptMilliseconds,
                item.EstimatedNetworkKilobitsPerSecond,
                item.PackageBytes,
            }),
            evaluations,
        })));
        return new(passed, fingerprint, measurements, evaluations);
    }

    private static AreaDefinition SelectRepresentativeArea(ProjectDefinition project, AreaBenchmarkKind kind)
    {
        var entry = project.Areas.Single(candidate => candidate.Id == project.Module.EntryAreaId);
        if (kind == AreaBenchmarkKind.Interior)
            return entry.Kind == AreaKind.Interior
                ? entry
                : project.Areas.Where(area => area.Kind == AreaKind.Interior)
                      .OrderByDescending(area => area.Entities.Count)
                      .ThenBy(area => area.Slug, StringComparer.Ordinal)
                      .FirstOrDefault() ?? entry;

        return project.Areas.Where(area => area.Kind == AreaKind.Exterior)
                   .OrderByDescending(area => (long)area.Dimensions.Width * area.Dimensions.Depth)
                   .ThenByDescending(area => area.Entities.Count)
                   .ThenBy(area => area.Slug, StringComparer.Ordinal)
                   .FirstOrDefault() ?? entry;
    }

    public static VisualReviewPlan PlanVisualReviews(VisualRegressionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var cases = (from resolution in profile.Resolutions
                     from camera in profile.CameraFixtures
                     from quality in profile.QualityTiers
                     let id = $"{camera}-{resolution}-{quality.ToString().ToLowerInvariant()}"
                     select new VisualReviewCase(id, resolution, camera, quality, profile.PixelTolerance,
                         $"{profile.ReviewDirectory}/{id}.reference.png",
                         $"{profile.ReviewDirectory}/{id}.candidate.png",
                         $"{profile.ReviewDirectory}/{id}.review.json"))
            .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(cases)));
        return new(1, fingerprint, profile.ReviewDirectory, cases);
    }

    private static PerformanceBudgetEvaluation Evaluate(
        PlatformPerformanceBudget budget,
        AreaBenchmarkKind scenario,
        IReadOnlyList<AreaBenchmarkMeasurement> measurements)
    {
        var diagnosticRatios = new List<(string Code, double Ratio)>();
        var server = string.Equals(budget.Platform, "dedicated-server", StringComparison.Ordinal);
        foreach (var item in measurements)
        {
            diagnosticRatios.Add(("frame", item.EstimatedFrameMilliseconds / budget.MaximumFrameMilliseconds));
            diagnosticRatios.Add(("memory", item.EstimatedMemoryMegabytes / budget.MaximumMemoryMegabytes));
            diagnosticRatios.Add(("script", item.EstimatedScriptMilliseconds / budget.MaximumScriptMilliseconds));
            diagnosticRatios.Add(("network", item.EstimatedNetworkKilobitsPerSecond / budget.MaximumNetworkKilobitsPerSecond));
            if (!server)
            {
                diagnosticRatios.Add(("texture", item.EstimatedTextureMegabytes / budget.MaximumTextureMegabytes));
                diagnosticRatios.Add(("draw", item.EstimatedDrawCalls / (double)budget.MaximumDrawCalls));
                diagnosticRatios.Add(("lights", budget.MaximumDynamicLights == 0 ? double.PositiveInfinity :
                    item.EstimatedDynamicLights / (double)budget.MaximumDynamicLights));
            }
        }

        var diagnostics = diagnosticRatios.Where(item => item.Ratio >= budget.WarningThreshold)
            .GroupBy(item => item.Code, StringComparer.Ordinal)
            .Select(group => $"{group.Key}:{group.Max(item => item.Ratio):P0}")
            .Order(StringComparer.Ordinal).ToArray();
        var over = diagnosticRatios.Any(item => item.Ratio > 1);
        var warning = diagnosticRatios.Any(item => item.Ratio >= budget.WarningThreshold);
        var status = over && budget.GateBuild ? PerformanceBudgetStatus.Failed :
            warning ? PerformanceBudgetStatus.Warning : PerformanceBudgetStatus.Passed;
        return new(budget.Platform, scenario, status, diagnostics);
    }
}

public sealed record QualityTraceSpan(
    Guid Id,
    Guid? ParentId,
    string Name,
    QualityTraceSurface Surface,
    string Category,
    long StartedMicroseconds,
    long DurationMicroseconds,
    bool Failed,
    IReadOnlyDictionary<string, string> Metadata);

public sealed class IntegratedQualityProfiler
{
    private readonly long _origin = Stopwatch.GetTimestamp();
    private readonly List<QualityTraceSpan> _spans = [];

    public T Capture<T>(string name, QualityTraceSurface surface, string category, Func<T> operation,
        IReadOnlyDictionary<string, string>? metadata = null, Guid? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(operation);
        var started = Stopwatch.GetTimestamp();
        var failed = false;
        try { return operation(); }
        catch { failed = true; throw; }
        finally
        {
            var id = DeterministicId(name, surface, category, _spans.Count);
            _spans.Add(new(id, parentId, name, surface, category,
                ToMicroseconds(started - _origin), ToMicroseconds(Stopwatch.GetTimestamp() - started), failed,
                (metadata ?? new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal)
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)));
        }
    }

    public IReadOnlyList<QualityTraceSpan> Snapshot() => _spans.OrderBy(item => item.StartedMicroseconds)
        .ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();

    private static long ToMicroseconds(long timestamp) => (long)(timestamp * 1_000_000d / Stopwatch.Frequency);
    private static Guid DeterministicId(string name, QualityTraceSurface surface, string category, int sequence)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"openrpg:quality-trace:{sequence}:{surface}:{category}:{name}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
