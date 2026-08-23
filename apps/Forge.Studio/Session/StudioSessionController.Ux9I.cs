using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Players;
using Forge.LiveDirector.World;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private LiveDirectorGroupHistory? _liveGroupHistoryUx9I;
    private LiveDirectorGroupProfile? _persistedLiveGroupUx9I;
    private string? _liveGroupWorkspaceRootUx9I;
    private StudioUx9IGroupDraft? _approvedLiveGroupUx9I;
    private string? _approvedLiveGroupFingerprintUx9I;
    private string? _liveGroupLoadMessageUx9I;
    private bool _liveGroupFileMissingUx9I;
    private StudioUx9IGroupProbe _liveGroupProbeUx9I = EmptyProbeUx9I();
    private readonly List<string> _liveGroupAuditUx9I = [];

    internal string? LiveGroupLoadMessageUx9I => _liveGroupLoadMessageUx9I;
    internal bool LiveGroupFileMissingUx9I => _liveGroupFileMissingUx9I;
    internal StudioUx9IGroupProbe LiveGroupProbeUx9I => _liveGroupProbeUx9I;
    internal IReadOnlyList<string> LiveGroupAuditUx9I => _liveGroupAuditUx9I.ToArray();
    internal LiveDirectorGroupProfile? CurrentLiveGroupUx9I =>
        EnsureLiveGroupUx9I() ? _liveGroupHistoryUx9I!.Current : null;
    internal bool LiveGroupHasUnsavedChangesUx9I =>
        EnsureLiveGroupUx9I() && _persistedLiveGroupUx9I is not null &&
        ProfileFingerprintUx9I(_liveGroupHistoryUx9I!.Current) !=
        ProfileFingerprintUx9I(_persistedLiveGroupUx9I);

    internal StudioUx9IGroupDraft? CreateLiveGroupDraftUx9I(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureLiveGroupUx9I()) return null;
        var profile = _liveGroupHistoryUx9I!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9I(profile),
            profile.SelectionCap, profile.RequiredAtRuntime);
    }

    internal StudioUx9IGroupPreview PreviewLiveGroupUx9I(StudioUx9IGroupDraft draft,
        StudioUx9IGroupProbeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLiveGroupApprovalUx9I();
        if (!EnsureLiveGroupUx9I())
            return FailedPreviewUx9I(_liveGroupLoadMessageUx9I ??
                                     "Le profil de groupe Live Director ne peut pas être chargé.");
        var current = _liveGroupHistoryUx9I!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            draft.SourceProfileFingerprint != ProfileFingerprintUx9I(current))
            return FailedPreviewUx9I(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");
        var candidate = BuildCandidateUx9I(current, draft);
        var command = new LiveDirectorGroupCommand(Guid.NewGuid(), 1, "studio:ux9-i",
            current.ServerRevision, "studio.live.group", NetworkRole.Administrator, false,
            candidate);
        var dryRun = _liveGroupHistoryUx9I.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedPreviewUx9I(HumanDiagnosticsUx9I(dryRun.Diagnostics),
                dryRun.Diagnostics);
        GroupProbeResultUx9I probe;
        try
        {
            probe = RunLiveGroupProbeUx9I(candidate, false,
                options ?? StudioUx9IGroupProbeOptions.CreateDefault());
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            return FailedPreviewUx9I(
                $"La sonde locale de groupe a refusé la configuration : {exception.Message}");
        }
        _liveGroupProbeUx9I = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only" ? [] :
            dryRun.SemanticDiff.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = changes.Length > 0 && probe.Trigger && probe.Selection &&
                       probe.Movement && probe.Attack && probe.Travel;
        if (canApply)
        {
            _approvedLiveGroupUx9I = draft with { };
            _approvedLiveGroupFingerprintUx9I = DraftFingerprintUx9I(draft);
        }
        return new(true, canApply, true, probe.Trigger, probe.Selection, probe.Movement,
            probe.Attack, probe.Travel,
            canApply
                ? "Aperçu sec validé · aucun trigger, groupe ou joueur réel n’a été modifié."
                : "Aperçu sec incomplet · vérifiez avertissement de faction et consentements.",
            changes.Select(HumanDiffUx9I).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9IActionResult ApplyApprovedLiveGroupUx9I(StudioUx9IGroupDraft draft)
    {
        var approved = _approvedLiveGroupUx9I;
        if (approved is null || _approvedLiveGroupFingerprintUx9I is null ||
            !EnsureLiveGroupUx9I())
        {
            CancelLiveGroupApprovalUx9I();
            return new(false, "Prévisualisez d’abord le profil de groupe.");
        }
        var current = _liveGroupHistoryUx9I!.Current;
        if (DraftFingerprintUx9I(draft) != _approvedLiveGroupFingerprintUx9I ||
            DraftFingerprintUx9I(approved) != _approvedLiveGroupFingerprintUx9I ||
            approved.BaseServerRevision != current.ServerRevision ||
            approved.SourceProfileFingerprint != ProfileFingerprintUx9I(current))
        {
            CancelLiveGroupApprovalUx9I();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var command = new LiveDirectorGroupCommand(Guid.NewGuid(), 1, "studio:ux9-i",
            current.ServerRevision, "studio.live.group", NetworkRole.Administrator, false,
            BuildCandidateUx9I(current, approved));
        var dryRun = _liveGroupHistoryUx9I.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelLiveGroupApprovalUx9I();
            return new(false, HumanDiagnosticsUx9I(dryRun.Diagnostics));
        }
        var applied = _liveGroupHistoryUx9I.Execute(command with { Apply = true });
        CancelLiveGroupApprovalUx9I();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanDiagnosticsUx9I(applied.Diagnostics));
        _liveGroupAuditUx9I.Add(applied.AuditEntry);
        return new(true,
            "Profil de groupe appliqué par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9IActionResult UndoLiveGroupUx9I() => MoveHistoryUx9I(true);
    internal StudioUx9IActionResult RedoLiveGroupUx9I() => MoveHistoryUx9I(false);

    private StudioUx9IActionResult MoveHistoryUx9I(bool undo)
    {
        CancelLiveGroupApprovalUx9I();
        if (!EnsureLiveGroupUx9I())
            return new(false, _liveGroupLoadMessageUx9I ?? "Aucun profil de groupe actif.");
        var result = undo
            ? _liveGroupHistoryUx9I!.Undo("studio.live.group", NetworkRole.Administrator)
            : _liveGroupHistoryUx9I!.Redo("studio.live.group", NetworkRole.Administrator);
        if (result.Succeeded) _liveGroupAuditUx9I.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? undo ? "Modification de groupe annulée." :
                "Modification de groupe rétablie." : HumanDiagnosticsUx9I(result.Diagnostics));
    }

    internal StudioUx9IActionResult SaveLiveGroupUx9I()
    {
        CancelLiveGroupApprovalUx9I();
        if (!EnsureLiveGroupUx9I() || Session is null)
            return new(false, _liveGroupLoadMessageUx9I ?? "Aucun profil de groupe actif.");
        var result = WorkspaceLiveDirectorGroupStore.Save(Session.RootDirectory,
            _liveGroupHistoryUx9I!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "Le profil de groupe n'a pas été enregistré.");
        _persistedLiveGroupUx9I = _liveGroupHistoryUx9I.Current;
        _liveGroupFileMissingUx9I = false;
        return new(true, "Profil de groupe enregistré atomiquement dans le workspace.");
    }

    internal StudioUx9IActionResult RunLocalLiveGroupUx9I(bool confirmed, NetworkRole role,
        StudioUx9IGroupProbeOptions? options = null)
    {
        var actual = options ?? StudioUx9IGroupProbeOptions.CreateDefault();
        if (!confirmed)
        {
            _liveGroupAuditUx9I.Add("rejected:studio.live-group:confirmation-required");
            return new(false, "Confirmez explicitement la sandbox locale de groupe.");
        }
        if (role != NetworkRole.Administrator)
        {
            _liveGroupAuditUx9I.Add("rejected:studio.live-group:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sandbox.");
        }
        if (actual.PlayerTravelMode == "summon-party" && !actual.PartyConsentConfirmed)
        {
            _liveGroupAuditUx9I.Add("rejected:studio.live-group:party-consent-required");
            return new(false, "Confirmez le consentement du groupe avant son rappel.");
        }
        if (!EnsureLiveGroupUx9I())
            return new(false, _liveGroupLoadMessageUx9I ?? "Aucun profil de groupe actif.");
        try
        {
            var probe = RunLiveGroupProbeUx9I(_liveGroupHistoryUx9I!.Current, true, actual);
            _liveGroupProbeUx9I = probe.Snapshot;
            _liveGroupAuditUx9I.AddRange(probe.Audit);
            var ok = probe.Trigger && probe.Selection && probe.Movement && probe.Attack &&
                     probe.Travel && probe.Compensation && probe.Snapshot.ResourcesReleased;
            return new(ok, ok
                ? "Sandbox terminée · trigger simulé, groupe et joueurs synthétiques restaurés."
                : "La sandbox locale n'a pas validé les cinq commandes de groupe.");
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            _liveGroupAuditUx9I.Add($"rejected:studio.live-group:{exception.Message}");
            return new(false, $"La sandbox locale de groupe a échoué : {exception.Message}");
        }
    }

    internal StudioUx9IApprovalSnapshot CaptureLiveGroupApprovalUx9I() =>
        new(_approvedLiveGroupUx9I, _approvedLiveGroupFingerprintUx9I);
    internal void RestoreLiveGroupApprovalUx9I(StudioUx9IApprovalSnapshot snapshot)
    {
        _approvedLiveGroupUx9I = snapshot.Draft;
        _approvedLiveGroupFingerprintUx9I = snapshot.DraftFingerprint;
    }
    internal void CancelLiveGroupApprovalUx9I()
    {
        _approvedLiveGroupUx9I = null;
        _approvedLiveGroupFingerprintUx9I = null;
    }
    internal void CloseUx9I()
    {
        CancelLiveGroupApprovalUx9I();
        _liveGroupHistoryUx9I = null;
        _persistedLiveGroupUx9I = null;
        _liveGroupWorkspaceRootUx9I = null;
        _liveGroupLoadMessageUx9I = null;
        _liveGroupFileMissingUx9I = false;
        _liveGroupProbeUx9I = EmptyProbeUx9I();
        _liveGroupAuditUx9I.Clear();
    }

    internal StudioUx9ISmokeResult RunUx9IContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9I();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureLiveGroupApprovalUx9I();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-i-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9I();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            var baseline = LiveDirectorGroupProfile.CreateDefault("evidence:ux9-i");
            if (!WorkspaceLiveDirectorGroupStore.Save(root, baseline).Succeeded) return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var draft = editing.CreateLiveGroupDraftUx9I(explicitlySelected: true);
            if (draft is null) return result;
            var fixture = draft with { SelectionCap = 96, RequiredAtRuntime = true };
            var invalid = !editing.PreviewLiveGroupUx9I(fixture with
            { SelectionCap = 0 }).Succeeded;
            var staleSource = !editing.PreviewLiveGroupUx9I(fixture with
            { BaseServerRevision = fixture.BaseServerRevision + 1 }).Succeeded;
            var preview = editing.PreviewLiveGroupUx9I(fixture);
            var staleApproval = !editing.ApplyApprovedLiveGroupUx9I(fixture with
            { SelectionCap = 95 }).Succeeded;
            preview = editing.PreviewLiveGroupUx9I(fixture);
            var applied = editing.ApplyApprovedLiveGroupUx9I(fixture);
            var current = editing.CurrentLiveGroupUx9I;
            if (current is null) return result;
            var fiveRoutes = current.TriggerEvents.SequenceEqual(["enter", "exit"]) &&
                             current.TriggerActorRequired && current.ScriptTracePreview &&
                             current.SelectionCap == 96 && current.PermissionFilteredSelection &&
                             current.StableSelectionIds && current.ViewportLasso &&
                             current.MovementFormations.SequenceEqual(
                                 ["line", "wedge", "circle", "free"]) &&
                             current.PathPreview && current.PartialFailureReport &&
                             current.RuntimePathAdapter && current.FactionSafetyAcknowledgement &&
                             current.AttackObjective && current.RuntimeCombatAdapter &&
                             current.PlayerTravelModes.Count == 4 && current.PlayerConfirmation &&
                             current.PartyConsent && current.CameraTransitionAdapter;
            var undo = editing.UndoLiveGroupUx9I().Succeeded;
            var redo = editing.RedoLiveGroupUx9I().Succeeded;
            var saved = editing.SaveLiveGroupUx9I().Succeeded;
            var options = StudioUx9IGroupProbeOptions.CreateDefault();
            var permission = !editing.RunLocalLiveGroupUx9I(true, NetworkRole.Player,
                options).Succeeded;
            var confirmation = !editing.RunLocalLiveGroupUx9I(false,
                NetworkRole.Administrator, options).Succeeded;
            var partyConsent = !editing.RunLocalLiveGroupUx9I(true,
                NetworkRole.Administrator, options with
                { PartyConsentConfirmed = false }).Succeeded;
            var probed = editing.RunLocalLiveGroupUx9I(true,
                NetworkRole.Administrator, options);
            var snapshot = editing.LiveGroupProbeUx9I;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              reopened.CurrentLiveGroupUx9I is { } loaded &&
                              new LiveDirectorGroupStore().Serialize(current) ==
                              new LiveDirectorGroupStore().Serialize(loaded);
            result = new(true, fiveRoutes, true, invalid, permission, confirmation,
                partyConsent, preview.Succeeded && preview.CanApply && preview.CanonicalDryRun,
                applied.Succeeded, staleSource, staleApproval, undo && redo, persistence,
                snapshot.TriggerCode == "LIVE_RULE_APPLIED" &&
                snapshot.TriggerTraceEntries == 3,
                snapshot.SelectionCount == 2 && snapshot.StableSelectionId,
                snapshot.MovementCode == "LIVE_MOVEMENT_PARTIAL" &&
                snapshot.PathAdapter && snapshot.PartialFailures == 1,
                snapshot.AttackCode == "LIVE_ATTACK_APPLIED" && snapshot.ObjectiveReady &&
                snapshot.CombatAdapter,
                snapshot.TravelCode == "LIVE_PLAYER_TRAVEL_APPLIED" &&
                snapshot.CameraTransition != "camera:none" && snapshot.PartyConsent,
                snapshot.Compensated, editing.LiveGroupAuditUx9I.Count >= 8, false, false);
            result = result with
            {
                Succeeded = result.FiveRoutes && result.ExplicitSelection &&
                            result.InvalidBlocked && result.PermissionBlocked &&
                            result.ConfirmationRequired && result.PartyConsentRequired &&
                            result.CanonicalDryRun && result.CanonicalCommand &&
                            result.StaleSourceBlocked && result.StaleApprovalBlocked &&
                            result.UndoRedo && result.Persistence && result.TriggerRuntime &&
                            result.SelectionRuntime && result.MovementRuntime &&
                            result.AttackRuntime && result.TravelRuntime &&
                            result.Compensation && result.Audit && probed.Succeeded
            };
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            var released = DeleteUx9ITemporaryWorkspace(root);
            var sourceUnchanged = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  ReferenceEquals(sourceSession, Session) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureLiveGroupApprovalUx9I() == sourceApproval;
            result = result with
            {
                SourceUnchanged = sourceUnchanged,
                TemporaryWorkspaceReleased = released,
                Succeeded = result.Succeeded && sourceUnchanged && released
            };
        }
        return result;
    }

    private bool EnsureLiveGroupUx9I()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _liveGroupLoadMessageUx9I = "Ouvrez d’abord un module.";
            return false;
        }
        var full = Path.GetFullPath(root);
        if (_liveGroupHistoryUx9I is not null &&
            string.Equals(_liveGroupWorkspaceRootUx9I, full,
                StringComparison.OrdinalIgnoreCase)) return true;
        var loaded = WorkspaceLiveDirectorGroupStore.Load(full);
        if (!loaded.Succeeded)
        {
            _liveGroupHistoryUx9I = null;
            _persistedLiveGroupUx9I = null;
            _liveGroupWorkspaceRootUx9I = full;
            _liveGroupFileMissingUx9I = false;
            _liveGroupLoadMessageUx9I =
                $"Profil de groupe invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _liveGroupHistoryUx9I = new(loaded.Profile);
        _persistedLiveGroupUx9I = loaded.Profile;
        _liveGroupWorkspaceRootUx9I = full;
        _liveGroupFileMissingUx9I = loaded.FileMissing;
        _liveGroupLoadMessageUx9I = loaded.FileMissing
            ? "Aucun profil de groupe enregistré · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static LiveDirectorGroupProfile BuildCandidateUx9I(
        LiveDirectorGroupProfile current, StudioUx9IGroupDraft draft) => current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-i",
            RequiredAtRuntime = draft.RequiredAtRuntime,
            TriggerEvents = ["enter", "exit"],
            TriggerActorRequired = true,
            ScriptTracePreview = true,
            SelectionCap = draft.SelectionCap,
            PermissionFilteredSelection = true,
            StableSelectionIds = true,
            ViewportLasso = true,
            MovementFormations = ["line", "wedge", "circle", "free"],
            PathPreview = true,
            PartialFailureReport = true,
            RuntimePathAdapter = true,
            FactionSafetyAcknowledgement = true,
            AttackObjective = true,
            RuntimeCombatAdapter = true,
            PlayerTravelModes = ["spectate", "teleport-gm", "summon-player", "summon-party"],
            PlayerConfirmation = true,
            PartyConsent = true,
            CameraTransitionAdapter = true
        };

    private static GroupProbeResultUx9I RunLiveGroupProbeUx9I(
        LiveDirectorGroupProfile profile, bool applyRuntime, StudioUx9IGroupProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.TriggerEvent is not ("enter" or "exit") ||
            string.IsNullOrWhiteSpace(options.TriggerPayload) ||
            options.MovementFormation is not ("line" or "wedge" or "circle" or "free") ||
            options.PlayerTravelMode is not ("spectate" or "teleport-gm" or
                "summon-player" or "summon-party"))
            throw new ArgumentException("LIVE_STUDIO_GROUP_OPTIONS_INVALID", nameof(options));

        int Level(long xp) => (int)(xp / 1_000) + 1;
        var runtime = LiveDirectorGroupRuntimeAdapter.Create(profile, Level);
        var zone = Guid.Parse("d1280000-0000-4000-8000-000000000200");
        var actorId = Guid.Parse("d1280000-0000-4000-8000-000000000201");
        var gmObject = Guid.Parse("d1280000-0000-4000-8000-000000000202");
        var first = Guid.Parse("d1280000-0000-4000-8000-000000000203");
        var blocked = Guid.Parse("d1280000-0000-4000-8000-000000000204");
        var enemy = Guid.Parse("d1280000-0000-4000-8000-000000000205");
        var trigger = Guid.Parse("d1280000-0000-4000-8000-000000000206");
        var destination = Guid.Parse("d1280000-0000-4000-8000-000000000207");
        var playerAccount = Guid.Parse("d1280000-0000-4000-8000-000000000208");
        var playerCharacter = Guid.Parse("d1280000-0000-4000-8000-000000000209");
        var partyAccount = Guid.Parse("d1280000-0000-4000-8000-00000000020a");
        var partyCharacter = Guid.Parse("d1280000-0000-4000-8000-00000000020b");
        var partyId = Guid.Parse("d1280000-0000-4000-8000-00000000020c");
        runtime.Chooser.Upsert(new(gmObject, zone, "director", "Maître du jeu", "gm", null,
            "active", [], "main", false, 0, 0, 0));
        runtime.Chooser.Upsert(new(first, zone, "creature", "Garde mobile", "garde", null,
            "active", [], "main", false, 1, 0, 1));
        runtime.Chooser.Upsert(new(blocked, zone, "creature", "Garde bloquée", "garde", null,
            "movement:blocked", [], "main", false, 2, 0, 2));
        runtime.Chooser.Upsert(new(enemy, zone, "creature", "Adversaire", "enemy", null,
            "active", [], "main", false, 9, 0, 9));
        runtime.Chooser.Upsert(new(trigger, zone, "trigger", "Seuil des cendres", "volume",
            null, "active", []));
        runtime.Chooser.Upsert(new(destination, zone, "waypoint", "Cour nord", "destination",
            null, "active", [], "main", false, 12, 0, 4));
        runtime.Chooser.Upsert(new(playerCharacter, zone, "creature", "Elyra", "player",
            playerAccount, "active", [], "main", false, 6, 0, 6));
        runtime.Chooser.Upsert(new(partyCharacter, zone, "creature", "Borin", "player",
            partyAccount, "active", [], "main", false, 7, 0, 7));
        runtime.Players.Upsert(new(playerAccount, playerCharacter, "elyra", "Elyra", zone, 42,
            partyId, "player", "connected", []));
        runtime.Players.Upsert(new(partyAccount, partyCharacter, "borin", "Borin", zone, 55,
            partyId, "player", "connected", []));
        var actor = new AuthorizationSubject(actorId, NetworkRole.Administrator,
            new HashSet<Guid> { zone });
        var audit = new List<string>();

        var restoreIds = new[] { first, blocked, gmObject, playerCharacter, partyCharacter };
        var originals = restoreIds.ToDictionary(id => id, id => runtime.Chooser.Find(id)!);

        var triggerRequest = new LiveProgressionWorldService.Request(
            Guid.Parse("d1280000-0000-4000-8000-000000000210"), "trigger.simulate", trigger,
            0, new Dictionary<string, string>
            {
                ["event"] = options.TriggerEvent,
                ["actor"] = first.ToString("D"),
                ["payload"] = options.TriggerPayload
            });
        var triggerResult = runtime.Progression.Execute(actor, triggerRequest, false);
        audit.Add(triggerResult.AuditEntry);
        if (applyRuntime)
        {
            triggerResult = runtime.Progression.Execute(actor, triggerRequest, true);
            audit.Add(triggerResult.AuditEntry);
        }
        var triggerReady = triggerResult.Code == (applyRuntime ? "LIVE_RULE_APPLIED" :
            "LIVE_RULE_PREVIEW") && triggerResult.ScriptTrace.Count == 3;

        var selectionId = Guid.Parse("d1280000-0000-4000-8000-000000000211");
        var selection = runtime.Groups.Select(actor, selectionId,
            new LiveWorldChooser.Query(zone, "creature", "Garde", null, null, "main"));
        audit.Add(selection.AuditEntry);
        var selectionReady = selection.SelectionId == selectionId &&
                             selection.ObjectIds.Count == 2 &&
                             selection.ObjectIds.Contains(first) &&
                             selection.ObjectIds.Contains(blocked);

        var movementCommand = new LiveGroupCommandService.Movement(
            Guid.Parse("d1280000-0000-4000-8000-000000000212"), selectionId, destination,
            options.MovementFormation, 0);
        var movementResult = runtime.Groups.Move(actor, movementCommand, false);
        audit.Add(movementResult.AuditEntry);
        var pathAdapter = movementResult.PathPreview.Contains("path-preview:",
            StringComparison.Ordinal) && movementResult.PathPreview.Contains(
            $"formation={options.MovementFormation}", StringComparison.Ordinal);
        if (applyRuntime)
        {
            movementResult = runtime.Groups.Move(actor, movementCommand, true);
            audit.Add(movementResult.AuditEntry);
        }
        var movementReady = movementResult.Code == (applyRuntime ? "LIVE_MOVEMENT_PARTIAL" :
            "LIVE_MOVEMENT_PREVIEW") && movementResult.Moved.Count == 1 &&
                            movementResult.Failed.Count == 1 && pathAdapter;

        var attackCommand = new LiveGroupCommandService.Attack(
            Guid.Parse("d1280000-0000-4000-8000-000000000213"), selectionId, enemy,
            options.FactionWarningAccepted, applyRuntime ? movementResult.Revision : 0);
        var attackResult = runtime.Groups.AttackTarget(actor, attackCommand, false);
        audit.Add(attackResult.AuditEntry);
        var objectiveReady = attackResult.ObjectivePreview.Contains("attack-objective:",
            StringComparison.Ordinal) && attackResult.ObjectivePreview.Contains(
            enemy.ToString(), StringComparison.Ordinal);
        if (applyRuntime && attackResult.Code == "LIVE_ATTACK_PREVIEW")
        {
            attackResult = runtime.Groups.AttackTarget(actor, attackCommand, true);
            audit.Add(attackResult.AuditEntry);
            objectiveReady = attackResult.Assigned.All(id => runtime.Chooser.Find(id)?.State ==
                $"attack-objective:{enemy}");
        }
        var attackReady = attackResult.Code == (applyRuntime ? "LIVE_ATTACK_APPLIED" :
            "LIVE_ATTACK_PREVIEW") && attackResult.Assigned.Count == 2 && objectiveReady &&
                          options.FactionWarningAccepted;

        var travelCommand = new LivePlayerService.Travel(
            Guid.Parse("d1280000-0000-4000-8000-000000000214"), options.PlayerTravelMode,
            playerAccount, gmObject, options.PlayerTravelConfirmed, 0);
        var travelResult = runtime.Players.TravelToPlayer(actor, travelCommand, false);
        audit.Add(travelResult.AuditEntry);
        var partyConsent = options.PlayerTravelMode != "summon-party" ||
                           options.PartyConsentConfirmed;
        if (applyRuntime && travelResult.Code == "LIVE_PLAYER_TRAVEL_PREVIEW" && partyConsent)
        {
            travelResult = runtime.Players.TravelToPlayer(actor, travelCommand, true);
            audit.Add(travelResult.AuditEntry);
        }
        var travelReady = travelResult.Code == (applyRuntime ? "LIVE_PLAYER_TRAVEL_APPLIED" :
            "LIVE_PLAYER_TRAVEL_PREVIEW") && travelResult.CameraTransition != "camera:none" &&
                          partyConsent && (options.PlayerTravelMode != "summon-party" ||
                                           travelResult.MovedObjects.Count == 2);

        var compensated = true;
        if (applyRuntime)
        {
            foreach (var entry in originals.Values) runtime.Chooser.Upsert(entry);
            compensated = originals.All(pair => runtime.Chooser.Find(pair.Key) == pair.Value);
        }
        var resourcesReleased = compensated;
        var compensationReady = !applyRuntime || compensated;
        var snapshot = new StudioUx9IGroupProbe(
            triggerReady && selectionReady && movementReady && attackReady && travelReady &&
            compensationReady && resourcesReleased
                ? "Cinq commandes de groupe locales validées"
                : "Sandbox de groupe incomplète",
            triggerResult.Code, triggerResult.ScriptTrace.Count, selection.ObjectIds.Count,
            selection.SelectionId == selectionId, movementResult.Code,
            movementResult.Moved.Count, movementResult.Failed.Count, pathAdapter,
            attackResult.Code, attackResult.Assigned.Count, objectiveReady,
            attackReady && profile.RuntimeCombatAdapter, travelResult.Code,
            travelResult.MovedObjects.Count, travelResult.CameraTransition, partyConsent,
            compensated, audit.Count, resourcesReleased);
        return new(triggerReady, selectionReady, movementReady, attackReady, travelReady,
            compensationReady, snapshot, audit);
    }

    private static string ProfileFingerprintUx9I(LiveDirectorGroupProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            new LiveDirectorGroupStore().Serialize(profile))));
    private static string DraftFingerprintUx9I(StudioUx9IGroupDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(draft))));
    private static StudioUx9IGroupPreview FailedPreviewUx9I(string message,
        IReadOnlyList<NetworkDiagnostic>? diagnostics = null) => new(false, false, false,
        false, false, false, false, false, message, [], diagnostics?.Select(item =>
            LiveDirectorGroupLocalization.French(item.Code)).Distinct().ToArray() ?? [],
        EmptyProbeUx9I());
    private static string HumanDiagnosticsUx9I(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        diagnostics.Count == 0 ? "La commande n'a produit aucun changement applicable." :
            string.Join(" · ", diagnostics.Select(item =>
                LiveDirectorGroupLocalization.French(item.Code)).Distinct());
    private static string HumanDiffUx9I(string value) => value switch
    {
        "trigger" => "Simulation de trigger et trace",
        "selection" => "Sélection permissionnée et plafond",
        "movement" => "Formations, chemins et échecs partiels",
        "attack" => "Sécurité de faction et objectif de combat",
        "player-travel" => "Voyage, consentement et transition caméra",
        "runtime" => "Chargement runtime requis",
        _ => value
    };
    private static StudioUx9IGroupProbe EmptyProbeUx9I() => new(
        "Aucune sandbox exécutée", "—", 0, 0, false, "—", 0, 0, false, "—", 0,
        false, false, "—", 0, "camera:none", false, false, 0, true);
    private static StudioUx9ISmokeResult FailedSmokeUx9I() => new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false);
    private static bool DeleteUx9ITemporaryWorkspace(string root)
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-i-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record GroupProbeResultUx9I(
        bool Trigger, bool Selection, bool Movement, bool Attack, bool Travel,
        bool Compensation, StudioUx9IGroupProbe Snapshot, IReadOnlyList<string> Audit);
}
