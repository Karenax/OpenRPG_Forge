using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Observability;
using Forge.Network.Operations;
using Forge.Network.Sessions;
using Forge.Network.Storage;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private NetworkPersistenceObservabilityHistory? _persistenceObservabilityHistoryUx9D;
    private NetworkPersistenceObservabilityProfile? _persistedPersistenceObservabilityUx9D;
    private string? _persistenceObservabilityWorkspaceRootUx9D;
    private StudioUx9DPersistenceObservabilityDraft? _approvedPersistenceObservabilityUx9D;
    private string? _approvedPersistenceObservabilityFingerprintUx9D;
    private string? _persistenceObservabilityLoadMessageUx9D;
    private bool _persistenceObservabilityFileMissingUx9D;
    private StudioUx9DProbeSnapshot _persistenceObservabilityProbeUx9D = EmptyProbeUx9D();
    private readonly List<string> _persistenceObservabilityAuditUx9D = [];
    private bool _lastUx9DTemporaryReleased = true;

    internal string? PersistenceObservabilityLoadMessageUx9D => _persistenceObservabilityLoadMessageUx9D;
    internal bool PersistenceObservabilityFileMissingUx9D => _persistenceObservabilityFileMissingUx9D;
    internal bool LastUx9DTemporaryReleased => _lastUx9DTemporaryReleased;
    internal StudioUx9DProbeSnapshot PersistenceObservabilityProbeUx9D => _persistenceObservabilityProbeUx9D;
    internal IReadOnlyList<string> PersistenceObservabilityAuditUx9D =>
        _persistenceObservabilityAuditUx9D.ToArray();
    internal NetworkPersistenceObservabilityProfile? CurrentPersistenceObservabilityUx9D =>
        EnsurePersistenceObservabilityUx9D() ? _persistenceObservabilityHistoryUx9D!.Current : null;
    internal bool PersistenceObservabilityHasUnsavedChangesUx9D =>
        EnsurePersistenceObservabilityUx9D() && _persistedPersistenceObservabilityUx9D is not null &&
        !string.Equals(ProfileFingerprintUx9D(_persistenceObservabilityHistoryUx9D!.Current),
            ProfileFingerprintUx9D(_persistedPersistenceObservabilityUx9D), StringComparison.Ordinal);

    internal StudioUx9DPersistenceObservabilityDraft? CreatePersistenceObservabilityDraftUx9D(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsurePersistenceObservabilityUx9D()) return null;
        var profile = _persistenceObservabilityHistoryUx9D!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9D(profile),
            profile.Persistence.BackupRetentionDays, string.Join(", ", profile.Sql.AllowedOperationIds),
            string.Join(", ", profile.Observability.Metrics), profile.Observability.HealthEndpoint,
            profile.Observability.MetricsEndpoint, profile.Profiler.MaximumCapturedSpans,
            profile.Reconnection.GracePeriodSeconds);
    }

    internal StudioUx9DPersistenceObservabilityPreview PreviewPersistenceObservabilityUx9D(
        StudioUx9DPersistenceObservabilityDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelPersistenceObservabilityApprovalUx9D();
        if (!EnsurePersistenceObservabilityUx9D())
            return FailedPersistenceObservabilityPreviewUx9D(
                _persistenceObservabilityLoadMessageUx9D ?? "La persistance réseau ne peut pas être chargée.");
        var current = _persistenceObservabilityHistoryUx9D!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            !string.Equals(draft.SourceProfileFingerprint, ProfileFingerprintUx9D(current),
                StringComparison.Ordinal))
            return FailedPersistenceObservabilityPreviewUx9D(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");

        var normalized = NormalizeDraftUx9D(draft);
        var operations = ParseTokensUx9D(normalized.AllowedOperationIds);
        var metrics = ParseTokensUx9D(normalized.Metrics);
        if (!operations.Contains("world_flag.upsert", StringComparer.Ordinal) ||
            !operations.Contains("world_flag.by_scope", StringComparer.Ordinal) || metrics.Count < 6 ||
            !metrics.Contains("server.tps", StringComparer.Ordinal) ||
            !metrics.Contains("connections.active", StringComparer.Ordinal))
            return FailedPersistenceObservabilityPreviewUx9D(
                "Conservez les opérations world_flag et au moins six métriques, dont server.tps et connections.active.");
        var candidate = BuildCandidateUx9D(current, normalized);
        var command = new NetworkPersistenceObservabilityCommand(Guid.NewGuid(), 1, "studio:ux9-d",
            current.ServerRevision, "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _persistenceObservabilityHistoryUx9D.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedPersistenceObservabilityPreviewUx9D(
                HumanPersistenceObservabilityDiagnosticsUx9D(dryRun.Diagnostics), dryRun.Diagnostics);

        PersistenceObservabilityProbeResultUx9D probe;
        try
        {
            probe = RunPersistenceObservabilityProbeUx9D(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException or InvalidDataException)
        {
            return FailedPersistenceObservabilityPreviewUx9D(
                $"La sonde locale a refusé la configuration : {exception.Message}");
        }

        _persistenceObservabilityProbeUx9D = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only"
            ? Array.Empty<string>()
            : dryRun.SemanticDiff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var runtimeReady = probe.Persistence && probe.Sql && probe.Observability && probe.Profiler &&
                           probe.Reconnection && probe.Snapshot.ResourcesReleased;
        var canApply = runtimeReady && changes.Length > 0;
        if (canApply)
        {
            _approvedPersistenceObservabilityUx9D = normalized with { };
            _approvedPersistenceObservabilityFingerprintUx9D =
                DraftFingerprintUx9D(_approvedPersistenceObservabilityUx9D);
        }
        return new(true, canApply, true, probe.Persistence, probe.Sql, probe.Observability,
            probe.Profiler, probe.Reconnection,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique."
                : "Aperçu sec et cinq sondes locales validés · aucun exporter externe ouvert.",
            changes.Select(HumanPersistenceObservabilityDiffUx9D).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9DActionResult ApplyApprovedPersistenceObservabilityUx9D(
        StudioUx9DPersistenceObservabilityDraft currentDraft)
    {
        var approved = _approvedPersistenceObservabilityUx9D;
        if (approved is null || _approvedPersistenceObservabilityFingerprintUx9D is null ||
            !EnsurePersistenceObservabilityUx9D())
        {
            CancelPersistenceObservabilityApprovalUx9D();
            return new(false, "Prévisualisez d’abord la persistance et l’observabilité réseau.");
        }
        var current = _persistenceObservabilityHistoryUx9D!.Current;
        var normalizedCurrent = NormalizeDraftUx9D(currentDraft);
        if (!string.Equals(DraftFingerprintUx9D(normalizedCurrent),
                _approvedPersistenceObservabilityFingerprintUx9D, StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9D(approved), _approvedPersistenceObservabilityFingerprintUx9D,
                StringComparison.Ordinal) || approved.BaseServerRevision != current.ServerRevision ||
            !string.Equals(approved.SourceProfileFingerprint, ProfileFingerprintUx9D(current),
                StringComparison.Ordinal))
        {
            CancelPersistenceObservabilityApprovalUx9D();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var candidate = BuildCandidateUx9D(current, approved);
        var command = new NetworkPersistenceObservabilityCommand(Guid.NewGuid(), 1, "studio:ux9-d",
            current.ServerRevision, "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _persistenceObservabilityHistoryUx9D.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelPersistenceObservabilityApprovalUx9D();
            return new(false, HumanPersistenceObservabilityDiagnosticsUx9D(dryRun.Diagnostics));
        }
        var applied = _persistenceObservabilityHistoryUx9D.Execute(command with { Apply = true });
        CancelPersistenceObservabilityApprovalUx9D();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanPersistenceObservabilityDiagnosticsUx9D(applied.Diagnostics));
        _persistenceObservabilityAuditUx9D.Add(applied.AuditEntry);
        return new(true, "Persistance réseau appliquée par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9DActionResult UndoPersistenceObservabilityUx9D()
    {
        CancelPersistenceObservabilityApprovalUx9D();
        if (!EnsurePersistenceObservabilityUx9D())
            return new(false, _persistenceObservabilityLoadMessageUx9D ?? "Aucune configuration active.");
        var result = _persistenceObservabilityHistoryUx9D!.Undo("studio.network.operator",
            NetworkRole.Administrator);
        if (result.Succeeded) _persistenceObservabilityAuditUx9D.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau annulée." :
            HumanPersistenceObservabilityDiagnosticsUx9D(result.Diagnostics));
    }

    internal StudioUx9DActionResult RedoPersistenceObservabilityUx9D()
    {
        CancelPersistenceObservabilityApprovalUx9D();
        if (!EnsurePersistenceObservabilityUx9D())
            return new(false, _persistenceObservabilityLoadMessageUx9D ?? "Aucune configuration active.");
        var result = _persistenceObservabilityHistoryUx9D!.Redo("studio.network.operator",
            NetworkRole.Administrator);
        if (result.Succeeded) _persistenceObservabilityAuditUx9D.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau rétablie." :
            HumanPersistenceObservabilityDiagnosticsUx9D(result.Diagnostics));
    }

    internal StudioUx9DActionResult SavePersistenceObservabilityUx9D()
    {
        CancelPersistenceObservabilityApprovalUx9D();
        if (!EnsurePersistenceObservabilityUx9D() || Session is null)
            return new(false, _persistenceObservabilityLoadMessageUx9D ?? "Aucune configuration active.");
        var result = WorkspaceNetworkPersistenceObservabilityStore.Save(Session.RootDirectory,
            _persistenceObservabilityHistoryUx9D!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "La persistance réseau n’a pas été enregistrée.");
        _persistedPersistenceObservabilityUx9D = _persistenceObservabilityHistoryUx9D.Current;
        _persistenceObservabilityFileMissingUx9D = false;
        return new(true, "Persistance et observabilité enregistrées atomiquement dans le workspace.");
    }

    internal StudioUx9DActionResult RunLocalPersistenceObservabilityUx9D(bool confirmed, NetworkRole role)
    {
        if (!confirmed)
        {
            _persistenceObservabilityAuditUx9D.Add(
                "rejected:studio.network.persistence-observability:confirmation-required");
            return new(false, "Confirmez explicitement l’exécution de la sonde locale.");
        }
        if (role != NetworkRole.Administrator)
        {
            _persistenceObservabilityAuditUx9D.Add(
                "rejected:studio.network.persistence-observability:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sonde.");
        }
        if (!EnsurePersistenceObservabilityUx9D())
            return new(false, _persistenceObservabilityLoadMessageUx9D ?? "Aucune configuration active.");
        try
        {
            var probe = RunPersistenceObservabilityProbeUx9D(_persistenceObservabilityHistoryUx9D!.Current);
            _persistenceObservabilityProbeUx9D = probe.Snapshot;
            _persistenceObservabilityAuditUx9D.AddRange(probe.Audit);
            var succeeded = probe.Persistence && probe.Sql && probe.Observability && probe.Profiler &&
                            probe.Reconnection && probe.Snapshot.ResourcesReleased;
            return new(succeeded, succeeded
                ? "Sonde locale terminée · services en mémoire libérés, aucun exporter externe ouvert."
                : "La sonde locale n’a pas validé les cinq autorités.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException or InvalidDataException)
        {
            _persistenceObservabilityAuditUx9D.Add(
                $"rejected:studio.network.persistence-observability:{exception.Message}");
            return new(false, $"La sonde locale a échoué : {exception.Message}");
        }
    }

    internal StudioUx9DApprovalSnapshot CapturePersistenceObservabilityApprovalUx9D() =>
        new(_approvedPersistenceObservabilityUx9D, _approvedPersistenceObservabilityFingerprintUx9D);

    internal void RestorePersistenceObservabilityApprovalUx9D(StudioUx9DApprovalSnapshot snapshot)
    {
        _approvedPersistenceObservabilityUx9D = snapshot.Draft;
        _approvedPersistenceObservabilityFingerprintUx9D = snapshot.DraftFingerprint;
    }

    internal void CancelPersistenceObservabilityApprovalUx9D()
    {
        _approvedPersistenceObservabilityUx9D = null;
        _approvedPersistenceObservabilityFingerprintUx9D = null;
    }

    internal void CloseUx9D()
    {
        CancelPersistenceObservabilityApprovalUx9D();
        _persistenceObservabilityHistoryUx9D = null;
        _persistedPersistenceObservabilityUx9D = null;
        _persistenceObservabilityWorkspaceRootUx9D = null;
        _persistenceObservabilityLoadMessageUx9D = null;
        _persistenceObservabilityFileMissingUx9D = false;
        _persistenceObservabilityProbeUx9D = EmptyProbeUx9D();
        _persistenceObservabilityAuditUx9D.Clear();
    }

    internal StudioUx9DSmokeResult RunUx9DContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null) return FailedSmokeUx9D();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CapturePersistenceObservabilityApprovalUx9D();
        var temporaryRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-d-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var released = false;
        var fiveRoutes = false;
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
        var persistenceRuntime = false;
        var sqlRuntime = false;
        var observabilityRuntime = false;
        var profilerRuntime = false;
        var reconnectionRuntime = false;
        var audit = false;
        try
        {
            if (!ProjectSession.Create(temporaryRoot, sourceProject).Succeeded) return FailedSmokeUx9D();
            var baseline = NetworkPersistenceObservabilityProfile.CreateDefault("evidence:ux9-d");
            if (!WorkspaceNetworkPersistenceObservabilityStore.Save(temporaryRoot, baseline).Succeeded)
                return FailedSmokeUx9D();
            editing = new StudioSessionController();
            if (!editing.Open(temporaryRoot).Succeeded) return FailedSmokeUx9D();
            var draft = editing.CreatePersistenceObservabilityDraftUx9D(explicitlySelected: true);
            explicitSelection = draft is not null;
            if (draft is null) return FailedSmokeUx9D();
            var fixture = draft with
            {
                BackupRetentionDays = 60,
                AllowedOperationIds = "world_flag.upsert, world_flag.by_scope",
                Metrics = "server.tps, server.tick.ms, process.memory, connections.active, queues.pending, scripts.hotspots",
                HealthEndpoint = "/healthz",
                MetricsEndpoint = "/metricsz",
                MaximumCapturedSpans = 250_000,
                ReconnectionGraceSeconds = 180
            };

            invalidBlocked = !editing.PreviewPersistenceObservabilityUx9D(fixture with
            {
                AllowedOperationIds = "world_flag.upsert"
            }).Succeeded;
            staleSource = !editing.PreviewPersistenceObservabilityUx9D(fixture with
            {
                BaseServerRevision = fixture.BaseServerRevision + 9
            }).Succeeded;
            var preview = editing.PreviewPersistenceObservabilityUx9D(fixture);
            canonicalDryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun;
            persistenceRuntime = preview.PersistenceRuntime;
            sqlRuntime = preview.SqlRuntime;
            observabilityRuntime = preview.ObservabilityRuntime;
            profilerRuntime = preview.ProfilerRuntime;
            reconnectionRuntime = preview.ReconnectionRuntime;
            staleApproval = !editing.ApplyApprovedPersistenceObservabilityUx9D(fixture with
            {
                ReconnectionGraceSeconds = fixture.ReconnectionGraceSeconds + 1
            }).Succeeded;
            preview = editing.PreviewPersistenceObservabilityUx9D(fixture);
            var applied = editing.ApplyApprovedPersistenceObservabilityUx9D(fixture);
            canonicalCommand = preview.CanApply && applied.Succeeded;
            var current = editing.CurrentPersistenceObservabilityUx9D;
            if (current is null) return FailedSmokeUx9D();
            fiveRoutes = current.Persistence.BackupRetentionDays == 60 &&
                         current.Sql.AllowedOperationIds.SequenceEqual(
                             ["world_flag.upsert", "world_flag.by_scope"]) &&
                         current.Observability.HealthEndpoint == "/healthz" &&
                         current.Observability.MetricsEndpoint == "/metricsz" &&
                         current.Profiler.MaximumCapturedSpans == 250_000 &&
                         current.Reconnection.GracePeriodSeconds == 180;

            var undone = editing.UndoPersistenceObservabilityUx9D();
            var afterUndo = editing.CurrentPersistenceObservabilityUx9D;
            var redone = editing.RedoPersistenceObservabilityUx9D();
            var afterRedo = editing.CurrentPersistenceObservabilityUx9D;
            undoRedo = undone.Succeeded && redone.Succeeded && afterUndo is not null && afterRedo is not null &&
                       afterUndo.ServerRevision == baseline.ServerRevision &&
                       afterRedo.ServerRevision == current.ServerRevision;
            var saved = editing.SavePersistenceObservabilityUx9D();
            var loaded = WorkspaceNetworkPersistenceObservabilityStore.Load(temporaryRoot);
            var serializer = new NetworkPersistenceObservabilityStore();
            persistence = saved.Succeeded && loaded.Succeeded && !loaded.FileMissing &&
                          string.Equals(serializer.Serialize(afterRedo!), serializer.Serialize(loaded.Profile),
                              StringComparison.Ordinal);
            reopened = new StudioSessionController();
            persistence = persistence && reopened.Open(temporaryRoot).Succeeded &&
                          reopened.CreatePersistenceObservabilityDraftUx9D(explicitlySelected: true) is
                          { } reopenedDraft && reopenedDraft.BaseServerRevision == afterRedo!.ServerRevision &&
                          string.Equals(reopenedDraft.SourceProfileFingerprint, ProfileFingerprintUx9D(afterRedo),
                              StringComparison.Ordinal);

            confirmationRequired = !editing.RunLocalPersistenceObservabilityUx9D(false,
                NetworkRole.Administrator).Succeeded;
            permissionBlocked = !editing.RunLocalPersistenceObservabilityUx9D(true, NetworkRole.Player).Succeeded;
            var tested = editing.RunLocalPersistenceObservabilityUx9D(true, NetworkRole.Administrator);
            var probe = editing.PersistenceObservabilityProbeUx9D;
            persistenceRuntime = persistenceRuntime && tested.Succeeded && probe.SnapshotRevision == 1;
            sqlRuntime = sqlRuntime && probe.DataCode == "NET_DATA_APPLIED" && probe.DataRows == 1 &&
                         probe.UnlistedOperationBlocked;
            observabilityRuntime = observabilityRuntime && probe.MetricSignals == 2 &&
                                   probe.HealthStatus == HealthStatus.Healthy.ToString() &&
                                   probe.UnlistedMetricBlocked;
            profilerRuntime = profilerRuntime && probe.ProfileOperation == "server.tick" &&
                              probe.ProfileCaptures == 1;
            reconnectionRuntime = reconnectionRuntime && probe.ReconnectionCode == "NET_RECONNECT_ACCEPTED" &&
                                  probe.ReconnectionReplayBlocked;
            audit = editing.PersistenceObservabilityAuditUx9D.Count >= 8 && probe.AuditEntries >= 6 &&
                    probe.ResourcesReleased;
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            released = DeleteUx9DTemporaryWorkspace(temporaryRoot);
            _lastUx9DTemporaryReleased = released;
            RestorePersistenceObservabilityApprovalUx9D(sourceApproval);
        }
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) && SelectedEntityId == sourceSelected &&
                              CapturePersistenceObservabilityApprovalUx9D() == sourceApproval;
        var succeeded = fiveRoutes && explicitSelection && invalidBlocked && permissionBlocked &&
                        confirmationRequired && canonicalDryRun && canonicalCommand && staleSource && staleApproval &&
                        undoRedo && persistence && persistenceRuntime && sqlRuntime && observabilityRuntime &&
                        profilerRuntime && reconnectionRuntime && audit && sourceUnchanged && released;
        return new(succeeded, fiveRoutes, explicitSelection, invalidBlocked, permissionBlocked,
            confirmationRequired, canonicalDryRun, canonicalCommand, staleSource, staleApproval, undoRedo,
            persistence, persistenceRuntime, sqlRuntime, observabilityRuntime, profilerRuntime,
            reconnectionRuntime, audit, sourceUnchanged, released);
    }

    private bool EnsurePersistenceObservabilityUx9D()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _persistenceObservabilityLoadMessageUx9D = "Ouvrez d’abord un module.";
            return false;
        }
        var fullRoot = Path.GetFullPath(root);
        if (_persistenceObservabilityHistoryUx9D is not null &&
            string.Equals(_persistenceObservabilityWorkspaceRootUx9D, fullRoot,
                StringComparison.OrdinalIgnoreCase))
            return true;
        var loaded = WorkspaceNetworkPersistenceObservabilityStore.Load(fullRoot);
        if (!loaded.Succeeded)
        {
            _persistenceObservabilityHistoryUx9D = null;
            _persistedPersistenceObservabilityUx9D = null;
            _persistenceObservabilityWorkspaceRootUx9D = fullRoot;
            _persistenceObservabilityFileMissingUx9D = false;
            _persistenceObservabilityLoadMessageUx9D =
                $"Persistance réseau invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _persistenceObservabilityHistoryUx9D = new NetworkPersistenceObservabilityHistory(loaded.Profile);
        _persistedPersistenceObservabilityUx9D = loaded.Profile;
        _persistenceObservabilityWorkspaceRootUx9D = fullRoot;
        _persistenceObservabilityFileMissingUx9D = loaded.FileMissing;
        _persistenceObservabilityLoadMessageUx9D = loaded.FileMissing
            ? "Aucune persistance réseau enregistrée · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static NetworkPersistenceObservabilityProfile BuildCandidateUx9D(
        NetworkPersistenceObservabilityProfile current, StudioUx9DPersistenceObservabilityDraft draft) =>
        current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-d",
            RequiredAtRuntime = true,
            Persistence = current.Persistence with
            {
                Providers = [PersistenceProviderKind.Embedded, PersistenceProviderKind.Relational],
                Transactions = true,
                Migrations = true,
                Backups = true,
                Failover = true,
                BackupRetentionDays = draft.BackupRetentionDays
            },
            Sql = current.Sql with
            {
                ServerOnlyRepositories = true,
                ParameterizedQueries = true,
                AllowedOperationIds = ParseTokensUx9D(draft.AllowedOperationIds),
                ModuleScriptsUseDomainServices = true,
                RawSqlAllowed = false
            },
            Observability = current.Observability with
            {
                Exporters = [MetricsExporterKind.Prometheus, MetricsExporterKind.OpenTelemetry],
                Metrics = ParseTokensUx9D(draft.Metrics),
                HealthEndpoint = draft.HealthEndpoint,
                MetricsEndpoint = draft.MetricsEndpoint,
                PrivacyFiltering = true
            },
            Profiler = current.Profiler with
            {
                Sampling = true,
                Instrumentation = true,
                TraceCapture = true,
                TickBreakdown = true,
                ScriptSpans = true,
                DatabaseSpans = true,
                NetworkSpans = true,
                MaximumCapturedSpans = draft.MaximumCapturedSpans
            },
            Reconnection = current.Reconnection with
            {
                GracePeriodSeconds = draft.ReconnectionGraceSeconds,
                SessionTokens = true,
                EntityHandoff = true,
                PartyState = true,
                PendingTransactions = true,
                SafeFallback = true,
                SingleUseTokens = true
            }
        };

    private static PersistenceObservabilityProbeResultUx9D RunPersistenceObservabilityProbeUx9D(
        NetworkPersistenceObservabilityProfile profile)
    {
        var runtime = NetworkPersistenceObservabilityRuntimeAdapter.Build(profile);
        var audit = new List<string>();
        var actor = Guid.NewGuid();
        var snapshot = runtime.Snapshots.Save(0, 1, "{\"world\":\"studio-probe\"}", actor);
        var persistenceReady = snapshot.WorldRevision == 1 && snapshot.Sha256.Length == 64;
        audit.Add($"snapshot:saved:revision={snapshot.WorldRevision}");

        var scope = Guid.NewGuid();
        var parameters = new DataParameter[]
        {
            new("scope_id", DataParameterKind.Uuid, scope),
            new("key", DataParameterKind.Text, "studio.probe"),
            new("value", DataParameterKind.Text, "ready")
        };
        var applied = runtime.Data.Execute(new("world_flag.upsert", parameters, 0), actor);
        var rows = runtime.Data.Query("world_flag.by_scope",
            [new("scope_id", DataParameterKind.Uuid, scope)], actor);
        var unlisted = runtime.Data.Execute(new("raw.sql", [], applied.Revision), actor);
        var rawQueryBlocked = false;
        try
        {
            runtime.Data.Query("SELECT * FROM world_flags", [], actor);
        }
        catch (InvalidOperationException exception) when (exception.Message == "NET_DATA_OPERATION_NOT_CONFIGURED")
        {
            rawQueryBlocked = true;
        }
        var sqlReady = applied.Applied && rows.Count == 1 &&
                       unlisted.Code == "NET_DATA_OPERATION_NOT_CONFIGURED" && rawQueryBlocked;
        audit.Add(applied.AuditEntry);
        audit.Add(unlisted.AuditEntry);

        runtime.Observability.Gauge("server.tps", 60);
        runtime.Observability.Increment("connections.active", 2);
        var metricSnapshot = runtime.Observability.Snapshot();
        var health = runtime.Observability.Health();
        var unlistedMetricBlocked = false;
        try
        {
            runtime.Observability.Gauge("private.player.secret", 1);
        }
        catch (InvalidOperationException exception) when (exception.Message == "NET_METRIC_NOT_CONFIGURED")
        {
            unlistedMetricBlocked = true;
        }
        var signals = metricSnapshot.Gauges.Count + metricSnapshot.Counters.Count;
        var observabilityReady = signals == 2 && health.Status == HealthStatus.Healthy &&
                                 unlistedMetricBlocked && runtime.Observability.HealthEndpoint.StartsWith('/') &&
                                 runtime.Observability.MetricsEndpoint.StartsWith('/');
        audit.Add($"observability:health={health.Status}:signals={signals}");

        using (runtime.Profiler.Measure("server.tick")) { }
        var profileSnapshot = runtime.Profiler.Snapshot();
        var profiled = profileSnapshot.SingleOrDefault();
        var profilerReady = profiled?.Operation == "server.tick" && runtime.Profiler.Captures == 1;
        audit.Add($"profiler:captures={runtime.Profiler.Captures}");

        var account = Guid.NewGuid();
        var session = Guid.NewGuid();
        var ticket = runtime.Reconnection.Issue(account, Guid.NewGuid(), session, snapshot.WorldRevision);
        var reconnected = runtime.Reconnection.Redeem(ticket.Token, account, session);
        var replayed = runtime.Reconnection.Redeem(ticket.Token, account, session);
        var reconnectionReady = reconnected.Accepted && replayed.Code == "NET_RECONNECT_TOKEN_INVALID";
        audit.Add(reconnected.AuditEntry);
        audit.Add(replayed.AuditEntry);

        var ready = persistenceReady && sqlReady && observabilityReady && profilerReady && reconnectionReady;
        var probe = new StudioUx9DProbeSnapshot(ready ? "Cinq sondes locales validées" :
            "Sonde locale incomplète", snapshot.WorldRevision, applied.Code, rows.Count,
            unlisted.Code == "NET_DATA_OPERATION_NOT_CONFIGURED" && rawQueryBlocked, signals,
            health.Status.ToString(), unlistedMetricBlocked, profiled?.Operation ?? "—", runtime.Profiler.Captures,
            reconnected.Code, replayed.Code == "NET_RECONNECT_TOKEN_INVALID", audit.Count, true);
        return new(persistenceReady, sqlReady, observabilityReady, profilerReady, reconnectionReady, probe, audit);
    }

    private static StudioUx9DPersistenceObservabilityDraft NormalizeDraftUx9D(
        StudioUx9DPersistenceObservabilityDraft draft) => draft with
        {
            AllowedOperationIds = NormalizeTokensUx9D(draft.AllowedOperationIds),
            Metrics = NormalizeTokensUx9D(draft.Metrics),
            HealthEndpoint = draft.HealthEndpoint.Trim(),
            MetricsEndpoint = draft.MetricsEndpoint.Trim()
        };

    private static string NormalizeTokensUx9D(string value) => string.Join(", ", ParseTokensUx9D(value));
    private static IReadOnlyList<string> ParseTokensUx9D(string value) => value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray();
    private static string ProfileFingerprintUx9D(NetworkPersistenceObservabilityProfile profile) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(new NetworkPersistenceObservabilityStore().Serialize(profile))));
    private static string DraftFingerprintUx9D(StudioUx9DPersistenceObservabilityDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    private static StudioUx9DPersistenceObservabilityPreview FailedPersistenceObservabilityPreviewUx9D(
        string message, IReadOnlyList<NetworkDiagnostic>? diagnostics = null) =>
        new(false, false, false, false, false, false, false, false, message, [],
            diagnostics?.Select(item => HumanPersistenceObservabilityDiagnosticUx9D(item.Code))
                .Distinct(StringComparer.Ordinal).ToArray() ?? [], EmptyProbeUx9D());

    private static string HumanPersistenceObservabilityDiagnosticsUx9D(
        IReadOnlyList<NetworkDiagnostic> diagnostics) => diagnostics.Count == 0
        ? "La commande réseau n’a produit aucun changement applicable."
        : string.Join(" · ", diagnostics.Select(item => HumanPersistenceObservabilityDiagnosticUx9D(item.Code))
            .Distinct(StringComparer.Ordinal));

    private static string HumanPersistenceObservabilityDiagnosticUx9D(string? code) => code switch
    {
        "NET_WORLD_PERSISTENCE_POLICY_INVALID" =>
            "La persistance exige fournisseurs embedded/relationnel, transactions, migrations, backups et failover.",
        "NET_SECURE_SQL_POLICY_INVALID" =>
            "Le SQL exige dépôts serveur, paramètres, opérations nommées et interdit le SQL brut.",
        "NET_OBSERVABILITY_POLICY_INVALID" =>
            "L’observabilité exige deux exporters, six métriques, endpoints et filtrage privé.",
        "NET_SERVER_PROFILER_POLICY_INVALID" =>
            "Le profiler exige sampling, instrumentation, traces et captures bornées.",
        "NET_RECONNECTION_POLICY_INVALID" =>
            "La reconnexion exige grâce, ticket à usage unique et restauration bornée.",
        "NET_PERSIST_OBSERVE_PERMISSION_DENIED" => "La permission Administrateur est requise.",
        "NET_PERSIST_OBSERVE_REVISION_STALE" => "La révision serveur a changé ; rechargez le formulaire.",
        _ => string.IsNullOrWhiteSpace(code) ? "La configuration réseau est invalide." : code
    };

    private static string HumanPersistenceObservabilityDiffUx9D(string value) => value switch
    {
        "persistence" => "Persistance de monde",
        "sql" => "SQL sécurisé",
        "observability" => "Métriques et santé",
        "profiler" => "Profiler serveur",
        "reconnection" => "Reconnexion",
        _ => value
    };

    private static StudioUx9DProbeSnapshot EmptyProbeUx9D() =>
        new("Aucune sonde exécutée", 0, "—", 0, false, 0, "—", false, "—", 0, "—", false, 0, true);

    private static StudioUx9DSmokeResult FailedSmokeUx9D() => new(
        Succeeded: false, FiveRoutes: false, ExplicitSelection: false, InvalidBlocked: false,
        PermissionBlocked: false, ConfirmationRequired: false, CanonicalDryRun: false,
        CanonicalCommand: false, StaleSourceBlocked: false, StaleApprovalBlocked: false,
        UndoRedo: false, Persistence: false, PersistenceRuntime: false, SqlRuntime: false,
        ObservabilityRuntime: false, ProfilerRuntime: false, ReconnectionRuntime: false, Audit: false,
        SourceUnchanged: false, TemporaryWorkspaceReleased: false);

    private static bool DeleteUx9DTemporaryWorkspace(string root)
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
                !Path.GetFileName(fullRoot).StartsWith("openrpg-forge-studio-ux9-d-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
            return !Directory.Exists(fullRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record PersistenceObservabilityProbeResultUx9D(bool Persistence, bool Sql,
        bool Observability, bool Profiler, bool Reconnection, StudioUx9DProbeSnapshot Snapshot,
        IReadOnlyList<string> Audit);
}
