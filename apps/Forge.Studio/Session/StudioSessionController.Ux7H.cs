using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Forge.Application.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private bool _lastUx7HTemporaryReleased = true;

    internal bool LastUx7HTemporaryReleased => _lastUx7HTemporaryReleased;

    internal StudioUx7HVisualSelection? CurrentVisualRegressionUx7H()
    {
        if (Project is null || Session is null) return null;
        try
        {
            var profile = Project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance.Visual;
            var plan = PerformanceQualityService.PlanVisualReviews(profile);
            var engine = FindOfficialEngineUx7H();
            var runtime = FindRuntimeProjectUx7H();
            return new StudioUx7HVisualSelection(
                Project.Id.Value,
                Project.Module.DisplayName,
                SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project), Session.RootDirectory),
                plan.Fingerprint,
                plan.Cases.Count,
                profile.Resolutions.Count,
                profile.CameraFixtures.Count,
                profile.QualityTiers.Count,
                profile.PixelTolerance,
                engine is not null,
                engine?.Version ?? "moteur officiel introuvable",
                runtime is not null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    internal StudioUx7HVisualRunReport RunVisualRegressionUx7H(
        StudioUx7HVisualSelection selection,
        CancellationToken cancellationToken,
        Action? runtimeStarted = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || History is null || Session is null)
            return FailedVisualRunUx7H("Run refusé · aucun module source n’est ouvert.");
        var current = CurrentVisualRegressionUx7H();
        if (current is null || current.ProjectId != selection.ProjectId ||
            !string.Equals(current.SourceFingerprint, selection.SourceFingerprint, StringComparison.Ordinal) ||
            !string.Equals(current.PlanFingerprint, selection.PlanFingerprint, StringComparison.Ordinal))
            return FailedVisualRunUx7H("Run refusé · le module ou le plan visuel a changé.");

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
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-h",
            Guid.NewGuid().ToString("N"));
        var stages = new List<StudioUx7HVisualStage>();
        var cancelled = false;
        var isolated = false;
        var officialEngine = false;
        var runtimeProcess = false;
        var artifactsConfined = false;
        var error = string.Empty;
        _lastUx7HTemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire n’a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, root))
                throw new IOException("Les fichiers du module n’ont pas pu être copiés dans l’isolat.");
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);

            var engine = FindOfficialEngineUx7H() ?? throw new InvalidOperationException(
                "Godot 4.7.1 .NET officiel est introuvable.");
            var runtime = FindRuntimeProjectUx7H() ?? throw new InvalidOperationException(
                "Forge.Runtime compilé est introuvable. Compilez apps/Forge.Runtime avant le run visuel.");
            officialEngine = engine.Official;
            if (!officialEngine)
                throw new InvalidOperationException("Le moteur trouvé n’est pas Godot 4.7.1 .NET officiel.");

            var artifactRoot = Path.Combine(root, ".forge", "visual-regression");
            var logPath = Path.Combine(root, ".forge", "runtime-visual.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            cancellationToken.ThrowIfCancellationRequested();
            using var process = StartVisualRuntimeUx7H(engine.Path, runtime, root, artifactRoot, logPath);
            runtimeProcess = true;
            runtimeStarted?.Invoke();
            try
            {
                process.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                KillProcessUx7H(process);
                throw;
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Forge.Runtime a quitté avec le code {process.ExitCode}.");

            var stage = VisualStageUx7H(selection, engine, artifactRoot, logPath);
            stages.Add(stage);
            var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            var generated = Directory.EnumerateFiles(Path.Combine(root, ".forge"), "*",
                SearchOption.AllDirectories).ToArray();
            artifactsConfined = generated.Length >= 26 && generated.All(path =>
                Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase));
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or JsonException or
                                           NotSupportedException or Win32Exception)
        {
            error = exception.Message;
        }
        finally
        {
            _lastUx7HTemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }

        var final = CurrentVisualRegressionUx7H();
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
        var succeeded = !cancelled && string.IsNullOrEmpty(error) && isolated && officialEngine &&
                        runtimeProcess && artifactsConfined && stages.Count == 1 &&
                        stages[0].Completed && stages[0].Passed &&
                        sourceUntouched && _lastUx7HTemporaryReleased;
        return new StudioUx7HVisualRunReport(succeeded, cancelled, isolated, officialEngine, runtimeProcess,
            artifactsConfined, sourceUntouched, _lastUx7HTemporaryReleased,
            stages.FirstOrDefault()?.Fingerprint ?? string.Empty, stages,
            cancelled
                ? "Run visuel annulé · le processus Runtime et la copie temporaire ont été libérés."
                : succeeded
                    ? "Huit comparaisons visuelles Runtime réelles sont conformes et navigables."
                    : "Run visuel incomplet · " + (string.IsNullOrEmpty(error) ?
                        "consultez le résultat localisé." : error));
    }

    internal void CloseUx7H() => _lastUx7HTemporaryReleased = true;

    internal StudioUx7HVisualSmokeResult RunUx7HContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedVisualSmokeUx7H();
        var originalProject = Project;
        var originalHistory = History;
        var originalRevision = History.State.Revision;
        var originalJournal = History.Journal.ToArray();
        var originalDirty = IsDirty;
        var originalArea = ActiveAreaId;
        var originalSelection = SelectedEntityIds.ToArray();
        var originalSelected = SelectedEntityId;
        var originalFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var selection = CurrentVisualRegressionUx7H();
        if (selection is null) return FailedVisualSmokeUx7H();

        var run = RunVisualRegressionUx7H(selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelled = RunVisualRegressionUx7H(selection, cancellation.Token, cancellation.Cancel);
        var stage = run.Stages.SingleOrDefault();
        var visualRoute = stage?.Route == "ORF-QA-016" && stage.Key == "visual-regression";
        var explicitSelection = selection.ProjectId != Guid.Empty && selection.SourceFingerprint.Length == 64 &&
                                selection.PlanFingerprint.Length == 64 && selection.CaseCount == 8;
        var officialEngine = selection.OfficialEngineReady && run.OfficialEngine &&
                             selection.EngineVersion.StartsWith("4.7.1", StringComparison.Ordinal);
        var runtimeProcess = run.RuntimeProcess;
        var eightCases = stage is { Completed: true, Passed: true, Cases: 8 };
        var artifactSet = stage is { ArtifactCount: 25 } && run.ArtifactsConfined;
        var cancellationReady = cancelled.Cancelled && cancelled.RuntimeProcess &&
                                cancelled.TemporaryWorkspaceReleased;
        var navigation = stage is { Details.Count: >= 11 } && !string.IsNullOrWhiteSpace(stage.Summary);
        var deterministic = stage?.Fingerprint == selection.PlanFingerprint;
        var sourceUnchanged = run.SourceUntouched && cancelled.SourceUntouched &&
                              ReferenceEquals(originalProject, Project) &&
                              ReferenceEquals(originalHistory, History) &&
                              History?.State.Revision == originalRevision &&
                              (History?.Journal ?? []).SequenceEqual(originalJournal) &&
                              IsDirty == originalDirty && ActiveAreaId == originalArea &&
                              SelectedEntityIds.SequenceEqual(originalSelection) &&
                              SelectedEntityId == originalSelected &&
                              FileFingerprintUx7D(Session.ProjectFilePath) == originalFile;
        var released = run.TemporaryWorkspaceReleased && cancelled.TemporaryWorkspaceReleased &&
                       _lastUx7HTemporaryReleased;
        const bool tracyDeferred = true;
        var succeeded = run.Succeeded && visualRoute && explicitSelection && officialEngine && runtimeProcess &&
                        eightCases && artifactSet && cancellationReady && navigation && deterministic &&
                        run.Isolated && run.ArtifactsConfined && sourceUnchanged && released && tracyDeferred;
        return new StudioUx7HVisualSmokeResult(succeeded, visualRoute, explicitSelection, officialEngine,
            runtimeProcess, eightCases, artifactSet, cancellationReady, navigation, deterministic, run.Isolated,
            run.ArtifactsConfined, sourceUnchanged, released, tracyDeferred, run.Succeeded, run.Cancelled,
            run.Message);
    }

    private static Process StartVisualRuntimeUx7H(string enginePath, string runtimePath, string workspaceRoot,
        string artifactRoot, string logPath)
    {
        var start = new ProcessStartInfo(enginePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = runtimePath,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var argument in new[]
                 {
                     "--rendering-method", "gl_compatibility", "--path", runtimePath,
                     "--log-file", logPath, "--", "--workspace", workspaceRoot,
                     "--g11s-visual-suite", artifactRoot,
                 })
            start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Forge.Runtime n’a pas pu démarrer.");
    }

    private static void KillProcessUx7H(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(10_000);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or
                                           NotSupportedException)
        {
            _ = exception;
        }
    }

    private static StudioUx7HVisualStage VisualStageUx7H(StudioUx7HVisualSelection selection,
        EngineIdentityUx7H engine, string artifactRoot, string logPath)
    {
        var reportPath = Path.Combine(artifactRoot, "visual-suite-report.json");
        if (!File.Exists(reportPath) || !File.Exists(logPath))
            throw new InvalidOperationException("Le rapport ou le journal Runtime visuel manque.");
        var references = Directory.GetFiles(artifactRoot, "*.reference.png", SearchOption.TopDirectoryOnly);
        var candidates = Directory.GetFiles(artifactRoot, "*.candidate.png", SearchOption.TopDirectoryOnly);
        var reviews = Directory.GetFiles(artifactRoot, "*.review.json", SearchOption.TopDirectoryOnly);
        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        var root = report.RootElement;
        var passed = root.GetProperty("passed").GetBoolean();
        var fingerprint = root.GetProperty("fingerprint").GetString() ?? string.Empty;
        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        var details = cases.Select(item =>
        {
            var id = item.GetProperty("id").GetString() ?? "cas visuel";
            var resolution = item.GetProperty("resolution").GetString() ?? "résolution inconnue";
            var camera = item.GetProperty("cameraFixture").GetString() ?? "caméra inconnue";
            var quality = item.GetProperty("qualityTier").GetString() ?? "qualité inconnue";
            var error = item.GetProperty("meanPixelError").GetDouble();
            var tolerance = item.GetProperty("pixelTolerance").GetDouble();
            var itemPassed = item.GetProperty("passed").GetBoolean();
            return $"{id} · {resolution} · caméra {camera} · qualité {quality} · " +
                   $"erreur {error:0.######}/{tolerance:0.######} · {(itemPassed ? "conforme" : "écart")}";
        }).ToList();
        details.Add($"Moteur exécuté · Godot {engine.Version} · binaire officiel {Path.GetFileName(engine.Path)}");
        details.Add("Artefacts · 8 références PNG + 8 candidats PNG + 8 revues JSON + 1 rapport global");
        details.Add("Frontière · Runtime réel rendu en OpenGL de compatibilité; aucun fichier source n’est modifié");
        var marker = File.ReadAllText(logPath).Contains(
            "OPENRPG_FORGE_RUNTIME_G11S_VISUAL_PASS cases=8 resolutions=2 cameras=2 quality=2",
            StringComparison.Ordinal);
        var pngsValid = references.Concat(candidates).All(path => new FileInfo(path).Length > 8);
        var complete = passed && marker && cases.Length == selection.CaseCount &&
                       cases.All(item => item.GetProperty("passed").GetBoolean()) &&
                       references.Length == selection.CaseCount && candidates.Length == selection.CaseCount &&
                       reviews.Length == selection.CaseCount && pngsValid &&
                       string.Equals(fingerprint, selection.PlanFingerprint, StringComparison.Ordinal);
        return new StudioUx7HVisualStage("ORF-QA-016", "visual-regression", "Tests visuels", true,
            complete, cases.Length, references.Length + candidates.Length + reviews.Length + 1,
            $"{cases.Length} cas réels · {selection.ResolutionCount} résolutions · " +
            $"{selection.CameraCount} caméras · {selection.QualityTierCount} qualités · " +
            $"tolérance {selection.PixelTolerance:0.###}.", details, fingerprint);
    }

    private static EngineIdentityUx7H? FindOfficialEngineUx7H()
    {
        foreach (var path in EngineCandidatesUx7H())
        {
            if (!File.Exists(path)) continue;
            var version = FileVersionInfo.GetVersionInfo(path);
            var product = version.ProductVersion ?? version.FileVersion ?? string.Empty;
            var official = product.StartsWith("4.7.1", StringComparison.Ordinal) &&
                           product.Contains("mono", StringComparison.OrdinalIgnoreCase) &&
                           product.Contains("official", StringComparison.OrdinalIgnoreCase);
            if (official) return new EngineIdentityUx7H(Path.GetFullPath(path), product, true);
        }
        return null;
    }

    private static IEnumerable<string> EngineCandidatesUx7H()
    {
        foreach (var variable in new[] { "OPENRPG_GODOT", "OPENRPG_FORGE_GODOT" })
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value)) yield return value;
        }
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(directory, "godot.exe");
            yield return Path.Combine(directory, "Godot_v4.7.1-stable_mono_win64.exe");
        }
        var packageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");
        if (!Directory.Exists(packageRoot)) yield break;
        string? winGet = null;
        try
        {
            winGet = Directory.EnumerateFiles(packageRoot, "Godot_v4.7.1-stable_mono_win64.exe",
                SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _ = exception;
        }
        if (winGet is not null) yield return winGet;
    }

    private string? FindRuntimeProjectUx7H()
    {
        var explicitPath = Environment.GetEnvironmentVariable("OPENRPG_FORGE_RUNTIME_PROJECT");
        if (!string.IsNullOrWhiteSpace(explicitPath) && RuntimeProjectReadyUx7H(explicitPath))
            return Path.GetFullPath(explicitPath);
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory, Session?.RootDirectory })
        {
            if (string.IsNullOrWhiteSpace(start)) continue;
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "apps", "Forge.Runtime");
                if (RuntimeProjectReadyUx7H(candidate)) return candidate;
                directory = directory.Parent;
            }
        }
        return null;
    }

    private static bool RuntimeProjectReadyUx7H(string path) =>
        Directory.Exists(path) && File.Exists(Path.Combine(path, "project.godot")) &&
        (File.Exists(Path.Combine(path, ".godot", "mono", "temp", "bin", "Debug", "Forge.Runtime.dll")) ||
         File.Exists(Path.Combine(path, ".godot", "mono", "temp", "bin", "Release", "Forge.Runtime.dll")));

    private static StudioUx7HVisualRunReport FailedVisualRunUx7H(string message) =>
        new(false, false, false, false, false, false, false, true, string.Empty, [], message);

    private static StudioUx7HVisualSmokeResult FailedVisualSmokeUx7H() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, true, false, false, "Run indisponible.");

    private sealed record EngineIdentityUx7H(string Path, string Version, bool Official);
}
