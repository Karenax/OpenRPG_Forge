using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvGPlaylistCode = "studio.audio_workbench.playlist_invalid";
    private const string Ux2EnvGNumberCode = "studio.audio_workbench.number_invalid";
    private const string Ux2EnvGCurveCode = "studio.audio_workbench.curve_invalid";
    private const string Ux2EnvGListenerCode = "studio.audio_workbench.listener_invalid";

    private readonly SetAreaEnvironmentHandler _setAreaEnvironmentUx2EnvG = new(new SystemClock());
    private StudioUx2EnvGDraft? _approvedAudioWorkbenchUx2EnvG;
    private ProjectRevision? _approvedAudioWorkbenchRevisionUx2EnvG;
    private string? _approvedAudioWorkbenchFingerprintUx2EnvG;

    internal IReadOnlyList<StudioUx2EnvGAreaChoice> AudioWorkbenchChoicesUx2EnvG
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildAudioWorkbenchChoicesUx2EnvG(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvGAreaChoice> BuildAudioWorkbenchChoicesUx2EnvG(
        ProjectDefinition project)
    {
        var duplicateNames = project.Areas.GroupBy(area => area.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var display = HumanTokenUx2EnvG(area.DisplayName);
                var name = duplicateNames[area.DisplayName] > 1
                    ? $"{display} · {HumanTokenUx2EnvG(area.Slug)}"
                    : display;
                var resources = area.Environment.Audio.Emitters.Definitions
                    .Sum(item => item.ClipSet.Clips.Count);
                return new StudioUx2EnvGAreaChoice(
                    area.Id,
                    $"{name} · {area.Environment.Audio.Playlist.Count} cue(s) · {resources} ressource(s) gouvernée(s)",
                    area.Environment.Audio.Playlist.Count,
                    resources);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvGDraft? CreateAudioWorkbenchDraftUx2EnvG(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1
            ? CreateAudioWorkbenchDraftUx2EnvG(project, history.State.Revision, matches[0])
            : null;
    }

    private static StudioUx2EnvGDraft CreateAudioWorkbenchDraftUx2EnvG(
        ProjectDefinition project,
        ProjectRevision revision,
        AreaDefinition area)
    {
        var audio = area.Environment.Audio;
        var authoring = audio.Authoring;
        var scopes = new List<StudioUx2EnvGScopeChoice>
        {
            new(string.Empty, "Toutes les zones · ambiance globale"),
        };
        var duplicateScopeNames = project.Areas.GroupBy(item => item.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        scopes.AddRange(project.Areas.Select(item =>
        {
            var label = HumanTokenUx2EnvG(item.DisplayName);
            if (duplicateScopeNames[item.DisplayName] > 1)
                label = $"{label} · {HumanTokenUx2EnvG(item.Slug)}";
            return new StudioUx2EnvGScopeChoice(item.Id.ToString(), label);
        }));
        if (!string.IsNullOrWhiteSpace(authoring.Ambient.AreaScope) &&
            scopes.All(item => !item.Value.Equals(authoring.Ambient.AreaScope, StringComparison.OrdinalIgnoreCase)))
            scopes.Add(new(authoring.Ambient.AreaScope, "Portée existante · valeur conservée"));

        var listenerOccurrences = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        var listenerGains = authoring.Ambient.ListenerGains.Select(item =>
        {
            var label = HumanListenerUx2EnvG(item.Key);
            listenerOccurrences.TryGetValue(label, out var occurrence);
            listenerOccurrences[label] = ++occurrence;
            if (occurrence > 1) label = $"{label} · profil {occurrence}";
            return new StudioUx2EnvGListenerGainDraft(item.Key, label, InvariantUx2EnvG(item.Value));
        }).ToArray();

        var playlist = audio.Playlist.Select(resource => new StudioUx2EnvGPlaylistEntry(
            resource,
            HumanResourceUx2EnvG(resource),
            authoring.Selection.Weights.TryGetValue(resource, out var weight)
                ? InvariantUx2EnvG(weight)
                : string.Empty)).ToArray();
        return new StudioUx2EnvGDraft(
            area.Id,
            revision,
            AudioWorkbenchSourceFingerprintUx2EnvG(area),
            area.DisplayName,
            area.Slug,
            playlist,
            audio.OrderMode,
            InvariantUx2EnvG(audio.IntervalSeconds),
            InvariantUx2EnvG(audio.IntervalVariationSeconds),
            InvariantUx2EnvG(audio.RelativeVolume),
            InvariantUx2EnvG(authoring.Selection.DeterministicSeed),
            InvariantUx2EnvG(authoring.Selection.NoRepeatWindow),
            InvariantUx2EnvG(authoring.Modulation.MinimumIntervalSeconds),
            InvariantUx2EnvG(authoring.Modulation.MaximumIntervalSeconds),
            InvariantUx2EnvG(authoring.Modulation.MinimumPitchSemitones),
            InvariantUx2EnvG(authoring.Modulation.MaximumPitchSemitones),
            InvariantUx2EnvG(authoring.Modulation.MinimumVolume),
            InvariantUx2EnvG(authoring.Modulation.MaximumVolume),
            authoring.Modulation.Distribution,
            FormatCurveUx2EnvG(authoring.Modulation.Curve),
            InvariantUx2EnvG(authoring.Modulation.DeterministicSeed),
            authoring.Modulation.SyncToTempo,
            InvariantUx2EnvG(authoring.Modulation.TempoBpm),
            InvariantUx2EnvG(authoring.Modulation.BeatDivision),
            InvariantUx2EnvG(authoring.Mix.TargetLoudnessLufs),
            authoring.Mix.Bus,
            InvariantUx2EnvG(authoring.Mix.DuckingDb),
            InvariantUx2EnvG(authoring.Mix.DistanceScale),
            InvariantUx2EnvG(authoring.Mix.AccessibilityVolumeCap),
            authoring.Mix.MeterMode,
            authoring.Ambient.AreaScope,
            authoring.Ambient.Bus,
            authoring.Ambient.ListenerPolicy,
            listenerGains,
            scopes,
            authoring.RequiredAtRuntime,
            $"Schéma {authoring.SchemaVersion} · licence {HumanTokenUx2EnvG(authoring.LicenseSpdx)} · " +
            $"provenance {HumanTokenUx2EnvG(authoring.Provenance)} · " +
            $"{(authoring.RequiredAtRuntime ? "requis au runtime" : "facultatif au runtime")}");
    }

    internal IReadOnlyList<StudioUx2EnvGResourceResult> BrowseAudioResourcesUx2EnvG(
        AreaId areaId,
        string text,
        string tags,
        string locale,
        string license)
    {
        var area = Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return [];
        var tagSet = ParseTokensUx2EnvG(tags).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            return AudioResourceBrowser.Search(area.Environment.Audio.Emitters,
                    new AudioResourceSearch(text ?? string.Empty, tagSet,
                        EmptyToNullUx2EnvG(locale), EmptyToNullUx2EnvG(license), 200))
                .Select(item => new StudioUx2EnvGResourceResult(
                    item.EmitterId,
                    item.ClipId,
                    HumanResourcePreviewUx2EnvG(item),
                    item.ResourcePath,
                    string.Join(", ", item.Tags.Select(HumanTokenUx2EnvG)),
                    HumanTokenUx2EnvG(item.Locale),
                    HumanFreeTextUx2EnvG(item.Transcript),
                    HumanTokenUx2EnvG(item.LicenseSpdx),
                    HumanTokenUx2EnvG(item.Provenance),
                    $"{item.LoudnessLufs:0.##} LUFS",
                    $"{item.DurationSeconds:0.##} s",
                    RenderWaveformUx2EnvG(item.WaveformPeaks),
                    item.DistributionReady))
                .ToArray();
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    internal StudioUx2EnvGAuditionPlan PlanAudioAuditionUx2EnvG(
        StudioUx2EnvGDraft draft,
        string text,
        string tags,
        string locale,
        string license,
        bool batch)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!TryBuildAudioWorkbenchUx2EnvG(draft, true, out _, out var sourceArea,
                out var candidateEnvironment, out _, out var message))
            return new(false, message, [], string.Empty, "0", batch, true, true);
        var tagSet = ParseTokensUx2EnvG(tags).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resources = AudioResourceBrowser.Search(sourceArea!.Environment.Audio.Emitters,
            new AudioResourceSearch(text ?? string.Empty, tagSet,
                EmptyToNullUx2EnvG(locale), EmptyToNullUx2EnvG(license), 200));
        var plan = AudioResourceBrowser.PlanAudition(resources, candidateEnvironment!.Audio.Authoring.Mix, batch);
        var labels = plan.Resources.Select(HumanResourcePreviewUx2EnvG).ToArray();
        var succeeded = plan.Resources.Count > 0 && plan.Diagnostics.Count == 0;
        return new(succeeded,
            plan.Resources.Count == 0
                ? "Aucune ressource ne correspond aux facettes."
                : plan.Diagnostics.Count > 0
                    ? "Le plan d’écoute est bloqué : la gouvernance d’une ressource est incomplète."
                    : $"Plan d’écoute {(batch ? "par lot" : "unique")} calculé · aucun lecteur Studio créé.",
            labels,
            HumanTokenUx2EnvG(plan.Bus),
            plan.NormalizedGain.ToString("0.###", CultureInfo.CurrentCulture),
            batch,
            true,
            true);
    }

    internal StudioUx2EnvGPreview PreviewAudioWorkbenchUx2EnvG(StudioUx2EnvGDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAudioWorkbenchApprovalUx2EnvG();
        var history = History;
        if (history is null || Project is null)
            return FailedAudioWorkbenchPreviewUx2EnvG("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedAudioWorkbenchPreviewUx2EnvG(
                "Le projet a changé depuis l’ouverture de l’atelier. Rechargez la zone avant l’aperçu.");
        if (!TryBuildAudioWorkbenchUx2EnvG(draft, true, out var command, out var sourceArea,
                out var candidateEnvironment, out var diagnostics, out var message))
            return new(false, message, string.Empty, false, new([]),
                GroupAudioWorkbenchDiagnosticsUx2EnvG(diagnostics), [], [], false, false, true, true, true);

        var dryRun = _setAreaEnvironmentUx2EnvG.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => CommandDiagnosticUx2EnvG(item,
                $"areas/{draft.AreaId}/environment/audio/authoring")).ToArray();
            return new(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé cet aperçu.",
                string.Empty,
                false,
                BuildAudioWorkbenchDiffUx2EnvG(sourceArea!.Environment.Audio, candidateEnvironment!.Audio),
                GroupAudioWorkbenchDiagnosticsUx2EnvG(commandDiagnostics), [], [], false, false, true, true, true);
        }

        var sourceAudio = sourceArea!.Environment.Audio;
        var candidateAudio = candidateEnvironment!.Audio;
        var diff = BuildAudioWorkbenchDiffUx2EnvG(sourceAudio, candidateAudio);
        AudioModulationStatistics statistics;
        IReadOnlyList<PlannedAudioCue> planned;
        try
        {
            statistics = AreaAudioSequencePlanner.PreviewStatistics(candidateAudio, 512,
                candidateAudio.Authoring.Selection.DeterministicSeed);
            planned = AreaAudioSequencePlanner.Plan(candidateAudio,
                Math.Min(8, Math.Max(0, candidateAudio.Playlist.Count * 2)),
                candidateAudio.Authoring.Selection.DeterministicSeed);
        }
        catch (ArgumentException exception)
        {
            return FailedAudioWorkbenchPreviewUx2EnvG($"Le plan déterministe est invalide · {exception.Message}");
        }

        var sequence = planned.Select(item => new StudioUx2EnvGSequencePlan(
            HumanResourceUx2EnvG(item.Resource),
            $"{item.DelaySeconds:0.###} s",
            item.RelativeVolume.ToString("0.###", CultureInfo.CurrentCulture),
            $"{item.PitchSemitones:0.###} demi-ton(s)",
            HumanTokenUx2EnvG(item.Bus))).ToArray();
        var mixes = BuildAudioMixPlansUx2EnvG(candidateAudio, draft.AreaId.ToString());
        if (diff.HasChanges)
        {
            _approvedAudioWorkbenchUx2EnvG = draft;
            _approvedAudioWorkbenchRevisionUx2EnvG = history.State.Revision;
            _approvedAudioWorkbenchFingerprintUx2EnvG = AudioWorkbenchDraftFingerprintUx2EnvG(draft);
        }
        var summary = statistics.SampleCount == 0
            ? "Playlist vide · le moteur ne planifiera aucun cue. Le catalogue reste consultable sans chargement de ressource."
            : $"Simulation déterministe : {statistics.SampleCount} tirages, délai " +
              $"{statistics.MinimumDelaySeconds:0.##}–{statistics.MaximumDelaySeconds:0.##} s " +
              $"(moyenne {statistics.MeanDelaySeconds:0.##}), volume moyen {statistics.MeanRelativeVolume:0.##}. " +
              "Les facettes, waveforms, séquences et mix sont des plans de données; cet atelier Studio " +
              "ne charge aucune ressource et n’instancie aucun AudioStreamPlayer. Le playback réel reste au runtime Godot.";
        return new(true,
            diff.HasChanges
                ? "Aperçu sec validé · les réglages audio ne sont pas encore appliqués."
                : "Aperçu sec validé · aucune différence à appliquer.",
            summary,
            diff.HasChanges,
            diff,
            GroupAudioWorkbenchDiagnosticsUx2EnvG([]),
            sequence,
            mixes,
            true,
            true,
            true,
            true,
            true);
    }

    internal StudioUx2EnvGActionResult ApplyApprovedAudioWorkbenchUx2EnvG()
    {
        var draft = _approvedAudioWorkbenchUx2EnvG;
        var history = History;
        if (draft is null || history is null || _approvedAudioWorkbenchRevisionUx2EnvG != history.State.Revision ||
            !string.Equals(_approvedAudioWorkbenchFingerprintUx2EnvG,
                AudioWorkbenchDraftFingerprintUx2EnvG(draft), StringComparison.Ordinal))
        {
            CancelAudioWorkbenchApprovalUx2EnvG();
            return new(false, "Relancez l’aperçu sec avant d’appliquer l’atelier audio.");
        }
        if (!TryBuildAudioWorkbenchUx2EnvG(draft, false, out var command, out _, out _, out _, out var message))
        {
            CancelAudioWorkbenchApprovalUx2EnvG();
            return new(false, message);
        }
        var dryRun = _setAreaEnvironmentUx2EnvG.Handle(history.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            CancelAudioWorkbenchApprovalUx2EnvG();
            return new(false, HumanizeAudioWorkbenchDiagnosticUx2EnvG(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaEnvironmentUx2EnvG.Handle(history.State, command!);
        CancelAudioWorkbenchApprovalUx2EnvG();
        if (!applied.Succeeded)
            return new(false, HumanizeAudioWorkbenchDiagnosticUx2EnvG(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Atelier audio appliqué par set_area_environment · une commande annulable.");
    }

    internal void CancelAudioWorkbenchApprovalUx2EnvG()
    {
        _approvedAudioWorkbenchUx2EnvG = null;
        _approvedAudioWorkbenchRevisionUx2EnvG = null;
        _approvedAudioWorkbenchFingerprintUx2EnvG = null;
    }

    internal StudioUx2EnvGApprovalSnapshot CaptureAudioWorkbenchApprovalUx2EnvG() => new(
        _approvedAudioWorkbenchUx2EnvG,
        _approvedAudioWorkbenchRevisionUx2EnvG,
        _approvedAudioWorkbenchFingerprintUx2EnvG);

    internal void RestoreAudioWorkbenchApprovalUx2EnvG(StudioUx2EnvGApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAudioWorkbenchUx2EnvG = snapshot.Draft;
        _approvedAudioWorkbenchRevisionUx2EnvG = snapshot.Revision;
        _approvedAudioWorkbenchFingerprintUx2EnvG = snapshot.Fingerprint;
    }

    private bool TryBuildAudioWorkbenchUx2EnvG(
        StudioUx2EnvGDraft draft,
        bool dryRun,
        out SetAreaEnvironmentCommand? command,
        out AreaDefinition? sourceArea,
        out AreaEnvironmentProfile? candidateEnvironment,
        out IReadOnlyList<StudioUx2EnvGDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateEnvironment = null;
        var found = new List<StudioUx2EnvGDiagnostic>();
        diagnostics = found;
        message = "La fiche audio est invalide.";
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé depuis l’ouverture de l’atelier.";
            return false;
        }
        var matches = project.Areas.Where(area => area.Id == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "La zone sélectionnée n’est plus disponible.";
            return false;
        }
        sourceArea = matches[0];
        if (!string.Equals(draft.SourceFingerprint,
                AudioWorkbenchSourceFingerprintUx2EnvG(sourceArea), StringComparison.Ordinal))
        {
            message = "La zone a changé depuis l’ouverture de l’atelier; ses données voisines ne seront pas réinjectées.";
            return false;
        }

        var playlist = draft.Playlist.Select(item => (item.Resource ?? string.Empty).Trim()).ToArray();
        if (playlist.Any(string.IsNullOrWhiteSpace) ||
            playlist.Distinct(StringComparer.Ordinal).Count() != playlist.Length)
            found.Add(new(Ux2EnvGPlaylistCode, "audio/playlist",
                "La playlist exige des ressources non vides et uniques, dans l’ordre affiché."));
        var weights = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var (entry, index) in draft.Playlist.Select((value, index) => (value, index)))
        {
            if (string.IsNullOrWhiteSpace(entry.Weight)) continue;
            if (!TryFloatUx2EnvG(entry.Weight, out var weight) || weight <= 0 ||
                !weights.TryAdd(entry.Resource.Trim(), weight))
                found.Add(new(Ux2EnvGNumberCode, $"audio/selection/weights/{index}",
                    $"Le poids de « {HumanResourceUx2EnvG(entry.Resource)} » doit être un nombre strictement positif."));
        }

        if (!TryFloatUx2EnvG(draft.IntervalSeconds, out var interval) || interval < 0)
            found.Add(NumberDiagnosticUx2EnvG("audio/intervalSeconds", "L’intervalle compatible doit être positif ou nul."));
        if (!TryFloatUx2EnvG(draft.IntervalVariationSeconds, out var variation) || variation < 0)
            found.Add(NumberDiagnosticUx2EnvG("audio/intervalVariationSeconds", "La variation compatible doit être positive ou nulle."));
        if (!TryFloatUx2EnvG(draft.RelativeVolume, out var relativeVolume) || relativeVolume is < 0 or > 4)
            found.Add(NumberDiagnosticUx2EnvG("audio/relativeVolume", "Le volume relatif doit rester entre 0 et 4."));
        if (!TryIntUx2EnvG(draft.SelectionSeed, out var selectionSeed))
            found.Add(NumberDiagnosticUx2EnvG("audio/selection/seed", "La graine de sélection doit être un entier exact."));
        if (!TryIntUx2EnvG(draft.NoRepeatWindow, out var noRepeat) || noRepeat is < 0 or > 1024)
            found.Add(NumberDiagnosticUx2EnvG("audio/selection/noRepeat", "La fenêtre sans répétition doit rester entre 0 et 1024."));
        var minimumIntervalValid = TryFloatUx2EnvG(draft.MinimumIntervalSeconds, out var minimumInterval);
        var maximumIntervalValid = TryFloatUx2EnvG(draft.MaximumIntervalSeconds, out var maximumInterval);
        if (!minimumIntervalValid || !maximumIntervalValid)
            found.Add(NumberDiagnosticUx2EnvG("audio/modulation/interval", "La plage d’intervalle exige deux nombres finis."));
        var minimumPitchValid = TryFloatUx2EnvG(draft.MinimumPitchSemitones, out var minimumPitch);
        var maximumPitchValid = TryFloatUx2EnvG(draft.MaximumPitchSemitones, out var maximumPitch);
        if (!minimumPitchValid || !maximumPitchValid)
            found.Add(NumberDiagnosticUx2EnvG("audio/modulation/pitch", "La plage de hauteur exige deux nombres finis."));
        var minimumVolumeValid = TryFloatUx2EnvG(draft.MinimumVolume, out var minimumVolume);
        var maximumVolumeValid = TryFloatUx2EnvG(draft.MaximumVolume, out var maximumVolume);
        if (!minimumVolumeValid || !maximumVolumeValid)
            found.Add(NumberDiagnosticUx2EnvG("audio/modulation/volume", "La plage de volume exige deux nombres finis."));
        if (!TryIntUx2EnvG(draft.ModulationSeed, out var modulationSeed))
            found.Add(NumberDiagnosticUx2EnvG("audio/modulation/seed", "La graine de modulation doit être un entier exact."));
        if (!TryFloatUx2EnvG(draft.TempoBpm, out var tempo))
            found.Add(NumberDiagnosticUx2EnvG("audio/modulation/tempo", "Le tempo doit être un nombre fini."));
        if (!TryFloatUx2EnvG(draft.BeatDivision, out var beatDivision))
            found.Add(NumberDiagnosticUx2EnvG("audio/modulation/beatDivision", "La division de temps doit être un nombre fini."));
        if (!TryCurveUx2EnvG(draft.Curve, out var curve))
            found.Add(new(Ux2EnvGCurveCode, "audio/modulation/curve",
                "La courbe exige au moins deux points position:valeur, ordonnés de 0 à 1."));
        if (!TryFloatUx2EnvG(draft.TargetLoudnessLufs, out var targetLoudness))
            found.Add(NumberDiagnosticUx2EnvG("audio/mix/targetLoudness", "La cible LUFS doit être un nombre fini."));
        if (!TryFloatUx2EnvG(draft.DuckingDb, out var ducking))
            found.Add(NumberDiagnosticUx2EnvG("audio/mix/ducking", "Le ducking doit être un nombre fini."));
        if (!TryFloatUx2EnvG(draft.DistanceScale, out var distanceScale))
            found.Add(NumberDiagnosticUx2EnvG("audio/mix/distanceScale", "L’échelle de distance doit être un nombre fini."));
        if (!TryFloatUx2EnvG(draft.AccessibilityVolumeCap, out var accessibilityCap))
            found.Add(NumberDiagnosticUx2EnvG("audio/mix/accessibilityCap", "Le plafond d’accessibilité doit être un nombre fini."));
        if (string.IsNullOrWhiteSpace(draft.MixBus) || string.IsNullOrWhiteSpace(draft.AmbientBus))
            found.Add(new(AudioAuthoringProfileValidator.MixInvalidCode, "audio/mix/bus",
                "Les bus de mix et d’ambiance doivent être renseignés."));
        var listenerGains = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var (entry, index) in draft.ListenerGains.Select((value, index) => (value, index)))
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || !TryFloatUx2EnvG(entry.Gain, out var gain) ||
                !listenerGains.TryAdd(entry.Key, gain))
                found.Add(new(Ux2EnvGListenerCode, $"audio/ambient/listeners/{index}",
                    $"Le gain de « {HumanTokenUx2EnvG(entry.Label)} » doit être fini et son profil unique."));
        }
        if (found.Count > 0)
        {
            message = found[0].Message;
            return false;
        }

        try
        {
            var sourceEnvironment = sourceArea.Environment;
            var sourceAudio = sourceEnvironment.Audio;
            var authoring = sourceAudio.Authoring with
            {
                Selection = new AudioPlaybackSelectionProfile(selectionSeed, noRepeat, weights),
                Modulation = new AudioModulationProfile(
                    minimumInterval,
                    maximumInterval,
                    minimumPitch,
                    maximumPitch,
                    minimumVolume,
                    maximumVolume,
                    draft.Distribution,
                    curve,
                    modulationSeed,
                    draft.SyncToTempo,
                    tempo,
                    beatDivision),
                Mix = new AudioLoudnessMixProfile(
                    targetLoudness,
                    draft.MixBus.Trim(),
                    ducking,
                    distanceScale,
                    accessibilityCap,
                    draft.MeterMode),
                Ambient = new AreaAmbientScopeProfile(
                    draft.AreaScope.Trim(),
                    draft.AmbientBus.Trim(),
                    draft.ListenerPolicy,
                    listenerGains),
            };
            var audio = new AreaAudioProfile(
                sourceAudio.DayMusic,
                sourceAudio.NightMusic,
                sourceAudio.CombatMusic,
                sourceAudio.Ambience,
                sourceAudio.ReverbPreset,
                sourceAudio.TransitionSeconds,
                playlist,
                draft.OrderMode,
                interval,
                variation,
                relativeVolume,
                sourceAudio.Spatial,
                sourceAudio.RuntimeControl,
                sourceAudio.AdaptiveLayers,
                sourceAudio.Orchestration,
                sourceAudio.Emitters,
                authoring);
            candidateEnvironment = new AreaEnvironmentProfile(
                sourceEnvironment.SchemaVersion,
                sourceEnvironment.Classification,
                sourceEnvironment.Tags,
                sourceEnvironment.Lighting,
                sourceEnvironment.Fog,
                audio,
                sourceEnvironment.Rendering,
                sourceEnvironment.Sky,
                sourceEnvironment.Wind,
                sourceEnvironment.Weather,
                sourceEnvironment.Water,
                sourceEnvironment.Reverb,
                sourceEnvironment.Authoring,
                sourceEnvironment.RequireAuthoringRuntimeMetadata);
            command = new SetAreaEnvironmentCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                sourceArea.Id,
                candidateEnvironment,
                dryRun);
            message = "Atelier audio prêt pour la commande canonique.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            found.Add(new(Ux2EnvGNumberCode, "audio", $"Le profil audio ne peut pas être reconstruit · {exception.Message}"));
            message = found[0].Message;
            return false;
        }
    }

    private static StudioUx2EnvGDiagnostic NumberDiagnosticUx2EnvG(string path, string message) =>
        new(Ux2EnvGNumberCode, path, message);

    private static IReadOnlyList<StudioUx2EnvGMixPlan> BuildAudioMixPlansUx2EnvG(
        AreaAudioProfile audio,
        string areaId)
    {
        var personalizedKey = audio.Authoring.Ambient.ListenerGains.Keys.FirstOrDefault() ?? "auditeur-local";
        var contexts = new[]
        {
            (Label: "Joueur local", Context: new AudioListenerContext(0, 12, string.Empty,
                new HashSet<string>(StringComparer.Ordinal), 0, false, areaId, personalizedKey,
                true, "groupe-local", "groupe-local", -18)),
            (Label: "Membre du groupe", Context: new AudioListenerContext(0, 12, string.Empty,
                new HashSet<string>(StringComparer.Ordinal), 0, false, areaId, "membre-groupe",
                false, "groupe-local", "groupe-local", -20)),
            (Label: "Auditeur externe", Context: new AudioListenerContext(0, 12, string.Empty,
                new HashSet<string>(StringComparer.Ordinal), 0, false, areaId, "auditeur-externe",
                false, "autre-groupe", "groupe-local", -16)),
        };
        return contexts.Select(item =>
        {
            var mix = AreaAudioMixer.Evaluate(audio, item.Context);
            return new StudioUx2EnvGMixPlan(
                string.IsNullOrWhiteSpace(audio.Authoring.Ambient.AreaScope)
                    ? "Toutes les zones"
                    : "Zone sélectionnée",
                item.Label,
                HumanTokenUx2EnvG(mix.Bus),
                mix.NormalizedGain.ToString("0.###", CultureInfo.CurrentCulture),
                $"{mix.MeterLufs:0.##} LUFS",
                mix.ListenerAllowed,
                mix.Personalized,
                mix.AccessibilityCapped);
        }).ToArray();
    }

    private static StudioUx2EnvGDiff BuildAudioWorkbenchDiffUx2EnvG(
        AreaAudioProfile source,
        AreaAudioProfile candidate)
    {
        var entries = new List<string>();
        if (!source.Playlist.SequenceEqual(candidate.Playlist, StringComparer.Ordinal))
            entries.Add($"Playlist ordonnée : {source.Playlist.Count} → {candidate.Playlist.Count} cue(s).");
        if (source.OrderMode != candidate.OrderMode ||
            !SameSelectionUx2EnvG(source.Authoring.Selection, candidate.Authoring.Selection))
            entries.Add($"Sélection : {HumanOrderUx2EnvG(source.OrderMode)} → {HumanOrderUx2EnvG(candidate.OrderMode)}; " +
                        $"graine {candidate.Authoring.Selection.DeterministicSeed}, " +
                        $"fenêtre {candidate.Authoring.Selection.NoRepeatWindow}.");
        if (!source.IntervalSeconds.Equals(candidate.IntervalSeconds) ||
            !source.IntervalVariationSeconds.Equals(candidate.IntervalVariationSeconds) ||
            !SameModulationUx2EnvG(source.Authoring.Modulation, candidate.Authoring.Modulation))
            entries.Add($"Modulation : {candidate.Authoring.Modulation.MinimumIntervalSeconds:0.###}–" +
                        $"{candidate.Authoring.Modulation.MaximumIntervalSeconds:0.###} s · " +
                        $"{HumanDistributionUx2EnvG(candidate.Authoring.Modulation.Distribution)}.");
        if (!source.RelativeVolume.Equals(candidate.RelativeVolume) || source.Authoring.Mix != candidate.Authoring.Mix)
            entries.Add($"Mix : volume relatif {candidate.RelativeVolume:0.###} · " +
                        $"cible {candidate.Authoring.Mix.TargetLoudnessLufs:0.##} LUFS · " +
                        $"bus {HumanTokenUx2EnvG(candidate.Authoring.Mix.Bus)}.");
        if (!SameAmbientUx2EnvG(source.Authoring.Ambient, candidate.Authoring.Ambient))
            entries.Add($"Auditeurs : {HumanListenerPolicyUx2EnvG(candidate.Authoring.Ambient.ListenerPolicy)} · " +
                        $"{candidate.Authoring.Ambient.ListenerGains.Count} gain(s) personnalisé(s).");
        return new(entries);
    }

    private static bool SameSelectionUx2EnvG(
        AudioPlaybackSelectionProfile source,
        AudioPlaybackSelectionProfile candidate) =>
        source.DeterministicSeed == candidate.DeterministicSeed &&
        source.NoRepeatWindow == candidate.NoRepeatWindow &&
        SameMapUx2EnvG(source.Weights, candidate.Weights);

    private static bool SameModulationUx2EnvG(
        AudioModulationProfile source,
        AudioModulationProfile candidate) =>
        source.MinimumIntervalSeconds.Equals(candidate.MinimumIntervalSeconds) &&
        source.MaximumIntervalSeconds.Equals(candidate.MaximumIntervalSeconds) &&
        source.MinimumPitchSemitones.Equals(candidate.MinimumPitchSemitones) &&
        source.MaximumPitchSemitones.Equals(candidate.MaximumPitchSemitones) &&
        source.MinimumVolume.Equals(candidate.MinimumVolume) &&
        source.MaximumVolume.Equals(candidate.MaximumVolume) &&
        source.Distribution == candidate.Distribution &&
        source.Curve.SequenceEqual(candidate.Curve) &&
        source.DeterministicSeed == candidate.DeterministicSeed &&
        source.SyncToTempo == candidate.SyncToTempo &&
        source.TempoBpm.Equals(candidate.TempoBpm) &&
        source.BeatDivision.Equals(candidate.BeatDivision);

    private static bool SameAmbientUx2EnvG(
        AreaAmbientScopeProfile source,
        AreaAmbientScopeProfile candidate) =>
        string.Equals(source.AreaScope, candidate.AreaScope, StringComparison.Ordinal) &&
        string.Equals(source.Bus, candidate.Bus, StringComparison.Ordinal) &&
        source.ListenerPolicy == candidate.ListenerPolicy &&
        SameMapUx2EnvG(source.ListenerGains, candidate.ListenerGains);

    private static bool SameMapUx2EnvG(
        IReadOnlyDictionary<string, float> source,
        IReadOnlyDictionary<string, float> candidate) =>
        source.Count == candidate.Count && source.All(item =>
            candidate.TryGetValue(item.Key, out var value) && value.Equals(item.Value));

    private static IReadOnlyList<StudioUx2EnvGDiagnosticGroup> GroupAudioWorkbenchDiagnosticsUx2EnvG(
        IReadOnlyList<StudioUx2EnvGDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            (Key: "resources", Title: "Ressources & playlist"),
            (Key: "selection", Title: "Ordre & sélection"),
            (Key: "modulation", Title: "Intervalle & modulation"),
            (Key: "listeners", Title: "Volume & auditeurs"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item =>
                AudioWorkbenchDiagnosticGroupUx2EnvG(item) == definition.Key).ToArray();
            return new StudioUx2EnvGDiagnosticGroup(
                definition.Key,
                definition.Title,
                items.Any(item => item.Blocking) ? "à corriger" :
                items.Length > 0 ? "avertissement" : "conforme",
                items);
        }).ToArray();
    }

    private static string AudioWorkbenchDiagnosticGroupUx2EnvG(StudioUx2EnvGDiagnostic diagnostic)
    {
        if (diagnostic.Code is Ux2EnvGPlaylistCode or AudioAuthoringProfileValidator.SelectionReferenceMissingCode ||
            diagnostic.Path.Contains("playlist", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Path.Contains("resource", StringComparison.OrdinalIgnoreCase)) return "resources";
        if (diagnostic.Code == AudioAuthoringProfileValidator.SelectionInvalidCode ||
            diagnostic.Path.Contains("selection", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Path.Contains("order", StringComparison.OrdinalIgnoreCase)) return "selection";
        if (diagnostic.Code is Ux2EnvGCurveCode or AudioAuthoringProfileValidator.ModulationInvalidCode ||
            diagnostic.Path.Contains("modulation", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Path.Contains("interval", StringComparison.OrdinalIgnoreCase)) return "modulation";
        return "listeners";
    }

    private static StudioUx2EnvGDiagnostic CommandDiagnosticUx2EnvG(
        CommandDiagnostic diagnostic,
        string fallbackPath)
    {
        var message = diagnostic.Message ?? string.Empty;
        var separator = message.IndexOf(" · ", StringComparison.Ordinal);
        var path = separator > 0 ? message[..separator].Trim() : fallbackPath;
        return new(diagnostic.Code, path,
            HumanizeAudioWorkbenchDiagnosticUx2EnvG(diagnostic.Code));
    }

    private static string HumanizeAudioWorkbenchDiagnosticUx2EnvG(string? code) => code switch
    {
        AudioAuthoringProfileValidator.SchemaUnsupportedCode => "Le schéma audio n’est pas pris en charge.",
        AudioAuthoringProfileValidator.IdentityInvalidCode => "La gouvernance audio est incomplète.",
        AudioAuthoringProfileValidator.SelectionInvalidCode => "La politique de sélection est invalide.",
        AudioAuthoringProfileValidator.SelectionReferenceMissingCode =>
            "Une ressource pondérée ou distribuée n’appartient pas à la playlist gouvernée.",
        AudioAuthoringProfileValidator.ModulationInvalidCode => "La plage ou la courbe de modulation est invalide.",
        AudioAuthoringProfileValidator.MixInvalidCode => "Le volume, le bus ou le plafond de mix est invalide.",
        AudioAuthoringProfileValidator.AmbientInvalidCode => "La portée ou les gains d’auditeur sont invalides.",
        "command.revision_conflict" => "Le projet a changé; relancez l’aperçu.",
        _ => "La commande canonique a refusé le profil audio.",
    };

    private static StudioUx2EnvGPreview FailedAudioWorkbenchPreviewUx2EnvG(string message) => new(
        false,
        message,
        string.Empty,
        false,
        new([]),
        GroupAudioWorkbenchDiagnosticsUx2EnvG([]),
        [],
        [],
        false,
        false,
        true,
        true,
        true);

    private static string AudioWorkbenchSourceFingerprintUx2EnvG(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string AudioWorkbenchDraftFingerprintUx2EnvG(StudioUx2EnvGDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString());
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint ?? string.Empty);
            writer.Write(draft.Playlist.Count);
            foreach (var item in draft.Playlist)
            {
                writer.Write(item.Resource ?? string.Empty);
                writer.Write(item.Label ?? string.Empty);
                writer.Write(item.Weight ?? string.Empty);
            }
            writer.Write((int)draft.OrderMode);
            writer.Write(draft.IntervalSeconds ?? string.Empty);
            writer.Write(draft.IntervalVariationSeconds ?? string.Empty);
            writer.Write(draft.RelativeVolume ?? string.Empty);
            writer.Write(draft.SelectionSeed ?? string.Empty);
            writer.Write(draft.NoRepeatWindow ?? string.Empty);
            writer.Write(draft.MinimumIntervalSeconds ?? string.Empty);
            writer.Write(draft.MaximumIntervalSeconds ?? string.Empty);
            writer.Write(draft.MinimumPitchSemitones ?? string.Empty);
            writer.Write(draft.MaximumPitchSemitones ?? string.Empty);
            writer.Write(draft.MinimumVolume ?? string.Empty);
            writer.Write(draft.MaximumVolume ?? string.Empty);
            writer.Write((int)draft.Distribution);
            writer.Write(draft.Curve ?? string.Empty);
            writer.Write(draft.ModulationSeed ?? string.Empty);
            writer.Write(draft.SyncToTempo);
            writer.Write(draft.TempoBpm ?? string.Empty);
            writer.Write(draft.BeatDivision ?? string.Empty);
            writer.Write(draft.TargetLoudnessLufs ?? string.Empty);
            writer.Write(draft.MixBus ?? string.Empty);
            writer.Write(draft.DuckingDb ?? string.Empty);
            writer.Write(draft.DistanceScale ?? string.Empty);
            writer.Write(draft.AccessibilityVolumeCap ?? string.Empty);
            writer.Write((int)draft.MeterMode);
            writer.Write(draft.AreaScope ?? string.Empty);
            writer.Write(draft.AmbientBus ?? string.Empty);
            writer.Write((int)draft.ListenerPolicy);
            writer.Write(draft.ListenerGains.Count);
            foreach (var item in draft.ListenerGains)
            {
                writer.Write(item.Key ?? string.Empty);
                writer.Write(item.Label ?? string.Empty);
                writer.Write(item.Gain ?? string.Empty);
            }
            writer.Write(draft.ScopeChoices.Count);
            foreach (var item in draft.ScopeChoices)
            {
                writer.Write(item.Value ?? string.Empty);
                writer.Write(item.Label ?? string.Empty);
            }
            writer.Write(draft.RequiredAtRuntime);
            writer.Write(draft.GovernanceSummary ?? string.Empty);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool TryFloatUx2EnvG(string? text, out float value)
    {
        value = 0;
        var normalized = (text ?? string.Empty).Trim();
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            double.IsFinite(parsed) && parsed is >= -float.MaxValue and <= float.MaxValue)
        {
            value = (float)parsed;
            return float.IsFinite(value);
        }
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed) ||
            !double.IsFinite(parsed) || parsed is < -float.MaxValue or > float.MaxValue) return false;
        value = (float)parsed;
        return float.IsFinite(value);
    }

    private static bool TryIntUx2EnvG(string? text, out int value) =>
        int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value);

    private static bool TryCurveUx2EnvG(
        string? text,
        out IReadOnlyList<AudioModulationCurvePoint> curve)
    {
        var values = new List<AudioModulationCurvePoint>();
        foreach (var token in ParseTokensUx2EnvG(text))
        {
            var separator = token.IndexOf(':');
            if (separator <= 0 || !TryFloatUx2EnvG(token[..separator], out var position) ||
                !TryFloatUx2EnvG(token[(separator + 1)..], out var value))
            {
                curve = [];
                return false;
            }
            values.Add(new(position, value));
        }
        curve = values;
        return values.Count >= 2 && values.All(item => item.Position is >= 0 and <= 1 &&
                                                     item.Value is >= 0 and <= 1) &&
               values.Zip(values.Skip(1), (first, second) => second.Position > first.Position).All(item => item);
    }

    private static IReadOnlyList<string> ParseTokensUx2EnvG(string? text) =>
        (text ?? string.Empty).Replace("\r", string.Empty)
        .Split([',', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string? EmptyToNullUx2EnvG(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatCurveUx2EnvG(IReadOnlyList<AudioModulationCurvePoint> curve) =>
        string.Join("\n", curve.Select(item =>
            $"{InvariantUx2EnvG(item.Position)}:{InvariantUx2EnvG(item.Value)}"));

    private static string InvariantUx2EnvG(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string InvariantUx2EnvG(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string HumanResourcePreviewUx2EnvG(AudioResourcePreview item) =>
        $"{HumanTokenUx2EnvG(item.Name)} · {HumanResourceUx2EnvG(item.ResourcePath)}";

    private static string HumanResourceUx2EnvG(string? resource)
    {
        var normalized = (resource ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        var leaf = normalized.Split('/').LastOrDefault() ?? string.Empty;
        var extension = leaf.LastIndexOf('.');
        if (extension > 0) leaf = leaf[..extension];
        var human = HumanTokenUx2EnvG(leaf);
        return human == "élément sans nom lisible" ? "ressource audio" : human;
    }

    private static string HumanListenerUx2EnvG(string? key)
    {
        var value = (key ?? string.Empty).Trim();
        if (ContainsTechnicalIdentifierUx2EnvG(value)) return "Auditeur personnalisé";
        var human = HumanTokenUx2EnvG(value);
        return human == "élément sans nom lisible" ? "Auditeur personnalisé" : human;
    }

    private static string HumanFreeTextUx2EnvG(string? value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value ?? string.Empty,
            @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
            "identifiant interne",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string HumanTokenUx2EnvG(string? value)
    {
        var text = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsTechnicalIdentifierUx2EnvG(text)
            ? "élément sans nom lisible"
            : text;
    }

    private static bool ContainsTechnicalIdentifierUx2EnvG(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string RenderWaveformUx2EnvG(IReadOnlyList<float> peaks)
    {
        if (peaks.Count == 0) return "▁▁▁▁▁▁▁▁";
        const string bars = "▁▂▃▄▅▆▇█";
        return string.Concat(peaks.Take(48).Select(value =>
            bars[(int)Math.Clamp(MathF.Round(Math.Clamp(value, 0, 1) * (bars.Length - 1)), 0, bars.Length - 1)]));
    }

    private static string HumanOrderUx2EnvG(AudioOrderMode mode) => mode switch
    {
        AudioOrderMode.Sequential => "ordre séquentiel",
        AudioOrderMode.DeterministicRandom => "aléatoire déterministe",
        AudioOrderMode.ShuffleBag => "sac mélangé",
        AudioOrderMode.Weighted => "sélection pondérée",
        _ => "mode inconnu",
    };

    private static string HumanDistributionUx2EnvG(AudioModulationDistribution distribution) => distribution switch
    {
        AudioModulationDistribution.Uniform => "uniforme",
        AudioModulationDistribution.Triangular => "triangulaire",
        AudioModulationDistribution.Gaussian => "gaussienne",
        _ => "distribution inconnue",
    };

    private static string HumanListenerPolicyUx2EnvG(AudioListenerPolicy policy) => policy switch
    {
        AudioListenerPolicy.EveryListener => "tous les auditeurs",
        AudioListenerPolicy.LocalPlayer => "joueur local",
        AudioListenerPolicy.Party => "groupe local",
        AudioListenerPolicy.Personalized => "personnalisation par auditeur",
        _ => "politique inconnue",
    };

    private static ProjectDefinition ReplaceAreaUx2EnvG(
        ProjectDefinition project,
        AreaId areaId,
        AreaDefinition replacement) => new(
        project.Id,
        project.Title,
        project.Module,
        project.Blueprints,
        project.Areas.Select(item => item.Id == areaId ? replacement : item).ToArray(),
        project.Dialogues,
        project.Quests);

    internal static ProjectDefinition CreateUx2EnvGContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var withEmitters = CreateUx2EnvFContractProject(source);
        var area = withEmitters.Areas.First();
        var sourceEnvironment = area.Environment;
        var sourceAudio = sourceEnvironment.Audio;
        var resources = sourceAudio.Emitters.Definitions.SelectMany(item => item.ClipSet.Clips)
            .Select(item => item.ResourcePath).Take(2).ToArray();
        if (resources.Length < 2)
            throw new InvalidOperationException("The audio workbench fixture requires two governed resources.");
        var authoring = new AudioAuthoringProfile(
            Guid.Parse("a0047000-0000-4000-8000-000000000001"),
            AudioAuthoringProfile.CurrentSchemaVersion,
            "studio:ux2-env-g",
            "assets/audio-manifest.json",
            "CC0-1.0",
            new string('7', 64),
            false,
            new AudioPlaybackSelectionProfile(2026, 1,
                new Dictionary<string, float>(StringComparer.Ordinal)
                {
                    [resources[0]] = 2,
                    [resources[1]] = 1,
                }),
            new AudioModulationProfile(8, 16, -1, 1, .8f, 1.1f,
                AudioModulationDistribution.Triangular,
                [new(0, 0), new(.5f, .35f), new(1, 1)],
                2026, false, 120, 1),
            new AudioLoudnessMixProfile(-18, "Ambience", -6, 1, .9f, AudioMeterMode.Loudness),
            new AreaAmbientScopeProfile(area.Id.ToString(), "Ambience", AudioListenerPolicy.Personalized,
                new Dictionary<string, float>(StringComparer.Ordinal)
                {
                    ["auditeur-local"] = .8f,
                }));
        var audio = new AreaAudioProfile(
            sourceAudio.DayMusic,
            sourceAudio.NightMusic,
            sourceAudio.CombatMusic,
            sourceAudio.Ambience,
            sourceAudio.ReverbPreset,
            sourceAudio.TransitionSeconds,
            resources,
            AudioOrderMode.Weighted,
            12,
            4,
            .75f,
            sourceAudio.Spatial,
            sourceAudio.RuntimeControl,
            sourceAudio.AdaptiveLayers,
            sourceAudio.Orchestration,
            sourceAudio.Emitters,
            authoring);
        var environment = new AreaEnvironmentProfile(
            sourceEnvironment.SchemaVersion,
            sourceEnvironment.Classification,
            sourceEnvironment.Tags,
            sourceEnvironment.Lighting,
            sourceEnvironment.Fog,
            audio,
            sourceEnvironment.Rendering,
            sourceEnvironment.Sky,
            sourceEnvironment.Wind,
            sourceEnvironment.Weather,
            sourceEnvironment.Water,
            sourceEnvironment.Reverb,
            sourceEnvironment.Authoring,
            sourceEnvironment.RequireAuthoringRuntimeMetadata);
        return ReplaceAreaUx2EnvG(withEmitters, area.Id, area.WithEnvironment(environment));
    }

    internal StudioUx2EnvGSmokeResult RunUx2EnvGContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0) return FailedUx2EnvGSmoke();
        var sourceArea = sourceProject.Areas[0];
        var sourceEnvironment = sourceArea.Environment;
        var sourceAudio = sourceEnvironment.Audio;
        var emptyAuthoring = AudioAuthoringProfile.CreateDefault([], 0, 0);
        var emptyAudio = new AreaAudioProfile(
            sourceAudio.DayMusic,
            sourceAudio.NightMusic,
            sourceAudio.CombatMusic,
            sourceAudio.Ambience,
            sourceAudio.ReverbPreset,
            sourceAudio.TransitionSeconds,
            [],
            AudioOrderMode.Sequential,
            0,
            0,
            sourceAudio.RelativeVolume,
            sourceAudio.Spatial,
            sourceAudio.RuntimeControl,
            sourceAudio.AdaptiveLayers,
            sourceAudio.Orchestration,
            AudioEmitterCatalog.Empty,
            emptyAuthoring);
        var emptyEnvironment = new AreaEnvironmentProfile(
            sourceEnvironment.SchemaVersion,
            sourceEnvironment.Classification,
            sourceEnvironment.Tags,
            sourceEnvironment.Lighting,
            sourceEnvironment.Fog,
            emptyAudio,
            sourceEnvironment.Rendering,
            sourceEnvironment.Sky,
            sourceEnvironment.Wind,
            sourceEnvironment.Weather,
            sourceEnvironment.Water,
            sourceEnvironment.Reverb,
            sourceEnvironment.Authoring,
            sourceEnvironment.RequireAuthoringRuntimeMetadata);
        var emptyArea = sourceArea.WithEnvironment(emptyEnvironment);
        var emptyProject = ReplaceAreaUx2EnvG(sourceProject, sourceArea.Id, emptyArea);
        var emptyDraft = CreateAudioWorkbenchDraftUx2EnvG(emptyProject, new ProjectRevision(0), emptyArea);
        var emptySourceSupported = emptyDraft.Playlist.Count == 0 &&
                                   emptyDraft.GovernanceSummary.Contains("Schéma", StringComparison.Ordinal);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-g-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, CreateUx2EnvGContractProject(sourceProject));
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded) return FailedUx2EnvGSmoke();
            var target = editing.Project!.Areas[0];
            var explicitSelection = editing.CreateAudioWorkbenchDraftUx2EnvG() is null &&
                                    editing.CreateAudioWorkbenchDraftUx2EnvG(target.Id) is not null;
            var baseDraft = editing.CreateAudioWorkbenchDraftUx2EnvG(target.Id)!;

            var search = editing.BrowseAudioResourcesUx2EnvG(target.Id, "cascade", "eau", "fr", "CC0-1.0");
            var searchFacets = search.Count == 2 && search.All(item =>
                item.Tags.Contains("eau", StringComparison.CurrentCultureIgnoreCase) &&
                item.Locale.Equals("fr", StringComparison.CurrentCultureIgnoreCase) &&
                item.License.Equals("CC0 1.0", StringComparison.CurrentCultureIgnoreCase));
            var waveformAndGovernance = search.All(item => item.Waveform.Length >= 4 &&
                !string.IsNullOrWhiteSpace(item.Transcript) && !string.IsNullOrWhiteSpace(item.Provenance) &&
                item.DistributionReady);
            var singleAudition = editing.PlanAudioAuditionUx2EnvG(baseDraft,
                "cascade", "eau", "fr", "CC0-1.0", false);
            var batchAudition = editing.PlanAudioAuditionUx2EnvG(baseDraft,
                "cascade", "eau", "fr", "CC0-1.0", true);
            var auditionPlansOnly = singleAudition.Succeeded && singleAudition.Resources.Count == 1 &&
                                    batchAudition.Succeeded && batchAudition.Resources.Count == 2 &&
                                    singleAudition.PlayerInstantiationSkipped &&
                                    singleAudition.ResourceLoadingSkipped &&
                                    batchAudition.PlayerInstantiationSkipped &&
                                    batchAudition.ResourceLoadingSkipped &&
                                    singleAudition.Message.Contains("aucun lecteur Studio", StringComparison.Ordinal);

            var invalidPlaylist = editing.PreviewAudioWorkbenchUx2EnvG(baseDraft with
            {
                Playlist = baseDraft.Playlist.Concat([baseDraft.Playlist[0]]).ToArray(),
            });
            var invalidWeight = editing.PreviewAudioWorkbenchUx2EnvG(baseDraft with
            {
                Playlist = baseDraft.Playlist.Select((item, index) => index == 0
                    ? item with { Weight = "0" }
                    : item).ToArray(),
            });
            var invalidModulation = editing.PreviewAudioWorkbenchUx2EnvG(baseDraft with
            {
                MinimumIntervalSeconds = "24",
                MaximumIntervalSeconds = "2",
            });
            var invalidMix = editing.PreviewAudioWorkbenchUx2EnvG(baseDraft with
            {
                MixBus = string.Empty,
            });
            var invalidListener = editing.PreviewAudioWorkbenchUx2EnvG(baseDraft with
            {
                ListenerGains = baseDraft.ListenerGains.Select((item, index) => index == 0
                    ? item with { Gain = "9" }
                    : item).ToArray(),
            });

            var personalizedPreview = editing.PreviewAudioWorkbenchUx2EnvG(baseDraft);
            var personalizedMix = personalizedPreview.Succeeded && personalizedPreview.Mixes.Count == 3 &&
                                  personalizedPreview.Mixes.Any(item => item.Allowed && item.Personalized) &&
                                  personalizedPreview.Mixes.Any(item => !item.Allowed);
            var globalDraft = baseDraft with
            {
                AreaScope = string.Empty,
                ListenerPolicy = AudioListenerPolicy.EveryListener,
            };
            var globalPreview = editing.PreviewAudioWorkbenchUx2EnvG(globalDraft);
            var globalAndPersonalizedMix = personalizedMix && globalPreview.Succeeded &&
                                           globalPreview.Mixes.Count == 3 &&
                                           globalPreview.Mixes.All(item => item.Allowed);

            var history = editing.History!;
            var projectBeforeDryRun = editing.Project;
            var revisionBeforeDryRun = history.State.Revision;
            var journalBeforeDryRun = history.Journal.ToArray();
            var dirtyBeforeDryRun = editing.IsDirty;
            var undoBeforeDryRun = history.CanUndo;
            var redoBeforeDryRun = history.CanRedo;
            var dryDraft = baseDraft with
            {
                Playlist = baseDraft.Playlist.Reverse().ToArray(),
                OrderMode = AudioOrderMode.ShuffleBag,
                IntervalSeconds = "10",
                IntervalVariationSeconds = "3",
                RelativeVolume = "0.82",
                SelectionSeed = "451",
                NoRepeatWindow = "2",
                MinimumIntervalSeconds = "7",
                MaximumIntervalSeconds = "13",
                Distribution = AudioModulationDistribution.Gaussian,
                ModulationSeed = "451",
                TargetLoudnessLufs = "-19",
                ListenerPolicy = AudioListenerPolicy.EveryListener,
                AreaScope = string.Empty,
            };
            var dryPreview = editing.PreviewAudioWorkbenchUx2EnvG(dryDraft);
            var dryStatePreserved = ReferenceEquals(projectBeforeDryRun, editing.Project) &&
                                    history.State.Revision == revisionBeforeDryRun &&
                                    history.Journal.SequenceEqual(journalBeforeDryRun) &&
                                    editing.IsDirty == dirtyBeforeDryRun &&
                                    history.CanUndo == undoBeforeDryRun && history.CanRedo == redoBeforeDryRun;
            var canonicalDryRun = dryPreview.Succeeded && dryPreview.CanApply &&
                                  dryPreview.CanonicalDryRun && dryStatePreserved;
            var repeatedPreview = editing.PreviewAudioWorkbenchUx2EnvG(dryDraft);
            var deterministicSequence = dryPreview.StatisticsComputed &&
                                        dryPreview.Sequence.Count > 0 &&
                                        dryPreview.Sequence.SequenceEqual(repeatedPreview.Sequence);
            var boundedRuntime = dryPreview.PlayerInstantiationSkipped &&
                                 dryPreview.ResourceLoadingSkipped && dryPreview.RuntimeClaimsBounded &&
                                 dryPreview.Summary.Contains("aucun AudioStreamPlayer", StringComparison.Ordinal) &&
                                 dryPreview.Summary.Contains("runtime Godot", StringComparison.Ordinal);

            var sourceGuard = editing.PreviewAudioWorkbenchUx2EnvG(baseDraft with
            {
                SourceFingerprint = new string('0', 64),
            });
            var staleDraft = editing.CreateAudioWorkbenchDraftUx2EnvG(target.Id)!;
            var staleMutation = ApplyUx2EnvGRevisionMutation(editing, target.Id, "draft");
            var stalePreview = editing.PreviewAudioWorkbenchUx2EnvG(staleDraft);
            var stalePreviewBlocked = staleMutation && !stalePreview.Succeeded && editing.Undo().Succeeded;
            var staleApprovalDraft = editing.CreateAudioWorkbenchDraftUx2EnvG(target.Id)! with
            {
                RelativeVolume = "0.91",
            };
            var staleApprovalPreview = editing.PreviewAudioWorkbenchUx2EnvG(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2EnvGRevisionMutation(editing, target.Id, "approval");
            var staleApprovalApply = editing.ApplyApprovedAudioWorkbenchUx2EnvG();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;
            var staleRevisionBlocked = !sourceGuard.Succeeded && stalePreviewBlocked && staleApprovalBlocked;

            var mutablePlaylist = editing.CreateAudioWorkbenchDraftUx2EnvG(target.Id)!.Playlist.ToArray();
            var mutableDraft = editing.CreateAudioWorkbenchDraftUx2EnvG(target.Id)! with
            {
                Playlist = mutablePlaylist,
                RelativeVolume = "0.88",
            };
            var mutablePreview = editing.PreviewAudioWorkbenchUx2EnvG(mutableDraft);
            mutablePlaylist[0] = mutablePlaylist[0] with { Weight = "3.75" };
            var fingerprintApply = editing.ApplyApprovedAudioWorkbenchUx2EnvG();
            var approvalFingerprintBlocked = mutablePreview.Succeeded && !fingerprintApply.Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var sourceAudioAtApply = sourceTarget.Environment.Audio;
            var fresh = editing.CreateAudioWorkbenchDraftUx2EnvG(target.Id)!;
            fresh = fresh with
            {
                Playlist = fresh.Playlist.Reverse().Select((item, index) => index == 0
                    ? item with { Weight = "4" }
                    : item).ToArray(),
                OrderMode = AudioOrderMode.Weighted,
                IntervalSeconds = "11.25",
                IntervalVariationSeconds = "2.75",
                RelativeVolume = "0.84",
                SelectionSeed = "8801",
                NoRepeatWindow = "2",
                MinimumIntervalSeconds = "8.5",
                MaximumIntervalSeconds = "14",
                MinimumPitchSemitones = "-0.75",
                MaximumPitchSemitones = "1.25",
                MinimumVolume = "0.7",
                MaximumVolume = "1.2",
                Distribution = AudioModulationDistribution.Gaussian,
                Curve = "0:0\n0.4:0.25\n1:1",
                ModulationSeed = "8802",
                SyncToTempo = true,
                TempoBpm = "96",
                BeatDivision = "2",
                TargetLoudnessLufs = "-20",
                MixBus = "Ambience",
                DuckingDb = "-8",
                DistanceScale = "1.25",
                AccessibilityVolumeCap = "0.8",
                MeterMode = AudioMeterMode.Rms,
                AreaScope = string.Empty,
                AmbientBus = "Ambience",
                ListenerPolicy = AudioListenerPolicy.EveryListener,
            };
            var preview = editing.PreviewAudioWorkbenchUx2EnvG(fresh);
            var apply = editing.ApplyApprovedAudioWorkbenchUx2EnvG();
            var finalProject = editing.Project!;
            var finalTarget = finalProject.Areas.Single(area => area.Id == target.Id);
            var finalAudio = finalTarget.Environment.Audio;
            var journalTail = editing.History!.Journal.LastOrDefault();
            var applied = preview.Succeeded && preview.CanApply && apply.Succeeded &&
                          journalTail?.CommandName == "set_area_environment";

            var audioTailsPreserved =
                string.Equals(sourceAudioAtApply.DayMusic, finalAudio.DayMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudioAtApply.NightMusic, finalAudio.NightMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudioAtApply.CombatMusic, finalAudio.CombatMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudioAtApply.Ambience, finalAudio.Ambience, StringComparison.Ordinal) &&
                string.Equals(sourceAudioAtApply.ReverbPreset, finalAudio.ReverbPreset, StringComparison.Ordinal) &&
                sourceAudioAtApply.TransitionSeconds.Equals(finalAudio.TransitionSeconds) &&
                ReferenceEquals(sourceAudioAtApply.Spatial, finalAudio.Spatial) &&
                ReferenceEquals(sourceAudioAtApply.RuntimeControl, finalAudio.RuntimeControl) &&
                sourceAudioAtApply.AdaptiveLayers.SequenceEqual(finalAudio.AdaptiveLayers) &&
                ReferenceEquals(sourceAudioAtApply.Orchestration, finalAudio.Orchestration) &&
                ReferenceEquals(sourceAudioAtApply.Emitters, finalAudio.Emitters) &&
                sourceAudioAtApply.Authoring.Id == finalAudio.Authoring.Id &&
                sourceAudioAtApply.Authoring.SchemaVersion == finalAudio.Authoring.SchemaVersion &&
                string.Equals(sourceAudioAtApply.Authoring.Provenance, finalAudio.Authoring.Provenance,
                    StringComparison.Ordinal) &&
                string.Equals(sourceAudioAtApply.Authoring.PackageManifest, finalAudio.Authoring.PackageManifest,
                    StringComparison.Ordinal) &&
                string.Equals(sourceAudioAtApply.Authoring.LicenseSpdx, finalAudio.Authoring.LicenseSpdx,
                    StringComparison.Ordinal) &&
                string.Equals(sourceAudioAtApply.Authoring.ContentSha256, finalAudio.Authoring.ContentSha256,
                    StringComparison.Ordinal) &&
                sourceAudioAtApply.Authoring.RequiredAtRuntime == finalAudio.Authoring.RequiredAtRuntime;
            var sourceEnvironmentAtApply = sourceTarget.Environment;
            var finalEnvironment = finalTarget.Environment;
            var environmentLotsPreserved =
                sourceEnvironmentAtApply.SchemaVersion == finalEnvironment.SchemaVersion &&
                sourceEnvironmentAtApply.Classification == finalEnvironment.Classification &&
                sourceEnvironmentAtApply.Tags.SequenceEqual(finalEnvironment.Tags, StringComparer.Ordinal) &&
                ReferenceEquals(sourceEnvironmentAtApply.Lighting, finalEnvironment.Lighting) &&
                ReferenceEquals(sourceEnvironmentAtApply.Fog, finalEnvironment.Fog) &&
                ReferenceEquals(sourceEnvironmentAtApply.Rendering, finalEnvironment.Rendering) &&
                ReferenceEquals(sourceEnvironmentAtApply.Sky, finalEnvironment.Sky) &&
                ReferenceEquals(sourceEnvironmentAtApply.Wind, finalEnvironment.Wind) &&
                ReferenceEquals(sourceEnvironmentAtApply.Weather, finalEnvironment.Weather) &&
                ReferenceEquals(sourceEnvironmentAtApply.Water, finalEnvironment.Water) &&
                ReferenceEquals(sourceEnvironmentAtApply.Reverb, finalEnvironment.Reverb) &&
                ReferenceEquals(sourceEnvironmentAtApply.Authoring, finalEnvironment.Authoring) &&
                sourceEnvironmentAtApply.RequireAuthoringRuntimeMetadata ==
                finalEnvironment.RequireAuthoringRuntimeMetadata;
            var expectedArea = sourceTarget.WithEnvironment(finalEnvironment);
            var areaNeighborsPreserved = string.Equals(
                AreaJsonSerializer.Serialize(expectedArea),
                AreaJsonSerializer.Serialize(finalTarget),
                StringComparison.Ordinal);
            var projectNeighborsPreserved = ReferenceEquals(sourceState.Module, finalProject.Module) &&
                                            sourceState.Blueprints.SequenceEqual(finalProject.Blueprints) &&
                                            sourceState.Dialogues.SequenceEqual(finalProject.Dialogues) &&
                                            sourceState.Quests.SequenceEqual(finalProject.Quests) &&
                                            sourceState.Areas.Where(area => area.Id != target.Id).All(sourceNeighbor =>
                                                ReferenceEquals(sourceNeighbor, finalProject.Areas.Single(area =>
                                                    area.Id == sourceNeighbor.Id)));

            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceState, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var applyUndoRedo = applied && undone && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, target.Id.ToString());
            var persistence = save.Succeeded && reopen.Succeeded && reopened.Project is not null &&
                              string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopened.Project!),
                                  StringComparison.Ordinal);
            var structuredDiagnostics = invalidPlaylist.Groups.Count == 4 && invalidWeight.Groups.Count == 4 &&
                                        invalidModulation.Groups.Count == 4 && invalidMix.Groups.Count == 4 &&
                                        invalidListener.Groups.Count == 4 && preview.Groups.Count == 4;
            var humanText = !ContainsTechnicalIdentifierUx2EnvG(
                string.Join(' ', editing.AudioWorkbenchChoicesUx2EnvG.Select(choice => choice.Label)) + " " +
                preview.Message + " " + preview.Summary + " " +
                string.Join(' ', preview.Diff.Entries) + " " +
                string.Join(' ', preview.Sequence.Select(item => item.Resource)) + " " +
                string.Join(' ', preview.Mixes.Select(item => item.Listener)));

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvGTemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && emptySourceSupported && searchFacets &&
                            waveformAndGovernance && auditionPlansOnly && !invalidPlaylist.Succeeded &&
                            !invalidWeight.Succeeded && !invalidModulation.Succeeded && !invalidMix.Succeeded &&
                            !invalidListener.Succeeded && canonicalDryRun && deterministicSequence && boundedRuntime &&
                            globalAndPersonalizedMix && staleRevisionBlocked && approvalFingerprintBlocked &&
                            applyUndoRedo && persistence && environmentLotsPreserved && audioTailsPreserved &&
                            areaNeighborsPreserved && projectNeighborsPreserved && structuredDiagnostics && humanText;
            return new StudioUx2EnvGSmokeResult(
                succeeded,
                explicitSelection,
                emptySourceSupported,
                searchFacets,
                waveformAndGovernance,
                auditionPlansOnly && boundedRuntime,
                !invalidPlaylist.Succeeded,
                !invalidWeight.Succeeded,
                !invalidModulation.Succeeded,
                !invalidMix.Succeeded,
                !invalidListener.Succeeded,
                canonicalDryRun,
                deterministicSequence,
                globalAndPersonalizedMix,
                staleRevisionBlocked,
                approvalFingerprintBlocked,
                applyUndoRedo,
                persistence,
                environmentLotsPreserved,
                audioTailsPreserved,
                areaNeighborsPreserved,
                projectNeighborsPreserved,
                structuredDiagnostics,
                humanText,
                cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_G_CONTRACT_FAILED type={exception.GetType().Name} " +
                $"message={exception.Message}");
            return FailedUx2EnvGSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvGTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_G_CLEANUP_FAILED");
        }
    }

    private static bool ApplyUx2EnvGRevisionMutation(
        StudioSessionController controller,
        AreaId areaId,
        string marker)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null) return false;
        var source = area.Environment;
        var wind = new WindProfile(
            source.Wind.SchemaVersion,
            source.Wind.DirectionX,
            source.Wind.DirectionZ,
            source.Wind.Speed + .0078125f,
            source.Wind.GustStrength,
            source.Wind.GustIntervalSeconds,
            source.Wind.InfluencesVegetation,
            source.Wind.InfluencesParticles,
            source.Wind.AudioCue + $"-{marker}",
            source.Wind.GameplayForce,
            source.Wind.LocalZones);
        var candidate = new AreaEnvironmentProfile(
            source.SchemaVersion,
            source.Classification,
            source.Tags,
            source.Lighting,
            source.Fog,
            source.Audio,
            source.Rendering,
            source.Sky,
            wind,
            source.Weather,
            source.Water,
            source.Reverb,
            source.Authoring,
            source.RequireAuthoringRuntimeMetadata);
        var result = controller._setAreaEnvironmentUx2EnvG.Handle(history.State,
            new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvGTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-g-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvGSmokeResult FailedUx2EnvGSmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false);
}
