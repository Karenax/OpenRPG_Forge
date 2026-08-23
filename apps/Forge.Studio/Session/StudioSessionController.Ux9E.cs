using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Capacity;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private NetworkResilienceCapacityHistory? _resilienceCapacityHistoryUx9E;
    private NetworkResilienceCapacityProfile? _persistedResilienceCapacityUx9E;
    private string? _resilienceCapacityWorkspaceRootUx9E;
    private StudioUx9EResilienceCapacityDraft? _approvedResilienceCapacityUx9E;
    private string? _approvedResilienceCapacityFingerprintUx9E;
    private string? _resilienceCapacityLoadMessageUx9E;
    private bool _resilienceCapacityFileMissingUx9E;
    private StudioUx9EResilienceCapacityProbe _resilienceCapacityProbeUx9E = EmptyProbeUx9E();
    private readonly List<string> _resilienceCapacityAuditUx9E = [];
    private bool _lastUx9ETemporaryReleased = true;

    internal string? ResilienceCapacityLoadMessageUx9E => _resilienceCapacityLoadMessageUx9E;
    internal bool ResilienceCapacityFileMissingUx9E => _resilienceCapacityFileMissingUx9E;
    internal bool LastUx9ETemporaryReleased => _lastUx9ETemporaryReleased;
    internal StudioUx9EResilienceCapacityProbe ResilienceCapacityProbeUx9E => _resilienceCapacityProbeUx9E;
    internal IReadOnlyList<string> ResilienceCapacityAuditUx9E => _resilienceCapacityAuditUx9E.ToArray();
    internal NetworkResilienceCapacityProfile? CurrentResilienceCapacityUx9E =>
        EnsureResilienceCapacityUx9E() ? _resilienceCapacityHistoryUx9E!.Current : null;
    internal bool ResilienceCapacityHasUnsavedChangesUx9E =>
        EnsureResilienceCapacityUx9E() && _persistedResilienceCapacityUx9E is not null &&
        !string.Equals(ProfileFingerprintUx9E(_resilienceCapacityHistoryUx9E!.Current),
            ProfileFingerprintUx9E(_persistedResilienceCapacityUx9E), StringComparison.Ordinal);

    internal StudioUx9EResilienceCapacityDraft? CreateResilienceCapacityDraftUx9E(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureResilienceCapacityUx9E()) return null;
        var profile = _resilienceCapacityHistoryUx9E!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9E(profile),
            profile.Backup.SnapshotIntervalMinutes, profile.Backup.RetentionDays,
            profile.Capacity.MaximumPlayers, profile.Capacity.ReservedAdministratorSlots,
            profile.Capacity.ReservationSeconds);
    }

    internal StudioUx9EResilienceCapacityPreview PreviewResilienceCapacityUx9E(
        StudioUx9EResilienceCapacityDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelResilienceCapacityApprovalUx9E();
        if (!EnsureResilienceCapacityUx9E())
            return FailedResilienceCapacityPreviewUx9E(
                _resilienceCapacityLoadMessageUx9E ?? "La résilience réseau ne peut pas être chargée.");
        var current = _resilienceCapacityHistoryUx9E!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            !string.Equals(draft.SourceProfileFingerprint, ProfileFingerprintUx9E(current),
                StringComparison.Ordinal))
            return FailedResilienceCapacityPreviewUx9E(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");

        var normalized = NormalizeDraftUx9E(draft);
        var candidate = BuildCandidateUx9E(current, normalized);
        var command = new NetworkResilienceCapacityCommand(Guid.NewGuid(), 1, "studio:ux9-e",
            current.ServerRevision, "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _resilienceCapacityHistoryUx9E.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedResilienceCapacityPreviewUx9E(
                HumanResilienceCapacityDiagnosticsUx9E(dryRun.Diagnostics), dryRun.Diagnostics);

        ResilienceCapacityProbeResultUx9E probe;
        try
        {
            probe = RunResilienceCapacityProbeUx9E(candidate, applyRollback: false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException or InvalidDataException)
        {
            return FailedResilienceCapacityPreviewUx9E(
                $"La sonde locale a refusé la configuration : {exception.Message}");
        }

        _resilienceCapacityProbeUx9E = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only"
            ? Array.Empty<string>()
            : dryRun.SemanticDiff.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = probe.Rollback && probe.Capacity && changes.Length > 0;
        if (canApply)
        {
            _approvedResilienceCapacityUx9E = normalized with { };
            _approvedResilienceCapacityFingerprintUx9E =
                DraftFingerprintUx9E(_approvedResilienceCapacityUx9E);
        }
        return new(true, canApply, true, probe.Rollback, probe.Capacity,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique."
                : "Aperçu sec et sondes locales validés · aucun rollback appliqué au module réel.",
            changes.Select(HumanResilienceCapacityDiffUx9E).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9EActionResult ApplyApprovedResilienceCapacityUx9E(
        StudioUx9EResilienceCapacityDraft currentDraft)
    {
        var approved = _approvedResilienceCapacityUx9E;
        if (approved is null || _approvedResilienceCapacityFingerprintUx9E is null ||
            !EnsureResilienceCapacityUx9E())
        {
            CancelResilienceCapacityApprovalUx9E();
            return new(false, "Prévisualisez d’abord la résilience et la capacité réseau.");
        }
        var current = _resilienceCapacityHistoryUx9E!.Current;
        var normalizedCurrent = NormalizeDraftUx9E(currentDraft);
        if (!string.Equals(DraftFingerprintUx9E(normalizedCurrent),
                _approvedResilienceCapacityFingerprintUx9E, StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9E(approved), _approvedResilienceCapacityFingerprintUx9E,
                StringComparison.Ordinal) || approved.BaseServerRevision != current.ServerRevision ||
            !string.Equals(approved.SourceProfileFingerprint, ProfileFingerprintUx9E(current),
                StringComparison.Ordinal))
        {
            CancelResilienceCapacityApprovalUx9E();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var candidate = BuildCandidateUx9E(current, approved);
        var command = new NetworkResilienceCapacityCommand(Guid.NewGuid(), 1, "studio:ux9-e",
            current.ServerRevision, "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _resilienceCapacityHistoryUx9E.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelResilienceCapacityApprovalUx9E();
            return new(false, HumanResilienceCapacityDiagnosticsUx9E(dryRun.Diagnostics));
        }
        var applied = _resilienceCapacityHistoryUx9E.Execute(command with { Apply = true });
        CancelResilienceCapacityApprovalUx9E();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanResilienceCapacityDiagnosticsUx9E(applied.Diagnostics));
        _resilienceCapacityAuditUx9E.Add(applied.AuditEntry);
        return new(true, "Résilience réseau appliquée par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9EActionResult UndoResilienceCapacityUx9E()
    {
        CancelResilienceCapacityApprovalUx9E();
        if (!EnsureResilienceCapacityUx9E())
            return new(false, _resilienceCapacityLoadMessageUx9E ?? "Aucune configuration active.");
        var result = _resilienceCapacityHistoryUx9E!.Undo("studio.network.operator",
            NetworkRole.Administrator);
        if (result.Succeeded) _resilienceCapacityAuditUx9E.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau annulée." :
            HumanResilienceCapacityDiagnosticsUx9E(result.Diagnostics));
    }

    internal StudioUx9EActionResult RedoResilienceCapacityUx9E()
    {
        CancelResilienceCapacityApprovalUx9E();
        if (!EnsureResilienceCapacityUx9E())
            return new(false, _resilienceCapacityLoadMessageUx9E ?? "Aucune configuration active.");
        var result = _resilienceCapacityHistoryUx9E!.Redo("studio.network.operator",
            NetworkRole.Administrator);
        if (result.Succeeded) _resilienceCapacityAuditUx9E.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau rétablie." :
            HumanResilienceCapacityDiagnosticsUx9E(result.Diagnostics));
    }

    internal StudioUx9EActionResult SaveResilienceCapacityUx9E()
    {
        CancelResilienceCapacityApprovalUx9E();
        if (!EnsureResilienceCapacityUx9E() || Session is null)
            return new(false, _resilienceCapacityLoadMessageUx9E ?? "Aucune configuration active.");
        var result = WorkspaceNetworkResilienceCapacityStore.Save(Session.RootDirectory,
            _resilienceCapacityHistoryUx9E!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "La résilience réseau n’a pas été enregistrée.");
        _persistedResilienceCapacityUx9E = _resilienceCapacityHistoryUx9E.Current;
        _resilienceCapacityFileMissingUx9E = false;
        return new(true, "Résilience et capacité enregistrées atomiquement dans le workspace.");
    }

    internal StudioUx9EActionResult RunLocalResilienceCapacityUx9E(bool confirmed, NetworkRole role)
    {
        if (!confirmed)
        {
            _resilienceCapacityAuditUx9E.Add(
                "rejected:studio.network.resilience-capacity:confirmation-required");
            return new(false, "Confirmez explicitement la répétition locale de restauration.");
        }
        if (role != NetworkRole.Administrator)
        {
            _resilienceCapacityAuditUx9E.Add(
                "rejected:studio.network.resilience-capacity:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sonde.");
        }
        if (!EnsureResilienceCapacityUx9E())
            return new(false, _resilienceCapacityLoadMessageUx9E ?? "Aucune configuration active.");
        try
        {
            var probe = RunResilienceCapacityProbeUx9E(_resilienceCapacityHistoryUx9E!.Current,
                applyRollback: true);
            _resilienceCapacityProbeUx9E = probe.Snapshot;
            _resilienceCapacityAuditUx9E.AddRange(probe.Audit);
            var succeeded = probe.Rollback && probe.Capacity && probe.Snapshot.ResourcesReleased;
            return new(succeeded, succeeded
                ? "Sonde locale terminée · restauration répétée sur copie et file mémoire libérée."
                : "La sonde locale n’a pas validé rollback et capacité.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException or InvalidDataException)
        {
            _resilienceCapacityAuditUx9E.Add(
                $"rejected:studio.network.resilience-capacity:{exception.Message}");
            return new(false, $"La sonde locale a échoué : {exception.Message}");
        }
    }

    internal StudioUx9EApprovalSnapshot CaptureResilienceCapacityApprovalUx9E() =>
        new(_approvedResilienceCapacityUx9E, _approvedResilienceCapacityFingerprintUx9E);

    internal void RestoreResilienceCapacityApprovalUx9E(StudioUx9EApprovalSnapshot snapshot)
    {
        _approvedResilienceCapacityUx9E = snapshot.Draft;
        _approvedResilienceCapacityFingerprintUx9E = snapshot.DraftFingerprint;
    }

    internal void CancelResilienceCapacityApprovalUx9E()
    {
        _approvedResilienceCapacityUx9E = null;
        _approvedResilienceCapacityFingerprintUx9E = null;
    }

    internal void CloseUx9E()
    {
        CancelResilienceCapacityApprovalUx9E();
        _resilienceCapacityHistoryUx9E = null;
        _persistedResilienceCapacityUx9E = null;
        _resilienceCapacityWorkspaceRootUx9E = null;
        _resilienceCapacityLoadMessageUx9E = null;
        _resilienceCapacityFileMissingUx9E = false;
        _resilienceCapacityProbeUx9E = EmptyProbeUx9E();
        _resilienceCapacityAuditUx9E.Clear();
    }

    internal StudioUx9ESmokeResult RunUx9EContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null) return FailedSmokeUx9E();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureResilienceCapacityApprovalUx9E();
        var temporaryRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-e-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var released = false;
        var twoRoutes = false;
        var explicitSelection = false;
        var invalidBlocked = false;
        var permissionBlocked = false;
        var confirmationRequired = false;
        var canonicalDryRun = false;
        var canonicalCommand = false;
        var staleSource = false;
        var staleApproval = false;
        var undoRedo = false;
        var persistence = false;
        var rollbackRuntime = false;
        var capacityRuntime = false;
        var audit = false;
        try
        {
            if (!ProjectSession.Create(temporaryRoot, sourceProject).Succeeded) return FailedSmokeUx9E();
            var baseline = NetworkResilienceCapacityProfile.CreateDefault("evidence:ux9-e");
            if (!WorkspaceNetworkResilienceCapacityStore.Save(temporaryRoot, baseline).Succeeded)
                return FailedSmokeUx9E();
            editing = new StudioSessionController();
            if (!editing.Open(temporaryRoot).Succeeded) return FailedSmokeUx9E();
            var draft = editing.CreateResilienceCapacityDraftUx9E(explicitlySelected: true);
            explicitSelection = draft is not null;
            if (draft is null) return FailedSmokeUx9E();
            var fixture = draft with
            {
                SnapshotIntervalMinutes = 10,
                BackupRetentionDays = 45,
                MaximumPlayers = 3,
                ReservedAdministratorSlots = 1,
                ReservationSeconds = 20
            };

            invalidBlocked = !editing.PreviewResilienceCapacityUx9E(fixture with
            {
                ReservedAdministratorSlots = 3
            }).Succeeded;
            staleSource = !editing.PreviewResilienceCapacityUx9E(fixture with
            {
                BaseServerRevision = fixture.BaseServerRevision + 9
            }).Succeeded;
            var preview = editing.PreviewResilienceCapacityUx9E(fixture);
            canonicalDryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun;
            rollbackRuntime = preview.RollbackRuntime;
            capacityRuntime = preview.CapacityRuntime;
            staleApproval = !editing.ApplyApprovedResilienceCapacityUx9E(fixture with
            {
                ReservationSeconds = fixture.ReservationSeconds + 5
            }).Succeeded;
            preview = editing.PreviewResilienceCapacityUx9E(fixture);
            var applied = editing.ApplyApprovedResilienceCapacityUx9E(fixture);
            canonicalCommand = preview.CanApply && applied.Succeeded;
            var current = editing.CurrentResilienceCapacityUx9E;
            if (current is null) return FailedSmokeUx9E();
            twoRoutes = current.Backup.SnapshotIntervalMinutes == 10 &&
                        current.Backup.RetentionDays == 45 && current.Backup.WriteAheadLog &&
                        current.Backup.RestoreRehearsal && current.Backup.PointInTimeRecovery &&
                        current.Backup.Migrations && current.Backup.DestructiveConfirmation &&
                        current.Capacity.MaximumPlayers == 3 &&
                        current.Capacity.ReservedAdministratorSlots == 1 &&
                        current.Capacity.ReservationSeconds == 20 && current.Capacity.AdmissionQueue &&
                        current.Capacity.LoadShedding && current.Capacity.PositionVisible &&
                        current.Capacity.MetricsEnabled;

            var undone = editing.UndoResilienceCapacityUx9E();
            var afterUndo = editing.CurrentResilienceCapacityUx9E;
            var redone = editing.RedoResilienceCapacityUx9E();
            var afterRedo = editing.CurrentResilienceCapacityUx9E;
            undoRedo = undone.Succeeded && redone.Succeeded && afterUndo is not null && afterRedo is not null &&
                       afterUndo.ServerRevision == baseline.ServerRevision &&
                       afterRedo.ServerRevision == current.ServerRevision;
            var saved = editing.SaveResilienceCapacityUx9E();
            var loaded = WorkspaceNetworkResilienceCapacityStore.Load(temporaryRoot);
            var serializer = new NetworkResilienceCapacityStore();
            persistence = saved.Succeeded && loaded.Succeeded && !loaded.FileMissing &&
                          string.Equals(serializer.Serialize(afterRedo!), serializer.Serialize(loaded.Profile),
                              StringComparison.Ordinal);
            reopened = new StudioSessionController();
            persistence = persistence && reopened.Open(temporaryRoot).Succeeded &&
                          reopened.CreateResilienceCapacityDraftUx9E(explicitlySelected: true) is
                          { } reopenedDraft && reopenedDraft.BaseServerRevision == afterRedo!.ServerRevision &&
                          string.Equals(reopenedDraft.SourceProfileFingerprint,
                              ProfileFingerprintUx9E(afterRedo), StringComparison.Ordinal);

            confirmationRequired = !editing.RunLocalResilienceCapacityUx9E(false,
                NetworkRole.Administrator).Succeeded;
            permissionBlocked = !editing.RunLocalResilienceCapacityUx9E(true, NetworkRole.Player).Succeeded;
            var tested = editing.RunLocalResilienceCapacityUx9E(true, NetworkRole.Administrator);
            var probe = editing.ResilienceCapacityProbeUx9E;
            rollbackRuntime = rollbackRuntime && tested.Succeeded &&
                              probe.RollbackPreviewCode == "NET_ROLLBACK_PREVIEW" &&
                              probe.RollbackApplyCode == "NET_ROLLBACK_APPLIED" &&
                              probe.RestoredRevision == 6;
            capacityRuntime = capacityRuntime && probe.PublicCapacity == 2 &&
                              probe.ReservedAdministratorSlots == 1 && probe.FirstQueuedPosition == 1 &&
                              probe.SecondQueuedPosition == 2 && probe.PromotedCode == "NET_CAPACITY_RESERVED" &&
                              probe.LoadSheddingEnabled && probe.MetricsEnabled;
            audit = editing.ResilienceCapacityAuditUx9E.Count >= 8 && probe.AuditEntries >= 5 &&
                    probe.ResourcesReleased;
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            released = DeleteUx9ETemporaryWorkspace(temporaryRoot);
            _lastUx9ETemporaryReleased = released;
            RestoreResilienceCapacityApprovalUx9E(sourceApproval);
        }
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) && SelectedEntityId == sourceSelected &&
                              CaptureResilienceCapacityApprovalUx9E() == sourceApproval;
        var succeeded = twoRoutes && explicitSelection && invalidBlocked && permissionBlocked &&
                        confirmationRequired && canonicalDryRun && canonicalCommand && staleSource && staleApproval &&
                        undoRedo && persistence && rollbackRuntime && capacityRuntime && audit && sourceUnchanged &&
                        released;
        return new(succeeded, twoRoutes, explicitSelection, invalidBlocked, permissionBlocked,
            confirmationRequired, canonicalDryRun, canonicalCommand, staleSource, staleApproval, undoRedo,
            persistence, rollbackRuntime, capacityRuntime, audit, sourceUnchanged, released);
    }

    private bool EnsureResilienceCapacityUx9E()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _resilienceCapacityLoadMessageUx9E = "Ouvrez d’abord un module.";
            return false;
        }
        var fullRoot = Path.GetFullPath(root);
        if (_resilienceCapacityHistoryUx9E is not null &&
            string.Equals(_resilienceCapacityWorkspaceRootUx9E, fullRoot,
                StringComparison.OrdinalIgnoreCase))
            return true;
        var loaded = WorkspaceNetworkResilienceCapacityStore.Load(fullRoot);
        if (!loaded.Succeeded)
        {
            _resilienceCapacityHistoryUx9E = null;
            _persistedResilienceCapacityUx9E = null;
            _resilienceCapacityWorkspaceRootUx9E = fullRoot;
            _resilienceCapacityFileMissingUx9E = false;
            _resilienceCapacityLoadMessageUx9E =
                $"Résilience réseau invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _resilienceCapacityHistoryUx9E = new NetworkResilienceCapacityHistory(loaded.Profile);
        _persistedResilienceCapacityUx9E = loaded.Profile;
        _resilienceCapacityWorkspaceRootUx9E = fullRoot;
        _resilienceCapacityFileMissingUx9E = loaded.FileMissing;
        _resilienceCapacityLoadMessageUx9E = loaded.FileMissing
            ? "Aucune résilience réseau enregistrée · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static NetworkResilienceCapacityProfile BuildCandidateUx9E(
        NetworkResilienceCapacityProfile current, StudioUx9EResilienceCapacityDraft draft) =>
        current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-e",
            RequiredAtRuntime = true,
            Backup = current.Backup with
            {
                SnapshotIntervalMinutes = draft.SnapshotIntervalMinutes,
                WriteAheadLog = true,
                RestoreRehearsal = true,
                PointInTimeRecovery = true,
                Migrations = true,
                RetentionDays = draft.BackupRetentionDays,
                DestructiveConfirmation = true
            },
            Capacity = current.Capacity with
            {
                MaximumPlayers = draft.MaximumPlayers,
                ReservedAdministratorSlots = draft.ReservedAdministratorSlots,
                ReservationSeconds = draft.ReservationSeconds,
                AdmissionQueue = true,
                LoadShedding = true,
                PositionVisible = true,
                MetricsEnabled = true
            }
        };

    private static ResilienceCapacityProbeResultUx9E RunResilienceCapacityProbeUx9E(
        NetworkResilienceCapacityProfile profile, bool applyRollback)
    {
        var runtime = NetworkResilienceCapacityRuntimeAdapter.Build(profile);
        var audit = new List<string>();
        var actor = Guid.NewGuid();
        var snapshot = runtime.Snapshots.Save(0, 2, "{\"world\":\"studio-r124-probe\"}", actor);
        var preview = runtime.Rollback.Preview(snapshot.SnapshotId, 5, actor);
        var applied = applyRollback
            ? runtime.Rollback.Apply(snapshot.SnapshotId, 5, actor)
            : new(false, "NET_ROLLBACK_NOT_APPLIED", snapshot, 5, "preview-only");
        var rollbackReady = snapshot.WorldRevision == 2 && snapshot.Sha256.Length == 64 &&
                            preview.Code == "NET_ROLLBACK_PREVIEW" &&
                            (!applyRollback || applied is
                            {
                                Applied: true, Code: "NET_ROLLBACK_APPLIED",
                                NewWorldRevision: 6
                            });
        audit.Add($"snapshot:saved:revision={snapshot.WorldRevision}");
        audit.Add(preview.AuditEntry);
        if (applyRollback) audit.Add(applied.AuditEntry);

        var queue = runtime.PublicCapacity <= 32
            ? runtime.PublicAdmissions
            : new ConnectionQueue(2, TimeProvider.System,
                TimeSpan.FromSeconds(profile.Capacity.ReservationSeconds));
        var connected = new List<Guid>();
        for (var index = 0; index < queue.Capacity; index++)
        {
            var account = Guid.NewGuid();
            var reservation = queue.Request(account);
            if (reservation.Reservation is null || !queue.Confirm(reservation.Reservation.ReservationId))
                throw new InvalidOperationException("NET_CAPACITY_PROBE_RESERVATION_FAILED");
            connected.Add(account);
        }
        var firstQueued = queue.Request(Guid.NewGuid());
        var secondQueued = queue.Request(Guid.NewGuid());
        var promoted = queue.Disconnect(connected[0]);
        var capacityReady = runtime.PublicCapacity ==
                            profile.Capacity.MaximumPlayers - profile.Capacity.ReservedAdministratorSlots &&
                            runtime.ReservedAdministratorSlots == profile.Capacity.ReservedAdministratorSlots &&
                            firstQueued.Position == 1 && secondQueued.Position == 2 &&
                            promoted?.Code == "NET_CAPACITY_RESERVED" && profile.Capacity.AdmissionQueue &&
                            profile.Capacity.LoadShedding && profile.Capacity.PositionVisible &&
                            profile.Capacity.MetricsEnabled;
        audit.Add($"capacity:public={runtime.PublicCapacity}:reserved={runtime.ReservedAdministratorSlots}");
        audit.Add($"queue:first={firstQueued.Position}:second={secondQueued.Position}:depth={queue.QueuedCount}");

        var ready = rollbackReady && capacityReady;
        var probe = new StudioUx9EResilienceCapacityProbe(
            ready ? "Deux sondes locales validées" : "Sonde locale incomplète",
            snapshot.WorldRevision, preview.Code, applied.Code, applied.NewWorldRevision,
            runtime.PublicCapacity, runtime.ReservedAdministratorSlots, firstQueued.Position,
            secondQueued.Position, promoted?.Code ?? "—", queue.QueuedCount,
            profile.Capacity.LoadShedding, profile.Capacity.MetricsEnabled, audit.Count, true);
        return new(rollbackReady, capacityReady, probe, audit);
    }

    private static StudioUx9EResilienceCapacityDraft NormalizeDraftUx9E(
        StudioUx9EResilienceCapacityDraft draft) => draft with { };

    private static string ProfileFingerprintUx9E(NetworkResilienceCapacityProfile profile) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(new NetworkResilienceCapacityStore().Serialize(profile))));

    private static string DraftFingerprintUx9E(StudioUx9EResilienceCapacityDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    private static StudioUx9EResilienceCapacityPreview FailedResilienceCapacityPreviewUx9E(
        string message, IReadOnlyList<NetworkDiagnostic>? diagnostics = null) =>
        new(false, false, false, false, false, message, [],
            diagnostics?.Select(item => HumanResilienceCapacityDiagnosticUx9E(item.Code))
                .Distinct(StringComparer.Ordinal).ToArray() ?? [], EmptyProbeUx9E());

    private static string HumanResilienceCapacityDiagnosticsUx9E(
        IReadOnlyList<NetworkDiagnostic> diagnostics) => diagnostics.Count == 0
        ? "La commande réseau n’a produit aucun changement applicable."
        : string.Join(" · ", diagnostics.Select(item => HumanResilienceCapacityDiagnosticUx9E(item.Code))
            .Distinct(StringComparer.Ordinal));

    private static string HumanResilienceCapacityDiagnosticUx9E(string? code) => code switch
    {
        "NET_BACKUP_POLICY_INVALID" =>
            "La sauvegarde exige snapshot, WAL, répétition, migrations, PITR, rétention et confirmation.",
        "NET_CAPACITY_POLICY_INVALID" =>
            "La capacité exige un plafond cohérent, des slots réservés, une file, une position et des métriques.",
        "NET_RESILIENCE_PERMISSION_DENIED" => "La permission Administrateur est requise.",
        "NET_RESILIENCE_REVISION_STALE" => "La révision serveur a changé ; rechargez le formulaire.",
        _ => string.IsNullOrWhiteSpace(code) ? "La résilience réseau est invalide." : code
    };

    private static string HumanResilienceCapacityDiffUx9E(string value) => value switch
    {
        "backup" => "Sauvegarde et rollback serveur",
        "capacity" => "Capacité et file d’attente",
        _ => value
    };

    private static StudioUx9EResilienceCapacityProbe EmptyProbeUx9E() =>
        new("Aucune sonde exécutée", 0, "—", "—", 0, 0, 0, 0, 0, "—", 0,
            false, false, 0, true);

    private static StudioUx9ESmokeResult FailedSmokeUx9E() => new(
        Succeeded: false, TwoRoutes: false, ExplicitSelection: false, InvalidBlocked: false,
        PermissionBlocked: false, ConfirmationRequired: false, CanonicalDryRun: false,
        CanonicalCommand: false, StaleSourceBlocked: false, StaleApprovalBlocked: false,
        UndoRedo: false, Persistence: false, RollbackRuntime: false, CapacityRuntime: false,
        Audit: false, SourceUnchanged: false, TemporaryWorkspaceReleased: false);

    private static bool DeleteUx9ETemporaryWorkspace(string root)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(fullRoot)?.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!string.Equals(parent, temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith("openrpg-forge-studio-ux9-e-",
                    StringComparison.Ordinal))
                return false;
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
            return !Directory.Exists(fullRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record ResilienceCapacityProbeResultUx9E(bool Rollback, bool Capacity,
        StudioUx9EResilienceCapacityProbe Snapshot, IReadOnlyList<string> Audit);
}
