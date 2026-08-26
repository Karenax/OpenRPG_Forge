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
using Forge.EngineAdapter.Audio;
using Forge.EngineAdapter.Rendering;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvCWindCode = "studio.environment.wind_invalid";
    private const string Ux2EnvCWeatherCode = "studio.environment.weather_invalid";
    private const string Ux2EnvCWaterCode = "studio.environment.water_invalid";
    private const string Ux2EnvCReverbCode = "studio.environment.reverb_invalid";

    private readonly PatchAreaEnvironmentHandler _setAreaEnvironmentUx2EnvC = new(new SystemClock());
    private StudioUx2EnvCDraft? _approvedClimateUx2EnvC;
    private ProjectRevision? _approvedClimateRevisionUx2EnvC;
    private string? _approvedClimateFingerprintUx2EnvC;

    internal IReadOnlyList<StudioUx2EnvCAreaChoice> ClimateChoicesUx2EnvC
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildClimateChoicesUx2EnvC(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvCAreaChoice> BuildClimateChoicesUx2EnvC(ProjectDefinition project)
    {
        var duplicateNames = project.Areas.GroupBy(area => area.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var displayName = ContainsTechnicalIdentifierUx2EnvC(area.DisplayName)
                    ? "Zone sans nom lisible"
                    : area.DisplayName.Trim();
                var name = duplicateNames[area.DisplayName] > 1
                    ? $"{displayName} · {HumanTokenUx2EnvC(area.Slug)}"
                    : displayName;
                var environment = area.Environment;
                return new StudioUx2EnvCAreaChoice(
                    area.Id,
                    $"{name} · vent {environment.Wind.Speed:0.##} m/s · " +
                    $"{environment.Weather.States.Count} météo(s) · " +
                    $"{environment.Water.Bodies.Count} eau(x) · " +
                    $"réverbération {HumanTokenUx2EnvC(environment.Reverb.DefaultPreset)}",
                    environment.Water.Bodies.Count > 0,
                    environment.Reverb.Zones.Count > 0);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvCDraft? CreateClimateDraftUx2EnvC(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1 ? CreateClimateDraftUx2EnvC(history.State.Revision, matches[0]) : null;
    }

    private static StudioUx2EnvCDraft CreateClimateDraftUx2EnvC(
        ProjectRevision revision,
        AreaDefinition area)
    {
        var environment = area.Environment;
        var wind = environment.Wind;
        var weather = environment.Weather;
        return new StudioUx2EnvCDraft(
            area.Id,
            revision,
            ClimateSourceFingerprintUx2EnvC(area),
            area.DisplayName,
            area.Slug,
            wind.DirectionX,
            wind.DirectionZ,
            wind.Speed,
            wind.GustStrength,
            wind.GustIntervalSeconds,
            wind.InfluencesVegetation,
            wind.InfluencesParticles,
            wind.AudioCue,
            wind.GameplayForce,
            wind.LocalZones.Select(zone => new StudioUx2EnvCWindZoneDraft(
                zone.Id,
                zone.Center,
                zone.Radius,
                zone.DirectionX,
                zone.DirectionZ,
                zone.Speed,
                zone.GustMultiplier)).ToArray(),
            weather.InitialStateId,
            weather.States.Select(state => new StudioUx2EnvCWeatherStateDraft(
                state.Id,
                state.Name,
                state.Kind,
                state.Intensity,
                state.Humidity,
                state.SurfaceWetness,
                state.WetnessMaterial,
                state.Lightning,
                state.PrecipitationResource,
                state.AudioCue,
                state.GameplayEffect,
                state.MinimumDurationSeconds,
                state.MaximumDurationSeconds,
                state.TransitionSeconds,
                state.Precipitation)).ToArray(),
            weather.ServerAuthoritative,
            weather.DeterministicSeed,
            weather.PersistState,
            weather.ReplicateState,
            environment.Water.Bodies.Select(body => new StudioUx2EnvCWaterBodyDraft(
                body.Id,
                body.Name,
                body.Kind,
                body.Center,
                body.Size,
                body.MaximumDepth,
                body.FlowDirectionX,
                body.FlowDirectionZ,
                body.FlowSpeed,
                body.Buoyancy,
                body.NavigationCost,
                body.SwimmingEnabled,
                body.Quality,
                body.MaterialResource,
                body.AudioCue)).ToArray(),
            environment.Reverb.DefaultPreset,
            environment.Reverb.Zones.Select(zone => new StudioUx2EnvCReverbZoneDraft(
                zone.Id,
                zone.Name,
                zone.Center,
                zone.Size,
                zone.Preset,
                zone.Wet,
                zone.Dry,
                zone.BlendSeconds,
                zone.ImpulseResource,
                zone.LicenseSpdx,
                zone.ContentSha256,
                zone.Provenance)).ToArray());
    }

    internal StudioUx2EnvCPreview PreviewClimateUx2EnvC(StudioUx2EnvCDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelClimateUx2EnvC();
        var history = History;
        if (history is null || Project is null)
            return FailedClimatePreviewUx2EnvC("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedClimatePreviewUx2EnvC(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la zone avant de prévisualiser.");
        if (!TryBuildClimateUx2EnvC(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var candidateEnvironment, out var diagnostics, out var message))
        {
            return new StudioUx2EnvCPreview(false, message, string.Empty, false,
                EmptyClimateDiffUx2EnvC(), GroupClimateDiagnosticsUx2EnvC(diagnostics),
                false, false, false, false, false, true);
        }

        var dryRun = _setAreaEnvironmentUx2EnvC.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2EnvCDiagnostic(
                item.Code,
                $"areas/{draft.AreaId}/environment",
                HumanizeClimateDiagnosticUx2EnvC(item.Code))).ToArray();
            return new StudioUx2EnvCPreview(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande d’environnement a refusé cet aperçu.",
                string.Empty, false,
                BuildClimateDiffUx2EnvC(sourceArea!.Environment, candidateEnvironment!),
                GroupClimateDiagnosticsUx2EnvC(commandDiagnostics),
                false, false, false, false, false, true);
        }

        var candidate = candidateEnvironment!;
        var listener = Vector3Value.Zero;
        var climatePlan = AreaClimatePlanBuilder.Build(
            candidate,
            candidate.Weather.InitialStateId,
            listener,
            "desktop");
        var windPlan = AreaProductionPlanBuilder.Wind(candidate.Wind, climatePlan.ActiveWindZone, []);
        var weatherSnapshot = WeatherRuntimeStateMachine.CreateInitial(candidate.Weather);
        var weatherState = WeatherRuntimeStateMachine.Resolve(candidate.Weather, weatherSnapshot);
        var weatherAdvance = WeatherRuntimeStateMachine.Advance(
            candidate.Weather,
            weatherSnapshot,
            weatherState.MaximumDurationSeconds,
            hasAuthority: true);
        var weatherPlan = AreaProductionPlanBuilder.Weather(candidate.Weather, weatherState);
        var waterPlans = candidate.Water.Bodies.Select(body =>
            AreaProductionPlanBuilder.Water(body, climatePlan.EffectiveWaterQuality, submergedFraction: 0)).ToArray();
        var reverbPlan = ReverbConvolutionPlanBuilder.Build(
            candidate.Reverb,
            listener,
            [],
            supportsConvolution: false);
        var diff = BuildClimateDiffUx2EnvC(sourceArea!.Environment, candidate);
        if (diff.HasChanges)
        {
            _approvedClimateUx2EnvC = draft;
            _approvedClimateRevisionUx2EnvC = history.State.Revision;
            _approvedClimateFingerprintUx2EnvC = ClimateDraftFingerprintUx2EnvC(draft);
        }

        var transitionText = weatherAdvance.Transition is null
            ? "état unique, aucune transition planifiée"
            : $"transition déterministe locale vers {HumanWeatherNameUx2EnvC(
                candidate.Weather.States.Single(item => item.Id == weatherAdvance.State.StateId).Name)}";
        var convolutionText = reverbPlan.Diagnostics.Any(item => item.Code ==
            ReverbConvolutionPlanBuilder.ConvolutionUnavailableCode)
            ? "impulsion demandée, fallback algorithmique seulement planifié"
            : "preset algorithmique planifié";
        var summary =
            $"Plans déterministes sans instanciation Godot : vent {windPlan.ShaderGlobals["wind_speed"]:0.##} m/s; " +
            $"météo {HumanWeatherNameUx2EnvC(weatherState.Name)}, {transitionText}; " +
            $"{waterPlans.Length} surface(s) d’eau planifiée(s), qualité {HumanWaterQualityUx2EnvC(climatePlan.EffectiveWaterQuality)}; " +
            $"réverbération {HumanTokenUx2EnvC(reverbPlan.Preset)}, {convolutionText}. " +
            $"Aucun réseau, ForgeScript, effet de flottabilité, convolution audio ni rendu live n’a été exécuté. " +
            $"Autorité déclarée : {(weatherPlan.ServerAuthoritative ? "oui" : "non")}; " +
            $"réplication déclarée : {(weatherPlan.Replicated ? "oui" : "non")}. " +
            $"Ces choix restent des données, pas une preuve de transport actif.";
        return new StudioUx2EnvCPreview(
            true,
            diff.HasChanges
                ? "Aperçu validé · aucun document, adaptateur moteur, script, réseau ni provider audio n’a été modifié."
                : "Aperçu validé · aucune différence sémantique à appliquer.",
            summary,
            diff.HasChanges,
            diff,
            GroupClimateDiagnosticsUx2EnvC([]),
            true,
            true,
            true,
            true,
            true,
            true);
    }

    internal StudioUx2EnvCActionResult ApplyApprovedClimateUx2EnvC()
    {
        var draft = _approvedClimateUx2EnvC;
        var history = History;
        if (draft is null || history is null || _approvedClimateRevisionUx2EnvC != history.State.Revision ||
            !string.Equals(_approvedClimateFingerprintUx2EnvC, ClimateDraftFingerprintUx2EnvC(draft),
                StringComparison.Ordinal))
        {
            CancelClimateUx2EnvC();
            return new StudioUx2EnvCActionResult(false,
                "Prévisualisez de nouveau le climat, l’eau et la réverbération avant de les appliquer.");
        }
        if (!TryBuildClimateUx2EnvC(draft, false, out var command, out _, out _, out _, out _, out var message))
        {
            CancelClimateUx2EnvC();
            return new StudioUx2EnvCActionResult(false, message);
        }
        var preview = _setAreaEnvironmentUx2EnvC.Handle(history.State, command! with { DryRun = true });
        if (!preview.Succeeded)
        {
            CancelClimateUx2EnvC();
            return new StudioUx2EnvCActionResult(false,
                HumanizeClimateDiagnosticUx2EnvC(preview.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaEnvironmentUx2EnvC.Handle(history.State, command!);
        CancelClimateUx2EnvC();
        if (!applied.Succeeded)
            return new StudioUx2EnvCActionResult(false,
                HumanizeClimateDiagnosticUx2EnvC(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new StudioUx2EnvCActionResult(true,
            "Climat, eau et réverbération appliqués · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelClimateUx2EnvC()
    {
        _approvedClimateUx2EnvC = null;
        _approvedClimateRevisionUx2EnvC = null;
        _approvedClimateFingerprintUx2EnvC = null;
    }

    internal void CloseUx2EnvC() => CancelClimateUx2EnvC();

    internal StudioUx2EnvCApprovalSnapshot CaptureClimateApprovalUx2EnvC() => new(
        _approvedClimateUx2EnvC,
        _approvedClimateRevisionUx2EnvC,
        _approvedClimateFingerprintUx2EnvC);

    internal void RestoreClimateApprovalUx2EnvC(StudioUx2EnvCApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedClimateUx2EnvC = snapshot.Draft;
        _approvedClimateRevisionUx2EnvC = snapshot.Revision;
        _approvedClimateFingerprintUx2EnvC = snapshot.Fingerprint;
    }

    private bool TryBuildClimateUx2EnvC(
        StudioUx2EnvCDraft draft,
        bool dryRun,
        out PatchAreaEnvironmentCommand? command,
        out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea,
        out AreaEnvironmentProfile? candidateEnvironment,
        out IReadOnlyList<StudioUx2EnvCDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
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
        if (!string.Equals(draft.SourceFingerprint, ClimateSourceFingerprintUx2EnvC(sourceArea),
                StringComparison.Ordinal))
        {
            message = "L’environnement ou un voisin de la zone a changé. Rechargez la fiche avant de prévisualiser.";
            return false;
        }
        var inputDiagnostics = ValidateClimateDraftUx2EnvC(draft).ToArray();
        if (inputDiagnostics.Length > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var source = sourceArea.Environment;
            var sourceWindZones = source.Wind.LocalZones.ToDictionary(item => item.Id);
            var windZones = draft.WindZones.Select(item =>
            {
                if (sourceWindZones.TryGetValue(item.Id, out var existing) && SameWindZoneUx2EnvC(item, existing))
                    return existing;
                return new WindZoneProfile(item.Id, item.Center, item.Radius, item.DirectionX,
                    item.DirectionZ, item.Speed, item.GustMultiplier);
            }).ToArray();
            var rebuiltWind = new WindProfile(
                source.Wind.SchemaVersion,
                draft.WindDirectionX,
                draft.WindDirectionZ,
                draft.WindSpeed,
                draft.WindGustStrength,
                draft.WindGustIntervalSeconds,
                draft.WindInfluencesVegetation,
                draft.WindInfluencesParticles,
                draft.WindAudioCue,
                draft.WindGameplayForce,
                windZones);
            var wind = SameWindUx2EnvC(source.Wind, rebuiltWind) ? source.Wind : rebuiltWind;

            var sourceWeatherStates = source.Weather.States.ToDictionary(item => item.Id);
            var weatherStates = draft.WeatherStates.Select(item =>
            {
                if (sourceWeatherStates.TryGetValue(item.Id, out var existing) &&
                    SameWeatherStateUx2EnvC(item, existing)) return existing;
                return new WeatherStateProfile(
                    item.Id,
                    item.Name,
                    item.Kind,
                    item.Intensity,
                    item.Humidity,
                    item.SurfaceWetness,
                    item.Lightning,
                    item.PrecipitationResource,
                    item.AudioCue,
                    item.GameplayEffect,
                    item.MinimumDurationSeconds,
                    item.MaximumDurationSeconds,
                    item.TransitionSeconds,
                    item.Precipitation,
                    item.WetnessMaterial);
            }).ToArray();
            var rebuiltWeather = new WeatherProfile(
                source.Weather.SchemaVersion,
                draft.InitialWeatherStateId,
                weatherStates,
                draft.WeatherServerAuthoritative,
                draft.WeatherDeterministicSeed,
                draft.WeatherPersistState,
                draft.WeatherReplicateState);
            var weather = SameWeatherUx2EnvC(source.Weather, rebuiltWeather) ? source.Weather : rebuiltWeather;

            var sourceWaterBodies = source.Water.Bodies.ToDictionary(item => item.Id);
            var waterBodies = draft.WaterBodies.Select(item =>
            {
                if (sourceWaterBodies.TryGetValue(item.Id, out var existing) && SameWaterBodyUx2EnvC(item, existing))
                    return existing;
                return new WaterBodyProfile(
                    item.Id,
                    item.Name,
                    item.Kind,
                    item.Center,
                    item.Size,
                    item.MaximumDepth,
                    item.FlowDirectionX,
                    item.FlowDirectionZ,
                    item.FlowSpeed,
                    item.Buoyancy,
                    item.NavigationCost,
                    item.SwimmingEnabled,
                    item.Quality,
                    item.MaterialResource,
                    item.AudioCue);
            }).ToArray();
            var rebuiltWater = new WaterProfile(source.Water.SchemaVersion, waterBodies);
            var water = source.Water.Bodies.SequenceEqual(rebuiltWater.Bodies) ? source.Water : rebuiltWater;

            var sourceReverbZones = source.Reverb.Zones.ToDictionary(item => item.Id);
            var reverbZones = draft.ReverbZones.Select(item =>
            {
                if (sourceReverbZones.TryGetValue(item.Id, out var existing) &&
                    SameReverbZoneUx2EnvC(item, existing)) return existing;
                return new ReverbZoneProfile(
                    item.Id,
                    item.Name,
                    item.Center,
                    item.Size,
                    item.Preset,
                    item.Wet,
                    item.Dry,
                    item.BlendSeconds,
                    item.ImpulseResource,
                    item.LicenseSpdx,
                    item.ContentSha256,
                    item.Provenance);
            }).ToArray();
            var rebuiltReverb = new AreaReverbProfile(
                source.Reverb.SchemaVersion,
                draft.ReverbDefaultPreset,
                reverbZones);
            var reverb = string.Equals(source.Reverb.DefaultPreset, rebuiltReverb.DefaultPreset,
                             StringComparison.Ordinal) && source.Reverb.Zones.SequenceEqual(rebuiltReverb.Zones)
                ? source.Reverb
                : rebuiltReverb;

            var environment = new AreaEnvironmentProfile(
                source.SchemaVersion,
                source.Classification,
                source.Tags,
                source.Lighting,
                source.Fog,
                source.Audio,
                source.Rendering,
                source.Sky,
                wind,
                weather,
                water,
                reverb,
                source.Authoring,
                source.RequireAuthoringRuntimeMetadata);
            var diff = BuildClimateDiffUx2EnvC(source, environment);
            if (!diff.HasChanges)
                environment = source;
            var updatedArea = ReferenceEquals(environment, source) ? sourceArea : sourceArea.WithEnvironment(environment);
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
            var canonicalDiagnostics = ProjectValidator.Validate(candidateProject).Select(item =>
                new StudioUx2EnvCDiagnostic(
                    item.Code,
                    item.Path,
                    HumanizeClimateDiagnosticUx2EnvC(item.Code))).ToArray();
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
                                           KeyNotFoundException)
        {
            var diagnostic = new StudioUx2EnvCDiagnostic(
                ClimateConstructionCodeUx2EnvC(exception, draft),
                $"areas/{draft.AreaId}/environment",
                ClimateConstructionMessageUx2EnvC(exception));
            diagnostics = [diagnostic];
            message = diagnostic.Message;
            return false;
        }
    }

    private static IReadOnlyList<StudioUx2EnvCDiagnostic> ValidateClimateDraftUx2EnvC(StudioUx2EnvCDraft draft)
    {
        var result = new List<StudioUx2EnvCDiagnostic>();
        if (!FiniteUx2EnvC(draft.WindDirectionX, draft.WindDirectionZ, draft.WindSpeed,
                draft.WindGustStrength, draft.WindGustIntervalSeconds, draft.WindGameplayForce) ||
            draft.WindSpeed < 0 || draft.WindGustStrength is < 0 or > 8 ||
            draft.WindGustIntervalSeconds is < .1f or > 3_600 || draft.WindGameplayForce is < 0 or > 100 ||
            draft.WindZones.Any(zone => zone.Id == Guid.Empty || zone.Radius <= 0 || zone.Speed < 0 ||
                zone.GustMultiplier is < 0 or > 8 || !FiniteUx2EnvC(zone.Radius, zone.DirectionX,
                    zone.DirectionZ, zone.Speed, zone.GustMultiplier, zone.Center.X, zone.Center.Y, zone.Center.Z)))
            result.Add(new(Ux2EnvCWindCode, $"areas/{draft.AreaId}/environment/wind",
                "Vent : direction, vitesse, rafales, force et zones locales doivent rester dans leurs plages."));
        if (draft.WindZones.Select(item => item.Id).Distinct().Count() != draft.WindZones.Count)
            result.Add(new(Ux2EnvCWindCode, $"areas/{draft.AreaId}/environment/wind/localZones",
                "Les zones de vent doivent garder des identités internes uniques."));

        if (draft.WeatherStates.Count == 0 ||
            draft.WeatherStates.All(state => state.Id != draft.InitialWeatherStateId) ||
            draft.WeatherStates.Select(item => item.Id).Distinct().Count() != draft.WeatherStates.Count ||
            draft.WeatherStates.Any(state => state.Id == Guid.Empty || string.IsNullOrWhiteSpace(state.Name) ||
                !Enum.IsDefined(state.Kind) ||
                state.Intensity is < 0 or > 1 || state.Humidity is < 0 or > 1 ||
                state.SurfaceWetness is < 0 or > 1 || state.MinimumDurationSeconds < 0 ||
                state.MaximumDurationSeconds < state.MinimumDurationSeconds ||
                state.TransitionSeconds is < 0 or > 3_600 ||
                state.Precipitation is null ||
                state.WetnessMaterial is null ||
                state.WetnessMaterial.SchemaVersion != SurfaceWetnessMaterialProfile.CurrentSchemaVersion ||
                state.WetnessMaterial.AlbedoDarkening is < 0 or > .35f ||
                state.WetnessMaterial.RoughnessReduction is < 0 or > .8f ||
                state.WetnessMaterial.SpecularIncrease is < 0 or > .6f ||
                !FiniteUx2EnvC(state.Intensity, state.Humidity, state.SurfaceWetness,
                    state.MinimumDurationSeconds, state.MaximumDurationSeconds, state.TransitionSeconds,
                    state.WetnessMaterial.AlbedoDarkening, state.WetnessMaterial.RoughnessReduction,
                    state.WetnessMaterial.SpecularIncrease) ||
                !SafeOptionalResourceUx2EnvC(state.PrecipitationResource)))
            result.Add(new(Ux2EnvCWeatherCode, $"areas/{draft.AreaId}/environment/weather",
                "Météo : choisissez un état initial et gardez des états uniques, bornés et des ressources relatives sûres."));

        if (draft.WaterBodies.Select(item => item.Id).Distinct().Count() != draft.WaterBodies.Count ||
            draft.WaterBodies.Any(body => body.Id == Guid.Empty || string.IsNullOrWhiteSpace(body.Name) ||
                !Enum.IsDefined(body.Kind) || !Enum.IsDefined(body.Quality) ||
                body.Size.X <= 0 || body.Size.Y <= 0 || body.Size.Z <= 0 || body.MaximumDepth <= 0 ||
                body.FlowSpeed is < 0 or > 100 || body.Buoyancy is < 0 or > 10 ||
                body.NavigationCost is < .1f or > 100 ||
                !FiniteUx2EnvC(body.Center.X, body.Center.Y, body.Center.Z,
                    body.Size.X, body.Size.Y, body.Size.Z, body.MaximumDepth,
                    body.FlowDirectionX, body.FlowDirectionZ, body.FlowSpeed, body.Buoyancy, body.NavigationCost) ||
                !SafeOptionalResourceUx2EnvC(body.MaterialResource)))
            result.Add(new(Ux2EnvCWaterCode, $"areas/{draft.AreaId}/environment/water",
                "Eau : volumes, profondeur, flux, paramètres de navigation et ressources doivent être valides."));

        if (string.IsNullOrWhiteSpace(draft.ReverbDefaultPreset) ||
            draft.ReverbZones.Select(item => item.Id).Distinct().Count() != draft.ReverbZones.Count ||
            draft.ReverbZones.Any(zone => zone.Id == Guid.Empty || string.IsNullOrWhiteSpace(zone.Name) ||
                string.IsNullOrWhiteSpace(zone.Preset) || zone.Size.X <= 0 || zone.Size.Y <= 0 || zone.Size.Z <= 0 ||
                zone.Wet is < 0 or > 1 || zone.Dry is < 0 or > 1 || zone.BlendSeconds is < 0 or > 60 ||
                !FiniteUx2EnvC(zone.Center.X, zone.Center.Y, zone.Center.Z,
                    zone.Size.X, zone.Size.Y, zone.Size.Z, zone.Wet, zone.Dry, zone.BlendSeconds) ||
                !SafeOptionalResourceUx2EnvC(zone.ImpulseResource) || !string.IsNullOrWhiteSpace(zone.ImpulseResource) &&
                (string.IsNullOrWhiteSpace(zone.LicenseSpdx) || string.IsNullOrWhiteSpace(zone.Provenance) ||
                 (zone.ContentSha256 ?? string.Empty).Length != 64 ||
                 !(zone.ContentSha256 ?? string.Empty).All(Uri.IsHexDigit))))
            result.Add(new(Ux2EnvCReverbCode, $"areas/{draft.AreaId}/environment/reverb",
                "Réverbération : zones, mix et impulsions doivent inclure une provenance SPDX et SHA-256 complète."));
        return result;
    }

    private static StudioUx2EnvCSemanticDiff BuildClimateDiffUx2EnvC(
        AreaEnvironmentProfile source,
        AreaEnvironmentProfile candidate)
    {
        var wind = new List<string>();
        if (!NearlyEqualUx2EnvC(source.Wind.DirectionX, candidate.Wind.DirectionX) ||
            !NearlyEqualUx2EnvC(source.Wind.DirectionZ, candidate.Wind.DirectionZ) ||
            !source.Wind.Speed.Equals(candidate.Wind.Speed) ||
            !source.Wind.GustStrength.Equals(candidate.Wind.GustStrength) ||
            !source.Wind.GustIntervalSeconds.Equals(candidate.Wind.GustIntervalSeconds))
            wind.Add("Direction, vitesse ou rafales globales modifiées.");
        if (source.Wind.InfluencesVegetation != candidate.Wind.InfluencesVegetation ||
            source.Wind.InfluencesParticles != candidate.Wind.InfluencesParticles ||
            !string.Equals(source.Wind.AudioCue, candidate.Wind.AudioCue, StringComparison.Ordinal) ||
            !source.Wind.GameplayForce.Equals(candidate.Wind.GameplayForce))
            wind.Add("Influences, repère audio ou force de politique modifiés.");
        if (!source.Wind.LocalZones.SequenceEqual(candidate.Wind.LocalZones))
            wind.Add($"Zones locales : {source.Wind.LocalZones.Count} → {candidate.Wind.LocalZones.Count}.");

        var weather = new List<string>();
        if (source.Weather.InitialStateId != candidate.Weather.InitialStateId)
            weather.Add($"État initial : {HumanWeatherNameUx2EnvC(source.Weather.States.Single(item =>
                item.Id == source.Weather.InitialStateId).Name)} → {HumanWeatherNameUx2EnvC(
                candidate.Weather.States.Single(item => item.Id == candidate.Weather.InitialStateId).Name)}.");
        if (!source.Weather.States.SequenceEqual(candidate.Weather.States))
            weather.Add($"États : {source.Weather.States.Count} → {candidate.Weather.States.Count}.");
        if (source.Weather.ServerAuthoritative != candidate.Weather.ServerAuthoritative ||
            source.Weather.DeterministicSeed != candidate.Weather.DeterministicSeed ||
            source.Weather.PersistState != candidate.Weather.PersistState ||
            source.Weather.ReplicateState != candidate.Weather.ReplicateState)
            weather.Add("Politique déterministe, persistance ou réplication déclarée modifiée.");

        var water = new List<string>();
        if (!source.Water.Bodies.SequenceEqual(candidate.Water.Bodies))
            water.Add($"Corps d’eau : {source.Water.Bodies.Count} → {candidate.Water.Bodies.Count}.");

        var reverb = new List<string>();
        if (!string.Equals(source.Reverb.DefaultPreset, candidate.Reverb.DefaultPreset, StringComparison.Ordinal))
            reverb.Add($"Preset global : {HumanTokenUx2EnvC(source.Reverb.DefaultPreset)} → " +
                       $"{HumanTokenUx2EnvC(candidate.Reverb.DefaultPreset)}.");
        if (!source.Reverb.Zones.SequenceEqual(candidate.Reverb.Zones))
            reverb.Add($"Zones spatiales : {source.Reverb.Zones.Count} → {candidate.Reverb.Zones.Count}.");
        return new StudioUx2EnvCSemanticDiff(wind, weather, water, reverb);
    }

    private static StudioUx2EnvCSemanticDiff EmptyClimateDiffUx2EnvC() => new([], [], [], []);

    private static StudioUx2EnvCPreview FailedClimatePreviewUx2EnvC(string message) => new(
        false, message, string.Empty, false, EmptyClimateDiffUx2EnvC(), GroupClimateDiagnosticsUx2EnvC([]),
        false, false, false, false, false, true);

    private static IReadOnlyList<StudioUx2EnvCDiagnosticGroup> GroupClimateDiagnosticsUx2EnvC(
        IReadOnlyList<StudioUx2EnvCDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            ("wind", "Vent global et local", "/wind", Ux2EnvCWindCode),
            ("weather", "Météo déterministe", "/weather", Ux2EnvCWeatherCode),
            ("water", "Eau et navigation déclarée", "/water", Ux2EnvCWaterCode),
            ("reverb", "Réverbération spatiale", "/reverb", Ux2EnvCReverbCode),
        };
        var matched = diagnostics.Where(item => definitions.Any(definition =>
                item.Code == definition.Item4 || item.Path.Contains(definition.Item3,
                    StringComparison.OrdinalIgnoreCase)))
            .ToHashSet();
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item => item.Code == definition.Item4 ||
                    item.Path.Contains(definition.Item3, StringComparison.OrdinalIgnoreCase) ||
                    definition.Item1 == "wind" && !matched.Contains(item))
                .ToArray();
            return new StudioUx2EnvCDiagnosticGroup(definition.Item1, definition.Item2,
                items.Length == 0 ? "conforme" : "à corriger", items);
        }).ToArray();
    }

    private static string HumanizeClimateDiagnosticUx2EnvC(string? code) => code switch
    {
        Ux2EnvCWindCode => "Le profil de vent global ou local contient une valeur invalide.",
        Ux2EnvCWeatherCode => "La météo demande des états uniques, un état initial et des plages valides.",
        Ux2EnvCWaterCode => "Un corps d’eau contient une dimension, un flux ou une ressource invalide.",
        Ux2EnvCReverbCode => "Une zone de réverbération ou son impulsion n’est pas gouvernée correctement.",
        "command.revision_conflict" => "Le projet a changé. Rechargez la zone et relancez l’aperçu.",
        _ => "La validation canonique du projet a refusé l’environnement ; consultez le groupe concerné.",
    };

    private static string ClimateConstructionCodeUx2EnvC(Exception exception, StudioUx2EnvCDraft draft)
    {
        if (draft.WeatherStates.Count == 0 || draft.WeatherStates.All(item => item.Id != draft.InitialWeatherStateId))
            return Ux2EnvCWeatherCode;
        if (draft.WaterBodies.Any(item => item.Size.X <= 0 || item.Size.Y <= 0 || item.Size.Z <= 0))
            return Ux2EnvCWaterCode;
        if (draft.ReverbZones.Any(item => !string.IsNullOrWhiteSpace(item.ImpulseResource)))
            return Ux2EnvCReverbCode;
        return exception is ArgumentOutOfRangeException ? Ux2EnvCWindCode : Ux2EnvCWeatherCode;
    }

    private static string ClimateConstructionMessageUx2EnvC(Exception exception) => exception switch
    {
        ArgumentOutOfRangeException => "Une valeur de climat, d’eau ou de mix sort de la plage canonique.",
        ArgumentException => "Une ressource, une identité ou une collection de climat est invalide.",
        _ => "Le profil climat/eau/réverbération ne peut pas être reconstruit.",
    };

    private static string ClimateSourceFingerprintUx2EnvC(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string ClimateDraftFingerprintUx2EnvC(StudioUx2EnvCDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString());
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint);
            writer.Write(draft.WindDirectionX); writer.Write(draft.WindDirectionZ); writer.Write(draft.WindSpeed);
            writer.Write(draft.WindGustStrength); writer.Write(draft.WindGustIntervalSeconds);
            writer.Write(draft.WindInfluencesVegetation); writer.Write(draft.WindInfluencesParticles);
            writer.Write(draft.WindAudioCue ?? string.Empty); writer.Write(draft.WindGameplayForce);
            writer.Write(draft.WindZones.Count);
            foreach (var item in draft.WindZones)
            {
                writer.Write(item.Id.ToByteArray()); WriteVectorUx2EnvC(writer, item.Center);
                writer.Write(item.Radius); writer.Write(item.DirectionX); writer.Write(item.DirectionZ);
                writer.Write(item.Speed); writer.Write(item.GustMultiplier);
            }
            writer.Write(draft.InitialWeatherStateId.ToByteArray());
            writer.Write(draft.WeatherStates.Count);
            foreach (var item in draft.WeatherStates)
            {
                writer.Write(item.Id.ToByteArray()); writer.Write(item.Name ?? string.Empty); writer.Write((int)item.Kind);
                writer.Write(item.Intensity); writer.Write(item.Humidity); writer.Write(item.SurfaceWetness);
                writer.Write(item.WetnessMaterial.SchemaVersion);
                writer.Write(item.WetnessMaterial.AlbedoDarkening);
                writer.Write(item.WetnessMaterial.RoughnessReduction);
                writer.Write(item.WetnessMaterial.SpecularIncrease);
                writer.Write(item.Lightning); writer.Write(item.PrecipitationResource ?? string.Empty);
                writer.Write(item.AudioCue ?? string.Empty); writer.Write(item.GameplayEffect ?? string.Empty);
                writer.Write(item.MinimumDurationSeconds); writer.Write(item.MaximumDurationSeconds);
                writer.Write(item.TransitionSeconds);
                writer.Write(item.Precipitation.SchemaVersion);
                writer.Write(item.Precipitation.DirectionX); writer.Write(item.Precipitation.DirectionZ);
                writer.Write(item.Precipitation.WindInfluence); writer.Write(item.Precipitation.SpreadDegrees);
                writer.Write(item.Precipitation.MinimumVelocity); writer.Write(item.Precipitation.MaximumVelocity);
                writer.Write(item.Precipitation.GravityY); writer.Write(item.Precipitation.ParticlesPerIntensity);
                writer.Write(item.Precipitation.MinimumParticles); writer.Write(item.Precipitation.ParticleWidth);
                writer.Write(item.Precipitation.ParticleLength); writer.Write(item.Precipitation.LifetimeSeconds);
                writer.Write(item.Precipitation.Randomness); writer.Write(item.Precipitation.Color);
            }
            writer.Write(draft.WeatherServerAuthoritative); writer.Write(draft.WeatherDeterministicSeed);
            writer.Write(draft.WeatherPersistState); writer.Write(draft.WeatherReplicateState);
            writer.Write(draft.WaterBodies.Count);
            foreach (var item in draft.WaterBodies)
            {
                writer.Write(item.Id.ToByteArray()); writer.Write(item.Name ?? string.Empty); writer.Write((int)item.Kind);
                WriteVectorUx2EnvC(writer, item.Center); WriteVectorUx2EnvC(writer, item.Size);
                writer.Write(item.MaximumDepth); writer.Write(item.FlowDirectionX); writer.Write(item.FlowDirectionZ);
                writer.Write(item.FlowSpeed); writer.Write(item.Buoyancy); writer.Write(item.NavigationCost);
                writer.Write(item.SwimmingEnabled); writer.Write((int)item.Quality);
                writer.Write(item.MaterialResource ?? string.Empty); writer.Write(item.AudioCue ?? string.Empty);
            }
            writer.Write(draft.ReverbDefaultPreset ?? string.Empty); writer.Write(draft.ReverbZones.Count);
            foreach (var item in draft.ReverbZones)
            {
                writer.Write(item.Id.ToByteArray()); writer.Write(item.Name ?? string.Empty);
                WriteVectorUx2EnvC(writer, item.Center); WriteVectorUx2EnvC(writer, item.Size);
                writer.Write(item.Preset ?? string.Empty); writer.Write(item.Wet); writer.Write(item.Dry);
                writer.Write(item.BlendSeconds); writer.Write(item.ImpulseResource ?? string.Empty);
                writer.Write(item.LicenseSpdx ?? string.Empty); writer.Write(item.ContentSha256 ?? string.Empty);
                writer.Write(item.Provenance ?? string.Empty);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteVectorUx2EnvC(BinaryWriter writer, Vector3Value value)
    {
        writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z);
    }

    private static bool SameWindUx2EnvC(WindProfile source, WindProfile candidate) =>
        NearlyEqualUx2EnvC(source.DirectionX, candidate.DirectionX) &&
        NearlyEqualUx2EnvC(source.DirectionZ, candidate.DirectionZ) &&
        source.Speed.Equals(candidate.Speed) && source.GustStrength.Equals(candidate.GustStrength) &&
        source.GustIntervalSeconds.Equals(candidate.GustIntervalSeconds) &&
        source.InfluencesVegetation == candidate.InfluencesVegetation &&
        source.InfluencesParticles == candidate.InfluencesParticles &&
        string.Equals(source.AudioCue, candidate.AudioCue, StringComparison.Ordinal) &&
        source.GameplayForce.Equals(candidate.GameplayForce) &&
        source.LocalZones.SequenceEqual(candidate.LocalZones);

    private static bool SameWeatherUx2EnvC(WeatherProfile source, WeatherProfile candidate) =>
        source.InitialStateId == candidate.InitialStateId && source.States.SequenceEqual(candidate.States) &&
        source.ServerAuthoritative == candidate.ServerAuthoritative &&
        source.DeterministicSeed == candidate.DeterministicSeed &&
        source.PersistState == candidate.PersistState && source.ReplicateState == candidate.ReplicateState;

    private static bool SameWindZoneUx2EnvC(StudioUx2EnvCWindZoneDraft draft, WindZoneProfile source) =>
        draft.Id == source.Id && draft.Center == source.Center && draft.Radius.Equals(source.Radius) &&
        NormalizedDirectionEqualUx2EnvC(draft.DirectionX, draft.DirectionZ, source.DirectionX, source.DirectionZ) &&
        draft.Speed.Equals(source.Speed) && draft.GustMultiplier.Equals(source.GustMultiplier);

    private static bool SameWeatherStateUx2EnvC(
        StudioUx2EnvCWeatherStateDraft draft,
        WeatherStateProfile source) =>
        draft.Id == source.Id && string.Equals(draft.Name.Trim(), source.Name, StringComparison.Ordinal) &&
        draft.Kind == source.Kind && draft.Intensity.Equals(source.Intensity) &&
        draft.Humidity.Equals(source.Humidity) && draft.SurfaceWetness.Equals(source.SurfaceWetness) &&
        draft.WetnessMaterial == source.WetnessMaterial &&
        draft.Lightning == source.Lightning && string.Equals(NormalizeResourceUx2EnvC(draft.PrecipitationResource),
            source.PrecipitationResource, StringComparison.Ordinal) &&
        string.Equals(draft.AudioCue.Trim(), source.AudioCue, StringComparison.Ordinal) &&
        string.Equals(draft.GameplayEffect.Trim(), source.GameplayEffect, StringComparison.Ordinal) &&
        draft.MinimumDurationSeconds.Equals(source.MinimumDurationSeconds) &&
        draft.MaximumDurationSeconds.Equals(source.MaximumDurationSeconds) &&
        draft.TransitionSeconds.Equals(source.TransitionSeconds) && draft.Precipitation == source.Precipitation;

    private static bool SameWaterBodyUx2EnvC(StudioUx2EnvCWaterBodyDraft draft, WaterBodyProfile source) =>
        draft.Id == source.Id && string.Equals(draft.Name.Trim(), source.Name, StringComparison.Ordinal) &&
        draft.Kind == source.Kind && draft.Center == source.Center && draft.Size == source.Size &&
        draft.MaximumDepth.Equals(source.MaximumDepth) &&
        NormalizedDirectionEqualUx2EnvC(draft.FlowDirectionX, draft.FlowDirectionZ,
            source.FlowDirectionX, source.FlowDirectionZ) && draft.FlowSpeed.Equals(source.FlowSpeed) &&
        draft.Buoyancy.Equals(source.Buoyancy) && draft.NavigationCost.Equals(source.NavigationCost) &&
        draft.SwimmingEnabled == source.SwimmingEnabled && draft.Quality == source.Quality &&
        string.Equals(NormalizeResourceUx2EnvC(draft.MaterialResource), source.MaterialResource,
            StringComparison.Ordinal) && string.Equals(draft.AudioCue.Trim(), source.AudioCue, StringComparison.Ordinal);

    private static bool SameReverbZoneUx2EnvC(StudioUx2EnvCReverbZoneDraft draft, ReverbZoneProfile source) =>
        draft.Id == source.Id && string.Equals(draft.Name.Trim(), source.Name, StringComparison.Ordinal) &&
        draft.Center == source.Center && draft.Size == source.Size &&
        string.Equals(draft.Preset.Trim().ToLowerInvariant(), source.Preset, StringComparison.Ordinal) &&
        draft.Wet.Equals(source.Wet) && draft.Dry.Equals(source.Dry) &&
        draft.BlendSeconds.Equals(source.BlendSeconds) &&
        string.Equals(NormalizeResourceUx2EnvC(draft.ImpulseResource), source.ImpulseResource,
            StringComparison.Ordinal) && string.Equals(draft.LicenseSpdx.Trim(), source.LicenseSpdx,
            StringComparison.Ordinal) && string.Equals(draft.ContentSha256.Trim().ToLowerInvariant(),
            source.ContentSha256, StringComparison.Ordinal) && string.Equals(draft.Provenance.Trim(),
            source.Provenance, StringComparison.Ordinal);

    private static bool NormalizedDirectionEqualUx2EnvC(float x, float z, float sourceX, float sourceZ)
    {
        var length = MathF.Sqrt((x * x) + (z * z));
        var normalizedX = length <= .0001f ? 0 : x / length;
        var normalizedZ = length <= .0001f ? 0 : z / length;
        return NearlyEqualUx2EnvC(normalizedX, sourceX) && NearlyEqualUx2EnvC(normalizedZ, sourceZ);
    }

    private static bool NearlyEqualUx2EnvC(float left, float right) => MathF.Abs(left - right) <= .000001f;

    private static bool SafeOptionalResourceUx2EnvC(string value)
    {
        try { _ = WeatherStateProfile.NormalizeOptionalResource(value ?? string.Empty); return true; }
        catch (ArgumentException) { return false; }
    }

    private static string NormalizeResourceUx2EnvC(string value) =>
        WeatherStateProfile.NormalizeOptionalResource(value ?? string.Empty);

    private static bool FiniteUx2EnvC(params float[] values) => values.All(float.IsFinite);

    private static string HumanWeatherNameUx2EnvC(string value) =>
        ContainsTechnicalIdentifierUx2EnvC(value) || string.IsNullOrWhiteSpace(value)
            ? "météo sans nom lisible"
            : value.Trim();

    private static string HumanWaterQualityUx2EnvC(WaterQualityTier value) => value switch
    {
        WaterQualityTier.Low => "basse",
        WaterQualityTier.Medium => "moyenne",
        WaterQualityTier.High => "élevée",
        WaterQualityTier.Ultra => "ultra",
        _ => "personnalisée",
    };

    private static string HumanTokenUx2EnvC(string value)
    {
        var normalized = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(normalized) || ContainsTechnicalIdentifierUx2EnvC(normalized)
            ? "profil sans nom lisible"
            : normalized;
    }

    private static bool ContainsTechnicalIdentifierUx2EnvC(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) || text.Contains(".json", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    internal static ProjectDefinition CreateUx2EnvCContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var area = source.Areas.FirstOrDefault() ??
                   throw new InvalidOperationException("The contract project requires an area.");
        var environment = area.Environment;
        var clearId = Guid.NewGuid();
        var rainId = Guid.NewGuid();
        var stormId = Guid.NewGuid();
        var wind = new WindProfile(WindProfile.CurrentSchemaVersion, .8f, .25f, 4.25f, .6f, 6.5f,
            true, true, "wind.forest", 2.5f,
            [new(Guid.NewGuid(), new(4, 0, -2), 12, -.2f, 1, 7, 1.8f)]);
        var weather = new WeatherProfile(WeatherProfile.CurrentSchemaVersion, clearId,
            [
                new(clearId, "Clair", WeatherKind.Clear, 0, .3f, 0, false, string.Empty,
                    string.Empty, string.Empty, 300, 700, 8),
                new(rainId, "Pluie douce", WeatherKind.Rain, .55f, .8f, .65f, false,
                    "effects/weather/rain.tres", "weather.rain", "surface.wet", 120, 360, 10),
                new(stormId, "Orage", WeatherKind.Storm, .9f, .95f, 1, true,
                    "effects/weather/storm.tres", "weather.storm", "surface.storm", 60, 180, 6),
            ], true, 4_271, true, true);
        var water = new WaterProfile(WaterProfile.CurrentSchemaVersion,
            [
                new(Guid.NewGuid(), "Lac calme", WaterBodyKind.Plane, new(0, 1, 0), new(30, 2, 24), 4,
                    1, 0, .3f, 1.2f, 2, true, WaterQualityTier.High,
                    "materials/water/lake.tres", "water.lake"),
                new(Guid.NewGuid(), "Rivière basse", WaterBodyKind.River, new(5, .5f, 8), new(8, 1, 36), 2,
                    .25f, 1, 1.6f, .8f, 4, true, WaterQualityTier.Medium,
                    "materials/water/river.tres", "water.river"),
            ]);
        var reverb = new AreaReverbProfile(AreaReverbProfile.CurrentSchemaVersion, "forest",
            [
                new(Guid.NewGuid(), "Sous-bois", new(30, 0, 30), new(20, 8, 20), "forest", .32f, 1, .8f,
                    string.Empty, string.Empty, string.Empty, "builtin://audio/reverb"),
                new(Guid.NewGuid(), "Grotte humide", Vector3Value.Zero, new(12, 6, 16), "cave", .65f, .85f, 1.4f,
                    "audio/impulses/cave.wav", "CC0-1.0", new string('a', 64), "fixture:ux2-env-c"),
            ]);
        var candidate = new AreaEnvironmentProfile(environment.SchemaVersion, environment.Classification,
            environment.Tags, environment.Lighting, environment.Fog, environment.Audio, environment.Rendering,
            environment.Sky, wind, weather, water, reverb, environment.Authoring,
            environment.RequireAuthoringRuntimeMetadata);
        var updated = area.WithEnvironment(candidate);
        return new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? updated : item).ToArray(), source.Dialogues, source.Quests);
    }

    internal StudioUx2EnvCSmokeResult RunUx2EnvCContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0) return FailedUx2EnvCSmoke();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-c-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, CreateUx2EnvCContractProject(sourceProject));
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded) return FailedUx2EnvCSmoke();
            var target = editing.Project!.Areas[0];
            var explicitSelection = editing.CreateClimateDraftUx2EnvC() is null &&
                                    editing.CreateClimateDraftUx2EnvC(target.Id) is not null;
            var draft = editing.CreateClimateDraftUx2EnvC(target.Id)!;
            var completeCollections = draft.WindZones.Count >= 1 && draft.WeatherStates.Count >= 3 &&
                                      draft.WaterBodies.Count >= 2 && draft.ReverbZones.Count >= 2;
            var invalidWind = editing.PreviewClimateUx2EnvC(draft with
            {
                WindZones = draft.WindZones.Select((item, index) => index == 0
                    ? item with { Radius = 0 }
                    : item).ToArray(),
            });
            var invalidWeather = editing.PreviewClimateUx2EnvC(draft with { WeatherStates = [] });
            var invalidWater = editing.PreviewClimateUx2EnvC(draft with
            {
                WaterBodies = draft.WaterBodies.Select((item, index) => index == 0
                    ? item with { Size = new Vector3Value(0, 1, 1) }
                    : item).ToArray(),
            });
            var invalidReverb = editing.PreviewClimateUx2EnvC(draft with
            {
                ReverbZones = draft.ReverbZones.Select(item => string.IsNullOrWhiteSpace(item.ImpulseResource)
                    ? item
                    : item with { LicenseSpdx = string.Empty }).ToArray(),
            });
            var invalidWindBlocked = !invalidWind.Succeeded;
            var invalidWeatherBlocked = !invalidWeather.Succeeded;
            var invalidWaterBlocked = !invalidWater.Succeeded;
            var invalidReverbBlocked = !invalidReverb.Succeeded;

            var configured = draft with
            {
                WindSpeed = 5.375f,
                WeatherDeterministicSeed = 4_999,
                ReverbDefaultPreset = "forest-wide",
            };
            var history = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = history.State.Revision;
            var journalBefore = history.Journal.ToArray();
            var dirtyBefore = editing.IsDirty;
            var undoBefore = history.CanUndo;
            var redoBefore = history.CanRedo;
            var dryPreview = editing.PreviewClimateUx2EnvC(configured);
            var dryRun = dryPreview.Succeeded && dryPreview.CanApply && dryPreview.CanonicalDryRun &&
                         ReferenceEquals(projectBefore, editing.Project) && history.State.Revision == revisionBefore &&
                         history.Journal.SequenceEqual(journalBefore) && editing.IsDirty == dirtyBefore &&
                         history.CanUndo == undoBefore && history.CanRedo == redoBefore;
            var boundedPlans = dryPreview.ClimatePlanBuilt && dryPreview.WeatherSimulationBuilt &&
                               dryPreview.WaterPlansBuilt && dryPreview.ReverbPlanBuilt &&
                               dryPreview.RuntimeClaimsBounded && dryPreview.Summary.Contains("Aucun réseau",
                                   StringComparison.Ordinal) && dryPreview.Summary.Contains("flottabilité",
                                   StringComparison.Ordinal) && dryPreview.Summary.Contains("convolution",
                                   StringComparison.Ordinal) && dryPreview.Summary.Contains(
                                   "transition déterministe locale", StringComparison.Ordinal) &&
                               dryPreview.Summary.Contains("2 surface", StringComparison.Ordinal) &&
                               dryPreview.Summary.Contains("fallback algorithmique", StringComparison.Ordinal);

            var staleDraft = editing.CreateClimateDraftUx2EnvC(target.Id)!;
            var staleMutation = ApplyUx2EnvCRevisionMutation(editing, target.Id, "stale-draft");
            var staleDraftPreview = editing.PreviewClimateUx2EnvC(staleDraft);
            var staleDraftBlocked = staleMutation && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé",
                                        StringComparison.OrdinalIgnoreCase) && editing.Undo().Succeeded;

            var fingerprintDraft = editing.CreateClimateDraftUx2EnvC(target.Id)!;
            var sourceFingerprintPreview = editing.PreviewClimateUx2EnvC(fingerprintDraft with
            {
                SourceFingerprint = new string('0', 64),
            });
            var mutableStates = editing.CreateClimateDraftUx2EnvC(target.Id)!.WeatherStates.ToArray();
            var mutableDraft = editing.CreateClimateDraftUx2EnvC(target.Id)! with
            {
                WeatherStates = mutableStates,
                WindSpeed = editing.CreateClimateDraftUx2EnvC(target.Id)!.WindSpeed + .25f,
            };
            var mutablePreview = editing.PreviewClimateUx2EnvC(mutableDraft);
            mutableStates[0] = mutableStates[0] with { Humidity = Math.Clamp(mutableStates[0].Humidity + .1f, 0, 1) };
            var staleFingerprintApply = editing.ApplyApprovedClimateUx2EnvC();
            var staleFingerprintBlocked = !sourceFingerprintPreview.Succeeded &&
                                          sourceFingerprintPreview.Message.Contains("environnement",
                                              StringComparison.OrdinalIgnoreCase) &&
                                          mutablePreview.Succeeded && !staleFingerprintApply.Succeeded;

            var staleApprovalDraft = editing.CreateClimateDraftUx2EnvC(target.Id)!;
            var staleApprovalPreview = editing.PreviewClimateUx2EnvC(staleApprovalDraft with
            {
                WindSpeed = staleApprovalDraft.WindSpeed + .5f,
            });
            var staleApprovalMutation = ApplyUx2EnvCRevisionMutation(editing, target.Id, "stale-approval");
            var staleApprovalApply = editing.ApplyApprovedClimateUx2EnvC();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var freshDraft = editing.CreateClimateDraftUx2EnvC(target.Id)! with
            {
                WindSpeed = 5.625f,
                WeatherDeterministicSeed = 5_321,
                ReverbDefaultPreset = "forest-deep",
            };
            var freshPreview = editing.PreviewClimateUx2EnvC(freshDraft);
            var applied = editing.ApplyApprovedClimateUx2EnvC();
            var appliedProject = editing.Project!;
            var appliedTarget = appliedProject.Areas.Single(area => area.Id == target.Id);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName == "patch_area_environment" &&
                                   appliedTarget.Environment.Wind.Speed.Equals(5.625f) &&
                                   appliedTarget.Environment.Weather.DeterministicSeed == 5_321 &&
                                   appliedTarget.Environment.Reverb.DefaultPreset == "forest-deep";
            var lotBPreserved = sourceTarget.Environment.Classification == appliedTarget.Environment.Classification &&
                                sourceTarget.Environment.Tags.SequenceEqual(appliedTarget.Environment.Tags) &&
                                ReferenceEquals(sourceTarget.Environment.Lighting, appliedTarget.Environment.Lighting) &&
                                ReferenceEquals(sourceTarget.Environment.Fog, appliedTarget.Environment.Fog) &&
                                ReferenceEquals(sourceTarget.Environment.Authoring, appliedTarget.Environment.Authoring) &&
                                sourceTarget.Environment.RequireAuthoringRuntimeMetadata ==
                                appliedTarget.Environment.RequireAuthoringRuntimeMetadata;
            var environmentTailsPreserved =
                ReferenceEquals(sourceTarget.Environment.Audio, appliedTarget.Environment.Audio) &&
                ReferenceEquals(sourceTarget.Environment.Rendering, appliedTarget.Environment.Rendering) &&
                ReferenceEquals(sourceTarget.Environment.Sky, appliedTarget.Environment.Sky) &&
                ReferenceEquals(sourceTarget.Environment.Water, appliedTarget.Environment.Water);
            var areaNeighborsPreserved = string.Equals(
                AreaJsonSerializer.Serialize(sourceTarget.WithEnvironment(appliedTarget.Environment)),
                AreaJsonSerializer.Serialize(appliedTarget),
                StringComparison.Ordinal);
            var projectNeighborsPreserved = ReferenceEquals(sourceState.Module, appliedProject.Module) &&
                                            sourceState.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                            sourceState.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                            sourceState.Quests.SequenceEqual(appliedProject.Quests) &&
                                            sourceState.Areas.Where(area => area.Id != target.Id).All(sourceArea =>
                                                ReferenceEquals(sourceArea, appliedProject.Areas.Single(area =>
                                                    area.Id == sourceArea.Id)));
            var appliedJson = ProjectJsonSerializer.Serialize(appliedProject);
            var undo = editing.Undo();
            var undone = ReferenceEquals(sourceState, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, target.Id.ToString());
            var persistence = save.Succeeded && reopen.Succeeded && reopened.Project is not null &&
                              string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopened.Project!),
                                  StringComparison.Ordinal);
            var structuredDiagnostics = invalidWind.Groups.Count == 4 && invalidWeather.Groups.Count == 4 &&
                                        invalidWater.Groups.Count == 4 && invalidReverb.Groups.Count == 4;
            var humanText = !ContainsTechnicalIdentifierUx2EnvC(
                string.Join(' ', editing.ClimateChoicesUx2EnvC.Select(choice => choice.Label)) + " " +
                dryPreview.Message + " " + dryPreview.Summary + " " +
                string.Join(' ', dryPreview.Diff.Wind) + " " + string.Join(' ', dryPreview.Diff.Weather) + " " +
                string.Join(' ', dryPreview.Diff.Water) + " " + string.Join(' ', dryPreview.Diff.Reverb));
            editing.Close(); reopened.Close();
            cleanup = DeleteUx2EnvCTemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && completeCollections && invalidWindBlocked &&
                            invalidWeatherBlocked && invalidWaterBlocked && invalidReverbBlocked && dryRun &&
                            boundedPlans && staleDraftBlocked && staleFingerprintBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && persistence && lotBPreserved &&
                            environmentTailsPreserved && areaNeighborsPreserved && projectNeighborsPreserved &&
                            structuredDiagnostics && humanText;
            return new StudioUx2EnvCSmokeResult(succeeded, explicitSelection, completeCollections,
                invalidWindBlocked, invalidWeatherBlocked, invalidWaterBlocked, invalidReverbBlocked,
                dryRun, boundedPlans, staleDraftBlocked, staleFingerprintBlocked, staleApprovalBlocked,
                canonicalCommand, undoRedo, persistence, lotBPreserved, environmentTailsPreserved,
                areaNeighborsPreserved, projectNeighborsPreserved, structuredDiagnostics, humanText, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_C_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2EnvCSmoke();
        }
        finally
        {
            editing.Close(); reopened.Close();
            cleanup = DeleteUx2EnvCTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_C_CLEANUP_FAILED");
        }
    }

    private static bool ApplyUx2EnvCRevisionMutation(
        StudioSessionController controller,
        AreaId areaId,
        string marker)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null) return false;
        var source = area.Environment;
        var wind = new WindProfile(source.Wind.SchemaVersion, source.Wind.DirectionX, source.Wind.DirectionZ,
            source.Wind.Speed + .03125f, source.Wind.GustStrength, source.Wind.GustIntervalSeconds,
            source.Wind.InfluencesVegetation, source.Wind.InfluencesParticles,
            source.Wind.AudioCue + $"-{marker}", source.Wind.GameplayForce, source.Wind.LocalZones);
        var candidate = new AreaEnvironmentProfile(source.SchemaVersion, source.Classification, source.Tags,
            source.Lighting, source.Fog, source.Audio, source.Rendering, source.Sky, wind, source.Weather,
            source.Water, source.Reverb, source.Authoring, source.RequireAuthoringRuntimeMetadata);
        var result = controller._setAreaEnvironmentUx2EnvC.Handle(history.State,
            new PatchAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvCTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-c-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvCSmokeResult FailedUx2EnvCSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false);
}
