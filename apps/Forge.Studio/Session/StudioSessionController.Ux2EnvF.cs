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
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvFDefinitionCode = "studio.audio_emitter.definition_invalid";
    private const string Ux2EnvFClipCode = "studio.audio_emitter.clip_invalid";
    private const string Ux2EnvFLoopCode = "studio.audio_emitter.loop_invalid";
    private const string Ux2EnvFSpatialCode = "studio.audio_emitter.spatial_invalid";
    private const string Ux2EnvFInstanceCode = "studio.audio_emitter.instance_invalid";

    private readonly SetAreaEnvironmentHandler _setAreaEnvironmentUx2EnvF = new(new SystemClock());
    private StudioUx2EnvFDraft? _approvedEmittersUx2EnvF;
    private ProjectRevision? _approvedEmittersRevisionUx2EnvF;
    private string? _approvedEmittersFingerprintUx2EnvF;

    internal IReadOnlyList<StudioUx2EnvFAreaChoice> AudioEmitterChoicesUx2EnvF
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildAudioEmitterChoicesUx2EnvF(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvFAreaChoice> BuildAudioEmitterChoicesUx2EnvF(
        ProjectDefinition project)
    {
        var duplicateNames = project.Areas.GroupBy(area => area.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var displayName = ContainsTechnicalIdentifierUx2EnvF(area.DisplayName)
                    ? "Zone sans nom lisible"
                    : area.DisplayName.Trim();
                var name = duplicateNames[area.DisplayName] > 1
                    ? $"{displayName} · {HumanTokenUx2EnvF(area.Slug)}"
                    : displayName;
                var catalog = area.Environment.Audio.Emitters;
                return new StudioUx2EnvFAreaChoice(
                    area.Id,
                    $"{name} · {catalog.Definitions.Count} émetteur(s) · " +
                    $"{catalog.Definitions.Sum(item => item.ClipSet.Clips.Count)} clip(s) · " +
                    $"{catalog.Instances.Count} instance(s)",
                    catalog.Definitions.Count,
                    catalog.Definitions.Sum(item => item.ClipSet.Clips.Count),
                    catalog.Instances.Count);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvFDraft? CreateAudioEmittersDraftUx2EnvF(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1
            ? CreateAudioEmittersDraftUx2EnvF(history.State.Revision, matches[0])
            : null;
    }

    private static StudioUx2EnvFDraft CreateAudioEmittersDraftUx2EnvF(
        ProjectRevision revision,
        AreaDefinition area)
    {
        var catalog = area.Environment.Audio.Emitters;
        var buses = new List<StudioUx2EnvFMixBusChoice> { new(null, "Aucun bus dédié · Master au runtime") };
        buses.AddRange(area.Environment.Audio.Orchestration.MixBuses.Select(item =>
            new StudioUx2EnvFMixBusChoice(item.Id, HumanTokenUx2EnvF(item.Name))));
        return new StudioUx2EnvFDraft(
            area.Id,
            revision,
            AudioEmitterSourceFingerprintUx2EnvF(area),
            area.DisplayName,
            area.Slug,
            catalog.Definitions.Select(ToDefinitionDraftUx2EnvF).ToArray(),
            catalog.Instances.Select(ToInstanceDraftUx2EnvF).ToArray(),
            buses);
    }

    private static StudioUx2EnvFDefinitionDraft ToDefinitionDraftUx2EnvF(AudioEmitterDefinition item) => new(
        item.Id,
        item.SchemaVersion,
        item.Name,
        item.ClipSet.Id,
        item.ClipSet.SchemaVersion,
        item.ClipSet.Name,
        item.ClipSet.Selection,
        item.ClipSet.Clips.Select(ToClipDraftUx2EnvF).ToArray(),
        item.ClipSet.Provenance,
        item.Loop.Mode,
        InvariantUx2EnvF(item.Loop.RepeatCount),
        InvariantUx2EnvF(item.Loop.RegionStartSeconds),
        InvariantUx2EnvF(item.Loop.RegionEndSeconds),
        InvariantUx2EnvF(item.Loop.CrossfadeSeconds),
        item.Loop.StopEvent,
        InvariantUx2EnvF(item.Spatial.InnerRadius),
        InvariantUx2EnvF(item.Spatial.OuterRadius),
        item.Spatial.AttenuationCurve,
        InvariantUx2EnvF(item.Spatial.ConeAngleDegrees),
        InvariantUx2EnvF(item.Spatial.ConeAttenuationDb),
        InvariantUx2EnvF(item.Spatial.Occlusion),
        InvariantUx2EnvF(item.Spatial.HeightOffset),
        item.Spatial.VirtualizeOutsideRange,
        item.MixBusId,
        InvariantUx2EnvF(item.StartDelaySeconds),
        item.ActivatedScript,
        item.CompletedScript,
        item.Required,
        item.Provenance,
        item.PackageManifest,
        item.LicenseSpdx,
        item.ContentSha256);

    private static StudioUx2EnvFClipDraft ToClipDraftUx2EnvF(AudioClipDefinition item) => new(
        item.Id,
        item.ResourcePath,
        InvariantUx2EnvF(item.Weight),
        item.Tags.ToArray(),
        item.Variant,
        item.Locale,
        InvariantUx2EnvF(item.LoudnessLufs),
        item.PreviewResourcePath,
        item.MimeType,
        InvariantUx2EnvF(item.MemoryBytes),
        item.LicenseSpdx,
        item.Provenance,
        item.ContentSha256,
        item.Transcript,
        item.WaveformPeaks?.Select(InvariantUx2EnvF).ToArray() ?? [],
        InvariantUx2EnvF(item.DurationSeconds));

    private static StudioUx2EnvFInstanceDraft ToInstanceDraftUx2EnvF(AudioEmitterInstance item) => new(
        item.Id,
        item.DefinitionId,
        InvariantUx2EnvF(item.Transform.Position.X),
        InvariantUx2EnvF(item.Transform.Position.Y),
        InvariantUx2EnvF(item.Transform.Position.Z),
        InvariantUx2EnvF(item.Transform.Rotation.X),
        InvariantUx2EnvF(item.Transform.Rotation.Y),
        InvariantUx2EnvF(item.Transform.Rotation.Z),
        InvariantUx2EnvF(item.Transform.Rotation.W),
        InvariantUx2EnvF(item.Transform.Scale.X),
        InvariantUx2EnvF(item.Transform.Scale.Y),
        InvariantUx2EnvF(item.Transform.Scale.Z),
        item.Enabled,
        item.Provenance);

    internal StudioUx2EnvFPreview PreviewAudioEmittersUx2EnvF(StudioUx2EnvFDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAudioEmittersApprovalUx2EnvF();
        var history = History;
        if (history is null || Project is null)
            return FailedAudioEmittersPreviewUx2EnvF("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedAudioEmittersPreviewUx2EnvF(
                "Le projet a changé depuis l’ouverture de la fiche. Rechargez la zone avant l’aperçu.");
        if (!TryBuildAudioEmittersUx2EnvF(draft, true, out var command, out var sourceArea,
                out var candidateEnvironment, out var diagnostics, out var message))
            return new(false, message, string.Empty, false, new([]),
                GroupAudioEmitterDiagnosticsUx2EnvF(diagnostics), [], false, false, true, true, true);

        var dryRun = _setAreaEnvironmentUx2EnvF.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => CommandDiagnosticUx2EnvF(item,
                $"areas/{draft.AreaId}/environment/audio/emitters")).ToArray();
            return new(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé cet aperçu.",
                string.Empty,
                false,
                BuildAudioEmitterDiffUx2EnvF(sourceArea!.Environment.Audio.Emitters,
                    candidateEnvironment!.Audio.Emitters),
                GroupAudioEmitterDiagnosticsUx2EnvF(commandDiagnostics),
                [], false, false, true, true, true);
        }

        var sourceCatalog = sourceArea!.Environment.Audio.Emitters;
        var candidateCatalog = candidateEnvironment!.Audio.Emitters;
        var diff = BuildAudioEmitterDiffUx2EnvF(sourceCatalog, candidateCatalog);
        var playbacks = AudioEmitterRuntimeService.CreateInitialPlaybacks(candidateCatalog, 2026);
        var definitions = candidateCatalog.Definitions.ToDictionary(item => item.Id);
        var plans = playbacks.Select(playback =>
        {
            var definition = definitions[playback.DefinitionId];
            var clip = definition.ClipSet.Clips.Single(item => item.Id == playback.ClipId);
            return new StudioUx2EnvFPlaybackPlan(
                HumanTokenUx2EnvF(definition.Name),
                HumanClipUx2EnvF(clip),
                HumanLoopUx2EnvF(playback.Loop.Mode),
                $"X {playback.Transform.Position.X:0.##} · Y {playback.Transform.Position.Y:0.##} · " +
                $"Z {playback.Transform.Position.Z:0.##}",
                playback.Autoplay);
        }).ToArray();
        if (diff.HasChanges)
        {
            _approvedEmittersUx2EnvF = draft;
            _approvedEmittersRevisionUx2EnvF = history.State.Revision;
            _approvedEmittersFingerprintUx2EnvF = AudioEmitterDraftFingerprintUx2EnvF(draft);
        }
        var summary =
            $"Plan déterministe : {candidateCatalog.Definitions.Count} émetteur(s), " +
            $"{candidateCatalog.Definitions.Sum(item => item.ClipSet.Clips.Count)} clip(s), " +
            $"{candidateCatalog.Instances.Count} instance(s), {plans.Length} départ(s) initial(aux). " +
            "La sélection pondérée utilise une graine stable; les positions, boucles et distances sont des données auteur. " +
            "Cet aperçu Studio ne charge aucune ressource et n’instancie aucun AudioStreamPlayer3D : " +
            "le playback réel reste la responsabilité du runtime Godot.";
        return new(true,
            diff.HasChanges
                ? "Aperçu sec validé · les émetteurs ne sont pas encore appliqués."
                : "Aperçu sec validé · aucune différence à appliquer.",
            summary,
            diff.HasChanges,
            diff,
            GroupAudioEmitterDiagnosticsUx2EnvF([]),
            plans,
            true,
            true,
            true,
            true,
            true);
    }

    internal StudioUx2EnvFActionResult ApplyApprovedAudioEmittersUx2EnvF()
    {
        var draft = _approvedEmittersUx2EnvF;
        var history = History;
        if (draft is null || history is null || _approvedEmittersRevisionUx2EnvF != history.State.Revision ||
            !string.Equals(_approvedEmittersFingerprintUx2EnvF,
                AudioEmitterDraftFingerprintUx2EnvF(draft), StringComparison.Ordinal))
        {
            CancelAudioEmittersApprovalUx2EnvF();
            return new(false, "Relancez l’aperçu sec avant d’appliquer les émetteurs audio.");
        }
        if (!TryBuildAudioEmittersUx2EnvF(draft, false, out var command, out _, out _, out _, out var message))
        {
            CancelAudioEmittersApprovalUx2EnvF();
            return new(false, message);
        }
        var dryRun = _setAreaEnvironmentUx2EnvF.Handle(history.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            CancelAudioEmittersApprovalUx2EnvF();
            return new(false, HumanizeAudioEmitterDiagnosticUx2EnvF(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaEnvironmentUx2EnvF.Handle(history.State, command!);
        CancelAudioEmittersApprovalUx2EnvF();
        if (!applied.Succeeded)
            return new(false, HumanizeAudioEmitterDiagnosticUx2EnvF(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Émetteurs audio appliqués par set_area_environment · une commande annulable.");
    }

    internal void CancelAudioEmittersApprovalUx2EnvF()
    {
        _approvedEmittersUx2EnvF = null;
        _approvedEmittersRevisionUx2EnvF = null;
        _approvedEmittersFingerprintUx2EnvF = null;
    }

    internal StudioUx2EnvFApprovalSnapshot CaptureAudioEmittersApprovalUx2EnvF() => new(
        _approvedEmittersUx2EnvF,
        _approvedEmittersRevisionUx2EnvF,
        _approvedEmittersFingerprintUx2EnvF);

    internal void RestoreAudioEmittersApprovalUx2EnvF(StudioUx2EnvFApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedEmittersUx2EnvF = snapshot.Draft;
        _approvedEmittersRevisionUx2EnvF = snapshot.Revision;
        _approvedEmittersFingerprintUx2EnvF = snapshot.Fingerprint;
    }

    private bool TryBuildAudioEmittersUx2EnvF(
        StudioUx2EnvFDraft draft,
        bool dryRun,
        out SetAreaEnvironmentCommand? command,
        out AreaDefinition? sourceArea,
        out AreaEnvironmentProfile? candidateEnvironment,
        out IReadOnlyList<StudioUx2EnvFDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateEnvironment = null;
        diagnostics = [];
        message = string.Empty;
        var project = Project;
        var history = History;
        if (project is null || history is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez la zone avant de reconstruire les émetteurs.";
            return false;
        }
        var matches = project.Areas.Where(area => area.Id == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "La zone choisie n’existe plus ou n’est plus unique.";
            return false;
        }
        sourceArea = matches[0];
        if (!string.Equals(draft.SourceFingerprint,
                AudioEmitterSourceFingerprintUx2EnvF(sourceArea), StringComparison.Ordinal))
        {
            message = "La zone ou un profil voisin a changé. Rechargez la fiche avant l’aperçu.";
            return false;
        }

        var inputDiagnostics = ValidateAudioEmitterDraftUx2EnvF(draft, sourceArea).ToArray();
        if (inputDiagnostics.Length > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var sourceCatalog = sourceArea.Environment.Audio.Emitters;
            var sourceDefinitions = sourceCatalog.Definitions.ToDictionary(item => item.Id);
            var definitions = draft.Definitions.Select(definitionDraft =>
            {
                var sourceClips = sourceDefinitions.TryGetValue(definitionDraft.Id, out var sourceDefinition)
                    ? sourceDefinition.ClipSet.Clips.ToDictionary(item => item.Id)
                    : new Dictionary<Guid, AudioClipDefinition>();
                var clips = definitionDraft.Clips.Select(clipDraft =>
                {
                    var rebuilt = new AudioClipDefinition(
                        clipDraft.Id,
                        NormalizeResourceUx2EnvF(clipDraft.ResourcePath),
                        ExactFloatUx2EnvF(clipDraft.Weight),
                        clipDraft.Tags.Select(item => item.Trim()).ToArray(),
                        clipDraft.Variant.Trim(),
                        clipDraft.Locale.Trim(),
                        ExactFloatUx2EnvF(clipDraft.LoudnessLufs),
                        NormalizeResourceUx2EnvF(clipDraft.PreviewResourcePath),
                        clipDraft.MimeType.Trim(),
                        ExactLongUx2EnvF(clipDraft.MemoryBytes),
                        clipDraft.LicenseSpdx.Trim(),
                        clipDraft.Provenance.Trim(),
                        clipDraft.ContentSha256.Trim(),
                        clipDraft.Transcript,
                        clipDraft.WaveformPeaks.Select(ExactFloatUx2EnvF).ToArray(),
                        ExactFloatUx2EnvF(clipDraft.DurationSeconds));
                    return sourceClips.TryGetValue(clipDraft.Id, out var existingClip) &&
                           SameClipUx2EnvF(existingClip, rebuilt)
                        ? existingClip
                        : rebuilt;
                }).ToArray();
                var clipSet = new AudioClipSet(
                    definitionDraft.ClipSetId,
                    definitionDraft.ClipSetSchemaVersion,
                    definitionDraft.ClipSetName.Trim(),
                    definitionDraft.Selection,
                    clips,
                    definitionDraft.ClipSetProvenance.Trim());
                var loop = new AudioLoopPolicy(
                    definitionDraft.LoopMode,
                    ExactIntUx2EnvF(definitionDraft.RepeatCount),
                    ExactFloatUx2EnvF(definitionDraft.RegionStartSeconds),
                    ExactFloatUx2EnvF(definitionDraft.RegionEndSeconds),
                    ExactFloatUx2EnvF(definitionDraft.CrossfadeSeconds),
                    definitionDraft.StopEvent.Trim());
                var spatial = new AudioEmitterSpatialProfile(
                    ExactFloatUx2EnvF(definitionDraft.InnerRadius),
                    ExactFloatUx2EnvF(definitionDraft.OuterRadius),
                    definitionDraft.AttenuationCurve.Trim(),
                    ExactFloatUx2EnvF(definitionDraft.ConeAngleDegrees),
                    ExactFloatUx2EnvF(definitionDraft.ConeAttenuationDb),
                    ExactFloatUx2EnvF(definitionDraft.Occlusion),
                    ExactFloatUx2EnvF(definitionDraft.HeightOffset),
                    definitionDraft.VirtualizeOutsideRange);
                var rebuilt = new AudioEmitterDefinition(
                    definitionDraft.Id,
                    definitionDraft.SchemaVersion,
                    definitionDraft.Name.Trim(),
                    clipSet,
                    loop,
                    spatial,
                    definitionDraft.MixBusId,
                    ExactFloatUx2EnvF(definitionDraft.StartDelaySeconds),
                    definitionDraft.ActivatedScript.Trim(),
                    definitionDraft.CompletedScript.Trim(),
                    definitionDraft.Required,
                    definitionDraft.Provenance.Trim(),
                    definitionDraft.PackageManifest.Trim(),
                    definitionDraft.LicenseSpdx.Trim(),
                    definitionDraft.ContentSha256.Trim());
                return sourceDefinition is not null && SameDefinitionUx2EnvF(sourceDefinition, rebuilt)
                    ? sourceDefinition
                    : rebuilt;
            }).ToArray();
            var sourceInstances = sourceCatalog.Instances.ToDictionary(item => item.Id);
            var instances = draft.Instances.Select(item =>
            {
                var transform = new TransformValue(
                    new Vector3Value(ExactFloatUx2EnvF(item.PositionX), ExactFloatUx2EnvF(item.PositionY),
                        ExactFloatUx2EnvF(item.PositionZ)),
                    new QuaternionValue(ExactFloatUx2EnvF(item.RotationX), ExactFloatUx2EnvF(item.RotationY),
                        ExactFloatUx2EnvF(item.RotationZ), ExactFloatUx2EnvF(item.RotationW)),
                    new Vector3Value(ExactFloatUx2EnvF(item.ScaleX), ExactFloatUx2EnvF(item.ScaleY),
                        ExactFloatUx2EnvF(item.ScaleZ)));
                var rebuilt = new AudioEmitterInstance(item.Id, item.DefinitionId, transform,
                    item.Enabled, item.Provenance.Trim());
                return sourceInstances.TryGetValue(item.Id, out var existing) &&
                       SameInstanceUx2EnvF(existing, rebuilt)
                    ? existing
                    : rebuilt;
            }).ToArray();
            var catalog = new AudioEmitterCatalog(AudioEmitterCatalog.CurrentSchemaVersion, definitions, instances);
            if (SameCatalogUx2EnvF(sourceCatalog, catalog)) catalog = sourceCatalog;

            var sourceEnvironment = sourceArea.Environment;
            var sourceAudio = sourceEnvironment.Audio;
            var audio = ReferenceEquals(catalog, sourceCatalog)
                ? sourceAudio
                : new AreaAudioProfile(
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
                    sourceAudio.Spatial,
                    sourceAudio.RuntimeControl,
                    sourceAudio.AdaptiveLayers,
                    sourceAudio.Orchestration,
                    catalog,
                    sourceAudio.Authoring);
            var environment = ReferenceEquals(audio, sourceAudio)
                ? sourceEnvironment
                : new AreaEnvironmentProfile(
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
            var updatedArea = ReferenceEquals(environment, sourceEnvironment)
                ? sourceArea
                : sourceArea.WithEnvironment(environment);
            var candidateProject = ReplaceAreaUx2EnvF(project, sourceArea.Id, updatedArea);
            var projectDiagnostics = ProjectValidator.Validate(candidateProject).Select(item =>
                new StudioUx2EnvFDiagnostic(item.Code, item.Path,
                    HumanizeAudioEmitterDiagnosticUx2EnvF(item.Code))).ToArray();
            if (projectDiagnostics.Length > 0)
            {
                diagnostics = projectDiagnostics;
                message = projectDiagnostics[0].Message;
                return false;
            }
            candidateEnvironment = environment;
            command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                sourceArea.Id, environment, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or OverflowException)
        {
            var diagnostic = new StudioUx2EnvFDiagnostic(
                Ux2EnvFInstanceCode,
                $"areas/{draft.AreaId}/environment/audio/emitters",
                exception is OverflowException
                    ? "Une valeur numérique doit être finie et exactement représentable par le contrat canonique."
                    : "Le catalogue d’émetteurs ne peut pas être reconstruit avec ces valeurs.");
            diagnostics = [diagnostic];
            message = diagnostic.Message;
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2EnvFDiagnostic> ValidateAudioEmitterDraftUx2EnvF(
        StudioUx2EnvFDraft draft,
        AreaDefinition sourceArea)
    {
        var diagnostics = new List<StudioUx2EnvFDiagnostic>();
        var definitionIds = draft.Definitions.Select(item => item.Id).ToArray();
        var clipIds = draft.Definitions.SelectMany(item => item.Clips).Select(item => item.Id).ToArray();
        var busIds = sourceArea.Environment.Audio.Orchestration.MixBuses.Select(item => item.Id).ToHashSet();

        if (definitionIds.Any(id => id == Guid.Empty) || definitionIds.Distinct().Count() != definitionIds.Length ||
            draft.Definitions.Any(item => item.SchemaVersion != AudioEmitterDefinition.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.Name) || item.ClipSetId == Guid.Empty ||
                item.ClipSetSchemaVersion != AudioClipSet.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(item.ClipSetName) || string.IsNullOrWhiteSpace(item.ClipSetProvenance) ||
                !Enum.IsDefined(item.Selection) || string.IsNullOrWhiteSpace(item.Provenance) ||
                string.IsNullOrWhiteSpace(item.PackageManifest) || !ValidSpdxUx2EnvF(item.LicenseSpdx) ||
                !ValidSha256Ux2EnvF(item.ContentSha256) ||
                !TryFloatUx2EnvF(item.StartDelaySeconds, out var delay) || delay < 0 ||
                item.MixBusId is { } busId && !busIds.Contains(busId)))
            diagnostics.Add(new(Ux2EnvFDefinitionCode,
                $"areas/{draft.AreaId}/environment/audio/emitters/definitions",
                "Chaque émetteur requiert un nom, un clip-set, une provenance, un manifeste, une licence, " +
                "une empreinte et, s’il est choisi, un bus existant."));

        if (clipIds.Any(id => id == Guid.Empty) || clipIds.Distinct().Count() != clipIds.Length ||
            draft.Definitions.Any(item => item.Clips.Count == 0) ||
            draft.Definitions.SelectMany(item => item.Clips).Any(item =>
                !SafeResourceUx2EnvF(item.ResourcePath, allowEmpty: false) ||
                !TryFloatUx2EnvF(item.Weight, out var weight) || weight <= 0 ||
                string.IsNullOrWhiteSpace(item.Locale) ||
                !TryFloatUx2EnvF(item.LoudnessLufs, out var loudness) || loudness is < -120 or > 24 ||
                !SafeResourceUx2EnvF(item.PreviewResourcePath, allowEmpty: true) ||
                !item.MimeType.Trim().StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                !TryLongUx2EnvF(item.MemoryBytes, out var bytes) || bytes <= 0 ||
                !ValidSpdxUx2EnvF(item.LicenseSpdx) || string.IsNullOrWhiteSpace(item.Provenance) ||
                !ValidSha256Ux2EnvF(item.ContentSha256) ||
                !TryFloatUx2EnvF(item.DurationSeconds, out var duration) || duration < 0 ||
                item.WaveformPeaks.Any(peak => !TryFloatUx2EnvF(peak, out var value) || value is < 0 or > 1)))
            diagnostics.Add(new(Ux2EnvFClipCode,
                $"areas/{draft.AreaId}/environment/audio/emitters/definitions/clips",
                "Chaque clip requiert une ressource sûre, un poids et une mémoire positifs, une langue, " +
                "un niveau LUFS, un MIME audio, une licence, une provenance et une empreinte SHA-256."));

        if (draft.Definitions.Any(item => !Enum.IsDefined(item.LoopMode) ||
                !TryIntUx2EnvF(item.RepeatCount, out var repeat) || repeat < 0 ||
                item.LoopMode == AudioLoopMode.FiniteCount && repeat < 1 ||
                !TryFloatUx2EnvF(item.RegionStartSeconds, out var start) || start < 0 ||
                !TryFloatUx2EnvF(item.RegionEndSeconds, out var end) || end < 0 ||
                item.LoopMode == AudioLoopMode.SeamlessRegion && end <= start ||
                !TryFloatUx2EnvF(item.CrossfadeSeconds, out var crossfade) || crossfade < 0 ||
                item.LoopMode == AudioLoopMode.SeamlessRegion && crossfade * 2 > end - start ||
                item.LoopMode == AudioLoopMode.EventDriven && string.IsNullOrWhiteSpace(item.StopEvent)))
            diagnostics.Add(new(Ux2EnvFLoopCode,
                $"areas/{draft.AreaId}/environment/audio/emitters/definitions/loop",
                "La boucle requiert un compte, une région, un fondu et, en mode événementiel, " +
                "un événement d’arrêt cohérents."));

        if (draft.Definitions.Any(item =>
                !TryFloatUx2EnvF(item.InnerRadius, out var inner) || inner < 0 ||
                !TryFloatUx2EnvF(item.OuterRadius, out var outer) || outer <= inner ||
                string.IsNullOrWhiteSpace(item.AttenuationCurve) ||
                !TryFloatUx2EnvF(item.ConeAngleDegrees, out var cone) || cone is < 1 or > 360 ||
                !TryFloatUx2EnvF(item.ConeAttenuationDb, out var coneDb) || coneDb is < -80 or > 0 ||
                !TryFloatUx2EnvF(item.Occlusion, out var occlusion) || occlusion is < 0 or > 1 ||
                !TryFloatUx2EnvF(item.HeightOffset, out _)))
            diagnostics.Add(new(Ux2EnvFSpatialCode,
                $"areas/{draft.AreaId}/environment/audio/emitters/definitions/spatial",
                "La portée, la courbe, le cône, l’occlusion et la hauteur 3D doivent être finies et cohérentes."));

        var instanceIds = draft.Instances.Select(item => item.Id).ToArray();
        if (instanceIds.Any(id => id == Guid.Empty) || instanceIds.Distinct().Count() != instanceIds.Length ||
            draft.Instances.Any(item => !definitionIds.Contains(item.DefinitionId) ||
                string.IsNullOrWhiteSpace(item.Provenance) ||
                !ValidTransformNumberUx2EnvF(item.PositionX) || !ValidTransformNumberUx2EnvF(item.PositionY) ||
                !ValidTransformNumberUx2EnvF(item.PositionZ) || !ValidTransformNumberUx2EnvF(item.RotationX) ||
                !ValidTransformNumberUx2EnvF(item.RotationY) || !ValidTransformNumberUx2EnvF(item.RotationZ) ||
                !ValidTransformNumberUx2EnvF(item.RotationW) ||
                !TryFloatUx2EnvF(item.ScaleX, out var scaleX) || scaleX <= 0 ||
                !TryFloatUx2EnvF(item.ScaleY, out var scaleY) || scaleY <= 0 ||
                !TryFloatUx2EnvF(item.ScaleZ, out var scaleZ) || scaleZ <= 0))
            diagnostics.Add(new(Ux2EnvFInstanceCode,
                $"areas/{draft.AreaId}/environment/audio/emitters/instances",
                "Chaque instance requiert un émetteur existant, une position, une rotation et une échelle 3D " +
                "finies, ainsi qu’une provenance."));
        return diagnostics;
    }

    private static StudioUx2EnvFDiff BuildAudioEmitterDiffUx2EnvF(
        AudioEmitterCatalog source,
        AudioEmitterCatalog candidate)
    {
        if (SameCatalogUx2EnvF(source, candidate)) return new([]);
        var entries = new List<string>();
        var sourceDefinitions = source.Definitions.ToDictionary(item => item.Id);
        var candidateDefinitions = candidate.Definitions.ToDictionary(item => item.Id);
        foreach (var id in sourceDefinitions.Keys.Union(candidateDefinitions.Keys))
        {
            if (!sourceDefinitions.TryGetValue(id, out var before))
                entries.Add($"Émetteur « {HumanTokenUx2EnvF(candidateDefinitions[id].Name)} » ajouté.");
            else if (!candidateDefinitions.TryGetValue(id, out var after))
                entries.Add($"Émetteur « {HumanTokenUx2EnvF(before.Name)} » retiré.");
            else if (!SameDefinitionUx2EnvF(before, after))
                entries.Add($"Émetteur « {HumanTokenUx2EnvF(after.Name)} » modifié · " +
                            $"{before.ClipSet.Clips.Count} → {after.ClipSet.Clips.Count} clip(s).");
        }
        if (!SameInstancesUx2EnvF(source.Instances, candidate.Instances))
            entries.Add($"Instances 3D : {source.Instances.Count} → {candidate.Instances.Count}.");
        if (!source.Definitions.SelectMany(item => item.ClipSet.Clips).Select(item => item.Id)
                .SequenceEqual(candidate.Definitions.SelectMany(item => item.ClipSet.Clips).Select(item => item.Id)))
            entries.Add("Ordre des clips modifié; la sélection ordonnée suivra ce nouvel ordre.");
        return new(entries);
    }

    private static IReadOnlyList<StudioUx2EnvFDiagnosticGroup> GroupAudioEmitterDiagnosticsUx2EnvF(
        IReadOnlyList<StudioUx2EnvFDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            ("definitions", "Émetteurs"),
            ("clips", "Clips et provenance"),
            ("loops", "Boucles et spatialisation"),
            ("instances", "Instances 3D"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item =>
                AudioEmitterDiagnosticGroupUx2EnvF(item) == definition.Item1).ToArray();
            return new StudioUx2EnvFDiagnosticGroup(
                definition.Item1,
                definition.Item2,
                items.Any(item => item.Blocking) ? "à corriger" :
                items.Length > 0 ? "avertissement" : "conforme",
                items);
        }).ToArray();
    }

    private static string AudioEmitterDiagnosticGroupUx2EnvF(StudioUx2EnvFDiagnostic diagnostic)
    {
        if (diagnostic.Code is Ux2EnvFClipCode or AudioEmitterCatalogValidator.ClipInvalidCode or
            AudioEmitterCatalogValidator.ClipSetInvalidCode or AudioEmitterCatalogValidator.ClipDuplicateCode ||
            diagnostic.Path.Contains("clip", StringComparison.OrdinalIgnoreCase)) return "clips";
        if (diagnostic.Code is Ux2EnvFLoopCode or Ux2EnvFSpatialCode or
            AudioEmitterCatalogValidator.LoopInvalidCode or AudioEmitterCatalogValidator.SpatialInvalidCode ||
            diagnostic.Path.Contains("loop", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Path.Contains("spatial", StringComparison.OrdinalIgnoreCase)) return "loops";
        if (diagnostic.Code is Ux2EnvFInstanceCode or AudioEmitterCatalogValidator.InstanceInvalidCode or
            AudioEmitterCatalogValidator.InstanceDuplicateCode or AudioEmitterCatalogValidator.DefinitionMissingCode ||
            diagnostic.Path.Contains("instances", StringComparison.OrdinalIgnoreCase)) return "instances";
        return "definitions";
    }

    private static StudioUx2EnvFDiagnostic CommandDiagnosticUx2EnvF(
        CommandDiagnostic diagnostic,
        string fallbackPath)
    {
        var separator = diagnostic.Message.IndexOf(" · ", StringComparison.Ordinal);
        var path = separator > 0 ? diagnostic.Message[..separator] : fallbackPath;
        return new(diagnostic.Code, path, HumanizeAudioEmitterDiagnosticUx2EnvF(diagnostic.Code));
    }

    private static string HumanizeAudioEmitterDiagnosticUx2EnvF(string? code) => code switch
    {
        Ux2EnvFDefinitionCode or AudioEmitterCatalogValidator.DefinitionInvalidCode or
            AudioEmitterCatalogValidator.DefinitionDuplicateCode or AudioEmitterCatalogValidator.SchemaUnsupportedCode =>
            "La définition d’émetteur, son identité ou sa provenance n’est pas valide.",
        Ux2EnvFClipCode or AudioEmitterCatalogValidator.ClipInvalidCode or
            AudioEmitterCatalogValidator.ClipDuplicateCode or AudioEmitterCatalogValidator.ClipSetInvalidCode =>
            "Un clip, son ordre ou ses métadonnées gouvernées n’est pas valide.",
        Ux2EnvFLoopCode or AudioEmitterCatalogValidator.LoopInvalidCode =>
            "La politique de lecture unique, répétée ou événementielle n’est pas valide.",
        Ux2EnvFSpatialCode or AudioEmitterCatalogValidator.SpatialInvalidCode =>
            "La portée, le cône, l’occlusion ou la hauteur de l’émetteur n’est pas valide.",
        Ux2EnvFInstanceCode or AudioEmitterCatalogValidator.InstanceInvalidCode or
            AudioEmitterCatalogValidator.InstanceDuplicateCode or AudioEmitterCatalogValidator.DefinitionMissingCode =>
            "Une instance 3D ou sa référence d’émetteur n’est pas valide.",
        AudioEmitterCatalogValidator.MixBusMissingCode =>
            "Le bus de mixage choisi n’existe plus dans cette zone.",
        AudioEmitterCatalogValidator.MemoryBudgetCode =>
            "La mémoire déclarée par les clips dépasse le budget audio canonique.",
        "command.revision_conflict" =>
            "Le projet a changé : rechargez la fiche et relancez l’aperçu sec.",
        _ => "La validation canonique du projet a refusé le catalogue d’émetteurs audio.",
    };

    private static StudioUx2EnvFPreview FailedAudioEmittersPreviewUx2EnvF(string message) => new(
        false,
        message,
        string.Empty,
        false,
        new([]),
        GroupAudioEmitterDiagnosticsUx2EnvF([]),
        [],
        false,
        false,
        true,
        true,
        true);

    private static bool SameCatalogUx2EnvF(AudioEmitterCatalog source, AudioEmitterCatalog candidate) =>
        source.SchemaVersion == candidate.SchemaVersion &&
        source.Definitions.Count == candidate.Definitions.Count &&
        source.Definitions.Zip(candidate.Definitions).All(pair => SameDefinitionUx2EnvF(pair.First, pair.Second)) &&
        SameInstancesUx2EnvF(source.Instances, candidate.Instances);

    private static bool SameDefinitionUx2EnvF(AudioEmitterDefinition source, AudioEmitterDefinition candidate) =>
        source.Id == candidate.Id && source.SchemaVersion == candidate.SchemaVersion &&
        string.Equals(source.Name, candidate.Name, StringComparison.Ordinal) &&
        SameClipSetUx2EnvF(source.ClipSet, candidate.ClipSet) && source.Loop == candidate.Loop &&
        source.Spatial == candidate.Spatial && source.MixBusId == candidate.MixBusId &&
        source.StartDelaySeconds.Equals(candidate.StartDelaySeconds) &&
        string.Equals(source.ActivatedScript, candidate.ActivatedScript, StringComparison.Ordinal) &&
        string.Equals(source.CompletedScript, candidate.CompletedScript, StringComparison.Ordinal) &&
        source.Required == candidate.Required &&
        string.Equals(source.Provenance, candidate.Provenance, StringComparison.Ordinal) &&
        string.Equals(source.PackageManifest, candidate.PackageManifest, StringComparison.Ordinal) &&
        string.Equals(source.LicenseSpdx, candidate.LicenseSpdx, StringComparison.Ordinal) &&
        string.Equals(source.ContentSha256, candidate.ContentSha256, StringComparison.Ordinal);

    private static bool SameClipSetUx2EnvF(AudioClipSet source, AudioClipSet candidate) =>
        source.Id == candidate.Id && source.SchemaVersion == candidate.SchemaVersion &&
        string.Equals(source.Name, candidate.Name, StringComparison.Ordinal) &&
        source.Selection == candidate.Selection &&
        string.Equals(source.Provenance, candidate.Provenance, StringComparison.Ordinal) &&
        source.Clips.Count == candidate.Clips.Count &&
        source.Clips.Zip(candidate.Clips).All(pair => SameClipUx2EnvF(pair.First, pair.Second));

    private static bool SameClipUx2EnvF(AudioClipDefinition source, AudioClipDefinition candidate) =>
        source.Id == candidate.Id &&
        string.Equals(source.ResourcePath, candidate.ResourcePath, StringComparison.Ordinal) &&
        source.Weight.Equals(candidate.Weight) && source.Tags.SequenceEqual(candidate.Tags, StringComparer.Ordinal) &&
        string.Equals(source.Variant, candidate.Variant, StringComparison.Ordinal) &&
        string.Equals(source.Locale, candidate.Locale, StringComparison.Ordinal) &&
        source.LoudnessLufs.Equals(candidate.LoudnessLufs) &&
        string.Equals(source.PreviewResourcePath, candidate.PreviewResourcePath, StringComparison.Ordinal) &&
        string.Equals(source.MimeType, candidate.MimeType, StringComparison.Ordinal) &&
        source.MemoryBytes == candidate.MemoryBytes &&
        string.Equals(source.LicenseSpdx, candidate.LicenseSpdx, StringComparison.Ordinal) &&
        string.Equals(source.Provenance, candidate.Provenance, StringComparison.Ordinal) &&
        string.Equals(source.ContentSha256, candidate.ContentSha256, StringComparison.Ordinal) &&
        string.Equals(source.Transcript, candidate.Transcript, StringComparison.Ordinal) &&
        (source.WaveformPeaks ?? []).SequenceEqual(candidate.WaveformPeaks ?? []) &&
        source.DurationSeconds.Equals(candidate.DurationSeconds);

    private static bool SameInstancesUx2EnvF(
        IReadOnlyList<AudioEmitterInstance> source,
        IReadOnlyList<AudioEmitterInstance> candidate) =>
        source.Count == candidate.Count &&
        source.Zip(candidate).All(pair => SameInstanceUx2EnvF(pair.First, pair.Second));

    private static bool SameInstanceUx2EnvF(AudioEmitterInstance source, AudioEmitterInstance candidate) =>
        source.Id == candidate.Id && source.DefinitionId == candidate.DefinitionId &&
        source.Transform == candidate.Transform && source.Enabled == candidate.Enabled &&
        string.Equals(source.Provenance, candidate.Provenance, StringComparison.Ordinal);

    private static string AudioEmitterSourceFingerprintUx2EnvF(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string AudioEmitterDraftFingerprintUx2EnvF(StudioUx2EnvFDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString());
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint ?? string.Empty);
            writer.Write(draft.Definitions.Count);
            foreach (var item in draft.Definitions)
            {
                writer.Write(item.Id.ToByteArray());
                writer.Write(item.SchemaVersion);
                writer.Write(item.Name ?? string.Empty);
                writer.Write(item.ClipSetId.ToByteArray());
                writer.Write(item.ClipSetSchemaVersion);
                writer.Write(item.ClipSetName ?? string.Empty);
                writer.Write((int)item.Selection);
                writer.Write(item.ClipSetProvenance ?? string.Empty);
                writer.Write(item.Clips.Count);
                foreach (var clip in item.Clips)
                {
                    writer.Write(clip.Id.ToByteArray());
                    writer.Write(clip.ResourcePath ?? string.Empty);
                    writer.Write(clip.Weight ?? string.Empty);
                    writer.Write(clip.Tags.Count);
                    foreach (var tag in clip.Tags) writer.Write(tag ?? string.Empty);
                    writer.Write(clip.Variant ?? string.Empty);
                    writer.Write(clip.Locale ?? string.Empty);
                    writer.Write(clip.LoudnessLufs ?? string.Empty);
                    writer.Write(clip.PreviewResourcePath ?? string.Empty);
                    writer.Write(clip.MimeType ?? string.Empty);
                    writer.Write(clip.MemoryBytes ?? string.Empty);
                    writer.Write(clip.LicenseSpdx ?? string.Empty);
                    writer.Write(clip.Provenance ?? string.Empty);
                    writer.Write(clip.ContentSha256 ?? string.Empty);
                    writer.Write(clip.Transcript ?? string.Empty);
                    writer.Write(clip.WaveformPeaks.Count);
                    foreach (var peak in clip.WaveformPeaks) writer.Write(peak ?? string.Empty);
                    writer.Write(clip.DurationSeconds ?? string.Empty);
                }
                writer.Write((int)item.LoopMode);
                writer.Write(item.RepeatCount ?? string.Empty);
                writer.Write(item.RegionStartSeconds ?? string.Empty);
                writer.Write(item.RegionEndSeconds ?? string.Empty);
                writer.Write(item.CrossfadeSeconds ?? string.Empty);
                writer.Write(item.StopEvent ?? string.Empty);
                writer.Write(item.InnerRadius ?? string.Empty);
                writer.Write(item.OuterRadius ?? string.Empty);
                writer.Write(item.AttenuationCurve ?? string.Empty);
                writer.Write(item.ConeAngleDegrees ?? string.Empty);
                writer.Write(item.ConeAttenuationDb ?? string.Empty);
                writer.Write(item.Occlusion ?? string.Empty);
                writer.Write(item.HeightOffset ?? string.Empty);
                writer.Write(item.VirtualizeOutsideRange);
                writer.Write(item.MixBusId.HasValue);
                if (item.MixBusId is { } busId) writer.Write(busId.ToByteArray());
                writer.Write(item.StartDelaySeconds ?? string.Empty);
                writer.Write(item.ActivatedScript ?? string.Empty);
                writer.Write(item.CompletedScript ?? string.Empty);
                writer.Write(item.Required);
                writer.Write(item.Provenance ?? string.Empty);
                writer.Write(item.PackageManifest ?? string.Empty);
                writer.Write(item.LicenseSpdx ?? string.Empty);
                writer.Write(item.ContentSha256 ?? string.Empty);
            }
            writer.Write(draft.Instances.Count);
            foreach (var item in draft.Instances)
            {
                writer.Write(item.Id.ToByteArray());
                writer.Write(item.DefinitionId.ToByteArray());
                writer.Write(item.PositionX ?? string.Empty);
                writer.Write(item.PositionY ?? string.Empty);
                writer.Write(item.PositionZ ?? string.Empty);
                writer.Write(item.RotationX ?? string.Empty);
                writer.Write(item.RotationY ?? string.Empty);
                writer.Write(item.RotationZ ?? string.Empty);
                writer.Write(item.RotationW ?? string.Empty);
                writer.Write(item.ScaleX ?? string.Empty);
                writer.Write(item.ScaleY ?? string.Empty);
                writer.Write(item.ScaleZ ?? string.Empty);
                writer.Write(item.Enabled);
                writer.Write(item.Provenance ?? string.Empty);
            }
            writer.Write(draft.MixBuses.Count);
            foreach (var item in draft.MixBuses)
            {
                writer.Write(item.Id.HasValue);
                if (item.Id is { } id) writer.Write(id.ToByteArray());
                writer.Write(item.Label ?? string.Empty);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool TryFloatUx2EnvF(string? text, out float value)
    {
        value = 0;
        if (!TryDoubleUx2EnvF(text, out var parsed) || parsed < -float.MaxValue || parsed > float.MaxValue)
            return false;
        value = (float)parsed;
        return float.IsFinite(value);
    }

    private static bool TryDoubleUx2EnvF(string? text, out double value)
    {
        var normalized = (text ?? string.Empty).Trim();
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            double.IsFinite(value)) return true;
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
               double.IsFinite(value);
    }

    private static bool TryIntUx2EnvF(string? text, out int value) =>
        int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value);

    private static bool TryLongUx2EnvF(string? text, out long value) =>
        long.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value);

    private static float ExactFloatUx2EnvF(string? text)
    {
        if (!TryFloatUx2EnvF(text, out var value))
            throw new OverflowException("The value is not a finite single.");
        return value;
    }

    private static int ExactIntUx2EnvF(string? text)
    {
        if (!TryIntUx2EnvF(text, out var value))
            throw new OverflowException("The value is not an exact 32-bit integer.");
        return value;
    }

    private static long ExactLongUx2EnvF(string? text)
    {
        if (!TryLongUx2EnvF(text, out var value))
            throw new OverflowException("The value is not an exact 64-bit integer.");
        return value;
    }

    private static bool ValidTransformNumberUx2EnvF(string? text) => TryFloatUx2EnvF(text, out _);

    private static string InvariantUx2EnvF(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string InvariantUx2EnvF(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string InvariantUx2EnvF(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool ValidSpdxUx2EnvF(string? value) => !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), "UNLICENSED", StringComparison.OrdinalIgnoreCase) &&
        value.Trim().All(character => char.IsAsciiLetterOrDigit(character) ||
                                      character is '-' or '.' or '+' or '(' or ')' or ' ');

    private static bool ValidSha256Ux2EnvF(string? value) => value is { Length: 64 } &&
        value.All(char.IsAsciiHexDigit);

    private static bool SafeResourceUx2EnvF(string? value, bool allowEmpty)
    {
        var normalized = NormalizeResourceUx2EnvF(value);
        if (normalized.Length == 0) return allowEmpty;
        if (normalized.StartsWith("builtin://", StringComparison.Ordinal))
            return normalized.Length > "builtin://".Length;
        return !normalized.Contains("://", StringComparison.Ordinal) &&
               !Path.IsPathRooted(normalized) &&
               !normalized.Split('/').Any(segment => segment == "..");
    }

    private static string NormalizeResourceUx2EnvF(string? value) =>
        (value ?? string.Empty).Trim().Replace('\\', '/');

    private static string HumanClipUx2EnvF(AudioClipDefinition clip)
    {
        var candidate = clip.Variant.Trim();
        if (candidate.Length == 0)
            candidate = Path.GetFileNameWithoutExtension(clip.ResourcePath.Replace('\\', '/'));
        return HumanTokenUx2EnvF(candidate);
    }

    private static string HumanLoopUx2EnvF(AudioLoopMode mode) => mode switch
    {
        AudioLoopMode.OneShot => "lecture unique",
        AudioLoopMode.Loop => "boucle continue",
        AudioLoopMode.SeamlessRegion => "boucle de région avec fondu",
        AudioLoopMode.FiniteCount => "répétition finie",
        AudioLoopMode.EventDriven => "arrêt piloté par événement",
        _ => "mode inconnu",
    };

    private static string HumanTokenUx2EnvF(string? value)
    {
        var normalized = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(normalized) || ContainsTechnicalIdentifierUx2EnvF(normalized)
            ? "élément sans nom lisible"
            : normalized;
    }

    private static bool ContainsTechnicalIdentifierUx2EnvF(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static ProjectDefinition ReplaceAreaUx2EnvF(
        ProjectDefinition project,
        AreaId areaId,
        AreaDefinition replacement) => new(
        project.Id,
        project.Title,
        project.Module,
        project.Blueprints,
        project.Areas.Select(area => area.Id == areaId ? replacement : area).ToArray(),
        project.Dialogues,
        project.Quests);

    internal static ProjectDefinition CreateUx2EnvFContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var area = source.Areas.FirstOrDefault() ??
                   throw new InvalidOperationException("The contract project requires an area.");
        var definitionId = Guid.Parse("a004f000-0000-4000-8000-000000000001");
        var catalog = new AudioEmitterCatalog(AudioEmitterCatalog.CurrentSchemaVersion,
        [
            new AudioEmitterDefinition(
                definitionId,
                AudioEmitterDefinition.CurrentSchemaVersion,
                "Cascade ancienne",
                new AudioClipSet(
                    Guid.Parse("a004f000-0000-4000-8000-000000000002"),
                    AudioClipSet.CurrentSchemaVersion,
                    "Variantes de cascade",
                    AudioClipSelectionMode.Weighted,
                    [
                        new AudioClipDefinition(
                            Guid.Parse("a004f000-0000-4000-8000-000000000003"),
                            "builtin://openrpg/audio/waterfall-close",
                            2,
                            ["eau", "ambiance"],
                            "Proche",
                            "fr",
                            -18,
                            "builtin://openrpg/audio/waterfall-preview",
                            "audio/ogg",
                            1_048_576,
                            "CC0-1.0",
                            "studio:ux2-env-f",
                            new string('a', 64),
                            "[Cascade proche]",
                            [.1f, .35f, .8f, .4f],
                            12.5f),
                        new AudioClipDefinition(
                            Guid.Parse("a004f000-0000-4000-8000-000000000004"),
                            "builtin://openrpg/audio/waterfall-far",
                            1,
                            ["eau", "ambiance"],
                            "Lointaine",
                            "fr",
                            -22,
                            string.Empty,
                            "audio/ogg",
                            786_432,
                            "CC0-1.0",
                            "studio:ux2-env-f",
                            new string('b', 64),
                            "[Cascade lointaine]",
                            [.05f, .2f, .5f, .25f],
                            14),
                    ],
                    "studio:ux2-env-f"),
                new AudioLoopPolicy(AudioLoopMode.SeamlessRegion, 0, 1, 10, .5f, string.Empty),
                new AudioEmitterSpatialProfile(2, 48, "inverse-square", 210, -12, .25f, 1.5f, true),
                null,
                .25f,
                "scripts/audio/waterfall-enter.forge",
                "scripts/audio/waterfall-exit.forge",
                true,
                "studio:ux2-env-f",
                "assets/audio-manifest.json",
                "CC0-1.0",
                new string('c', 64)),
        ],
        [
            new AudioEmitterInstance(
                Guid.Parse("a004f000-0000-4000-8000-000000000005"),
                definitionId,
                new TransformValue(new Vector3Value(8, 2, -5), QuaternionValue.Identity, Vector3Value.One),
                true,
                "studio:ux2-env-f"),
        ]);
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
            sourceAudio.Spatial,
            sourceAudio.RuntimeControl,
            sourceAudio.AdaptiveLayers,
            sourceAudio.Orchestration,
            catalog,
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
        return ReplaceAreaUx2EnvF(source, area.Id, area.WithEnvironment(environment));
    }

    internal StudioUx2EnvFSmokeResult RunUx2EnvFContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0) return FailedUx2EnvFSmoke();
        var sourceArea = sourceProject.Areas[0];
        var sourceEnvironment = sourceArea.Environment;
        var sourceAudio = sourceEnvironment.Audio;
        var emptyAudio = new AreaAudioProfile(
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
            sourceAudio.Spatial,
            sourceAudio.RuntimeControl,
            sourceAudio.AdaptiveLayers,
            sourceAudio.Orchestration,
            AudioEmitterCatalog.Empty,
            sourceAudio.Authoring);
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
        var emptyDraft = CreateAudioEmittersDraftUx2EnvF(
            new ProjectRevision(0), sourceArea.WithEnvironment(emptyEnvironment));
        var emptySourceSupported = emptyDraft.Definitions.Count == 0 && emptyDraft.Instances.Count == 0;

        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-f-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, CreateUx2EnvFContractProject(sourceProject));
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded) return FailedUx2EnvFSmoke();
            var target = editing.Project!.Areas[0];
            var explicitSelection = editing.CreateAudioEmittersDraftUx2EnvF() is null &&
                                    editing.CreateAudioEmittersDraftUx2EnvF(target.Id) is not null;
            var baseDraft = editing.CreateAudioEmittersDraftUx2EnvF(target.Id)!;

            var invalidDefinition = editing.PreviewAudioEmittersUx2EnvF(baseDraft with
            {
                Definitions = baseDraft.Definitions.Select((item, index) => index == 0
                    ? item with { Name = string.Empty }
                    : item).ToArray(),
            });
            var invalidClip = editing.PreviewAudioEmittersUx2EnvF(baseDraft with
            {
                Definitions = baseDraft.Definitions.Select((item, index) => index == 0
                    ? item with
                    {
                        Clips = item.Clips.Select((clip, clipIndex) => clipIndex == 0
                            ? clip with { ResourcePath = "../escape.ogg", LicenseSpdx = "UNLICENSED" }
                            : clip).ToArray(),
                    }
                    : item).ToArray(),
            });
            var invalidLoop = editing.PreviewAudioEmittersUx2EnvF(baseDraft with
            {
                Definitions = baseDraft.Definitions.Select((item, index) => index == 0
                    ? item with
                    {
                        LoopMode = AudioLoopMode.SeamlessRegion,
                        RegionStartSeconds = "4",
                        RegionEndSeconds = "4",
                    }
                    : item).ToArray(),
            });
            var invalidSpatial = editing.PreviewAudioEmittersUx2EnvF(baseDraft with
            {
                Definitions = baseDraft.Definitions.Select((item, index) => index == 0
                    ? item with { InnerRadius = "8", OuterRadius = "8" }
                    : item).ToArray(),
            });
            var invalidInstance = editing.PreviewAudioEmittersUx2EnvF(baseDraft with
            {
                Instances = baseDraft.Instances.Select((item, index) => index == 0
                    ? item with { DefinitionId = Guid.NewGuid(), ScaleX = "0" }
                    : item).ToArray(),
            });

            var history = editing.History!;
            var projectBeforeDryRun = editing.Project;
            var revisionBeforeDryRun = history.State.Revision;
            var journalBeforeDryRun = history.Journal.ToArray();
            var dirtyBeforeDryRun = editing.IsDirty;
            var undoBeforeDryRun = history.CanUndo;
            var redoBeforeDryRun = history.CanRedo;
            var dryDraft = baseDraft with
            {
                Definitions = baseDraft.Definitions.Select((item, index) => index == 0
                    ? item with
                    {
                        Name = item.Name + " révisée",
                        Clips = item.Clips.Reverse().ToArray(),
                        LoopMode = AudioLoopMode.FiniteCount,
                        RepeatCount = "3",
                    }
                    : item).ToArray(),
                Instances = baseDraft.Instances.Select((item, index) => index == 0
                    ? item with { PositionX = "9.5" }
                    : item).ToArray(),
            };
            var dryPreview = editing.PreviewAudioEmittersUx2EnvF(dryDraft);
            var dryStatePreserved = ReferenceEquals(projectBeforeDryRun, editing.Project) &&
                                    history.State.Revision == revisionBeforeDryRun &&
                                    history.Journal.SequenceEqual(journalBeforeDryRun) &&
                                    editing.IsDirty == dirtyBeforeDryRun &&
                                    history.CanUndo == undoBeforeDryRun && history.CanRedo == redoBeforeDryRun;
            var canonicalDryRun = dryPreview.Succeeded && dryPreview.CanApply &&
                                  dryPreview.CanonicalDryRun && dryStatePreserved;
            var deterministicPlans = dryPreview.DeterministicPlansBuilt && dryPreview.PlaybackPlans.Count == 1 &&
                                     dryPreview.PlaybackPlans[0].Loop == "répétition finie" &&
                                     dryPreview.PlaybackPlans[0].Position.Contains("X 9", StringComparison.Ordinal);
            var noLivePlaybackClaim = dryPreview.PlayerInstantiationSkipped &&
                                      dryPreview.ResourceLoadingSkipped && dryPreview.RuntimeClaimsBounded &&
                                      dryPreview.Summary.Contains("aucun AudioStreamPlayer3D", StringComparison.Ordinal) &&
                                      dryPreview.Summary.Contains("runtime Godot", StringComparison.Ordinal);

            var sourceGuard = editing.PreviewAudioEmittersUx2EnvF(baseDraft with
            {
                SourceFingerprint = new string('0', 64),
            });
            var staleDraft = editing.CreateAudioEmittersDraftUx2EnvF(target.Id)!;
            var staleMutation = ApplyUx2EnvFRevisionMutation(editing, target.Id, "draft");
            var stalePreview = editing.PreviewAudioEmittersUx2EnvF(staleDraft);
            var stalePreviewBlocked = staleMutation && !stalePreview.Succeeded && editing.Undo().Succeeded;
            var staleApprovalDraft = editing.CreateAudioEmittersDraftUx2EnvF(target.Id)!;
            staleApprovalDraft = staleApprovalDraft with
            {
                Definitions = staleApprovalDraft.Definitions.Select((item, index) => index == 0
                    ? item with { Name = item.Name + " approbation" }
                    : item).ToArray(),
            };
            var staleApprovalPreview = editing.PreviewAudioEmittersUx2EnvF(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2EnvFRevisionMutation(editing, target.Id, "approval");
            var staleApprovalApply = editing.ApplyApprovedAudioEmittersUx2EnvF();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;
            var staleRevisionBlocked = !sourceGuard.Succeeded && stalePreviewBlocked && staleApprovalBlocked;

            var mutableDefinitions = editing.CreateAudioEmittersDraftUx2EnvF(target.Id)!.Definitions.ToArray();
            var mutableDraft = editing.CreateAudioEmittersDraftUx2EnvF(target.Id)! with
            {
                Definitions = mutableDefinitions,
            };
            mutableDefinitions[0] = mutableDefinitions[0] with { Name = mutableDefinitions[0].Name + " A" };
            var mutablePreview = editing.PreviewAudioEmittersUx2EnvF(mutableDraft);
            mutableDefinitions[0] = mutableDefinitions[0] with { Name = mutableDefinitions[0].Name + " B" };
            var fingerprintApply = editing.ApplyApprovedAudioEmittersUx2EnvF();
            var approvalFingerprintBlocked = mutablePreview.Succeeded && !fingerprintApply.Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var sourceCatalog = sourceTarget.Environment.Audio.Emitters;
            var fresh = editing.CreateAudioEmittersDraftUx2EnvF(target.Id)!;
            fresh = fresh with
            {
                Definitions = fresh.Definitions.Select((item, index) => index == 0
                    ? item with
                    {
                        Name = item.Name + " · scène nord",
                        Clips = item.Clips.Reverse().Select((clip, clipIndex) => clipIndex == 0
                            ? clip with { Weight = "2.5" }
                            : clip).ToArray(),
                        LoopMode = AudioLoopMode.FiniteCount,
                        RepeatCount = "3",
                        RegionStartSeconds = "0",
                        RegionEndSeconds = "0",
                        CrossfadeSeconds = "0",
                    }
                    : item).ToArray(),
                Instances = fresh.Instances.Select((item, index) => index == 0
                    ? item with { PositionX = "11", PositionY = "3.25", PositionZ = "-7" }
                    : item).ToArray(),
            };
            var preview = editing.PreviewAudioEmittersUx2EnvF(fresh);
            var apply = editing.ApplyApprovedAudioEmittersUx2EnvF();
            var finalProject = editing.Project!;
            var finalTarget = finalProject.Areas.Single(area => area.Id == target.Id);
            var finalCatalog = finalTarget.Environment.Audio.Emitters;
            var journalTail = editing.History!.Journal.LastOrDefault();
            var applied = preview.Succeeded && preview.CanApply && apply.Succeeded &&
                          journalTail?.CommandName == "set_area_environment";

            var expectedOrder = sourceCatalog.Definitions[0].ClipSet.Clips.Select(item => item.Id).Reverse();
            var clipOrderPreserved = expectedOrder.SequenceEqual(
                finalCatalog.Definitions[0].ClipSet.Clips.Select(item => item.Id));
            var hiddenClipTailsPreserved = sourceCatalog.Definitions[0].ClipSet.Clips.All(sourceClip =>
            {
                var finalClip = finalCatalog.Definitions[0].ClipSet.Clips.Single(item => item.Id == sourceClip.Id);
                return string.Equals(sourceClip.Transcript, finalClip.Transcript, StringComparison.Ordinal) &&
                       (sourceClip.WaveformPeaks ?? []).SequenceEqual(finalClip.WaveformPeaks ?? []) &&
                       sourceClip.DurationSeconds.Equals(finalClip.DurationSeconds);
            });
            var sourceAudioAfterGuards = sourceTarget.Environment.Audio;
            var finalAudio = finalTarget.Environment.Audio;
            var audioTailsPreserved =
                string.Equals(sourceAudioAfterGuards.DayMusic, finalAudio.DayMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudioAfterGuards.NightMusic, finalAudio.NightMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudioAfterGuards.CombatMusic, finalAudio.CombatMusic, StringComparison.Ordinal) &&
                string.Equals(sourceAudioAfterGuards.Ambience, finalAudio.Ambience, StringComparison.Ordinal) &&
                string.Equals(sourceAudioAfterGuards.ReverbPreset, finalAudio.ReverbPreset, StringComparison.Ordinal) &&
                sourceAudioAfterGuards.TransitionSeconds.Equals(finalAudio.TransitionSeconds) &&
                sourceAudioAfterGuards.Playlist.SequenceEqual(finalAudio.Playlist, StringComparer.Ordinal) &&
                sourceAudioAfterGuards.OrderMode == finalAudio.OrderMode &&
                sourceAudioAfterGuards.IntervalSeconds.Equals(finalAudio.IntervalSeconds) &&
                sourceAudioAfterGuards.IntervalVariationSeconds.Equals(finalAudio.IntervalVariationSeconds) &&
                sourceAudioAfterGuards.RelativeVolume.Equals(finalAudio.RelativeVolume) &&
                ReferenceEquals(sourceAudioAfterGuards.Spatial, finalAudio.Spatial) &&
                ReferenceEquals(sourceAudioAfterGuards.RuntimeControl, finalAudio.RuntimeControl) &&
                sourceAudioAfterGuards.AdaptiveLayers.SequenceEqual(finalAudio.AdaptiveLayers) &&
                ReferenceEquals(sourceAudioAfterGuards.Orchestration, finalAudio.Orchestration) &&
                ReferenceEquals(sourceAudioAfterGuards.Authoring, finalAudio.Authoring);
            var sourceEnvironmentAfterGuards = sourceTarget.Environment;
            var finalEnvironment = finalTarget.Environment;
            var environmentLotsPreserved =
                sourceEnvironmentAfterGuards.SchemaVersion == finalEnvironment.SchemaVersion &&
                sourceEnvironmentAfterGuards.Classification == finalEnvironment.Classification &&
                sourceEnvironmentAfterGuards.Tags.SequenceEqual(finalEnvironment.Tags, StringComparer.Ordinal) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Lighting, finalEnvironment.Lighting) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Fog, finalEnvironment.Fog) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Rendering, finalEnvironment.Rendering) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Sky, finalEnvironment.Sky) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Wind, finalEnvironment.Wind) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Weather, finalEnvironment.Weather) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Water, finalEnvironment.Water) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Reverb, finalEnvironment.Reverb) &&
                ReferenceEquals(sourceEnvironmentAfterGuards.Authoring, finalEnvironment.Authoring) &&
                sourceEnvironmentAfterGuards.RequireAuthoringRuntimeMetadata ==
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
            var structuredDiagnostics = invalidDefinition.Groups.Count == 4 && invalidClip.Groups.Count == 4 &&
                                        invalidLoop.Groups.Count == 4 && invalidSpatial.Groups.Count == 4 &&
                                        invalidInstance.Groups.Count == 4 && preview.Groups.Count == 4;
            var humanText = !ContainsTechnicalIdentifierUx2EnvF(
                string.Join(' ', editing.AudioEmitterChoicesUx2EnvF.Select(choice => choice.Label)) + " " +
                preview.Message + " " + preview.Summary + " " +
                string.Join(' ', preview.Diff.Entries) + " " +
                string.Join(' ', preview.PlaybackPlans.Select(plan =>
                    $"{plan.Emitter} {plan.Clip} {plan.Loop} {plan.Position}")));

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvFTemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && emptySourceSupported &&
                            !invalidDefinition.Succeeded && !invalidClip.Succeeded &&
                            !invalidLoop.Succeeded && !invalidSpatial.Succeeded && !invalidInstance.Succeeded &&
                            canonicalDryRun && deterministicPlans && noLivePlaybackClaim &&
                            staleRevisionBlocked && approvalFingerprintBlocked && applyUndoRedo && persistence &&
                            clipOrderPreserved && hiddenClipTailsPreserved && environmentLotsPreserved &&
                            audioTailsPreserved && areaNeighborsPreserved && projectNeighborsPreserved &&
                            structuredDiagnostics && humanText;
            return new StudioUx2EnvFSmokeResult(
                succeeded,
                explicitSelection,
                emptySourceSupported,
                !invalidDefinition.Succeeded,
                !invalidClip.Succeeded,
                !invalidLoop.Succeeded,
                !invalidSpatial.Succeeded,
                !invalidInstance.Succeeded,
                canonicalDryRun,
                deterministicPlans,
                noLivePlaybackClaim,
                staleRevisionBlocked,
                approvalFingerprintBlocked,
                applyUndoRedo,
                persistence,
                clipOrderPreserved,
                hiddenClipTailsPreserved,
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
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_F_CONTRACT_FAILED type={exception.GetType().Name} " +
                $"message={exception.Message}");
            return FailedUx2EnvFSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvFTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_F_CLEANUP_FAILED");
        }
    }

    private static bool ApplyUx2EnvFRevisionMutation(
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
            source.Wind.Speed + .015625f,
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
        var result = controller._setAreaEnvironmentUx2EnvF.Handle(history.State,
            new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvFTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-f-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvFSmokeResult FailedUx2EnvFSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false);
}
