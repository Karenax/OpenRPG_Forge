using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.World;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private LiveDirectorActionHistory? _liveActionHistoryUx9G;
    private LiveDirectorActionProfile? _persistedLiveActionUx9G;
    private string? _liveActionWorkspaceRootUx9G;
    private StudioUx9GLiveActionDraft? _approvedLiveActionUx9G;
    private string? _approvedLiveActionFingerprintUx9G;
    private string? _liveActionLoadMessageUx9G;
    private bool _liveActionFileMissingUx9G;
    private StudioUx9GLiveActionProbe _liveActionProbeUx9G = EmptyProbeUx9G();
    private readonly List<string> _liveActionAuditUx9G = [];

    internal string? LiveActionLoadMessageUx9G => _liveActionLoadMessageUx9G;
    internal bool LiveActionFileMissingUx9G => _liveActionFileMissingUx9G;
    internal StudioUx9GLiveActionProbe LiveActionProbeUx9G => _liveActionProbeUx9G;
    internal IReadOnlyList<string> LiveActionAuditUx9G => _liveActionAuditUx9G.ToArray();
    internal LiveDirectorActionProfile? CurrentLiveActionUx9G =>
        EnsureLiveActionUx9G() ? _liveActionHistoryUx9G!.Current : null;
    internal bool LiveActionHasUnsavedChangesUx9G =>
        EnsureLiveActionUx9G() && _persistedLiveActionUx9G is not null &&
        !string.Equals(ProfileFingerprintUx9G(_liveActionHistoryUx9G!.Current),
            ProfileFingerprintUx9G(_persistedLiveActionUx9G), StringComparison.Ordinal);

    internal StudioUx9GLiveActionDraft? CreateLiveActionDraftUx9G(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureLiveActionUx9G()) return null;
        var profile = _liveActionHistoryUx9G!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9G(profile),
            profile.Possession.MaximumMinutes, profile.RequiredAtRuntime);
    }

    internal StudioUx9GLiveActionPreview PreviewLiveActionUx9G(
        StudioUx9GLiveActionDraft draft, StudioUx9GProbeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLiveActionApprovalUx9G();
        if (!EnsureLiveActionUx9G())
            return FailedPreviewUx9G(_liveActionLoadMessageUx9G ??
                                     "Les actions Live Director ne peuvent pas être chargées.");
        var current = _liveActionHistoryUx9G!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            !string.Equals(draft.SourceProfileFingerprint, ProfileFingerprintUx9G(current),
                StringComparison.Ordinal))
            return FailedPreviewUx9G(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");

        var normalized = draft with { };
        var candidate = BuildCandidateUx9G(current, normalized);
        var command = new LiveDirectorActionCommand(Guid.NewGuid(), 1, "studio:ux9-g",
            current.ServerRevision, "studio.live.actions", NetworkRole.Administrator, false,
            candidate);
        var dryRun = _liveActionHistoryUx9G.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedPreviewUx9G(HumanDiagnosticsUx9G(dryRun.Diagnostics),
                dryRun.Diagnostics);

        LiveActionProbeResultUx9G probe;
        try
        {
            probe = RunLiveActionProbeUx9G(candidate, false,
                options ?? StudioUx9GProbeOptions.CreateDefault());
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            return FailedPreviewUx9G(
                $"La sonde locale des actions a refusé la configuration : {exception.Message}");
        }

        _liveActionProbeUx9G = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only"
            ? Array.Empty<string>()
            : dryRun.SemanticDiff.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = probe.Relocation && probe.Destruction && probe.Healing && probe.Limbo &&
                       probe.Possession && changes.Length > 0;
        if (canApply)
        {
            _approvedLiveActionUx9G = normalized with { };
            _approvedLiveActionFingerprintUx9G = DraftFingerprintUx9G(normalized);
        }
        return new(true, canApply, true, probe.Relocation, probe.Destruction, probe.Healing,
            probe.Limbo, probe.Possession,
            changes.Length == 0
                ? "Aperçu sec validé · le profil est déjà identique."
                : "Aperçu sec validé · aucune action n’a été exécutée dans le monde réel.",
            changes.Select(HumanDiffUx9G).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9GActionResult ApplyApprovedLiveActionUx9G(
        StudioUx9GLiveActionDraft currentDraft)
    {
        var approved = _approvedLiveActionUx9G;
        if (approved is null || _approvedLiveActionFingerprintUx9G is null ||
            !EnsureLiveActionUx9G())
        {
            CancelLiveActionApprovalUx9G();
            return new(false, "Prévisualisez d’abord les actions Live Director.");
        }
        var current = _liveActionHistoryUx9G!.Current;
        var currentFingerprint = DraftFingerprintUx9G(currentDraft with { });
        if (!string.Equals(currentFingerprint, _approvedLiveActionFingerprintUx9G,
                StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9G(approved),
                _approvedLiveActionFingerprintUx9G, StringComparison.Ordinal) ||
            approved.BaseServerRevision != current.ServerRevision ||
            !string.Equals(approved.SourceProfileFingerprint, ProfileFingerprintUx9G(current),
                StringComparison.Ordinal))
        {
            CancelLiveActionApprovalUx9G();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var command = new LiveDirectorActionCommand(Guid.NewGuid(), 1, "studio:ux9-g",
            current.ServerRevision, "studio.live.actions", NetworkRole.Administrator, false,
            BuildCandidateUx9G(current, approved));
        var dryRun = _liveActionHistoryUx9G.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelLiveActionApprovalUx9G();
            return new(false, HumanDiagnosticsUx9G(dryRun.Diagnostics));
        }
        var applied = _liveActionHistoryUx9G.Execute(command with { Apply = true });
        CancelLiveActionApprovalUx9G();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanDiagnosticsUx9G(applied.Diagnostics));
        _liveActionAuditUx9G.Add(applied.AuditEntry);
        return new(true,
            "Profil des actions appliqué par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9GActionResult UndoLiveActionUx9G()
    {
        CancelLiveActionApprovalUx9G();
        if (!EnsureLiveActionUx9G())
            return new(false, _liveActionLoadMessageUx9G ?? "Aucun profil d’actions actif.");
        var result = _liveActionHistoryUx9G!.Undo("studio.live.actions",
            NetworkRole.Administrator);
        if (result.Succeeded) _liveActionAuditUx9G.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification des actions annulée." :
            HumanDiagnosticsUx9G(result.Diagnostics));
    }

    internal StudioUx9GActionResult RedoLiveActionUx9G()
    {
        CancelLiveActionApprovalUx9G();
        if (!EnsureLiveActionUx9G())
            return new(false, _liveActionLoadMessageUx9G ?? "Aucun profil d’actions actif.");
        var result = _liveActionHistoryUx9G!.Redo("studio.live.actions",
            NetworkRole.Administrator);
        if (result.Succeeded) _liveActionAuditUx9G.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? "Modification des actions rétablie." :
            HumanDiagnosticsUx9G(result.Diagnostics));
    }

    internal StudioUx9GActionResult SaveLiveActionUx9G()
    {
        CancelLiveActionApprovalUx9G();
        if (!EnsureLiveActionUx9G() || Session is null)
            return new(false, _liveActionLoadMessageUx9G ?? "Aucun profil d’actions actif.");
        var result = WorkspaceLiveDirectorActionStore.Save(Session.RootDirectory,
            _liveActionHistoryUx9G!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "Les actions Live Director n’ont pas été enregistrées.");
        _persistedLiveActionUx9G = _liveActionHistoryUx9G.Current;
        _liveActionFileMissingUx9G = false;
        return new(true, "Actions Live Director enregistrées atomiquement dans le workspace.");
    }

    internal StudioUx9GActionResult RunLocalLiveActionUx9G(bool confirmed, NetworkRole role,
        StudioUx9GProbeOptions? options = null)
    {
        if (!confirmed)
        {
            _liveActionAuditUx9G.Add("rejected:studio.live-actions:confirmation-required");
            return new(false, "Confirmez explicitement la sonde locale des cinq actions.");
        }
        if (role != NetworkRole.Administrator)
        {
            _liveActionAuditUx9G.Add("rejected:studio.live-actions:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sonde.");
        }
        if (!EnsureLiveActionUx9G())
            return new(false, _liveActionLoadMessageUx9G ?? "Aucun profil d’actions actif.");
        try
        {
            var probe = RunLiveActionProbeUx9G(_liveActionHistoryUx9G!.Current, true,
                options ?? StudioUx9GProbeOptions.CreateDefault());
            _liveActionProbeUx9G = probe.Snapshot;
            _liveActionAuditUx9G.AddRange(probe.Audit);
            var succeeded = probe.Relocation && probe.Destruction && probe.Healing &&
                            probe.Limbo && probe.Possession && probe.Compensation &&
                            probe.Snapshot.ResourcesReleased;
            return new(succeeded, succeeded
                ? "Sonde locale terminée · actions compensées et possession restituée."
                : "La sonde locale n’a pas validé les cinq actions Live Director.");
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            _liveActionAuditUx9G.Add($"rejected:studio.live-actions:{exception.Message}");
            return new(false, $"La sonde locale des actions a échoué : {exception.Message}");
        }
    }

    internal StudioUx9GApprovalSnapshot CaptureLiveActionApprovalUx9G() =>
        new(_approvedLiveActionUx9G, _approvedLiveActionFingerprintUx9G);

    internal void RestoreLiveActionApprovalUx9G(StudioUx9GApprovalSnapshot snapshot)
    {
        _approvedLiveActionUx9G = snapshot.Draft;
        _approvedLiveActionFingerprintUx9G = snapshot.DraftFingerprint;
    }

    internal void CancelLiveActionApprovalUx9G()
    {
        _approvedLiveActionUx9G = null;
        _approvedLiveActionFingerprintUx9G = null;
    }

    internal void CloseUx9G()
    {
        CancelLiveActionApprovalUx9G();
        _liveActionHistoryUx9G = null;
        _persistedLiveActionUx9G = null;
        _liveActionWorkspaceRootUx9G = null;
        _liveActionLoadMessageUx9G = null;
        _liveActionFileMissingUx9G = false;
        _liveActionProbeUx9G = EmptyProbeUx9G();
        _liveActionAuditUx9G.Clear();
    }

    internal StudioUx9GSmokeResult RunUx9GContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9G();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureLiveActionApprovalUx9G();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-g-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9G();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            var baseline = LiveDirectorActionProfile.CreateDefault("evidence:ux9-g");
            if (!WorkspaceLiveDirectorActionStore.Save(root, baseline).Succeeded) return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var draft = editing.CreateLiveActionDraftUx9G(explicitlySelected: true);
            if (draft is null) return result;
            var fixture = draft with
            {
                MaximumPossessionMinutes = 45,
                RequiredAtRuntime = true
            };
            var invalid = !editing.PreviewLiveActionUx9G(fixture with
            { MaximumPossessionMinutes = 0 }).Succeeded;
            var staleSource = !editing.PreviewLiveActionUx9G(fixture with
            { BaseServerRevision = fixture.BaseServerRevision + 1 }).Succeeded;
            var preview = editing.PreviewLiveActionUx9G(fixture);
            var staleApproval = !editing.ApplyApprovedLiveActionUx9G(fixture with
            { MaximumPossessionMinutes = 44 }).Succeeded;
            preview = editing.PreviewLiveActionUx9G(fixture);
            var applied = editing.ApplyApprovedLiveActionUx9G(fixture);
            var current = editing.CurrentLiveActionUx9G;
            if (current is null) return result;
            var fiveRoutes = current.Relocation is
            {
                NavigationProbe: true, PlayerConsent: true, PartyPolicy: true,
                PreserveOrientation: true, Preview: true
            } &&
                             current.Destruction is
                             {
                                 CauseRequired: true, ConfirmationRequired: true,
                                 QuestImpactPreview: true, RollbackLimitVisible: true
                             } &&
                             current.Healing is
                             {
                                 ReasonRequired: true, Preview: true,
                                 RuntimeResourceAdapter: true
                             } && current.Healing.Scopes.Count == 4 &&
                             current.Limbo is
                             {
                                 PreserveIdentity: true, AiToggle: true, MovementPolicy: true,
                                 CollisionPolicy: true, VisibilityPolicy: true,
                                 RuntimeNodeAdapter: true
                             } &&
                             current.Possession is
                             {
                                 MaximumMinutes: 45, InputContext: true, ReturnPoint: true,
                                 ProtectedPlayerConsent: true, Expiry: true,
                                 RuntimeInputAdapter: true
                             };
            var undo = editing.UndoLiveActionUx9G().Succeeded;
            var redo = editing.RedoLiveActionUx9G().Succeeded;
            var saved = editing.SaveLiveActionUx9G().Succeeded;
            var permission = !editing.RunLocalLiveActionUx9G(true, NetworkRole.Player).Succeeded;
            var confirmation = !editing.RunLocalLiveActionUx9G(false,
                NetworkRole.Administrator).Succeeded;
            var probe = editing.RunLocalLiveActionUx9G(true,
                NetworkRole.Administrator);
            var snapshot = editing.LiveActionProbeUx9G;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              reopened.CurrentLiveActionUx9G is { } loaded &&
                              new LiveDirectorActionStore().Serialize(current) ==
                              new LiveDirectorActionStore().Serialize(loaded);
            var compensation = snapshot.RelocationCompensationCode ==
                               "LIVE_ACTION_COMPENSATED" && snapshot.DestructionRestored &&
                               snapshot.ReturnPointRestored;
            var audit = editing.LiveActionAuditUx9G.Count >= 7;
            result = new(true, fiveRoutes, true, invalid, permission, confirmation,
                preview.Succeeded && preview.CanApply && preview.CanonicalDryRun,
                applied.Succeeded, staleSource, staleApproval, undo && redo, persistence,
                snapshot.RelocationCode == "LIVE_ACTION_APPLIED",
                snapshot.DestructionCode == "LIVE_ACTION_APPLIED",
                snapshot.HealingCode == "LIVE_ACTION_APPLIED",
                snapshot.LimboCode == "LIVE_ACTION_APPLIED",
                snapshot.PossessionCode == "LIVE_CONTROL_APPLIED" &&
                snapshot.PossessionExpired, compensation, audit, false, false);
            result = result with
            {
                Succeeded = result.FiveRoutes && result.ExplicitSelection &&
                            result.InvalidBlocked && result.PermissionBlocked &&
                            result.ConfirmationRequired && result.CanonicalDryRun &&
                            result.CanonicalCommand && result.StaleSourceBlocked &&
                            result.StaleApprovalBlocked && result.UndoRedo &&
                            result.Persistence && result.RelocationRuntime &&
                            result.DestructionRuntime && result.HealingRuntime &&
                            result.LimboRuntime && result.PossessionRuntime &&
                            result.Compensation && result.Audit && probe.Succeeded
            };
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            var released = DeleteUx9GTemporaryWorkspace(root);
            var sourceUnchanged = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  ReferenceEquals(sourceSession, Session) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureLiveActionApprovalUx9G() == sourceApproval;
            result = result with
            {
                SourceUnchanged = sourceUnchanged,
                TemporaryWorkspaceReleased = released,
                Succeeded = result.Succeeded && sourceUnchanged && released
            };
        }
        return result;
    }

    private bool EnsureLiveActionUx9G()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _liveActionLoadMessageUx9G = "Ouvrez d’abord un module.";
            return false;
        }
        var fullRoot = Path.GetFullPath(root);
        if (_liveActionHistoryUx9G is not null &&
            string.Equals(_liveActionWorkspaceRootUx9G, fullRoot,
                StringComparison.OrdinalIgnoreCase))
            return true;
        var loaded = WorkspaceLiveDirectorActionStore.Load(fullRoot);
        if (!loaded.Succeeded)
        {
            _liveActionHistoryUx9G = null;
            _persistedLiveActionUx9G = null;
            _liveActionWorkspaceRootUx9G = fullRoot;
            _liveActionFileMissingUx9G = false;
            _liveActionLoadMessageUx9G =
                $"Actions Live Director invalides : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _liveActionHistoryUx9G = new LiveDirectorActionHistory(loaded.Profile);
        _persistedLiveActionUx9G = loaded.Profile;
        _liveActionWorkspaceRootUx9G = fullRoot;
        _liveActionFileMissingUx9G = loaded.FileMissing;
        _liveActionLoadMessageUx9G = loaded.FileMissing
            ? "Aucun profil d’actions enregistré · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static LiveDirectorActionProfile BuildCandidateUx9G(
        LiveDirectorActionProfile current, StudioUx9GLiveActionDraft draft) => current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-g",
            RequiredAtRuntime = draft.RequiredAtRuntime,
            Relocation = current.Relocation with
            {
                NavigationProbe = true,
                PlayerConsent = true,
                PartyPolicy = true,
                PreserveOrientation = true,
                Preview = true
            },
            Destruction = current.Destruction with
            {
                CauseRequired = true,
                ConfirmationRequired = true,
                LootPolicies = ["preserve", "discard"],
                QuestImpactPreview = true,
                RollbackLimitVisible = true
            },
            Healing = current.Healing with
            {
                Scopes = ["health", "resources", "effects", "all"],
                ReasonRequired = true,
                Preview = true,
                RuntimeResourceAdapter = true
            },
            Limbo = current.Limbo with
            {
                PreserveIdentity = true,
                AiToggle = true,
                MovementPolicy = true,
                CollisionPolicy = true,
                VisibilityPolicy = true,
                RuntimeNodeAdapter = true
            },
            Possession = current.Possession with
            {
                MaximumMinutes = draft.MaximumPossessionMinutes,
                InputContext = true,
                ReturnPoint = true,
                ProtectedPlayerConsent = true,
                Expiry = true,
                RuntimeInputAdapter = true
            }
        };

    private static LiveActionProbeResultUx9G RunLiveActionProbeUx9G(
        LiveDirectorActionProfile profile, bool applyRuntime, StudioUx9GProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.PartyPolicy is not ("target-only" or "party-consenting" or
                "party-all-npcs") || string.IsNullOrWhiteSpace(options.DestructionCause) ||
            options.LootPolicy is not ("preserve" or "discard") ||
            options.QuestImpact is not ("none" or "previewed" or "recorded") ||
            options.RollbackLimit is not ("session-only" or "checkpoint-only" or "none") ||
            options.HealingScope is not ("health" or "resources" or "effects" or "all") ||
            string.IsNullOrWhiteSpace(options.HealingReason) ||
            options.AiState is not ("suspended" or "active") ||
            options.MovementPolicy is not ("blocked" or "preserved" or "free") ||
            options.CollisionPolicy is not ("disabled" or "preserved" or "enabled") ||
            options.VisibilityPolicy is not ("hidden" or "preserved" or "visible") ||
            options.PossessionMinutes < 1 ||
            options.PossessionMinutes > profile.Possession.MaximumMinutes ||
            options.InputContext is not ("gm" or "director" or "spectator") ||
            string.IsNullOrWhiteSpace(options.ReturnPoint))
            throw new ArgumentException("LIVE_STUDIO_ACTION_OPTIONS_INVALID", nameof(options));

        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var runtime = LiveDirectorActionRuntimeAdapter.Create(profile, () => now);
        var zoneA = Guid.Parse("d1260000-0000-4000-8000-000000000100");
        var zoneB = Guid.Parse("d1260000-0000-4000-8000-000000000101");
        var actorId = Guid.Parse("d1260000-0000-4000-8000-000000000102");
        var playerId = Guid.Parse("d1260000-0000-4000-8000-000000000103");
        var relocateId = Guid.Parse("d1260000-0000-4000-8000-000000000110");
        var destinationId = Guid.Parse("d1260000-0000-4000-8000-000000000111");
        var destroyId = Guid.Parse("d1260000-0000-4000-8000-000000000112");
        var healId = Guid.Parse("d1260000-0000-4000-8000-000000000113");
        var limboId = Guid.Parse("d1260000-0000-4000-8000-000000000114");
        var possessId = Guid.Parse("d1260000-0000-4000-8000-000000000115");
        runtime.Chooser.Upsert(new(relocateId, zoneA, "creature", "Elyra", "player",
            playerId, "active", [], "crypt", false, 1, 0, 1, 87));
        runtime.Chooser.Upsert(new(destinationId, zoneB, "waypoint", "Camp sûr", "safe",
            null, "active", [], "surface", false, 10, 2, 30, 12));
        runtime.Chooser.Upsert(new(destroyId, zoneA, "creature", "Gardien", "guardian",
            null, "active", [], "crypt"));
        runtime.Chooser.Upsert(new(healId, zoneA, "creature", "Cael", "companion",
            null, "wounded", [], "crypt"));
        runtime.Chooser.Upsert(new(limboId, zoneA, "creature", "Spectre", "specter",
            null, "active", [], "crypt"));
        runtime.Chooser.Upsert(new(possessId, zoneA, "creature", "Messager", "player",
            playerId, "active", [], "crypt"));
        var actor = new AuthorizationSubject(actorId, NetworkRole.Administrator,
            new HashSet<Guid> { zoneA, zoneB });
        var audit = new List<string>();
        long revision = 0;

        var relocateCommand = Guid.Parse("d1260000-0000-4000-8000-000000000120");
        var relocateRequest = new LiveWorldActionService.Request(relocateCommand,
            "world.relocate", relocateId, destinationId, revision,
            new Dictionary<string, string>
            {
                ["navigationSafe"] = "true",
                ["consent"] = options.PlayerConsent ? "granted" : "denied",
                ["partyPolicy"] = options.PartyPolicy
            });
        var relocation = runtime.WorldActions.Execute(actor, relocateRequest, false);
        audit.Add(relocation.AuditEntry);
        var relocationCompensation = "LIVE_ACTION_COMPENSATION_NOT_REQUIRED";
        var orientationPreserved = relocation.Target?.Heading == 87;
        if (applyRuntime)
        {
            relocation = runtime.WorldActions.Execute(actor, relocateRequest, true);
            revision = relocation.Revision;
            orientationPreserved = relocation.Target?.Heading == 87;
            audit.Add(relocation.AuditEntry);
            audit.Add(runtime.WorldActions.Compensate(actor, relocateCommand, revision,
                false).AuditEntry);
            var compensated = runtime.WorldActions.Compensate(actor, relocateCommand,
                revision, true);
            revision = compensated.Revision;
            relocationCompensation = compensated.Code;
            audit.Add(compensated.AuditEntry);
        }

        var destroyCommand = Guid.Parse("d1260000-0000-4000-8000-000000000121");
        var destroyRequest = new LiveWorldActionService.Request(destroyCommand,
            "world.destroy", destroyId, null, revision,
            new Dictionary<string, string>
            {
                ["cause"] = options.DestructionCause,
                ["confirmed"] = options.DestructionConfirmed ? "true" : "false",
                ["lootPolicy"] = options.LootPolicy,
                ["questImpact"] = options.QuestImpact,
                ["rollback"] = options.RollbackLimit
            });
        var destruction = runtime.WorldActions.Execute(actor, destroyRequest, false);
        audit.Add(destruction.AuditEntry);
        var destructionRestored = !applyRuntime;
        if (applyRuntime)
        {
            destruction = runtime.WorldActions.Execute(actor, destroyRequest, true);
            revision = destruction.Revision;
            audit.Add(destruction.AuditEntry);
            var compensated = runtime.WorldActions.Compensate(actor, destroyCommand,
                revision, true);
            revision = compensated.Revision;
            destructionRestored = compensated.Code == "LIVE_ACTION_COMPENSATED" &&
                                  runtime.Chooser.Find(destroyId)?.Name == "Gardien";
            audit.Add(compensated.AuditEntry);
        }

        var healCommand = Guid.Parse("d1260000-0000-4000-8000-000000000122");
        var healRequest = new LiveWorldActionService.Request(healCommand, "world.heal",
            healId, null, revision, new Dictionary<string, string>
            {
                ["scope"] = options.HealingScope,
                ["reason"] = options.HealingReason
            });
        var healing = runtime.WorldActions.Execute(actor, healRequest, false);
        audit.Add(healing.AuditEntry);
        var resourcesRestored = !applyRuntime;
        if (applyRuntime)
        {
            healing = runtime.WorldActions.Execute(actor, healRequest, true);
            revision = healing.Revision;
            audit.Add(healing.AuditEntry);
            var compensated = runtime.WorldActions.Compensate(actor, healCommand,
                revision, true);
            revision = compensated.Revision;
            resourcesRestored = compensated.Target?.State == "wounded";
            audit.Add(compensated.AuditEntry);
        }

        var limboCommand = Guid.Parse("d1260000-0000-4000-8000-000000000123");
        var limboRequest = new LiveWorldActionService.Request(limboCommand, "world.freeze",
            limboId, null, revision, new Dictionary<string, string>
            {
                ["ai"] = options.AiState,
                ["movement"] = options.MovementPolicy,
                ["collision"] = options.CollisionPolicy,
                ["visibility"] = options.VisibilityPolicy
            });
        var beforeLimbo = runtime.Chooser.Find(limboId)!;
        var limbo = runtime.WorldActions.Execute(actor, limboRequest, false);
        audit.Add(limbo.AuditEntry);
        var identityPreserved = limbo.Target?.ObjectId == beforeLimbo.ObjectId &&
                                limbo.Target.Name == beforeLimbo.Name;
        if (applyRuntime)
        {
            limbo = runtime.WorldActions.Execute(actor, limboRequest, true);
            revision = limbo.Revision;
            identityPreserved = limbo.Target?.ObjectId == beforeLimbo.ObjectId &&
                                limbo.Target.Name == beforeLimbo.Name;
            audit.Add(limbo.AuditEntry);
            var compensated = runtime.WorldActions.Compensate(actor, limboCommand,
                revision, true);
            revision = compensated.Revision;
            audit.Add(compensated.AuditEntry);
        }

        var possessRequest = new Forge.LiveDirector.Control.LiveDirectorControlService.Request(
            Guid.Parse("d1260000-0000-4000-8000-000000000124"), "director.possess",
            possessId, 0, TimeSpan.FromMinutes(options.PossessionMinutes),
            new Dictionary<string, string>
            {
                ["inputContext"] = options.InputContext,
                ["returnPoint"] = options.ReturnPoint,
                ["protectedPlayerConsent"] = options.ProtectedPlayerConsent
                    ? "granted" : "denied"
            });
        var possession = runtime.Control.Execute(actor, possessRequest, false);
        audit.Add(possession.AuditEntry);
        var possessionExpired = !applyRuntime;
        var returnPointRestored = !applyRuntime;
        if (applyRuntime)
        {
            possession = runtime.Control.Execute(actor, possessRequest, true);
            audit.Add(possession.AuditEntry);
            now = now.AddMinutes(options.PossessionMinutes + 1);
            var expiry = runtime.Control.ReleaseExpiredPossession(actorId);
            audit.Add(expiry);
            possessionExpired = expiry.StartsWith("LIVE_POSSESSION_RETURNED:",
                StringComparison.Ordinal);
            returnPointRestored = runtime.Chooser.Find(possessId)?.State == "active";
        }

        var previewCode = applyRuntime ? "LIVE_ACTION_APPLIED" : "LIVE_ACTION_PREVIEW";
        var relocationReady = relocation.Code == previewCode && orientationPreserved &&
                              (!applyRuntime || relocationCompensation ==
                                  "LIVE_ACTION_COMPENSATED");
        var destructionReady = destruction.Code == previewCode && destructionRestored;
        var healingReady = healing.Code == previewCode && resourcesRestored;
        var limboReady = limbo.Code == previewCode && identityPreserved;
        var possessionReady = possession.Code == (applyRuntime
            ? "LIVE_CONTROL_APPLIED" : "LIVE_CONTROL_PREVIEW") && possessionExpired &&
                              returnPointRestored;
        var compensationReady = !applyRuntime || relocationCompensation ==
                                "LIVE_ACTION_COMPENSATED" && destructionRestored &&
                                resourcesRestored && returnPointRestored;
        var resourcesReleased = !applyRuntime || runtime.Chooser.Find(relocateId)?.State ==
                                "active" && runtime.Chooser.Find(destroyId) is not null &&
                                runtime.Chooser.Find(healId)?.State == "wounded" &&
                                runtime.Chooser.Find(limboId)?.State == "active" &&
                                runtime.Chooser.Find(possessId)?.State == "active";
        var snapshot = new StudioUx9GLiveActionProbe(
            relocationReady && destructionReady && healingReady && limboReady &&
            possessionReady && compensationReady && resourcesReleased
                ? "Cinq actions locales validées"
                : "Sonde des actions incomplète",
            relocation.Code, orientationPreserved, relocationCompensation,
            destruction.Code, destructionRestored, healing.Code, resourcesRestored,
            limbo.Code, identityPreserved, possession.Code, possessionExpired,
            returnPointRestored, audit.Count, resourcesReleased);
        return new(relocationReady, destructionReady, healingReady, limboReady,
            possessionReady, compensationReady, snapshot, audit);
    }

    private static string ProfileFingerprintUx9G(LiveDirectorActionProfile profile) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(new LiveDirectorActionStore().Serialize(profile))));

    private static string DraftFingerprintUx9G(StudioUx9GLiveActionDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(draft))));

    private static StudioUx9GLiveActionPreview FailedPreviewUx9G(string message,
        IReadOnlyList<NetworkDiagnostic>? diagnostics = null) => new(false, false, false,
        false, false, false, false, false, message, [],
        diagnostics?.Select(item => LiveDirectorActionLocalization.French(item.Code))
            .Distinct(StringComparer.Ordinal).ToArray() ?? [], EmptyProbeUx9G());

    private static string HumanDiagnosticsUx9G(
        IReadOnlyList<NetworkDiagnostic> diagnostics) => diagnostics.Count == 0
        ? "La commande n’a produit aucun changement applicable."
        : string.Join(" · ", diagnostics.Select(item =>
            LiveDirectorActionLocalization.French(item.Code)).Distinct(StringComparer.Ordinal));

    private static string HumanDiffUx9G(string value) => value switch
    {
        "relocation" => "Téléportation sûre et consentie",
        "destruction" => "Destruction confirmée et restaurable",
        "healing" => "Soin des ressources runtime",
        "limbo" => "Limbo avec identité préservée",
        "possession" => "Possession bornée avec retour",
        "runtime" => "Chargement runtime requis",
        _ => value
    };

    private static StudioUx9GLiveActionProbe EmptyProbeUx9G() => new(
        "Aucune sonde exécutée", "—", false, "—", "—", false, "—", false,
        "—", false, "—", false, false, 0, true);

    private static StudioUx9GSmokeResult FailedSmokeUx9G() => new(
        Succeeded: false, FiveRoutes: false, ExplicitSelection: false,
        InvalidBlocked: false, PermissionBlocked: false, ConfirmationRequired: false,
        CanonicalDryRun: false, CanonicalCommand: false, StaleSourceBlocked: false,
        StaleApprovalBlocked: false, UndoRedo: false, Persistence: false,
        RelocationRuntime: false, DestructionRuntime: false, HealingRuntime: false,
        LimboRuntime: false, PossessionRuntime: false, Compensation: false, Audit: false,
        SourceUnchanged: false, TemporaryWorkspaceReleased: false);

    private static bool DeleteUx9GTemporaryWorkspace(string root)
    {
        try
        {
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), temp,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-g-",
                    StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record LiveActionProbeResultUx9G(
        bool Relocation,
        bool Destruction,
        bool Healing,
        bool Limbo,
        bool Possession,
        bool Compensation,
        StudioUx9GLiveActionProbe Snapshot,
        IReadOnlyList<string> Audit);
}
