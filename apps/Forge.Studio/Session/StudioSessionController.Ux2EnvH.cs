using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvHRangeCode = "studio.audio_range.range_invalid";
    private const string Ux2EnvHCurveCode = "studio.audio_range.curve_invalid";
    private const string Ux2EnvHAbsorptionCode = "studio.audio_range.absorption_invalid";
    private const string Ux2EnvHControlCode = "studio.audio_control.value_invalid";
    private const string Ux2EnvHEventCode = "studio.audio_control.event_invalid";
    private const string Ux2EnvHApiCode = "studio.audio_control.api_unsupported";

    private static readonly string[] Ux2EnvHControlApiNames =
        ["audio_start", "audio_stop", "audio_pause", "audio_resume", "audio_fade"];

    private readonly SetAreaEnvironmentHandler _setAreaEnvironmentUx2EnvH = new(new SystemClock());
    private StudioUx2EnvHDraft? _approvedAudioRangeUx2EnvH;
    private ProjectRevision? _approvedAudioRangeRevisionUx2EnvH;
    private string? _approvedAudioRangeFingerprintUx2EnvH;

    internal IReadOnlyList<StudioUx2EnvHAreaChoice> AudioRangeChoicesUx2EnvH
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildAudioRangeChoicesUx2EnvH(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvHAreaChoice> BuildAudioRangeChoicesUx2EnvH(
        ProjectDefinition project)
    {
        var duplicateNames = project.Areas.GroupBy(area => area.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var audio = area.Environment.Audio;
                var display = HumanTokenUx2EnvH(area.DisplayName);
                if (duplicateNames[area.DisplayName] > 1)
                    display = $"{display} · {HumanTokenUx2EnvH(area.Slug)}";
                var range = audio.Spatial.Mode == AreaSoundMode.Global2D
                    ? "ambiance globale 2D"
                    : $"3D · {audio.Spatial.InnerRadius:0.##}–{audio.Spatial.OuterRadius:0.##} m";
                var control = $"{HumanInstancePolicyUx2EnvH(audio.RuntimeControl.InstancePolicy)} · " +
                              HumanAuthorityUx2EnvH(audio.RuntimeControl.Authority);
                return new StudioUx2EnvHAreaChoice(area.Id, $"{display} · {range} · {control}", range, control);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvHDraft? CreateAudioRangeDraftUx2EnvH(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1
            ? CreateAudioRangeDraftUx2EnvH(history.State.Revision, matches[0])
            : null;
    }

    private static StudioUx2EnvHDraft CreateAudioRangeDraftUx2EnvH(
        ProjectRevision revision,
        AreaDefinition area)
    {
        var spatial = area.Environment.Audio.Spatial;
        var control = area.Environment.Audio.RuntimeControl;
        return new StudioUx2EnvHDraft(
            area.Id,
            revision,
            AudioRangeSourceFingerprintUx2EnvH(area),
            area.DisplayName,
            area.Slug,
            spatial.Mode,
            InvariantUx2EnvH(spatial.InnerRadius),
            InvariantUx2EnvH(spatial.OuterRadius),
            InvariantUx2EnvH(spatial.AirAbsorption),
            spatial.AttenuationCurve,
            spatial.DebugSpheresVisible,
            control.InitiallyActive,
            control.InstancePolicy,
            control.Authority,
            InvariantUx2EnvH(control.DefaultFadeSeconds),
            control.StartedEvent,
            control.StoppedEvent,
            control.PausedEvent,
            control.FadedEvent,
            control.ForgeScriptApiVersion);
    }

    internal StudioUx2EnvHPreview PreviewAudioRangeUx2EnvH(StudioUx2EnvHDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAudioRangeApprovalUx2EnvH();
        var history = History;
        if (history is null || Project is null)
            return FailedAudioRangePreviewUx2EnvH("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedAudioRangePreviewUx2EnvH(
                "Le projet a changé depuis l’ouverture. Rechargez la zone avant l’aperçu.");
        if (!TryBuildAudioRangeUx2EnvH(draft, true, out var command, out var sourceArea,
                out var candidateEnvironment, out var diagnostics, out var message))
            return new(false, message, string.Empty, false, new([]),
                GroupAudioRangeDiagnosticsUx2EnvH(diagnostics), [], [], [], [], false, false,
                false, true, true, true);

        var dryRun = _setAreaEnvironmentUx2EnvH.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => CommandDiagnosticUx2EnvH(item,
                $"areas/{draft.AreaId}/environment/audio")).ToArray();
            return new(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé cet aperçu.",
                string.Empty,
                false,
                BuildAudioRangeDiffUx2EnvH(sourceArea!.Environment.Audio, candidateEnvironment!.Audio),
                GroupAudioRangeDiagnosticsUx2EnvH(commandDiagnostics), [], [], [], [], false, false,
                false, true, true, true);
        }

        var sourceAudio = sourceArea!.Environment.Audio;
        var candidateAudio = candidateEnvironment!.Audio;
        var diff = BuildAudioRangeDiffUx2EnvH(sourceAudio, candidateAudio);
        var attenuation = AudioOrchestrationRuntimeService.PreviewAttenuation(candidateAudio.Spatial, 13);
        var samples = attenuation.Samples.Select(item => new StudioUx2EnvHAttenuationSample(
            $"{item.Distance:0.##} m",
            item.Gain.ToString("0.###", CultureInfo.CurrentCulture),
            (item.Gain * (1 - candidateAudio.Spatial.AirAbsorption))
                .ToString("0.###", CultureInfo.CurrentCulture))).ToArray();
        var spheres = attenuation.DebugSpheres.Select(item => new StudioUx2EnvHDebugSphere(
            item.Kind == "inner" ? "Sphère intérieure" : "Sphère extérieure",
            $"{item.Radius:0.##} m")).ToArray();
        var scriptActions = ForgeScriptApiCatalog.Registrations
            .Where(item => Ux2EnvHControlApiNames.Contains(item.Name, StringComparer.Ordinal))
            .OrderBy(item => Array.IndexOf(Ux2EnvHControlApiNames, item.Name))
            .Select(item => new StudioUx2EnvHScriptAction(
                HumanScriptActionUx2EnvH(item.Name), item.Signature, item.Summary,
                string.Join(" · ", item.Availability.Order(StringComparer.Ordinal))))
            .ToArray();
        var controlSteps = BuildAudioControlStepsUx2EnvH(candidateAudio.RuntimeControl,
            out var authorityEvaluated);
        var scriptContractAligned = EvaluateForgeScriptControlUx2EnvH(candidateAudio.RuntimeControl,
            scriptActions);

        if (diff.HasChanges)
        {
            _approvedAudioRangeUx2EnvH = draft;
            _approvedAudioRangeRevisionUx2EnvH = history.State.Revision;
            _approvedAudioRangeFingerprintUx2EnvH = AudioRangeDraftFingerprintUx2EnvH(draft);
        }

        var spatialSummary = candidateAudio.Spatial.Mode == AreaSoundMode.Global2D
            ? "Ambiance globale 2D : la portée reste décrite mais le gain de distance vaut 1."
            : $"Portée 3D {candidateAudio.Spatial.InnerRadius:0.##}–{candidateAudio.Spatial.OuterRadius:0.##} m, " +
              $"courbe {HumanCurveUx2EnvH(candidateAudio.Spatial.AttenuationCurve)}.";
        var summary = $"{spatialSummary} Absorption de l’air {candidateAudio.Spatial.AirAbsorption:P0}; " +
                      $"{spheres.Length} sphère(s) de diagnostic planifiée(s). " +
                      $"Contrôle {HumanInstancePolicyUx2EnvH(candidateAudio.RuntimeControl.InstancePolicy)}, " +
                      $"autorité {HumanAuthorityUx2EnvH(candidateAudio.RuntimeControl.Authority)}, API ForgeScript " +
                      $"{HumanTokenUx2EnvH(candidateAudio.RuntimeControl.ForgeScriptApiVersion)}. " +
                      "Studio calcule uniquement les plans de gain, de sphères et d’état : aucun AudioStreamPlayer " +
                      "n’est instancié et aucun script n’est exécuté. Le playback et les callbacks réels restent au runtime Godot.";
        return new(true,
            diff.HasChanges
                ? "Aperçu sec validé · les réglages ne sont pas encore appliqués."
                : "Aperçu sec validé · aucune différence à appliquer.",
            summary,
            diff.HasChanges,
            diff,
            GroupAudioRangeDiagnosticsUx2EnvH([]),
            samples,
            spheres,
            scriptActions,
            controlSteps,
            true,
            scriptContractAligned,
            authorityEvaluated,
            true,
            true,
            true);
    }

    internal StudioUx2EnvHActionResult ApplyApprovedAudioRangeUx2EnvH()
    {
        var draft = _approvedAudioRangeUx2EnvH;
        var history = History;
        if (draft is null || history is null || _approvedAudioRangeRevisionUx2EnvH != history.State.Revision ||
            !string.Equals(_approvedAudioRangeFingerprintUx2EnvH,
                AudioRangeDraftFingerprintUx2EnvH(draft), StringComparison.Ordinal))
        {
            CancelAudioRangeApprovalUx2EnvH();
            return new(false, "Relancez l’aperçu sec avant d’appliquer la portée et le contrôle audio.");
        }
        if (!TryBuildAudioRangeUx2EnvH(draft, false, out var command, out _, out _, out _, out var message))
        {
            CancelAudioRangeApprovalUx2EnvH();
            return new(false, message);
        }
        var dryRun = _setAreaEnvironmentUx2EnvH.Handle(history.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            CancelAudioRangeApprovalUx2EnvH();
            return new(false, HumanizeAudioRangeDiagnosticUx2EnvH(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaEnvironmentUx2EnvH.Handle(history.State, command!);
        CancelAudioRangeApprovalUx2EnvH();
        if (!applied.Succeeded)
            return new(false, HumanizeAudioRangeDiagnosticUx2EnvH(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Portée et contrôle audio appliqués par set_area_environment · une commande annulable.");
    }

    internal void CancelAudioRangeApprovalUx2EnvH()
    {
        _approvedAudioRangeUx2EnvH = null;
        _approvedAudioRangeRevisionUx2EnvH = null;
        _approvedAudioRangeFingerprintUx2EnvH = null;
    }

    internal StudioUx2EnvHApprovalSnapshot CaptureAudioRangeApprovalUx2EnvH() => new(
        _approvedAudioRangeUx2EnvH,
        _approvedAudioRangeRevisionUx2EnvH,
        _approvedAudioRangeFingerprintUx2EnvH);

    internal void RestoreAudioRangeApprovalUx2EnvH(StudioUx2EnvHApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAudioRangeUx2EnvH = snapshot.Draft;
        _approvedAudioRangeRevisionUx2EnvH = snapshot.Revision;
        _approvedAudioRangeFingerprintUx2EnvH = snapshot.Fingerprint;
    }

    private bool TryBuildAudioRangeUx2EnvH(
        StudioUx2EnvHDraft draft,
        bool dryRun,
        out SetAreaEnvironmentCommand? command,
        out AreaDefinition? sourceArea,
        out AreaEnvironmentProfile? candidateEnvironment,
        out IReadOnlyList<StudioUx2EnvHDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateEnvironment = null;
        var found = new List<StudioUx2EnvHDiagnostic>();
        diagnostics = found;
        message = "La fiche de portée et de contrôle est invalide.";
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé depuis l’ouverture de la fiche.";
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
                AudioRangeSourceFingerprintUx2EnvH(sourceArea), StringComparison.Ordinal))
        {
            message = "La zone a changé depuis l’ouverture; ses données voisines ne seront pas réinjectées.";
            return false;
        }

        var innerValid = TryFloatUx2EnvH(draft.InnerRadius, out var inner);
        var outerValid = TryFloatUx2EnvH(draft.OuterRadius, out var outer);
        if (!innerValid || !outerValid || inner < 0 || outer < inner)
            found.Add(new(Ux2EnvHRangeCode, "audio/spatial/range",
                "La portée exige deux rayons finis : intérieur positif ou nul, extérieur supérieur ou égal."));
        if (!TryFloatUx2EnvH(draft.AirAbsorption, out var absorption) || absorption is < 0 or > 1)
            found.Add(new(Ux2EnvHAbsorptionCode, "audio/spatial/airAbsorption",
                "L’absorption de l’air doit rester entre 0 et 1."));
        var curve = (draft.AttenuationCurve ?? string.Empty).Trim().ToLowerInvariant();
        if (curve is not ("linear" or "inverse" or "inverse-square"))
            found.Add(new(Ux2EnvHCurveCode, "audio/spatial/attenuationCurve",
                "Choisissez une courbe linéaire, inverse ou inverse carrée."));
        if (!TryFloatUx2EnvH(draft.DefaultFadeSeconds, out var fade) || fade is < 0 or > 120)
            found.Add(new(Ux2EnvHControlCode, "audio/runtime/defaultFadeSeconds",
                "Le fondu par défaut doit être fini et rester entre 0 et 120 secondes."));
        var events = new[]
        {
            (draft.StartedEvent, "startedEvent", "démarrage"),
            (draft.StoppedEvent, "stoppedEvent", "arrêt"),
            (draft.PausedEvent, "pausedEvent", "pause"),
            (draft.FadedEvent, "fadedEvent", "fondu"),
        };
        foreach (var (value, path, label) in events)
        {
            if (!IsNamespacedEventUx2EnvH(value))
                found.Add(new(Ux2EnvHEventCode, $"audio/runtime/{path}",
                    $"L’événement de {label} doit être un nom humain namespacé, par exemple audio.{path.Replace("Event", string.Empty, StringComparison.Ordinal)}."));
        }
        var api = (draft.ForgeScriptApiVersion ?? string.Empty).Trim();
        var apiActions = ForgeScriptApiCatalog.Registrations
            .Where(item => item.ApiVersion.Equals(api, StringComparison.Ordinal) &&
                           Ux2EnvHControlApiNames.Contains(item.Name, StringComparer.Ordinal))
            .Select(item => item.Name)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (apiActions != Ux2EnvHControlApiNames.Length)
            found.Add(new(Ux2EnvHApiCode, "audio/runtime/forgeScriptApiVersion",
                "Cette version de l’API ForgeScript n’expose pas les cinq commandes audio requises."));
        if (found.Count > 0)
        {
            message = found[0].Message;
            return false;
        }

        try
        {
            var sourceEnvironment = sourceArea.Environment;
            var sourceAudio = sourceEnvironment.Audio;
            var spatial = new AudioSpatialProfile(draft.Mode, inner, outer, absorption, curve,
                draft.DebugSpheresVisible);
            var control = new AudioRuntimeControl(
                draft.InitiallyActive,
                draft.InstancePolicy,
                draft.Authority,
                fade,
                draft.StartedEvent.Trim(),
                draft.StoppedEvent.Trim(),
                draft.PausedEvent.Trim(),
                draft.FadedEvent.Trim(),
                api);
            var audio = new AreaAudioProfile(
                sourceAudio.DayMusic,
                sourceAudio.NightMusic,
                sourceAudio.CombatMusic,
                sourceAudio.Ambience,
                sourceAudio.ReverbPreset,
                sourceAudio.TransitionSeconds,
                sourceAudio.Playlist,
                sourceAudio.OrderMode,
                sourceAudio.IntervalSeconds,
                sourceAudio.IntervalVariationSeconds,
                sourceAudio.RelativeVolume,
                spatial,
                control,
                sourceAudio.AdaptiveLayers,
                sourceAudio.Orchestration,
                sourceAudio.Emitters,
                sourceAudio.Authoring);
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
            message = "Portée et contrôle prêts pour la commande canonique.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            found.Add(new(Ux2EnvHControlCode, "audio",
                $"Le profil audio ne peut pas être reconstruit · {exception.Message}"));
            message = found[0].Message;
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2EnvHControlStep> BuildAudioControlStepsUx2EnvH(
        AudioRuntimeControl control,
        out bool authorityEvaluated)
    {
        var state = AudioOrchestrationRuntimeService.Create(
            Guid.Parse("a0621200-0000-4000-8000-000000000001"));
        var steps = new List<StudioUx2EnvHControlStep>();
        void Add(string operation, Func<AudioPlaybackControlState, AudioPlaybackControlState> transition)
        {
            state = transition(state);
            steps.Add(new(operation,
                HumanPlaybackStatusUx2EnvH(state.Status),
                state.Gain.ToString("0.###", CultureInfo.CurrentCulture),
                state.ActiveInstances,
                state.Handle.Generation,
                HumanEventUx2EnvH(state.Events.LastOrDefault()),
                HumanizeAudioRangeDiagnosticUx2EnvH(state.Diagnostics.LastOrDefault()?.Code)));
        }
        var caller = control.Authority;
        Add("Démarrer", value => AudioOrchestrationRuntimeService.Start(value, control, caller));
        Add(control.InstancePolicy switch
        {
            AudioInstancePolicy.Single => "Tester une seconde instance",
            AudioInstancePolicy.Restart => "Redémarrer l’instance",
            _ => "Superposer une seconde instance",
        }, value => AudioOrchestrationRuntimeService.Start(value, control, caller));
        Add("Mettre en pause", value => AudioOrchestrationRuntimeService.Pause(value, control, caller));
        Add("Reprendre", value => AudioOrchestrationRuntimeService.Resume(value, control, caller));
        Add("Fondre à 25 %", value => AudioOrchestrationRuntimeService.Fade(
            value, control, caller, .25f, control.DefaultFadeSeconds));
        Add("Arrêter", value => AudioOrchestrationRuntimeService.Stop(value, control, caller));

        if (control.Authority == AudioAuthority.Server)
        {
            var denied = AudioOrchestrationRuntimeService.Start(
                AudioOrchestrationRuntimeService.Create(Guid.Parse("a0621200-0000-4000-8000-000000000002")),
                control,
                AudioAuthority.Local);
            authorityEvaluated = denied.Diagnostics.Any(item =>
                item.Code == AudioOrchestrationRuntimeService.AuthorityDeniedCode);
            steps.Add(new("Essai local refusé", HumanPlaybackStatusUx2EnvH(denied.Status),
                denied.Gain.ToString("0.###", CultureInfo.CurrentCulture), denied.ActiveInstances,
                denied.Handle.Generation, "Aucun callback publié",
                HumanizeAudioRangeDiagnosticUx2EnvH(denied.Diagnostics.FirstOrDefault()?.Code)));
        }
        else
        {
            authorityEvaluated = true;
            steps.Add(new("Autorité locale vérifiée", "Autorisé", "—", 0, state.Handle.Generation,
                "Aucun callback supplémentaire", "La commande locale est conforme à la politique."));
        }
        return steps;
    }

    private static bool EvaluateForgeScriptControlUx2EnvH(
        AudioRuntimeControl control,
        IReadOnlyList<StudioUx2EnvHScriptAction> actions)
    {
        if (actions.Count != Ux2EnvHControlApiNames.Length) return false;
        var state = new ScriptAudioEmitterState(
            new ScriptAudioEmitterRef(Guid.Parse("a0621200-0000-4000-8000-000000000003"), 1),
            "stopped", 1, []);
        state = ForgeScriptRuntimeTools.Control(state, "start");
        state = ForgeScriptRuntimeTools.Control(state, "pause");
        state = ForgeScriptRuntimeTools.Control(state, "resume");
        state = ForgeScriptRuntimeTools.Control(state, "fade", .25, control.DefaultFadeSeconds);
        state = ForgeScriptRuntimeTools.Control(state, "stop");
        return state is { Status: "stopped", Gain: 0, Revision: 5 } && state.Handle.Generation == 1 &&
               state.Events.Count == 5;
    }

    private static StudioUx2EnvHDiff BuildAudioRangeDiffUx2EnvH(
        AreaAudioProfile source,
        AreaAudioProfile candidate)
    {
        var entries = new List<string>();
        if (source.Spatial.Mode != candidate.Spatial.Mode)
            entries.Add($"Mode : {HumanSoundModeUx2EnvH(source.Spatial.Mode)} → {HumanSoundModeUx2EnvH(candidate.Spatial.Mode)}.");
        if (!source.Spatial.InnerRadius.Equals(candidate.Spatial.InnerRadius) ||
            !source.Spatial.OuterRadius.Equals(candidate.Spatial.OuterRadius))
            entries.Add($"Portée : {source.Spatial.InnerRadius:0.##}–{source.Spatial.OuterRadius:0.##} m → " +
                        $"{candidate.Spatial.InnerRadius:0.##}–{candidate.Spatial.OuterRadius:0.##} m.");
        if (!source.Spatial.AirAbsorption.Equals(candidate.Spatial.AirAbsorption) ||
            !source.Spatial.AttenuationCurve.Equals(candidate.Spatial.AttenuationCurve,
                StringComparison.OrdinalIgnoreCase))
            entries.Add($"Atténuation : {HumanCurveUx2EnvH(candidate.Spatial.AttenuationCurve)} · " +
                        $"absorption {candidate.Spatial.AirAbsorption:P0}.");
        if (source.Spatial.DebugSpheresVisible != candidate.Spatial.DebugSpheresVisible)
            entries.Add(candidate.Spatial.DebugSpheresVisible
                ? "Diagnostic : deux sphères de portée seront demandées au runtime."
                : "Diagnostic : les sphères de portée seront masquées.");
        if (source.RuntimeControl.InitiallyActive != candidate.RuntimeControl.InitiallyActive ||
            source.RuntimeControl.InstancePolicy != candidate.RuntimeControl.InstancePolicy ||
            source.RuntimeControl.Authority != candidate.RuntimeControl.Authority ||
            !source.RuntimeControl.DefaultFadeSeconds.Equals(candidate.RuntimeControl.DefaultFadeSeconds))
            entries.Add($"Contrôle : {(candidate.RuntimeControl.InitiallyActive ? "actif au chargement" : "démarrage par script")} · " +
                        $"{HumanInstancePolicyUx2EnvH(candidate.RuntimeControl.InstancePolicy)} · " +
                        $"{HumanAuthorityUx2EnvH(candidate.RuntimeControl.Authority)} · " +
                        $"fondu {candidate.RuntimeControl.DefaultFadeSeconds:0.##} s.");
        if (!source.RuntimeControl.StartedEvent.Equals(candidate.RuntimeControl.StartedEvent, StringComparison.Ordinal) ||
            !source.RuntimeControl.StoppedEvent.Equals(candidate.RuntimeControl.StoppedEvent, StringComparison.Ordinal) ||
            !source.RuntimeControl.PausedEvent.Equals(candidate.RuntimeControl.PausedEvent, StringComparison.Ordinal) ||
            !source.RuntimeControl.FadedEvent.Equals(candidate.RuntimeControl.FadedEvent, StringComparison.Ordinal))
            entries.Add("Callbacks : démarrage, arrêt, pause et fondu seront publiés avec les noms affichés.");
        if (!source.RuntimeControl.ForgeScriptApiVersion.Equals(
                candidate.RuntimeControl.ForgeScriptApiVersion, StringComparison.Ordinal))
            entries.Add($"API ForgeScript : version {HumanTokenUx2EnvH(candidate.RuntimeControl.ForgeScriptApiVersion)}.");
        return new(entries);
    }

    private static IReadOnlyList<StudioUx2EnvHDiagnosticGroup> GroupAudioRangeDiagnosticsUx2EnvH(
        IReadOnlyList<StudioUx2EnvHDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            (Key: "range", Title: "Portée et atténuation"),
            (Key: "control", Title: "Contrôle et callbacks"),
            (Key: "command", Title: "Commande canonique"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item =>
                AudioRangeDiagnosticGroupUx2EnvH(item) == definition.Key).ToArray();
            return new StudioUx2EnvHDiagnosticGroup(definition.Key, definition.Title,
                items.Length == 0 ? "Valide" : "À corriger", items);
        }).ToArray();
    }

    private static string AudioRangeDiagnosticGroupUx2EnvH(StudioUx2EnvHDiagnostic diagnostic)
    {
        if (diagnostic.Code is Ux2EnvHRangeCode or Ux2EnvHCurveCode or Ux2EnvHAbsorptionCode)
            return "range";
        if (diagnostic.Code is Ux2EnvHControlCode or Ux2EnvHEventCode or Ux2EnvHApiCode ||
            diagnostic.Code.StartsWith("audio.runtime.", StringComparison.Ordinal))
            return "control";
        return "command";
    }

    private static StudioUx2EnvHDiagnostic CommandDiagnosticUx2EnvH(
        CommandDiagnostic diagnostic,
        string fallbackPath)
    {
        var message = diagnostic.Message ?? string.Empty;
        var separator = message.IndexOf(" · ", StringComparison.Ordinal);
        var path = separator > 0 ? message[..separator] : fallbackPath;
        return new(diagnostic.Code, path,
            separator > 0 ? message[(separator + 3)..] : HumanizeAudioRangeDiagnosticUx2EnvH(diagnostic.Code));
    }

    private static string HumanizeAudioRangeDiagnosticUx2EnvH(string? code) => code switch
    {
        null or "" => "Aucun diagnostic.",
        "command.revision_conflict" => "Le projet a changé; rechargez la zone et relancez l’aperçu.",
        "area.environment.area_missing" => "La zone sélectionnée n’existe plus.",
        AudioOrchestrationRuntimeService.AuthorityDeniedCode =>
            "La politique serveur refuse honnêtement un appel local.",
        AudioOrchestrationRuntimeService.HandleInvalidCode => "Le handle audio ou le fondu est invalide.",
        AudioOrchestrationRuntimeService.InstanceRejectedCode =>
            "La politique d’instance unique refuse une seconde lecture.",
        _ => "La validation canonique a refusé ce profil audio.",
    };

    private static StudioUx2EnvHPreview FailedAudioRangePreviewUx2EnvH(string message) => new(
        false, message, string.Empty, false, new([]), GroupAudioRangeDiagnosticsUx2EnvH([]),
        [], [], [], [], false, false, false, true, true, true);

    private static string AudioRangeSourceFingerprintUx2EnvH(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string AudioRangeDraftFingerprintUx2EnvH(StudioUx2EnvHDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString());
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint ?? string.Empty);
            writer.Write(draft.AreaDisplayName ?? string.Empty);
            writer.Write(draft.AreaSlug ?? string.Empty);
            writer.Write((int)draft.Mode);
            writer.Write(draft.InnerRadius ?? string.Empty);
            writer.Write(draft.OuterRadius ?? string.Empty);
            writer.Write(draft.AirAbsorption ?? string.Empty);
            writer.Write(draft.AttenuationCurve ?? string.Empty);
            writer.Write(draft.DebugSpheresVisible);
            writer.Write(draft.InitiallyActive);
            writer.Write((int)draft.InstancePolicy);
            writer.Write((int)draft.Authority);
            writer.Write(draft.DefaultFadeSeconds ?? string.Empty);
            writer.Write(draft.StartedEvent ?? string.Empty);
            writer.Write(draft.StoppedEvent ?? string.Empty);
            writer.Write(draft.PausedEvent ?? string.Empty);
            writer.Write(draft.FadedEvent ?? string.Empty);
            writer.Write(draft.ForgeScriptApiVersion ?? string.Empty);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool TryFloatUx2EnvH(string? text, out float value)
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

    private static bool IsNamespacedEventUx2EnvH(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length is < 3 or > 128 || text.Contains(' ') || !text.Contains('.')) return false;
        var segments = text.Split('.');
        return segments.Length >= 2 && segments.All(segment => segment.Length > 0 &&
            char.IsLetter(segment[0]) && segment.All(character =>
                char.IsLetterOrDigit(character) || character is '_' or '-'));
    }

    private static string InvariantUx2EnvH(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string HumanTokenUx2EnvH(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsTechnicalIdentifierUx2EnvH(text)
            ? "Valeur conservée"
            : text;
    }

    private static string HumanEventUx2EnvH(string? value) => string.IsNullOrWhiteSpace(value)
        ? "Aucun callback publié"
        : HumanTokenUx2EnvH(value);

    private static bool ContainsTechnicalIdentifierUx2EnvH(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            "(?i)(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})") ||
        text.Contains("areas/", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("forge://", StringComparison.OrdinalIgnoreCase);

    private static string HumanSoundModeUx2EnvH(AreaSoundMode mode) => mode switch
    {
        AreaSoundMode.Global2D => "ambiance globale 2D",
        AreaSoundMode.Spatial3D => "source spatiale 3D",
        _ => "mode inconnu",
    };

    private static string HumanCurveUx2EnvH(string? curve) => curve?.Trim().ToLowerInvariant() switch
    {
        "linear" => "linéaire",
        "inverse" => "inverse",
        "inverse-square" => "inverse carrée",
        _ => "courbe inconnue",
    };

    private static string HumanInstancePolicyUx2EnvH(AudioInstancePolicy policy) => policy switch
    {
        AudioInstancePolicy.Single => "instance unique",
        AudioInstancePolicy.Restart => "redémarrage",
        AudioInstancePolicy.Overlap => "chevauchement",
        _ => "politique inconnue",
    };

    private static string HumanAuthorityUx2EnvH(AudioAuthority authority) => authority switch
    {
        AudioAuthority.Local => "locale",
        AudioAuthority.Server => "serveur",
        _ => "inconnue",
    };

    private static string HumanPlaybackStatusUx2EnvH(AudioPlaybackStatus status) => status switch
    {
        AudioPlaybackStatus.Stopped => "Arrêté",
        AudioPlaybackStatus.Playing => "En lecture",
        AudioPlaybackStatus.Paused => "En pause",
        AudioPlaybackStatus.Fading => "Fondu en cours",
        _ => "État inconnu",
    };

    private static string HumanScriptActionUx2EnvH(string name) => name switch
    {
        "audio_start" => "Démarrer",
        "audio_stop" => "Arrêter",
        "audio_pause" => "Mettre en pause",
        "audio_resume" => "Reprendre",
        "audio_fade" => "Appliquer un fondu",
        _ => "Commande audio",
    };

    private static ProjectDefinition ReplaceAreaUx2EnvH(
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

    internal static ProjectDefinition CreateUx2EnvHContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var withWorkbench = CreateUx2EnvGContractProject(source);
        var area = withWorkbench.Areas.First();
        var sourceEnvironment = area.Environment;
        var sourceAudio = sourceEnvironment.Audio;
        var audio = new AreaAudioProfile(
            sourceAudio.DayMusic,
            sourceAudio.NightMusic,
            sourceAudio.CombatMusic,
            sourceAudio.Ambience,
            sourceAudio.ReverbPreset,
            sourceAudio.TransitionSeconds,
            sourceAudio.Playlist,
            sourceAudio.OrderMode,
            sourceAudio.IntervalSeconds,
            sourceAudio.IntervalVariationSeconds,
            sourceAudio.RelativeVolume,
            new AudioSpatialProfile(AreaSoundMode.Spatial3D, 2.5f, 42, .12f, "inverse-square", true),
            new AudioRuntimeControl(false, AudioInstancePolicy.Restart, AudioAuthority.Server, 1.5f,
                "audio.started", "audio.stopped", "audio.paused", "audio.faded", "1.0"),
            sourceAudio.AdaptiveLayers,
            sourceAudio.Orchestration,
            sourceAudio.Emitters,
            sourceAudio.Authoring);
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
        return ReplaceAreaUx2EnvH(withWorkbench, area.Id, area.WithEnvironment(environment));
    }

    internal StudioUx2EnvHSmokeResult RunUx2EnvHContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0) return FailedUx2EnvHSmoke();
        var realSpatialSource = sourceProject.Areas.Any(area =>
            area.Environment.Audio.Spatial.Mode == AreaSoundMode.Spatial3D);
        var contractProject = CreateUx2EnvHContractProject(sourceProject);
        var target = contractProject.Areas.First();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux2-env-h-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, contractProject);
            if (!created.Succeeded || !editing.Open(tempRoot, target.Id.ToString()).Succeeded)
                return FailedUx2EnvHSmoke();
            var history = editing.History!;
            var explicitSelection = editing.CreateAudioRangeDraftUx2EnvH() is null &&
                                    editing.CreateAudioRangeDraftUx2EnvH(target.Id) is not null;
            var baseDraft = editing.CreateAudioRangeDraftUx2EnvH(target.Id)!;

            var invalidRange = editing.PreviewAudioRangeUx2EnvH(baseDraft with
            {
                InnerRadius = "30",
                OuterRadius = "12",
            });
            var invalidCurve = editing.PreviewAudioRangeUx2EnvH(baseDraft with
            {
                AttenuationCurve = "mystery",
            });
            var invalidAbsorption = editing.PreviewAudioRangeUx2EnvH(baseDraft with
            {
                AirAbsorption = "1.25",
            });
            var invalidEvents = editing.PreviewAudioRangeUx2EnvH(baseDraft with
            {
                StartedEvent = "started",
            });
            var invalidApi = editing.PreviewAudioRangeUx2EnvH(baseDraft with
            {
                ForgeScriptApiVersion = "9.9",
            });

            var projectBeforeDryRun = editing.Project;
            var revisionBeforeDryRun = history.State.Revision;
            var journalBeforeDryRun = history.Journal.ToArray();
            var dirtyBeforeDryRun = editing.IsDirty;
            var canUndoBeforeDryRun = history.CanUndo;
            var canRedoBeforeDryRun = history.CanRedo;
            var dryDraft = baseDraft with
            {
                Mode = AreaSoundMode.Spatial3D,
                InnerRadius = "3.25",
                OuterRadius = "47.5",
                AirAbsorption = "0.18",
                AttenuationCurve = "inverse",
                DebugSpheresVisible = true,
                InitiallyActive = false,
                InstancePolicy = AudioInstancePolicy.Overlap,
                Authority = AudioAuthority.Server,
                DefaultFadeSeconds = "2.25",
                StartedEvent = "audio.zone_started",
                StoppedEvent = "audio.zone_stopped",
                PausedEvent = "audio.zone_paused",
                FadedEvent = "audio.zone_faded",
            };
            var dryPreview = editing.PreviewAudioRangeUx2EnvH(dryDraft);
            var dryStatePreserved = ReferenceEquals(projectBeforeDryRun, editing.Project) &&
                                    history.State.Revision == revisionBeforeDryRun &&
                                    history.Journal.SequenceEqual(journalBeforeDryRun) &&
                                    editing.IsDirty == dirtyBeforeDryRun &&
                                    history.CanUndo == canUndoBeforeDryRun && history.CanRedo == canRedoBeforeDryRun;
            var canonicalDryRun = dryPreview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  dryStatePreserved;
            var attenuationPlan = dryPreview.Attenuation.Count == 13 &&
                                  dryPreview.Attenuation.First().CurveGain == "1" &&
                                  dryPreview.Attenuation.Last().CurveGain == "0";
            var airAbsorptionPlan = dryPreview.Attenuation.First().GainAfterAirAbsorption !=
                                    dryPreview.Attenuation.First().CurveGain &&
                                    dryPreview.Summary.Contains("Absorption de l’air", StringComparison.Ordinal);
            var debugSpherePlan = dryPreview.DebugSpheres.Count == 2 &&
                                  dryPreview.DebugSpheres.Select(item => item.Kind).SequenceEqual(
                                      ["Sphère intérieure", "Sphère extérieure"]);
            var scriptApiCatalog = dryPreview.ScriptActions.Count == 5 &&
                                   dryPreview.ScriptActions.Select(item => item.Name).SequenceEqual(
                                       ["Démarrer", "Arrêter", "Mettre en pause", "Reprendre", "Appliquer un fondu"]);
            var typedControlPlan = dryPreview.ScriptContractAligned &&
                                   dryPreview.ControlSteps.Count == 7 &&
                                   dryPreview.ControlSteps.Take(6).All(item => item.HandleGeneration == 1) &&
                                   dryPreview.ControlSteps.Any(item =>
                                       item.Operation == "Superposer une seconde instance" &&
                                       item.ActiveInstances == 2) &&
                                   dryPreview.ControlSteps.Any(item => item.Operation == "Fondre à 25 %" &&
                                       item.Status == "Fondu en cours");
            var authorityDiagnostic = dryPreview.AuthorityDiagnosticExercised &&
                                      dryPreview.ControlSteps.Any(item => item.Operation == "Essai local refusé" &&
                                          item.Diagnostic.Contains("refuse", StringComparison.CurrentCultureIgnoreCase));

            var staleDraft = editing.CreateAudioRangeDraftUx2EnvH(target.Id)!;
            var staleMutation = ApplyUx2EnvHRevisionMutation(editing, target.Id, "draft");
            var stalePreview = editing.PreviewAudioRangeUx2EnvH(staleDraft);
            var stalePreviewBlocked = staleMutation && !stalePreview.Succeeded && editing.Undo().Succeeded;
            var staleApprovalDraft = editing.CreateAudioRangeDraftUx2EnvH(target.Id)! with
            {
                OuterRadius = "48",
            };
            var staleApprovalPreview = editing.PreviewAudioRangeUx2EnvH(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2EnvHRevisionMutation(editing, target.Id, "approval");
            var staleApprovalApply = editing.ApplyApprovedAudioRangeUx2EnvH();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;
            var staleRevisionBlocked = stalePreviewBlocked && staleApprovalBlocked;

            var fingerprintDraft = editing.CreateAudioRangeDraftUx2EnvH(target.Id)! with
            {
                OuterRadius = "49",
            };
            var fingerprintPreview = editing.PreviewAudioRangeUx2EnvH(fingerprintDraft);
            var approval = editing.CaptureAudioRangeApprovalUx2EnvH();
            editing.RestoreAudioRangeApprovalUx2EnvH(approval with
            {
                Draft = approval.Draft! with { OuterRadius = "49.5" },
            });
            var fingerprintApply = editing.ApplyApprovedAudioRangeUx2EnvH();
            var approvalFingerprintBlocked = fingerprintPreview.Succeeded && !fingerprintApply.Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var sourceEnvironment = sourceTarget.Environment;
            var sourceAudio = sourceEnvironment.Audio;
            var fresh = editing.CreateAudioRangeDraftUx2EnvH(target.Id)! with
            {
                Mode = AreaSoundMode.Spatial3D,
                InnerRadius = "4.125",
                OuterRadius = "51.75",
                AirAbsorption = "0.21",
                AttenuationCurve = "linear",
                DebugSpheresVisible = true,
                InitiallyActive = false,
                InstancePolicy = AudioInstancePolicy.Overlap,
                Authority = AudioAuthority.Server,
                DefaultFadeSeconds = "2.75",
                StartedEvent = "audio.studio_started",
                StoppedEvent = "audio.studio_stopped",
                PausedEvent = "audio.studio_paused",
                FadedEvent = "audio.studio_faded",
                ForgeScriptApiVersion = "1.0",
            };
            var preview = editing.PreviewAudioRangeUx2EnvH(fresh);
            var apply = editing.ApplyApprovedAudioRangeUx2EnvH();
            var finalProject = editing.Project!;
            var finalTarget = finalProject.Areas.Single(area => area.Id == target.Id);
            var finalEnvironment = finalTarget.Environment;
            var finalAudio = finalEnvironment.Audio;
            var journalTail = editing.History!.Journal.LastOrDefault();
            var applied = preview.Succeeded && preview.CanApply && apply.Succeeded &&
                          journalTail?.CommandName == "set_area_environment" &&
                          finalAudio.Spatial is
                          {
                              Mode: AreaSoundMode.Spatial3D,
                              InnerRadius: 4.125f,
                              OuterRadius: 51.75f,
                              AirAbsorption: .21f,
                              AttenuationCurve: "linear",
                              DebugSpheresVisible: true,
                          } &&
                          finalAudio.RuntimeControl is
                          {
                              InitiallyActive: false,
                              InstancePolicy: AudioInstancePolicy.Overlap,
                              Authority: AudioAuthority.Server,
                              DefaultFadeSeconds: 2.75f,
                              StartedEvent: "audio.studio_started",
                              StoppedEvent: "audio.studio_stopped",
                              PausedEvent: "audio.studio_paused",
                              FadedEvent: "audio.studio_faded",
                              ForgeScriptApiVersion: "1.0",
                          };

            var audioTailsPreserved =
                string.Equals(sourceAudio.DayMusic, finalAudio.DayMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudio.NightMusic, finalAudio.NightMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudio.CombatMusic, finalAudio.CombatMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudio.Ambience, finalAudio.Ambience, StringComparison.Ordinal) &&
                string.Equals(sourceAudio.ReverbPreset, finalAudio.ReverbPreset, StringComparison.Ordinal) &&
                sourceAudio.TransitionSeconds.Equals(finalAudio.TransitionSeconds) &&
                sourceAudio.Playlist.SequenceEqual(finalAudio.Playlist, StringComparer.Ordinal) &&
                sourceAudio.OrderMode == finalAudio.OrderMode &&
                sourceAudio.IntervalSeconds.Equals(finalAudio.IntervalSeconds) &&
                sourceAudio.IntervalVariationSeconds.Equals(finalAudio.IntervalVariationSeconds) &&
                sourceAudio.RelativeVolume.Equals(finalAudio.RelativeVolume) &&
                sourceAudio.AdaptiveLayers.SequenceEqual(finalAudio.AdaptiveLayers) &&
                ReferenceEquals(sourceAudio.Orchestration, finalAudio.Orchestration) &&
                ReferenceEquals(sourceAudio.Emitters, finalAudio.Emitters) &&
                ReferenceEquals(sourceAudio.Authoring, finalAudio.Authoring);
            var environmentLotsPreserved =
                sourceEnvironment.SchemaVersion == finalEnvironment.SchemaVersion &&
                sourceEnvironment.Classification == finalEnvironment.Classification &&
                sourceEnvironment.Tags.SequenceEqual(finalEnvironment.Tags, StringComparer.Ordinal) &&
                ReferenceEquals(sourceEnvironment.Lighting, finalEnvironment.Lighting) &&
                ReferenceEquals(sourceEnvironment.Fog, finalEnvironment.Fog) &&
                ReferenceEquals(sourceEnvironment.Rendering, finalEnvironment.Rendering) &&
                ReferenceEquals(sourceEnvironment.Sky, finalEnvironment.Sky) &&
                ReferenceEquals(sourceEnvironment.Wind, finalEnvironment.Wind) &&
                ReferenceEquals(sourceEnvironment.Weather, finalEnvironment.Weather) &&
                ReferenceEquals(sourceEnvironment.Water, finalEnvironment.Water) &&
                ReferenceEquals(sourceEnvironment.Reverb, finalEnvironment.Reverb) &&
                ReferenceEquals(sourceEnvironment.Authoring, finalEnvironment.Authoring) &&
                sourceEnvironment.RequireAuthoringRuntimeMetadata == finalEnvironment.RequireAuthoringRuntimeMetadata;
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
            var structuredDiagnostics = invalidRange.Groups.Count == 3 && invalidCurve.Groups.Count == 3 &&
                                        invalidAbsorption.Groups.Count == 3 && invalidEvents.Groups.Count == 3 &&
                                        invalidApi.Groups.Count == 3 && preview.Groups.Count == 3;
            var humanText = !ContainsTechnicalIdentifierUx2EnvH(
                string.Join(' ', editing.AudioRangeChoicesUx2EnvH.Select(choice => choice.Label)) + " " +
                preview.Message + " " + preview.Summary + " " +
                string.Join(' ', preview.Diff.Entries) + " " +
                string.Join(' ', preview.ControlSteps.Select(item => item.Operation + " " + item.Status)));
            var boundedRuntime = preview is
            {
                PlayerInstantiationSkipped: true,
                ScriptExecutionSkipped: true,
                RuntimeClaimsBounded: true,
            } && preview.Summary.Contains("aucun AudioStreamPlayer", StringComparison.Ordinal) &&
                 preview.Summary.Contains("aucun script n’est exécuté", StringComparison.Ordinal);

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvHTemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && !invalidRange.Succeeded && !invalidCurve.Succeeded &&
                            !invalidAbsorption.Succeeded && !invalidEvents.Succeeded && !invalidApi.Succeeded &&
                            canonicalDryRun && attenuationPlan && airAbsorptionPlan && debugSpherePlan &&
                            scriptApiCatalog && typedControlPlan && authorityDiagnostic && staleRevisionBlocked &&
                            approvalFingerprintBlocked && applyUndoRedo && persistence && environmentLotsPreserved &&
                            audioTailsPreserved && areaNeighborsPreserved && projectNeighborsPreserved &&
                            structuredDiagnostics && humanText && boundedRuntime;
            return new StudioUx2EnvHSmokeResult(
                succeeded,
                explicitSelection,
                realSpatialSource,
                !invalidRange.Succeeded,
                !invalidCurve.Succeeded,
                !invalidAbsorption.Succeeded,
                !invalidEvents.Succeeded,
                !invalidApi.Succeeded,
                canonicalDryRun,
                attenuationPlan,
                airAbsorptionPlan,
                debugSpherePlan,
                scriptApiCatalog,
                typedControlPlan,
                authorityDiagnostic,
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
                boundedRuntime,
                cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_H_CONTRACT_FAILED type={exception.GetType().Name} " +
                $"message={exception.Message}");
            return FailedUx2EnvHSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvHTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_H_CLEANUP_FAILED");
        }
    }

    private static bool ApplyUx2EnvHRevisionMutation(
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
            source.Wind.Speed + .00390625f,
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
        var result = controller._setAreaEnvironmentUx2EnvH.Handle(history.State,
            new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvHTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-h-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvHSmokeResult FailedUx2EnvHSmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false);
}
