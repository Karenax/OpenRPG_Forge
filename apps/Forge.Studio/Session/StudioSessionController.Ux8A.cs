using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Forge.Domain.Build;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private string? _retainedUx8ARoot;
    private bool _lastUx8ATemporaryReleased = true;

    internal bool LastUx8ATemporaryReleased => _lastUx8ATemporaryReleased;

    internal StudioUx8ABuildSelection? CurrentBuildUx8A()
    {
        if (Project is null || Session is null) return null;
        try
        {
            var loadedProfile = WorkspaceBuildProfileStore.Load(Session.RootDirectory);
            var profile = loadedProfile.Profile;
            var profileReady = loadedProfile.Succeeded &&
                               profile.CliOperations.Contains("build", StringComparer.Ordinal) &&
                               profile.StableOrdering && profile.NormalizedTimestamps &&
                               profile.LockedDependencies && profile.ContentHashes &&
                               profile.VerifyReproducibility && profile.IncrementalBuild &&
                               profile.RebuildAffectedOnly;
            var build = WorkspaceBuildGraphBuilder.Build(
                Session.RootDirectory, Project, Session.ProjectFilePath);
            var plan = WorkspaceIncrementalBuildPlanner.Plan(
                WorkspaceBuildStateStore.TryLoad(Session.RootDirectory), build.Graph);
            var graphFingerprint = HashJsonUx7D(build.Graph.Nodes.Select(node => new
            {
                path = node.Path,
                kind = node.Kind.ToString(),
                hash = node.ContentSha256,
                dependencies = node.Dependencies.Select(item => item.Value).Order().ToArray(),
            }).ToArray());
            var cli = FindCliUx8A();
            return new StudioUx8ABuildSelection(
                Project.Id.Value,
                Project.Module.DisplayName,
                SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project), Session.RootDirectory),
                graphFingerprint,
                build.Graph.Nodes.Count,
                build.Graph.Nodes.Sum(node => node.Dependencies.Count),
                build.Materialization.TaskCount,
                build.Materialization.MaximumParallelism,
                build.Materialization.PeakParallelism,
                build.Materialization.OrderFingerprint,
                plan.RebuiltResources.Count,
                plan.ReusedResources.Count,
                profileReady,
                cli is not null,
                "dotnet Forge.Cli.dll build <copie temporaire> <module>.orpmod");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or InvalidDataException or
                                           JsonException or NotSupportedException)
        {
            _ = exception;
            return null;
        }
    }

    internal StudioUx8ABuildRunReport RunBuildUx8A(
        StudioUx8ABuildSelection selection,
        CancellationToken cancellationToken,
        bool retainArtifacts,
        Action? cliStarted = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || History is null || Session is null)
            return FailedBuildRunUx8A("Build refusé · aucun module source n’est ouvert.");
        var current = CurrentBuildUx8A();
        if (current is null || current.ProjectId != selection.ProjectId ||
            !string.Equals(current.SourceFingerprint, selection.SourceFingerprint, StringComparison.Ordinal) ||
            !string.Equals(current.GraphFingerprint, selection.GraphFingerprint, StringComparison.Ordinal))
            return FailedBuildRunUx8A("Build refusé · le module ou son graphe a changé.");
        if (!selection.ProfileReady || !selection.CliReady)
            return FailedBuildRunUx8A("Build refusé · le profil déterministe ou Forge.Cli est indisponible.");

        ReleaseBuildArtifactsUx8A();
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
        var sourceFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var sourceRoot = Session.RootDirectory;
        var runRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux8-a",
            Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(runRoot, "workspace");
        var outputRoot = Path.Combine(runRoot, "output");
        var stages = new List<StudioUx8ABuildStage>();
        var cancelled = false;
        var isolated = false;
        var cliProcess = false;
        var artifactsConfined = false;
        var sourceUntouchedDuringRun = false;
        var eligibleToRetain = false;
        var error = string.Empty;
        _lastUx8ATemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(workspaceRoot, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire du module n’a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, workspaceRoot))
                throw new IOException("Les ressources du module n’ont pas pu être copiées dans l’isolat.");
            isolated = !string.Equals(Path.GetFullPath(workspaceRoot), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(outputRoot);

            var cli = FindCliUx8A() ?? throw new InvalidOperationException(
                "Forge.Cli compilé est introuvable. Compilez Forge.Studio avant le build.");
            var isolatedSession = ProjectSession.Open(workspaceRoot);
            if (!isolatedSession.Succeeded || isolatedSession.Session is null)
                throw new InvalidOperationException("Le workspace temporaire n’est pas relisible.");
            var graph = WorkspaceBuildGraphBuilder.Build(workspaceRoot, isolatedSession.Session.Project,
                isolatedSession.Session.ProjectFilePath);
            var initialPlan = WorkspaceIncrementalBuildPlanner.Plan(
                WorkspaceBuildStateStore.TryLoad(workspaceRoot), graph.Graph);

            var firstPath = Path.Combine(outputRoot, $"{sourceProject.Module.Slug}.orpmod");
            var secondPath = Path.Combine(outputRoot, $"{sourceProject.Module.Slug}.repeat.orpmod");
            cancellationToken.ThrowIfCancellationRequested();
            var first = RunCliBuildUx8A(cli, workspaceRoot, firstPath, cancellationToken, () =>
            {
                cliProcess = true;
                cliStarted?.Invoke();
            });
            cancellationToken.ThrowIfCancellationRequested();
            var second = RunCliBuildUx8A(cli, workspaceRoot, secondPath, cancellationToken, () =>
            {
                cliProcess = true;
            });

            var finalGraph = WorkspaceBuildGraphBuilder.Build(workspaceRoot, isolatedSession.Session.Project,
                isolatedSession.Session.ProjectFilePath);
            var restoredState = WorkspaceBuildStateStore.TryLoad(workspaceRoot);
            var finalPlan = WorkspaceIncrementalBuildPlanner.Plan(restoredState, finalGraph.Graph);
            var statePath = Path.Combine(workspaceRoot,
                WorkspaceBuildStateStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var packageEqual = File.Exists(firstPath) && File.Exists(secondPath) &&
                               File.ReadAllBytes(firstPath).SequenceEqual(File.ReadAllBytes(secondPath));
            var fullRoot = Path.GetFullPath(runRoot).TrimEnd(Path.DirectorySeparatorChar) +
                           Path.DirectorySeparatorChar;
            var requiredArtifacts = new[] { firstPath, secondPath, statePath };
            artifactsConfined = requiredArtifacts.All(File.Exists) && requiredArtifacts.All(path =>
                Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase));

            var commonLog = BuildLogUx8A(first, second);
            var packageReady = first.Succeeded && File.Exists(firstPath) &&
                               first.ContentSha256.Length == 64 && new FileInfo(firstPath).Length > 0;
            stages.Add(new StudioUx8ABuildStage(
                "ORF-SHELL-019", "module-build", "Build du module", true, packageReady,
                first.Rebuilt.Count, first.Reused.Count,
                packageReady
                    ? "Un paquet .orpmod réel a été produit par Forge.Cli dans la copie temporaire."
                    : "Le paquet du module n’a pas été produit.",
                [
                    $"Paquet · {Path.GetFileName(firstPath)} · {new FileInfo(firstPath).Length:N0} octets",
                    $"Contenu · SHA-256 {ShortHashUx8A(first.ContentSha256)}",
                    "Source ouverte · lecture seule; sortie · isolat jetable",
                ], commonLog));

            var optimized = graph.Materialization.TaskCount > 0 &&
                            graph.Materialization.MaximumParallelism ==
                            DeterministicBuildTaskScheduler.DefaultMaximumParallelism &&
                            graph.Materialization.PeakParallelism is >= 1 and <=
                                DeterministicBuildTaskScheduler.DefaultMaximumParallelism &&
                            graph.Materialization.OrderFingerprint.Length == 64 &&
                            graph.Graph.Nodes.Count == selection.ResourceCount;
            stages.Add(new StudioUx8ABuildStage(
                "ORF-EE-004", "build-optimization", "Optimisation Build Module", true, optimized,
                initialPlan.RebuiltResources.Count, initialPlan.ReusedResources.Count,
                $"{graph.Materialization.TaskCount} fichiers matérialisés avec un pic de " +
                $"{graph.Materialization.PeakParallelism}/{graph.Materialization.MaximumParallelism} tâches.",
                [
                    $"Graphe · {graph.Graph.Nodes.Count} ressources · " +
                    $"{graph.Graph.Nodes.Sum(node => node.Dependencies.Count)} dépendances",
                    $"Ordre stable · {ShortHashUx8A(graph.Materialization.OrderFingerprint)}",
                    $"Temps de matérialisation · {graph.Materialization.DurationMilliseconds:0.##} ms",
                ], commonLog));

            var cliReady = first.Succeeded && second.Succeeded && first.Operation == "build" &&
                           second.Operation == "build" && first.ExitCode == 0 && second.ExitCode == 0;
            stages.Add(new StudioUx8ABuildStage(
                "ORF-BUILD-003", "build-cli", "Build CLI", true, cliReady,
                first.Rebuilt.Count + second.Rebuilt.Count, first.Reused.Count + second.Reused.Count,
                "Deux processus enfant dotnet/Forge.Cli ont exécuté la commande build et rendu un JSON structuré.",
                [
                    "Commande · dotnet Forge.Cli.dll build <copie temporaire> <module>.orpmod",
                    $"Premier exit code · {first.ExitCode}",
                    $"Second exit code · {second.ExitCode}",
                    "Journaux · disponibles à la demande dans cette surface",
                ], commonLog));

            var deterministic = first.Reproducible && second.Reproducible && packageEqual &&
                                string.Equals(first.ContentSha256, second.ContentSha256,
                                    StringComparison.OrdinalIgnoreCase);
            stages.Add(new StudioUx8ABuildStage(
                "ORF-BUILD-004", "deterministic-build", "Build déterministe", true, deterministic,
                first.Rebuilt.Count, second.Reused.Count,
                deterministic
                    ? "Les vérifications CLI et la comparaison octet par octet donnent le même paquet."
                    : "Les deux paquets ne sont pas reproductibles.",
                [
                    $"Vérification CLI n°1 · {(first.Reproducible ? "conforme" : "écart")}",
                    $"Vérification CLI n°2 · {(second.Reproducible ? "conforme" : "écart")}",
                    $"Comparaison octets · {(packageEqual ? "identique" : "différente")}",
                    $"Empreinte · {ShortHashUx8A(first.ContentSha256)}",
                ], commonLog));

            var incremental = first.Rebuilt.Count > 0 && second.Rebuilt.Count == 0 &&
                              second.Reused.Count > 0 && restoredState is not null &&
                              finalPlan.RebuiltResources.Count == 0 &&
                              finalPlan.ReusedResources.Count == finalGraph.Graph.Nodes.Count;
            stages.Add(new StudioUx8ABuildStage(
                "ORF-BUILD-005", "incremental-build", "Build incrémental", true, incremental,
                second.Rebuilt.Count, second.Reused.Count,
                $"Premier passage : {first.Rebuilt.Count} reconstruit(s). Second passage : " +
                $"{second.Rebuilt.Count} reconstruit(s), {second.Reused.Count} réutilisé(s).",
                [
                    $"État persistant · {WorkspaceBuildStateStore.RelativePath}",
                    $"Plan final · {finalPlan.RebuiltResources.Count} reconstruit · " +
                    $"{finalPlan.ReusedResources.Count} réutilisé",
                    "Politique · reconstruire uniquement les ressources affectées",
                ], commonLog));

            sourceUntouchedDuringRun = SourceMatchesUx8A(sourceProject, sourceHistory, sourceRevision,
                sourceJournal, sourceCanUndo, sourceCanRedo, sourceDirty, sourceArea, sourceSelection,
                sourceSelected, sourceFile, selection.SourceFingerprint);
            eligibleToRetain = retainArtifacts && stages.Count == 5 && stages.All(stage => stage.Passed) &&
                               isolated && cliProcess && artifactsConfined && sourceUntouchedDuringRun;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or InvalidDataException or
                                           JsonException or NotSupportedException or Win32Exception)
        {
            error = exception.Message;
        }
        finally
        {
            if (eligibleToRetain)
            {
                _retainedUx8ARoot = runRoot;
                _lastUx8ATemporaryReleased = false;
            }
            else
            {
                _lastUx8ATemporaryReleased = DeleteTemporaryBlueprintUx3A(runRoot);
            }
        }

        var sourceUntouched = sourceUntouchedDuringRun || SourceMatchesUx8A(sourceProject, sourceHistory,
            sourceRevision, sourceJournal, sourceCanUndo, sourceCanRedo, sourceDirty, sourceArea,
            sourceSelection, sourceSelected, sourceFile, selection.SourceFingerprint);
        var retained = eligibleToRetain && Directory.Exists(runRoot);
        var released = !retained && _lastUx8ATemporaryReleased;
        var succeeded = !cancelled && string.IsNullOrEmpty(error) && stages.Count == 5 &&
                        stages.All(stage => stage.Completed && stage.Passed) && isolated && cliProcess &&
                        artifactsConfined && sourceUntouched && (retained || released);
        var fingerprint = stages.Count == 0 ? string.Empty : HashJsonUx7D(stages.Select(stage => new
        {
            stage.Route,
            stage.Key,
            stage.Passed,
            stage.Rebuilt,
            stage.Reused,
            details = stage.Details,
        }).ToArray());
        return new StudioUx8ABuildRunReport(
            succeeded, cancelled, isolated, cliProcess, artifactsConfined, sourceUntouched, released,
            retained, retained ? outputRoot : string.Empty, fingerprint, stages,
            cancelled
                ? "Build annulé · le processus CLI et la copie temporaire ont été libérés."
                : succeeded
                    ? "Build réel terminé · paquet déterministe et second passage incrémental vérifiés."
                    : "Build incomplet · " + (string.IsNullOrWhiteSpace(error)
                        ? "consultez le résultat localisé." : error));
    }

    internal bool ReleaseBuildArtifactsUx8A()
    {
        var root = _retainedUx8ARoot;
        _retainedUx8ARoot = null;
        _lastUx8ATemporaryReleased = string.IsNullOrWhiteSpace(root) ||
                                     DeleteTemporaryBlueprintUx3A(root);
        return _lastUx8ATemporaryReleased;
    }

    internal void CloseUx8A() => ReleaseBuildArtifactsUx8A();

    internal StudioUx8ABuildSmokeResult RunUx8AContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedBuildSmokeUx8A();
        var originalProject = Project;
        var originalHistory = History;
        var originalRevision = History.State.Revision;
        var originalJournal = History.Journal.ToArray();
        var originalDirty = IsDirty;
        var originalArea = ActiveAreaId;
        var originalSelection = SelectedEntityIds.ToArray();
        var originalSelected = SelectedEntityId;
        var originalFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var selection = CurrentBuildUx8A();
        if (selection is null) return FailedBuildSmokeUx8A();

        var run = RunBuildUx8A(selection, CancellationToken.None, retainArtifacts: false);
        using var cancellation = new CancellationTokenSource();
        var cancelled = RunBuildUx8A(selection, cancellation.Token, retainArtifacts: false,
            cancellation.Cancel);
        var routes = run.Stages.Select(stage => stage.Route).ToArray();
        var fiveRoutes = routes.SequenceEqual(new[]
        {
            "ORF-SHELL-019", "ORF-EE-004", "ORF-BUILD-003", "ORF-BUILD-004", "ORF-BUILD-005",
        });
        var explicitSelection = selection.ProjectId != Guid.Empty && selection.SourceFingerprint.Length == 64 &&
                                selection.GraphFingerprint.Length == 64 && selection.ResourceCount > 0 &&
                                selection.ProfileReady && selection.CliReady;
        var package = run.Stages.SingleOrDefault(stage => stage.Route == "ORF-SHELL-019")?.Passed == true;
        var optimized = run.Stages.SingleOrDefault(stage => stage.Route == "ORF-EE-004")?.Passed == true;
        var cli = run.Stages.SingleOrDefault(stage => stage.Route == "ORF-BUILD-003")?.Passed == true;
        var deterministic = run.Stages.SingleOrDefault(stage => stage.Route == "ORF-BUILD-004")?.Passed == true;
        var incremental = run.Stages.SingleOrDefault(stage => stage.Route == "ORF-BUILD-005") is
        { Passed: true, Rebuilt: 0, Reused: > 0 };
        var cancellationReady = cancelled.Cancelled && cancelled.CliProcess &&
                                cancelled.TemporaryWorkspaceReleased;
        var navigation = run.Stages.Count == 5 && run.Stages.All(stage =>
            !string.IsNullOrWhiteSpace(stage.Title) && !string.IsNullOrWhiteSpace(stage.Summary) &&
            stage.Details.Count >= 3 && !string.IsNullOrWhiteSpace(stage.Log));
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
                       _lastUx8ATemporaryReleased;
        var succeeded = run.Succeeded && fiveRoutes && explicitSelection && run.CliProcess && package &&
                        optimized && cli && deterministic && incremental && cancellationReady && navigation &&
                        run.Isolated && run.ArtifactsConfined && sourceUnchanged && released;
        return new StudioUx8ABuildSmokeResult(succeeded, fiveRoutes, explicitSelection, run.CliProcess,
            package, optimized, cli, deterministic, incremental, cancellationReady, navigation,
            run.Isolated, run.ArtifactsConfined, sourceUnchanged, released, run.Message);
    }

    private bool SourceMatchesUx8A(
        Forge.Domain.Projects.ProjectDefinition project,
        Forge.Application.Commands.ProjectCommandHistory history,
        Forge.Application.Projects.ProjectRevision revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> journal,
        bool canUndo,
        bool canRedo,
        bool dirty,
        Forge.Domain.Areas.AreaId? area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> selection,
        Forge.Domain.Entities.EntityId? selected,
        string projectFile,
        string sourceFingerprint) =>
        ReferenceEquals(project, Project) && ReferenceEquals(history, History) &&
        History?.State.Revision == revision && (History?.Journal ?? []).SequenceEqual(journal) &&
        History?.CanUndo == canUndo && History?.CanRedo == canRedo && IsDirty == dirty &&
        ActiveAreaId == area && SelectedEntityIds.SequenceEqual(selection) && SelectedEntityId == selected &&
        Session is not null && FileFingerprintUx7D(Session.ProjectFilePath) == projectFile &&
        string.Equals(SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project!), Session.RootDirectory),
            sourceFingerprint, StringComparison.Ordinal);

    private static CliBuildResultUx8A RunCliBuildUx8A(
        string cliPath,
        string workspaceRoot,
        string outputPath,
        CancellationToken cancellationToken,
        Action runtimeStarted)
    {
        var start = new ProcessStartInfo(ResolveDotnetUx8A())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(cliPath)!,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var argument in new[] { cliPath, "build", workspaceRoot, outputPath })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ??
                            throw new InvalidOperationException("Forge.Cli n’a pas pu démarrer.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        runtimeStarted();
        try
        {
            process.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            KillBuildProcessUx8A(process);
            _ = outputTask.GetAwaiter().GetResult();
            _ = errorTask.GetAwaiter().GetResult();
            throw;
        }
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        var json = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(line => line.TrimStart().StartsWith('{'));
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("Forge.Cli n’a pas rendu son résultat JSON.");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var valid = root.TryGetProperty("valid", out var validProperty) && validProperty.GetBoolean();
        var operation = root.TryGetProperty("operation", out var operationProperty)
            ? operationProperty.GetString() ?? string.Empty : string.Empty;
        var contentSha = root.TryGetProperty("ContentSha256", out var hashProperty)
            ? hashProperty.GetString() ?? string.Empty : string.Empty;
        var rebuilt = root.TryGetProperty("rebuilt", out var rebuiltProperty)
            ? rebuiltProperty.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
            : [];
        var reused = root.TryGetProperty("reused", out var reusedProperty)
            ? reusedProperty.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
            : [];
        var reproducible = root.TryGetProperty("reproducible", out var reproducibleProperty) &&
                           reproducibleProperty.GetBoolean();
        return new CliBuildResultUx8A(process.ExitCode, valid && process.ExitCode == 0, operation,
            contentSha, rebuilt, reused, reproducible, output, error);
    }

    private static void KillBuildProcessUx8A(Process process)
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

    private static string? FindCliUx8A()
    {
        var explicitPath = Environment.GetEnvironmentVariable("OPENRPG_FORGE_CLI_DLL");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return Path.GetFullPath(explicitPath);
        var root = FindRepositoryRootUx7E();
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(root, "tools", "Forge.Cli", "bin", configuration,
                "net10.0", "Forge.Cli.dll");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string ResolveDotnetUx8A()
    {
        var explicitPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return !string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)
            ? explicitPath : "dotnet";
    }

    private static string BuildLogUx8A(CliBuildResultUx8A first, CliBuildResultUx8A second) =>
        "COMMANDE 1 · dotnet Forge.Cli.dll build <copie temporaire> <module>.orpmod\n" +
        first.StandardOutput.Trim() +
        (string.IsNullOrWhiteSpace(first.StandardError) ? string.Empty : "\nERREUR 1 · " + first.StandardError.Trim()) +
        "\n\nCOMMANDE 2 · dotnet Forge.Cli.dll build <copie temporaire> <module>.repeat.orpmod\n" +
        second.StandardOutput.Trim() +
        (string.IsNullOrWhiteSpace(second.StandardError) ? string.Empty : "\nERREUR 2 · " + second.StandardError.Trim());

    private static string ShortHashUx8A(string value) => value.Length >= 12 ? value[..12] + "…" : value;

    private static StudioUx8ABuildRunReport FailedBuildRunUx8A(string message) =>
        new(false, false, false, false, false, false, true, false, string.Empty, string.Empty, [], message);

    private static StudioUx8ABuildSmokeResult FailedBuildSmokeUx8A() =>
        new(false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, true, "Build indisponible.");

    private sealed record CliBuildResultUx8A(
        int ExitCode,
        bool Succeeded,
        string Operation,
        string ContentSha256,
        IReadOnlyList<string> Rebuilt,
        IReadOnlyList<string> Reused,
        bool Reproducible,
        string StandardOutput,
        string StandardError);
}
