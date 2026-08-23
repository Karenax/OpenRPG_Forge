using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.LiveDirector.Production;
using Forge.LiveDirector.World;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux9MProvenance = "studio:ux9-m:live-production";
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx9M = new(new SystemClock());
    private StudioUx9MProductionDraft? _approvedLiveProductionUx9M;
    private ProjectRevision? _approvedLiveProductionRevisionUx9M;
    private string? _approvedLiveProductionFingerprintUx9M;
    private StudioUx9MProductionProbe _liveProductionProbeUx9M = EmptyProbeUx9M();
    private readonly List<string> _liveProductionAuditUx9M = [];

    internal StudioUx9MProductionProbe LiveProductionProbeUx9M => _liveProductionProbeUx9M;
    internal IReadOnlyList<string> LiveProductionAuditUx9M => _liveProductionAuditUx9M.ToArray();
    internal LiveDirectorProductionProfile? CurrentLiveProductionUx9M =>
        Project?.Module.RuntimePolicies.Extensions.PostLaunch.LiveDirector;

    internal StudioUx9MProductionDraft? CreateLiveProductionDraftUx9M(
        bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var profile = project.Module.RuntimePolicies.Extensions.PostLaunch.LiveDirector;
        var group = profile.SavedGroups.FirstOrDefault();
        return new(
            history.State.Revision,
            LiveProductionSourceFingerprintUx9M(project),
            profile.ForcedRest.Enabled,
            profile.ForcedRest.MaximumTargets,
            profile.LevelAdjustment.Enabled,
            profile.LevelAdjustment.MaximumDelta,
            profile.LevelAdjustment.RequireRollbackSnapshot,
            profile.AppearanceOverrides.Enabled,
            profile.AppearanceOverrides.MinimumScale,
            profile.AppearanceOverrides.MaximumScale,
            group is not null,
            group?.Key ?? "groupe-live",
            group?.Query ?? "tag:compagnons",
            group?.Hotkey ?? FirstAvailableHotkeyUx9M(profile.SavedGroups),
            group?.MaximumMembers ?? 16,
            profile.SessionRecording.Enabled,
            profile.SessionRecording.ExportFormat,
            profile.SessionRecording.MaximumMinutes,
            profile.SessionRecording.RedactPrivateData);
    }

    internal StudioUx9MProductionPreview PreviewLiveProductionUx9M(
        StudioUx9MProductionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelLiveProductionApprovalUx9M();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPreviewUx9M("Ouvrez d’abord un module.");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint,
                LiveProductionSourceFingerprintUx9M(project), StringComparison.Ordinal))
            return FailedPreviewUx9M(
                "Le module a changé ; rechargez les politiques avant l’aperçu sec.");
        if (!TryBuildLiveProductionProfileUx9M(project, draft, out var extensions,
                out var diagnostic) || extensions is null)
            return FailedPreviewUx9M(diagnostic ??
                                     "Les politiques Live Director sont invalides.");

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, extensions, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx9M.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return FailedPreviewUx9M(HumanDiagnosticsUx9M(dryRun.Diagnostics));

        var current = project.Module.RuntimePolicies.Extensions.PostLaunch.LiveDirector;
        var candidate = extensions.PostLaunch.LiveDirector;
        var changes = BuildLiveProductionDiffUx9M(current, candidate);
        StudioUx9MProductionProbe probe;
        try
        {
            probe = RunLiveProductionProbeUx9M(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            return FailedPreviewUx9M(
                $"La sandbox locale refuse ces politiques : {exception.Message}");
        }
        _liveProductionProbeUx9M = probe;
        var canApply = changes.Count > 0 && ProbeReadyUx9M(probe);
        if (canApply)
        {
            _approvedLiveProductionUx9M = draft with { };
            _approvedLiveProductionRevisionUx9M = history.State.Revision;
            _approvedLiveProductionFingerprintUx9M = DraftFingerprintUx9M(draft);
        }
        return new(true, canApply, true, ProbeReadyUx9M(probe),
            changes.Count == 0
                ? "Aperçu sec validé · ces politiques sont déjà actives."
                : "Aperçu sec validé · aucun joueur ni serveur réel n’a été modifié.",
            changes, [], probe);
    }

    internal StudioUx9MActionResult ApplyApprovedLiveProductionUx9M(
        StudioUx9MProductionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var approved = _approvedLiveProductionUx9M;
        var project = Project;
        var history = History;
        if (approved is null || project is null || history is null ||
            _approvedLiveProductionRevisionUx9M != history.State.Revision ||
            _approvedLiveProductionFingerprintUx9M is null ||
            !string.Equals(DraftFingerprintUx9M(draft),
                _approvedLiveProductionFingerprintUx9M, StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9M(approved),
                _approvedLiveProductionFingerprintUx9M, StringComparison.Ordinal) ||
            approved.BaseRevision != history.State.Revision ||
            !string.Equals(approved.SourceFingerprint,
                LiveProductionSourceFingerprintUx9M(project), StringComparison.Ordinal))
        {
            CancelLiveProductionApprovalUx9M();
            return new(false, "Relancez l’aperçu sec avant d’appliquer.");
        }
        if (!TryBuildLiveProductionProfileUx9M(project, approved, out var extensions,
                out var diagnostic) || extensions is null)
        {
            CancelLiveProductionApprovalUx9M();
            return new(false, diagnostic ?? "Les politiques ne peuvent pas être reconstruites.");
        }
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, extensions, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx9M.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelLiveProductionApprovalUx9M();
            return new(false, HumanDiagnosticsUx9M(dryRun.Diagnostics));
        }
        var applied = _setRuntimeExtensionsUx9M.Handle(history.State,
            command with { DryRun = false });
        CancelLiveProductionApprovalUx9M();
        if (!applied.Succeeded)
            return new(false, HumanDiagnosticsUx9M(applied.Diagnostics));
        history.Record(applied);
        InvalidateContentIndexes();
        _liveProductionAuditUx9M.Add(
            $"applied:studio.live-production:revision={history.State.Revision.Value}");
        return new(true,
            "Politiques Live Director appliquées par une commande canonique annulable.");
    }

    internal StudioUx9MActionResult UndoLiveProductionUx9M()
    {
        CancelLiveProductionApprovalUx9M();
        var result = Undo();
        if (result.Succeeded) _liveProductionAuditUx9M.Add("undo:studio.live-production");
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx9MActionResult RedoLiveProductionUx9M()
    {
        CancelLiveProductionApprovalUx9M();
        var result = Redo();
        if (result.Succeeded) _liveProductionAuditUx9M.Add("redo:studio.live-production");
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx9MActionResult SaveLiveProductionUx9M()
    {
        CancelLiveProductionApprovalUx9M();
        var result = Save();
        if (result.Succeeded) _liveProductionAuditUx9M.Add("saved:studio.live-production");
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx9MActionResult RunLocalLiveProductionUx9M(bool confirmed,
        NetworkRole role)
    {
        if (!confirmed)
        {
            _liveProductionAuditUx9M.Add(
                "rejected:studio.live-production:confirmation-required");
            return new(false, "Confirmez explicitement la sandbox locale restaurée.");
        }
        if (role != NetworkRole.Administrator)
        {
            _liveProductionAuditUx9M.Add(
                "rejected:studio.live-production:permission-denied");
            return new(false, "La permission Administrateur est requise.");
        }
        var profile = CurrentLiveProductionUx9M;
        if (profile is null) return new(false, "Ouvrez d’abord un module.");
        try
        {
            var probe = RunLiveProductionProbeUx9M(profile);
            _liveProductionProbeUx9M = probe;
            _liveProductionAuditUx9M.Add(
                $"sandbox:rest={probe.RestCode}:level={probe.LevelCode}:appearance={probe.AppearanceCode}:recording={probe.RecordingCode}");
            return new(ProbeReadyUx9M(probe), ProbeReadyUx9M(probe)
                ? "Sandbox terminée · interventions, groupe et enregistrement synthétiques validés."
                : "La sandbox n’a pas validé les cinq politiques.");
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            _liveProductionAuditUx9M.Add(
                $"rejected:studio.live-production:{exception.Message}");
            return new(false, $"La sandbox locale a échoué : {exception.Message}");
        }
    }

    internal StudioUx9MApprovalSnapshot CaptureLiveProductionApprovalUx9M() => new(
        _approvedLiveProductionUx9M, _approvedLiveProductionRevisionUx9M,
        _approvedLiveProductionFingerprintUx9M);

    internal void RestoreLiveProductionApprovalUx9M(StudioUx9MApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedLiveProductionUx9M = snapshot.Draft;
        _approvedLiveProductionRevisionUx9M = snapshot.Revision;
        _approvedLiveProductionFingerprintUx9M = snapshot.Fingerprint;
    }

    internal void CancelLiveProductionApprovalUx9M()
    {
        _approvedLiveProductionUx9M = null;
        _approvedLiveProductionRevisionUx9M = null;
        _approvedLiveProductionFingerprintUx9M = null;
    }

    internal void CloseUx9M()
    {
        CancelLiveProductionApprovalUx9M();
        _liveProductionProbeUx9M = EmptyProbeUx9M();
        _liveProductionAuditUx9M.Clear();
    }

    private static bool TryBuildLiveProductionProfileUx9M(ProjectDefinition project,
        StudioUx9MProductionDraft draft, out ModuleRuntimeExtensionProfile? candidate,
        out string? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        if (draft.HasSavedGroup && (string.IsNullOrWhiteSpace(draft.GroupKey) ||
                                    string.IsNullOrWhiteSpace(draft.GroupQuery)))
        {
            diagnostic = "Le groupe sauvegardé exige un nom et une requête lisibles.";
            return false;
        }
        if (draft.ExportFormat is not ("jsonl" or "orp-replay"))
        {
            diagnostic = "Choisissez JSONL ou replay OpenRPG comme format d’export.";
            return false;
        }
        try
        {
            var extensions = project.Module.RuntimePolicies.Extensions;
            var post = extensions.PostLaunch;
            var source = post.LiveDirector;
            var groups = source.SavedGroups.Skip(1).ToList();
            if (draft.HasSavedGroup)
            {
                var id = source.SavedGroups.FirstOrDefault()?.Id ??
                         StableUx9M(source.Id, "primary-saved-group");
                groups.Insert(0, new LiveDirectorProductionProfile.SavedGroup(id,
                    draft.GroupKey, draft.GroupQuery, draft.GroupHotkey,
                    draft.MaximumGroupMembers));
            }
            var liveDirector = new LiveDirectorProductionProfile(
                source.Id,
                source.SchemaVersion,
                source.ForcedRest with
                {
                    Enabled = draft.ForcedRestEnabled,
                    MaximumTargets = draft.MaximumRestTargets
                },
                source.LevelAdjustment with
                {
                    Enabled = draft.LevelAdjustmentEnabled,
                    MaximumDelta = draft.MaximumLevelDelta,
                    RequireRollbackSnapshot = draft.RequireRollbackSnapshot
                },
                groups,
                source.AppearanceOverrides with
                {
                    Enabled = draft.AppearanceEnabled,
                    MinimumScale = (float)draft.MinimumScale,
                    MaximumScale = (float)draft.MaximumScale
                },
                source.SessionRecording with
                {
                    Enabled = draft.RecordingEnabled,
                    ExportFormat = draft.ExportFormat,
                    MaximumMinutes = draft.MaximumRecordingMinutes,
                    RedactPrivateData = draft.RedactPrivateData
                },
                Ux9MProvenance);
            var updatedPost = new PostLaunchCapabilityProfile(post.Id, post.SchemaVersion,
                post.Gameplay, liveDirector, post.Network, post.CreatorExperience,
                post.Production, Ux9MProvenance);
            candidate = new ModuleRuntimeExtensionProfile(extensions.Id,
                extensions.SchemaVersion, extensions.RuleSchemas, extensions.UiDocuments,
                extensions.JsonDocuments, extensions.SnapshotPolicy, Ux9MProvenance,
                extensions.ModernTooling, extensions.OperationalReadiness, updatedPost);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               ArgumentOutOfRangeException)
        {
            diagnostic = HumanProfileErrorUx9M(exception.Message);
            return false;
        }
    }

    private static IReadOnlyList<string> BuildLiveProductionDiffUx9M(
        LiveDirectorProductionProfile source, LiveDirectorProductionProfile candidate)
    {
        var entries = new List<string>();
        if (source.ForcedRest != candidate.ForcedRest)
            entries.Add("Repos forcé · activation et plafond de cibles");
        if (source.LevelAdjustment != candidate.LevelAdjustment)
            entries.Add("Ajustement de niveau · amplitude et snapshot de retour");
        if (source.AppearanceOverrides != candidate.AppearanceOverrides)
            entries.Add("Apparence · échelle bornée et validation des assets");
        if (!source.SavedGroups.SequenceEqual(candidate.SavedGroups))
            entries.Add("Groupe sauvegardé · requête, raccourci et plafond de membres");
        if (source.SessionRecording != candidate.SessionRecording)
            entries.Add("Enregistrement · format, durée et données privées expurgées");
        return entries;
    }

    private static StudioUx9MProductionProbe RunLiveProductionProbeUx9M(
        LiveDirectorProductionProfile profile)
    {
        var authorization = new AuthorizationService();
        var chooser = new LiveWorldChooser(authorization);
        var actorId = Guid.Parse("4d9a0000-0000-4000-8000-000000000001");
        var zoneId = Guid.Parse("4d9a0000-0000-4000-8000-000000000002");
        var targetId = Guid.Parse("4d9a0000-0000-4000-8000-000000000003");
        var recordingId = Guid.Parse("4d9a0000-0000-4000-8000-000000000004");
        chooser.Upsert(new(targetId, zoneId, "creature", "Compagnon de preuve",
            "companion", null, "active", []));
        var administrator = new AuthorizationSubject(actorId, NetworkRole.Administrator,
            new HashSet<Guid> { zoneId });
        var player = new AuthorizationSubject(
            Guid.Parse("4d9a0000-0000-4000-8000-000000000005"),
            NetworkRole.Player, new HashSet<Guid> { zoneId });
        var service = new LiveProductionService(chooser, authorization, profile,
            () => DateTimeOffset.Parse("2026-08-23T12:00:00Z",
                CultureInfo.InvariantCulture), () => recordingId);

        var reasonRejected = service.Execute(administrator,
            new(Guid.NewGuid(), "force-rest", [targetId], 0, ""), apply: false);
        var confirmationRejected = service.Execute(administrator,
            new(Guid.NewGuid(), "adjust-level", [targetId], 0, "preuve",
                LevelDelta: 1, Confirmed: false), apply: false);
        var permissionRejected = service.Execute(player,
            new(Guid.NewGuid(), "force-rest", [targetId], 0, "preuve"), apply: false);

        long revision = 0;
        var restRequest = new LiveProductionService.InterventionRequest(Guid.NewGuid(),
            "force-rest", [targetId], revision, "repos narratif");
        var restPreview = service.Execute(administrator, restRequest, apply: false);
        var restResult = profile.ForcedRest.Enabled
            ? service.Execute(administrator, restRequest, apply: true)
            : restPreview;
        var restRespected = profile.ForcedRest.Enabled
            ? restPreview.Code == "LIVE_PRODUCTION_PREVIEW" && restResult.Applied &&
              chooser.Find(targetId)?.State == "rest:completed:director"
            : restResult.Code == "POST_DIRECTOR_REST_DENIED";
        if (restResult.Applied) revision = restResult.Revision;

        var levelRequest = new LiveProductionService.InterventionRequest(Guid.NewGuid(),
            "adjust-level", [targetId], revision, "correction de niveau",
            LevelDelta: Math.Min(2, profile.LevelAdjustment.MaximumDelta), Confirmed: true);
        var levelPreview = service.Execute(administrator, levelRequest, apply: false);
        var levelResult = profile.LevelAdjustment.Enabled
            ? service.Execute(administrator, levelRequest, apply: true)
            : levelPreview;
        var levelRespected = profile.LevelAdjustment.Enabled
            ? levelPreview.Code == "LIVE_PRODUCTION_PREVIEW" && levelResult.Applied &&
              chooser.Find(targetId)?.State.Contains("level-adjusted:",
                  StringComparison.Ordinal) == true
            : levelResult.Code == "POST_DIRECTOR_LEVEL_DENIED";
        if (levelResult.Applied) revision = levelResult.Revision;

        var scale = Math.Clamp(1f, profile.AppearanceOverrides.MinimumScale,
            profile.AppearanceOverrides.MaximumScale);
        var appearanceRequest = new LiveProductionService.InterventionRequest(
            Guid.NewGuid(), "appearance", [targetId], revision,
            "mise en scène temporaire", Scale: scale, Confirmed: true);
        var appearancePreview = service.Execute(administrator, appearanceRequest,
            apply: false);
        var appearanceResult = profile.AppearanceOverrides.Enabled
            ? service.Execute(administrator, appearanceRequest, apply: true)
            : appearancePreview;
        var appearanceRespected = profile.AppearanceOverrides.Enabled
            ? appearancePreview.Code == "LIVE_PRODUCTION_PREVIEW" &&
              appearanceResult.Applied && chooser.Find(targetId)?.State.Contains(
                  "appearance-scale:", StringComparison.Ordinal) == true
            : appearanceResult.Code == "POST_DIRECTOR_APPEARANCE_DENIED";

        var primaryGroup = profile.SavedGroups.FirstOrDefault();
        var hotkey = primaryGroup?.Hotkey ?? 1;
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> queryResults = primaryGroup is null
            ? new Dictionary<string, IReadOnlyList<Guid>>()
            : new Dictionary<string, IReadOnlyList<Guid>>
            {
                [primaryGroup.Query] = [targetId, targetId]
            };
        var recording = service.StartRecording(administrator, hotkey, queryResults);
        var recordingPermission = service.StartRecording(player, hotkey, queryResults);
        var recordingRespected = primaryGroup is null
            ? recording.Code == "POST_GROUP_UNKNOWN" && !recording.Started
            : profile.SessionRecording.Enabled
                ? recording.Started && recording.RecordingId == recordingId &&
                  recording.Members.SequenceEqual([targetId]) &&
                  recording.ExportFormat == profile.SessionRecording.ExportFormat &&
                  recording.Redacted == profile.SessionRecording.RedactPrivateData
                : recording.Code == "POST_RECORDING_DISABLED" && !recording.Started;
        var permissionCode = permissionRejected.Code == "LIVE_PRODUCTION_PERMISSION_DENIED" &&
                             recordingPermission.Code == "LIVE_RECORDING_PERMISSION_DENIED"
            ? "LIVE_PRODUCTION_PERMISSION_DENIED"
            : "LIVE_PRODUCTION_PERMISSION_CHECK_FAILED";
        var ready = restRespected && levelRespected && appearanceRespected &&
                    recordingRespected &&
                    reasonRejected.Code == "LIVE_PRODUCTION_REASON_REQUIRED" &&
                    confirmationRejected.Code == "LIVE_PRODUCTION_CONFIRMATION_REQUIRED" &&
                    permissionCode == "LIVE_PRODUCTION_PERMISSION_DENIED";
        return new(ready ? "Cinq politiques vérifiées" : "Sandbox incomplète",
            restResult.Code, restRespected, levelResult.Code, levelRespected,
            profile.LevelAdjustment.RequireRollbackSnapshot,
            appearanceResult.Code, appearanceRespected, recording.Code,
            recordingRespected, recording.Members.Count, recording.ExportFormat,
            recording.Redacted, permissionCode, confirmationRejected.Code,
            reasonRejected.Code, true);
    }

    internal StudioUx9MSmokeResult RunUx9MContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9M();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureLiveProductionApprovalUx9M();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-m-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9M();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var explicitSelection = editing.CreateLiveProductionDraftUx9M() is null &&
                                    editing.CreateLiveProductionDraftUx9M(true) is not null;
            var baseline = editing.CreateLiveProductionDraftUx9M(true)!;
            var fixture = ChangedDraftUx9M(baseline);
            var invalid = !editing.PreviewLiveProductionUx9M(fixture with
            { MaximumRestTargets = 0 }).Succeeded &&
                          !editing.PreviewLiveProductionUx9M(fixture with
                          { MaximumLevelDelta = 0 }).Succeeded &&
                          !editing.PreviewLiveProductionUx9M(fixture with
                          { MinimumScale = 2, MaximumScale = 1 }).Succeeded &&
                          !editing.PreviewLiveProductionUx9M(fixture with
                          { GroupHotkey = 13 }).Succeeded &&
                          !editing.PreviewLiveProductionUx9M(fixture with
                          { GroupQuery = "" }).Succeeded &&
                          !editing.PreviewLiveProductionUx9M(fixture with
                          { ExportFormat = "mp4" }).Succeeded &&
                          !editing.PreviewLiveProductionUx9M(fixture with
                          { MaximumRecordingMinutes = 0 }).Succeeded;
            var staleSource = !editing.PreviewLiveProductionUx9M(fixture with
            {
                BaseRevision = new ProjectRevision(fixture.BaseRevision.Value + 1)
            }).Succeeded;
            var beforeDryRun = editing.Project;
            var revisionBeforeDryRun = editing.History!.State.Revision;
            var journalBeforeDryRun = editing.History.Journal.ToArray();
            var dirtyBeforeDryRun = editing.IsDirty;
            var preview = editing.PreviewLiveProductionUx9M(fixture);
            var canonicalDryRun = preview.Succeeded && preview.CanApply &&
                                  preview.CanonicalDryRun &&
                                  ReferenceEquals(beforeDryRun, editing.Project) &&
                                  editing.History.State.Revision == revisionBeforeDryRun &&
                                  editing.History.Journal.SequenceEqual(journalBeforeDryRun) &&
                                  editing.IsDirty == dirtyBeforeDryRun;
            var staleApproval = !editing.ApplyApprovedLiveProductionUx9M(fixture with
            {
                MaximumRecordingMinutes = fixture.MaximumRecordingMinutes == 1440
                    ? 1439 : fixture.MaximumRecordingMinutes + 1
            }).Succeeded;
            preview = editing.PreviewLiveProductionUx9M(fixture);
            var sourceState = editing.Project!;
            var sourcePolicies = sourceState.Module.RuntimePolicies;
            var sourceExtensions = sourcePolicies.Extensions;
            var sourcePost = sourceExtensions.PostLaunch;
            if (!TryBuildLiveProductionProfileUx9M(sourceState, fixture,
                    out var expectedExtensions, out _) || expectedExtensions is null)
                return result;
            var applied = editing.ApplyApprovedLiveProductionUx9M(fixture);
            var current = editing.CurrentLiveProductionUx9M;
            if (current is null) return result;
            var primary = current.SavedGroups.FirstOrDefault();
            var fiveRoutes = current.ForcedRest.Enabled == fixture.ForcedRestEnabled &&
                             current.ForcedRest.MaximumTargets == fixture.MaximumRestTargets &&
                             current.LevelAdjustment.Enabled == fixture.LevelAdjustmentEnabled &&
                             current.LevelAdjustment.MaximumDelta == fixture.MaximumLevelDelta &&
                             current.AppearanceOverrides.Enabled == fixture.AppearanceEnabled &&
                             Math.Abs(current.AppearanceOverrides.MinimumScale -
                                      fixture.MinimumScale) < .001 &&
                             Math.Abs(current.AppearanceOverrides.MaximumScale -
                                      fixture.MaximumScale) < .001 &&
                             primary is not null && primary.Key == NormalizeKeyUx9M(fixture.GroupKey) &&
                             primary.Query == fixture.GroupQuery.Trim() &&
                             primary.Hotkey == fixture.GroupHotkey &&
                             current.SessionRecording.Enabled == fixture.RecordingEnabled &&
                             current.SessionRecording.ExportFormat == fixture.ExportFormat &&
                             current.SessionRecording.RedactPrivateData == fixture.RedactPrivateData;
            var command = applied.Succeeded && editing.History!.Journal.LastOrDefault()?.CommandName ==
                          "set_runtime_extensions";
            var finalProject = editing.Project!;
            var expectedPolicies = new ModuleRuntimePolicyProfile(sourcePolicies.Id,
                sourcePolicies.SchemaVersion, sourcePolicies.Heartbeats, sourcePolicies.Rest,
                sourcePolicies.SequenceAbort, "runtime-extension-command",
                sourcePolicies.Cinematics, sourcePolicies.Gameplay, sourcePolicies.Rules,
                expectedExtensions, sourcePolicies.GameplayEvents, sourcePolicies.World,
                sourcePolicies.LifecycleEvents);
            var expectedProject = new ProjectDefinition(sourceState.Id, sourceState.Title,
                sourceState.Module.WithRuntimePolicies(expectedPolicies),
                sourceState.Blueprints, sourceState.Areas, sourceState.Dialogues,
                sourceState.Quests);
            var neighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedProject),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal) &&
                            ReferenceEquals(sourcePost.Gameplay,
                                finalProject.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay) &&
                            ReferenceEquals(sourcePost.Network,
                                finalProject.Module.RuntimePolicies.Extensions.PostLaunch.Network) &&
                            ReferenceEquals(sourcePost.CreatorExperience,
                                finalProject.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience) &&
                            ReferenceEquals(sourcePost.Production,
                                finalProject.Module.RuntimePolicies.Extensions.PostLaunch.Production);
            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.UndoLiveProductionUx9M().Succeeded;
            var redo = editing.RedoLiveProductionUx9M().Succeeded;
            var saved = editing.SaveLiveProductionUx9M().Succeeded;
            var permission = !editing.RunLocalLiveProductionUx9M(true,
                NetworkRole.Player).Succeeded;
            var confirmation = !editing.RunLocalLiveProductionUx9M(false,
                NetworkRole.Administrator).Succeeded;
            var probed = editing.RunLocalLiveProductionUx9M(true,
                NetworkRole.Administrator).Succeeded;
            var probe = editing.LiveProductionProbeUx9M;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              reopened.Project is not null &&
                              string.Equals(finalJson,
                                  ProjectJsonSerializer.Serialize(reopened.Project),
                                  StringComparison.Ordinal);
            result = new(true, fiveRoutes, explicitSelection, invalid, permission,
                confirmation, canonicalDryRun, command, staleSource, staleApproval,
                undo && redo, persistence, probe.RestPolicyRespected,
                probe.LevelPolicyRespected && probe.RollbackSnapshot,
                probe.AppearancePolicyRespected,
                primary is not null && primary.MaximumMembers == fixture.MaximumGroupMembers,
                probe.RecordingPolicyRespected && probe.RecordedMembers == 1,
                probe.PrivateDataRedacted, neighbors,
                SourceSessionMatchesUx9M(sourceProject, sourceHistory, sourceRevision,
                    sourceJournal, sourceDirty, sourceCanUndo, sourceCanRedo, sourceArea,
                    sourceSelection, sourceSelected, sourceApproval),
                probed && probe.ResourcesReleased);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX9_M_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            result = FailedSmokeUx9M();
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            if (!DeleteTemporaryWorkspaceUx9M(root))
                result = result with { TemporaryWorkspaceReleased = false };
        }
        var sourceUnchanged = SourceSessionMatchesUx9M(sourceProject, sourceHistory,
            sourceRevision, sourceJournal, sourceDirty, sourceCanUndo, sourceCanRedo,
            sourceArea, sourceSelection, sourceSelected, sourceApproval);
        var released = result.TemporaryWorkspaceReleased && !Directory.Exists(root);
        var succeeded = result.FiveRoutes && result.ExplicitSelection &&
                        result.InvalidBlocked && result.PermissionBlocked &&
                        result.ConfirmationRequired && result.CanonicalDryRun &&
                        result.CanonicalCommand && result.StaleSourceBlocked &&
                        result.StaleApprovalBlocked && result.UndoRedo &&
                        result.Persistence && result.ForcedRest &&
                        result.LevelAdjustment && result.Appearance &&
                        result.SavedGroup && result.Recording && result.Privacy &&
                        result.ProjectNeighborsPreserved && sourceUnchanged && released;
        return result with
        {
            Succeeded = succeeded,
            SourceUnchanged = sourceUnchanged,
            TemporaryWorkspaceReleased = released
        };
    }

    private static StudioUx9MProductionDraft ChangedDraftUx9M(
        StudioUx9MProductionDraft source)
    {
        var min = Math.Abs(source.MinimumScale - .75) < .001 ? .8 : .75;
        var max = Math.Abs(source.MaximumScale - 1.75) < .001 ? 1.8 : 1.75;
        return source with
        {
            ForcedRestEnabled = true,
            MaximumRestTargets = source.MaximumRestTargets == 24 ? 25 : 24,
            LevelAdjustmentEnabled = true,
            MaximumLevelDelta = source.MaximumLevelDelta == 7 ? 8 : 7,
            RequireRollbackSnapshot = true,
            AppearanceEnabled = true,
            MinimumScale = min,
            MaximumScale = max,
            HasSavedGroup = true,
            GroupKey = source.HasSavedGroup ? source.GroupKey : "groupe-preuve",
            GroupQuery = source.HasSavedGroup ? source.GroupQuery : "tag:compagnons",
            GroupHotkey = source.HasSavedGroup ? source.GroupHotkey : source.GroupHotkey,
            MaximumGroupMembers = source.MaximumGroupMembers == 24 ? 25 : 24,
            RecordingEnabled = true,
            ExportFormat = source.ExportFormat == "orp-replay" ? "jsonl" : "orp-replay",
            MaximumRecordingMinutes = source.MaximumRecordingMinutes == 180 ? 181 : 180,
            RedactPrivateData = true
        };
    }

    private bool SourceSessionMatchesUx9M(ProjectDefinition project,
        ProjectCommandHistory history, ProjectRevision revision,
        IReadOnlyList<CommandHistoryJournalEntry> journal, bool dirty, bool canUndo,
        bool canRedo, Forge.Domain.Areas.AreaId? area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> selection,
        Forge.Domain.Entities.EntityId? selected, StudioUx9MApprovalSnapshot approval) =>
        ReferenceEquals(Project, project) && ReferenceEquals(History, history) &&
        history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
        IsDirty == dirty && history.CanUndo == canUndo && history.CanRedo == canRedo &&
        ActiveAreaId == area && SelectedEntityIds.SequenceEqual(selection) &&
        SelectedEntityId == selected && CaptureLiveProductionApprovalUx9M() == approval;

    private static bool DeleteTemporaryWorkspaceUx9M(string root)
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-m-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string LiveProductionSourceFingerprintUx9M(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            ProjectJsonSerializer.Serialize(project))));

    private static string DraftFingerprintUx9M(StudioUx9MProductionDraft draft)
    {
        var text = string.Join('|', draft.BaseRevision.Value,
            draft.SourceFingerprint, draft.ForcedRestEnabled, draft.MaximumRestTargets,
            draft.LevelAdjustmentEnabled, draft.MaximumLevelDelta,
            draft.RequireRollbackSnapshot, draft.AppearanceEnabled,
            draft.MinimumScale.ToString("R", CultureInfo.InvariantCulture),
            draft.MaximumScale.ToString("R", CultureInfo.InvariantCulture),
            draft.HasSavedGroup, draft.GroupKey, draft.GroupQuery, draft.GroupHotkey,
            draft.MaximumGroupMembers, draft.RecordingEnabled, draft.ExportFormat,
            draft.MaximumRecordingMinutes, draft.RedactPrivateData);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static int FirstAvailableHotkeyUx9M(
        IReadOnlyList<LiveDirectorProductionProfile.SavedGroup> groups) =>
        Enumerable.Range(1, 12).FirstOrDefault(value =>
            groups.All(group => group.Hotkey != value), 1);

    private static Guid StableUx9M(Guid ownerId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:studio:ux9-m:{ownerId:D}:{discriminator}"));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string NormalizeKeyUx9M(string value) => string.Join('-', value.Trim()
        .ToLowerInvariant().Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries));

    private static bool ProbeReadyUx9M(StudioUx9MProductionProbe probe) =>
        probe.RestPolicyRespected && probe.LevelPolicyRespected &&
        probe.AppearancePolicyRespected && probe.RecordingPolicyRespected &&
        probe.PermissionCode == "LIVE_PRODUCTION_PERMISSION_DENIED" &&
        probe.ConfirmationCode == "LIVE_PRODUCTION_CONFIRMATION_REQUIRED" &&
        probe.ReasonCode == "LIVE_PRODUCTION_REASON_REQUIRED" &&
        probe.ResourcesReleased;

    private static string HumanProfileErrorUx9M(string message) =>
        message.Contains("Saved Live Director groups", StringComparison.Ordinal)
            ? "Le groupe exige un nom et un raccourci uniques, de 1 à 12, avec 1 à 512 membres."
            : message.Contains("Appearance overrides", StringComparison.Ordinal)
                ? "Les échelles d’apparence doivent rester ordonnées entre 0,1 et 10."
                : message.Contains("Session recording", StringComparison.Ordinal)
                    ? "L’enregistrement exige un format pris en charge et 1 à 1 440 minutes."
                    : message.Contains("bounded operations", StringComparison.Ordinal)
                        ? "Les plafonds de repos et de niveau doivent rester dans leurs bornes."
                        : $"Le contrat canonique refuse le profil : {message}";

    private static string HumanDiagnosticsUx9M(
        IReadOnlyList<CommandDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "La commande canonique a refusé les politiques."
            : string.Join(" · ", diagnostics.Select(item => item.Message));

    private static StudioUx9MProductionPreview FailedPreviewUx9M(string message) => new(
        false, false, false, false, message, [], [message], EmptyProbeUx9M());

    private static StudioUx9MProductionProbe EmptyProbeUx9M() => new(
        "Aucune sandbox exécutée", "—", false, "—", false, false, "—", false,
        "—", false, 0, "—", false, "—", "—", "—", true);

    private static StudioUx9MSmokeResult FailedSmokeUx9M() => new(false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false);
}
