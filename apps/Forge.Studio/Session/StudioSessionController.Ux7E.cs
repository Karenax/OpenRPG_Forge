using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Quality;
using Forge.Application.Validation;
using Forge.Domain.Authoring;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Testing;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private bool _lastUx7ETemporaryReleased = true;

    internal bool LastUx7ETemporaryReleased => _lastUx7ETemporaryReleased;

    internal StudioUx7EGovernanceSelection? CurrentGovernanceLabUx7E()
    {
        if (Project is null || Session is null) return null;
        try
        {
            var repositoryRoot = FindRepositoryRootUx7E();
            var profile = Project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance;
            var editor = EditorWorkspaceProfileStore.TryLoad(Session.RootDirectory) ??
                         EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, Project.Module.EntryAreaId);
            var accessibility = AccessibilityGovernanceService.Evaluate(editor, Project);
            var source = HashJsonUx7D(new
            {
                workspace = SourceFingerprintUx7D(ProjectJsonSerializer.Serialize(Project), Session.RootDirectory),
                repository = RepositoryEvidenceFingerprintUx7E(repositoryRoot, profile),
            });
            var network = NetworkAssuranceProfile.CreateDefault().DeterministicTests;
            var soak = Project.Module.RuntimePolicies.Extensions.PostLaunch.Production.SoakTests;
            return new StudioUx7EGovernanceSelection(Project.Id.Value, Project.Module.DisplayName, source,
                Project.Module.RuntimePolicies.Extensions.OperationalReadiness.ProjectHealth.Domains.Count,
                accessibility.EditorChecks.Count, accessibility.RuntimeChecks.Count,
                profile.SecurityFuzz.Boundaries.Count, profile.MaliciousFixtures.Families.Count,
                profile.AuroraParityCoverage.ExpectedFeatureCount, profile.AcceptanceGates.RequiredGates.Count,
                network.ClientCount, network.ReplayPasses, network.SoakCycles,
                soak.DurationMinutes, soak.VirtualPlayers);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or JsonException or
                                           NotSupportedException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    internal StudioUx7EGovernanceRunReport RunIsolatedGovernanceUx7E(
        StudioUx7EGovernanceSelection selection,
        CancellationToken cancellationToken,
        Action<StudioUx7EGovernanceStage>? stageCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || History is null || Session is null)
            return FailedGovernanceRunUx7E("Run refusé · aucun module source n’est ouvert.");
        var current = CurrentGovernanceLabUx7E();
        if (current is null || current.ProjectId != selection.ProjectId ||
            !string.Equals(current.SourceFingerprint, selection.SourceFingerprint, StringComparison.Ordinal))
            return FailedGovernanceRunUx7E("Run refusé · le module ou les preuves de dépôt ont changé.");

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
        var repositoryRoot = FindRepositoryRootUx7E();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-e",
            Guid.NewGuid().ToString("N"));
        var stages = new List<StudioUx7EGovernanceStage>();
        var cancelled = false;
        var isolated = false;
        var artifactConfined = false;
        var error = string.Empty;
        _lastUx7ETemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("La copie temporaire n’a pas pu être créée.");
            if (!CopyWorkspaceFilesUx7D(sourceRoot, root))
                throw new IOException("Les fichiers du module n’ont pas pu être copiés dans l’isolat.");
            if (!CopyRepositoryEvidenceUx7E(repositoryRoot, root, sourceProject.Module.RuntimePolicies.Extensions
                    .ModernTooling.QualityAssurance))
                throw new IOException("Les preuves Aurora n’ont pas pu être copiées dans l’isolat.");
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(sourceRoot),
                StringComparison.OrdinalIgnoreCase);
            var project = created.Session.Project;
            var profile = project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance;

            cancellationToken.ThrowIfCancellationRequested();
            var health = ProjectHealthReportService.Evaluate(project, DateTimeOffset.UnixEpoch);
            AddStage(ProjectHealthStageUx7E(health));

            cancellationToken.ThrowIfCancellationRequested();
            var editor = EditorWorkspaceProfileStore.TryLoad(root) ??
                         EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, project.Module.EntryAreaId);
            var editorAccessibility = AccessibilityGovernanceService.Evaluate(editor, project);
            AddStage(AccessibilityStageUx7E("ORF-QA-023", "editor-accessibility",
                "Accessibilité de l’éditeur", editorAccessibility.EditorChecks,
                AccessibilityGovernanceService.Fingerprint(new
                {
                    editorAccessibility.Fingerprint,
                    checks = editorAccessibility.EditorChecks.Select(item => new
                    { item.Code, item.Path, item.Passed }),
                })));

            cancellationToken.ThrowIfCancellationRequested();
            var runtimeAccessibility = AccessibilityGovernanceService.Evaluate(editor, project);
            AddStage(AccessibilityStageUx7E("ORF-QA-024", "runtime-accessibility",
                "Accessibilité du runtime", runtimeAccessibility.RuntimeChecks,
                AccessibilityGovernanceService.Fingerprint(new
                {
                    runtimeAccessibility.Fingerprint,
                    checks = runtimeAccessibility.RuntimeChecks.Select(item => new
                    { item.Code, item.Path, item.Passed }),
                })));

            cancellationToken.ThrowIfCancellationRequested();
            var governance = new WorkspaceGovernanceLabService(new NativeForgeScriptRuntime()).Run(root, project,
                new WorkspaceGovernanceLabOptions(true));
            artifactConfined = ConfinedArtifactExistsUx7D(root, governance.ReportPath);
            AddStage(SecurityFuzzStageUx7E(governance.SecurityFuzz, artifactConfined));

            cancellationToken.ThrowIfCancellationRequested();
            var fixtures = WorkspaceQualityAcceptanceEvidenceValidator.Validate(root, profile);
            AddStage(MaliciousFixturesStageUx7E(fixtures, profile.MaliciousFixtures));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(AuroraCoverageStageUx7E(fixtures, profile.AuroraParityCoverage, root));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(AcceptanceGatesStageUx7E(fixtures, profile.AcceptanceGates, root));

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(DeterministicNetworkStageUx7E());

            cancellationToken.ThrowIfCancellationRequested();
            AddStage(SoakStageUx7E(project.Module.RuntimePolicies.Extensions.PostLaunch.Production.SoakTests));
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or JsonException or
                                           NotSupportedException or DirectoryNotFoundException)
        {
            error = exception.Message;
        }
        finally
        {
            _lastUx7ETemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }

        var final = CurrentGovernanceLabUx7E();
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
        var succeeded = !cancelled && string.IsNullOrEmpty(error) && isolated && artifactConfined &&
                        stages.Count == 9 && stages.All(stage => stage.Completed) && sourceUntouched &&
                        _lastUx7ETemporaryReleased;
        return new StudioUx7EGovernanceRunReport(succeeded, cancelled, isolated, artifactConfined,
            sourceUntouched, _lastUx7ETemporaryReleased, GovernanceRunFingerprintUx7E(stages), stages,
            cancelled
                ? "Run annulé proprement · aucune donnée source n’a été modifiée."
                : succeeded
                    ? "Gouvernance, sécurité et campagnes déterministes inspectées · résultats navigables."
                    : "Run incomplet · " + (string.IsNullOrEmpty(error) ?
                        "consultez les résultats localisés." : error));

        void AddStage(StudioUx7EGovernanceStage stage)
        {
            stages.Add(stage);
            stageCompleted?.Invoke(stage);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal void CloseUx7E() => _lastUx7ETemporaryReleased = true;

    internal StudioUx7EGovernanceSmokeResult RunUx7EContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedGovernanceSmokeUx7E();
        var originalProject = Project;
        var originalHistory = History;
        var originalRevision = History.State.Revision;
        var originalJournal = History.Journal.ToArray();
        var originalDirty = IsDirty;
        var originalArea = ActiveAreaId;
        var originalSelection = SelectedEntityIds.ToArray();
        var originalSelected = SelectedEntityId;
        var originalFile = FileFingerprintUx7D(Session.ProjectFilePath);
        var selection = CurrentGovernanceLabUx7E();
        if (selection is null) return FailedGovernanceSmokeUx7E();

        var run = RunIsolatedGovernanceUx7E(selection, CancellationToken.None);
        var second = RunIsolatedGovernanceUx7E(selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelled = RunIsolatedGovernanceUx7E(selection, cancellation.Token, stage =>
        {
            if (stage.Key == "health") cancellation.Cancel();
        });

        var routes = run.Stages.Select(stage => stage.Route).ToHashSet(StringComparer.Ordinal);
        var nineRoutes = routes.SetEquals(["ORF-QA-022", "ORF-QA-023", "ORF-QA-024", "ORF-QA-025",
            "ORF-QA-026", "ORF-QA-029", "ORF-QA-030", "ORF-MP-036", "ORF-QA-027"]);
        var health = run.Stages.SingleOrDefault(stage => stage.Key == "health");
        var editor = run.Stages.SingleOrDefault(stage => stage.Key == "editor-accessibility");
        var runtime = run.Stages.SingleOrDefault(stage => stage.Key == "runtime-accessibility");
        var fuzz = run.Stages.SingleOrDefault(stage => stage.Key == "security-fuzz");
        var fixtures = run.Stages.SingleOrDefault(stage => stage.Key == "malicious-fixtures");
        var coverage = run.Stages.SingleOrDefault(stage => stage.Key == "aurora-coverage");
        var gates = run.Stages.SingleOrDefault(stage => stage.Key == "acceptance-gates");
        var network = run.Stages.SingleOrDefault(stage => stage.Key == "deterministic-network");
        var soak = run.Stages.SingleOrDefault(stage => stage.Key == "soak-tests");
        var explicitSelection = selection.ProjectId != Guid.Empty && selection.SourceFingerprint.Length == 64 &&
                                selection.HealthDomainCount > 0 && selection.EditorCheckCount == 7 &&
                                selection.RuntimeCheckCount == 7 && selection.FuzzBoundaryCount == 7 &&
                                selection.FixtureFamilyCount == 7 && selection.AuroraFeatureCount == 717 &&
                                selection.RequiredGateCount == 14 && selection.NetworkClientCount >= 2 &&
                                selection.NetworkReplayPasses >= 2 && selection.NetworkSoakCycles > 0 &&
                                selection.SoakDurationMinutes > 0 && selection.SoakVirtualPlayers > 0;
        var healthReady = health is { Completed: true, Cases: > 0 } && health.Details.Count > 0;
        var editorReady = editor is { Completed: true, Cases: 7 } && editor.Details.Count == 7;
        var runtimeReady = runtime is { Completed: true, Cases: 7 } && runtime.Details.Count == 7;
        var fuzzReady = fuzz is { Completed: true, Passed: true, Cases: 112 } && fuzz.Details.Count == 7;
        var fixtureReady = fixtures is { Completed: true, Passed: true, Cases: 7 } &&
                           fixtures.Details.Count >= 10;
        var coverageReady = coverage is { Completed: true, Passed: true, Cases: 717 } &&
                            coverage.Details.Count >= 7;
        var gatesReady = gates is { Completed: true, Passed: true, Cases: 717 } &&
                         gates.Details.Count >= 14;
        var networkReady = network is { Completed: true, Passed: true, Cases: > 0 } &&
                           network.Details.Count >= 5;
        var soakReady = soak is { Completed: true, Passed: true, Cases: >= 3 } &&
                        soak.Details.Count >= 6;
        var cancellationReady = cancelled.Cancelled && cancelled.Stages.Count == 1 &&
                                cancelled.Stages[0].Key == "health" &&
                                cancelled.TemporaryWorkspaceReleased;
        var navigation = run.Stages.Count == 9 && run.Stages.All(stage =>
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
                       cancelled.TemporaryWorkspaceReleased && _lastUx7ETemporaryReleased;
        var succeeded = run.Succeeded && second.Succeeded && nineRoutes && explicitSelection && healthReady &&
                        editorReady && runtimeReady && fuzzReady && fixtureReady && coverageReady && gatesReady &&
                        networkReady && soakReady && cancellationReady &&
                        navigation && deterministic && run.Isolated && run.ArtifactConfined && sourceUnchanged &&
                        released;
        return new StudioUx7EGovernanceSmokeResult(succeeded, nineRoutes, explicitSelection, healthReady,
            editorReady, runtimeReady, fuzzReady, fixtureReady, coverageReady, gatesReady,
            networkReady, soakReady, cancellationReady, navigation, deterministic,
            run.Isolated, run.ArtifactConfined, sourceUnchanged, released);
    }

    private static StudioUx7EGovernanceStage ProjectHealthStageUx7E(ProjectHealthReport report)
    {
        var warnings = report.Domains.Sum(domain => domain.Warning);
        var failures = report.Domains.Sum(domain => domain.Failed);
        var details = report.Domains.Select(domain =>
            $"{domain.Domain} · {domain.Status} · conformes {domain.Passed} · avertissements {domain.Warning} · " +
            $"erreurs {domain.Failed}").ToArray();
        return new StudioUx7EGovernanceStage("ORF-QA-022", "health", "Rapport de santé du projet", true,
            report.BuildAllowed, report.Domains.Sum(domain => domain.Signals.Count), warnings, failures,
            $"État {report.Status} · build {(report.BuildAllowed ? "autorisé" : "bloqué")} · " +
            $"{report.Domains.Count} domaines.", details, report.Fingerprint);
    }

    private static StudioUx7EGovernanceStage AccessibilityStageUx7E(string route, string key, string title,
        IReadOnlyList<QualityGovernanceCheck> checks, string fingerprint)
    {
        var failures = checks.Count(check => !check.Passed);
        var details = checks.Select(check =>
            $"{check.Code} · {(check.Passed ? "conforme" : "à corriger")} · {check.Message} · {check.Path}")
            .ToArray();
        return new StudioUx7EGovernanceStage(route, key, title, true, failures == 0, checks.Count, 0, failures,
            $"{checks.Count - failures}/{checks.Count} exigences conformes.", details, fingerprint);
    }

    private static StudioUx7EGovernanceStage SecurityFuzzStageUx7E(SecurityFuzzReport report, bool confined)
    {
        var failures = report.Boundaries.Sum(boundary => boundary.UnexpectedFailures + boundary.PathEscapes);
        var details = report.Boundaries.Select(boundary =>
            $"{boundary.Boundary} · {boundary.Cases} cas · acceptés {boundary.Accepted} · rejetés " +
            $"{boundary.Rejected} · erreurs inattendues {boundary.UnexpectedFailures} · évasions " +
            $"{boundary.PathEscapes}").ToArray();
        var passed = report.Passed && confined && report.TotalCases == 112 && failures == 0;
        return new StudioUx7EGovernanceStage("ORF-QA-025", "security-fuzz", "Security fuzzing", true,
            passed, report.TotalCases, 0, failures,
            $"{report.TotalCases} charges sur {report.Boundaries.Count} frontières · rapport confiné " +
            $"{(confined ? "oui" : "non")}.", details, report.Fingerprint);
    }

    private static StudioUx7EGovernanceStage MaliciousFixturesStageUx7E(
        WorkspaceQualityAcceptanceEvidenceReport report,
        MaliciousModuleFixturePolicy policy)
    {
        var huge = MaliciousModuleFixtureProbe.InspectHugeArray(policy.MaximumArrayEntries + 1, policy);
        var injection = MaliciousModuleFixtureProbe.InspectInjectionText("<script>forge.inject()</script>", policy);
        var asset = MaliciousModuleFixtureProbe.InspectAssetHeader([0xde, 0xad, 0xbe, 0xef], policy);
        var fixtureDiagnostics = report.Diagnostics.Where(item =>
            item.Code.StartsWith("qa.malicious_fixtures", StringComparison.Ordinal) ||
            item.Code.StartsWith("qa.fixture", StringComparison.Ordinal)).ToArray();
        var details = policy.Families.Order(StringComparer.Ordinal)
            .Select(family => $"Famille · {family} · catalogue et preuve référencés")
            .Concat(new[]
            {
                $"Sonde tableau surdimensionné · {huge?.Code ?? "NON REJETÉ"}",
                $"Sonde texte d’injection · {injection?.Code ?? "NON REJETÉ"}",
                $"Sonde en-tête asset invalide · {asset?.Code ?? "NON REJETÉ"}",
            }).ToArray();
        var passed = report.FixtureCount == MaliciousModuleFixturePolicy.RequiredFamilies.Count &&
                     fixtureDiagnostics.Length == 0 && huge is not null && injection is not null && asset is not null;
        return new StudioUx7EGovernanceStage("ORF-QA-026", "malicious-fixtures",
            "Malicious module fixtures", true, passed, report.FixtureCount, 0, fixtureDiagnostics.Length,
            $"{report.FixtureCount}/7 familles versionnées · trois sondes hostiles rejetées en mémoire.", details,
            HashJsonUx7D(new
            {
                report.FixtureCount,
                diagnostics = fixtureDiagnostics.Select(item => new { item.Code, item.Path }),
                huge = huge?.Code,
                injection = injection?.Code,
                asset = asset?.Code,
            }));
    }

    private static StudioUx7EGovernanceStage AuroraCoverageStageUx7E(
        WorkspaceQualityAcceptanceEvidenceReport report,
        AuroraParityCoveragePolicy policy,
        string root)
    {
        var rows = ReadDelimitedUx7E(Path.Combine(root,
            policy.TrackerPath.Replace('/', Path.DirectorySeparatorChar)));
        var header = rows.Count == 0 ? [] : rows[0];
        var featureIndex = Array.IndexOf(header, "feature_id");
        var domainIndex = Array.IndexOf(header, "domain");
        var statusIndex = Array.IndexOf(header, "status");
        var evidenceIndex = Array.IndexOf(header, "test_evidence");
        var values = rows.Skip(1).Where(row => row.Length == header.Length).ToArray();
        var diagnostics = report.Diagnostics.Where(item =>
            item.Code.StartsWith("qa.aurora_coverage", StringComparison.Ordinal)).ToArray();
        var unique = featureIndex >= 0
            ? values.Select(row => row[featureIndex]).Distinct(StringComparer.Ordinal).Count()
            : 0;
        var testedMissing = statusIndex >= 0 && evidenceIndex >= 0
            ? values.Count(row => string.Equals(row[statusIndex], "Tested", StringComparison.Ordinal) &&
                                  string.IsNullOrWhiteSpace(row[evidenceIndex]))
            : values.Length;
        var details = new List<string>();
        foreach (var status in policy.AllowedStatuses.Order(StringComparer.Ordinal))
        {
            var count = statusIndex >= 0
                ? values.Count(row => string.Equals(row[statusIndex], status, StringComparison.Ordinal))
                : 0;
            details.Add($"Statut {status} · {count} route{(count == 1 ? string.Empty : "s")}");
        }
        if (domainIndex >= 0)
        {
            details.AddRange(values.GroupBy(row => row[domainIndex], StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"Domaine {group.Key} · {group.Count()} routes"));
        }
        details.Add($"Preuves Tested manquantes · {testedMissing}");
        var passed = report.FeatureCount == policy.ExpectedFeatureCount && unique == policy.ExpectedFeatureCount &&
                     testedMissing == 0 && diagnostics.Length == 0;
        return new StudioUx7EGovernanceStage("ORF-QA-029", "aurora-coverage",
            "Coverage Aurora-parity", true, passed, report.FeatureCount, 0,
            diagnostics.Length + testedMissing,
            $"{report.FeatureCount}/{policy.ExpectedFeatureCount} routes · {unique} identifiants uniques · " +
            $"preuves manquantes {testedMissing}.", details, HashJsonUx7D(new
            {
                report.FeatureCount,
                unique,
                testedMissing,
                statuses = policy.AllowedStatuses.Order(StringComparer.Ordinal).Select(status => new
                {
                    status,
                    count = statusIndex >= 0
                        ? values.Count(row => string.Equals(row[statusIndex], status, StringComparison.Ordinal))
                        : 0,
                }),
                diagnostics = diagnostics.Select(item => new { item.Code, item.Path }),
            }));
    }

    private static StudioUx7EGovernanceStage AcceptanceGatesStageUx7E(
        WorkspaceQualityAcceptanceEvidenceReport report,
        AcceptanceGatePolicy policy,
        string root)
    {
        var rows = ReadDelimitedUx7E(Path.Combine(root,
            policy.GateMatrixPath.Replace('/', Path.DirectorySeparatorChar)));
        var header = rows.Count == 0 ? [] : rows[0];
        var featureIndex = Array.IndexOf(header, "feature_id");
        var gateIndex = Array.IndexOf(header, "target_gate");
        var milestoneIndex = Array.IndexOf(header, "target_milestone");
        var values = rows.Skip(1).Where(row => row.Length == header.Length).ToArray();
        var diagnostics = report.Diagnostics.Where(item =>
            item.Code.StartsWith("qa.acceptance_gates", StringComparison.Ordinal)).ToArray();
        var unique = featureIndex >= 0
            ? values.Select(row => row[featureIndex]).Distinct(StringComparer.Ordinal).Count()
            : 0;
        var details = policy.RequiredGates.OrderBy(GateOrderUx7E).Select(gate =>
        {
            var assignments = gateIndex >= 0
                ? values.Count(row => string.Equals(row[gateIndex], gate, StringComparison.Ordinal))
                : 0;
            return $"Gate {gate} · {assignments} affectation{(assignments == 1 ? string.Empty : "s")}";
        }).ToList();
        if (milestoneIndex >= 0)
        {
            details.AddRange(values.GroupBy(row => row[milestoneIndex], StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"Jalon {group.Key} · {group.Count()} routes"));
        }
        details.Add("Programme P0 · audit source, tests ciblés, preuve spécifique, documentation et gate globale requis");
        var passed = report.GateAssignmentCount == 717 && unique == 717 && diagnostics.Length == 0;
        return new StudioUx7EGovernanceStage("ORF-QA-030", "acceptance-gates", "Acceptance gates", true,
            passed, report.GateAssignmentCount, 0, diagnostics.Length,
            $"{report.GateAssignmentCount}/717 affectations · {policy.RequiredGates.Count} gates configurées · " +
            $"tests/docs/preuves obligatoires.", details, HashJsonUx7D(new
            {
                report.GateAssignmentCount,
                unique,
                gates = policy.RequiredGates.OrderBy(GateOrderUx7E).Select(gate => new
                {
                    gate,
                    count = gateIndex >= 0
                        ? values.Count(row => string.Equals(row[gateIndex], gate, StringComparison.Ordinal))
                        : 0,
                }),
                diagnostics = diagnostics.Select(item => new { item.Code, item.Path }),
            }));
    }

    private static StudioUx7EGovernanceStage DeterministicNetworkStageUx7E()
    {
        var profile = NetworkAssuranceProfile.CreateDefault();
        var validation = NetworkAssuranceValidator.Validate(profile);
        var result = DeterministicNetworkTestRunner.Run(profile.DeterministicTests);
        var details = new[]
        {
            $"Clients simulés · {result.Clients}",
            $"Défauts · latence {profile.DeterministicTests.LatencyTicks} ticks · perte chaque " +
            $"{profile.DeterministicTests.DropEvery} · duplication chaque {profile.DeterministicTests.DuplicateEvery} · " +
            $"ordre inversé même tick {(profile.DeterministicTests.ReverseSameTick ? "oui" : "non")}",
            $"Replay · {result.ReplayPasses} passes · empreinte stable {result.Fingerprint[..12]}",
            $"Soak déterministe · {result.SoakCycles} cycles · assertions d’état {result.StateAssertions}",
            $"Paquets · envoyés {result.SentPackets} · perdus {result.DroppedPackets} · dupliqués {result.DuplicatedPackets}",
            "Frontière · harness en mémoire, sans socket, pair distant, bande passante réelle ni serveur externe",
        }.Concat(result.Diagnostics.Select(diagnostic => $"Diagnostic · {diagnostic}")).ToArray();
        var passed = validation.Count == 0 && result.Passed && result.Fingerprint.Length == 64 &&
                     result.StateAssertions == result.Clients * result.ReplayPasses;
        return new StudioUx7EGovernanceStage("ORF-MP-036", "deterministic-network",
            "Tests réseau déterministes", true, passed, result.SoakCycles, 0,
            validation.Count + result.Diagnostics.Count,
            $"{result.Clients} clients · {result.ReplayPasses} replays · {result.SoakCycles} cycles · " +
            $"convergence {(result.Passed ? "confirmée" : "refusée")}.", details, HashJsonUx7D(new
            {
                result.Passed,
                result.Clients,
                result.ReplayPasses,
                result.SoakCycles,
                result.StateAssertions,
                result.SentPackets,
                result.DroppedPackets,
                result.DuplicatedPackets,
                result.Fingerprint,
                validation = validation.Select(item => new { item.Code, item.Path }),
            }));
    }

    private static StudioUx7EGovernanceStage SoakStageUx7E(
        ProductionAutomationProfile.SoakTestSettings authoredPolicy)
    {
        var policy = authoredPolicy with { Enabled = true };
        var memoryGrowth = policy.MaximumMemoryGrowthMegabytes / 2;
        var handleGrowth = policy.MaximumHandleGrowth / 2;
        var passing = new[]
        {
            new SoakTestSample(0, 512, 1_000,
                Math.Max(1, policy.MaximumP95LatencyMilliseconds * 0.4), true, policy.VirtualPlayers),
            new SoakTestSample(Math.Max(1, policy.DurationMinutes / 2), 512 + memoryGrowth / 2,
                1_000 + handleGrowth / 2, Math.Max(1, policy.MaximumP95LatencyMilliseconds * 0.6), true,
                policy.VirtualPlayers),
            new SoakTestSample(policy.DurationMinutes, 512 + memoryGrowth, 1_000 + handleGrowth,
                Math.Max(1, policy.MaximumP95LatencyMilliseconds * 0.8), true, policy.VirtualPlayers),
        }.GroupBy(sample => sample.ElapsedMinutes).Select(group => group.Last())
            .OrderBy(sample => sample.ElapsedMinutes).ToArray();
        if (passing.Length < 2)
        {
            passing =
            [
                passing[0] with { ElapsedMinutes = 0 },
                passing[0] with { ElapsedMinutes = policy.DurationMinutes },
            ];
        }
        var positive = DeterministicSoakTestService.Evaluate(policy, passing);
        var negative = DeterministicSoakTestService.Evaluate(policy,
        [
            new(0, 512, 1_000, 1, true, policy.VirtualPlayers),
            new(policy.DurationMinutes, 513 + policy.MaximumMemoryGrowthMegabytes,
                1_001 + policy.MaximumHandleGrowth, policy.MaximumP95LatencyMilliseconds + 1,
                !policy.AssertDatabaseHealth, Math.Max(0, policy.VirtualPlayers - 1)),
        ]);
        var details = new List<string>
        {
            $"Politique auteur · {(authoredPolicy.Enabled ? "activée" : "désactivée")} · campagne isolée activée en mémoire uniquement",
            $"Durée simulée · {positive.DurationMinutes}/{policy.DurationMinutes} min · {positive.Samples} échantillons",
            $"Population · minimum {positive.MinimumConnectedPlayers}/{policy.VirtualPlayers} joueurs virtuels",
            $"Mémoire · croissance {positive.MemoryGrowthMegabytes}/{policy.MaximumMemoryGrowthMegabytes} Mio",
            $"Handles · croissance {positive.HandleGrowth}/{policy.MaximumHandleGrowth}",
            FormattableString.Invariant($"Latence p95 · {positive.MaximumP95LatencyMilliseconds:0.###}/{policy.MaximumP95LatencyMilliseconds:0.###} ms"),
            $"Base de données · {(policy.AssertDatabaseHealth ? "santé exigée" : "non exigée")}",
            "Frontière · échantillons déterministes, sans attente murale longue, machine distante ni charge matérielle réelle",
        };
        details.AddRange(negative.Diagnostics.Select(diagnostic => $"Campagne négative rejetée · {diagnostic}"));
        var passed = positive.Passed && !negative.Passed && negative.Diagnostics.Count >= 4;
        return new StudioUx7EGovernanceStage("ORF-QA-027", "soak-tests", "Soak tests", true,
            passed, positive.Samples, 0, passed ? 0 : 1,
            $"Campagne positive {positive.Code} · campagne négative {negative.Code} · " +
            $"profil auteur {(authoredPolicy.Enabled ? "actif" : "dormant")} préservé.", details,
            HashJsonUx7D(new
            {
                authoredPolicy.Enabled,
                positive,
                negative,
            }));
    }

    private static bool CopyRepositoryEvidenceUx7E(string sourceRoot, string destinationRoot,
        QualityAssuranceProfile profile)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            profile.MaliciousFixtures.CatalogPath,
            profile.AuroraParityCoverage.TrackerPath,
            profile.AcceptanceGates.GateMatrixPath,
            "docs/planning/P0_ACCEPTANCE_PROGRAM.md",
        };
        var catalogPath = Path.Combine(sourceRoot,
            profile.MaliciousFixtures.CatalogPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(catalogPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            if (document.RootElement.TryGetProperty("fixtures", out var fixtures))
            {
                foreach (var fixture in fixtures.EnumerateArray())
                {
                    if (fixture.TryGetProperty("evidencePath", out var evidence) &&
                        !string.IsNullOrWhiteSpace(evidence.GetString()))
                        paths.Add(evidence.GetString()!);
                }
            }
        }

        Directory.CreateDirectory(Path.Combine(destinationRoot, ".git"));
        var sourcePrefix = Path.GetFullPath(sourceRoot) + Path.DirectorySeparatorChar;
        var destinationPrefix = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
        foreach (var relative in paths)
        {
            var normalized = relative.Replace('/', Path.DirectorySeparatorChar);
            var source = Path.GetFullPath(Path.Combine(sourceRoot, normalized));
            var destination = Path.GetFullPath(Path.Combine(destinationRoot, normalized));
            if (!source.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) ||
                !destination.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(source)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
        return true;
    }

    private static IReadOnlyList<string[]> ReadDelimitedUx7E(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseDelimitedLineUx7E).ToArray();
    }

    private static string[] ParseDelimitedLineUx7E(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (character == ';' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else current.Append(character);
        }
        values.Add(current.ToString());
        return values.ToArray();
    }

    private static int GateOrderUx7E(string gate) =>
        int.TryParse(gate.AsSpan(1), out var value) ? value : int.MaxValue;

    private static string GovernanceRunFingerprintUx7E(IReadOnlyList<StudioUx7EGovernanceStage> stages) =>
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
        }).ToArray());

    private static string FindRepositoryRootUx7E()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "OpenRPGForge.sln")) &&
                    Directory.Exists(Path.Combine(directory.FullName, ".git")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Le dépôt OpenRPG Forge n’a pas été trouvé.");
    }

    private static string RepositoryEvidenceFingerprintUx7E(string root, QualityAssuranceProfile profile)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            profile.MaliciousFixtures.CatalogPath,
            profile.AuroraParityCoverage.TrackerPath,
            profile.AcceptanceGates.GateMatrixPath,
            "docs/planning/P0_ACCEPTANCE_PROGRAM.md",
        };
        var catalogPath = Path.Combine(root,
            profile.MaliciousFixtures.CatalogPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(catalogPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            if (document.RootElement.TryGetProperty("fixtures", out var fixtures))
            {
                foreach (var fixture in fixtures.EnumerateArray())
                {
                    if (fixture.TryGetProperty("evidencePath", out var evidence) &&
                        !string.IsNullOrWhiteSpace(evidence.GetString()))
                        paths.Add(evidence.GetString()!);
                }
            }
        }
        foreach (var relative in paths.Order(StringComparer.OrdinalIgnoreCase))
        {
            AppendHashUx7D(hash, relative.Replace('\\', '/'));
            var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            {
                AppendHashUx7D(hash, "missing");
                continue;
            }
            hash.AppendData(File.ReadAllBytes(full));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static StudioUx7EGovernanceRunReport FailedGovernanceRunUx7E(string message) =>
        new(false, false, false, false, false, true, string.Empty, [], message);

    private static StudioUx7EGovernanceSmokeResult FailedGovernanceSmokeUx7E() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false);
}
