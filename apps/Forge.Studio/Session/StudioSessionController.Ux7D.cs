using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Quality;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private bool _lastUx7DTemporaryReleased = true;

    internal bool LastUx7DTemporaryReleased => _lastUx7DTemporaryReleased;

    internal StudioUx7DPerformanceSelection? CurrentPerformanceLabUx7D()
    {
        if (Project is null || Session is null) return null;
        try
        {
            var profile = Project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance;
            var inventory = InventoryWorkspaceUx7D(Session.RootDirectory);
            return new StudioUx7DPerformanceSelection(
                Project.Id.Value,
                Project.Module.DisplayName,
                SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project), Session.RootDirectory),
                Project.Areas.Count,
                inventory.ScriptCount,
                profile.Benchmarks.Scenarios.Count,
                profile.PlatformBudgets.Count,
                profile.Profiler.Surfaces.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal StudioUx7DRunReport RunIsolatedPerformanceUx7D(
        StudioUx7DPerformanceSelection selection,
        CancellationToken cancellationToken,
        Action<StudioUx7DPerformanceStage>? stageCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || History is null || Session is null)
            return FailedRunUx7D("Run refusé · aucun module source n’est ouvert.");
        var current = CurrentPerformanceLabUx7D();
        if (current is null || current.ProjectId != selection.ProjectId ||
            !string.Equals(current.SourceFingerprint, selection.SourceFingerprint, StringComparison.Ordinal))
            return FailedRunUx7D("Run refusé · la sélection ou les sources ont changé.");

        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceProjectFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var sourceRoot = Session.RootDirectory;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-d",
            Guid.NewGuid().ToString("N"));
        var stages = new List<StudioUx7DPerformanceStage>();
        var cancelled = false;
        var isolated = false;
        var artifactsConfined = false;
        var error = string.Empty;
        _lastUx7DTemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire n’a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, root))
                throw new IOException("Les fichiers du module n’ont pas pu être copiés dans l’isolat.");
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);
            var project = created.Session.Project;
            var profile = project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance;
            var inventory = InventoryWorkspaceUx7D(root);

            cancellationToken.ThrowIfCancellationRequested();
            var benchmark = PerformanceQualityService.Benchmark(project, profile, inventory.PackageBytes,
                inventory.ScriptCount);
            AddStageUx7D(BenchmarkStageUx7D(benchmark, profile));

            cancellationToken.ThrowIfCancellationRequested();
            AddStageUx7D(BudgetStageUx7D(benchmark));

            cancellationToken.ThrowIfCancellationRequested();
            var profiling = ProfilingStageUx7D(root, project, profile);
            artifactsConfined = profiling.Confined;
            AddStageUx7D(profiling.Stage);

            WriteReportUx7D(root, cancelled, isolated, artifactsConfined, stages);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            TryWriteReportUx7D(root, cancelled, isolated, artifactsConfined, stages);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or JsonException or
                                           NotSupportedException)
        {
            error = exception.Message;
            TryWriteReportUx7D(root, cancelled, isolated, artifactsConfined, stages);
        }
        finally
        {
            _lastUx7DTemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }

        var final = CurrentPerformanceLabUx7D();
        var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                              ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              History?.CanUndo == sourceCanUndo && History?.CanRedo == sourceCanRedo &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected &&
                              FileFingerprintUx7D(Session.ProjectFilePath) == sourceProjectFile &&
                              final?.SourceFingerprint == selection.SourceFingerprint;
        var succeeded = !cancelled && string.IsNullOrEmpty(error) && isolated && artifactsConfined &&
                        stages.Count == 3 && stages.All(stage => stage.Completed) && sourceUntouched &&
                        _lastUx7DTemporaryReleased;
        return new StudioUx7DRunReport(
            succeeded,
            cancelled,
            isolated,
            artifactsConfined,
            sourceUntouched,
            _lastUx7DTemporaryReleased,
            RunFingerprintUx7D(stages),
            stages,
            cancelled
                ? "Run annulé proprement · aucun artefact n’a quitté la copie temporaire."
                : succeeded
                    ? "Benchmarks, budgets et profilage terminés · résultats navigables."
                    : "Run incomplet · " + (string.IsNullOrEmpty(error) ?
                        "consultez les résultats localisés." : error));

        void AddStageUx7D(StudioUx7DPerformanceStage stage)
        {
            stages.Add(stage);
            stageCompleted?.Invoke(stage);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal void CloseUx7D() => _lastUx7DTemporaryReleased = true;

    internal StudioUx7DSmokeResult RunUx7DContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedSmokeUx7D();
        var originalProject = Project;
        var originalHistory = History;
        var originalRevision = History.State.Revision;
        var originalJournal = History.Journal.ToArray();
        var originalDirty = IsDirty;
        var originalArea = ActiveAreaId;
        var originalSelection = SelectedEntityIds.ToArray();
        var originalSelected = SelectedEntityId;
        var originalFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var selection = CurrentPerformanceLabUx7D();
        if (selection is null) return FailedSmokeUx7D();

        var run = RunIsolatedPerformanceUx7D(selection, CancellationToken.None);
        var second = RunIsolatedPerformanceUx7D(selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelled = RunIsolatedPerformanceUx7D(selection, cancellation.Token, stage =>
        {
            if (stage.Key == "benchmarks") cancellation.Cancel();
        });

        var routeIds = run.Stages.Select(stage => stage.Route).ToHashSet(StringComparer.Ordinal);
        var threeRoutes = routeIds.SetEquals(["ORF-QA-017", "ORF-QA-018", "ORF-QA-019"]);
        var benchmarks = run.Stages.SingleOrDefault(stage => stage.Key == "benchmarks");
        var budgets = run.Stages.SingleOrDefault(stage => stage.Key == "budgets");
        var profiling = run.Stages.SingleOrDefault(stage => stage.Key == "profiling");
        var explicitSelection = selection.ProjectId != Guid.Empty && selection.SourceFingerprint.Length == 64 &&
                                selection.AreaCount > 0 && selection.ScenarioCount > 0 &&
                                selection.BudgetCount > 0 && selection.ProfilerSurfaceCount > 0;
        var benchmarkReady = benchmarks is { Completed: true, Cases: > 0 } && benchmarks.Details.Count > 0;
        var budgetReady = budgets is { Completed: true, Cases: > 0 } &&
                          budgets.Cases == selection.ScenarioCount * selection.BudgetCount;
        var profilingReady = profiling is { Completed: true, Cases: > 0, Passed: true } &&
                             profiling.Cases >= selection.ProfilerSurfaceCount;
        var cancellationReady = cancelled.Cancelled && cancelled.Stages.Count == 1 &&
                                cancelled.Stages[0].Key == "benchmarks" &&
                                cancelled.TemporaryWorkspaceReleased;
        var navigation = run.Stages.Count == 3 && run.Stages.All(stage =>
            !string.IsNullOrWhiteSpace(stage.Title) && !string.IsNullOrWhiteSpace(stage.Summary) &&
            stage.Details.Count > 0);
        var deterministic = run.Succeeded && second.Succeeded &&
                            string.Equals(run.Fingerprint, second.Fingerprint, StringComparison.Ordinal);
        var sourceUnchanged = run.SourceUntouched && second.SourceUntouched && cancelled.SourceUntouched &&
                              ReferenceEquals(originalProject, Project) &&
                              ReferenceEquals(originalHistory, History) &&
                              History?.State.Revision == originalRevision &&
                              (History?.Journal ?? []).SequenceEqual(originalJournal) &&
                              IsDirty == originalDirty && ActiveAreaId == originalArea &&
                              SelectedEntityIds.SequenceEqual(originalSelection) &&
                              SelectedEntityId == originalSelected &&
                              FileFingerprintUx7D(Session.ProjectFilePath) == originalFile;
        var released = run.TemporaryWorkspaceReleased && second.TemporaryWorkspaceReleased &&
                       cancelled.TemporaryWorkspaceReleased && _lastUx7DTemporaryReleased;
        var succeeded = run.Succeeded && second.Succeeded && threeRoutes && explicitSelection &&
                        benchmarkReady && budgetReady && profilingReady && cancellationReady && navigation &&
                        deterministic && run.Isolated && run.ArtifactsConfined && sourceUnchanged && released;
        return new StudioUx7DSmokeResult(succeeded, threeRoutes, explicitSelection, benchmarkReady,
            budgetReady, profilingReady, cancellationReady, navigation, deterministic, run.Isolated,
            run.ArtifactsConfined, sourceUnchanged, released);
    }

    private static StudioUx7DPerformanceStage BenchmarkStageUx7D(
        AreaBenchmarkQualityReport report,
        QualityAssuranceProfile profile)
    {
        var expected = profile.Benchmarks.Scenarios.Count * profile.Benchmarks.Iterations;
        var valid = report.Measurements.Count == expected && report.Measurements.All(item =>
            double.IsFinite(item.EstimatedFrameMilliseconds) &&
            double.IsFinite(item.EstimatedMemoryMegabytes) &&
            double.IsFinite(item.NavigationRebuildMilliseconds));
        var details = report.Measurements.GroupBy(item => item.Kind).OrderBy(group => group.Key)
            .Select(group =>
                $"{BenchmarkLabelUx7D(group.Key)} · {group.Count()} itérations · " +
                $"frame estimée {group.Average(item => item.EstimatedFrameMilliseconds):0.00} ms · " +
                $"mémoire {group.Average(item => item.EstimatedMemoryMegabytes):0.0} Mo · " +
                $"navigation mesurée {group.Average(item => item.NavigationRebuildMilliseconds):0.00} ms · " +
                $"entités max {group.Max(item => item.EntityCount)}")
            .ToArray();
        return new StudioUx7DPerformanceStage("ORF-QA-017", "benchmarks", "Benchmarks de zone", true,
            valid, report.Measurements.Count, 0, valid ? 0 : 1,
            $"{report.Measurements.Count}/{expected} mesures intérieur, extérieur et stress produites.",
            details, HashJsonUx7D(new { report.Fingerprint, expected, valid }));
    }

    private static StudioUx7DPerformanceStage BudgetStageUx7D(AreaBenchmarkQualityReport report)
    {
        var warnings = report.Budgets.Count(item => item.Status == PerformanceBudgetStatus.Warning);
        var failures = report.Budgets.Count(item => item.Status == PerformanceBudgetStatus.Failed);
        var details = report.Budgets.OrderBy(item => item.Platform, StringComparer.Ordinal)
            .ThenBy(item => item.Scenario)
            .Select(item =>
                $"{item.Platform} · {BenchmarkLabelUx7D(item.Scenario)} · {BudgetLabelUx7D(item.Status)}" +
                (item.Diagnostics.Count == 0 ? " · aucun seuil approché" :
                    $" · {string.Join(", ", item.Diagnostics)}"))
            .ToArray();
        return new StudioUx7DPerformanceStage("ORF-QA-018", "budgets", "Budgets par plateforme", true,
            failures == 0, report.Budgets.Count, warnings, failures,
            $"{report.Budgets.Count} évaluations · {warnings} avertissements · {failures} blocages configurés.",
            details, HashJsonUx7D(report.Budgets));
    }

    private static (StudioUx7DPerformanceStage Stage, bool Confined) ProfilingStageUx7D(
        string root,
        Forge.Domain.Projects.ProjectDefinition project,
        QualityAssuranceProfile profile)
    {
        var report = new WorkspacePerformanceLabService().Run(root, project,
            new WorkspacePerformanceLabOptions(true));
        var paths = new[] { report.PerformanceReportPath, report.VisualManifestPath, report.TracePath };
        var confined = paths.All(path => ConfinedArtifactExistsUx7D(root, path));
        var surfaces = report.TraceSpans.Select(span => span.Surface).Distinct().Order().ToArray();
        var categories = report.TraceSpans.Select(span => span.Category).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        var requiredSurfaces = profile.Profiler.Surfaces.All(surface => surfaces.Contains(surface));
        var requiredCategories = (!profile.Profiler.IncludeScriptSpans || categories.Contains("script")) &&
                                 (!profile.Profiler.IncludeDatabaseSpans || categories.Contains("database")) &&
                                 (!profile.Profiler.IncludeNetworkSpans || categories.Contains("network"));
        var failed = report.TraceSpans.Count(span => span.Failed);
        var passed = confined && requiredSurfaces && requiredCategories && failed == 0;
        var details = report.TraceSpans.OrderBy(span => span.StartedMicroseconds).ThenBy(span => span.Name,
                StringComparer.Ordinal)
            .Select(span =>
                $"{span.Name} · {SurfaceLabelUx7D(span.Surface)} · {span.Category} · " +
                $"{span.DurationMicroseconds / 1000d:0.000} ms · {(span.Failed ? "échec" : "terminé")}")
            .Append("Export · JSON Chrome/Perfetto confiné · aucune capture Tracy revendiquée")
            .ToArray();
        return (new StudioUx7DPerformanceStage("ORF-QA-019", "profiling", "Profilage intégré", true,
            passed, report.TraceSpans.Count, 0, failed,
            $"{report.TraceSpans.Count} spans · surfaces {string.Join(", ", surfaces.Select(SurfaceLabelUx7D))} · " +
            $"catégories {string.Join(", ", categories)}.", details,
            HashJsonUx7D(new { report.Fingerprint, surfaces, categories, confined, failed })), confined);
    }

    private static bool ConfinedArtifactExistsUx7D(string root, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath);
    }

    private static WorkspaceInventoryUx7D InventoryWorkspaceUx7D(string root)
    {
        var files = EnumerateWorkspaceFilesUx7D(root).ToArray();
        return new WorkspaceInventoryUx7D(files.Sum(path => new FileInfo(path).Length),
            files.Count(path => string.Equals(Path.GetExtension(path), ".forge",
                StringComparison.OrdinalIgnoreCase)), files.Length);
    }

    private static IReadOnlyList<string> EnumerateWorkspaceFilesUx7D(string root)
    {
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                return !relative.StartsWith(".forge/", StringComparison.OrdinalIgnoreCase) &&
                       !relative.StartsWith(".godot/", StringComparison.OrdinalIgnoreCase) &&
                       !relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) &&
                       !relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase);
            })
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool CopyWorkspaceFilesUx7D(string sourceRoot, string destinationRoot)
    {
        try
        {
            foreach (var source in EnumerateWorkspaceFilesUx7D(sourceRoot))
            {
                var relative = Path.GetRelativePath(sourceRoot, source);
                if (string.Equals(relative.Replace('\\', '/'), "project.json",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                var destination = Path.GetFullPath(Path.Combine(destinationRoot, relative));
                var fullRoot = Path.GetFullPath(destinationRoot);
                if (!destination.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string SourceFingerprintUx7D(string projectJson, string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendHashUx7D(hash, "project");
        AppendHashUx7D(hash, projectJson);
        foreach (var path in EnumerateWorkspaceFilesUx7D(root))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/').ToLowerInvariant();
            if (relative == "project.json") continue;
            AppendHashUx7D(hash, relative);
            hash.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendHashUx7D(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }

    private static string FileFingerprintUx7D(string path) => File.Exists(path)
        ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
        : "missing";

    private static string HashJsonUx7D(object value) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        })));

    private static string RunFingerprintUx7D(IReadOnlyList<StudioUx7DPerformanceStage> stages) =>
        HashJsonUx7D(stages.Select(stage => new
        {
            stage.Route,
            stage.Key,
            stage.Completed,
            stage.Passed,
            stage.Cases,
            stage.WarningCases,
            stage.FailedCases,
            stage.Fingerprint,
        }));

    private static void WriteReportUx7D(string root, bool cancelled, bool isolated, bool artifactsConfined,
        IReadOnlyList<StudioUx7DPerformanceStage> stages)
    {
        var directory = Path.Combine(root, ".forge", "quality");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "ux7-d-studio-run.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            cancelled,
            isolated,
            artifactsConfined,
            fingerprint = RunFingerprintUx7D(stages),
            stages,
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static void TryWriteReportUx7D(string root, bool cancelled, bool isolated,
        bool artifactsConfined, IReadOnlyList<StudioUx7DPerformanceStage> stages)
    {
        try
        {
            if (Directory.Exists(root)) WriteReportUx7D(root, cancelled, isolated, artifactsConfined, stages);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            _ = exception;
        }
    }

    private static string BenchmarkLabelUx7D(AreaBenchmarkKind kind) => kind switch
    {
        AreaBenchmarkKind.Interior => "Intérieur",
        AreaBenchmarkKind.Exterior => "Extérieur",
        AreaBenchmarkKind.Stress => "Stress",
        _ => "Scénario inconnu",
    };

    private static string BudgetLabelUx7D(PerformanceBudgetStatus status) => status switch
    {
        PerformanceBudgetStatus.Passed => "conforme",
        PerformanceBudgetStatus.Warning => "avertissement",
        PerformanceBudgetStatus.Failed => "bloqué",
        _ => "inconnu",
    };

    private static string SurfaceLabelUx7D(QualityTraceSurface surface) => surface switch
    {
        QualityTraceSurface.Editor => "éditeur",
        QualityTraceSurface.Runtime => "runtime",
        QualityTraceSurface.Server => "serveur",
        _ => "inconnue",
    };

    private static StudioUx7DRunReport FailedRunUx7D(string message) => new(false, false, false, false,
        false, true, string.Empty, [], message);

    private static StudioUx7DSmokeResult FailedSmokeUx7D() => new(false, false, false, false, false,
        false, false, false, false, false, false, false, false);

    private sealed record WorkspaceInventoryUx7D(long PackageBytes, int ScriptCount, int FileCount);
}
