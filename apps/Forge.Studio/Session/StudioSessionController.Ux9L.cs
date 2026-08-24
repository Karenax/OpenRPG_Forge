using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenarios;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private LiveDirectorAccessHistory? _liveAccessHistoryUx9L;
    private LiveDirectorAccessProfile? _persistedLiveAccessUx9L;
    private string? _liveAccessWorkspaceRootUx9L;
    private StudioUx9LAccessDraft? _approvedLiveAccessUx9L;
    private string? _approvedLiveAccessFingerprintUx9L;
    private string? _liveAccessLoadMessageUx9L;
    private bool _liveAccessFileMissingUx9L;
    private StudioUx9LAccessProbe _liveAccessProbeUx9L = EmptyProbeUx9L();
    private readonly List<string> _liveAccessAuditUx9L = [];

    internal string? LiveAccessLoadMessageUx9L => _liveAccessLoadMessageUx9L;
    internal bool LiveAccessFileMissingUx9L => _liveAccessFileMissingUx9L;
    internal StudioUx9LAccessProbe LiveAccessProbeUx9L => _liveAccessProbeUx9L;
    internal IReadOnlyList<string> LiveAccessAuditUx9L => _liveAccessAuditUx9L.ToArray();
    internal LiveDirectorAccessProfile? CurrentLiveAccessUx9L =>
        EnsureLiveAccessUx9L() ? _liveAccessHistoryUx9L!.Current : null;
    internal bool LiveAccessHasUnsavedChangesUx9L =>
        EnsureLiveAccessUx9L() && _persistedLiveAccessUx9L is not null &&
        ProfileFingerprintUx9L(_liveAccessHistoryUx9L!.Current) !=
        ProfileFingerprintUx9L(_persistedLiveAccessUx9L);

    internal StudioUx9LAccessDraft? CreateLiveAccessDraftUx9L(bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureLiveAccessUx9L()) return null;
        var profile = _liveAccessHistoryUx9L!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9L(profile),
            profile.RequiredAtRuntime, profile.Authentication.SessionLifetimeMinutes,
            profile.Inspection.HistoryEntryLimit, profile.Players.RefreshSeconds,
            profile.Audit.RetentionDays, profile.Chat.MaximumMessageLength);
    }

    internal StudioUx9LAccessPreview PreviewLiveAccessUx9L(StudioUx9LAccessDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLiveAccessApprovalUx9L();
        if (!EnsureLiveAccessUx9L())
            return FailedPreviewUx9L(_liveAccessLoadMessageUx9L ??
                                     "Le profil d’accès Live Director ne peut pas être chargé.");
        var current = _liveAccessHistoryUx9L!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            draft.SourceProfileFingerprint != ProfileFingerprintUx9L(current))
            return FailedPreviewUx9L(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");
        var candidate = BuildCandidateUx9L(current, draft);
        var command = new LiveDirectorAccessCommand(Guid.NewGuid(), 1, "studio:ux9-l",
            current.ServerRevision, "studio.live.access", NetworkRole.Administrator,
            NetworkAuthorityScope.Server, false, candidate);
        var dryRun = _liveAccessHistoryUx9L.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedPreviewUx9L(HumanDiagnosticsUx9L(dryRun.Diagnostics),
                dryRun.Diagnostics);
        StudioUx9LAccessProbe probe;
        try
        {
            probe = RunLiveAccessProbeUx9L(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            return FailedPreviewUx9L(
                $"La sonde d’accès locale a refusé la configuration : {exception.Message}");
        }
        _liveAccessProbeUx9L = probe;
        var changes = dryRun.SemanticDiff == "revision-only" ? [] :
            dryRun.SemanticDiff.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = changes.Length > 0 && ProbeReadyUx9L(probe);
        if (canApply)
        {
            _approvedLiveAccessUx9L = draft with { };
            _approvedLiveAccessFingerprintUx9L = DraftFingerprintUx9L(draft);
        }
        return new(true, canApply, true, ProbeReadyUx9L(probe),
            canApply
                ? "Aperçu sec validé · aucun compte, joueur, message ou serveur réel n’a été modifié."
                : "Aperçu sec sans changement · ajustez au moins une politique d’accès.",
            changes.Select(HumanDiffUx9L).ToArray(), [], probe);
    }

    internal StudioUx9LActionResult ApplyApprovedLiveAccessUx9L(
        StudioUx9LAccessDraft draft)
    {
        var approved = _approvedLiveAccessUx9L;
        if (approved is null || _approvedLiveAccessFingerprintUx9L is null ||
            !EnsureLiveAccessUx9L())
        {
            CancelLiveAccessApprovalUx9L();
            return new(false, "Prévisualisez d’abord les politiques d’accès.");
        }
        var current = _liveAccessHistoryUx9L!.Current;
        if (DraftFingerprintUx9L(draft) != _approvedLiveAccessFingerprintUx9L ||
            DraftFingerprintUx9L(approved) != _approvedLiveAccessFingerprintUx9L ||
            approved.BaseServerRevision != current.ServerRevision ||
            approved.SourceProfileFingerprint != ProfileFingerprintUx9L(current))
        {
            CancelLiveAccessApprovalUx9L();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var command = new LiveDirectorAccessCommand(Guid.NewGuid(), 1, "studio:ux9-l",
            current.ServerRevision, "studio.live.access", NetworkRole.Administrator,
            NetworkAuthorityScope.Server, false, BuildCandidateUx9L(current, approved));
        var dryRun = _liveAccessHistoryUx9L.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelLiveAccessApprovalUx9L();
            return new(false, HumanDiagnosticsUx9L(dryRun.Diagnostics));
        }
        var applied = _liveAccessHistoryUx9L.Execute(command with
        {
            Apply = true,
            CommandId = Guid.NewGuid()
        });
        CancelLiveAccessApprovalUx9L();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanDiagnosticsUx9L(applied.Diagnostics));
        _liveAccessAuditUx9L.Add(applied.AuditEntry);
        return new(true,
            "Politiques d’accès appliquées par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9LActionResult UndoLiveAccessUx9L() => MoveHistoryUx9L(true);
    internal StudioUx9LActionResult RedoLiveAccessUx9L() => MoveHistoryUx9L(false);

    private StudioUx9LActionResult MoveHistoryUx9L(bool undo)
    {
        CancelLiveAccessApprovalUx9L();
        if (!EnsureLiveAccessUx9L())
            return new(false, _liveAccessLoadMessageUx9L ?? "Aucun profil d’accès actif.");
        var result = undo
            ? _liveAccessHistoryUx9L!.Undo("studio.live.access", NetworkRole.Administrator)
            : _liveAccessHistoryUx9L!.Redo("studio.live.access", NetworkRole.Administrator);
        if (result.Succeeded) _liveAccessAuditUx9L.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? undo ? "Modification d’accès annulée." :
                "Modification d’accès rétablie." : HumanDiagnosticsUx9L(result.Diagnostics));
    }

    internal StudioUx9LActionResult SaveLiveAccessUx9L()
    {
        CancelLiveAccessApprovalUx9L();
        if (!EnsureLiveAccessUx9L() || Session is null)
            return new(false, _liveAccessLoadMessageUx9L ?? "Aucun profil d’accès actif.");
        var result = WorkspaceLiveDirectorAccessStore.Save(Session.RootDirectory,
            _liveAccessHistoryUx9L!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "Le profil d’accès n'a pas été enregistré.");
        _persistedLiveAccessUx9L = _liveAccessHistoryUx9L.Current;
        _liveAccessFileMissingUx9L = false;
        return new(true, "Profil d’accès enregistré atomiquement dans le workspace.");
    }

    internal StudioUx9LActionResult RunLocalLiveAccessUx9L(bool confirmed, NetworkRole role)
    {
        if (!confirmed)
        {
            _liveAccessAuditUx9L.Add("rejected:studio.live-access:confirmation-required");
            return new(false, "Confirmez explicitement la sandbox d’accès locale.");
        }
        if (role != NetworkRole.Administrator)
        {
            _liveAccessAuditUx9L.Add("rejected:studio.live-access:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sandbox.");
        }
        if (!EnsureLiveAccessUx9L())
            return new(false, _liveAccessLoadMessageUx9L ?? "Aucun profil d’accès actif.");
        try
        {
            var probe = RunLiveAccessProbeUx9L(_liveAccessHistoryUx9L!.Current);
            _liveAccessProbeUx9L = probe;
            _liveAccessAuditUx9L.AddRange(LiveDirectorAccessAcceptanceScenario.Run().AuditTrail);
            return new(ProbeReadyUx9L(probe), ProbeReadyUx9L(probe)
                ? "Sandbox terminée · authentification, inspection, joueurs, audit et chat synthétiques validés."
                : "La sandbox locale n'a pas validé les cinq routes d’accès.");
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            _liveAccessAuditUx9L.Add($"rejected:studio.live-access:{exception.Message}");
            return new(false, $"La sandbox d’accès locale a échoué : {exception.Message}");
        }
    }

    internal StudioUx9LApprovalSnapshot CaptureLiveAccessApprovalUx9L() =>
        new(_approvedLiveAccessUx9L, _approvedLiveAccessFingerprintUx9L);

    internal void RestoreLiveAccessApprovalUx9L(StudioUx9LApprovalSnapshot snapshot)
    {
        _approvedLiveAccessUx9L = snapshot.Draft;
        _approvedLiveAccessFingerprintUx9L = snapshot.DraftFingerprint;
    }

    internal void CancelLiveAccessApprovalUx9L()
    {
        _approvedLiveAccessUx9L = null;
        _approvedLiveAccessFingerprintUx9L = null;
    }

    internal void CloseUx9L()
    {
        CancelLiveAccessApprovalUx9L();
        _liveAccessHistoryUx9L = null;
        _persistedLiveAccessUx9L = null;
        _liveAccessWorkspaceRootUx9L = null;
        _liveAccessLoadMessageUx9L = null;
        _liveAccessFileMissingUx9L = false;
        _liveAccessProbeUx9L = EmptyProbeUx9L();
        _liveAccessAuditUx9L.Clear();
    }

    internal StudioUx9LSmokeResult RunUx9LContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9L();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureLiveAccessApprovalUx9L();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-l-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9L();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            var baseline = LiveDirectorAccessProfile.CreateDefault("evidence:ux9-l");
            if (!WorkspaceLiveDirectorAccessStore.Save(root, baseline).Succeeded) return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var draft = editing.CreateLiveAccessDraftUx9L(explicitlySelected: true);
            if (draft is null) return result;
            var fixture = draft with
            {
                RequiredAtRuntime = true,
                SessionLifetimeMinutes = 45,
                InspectionHistoryLimit = 120,
                PlayerRefreshSeconds = 3,
                AuditRetentionDays = 120,
                ChatMessageLimit = 420
            };
            var invalid = !editing.PreviewLiveAccessUx9L(fixture with
            {
                SessionLifetimeMinutes = 0
            }).Succeeded;
            var staleSource = !editing.PreviewLiveAccessUx9L(fixture with
            {
                BaseServerRevision = fixture.BaseServerRevision + 1
            }).Succeeded;
            var preview = editing.PreviewLiveAccessUx9L(fixture);
            var staleApproval = !editing.ApplyApprovedLiveAccessUx9L(fixture with
            {
                ChatMessageLimit = 419
            }).Succeeded;
            preview = editing.PreviewLiveAccessUx9L(fixture);
            var applied = editing.ApplyApprovedLiveAccessUx9L(fixture);
            var current = editing.CurrentLiveAccessUx9L;
            if (current is null) return result;
            var fiveRoutes = current.Authentication.SessionLifetimeMinutes == 45 &&
                             current.Inspection.HistoryEntryLimit == 120 &&
                             current.Players.RefreshSeconds == 3 &&
                             current.Audit.RetentionDays == 120 &&
                             current.Chat.MaximumMessageLength == 420;
            var undo = editing.UndoLiveAccessUx9L().Succeeded;
            var redo = editing.RedoLiveAccessUx9L().Succeeded;
            var saved = editing.SaveLiveAccessUx9L().Succeeded;
            var permission = !editing.RunLocalLiveAccessUx9L(true, NetworkRole.Player).Succeeded;
            var confirmation = !editing.RunLocalLiveAccessUx9L(false,
                NetworkRole.Administrator).Succeeded;
            var probed = editing.RunLocalLiveAccessUx9L(true,
                NetworkRole.Administrator).Succeeded;
            var probe = editing.LiveAccessProbeUx9L;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              reopened.CurrentLiveAccessUx9L is { } loaded &&
                              new LiveDirectorAccessStore().Serialize(current) ==
                              new LiveDirectorAccessStore().Serialize(loaded);
            result = new(true, fiveRoutes, true, invalid, permission, confirmation,
                preview.Succeeded && preview.CanApply && preview.CanonicalDryRun,
                applied.Succeeded, staleSource, staleApproval, undo && redo, persistence,
                probe.AuthenticationCode == "LIVE_GM_AUTHENTICATED" && probe.SessionActive,
                probe.InspectionCode == "LIVE_INSPECTION_READY" &&
                probe.InspectionSections == 6,
                probe.Players == 1 && probe.LatencyMs == 37,
                probe.AuditEntries == 3 && probe.AuditVerified,
                probe.ChatMessages == 2 && probe.LastChannel == "impersonation" &&
                probe.PermissionCode == "LIVE_ACCESS_PERMISSION_DENIED",
                probe.ScriptHandles, SourceSessionMatchesUx9L(sourceProject, sourceHistory,
                    sourceRevision, sourceJournal, sourceDirty, sourceArea, sourceSelection,
                    sourceSelected, sourceApproval), probed && probe.ResourcesReleased);
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                result = result with { TemporaryWorkspaceReleased = false };
            }
        }
        return result with
        {
            SourceUnchanged = result.SourceUnchanged && SourceSessionMatchesUx9L(sourceProject,
                sourceHistory, sourceRevision, sourceJournal, sourceDirty, sourceArea,
                sourceSelection, sourceSelected, sourceApproval),
            TemporaryWorkspaceReleased = result.TemporaryWorkspaceReleased &&
                                         !Directory.Exists(root)
        };
    }

    private bool EnsureLiveAccessUx9L()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _liveAccessLoadMessageUx9L = "Ouvrez d’abord un module.";
            return false;
        }
        var full = Path.GetFullPath(root);
        if (_liveAccessHistoryUx9L is not null &&
            string.Equals(_liveAccessWorkspaceRootUx9L, full,
                StringComparison.OrdinalIgnoreCase)) return true;
        var loaded = WorkspaceLiveDirectorAccessStore.Load(full);
        if (!loaded.Succeeded)
        {
            _liveAccessHistoryUx9L = null;
            _persistedLiveAccessUx9L = null;
            _liveAccessWorkspaceRootUx9L = full;
            _liveAccessFileMissingUx9L = false;
            _liveAccessLoadMessageUx9L =
                $"Profil d’accès invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _liveAccessHistoryUx9L = new(loaded.Profile);
        _persistedLiveAccessUx9L = loaded.Profile;
        _liveAccessWorkspaceRootUx9L = full;
        _liveAccessFileMissingUx9L = loaded.FileMissing;
        _liveAccessLoadMessageUx9L = loaded.FileMissing
            ? "Aucun profil d’accès enregistré · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static LiveDirectorAccessProfile BuildCandidateUx9L(
        LiveDirectorAccessProfile current, StudioUx9LAccessDraft draft) => current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-l",
            RequiredAtRuntime = draft.RequiredAtRuntime,
            Authentication = current.Authentication with
            {
                SessionLifetimeMinutes = draft.SessionLifetimeMinutes
            },
            Inspection = current.Inspection with
            {
                HistoryEntryLimit = draft.InspectionHistoryLimit
            },
            Players = current.Players with { RefreshSeconds = draft.PlayerRefreshSeconds },
            Audit = current.Audit with { RetentionDays = draft.AuditRetentionDays },
            Chat = current.Chat with { MaximumMessageLength = draft.ChatMessageLimit }
        };

    private static StudioUx9LAccessProbe RunLiveAccessProbeUx9L(
        LiveDirectorAccessProfile profile)
    {
        var runtime = LiveDirectorAccessRuntimeAdapter.Build(profile);
        var snapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var scriptHandles = snapshot.InspectionSections == 6 && snapshot.PlayerColumns == 8 &&
                            snapshot.ChatChannels == 7 &&
                            runtime.Scripts.Authentication(runtime.Scripts.Handle).PolicyId ==
                            profile.Authentication.Id &&
                            runtime.Scripts.Inspection(runtime.Scripts.Handle).PolicyId ==
                            profile.Inspection.Id &&
                            runtime.Scripts.Players(runtime.Scripts.Handle).PolicyId ==
                            profile.Players.Id &&
                            runtime.Scripts.Audit(runtime.Scripts.Handle).PolicyId == profile.Audit.Id &&
                            runtime.Scripts.Chat(runtime.Scripts.Handle).PolicyId == profile.Chat.Id;
        var scenario = LiveDirectorAccessAcceptanceScenario.Run();
        return new(scenario.Passed ? "Cinq routes d’accès locales validées" :
                "Sandbox d’accès incomplète", scenario.AuthenticationCode,
            scenario.SessionActive, scenario.InspectionCode, scenario.InspectionSections,
            scenario.Players, scenario.LatencyMs, scenario.AuditEntries,
            scenario.AuditVerified, scenario.ChatMessages, scenario.LastChannel,
            scenario.PermissionCode, scriptHandles, true);
    }

    private static bool ProbeReadyUx9L(StudioUx9LAccessProbe probe) =>
        probe.AuthenticationCode == "LIVE_GM_AUTHENTICATED" && probe.SessionActive &&
        probe.InspectionCode == "LIVE_INSPECTION_READY" && probe.InspectionSections == 6 &&
        probe.Players == 1 && probe.LatencyMs == 37 && probe.AuditEntries == 3 &&
        probe.AuditVerified && probe.ChatMessages == 2 &&
        probe.LastChannel == "impersonation" &&
        probe.PermissionCode == "LIVE_ACCESS_PERMISSION_DENIED" && probe.ScriptHandles &&
        probe.ResourcesReleased;

    private bool SourceSessionMatchesUx9L(Forge.Domain.Projects.ProjectDefinition sourceProject,
        Forge.Application.Commands.ProjectCommandHistory sourceHistory,
        Forge.Application.Projects.ProjectRevision sourceRevision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> sourceJournal,
        bool sourceDirty, Forge.Domain.Areas.AreaId? sourceArea,
        IReadOnlyList<Forge.Domain.Entities.EntityId> sourceSelection,
        Forge.Domain.Entities.EntityId? sourceSelected,
        StudioUx9LApprovalSnapshot sourceApproval) =>
        ReferenceEquals(Project, sourceProject) && ReferenceEquals(History, sourceHistory) &&
        History?.State.Revision == sourceRevision &&
        (History?.Journal ?? []).SequenceEqual(sourceJournal) && IsDirty == sourceDirty &&
        ActiveAreaId == sourceArea && SelectedEntityIds.SequenceEqual(sourceSelection) &&
        SelectedEntityId == sourceSelected && CaptureLiveAccessApprovalUx9L() == sourceApproval;

    private static string ProfileFingerprintUx9L(LiveDirectorAccessProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            new LiveDirectorAccessStore().Serialize(profile))));

    private static string DraftFingerprintUx9L(StudioUx9LAccessDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(draft))));

    private static StudioUx9LAccessPreview FailedPreviewUx9L(string message,
        IReadOnlyList<NetworkDiagnostic>? diagnostics = null) => new(false, false, false,
        false, message, [], diagnostics?.Select(item => HumanCodeUx9L(item.Code))
            .Distinct().ToArray() ?? [], EmptyProbeUx9L());

    private static string HumanDiagnosticsUx9L(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        diagnostics.Count == 0 ? "La commande n'a produit aucun changement applicable." :
        string.Join(" · ", diagnostics.Select(item => HumanCodeUx9L(item.Code)).Distinct());

    private static string HumanCodeUx9L(string code) => code switch
    {
        "LIVE_ACCESS_AUTH_POLICY_INVALID" => "La durée de session doit rester comprise entre 1 et 1 440 minutes.",
        "LIVE_ACCESS_INSPECTION_POLICY_INVALID" => "L’historique d’inspection doit rester compris entre 1 et 10 000 entrées.",
        "LIVE_ACCESS_PLAYER_POLICY_INVALID" => "Le rafraîchissement joueurs doit rester compris entre 1 et 300 secondes.",
        "LIVE_ACCESS_AUDIT_POLICY_INVALID" => "La rétention d’audit doit rester comprise entre 1 et 3 650 jours.",
        "LIVE_ACCESS_CHAT_POLICY_INVALID" => "La longueur des messages doit rester comprise entre 1 et 500 caractères.",
        "LIVE_ACCESS_PERMISSION_DENIED" => "La permission Administrateur est requise.",
        "LIVE_ACCESS_REVISION_STALE" => "La source a changé ; prévisualisez de nouveau.",
        _ => "La politique d’accès Live Director est invalide."
    };

    private static string HumanDiffUx9L(string value) => value switch
    {
        "authentication" => "Authentification MFA, expiration et révocation",
        "inspection" => "Inspection en lecture seule et historique borné",
        "players" => "Liste des joueurs et latence autoritaire",
        "audit" => "Journal immuable, filtrable et borné",
        "chat" => "Canaux MJ, modération et incarnation motivée",
        _ => value
    };

    private static StudioUx9LAccessProbe EmptyProbeUx9L() => new(
        "Aucune sandbox exécutée", "—", false, "—", 0, 0, 0, 0, false, 0,
        "—", "—", false, true);

    private static StudioUx9LSmokeResult FailedSmokeUx9L() => new(false, false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false);
}
