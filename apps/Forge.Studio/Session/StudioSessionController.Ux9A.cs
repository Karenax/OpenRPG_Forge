using System.Security.Cryptography;
using System.Text;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Replication;
using Forge.Network.Sessions;
using Forge.Network.World;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private NetworkFoundationHistory? _networkFoundationHistoryUx9A;
    private NetworkFoundationProfile? _persistedNetworkFoundationUx9A;
    private string? _networkFoundationWorkspaceRootUx9A;
    private StudioUx9AFoundationDraft? _approvedNetworkFoundationUx9A;
    private string? _approvedNetworkFoundationFingerprintUx9A;
    private string? _networkFoundationLoadMessageUx9A;
    private bool _networkFoundationFileMissingUx9A;
    private NetworkFoundationRuntime? _localNetworkRuntimeUx9A;
    private Guid _localNetworkConnectionUx9A;
    private StudioUx9ALiveSnapshot _localNetworkSnapshotUx9A = DisconnectedSnapshotUx9A();
    private readonly List<string> _localNetworkAuditUx9A = [];
    private bool _lastUx9ATemporaryReleased = true;

    internal string? NetworkFoundationLoadMessageUx9A => _networkFoundationLoadMessageUx9A;
    internal bool NetworkFoundationFileMissingUx9A => _networkFoundationFileMissingUx9A;
    internal bool LastUx9ATemporaryReleased => _lastUx9ATemporaryReleased;
    internal bool IsLocalNetworkConnectedUx9A => _localNetworkRuntimeUx9A is not null;
    internal StudioUx9ALiveSnapshot LocalNetworkSnapshotUx9A => _localNetworkSnapshotUx9A;
    internal IReadOnlyList<string> LocalNetworkAuditUx9A => _localNetworkAuditUx9A.ToArray();
    internal NetworkFoundationProfile? CurrentNetworkFoundationUx9A =>
        EnsureNetworkFoundationUx9A() ? _networkFoundationHistoryUx9A!.Current : null;
    internal bool NetworkFoundationHasUnsavedChangesUx9A =>
        EnsureNetworkFoundationUx9A() && _persistedNetworkFoundationUx9A is not null &&
        !string.Equals(ProfileFingerprintUx9A(_networkFoundationHistoryUx9A!.Current),
            ProfileFingerprintUx9A(_persistedNetworkFoundationUx9A), StringComparison.Ordinal);

    internal StudioUx9AFoundationDraft? CreateNetworkFoundationDraftUx9A(bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureNetworkFoundationUx9A()) return null;
        var profile = _networkFoundationHistoryUx9A!.Current;
        return new(
            profile.ServerRevision,
            ProfileFingerprintUx9A(profile),
            profile.Server.Name,
            profile.Server.Port,
            profile.Server.Capacity,
            profile.Party.MaximumMembers,
            profile.Party.Loot,
            profile.Party.Separation,
            profile.Replication.MaximumDeltaBytes,
            profile.Interest.MaximumDistance,
            profile.Interest.ByArea,
            profile.Interest.ByDistance,
            profile.Interest.ByVisibility,
            profile.Interest.ByParty,
            profile.Interest.ByOwnership,
            profile.Interest.ExplicitSubscriptions);
    }

    internal StudioUx9AFoundationPreview PreviewNetworkFoundationUx9A(StudioUx9AFoundationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelNetworkFoundationApprovalUx9A();
        if (!EnsureNetworkFoundationUx9A())
            return FailedNetworkFoundationPreviewUx9A(
                _networkFoundationLoadMessageUx9A ?? "Le socle réseau ne peut pas être chargé.");
        if (IsLocalNetworkConnectedUx9A)
            return FailedNetworkFoundationPreviewUx9A(
                "Déconnectez d’abord la session locale avant de modifier sa configuration.");

        var current = _networkFoundationHistoryUx9A!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            !string.Equals(draft.SourceProfileFingerprint, ProfileFingerprintUx9A(current),
                StringComparison.Ordinal))
            return FailedNetworkFoundationPreviewUx9A(
                "La configuration réseau a changé ; rechargez le formulaire avant l’aperçu sec.");

        var normalized = NormalizeDraftUx9A(draft);
        var candidate = BuildCandidateUx9A(current, normalized);
        var command = new NetworkFoundationCommand(
            Guid.NewGuid(), 1, "studio:ux9-a", current.ServerRevision,
            "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _networkFoundationHistoryUx9A.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedNetworkFoundationPreviewUx9A(
                HumanNetworkFoundationDiagnosticsUx9A(dryRun.Diagnostics), dryRun.Diagnostics);

        NetworkProbeUx9A probe;
        try
        {
            probe = RunNetworkFoundationProbeUx9A(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException)
        {
            return FailedNetworkFoundationPreviewUx9A($"La sonde locale a refusé la configuration : {exception.Message}");
        }

        var changes = dryRun.SemanticDiff == "revision-only"
            ? Array.Empty<string>()
            : dryRun.SemanticDiff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var runtimeReady = probe.Authoritative && probe.Party && probe.Replication && probe.Interest;
        var canApply = runtimeReady && changes.Length > 0;
        if (canApply)
        {
            _approvedNetworkFoundationUx9A = FreezeDraftUx9A(normalized);
            _approvedNetworkFoundationFingerprintUx9A = DraftFingerprintUx9A(_approvedNetworkFoundationUx9A);
        }

        return new(
            true,
            canApply,
            true,
            probe.Authoritative,
            probe.Party,
            probe.Replication,
            probe.Interest,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique."
                : "Aperçu sec et sonde locale validés · aucun port public ni transport distant ouvert.",
            changes.Select(HumanFoundationDiffUx9A).ToArray(),
            [],
            probe.Snapshot);
    }

    internal StudioUx9AActionResult ApplyApprovedNetworkFoundationUx9A(StudioUx9AFoundationDraft currentDraft)
    {
        var approved = _approvedNetworkFoundationUx9A;
        if (approved is null || _approvedNetworkFoundationFingerprintUx9A is null ||
            !EnsureNetworkFoundationUx9A())
        {
            CancelNetworkFoundationApprovalUx9A();
            return new(false, "Prévisualisez d’abord le socle réseau.");
        }
        if (IsLocalNetworkConnectedUx9A)
            return new(false, "Déconnectez la session locale avant d’appliquer une configuration.");

        var current = _networkFoundationHistoryUx9A!.Current;
        var normalizedCurrent = NormalizeDraftUx9A(currentDraft);
        if (!string.Equals(DraftFingerprintUx9A(normalizedCurrent), _approvedNetworkFoundationFingerprintUx9A,
                StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9A(approved), _approvedNetworkFoundationFingerprintUx9A,
                StringComparison.Ordinal) ||
            approved.BaseServerRevision != current.ServerRevision ||
            !string.Equals(approved.SourceProfileFingerprint, ProfileFingerprintUx9A(current),
                StringComparison.Ordinal))
        {
            CancelNetworkFoundationApprovalUx9A();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }

        var candidate = BuildCandidateUx9A(current, approved);
        var command = new NetworkFoundationCommand(
            Guid.NewGuid(), 1, "studio:ux9-a", current.ServerRevision,
            "studio.network.operator", NetworkRole.Administrator, false, candidate);
        var dryRun = _networkFoundationHistoryUx9A.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelNetworkFoundationApprovalUx9A();
            return new(false, HumanNetworkFoundationDiagnosticsUx9A(dryRun.Diagnostics));
        }

        var applied = _networkFoundationHistoryUx9A.Execute(command with { Apply = true });
        CancelNetworkFoundationApprovalUx9A();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanNetworkFoundationDiagnosticsUx9A(applied.Diagnostics));
        _localNetworkAuditUx9A.Add(applied.AuditEntry);
        return new(true, "Socle réseau appliqué par la commande canonique · enregistrez pour le persister.");
    }

    internal StudioUx9AActionResult UndoNetworkFoundationUx9A()
    {
        CancelNetworkFoundationApprovalUx9A();
        if (IsLocalNetworkConnectedUx9A)
            return new(false, "Déconnectez la session locale avant d’annuler la configuration.");
        if (!EnsureNetworkFoundationUx9A())
            return new(false, _networkFoundationLoadMessageUx9A ?? "Aucun socle réseau actif.");
        var result = _networkFoundationHistoryUx9A!.Undo("studio.network.operator", NetworkRole.Administrator);
        if (result.Succeeded) _localNetworkAuditUx9A.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau annulée." : HumanNetworkFoundationDiagnosticsUx9A(result.Diagnostics));
    }

    internal StudioUx9AActionResult RedoNetworkFoundationUx9A()
    {
        CancelNetworkFoundationApprovalUx9A();
        if (IsLocalNetworkConnectedUx9A)
            return new(false, "Déconnectez la session locale avant de rétablir la configuration.");
        if (!EnsureNetworkFoundationUx9A())
            return new(false, _networkFoundationLoadMessageUx9A ?? "Aucun socle réseau actif.");
        var result = _networkFoundationHistoryUx9A!.Redo("studio.network.operator", NetworkRole.Administrator);
        if (result.Succeeded) _localNetworkAuditUx9A.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification réseau rétablie." : HumanNetworkFoundationDiagnosticsUx9A(result.Diagnostics));
    }

    internal StudioUx9AActionResult SaveNetworkFoundationUx9A()
    {
        CancelNetworkFoundationApprovalUx9A();
        if (!EnsureNetworkFoundationUx9A() || Session is null)
            return new(false, _networkFoundationLoadMessageUx9A ?? "Aucun socle réseau actif.");
        var result = WorkspaceNetworkFoundationStore.Save(
            Session.RootDirectory, _networkFoundationHistoryUx9A!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Le socle réseau n’a pas été enregistré.");
        _persistedNetworkFoundationUx9A = _networkFoundationHistoryUx9A.Current;
        _networkFoundationFileMissingUx9A = false;
        return new(true, "Socle réseau enregistré atomiquement dans le workspace.");
    }

    internal StudioUx9AActionResult StartLocalNetworkSessionUx9A(bool confirmed, NetworkRole role)
    {
        if (!confirmed)
        {
            _localNetworkAuditUx9A.Add("rejected:studio.network.local:confirmation-required");
            return new(false, "Confirmez explicitement le démarrage de la session locale.");
        }
        if (role != NetworkRole.Administrator)
        {
            _localNetworkAuditUx9A.Add("rejected:studio.network.local:permission-denied");
            return new(false, "La permission Administrateur est requise pour démarrer la session locale.");
        }
        if (!EnsureNetworkFoundationUx9A())
            return new(false, _networkFoundationLoadMessageUx9A ?? "Aucun socle réseau actif.");
        if (IsLocalNetworkConnectedUx9A)
            return new(false, "Une session locale est déjà active.");

        try
        {
            var probe = RunNetworkFoundationProbeUx9A(_networkFoundationHistoryUx9A!.Current);
            _localNetworkRuntimeUx9A = probe.Runtime;
            _localNetworkConnectionUx9A = probe.ConnectionId;
            _localNetworkSnapshotUx9A = probe.Snapshot with { Status = "Session autoritaire locale active" };
            _localNetworkAuditUx9A.AddRange(probe.Audit);
            return new(true,
                "Session autoritaire locale active sur loopback · aucun client distant ni serveur public exposé.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException)
        {
            _localNetworkRuntimeUx9A = null;
            _localNetworkConnectionUx9A = Guid.Empty;
            _localNetworkSnapshotUx9A = DisconnectedSnapshotUx9A();
            _localNetworkAuditUx9A.Add($"rejected:studio.network.local:{exception.Message}");
            return new(false, $"La session locale n’a pas démarré : {exception.Message}");
        }
    }

    internal StudioUx9AActionResult DisconnectLocalNetworkSessionUx9A()
    {
        if (_localNetworkRuntimeUx9A is null || _localNetworkConnectionUx9A == Guid.Empty)
            return new(false, "Aucune session locale n’est connectée.");
        var result = _localNetworkRuntimeUx9A.Session.Disconnect(_localNetworkConnectionUx9A);
        _localNetworkAuditUx9A.Add(result.AuditEntry);
        _localNetworkRuntimeUx9A = null;
        _localNetworkConnectionUx9A = Guid.Empty;
        _localNetworkSnapshotUx9A = DisconnectedSnapshotUx9A(_localNetworkAuditUx9A.Count);
        return new(result.Accepted,
            result.Accepted ? "Session locale déconnectée et ressources libérées." : HumanNetworkFoundationDiagnosticUx9A(result.Code));
    }

    internal StudioUx9AApprovalSnapshot CaptureNetworkFoundationApprovalUx9A() =>
        new(_approvedNetworkFoundationUx9A, _approvedNetworkFoundationFingerprintUx9A);

    internal void RestoreNetworkFoundationApprovalUx9A(StudioUx9AApprovalSnapshot snapshot)
    {
        _approvedNetworkFoundationUx9A = snapshot.Draft;
        _approvedNetworkFoundationFingerprintUx9A = snapshot.DraftFingerprint;
    }

    internal void CancelNetworkFoundationApprovalUx9A()
    {
        _approvedNetworkFoundationUx9A = null;
        _approvedNetworkFoundationFingerprintUx9A = null;
    }

    internal void CloseUx9A()
    {
        if (IsLocalNetworkConnectedUx9A) DisconnectLocalNetworkSessionUx9A();
        CancelNetworkFoundationApprovalUx9A();
        _networkFoundationHistoryUx9A = null;
        _persistedNetworkFoundationUx9A = null;
        _networkFoundationWorkspaceRootUx9A = null;
        _networkFoundationLoadMessageUx9A = null;
        _networkFoundationFileMissingUx9A = false;
        _localNetworkRuntimeUx9A = null;
        _localNetworkConnectionUx9A = Guid.Empty;
        _localNetworkSnapshotUx9A = DisconnectedSnapshotUx9A();
        _localNetworkAuditUx9A.Clear();
    }

    internal StudioUx9ASmokeResult RunUx9AContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9A();

        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureNetworkFoundationApprovalUx9A();
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux9-a-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var released = false;
        var fourRoutes = false;
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
        var authoritative = false;
        var party = false;
        var replication = false;
        var interest = false;
        var disconnected = false;
        var audit = false;
        try
        {
            if (!ProjectSession.Create(temporaryRoot, sourceProject).Succeeded) return FailedSmokeUx9A();
            var baseline = NetworkFoundationProfile.CreateDefault("evidence:ux9-a");
            if (!WorkspaceNetworkFoundationStore.Save(temporaryRoot, baseline).Succeeded)
                return FailedSmokeUx9A();

            editing = new StudioSessionController();
            if (!editing.Open(temporaryRoot).Succeeded) return FailedSmokeUx9A();
            var draft = editing.CreateNetworkFoundationDraftUx9A(explicitlySelected: true);
            explicitSelection = draft is not null;
            if (draft is null) return FailedSmokeUx9A();
            var fixture = draft with
            {
                ServerName = "Crypte des Cendres · session locale",
                ServerCapacity = 12,
                PartyCapacity = 6,
                Loot = PartyLootPolicy.RoundRobin,
                Separation = PartySeparationPolicy.Block,
                MaximumDeltaBytes = 131_072,
                MaximumDistance = 384,
                InterestByVisibility = false
            };

            invalidBlocked = !editing.PreviewNetworkFoundationUx9A(fixture with { PartyCapacity = 1 }).Succeeded;
            staleSource = !editing.PreviewNetworkFoundationUx9A(fixture with
            {
                BaseServerRevision = fixture.BaseServerRevision + 9
            }).Succeeded;
            var preview = editing.PreviewNetworkFoundationUx9A(fixture);
            canonicalDryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun;
            authoritative = preview.AuthoritativeRuntime;
            party = preview.PartyRuntime;
            replication = preview.ReplicationRuntime;
            interest = preview.InterestRuntime;
            staleApproval = !editing.ApplyApprovedNetworkFoundationUx9A(fixture with
            {
                ServerCapacity = fixture.ServerCapacity + 1
            }).Succeeded;
            preview = editing.PreviewNetworkFoundationUx9A(fixture);
            var applied = editing.ApplyApprovedNetworkFoundationUx9A(fixture);
            canonicalCommand = preview.CanApply && applied.Succeeded;
            var current = editing.CurrentNetworkFoundationUx9A;
            if (current is null) return FailedSmokeUx9A();
            fourRoutes = current.Authority.ValidateAllMutations && current.Authority.ClientsSendIntentsOnly &&
                         current.Authority.ReplicateAcceptedState && current.Authority.AuditRejectedIntents &&
                         current.Party.MaximumMembers == 6 && current.Party.Loot == PartyLootPolicy.RoundRobin &&
                         current.Replication.MaximumDeltaBytes == 131_072 &&
                         current.Interest.MaximumDistance == 384 && current.Interest.ByArea &&
                         current.Interest.ByDistance && !current.Interest.ByVisibility &&
                         current.Interest.ByParty && current.Interest.ByOwnership &&
                         current.Interest.ExplicitSubscriptions;

            var undone = editing.UndoNetworkFoundationUx9A();
            var afterUndo = editing.CurrentNetworkFoundationUx9A;
            var redone = editing.RedoNetworkFoundationUx9A();
            var afterRedo = editing.CurrentNetworkFoundationUx9A;
            undoRedo = undone.Succeeded && redone.Succeeded && afterUndo is not null && afterRedo is not null &&
                       afterUndo.ServerRevision == baseline.ServerRevision &&
                       afterRedo.ServerRevision == current.ServerRevision;
            var saved = editing.SaveNetworkFoundationUx9A();
            var loaded = WorkspaceNetworkFoundationStore.Load(temporaryRoot);
            var serializer = new NetworkFoundationStore();
            persistence = saved.Succeeded && loaded.Succeeded && !loaded.FileMissing &&
                          string.Equals(serializer.Serialize(afterRedo!), serializer.Serialize(loaded.Profile),
                              StringComparison.Ordinal);
            reopened = new StudioSessionController();
            persistence = persistence && reopened.Open(temporaryRoot).Succeeded &&
                          reopened.CreateNetworkFoundationDraftUx9A(explicitlySelected: true) is { } reopenedDraft &&
                          reopenedDraft.BaseServerRevision == afterRedo!.ServerRevision &&
                          string.Equals(reopenedDraft.SourceProfileFingerprint,
                              ProfileFingerprintUx9A(afterRedo), StringComparison.Ordinal);

            confirmationRequired = !editing.StartLocalNetworkSessionUx9A(false, NetworkRole.Administrator).Succeeded;
            permissionBlocked = !editing.StartLocalNetworkSessionUx9A(true, NetworkRole.Player).Succeeded;
            var started = editing.StartLocalNetworkSessionUx9A(true, NetworkRole.Administrator);
            var live = editing.LocalNetworkSnapshotUx9A;
            authoritative = authoritative && started.Succeeded && live.Connected &&
                            live.AuthorityDecision == "Mutation cliente interdite";
            party = party && live.PartyMembers == 2;
            replication = replication && live.ReplicatedEntities == 1 && live.DeltaCompressed;
            interest = interest && live.ReplicatedEntities == 1;
            disconnected = editing.DisconnectLocalNetworkSessionUx9A().Succeeded &&
                           !editing.IsLocalNetworkConnectedUx9A &&
                           editing.LocalNetworkSnapshotUx9A.Status == "Déconnecté";
            audit = editing.LocalNetworkAuditUx9A.Count >= 9 &&
                    editing.LocalNetworkAuditUx9A.Any(item => item.Contains("NET_DISCONNECTED", StringComparison.Ordinal));
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            released = DeleteUx9ATemporaryWorkspace(temporaryRoot);
            _lastUx9ATemporaryReleased = released;
            RestoreNetworkFoundationApprovalUx9A(sourceApproval);
        }

        var sourceUnchanged = ReferenceEquals(sourceProject, Project) && ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision && History.Journal.SequenceEqual(sourceJournal) &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) && SelectedEntityId == sourceSelected &&
                              CaptureNetworkFoundationApprovalUx9A() == sourceApproval;
        var succeeded = fourRoutes && explicitSelection && invalidBlocked && permissionBlocked && confirmationRequired &&
                        canonicalDryRun && canonicalCommand && staleSource && staleApproval && undoRedo && persistence &&
                        authoritative && party && replication && interest && disconnected && audit && sourceUnchanged &&
                        released;
        return new(succeeded, fourRoutes, explicitSelection, invalidBlocked, permissionBlocked, confirmationRequired,
            canonicalDryRun, canonicalCommand, staleSource, staleApproval, undoRedo, persistence, authoritative, party,
            replication, interest, disconnected, audit, sourceUnchanged, released);
    }

    private bool EnsureNetworkFoundationUx9A()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _networkFoundationLoadMessageUx9A = "Ouvrez d’abord un module.";
            return false;
        }

        var fullRoot = Path.GetFullPath(root);
        if (_networkFoundationHistoryUx9A is not null &&
            string.Equals(_networkFoundationWorkspaceRootUx9A, fullRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        var loaded = WorkspaceNetworkFoundationStore.Load(fullRoot);
        if (!loaded.Succeeded)
        {
            _networkFoundationHistoryUx9A = null;
            _persistedNetworkFoundationUx9A = null;
            _networkFoundationWorkspaceRootUx9A = fullRoot;
            _networkFoundationFileMissingUx9A = false;
            _networkFoundationLoadMessageUx9A =
                $"Socle réseau invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }

        _networkFoundationHistoryUx9A = new NetworkFoundationHistory(loaded.Profile);
        _persistedNetworkFoundationUx9A = loaded.Profile;
        _networkFoundationWorkspaceRootUx9A = fullRoot;
        _networkFoundationFileMissingUx9A = loaded.FileMissing;
        _networkFoundationLoadMessageUx9A = loaded.FileMissing
            ? "Aucun socle réseau enregistré · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static NetworkFoundationProfile BuildCandidateUx9A(
        NetworkFoundationProfile current, StudioUx9AFoundationDraft draft) => current with
        {
            ServerRevision = current.ServerRevision + 1,
            RequiredAtRuntime = true,
            Server = current.Server with
            {
                Name = draft.ServerName,
                BindAddress = "127.0.0.1",
                Port = draft.Port,
                Capacity = draft.ServerCapacity,
                Discovery = DiscoveryMode.Direct
            },
            Authority = current.Authority with
            {
                ValidateAllMutations = true,
                ClientsSendIntentsOnly = true,
                ReplicateAcceptedState = true,
                AuditRejectedIntents = true
            },
            Party = current.Party with
            {
                MaximumMembers = draft.PartyCapacity,
                Loot = draft.Loot,
                Separation = draft.Separation
            },
            Replication = current.Replication with { MaximumDeltaBytes = draft.MaximumDeltaBytes },
            Interest = current.Interest with
            {
                MaximumDistance = draft.MaximumDistance,
                ByArea = draft.InterestByArea,
                ByDistance = draft.InterestByDistance,
                ByVisibility = draft.InterestByVisibility,
                ByParty = draft.InterestByParty,
                ByOwnership = draft.InterestByOwnership,
                ExplicitSubscriptions = draft.ExplicitSubscriptions
            }
        };

    private static StudioUx9AFoundationDraft NormalizeDraftUx9A(StudioUx9AFoundationDraft draft) =>
        draft with { ServerName = draft.ServerName.Trim() };

    private static StudioUx9AFoundationDraft FreezeDraftUx9A(StudioUx9AFoundationDraft draft) => draft with { };

    private static string ProfileFingerprintUx9A(NetworkFoundationProfile profile) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(new NetworkFoundationStore().Serialize(profile))));

    private static string DraftFingerprintUx9A(StudioUx9AFoundationDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write(draft.BaseServerRevision);
            writer.Write(draft.SourceProfileFingerprint);
            writer.Write(draft.ServerName);
            writer.Write(draft.Port);
            writer.Write(draft.ServerCapacity);
            writer.Write(draft.PartyCapacity);
            writer.Write((int)draft.Loot);
            writer.Write((int)draft.Separation);
            writer.Write(draft.MaximumDeltaBytes);
            writer.Write(draft.MaximumDistance);
            writer.Write(draft.InterestByArea);
            writer.Write(draft.InterestByDistance);
            writer.Write(draft.InterestByVisibility);
            writer.Write(draft.InterestByParty);
            writer.Write(draft.InterestByOwnership);
            writer.Write(draft.ExplicitSubscriptions);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static NetworkProbeUx9A RunNetworkFoundationProbeUx9A(NetworkFoundationProfile profile)
    {
        var runtime = NetworkFoundationRuntimeAdapter.Build(profile);
        var connection = Guid.NewGuid();
        var account = Guid.NewGuid();
        var connect = runtime.Session.Connect(connection,
            new(account, "Opérateur local", SessionRole.Administrator),
            profile.Server.ModuleFingerprint, profile.Server.ProtocolVersion);
        var forbidden = runtime.Session.Apply(new(Guid.NewGuid(), connection, connect.Revision,
            "world.set-state", new Dictionary<string, string> { ["gold"] = "999999" }));

        var leader = Guid.NewGuid();
        var member = Guid.NewGuid();
        var party = NetworkFoundationRuntimeAdapter.CreateParty(profile, runtime.Parties, leader);
        var invitation = runtime.Parties.Invite(party.PartyId, leader, member, party.Revision,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var joined = invitation.Invitation is null
            ? new WorldDecision(false, "NET_PARTY_INVITATION_UNKNOWN", party.Revision, "rejected")
            : runtime.Parties.AcceptInvitation(invitation.Invitation.InvitationId, member,
                new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero));

        var entity = Guid.NewGuid();
        var zone = Guid.NewGuid();
        runtime.Replication.Upsert(connection,
            new(entity, zone, new(2, 0, 0), 0,
                new Dictionary<string, string> { ["transform"] = "2,0,0", ["gameplay"] = "hp=12" },
                OwnerConnectionId: connection), true);
        var observer = new ObserverScope(connection, zone, new(0, 0, 0), profile.Interest.MaximumDistance,
            new HashSet<Guid> { entity }, new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { entity });
        var delta = runtime.Replication.CreateDelta(observer, 0);
        var prediction = runtime.Replication.Predict(connection, entity, "transform", "3,0,0", 1);
        runtime.Replication.Upsert(connection,
            new(entity, zone, new(3.5f, 0, 0), 0,
                new Dictionary<string, string> { ["transform"] = "3.5,0,0", ["gameplay"] = "hp=12" },
                OwnerConnectionId: connection), true);
        var reconciled = prediction.Ticket is null
            ? prediction
            : runtime.Replication.Reconcile(prediction.Ticket.PredictionId);
        var script = runtime.Scripts.Read(runtime.Scripts.Handle);
        var partyMembers = runtime.Parties.Find(party.PartyId)?.MemberCharacterIds.Count ?? 0;
        var audit = new[]
        {
            connect.AuditEntry,
            forbidden.AuditEntry,
            invitation.AuditEntry,
            joined.AuditEntry,
            prediction.AuditEntry,
            reconciled.AuditEntry
        };
        var authoritative = connect.Accepted && !forbidden.Accepted && forbidden.Code == "NET_INTENT_FORBIDDEN" &&
                            script.Authority == NetworkAuthorityScope.Server.ToString();
        var partyReady = invitation.Accepted && joined.Accepted && partyMembers == 2;
        var replicationReady = delta.Upserts.Count == 1 && delta.Compressed && prediction.Accepted &&
                               reconciled.Accepted && reconciled.Code == "NET_PREDICTION_CORRECTED";
        var interestReady = delta.Upserts.SingleOrDefault()?.EntityId == entity;
        var snapshot = new StudioUx9ALiveSnapshot(true, "Sonde locale prête", connect.Snapshot.Members.Count,
            partyMembers, delta.Upserts.Count, delta.Compressed,
            forbidden.Code == "NET_INTENT_FORBIDDEN" ? "Mutation cliente interdite" : forbidden.Code,
            audit.Length);
        return new(runtime, connection, authoritative, partyReady, replicationReady, interestReady, snapshot, audit);
    }

    private static StudioUx9AFoundationPreview FailedNetworkFoundationPreviewUx9A(
        string message, IReadOnlyList<NetworkDiagnostic>? diagnostics = null) =>
        new(false, false, false, false, false, false, false, message, [],
            diagnostics?.Select(item => HumanNetworkFoundationDiagnosticUx9A(item.Code))
                .Distinct(StringComparer.Ordinal).ToArray() ?? [],
            DisconnectedSnapshotUx9A());

    private static string HumanNetworkFoundationDiagnosticsUx9A(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "La commande réseau n’a produit aucun changement applicable."
            : string.Join(" · ", diagnostics.Select(item => HumanNetworkFoundationDiagnosticUx9A(item.Code))
                .Distinct(StringComparer.Ordinal));

    private static string HumanNetworkFoundationDiagnosticUx9A(string? code) => code switch
    {
        "NET_PARTY_CAPACITY_INVALID" => "La taille d’un groupe doit rester comprise entre 2 et 64.",
        "NET_REPLICATION_DELTA_BUDGET_INVALID" => "Le budget de réplication doit rester entre 1 Kio et 4 Mio.",
        "NET_INTEREST_RULE_REQUIRED" => "Activez au moins une règle d’intérêt.",
        "NET_INTEREST_DISTANCE_INVALID" => "La distance d’intérêt doit rester entre 1 et 10 000 mètres.",
        "NET_PORT_INVALID" => "Le port local doit rester dans la plage autorisée.",
        "NET_CAPACITY_INVALID" => "La capacité serveur doit rester dans la plage autorisée.",
        "NET_PERMISSION_DENIED" => "La permission Administrateur est requise.",
        "NET_REVISION_STALE" => "La révision serveur a changé ; rechargez le formulaire.",
        "NET_CONNECTION_UNKNOWN" => "La connexion locale n’existe plus.",
        _ => string.IsNullOrWhiteSpace(code) ? "La configuration réseau est invalide." : code
    };

    private static string HumanFoundationDiffUx9A(string value) => value switch
    {
        "authority" => "Autorité serveur",
        "party" => "Parties et groupes",
        "replication" => "Réplication d’entités",
        "interest" => "Gestion d’intérêt",
        "dedicated-server" => "Configuration locale du serveur",
        _ => value
    };

    private static StudioUx9ALiveSnapshot DisconnectedSnapshotUx9A(int auditEntries = 0) =>
        new(false, "Déconnecté", 0, 0, 0, false, "Aucune session", auditEntries);

    private static StudioUx9ASmokeResult FailedSmokeUx9A() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false);

    private static bool DeleteUx9ATemporaryWorkspace(string root)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(fullRoot)?.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!string.Equals(parent, temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith("openrpg-forge-studio-ux9-a-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
            return !Directory.Exists(fullRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record NetworkProbeUx9A(
        NetworkFoundationRuntime Runtime,
        Guid ConnectionId,
        bool Authoritative,
        bool Party,
        bool Replication,
        bool Interest,
        StudioUx9ALiveSnapshot Snapshot,
        IReadOnlyList<string> Audit);
}
