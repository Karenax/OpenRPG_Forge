using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Quality;
using Forge.Application.Time;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetQualityFoundationHandler _setQualityFoundationUx7B = new(new SystemClock());
    private QualityFoundationCommandHistory? _qualityFoundationHistoryUx7B;
    private QualityFoundationProfile? _persistedQualityFoundationUx7B;
    private StudioUx7BProfileDraft? _approvedQualityFoundationUx7B;
    private QualityFoundationRevision? _approvedQualityRevisionUx7B;
    private string? _approvedQualityCandidateFingerprintUx7B;
    private string? _approvedQualitySourceFingerprintUx7B;
    private int _qualityUndoDepthUx7B;
    private int _qualityRedoDepthUx7B;
    private bool _lastUx7BTemporaryReleased = true;

    internal bool LastUx7BTemporaryReleased => _lastUx7BTemporaryReleased;

    internal bool QualityFoundationHasUnsavedChangesUx7B =>
        _qualityFoundationHistoryUx7B is not null &&
        (_persistedQualityFoundationUx7B is null ||
         !string.Equals(ProfileFingerprintUx7B(_qualityFoundationHistoryUx7B.State.Profile),
             ProfileFingerprintUx7B(_persistedQualityFoundationUx7B), StringComparison.Ordinal));

    internal StudioUx7BProfileDraft? CurrentQualityFoundationUx7B()
    {
        if (!EnsureQualityFoundationUx7B(out _)) return null;
        return ToDraftUx7B(_qualityFoundationHistoryUx7B!.State);
    }

    internal StudioUx7BPreview PreviewQualityFoundationUx7B(StudioUx7BProfileDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelQualityFoundationApprovalUx7B();
        if (!EnsureQualityFoundationUx7B(out var failure)) return FailedPreviewUx7B(failure);
        var history = _qualityFoundationHistoryUx7B!;
        var diagnostics = ValidateDraftUx7B(draft, history).ToArray();
        if (diagnostics.Length > 0)
            return new(false, false, false, "Aperçu refusé · corrigez le profil qualité.", [], diagnostics);

        var candidate = ToProfileUx7B(draft, history.State.Profile);
        var result = _setQualityFoundationUx7B.Handle(history.State,
            new SetQualityFoundationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                candidate, DryRun: true));
        var mapped = result.Diagnostics.Select(diagnostic => new StudioUx7BDiagnostic(
            diagnostic.Code, "quality/foundation", "error", HumanQualityMessageUx7B(diagnostic.Code,
                diagnostic.Message))).ToArray();
        if (!result.Succeeded || result.Patch is null || result.Audit.Applied)
            return new(false, false, false, "Le dry-run canonique a refusé ce profil.", [], mapped);

        var changed = !string.Equals(ProfileFingerprintUx7B(history.State.Profile),
            ProfileFingerprintUx7B(candidate), StringComparison.Ordinal);
        var diff = BuildSemanticDiffUx7B(history.State.Profile, candidate);
        if (changed)
        {
            _approvedQualityFoundationUx7B = draft;
            _approvedQualityRevisionUx7B = history.State.Revision;
            _approvedQualityCandidateFingerprintUx7B = ProfileFingerprintUx7B(candidate);
            _approvedQualitySourceFingerprintUx7B = ProfileFingerprintUx7B(history.State.Profile);
        }
        return new(true, changed, true,
            changed
                ? "Aperçu sec valide · le profil peut être appliqué."
                : "Aperçu sec valide · aucune valeur n’a changé.",
            diff, []);
    }

    internal StudioUx7BActionResult ApplyApprovedQualityFoundationUx7B(StudioUx7BProfileDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!EnsureQualityFoundationUx7B(out var failure)) return FailedActionUx7B(failure);
        var history = _qualityFoundationHistoryUx7B!;
        var current = history.State.Profile;
        var candidate = ToProfileUx7B(draft, current);
        var candidateFingerprint = ProfileFingerprintUx7B(candidate);
        var sourceFingerprint = ProfileFingerprintUx7B(current);
        if (_approvedQualityFoundationUx7B is null || _approvedQualityRevisionUx7B != history.State.Revision ||
            !string.Equals(_approvedQualityCandidateFingerprintUx7B, candidateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(_approvedQualitySourceFingerprintUx7B, sourceFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx7B(_approvedQualityFoundationUx7B),
                DraftFingerprintUx7B(draft), StringComparison.Ordinal) ||
            ValidateDraftUx7B(draft, history).Count > 0)
            return FailedActionUx7B("Application refusée · relancez l’aperçu du brouillon courant.");

        var dryRun = _setQualityFoundationUx7B.Handle(history.State,
            new SetQualityFoundationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                candidate, DryRun: true));
        if (!dryRun.Succeeded || dryRun.Patch is null || dryRun.Audit.Applied)
            return FailedActionUx7B("Application refusée · le dry-run canonique n’est plus valide.");

        var command = new SetQualityFoundationCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, candidate, DryRun: false);
        var result = _setQualityFoundationUx7B.Handle(history.State, command);
        if (!result.Succeeded || result.NewRevision is null || !result.Audit.Applied)
            return FailedActionUx7B("La commande qualité a été refusée.");

        var snapshot = CaptureQualityFileUx7B(Session!.RootDirectory);
        var saved = QualityFoundationProfileStore.Save(Session.RootDirectory, result.State.Profile);
        if (!saved.Succeeded)
            return FailedActionUx7B("Écriture refusée · " + saved.Diagnostics[0].Message);
        try
        {
            history.Record(result);
            _persistedQualityFoundationUx7B = result.State.Profile;
            _qualityUndoDepthUx7B++;
            _qualityRedoDepthUx7B = 0;
            CancelQualityFoundationApprovalUx7B();
            return new(true, "Profil qualité appliqué et enregistré atomiquement.",
                _qualityUndoDepthUx7B > 0, _qualityRedoDepthUx7B > 0);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or
                                           UnauthorizedAccessException)
        {
            RestoreQualityFileUx7B(snapshot);
            return FailedActionUx7B("Application annulée : " + exception.Message);
        }
    }

    internal StudioUx7BActionResult UndoQualityFoundationUx7B() => MutateQualityHistoryUx7B(undo: true);

    internal StudioUx7BActionResult RedoQualityFoundationUx7B() => MutateQualityHistoryUx7B(undo: false);

    internal StudioUx7BActionResult SaveQualityFoundationUx7B()
    {
        if (!EnsureQualityFoundationUx7B(out var failure)) return FailedActionUx7B(failure);
        var profile = _qualityFoundationHistoryUx7B!.State.Profile;
        var saved = QualityFoundationProfileStore.Save(Session!.RootDirectory, profile);
        if (!saved.Succeeded) return FailedActionUx7B("Enregistrement refusé · " + saved.Diagnostics[0].Message);
        _persistedQualityFoundationUx7B = profile;
        return new(true, "Profil qualité enregistré.", _qualityUndoDepthUx7B > 0,
            _qualityRedoDepthUx7B > 0);
    }

    internal StudioUx7BRunReport RunIsolatedQualityUx7B(
        StudioUx7BProfileDraft draft,
        CancellationToken cancellationToken,
        Action<StudioUx7BRunStage>? stageCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!EnsureQualityFoundationUx7B(out var failure) || Project is null || History is null || Session is null)
            return FailedRunUx7B(failure);
        var history = _qualityFoundationHistoryUx7B!;
        var diagnostics = ValidateDraftUx7B(draft, history);
        if (diagnostics.Count > 0) return FailedRunUx7B("Run refusé · le profil courant est invalide.");

        var candidate = ToProfileUx7B(draft, history.State.Profile);
        var project = Project;
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = History.State.Revision;
        var sourceJournal = History.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceApproval = CaptureQualityFoundationApprovalUx7B();
        var sourceFile = CaptureQualityFileUx7B(Session.RootDirectory);
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-b",
            Guid.NewGuid().ToString("N"));
        var stages = new List<StudioUx7BRunStage>();
        var cancelled = false;
        var isolated = false;
        var error = string.Empty;
        _lastUx7BTemporaryReleased = false;

        try
        {
            var created = ProjectSession.Create(root, project);
            if (!created.Succeeded || created.Session is null)
                throw new InvalidOperationException("Le workspace temporaire n’a pas pu être créé.");
            isolated = !string.Equals(Path.GetFullPath(root), Path.GetFullPath(Session.RootDirectory),
                StringComparison.OrdinalIgnoreCase);
            var profileSaved = QualityFoundationProfileStore.Save(root, candidate);
            if (!profileSaved.Succeeded)
                throw new InvalidOperationException("Le profil temporaire n’a pas pu être enregistré.");

            cancelled = CheckCancelledUx7B(cancellationToken);
            if (!cancelled) AddStageUx7B(stages, RunDomainHarnessUx7B(candidate, project), stageCompleted);
            cancelled = CheckCancelledUx7B(cancellationToken);
            if (!cancelled) AddStageUx7B(stages, RunHeadlessHarnessUx7B(project), stageCompleted);
            cancelled = CheckCancelledUx7B(cancellationToken);
            if (!cancelled) AddStageUx7B(stages, RunSaveHarnessUx7B(root, project), stageCompleted);
            cancelled = CheckCancelledUx7B(cancellationToken);
            if (!cancelled) AddStageUx7B(stages, RunStructuredLogHarnessUx7B(candidate), stageCompleted);
            cancelled = cancelled || CheckCancelledUx7B(cancellationToken);

            var reportDirectory = ResolveReportDirectoryUx7B(root, candidate.Tests.ReportRelativePath);
            Directory.CreateDirectory(reportDirectory);
            var reportPath = Path.Combine(reportDirectory, "ux7-b-run.json");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                cancelled,
                isolated,
                stages = stages.Select(stage => new
                {
                    stage.Route,
                    stage.Key,
                    stage.Passed,
                    stage.Summary,
                    stage.DurationMilliseconds,
                }),
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }), new UTF8Encoding(false));
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or JsonException or
                                           NotSupportedException)
        {
            error = exception.Message;
        }
        finally
        {
            _lastUx7BTemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }

        var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                              ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
                              CaptureQualityFoundationApprovalUx7B() == sourceApproval &&
                              QualityFileMatchesUx7B(sourceFile);
        var complete = !cancelled && string.IsNullOrEmpty(error) && isolated && stages.Count == 4 &&
                       stages.All(stage => stage.Passed) && sourceUntouched && _lastUx7BTemporaryReleased;
        var fingerprint = HashTextUx7B(JsonSerializer.Serialize(stages.Select(stage => new
        {
            stage.Key,
            stage.Passed,
            stage.Summary,
            stage.Details,
        })));
        return new(complete, cancelled, isolated, sourceUntouched, _lastUx7BTemporaryReleased,
            fingerprint, stages,
            cancelled
                ? "Run annulé proprement · aucun résultat partiel n’a été appliqué."
                : complete
                    ? "Quatre contrôles isolés terminés · résultats consultables par famille."
                    : "Run terminé avec échec · " + (string.IsNullOrEmpty(error)
                        ? "consultez les résultats."
                        : error));
    }

    internal StudioUx7BApprovalSnapshot CaptureQualityFoundationApprovalUx7B() => new(
        _approvedQualityFoundationUx7B, _approvedQualityRevisionUx7B,
        _approvedQualityCandidateFingerprintUx7B, _approvedQualitySourceFingerprintUx7B);

    internal void RestoreQualityFoundationApprovalUx7B(StudioUx7BApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedQualityFoundationUx7B = snapshot.Draft;
        _approvedQualityRevisionUx7B = snapshot.Revision;
        _approvedQualityCandidateFingerprintUx7B = snapshot.CandidateFingerprint;
        _approvedQualitySourceFingerprintUx7B = snapshot.SourceFingerprint;
    }

    internal void CancelQualityFoundationApprovalUx7B()
    {
        _approvedQualityFoundationUx7B = null;
        _approvedQualityRevisionUx7B = null;
        _approvedQualityCandidateFingerprintUx7B = null;
        _approvedQualitySourceFingerprintUx7B = null;
    }

    internal void CloseUx7B()
    {
        _qualityFoundationHistoryUx7B = null;
        _persistedQualityFoundationUx7B = null;
        _qualityUndoDepthUx7B = 0;
        _qualityRedoDepthUx7B = 0;
        CancelQualityFoundationApprovalUx7B();
    }

    internal StudioUx7BSmokeResult RunUx7BContractSmoke()
    {
        if (Project is null || History is null || Session is null) return FailedSmokeUx7B();
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceRevision = History.State.Revision;
        var sourceJournal = History.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceApproval = CaptureQualityFoundationApprovalUx7B();
        var sourceFile = CaptureQualityFileUx7B(Session.RootDirectory);
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-b-contract",
            Guid.NewGuid().ToString("N"));
        var released = false;
        var result = FailedSmokeUx7B();
        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || created.Session is null) return result;
            var sourceProfile = QualityFoundationProfileStore.Load(Session.RootDirectory,
                sourceProject.Id.Value).Profile;
            if (!QualityFoundationProfileStore.Save(root, sourceProfile).Succeeded) return result;
            var canonicalSource = QualityFoundationProfileStore.Load(root, sourceProject.Id.Value);
            if (!canonicalSource.Succeeded) return result;
            var editing = new StudioSessionController();
            try
            {
                if (!editing.Open(root).Succeeded) return result;
                var draft = editing.CurrentQualityFoundationUx7B();
                if (draft is null) return result;
                var explicitSelection = draft.Id != Guid.Empty;
                var invalid = editing.PreviewQualityFoundationUx7B(draft with
                {
                    SaveRoundTrip = false,
                });
                var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == QualityFoundationProfileValidator.SaveCoverageInvalidCode);
                var staleSource = !editing.PreviewQualityFoundationUx7B(draft with
                {
                    SourceFingerprint = "source-périmée",
                }).Succeeded;
                var candidate = draft with
                {
                    DomainAreas = NormalizeStringsUx7B(draft.DomainAreas.Append("studio")),
                    HeadlessScenarios = NormalizeStringsUx7B(draft.HeadlessScenarios.Append("studio-headless")),
                    LogCategories = NormalizeStringsUx7B(draft.LogCategories.Append("studio")),
                    RetentionDays = Math.Min(365, draft.RetentionDays + 1),
                };
                var run = editing.RunIsolatedQualityUx7B(candidate, CancellationToken.None);
                using var cancellation = new CancellationTokenSource();
                var cancelled = editing.RunIsolatedQualityUx7B(candidate, cancellation.Token,
                    stage =>
                    {
                        if (stage.Key == "domain") cancellation.Cancel();
                    });
                var cancellationReady = cancelled.Cancelled && cancelled.Stages.Count == 1 &&
                                        cancelled.TemporaryWorkspaceReleased;
                var preview = editing.PreviewQualityFoundationUx7B(candidate);
                var approval = editing.CaptureQualityFoundationApprovalUx7B();
                var applied = preview.Succeeded && preview.Changed && preview.CanonicalDryRun &&
                              editing.ApplyApprovedQualityFoundationUx7B(candidate).Succeeded;
                var afterApply = editing.CurrentQualityFoundationUx7B();
                var undone = applied && editing.UndoQualityFoundationUx7B().Succeeded;
                var redone = undone && editing.RedoQualityFoundationUx7B().Succeeded;
                editing.RestoreQualityFoundationApprovalUx7B(approval);
                var staleApproval = !editing.ApplyApprovedQualityFoundationUx7B(candidate).Succeeded;
                var saved = editing.SaveQualityFoundationUx7B().Succeeded;
                var loaded = QualityFoundationProfileStore.Load(root, sourceProject.Id.Value);
                var persistence = saved && loaded.Succeeded && afterApply is not null &&
                                  string.Equals(ProfileFingerprintUx7B(loaded.Profile),
                                      ProfileFingerprintUx7B(ToProfileUx7B(afterApply,
                                          loaded.Profile)), StringComparison.Ordinal);
                var licensesPreserved = JsonSerializer.Serialize(canonicalSource.Profile.Licenses) ==
                                        JsonSerializer.Serialize(loaded.Profile.Licenses);
                var validationPreserved = JsonSerializer.Serialize(canonicalSource.Profile.Validation) ==
                                          JsonSerializer.Serialize(loaded.Profile.Validation);
                var navigation = run.Stages.Count == 4 && run.Stages.All(stage => stage.Navigable) &&
                                 run.Stages.Select(stage => stage.Route).SequenceEqual(
                                     new[] { "ORF-QA-008", "ORF-QA-010", "ORF-QA-015", "ORF-QA-020" });
                result = new(
                    Succeeded: run.Succeeded && cancellationReady && preview.Succeeded && applied && undone &&
                               redone && staleApproval && persistence && licensesPreserved &&
                               validationPreserved && navigation,
                    FourRoutes: run.Stages.Count == 4,
                    ExplicitSelection: explicitSelection,
                    DomainHarness: run.Stages.Any(stage => stage.Key == "domain" && stage.Passed),
                    HeadlessRuntime: run.Stages.Any(stage => stage.Key == "headless" && stage.Passed),
                    SaveCoverage: run.Stages.Any(stage => stage.Key == "saves" && stage.Passed),
                    StructuredLogs: run.Stages.Any(stage => stage.Key == "logs" && stage.Passed),
                    Cancellation: cancellationReady,
                    Navigation: navigation,
                    CanonicalDryRun: preview.CanonicalDryRun,
                    InvalidProfileBlocked: invalidBlocked,
                    StaleSourceBlocked: staleSource,
                    StaleApprovalBlocked: staleApproval,
                    ApplyUndoRedo: applied && undone && redone,
                    Persistence: persistence,
                    LicenseTailPreserved: licensesPreserved,
                    ValidationTailPreserved: validationPreserved,
                    SourcePreserved: false,
                    TemporaryWorkspaceReleased: false);
            }
            finally
            {
                editing.Close();
            }
        }
        finally
        {
            released = DeleteTemporaryBlueprintUx3A(root);
            var sourcePreserved = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty &&
                                  CaptureQualityFoundationApprovalUx7B() == sourceApproval &&
                                  QualityFileMatchesUx7B(sourceFile);
            result = result with
            {
                SourcePreserved = sourcePreserved,
                TemporaryWorkspaceReleased = released,
                Succeeded = result.Succeeded && sourcePreserved && released,
            };
            _lastUx7BTemporaryReleased = released;
        }
        return result;
    }

    private bool EnsureQualityFoundationUx7B(out string failure)
    {
        failure = string.Empty;
        if (Session is null || Project is null)
        {
            failure = "Ouvrez un module avant d’utiliser le laboratoire qualité.";
            return false;
        }
        if (_qualityFoundationHistoryUx7B is not null) return true;
        var loaded = QualityFoundationProfileStore.Load(Session.RootDirectory, Project.Id.Value);
        if (!loaded.Succeeded)
        {
            failure = "Le profil qualité est illisible · " + loaded.Diagnostics[0].Message;
            return false;
        }
        _qualityFoundationHistoryUx7B = new QualityFoundationCommandHistory(loaded.Profile);
        _persistedQualityFoundationUx7B = loaded.Profile;
        _qualityUndoDepthUx7B = 0;
        _qualityRedoDepthUx7B = 0;
        return true;
    }

    private StudioUx7BActionResult MutateQualityHistoryUx7B(bool undo)
    {
        if (!EnsureQualityFoundationUx7B(out var failure)) return FailedActionUx7B(failure);
        var history = _qualityFoundationHistoryUx7B!;
        var moved = undo ? history.Undo() : history.Redo();
        if (!moved) return FailedActionUx7B(undo ? "Aucune modification à annuler." : "Aucune modification à rétablir.");
        var saved = QualityFoundationProfileStore.Save(Session!.RootDirectory, history.State.Profile);
        if (!saved.Succeeded)
        {
            if (undo) history.Redo(); else history.Undo();
            return FailedActionUx7B("Historique non enregistré · " + saved.Diagnostics[0].Message);
        }
        _persistedQualityFoundationUx7B = history.State.Profile;
        if (undo)
        {
            _qualityUndoDepthUx7B = Math.Max(0, _qualityUndoDepthUx7B - 1);
            _qualityRedoDepthUx7B++;
        }
        else
        {
            _qualityRedoDepthUx7B = Math.Max(0, _qualityRedoDepthUx7B - 1);
            _qualityUndoDepthUx7B++;
        }
        CancelQualityFoundationApprovalUx7B();
        return new(true, undo ? "Profil qualité annulé." : "Profil qualité rétabli.",
            _qualityUndoDepthUx7B > 0, _qualityRedoDepthUx7B > 0);
    }

    private static IReadOnlyList<StudioUx7BDiagnostic> ValidateDraftUx7B(
        StudioUx7BProfileDraft draft,
        QualityFoundationCommandHistory history)
    {
        var diagnostics = new List<StudioUx7BDiagnostic>();
        if (draft.Revision != history.State.Revision)
            diagnostics.Add(new(SetQualityFoundationHandler.RevisionConflictCode, "quality/revision", "error",
                "Le profil a changé depuis son chargement."));
        if (!string.Equals(draft.SourceFingerprint, ProfileFingerprintUx7B(history.State.Profile),
                StringComparison.Ordinal))
            diagnostics.Add(new("qa.foundation.source_changed", "quality/source", "error",
                "Le profil source a changé ; rechargez-le."));
        if (draft.Id != history.State.Profile.Id || draft.SchemaVersion != history.State.Profile.SchemaVersion ||
            !string.Equals(draft.Provenance, history.State.Profile.Provenance, StringComparison.Ordinal))
            diagnostics.Add(new(QualityFoundationProfileValidator.IdentityInvalidCode, "quality/identity", "error",
                "L’identité technique protégée ne peut pas être modifiée ici."));
        try
        {
            var candidate = ToProfileUx7B(draft, history.State.Profile);
            diagnostics.AddRange(QualityFoundationProfileValidator.Validate(candidate).Select(item =>
                new StudioUx7BDiagnostic(item.Code, HumanQualityPathUx7B(item.Path), "error",
                    HumanQualityMessageUx7B(item.Code, item.Message))));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           OverflowException)
        {
            diagnostics.Add(new("qa.foundation.draft_invalid", "quality/profile", "error", exception.Message));
        }
        return diagnostics;
    }

    private static StudioUx7BProfileDraft ToDraftUx7B(QualityFoundationState state)
    {
        var profile = state.Profile;
        return new(state.Revision, profile.Id, profile.SchemaVersion, profile.Provenance,
            profile.Tests.DomainAreas.ToArray(), profile.Tests.HeadlessScenarios.ToArray(),
            profile.Tests.ReportRelativePath,
            profile.Saves.RoundTrip, profile.Saves.OldVersions, profile.Saves.PartialFailure,
            profile.Saves.MissingPackage, profile.Saves.Rollback, profile.Saves.DeterministicState,
            profile.Logs.MinimumSeverity, profile.Logs.Categories.ToArray(),
            profile.Logs.IncludeObjectIds, profile.Logs.IncludeSourceLines,
            profile.Logs.IncludeTransactions, profile.Logs.PlayerPrivacy,
            profile.Logs.RetentionDays, profile.Logs.MaximumEntries,
            ProfileFingerprintUx7B(profile));
    }

    private static QualityFoundationProfile ToProfileUx7B(
        StudioUx7BProfileDraft draft,
        QualityFoundationProfile source) => source with
        {
            Tests = source.Tests with
            {
                DomainAreas = NormalizeStringsUx7B(draft.DomainAreas),
                HeadlessScenarios = NormalizeStringsUx7B(draft.HeadlessScenarios),
                ReportRelativePath = draft.ReportRelativePath.Trim().Replace('\\', '/'),
            },
            Saves = new SaveVerificationPolicy(draft.SaveRoundTrip, draft.SaveOldVersions,
            draft.SavePartialFailure, draft.SaveMissingPackage, draft.SaveRollback,
            draft.SaveDeterministicState),
            Logs = new StructuredLogPolicy(draft.MinimumSeverity,
            NormalizeStringsUx7B(draft.LogCategories), draft.IncludeObjectIds,
            draft.IncludeSourceLines, draft.IncludeTransactions, draft.PlayerPrivacy,
            draft.RetentionDays, draft.MaximumEntries),
        };

    private static StudioUx7BRunStage RunDomainHarnessUx7B(
        QualityFoundationProfile profile,
        ProjectDefinition project)
    {
        var stopwatch = Stopwatch.StartNew();
        var valid = QualityFoundationProfileValidator.Validate(profile);
        var invalid = QualityFoundationProfileValidator.Validate(profile with
        {
            Tests = profile.Tests with { DomainAreas = [] },
        });
        var projectDiagnostics = ProjectValidator.Validate(project);
        var passed = valid.Count == 0 && invalid.Any(item =>
            item.Code == QualityFoundationProfileValidator.TestInventoryInvalidCode);
        stopwatch.Stop();
        return new("domain", "ORF-QA-008", "Contrats unitaires du domaine", passed,
            passed ? "Profil valide et cas négatif détecté." : "Le harness de contrats a échoué.",
            [
                $"{profile.Tests.DomainAreas.Count} familles déclarées",
                $"{projectDiagnostics.Count} diagnostic(s) projet observé(s), sans mutation",
                "Harness intégré ciblé · ce bouton ne remplace pas le runner xUnit du portail",
            ], stopwatch.ElapsedMilliseconds, true);
    }

    private static StudioUx7BRunStage RunHeadlessHarnessUx7B(ProjectDefinition project)
    {
        var stopwatch = Stopwatch.StartNew();
        var profile = GameplaySessionProfile.CreateDefault(project.Module.Id).Replay;
        var frames = Enumerable.Range(0, 4).Select(index =>
        {
            var state = ReplayStateUx7B(index, $"input-{index}", $"event-{index}", 41 + index);
            return new ReplayFrame(index, $"input-{index}", $"event-{index}", 41 + index,
                GameplaySessionServices.HashState(state));
        }).ToArray();
        var recording = GameplaySessionServices.Record(profile, StableUx7B(project.Module.Id.Value,
            "headless-recording"), frames, "studio:ux7-b:headless");
        var report = GameplaySessionServices.ReplayHeadless(profile, recording,
            frame => ReplayStateUx7B((int)frame.Tick, frame.Input, frame.Event, frame.Seed));
        stopwatch.Stop();
        return new("headless", "ORF-QA-010", "Replay runtime headless", report.Succeeded,
            report.Succeeded
                ? $"{report.FrameCount} frames rejouées sans divergence."
                : $"Divergence au tick {report.Divergence?.Tick}.",
            [
                "Entrées, événements, seeds et empreintes d’état réellement comparés",
                "Aucune fenêtre Godot, physique, audio ou session réseau lancée",
                $"Empreinte du replay · {report.RecordingHash[..12]}…",
            ], stopwatch.ElapsedMilliseconds, true);
    }

    private static StudioUx7BRunStage RunSaveHarnessUx7B(string root, ProjectDefinition project)
    {
        var stopwatch = Stopwatch.StartNew();
        var directory = Path.Combine(root, ".forge", "quality", "save-harness");
        var path = Path.Combine(directory, "roundtrip.orpsave");
        var save = CreateRuntimeSaveUx7B(project);
        var stored = RuntimeSaveStore.Save(path, save);
        var loaded = RuntimeSaveStore.Load(path, save.ModuleId, save.ModuleContentSha256);
        var roundTrip = stored.Succeeded && loaded.Succeeded && loaded.Save is not null &&
                        JsonSerializer.Serialize(save) == JsonSerializer.Serialize(loaded.Save);
        var firstBytes = File.Exists(path) ? File.ReadAllBytes(path) : [];
        var deterministic = RuntimeSaveStore.Save(path, save).Succeeded &&
                            firstBytes.SequenceEqual(File.ReadAllBytes(path));
        var legacyPath = Path.Combine(directory, "legacy.orpsave");
        var legacy = save with
        {
            FormatVersion = RuntimeSaveDocument.PreRuntimeExtensionsFormatVersion,
            EntitySnapshots = null,
        };
        var oldVersions = RuntimeSaveStore.Save(legacyPath, legacy).Succeeded &&
                          RuntimeSaveStore.Load(legacyPath, save.ModuleId, save.ModuleContentSha256)
                              is { Succeeded: true, Save.FormatVersion: RuntimeSaveDocument.CurrentFormatVersion };
        var missing = RuntimeSaveStore.Load(Path.Combine(directory, "missing.orpsave"),
            save.ModuleId, save.ModuleContentSha256);
        var missingPackage = !missing.Succeeded && missing.Diagnostics.Any(diagnostic =>
            diagnostic.Code == RuntimeSaveStore.FileMissingCode);
        var rollback = false;
        var partialFailure = false;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = RuntimeSaveStore.Save(path, save with { Inventory = ["quality-token", "second"] });
            partialFailure = !failed.Succeeded && failed.Diagnostics.Any(diagnostic =>
                diagnostic.Code == RuntimeSaveStore.SaveFailedCode);
        }
        var restored = RuntimeSaveStore.Load(path, save.ModuleId, save.ModuleContentSha256);
        rollback = restored.Succeeded && restored.Save?.Inventory.SequenceEqual(save.Inventory) == true &&
                   !Directory.EnumerateFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly).Any();
        var passed = roundTrip && oldVersions && partialFailure && missingPackage && rollback && deterministic;
        stopwatch.Stop();
        return new("saves", "ORF-QA-015", "Sauvegardes runtime", passed,
            passed ? "Six garanties exercées sur des fichiers temporaires." : "Une garantie de sauvegarde a échoué.",
            [
                $"Round-trip={(roundTrip ? "ok" : "échec")} · ancienne version={(oldVersions ? "ok" : "échec")}",
                $"Écriture partielle={(partialFailure ? "bloquée" : "échec")} · fichier manquant={(missingPackage ? "détecté" : "échec")}",
                $"Rollback={(rollback ? "préservé" : "échec")} · octets déterministes={(deterministic ? "oui" : "non")}",
            ], stopwatch.ElapsedMilliseconds, true);
    }

    private static StudioUx7BRunStage RunStructuredLogHarnessUx7B(QualityFoundationProfile profile)
    {
        var stopwatch = Stopwatch.StartNew();
        const string rawPlayer = "player@example.invalid";
        var player = profile.Logs.PlayerPrivacy == PlayerPrivacyMode.Redact
            ? "[redacted]"
            : HashTextUx7B(rawPlayer)[..16];
        var category = profile.Logs.Categories.First();
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestamp"] = "2026-08-21T12:00:00Z",
            ["severity"] = profile.Logs.MinimumSeverity.ToString(),
            ["category"] = category,
            ["message"] = "quality harness completed",
            ["player"] = player,
        };
        if (profile.Logs.IncludeObjectIds) payload["objectId"] = "quality-profile";
        if (profile.Logs.IncludeSourceLines) payload["source"] = "quality/run:1";
        if (profile.Logs.IncludeTransactions) payload["transaction"] = "quality-run";
        var json = JsonSerializer.Serialize(payload);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var passed = profile.Logs.Categories.Contains(category, StringComparer.Ordinal) &&
                     root.TryGetProperty("severity", out _) && root.TryGetProperty("category", out _) &&
                     root.TryGetProperty("objectId", out _) && root.TryGetProperty("source", out _) &&
                     root.TryGetProperty("transaction", out _) && !json.Contains(rawPlayer, StringComparison.Ordinal) &&
                     profile.Logs.RetentionDays is >= 1 and <= 365 &&
                     profile.Logs.MaximumEntries is >= 100 and <= 1_000_000;
        stopwatch.Stop();
        return new("logs", "ORF-QA-020", "Journal structuré", passed,
            passed ? "Champs structurés et confidentialité vérifiés." : "Le journal structuré est incomplet.",
            [
                $"Sévérité minimale · {profile.Logs.MinimumSeverity}",
                $"Catégorie · {category} · rétention {profile.Logs.RetentionDays} jours",
                $"Joueur · {(profile.Logs.PlayerPrivacy == PlayerPrivacyMode.Redact ? "masqué" : "haché")}",
                "Échantillon en mémoire uniquement · aucun journal de joueur n’est écrit",
            ], stopwatch.ElapsedMilliseconds, true);
    }

    private static RuntimeSaveDocument CreateRuntimeSaveUx7B(ProjectDefinition project)
    {
        var moduleId = project.Module.Id.Value;
        var areaId = project.Areas.FirstOrDefault()?.Id.Value ?? StableUx7B(moduleId, "save-area");
        var playerId = StableUx7B(moduleId, "save-player");
        var hostileId = StableUx7B(moduleId, "save-hostile");
        var contentHash = HashTextUx7B(ProjectJsonSerializer.Serialize(project));
        return new(RuntimeSaveDocument.CurrentFormatVersion, moduleId.ToString("D"), contentHash,
            areaId.ToString("D"), new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            new RuntimeVector3(1, 2, 3), new RuntimeCombatantSave(playerId.ToString("D"), 10, 5),
            ["quality-token"], [new RuntimeKeyValue("quality.completed", "true")], null, null,
            [new RuntimeCombatantSave(hostileId.ToString("D"), 3, 1)],
            new RuntimeProgressFlags(false, false, false, false, false), null,
            new RuntimeProgressionSave(1, 120, 2),
            [new RuntimeEntityStateSave(playerId.ToString("D"), 1,
                [new RuntimeStateComponentSave("transform", "{\"x\":1,\"y\":2,\"z\":3}")],
                [], ["quality-token"], [new RuntimeKeyValue("quality.completed", "true")])],
            new RuntimeWorldClockSave(1, 720, false, 1, "studio:ux7-b"),
            QuestStates: []);
    }

    private static IReadOnlyList<string> BuildSemanticDiffUx7B(
        QualityFoundationProfile source,
        QualityFoundationProfile candidate)
    {
        var diff = new List<string>();
        if (!source.Tests.DomainAreas.SequenceEqual(candidate.Tests.DomainAreas))
            diff.Add($"Domaines testés · {source.Tests.DomainAreas.Count} → {candidate.Tests.DomainAreas.Count}");
        if (!source.Tests.HeadlessScenarios.SequenceEqual(candidate.Tests.HeadlessScenarios))
            diff.Add($"Scénarios headless · {source.Tests.HeadlessScenarios.Count} → {candidate.Tests.HeadlessScenarios.Count}");
        if (!string.Equals(source.Tests.ReportRelativePath, candidate.Tests.ReportRelativePath,
                StringComparison.Ordinal))
            diff.Add($"Dossier des rapports · {source.Tests.ReportRelativePath} → {candidate.Tests.ReportRelativePath}");
        if (source.Saves != candidate.Saves) diff.Add("Couverture sauvegarde · politique modifiée");
        if (source.Logs.MinimumSeverity != candidate.Logs.MinimumSeverity)
            diff.Add($"Sévérité des logs · {source.Logs.MinimumSeverity} → {candidate.Logs.MinimumSeverity}");
        if (!source.Logs.Categories.SequenceEqual(candidate.Logs.Categories))
            diff.Add($"Catégories de logs · {source.Logs.Categories.Count} → {candidate.Logs.Categories.Count}");
        if (source.Logs.PlayerPrivacy != candidate.Logs.PlayerPrivacy)
            diff.Add($"Confidentialité · {source.Logs.PlayerPrivacy} → {candidate.Logs.PlayerPrivacy}");
        if (source.Logs.RetentionDays != candidate.Logs.RetentionDays ||
            source.Logs.MaximumEntries != candidate.Logs.MaximumEntries)
            diff.Add($"Conservation · {candidate.Logs.RetentionDays} jours / {candidate.Logs.MaximumEntries} entrées");
        return diff;
    }

    private static StudioUx7BProfileDraft ToRefreshedDraftUx7B(
        StudioUx7BProfileDraft draft,
        QualityFoundationState state) => ToDraftUx7B(state) with
        {
            DomainAreas = draft.DomainAreas,
            HeadlessScenarios = draft.HeadlessScenarios,
            ReportRelativePath = draft.ReportRelativePath,
        };

    private static string ProfileFingerprintUx7B(QualityFoundationProfile profile)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(profile.Id.ToByteArray());
        writer.Write(profile.SchemaVersion);
        WriteStringUx7B(writer, profile.Provenance);
        WriteStringsUx7B(writer, profile.Licenses.ApprovedSpdxIdentifiers);
        WriteStringsUx7B(writer, profile.Licenses.RequiredReviewerRoles);
        writer.Write(profile.Licenses.RequireProvenance);
        writer.Write(profile.Licenses.GenerateNotice);
        writer.Write(profile.Licenses.BlockUnknowns);
        WriteStringsUx7B(writer, profile.Tests.DomainAreas);
        WriteStringsUx7B(writer, profile.Tests.HeadlessScenarios);
        WriteStringUx7B(writer, profile.Tests.ReportRelativePath);
        writer.Write(profile.Saves.RoundTrip);
        writer.Write(profile.Saves.OldVersions);
        writer.Write(profile.Saves.PartialFailure);
        writer.Write(profile.Saves.MissingPackage);
        writer.Write(profile.Saves.Rollback);
        writer.Write(profile.Saves.DeterministicState);
        writer.Write((int)profile.Logs.MinimumSeverity);
        WriteStringsUx7B(writer, profile.Logs.Categories);
        writer.Write(profile.Logs.IncludeObjectIds);
        writer.Write(profile.Logs.IncludeSourceLines);
        writer.Write(profile.Logs.IncludeTransactions);
        writer.Write((int)profile.Logs.PlayerPrivacy);
        writer.Write(profile.Logs.RetentionDays);
        writer.Write(profile.Logs.MaximumEntries);
        WriteStringUx7B(writer, JsonSerializer.Serialize(profile.Validation));
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static string DraftFingerprintUx7B(StudioUx7BProfileDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(draft.Revision.Value);
        writer.Write(draft.Id.ToByteArray());
        writer.Write(draft.SchemaVersion);
        WriteStringUx7B(writer, draft.Provenance);
        WriteStringsUx7B(writer, draft.DomainAreas);
        WriteStringsUx7B(writer, draft.HeadlessScenarios);
        WriteStringUx7B(writer, draft.ReportRelativePath);
        writer.Write(draft.SaveRoundTrip);
        writer.Write(draft.SaveOldVersions);
        writer.Write(draft.SavePartialFailure);
        writer.Write(draft.SaveMissingPackage);
        writer.Write(draft.SaveRollback);
        writer.Write(draft.SaveDeterministicState);
        writer.Write((int)draft.MinimumSeverity);
        WriteStringsUx7B(writer, draft.LogCategories);
        writer.Write(draft.IncludeObjectIds);
        writer.Write(draft.IncludeSourceLines);
        writer.Write(draft.IncludeTransactions);
        writer.Write((int)draft.PlayerPrivacy);
        writer.Write(draft.RetentionDays);
        writer.Write(draft.MaximumEntries);
        WriteStringUx7B(writer, draft.SourceFingerprint);
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteStringUx7B(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteStringsUx7B(BinaryWriter writer, IEnumerable<string> values)
    {
        var materialized = values.ToArray();
        writer.Write(materialized.Length);
        foreach (var value in materialized) WriteStringUx7B(writer, value);
    }

    private static IReadOnlyList<string> NormalizeStringsUx7B(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static string ReplayStateUx7B(int tick, string input, string eventName, int seed) =>
        $"tick={tick};input={input};event={eventName};seed={seed}";

    private static Guid StableUx7B(Guid owner, string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{owner:D}:ux7-b:{suffix}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string HashTextUx7B(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool CheckCancelledUx7B(CancellationToken token)
    {
        if (!token.IsCancellationRequested) return false;
        return true;
    }

    private static void AddStageUx7B(
        ICollection<StudioUx7BRunStage> stages,
        StudioUx7BRunStage stage,
        Action<StudioUx7BRunStage>? callback)
    {
        stages.Add(stage);
        callback?.Invoke(stage);
    }

    private static string ResolveReportDirectoryUx7B(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var output = Path.GetFullPath(Path.Combine(normalizedRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!output.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Le dossier de rapport sort du workspace temporaire.");
        return output;
    }

    private static QualityFileSnapshotUx7B CaptureQualityFileUx7B(string root)
    {
        var path = Path.Combine(root, QualityFoundationProfileStore.RelativePath.Replace('/',
            Path.DirectorySeparatorChar));
        return new(path, File.Exists(path), File.Exists(path) ? File.ReadAllBytes(path) : null);
    }

    private static bool QualityFileMatchesUx7B(QualityFileSnapshotUx7B snapshot) =>
        File.Exists(snapshot.Path) == snapshot.Existed &&
        (!snapshot.Existed || snapshot.Bytes is not null &&
            File.ReadAllBytes(snapshot.Path).SequenceEqual(snapshot.Bytes));

    private static void RestoreQualityFileUx7B(QualityFileSnapshotUx7B snapshot)
    {
        try
        {
            if (snapshot.Existed && snapshot.Bytes is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(snapshot.Path)!);
                File.WriteAllBytes(snapshot.Path, snapshot.Bytes);
            }
            else if (File.Exists(snapshot.Path)) File.Delete(snapshot.Path);
        }
        catch (Exception)
        {
            // Best-effort rollback; the caller still reports a failed mutation.
        }
    }

    private StudioUx7BActionResult FailedActionUx7B(string message) => new(false, message,
        _qualityUndoDepthUx7B > 0, _qualityRedoDepthUx7B > 0);

    private static StudioUx7BPreview FailedPreviewUx7B(string message) => new(false, false, false,
        message, [], [new("qa.foundation.unavailable", "quality/profile", "error", message)]);

    private static StudioUx7BRunReport FailedRunUx7B(string message) => new(false, false, false,
        false, true, string.Empty, [], message);

    private static StudioUx7BSmokeResult FailedSmokeUx7B() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false);

    private static string HumanQualityPathUx7B(string path) => path
        .Replace("quality/foundation", "Profil qualité", StringComparison.OrdinalIgnoreCase)
        .Replace("/tests", " / Tests", StringComparison.OrdinalIgnoreCase)
        .Replace("/saves", " / Sauvegardes", StringComparison.OrdinalIgnoreCase)
        .Replace("/logs", " / Journaux", StringComparison.OrdinalIgnoreCase);

    private static string HumanQualityMessageUx7B(string code, string fallback) => code switch
    {
        QualityFoundationProfileValidator.TestInventoryInvalidCode =>
            "Les domaines obligatoires, deux scénarios headless et un dossier de rapport sûr sont requis.",
        QualityFoundationProfileValidator.SaveCoverageInvalidCode =>
            "Les six garanties de sauvegarde doivent rester couvertes.",
        QualityFoundationProfileValidator.LogPolicyInvalidCode =>
            "Les journaux exigent catégories, contexte, confidentialité et limites valides.",
        SetQualityFoundationHandler.RevisionConflictCode =>
            "Le profil a changé ; rechargez-le avant de poursuivre.",
        _ => fallback,
    };

    private sealed record QualityFileSnapshotUx7B(string Path, bool Existed, byte[]? Bytes);
}
