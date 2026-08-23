using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Quality;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Testing;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const int MaximumScriptsUx7C = 256;
    private bool _lastUx7CTemporaryReleased = true;

    internal bool LastUx7CTemporaryReleased => _lastUx7CTemporaryReleased;

    internal StudioUx7CSuiteSelection? CurrentQualitySuiteUx7C()
    {
        if (Project is null || Session is null) return null;
        try
        {
            var scriptCount = EnumerateScriptsUx7C(Session.RootDirectory).Count;
            var network = Project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance.Network;
            return new StudioUx7CSuiteSelection(
                Project.Id.Value,
                Project.Module.DisplayName,
                SourceFingerprintUx7C(ProjectJsonSerializer.Serialize(Project), Session.RootDirectory),
                scriptCount,
                Project.Dialogues.Count,
                Project.Quests.Count,
                Project.Areas.Count,
                network.LatencyTicks.Count * network.LossPercentages.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal StudioUx7CRunReport RunIsolatedQualityUx7C(
        StudioUx7CSuiteSelection selection,
        CancellationToken cancellationToken,
        Action<StudioUx7CTestStage>? stageCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || History is null || Session is null)
            return FailedRunUx7C("Run refusé · aucun module source n’est ouvert.");
        var currentSelection = CurrentQualitySuiteUx7C();
        if (currentSelection is null || selection.ProjectId != currentSelection.ProjectId ||
            !string.Equals(selection.SourceFingerprint, currentSelection.SourceFingerprint,
                StringComparison.Ordinal))
            return FailedRunUx7C("Run refusé · la sélection ou les sources ont changé.");

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
        var sourceProjectFile = FileFingerprintUx7C(Session.ProjectFilePath);
        var sourceRoot = Session.RootDirectory;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-c",
            Guid.NewGuid().ToString("N"));
        var stages = new List<StudioUx7CTestStage>();
        var cancelled = false;
        var isolated = false;
        var error = string.Empty;
        _lastUx7CTemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire n’a pas pu être créée.");
            if (!CopyScriptsUx7C(sourceRoot, root))
                throw new IOException("Les scripts n’ont pas pu être copiés dans le workspace temporaire.");
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);
            var isolatedProject = created.Session.Project;

            RunFamilyUx7C(() => RunForgeScriptStageUx7C(root, cancellationToken));
            RunFamilyUx7C(() => RunDialogueStageUx7C(isolatedProject, cancellationToken));
            RunFamilyUx7C(() => RunQuestStageUx7C(isolatedProject, cancellationToken));
            RunFamilyUx7C(() => RunEncounterStageUx7C(isolatedProject, cancellationToken));
            RunFamilyUx7C(() => RunNetworkStageUx7C(isolatedProject, cancellationToken));

            WriteReportUx7C(root, cancelled, isolated, stages);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            TryWriteReportUx7C(root, cancelled, isolated, stages);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or JsonException or
                                           NotSupportedException)
        {
            error = exception.Message;
            TryWriteReportUx7C(root, cancelled, isolated, stages);
        }
        finally
        {
            _lastUx7CTemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }

        var finalSelection = CurrentQualitySuiteUx7C();
        var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                              ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              History?.CanUndo == sourceCanUndo && History?.CanRedo == sourceCanRedo &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected &&
                              FileFingerprintUx7C(Session.ProjectFilePath) == sourceProjectFile &&
                              finalSelection?.SourceFingerprint == selection.SourceFingerprint;
        var succeeded = !cancelled && string.IsNullOrEmpty(error) && isolated && stages.Count == 5 &&
                        stages.All(stage => stage.Completed) && sourceUntouched &&
                        _lastUx7CTemporaryReleased;
        var fingerprint = RunFingerprintUx7C(stages);
        return new StudioUx7CRunReport(
            succeeded,
            cancelled,
            isolated,
            sourceUntouched,
            _lastUx7CTemporaryReleased,
            fingerprint,
            stages,
            cancelled
                ? "Run annulé proprement · aucun résultat partiel n’a été appliqué."
                : succeeded
                    ? "Cinq suites isolées terminées · les verdicts du contenu sont consultables."
                    : "Run incomplet · " + (string.IsNullOrEmpty(error) ?
                        "consultez les résultats localisés." : error));

        void RunFamilyUx7C(Func<StudioUx7CTestStage> run)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stage = SafeStageUx7C(run);
            stages.Add(stage);
            stageCompleted?.Invoke(stage);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal void CloseUx7C() => _lastUx7CTemporaryReleased = true;

    internal StudioUx7CSmokeResult RunUx7CContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedSmokeUx7C();
        var originalProject = Project;
        var originalHistory = History;
        var originalRevision = History.State.Revision;
        var originalJournal = History.Journal.ToArray();
        var originalDirty = IsDirty;
        var originalArea = ActiveAreaId;
        var originalSelection = SelectedEntityIds.ToArray();
        var originalSelected = SelectedEntityId;
        var originalFile = FileFingerprintUx7C(Session.ProjectFilePath);
        var selection = CurrentQualitySuiteUx7C();
        if (selection is null) return FailedSmokeUx7C();

        var run = RunIsolatedQualityUx7C(selection, CancellationToken.None);
        var second = RunIsolatedQualityUx7C(selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelled = RunIsolatedQualityUx7C(selection, cancellation.Token, stage =>
        {
            if (stage.Key == "forgescript") cancellation.Cancel();
        });

        var routeIds = run.Stages.Select(stage => stage.Route).ToHashSet(StringComparer.Ordinal);
        var fiveRoutes = routeIds.SetEquals(
            ["ORF-QA-009", "ORF-QA-011", "ORF-QA-012", "ORF-QA-013", "ORF-QA-014"]);
        var script = run.Stages.SingleOrDefault(stage => stage.Key == "forgescript");
        var dialogue = run.Stages.SingleOrDefault(stage => stage.Key == "dialogue");
        var quest = run.Stages.SingleOrDefault(stage => stage.Key == "quest");
        var encounter = run.Stages.SingleOrDefault(stage => stage.Key == "encounter");
        var network = run.Stages.SingleOrDefault(stage => stage.Key == "network");
        var explicitSelection = selection.ProjectId != Guid.Empty &&
                                !string.IsNullOrWhiteSpace(selection.ModuleName) &&
                                selection.SourceFingerprint.Length == 64;
        var scriptReady = script is { Completed: true } && script.Cases == selection.ScriptCount;
        var dialogueReady = dialogue is { Completed: true } && dialogue.Cases == selection.DialogueCount;
        var questReady = quest is { Completed: true } && quest.Cases == selection.QuestCount;
        var encounterReady = encounter is { Completed: true } && encounter.Details.Count > 0;
        var networkReady = network is { Completed: true } &&
                           network.Cases == selection.NetworkScenarioCount && network.Details.Count > 0;
        var cancellationReady = cancelled.Cancelled && cancelled.Stages.Count == 1 &&
                                cancelled.Stages[0].Key == "forgescript" &&
                                cancelled.TemporaryWorkspaceReleased;
        var navigation = run.Stages.Count == 5 && run.Stages.All(stage =>
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
                              FileFingerprintUx7C(Session.ProjectFilePath) == originalFile;
        var released = run.TemporaryWorkspaceReleased && second.TemporaryWorkspaceReleased &&
                       cancelled.TemporaryWorkspaceReleased && _lastUx7CTemporaryReleased;
        var succeeded = run.Succeeded && second.Succeeded && fiveRoutes && explicitSelection &&
                        scriptReady && dialogueReady && questReady && encounterReady && networkReady &&
                        cancellationReady && navigation && deterministic && run.Isolated &&
                        sourceUnchanged && released;
        return new StudioUx7CSmokeResult(succeeded, fiveRoutes, explicitSelection, scriptReady,
            dialogueReady, questReady, encounterReady, networkReady, cancellationReady, navigation,
            deterministic, run.Isolated, sourceUnchanged, released);
    }

    private static StudioUx7CTestStage RunForgeScriptStageUx7C(
        string root,
        CancellationToken cancellationToken)
    {
        var paths = EnumerateScriptsUx7C(root);
        if (paths.Count > MaximumScriptsUx7C)
            return StageUx7C("ORF-QA-009", "forgescript", "Tests ForgeScript", true, false,
                paths.Count, 0,
                $"Quota dépassé · {paths.Count} scripts pour une limite de {MaximumScriptsUx7C}.",
                ["Réduisez le périmètre de test ou scindez la suite ForgeScript."],
                new { paths = paths.Count, maximum = MaximumScriptsUx7C });

        var allowed = new HashSet<string>(
            ["health", "variable", "say", "cast", "spawn", "quest", "unlock", "world.area", "world.query"],
            StringComparer.Ordinal);
        var mock = new ForgeScriptMockWorld(
            new Dictionary<string, object?> { ["player"] = "mock:player", ["area"] = "mock:area" },
            new Dictionary<string, object?> { ["clock"] = "deterministic", ["random"] = "seeded" },
            allowed);
        var service = new ForgeScriptSandboxTestService(new NativeForgeScriptRuntime());
        var results = new List<ForgeScriptSandboxTestResult>();
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            results.Add(service.Run(new ForgeScriptSandboxTestCase(
                relative,
                relative,
                File.ReadAllText(path),
                WorkspaceQualityLabOptions.Default.ScriptQuotas,
                mock,
                new HashSet<string>(StringComparer.Ordinal))));
        }

        var passed = results.Count(result => result.Passed);
        var compiled = results.Count(result => result.Compiled);
        var executed = results.Count(result => result.Status == ForgeScriptExecutionStatus.Completed);
        var coverage = results.Count == 0 ? 1d : results.Average(result => result.EventCoverage);
        var details = results.Count == 0
            ? ["Aucun fichier .forge dans le module · état vide testé sans synthèse."]
            : results.Select(result =>
                $"{result.Id} · {(result.Passed ? "réussi" : "à corriger")} · " +
                $"compilé={result.Compiled.ToString().ToLowerInvariant()} · " +
                $"événements={result.CoveredEventKinds.Count} · diagnostics={result.Diagnostics.Count + result.Failures.Count}")
                .ToArray();
        return StageUx7C("ORF-QA-009", "forgescript", "Tests ForgeScript", true,
            results.All(result => result.Passed), results.Count, passed,
            $"{compiled}/{results.Count} compilés · {executed}/{results.Count} exécutés · couverture {coverage:P0}.",
            details, results.Select(result => new
            {
                result.Id,
                result.Compiled,
                status = result.Status.ToString(),
                result.Passed,
                result.EventCoverage,
                covered = result.CoveredEventKinds,
                failures = result.Failures,
                diagnostics = result.Diagnostics.Select(diagnostic => new
                {
                    diagnostic.Source,
                    diagnostic.Line,
                    diagnostic.Column,
                    diagnostic.Severity,
                    diagnostic.Message,
                }),
            }));
    }

    private static StudioUx7CTestStage RunDialogueStageUx7C(
        Forge.Domain.Projects.ProjectDefinition project,
        CancellationToken cancellationToken)
    {
        var reports = new List<(string Slug, DialogueQualityReport Report)>();
        foreach (var dialogue in project.Dialogues.OrderBy(item => item.Slug, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            reports.Add((dialogue.Slug, NarrativeQualityService.TestDialogue(dialogue)));
        }
        var passed = reports.Count(item => item.Report.Passed);
        var coveredNodes = reports.Sum(item => item.Report.CoveredNodes);
        var totalNodes = reports.Sum(item => item.Report.TotalNodes);
        var coveredChoices = reports.Sum(item => item.Report.CoveredChoices);
        var totalChoices = reports.Sum(item => item.Report.TotalChoices);
        var details = reports.Count == 0
            ? ["Aucun dialogue dans le module · état vide testé sans en créer."]
            : reports.Select(item =>
                $"{item.Slug} · {(item.Report.Passed ? "réussi" : "à corriger")} · " +
                $"nœuds {item.Report.CoveredNodes}/{item.Report.TotalNodes} · " +
                $"choix {item.Report.CoveredChoices}/{item.Report.TotalChoices} · chemins {item.Report.Paths.Count}")
                .ToArray();
        return StageUx7C("ORF-QA-011", "dialogue", "Tests de dialogue", true,
            reports.All(item => item.Report.Passed), reports.Count, passed,
            $"{passed}/{reports.Count} dialogues conformes · nœuds {coveredNodes}/{totalNodes} · choix {coveredChoices}/{totalChoices}.",
            details, reports.Select(item => new
            {
                item.Slug,
                item.Report.Passed,
                item.Report.CoveredNodes,
                item.Report.TotalNodes,
                item.Report.CoveredChoices,
                item.Report.TotalChoices,
                paths = item.Report.Paths.Select(path => new { path.FixtureId, path.Completed, path.StopReason }),
            }));
    }

    private static StudioUx7CTestStage RunQuestStageUx7C(
        Forge.Domain.Projects.ProjectDefinition project,
        CancellationToken cancellationToken)
    {
        var reports = new List<(string Slug, QuestQualityReport Report)>();
        foreach (var quest in project.Quests.OrderBy(item => item.Slug, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            reports.Add((quest.Slug, NarrativeQualityService.TestQuest(quest)));
        }
        var passed = reports.Count(item => item.Report.Passed);
        var coveredStates = reports.Sum(item => item.Report.CoveredStates);
        var totalStates = reports.Sum(item => item.Report.TotalStates);
        var coveredTransitions = reports.Sum(item => item.Report.CoveredTransitions);
        var totalTransitions = reports.Sum(item => item.Report.TotalTransitions);
        var details = reports.Count == 0
            ? ["Aucune quête dans le module · état vide testé sans en créer."]
            : reports.Select(item =>
                $"{item.Slug} · {(item.Report.Passed ? "réussie" : "à corriger")} · " +
                $"états {item.Report.CoveredStates}/{item.Report.TotalStates} · " +
                $"transitions {item.Report.CoveredTransitions}/{item.Report.TotalTransitions} · " +
                $"portées {string.Join(", ", item.Report.CoveredScopes.Order())}")
                .ToArray();
        return StageUx7C("ORF-QA-012", "quest", "Tests de quête", true,
            reports.All(item => item.Report.Passed), reports.Count, passed,
            $"{passed}/{reports.Count} quêtes conformes · états {coveredStates}/{totalStates} · transitions {coveredTransitions}/{totalTransitions}.",
            details, reports.Select(item => new
            {
                item.Slug,
                item.Report.Passed,
                item.Report.CoveredStates,
                item.Report.TotalStates,
                item.Report.CoveredTransitions,
                item.Report.TotalTransitions,
                scopes = item.Report.CoveredScopes.Order().Select(scope => scope.ToString()),
                paths = item.Report.Paths.Select(path => new
                {
                    path.FixtureId,
                    scope = path.Scope.ToString(),
                    path.SaveResumeEqual,
                    path.Completed,
                    path.StopReason,
                }),
            }));
    }

    private static StudioUx7CTestStage RunEncounterStageUx7C(
        Forge.Domain.Projects.ProjectDefinition project,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var report = EncounterQualityService.Simulate(project);
        cancellationToken.ThrowIfCancellationRequested();
        var passedRuns = report.Areas.SelectMany(area => area.Runs)
            .Count(run => run.NavigationPassed && run.CleanupPassed);
        var details = report.Areas.Count == 0
            ? ["Aucune zone contenant une créature placée · état vide testé sans rencontre synthétique."]
            : report.Areas.Select(area =>
                $"{area.AreaSlug} · {(area.Passed ? "réussie" : "à corriger")} · " +
                $"créatures {area.ReachableCreatures}/{area.CreatureCount} atteignables · runs {area.Runs.Count}")
                .ToArray();
        return StageUx7C("ORF-QA-013", "encounter", "Tests de rencontre", true,
            report.Passed, report.TotalRuns, passedRuns,
            $"{report.TotalRuns} simulations déterministes · {report.Areas.Count} zones évaluées · navigation et cleanup vérifiés.",
            details, new
            {
                report.Passed,
                report.TotalRuns,
                areas = report.Areas.Select(area => new
                {
                    area.AreaSlug,
                    area.CreatureCount,
                    area.ReachableCreatures,
                    area.Passed,
                    runs = area.Runs.Select(run => new
                    {
                        run.PartyProfileId,
                        run.Seed,
                        run.SpawnedCount,
                        run.Threat,
                        run.NavigationPassed,
                        run.CleanupPassed,
                    }),
                }),
            });
    }

    private static StudioUx7CTestStage RunNetworkStageUx7C(
        Forge.Domain.Projects.ProjectDefinition project,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profile = project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance.Network;
        var report = AdverseNetworkQualityService.Run(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var converged = report.Scenarios.Count(item => item.Converged);
        var details = report.Scenarios.Select(item =>
                $"Latence {item.LatencyTicks} ticks · perte {item.LossPercentage}% · " +
                $"clients {item.Clients} · {(item.Converged ? "convergence" : "échec de convergence")}")
            .Append($"Reconnexion · {(report.ReconnectPassed ? "réussie" : "à corriger")}")
            .Append($"Contenu incompatible · {(report.ContentMismatchPassed ? "refusé correctement" : "à corriger")}")
            .Append($"Attaque d’autorité · {(report.AuthorityAttackPassed ? "refusée correctement" : "à corriger")}")
            .ToArray();
        return StageUx7C("ORF-QA-014", "network", "Tests réseau", true,
            report.Passed, report.Scenarios.Count, converged,
            $"{converged}/{report.Scenarios.Count} scénarios convergents · reconnexion, mismatch et autorité vérifiés en mémoire.",
            details, new
            {
                report.Passed,
                report.Fingerprint,
                scenarios = report.Scenarios.Select(item => new
                {
                    item.LatencyTicks,
                    item.LossPercentage,
                    item.Clients,
                    item.SentPackets,
                    item.DroppedPackets,
                    item.DuplicatePackets,
                    item.Converged,
                    item.Digest,
                }),
                report.ReconnectPassed,
                report.ContentMismatchPassed,
                report.AuthorityAttackPassed,
                report.Diagnostics,
            });
    }

    private static StudioUx7CTestStage SafeStageUx7C(Func<StudioUx7CTestStage> run)
    {
        try
        {
            return run();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return StageUx7C("studio.quality.stage", "failed", "Suite interrompue", false, false,
                0, 0, "La suite n’a pas pu produire un verdict.",
                [$"Erreur {exception.GetType().Name} · {exception.Message}"],
                new { type = exception.GetType().Name, exception.Message });
        }
    }

    private static StudioUx7CTestStage StageUx7C(
        string route,
        string key,
        string title,
        bool completed,
        bool passed,
        int cases,
        int passedCases,
        string summary,
        IReadOnlyList<string> details,
        object fingerprintSource) => new(
        route,
        key,
        title,
        completed,
        passed,
        cases,
        passedCases,
        summary,
        details,
        HashJsonUx7C(fingerprintSource));

    private static IReadOnlyList<string> EnumerateScriptsUx7C(string root)
    {
        var scripts = Path.Combine(root, "scripts");
        return Directory.Exists(scripts)
            ? Directory.EnumerateFiles(scripts, "*.forge", SearchOption.AllDirectories)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    }

    private static bool CopyScriptsUx7C(string sourceRoot, string destinationRoot)
    {
        try
        {
            foreach (var source in EnumerateScriptsUx7C(sourceRoot))
            {
                var relative = Path.GetRelativePath(sourceRoot, source);
                var destination = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string SourceFingerprintUx7C(string projectJson, string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendHashUx7C(hash, "project");
        AppendHashUx7C(hash, projectJson);
        foreach (var path in EnumerateScriptsUx7C(root))
        {
            AppendHashUx7C(hash, Path.GetRelativePath(root, path).Replace('\\', '/').ToLowerInvariant());
            hash.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendHashUx7C(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }

    private static string FileFingerprintUx7C(string path) => File.Exists(path)
        ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
        : "missing";

    private static string HashJsonUx7C(object value) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        })));

    private static string RunFingerprintUx7C(IReadOnlyList<StudioUx7CTestStage> stages) => HashJsonUx7C(
        stages.Select(stage => new
        {
            stage.Route,
            stage.Key,
            stage.Completed,
            stage.Passed,
            stage.Cases,
            stage.PassedCases,
            stage.Summary,
            stage.Details,
            stage.Fingerprint,
        }));

    private static void WriteReportUx7C(
        string root,
        bool cancelled,
        bool isolated,
        IReadOnlyList<StudioUx7CTestStage> stages)
    {
        var directory = Path.Combine(root, ".forge", "quality");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "ux7-c-run.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            cancelled,
            isolated,
            fingerprint = RunFingerprintUx7C(stages),
            stages,
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }), new UTF8Encoding(false));
    }

    private static void TryWriteReportUx7C(
        string root,
        bool cancelled,
        bool isolated,
        IReadOnlyList<StudioUx7CTestStage> stages)
    {
        try
        {
            if (Directory.Exists(root)) WriteReportUx7C(root, cancelled, isolated, stages);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or JsonException or NotSupportedException)
        {
            // The bounded workspace is still cleaned up; the source workspace is never used as fallback.
        }
    }

    private static StudioUx7CRunReport FailedRunUx7C(string message) => new(
        false, false, false, true, true, string.Empty, [], message);

    private static StudioUx7CSmokeResult FailedSmokeUx7C() => new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false);
}
