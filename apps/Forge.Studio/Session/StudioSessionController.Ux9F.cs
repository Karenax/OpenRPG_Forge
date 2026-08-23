using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Catalog;
using Forge.LiveDirector.World;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private LiveDirectorWorkspaceHistory? _liveDirectorHistoryUx9F;
    private LiveDirectorWorkspaceProfile? _persistedLiveDirectorUx9F;
    private string? _liveDirectorWorkspaceRootUx9F;
    private StudioUx9FLiveDirectorDraft? _approvedLiveDirectorUx9F;
    private string? _approvedLiveDirectorFingerprintUx9F;
    private string? _liveDirectorLoadMessageUx9F;
    private bool _liveDirectorFileMissingUx9F;
    private StudioUx9FLiveDirectorProbe _liveDirectorProbeUx9F = EmptyProbeUx9F();
    private readonly List<string> _liveDirectorAuditUx9F = [];
    private bool _lastUx9FTemporaryReleased = true;

    internal string? LiveDirectorLoadMessageUx9F => _liveDirectorLoadMessageUx9F;
    internal bool LiveDirectorFileMissingUx9F => _liveDirectorFileMissingUx9F;
    internal StudioUx9FLiveDirectorProbe LiveDirectorProbeUx9F => _liveDirectorProbeUx9F;
    internal IReadOnlyList<string> LiveDirectorAuditUx9F => _liveDirectorAuditUx9F.ToArray();
    internal LiveDirectorWorkspaceProfile? CurrentLiveDirectorUx9F =>
        EnsureLiveDirectorUx9F() ? _liveDirectorHistoryUx9F!.Current : null;
    internal bool LiveDirectorHasUnsavedChangesUx9F =>
        EnsureLiveDirectorUx9F() && _persistedLiveDirectorUx9F is not null &&
        !string.Equals(ProfileFingerprintUx9F(_liveDirectorHistoryUx9F!.Current),
            ProfileFingerprintUx9F(_persistedLiveDirectorUx9F), StringComparison.Ordinal);

    internal StudioUx9FLiveDirectorDraft? CreateLiveDirectorDraftUx9F(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureLiveDirectorUx9F()) return null;
        var profile = _liveDirectorHistoryUx9F!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9F(profile),
            profile.Client.SessionMinutes, profile.Spawn.MaximumInstances,
            profile.RequiredAtRuntime);
    }

    internal StudioUx9FLiveDirectorPreview PreviewLiveDirectorUx9F(
        StudioUx9FLiveDirectorDraft draft, StudioUx9FProbeOptions? probeOptions = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLiveDirectorApprovalUx9F();
        if (!EnsureLiveDirectorUx9F())
            return FailedLiveDirectorPreviewUx9F(
                _liveDirectorLoadMessageUx9F ?? "Le Live Director ne peut pas être chargé.");
        var current = _liveDirectorHistoryUx9F!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            !string.Equals(draft.SourceProfileFingerprint, ProfileFingerprintUx9F(current),
                StringComparison.Ordinal))
            return FailedLiveDirectorPreviewUx9F(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");

        var normalized = NormalizeDraftUx9F(draft);
        var candidate = BuildCandidateUx9F(current, normalized);
        var command = new LiveDirectorWorkspaceCommand(Guid.NewGuid(), 1, "studio:ux9-f",
            current.ServerRevision, "studio.live.director", NetworkRole.Administrator, false,
            candidate);
        var dryRun = _liveDirectorHistoryUx9F.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedLiveDirectorPreviewUx9F(
                HumanLiveDirectorDiagnosticsUx9F(dryRun.Diagnostics), dryRun.Diagnostics);

        LiveDirectorProbeResultUx9F probe;
        try
        {
            probe = RunLiveDirectorProbeUx9F(candidate, applyRuntime: false,
                probeOptions ?? StudioUx9FProbeOptions.CreateDefault());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException or InvalidDataException)
        {
            return FailedLiveDirectorPreviewUx9F(
                $"La sonde locale Live Director a refusé la configuration : {exception.Message}");
        }

        _liveDirectorProbeUx9F = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only"
            ? Array.Empty<string>()
            : dryRun.SemanticDiff.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = probe.Client && probe.Creator && probe.Spawn && probe.Chooser && probe.Focus &&
                       changes.Length > 0;
        if (canApply)
        {
            _approvedLiveDirectorUx9F = normalized with { };
            _approvedLiveDirectorFingerprintUx9F =
                DraftFingerprintUx9F(_approvedLiveDirectorUx9F);
        }
        return new(true, canApply, true, probe.Client, probe.Creator, probe.Spawn,
            probe.Chooser, probe.Focus,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique."
                : "Aperçu sec validé · aucun client ni spawn réel n’a été ouvert.",
            changes.Select(HumanLiveDirectorDiffUx9F).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9FActionResult ApplyApprovedLiveDirectorUx9F(
        StudioUx9FLiveDirectorDraft currentDraft)
    {
        var approved = _approvedLiveDirectorUx9F;
        if (approved is null || _approvedLiveDirectorFingerprintUx9F is null ||
            !EnsureLiveDirectorUx9F())
        {
            CancelLiveDirectorApprovalUx9F();
            return new(false, "Prévisualisez d’abord le profil Live Director.");
        }
        var current = _liveDirectorHistoryUx9F!.Current;
        var normalizedCurrent = NormalizeDraftUx9F(currentDraft);
        if (!string.Equals(DraftFingerprintUx9F(normalizedCurrent),
                _approvedLiveDirectorFingerprintUx9F, StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9F(approved),
                _approvedLiveDirectorFingerprintUx9F, StringComparison.Ordinal) ||
            approved.BaseServerRevision != current.ServerRevision ||
            !string.Equals(approved.SourceProfileFingerprint, ProfileFingerprintUx9F(current),
                StringComparison.Ordinal))
        {
            CancelLiveDirectorApprovalUx9F();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var candidate = BuildCandidateUx9F(current, approved);
        var command = new LiveDirectorWorkspaceCommand(Guid.NewGuid(), 1, "studio:ux9-f",
            current.ServerRevision, "studio.live.director", NetworkRole.Administrator, false,
            candidate);
        var dryRun = _liveDirectorHistoryUx9F.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelLiveDirectorApprovalUx9F();
            return new(false, HumanLiveDirectorDiagnosticsUx9F(dryRun.Diagnostics));
        }
        var applied = _liveDirectorHistoryUx9F.Execute(command with { Apply = true });
        CancelLiveDirectorApprovalUx9F();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanLiveDirectorDiagnosticsUx9F(applied.Diagnostics));
        _liveDirectorAuditUx9F.Add(applied.AuditEntry);
        return new(true,
            "Profil Live Director appliqué par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9FActionResult UndoLiveDirectorUx9F()
    {
        CancelLiveDirectorApprovalUx9F();
        if (!EnsureLiveDirectorUx9F())
            return new(false, _liveDirectorLoadMessageUx9F ?? "Aucun profil Live Director actif.");
        var result = _liveDirectorHistoryUx9F!.Undo("studio.live.director",
            NetworkRole.Administrator);
        if (result.Succeeded) _liveDirectorAuditUx9F.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification Live Director annulée." :
            HumanLiveDirectorDiagnosticsUx9F(result.Diagnostics));
    }

    internal StudioUx9FActionResult RedoLiveDirectorUx9F()
    {
        CancelLiveDirectorApprovalUx9F();
        if (!EnsureLiveDirectorUx9F())
            return new(false, _liveDirectorLoadMessageUx9F ?? "Aucun profil Live Director actif.");
        var result = _liveDirectorHistoryUx9F!.Redo("studio.live.director",
            NetworkRole.Administrator);
        if (result.Succeeded) _liveDirectorAuditUx9F.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification Live Director rétablie." :
            HumanLiveDirectorDiagnosticsUx9F(result.Diagnostics));
    }

    internal StudioUx9FActionResult SaveLiveDirectorUx9F()
    {
        CancelLiveDirectorApprovalUx9F();
        if (!EnsureLiveDirectorUx9F() || Session is null)
            return new(false, _liveDirectorLoadMessageUx9F ?? "Aucun profil Live Director actif.");
        var result = WorkspaceLiveDirectorStore.Save(Session.RootDirectory,
            _liveDirectorHistoryUx9F!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "Le profil Live Director n’a pas été enregistré.");
        _persistedLiveDirectorUx9F = _liveDirectorHistoryUx9F.Current;
        _liveDirectorFileMissingUx9F = false;
        return new(true, "Live Director enregistré atomiquement dans le workspace.");
    }

    internal StudioUx9FActionResult RunLocalLiveDirectorUx9F(bool confirmed, NetworkRole role,
        StudioUx9FProbeOptions? probeOptions = null)
    {
        if (!confirmed)
        {
            _liveDirectorAuditUx9F.Add(
                "rejected:studio.live-director:confirmation-required");
            return new(false, "Confirmez explicitement la sonde Live Director locale.");
        }
        if (role != NetworkRole.Administrator)
        {
            _liveDirectorAuditUx9F.Add("rejected:studio.live-director:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sonde.");
        }
        if (!EnsureLiveDirectorUx9F())
            return new(false, _liveDirectorLoadMessageUx9F ?? "Aucun profil Live Director actif.");
        try
        {
            var probe = RunLiveDirectorProbeUx9F(_liveDirectorHistoryUx9F!.Current,
                applyRuntime: true, probeOptions ?? StudioUx9FProbeOptions.CreateDefault());
            _liveDirectorProbeUx9F = probe.Snapshot;
            _liveDirectorAuditUx9F.AddRange(probe.Audit);
            var succeeded = probe.Client && probe.Creator && probe.Spawn && probe.Chooser &&
                            probe.Focus && probe.Snapshot.ResourcesReleased;
            return new(succeeded, succeeded
                ? "Sonde locale terminée · session révoquée et spawn compensé."
                : "La sonde locale n’a pas validé les cinq parcours Live Director.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                               UnauthorizedAccessException or InvalidDataException)
        {
            _liveDirectorAuditUx9F.Add($"rejected:studio.live-director:{exception.Message}");
            return new(false, $"La sonde locale Live Director a échoué : {exception.Message}");
        }
    }

    internal StudioUx9FApprovalSnapshot CaptureLiveDirectorApprovalUx9F() =>
        new(_approvedLiveDirectorUx9F, _approvedLiveDirectorFingerprintUx9F);

    internal void RestoreLiveDirectorApprovalUx9F(StudioUx9FApprovalSnapshot snapshot)
    {
        _approvedLiveDirectorUx9F = snapshot.Draft;
        _approvedLiveDirectorFingerprintUx9F = snapshot.DraftFingerprint;
    }

    internal void CancelLiveDirectorApprovalUx9F()
    {
        _approvedLiveDirectorUx9F = null;
        _approvedLiveDirectorFingerprintUx9F = null;
    }

    internal void CloseUx9F()
    {
        CancelLiveDirectorApprovalUx9F();
        _liveDirectorHistoryUx9F = null;
        _persistedLiveDirectorUx9F = null;
        _liveDirectorWorkspaceRootUx9F = null;
        _liveDirectorLoadMessageUx9F = null;
        _liveDirectorFileMissingUx9F = false;
        _liveDirectorProbeUx9F = EmptyProbeUx9F();
        _liveDirectorAuditUx9F.Clear();
    }

    internal StudioUx9FSmokeResult RunUx9FContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9F();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureLiveDirectorApprovalUx9F();
        var temporaryRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-f-{Guid.NewGuid():N}");
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
        var clientRuntime = false;
        var creatorRuntime = false;
        var spawnRuntime = false;
        var chooserRuntime = false;
        var focusRuntime = false;
        var audit = false;
        try
        {
            if (!ProjectSession.Create(temporaryRoot, sourceProject).Succeeded)
                return FailedSmokeUx9F();
            var baseline = LiveDirectorWorkspaceProfile.CreateDefault("evidence:ux9-f");
            if (!WorkspaceLiveDirectorStore.Save(temporaryRoot, baseline).Succeeded)
                return FailedSmokeUx9F();
            editing = new StudioSessionController();
            if (!editing.Open(temporaryRoot).Succeeded) return FailedSmokeUx9F();
            var draft = editing.CreateLiveDirectorDraftUx9F(explicitlySelected: true);
            explicitSelection = draft is not null;
            if (draft is null) return FailedSmokeUx9F();
            var fixture = draft with
            {
                SessionMinutes = 45,
                MaximumInstances = 8,
                RequiredAtRuntime = true
            };

            invalidBlocked = !editing.PreviewLiveDirectorUx9F(fixture with
            {
                MaximumInstances = 0
            }).Succeeded;
            staleSource = !editing.PreviewLiveDirectorUx9F(fixture with
            {
                BaseServerRevision = fixture.BaseServerRevision + 9
            }).Succeeded;
            var preview = editing.PreviewLiveDirectorUx9F(fixture);
            canonicalDryRun = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun;
            clientRuntime = preview.ClientRuntime;
            creatorRuntime = preview.CreatorRuntime;
            spawnRuntime = preview.SpawnRuntime;
            chooserRuntime = preview.ChooserRuntime;
            focusRuntime = preview.FocusRuntime;
            staleApproval = !editing.ApplyApprovedLiveDirectorUx9F(fixture with
            {
                SessionMinutes = fixture.SessionMinutes + 1
            }).Succeeded;
            preview = editing.PreviewLiveDirectorUx9F(fixture);
            var applied = editing.ApplyApprovedLiveDirectorUx9F(fixture);
            canonicalCommand = preview.CanApply && applied.Succeeded;
            var current = editing.CurrentLiveDirectorUx9F;
            if (current is null) return FailedSmokeUx9F();
            fiveRoutes = current.Client is
            {
                SeparateClient: true, EngineIndependent: true,
                RoleAuthentication: true, SecondFactor: true, Revocation: true,
                Audit: true, SessionMinutes: 45
            } &&
                         current.Creator is
                         {
                             ServerAllowlist: true, PackageFilter: true, TextFilter: true,
                             TypeFilter: true, CostFilter: true, ZoneFilter: true,
                             PermissionFilter: true, Thumbnails: true
                         } &&
                         current.Spawn.MaximumInstances == 8 && current.Spawn.FormationCoordinates &&
                         current.Spawn.Preview && current.Spawn.Atomic && current.Spawn.StableIds &&
                         current.Spawn.SafeUndo && current.Spawn.PlacementModes.Count == 3 &&
                         current.Chooser.HierarchicalZones && current.Chooser.InstanceNavigation &&
                         current.Chooser.Filters.Count >= 5 && current.Chooser.Diagnostics &&
                         current.Chooser.PrivacyFiltering && current.Focus.SpectatorCamera &&
                         current.Focus.CrossZone && current.Focus.PrivacyOverrideRequiresPermission &&
                         current.Focus.ReadOnly && current.Focus.Audit && current.RequiredAtRuntime;

            var undone = editing.UndoLiveDirectorUx9F();
            var afterUndo = editing.CurrentLiveDirectorUx9F;
            var redone = editing.RedoLiveDirectorUx9F();
            var afterRedo = editing.CurrentLiveDirectorUx9F;
            undoRedo = undone.Succeeded && redone.Succeeded && afterUndo is not null &&
                       afterRedo is not null && afterUndo.ServerRevision == baseline.ServerRevision &&
                       afterRedo.ServerRevision == current.ServerRevision;
            var saved = editing.SaveLiveDirectorUx9F();
            var loaded = WorkspaceLiveDirectorStore.Load(temporaryRoot);
            var serializer = new LiveDirectorWorkspaceStore();
            persistence = saved.Succeeded && loaded.Succeeded && !loaded.FileMissing &&
                          string.Equals(serializer.Serialize(afterRedo!),
                              serializer.Serialize(loaded.Profile), StringComparison.Ordinal);
            reopened = new StudioSessionController();
            persistence = persistence && reopened.Open(temporaryRoot).Succeeded &&
                          reopened.CreateLiveDirectorDraftUx9F(explicitlySelected: true) is
                          { } reopenedDraft &&
                          reopenedDraft.BaseServerRevision == afterRedo!.ServerRevision &&
                          string.Equals(reopenedDraft.SourceProfileFingerprint,
                              ProfileFingerprintUx9F(afterRedo), StringComparison.Ordinal);

            confirmationRequired = !editing.RunLocalLiveDirectorUx9F(false,
                NetworkRole.Administrator).Succeeded;
            permissionBlocked = !editing.RunLocalLiveDirectorUx9F(true,
                NetworkRole.Player).Succeeded;
            var tested = editing.RunLocalLiveDirectorUx9F(true, NetworkRole.Administrator);
            var probe = editing.LiveDirectorProbeUx9F;
            clientRuntime = clientRuntime && tested.Succeeded && probe.SecondFactorEnforced &&
                            probe.SessionActive && probe.ClientConnected &&
                            probe.AuthenticationCode == "LIVE_GM_AUTHENTICATED";
            creatorRuntime = creatorRuntime && probe.CatalogMatches == 1 &&
                             probe.ThumbnailAvailable;
            spawnRuntime = spawnRuntime && probe.SpawnPreviewCode == "LIVE_SPAWN_PREVIEW" &&
                           probe.SpawnApplyCode == "LIVE_SPAWN_APPLIED" &&
                           probe.StableObjectIds && probe.DistinctCoordinates == 4 &&
                           probe.CompensationCode == "LIVE_SPAWN_COMPENSATED";
            chooserRuntime = chooserRuntime && probe.PublicChooserMatches == 1 &&
                             probe.PrivateHidden && probe.PrivateOverrideAllowed;
            focusRuntime = focusRuntime && probe.FocusCode == "LIVE_ACTION_APPLIED" &&
                           probe.FocusReadOnly;
            audit = editing.LiveDirectorAuditUx9F.Count >= 10 && probe.AuditEntries >= 8 &&
                    probe.ResourcesReleased;
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            released = DeleteUx9FTemporaryWorkspace(temporaryRoot);
            _lastUx9FTemporaryReleased = released;
            RestoreLiveDirectorApprovalUx9F(sourceApproval);
        }
        var sourceUnchanged = ReferenceEquals(sourceProject, Project) &&
                              ReferenceEquals(sourceHistory, History) &&
                              History?.State.Revision == sourceRevision &&
                              (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                              SelectedEntityIds.SequenceEqual(sourceSelection) &&
                              SelectedEntityId == sourceSelected &&
                              CaptureLiveDirectorApprovalUx9F() == sourceApproval;
        var succeeded = fiveRoutes && explicitSelection && invalidBlocked && permissionBlocked &&
                        confirmationRequired && canonicalDryRun && canonicalCommand && staleSource &&
                        staleApproval && undoRedo && persistence && clientRuntime && creatorRuntime &&
                        spawnRuntime && chooserRuntime && focusRuntime && audit && sourceUnchanged &&
                        released;
        return new(succeeded, fiveRoutes, explicitSelection, invalidBlocked, permissionBlocked,
            confirmationRequired, canonicalDryRun, canonicalCommand, staleSource, staleApproval,
            undoRedo, persistence, clientRuntime, creatorRuntime, spawnRuntime, chooserRuntime,
            focusRuntime, audit, sourceUnchanged, released);
    }

    private bool EnsureLiveDirectorUx9F()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _liveDirectorLoadMessageUx9F = "Ouvrez d’abord un module.";
            return false;
        }
        var fullRoot = Path.GetFullPath(root);
        if (_liveDirectorHistoryUx9F is not null &&
            string.Equals(_liveDirectorWorkspaceRootUx9F, fullRoot,
                StringComparison.OrdinalIgnoreCase))
            return true;
        var loaded = WorkspaceLiveDirectorStore.Load(fullRoot);
        if (!loaded.Succeeded)
        {
            _liveDirectorHistoryUx9F = null;
            _persistedLiveDirectorUx9F = null;
            _liveDirectorWorkspaceRootUx9F = fullRoot;
            _liveDirectorFileMissingUx9F = false;
            _liveDirectorLoadMessageUx9F =
                $"Live Director invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _liveDirectorHistoryUx9F = new LiveDirectorWorkspaceHistory(loaded.Profile);
        _persistedLiveDirectorUx9F = loaded.Profile;
        _liveDirectorWorkspaceRootUx9F = fullRoot;
        _liveDirectorFileMissingUx9F = loaded.FileMissing;
        _liveDirectorLoadMessageUx9F = loaded.FileMissing
            ? "Aucun profil Live Director enregistré · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static LiveDirectorWorkspaceProfile BuildCandidateUx9F(
        LiveDirectorWorkspaceProfile current, StudioUx9FLiveDirectorDraft draft) =>
        current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-f",
            RequiredAtRuntime = draft.RequiredAtRuntime,
            Client = current.Client with
            {
                SeparateClient = true,
                EngineIndependent = true,
                RoleAuthentication = true,
                SecondFactor = true,
                SessionMinutes = draft.SessionMinutes,
                Revocation = true,
                Audit = true
            },
            Creator = current.Creator with
            {
                ServerAllowlist = true,
                PackageFilter = true,
                TextFilter = true,
                TypeFilter = true,
                CostFilter = true,
                ZoneFilter = true,
                PermissionFilter = true,
                Thumbnails = true
            },
            Spawn = current.Spawn with
            {
                MaximumInstances = draft.MaximumInstances,
                PlacementModes = ["single", "formation", "scatter"],
                FormationCoordinates = true,
                Preview = true,
                Atomic = true,
                StableIds = true,
                SafeUndo = true
            },
            Chooser = current.Chooser with
            {
                HierarchicalZones = true,
                InstanceNavigation = true,
                Filters = ["zone", "type", "name", "tag", "player-state"],
                Diagnostics = true,
                PrivacyFiltering = true
            },
            Focus = current.Focus with
            {
                SpectatorCamera = true,
                CrossZone = true,
                PrivacyOverrideRequiresPermission = true,
                ReadOnly = true,
                Audit = true
            }
        };

    private static LiveDirectorProbeResultUx9F RunLiveDirectorProbeUx9F(
        LiveDirectorWorkspaceProfile profile, bool applyRuntime,
        StudioUx9FProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.CatalogText) ||
            string.IsNullOrWhiteSpace(options.CatalogPackage) ||
            string.IsNullOrWhiteSpace(options.CatalogType) || options.MaximumCost < 0 ||
            options.SpawnCount < 1 || options.SpawnCount > profile.Spawn.MaximumInstances ||
            options.Placement is not ("single" or "formation" or "scatter") ||
            string.IsNullOrWhiteSpace(options.InstanceId) || options.InstanceId.Length > 128 ||
            !double.IsFinite(options.X) || !double.IsFinite(options.Y) ||
            !double.IsFinite(options.Z) || !double.IsFinite(options.Spacing) ||
            options.Spacing is <= 0 or > 1000 || string.IsNullOrWhiteSpace(options.ChooserText))
            throw new ArgumentException("LIVE_STUDIO_PROBE_OPTIONS_INVALID", nameof(options));
        var nextObject = 300;
        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var runtime = LiveDirectorWorkspaceRuntimeAdapter.Create(profile, () => now,
            () => Guid.Parse("d1250000-0000-4000-8000-000000000100"),
            () => Guid.Parse($"d1250000-0000-4000-8000-{nextObject++:000000000000}"));
        var audit = new List<string>();
        var zoneA = Guid.Parse("d1250000-0000-4000-8000-000000000200");
        var zoneB = Guid.Parse("d1250000-0000-4000-8000-000000000201");
        var blueprint = Guid.Parse("d1250000-0000-4000-8000-000000000202");
        var account = runtime.Accounts.Register("studio-director", "correct-horse-battery");
        runtime.Authentication.GrantRole(account.AccountId, NetworkRole.Administrator);
        var missingFactor = runtime.Authentication.Authenticate(
            new("studio-director", "correct-horse-battery", null));
        var session = runtime.Authentication.Authenticate(
            new("studio-director", "correct-horse-battery", "246810"),
            factor => factor == "246810");
        audit.Add(missingFactor.AuditEntry);
        audit.Add(session.AuditEntry);
        if (!session.Authenticated || session.Subject is null)
            throw new InvalidOperationException(session.Code);
        var connection = runtime.Client.Connect(
            Guid.Parse("d1250000-0000-4000-8000-000000000203"), session);
        audit.Add(connection.AuditEntry);
        var actor = session.Subject;

        runtime.Catalog.Replace([
            new(blueprint, "Gardien des braises", "creature", "crypt", 10,
                new HashSet<Guid> { zoneA }, true, "res://thumbs/guardian.png"),
            new(Guid.Parse("d1250000-0000-4000-8000-000000000204"), "Interdit",
                "creature", "crypt", 1, new HashSet<Guid> { zoneA }, false)
        ]);
        var catalogMatches = runtime.Catalog.Search(actor,
            new(options.CatalogText, options.CatalogType, zoneA, options.MaximumCost,
                options.CatalogPackage));
        audit.Add($"catalog:matches={catalogMatches.Count}:thumbnail={catalogMatches.FirstOrDefault()?.Thumbnail}");

        var spawnCount = options.SpawnCount;
        var spawnCommand = Guid.Parse("d1250000-0000-4000-8000-000000000205");
        var spawnRequest = new LiveSpawnService.Request(spawnCommand, blueprint, zoneA,
            spawnCount, options.Placement, 0, options.InstanceId, options.X, options.Y,
            options.Z, options.Spacing);
        var spawnPreview = runtime.Spawn.Execute(actor, spawnRequest, false);
        var repeatedPreview = runtime.Spawn.Execute(actor, spawnRequest, false);
        var stableIds = spawnPreview.ObjectIds.SequenceEqual(repeatedPreview.ObjectIds);
        var coordinates = spawnPreview.Instances
            .Select(item => (item.X, item.Y, item.Z)).Distinct().Count();
        audit.Add(spawnPreview.AuditEntry);
        audit.Add($"spawn:stable={stableIds}:coordinates={coordinates}");
        var spawnApplyCode = "LIVE_SPAWN_NOT_APPLIED";
        var compensationCode = "LIVE_SPAWN_NOT_COMPENSATED";
        if (applyRuntime)
        {
            var applied = runtime.Spawn.Execute(actor, spawnRequest, true);
            spawnApplyCode = applied.Code;
            audit.Add(applied.AuditEntry);
            var compensationPreview = runtime.Spawn.Compensate(actor, spawnCommand,
                applied.Revision, false);
            audit.Add(compensationPreview.AuditEntry);
            var compensated = runtime.Spawn.Compensate(actor, spawnCommand,
                applied.Revision, true);
            compensationCode = compensated.Code;
            audit.Add(compensated.AuditEntry);
        }

        var publicTarget = Guid.Parse("d1250000-0000-4000-8000-000000000206");
        var privateTarget = Guid.Parse("d1250000-0000-4000-8000-000000000207");
        runtime.Chooser.Upsert(new(publicTarget, zoneA, "waypoint", "Place publique", "town",
            null, "active", [], "surface", false, 1, 0, 1));
        runtime.Chooser.Upsert(new(privateTarget, zoneB, "waypoint", "Crypte secrète", "secret",
            null, "active", ["accès privé"], "crypt-02", true, 4, 0, 8));
        var publicMatches = runtime.Chooser.Search(actor,
            new(null, "waypoint", options.ChooserText, null, "active"));
        var privateHidden = publicMatches.All(item => item.ObjectId != privateTarget);
        var overrideMatches = runtime.Chooser.Search(actor,
            new(zoneB, "waypoint", "secrète", null, "active", "crypt-02"),
            includePrivate: options.AllowPrivacyOverride);
        var privateOverride = options.AllowPrivacyOverride &&
                              overrideMatches.Any(item => item.ObjectId == privateTarget);
        audit.Add($"chooser:public={publicMatches.Count}:hidden={privateHidden}:override={privateOverride}");

        var beforeFocus = runtime.Chooser.Find(privateTarget);
        var focusArguments = options.AllowPrivacyOverride
            ? new Dictionary<string, string> { ["privacyOverride"] = "approved" }
            : new Dictionary<string, string>();
        var focus = runtime.WorldActions.Execute(actor,
            new(Guid.Parse("d1250000-0000-4000-8000-000000000208"), "director.focus",
                privateTarget, null, 0, focusArguments),
            applyRuntime);
        var focusReadOnly = beforeFocus == runtime.Chooser.Find(privateTarget);
        audit.Add(focus.AuditEntry);

        var revoked = runtime.Authentication.Revoke(session);
        audit.Add(revoked);
        var resourcesReleased = !runtime.Authentication.IsActive(session) &&
                                (!applyRuntime || spawnPreview.ObjectIds.All(id =>
                                    runtime.Chooser.Find(id) is null));
        var clientReady = missingFactor.Code == "LIVE_GM_SECOND_FACTOR_INVALID" &&
                          session.Authenticated && connection.AccountId == account.AccountId;
        var creatorReady = catalogMatches is [{ Thumbnail: not null }];
        var spawnReady = spawnPreview.Code == "LIVE_SPAWN_PREVIEW" && stableIds &&
                         spawnPreview.Instances.Count == spawnCount &&
                         spawnPreview.Instances.All(item => double.IsFinite(item.X) &&
                             double.IsFinite(item.Y) && double.IsFinite(item.Z)) &&
                         (!applyRuntime ||
                             spawnApplyCode == "LIVE_SPAWN_APPLIED" &&
                             compensationCode == "LIVE_SPAWN_COMPENSATED");
        var chooserReady = publicMatches.Count == 1 && privateHidden && privateOverride;
        var focusReady = options.AllowPrivacyOverride &&
                         focus.Code == (applyRuntime ? "LIVE_ACTION_APPLIED" :
                             "LIVE_ACTION_PREVIEW") && focusReadOnly;
        var ready = clientReady && creatorReady && spawnReady && chooserReady && focusReady &&
                    resourcesReleased;
        var snapshot = new StudioUx9FLiveDirectorProbe(
            ready ? "Cinq parcours locaux validés" : "Sonde Live Director incomplète",
            session.Code, missingFactor.Code == "LIVE_GM_SECOND_FACTOR_INVALID",
            session.Authenticated, connection.AccountId == account.AccountId,
            catalogMatches.Count, catalogMatches.FirstOrDefault()?.Thumbnail is not null,
            spawnPreview.Code, spawnApplyCode, stableIds, coordinates, compensationCode,
            publicMatches.Count, privateHidden, privateOverride, focus.Code, focusReadOnly,
            audit.Count, resourcesReleased);
        return new(clientReady, creatorReady, spawnReady, chooserReady, focusReady, snapshot,
            audit);
    }

    private static StudioUx9FLiveDirectorDraft NormalizeDraftUx9F(
        StudioUx9FLiveDirectorDraft draft) => draft with { };

    private static string ProfileFingerprintUx9F(LiveDirectorWorkspaceProfile profile) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(new LiveDirectorWorkspaceStore().Serialize(profile))));

    private static string DraftFingerprintUx9F(StudioUx9FLiveDirectorDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(draft))));

    private static StudioUx9FLiveDirectorPreview FailedLiveDirectorPreviewUx9F(
        string message, IReadOnlyList<NetworkDiagnostic>? diagnostics = null) =>
        new(false, false, false, false, false, false, false, false, message, [],
            diagnostics?.Select(item => HumanLiveDirectorDiagnosticUx9F(item.Code))
                .Distinct(StringComparer.Ordinal).ToArray() ?? [], EmptyProbeUx9F());

    private static string HumanLiveDirectorDiagnosticsUx9F(
        IReadOnlyList<NetworkDiagnostic> diagnostics) => diagnostics.Count == 0
        ? "La commande Live Director n’a produit aucun changement applicable."
        : string.Join(" · ", diagnostics.Select(item =>
                HumanLiveDirectorDiagnosticUx9F(item.Code))
            .Distinct(StringComparer.Ordinal));

    private static string HumanLiveDirectorDiagnosticUx9F(string? code) => code switch
    {
        "LIVE_CLIENT_POLICY_INVALID" =>
            "Le client exige authentification de rôle, second facteur, expiration, révocation et audit.",
        "LIVE_CREATOR_POLICY_INVALID" =>
            "La palette Creator exige allowlist, filtres et miniatures.",
        "LIVE_SPAWN_POLICY_INVALID" =>
            "Le spawn exige une limite, les trois placements, un aperçu atomique et une compensation.",
        "LIVE_CHOOSER_POLICY_INVALID" =>
            "Le Chooser exige zones, instances, filtres, diagnostics et confidentialité.",
        "LIVE_FOCUS_POLICY_INVALID" =>
            "Le focus exige une caméra spectateur en lecture seule et un override permissionné.",
        "LIVE_WORKSPACE_PERMISSION_DENIED" => "La permission Maître de jeu est requise.",
        "LIVE_WORKSPACE_REVISION_STALE" =>
            "La révision serveur a changé ; rechargez le formulaire.",
        _ => string.IsNullOrWhiteSpace(code) ? "Le profil Live Director est invalide." : code
    };

    private static string HumanLiveDirectorDiffUx9F(string value) => value switch
    {
        "client" => "Client séparé et authentification",
        "creator" => "Palette Creator filtrée",
        "spawn" => "Spawn borné et compensable",
        "chooser" => "Chooser hiérarchique et privé",
        "focus" => "Focus spectateur en lecture seule",
        "runtime" => "Chargement runtime requis",
        _ => value
    };

    private static StudioUx9FLiveDirectorProbe EmptyProbeUx9F() =>
        new("Aucune sonde exécutée", "—", false, false, false, 0, false, "—", "—",
            false, 0, "—", 0, false, false, "—", false, 0, true);

    private static StudioUx9FSmokeResult FailedSmokeUx9F() => new(
        Succeeded: false, FiveRoutes: false, ExplicitSelection: false,
        InvalidBlocked: false, PermissionBlocked: false, ConfirmationRequired: false,
        CanonicalDryRun: false, CanonicalCommand: false, StaleSourceBlocked: false,
        StaleApprovalBlocked: false, UndoRedo: false, Persistence: false,
        ClientRuntime: false, CreatorRuntime: false, SpawnRuntime: false,
        ChooserRuntime: false, FocusRuntime: false, Audit: false,
        SourceUnchanged: false, TemporaryWorkspaceReleased: false);

    private static bool DeleteUx9FTemporaryWorkspace(string root)
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
                !Path.GetFileName(fullRoot).StartsWith("openrpg-forge-studio-ux9-f-",
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

    private sealed record LiveDirectorProbeResultUx9F(
        bool Client,
        bool Creator,
        bool Spawn,
        bool Chooser,
        bool Focus,
        StudioUx9FLiveDirectorProbe Snapshot,
        IReadOnlyList<string> Audit);
}
