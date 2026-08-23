using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetModuleDistributionHandler _setModuleDistributionUx8F = new(new SystemClock());
    private StudioUx8FDependencyDraft? _approvedDependencyUx8F;
    private ProjectRevision? _approvedDependencyRevisionUx8F;
    private string? _approvedDependencySourceFingerprintUx8F;
    private string? _approvedDependencyDraftFingerprintUx8F;
    private string? _approvedDependencyCandidateFingerprintUx8F;

    internal StudioUx8FSelection? CurrentSupplyChainEvidenceUx8F()
    {
        if (Project is null || Session is null) return null;
        var governancePath = Path.Combine(Session.RootDirectory, AssetGovernanceStore.RelativePath);
        return new StudioUx8FSelection(
            Project.Id.Value,
            Project.Module.DisplayName,
            new DirectoryInfo(Session.RootDirectory).Name,
            File.Exists(governancePath));
    }

    internal IReadOnlyList<StudioUx8FDependencyChoice> CurrentDependencyChoicesUx8F() =>
        Project?.Module.Distribution.Dependencies
            .Select(item => new StudioUx8FDependencyChoice(
                item.Id.Value,
                $"{item.Name} {item.Version} · {item.ApprovalState} · risque {item.RiskLevel}"))
            .ToArray() ?? [];

    internal StudioUx8FDependencyDraft? CreateDependencyDraftUx8F(Guid? dependencyId)
    {
        if (Project is null || History is null) return null;
        var sourceFingerprint = ProjectFingerprintUx8F(Project);
        if (dependencyId is null)
        {
            return new StudioUx8FDependencyDraft(
                History.State.Revision,
                sourceFingerprint,
                Guid.NewGuid(),
                true,
                string.Empty,
                "1.0.0",
                "MIT",
                string.Empty,
                string.Empty,
                "trimestrielle",
                string.Empty,
                string.Empty,
                [ModuleTargetKind.Shipping],
                DependencyApprovalState.Draft,
                DependencyRiskLevel.Medium,
                ["legal-review", "security-review"],
                2,
                string.Empty);
        }

        var dependency = Project.Module.Distribution.Dependencies
            .SingleOrDefault(item => item.Id.Value == dependencyId.Value);
        return dependency is null
            ? null
            : new StudioUx8FDependencyDraft(
                History.State.Revision,
                sourceFingerprint,
                dependency.Id.Value,
                false,
                dependency.Name,
                dependency.Version,
                dependency.LicenseSpdx,
                dependency.Owner,
                dependency.Rationale,
                dependency.UpdateCadence,
                dependency.RemovalPlan,
                dependency.SourceReference,
                dependency.RequiredForTargets,
                dependency.ApprovalState,
                dependency.RiskLevel,
                dependency.ApproverRoles,
                dependency.RequiredApprovals,
                dependency.SecurityAuditReference);
    }

    internal StudioUx8FDistributionPreview PreviewDependencyUx8F(StudioUx8FDependencyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelDependencyApprovalUx8F();
        if (Project is null || History is null)
            return FailedDistributionPreviewUx8F("Aucun module n’est ouvert.");
        if (draft.BaseRevision != History.State.Revision)
            return FailedDistributionPreviewUx8F("Le projet a changé · rechargez le brouillon avant l’aperçu sec.",
                SetModuleDistributionHandler.RevisionConflictCode);
        var sourceFingerprint = ProjectFingerprintUx8F(Project);
        if (!string.Equals(draft.SourceFingerprint, sourceFingerprint, StringComparison.Ordinal))
            return FailedDistributionPreviewUx8F("La source a changé · le brouillon est périmé.",
                "studio.ux8f.source_changed");
        if (!TryBuildDistributionUx8F(Project, draft, out var distribution, out var candidate,
                out var constructionFailure))
            return FailedDistributionPreviewUx8F(constructionFailure, "studio.ux8f.draft_invalid");

        var dependencyDiagnostics = draft.RequiredForTargets
            .SelectMany(target => DistributionComplianceValidator.Validate(candidate, target))
            .Where(item => item.Code.StartsWith("distribution.dependency.", StringComparison.Ordinal))
            .GroupBy(item => (item.Code, item.Path))
            .Select(group => group.First())
            .ToArray();
        if (dependencyDiagnostics.Length > 0)
        {
            return new StudioUx8FDistributionPreview(
                false, false, false, false, string.Empty, string.Empty, string.Empty, string.Empty,
                dependencyDiagnostics.Select(item =>
                    new StudioUx8FDistributionDiagnostic(item.Code, item.Message)).ToArray(),
                "Aperçu refusé · " + dependencyDiagnostics[0].Message);
        }

        var command = new SetModuleDistributionCommand(
            CommandId.From(Guid.NewGuid()),
            History.State.Revision,
            distribution,
            DryRun: true);
        var beforeProject = Project;
        var beforeRevision = History.State.Revision;
        var preview = _setModuleDistributionUx8F.Handle(History.State, command);
        var canonicalDryRun = preview.Succeeded && ReferenceEquals(beforeProject, Project) &&
                              History.State.Revision == beforeRevision && !preview.Audit.Applied &&
                              preview.Patch?.Path == "module/distribution" &&
                              preview.Inverse?.Path == "module/distribution";
        if (!preview.Succeeded || !canonicalDryRun)
        {
            var diagnostics = preview.Diagnostics.Select(item =>
                new StudioUx8FDistributionDiagnostic(item.Code, item.Message)).ToArray();
            return new StudioUx8FDistributionPreview(
                false, false, canonicalDryRun, false, string.Empty, string.Empty,
                preview.Patch?.Summary ?? string.Empty,
                preview.Inverse?.Summary ?? string.Empty,
                diagnostics,
                diagnostics.Length > 0
                    ? "Aperçu refusé · " + diagnostics[0].Message
                    : "Aperçu refusé · le dry-run canonique n’a pas préservé la source.");
        }

        var sourceJson = ProjectJsonSerializer.Serialize(Project);
        var candidateJson = ProjectJsonSerializer.Serialize(candidate);
        var changed = !string.Equals(sourceJson, candidateJson, StringComparison.Ordinal);
        var noticeOne = DistributionNoticeBuilder.Build(candidate, ModuleTargetKind.Shipping);
        var noticeTwo = DistributionNoticeBuilder.Build(candidate, ModuleTargetKind.Shipping);
        var noticeDeterministic = string.Equals(noticeOne, noticeTwo, StringComparison.Ordinal);
        var candidateFingerprint = HashTextUx8F(candidateJson);
        var draftFingerprint = DependencyDraftFingerprintUx8F(draft);
        if (changed && noticeDeterministic)
        {
            _approvedDependencyUx8F = draft;
            _approvedDependencyRevisionUx8F = History.State.Revision;
            _approvedDependencySourceFingerprintUx8F = sourceFingerprint;
            _approvedDependencyDraftFingerprintUx8F = draftFingerprint;
            _approvedDependencyCandidateFingerprintUx8F = candidateFingerprint;
        }

        return new StudioUx8FDistributionPreview(
            true,
            changed,
            canonicalDryRun,
            changed && noticeDeterministic,
            candidateFingerprint,
            HashTextUx8F(noticeOne),
            preview.Patch?.Summary ?? string.Empty,
            preview.Inverse?.Summary ?? string.Empty,
            [],
            changed
                ? noticeDeterministic
                    ? "Aperçu sec prêt · approbation liée à cette révision et à ce brouillon."
                    : "Aperçu refusé · la notice tierce n’est pas déterministe."
                : "Aucun changement · la distribution candidate est identique à la source.");
    }

    internal StudioActionResult ApplyApprovedDependencyUx8F(StudioUx8FDependencyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (Project is null || History is null)
            return new StudioActionResult(false, "Aucun module n’est ouvert.");
        var sourceFingerprint = ProjectFingerprintUx8F(Project);
        var draftFingerprint = DependencyDraftFingerprintUx8F(draft);
        if (_approvedDependencyUx8F is null || _approvedDependencyRevisionUx8F != History.State.Revision ||
            !string.Equals(_approvedDependencySourceFingerprintUx8F, sourceFingerprint, StringComparison.Ordinal) ||
            !string.Equals(_approvedDependencyDraftFingerprintUx8F, draftFingerprint, StringComparison.Ordinal))
            return new StudioActionResult(false,
                "Application refusée · prévisualisez exactement ce brouillon sur la révision courante.");
        if (!TryBuildDistributionUx8F(Project, draft, out var distribution, out var candidate, out var failure))
            return new StudioActionResult(false, "Application refusée · " + failure);
        if (!string.Equals(_approvedDependencyCandidateFingerprintUx8F,
                ProjectFingerprintUx8F(candidate), StringComparison.Ordinal))
            return new StudioActionResult(false, "Application refusée · le candidat approuvé a changé.");

        var commandId = CommandId.From(Guid.NewGuid());
        var dryRun = _setModuleDistributionUx8F.Handle(History.State,
            new SetModuleDistributionCommand(commandId, History.State.Revision, distribution, DryRun: true));
        if (!dryRun.Succeeded) return FailureUx8F(dryRun, "Application refusée");
        var applied = _setModuleDistributionUx8F.Handle(History.State,
            new SetModuleDistributionCommand(commandId, History.State.Revision, distribution, DryRun: false));
        if (!applied.Succeeded) return FailureUx8F(applied, "Application refusée");
        History.Record(applied);
        CancelDependencyApprovalUx8F();
        return new StudioActionResult(true,
            "Approbation enregistrée dans l’historique · Ctrl+Z permet de la restaurer.");
    }

    internal StudioActionResult UndoDependencyUx8F()
    {
        CancelDependencyApprovalUx8F();
        return Undo();
    }

    internal StudioActionResult RedoDependencyUx8F()
    {
        CancelDependencyApprovalUx8F();
        return Redo();
    }

    internal StudioActionResult SaveDependencyUx8F() => Save();

    internal StudioUx8FDistributionApprovalSnapshot CaptureDependencyApprovalUx8F() => new(
        _approvedDependencyUx8F,
        _approvedDependencyRevisionUx8F,
        _approvedDependencySourceFingerprintUx8F,
        _approvedDependencyDraftFingerprintUx8F,
        _approvedDependencyCandidateFingerprintUx8F);

    internal void RestoreDependencyApprovalUx8F(StudioUx8FDistributionApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedDependencyUx8F = snapshot.Draft;
        _approvedDependencyRevisionUx8F = snapshot.Revision;
        _approvedDependencySourceFingerprintUx8F = snapshot.SourceFingerprint;
        _approvedDependencyDraftFingerprintUx8F = snapshot.DraftFingerprint;
        _approvedDependencyCandidateFingerprintUx8F = snapshot.CandidateFingerprint;
    }

    internal void CancelDependencyApprovalUx8F()
    {
        _approvedDependencyUx8F = null;
        _approvedDependencyRevisionUx8F = null;
        _approvedDependencySourceFingerprintUx8F = null;
        _approvedDependencyDraftFingerprintUx8F = null;
        _approvedDependencyCandidateFingerprintUx8F = null;
    }

    internal StudioUx8FReport RunSupplyChainEvidenceUx8F(
        StudioUx8FSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (Project is null || Session is null || selection.ProjectId != Project.Id.Value)
            return FailedUx8F(selection, "La sélection ne correspond plus au module ouvert.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stages = new List<StudioUx8FStage>(4)
            {
                InspectDependencyApprovalsUx8F(Project),
            };
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(InspectThirdPartyNoticesUx8F(Project));
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(InspectCreditsGenerationUx8F(Project));
            cancellationToken.ThrowIfCancellationRequested();
            stages.Add(InspectIpBoundaryUx8F(Session.RootDirectory));
            cancellationToken.ThrowIfCancellationRequested();

            return new StudioUx8FReport(
                true,
                false,
                true,
                selection.ModuleName,
                selection.WorkspaceName,
                stages,
                $"Inspection terminée · {stages.Count(stage => stage.Ready)}/4 états prêts · aucune source modifiée.");
        }
        catch (OperationCanceledException)
        {
            return new StudioUx8FReport(
                true,
                true,
                true,
                selection.ModuleName,
                selection.WorkspaceName,
                [],
                "Inspection annulée · aucune approbation, notice, archive ou frontière n’a été modifiée.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException or InvalidDataException)
        {
            return FailedUx8F(selection, "Inspection interrompue · " + exception.Message);
        }
    }

    internal StudioUx8FSmokeResult RunUx8FContractSmoke()
    {
        if (Project is null || History is null || Session is null)
            return FailedSmokeUx8F("Aucun module source n’est ouvert.");
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureDependencyApprovalUx8F();
        var selection = CurrentSupplyChainEvidenceUx8F();
        if (selection is null) return FailedSmokeUx8F("La sélection UX8-F est indisponible.");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = RunSupplyChainEvidenceUx8F(selection, cancellation.Token);
        var report = RunSupplyChainEvidenceUx8F(selection, CancellationToken.None);
        var fourRoutes = report.Succeeded && !report.Cancelled && report.Stages.Count == 4 &&
                         report.Stages.Select(stage => stage.Route).SequenceEqual(
                             new[] { "ORF-BUILD-022", "ORF-BUILD-023", "ORF-CONT-030", "ORF-CONT-031" },
                             StringComparer.Ordinal);
        StudioUx8FStage Stage(string route) => report.Stages.Single(stage => stage.Route == route);

        var approvedProject = CreateDependencyFixtureUx8F(Project, approved: true);
        var draftProject = CreateDependencyFixtureUx8F(Project, approved: false);
        var approvedDependency = DistributionComplianceValidator.Validate(approvedProject, ModuleTargetKind.Shipping)
            .All(diagnostic => !diagnostic.Code.StartsWith("distribution.dependency.", StringComparison.Ordinal));
        var draftBlocked = DistributionComplianceValidator.Validate(draftProject, ModuleTargetKind.Shipping)
            .Any(diagnostic => diagnostic.Code == DistributionComplianceValidator.DependencyApprovalRequiredCode);
        var dependencyApproval = fourRoutes && Stage("ORF-BUILD-022").Evaluated &&
                                 approvedDependency && draftBlocked;

        var noticeOne = DistributionNoticeBuilder.Build(approvedProject, ModuleTargetKind.Shipping);
        var noticeTwo = DistributionNoticeBuilder.Build(approvedProject, ModuleTargetKind.Shipping);
        var thirdPartyNotices = fourRoutes && Stage("ORF-BUILD-023").Evaluated &&
                                string.Equals(noticeOne, noticeTwo, StringComparison.Ordinal) &&
                                noticeOne.Contains("Bibliothèque témoin UX8-F 1.2.3 | MIT", StringComparison.Ordinal) &&
                                noticeOne.Contains("THIRD-PARTY DEPENDENCIES", StringComparison.Ordinal);
        var creditsGeneration = fourRoutes && Stage("ORF-CONT-030").Evaluated &&
                                noticeOne.Contains("CONTENT PROVENANCE", StringComparison.Ordinal) &&
                                noticeOne.Contains("DATA TABLES", StringComparison.Ordinal) &&
                                noticeOne.Contains("CONTENT PACKAGES", StringComparison.Ordinal);

        var validGovernance = CreateGovernanceFixtureUx8F(proprietaryViolation: false);
        var invalidGovernance = CreateGovernanceFixtureUx8F(proprietaryViolation: true);
        var validDiagnostics = AssetGovernanceValidator.Validate(validGovernance);
        var invalidDiagnostics = AssetGovernanceValidator.Validate(invalidGovernance);
        var ipBoundary = fourRoutes && Stage("ORF-CONT-031").Evaluated && validDiagnostics.Count == 0 &&
                         invalidDiagnostics.Any(item => item.Code == AssetGovernanceValidator.ProprietaryRedistributionCode) &&
                         invalidDiagnostics.Any(item => item.Code == AssetGovernanceValidator.CompatibilityIsolationRequiredCode) &&
                         invalidDiagnostics.Any(item => item.Code == AssetGovernanceValidator.LegalReviewRequiredCode);
        var cancellationReady = cancelled.Succeeded && cancelled.Cancelled && cancelled.Stages.Count == 0;
        var logs = report.Stages.All(stage => stage.Log.Count > 0) &&
                   report.Stages.SelectMany(stage => stage.Log).All(line => !Path.IsPathRooted(line));
        var canonicalDryRun = false;
        var canonicalCommand = false;
        var staleSource = false;
        var staleApproval = false;
        var undoRedo = false;
        var persistence = false;
        var neighbors = false;
        var released = false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux8-f-contract-" +
                                                     Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        StudioSessionController? reopened = null;
        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || !editing.Open(root).Succeeded)
                throw new InvalidOperationException("Le workspace transactionnel UX8-F n’a pas pu être ouvert.");
            var initial = editing.Project!;
            var sourceDraft = editing.CreateDependencyDraftUx8F(null) ??
                              throw new InvalidOperationException("Le brouillon UX8-F est indisponible.");
            var draft = sourceDraft with
            {
                Name = "Bibliothèque témoin UX8-F",
                Version = "1.2.3",
                LicenseSpdx = "MIT",
                Owner = "Équipe Forge",
                Rationale = "Preuve isolée de gouvernance de dépendance.",
                UpdateCadence = "trimestrielle",
                RemovalPlan = "Remplacer par l’implémentation interne.",
                SourceReference = "https://example.invalid/ux8-f-library",
                RequiredForTargets = [ModuleTargetKind.Shipping],
                ApprovalState = DependencyApprovalState.Approved,
                RiskLevel = DependencyRiskLevel.Medium,
                ApproverRoles = ["legal-review", "security-review"],
                RequiredApprovals = 2,
                SecurityAuditReference = "audit:ux8-f",
            };
            var invalid = editing.PreviewDependencyUx8F(draft with { Version = "latest" });
            staleSource = !editing.PreviewDependencyUx8F(draft with
            {
                SourceFingerprint = "source-périmée",
            }).Succeeded;
            var beforePreview = editing.Project;
            var beforeRevision = editing.History!.State.Revision;
            var preview = editing.PreviewDependencyUx8F(draft);
            var approval = editing.CaptureDependencyApprovalUx8F();
            canonicalDryRun = !invalid.Succeeded && preview.Succeeded && preview.Changed &&
                              preview.CanonicalDryRun && preview.CanApply &&
                              ReferenceEquals(beforePreview, editing.Project) &&
                              editing.History.State.Revision == beforeRevision &&
                              !string.IsNullOrWhiteSpace(preview.NoticeSha256) &&
                              preview.Patch.Contains("dependency approval", StringComparison.OrdinalIgnoreCase);
            var applied = canonicalDryRun && editing.ApplyApprovedDependencyUx8F(draft).Succeeded;
            var appliedProject = editing.Project!;
            var appliedDependency = appliedProject.Module.Distribution.Dependencies
                .SingleOrDefault(item => item.Id.Value == draft.Id);
            canonicalCommand = applied && appliedDependency is
            {
                ApprovalState: DependencyApprovalState.Approved,
                RiskLevel: DependencyRiskLevel.Medium,
                RequiredApprovals: 2,
            } && appliedDependency.ApproverRoles.Count == 2 &&
                editing.History.State.Revision != beforeRevision && editing.History.Journal.Count == 1;
            var afterApplyFingerprint = ProjectFingerprintUx8F(appliedProject);
            var undone = canonicalCommand && editing.UndoDependencyUx8F().Succeeded &&
                         ProjectFingerprintUx8F(editing.Project!) == ProjectFingerprintUx8F(initial);
            var redone = undone && editing.RedoDependencyUx8F().Succeeded &&
                         ProjectFingerprintUx8F(editing.Project!) == afterApplyFingerprint;
            undoRedo = undone && redone;
            editing.RestoreDependencyApprovalUx8F(approval);
            staleApproval = !editing.ApplyApprovedDependencyUx8F(draft).Succeeded;
            var saved = editing.SaveDependencyUx8F().Succeeded;
            var expectedJson = ProjectJsonSerializer.Serialize(editing.Project!);
            reopened = new StudioSessionController();
            persistence = saved && reopened.Open(root).Succeeded && reopened.Project is not null &&
                          string.Equals(ProjectJsonSerializer.Serialize(reopened.Project), expectedJson,
                              StringComparison.Ordinal);
            var finalProject = editing.Project!;
            var restored = ReplaceDistributionUx8F(finalProject, initial.Module.Distribution);
            neighbors = string.Equals(ProjectJsonSerializer.Serialize(restored),
                ProjectJsonSerializer.Serialize(initial), StringComparison.Ordinal) &&
                        finalProject.Module.Distribution.TargetProfiles.SequenceEqual(
                            initial.Module.Distribution.TargetProfiles) &&
                        Equals(finalProject.Module.Distribution.Release, initial.Module.Distribution.Release) &&
                        Equals(finalProject.Module.Distribution.Launcher, initial.Module.Distribution.Launcher) &&
                        finalProject.Module.Distribution.Dependencies
                            .Where(item => item.Id.Value != draft.Id)
                            .SequenceEqual(initial.Module.Distribution.Dependencies);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or InvalidDataException)
        {
            _ = exception;
        }
        finally
        {
            reopened?.Close();
            editing.Close();
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                released = !Directory.Exists(root);
            }
            catch
            {
                released = false;
            }
            RestoreDependencyApprovalUx8F(sourceApproval);
        }

        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected && CaptureDependencyApprovalUx8F() == sourceApproval;
        var resourcesReleased = report.ResourcesReleased && released;
        var succeeded = fourRoutes && dependencyApproval && thirdPartyNotices && creditsGeneration && ipBoundary &&
                        cancellationReady && logs && canonicalDryRun && canonicalCommand && staleSource &&
                        staleApproval && undoRedo && persistence && neighbors && resourcesReleased && sourceUnchanged;
        return new StudioUx8FSmokeResult(
            succeeded, fourRoutes, dependencyApproval, thirdPartyNotices, creditsGeneration, ipBoundary,
            cancellationReady, logs, canonicalDryRun, canonicalCommand, staleSource, staleApproval, undoRedo,
            persistence, neighbors, resourcesReleased, sourceUnchanged,
            succeeded
                ? "UX8-F · transaction canonique, notices, crédits et frontières IP prouvés sans mutation source."
                : "UX8-F · une preuve transactionnelle de chaîne de dépendances a échoué.");
    }

    internal void CloseUx8F()
    {
        CancelDependencyApprovalUx8F();
    }

    private static StudioUx8FStage InspectDependencyApprovalsUx8F(ProjectDefinition project)
    {
        var dependencies = project.Module.Distribution.Dependencies;
        var enabledTargets = project.Module.Distribution.TargetProfiles
            .Where(profile => profile.Enabled)
            .Select(profile => profile.Target)
            .Order()
            .ToArray();
        var diagnostics = enabledTargets
            .SelectMany(target => DistributionComplianceValidator.Validate(project, target))
            .Where(diagnostic => diagnostic.Code.StartsWith("distribution.dependency.", StringComparison.Ordinal))
            .GroupBy(diagnostic => (diagnostic.Code, diagnostic.Path))
            .Select(group => group.First())
            .ToArray();
        var ready = diagnostics.Length == 0;
        var details = new List<string>
        {
            $"Dépendances déclarées · {dependencies.Count}",
            $"Cibles actives contrôlées · {string.Join(" · ", enabledTargets)}",
            $"Diagnostics d’approbation · {diagnostics.Length}",
        };
        if (dependencies.Count == 0)
        {
            details.Add("Aucune dépendance tierce déclarée · aucun état d’approbation synthétisé.");
        }
        else
        {
            foreach (var dependency in dependencies)
            {
                details.Add($"{dependency.Name} {dependency.Version} · {dependency.LicenseSpdx} · {dependency.ApprovalState} · risque {dependency.RiskLevel}");
                details.Add($"Responsable · {dependency.Owner} · avis {dependency.ApproverRoles.Count}/{dependency.RequiredApprovals} · cibles {string.Join(" · ", dependency.RequiredForTargets)}");
            }
        }
        details.AddRange(diagnostics.Select(diagnostic => $"{diagnostic.Code} · {diagnostic.Message}"));
        return new StudioUx8FStage(
            "ORF-BUILD-022", "Dependency approval", true, ready,
            dependencies.Count == 0 ? "AUCUNE DÉPENDANCE TIERCE" : ready ? "APPROBATIONS VALIDES" : "APPROBATION BLOQUÉE",
            "Les approbations persistées sont croisées avec chaque cible active sans changer leur état.",
            details,
            [
                $"dependencies={dependencies.Count}",
                $"targets={enabledTargets.Length}",
                $"diagnostics={diagnostics.Length}",
                "approval_mutation=none",
            ]);
    }

    private static StudioUx8FStage InspectThirdPartyNoticesUx8F(ProjectDefinition project)
    {
        var first = DistributionNoticeBuilder.Build(project, ModuleTargetKind.Shipping);
        var second = DistributionNoticeBuilder.Build(project, ModuleTargetKind.Shipping);
        var deterministic = string.Equals(first, second, StringComparison.Ordinal);
        var requiredSections = new[]
        {
            "CONTENT PROVENANCE", "DATA TABLES", "CONTENT PACKAGES", "THIRD-PARTY DEPENDENCIES",
        };
        var complete = requiredSections.All(section => first.Contains(section, StringComparison.Ordinal));
        var dependencyCount = project.Module.Distribution.Dependencies
            .Count(item => item.RequiredForTargets.Contains(ModuleTargetKind.Shipping));
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(first)));
        return new StudioUx8FStage(
            "ORF-BUILD-023", "Third-party notices", true, deterministic && complete,
            deterministic && complete ? "NOTICE CANONIQUE DÉTERMINISTE" : "NOTICE INCOMPLÈTE",
            "La notice Shipping est générée deux fois en mémoire et comparée octet par octet, sans package.",
            [
                $"Chemin canonique · {DistributionNoticeBuilder.PackagePath}",
                $"Sections obligatoires · {requiredSections.Length}/{requiredSections.Length}",
                $"Dépendances Shipping · {dependencyCount}",
                $"Lignes générées · {first.Count(character => character == '\n')}",
                "Empreinte déterministe disponible dans le journal technique.",
            ],
            [
                $"deterministic={deterministic.ToString().ToLowerInvariant()}",
                $"sections_complete={complete.ToString().ToLowerInvariant()}",
                $"sha256={hash}",
                "package_write=none",
            ]);
    }

    private static StudioUx8FStage InspectCreditsGenerationUx8F(ProjectDefinition project)
    {
        var notice = DistributionNoticeBuilder.Build(project, ModuleTargetKind.Shipping);
        var packages = project.Module.Content.Packages.Packages;
        var sources = packages.Sum(package => package.Sources.Count);
        var assets = packages.Sum(package => package.Assets.Count);
        var details = new List<string>
        {
            $"Blueprints crédités · {project.Blueprints.Count}",
            $"Tables de données · {project.Module.Content.DataTables.Count}",
            $"Packages de contenu · {packages.Count} · sources {sources} · assets {assets}",
            $"Dépendances tierces · {project.Module.Distribution.Dependencies.Count}",
            "Ordre canonique · slug, namespace puis identités logiques ; aucun fichier écrit.",
        };
        details.AddRange(project.Blueprints.Take(5).Select(blueprint =>
            $"Exemple · {blueprint.DisplayName} · {blueprint.Provenance.LicenseSpdx} · {blueprint.Provenance.Author}"));
        var ready = notice.Contains("OpenRPG Forge - Distribution notices", StringComparison.Ordinal) &&
                    notice.Contains(project.Module.DisplayName, StringComparison.Ordinal);
        return new StudioUx8FStage(
            "ORF-CONT-030", "Credits and notices generation", true, ready,
            ready ? "CRÉDITS AGRÉGÉS EN MÉMOIRE" : "AGRÉGATION INCOMPLÈTE",
            "Les provenances projet, tables, packages, sources, assets et dépendances alimentent la notice canonique.",
            details,
            [
                $"blueprints={project.Blueprints.Count}",
                $"data_tables={project.Module.Content.DataTables.Count}",
                $"packages={packages.Count}",
                $"sources={sources}",
                $"assets={assets}",
                "credits_write=none",
            ]);
    }

    private static StudioUx8FStage InspectIpBoundaryUx8F(string workspaceRoot)
    {
        var loaded = AssetGovernanceStore.Load(workspaceRoot);
        var diagnostics = loaded.Succeeded
            ? loaded.Records.SelectMany(record => AssetGovernanceValidator.Validate(record)).ToArray()
            : [];
        var proprietary = loaded.Records.Count(record => record.IpBoundary.Origin is
            AssetOriginKind.ProprietaryNwn or AssetOriginKind.ProprietaryCep);
        var redistributable = loaded.Records.Count(record => record.IpBoundary.RedistributionAllowed);
        var ready = loaded.Succeeded && diagnostics.Length == 0;
        var verdict = loaded.FileMissing
            ? "REGISTRE ABSENT · AUCUN CLAIM"
            : ready ? "FRONTIÈRES VALIDES" : "FRONTIÈRE BLOQUÉE";
        var details = new List<string>
        {
            loaded.FileMissing
                ? "Registre de gouvernance absent · zéro frontière d’asset déclarée dans ce workspace."
                : $"Enregistrements de gouvernance · {loaded.Records.Count}",
            $"Redistribution autorisée · {redistributable}",
            $"Origines propriétaires isolées · {proprietary}",
            $"Diagnostics bloquants · {diagnostics.Length}",
            "Une absence reste une absence : aucune provenance ou permission n’est déduite du catalogue global.",
        };
        details.AddRange(loaded.Diagnostics.Select(diagnostic => $"{diagnostic.Code} · {diagnostic.Message}"));
        details.AddRange(diagnostics.Take(12).Select(diagnostic => $"{diagnostic.Code} · {diagnostic.Message}"));
        return new StudioUx8FStage(
            "ORF-CONT-031", "IP boundary enforcement", true, ready || loaded.FileMissing, verdict,
            "Le registre workspace est lu et validé ; les ressources propriétaires restent exclues de toute redistribution.",
            details,
            [
                $"registry_missing={loaded.FileMissing.ToString().ToLowerInvariant()}",
                $"records={loaded.Records.Count}",
                $"proprietary={proprietary}",
                $"diagnostics={diagnostics.Length + loaded.Diagnostics.Count}",
                "governance_write=none",
            ]);
    }

    private static ProjectDefinition CreateDependencyFixtureUx8F(ProjectDefinition source, bool approved)
    {
        var dependency = new ThirdPartyDependencyApproval(
            ThirdPartyDependencyId.From(Guid.Parse("8f000000-0000-4000-8000-000000000001")),
            "Bibliothèque témoin UX8-F",
            "1.2.3",
            "MIT",
            "Équipe Forge",
            "Preuve isolée de gouvernance de dépendance.",
            "trimestrielle",
            "Remplacer par l’implémentation interne.",
            "https://example.invalid/ux8-f-library",
            [ModuleTargetKind.Shipping],
            "isolated-smoke",
            approvalState: approved ? DependencyApprovalState.Approved : DependencyApprovalState.Draft,
            riskLevel: DependencyRiskLevel.Medium,
            approverRoles: ["legal-review", "security-review"],
            requiredApprovals: 2,
            securityAuditReference: "audit:ux8-f");
        var sourceDistribution = source.Module.Distribution;
        var distribution = new ModuleDistributionConfiguration(
            sourceDistribution.Id,
            sourceDistribution.TargetProfiles,
            sourceDistribution.Dependencies
                .Where(item => !item.Name.Equals(dependency.Name, StringComparison.OrdinalIgnoreCase))
                .Append(dependency)
                .ToArray(),
            "isolated-smoke",
            sourceDistribution.Release,
            sourceDistribution.SchemaVersion,
            sourceDistribution.Launcher);
        var module = source.Module;
        var candidateModule = new ModuleDefinition(
            module.Id, module.Slug, module.DisplayName, module.Description, module.EntryAreaId,
            module.Metadata, module.Variables, distribution, module.Localization, module.Content,
            module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        return new ProjectDefinition(source.Id, source.Title, candidateModule, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
    }

    private static AssetGovernanceRecord CreateGovernanceFixtureUx8F(bool proprietaryViolation)
    {
        var assetId = AssetCatalogItemId.ForExternalId("ux8-f:governance-fixture");
        return new AssetGovernanceRecord(
            AssetGovernanceRecord.CurrentSchemaVersion,
            AssetGovernanceId.ForAsset(assetId),
            assetId,
            AssetPackageManifestId.ForAsset(assetId),
            "MIT",
            new string('a', 64),
            "Équipe Forge",
            "https://example.invalid/ux8-f-asset",
            "permission:ux8-f",
            "aucune",
            false,
            string.Empty,
            string.Empty,
            4_096,
            new AssetQuarantineReview(
                "assets/ux8-f-fixture.glb",
                "model/gltf-binary",
                "model/gltf-binary",
                1_024,
                4_096,
                "isolated-scanner",
                "clean",
                "valid",
                AssetQuarantineStatus.Approved,
                "Revue UX8-F",
                DateTimeOffset.UnixEpoch),
            proprietaryViolation
                ? new AssetIpBoundary(AssetOriginKind.ProprietaryNwn, true, false, string.Empty)
                : new AssetIpBoundary(AssetOriginKind.OpenLicensed, true, false, string.Empty));
    }

    private static bool TryBuildDistributionUx8F(
        ProjectDefinition source,
        StudioUx8FDependencyDraft draft,
        out ModuleDistributionConfiguration distribution,
        out ProjectDefinition candidate,
        out string failure)
    {
        distribution = source.Module.Distribution;
        candidate = source;
        failure = string.Empty;
        try
        {
            var existing = source.Module.Distribution.Dependencies
                .SingleOrDefault(item => item.Id.Value == draft.Id);
            if (!draft.IsNew && existing is null)
            {
                failure = "La dépendance sélectionnée n’existe plus.";
                return false;
            }
            if (draft.IsNew && existing is not null)
            {
                failure = "L’identité de la nouvelle dépendance existe déjà.";
                return false;
            }

            var dependency = new ThirdPartyDependencyApproval(
                ThirdPartyDependencyId.From(draft.Id),
                draft.Name,
                draft.Version,
                draft.LicenseSpdx,
                draft.Owner,
                draft.Rationale,
                draft.UpdateCadence,
                draft.RemovalPlan,
                draft.SourceReference,
                draft.RequiredForTargets,
                existing?.Provenance ?? "studio-ux8-f",
                existing?.SchemaVersion ?? ThirdPartyDependencyApproval.CurrentSchemaVersion,
                draft.ApprovalState,
                draft.RiskLevel,
                draft.ApproverRoles,
                draft.RequiredApprovals,
                draft.SecurityAuditReference);
            var dependencies = source.Module.Distribution.Dependencies
                .Where(item => item.Id.Value != draft.Id)
                .Append(dependency)
                .ToArray();
            var sourceDistribution = source.Module.Distribution;
            distribution = new ModuleDistributionConfiguration(
                sourceDistribution.Id,
                sourceDistribution.TargetProfiles,
                dependencies,
                sourceDistribution.Provenance,
                sourceDistribution.Release,
                sourceDistribution.SchemaVersion,
                sourceDistribution.Launcher);
            candidate = ReplaceDistributionUx8F(source, distribution);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           NotSupportedException)
        {
            failure = exception.Message;
            return false;
        }
    }

    private static ProjectDefinition ReplaceDistributionUx8F(
        ProjectDefinition source,
        ModuleDistributionConfiguration distribution)
    {
        var module = source.Module;
        var candidateModule = new ModuleDefinition(
            module.Id,
            module.Slug,
            module.DisplayName,
            module.Description,
            module.EntryAreaId,
            module.Metadata,
            module.Variables,
            distribution,
            module.Localization,
            module.Content,
            module.ArtisticStyle,
            module.RuntimePolicies,
            module.Factions,
            module.WorldFeatures);
        return new ProjectDefinition(source.Id, source.Title, candidateModule, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
    }

    private static string DependencyDraftFingerprintUx8F(StudioUx8FDependencyDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.BaseRevision.Value);
        WriteFingerprintStringUx8F(writer, draft.SourceFingerprint);
        writer.Write(draft.Id.ToByteArray());
        writer.Write(draft.IsNew);
        foreach (var value in new[]
                 {
                     draft.Name, draft.Version, draft.LicenseSpdx, draft.Owner, draft.Rationale,
                     draft.UpdateCadence, draft.RemovalPlan, draft.SourceReference,
                 })
            WriteFingerprintStringUx8F(writer, value);
        writer.Write(draft.RequiredForTargets.Count);
        foreach (var target in draft.RequiredForTargets) writer.Write((int)target);
        writer.Write((int)draft.ApprovalState);
        writer.Write((int)draft.RiskLevel);
        writer.Write(draft.ApproverRoles.Count);
        foreach (var role in draft.ApproverRoles) WriteFingerprintStringUx8F(writer, role);
        writer.Write(draft.RequiredApprovals);
        WriteFingerprintStringUx8F(writer, draft.SecurityAuditReference);
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteFingerprintStringUx8F(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ProjectFingerprintUx8F(ProjectDefinition project) =>
        HashTextUx8F(ProjectJsonSerializer.Serialize(project));

    private static string HashTextUx8F(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static StudioUx8FDistributionPreview FailedDistributionPreviewUx8F(
        string message,
        string code = "studio.ux8f.preview_failed") =>
        new(false, false, false, false, string.Empty, string.Empty, string.Empty, string.Empty,
            [new StudioUx8FDistributionDiagnostic(code, message)], message);

    private static StudioActionResult FailureUx8F(CommandResult result, string prefix) =>
        new(false, result.Diagnostics.Count == 0
            ? prefix
            : prefix + " · " + string.Join(" · ", result.Diagnostics.Select(item => item.Message)));

    private static StudioUx8FReport FailedUx8F(StudioUx8FSelection selection, string message) =>
        new(false, false, true, selection.ModuleName, selection.WorkspaceName, [], message);

    private static StudioUx8FSmokeResult FailedSmokeUx8F(string message) =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, message);
}
