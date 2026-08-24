using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Encounters;
using Forge.LiveDirector.Narrative;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private LiveDirectorNarrativeHistory? _liveNarrativeHistoryUx9K;
    private LiveDirectorNarrativeProfile? _persistedLiveNarrativeUx9K;
    private string? _liveNarrativeWorkspaceRootUx9K;
    private StudioUx9KNarrativeDraft? _approvedLiveNarrativeUx9K;
    private string? _approvedLiveNarrativeFingerprintUx9K;
    private string? _liveNarrativeLoadMessageUx9K;
    private bool _liveNarrativeFileMissingUx9K;
    private StudioUx9KNarrativeProbe _liveNarrativeProbeUx9K = EmptyProbeUx9K();
    private readonly List<string> _liveNarrativeAuditUx9K = [];

    internal string? LiveNarrativeLoadMessageUx9K => _liveNarrativeLoadMessageUx9K;
    internal bool LiveNarrativeFileMissingUx9K => _liveNarrativeFileMissingUx9K;
    internal StudioUx9KNarrativeProbe LiveNarrativeProbeUx9K => _liveNarrativeProbeUx9K;
    internal IReadOnlyList<string> LiveNarrativeAuditUx9K => _liveNarrativeAuditUx9K.ToArray();
    internal LiveDirectorNarrativeProfile? CurrentLiveNarrativeUx9K =>
        EnsureLiveNarrativeUx9K() ? _liveNarrativeHistoryUx9K!.Current : null;
    internal bool LiveNarrativeHasUnsavedChangesUx9K =>
        EnsureLiveNarrativeUx9K() && _persistedLiveNarrativeUx9K is not null &&
        ProfileFingerprintUx9K(_liveNarrativeHistoryUx9K!.Current) !=
        ProfileFingerprintUx9K(_persistedLiveNarrativeUx9K);

    internal StudioUx9KNarrativeDraft? CreateLiveNarrativeDraftUx9K(
        bool explicitlySelected = false)
    {
        if (!explicitlySelected || !EnsureLiveNarrativeUx9K()) return null;
        var profile = _liveNarrativeHistoryUx9K!.Current;
        return new(profile.ServerRevision, ProfileFingerprintUx9K(profile),
            profile.RequiredAtRuntime);
    }

    internal StudioUx9KNarrativePreview PreviewLiveNarrativeUx9K(
        StudioUx9KNarrativeDraft draft, StudioUx9KNarrativeProbeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLiveNarrativeApprovalUx9K();
        if (!EnsureLiveNarrativeUx9K())
            return FailedPreviewUx9K(_liveNarrativeLoadMessageUx9K ??
                                     "Le profil narratif Live Director ne peut pas être chargé.");
        var current = _liveNarrativeHistoryUx9K!.Current;
        if (draft.BaseServerRevision != current.ServerRevision ||
            draft.SourceProfileFingerprint != ProfileFingerprintUx9K(current))
            return FailedPreviewUx9K(
                "La configuration a changé ; rechargez le formulaire avant l’aperçu sec.");
        var candidate = BuildCandidateUx9K(current, draft);
        var command = new LiveDirectorNarrativeCommand(Guid.NewGuid(), 1, "studio:ux9-k",
            current.ServerRevision, "studio.live.narrative", NetworkRole.Administrator, false,
            candidate);
        var dryRun = _liveNarrativeHistoryUx9K.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied)
            return FailedPreviewUx9K(HumanDiagnosticsUx9K(dryRun.Diagnostics),
                dryRun.Diagnostics);
        NarrativeProbeResultUx9K probe;
        try
        {
            probe = RunLiveNarrativeProbeUx9K(candidate, false,
                options ?? StudioUx9KNarrativeProbeOptions.CreateDefault());
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            return FailedPreviewUx9K(
                $"La sonde narrative locale a refusé la configuration : {exception.Message}");
        }
        _liveNarrativeProbeUx9K = probe.Snapshot;
        var changes = dryRun.SemanticDiff == "revision-only" ? [] :
            dryRun.SemanticDiff.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canApply = changes.Length > 0 && probe.Quest && probe.Encounter;
        if (canApply)
        {
            _approvedLiveNarrativeUx9K = draft with { };
            _approvedLiveNarrativeFingerprintUx9K = DraftFingerprintUx9K(draft);
        }
        return new(true, canApply, true, probe.Quest, probe.Encounter,
            canApply
                ? "Aperçu sec validé · aucune quête, rencontre ou partie réelle n’a été modifiée."
                : "Aperçu sec incomplet · vérifiez portée, transition, action et valeur.",
            changes.Select(HumanDiffUx9K).ToArray(), [], probe.Snapshot);
    }

    internal StudioUx9KActionResult ApplyApprovedLiveNarrativeUx9K(
        StudioUx9KNarrativeDraft draft)
    {
        var approved = _approvedLiveNarrativeUx9K;
        if (approved is null || _approvedLiveNarrativeFingerprintUx9K is null ||
            !EnsureLiveNarrativeUx9K())
        {
            CancelLiveNarrativeApprovalUx9K();
            return new(false, "Prévisualisez d’abord le profil narratif.");
        }
        var current = _liveNarrativeHistoryUx9K!.Current;
        if (DraftFingerprintUx9K(draft) != _approvedLiveNarrativeFingerprintUx9K ||
            DraftFingerprintUx9K(approved) != _approvedLiveNarrativeFingerprintUx9K ||
            approved.BaseServerRevision != current.ServerRevision ||
            approved.SourceProfileFingerprint != ProfileFingerprintUx9K(current))
        {
            CancelLiveNarrativeApprovalUx9K();
            return new(false, "La source ou le brouillon a changé ; relancez l’aperçu sec.");
        }
        var command = new LiveDirectorNarrativeCommand(Guid.NewGuid(), 1, "studio:ux9-k",
            current.ServerRevision, "studio.live.narrative", NetworkRole.Administrator, false,
            BuildCandidateUx9K(current, approved));
        var dryRun = _liveNarrativeHistoryUx9K.Execute(command);
        if (!dryRun.Succeeded || dryRun.Applied || dryRun.SemanticDiff == "revision-only")
        {
            CancelLiveNarrativeApprovalUx9K();
            return new(false, HumanDiagnosticsUx9K(dryRun.Diagnostics));
        }
        var applied = _liveNarrativeHistoryUx9K.Execute(command with { Apply = true });
        CancelLiveNarrativeApprovalUx9K();
        if (!applied.Succeeded || !applied.Applied)
            return new(false, HumanDiagnosticsUx9K(applied.Diagnostics));
        _liveNarrativeAuditUx9K.Add(applied.AuditEntry);
        return new(true,
            "Profil narratif appliqué par la commande canonique · enregistrez pour persister.");
    }

    internal StudioUx9KActionResult UndoLiveNarrativeUx9K() => MoveHistoryUx9K(true);
    internal StudioUx9KActionResult RedoLiveNarrativeUx9K() => MoveHistoryUx9K(false);

    private StudioUx9KActionResult MoveHistoryUx9K(bool undo)
    {
        CancelLiveNarrativeApprovalUx9K();
        if (!EnsureLiveNarrativeUx9K())
            return new(false, _liveNarrativeLoadMessageUx9K ??
                              "Aucun profil narratif actif.");
        var result = undo
            ? _liveNarrativeHistoryUx9K!.Undo("studio.live.narrative",
                NetworkRole.Administrator)
            : _liveNarrativeHistoryUx9K!.Redo("studio.live.narrative",
                NetworkRole.Administrator);
        if (result.Succeeded) _liveNarrativeAuditUx9K.Add(result.AuditEntry);
        return new(result.Succeeded && result.Applied,
            result.Succeeded ? undo ? "Modification narrative annulée." :
                "Modification narrative rétablie." :
                HumanDiagnosticsUx9K(result.Diagnostics));
    }

    internal StudioUx9KActionResult SaveLiveNarrativeUx9K()
    {
        CancelLiveNarrativeApprovalUx9K();
        if (!EnsureLiveNarrativeUx9K() || Session is null)
            return new(false, _liveNarrativeLoadMessageUx9K ??
                              "Aucun profil narratif actif.");
        var result = WorkspaceLiveDirectorNarrativeStore.Save(Session.RootDirectory,
            _liveNarrativeHistoryUx9K!.Current);
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ??
                              "Le profil narratif n'a pas été enregistré.");
        _persistedLiveNarrativeUx9K = _liveNarrativeHistoryUx9K.Current;
        _liveNarrativeFileMissingUx9K = false;
        return new(true, "Profil narratif enregistré atomiquement dans le workspace.");
    }

    internal StudioUx9KActionResult RunLocalLiveNarrativeUx9K(bool confirmed,
        NetworkRole role, StudioUx9KNarrativeProbeOptions? options = null)
    {
        if (!confirmed)
        {
            _liveNarrativeAuditUx9K.Add(
                "rejected:studio.live-narrative:confirmation-required");
            return new(false, "Confirmez explicitement la sandbox narrative locale.");
        }
        if (role != NetworkRole.Administrator)
        {
            _liveNarrativeAuditUx9K.Add(
                "rejected:studio.live-narrative:permission-denied");
            return new(false, "La permission Administrateur est requise pour cette sandbox.");
        }
        if (!EnsureLiveNarrativeUx9K())
            return new(false, _liveNarrativeLoadMessageUx9K ??
                              "Aucun profil narratif actif.");
        try
        {
            var probe = RunLiveNarrativeProbeUx9K(_liveNarrativeHistoryUx9K!.Current, true,
                options ?? StudioUx9KNarrativeProbeOptions.CreateDefault());
            _liveNarrativeProbeUx9K = probe.Snapshot;
            _liveNarrativeAuditUx9K.AddRange(probe.Audit);
            var ok = probe.Quest && probe.Encounter && probe.Compensation &&
                     probe.Snapshot.ResourcesReleased;
            return new(ok, ok
                ? "Sandbox terminée · quête et rencontre synthétiques restaurées."
                : "La sandbox locale n'a pas validé les deux routes narratives.");
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            _liveNarrativeAuditUx9K.Add(
                $"rejected:studio.live-narrative:{exception.Message}");
            return new(false, $"La sandbox narrative locale a échoué : {exception.Message}");
        }
    }

    internal StudioUx9KApprovalSnapshot CaptureLiveNarrativeApprovalUx9K() =>
        new(_approvedLiveNarrativeUx9K, _approvedLiveNarrativeFingerprintUx9K);

    internal void RestoreLiveNarrativeApprovalUx9K(StudioUx9KApprovalSnapshot snapshot)
    {
        _approvedLiveNarrativeUx9K = snapshot.Draft;
        _approvedLiveNarrativeFingerprintUx9K = snapshot.DraftFingerprint;
    }

    internal void CancelLiveNarrativeApprovalUx9K()
    {
        _approvedLiveNarrativeUx9K = null;
        _approvedLiveNarrativeFingerprintUx9K = null;
    }

    internal void CloseUx9K()
    {
        CancelLiveNarrativeApprovalUx9K();
        _liveNarrativeHistoryUx9K = null;
        _persistedLiveNarrativeUx9K = null;
        _liveNarrativeWorkspaceRootUx9K = null;
        _liveNarrativeLoadMessageUx9K = null;
        _liveNarrativeFileMissingUx9K = false;
        _liveNarrativeProbeUx9K = EmptyProbeUx9K();
        _liveNarrativeAuditUx9K.Clear();
    }

    internal StudioUx9KSmokeResult RunUx9KContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9K();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureLiveNarrativeApprovalUx9K();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-k-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9K();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            var baseline = LiveDirectorNarrativeProfile.CreateDefault("evidence:ux9-k");
            if (!WorkspaceLiveDirectorNarrativeStore.Save(root, baseline).Succeeded)
                return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var draft = editing.CreateLiveNarrativeDraftUx9K(explicitlySelected: true);
            if (draft is null) return result;
            var fixture = draft with { RequiredAtRuntime = true };
            var invalid = !editing.PreviewLiveNarrativeUx9K(fixture,
                StudioUx9KNarrativeProbeOptions.CreateDefault() with
                { EncounterValue = "9" }).Succeeded;
            var staleSource = !editing.PreviewLiveNarrativeUx9K(fixture with
            { BaseServerRevision = fixture.BaseServerRevision + 1 }).Succeeded;
            var preview = editing.PreviewLiveNarrativeUx9K(fixture);
            var staleApproval = !editing.ApplyApprovedLiveNarrativeUx9K(fixture with
            { RequiredAtRuntime = false }).Succeeded;
            preview = editing.PreviewLiveNarrativeUx9K(fixture);
            var applied = editing.ApplyApprovedLiveNarrativeUx9K(fixture);
            var current = editing.CurrentLiveNarrativeUx9K;
            if (current is null) return result;
            var twoRoutes = current.QuestInspection &&
                            current.QuestScopes.SequenceEqual(
                                ["all-players", "selected-players"]) &&
                            current.ConsequencePreview && current.QuestRuntimeAdapter &&
                            current.EncounterActions.SequenceEqual(
                                ["activate", "pause", "wave", "scale", "despawn"]) &&
                            current.BoundedScaling && current.CreatureSpawnAdapter &&
                            current.AiRuntimeAdapter;
            var undo = editing.UndoLiveNarrativeUx9K().Succeeded;
            var redo = editing.RedoLiveNarrativeUx9K().Succeeded;
            var saved = editing.SaveLiveNarrativeUx9K().Succeeded;
            var options = StudioUx9KNarrativeProbeOptions.CreateDefault();
            var permission = !editing.RunLocalLiveNarrativeUx9K(true, NetworkRole.Player,
                options).Succeeded;
            var confirmation = !editing.RunLocalLiveNarrativeUx9K(false,
                NetworkRole.Administrator, options).Succeeded;
            var probed = editing.RunLocalLiveNarrativeUx9K(true,
                NetworkRole.Administrator, options);
            var snapshot = editing.LiveNarrativeProbeUx9K;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              reopened.CurrentLiveNarrativeUx9K is { } loaded &&
                              new LiveDirectorNarrativeStore().Serialize(current) ==
                              new LiveDirectorNarrativeStore().Serialize(loaded);
            result = new(true, twoRoutes, true, invalid, permission, confirmation,
                preview.Succeeded && preview.CanApply && preview.CanonicalDryRun,
                applied.Succeeded, staleSource, staleApproval, undo && redo, persistence,
                snapshot.QuestCode == "LIVE_QUEST_APPLIED" &&
                snapshot.QuestConsequences == 4 && snapshot.AffectedPlayers == 2 &&
                snapshot.RpgAdapter && snapshot.QuestRolledBack,
                snapshot.EncounterInspectCode == "LIVE_ENCOUNTER_INSPECTED" &&
                snapshot.EncounterActionCode == "LIVE_ENCOUNTER_APPLIED" &&
                snapshot.EncounterActions == 5 && snapshot.ScalingBounded &&
                snapshot.SpawnAdapter && snapshot.AiAdapter &&
                snapshot.EncounterRolledBack,
                snapshot.Compensated, editing.LiveNarrativeAuditUx9K.Count >= 10,
                false, false);
            result = result with
            {
                Succeeded = result.TwoRoutes && result.ExplicitSelection &&
                            result.InvalidBlocked && result.PermissionBlocked &&
                            result.ConfirmationRequired && result.CanonicalDryRun &&
                            result.CanonicalCommand && result.StaleSourceBlocked &&
                            result.StaleApprovalBlocked && result.UndoRedo &&
                            result.Persistence && result.QuestRuntime &&
                            result.EncounterRuntime && result.Compensation &&
                            result.Audit && probed.Succeeded
            };
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            var released = DeleteUx9KTemporaryWorkspace(root);
            var sourceUnchanged = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  ReferenceEquals(sourceSession, Session) &&
                                  History?.State.Revision == sourceRevision &&
                                  (History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceArea &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelected &&
                                  CaptureLiveNarrativeApprovalUx9K() == sourceApproval;
            result = result with
            {
                SourceUnchanged = sourceUnchanged,
                TemporaryWorkspaceReleased = released,
                Succeeded = result.Succeeded && sourceUnchanged && released
            };
        }
        return result;
    }

    private bool EnsureLiveNarrativeUx9K()
    {
        var root = Session?.RootDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            _liveNarrativeLoadMessageUx9K = "Ouvrez d’abord un module.";
            return false;
        }
        var full = Path.GetFullPath(root);
        if (_liveNarrativeHistoryUx9K is not null &&
            string.Equals(_liveNarrativeWorkspaceRootUx9K, full,
                StringComparison.OrdinalIgnoreCase)) return true;
        var loaded = WorkspaceLiveDirectorNarrativeStore.Load(full);
        if (!loaded.Succeeded)
        {
            _liveNarrativeHistoryUx9K = null;
            _persistedLiveNarrativeUx9K = null;
            _liveNarrativeWorkspaceRootUx9K = full;
            _liveNarrativeFileMissingUx9K = false;
            _liveNarrativeLoadMessageUx9K =
                $"Profil narratif invalide : {loaded.Diagnostics.FirstOrDefault()?.Message ?? "diagnostic inconnu"}";
            return false;
        }
        _liveNarrativeHistoryUx9K = new(loaded.Profile);
        _persistedLiveNarrativeUx9K = loaded.Profile;
        _liveNarrativeWorkspaceRootUx9K = full;
        _liveNarrativeFileMissingUx9K = loaded.FileMissing;
        _liveNarrativeLoadMessageUx9K = loaded.FileMissing
            ? "Aucun profil narratif enregistré · le profil initial est affiché sans écriture implicite."
            : null;
        return true;
    }

    private static LiveDirectorNarrativeProfile BuildCandidateUx9K(
        LiveDirectorNarrativeProfile current, StudioUx9KNarrativeDraft draft) => current with
        {
            ServerRevision = current.ServerRevision + 1,
            Provenance = "studio:ux9-k",
            RequiredAtRuntime = draft.RequiredAtRuntime,
            QuestInspection = true,
            QuestScopes = ["all-players", "selected-players"],
            ConsequencePreview = true,
            QuestRuntimeAdapter = true,
            EncounterActions = ["activate", "pause", "wave", "scale", "despawn"],
            BoundedScaling = true,
            CreatureSpawnAdapter = true,
            AiRuntimeAdapter = true
        };

    private static NarrativeProbeResultUx9K RunLiveNarrativeProbeUx9K(
        LiveDirectorNarrativeProfile profile, bool applyRuntime,
        StudioUx9KNarrativeProbeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.QuestScope is not ("all-players" or "selected-players") ||
            string.IsNullOrWhiteSpace(options.QuestTargetState) ||
            !profile.EncounterActions.Contains(options.EncounterAction) ||
            options.EncounterAction == "scale" &&
            (!decimal.TryParse(options.EncounterValue, NumberStyles.Number,
                 CultureInfo.InvariantCulture, out var scale) || scale is < 0.25m or > 4m) ||
            options.EncounterAction == "wave" &&
            (!int.TryParse(options.EncounterValue, out var wave) || wave < 0))
            throw new ArgumentException("LIVE_STUDIO_NARRATIVE_OPTIONS_INVALID",
                nameof(options));

        var runtime = LiveDirectorNarrativeRuntimeAdapter.Create(profile,
            () => new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        var actorId = Guid.Parse("d1300000-0000-4000-8000-000000000201");
        var questId = Guid.Parse("d1300000-0000-4000-8000-000000000202");
        var encounterId = Guid.Parse("d1300000-0000-4000-8000-000000000203");
        var firstPlayer = Guid.Parse("d1300000-0000-4000-8000-000000000204");
        var secondPlayer = Guid.Parse("d1300000-0000-4000-8000-000000000205");
        var actor = new AuthorizationSubject(actorId, NetworkRole.Administrator,
            new HashSet<Guid>());
        var operationAudit = new List<string>();

        runtime.Quests.Upsert(new(questId, "Les Cendres", "started",
            new Dictionary<string, IReadOnlySet<string>>
            {
                ["started"] = new HashSet<string> { "completed", "failed" }
            }));
        var questRequest = new LiveQuestService.Request(
            Guid.Parse("d1300000-0000-4000-8000-000000000210"), questId,
            options.QuestTargetState, options.QuestScope, [firstPlayer, secondPlayer],
            "résolution MJ", 0);
        var questResult = runtime.Quests.Transition(actor, questRequest, false);
        operationAudit.Add(questResult.AuditEntry);
        var questCode = questResult.Code;
        var questRolledBack = !applyRuntime;
        if (applyRuntime && questResult.Code == "LIVE_QUEST_PREVIEW")
        {
            questResult = runtime.Quests.Transition(actor, questRequest, true);
            questCode = questResult.Code;
            operationAudit.Add(questResult.AuditEntry);
            var rollback = runtime.Quests.Rollback(actor,
                Guid.Parse("d1300000-0000-4000-8000-000000000211"), "fin de sandbox",
                questResult.Revision, true);
            operationAudit.Add(rollback.AuditEntry);
            questRolledBack = rollback.Code == "LIVE_QUEST_ROLLED_BACK" &&
                              rollback.Restored &&
                              runtime.Quests.Inspect(questId)?.State == "started";
        }
        var questReady = questCode == (applyRuntime ? "LIVE_QUEST_APPLIED" :
            "LIVE_QUEST_PREVIEW") && questResult.Consequences.Count == 4 &&
                         questResult.AffectedPlayers.Count == 2 &&
                         questResult.RpgProjection.StartsWith("rpg-kit:quest-journal:",
                             StringComparison.Ordinal) && questRolledBack;

        var originalEncounter = new LiveEncounterService.Encounter(encounterId,
            "Gardien de la Crypte", "idle", 0, 1m, 4);
        runtime.Encounters.Upsert(originalEncounter);
        var inspect = runtime.Encounters.Execute(actor, new(
            Guid.Parse("d1300000-0000-4000-8000-000000000212"), encounterId, "inspect",
            null, "inspection MJ", 0), true);
        operationAudit.Add(inspect.AuditEntry);
        var actions = new[] { "activate", "pause", "wave", "scale", "despawn" };
        var results = new Dictionary<string, LiveEncounterService.Result>(StringComparer.Ordinal);
        long revision = 0;
        for (var index = 0; index < actions.Length; index++)
        {
            var action = actions[index];
            var value = action switch
            {
                "wave" => options.EncounterAction == "wave" ? options.EncounterValue : "2",
                "scale" => options.EncounterAction == "scale" ? options.EncounterValue : "1.5",
                _ => null
            };
            var request = new LiveEncounterService.Request(
                Guid.Parse($"d1300000-0000-4000-8000-{(220 + index):000000000000}"),
                encounterId, action, value, "direction live", revision);
            var actionResult = runtime.Encounters.Execute(actor, request, false);
            operationAudit.Add(actionResult.AuditEntry);
            if (applyRuntime && actionResult.Code == "LIVE_ENCOUNTER_PREVIEW")
            {
                actionResult = runtime.Encounters.Execute(actor, request, true);
                revision = actionResult.Revision;
                operationAudit.Add(actionResult.AuditEntry);
            }
            results[action] = actionResult;
        }
        var invalidScale = runtime.Encounters.Execute(actor, new(
            Guid.Parse("d1300000-0000-4000-8000-000000000230"), encounterId, "scale", "9",
            "borne MJ", revision), false);
        var scalingBounded = invalidScale.Code == "LIVE_ENCOUNTER_SCALE_INVALID";
        var encounterRolledBack = !applyRuntime;
        if (applyRuntime)
        {
            var allRestored = true;
            for (var index = 0; index < actions.Length; index++)
            {
                var rollback = runtime.Encounters.Rollback(actor,
                    Guid.Parse($"d1300000-0000-4000-8000-{(240 + index):000000000000}"),
                    "fin de sandbox", revision, true);
                revision = rollback.Revision;
                operationAudit.Add(rollback.AuditEntry);
                allRestored &= rollback.Code == "LIVE_ENCOUNTER_ROLLED_BACK" &&
                               rollback.Restored;
            }
            encounterRolledBack = allRestored && runtime.Encounters.HistoryCount == 0 &&
                                  runtime.Encounters.Inspect(encounterId) == originalEncounter;
        }
        var selected = results[options.EncounterAction];
        var encounterReady = inspect.Code == "LIVE_ENCOUNTER_INSPECTED" &&
                             results.Count == 5 && results.Values.All(result =>
                                 result.Code == (applyRuntime ? "LIVE_ENCOUNTER_APPLIED" :
                                     "LIVE_ENCOUNTER_PREVIEW")) &&
                             selected.RuntimeAdapters.Contains("spawn-authority") &&
                             selected.RuntimeAdapters.Contains("ai-runtime") &&
                             scalingBounded && encounterRolledBack;

        var compensated = !applyRuntime || questRolledBack && encounterRolledBack;
        var resourcesReleased = compensated;
        var snapshot = new StudioUx9KNarrativeProbe(
            questReady && encounterReady && compensated
                ? "Deux routes narratives locales validées"
                : "Sandbox narrative incomplète",
            questCode, questResult.Consequences.Count, questResult.AffectedPlayers.Count,
            questResult.RpgProjection.StartsWith("rpg-kit:quest-journal:",
                StringComparison.Ordinal), questRolledBack, inspect.Code, selected.Code,
            results.Count, scalingBounded,
            selected.RuntimeAdapters.Contains("spawn-authority"),
            selected.RuntimeAdapters.Contains("ai-runtime"), encounterRolledBack,
            compensated, operationAudit.Count + runtime.Audit.Entries.Count,
            resourcesReleased);
        return new(questReady, encounterReady, compensated, snapshot, operationAudit);
    }

    private static string ProfileFingerprintUx9K(LiveDirectorNarrativeProfile profile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            new LiveDirectorNarrativeStore().Serialize(profile))));

    private static string DraftFingerprintUx9K(StudioUx9KNarrativeDraft draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(draft))));

    private static StudioUx9KNarrativePreview FailedPreviewUx9K(string message,
        IReadOnlyList<NetworkDiagnostic>? diagnostics = null) => new(false, false, false,
        false, false, message, [], diagnostics?.Select(item =>
            LiveDirectorNarrativeLocalization.French(item.Code)).Distinct().ToArray() ?? [],
        EmptyProbeUx9K());

    private static string HumanDiagnosticsUx9K(
        IReadOnlyList<NetworkDiagnostic> diagnostics) => diagnostics.Count == 0
        ? "La commande n'a produit aucun changement applicable."
        : string.Join(" · ", diagnostics.Select(item =>
            LiveDirectorNarrativeLocalization.French(item.Code)).Distinct());

    private static string HumanDiffUx9K(string value) => value switch
    {
        "quests" => "Quêtes, joueurs, conséquences et adaptateur RPG",
        "encounters" => "Rencontres, scaling et adaptateurs spawn/IA",
        "runtime" => "Chargement runtime requis",
        _ => value
    };

    private static StudioUx9KNarrativeProbe EmptyProbeUx9K() => new(
        "Aucune sandbox exécutée", "—", 0, 0, false, false, "—", "—", 0,
        false, false, false, false, false, 0, true);

    private static StudioUx9KSmokeResult FailedSmokeUx9K() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false);

    private static bool DeleteUx9KTemporaryWorkspace(string root)
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-k-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record NarrativeProbeResultUx9K(bool Quest, bool Encounter,
        bool Compensation, StudioUx9KNarrativeProbe Snapshot, IReadOnlyList<string> Audit);
}
