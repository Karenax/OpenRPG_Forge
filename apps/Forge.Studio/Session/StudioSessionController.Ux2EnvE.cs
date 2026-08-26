using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvEAudioCode = "studio.environment.audio_profile_invalid";
    private const string Ux2EnvELayerCode = "studio.environment.audio_layer_invalid";
    private const string Ux2EnvEMusicCode = "studio.environment.music_state_invalid";
    private const string Ux2EnvEBusCode = "studio.environment.mix_bus_invalid";
    private const string Ux2EnvESnapshotCode = "studio.environment.audio_snapshot_invalid";

    private readonly PatchAreaEnvironmentHandler _setAreaEnvironmentUx2EnvE = new(new SystemClock());
    private readonly SetAreaAuthoringHandler _setAreaAuthoringUx2EnvE = new(new SystemClock());
    private StudioUx2EnvEAudioDraft? _approvedAudioUx2EnvE;
    private ProjectRevision? _approvedAudioRevisionUx2EnvE;
    private string? _approvedAudioFingerprintUx2EnvE;
    private StudioUx2EnvESnapshotsDraft? _approvedSnapshotsUx2EnvE;
    private ProjectRevision? _approvedSnapshotsRevisionUx2EnvE;
    private string? _approvedSnapshotsFingerprintUx2EnvE;

    internal IReadOnlyList<StudioUx2EnvEAreaChoice> AudioChoicesUx2EnvE
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildAudioChoicesUx2EnvE(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvEAreaChoice> BuildAudioChoicesUx2EnvE(ProjectDefinition project)
    {
        var duplicateNames = project.Areas.GroupBy(area => area.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var displayName = ContainsTechnicalIdentifierUx2EnvE(area.DisplayName)
                    ? "Zone sans nom lisible"
                    : area.DisplayName.Trim();
                var name = duplicateNames[area.DisplayName] > 1
                    ? $"{displayName} · {HumanTokenUx2EnvE(area.Slug)}"
                    : displayName;
                var audio = area.Environment.Audio;
                return new StudioUx2EnvEAreaChoice(area.Id,
                    $"{name} · {audio.AdaptiveLayers.Count} couche(s) · " +
                    $"{audio.Orchestration.MusicStates.Count} état(s) musique · " +
                    $"{audio.Orchestration.MixBuses.Count} bus · " +
                    $"{area.Authoring.Experience.AudioSnapshots.Count} snapshot(s)",
                    audio.AdaptiveLayers.Count, audio.Orchestration.MusicStates.Count,
                    audio.Orchestration.MixBuses.Count, area.Authoring.Experience.AudioSnapshots.Count);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvEAudioDraft? CreateAudioDraftUx2EnvE(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1 ? CreateAudioDraftUx2EnvE(history.State.Revision, matches[0]) : null;
    }

    private static StudioUx2EnvEAudioDraft CreateAudioDraftUx2EnvE(ProjectRevision revision, AreaDefinition area)
    {
        var audio = area.Environment.Audio;
        return new StudioUx2EnvEAudioDraft(
            area.Id, revision, AudioSourceFingerprintUx2EnvE(area), area.DisplayName, area.Slug,
            audio.DayMusic, audio.NightMusic, audio.CombatMusic, audio.Ambience,
            audio.TransitionSeconds, audio.Playlist.ToArray(), audio.OrderMode,
            audio.IntervalSeconds, audio.IntervalVariationSeconds, audio.RelativeVolume,
            audio.AdaptiveLayers.Select(item => new StudioUx2EnvEAdaptiveLayerDraft(
                item.Resource, item.StartHour, item.EndHour, item.Weather, item.LocationTag,
                item.MinimumDanger, item.IndoorOnly, item.Volume, item.CrossfadeSeconds, item.VolumeId)).ToArray(),
            audio.Orchestration.MusicStates.Select(item => new StudioUx2EnvEMusicStateDraft(
                item.State, item.Track, item.Stems.ToArray(), item.TransitionSeconds, item.CooldownSeconds,
                item.BeatSynchronized, item.TempoBpm, item.BeatsPerBar)).ToArray(),
            audio.Orchestration.MixBuses.Select(item => new StudioUx2EnvEMixBusDraft(
                item.Id, item.Name, item.ParentId, item.Volume, item.Snapshot, item.Platform,
                item.DynamicRange, item.EnhanceImportantSounds, item.MonitorEnabled)).ToArray());
    }

    internal StudioUx2EnvESnapshotsDraft? CreateSnapshotsDraftUx2EnvE(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1 ? CreateSnapshotsDraftUx2EnvE(history.State.Revision, matches[0]) : null;
    }

    private static StudioUx2EnvESnapshotsDraft CreateSnapshotsDraftUx2EnvE(
        ProjectRevision revision,
        AreaDefinition area)
    {
        var busNames = area.Environment.Audio.Orchestration.MixBuses.Select(item => item.Name)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new StudioUx2EnvESnapshotsDraft(
            area.Id, revision, AudioSourceFingerprintUx2EnvE(area), area.DisplayName, area.Slug, busNames,
            area.Authoring.Experience.AudioSnapshots.Select(item => new StudioUx2EnvESnapshotDraft(
                item.Id, item.Name,
                item.BusVolumes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new StudioUx2EnvESnapshotBusDraft(pair.Key, pair.Value)).ToArray(),
                item.TransitionSeconds, item.TriggerEvent, item.Provenance)).ToArray());
    }

    internal StudioUx2EnvEAudioPreview PreviewAudioUx2EnvE(StudioUx2EnvEAudioDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAudioApprovalUx2EnvE();
        var history = History;
        if (history is null || Project is null)
            return FailedAudioPreviewUx2EnvE("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedAudioPreviewUx2EnvE(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la zone avant l’aperçu audio.");
        if (!TryBuildAudioUx2EnvE(draft, true, out var command, out var sourceArea, out _,
                out var candidateEnvironment, out var diagnostics, out var message))
            return new(false, message, string.Empty, false, EmptyAudioDiffUx2EnvE(),
                GroupAudioDiagnosticsUx2EnvE(diagnostics), false, false, false, false, false, false, true, true);

        var dryRun = _setAreaEnvironmentUx2EnvE.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => CommandDiagnosticUx2EnvE(item,
                $"areas/{draft.AreaId}/environment/audio")).ToArray();
            return new(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande audio a refusé cet aperçu.",
                string.Empty, false,
                BuildAudioDiffUx2EnvE(sourceArea!.Environment.Audio, candidateEnvironment!.Audio),
                GroupAudioDiagnosticsUx2EnvE(commandDiagnostics), false, false, false, false, false, false, true, true);
        }

        var candidate = candidateEnvironment!.Audio;
        var previousContext = BuildAudioContextUx2EnvE(candidate, previous: true);
        var nextContext = BuildAudioContextUx2EnvE(candidate, previous: false);
        var crossfade = AudioOrchestrationRuntimeService.PlanAdaptiveCrossfade(candidate, previousContext, nextContext);
        var musicFrom = candidate.Orchestration.MusicStates.Any(item => item.State == MusicState.Exploration)
            ? MusicState.Exploration
            : candidate.Orchestration.MusicStates.FirstOrDefault()?.State ?? MusicState.Exploration;
        var musicTo = candidate.Orchestration.MusicStates.FirstOrDefault(item => item.State == MusicState.Combat)?.State ??
                      candidate.Orchestration.MusicStates.FirstOrDefault(item => item.State != musicFrom)?.State ??
                      musicFrom;
        var musicPlan = AreaMusicStateMachine.Transition(candidate.Orchestration, musicFrom, musicTo,
            elapsedSinceLastTransition: float.MaxValue, playbackSeconds: 3.25f);
        var readings = candidate.Orchestration.MixBuses.ToDictionary(item => item.Name,
            _ => (PeakDb: -6f, RmsDb: -12f, LoudnessLufs: -18f), StringComparer.Ordinal);
        var platform = candidate.Orchestration.MixBuses.FirstOrDefault()?.Platform ?? "all";
        var snapshot = candidate.Orchestration.MixBuses.FirstOrDefault()?.Snapshot ?? "default";
        var meters = AudioOrchestrationRuntimeService.MonitorMix(candidate.Orchestration, readings,
            platform, snapshot);
        var listenerMix = AreaAudioMixer.Evaluate(candidate, nextContext);
        var sequenceStatistics = AreaAudioSequencePlanner.PreviewStatistics(candidate, 64, 2026);
        var diff = BuildAudioDiffUx2EnvE(sourceArea!.Environment.Audio, candidate);
        if (diff.HasChanges)
        {
            _approvedAudioUx2EnvE = draft;
            _approvedAudioRevisionUx2EnvE = history.State.Revision;
            _approvedAudioFingerprintUx2EnvE = AudioDraftFingerprintUx2EnvE(draft);
        }
        var summary =
            $"Plan audio déterministe : {crossfade.Entries.Count} transition(s) de couche entre midi et nuit; " +
            (musicPlan is null
                ? "aucune transition musicale disponible dans ce profil; "
                : $"musique {HumanMusicStateUx2EnvE(musicPlan.From)} → {HumanMusicStateUx2EnvE(musicPlan.To)}, " +
                  $"fondu {musicPlan.FadeSeconds:0.##} s, délai de mesure {musicPlan.StartDelaySeconds:0.##} s; ") +
            $"{meters.Count} bus simulé(s), gain auditeur {listenerMix.NormalizedGain:0.##}; " +
            $"playlist déterministe : {sequenceStatistics.SampleCount} échantillon(s). " +
            "Les niveaux pic/RMS/LUFS sont des entrées de simulation, pas des mesures moteur. " +
            "Aucun AudioStreamPlayer, périphérique, bus Godot ou playback live n’est instancié par cet aperçu.";
        return new(true,
            diff.HasChanges
                ? "Aperçu 1/2 validé · le profil audio n’est pas encore appliqué."
                : "Aperçu 1/2 validé · aucune différence audio à appliquer.",
            summary, diff.HasChanges, diff, GroupAudioDiagnosticsUx2EnvE([]), true, true,
            musicPlan is not null,
            true, true, true, true, true);
    }

    internal StudioUx2EnvESnapshotPreview PreviewSnapshotsUx2EnvE(StudioUx2EnvESnapshotsDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelSnapshotsApprovalUx2EnvE();
        var history = History;
        if (history is null || Project is null)
            return FailedSnapshotPreviewUx2EnvE("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedSnapshotPreviewUx2EnvE(
                "Le projet a changé. Rechargez les snapshots puis relancez l’aperçu 2/2.");
        if (!TryBuildSnapshotsUx2EnvE(draft, true, out var command, out var sourceArea, out _,
                out var candidateAuthoring, out var diagnostics, out var message))
            return new(false, message, string.Empty, false, new([]),
                GroupAudioDiagnosticsUx2EnvE(diagnostics), false, false, true);
        var dryRun = _setAreaAuthoringUx2EnvE.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => CommandDiagnosticUx2EnvE(item,
                $"areas/{draft.AreaId}/authoring/experience/audioSnapshots")).ToArray();
            return new(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande snapshots a refusé cet aperçu.",
                string.Empty, false,
                BuildSnapshotDiffUx2EnvE(sourceArea!.Authoring, candidateAuthoring!),
                GroupAudioDiagnosticsUx2EnvE(commandDiagnostics), false, false, true);
        }
        var diff = BuildSnapshotDiffUx2EnvE(sourceArea!.Authoring, candidateAuthoring!);
        if (diff.HasChanges)
        {
            _approvedSnapshotsUx2EnvE = draft;
            _approvedSnapshotsRevisionUx2EnvE = history.State.Revision;
            _approvedSnapshotsFingerprintUx2EnvE = SnapshotsDraftFingerprintUx2EnvE(draft);
        }
        return new(true,
            diff.HasChanges
                ? "Aperçu 2/2 validé · les snapshots restent une seconde transaction."
                : "Aperçu 2/2 validé · aucune différence de snapshot à appliquer.",
            $"{candidateAuthoring!.Experience.AudioSnapshots.Count} snapshot(s), " +
            $"{candidateAuthoring.Experience.AudioSnapshots.Sum(item => item.BusVolumes.Count)} niveau(x) de bus. " +
            "Cette commande n’inclut pas le profil audio : appliquez et annulez les deux étapes séparément.",
            diff.HasChanges, diff, GroupAudioDiagnosticsUx2EnvE([]), true, true, true);
    }

    internal StudioUx2EnvEActionResult ApplyApprovedAudioUx2EnvE()
    {
        var draft = _approvedAudioUx2EnvE;
        var history = History;
        if (draft is null || history is null || _approvedAudioRevisionUx2EnvE != history.State.Revision ||
            !string.Equals(_approvedAudioFingerprintUx2EnvE, AudioDraftFingerprintUx2EnvE(draft),
                StringComparison.Ordinal))
        {
            CancelAudioApprovalUx2EnvE();
            return new(false, "Relancez l’aperçu 1/2 avant d’appliquer le profil audio.");
        }
        if (!TryBuildAudioUx2EnvE(draft, false, out var command, out _, out _, out _, out _, out var message))
        {
            CancelAudioApprovalUx2EnvE();
            return new(false, message);
        }
        var preview = _setAreaEnvironmentUx2EnvE.Handle(history.State, command! with { DryRun = true });
        if (!preview.Succeeded)
        {
            CancelAudioApprovalUx2EnvE();
            return new(false, HumanizeAudioDiagnosticUx2EnvE(preview.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaEnvironmentUx2EnvE.Handle(history.State, command!);
        CancelAudioApprovalUx2EnvE();
        CancelSnapshotsApprovalUx2EnvE();
        if (!applied.Succeeded)
            return new(false, HumanizeAudioDiagnosticUx2EnvE(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true,
            "Étape 1/2 appliquée par patch_area_environment · snapshots inchangés et nouvel aperçu 2/2 requis");
    }

    internal StudioUx2EnvEActionResult ApplyApprovedSnapshotsUx2EnvE()
    {
        var draft = _approvedSnapshotsUx2EnvE;
        var history = History;
        if (draft is null || history is null || _approvedSnapshotsRevisionUx2EnvE != history.State.Revision ||
            !string.Equals(_approvedSnapshotsFingerprintUx2EnvE, SnapshotsDraftFingerprintUx2EnvE(draft),
                StringComparison.Ordinal))
        {
            CancelSnapshotsApprovalUx2EnvE();
            return new(false, "Relancez l’aperçu 2/2 avant d’appliquer les snapshots.");
        }
        if (!TryBuildSnapshotsUx2EnvE(draft, false, out var command, out _, out _, out _, out _, out var message))
        {
            CancelSnapshotsApprovalUx2EnvE();
            return new(false, message);
        }
        var preview = _setAreaAuthoringUx2EnvE.Handle(history.State, command! with { DryRun = true });
        if (!preview.Succeeded)
        {
            CancelSnapshotsApprovalUx2EnvE();
            return new(false, HumanizeAudioDiagnosticUx2EnvE(preview.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaAuthoringUx2EnvE.Handle(history.State, command!);
        CancelSnapshotsApprovalUx2EnvE();
        CancelAudioApprovalUx2EnvE();
        if (!applied.Succeeded)
            return new(false, HumanizeAudioDiagnosticUx2EnvE(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true,
            "Étape 2/2 appliquée par set_area_authoring · les deux commandes restent annulables séparément");
    }

    internal void CancelAudioApprovalUx2EnvE()
    {
        _approvedAudioUx2EnvE = null;
        _approvedAudioRevisionUx2EnvE = null;
        _approvedAudioFingerprintUx2EnvE = null;
    }

    internal void CancelSnapshotsApprovalUx2EnvE()
    {
        _approvedSnapshotsUx2EnvE = null;
        _approvedSnapshotsRevisionUx2EnvE = null;
        _approvedSnapshotsFingerprintUx2EnvE = null;
    }

    internal void CancelAudioApprovalsUx2EnvE()
    {
        CancelAudioApprovalUx2EnvE();
        CancelSnapshotsApprovalUx2EnvE();
    }

    internal StudioUx2EnvEApprovalSnapshot CaptureAudioApprovalUx2EnvE() => new(
        _approvedAudioUx2EnvE, _approvedAudioRevisionUx2EnvE, _approvedAudioFingerprintUx2EnvE,
        _approvedSnapshotsUx2EnvE, _approvedSnapshotsRevisionUx2EnvE, _approvedSnapshotsFingerprintUx2EnvE);

    internal void RestoreAudioApprovalUx2EnvE(StudioUx2EnvEApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAudioUx2EnvE = snapshot.AudioDraft;
        _approvedAudioRevisionUx2EnvE = snapshot.AudioRevision;
        _approvedAudioFingerprintUx2EnvE = snapshot.AudioFingerprint;
        _approvedSnapshotsUx2EnvE = snapshot.SnapshotsDraft;
        _approvedSnapshotsRevisionUx2EnvE = snapshot.SnapshotsRevision;
        _approvedSnapshotsFingerprintUx2EnvE = snapshot.SnapshotsFingerprint;
    }

    private bool TryBuildAudioUx2EnvE(
        StudioUx2EnvEAudioDraft draft,
        bool dryRun,
        out PatchAreaEnvironmentCommand? command,
        out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea,
        out AreaEnvironmentProfile? candidateEnvironment,
        out IReadOnlyList<StudioUx2EnvEDiagnostic> diagnostics,
        out string message)
    {
        command = null; sourceArea = null; candidateArea = null; candidateEnvironment = null;
        diagnostics = []; message = string.Empty;
        var project = Project; var history = History;
        if (project is null || history is null)
        {
            message = "Aucun module n’est ouvert."; return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez la zone avant l’aperçu audio."; return false;
        }
        var matches = project.Areas.Where(area => area.Id == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "La zone choisie n’existe plus ou n’est plus unique."; return false;
        }
        sourceArea = matches[0];
        if (!string.Equals(draft.SourceFingerprint, AudioSourceFingerprintUx2EnvE(sourceArea),
                StringComparison.Ordinal))
        {
            message = "L’audio, les snapshots ou un voisin de la zone ont changé. Rechargez la fiche.";
            return false;
        }
        var inputDiagnostics = ValidateAudioDraftUx2EnvE(draft, sourceArea.Environment.Audio).ToArray();
        if (inputDiagnostics.Length > 0)
        {
            diagnostics = inputDiagnostics; message = inputDiagnostics[0].Message; return false;
        }

        try
        {
            var sourceEnvironment = sourceArea.Environment;
            var sourceAudio = sourceEnvironment.Audio;
            var layers = draft.AdaptiveLayers.Select(item => new AdaptiveAmbienceLayer(
                item.Resource.Trim(), ExactFloatUx2EnvE(item.StartHour), ExactFloatUx2EnvE(item.EndHour),
                item.Weather.Trim(), item.LocationTag.Trim(), ExactFloatUx2EnvE(item.MinimumDanger),
                item.IndoorOnly, ExactFloatUx2EnvE(item.Volume), ExactFloatUx2EnvE(item.CrossfadeSeconds),
                item.VolumeId.Trim())).ToArray();
            var musicStates = draft.MusicStates.Select(item => new MusicStateEntry(
                item.State, item.Track.Trim(), item.Stems.Select(stem => stem.Trim()).ToArray(),
                ExactFloatUx2EnvE(item.TransitionSeconds), ExactFloatUx2EnvE(item.CooldownSeconds),
                item.BeatSynchronized, ExactFloatUx2EnvE(item.TempoBpm), ExactIntUx2EnvE(item.BeatsPerBar))).ToArray();
            var sourceBuses = sourceAudio.Orchestration.MixBuses.ToDictionary(item => item.Id);
            var buses = draft.MixBuses.Select(item =>
            {
                var rebuilt = new AudioMixBus(item.Id, item.Name.Trim(), item.ParentId,
                    ExactFloatUx2EnvE(item.Volume), item.Snapshot.Trim(), item.Platform.Trim(),
                    ExactFloatUx2EnvE(item.DynamicRange), item.EnhanceImportantSounds, item.MonitorEnabled);
                return sourceBuses.TryGetValue(item.Id, out var existing) && existing == rebuilt
                    ? existing
                    : rebuilt;
            }).ToArray();
            var orchestration = new AreaAudioOrchestration(musicStates, buses,
                sourceAudio.Orchestration.Captions);
            var rebuiltAudio = new AreaAudioProfile(
                draft.DayMusic.Trim(), draft.NightMusic.Trim(), draft.CombatMusic.Trim(), draft.Ambience.Trim(),
                sourceAudio.ReverbPreset, ExactFloatUx2EnvE(draft.TransitionSeconds),
                draft.Playlist.Select(item => item.Trim()).ToArray(), draft.OrderMode,
                ExactFloatUx2EnvE(draft.IntervalSeconds), ExactFloatUx2EnvE(draft.IntervalVariationSeconds),
                ExactFloatUx2EnvE(draft.RelativeVolume), sourceAudio.Spatial, sourceAudio.RuntimeControl,
                layers, orchestration, sourceAudio.Emitters, sourceAudio.Authoring);
            var audio = SameAudioUx2EnvE(sourceAudio, rebuiltAudio) ? sourceAudio : rebuiltAudio;
            var environment = new AreaEnvironmentProfile(
                sourceEnvironment.SchemaVersion, sourceEnvironment.Classification, sourceEnvironment.Tags,
                sourceEnvironment.Lighting, sourceEnvironment.Fog, audio, sourceEnvironment.Rendering,
                sourceEnvironment.Sky, sourceEnvironment.Wind, sourceEnvironment.Weather,
                sourceEnvironment.Water, sourceEnvironment.Reverb, sourceEnvironment.Authoring,
                sourceEnvironment.RequireAuthoringRuntimeMetadata);
            if (!BuildAudioDiffUx2EnvE(sourceAudio, audio).HasChanges) environment = sourceEnvironment;
            var updatedArea = ReferenceEquals(environment, sourceEnvironment)
                ? sourceArea
                : sourceArea.WithEnvironment(environment);
            candidateEnvironment = environment; candidateArea = updatedArea;
            var candidateProject = ReplaceAreaUx2EnvE(project, sourceArea.Id, updatedArea);
            var projectDiagnostics = ProjectValidator.Validate(candidateProject).Select(item =>
                new StudioUx2EnvEDiagnostic(item.Code, item.Path,
                    HumanizeAudioDiagnosticUx2EnvE(item.Code))).ToArray();
            if (projectDiagnostics.Length > 0)
            {
                diagnostics = projectDiagnostics; message = projectDiagnostics[0].Message; return false;
            }
            command = new PatchAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                sourceArea.Id, environment, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or OverflowException)
        {
            var diagnostic = new StudioUx2EnvEDiagnostic(Ux2EnvEAudioCode,
                $"areas/{draft.AreaId}/environment/audio",
                exception is OverflowException
                    ? "Une valeur audio doit être un nombre exact représentable par le profil canonique."
                    : "Le profil audio ne peut pas être reconstruit avec ces valeurs.");
            diagnostics = [diagnostic]; message = diagnostic.Message; return false;
        }
    }

    private bool TryBuildSnapshotsUx2EnvE(
        StudioUx2EnvESnapshotsDraft draft,
        bool dryRun,
        out SetAreaAuthoringCommand? command,
        out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea,
        out AreaAuthoringProfile? candidateAuthoring,
        out IReadOnlyList<StudioUx2EnvEDiagnostic> diagnostics,
        out string message)
    {
        command = null; sourceArea = null; candidateArea = null; candidateAuthoring = null;
        diagnostics = []; message = string.Empty;
        var project = Project; var history = History;
        if (project is null || history is null)
        {
            message = "Aucun module n’est ouvert."; return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez les snapshots avant l’aperçu 2/2."; return false;
        }
        var matches = project.Areas.Where(area => area.Id == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "La zone choisie n’existe plus ou n’est plus unique."; return false;
        }
        sourceArea = matches[0];
        if (!string.Equals(draft.SourceFingerprint, AudioSourceFingerprintUx2EnvE(sourceArea),
                StringComparison.Ordinal))
        {
            message = "Le profil audio ou les snapshots ont changé. Rechargez l’étape 2/2."; return false;
        }
        var inputDiagnostics = ValidateSnapshotsDraftUx2EnvE(draft, sourceArea).ToArray();
        if (inputDiagnostics.Length > 0)
        {
            diagnostics = inputDiagnostics; message = inputDiagnostics[0].Message; return false;
        }

        try
        {
            var sourceAuthoring = sourceArea.Authoring;
            var sourceExperience = sourceAuthoring.Experience;
            var sourceSnapshots = sourceExperience.AudioSnapshots.ToDictionary(item => item.Id);
            var snapshots = draft.Snapshots.Select(item =>
            {
                var volumes = item.BusVolumes.ToDictionary(level => level.BusName.Trim(),
                    level => ExactFloatUx2EnvE(level.Volume), StringComparer.Ordinal);
                var rebuilt = new AreaAudioSnapshotProfile(item.Id, item.Name.Trim(), volumes,
                    ExactFloatUx2EnvE(item.TransitionSeconds), item.TriggerEvent.Trim(), item.Provenance.Trim());
                return sourceSnapshots.TryGetValue(item.Id, out var existing) && SameSnapshotUx2EnvE(existing, rebuilt)
                    ? existing
                    : rebuilt;
            }).ToArray();
            var experience = SameSnapshotsUx2EnvE(sourceExperience.AudioSnapshots, snapshots)
                ? sourceExperience
                : new AreaExperienceAuthoringProfile(sourceExperience.Id, sourceExperience.SchemaVersion,
                    sourceExperience.Provenance, snapshots, sourceExperience.Visibility, sourceExperience.Lod,
                    sourceExperience.RequireRuntimeMetadata);
            var authoring = ReferenceEquals(experience, sourceExperience)
                ? sourceAuthoring
                : new AreaAuthoringProfile(sourceAuthoring.SchemaVersion, sourceAuthoring.UserEvents,
                    sourceAuthoring.Map, sourceAuthoring.Layers, experience,
                    sourceAuthoring.RequireLayerRuntimeMetadata);
            var updatedArea = ReferenceEquals(authoring, sourceAuthoring)
                ? sourceArea
                : sourceArea.WithAuthoring(authoring);
            candidateAuthoring = authoring; candidateArea = updatedArea;
            var candidateProject = ReplaceAreaUx2EnvE(project, sourceArea.Id, updatedArea);
            var projectDiagnostics = ProjectValidator.Validate(candidateProject).Select(item =>
                new StudioUx2EnvEDiagnostic(item.Code, item.Path,
                    HumanizeAudioDiagnosticUx2EnvE(item.Code))).ToArray();
            if (projectDiagnostics.Length > 0)
            {
                diagnostics = projectDiagnostics; message = projectDiagnostics[0].Message; return false;
            }
            command = new SetAreaAuthoringCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                sourceArea.Id, authoring, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or OverflowException)
        {
            var diagnostic = new StudioUx2EnvEDiagnostic(Ux2EnvESnapshotCode,
                $"areas/{draft.AreaId}/authoring/experience/audioSnapshots",
                exception is OverflowException
                    ? "Un niveau de snapshot doit être un nombre représentable par le profil canonique."
                    : "Les snapshots ne peuvent pas être reconstruits avec ces valeurs.");
            diagnostics = [diagnostic]; message = diagnostic.Message; return false;
        }
    }

    private static IReadOnlyList<StudioUx2EnvEDiagnostic> ValidateAudioDraftUx2EnvE(
        StudioUx2EnvEAudioDraft draft,
        AreaAudioProfile source)
    {
        var result = new List<StudioUx2EnvEDiagnostic>();
        var governedResources = source.Emitters.Definitions.SelectMany(item => item.ClipSet.Clips)
            .Select(item => item.ResourcePath.Trim().Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        var optionalResources = new[] { draft.DayMusic, draft.NightMusic, draft.CombatMusic, draft.Ambience };
        if (!Enum.IsDefined(draft.OrderMode) || !FiniteFloatUx2EnvE(draft.TransitionSeconds) ||
            draft.TransitionSeconds < 0 || !FiniteFloatUx2EnvE(draft.IntervalSeconds) ||
            draft.IntervalSeconds < 0 || !FiniteFloatUx2EnvE(draft.IntervalVariationSeconds) ||
            draft.IntervalVariationSeconds < 0 || !FiniteFloatUx2EnvE(draft.RelativeVolume) ||
            draft.RelativeVolume is < 0 or > 4 ||
            optionalResources.Any(resource => !ValidGovernedAudioResourceUx2EnvE(resource, governedResources,
                allowEmpty: true)) ||
            draft.Playlist.Any(resource => !ValidGovernedAudioResourceUx2EnvE(resource, governedResources,
                allowEmpty: false)) ||
            draft.Playlist.Select(NormalizeAudioResourceUx2EnvE).Distinct(StringComparer.Ordinal).Count() !=
            draft.Playlist.Count)
            result.Add(new(Ux2EnvEAudioCode, $"areas/{draft.AreaId}/environment/audio",
                "Audio de zone : ressources intégrées ou gouvernées, ordre et volumes doivent être valides."));

        if (draft.AdaptiveLayers.Select(item => NormalizeAudioResourceUx2EnvE(item.Resource))
                .Distinct(StringComparer.Ordinal).Count() != draft.AdaptiveLayers.Count ||
            draft.AdaptiveLayers.Any(item =>
                !ValidGovernedAudioResourceUx2EnvE(item.Resource, governedResources, allowEmpty: false) ||
                !FiniteFloatUx2EnvE(item.StartHour) || item.StartHour is < 0 or >= 24 ||
                !FiniteFloatUx2EnvE(item.EndHour) || item.EndHour is < 0 or >= 24 ||
                !FiniteFloatUx2EnvE(item.MinimumDanger) || item.MinimumDanger is < 0 or > 1 ||
                !FiniteFloatUx2EnvE(item.Volume) || item.Volume is < 0 or > 4 ||
                !FiniteFloatUx2EnvE(item.CrossfadeSeconds) || item.CrossfadeSeconds is < 0 or > 120))
            result.Add(new(Ux2EnvELayerCode, $"areas/{draft.AreaId}/environment/audio/adaptiveLayers",
                "Ambiance adaptative : ressources uniques, heures, danger, volume et fondu doivent être bornés."));

        if (draft.MusicStates.Select(item => item.State).Distinct().Count() != draft.MusicStates.Count ||
            draft.MusicStates.Any(item => !Enum.IsDefined(item.State) ||
                !ValidGovernedAudioResourceUx2EnvE(item.Track, governedResources, allowEmpty: false) ||
                item.Stems.Count != item.Stems.Select(NormalizeAudioResourceUx2EnvE)
                    .Distinct(StringComparer.Ordinal).Count() ||
                item.Stems.Any(stem => !ValidGovernedAudioResourceUx2EnvE(stem, governedResources,
                    allowEmpty: false)) ||
                !FiniteFloatUx2EnvE(item.TransitionSeconds) || item.TransitionSeconds < 0 ||
                !FiniteFloatUx2EnvE(item.CooldownSeconds) || item.CooldownSeconds < 0 ||
                !FiniteFloatUx2EnvE(item.TempoBpm) || item.TempoBpm is < 20 or > 400 ||
                !ValidExactIntUx2EnvE(item.BeatsPerBar, 1, 32)))
            result.Add(new(Ux2EnvEMusicCode, $"areas/{draft.AreaId}/environment/audio/orchestration/musicStates",
                "Musique : états uniques, pistes gouvernées, transitions, tempo et mesure doivent être valides."));

        var busIds = draft.MixBuses.Select(item => item.Id).ToArray();
        var busNames = draft.MixBuses.Select(item => item.Name.Trim()).ToArray();
        if (busIds.Any(id => id == Guid.Empty) || busIds.Distinct().Count() != busIds.Length ||
            busNames.Any(string.IsNullOrWhiteSpace) ||
            busNames.Distinct(StringComparer.Ordinal).Count() != busNames.Length ||
            draft.MixBuses.Any(item => item.ParentId == item.Id ||
                item.ParentId is { } parent && !busIds.Contains(parent) ||
                !FiniteFloatUx2EnvE(item.Volume) || item.Volume is < 0 or > 4 ||
                string.IsNullOrWhiteSpace(item.Snapshot) || string.IsNullOrWhiteSpace(item.Platform) ||
                !FiniteFloatUx2EnvE(item.DynamicRange) || item.DynamicRange is < 0 or > 1) ||
            MixBusCycleUx2EnvE(draft.MixBuses) ||
            draft.MixBuses.Count > 0 &&
            (!busNames.Contains(source.Authoring.Mix.Bus, StringComparer.Ordinal) ||
             !busNames.Contains(source.Authoring.Ambient.Bus, StringComparer.Ordinal)))
            result.Add(new(Ux2EnvEBusCode, $"areas/{draft.AreaId}/environment/audio/orchestration/mixBuses",
                "Mixage : noms et identités uniques, parents sans cycle, profils et bus auteur résolus sont requis."));
        return result;
    }

    private static IReadOnlyList<StudioUx2EnvEDiagnostic> ValidateSnapshotsDraftUx2EnvE(
        StudioUx2EnvESnapshotsDraft draft,
        AreaDefinition sourceArea)
    {
        var result = new List<StudioUx2EnvEDiagnostic>();
        var currentBusNames = sourceArea.Environment.Audio.Orchestration.MixBuses.Select(item => item.Name)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var draftBusNames = draft.AvailableBusNames.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        if (!currentBusNames.SequenceEqual(draftBusNames, StringComparer.Ordinal))
            result.Add(new(Ux2EnvESnapshotCode,
                $"areas/{draft.AreaId}/authoring/experience/audioSnapshots",
                "La liste des bus a changé : rechargez les snapshots après l’étape audio."));
        if (draft.Snapshots.Select(item => item.Id).Any(id => id == Guid.Empty) ||
            draft.Snapshots.Select(item => item.Id).Distinct().Count() != draft.Snapshots.Count ||
            draft.Snapshots.Select(item => item.Name.Trim()).Any(string.IsNullOrWhiteSpace) ||
            draft.Snapshots.Select(item => item.Name.Trim()).Distinct(StringComparer.Ordinal).Count() !=
            draft.Snapshots.Count ||
            draft.Snapshots.Any(item => string.IsNullOrWhiteSpace(item.Provenance) ||
                !FiniteFloatUx2EnvE(item.TransitionSeconds) || item.TransitionSeconds is < 0 or > 60 ||
                item.BusVolumes.Count == 0 ||
                item.BusVolumes.Select(level => level.BusName.Trim()).Any(string.IsNullOrWhiteSpace) ||
                item.BusVolumes.Select(level => level.BusName.Trim()).Distinct(StringComparer.Ordinal).Count() !=
                item.BusVolumes.Count || item.BusVolumes.Any(level =>
                    !currentBusNames.Contains(level.BusName.Trim(), StringComparer.Ordinal) ||
                    !FiniteFloatUx2EnvE(level.Volume) || level.Volume is < 0 or > 4)))
            result.Add(new(Ux2EnvESnapshotCode,
                $"areas/{draft.AreaId}/authoring/experience/audioSnapshots",
                "Snapshots : noms uniques, bus existants, niveaux de 0 à 4, transition et provenance sont requis."));
        return result;
    }

    private static StudioUx2EnvEAudioDiff BuildAudioDiffUx2EnvE(
        AreaAudioProfile source,
        AreaAudioProfile candidate)
    {
        var zone = new List<string>();
        if (!string.Equals(source.DayMusic, candidate.DayMusic, StringComparison.Ordinal) ||
            !string.Equals(source.NightMusic, candidate.NightMusic, StringComparison.Ordinal) ||
            !string.Equals(source.CombatMusic, candidate.CombatMusic, StringComparison.Ordinal) ||
            !string.Equals(source.Ambience, candidate.Ambience, StringComparison.Ordinal))
            zone.Add("Musiques jour, nuit, combat ou ambiance de zone modifiées.");
        if (!source.TransitionSeconds.Equals(candidate.TransitionSeconds) ||
            !source.Playlist.SequenceEqual(candidate.Playlist, StringComparer.Ordinal) ||
            source.OrderMode != candidate.OrderMode || !source.IntervalSeconds.Equals(candidate.IntervalSeconds) ||
            !source.IntervalVariationSeconds.Equals(candidate.IntervalVariationSeconds) ||
            !source.RelativeVolume.Equals(candidate.RelativeVolume))
            zone.Add("Playlist, ordre, intervalle, transition ou volume relatif modifié.");
        var ambience = source.AdaptiveLayers.SequenceEqual(candidate.AdaptiveLayers)
            ? new List<string>()
            : [$"Couches adaptatives : {source.AdaptiveLayers.Count} → {candidate.AdaptiveLayers.Count}."];
        var music = SameMusicStatesUx2EnvE(source.Orchestration.MusicStates,
            candidate.Orchestration.MusicStates)
            ? new List<string>()
            : [$"États musicaux : {source.Orchestration.MusicStates.Count} → " +
               $"{candidate.Orchestration.MusicStates.Count}."];
        var buses = source.Orchestration.MixBuses.SequenceEqual(candidate.Orchestration.MixBuses)
            ? new List<string>()
            : [$"Bus de mixage : {source.Orchestration.MixBuses.Count} → " +
               $"{candidate.Orchestration.MixBuses.Count}."];
        return new(zone, ambience, music, buses);
    }

    private static StudioUx2EnvESnapshotDiff BuildSnapshotDiffUx2EnvE(
        AreaAuthoringProfile source,
        AreaAuthoringProfile candidate)
    {
        if (SameSnapshotsUx2EnvE(source.Experience.AudioSnapshots, candidate.Experience.AudioSnapshots))
            return new([]);
        var before = source.Experience.AudioSnapshots.ToDictionary(item => item.Id);
        var after = candidate.Experience.AudioSnapshots.ToDictionary(item => item.Id);
        var entries = new List<string>();
        foreach (var id in before.Keys.Union(after.Keys).Order())
        {
            if (!before.TryGetValue(id, out var oldValue))
                entries.Add($"Snapshot « {HumanTokenUx2EnvE(after[id].Name)} » ajouté.");
            else if (!after.TryGetValue(id, out var newValue))
                entries.Add($"Snapshot « {HumanTokenUx2EnvE(oldValue.Name)} » retiré.");
            else if (!SameSnapshotUx2EnvE(oldValue, newValue))
                entries.Add($"Snapshot « {HumanTokenUx2EnvE(newValue.Name)} » modifié.");
        }
        return new(entries);
    }

    private static StudioUx2EnvEAudioDiff EmptyAudioDiffUx2EnvE() => new([], [], [], []);

    private static StudioUx2EnvEAudioPreview FailedAudioPreviewUx2EnvE(string message) => new(
        false, message, string.Empty, false, EmptyAudioDiffUx2EnvE(), GroupAudioDiagnosticsUx2EnvE([]),
        false, false, false, false, false, false, true, true);

    private static StudioUx2EnvESnapshotPreview FailedSnapshotPreviewUx2EnvE(string message) => new(
        false, message, string.Empty, false, new([]), GroupAudioDiagnosticsUx2EnvE([]), false, false, true);

    private static IReadOnlyList<StudioUx2EnvEDiagnosticGroup> GroupAudioDiagnosticsUx2EnvE(
        IReadOnlyList<StudioUx2EnvEDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            ("audio", "Audio de zone"),
            ("ambience", "Couches adaptatives"),
            ("music", "Musique et transitions"),
            ("buses", "Bus et mixage"),
            ("snapshots", "Snapshots séparés"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item => AudioDiagnosticGroupUx2EnvE(item) == definition.Item1).ToArray();
            return new StudioUx2EnvEDiagnosticGroup(definition.Item1, definition.Item2,
                items.Any(item => item.Blocking) ? "à corriger" :
                items.Length > 0 ? "avertissement" : "conforme", items);
        }).ToArray();
    }

    private static string AudioDiagnosticGroupUx2EnvE(StudioUx2EnvEDiagnostic diagnostic)
    {
        if (diagnostic.Code == Ux2EnvESnapshotCode || diagnostic.Path.Contains("audioSnapshots",
                StringComparison.OrdinalIgnoreCase)) return "snapshots";
        if (diagnostic.Code == Ux2EnvELayerCode || diagnostic.Path.Contains("adaptiveLayers",
                StringComparison.OrdinalIgnoreCase)) return "ambience";
        if (diagnostic.Code == Ux2EnvEMusicCode || diagnostic.Path.Contains("musicStates",
                StringComparison.OrdinalIgnoreCase)) return "music";
        if (diagnostic.Code == Ux2EnvEBusCode || diagnostic.Path.Contains("mixBuses",
                StringComparison.OrdinalIgnoreCase)) return "buses";
        return "audio";
    }

    private static StudioUx2EnvEDiagnostic CommandDiagnosticUx2EnvE(
        CommandDiagnostic diagnostic,
        string fallbackPath)
    {
        var separator = diagnostic.Message.IndexOf(" · ", StringComparison.Ordinal);
        var path = separator > 0 ? diagnostic.Message[..separator] : fallbackPath;
        return new(diagnostic.Code, path, HumanizeAudioDiagnosticUx2EnvE(diagnostic.Code));
    }

    private static string HumanizeAudioDiagnosticUx2EnvE(string? code) => code switch
    {
        Ux2EnvEAudioCode => "Le profil audio de zone ou une ressource gouvernée n’est pas valide.",
        Ux2EnvELayerCode => "Une couche d’ambiance adaptative n’est pas valide.",
        Ux2EnvEMusicCode => "Un état musical, une piste ou sa synchronisation n’est pas valide.",
        Ux2EnvEBusCode => "La hiérarchie ou le profil d’un bus de mixage n’est pas valide.",
        Ux2EnvESnapshotCode or AreaExperienceAuthoringValidator.AudioCode =>
            "Un snapshot de mixage ou sa référence de bus n’est pas valide.",
        AudioAuthoringProfileValidator.SelectionReferenceMissingCode =>
            "Une ressource audio n’a pas de provenance gouvernée dans ce module.",
        AudioAuthoringProfileValidator.IdentityInvalidCode =>
            "Le profil audio doit conserver son identité, sa provenance, sa licence et son empreinte.",
        "command.revision_conflict" => "Le projet a changé : rechargez cette étape et relancez son aperçu.",
        _ => "La validation canonique du projet a refusé cette étape audio.",
    };

    private static AudioListenerContext BuildAudioContextUx2EnvE(AreaAudioProfile profile, bool previous)
    {
        var layer = previous ? profile.AdaptiveLayers.FirstOrDefault() : profile.AdaptiveLayers.LastOrDefault();
        var tags = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(layer?.LocationTag)) tags.Add(layer.LocationTag);
        if (!string.IsNullOrWhiteSpace(layer?.VolumeId)) tags.Add(layer.VolumeId);
        return new AudioListenerContext(
            0,
            previous ? 12 : 22,
            previous ? string.Empty : layer?.Weather ?? string.Empty,
            tags,
            previous ? 0 : Math.Max(.5f, layer?.MinimumDanger ?? 0),
            layer?.IndoorOnly == true,
            SourceLoudnessLufs: -24);
    }

    private static string HumanMusicStateUx2EnvE(MusicState state) => state switch
    {
        MusicState.Exploration => "exploration",
        MusicState.Tension => "tension",
        MusicState.Combat => "combat",
        MusicState.Boss => "affrontement majeur",
        MusicState.Victory => "victoire",
        _ => "état musical",
    };

    private static string AudioSourceFingerprintUx2EnvE(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string AudioDraftFingerprintUx2EnvE(StudioUx2EnvEAudioDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString()); writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint ?? string.Empty);
            writer.Write(draft.DayMusic ?? string.Empty); writer.Write(draft.NightMusic ?? string.Empty);
            writer.Write(draft.CombatMusic ?? string.Empty); writer.Write(draft.Ambience ?? string.Empty);
            writer.Write(draft.TransitionSeconds); writer.Write((int)draft.OrderMode);
            writer.Write(draft.IntervalSeconds); writer.Write(draft.IntervalVariationSeconds);
            writer.Write(draft.RelativeVolume); writer.Write(draft.Playlist.Count);
            foreach (var resource in draft.Playlist) writer.Write(resource ?? string.Empty);
            writer.Write(draft.AdaptiveLayers.Count);
            foreach (var item in draft.AdaptiveLayers)
            {
                writer.Write(item.Resource ?? string.Empty); writer.Write(item.StartHour); writer.Write(item.EndHour);
                writer.Write(item.Weather ?? string.Empty); writer.Write(item.LocationTag ?? string.Empty);
                writer.Write(item.MinimumDanger); writer.Write(item.IndoorOnly); writer.Write(item.Volume);
                writer.Write(item.CrossfadeSeconds); writer.Write(item.VolumeId ?? string.Empty);
            }
            writer.Write(draft.MusicStates.Count);
            foreach (var item in draft.MusicStates)
            {
                writer.Write((int)item.State); writer.Write(item.Track ?? string.Empty);
                writer.Write(item.Stems.Count);
                foreach (var stem in item.Stems) writer.Write(stem ?? string.Empty);
                writer.Write(item.TransitionSeconds); writer.Write(item.CooldownSeconds);
                writer.Write(item.BeatSynchronized); writer.Write(item.TempoBpm); writer.Write(item.BeatsPerBar);
            }
            writer.Write(draft.MixBuses.Count);
            foreach (var item in draft.MixBuses)
            {
                writer.Write(item.Id.ToByteArray()); writer.Write(item.Name ?? string.Empty);
                writer.Write(item.ParentId is not null);
                if (item.ParentId is { } parentId) writer.Write(parentId.ToByteArray());
                writer.Write(item.Volume); writer.Write(item.Snapshot ?? string.Empty);
                writer.Write(item.Platform ?? string.Empty); writer.Write(item.DynamicRange);
                writer.Write(item.EnhanceImportantSounds); writer.Write(item.MonitorEnabled);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string SnapshotsDraftFingerprintUx2EnvE(StudioUx2EnvESnapshotsDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString()); writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint ?? string.Empty); writer.Write(draft.AvailableBusNames.Count);
            foreach (var bus in draft.AvailableBusNames) writer.Write(bus ?? string.Empty);
            writer.Write(draft.Snapshots.Count);
            foreach (var item in draft.Snapshots)
            {
                writer.Write(item.Id.ToByteArray()); writer.Write(item.Name ?? string.Empty);
                writer.Write(item.TransitionSeconds); writer.Write(item.TriggerEvent ?? string.Empty);
                writer.Write(item.Provenance ?? string.Empty); writer.Write(item.BusVolumes.Count);
                foreach (var level in item.BusVolumes)
                {
                    writer.Write(level.BusName ?? string.Empty); writer.Write(level.Volume);
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool SameAudioUx2EnvE(AreaAudioProfile source, AreaAudioProfile candidate) =>
        string.Equals(source.DayMusic, candidate.DayMusic, StringComparison.Ordinal) &&
        string.Equals(source.NightMusic, candidate.NightMusic, StringComparison.Ordinal) &&
        string.Equals(source.CombatMusic, candidate.CombatMusic, StringComparison.Ordinal) &&
        string.Equals(source.Ambience, candidate.Ambience, StringComparison.Ordinal) &&
        string.Equals(source.ReverbPreset, candidate.ReverbPreset, StringComparison.Ordinal) &&
        source.TransitionSeconds.Equals(candidate.TransitionSeconds) &&
        source.Playlist.SequenceEqual(candidate.Playlist, StringComparer.Ordinal) &&
        source.OrderMode == candidate.OrderMode && source.IntervalSeconds.Equals(candidate.IntervalSeconds) &&
        source.IntervalVariationSeconds.Equals(candidate.IntervalVariationSeconds) &&
        source.RelativeVolume.Equals(candidate.RelativeVolume) && source.Spatial == candidate.Spatial &&
        source.RuntimeControl == candidate.RuntimeControl && source.AdaptiveLayers.SequenceEqual(candidate.AdaptiveLayers) &&
        SameMusicStatesUx2EnvE(source.Orchestration.MusicStates, candidate.Orchestration.MusicStates) &&
        source.Orchestration.MixBuses.SequenceEqual(candidate.Orchestration.MixBuses) &&
        ReferenceEquals(source.Orchestration.Captions, candidate.Orchestration.Captions) &&
        ReferenceEquals(source.Emitters, candidate.Emitters) && ReferenceEquals(source.Authoring, candidate.Authoring);

    private static bool SameMusicStatesUx2EnvE(
        IReadOnlyList<MusicStateEntry> source,
        IReadOnlyList<MusicStateEntry> candidate) =>
        source.Count == candidate.Count && source.Zip(candidate).All(pair =>
            pair.First.State == pair.Second.State &&
            string.Equals(pair.First.Track, pair.Second.Track, StringComparison.Ordinal) &&
            pair.First.Stems.SequenceEqual(pair.Second.Stems, StringComparer.Ordinal) &&
            pair.First.TransitionSeconds.Equals(pair.Second.TransitionSeconds) &&
            pair.First.CooldownSeconds.Equals(pair.Second.CooldownSeconds) &&
            pair.First.BeatSynchronized == pair.Second.BeatSynchronized &&
            pair.First.TempoBpm.Equals(pair.Second.TempoBpm) &&
            pair.First.BeatsPerBar == pair.Second.BeatsPerBar);

    private static bool SameSnapshotUx2EnvE(AreaAudioSnapshotProfile source, AreaAudioSnapshotProfile candidate) =>
        source.Id == candidate.Id && string.Equals(source.Name, candidate.Name, StringComparison.Ordinal) &&
        source.TransitionSeconds.Equals(candidate.TransitionSeconds) &&
        string.Equals(source.TriggerEvent, candidate.TriggerEvent, StringComparison.Ordinal) &&
        string.Equals(source.Provenance, candidate.Provenance, StringComparison.Ordinal) &&
        source.BusVolumes.Count == candidate.BusVolumes.Count && source.BusVolumes.All(item =>
            candidate.BusVolumes.TryGetValue(item.Key, out var value) && value.Equals(item.Value));

    private static bool SameSnapshotsUx2EnvE(
        IReadOnlyList<AreaAudioSnapshotProfile> source,
        IReadOnlyList<AreaAudioSnapshotProfile> candidate)
    {
        if (source.Count != candidate.Count) return false;
        var byId = candidate.ToDictionary(item => item.Id);
        return source.All(item => byId.TryGetValue(item.Id, out var other) && SameSnapshotUx2EnvE(item, other));
    }

    private static bool FiniteFloatUx2EnvE(double value) =>
        double.IsFinite(value) && value >= -float.MaxValue && value <= float.MaxValue;

    private static bool ValidExactIntUx2EnvE(double value, int minimum, int maximum) =>
        double.IsFinite(value) && value == Math.Truncate(value) && value >= minimum && value <= maximum;

    private static float ExactFloatUx2EnvE(double value)
    {
        if (!FiniteFloatUx2EnvE(value)) throw new OverflowException("The value is not a finite single.");
        return (float)value;
    }

    private static int ExactIntUx2EnvE(double value)
    {
        if (!ValidExactIntUx2EnvE(value, int.MinValue, int.MaxValue))
            throw new OverflowException("The value is not an exact 32-bit integer.");
        return checked((int)value);
    }

    private static string NormalizeAudioResourceUx2EnvE(string? value) =>
        (value ?? string.Empty).Trim().Replace('\\', '/');

    private static bool ValidGovernedAudioResourceUx2EnvE(
        string? value,
        IReadOnlySet<string> governed,
        bool allowEmpty)
    {
        var normalized = NormalizeAudioResourceUx2EnvE(value);
        if (normalized.Length == 0) return allowEmpty;
        return normalized.StartsWith("builtin://", StringComparison.Ordinal) || governed.Contains(normalized);
    }

    private static bool MixBusCycleUx2EnvE(IReadOnlyList<StudioUx2EnvEMixBusDraft> buses)
    {
        var parents = buses.ToDictionary(item => item.Id, item => item.ParentId);
        foreach (var bus in buses)
        {
            var visited = new HashSet<Guid>();
            var current = bus.Id;
            while (parents.TryGetValue(current, out var parent) && parent is { } parentId)
            {
                if (!visited.Add(parentId)) return true;
                current = parentId;
            }
        }
        return false;
    }

    private static string HumanTokenUx2EnvE(string? value)
    {
        var normalized = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(normalized) || ContainsTechnicalIdentifierUx2EnvE(normalized)
            ? "profil sans nom lisible"
            : normalized;
    }

    private static bool ContainsTechnicalIdentifierUx2EnvE(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        text.Contains(".json", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static ProjectDefinition ReplaceAreaUx2EnvE(
        ProjectDefinition project,
        AreaId areaId,
        AreaDefinition replacement) => new(
        project.Id, project.Title, project.Module, project.Blueprints,
        project.Areas.Select(area => area.Id == areaId ? replacement : area).ToArray(),
        project.Dialogues, project.Quests);

    internal static ProjectDefinition CreateUx2EnvEContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var area = source.Areas.FirstOrDefault() ??
                   throw new InvalidOperationException("The contract project requires an area.");
        var masterId = AreaExperienceAuthoringProfile.CreateStableId(area.Id, "ux2-env-e-master");
        var ambienceId = AreaExperienceAuthoringProfile.CreateStableId(area.Id, "ux2-env-e-ambience");
        var audio = area.Environment.Audio;
        var orchestration = new AreaAudioOrchestration(
            [
                new(MusicState.Exploration, "builtin://openrpg/music/exploration", [], 2, 0, true, 120, 4),
                new(MusicState.Combat, "builtin://openrpg/music/combat", ["builtin://openrpg/music/combat-drums"],
                    1.5f, 4, true, 120, 4),
            ],
            [
                new(masterId, "Master", null, 1, "default", "all", .85f, true, true),
                new(ambienceId, "Ambience", masterId, .8f, "forest", "all", .7f, true, true),
            ],
            audio.Orchestration.Captions);
        var configuredAudio = new AreaAudioProfile(
            "builtin://openrpg/music/day", "builtin://openrpg/music/night",
            "builtin://openrpg/music/combat", "builtin://openrpg/ambience/forest",
            audio.ReverbPreset, 2.5f,
            ["builtin://openrpg/music/day", "builtin://openrpg/music/night"],
            AudioOrderMode.DeterministicRandom, 48, 12, .9f, audio.Spatial, audio.RuntimeControl,
            [
                new("builtin://openrpg/ambience/day", 6, 18, "", "forest", 0, false, .7f, 2),
                new("builtin://openrpg/ambience/night", 18, 6, "rain", "forest", .25f, false, .9f, 3,
                    "forest-volume"),
            ], orchestration, audio.Emitters, audio.Authoring);
        var environment = new AreaEnvironmentProfile(
            area.Environment.SchemaVersion, area.Environment.Classification, area.Environment.Tags,
            area.Environment.Lighting, area.Environment.Fog, configuredAudio, area.Environment.Rendering,
            area.Environment.Sky, area.Environment.Wind, area.Environment.Weather, area.Environment.Water,
            area.Environment.Reverb, area.Environment.Authoring,
            area.Environment.RequireAuthoringRuntimeMetadata);
        var experience = area.Authoring.Experience;
        var snapshots = new[]
        {
            new AreaAudioSnapshotProfile(
                AreaExperienceAuthoringProfile.CreateStableId(area.Id, "ux2-env-e-snapshot-exploration"),
                "Exploration forestière",
                new Dictionary<string, float>(StringComparer.Ordinal) { ["Master"] = 1, ["Ambience"] = .85f },
                1.5f, "music.exploration", "studio:ux2-env-e"),
            new AreaAudioSnapshotProfile(
                AreaExperienceAuthoringProfile.CreateStableId(area.Id, "ux2-env-e-snapshot-combat"),
                "Combat intense",
                new Dictionary<string, float>(StringComparer.Ordinal) { ["Master"] = 1, ["Ambience"] = .45f },
                .75f, "music.combat", "studio:ux2-env-e"),
        };
        var configuredExperience = new AreaExperienceAuthoringProfile(
            experience.Id, experience.SchemaVersion, experience.Provenance, snapshots,
            experience.Visibility, experience.Lod, experience.RequireRuntimeMetadata);
        var authoring = new AreaAuthoringProfile(
            area.Authoring.SchemaVersion, area.Authoring.UserEvents, area.Authoring.Map,
            area.Authoring.Layers, configuredExperience, area.Authoring.RequireLayerRuntimeMetadata);
        var updated = area.WithEnvironment(environment).WithAuthoring(authoring);
        return ReplaceAreaUx2EnvE(source, area.Id, updated);
    }

    internal StudioUx2EnvESmokeResult RunUx2EnvEContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0) return FailedUx2EnvESmoke();
        var sourceAreaForEmpty = sourceProject.Areas[0];
        var sourceAudioForEmpty = sourceAreaForEmpty.Environment.Audio;
        var emptyAudio = new AreaAudioProfile(
            string.Empty, string.Empty, string.Empty, string.Empty, sourceAudioForEmpty.ReverbPreset,
            sourceAudioForEmpty.TransitionSeconds, [], AudioOrderMode.Sequential, 0, 0,
            sourceAudioForEmpty.RelativeVolume, sourceAudioForEmpty.Spatial, sourceAudioForEmpty.RuntimeControl,
            [], new AreaAudioOrchestration([], [], sourceAudioForEmpty.Orchestration.Captions),
            sourceAudioForEmpty.Emitters, sourceAudioForEmpty.Authoring);
        var sourceEnvironmentForEmpty = sourceAreaForEmpty.Environment;
        var emptyEnvironment = new AreaEnvironmentProfile(
            sourceEnvironmentForEmpty.SchemaVersion, sourceEnvironmentForEmpty.Classification,
            sourceEnvironmentForEmpty.Tags, sourceEnvironmentForEmpty.Lighting, sourceEnvironmentForEmpty.Fog,
            emptyAudio, sourceEnvironmentForEmpty.Rendering, sourceEnvironmentForEmpty.Sky,
            sourceEnvironmentForEmpty.Wind, sourceEnvironmentForEmpty.Weather, sourceEnvironmentForEmpty.Water,
            sourceEnvironmentForEmpty.Reverb, sourceEnvironmentForEmpty.Authoring,
            sourceEnvironmentForEmpty.RequireAuthoringRuntimeMetadata);
        var sourceAuthoringForEmpty = sourceAreaForEmpty.Authoring;
        var sourceExperienceForEmpty = sourceAuthoringForEmpty.Experience;
        var emptyExperience = new AreaExperienceAuthoringProfile(
            sourceExperienceForEmpty.Id, sourceExperienceForEmpty.SchemaVersion,
            sourceExperienceForEmpty.Provenance, [], sourceExperienceForEmpty.Visibility,
            sourceExperienceForEmpty.Lod, sourceExperienceForEmpty.RequireRuntimeMetadata);
        var emptyAuthoring = new AreaAuthoringProfile(
            sourceAuthoringForEmpty.SchemaVersion, sourceAuthoringForEmpty.UserEvents, sourceAuthoringForEmpty.Map,
            sourceAuthoringForEmpty.Layers, emptyExperience, sourceAuthoringForEmpty.RequireLayerRuntimeMetadata);
        var emptyArea = sourceAreaForEmpty.WithEnvironment(emptyEnvironment).WithAuthoring(emptyAuthoring);
        var emptyAudioDraft = CreateAudioDraftUx2EnvE(new ProjectRevision(0), emptyArea);
        var emptySnapshotsDraft = CreateSnapshotsDraftUx2EnvE(new ProjectRevision(0), emptyArea);
        var emptySourceSupported = emptyAudioDraft.Playlist.Count == 0 && emptyAudioDraft.AdaptiveLayers.Count == 0 &&
                                   emptyAudioDraft.MusicStates.Count == 0 && emptyAudioDraft.MixBuses.Count == 0 &&
                                   emptySnapshotsDraft.Snapshots.Count == 0;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-e-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, CreateUx2EnvEContractProject(sourceProject));
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded) return FailedUx2EnvESmoke();
            var target = editing.Project!.Areas[0];
            var explicitSelection = editing.CreateAudioDraftUx2EnvE() is null &&
                                    editing.CreateSnapshotsDraftUx2EnvE() is null &&
                                    editing.CreateAudioDraftUx2EnvE(target.Id) is not null &&
                                    editing.CreateSnapshotsDraftUx2EnvE(target.Id) is not null;

            var baseAudio = editing.CreateAudioDraftUx2EnvE(target.Id)!;
            var invalidAudio = editing.PreviewAudioUx2EnvE(baseAudio with { DayMusic = "external/ungoverned.ogg" });
            var invalidLayer = editing.PreviewAudioUx2EnvE(baseAudio with
            {
                AdaptiveLayers = baseAudio.AdaptiveLayers.Select((item, index) => index == 0
                    ? item with { EndHour = 24 }
                    : item).ToArray(),
            });
            var invalidMusic = editing.PreviewAudioUx2EnvE(baseAudio with
            {
                MusicStates = baseAudio.MusicStates.Select((item, index) => index == 0
                    ? item with { TempoBpm = 0 }
                    : item).ToArray(),
            });
            var invalidBus = editing.PreviewAudioUx2EnvE(baseAudio with
            {
                MixBuses = baseAudio.MixBuses.Select((item, index) => index == 0
                    ? item with { ParentId = item.Id }
                    : item).ToArray(),
            });
            var baseSnapshots = editing.CreateSnapshotsDraftUx2EnvE(target.Id)!;
            var invalidSnapshot = editing.PreviewSnapshotsUx2EnvE(baseSnapshots with
            {
                Snapshots = baseSnapshots.Snapshots.Select((item, index) => index == 0
                    ? item with
                    {
                        BusVolumes = [new StudioUx2EnvESnapshotBusDraft("Bus absent", 1)],
                    }
                    : item).ToArray(),
            });

            var history = editing.History!;
            var projectBeforeDryRuns = editing.Project;
            var revisionBeforeDryRuns = history.State.Revision;
            var journalBeforeDryRuns = history.Journal.ToArray();
            var dirtyBeforeDryRuns = editing.IsDirty;
            var undoBeforeDryRuns = history.CanUndo;
            var redoBeforeDryRuns = history.CanRedo;
            var dryAudioDraft = editing.CreateAudioDraftUx2EnvE(target.Id)!;
            dryAudioDraft = dryAudioDraft with { RelativeVolume = dryAudioDraft.RelativeVolume + .0625 };
            var audioPreview = editing.PreviewAudioUx2EnvE(dryAudioDraft);
            var drySnapshotsDraft = editing.CreateSnapshotsDraftUx2EnvE(target.Id)!;
            drySnapshotsDraft = drySnapshotsDraft with
            {
                Snapshots = drySnapshotsDraft.Snapshots.Select((item, index) => index == 0
                    ? item with { TransitionSeconds = item.TransitionSeconds + .125 }
                    : item).ToArray(),
            };
            var snapshotPreview = editing.PreviewSnapshotsUx2EnvE(drySnapshotsDraft);
            var dryStatePreserved = ReferenceEquals(projectBeforeDryRuns, editing.Project) &&
                                    history.State.Revision == revisionBeforeDryRuns &&
                                    history.Journal.SequenceEqual(journalBeforeDryRuns) &&
                                    editing.IsDirty == dirtyBeforeDryRuns &&
                                    history.CanUndo == undoBeforeDryRuns && history.CanRedo == redoBeforeDryRuns;
            var audioDryRun = audioPreview.Succeeded && audioPreview.CanApply && audioPreview.CanonicalDryRun &&
                              dryStatePreserved;
            var snapshotDryRun = snapshotPreview.Succeeded && snapshotPreview.CanApply &&
                                 snapshotPreview.CanonicalDryRun && snapshotPreview.SeparateTransactionDeclared &&
                                 dryStatePreserved;
            var deterministicPlans = audioPreview.AdaptiveCrossfadePlanBuilt &&
                                     audioPreview.MusicTransitionPlanBuilt && audioPreview.MixPlanBuilt &&
                                     audioPreview.ListenerMixPlanBuilt && audioPreview.PlaylistPlanBuilt;
            var noLivePlaybackClaim = audioPreview.PlayerInstantiationSkipped && audioPreview.RuntimeClaimsBounded &&
                                      audioPreview.Summary.Contains("Aucun AudioStreamPlayer", StringComparison.Ordinal) &&
                                      audioPreview.Summary.Contains("pas des mesures moteur", StringComparison.Ordinal);

            var sourceGuardAudio = editing.PreviewAudioUx2EnvE(editing.CreateAudioDraftUx2EnvE(target.Id)! with
            {
                SourceFingerprint = new string('0', 64),
            });
            var staleAudioDraft = editing.CreateAudioDraftUx2EnvE(target.Id)!;
            var staleAudioMutation = ApplyUx2EnvERevisionMutation(editing, target.Id, "audio-draft");
            var staleAudioPreview = editing.PreviewAudioUx2EnvE(staleAudioDraft);
            var staleAudioPreviewBlocked = staleAudioMutation && !staleAudioPreview.Succeeded &&
                                           editing.Undo().Succeeded;
            var staleAudioApprovalDraft = editing.CreateAudioDraftUx2EnvE(target.Id)!;
            staleAudioApprovalDraft = staleAudioApprovalDraft with
            {
                RelativeVolume = staleAudioApprovalDraft.RelativeVolume + .015625,
            };
            var staleAudioApprovalPreview = editing.PreviewAudioUx2EnvE(staleAudioApprovalDraft);
            var staleAudioApprovalMutation = ApplyUx2EnvERevisionMutation(editing, target.Id, "audio-approval");
            var staleAudioApprovalApply = editing.ApplyApprovedAudioUx2EnvE();
            var staleAudioApprovalBlocked = staleAudioApprovalPreview.Succeeded && staleAudioApprovalMutation &&
                                            !staleAudioApprovalApply.Succeeded && editing.Undo().Succeeded;
            var staleAudioBlocked = !sourceGuardAudio.Succeeded && staleAudioPreviewBlocked &&
                                    staleAudioApprovalBlocked;

            var sourceGuardSnapshots = editing.PreviewSnapshotsUx2EnvE(
                editing.CreateSnapshotsDraftUx2EnvE(target.Id)! with
                {
                    SourceFingerprint = new string('0', 64),
                });
            var staleSnapshotDraft = editing.CreateSnapshotsDraftUx2EnvE(target.Id)!;
            var staleSnapshotMutation = ApplyUx2EnvERevisionMutation(editing, target.Id, "snapshot-draft");
            var staleSnapshotPreview = editing.PreviewSnapshotsUx2EnvE(staleSnapshotDraft);
            var staleSnapshotPreviewBlocked = staleSnapshotMutation && !staleSnapshotPreview.Succeeded &&
                                              editing.Undo().Succeeded;
            var staleSnapshotApprovalDraft = editing.CreateSnapshotsDraftUx2EnvE(target.Id)!;
            staleSnapshotApprovalDraft = staleSnapshotApprovalDraft with
            {
                Snapshots = staleSnapshotApprovalDraft.Snapshots.Select((item, index) => index == 0
                    ? item with { TransitionSeconds = item.TransitionSeconds + .015625 }
                    : item).ToArray(),
            };
            var staleSnapshotApprovalPreview = editing.PreviewSnapshotsUx2EnvE(staleSnapshotApprovalDraft);
            var staleSnapshotApprovalMutation = ApplyUx2EnvERevisionMutation(editing, target.Id, "snapshot-approval");
            var staleSnapshotApprovalApply = editing.ApplyApprovedSnapshotsUx2EnvE();
            var staleSnapshotApprovalBlocked = staleSnapshotApprovalPreview.Succeeded &&
                                               staleSnapshotApprovalMutation &&
                                               !staleSnapshotApprovalApply.Succeeded && editing.Undo().Succeeded;
            var staleSnapshotBlocked = !sourceGuardSnapshots.Succeeded && staleSnapshotPreviewBlocked &&
                                       staleSnapshotApprovalBlocked;

            var mutablePlaylist = editing.CreateAudioDraftUx2EnvE(target.Id)!.Playlist.ToArray();
            var mutableDraft = editing.CreateAudioDraftUx2EnvE(target.Id)! with
            {
                Playlist = mutablePlaylist,
                RelativeVolume = editing.CreateAudioDraftUx2EnvE(target.Id)!.RelativeVolume + .03125,
            };
            var mutablePreview = editing.PreviewAudioUx2EnvE(mutableDraft);
            mutablePlaylist[0] = "builtin://openrpg/music/fingerprint-change";
            var fingerprintApply = editing.ApplyApprovedAudioUx2EnvE();
            var mutableSnapshots = editing.CreateSnapshotsDraftUx2EnvE(target.Id)!.Snapshots.ToArray();
            var mutableSnapshotDraft = editing.CreateSnapshotsDraftUx2EnvE(target.Id)! with
            {
                Snapshots = mutableSnapshots.Select((item, index) => index == 0
                    ? item with { TransitionSeconds = item.TransitionSeconds + .03125 }
                    : item).ToArray(),
            };
            mutableSnapshots = mutableSnapshotDraft.Snapshots.ToArray();
            mutableSnapshotDraft = mutableSnapshotDraft with { Snapshots = mutableSnapshots };
            var mutableSnapshotPreview = editing.PreviewSnapshotsUx2EnvE(mutableSnapshotDraft);
            mutableSnapshots[0] = mutableSnapshots[0] with
            {
                TransitionSeconds = mutableSnapshots[0].TransitionSeconds + .03125,
            };
            var snapshotFingerprintApply = editing.ApplyApprovedSnapshotsUx2EnvE();
            var fingerprintBlocked = mutablePreview.Succeeded && !fingerprintApply.Succeeded &&
                                     mutableSnapshotPreview.Succeeded && !snapshotFingerprintApply.Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var freshAudio = editing.CreateAudioDraftUx2EnvE(target.Id)!;
            freshAudio = freshAudio with { RelativeVolume = freshAudio.RelativeVolume + .125 };
            var firstPreview = editing.PreviewAudioUx2EnvE(freshAudio);
            var firstApply = editing.ApplyApprovedAudioUx2EnvE();
            var afterAudio = editing.Project!;

            var refreshedSnapshots = editing.CreateSnapshotsDraftUx2EnvE(target.Id)!;
            refreshedSnapshots = refreshedSnapshots with
            {
                Snapshots = refreshedSnapshots.Snapshots.Select((item, index) => index == 0
                    ? item with { TransitionSeconds = item.TransitionSeconds + .25 }
                    : item).ToArray(),
            };
            var secondPreview = editing.PreviewSnapshotsUx2EnvE(refreshedSnapshots);
            var secondApply = editing.ApplyApprovedSnapshotsUx2EnvE();
            var finalProject = editing.Project!;
            var finalTarget = finalProject.Areas.Single(area => area.Id == target.Id);
            var afterAudioTarget = afterAudio.Areas.Single(area => area.Id == target.Id);
            var journalTail = editing.History!.Journal.TakeLast(2).ToArray();
            var separateCommands = firstPreview.Succeeded && firstApply.Succeeded && secondPreview.Succeeded &&
                                   secondApply.Succeeded && journalTail.Length == 2 &&
                                   journalTail[0].CommandName == "patch_area_environment" &&
                                   journalTail[1].CommandName == "set_area_authoring" &&
                                   SameSnapshotsUx2EnvE(sourceTarget.Authoring.Experience.AudioSnapshots,
                                       afterAudioTarget.Authoring.Experience.AudioSnapshots) &&
                                   SameAudioUx2EnvE(afterAudioTarget.Environment.Audio,
                                       finalTarget.Environment.Audio) &&
                                   firstApply.Message.Contains("Étape 1/2", StringComparison.Ordinal) &&
                                   secondApply.Message.Contains("Étape 2/2", StringComparison.Ordinal);

            var environmentLotsPreserved =
                sourceTarget.Environment.SchemaVersion == finalTarget.Environment.SchemaVersion &&
                sourceTarget.Environment.Classification == finalTarget.Environment.Classification &&
                sourceTarget.Environment.Tags.SequenceEqual(finalTarget.Environment.Tags) &&
                ReferenceEquals(sourceTarget.Environment.Lighting, finalTarget.Environment.Lighting) &&
                ReferenceEquals(sourceTarget.Environment.Fog, finalTarget.Environment.Fog) &&
                ReferenceEquals(sourceTarget.Environment.Rendering, finalTarget.Environment.Rendering) &&
                ReferenceEquals(sourceTarget.Environment.Sky, finalTarget.Environment.Sky) &&
                ReferenceEquals(sourceTarget.Environment.Wind, finalTarget.Environment.Wind) &&
                ReferenceEquals(sourceTarget.Environment.Weather, finalTarget.Environment.Weather) &&
                ReferenceEquals(sourceTarget.Environment.Water, finalTarget.Environment.Water) &&
                ReferenceEquals(sourceTarget.Environment.Reverb, finalTarget.Environment.Reverb) &&
                ReferenceEquals(sourceTarget.Environment.Authoring, finalTarget.Environment.Authoring) &&
                sourceTarget.Environment.RequireAuthoringRuntimeMetadata ==
                finalTarget.Environment.RequireAuthoringRuntimeMetadata;
            var sourceAudio = sourceTarget.Environment.Audio;
            var finalAudio = finalTarget.Environment.Audio;
            var audioTailsPreserved = string.Equals(sourceAudio.ReverbPreset, finalAudio.ReverbPreset,
                                          StringComparison.Ordinal) &&
                                      ReferenceEquals(sourceAudio.Spatial, finalAudio.Spatial) &&
                                      ReferenceEquals(sourceAudio.RuntimeControl, finalAudio.RuntimeControl) &&
                                      ReferenceEquals(sourceAudio.Emitters, finalAudio.Emitters) &&
                                      ReferenceEquals(sourceAudio.Authoring, finalAudio.Authoring) &&
                                      ReferenceEquals(sourceAudio.Orchestration.Captions,
                                          finalAudio.Orchestration.Captions);
            var sourceAuthoring = sourceTarget.Authoring;
            var finalAuthoring = finalTarget.Authoring;
            var expectedExperience = new AreaExperienceAuthoringProfile(
                sourceAuthoring.Experience.Id, sourceAuthoring.Experience.SchemaVersion,
                sourceAuthoring.Experience.Provenance, finalAuthoring.Experience.AudioSnapshots,
                sourceAuthoring.Experience.Visibility, sourceAuthoring.Experience.Lod,
                sourceAuthoring.Experience.RequireRuntimeMetadata);
            var expectedAuthoring = new AreaAuthoringProfile(
                sourceAuthoring.SchemaVersion, sourceAuthoring.UserEvents, sourceAuthoring.Map,
                sourceAuthoring.Layers, expectedExperience, sourceAuthoring.RequireLayerRuntimeMetadata);
            var authoringTailsPreserved = string.Equals(
                AreaJsonSerializer.Serialize(sourceTarget.WithAuthoring(expectedAuthoring)),
                AreaJsonSerializer.Serialize(sourceTarget.WithAuthoring(finalAuthoring)),
                StringComparison.Ordinal);
            var expectedFinalArea = sourceTarget.WithEnvironment(finalTarget.Environment)
                .WithAuthoring(expectedAuthoring);
            var areaNeighborsPreserved = string.Equals(AreaJsonSerializer.Serialize(expectedFinalArea),
                AreaJsonSerializer.Serialize(finalTarget), StringComparison.Ordinal);
            var projectNeighborsPreserved = ReferenceEquals(sourceState.Module, finalProject.Module) &&
                                            sourceState.Blueprints.SequenceEqual(finalProject.Blueprints) &&
                                            sourceState.Dialogues.SequenceEqual(finalProject.Dialogues) &&
                                            sourceState.Quests.SequenceEqual(finalProject.Quests) &&
                                            sourceState.Areas.Where(area => area.Id != target.Id).All(sourceArea =>
                                                ReferenceEquals(sourceArea, finalProject.Areas.Single(area =>
                                                    area.Id == sourceArea.Id)));

            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undoSnapshots = editing.Undo();
            var snapshotsUndone = undoSnapshots.Succeeded && ReferenceEquals(afterAudio, editing.Project);
            var undoAudio = editing.Undo();
            var audioUndone = undoAudio.Succeeded && ReferenceEquals(sourceState, editing.Project);
            var redoAudio = editing.Redo();
            var audioRedone = redoAudio.Succeeded && ReferenceEquals(afterAudio, editing.Project);
            var redoSnapshots = editing.Redo();
            var snapshotsRedone = redoSnapshots.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var twoStepUndoRedo = snapshotsUndone && audioUndone && audioRedone && snapshotsRedone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, target.Id.ToString());
            var persistence = save.Succeeded && reopen.Succeeded && reopened.Project is not null &&
                              string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopened.Project!),
                                  StringComparison.Ordinal);
            var structuredDiagnostics = invalidAudio.Groups.Count == 5 && invalidLayer.Groups.Count == 5 &&
                                        invalidMusic.Groups.Count == 5 && invalidBus.Groups.Count == 5 &&
                                        invalidSnapshot.Groups.Count == 5 && audioPreview.Groups.Count == 5 &&
                                        snapshotPreview.Groups.Count == 5;
            var humanText = !ContainsTechnicalIdentifierUx2EnvE(
                string.Join(' ', editing.AudioChoicesUx2EnvE.Select(choice => choice.Label)) + " " +
                audioPreview.Message + " " + audioPreview.Summary + " " + snapshotPreview.Message + " " +
                snapshotPreview.Summary + " " +
                string.Join(' ', audioPreview.Diff.Zone.Concat(audioPreview.Diff.Ambience)
                    .Concat(audioPreview.Diff.Music).Concat(audioPreview.Diff.Buses)) + " " +
                string.Join(' ', snapshotPreview.Diff.Entries));
            editing.Close(); reopened.Close();
            cleanup = DeleteUx2EnvETemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && emptySourceSupported && !invalidAudio.Succeeded &&
                            !invalidLayer.Succeeded && !invalidMusic.Succeeded && !invalidBus.Succeeded &&
                            !invalidSnapshot.Succeeded && audioDryRun && snapshotDryRun && deterministicPlans &&
                            noLivePlaybackClaim && staleAudioBlocked && staleSnapshotBlocked && fingerprintBlocked &&
                            separateCommands && twoStepUndoRedo && persistence && environmentLotsPreserved &&
                            audioTailsPreserved && authoringTailsPreserved && areaNeighborsPreserved &&
                            projectNeighborsPreserved && structuredDiagnostics && humanText;
            return new StudioUx2EnvESmokeResult(succeeded, explicitSelection, emptySourceSupported,
                !invalidAudio.Succeeded, !invalidLayer.Succeeded, !invalidMusic.Succeeded, !invalidBus.Succeeded,
                !invalidSnapshot.Succeeded, audioDryRun, snapshotDryRun, deterministicPlans, noLivePlaybackClaim,
                staleAudioBlocked, staleSnapshotBlocked, fingerprintBlocked, separateCommands, twoStepUndoRedo,
                persistence, environmentLotsPreserved, audioTailsPreserved, authoringTailsPreserved,
                areaNeighborsPreserved, projectNeighborsPreserved, structuredDiagnostics, humanText, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_E_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2EnvESmoke();
        }
        finally
        {
            editing.Close(); reopened.Close();
            cleanup = DeleteUx2EnvETemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_E_CLEANUP_FAILED");
        }
    }

    private static bool ApplyUx2EnvERevisionMutation(
        StudioSessionController controller,
        AreaId areaId,
        string marker)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null) return false;
        var source = area.Environment;
        var wind = new WindProfile(source.Wind.SchemaVersion, source.Wind.DirectionX, source.Wind.DirectionZ,
            source.Wind.Speed + .015625f, source.Wind.GustStrength, source.Wind.GustIntervalSeconds,
            source.Wind.InfluencesVegetation, source.Wind.InfluencesParticles,
            source.Wind.AudioCue + $"-{marker}", source.Wind.GameplayForce, source.Wind.LocalZones);
        var candidate = new AreaEnvironmentProfile(
            source.SchemaVersion, source.Classification, source.Tags, source.Lighting, source.Fog, source.Audio,
            source.Rendering, source.Sky, wind, source.Weather, source.Water, source.Reverb, source.Authoring,
            source.RequireAuthoringRuntimeMetadata);
        var result = controller._setAreaEnvironmentUx2EnvE.Handle(history.State,
            new PatchAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvETemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-e-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvESmokeResult FailedUx2EnvESmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false);
}
