using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvBProvenance = "studio:ux2-env-b:environment-foundation";

    private readonly PatchAreaEnvironmentHandler _setAreaEnvironmentUx2EnvB = new(new SystemClock());
    private StudioUx2EnvBDraft? _approvedAreaEnvironmentUx2EnvB;
    private ProjectRevision? _approvedAreaEnvironmentRevisionUx2EnvB;
    private string? _approvedAreaEnvironmentFingerprintUx2EnvB;

    internal IReadOnlyList<StudioUx2EnvBAreaChoice> AreaEnvironmentChoicesUx2EnvB
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildAreaEnvironmentChoicesUx2EnvB(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvBAreaChoice> BuildAreaEnvironmentChoicesUx2EnvB(
        ProjectDefinition project)
    {
        var duplicateNames = project.Areas
            .GroupBy(area => area.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var environment = area.Environment;
                var displayName = ContainsTechnicalIdentifierUx2EnvB(area.DisplayName)
                    ? "Zone sans nom lisible"
                    : area.DisplayName.Trim();
                var name = duplicateNames[area.DisplayName] > 1
                    ? $"{displayName} · {HumanTokenUx2EnvB(area.Slug)}"
                    : displayName;
                var authored = !string.Equals(environment.Authoring.Provenance, "migration-default",
                    StringComparison.Ordinal);
                return new StudioUx2EnvBAreaChoice(
                    area.Id,
                    $"{name} · {HumanClassificationUx2EnvB(environment.Classification)} · " +
                    $"{HumanDayNightModeUx2EnvB(environment.Lighting.Mode)} · " +
                    $"brouillard {HumanFogModeUx2EnvB(environment.Fog.Mode)}",
                    authored);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvBDraft? CreateAreaEnvironmentDraftUx2EnvB(AreaId? requestedAreaId = null)
    {
        var history = History;
        var project = Project;
        if (history is null || project is null || requestedAreaId is null)
        {
            return null;
        }

        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1
            ? CreateAreaEnvironmentDraftUx2EnvB(project, history.State.Revision, matches[0])
            : null;
    }

    private static StudioUx2EnvBDraft CreateAreaEnvironmentDraftUx2EnvB(
        ProjectDefinition project,
        ProjectRevision revision,
        AreaDefinition area)
    {
        var environment = area.Environment;
        var authoring = environment.Authoring;
        var classification = authoring.Classification;
        var lighting = authoring.Lighting;
        var fog = authoring.Fog;
        return new StudioUx2EnvBDraft(
            area.Id,
            revision,
            AreaEnvironmentSourceFingerprintUx2EnvB(area),
            area.DisplayName,
            area.Slug,
            environment.Classification,
            environment.Tags.ToArray(),
            classification.Key,
            classification.ExtensionPack,
            classification.DrivesRendering,
            classification.DrivesAudio,
            classification.DrivesNavigation,
            classification.DrivesWeather,
            classification.DrivesCamera,
            classification.DrivesGameplay,
            environment.Lighting.Mode,
            environment.Lighting.DayLengthMinutes,
            environment.Lighting.AmbientIntensity,
            environment.Lighting.DirectionalIntensity,
            environment.Lighting.ColorTemperatureKelvin,
            environment.Lighting.Hdr,
            environment.Lighting.Shadows,
            lighting.PresetKey,
            lighting.AmbientColor,
            lighting.DirectionalColor,
            lighting.SimpleControls,
            lighting.Curves.Select(item => new StudioUx2EnvBCurveDraft(
                item.Hour,
                item.SunIntensity,
                item.MoonIntensity,
                item.SunElevationDegrees)).ToArray(),
            lighting.Events.Select(item => new StudioUx2EnvBEventDraft(
                item.Id,
                item.Hour,
                item.EventName,
                item.ForgeScriptPath,
                item.Required)).ToArray(),
            lighting.Probes.Select(item => new StudioUx2EnvBProbeDraft(
                item.Id,
                item.Name,
                ToTransformDraftUx2EnvB(item.Transform),
                item.AssetId,
                item.BlueprintId,
                HumanBlueprintLabelUx2EnvB(project, item.BlueprintId),
                item.RadiusMeters,
                item.Realtime,
                CopyDictionaryUx2EnvB(item.Overrides))).ToArray(),
            lighting.VolumeOverrides.Select(item => new StudioUx2EnvBLightingVolumeDraft(
                item.Id,
                item.Name,
                ToTransformDraftUx2EnvB(item.Transform),
                item.LightingPresetKey,
                item.FogMode,
                item.BlendDistanceMeters,
                CopyDictionaryUx2EnvB(item.Overrides))).ToArray(),
            environment.Fog.Mode,
            environment.Fog.Density,
            environment.Fog.StartDistance,
            environment.Fog.EndDistance,
            environment.Fog.Height,
            fog.DayColor,
            fog.NightColor,
            fog.Noise is null
                ? null
                : new StudioUx2EnvBFogNoiseDraft(
                    fog.Noise.AssetId,
                    fog.Noise.ScaleMeters,
                    fog.Noise.SpeedX,
                    fog.Noise.SpeedZ,
                    fog.Noise.Contrast),
            fog.LocalVolumes.Select(item => new StudioUx2EnvBFogVolumeDraft(
                item.Id,
                item.Name,
                ToTransformDraftUx2EnvB(item.Transform),
                item.Mode,
                item.Density,
                item.HeightFalloff,
                item.Color,
                item.BlendDistanceMeters,
                CopyDictionaryUx2EnvB(item.Overrides))).ToArray(),
            fog.QualityProfiles.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new StudioUx2EnvBQualityDraft(item.Key, item.Value)).ToArray(),
            authoring.RequiredAtRuntime,
            environment.RequireAuthoringRuntimeMetadata);
    }

    internal StudioUx2EnvBPreview PreviewAreaEnvironmentUx2EnvB(StudioUx2EnvBDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAreaEnvironmentUx2EnvB();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedAreaEnvironmentPreviewUx2EnvB("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedAreaEnvironmentPreviewUx2EnvB(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la zone avant de prévisualiser.");
        }
        if (!TryBuildAreaEnvironmentUx2EnvB(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var candidateEnvironment, out var diagnostics, out var message))
        {
            return new StudioUx2EnvBPreview(
                false,
                message,
                string.Empty,
                false,
                EmptyAreaEnvironmentDiffUx2EnvB(),
                GroupAreaEnvironmentDiagnosticsUx2EnvB(diagnostics),
                false,
                false,
                RuntimeFallbackHonestUx2EnvB(draft));
        }

        var dryRun = _setAreaEnvironmentUx2EnvB.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2EnvBDiagnostic(
                item.Code,
                $"areas/{draft.AreaId}/environment",
                HumanizeAreaEnvironmentDiagnosticUx2EnvB(item.Code, item.Message))).ToArray();
            return new StudioUx2EnvBPreview(
                false,
                commandDiagnostics.FirstOrDefault()?.Message ??
                "La commande d’environnement a refusé cet aperçu.",
                string.Empty,
                false,
                BuildAreaEnvironmentDiffUx2EnvB(sourceArea!.Environment, candidateEnvironment!),
                GroupAreaEnvironmentDiagnosticsUx2EnvB(commandDiagnostics),
                false,
                false,
                RuntimeFallbackHonestUx2EnvB(draft));
        }

        var diff = BuildAreaEnvironmentDiffUx2EnvB(sourceArea!.Environment, candidateEnvironment!);
        var temporal = AreaEnvironmentScriptService.PreviewTime(candidateArea!, 12);
        var snapshot = AreaEnvironmentScriptService.Read(candidateArea!);
        if (diff.HasChanges)
        {
            _approvedAreaEnvironmentUx2EnvB = draft;
            _approvedAreaEnvironmentRevisionUx2EnvB = history.State.Revision;
            _approvedAreaEnvironmentFingerprintUx2EnvB = AreaEnvironmentDraftFingerprintUx2EnvB(draft);
        }
        var summary =
            $"Midi simulé sans exécution runtime : soleil {temporal.SunIntensity:0.###}, " +
            $"lune {temporal.MoonIntensity:0.###}, altitude {temporal.SunElevationDegrees:0.#}°. " +
            $"Profil : {HumanTokenUx2EnvB(snapshot.ClassificationKey)}, " +
            $"{snapshot.CurvePointCount} point(s), {snapshot.TimedEventCount} événement(s), " +
            $"{snapshot.ReflectionProbeCount} probe(s), {snapshot.LightingVolumeCount} volume(s) lumière, " +
            $"{snapshot.FogVolumeCount} volume(s) brouillard.";
        return new StudioUx2EnvBPreview(
            true,
            diff.HasChanges
                ? "Aperçu validé · aucun document, nœud moteur, script ni état runtime n’a été modifié."
                : "Aperçu validé · aucune différence sémantique à appliquer.",
            summary,
            diff.HasChanges,
            diff,
            GroupAreaEnvironmentDiagnosticsUx2EnvB([]),
            true,
            true,
            RuntimeFallbackHonestUx2EnvB(draft));
    }

    internal StudioUx2EnvBActionResult ApplyApprovedAreaEnvironmentUx2EnvB()
    {
        var draft = _approvedAreaEnvironmentUx2EnvB;
        var history = History;
        if (draft is null || history is null ||
            _approvedAreaEnvironmentRevisionUx2EnvB != history.State.Revision ||
            !string.Equals(_approvedAreaEnvironmentFingerprintUx2EnvB,
                AreaEnvironmentDraftFingerprintUx2EnvB(draft), StringComparison.Ordinal))
        {
            CancelAreaEnvironmentUx2EnvB();
            return new StudioUx2EnvBActionResult(
                false,
                "Prévisualisez de nouveau l’environnement de la zone avant de l’appliquer.");
        }

        if (!TryBuildAreaEnvironmentUx2EnvB(draft, false, out var command, out _, out _, out _,
                out _, out var message))
        {
            CancelAreaEnvironmentUx2EnvB();
            return new StudioUx2EnvBActionResult(false, message);
        }

        var preview = _setAreaEnvironmentUx2EnvB.Handle(history.State, command! with { DryRun = true });
        if (!preview.Succeeded)
        {
            CancelAreaEnvironmentUx2EnvB();
            return new StudioUx2EnvBActionResult(false, HumanizeAreaEnvironmentDiagnosticUx2EnvB(
                preview.Diagnostics.FirstOrDefault()?.Code,
                preview.Diagnostics.FirstOrDefault()?.Message));
        }

        var applied = _setAreaEnvironmentUx2EnvB.Handle(history.State, command!);
        CancelAreaEnvironmentUx2EnvB();
        if (!applied.Succeeded)
        {
            return new StudioUx2EnvBActionResult(false, HumanizeAreaEnvironmentDiagnosticUx2EnvB(
                applied.Diagnostics.FirstOrDefault()?.Code,
                applied.Diagnostics.FirstOrDefault()?.Message));
        }

        history.Record(applied);
        InvalidateContentIndexes();
        return new StudioUx2EnvBActionResult(
            true,
            "Environnement appliqué · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelAreaEnvironmentUx2EnvB()
    {
        _approvedAreaEnvironmentUx2EnvB = null;
        _approvedAreaEnvironmentRevisionUx2EnvB = null;
        _approvedAreaEnvironmentFingerprintUx2EnvB = null;
    }

    internal void CloseUx2EnvB() => CancelAreaEnvironmentUx2EnvB();

    internal StudioUx2EnvBApprovalSnapshot CaptureAreaEnvironmentApprovalUx2EnvB() => new(
        _approvedAreaEnvironmentUx2EnvB,
        _approvedAreaEnvironmentRevisionUx2EnvB,
        _approvedAreaEnvironmentFingerprintUx2EnvB);

    internal void RestoreAreaEnvironmentApprovalUx2EnvB(StudioUx2EnvBApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAreaEnvironmentUx2EnvB = snapshot.Draft;
        _approvedAreaEnvironmentRevisionUx2EnvB = snapshot.Revision;
        _approvedAreaEnvironmentFingerprintUx2EnvB = snapshot.Fingerprint;
    }

    private bool TryBuildAreaEnvironmentUx2EnvB(
        StudioUx2EnvBDraft draft,
        bool dryRun,
        out PatchAreaEnvironmentCommand? command,
        out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea,
        out AreaEnvironmentProfile? candidateEnvironment,
        out IReadOnlyList<StudioUx2EnvBDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
        candidateEnvironment = null;
        diagnostics = [];
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez la zone avant de prévisualiser.";
            return false;
        }

        var matches = project.Areas.Where(area => area.Id == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "La zone choisie n’existe plus ou n’est plus unique.";
            return false;
        }
        sourceArea = matches[0];
        if (!string.Equals(draft.SourceFingerprint, AreaEnvironmentSourceFingerprintUx2EnvB(sourceArea),
                StringComparison.Ordinal))
        {
            message = "L’environnement ou un voisin de la zone a changé. Rechargez la fiche avant de prévisualiser.";
            return false;
        }

        var inputDiagnostics = ValidateAreaEnvironmentDraftUx2EnvB(draft).ToArray();
        if (inputDiagnostics.Length > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var source = sourceArea.Environment;
            var sourceAuthoring = source.Authoring;
            var curves = draft.Curves.Select(item => new AreaDayNightCurvePoint(
                item.Hour,
                item.SunIntensity,
                item.MoonIntensity,
                item.SunElevationDegrees)).ToArray();
            var sourceEvents = sourceAuthoring.Lighting.Events.ToDictionary(item => item.Id);
            var events = draft.Events.Select(item =>
            {
                if (sourceEvents.TryGetValue(item.Id, out var existing) && SameEventUx2EnvB(item, existing))
                {
                    return existing;
                }
                return new AreaTimedEnvironmentEvent(
                    item.Id,
                    item.Hour,
                    item.EventName.Trim(),
                    NormalizeProjectPathUx2EnvB(item.ForgeScriptPath),
                    item.Required);
            }).ToArray();
            var sourceProbes = sourceAuthoring.Lighting.Probes.ToDictionary(item => item.Id);
            var probes = draft.Probes.Select(item =>
            {
                if (sourceProbes.TryGetValue(item.Id, out var existing) && SameProbeUx2EnvB(item, existing))
                {
                    return existing;
                }
                return new AreaReflectionProbeProfile(
                    item.Id,
                    item.Name.Trim(),
                    ToTransformUx2EnvB(item.Transform),
                    item.AssetId.Trim(),
                    item.BlueprintId,
                    item.RadiusMeters,
                    item.Realtime,
                    NormalizeDictionaryUx2EnvB(item.Overrides));
            }).ToArray();
            var sourceLightingVolumes = sourceAuthoring.Lighting.VolumeOverrides.ToDictionary(item => item.Id);
            var lightingVolumes = draft.LightingVolumes.Select(item =>
            {
                if (sourceLightingVolumes.TryGetValue(item.Id, out var existing) &&
                    SameLightingVolumeUx2EnvB(item, existing))
                {
                    return existing;
                }
                return new AreaEnvironmentVolumeOverride(
                    item.Id,
                    item.Name.Trim(),
                    ToTransformUx2EnvB(item.Transform),
                    item.LightingPresetKey.Trim(),
                    item.FogMode,
                    item.BlendDistanceMeters,
                    NormalizeDictionaryUx2EnvB(item.Overrides));
            }).ToArray();
            var sourceFogVolumes = sourceAuthoring.Fog.LocalVolumes.ToDictionary(item => item.Id);
            var fogVolumes = draft.FogVolumes.Select(item =>
            {
                if (sourceFogVolumes.TryGetValue(item.Id, out var existing) && SameFogVolumeUx2EnvB(item, existing))
                {
                    return existing;
                }
                return new AreaLocalFogVolume(
                    item.Id,
                    item.Name.Trim(),
                    ToTransformUx2EnvB(item.Transform),
                    item.Mode,
                    item.Density,
                    item.HeightFalloff,
                    item.Color.Trim(),
                    item.BlendDistanceMeters,
                    NormalizeDictionaryUx2EnvB(item.Overrides));
            }).ToArray();

            var classification = new AreaClassificationCapabilityProfile(
                draft.Classification,
                draft.ClassificationKey.Trim(),
                draft.ExtensionPack.Trim(),
                draft.DrivesRendering,
                draft.DrivesAudio,
                draft.DrivesNavigation,
                draft.DrivesWeather,
                draft.DrivesCamera,
                draft.DrivesGameplay);
            var lighting = new AreaLightingProfile(
                draft.DayNightMode,
                draft.DayLengthMinutes,
                draft.AmbientIntensity,
                draft.DirectionalIntensity,
                draft.ColorTemperatureKelvin,
                draft.Hdr,
                draft.Shadows);
            var physicalLighting = new AreaPhysicalLightingProfile(
                draft.LightingPresetKey.Trim(),
                draft.AmbientColor.Trim(),
                draft.DirectionalColor.Trim(),
                draft.SimpleControls,
                curves,
                events,
                probes,
                lightingVolumes);
            var fog = new AreaFogProfile(
                draft.FogMode,
                draft.FogDensity,
                draft.FogStartDistance,
                draft.FogEndDistance,
                draft.FogHeight);
            var fogAuthoring = new AreaFogAuthoringProfile(
                draft.FogDayColor.Trim(),
                draft.FogNightColor.Trim(),
                draft.FogNoise is null
                    ? null
                    : new AreaFogNoiseProfile(
                        draft.FogNoise.AssetId.Trim(),
                        draft.FogNoise.ScaleMeters,
                        draft.FogNoise.SpeedX,
                        draft.FogNoise.SpeedZ,
                        draft.FogNoise.Contrast),
                fogVolumes,
                draft.FogQualityProfiles.ToDictionary(
                    item => item.Platform.Trim(),
                    item => item.Tier,
                    StringComparer.Ordinal));
            var preservedProvenanceAuthoring = new AreaEnvironmentAuthoringProfile(
                sourceAuthoring.Id,
                sourceAuthoring.SchemaVersion,
                sourceAuthoring.Provenance,
                classification,
                physicalLighting,
                fogAuthoring,
                draft.RequiredAtRuntime);
            var preservedProvenanceEnvironment = new AreaEnvironmentProfile(
                source.SchemaVersion,
                draft.Classification,
                draft.Tags,
                lighting,
                fog,
                source.Audio,
                source.Rendering,
                source.Sky,
                source.Wind,
                source.Weather,
                source.Water,
                source.Reverb,
                preservedProvenanceAuthoring,
                draft.RequireAuthoringRuntimeMetadata);
            AreaEnvironmentProfile environment;
            AreaDefinition updatedArea;
            if (!BuildAreaEnvironmentDiffUx2EnvB(source, preservedProvenanceEnvironment).HasChanges)
            {
                environment = source;
                updatedArea = sourceArea;
            }
            else
            {
                var authoring = new AreaEnvironmentAuthoringProfile(
                    sourceAuthoring.Id,
                    sourceAuthoring.SchemaVersion,
                    Ux2EnvBProvenance,
                    classification,
                    physicalLighting,
                    fogAuthoring,
                    draft.RequiredAtRuntime);
                environment = new AreaEnvironmentProfile(
                    source.SchemaVersion,
                    draft.Classification,
                    draft.Tags,
                    lighting,
                    fog,
                    source.Audio,
                    source.Rendering,
                    source.Sky,
                    source.Wind,
                    source.Weather,
                    source.Water,
                    source.Reverb,
                    authoring,
                    draft.RequireAuthoringRuntimeMetadata);
                updatedArea = sourceArea.WithEnvironment(environment);
            }
            candidateEnvironment = environment;
            candidateArea = updatedArea;

            var sourceAreaId = sourceArea.Id;
            var candidateProject = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module,
                project.Blueprints,
                project.Areas.Select(area => area.Id == sourceAreaId ? updatedArea : area).ToArray(),
                project.Dialogues,
                project.Quests);
            var canonicalDiagnostics = ProjectValidator.Validate(candidateProject)
                .Select(item => new StudioUx2EnvBDiagnostic(
                    item.Code,
                    item.Path,
                    HumanizeAreaEnvironmentDiagnosticUx2EnvB(item.Code, item.Message)))
                .ToArray();
            if (canonicalDiagnostics.Length > 0)
            {
                diagnostics = canonicalDiagnostics;
                message = canonicalDiagnostics[0].Message;
                return false;
            }

            command = new PatchAreaEnvironmentCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                sourceArea.Id,
                environment,
                dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or ProjectValidationException)
        {
            var diagnostic = new StudioUx2EnvBDiagnostic(
                "studio.area_environment.invalid",
                $"areas/{draft.AreaId}/environment",
                HumanizeAreaEnvironmentConstructionUx2EnvB(exception));
            diagnostics = [diagnostic];
            message = diagnostic.Message;
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2EnvBDiagnostic> ValidateAreaEnvironmentDraftUx2EnvB(
        StudioUx2EnvBDraft draft)
    {
        var diagnostics = new List<StudioUx2EnvBDiagnostic>();
        if (string.IsNullOrWhiteSpace(draft.ClassificationKey) || draft.ClassificationKey.Contains(' ') ||
            !new[] { draft.DrivesRendering, draft.DrivesAudio, draft.DrivesNavigation, draft.DrivesWeather,
                draft.DrivesCamera, draft.DrivesGameplay }.Any(value => value))
        {
            diagnostics.Add(new StudioUx2EnvBDiagnostic(
                AreaEnvironmentAuthoringValidator.ClassificationCode,
                $"areas/{draft.AreaId}/environment/authoring/classification",
                "La classification demande une clé sans espace et au moins une capacité pilotée."));
        }
        if (!float.IsFinite(draft.DayLengthMinutes) || draft.DayLengthMinutes <= 0)
        {
            diagnostics.Add(new StudioUx2EnvBDiagnostic(
                AreaEnvironmentAuthoringValidator.LightingCode,
                $"areas/{draft.AreaId}/environment/lighting/dayLengthMinutes",
                "La durée du cycle doit être un nombre positif."));
        }
        if (draft.Curves.Count < 2 || draft.Curves.Any(item => !float.IsFinite(item.Hour) ||
                item.Hour is < 0 or > 24 || !float.IsFinite(item.SunIntensity) || item.SunIntensity < 0 ||
                !float.IsFinite(item.MoonIntensity) || item.MoonIntensity < 0 ||
                !float.IsFinite(item.SunElevationDegrees) || item.SunElevationDegrees is < -90 or > 90) ||
            draft.Curves.Select(item => item.Hour).Distinct().Count() != draft.Curves.Count)
        {
            diagnostics.Add(new StudioUx2EnvBDiagnostic(
                AreaEnvironmentAuthoringValidator.LightingCode,
                $"areas/{draft.AreaId}/environment/authoring/lighting/curves",
                "La courbe jour/nuit demande au moins deux heures uniques et des valeurs physiques valides."));
        }
        if (draft.Events.Any(item => item.Id == Guid.Empty || item.Hour is < 0 or >= 24 ||
                string.IsNullOrWhiteSpace(item.EventName) || string.IsNullOrWhiteSpace(item.ForgeScriptPath)))
        {
            diagnostics.Add(new StudioUx2EnvBDiagnostic(
                AreaEnvironmentAuthoringValidator.EventCode,
                $"areas/{draft.AreaId}/environment/authoring/lighting/events",
                "Chaque événement horaire demande un nom, une heure et un chemin ForgeScript relatif."));
        }
        if (string.IsNullOrWhiteSpace(draft.LightingPresetKey) || !ColorUx2EnvB(draft.AmbientColor) ||
            !ColorUx2EnvB(draft.DirectionalColor) || draft.Probes.Any(item => item.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.AssetId) &&
                item.BlueprintId is null || item.RadiusMeters <= 0 || !float.IsFinite(item.RadiusMeters)) ||
            draft.LightingVolumes.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.LightingPresetKey) || item.BlendDistanceMeters < 0 ||
                !float.IsFinite(item.BlendDistanceMeters)))
        {
            diagnostics.Add(new StudioUx2EnvBDiagnostic(
                AreaEnvironmentAuthoringValidator.LightingCode,
                $"areas/{draft.AreaId}/environment/authoring/lighting",
                "Éclairage : preset, couleurs, probes et volumes doivent être complets et physiquement valides."));
        }
        if (draft.FogDensity is < 0 or > 1 || draft.FogStartDistance < 0 ||
            draft.FogEndDistance < draft.FogStartDistance || !float.IsFinite(draft.FogHeight) ||
            !ColorUx2EnvB(draft.FogDayColor) || !ColorUx2EnvB(draft.FogNightColor) ||
            draft.FogQualityProfiles.Count == 0 ||
            draft.FogQualityProfiles.Any(item => string.IsNullOrWhiteSpace(item.Platform)) ||
            draft.FogQualityProfiles.Select(item => item.Platform.Trim()).Distinct(StringComparer.Ordinal).Count() !=
            draft.FogQualityProfiles.Count ||
            draft.FogNoise is { } noise && (string.IsNullOrWhiteSpace(noise.AssetId) ||
                !float.IsFinite(noise.ScaleMeters) || noise.ScaleMeters <= 0 ||
                !float.IsFinite(noise.Contrast) || noise.Contrast is < 0 or > 4) ||
            draft.FogVolumes.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) ||
                item.Density is < 0 or > 1 || item.HeightFalloff < 0 || item.BlendDistanceMeters < 0 ||
                !ColorUx2EnvB(item.Color)))
        {
            diagnostics.Add(new StudioUx2EnvBDiagnostic(
                AreaEnvironmentAuthoringValidator.FogCode,
                $"areas/{draft.AreaId}/environment/fog",
                "Brouillard : distances, couleurs, bruit, volumes et profils qualité doivent être valides."));
        }
        var childIds = draft.Events.Select(item => item.Id)
            .Concat(draft.Probes.Select(item => item.Id))
            .Concat(draft.LightingVolumes.Select(item => item.Id))
            .Concat(draft.FogVolumes.Select(item => item.Id)).ToArray();
        if (childIds.Any(item => item == Guid.Empty) || childIds.Distinct().Count() != childIds.Length)
        {
            diagnostics.Add(new StudioUx2EnvBDiagnostic(
                AreaEnvironmentAuthoringValidator.IdentityCode,
                $"areas/{draft.AreaId}/environment/authoring",
                "Les éléments d’environnement doivent garder des identités internes uniques."));
        }
        return diagnostics;
    }

    private static StudioUx2EnvBSemanticDiff BuildAreaEnvironmentDiffUx2EnvB(
        AreaEnvironmentProfile source,
        AreaEnvironmentProfile candidate)
    {
        var classification = new List<string>();
        if (source.Classification != candidate.Classification)
            classification.Add($"Type : {HumanClassificationUx2EnvB(source.Classification)} → " +
                               HumanClassificationUx2EnvB(candidate.Classification));
        if (!source.Tags.SequenceEqual(candidate.Tags))
            classification.Add($"Étiquettes : {source.Tags.Count} → {candidate.Tags.Count}");
        if (source.Authoring.Classification != candidate.Authoring.Classification)
            classification.Add("Capacités et extension de classification modifiées.");

        var cycle = new List<string>();
        if (source.Lighting.Mode != candidate.Lighting.Mode)
            cycle.Add($"Mode : {HumanDayNightModeUx2EnvB(source.Lighting.Mode)} → " +
                      HumanDayNightModeUx2EnvB(candidate.Lighting.Mode));
        if (!source.Lighting.DayLengthMinutes.Equals(candidate.Lighting.DayLengthMinutes))
            cycle.Add($"Durée : {source.Lighting.DayLengthMinutes:0.###} → " +
                      $"{candidate.Lighting.DayLengthMinutes:0.###} min");
        if (!source.Authoring.Lighting.Curves.SequenceEqual(candidate.Authoring.Lighting.Curves))
            cycle.Add($"Courbe : {source.Authoring.Lighting.Curves.Count} → " +
                      $"{candidate.Authoring.Lighting.Curves.Count} point(s)");
        if (!source.Authoring.Lighting.Events.SequenceEqual(candidate.Authoring.Lighting.Events))
            cycle.Add($"Événements : {source.Authoring.Lighting.Events.Count} → " +
                      $"{candidate.Authoring.Lighting.Events.Count}");

        var lighting = new List<string>();
        if (source.Lighting != candidate.Lighting)
            lighting.Add("Intensités, température, HDR ou ombres modifiés.");
        if (!string.Equals(source.Authoring.Lighting.PresetKey, candidate.Authoring.Lighting.PresetKey,
                StringComparison.Ordinal) ||
            !string.Equals(source.Authoring.Lighting.AmbientColor, candidate.Authoring.Lighting.AmbientColor,
                StringComparison.Ordinal) ||
            !string.Equals(source.Authoring.Lighting.DirectionalColor,
                candidate.Authoring.Lighting.DirectionalColor, StringComparison.Ordinal) ||
            source.Authoring.Lighting.SimpleControls != candidate.Authoring.Lighting.SimpleControls)
            lighting.Add("Preset, couleurs ou niveau de contrôle modifiés.");
        if (!source.Authoring.Lighting.Probes.SequenceEqual(candidate.Authoring.Lighting.Probes))
            lighting.Add($"Probes : {source.Authoring.Lighting.Probes.Count} → " +
                         $"{candidate.Authoring.Lighting.Probes.Count}");
        if (!source.Authoring.Lighting.VolumeOverrides.SequenceEqual(
                candidate.Authoring.Lighting.VolumeOverrides))
            lighting.Add($"Volumes locaux : {source.Authoring.Lighting.VolumeOverrides.Count} → " +
                         $"{candidate.Authoring.Lighting.VolumeOverrides.Count}");

        var fog = new List<string>();
        if (source.Fog != candidate.Fog)
            fog.Add($"Profil global : {HumanFogModeUx2EnvB(source.Fog.Mode)} → " +
                    HumanFogModeUx2EnvB(candidate.Fog.Mode));
        if (!Equals(source.Authoring.Fog.Noise, candidate.Authoring.Fog.Noise) ||
            !string.Equals(source.Authoring.Fog.DayColor, candidate.Authoring.Fog.DayColor,
                StringComparison.Ordinal) ||
            !string.Equals(source.Authoring.Fog.NightColor, candidate.Authoring.Fog.NightColor,
                StringComparison.Ordinal))
            fog.Add("Couleurs ou bruit procédural modifiés.");
        if (!source.Authoring.Fog.LocalVolumes.SequenceEqual(candidate.Authoring.Fog.LocalVolumes))
            fog.Add($"Volumes locaux : {source.Authoring.Fog.LocalVolumes.Count} → " +
                    $"{candidate.Authoring.Fog.LocalVolumes.Count}");
        if (!DictionaryEqualUx2EnvB(source.Authoring.Fog.QualityProfiles,
                candidate.Authoring.Fog.QualityProfiles))
            fog.Add("Profils qualité modifiés.");
        if (source.Authoring.RequiredAtRuntime != candidate.Authoring.RequiredAtRuntime ||
            source.RequireAuthoringRuntimeMetadata != candidate.RequireAuthoringRuntimeMetadata)
            fog.Add("Exigence de métadonnées runtime modifiée.");
        return new StudioUx2EnvBSemanticDiff(classification, cycle, lighting, fog);
    }

    private static StudioUx2EnvBSemanticDiff EmptyAreaEnvironmentDiffUx2EnvB() => new([], [], [], []);

    private static StudioUx2EnvBPreview FailedAreaEnvironmentPreviewUx2EnvB(string message) => new(
        false,
        message,
        string.Empty,
        false,
        EmptyAreaEnvironmentDiffUx2EnvB(),
        GroupAreaEnvironmentDiagnosticsUx2EnvB([]),
        false,
        false,
        false);

    private static IReadOnlyList<StudioUx2EnvBDiagnosticGroup> GroupAreaEnvironmentDiagnosticsUx2EnvB(
        IReadOnlyList<StudioUx2EnvBDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            ("classification", "Classification et capacités", "/classification"),
            ("cycle", "Cycle, courbes et événements", "/lighting"),
            ("lighting", "Éclairage physique et probes", "/authoring/lighting"),
            ("fog", "Brouillard et qualité", "/fog"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item => item.Path.Contains(definition.Item3,
                    StringComparison.OrdinalIgnoreCase) || definition.Item1 == "fog" &&
                !definitions.Take(3).Any(other => item.Path.Contains(other.Item3,
                    StringComparison.OrdinalIgnoreCase))).ToArray();
            return new StudioUx2EnvBDiagnosticGroup(
                definition.Item1,
                definition.Item2,
                items.Length == 0 ? "conforme" : "à corriger",
                items);
        }).ToArray();
    }

    private static string HumanizeAreaEnvironmentDiagnosticUx2EnvB(string? code, string? fallback) => code switch
    {
        AreaEnvironmentAuthoringValidator.ClassificationCode =>
            "La classification et ses capacités ne forment pas un profil valide.",
        AreaEnvironmentAuthoringValidator.LightingCode =>
            "La lumière physique ou la courbe jour/nuit contient une valeur invalide.",
        AreaEnvironmentAuthoringValidator.EventCode =>
            "Un événement horaire demande un nom, une heure et un chemin ForgeScript relatif.",
        AreaEnvironmentAuthoringValidator.ProbeCode =>
            "Une probe demande un nom, une ressource, un transform fini et un rayon positif.",
        AreaEnvironmentAuthoringValidator.VolumeCode =>
            "Un volume de lumière demande un transform, un preset et une transition valides.",
        AreaEnvironmentAuthoringValidator.FogCode =>
            "Le brouillard contient une couleur, un bruit, un volume ou un profil qualité invalide.",
        AreaEnvironmentAuthoringValidator.IdentityCode =>
            "Les éléments d’environnement doivent garder des identités internes uniques.",
        "command.revision_conflict" =>
            "Le projet a changé. Rechargez la zone et relancez l’aperçu.",
        _ => "La validation canonique du projet a refusé l’environnement ; consultez le groupe concerné.",
    };

    private static string HumanizeAreaEnvironmentConstructionUx2EnvB(Exception exception) => exception switch
    {
        ArgumentOutOfRangeException => "Une valeur physique sort de la plage acceptée.",
        ArgumentException => "Un champ ou une collection d’environnement est invalide.",
        KeyNotFoundException => "Un élément d’environnement référencé n’est plus disponible.",
        _ => "La fiche d’environnement ne peut pas être reconstruite.",
    };

    private static bool RuntimeFallbackHonestUx2EnvB(StudioUx2EnvBDraft draft) =>
        !draft.RequireAuthoringRuntimeMetadata || draft.RequiredAtRuntime;

    private static string AreaEnvironmentSourceFingerprintUx2EnvB(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(AreaJsonSerializer.Serialize(area));
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string AreaEnvironmentDraftFingerprintUx2EnvB(StudioUx2EnvBDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString());
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint);
            writer.Write((int)draft.Classification);
            WriteStringsUx2EnvB(writer, draft.Tags);
            writer.Write(draft.ClassificationKey ?? string.Empty);
            writer.Write(draft.ExtensionPack ?? string.Empty);
            writer.Write(draft.DrivesRendering);
            writer.Write(draft.DrivesAudio);
            writer.Write(draft.DrivesNavigation);
            writer.Write(draft.DrivesWeather);
            writer.Write(draft.DrivesCamera);
            writer.Write(draft.DrivesGameplay);
            writer.Write((int)draft.DayNightMode);
            writer.Write(draft.DayLengthMinutes);
            writer.Write(draft.AmbientIntensity);
            writer.Write(draft.DirectionalIntensity);
            writer.Write(draft.ColorTemperatureKelvin);
            writer.Write(draft.Hdr);
            writer.Write(draft.Shadows);
            writer.Write(draft.LightingPresetKey ?? string.Empty);
            writer.Write(draft.AmbientColor ?? string.Empty);
            writer.Write(draft.DirectionalColor ?? string.Empty);
            writer.Write(draft.SimpleControls);
            writer.Write(draft.Curves.Count);
            foreach (var item in draft.Curves)
            {
                writer.Write(item.Hour);
                writer.Write(item.SunIntensity);
                writer.Write(item.MoonIntensity);
                writer.Write(item.SunElevationDegrees);
            }
            writer.Write(draft.Events.Count);
            foreach (var item in draft.Events)
            {
                writer.Write(item.Id.ToByteArray());
                writer.Write(item.Hour);
                writer.Write(item.EventName ?? string.Empty);
                writer.Write(item.ForgeScriptPath ?? string.Empty);
                writer.Write(item.Required);
            }
            writer.Write(draft.Probes.Count);
            foreach (var item in draft.Probes)
            {
                writer.Write(item.Id.ToByteArray());
                writer.Write(item.Name ?? string.Empty);
                WriteTransformUx2EnvB(writer, item.Transform);
                writer.Write(item.AssetId ?? string.Empty);
                writer.Write(item.BlueprintId is not null);
                if (item.BlueprintId is not null) writer.Write(item.BlueprintId.ToString()!);
                writer.Write(item.RadiusMeters);
                writer.Write(item.Realtime);
                WriteDictionaryUx2EnvB(writer, item.Overrides);
            }
            writer.Write(draft.LightingVolumes.Count);
            foreach (var item in draft.LightingVolumes)
            {
                writer.Write(item.Id.ToByteArray());
                writer.Write(item.Name ?? string.Empty);
                WriteTransformUx2EnvB(writer, item.Transform);
                writer.Write(item.LightingPresetKey ?? string.Empty);
                writer.Write((int)item.FogMode);
                writer.Write(item.BlendDistanceMeters);
                WriteDictionaryUx2EnvB(writer, item.Overrides);
            }
            writer.Write((int)draft.FogMode);
            writer.Write(draft.FogDensity);
            writer.Write(draft.FogStartDistance);
            writer.Write(draft.FogEndDistance);
            writer.Write(draft.FogHeight);
            writer.Write(draft.FogDayColor ?? string.Empty);
            writer.Write(draft.FogNightColor ?? string.Empty);
            writer.Write(draft.FogNoise is not null);
            if (draft.FogNoise is { } noise)
            {
                writer.Write(noise.AssetId ?? string.Empty);
                writer.Write(noise.ScaleMeters);
                writer.Write(noise.SpeedX);
                writer.Write(noise.SpeedZ);
                writer.Write(noise.Contrast);
            }
            writer.Write(draft.FogVolumes.Count);
            foreach (var item in draft.FogVolumes)
            {
                writer.Write(item.Id.ToByteArray());
                writer.Write(item.Name ?? string.Empty);
                WriteTransformUx2EnvB(writer, item.Transform);
                writer.Write((int)item.Mode);
                writer.Write(item.Density);
                writer.Write(item.HeightFalloff);
                writer.Write(item.Color ?? string.Empty);
                writer.Write(item.BlendDistanceMeters);
                WriteDictionaryUx2EnvB(writer, item.Overrides);
            }
            writer.Write(draft.FogQualityProfiles.Count);
            foreach (var item in draft.FogQualityProfiles)
            {
                writer.Write(item.Platform ?? string.Empty);
                writer.Write((int)item.Tier);
            }
            writer.Write(draft.RequiredAtRuntime);
            writer.Write(draft.RequireAuthoringRuntimeMetadata);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteTransformUx2EnvB(BinaryWriter writer, StudioUx2EnvBTransformDraft value)
    {
        writer.Write(value.Position.X);
        writer.Write(value.Position.Y);
        writer.Write(value.Position.Z);
        writer.Write(value.Rotation.X);
        writer.Write(value.Rotation.Y);
        writer.Write(value.Rotation.Z);
        writer.Write(value.Rotation.W);
        writer.Write(value.Scale.X);
        writer.Write(value.Scale.Y);
        writer.Write(value.Scale.Z);
    }

    private static void WriteStringsUx2EnvB(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values) writer.Write(value ?? string.Empty);
    }

    private static void WriteDictionaryUx2EnvB(
        BinaryWriter writer,
        IReadOnlyDictionary<string, string> values)
    {
        writer.Write(values.Count);
        foreach (var item in values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.Write(item.Key ?? string.Empty);
            writer.Write(item.Value ?? string.Empty);
        }
    }

    private static StudioUx2EnvBTransformDraft ToTransformDraftUx2EnvB(TransformValue value) =>
        new(value.Position, value.Rotation, value.Scale);

    private static TransformValue ToTransformUx2EnvB(StudioUx2EnvBTransformDraft value) =>
        new(value.Position, value.Rotation, value.Scale);

    private static IReadOnlyDictionary<string, string> CopyDictionaryUx2EnvB(
        IReadOnlyDictionary<string, string> values) => values.ToDictionary(
        item => item.Key,
        item => item.Value,
        StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> NormalizeDictionaryUx2EnvB(
        IReadOnlyDictionary<string, string> values) => values
        .Where(item => !string.IsNullOrWhiteSpace(item.Key))
        .ToDictionary(item => item.Key.Trim(), item => item.Value?.Trim() ?? string.Empty, StringComparer.Ordinal);

    private static bool SameEventUx2EnvB(StudioUx2EnvBEventDraft draft, AreaTimedEnvironmentEvent source) =>
        draft.Id == source.Id && draft.Hour.Equals(source.Hour) &&
        string.Equals(draft.EventName.Trim(), source.EventName, StringComparison.Ordinal) &&
        string.Equals(NormalizeProjectPathUx2EnvB(draft.ForgeScriptPath), source.ForgeScriptPath,
            StringComparison.Ordinal) && draft.Required == source.Required;

    private static bool SameProbeUx2EnvB(StudioUx2EnvBProbeDraft draft, AreaReflectionProbeProfile source) =>
        draft.Id == source.Id && string.Equals(draft.Name.Trim(), source.Name, StringComparison.Ordinal) &&
        ToTransformUx2EnvB(draft.Transform) == source.Transform &&
        string.Equals(draft.AssetId.Trim(), source.AssetId, StringComparison.Ordinal) &&
        Equals(draft.BlueprintId, source.BlueprintId) && draft.RadiusMeters.Equals(source.RadiusMeters) &&
        draft.Realtime == source.Realtime && DictionaryEqualUx2EnvB(draft.Overrides, source.Overrides);

    private static bool SameLightingVolumeUx2EnvB(
        StudioUx2EnvBLightingVolumeDraft draft,
        AreaEnvironmentVolumeOverride source) =>
        draft.Id == source.Id && string.Equals(draft.Name.Trim(), source.Name, StringComparison.Ordinal) &&
        ToTransformUx2EnvB(draft.Transform) == source.Transform &&
        string.Equals(draft.LightingPresetKey.Trim(), source.LightingPresetKey, StringComparison.Ordinal) &&
        draft.FogMode == source.FogMode && draft.BlendDistanceMeters.Equals(source.BlendDistanceMeters) &&
        DictionaryEqualUx2EnvB(draft.Overrides, source.Overrides);

    private static bool SameFogVolumeUx2EnvB(
        StudioUx2EnvBFogVolumeDraft draft,
        AreaLocalFogVolume source) =>
        draft.Id == source.Id && string.Equals(draft.Name.Trim(), source.Name, StringComparison.Ordinal) &&
        ToTransformUx2EnvB(draft.Transform) == source.Transform && draft.Mode == source.Mode &&
        draft.Density.Equals(source.Density) && draft.HeightFalloff.Equals(source.HeightFalloff) &&
        string.Equals(draft.Color.Trim(), source.Color, StringComparison.Ordinal) &&
        draft.BlendDistanceMeters.Equals(source.BlendDistanceMeters) &&
        DictionaryEqualUx2EnvB(draft.Overrides, source.Overrides);

    private static bool DictionaryEqualUx2EnvB<T>(
        IReadOnlyDictionary<string, T> first,
        IReadOnlyDictionary<string, T> second)
    {
        if (first.Count != second.Count) return false;
        var comparer = EqualityComparer<T>.Default;
        return first.All(item => second.TryGetValue(item.Key, out var value) && comparer.Equals(item.Value, value));
    }

    private static string NormalizeProjectPathUx2EnvB(string value) =>
        (value ?? string.Empty).Trim().Replace('\\', '/');

    private static bool ColorUx2EnvB(string value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private static string HumanBlueprintLabelUx2EnvB(ProjectDefinition project, Forge.Domain.Blueprints.BlueprintId? id)
    {
        if (id is null) return "Aucun blueprint associé";
        var match = project.Blueprints.SingleOrDefault(item => item.Id == id);
        return match is null || ContainsTechnicalIdentifierUx2EnvB(match.DisplayName)
            ? "Blueprint associé non disponible"
            : match.DisplayName.Trim();
    }

    private static string HumanClassificationUx2EnvB(AreaEnvironmentClass value) => value switch
    {
        AreaEnvironmentClass.Interior => "intérieure",
        AreaEnvironmentClass.Exterior => "extérieure",
        AreaEnvironmentClass.Natural => "naturelle",
        AreaEnvironmentClass.Artificial => "artificielle",
        AreaEnvironmentClass.Underground => "souterraine",
        _ => "personnalisée",
    };

    private static string HumanDayNightModeUx2EnvB(DayNightMode value) => value switch
    {
        DayNightMode.Clock => "cycle horloge",
        DayNightMode.AlwaysDay => "jour permanent",
        DayNightMode.AlwaysNight => "nuit permanente",
        _ => "cycle personnalisé",
    };

    private static string HumanFogModeUx2EnvB(FogMode value) => value switch
    {
        FogMode.Disabled => "désactivé",
        FogMode.Distance => "à distance",
        FogMode.Volumetric => "volumétrique",
        _ => "personnalisé",
    };

    private static string HumanTokenUx2EnvB(string value)
    {
        var normalized = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(normalized) || ContainsTechnicalIdentifierUx2EnvB(normalized)
            ? "profil sans nom lisible"
            : normalized;
    }

    private static bool ContainsTechnicalIdentifierUx2EnvB(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        text.Contains(".json", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    internal static ProjectDefinition CreateUx2EnvBEvidenceProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sourceArea = source.Areas.FirstOrDefault() ??
                         throw new InvalidOperationException("The evidence project requires an area.");
        var sourceEnvironment = sourceArea.Environment;
        var sourceAuthoring = sourceEnvironment.Authoring;
        var firstEventId = Guid.NewGuid();
        var secondEventId = Guid.NewGuid();
        var probeId = Guid.NewGuid();
        var lightingVolumeId = Guid.NewGuid();
        var fogVolumeId = Guid.NewGuid();
        var classification = new AreaClassificationCapabilityProfile(
            AreaEnvironmentClass.Natural,
            "natural-forest",
            "builtin-environments",
            true,
            true,
            true,
            true,
            true,
            true);
        var physical = new AreaPhysicalLightingProfile(
            "forest-soft",
            "#53657a",
            "#ffe1b0",
            false,
            [
                new(0, .05f, .35f, -40),
                new(6, .35f, .12f, 4),
                new(12, 1.15f, 0, 62),
                new(18, .3f, .15f, 2),
                new(24, .05f, .35f, -40),
            ],
            [
                new(firstEventId, 6, "Réveil de la forêt", "scripts/environment/dawn.forge", false),
                new(secondEventId, 18, "Veille crépusculaire", "scripts/environment/dusk.forge", false),
            ],
            [
                new(probeId, "Reflets de la clairière", TransformValue.Identity,
                    "builtin://openrpg/environment/reflection-neutral", null, 18, false,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["priority"] = "local" }),
            ],
            [
                new(lightingVolumeId, "Sous-bois", TransformValue.Identity, "forest-shadow",
                    FogMode.Distance, 3,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["exposure"] = "0.82" }),
            ]);
        var fogAuthoring = new AreaFogAuthoringProfile(
            "#b9ced7",
            "#2c3854",
            new("builtin://openrpg/environment/fog-soft", 22, .15f, -.08f, 1.2f),
            [
                new(fogVolumeId, "Brume du ruisseau", TransformValue.Identity, FogMode.Volumetric,
                    .17f, .4f, "#adc9d2", 4,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["depth"] = "shallow" }),
            ],
            new Dictionary<string, AreaFogQualityTier>(StringComparer.Ordinal)
            {
                ["desktop"] = AreaFogQualityTier.High,
                ["performance"] = AreaFogQualityTier.Low,
            });
        var authoring = new AreaEnvironmentAuthoringProfile(
            sourceAuthoring.Id,
            sourceAuthoring.SchemaVersion,
            Ux2EnvBProvenance,
            classification,
            physical,
            fogAuthoring,
            true);
        var environment = new AreaEnvironmentProfile(
            sourceEnvironment.SchemaVersion,
            AreaEnvironmentClass.Natural,
            sourceEnvironment.Tags.Concat(["ux2-evidence"]).Distinct(StringComparer.Ordinal).ToArray(),
            new(DayNightMode.Clock, 36.5f, .42f, 1.18f, 5_850, true, true),
            new(FogMode.Distance, .08f, 12, 140, 3),
            sourceEnvironment.Audio,
            sourceEnvironment.Rendering,
            sourceEnvironment.Sky,
            sourceEnvironment.Wind,
            sourceEnvironment.Weather,
            sourceEnvironment.Water,
            sourceEnvironment.Reverb,
            authoring,
            true);
        var evidenceArea = sourceArea.WithEnvironment(environment);
        return new ProjectDefinition(
            source.Id,
            source.Title,
            source.Module,
            source.Blueprints,
            source.Areas.Select(area => area.Id == sourceArea.Id ? evidenceArea : area).ToArray(),
            source.Dialogues,
            source.Quests);
    }

    internal StudioUx2EnvBSmokeResult RunUx2EnvBContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0)
        {
            return FailedUx2EnvBSmoke();
        }
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-b-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var evidence = CreateUx2EnvBEvidenceProject(sourceProject);
            var created = ProjectSession.Create(tempRoot, evidence);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx2EnvBSmoke();
            }
            var target = editing.Project!.Areas[0];
            var explicitSelection = editing.CreateAreaEnvironmentDraftUx2EnvB() is null &&
                                    editing.CreateAreaEnvironmentDraftUx2EnvB(target.Id) is not null;
            var draft = editing.CreateAreaEnvironmentDraftUx2EnvB(target.Id)!;
            var completeCollections = draft.Curves.Count >= 5 && draft.Events.Count >= 2 &&
                                      draft.Probes.Count >= 1 && draft.LightingVolumes.Count >= 1 &&
                                      draft.FogVolumes.Count >= 1 && draft.FogQualityProfiles.Count >= 2;

            var invalidClassification = editing.PreviewAreaEnvironmentUx2EnvB(
                draft with { ClassificationKey = "invalid key" });
            var invalidClassificationBlocked = !invalidClassification.Succeeded &&
                                               invalidClassification.Groups.Any(group =>
                                                   group.Diagnostics.Any(item => item.Code ==
                                                       AreaEnvironmentAuthoringValidator.ClassificationCode));
            var invalidCycle = editing.PreviewAreaEnvironmentUx2EnvB(
                draft with { DayLengthMinutes = 0 });
            var invalidCycleBlocked = !invalidCycle.Succeeded;
            var invalidLighting = editing.PreviewAreaEnvironmentUx2EnvB(
                draft with { Curves = draft.Curves.Take(1).ToArray() });
            var invalidLightingBlocked = !invalidLighting.Succeeded;
            var invalidFog = editing.PreviewAreaEnvironmentUx2EnvB(
                draft with { FogQualityProfiles = [] });
            var invalidFogBlocked = !invalidFog.Succeeded;

            var configured = draft with
            {
                DayLengthMinutes = 41.375f,
                AmbientIntensity = .4375f,
                FogDensity = .1375f,
                ClassificationKey = "natural-canopy",
            };
            var history = editing.History!;
            var projectBeforePreview = editing.Project;
            var revisionBeforePreview = history.State.Revision;
            var journalBeforePreview = history.Journal.ToArray();
            var dirtyBeforePreview = editing.IsDirty;
            var undoBeforePreview = history.CanUndo;
            var redoBeforePreview = history.CanRedo;
            var dryPreview = editing.PreviewAreaEnvironmentUx2EnvB(configured);
            var dryRun = dryPreview.Succeeded && dryPreview.CanApply && dryPreview.CanonicalDryRun &&
                         ReferenceEquals(projectBeforePreview, editing.Project) &&
                         history.State.Revision == revisionBeforePreview &&
                         history.Journal.SequenceEqual(journalBeforePreview) &&
                         editing.IsDirty == dirtyBeforePreview && history.CanUndo == undoBeforePreview &&
                         history.CanRedo == redoBeforePreview;

            var staleDraft = editing.CreateAreaEnvironmentDraftUx2EnvB(target.Id)!;
            var staleMutation = ApplyUx2EnvBRevisionMutation(editing, target.Id, "stale-draft");
            var staleDraftPreview = editing.PreviewAreaEnvironmentUx2EnvB(staleDraft);
            var staleDraftBlocked = staleMutation && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé",
                                        StringComparison.OrdinalIgnoreCase) && editing.Undo().Succeeded;

            var mutableTags = editing.CreateAreaEnvironmentDraftUx2EnvB(target.Id)!.Tags
                .Concat(["empreinte-initiale"]).ToArray();
            var mutableDraft = editing.CreateAreaEnvironmentDraftUx2EnvB(target.Id)! with { Tags = mutableTags };
            var mutablePreview = editing.PreviewAreaEnvironmentUx2EnvB(mutableDraft);
            mutableTags[^1] = "empreinte-modifiee";
            var staleFingerprintApply = editing.ApplyApprovedAreaEnvironmentUx2EnvB();
            var staleFingerprintBlocked = mutablePreview.Succeeded && !staleFingerprintApply.Succeeded;

            var staleApprovalDraft = editing.CreateAreaEnvironmentDraftUx2EnvB(target.Id)!;
            var staleApprovalPreview = editing.PreviewAreaEnvironmentUx2EnvB(staleApprovalDraft with
            {
                DayLengthMinutes = staleApprovalDraft.DayLengthMinutes + 1,
            });
            var staleApprovalMutation = ApplyUx2EnvBRevisionMutation(editing, target.Id, "stale-approval");
            var staleApprovalApply = editing.ApplyApprovedAreaEnvironmentUx2EnvB();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var freshDraft = editing.CreateAreaEnvironmentDraftUx2EnvB(target.Id)! with
            {
                DayLengthMinutes = 43.625f,
                AmbientIntensity = .46875f,
                FogDensity = .15625f,
                ClassificationKey = "natural-deep-canopy",
            };
            var freshPreview = editing.PreviewAreaEnvironmentUx2EnvB(freshDraft);
            var applied = editing.ApplyApprovedAreaEnvironmentUx2EnvB();
            var appliedProject = editing.Project!;
            var appliedTarget = appliedProject.Areas.Single(area => area.Id == target.Id);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName == "patch_area_environment" &&
                                   appliedTarget.Environment.Lighting.DayLengthMinutes.Equals(43.625f) &&
                                   appliedTarget.Environment.Lighting.AmbientIntensity.Equals(.46875f) &&
                                   appliedTarget.Environment.Fog.Density.Equals(.15625f);
            var environmentTailsPreserved =
                ReferenceEquals(sourceTarget.Environment.Audio, appliedTarget.Environment.Audio) &&
                ReferenceEquals(sourceTarget.Environment.Rendering, appliedTarget.Environment.Rendering) &&
                ReferenceEquals(sourceTarget.Environment.Sky, appliedTarget.Environment.Sky) &&
                ReferenceEquals(sourceTarget.Environment.Wind, appliedTarget.Environment.Wind) &&
                ReferenceEquals(sourceTarget.Environment.Weather, appliedTarget.Environment.Weather) &&
                ReferenceEquals(sourceTarget.Environment.Water, appliedTarget.Environment.Water) &&
                ReferenceEquals(sourceTarget.Environment.Reverb, appliedTarget.Environment.Reverb);
            var expectedAppliedTarget = sourceTarget.WithEnvironment(appliedTarget.Environment);
            var areaNeighborsPreserved = string.Equals(
                AreaJsonSerializer.Serialize(expectedAppliedTarget),
                AreaJsonSerializer.Serialize(appliedTarget),
                StringComparison.Ordinal);
            var projectNeighborsPreserved = ReferenceEquals(sourceState.Module, appliedProject.Module) &&
                                            sourceState.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                            sourceState.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                            sourceState.Quests.SequenceEqual(appliedProject.Quests) &&
                                            sourceState.Areas.Where(area => area.Id != target.Id).All(sourceArea =>
                                                ReferenceEquals(sourceArea, appliedProject.Areas.Single(area =>
                                                    area.Id == sourceArea.Id)));
            var appliedProjectJson = ProjectJsonSerializer.Serialize(appliedProject);
            var undo = editing.Undo();
            var undone = ReferenceEquals(sourceState, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, target.Id.ToString());
            var persistence = save.Succeeded && reopen.Succeeded && reopened.Project is not null &&
                              string.Equals(appliedProjectJson, ProjectJsonSerializer.Serialize(reopened.Project!),
                                  StringComparison.Ordinal);
            var structuredDiagnostics = invalidClassification.Groups.Count == 4 &&
                                        invalidClassification.Groups.Any(group =>
                                            group.Diagnostics.Count > 0 && group.State == "à corriger");
            var humanText = !ContainsTechnicalIdentifierUx2EnvB(
                string.Join(' ', editing.AreaEnvironmentChoicesUx2EnvB.Select(choice => choice.Label)) + " " +
                dryPreview.Message + " " + dryPreview.Summary + " " +
                string.Join(' ', dryPreview.Diff.Classification) + " " +
                string.Join(' ', dryPreview.Diff.Cycle) + " " +
                string.Join(' ', dryPreview.Diff.Lighting) + " " +
                string.Join(' ', dryPreview.Diff.Fog));
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvBTemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && completeCollections && invalidClassificationBlocked &&
                            invalidCycleBlocked && invalidLightingBlocked && invalidFogBlocked && dryRun &&
                            staleDraftBlocked && staleFingerprintBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && persistence && environmentTailsPreserved &&
                            areaNeighborsPreserved && projectNeighborsPreserved && structuredDiagnostics && humanText;
            return new StudioUx2EnvBSmokeResult(
                succeeded,
                explicitSelection,
                completeCollections,
                invalidClassificationBlocked,
                invalidCycleBlocked,
                invalidLightingBlocked,
                invalidFogBlocked,
                dryRun,
                staleDraftBlocked,
                staleFingerprintBlocked,
                staleApprovalBlocked,
                canonicalCommand,
                undoRedo,
                persistence,
                environmentTailsPreserved,
                areaNeighborsPreserved,
                projectNeighborsPreserved,
                structuredDiagnostics,
                humanText,
                cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_B_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2EnvBSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvBTemporaryWorkspace(tempRoot);
            if (!cleanup)
            {
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_B_CLEANUP_FAILED");
            }
        }
    }

    private static bool ApplyUx2EnvBRevisionMutation(
        StudioSessionController controller,
        AreaId areaId,
        string marker)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null) return false;
        var source = area.Environment;
        var candidate = new AreaEnvironmentProfile(
            source.SchemaVersion,
            source.Classification,
            source.Tags.Concat([$"ux2-env-b-{marker}"]).Distinct(StringComparer.Ordinal).ToArray(),
            source.Lighting,
            source.Fog,
            source.Audio,
            source.Rendering,
            source.Sky,
            source.Wind,
            source.Weather,
            source.Water,
            source.Reverb,
            source.Authoring,
            source.RequireAuthoringRuntimeMetadata);
        var result = controller._setAreaEnvironmentUx2EnvB.Handle(
            history.State,
            new PatchAreaEnvironmentCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                area.Id,
                candidate,
                false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvBTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-b-", StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvBSmokeResult FailedUx2EnvBSmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);
}
