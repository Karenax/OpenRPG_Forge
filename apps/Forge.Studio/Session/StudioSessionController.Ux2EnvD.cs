using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.EngineAdapter.Rendering;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvDPbrCode = "studio.environment.pbr_invalid";
    private const string Ux2EnvDColorCode = "studio.environment.color_invalid";
    private const string Ux2EnvDBudgetCode = "studio.environment.light_budget_invalid";
    private const string Ux2EnvDSkyCode = "studio.environment.sky_invalid";

    private readonly PatchAreaEnvironmentHandler _setAreaEnvironmentUx2EnvD = new(new SystemClock());
    private StudioUx2EnvDDraft? _approvedRenderingUx2EnvD;
    private ProjectRevision? _approvedRenderingRevisionUx2EnvD;
    private string? _approvedRenderingFingerprintUx2EnvD;

    internal IReadOnlyList<StudioUx2EnvDAreaChoice> RenderingChoicesUx2EnvD
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildRenderingChoicesUx2EnvD(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvDAreaChoice> BuildRenderingChoicesUx2EnvD(ProjectDefinition project)
    {
        var duplicateNames = project.Areas.GroupBy(area => area.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var displayName = ContainsTechnicalIdentifierUx2EnvD(area.DisplayName)
                    ? "Zone sans nom lisible"
                    : area.DisplayName.Trim();
                var name = duplicateNames[area.DisplayName] > 1
                    ? $"{displayName} · {HumanTokenUx2EnvD(area.Slug)}"
                    : displayName;
                var rendering = area.Environment.Rendering;
                return new StudioUx2EnvDAreaChoice(
                    area.Id,
                    $"{name} · {HumanToneMappingUx2EnvD(rendering.ToneMapping)} · " +
                    $"{HumanQualityUx2EnvD(rendering.Quality)} · " +
                    $"{rendering.LightBudgets.Count} budget(s) · ciel {HumanSkyModeUx2EnvD(area.Environment.Sky.Mode)}",
                    rendering.LightBudgets.Count,
                    area.Environment.Sky.Mode);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvDDraft? CreateRenderingDraftUx2EnvD(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1 ? CreateRenderingDraftUx2EnvD(history.State.Revision, matches[0]) : null;
    }

    private static StudioUx2EnvDDraft CreateRenderingDraftUx2EnvD(
        ProjectRevision revision,
        AreaDefinition area)
    {
        var rendering = area.Environment.Rendering;
        var sky = area.Environment.Sky;
        var simulationPlatform = rendering.LightBudgets.FirstOrDefault(item => item.Platform == "desktop")?.Platform ??
                                 rendering.LightBudgets[0].Platform;
        return new StudioUx2EnvDDraft(
            area.Id,
            revision,
            RenderingSourceFingerprintUx2EnvD(area),
            area.DisplayName,
            area.Slug,
            rendering.MaterialWorkflow,
            rendering.ToneMapping,
            rendering.Quality,
            rendering.ForwardPlus,
            rendering.PerPixelLighting,
            rendering.MaterialInstances,
            rendering.HdrOutput,
            rendering.Exposure,
            rendering.WhitePoint,
            rendering.LutResource,
            rendering.VisualTuning,
            rendering.LightBudgets.Select(item => new StudioUx2EnvDLightBudgetDraft(
                item.Platform,
                item.MaximumDynamicLights,
                item.MaximumShadowCastingLights,
                item.ShadowAtlasSize,
                item.WarningThreshold)).ToArray(),
            sky.Mode,
            sky.PanoramaResource,
            sky.ZenithColor,
            sky.HorizonColor,
            sky.GroundColor,
            sky.StarIntensity,
            sky.CloudCoverage,
            sky.MoonIntensity,
            sky.EnergyMultiplier,
            sky.TransitionSeconds,
            simulationPlatform,
            0,
            0,
            Math.Min(1, sky.TransitionSeconds));
    }

    internal StudioUx2EnvDPreview PreviewRenderingUx2EnvD(StudioUx2EnvDDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelRenderingUx2EnvD();
        var history = History;
        if (history is null || Project is null)
            return FailedRenderingPreviewUx2EnvD("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedRenderingPreviewUx2EnvD(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la zone avant de prévisualiser.");
        if (!TryBuildRenderingUx2EnvD(draft, true, out var command, out var sourceArea,
                out _, out var candidateEnvironment, out var diagnostics, out var message))
        {
            return new StudioUx2EnvDPreview(false, message, string.Empty, false,
                EmptyRenderingDiffUx2EnvD(), GroupRenderingDiagnosticsUx2EnvD(diagnostics),
                false, false, false, false, false, true, true);
        }

        var dryRun = _setAreaEnvironmentUx2EnvD.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2EnvDDiagnostic(
                item.Code,
                $"areas/{draft.AreaId}/environment/rendering",
                HumanizeRenderingDiagnosticUx2EnvD(item.Code))).ToArray();
            return new StudioUx2EnvDPreview(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande de rendu a refusé cet aperçu.",
                string.Empty, false,
                BuildRenderingDiffUx2EnvD(sourceArea!.Environment, candidateEnvironment!),
                GroupRenderingDiagnosticsUx2EnvD(commandDiagnostics),
                false, false, false, false, false, true, true);
        }

        var candidate = candidateEnvironment!;
        var renderPlan = AreaRenderPlanBuilder.Build(
            candidate,
            NormalizePlatformUx2EnvD(draft.SimulationPlatform),
            ExactIntUx2EnvD(draft.SimulatedDynamicLights),
            ExactIntUx2EnvD(draft.SimulatedShadowCastingLights));
        var visualPlan = EnvironmentVisualQualityPlanBuilder.Build(renderPlan);
        var skyPlan = AreaProductionPlanBuilder.Sky(
            sourceArea!.Environment.Sky,
            candidate.Sky,
            draft.SimulatedTransitionElapsedSeconds);
        var planDiagnostics = renderPlan.Diagnostics.Select(item => new StudioUx2EnvDDiagnostic(
            item.Code,
            item.Path,
            HumanizeRenderingDiagnosticUx2EnvD(item.Code),
            Blocking: false)).ToArray();
        var budgetDiagnosticsBuilt = planDiagnostics.Any(item => IsBudgetPlanDiagnosticUx2EnvD(item.Code));
        var diff = BuildRenderingDiffUx2EnvD(sourceArea.Environment, candidate);
        if (diff.HasChanges)
        {
            _approvedRenderingUx2EnvD = draft;
            _approvedRenderingRevisionUx2EnvD = history.State.Revision;
            _approvedRenderingFingerprintUx2EnvD = RenderingDraftFingerprintUx2EnvD(draft);
        }

        var groups = GroupRenderingDiagnosticsUx2EnvD(planDiagnostics);
        var summary =
            $"Plan déterministe : workflow Metallic / Roughness, instances " +
            $"{(candidate.Rendering.MaterialInstances ? "demandées" : "désactivées")}; " +
            $"{HumanToneMappingUx2EnvD(candidate.Rendering.ToneMapping)}, HDR " +
            $"{(candidate.Rendering.HdrOutput ? "demandé" : "désactivé")}, qualité " +
            $"{HumanQualityUx2EnvD(candidate.Rendering.Quality)}. " +
            $"Forward+ demandé : {(candidate.Rendering.ForwardPlus ? "oui" : "non")}; " +
            $"éclairage par pixel demandé : {(candidate.Rendering.PerPixelLighting ? "oui" : "non")}. " +
            $"Simulation de budget {HumanTokenUx2EnvD(renderPlan.LightBudget.Platform)} : " +
            $"{renderPlan.ObservedDynamicLights}/{renderPlan.LightBudget.MaximumDynamicLights} lumières, " +
            $"{renderPlan.ObservedShadowCastingLights}/{renderPlan.LightBudget.MaximumShadowCastingLights} ombres; " +
            $"{planDiagnostics.Length} avertissement(s). " +
            $"Plan visuel : occlusion {(visualPlan.ScreenSpaceAmbientOcclusion ? "prévue" : "non prévue")}, " +
            $"lueur HDR {(visualPlan.Glow ? "prévue" : "non prévue")}. " +
            $"Transition de ciel {HumanSkyModeUx2EnvD(candidate.Sky.Mode)} à " +
            $"{skyPlan.Progress:P0}, diagnostic {HumanSkyPlanUx2EnvD(skyPlan.DiagnosticCode)}. " +
            $"Ces nombres sont une simulation auteur, pas des mesures runtime. Aucun environnement Godot, GPU, " +
            $"driver, écran HDR ni backend Forward+ n’a été instancié ou inspecté par cet aperçu. " +
            $"L’import des canaux PBR reste gouverné par les profils d’assets.";
        return new StudioUx2EnvDPreview(
            true,
            diff.HasChanges
                ? "Aperçu validé · aucun document ni backend de rendu n’a été modifié."
                : "Aperçu validé · aucune différence sémantique à appliquer.",
            summary,
            diff.HasChanges,
            diff,
            groups,
            true,
            true,
            true,
            true,
            budgetDiagnosticsBuilt,
            true,
            true);
    }

    internal StudioUx2EnvDActionResult ApplyApprovedRenderingUx2EnvD()
    {
        var draft = _approvedRenderingUx2EnvD;
        var history = History;
        if (draft is null || history is null || _approvedRenderingRevisionUx2EnvD != history.State.Revision ||
            !string.Equals(_approvedRenderingFingerprintUx2EnvD, RenderingDraftFingerprintUx2EnvD(draft),
                StringComparison.Ordinal))
        {
            CancelRenderingUx2EnvD();
            return new StudioUx2EnvDActionResult(false,
                "Prévisualisez de nouveau le rendu et le ciel avant de les appliquer.");
        }
        if (!TryBuildRenderingUx2EnvD(draft, false, out var command, out _, out _, out _, out _, out var message))
        {
            CancelRenderingUx2EnvD();
            return new StudioUx2EnvDActionResult(false, message);
        }
        var preview = _setAreaEnvironmentUx2EnvD.Handle(history.State, command! with { DryRun = true });
        if (!preview.Succeeded)
        {
            CancelRenderingUx2EnvD();
            return new StudioUx2EnvDActionResult(false,
                HumanizeRenderingDiagnosticUx2EnvD(preview.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaEnvironmentUx2EnvD.Handle(history.State, command!);
        CancelRenderingUx2EnvD();
        if (!applied.Succeeded)
            return new StudioUx2EnvDActionResult(false,
                HumanizeRenderingDiagnosticUx2EnvD(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new StudioUx2EnvDActionResult(true,
            "Rendu et ciel appliqués · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelRenderingUx2EnvD()
    {
        _approvedRenderingUx2EnvD = null;
        _approvedRenderingRevisionUx2EnvD = null;
        _approvedRenderingFingerprintUx2EnvD = null;
    }

    internal void CloseUx2EnvD() => CancelRenderingUx2EnvD();

    internal StudioUx2EnvDApprovalSnapshot CaptureRenderingApprovalUx2EnvD() => new(
        _approvedRenderingUx2EnvD,
        _approvedRenderingRevisionUx2EnvD,
        _approvedRenderingFingerprintUx2EnvD);

    internal void RestoreRenderingApprovalUx2EnvD(StudioUx2EnvDApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedRenderingUx2EnvD = snapshot.Draft;
        _approvedRenderingRevisionUx2EnvD = snapshot.Revision;
        _approvedRenderingFingerprintUx2EnvD = snapshot.Fingerprint;
    }

    private bool TryBuildRenderingUx2EnvD(
        StudioUx2EnvDDraft draft,
        bool dryRun,
        out PatchAreaEnvironmentCommand? command,
        out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea,
        out AreaEnvironmentProfile? candidateEnvironment,
        out IReadOnlyList<StudioUx2EnvDDiagnostic> diagnostics,
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
            message = "Le projet a changé. Rechargez la zone avant de prévisualiser."; return false;
        }
        var matches = project.Areas.Where(area => area.Id == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "La zone choisie n’existe plus ou n’est plus unique."; return false;
        }
        sourceArea = matches[0];
        if (!string.Equals(draft.SourceFingerprint, RenderingSourceFingerprintUx2EnvD(sourceArea),
                StringComparison.Ordinal))
        {
            message = "L’environnement ou un voisin de la zone a changé. Rechargez la fiche avant de prévisualiser.";
            return false;
        }
        var inputDiagnostics = ValidateRenderingDraftUx2EnvD(draft).ToArray();
        if (inputDiagnostics.Length > 0)
        {
            diagnostics = inputDiagnostics; message = inputDiagnostics[0].Message; return false;
        }

        try
        {
            var source = sourceArea.Environment;
            var sourceBudgets = source.Rendering.LightBudgets.ToDictionary(item => item.Platform,
                StringComparer.Ordinal);
            var budgets = draft.LightBudgets.Select(item =>
            {
                var platform = NormalizePlatformUx2EnvD(item.Platform);
                if (sourceBudgets.TryGetValue(platform, out var existing) && SameBudgetUx2EnvD(item, existing))
                    return existing;
                return new DynamicLightBudgetProfile(
                    platform,
                    ExactIntUx2EnvD(item.MaximumDynamicLights),
                    ExactIntUx2EnvD(item.MaximumShadowCastingLights),
                    ExactIntUx2EnvD(item.ShadowAtlasSize),
                    item.WarningThreshold);
            }).ToArray();
            var rebuiltRendering = new AreaRenderingProfile(
                source.Rendering.SchemaVersion,
                draft.MaterialWorkflow,
                draft.ToneMapping,
                draft.Quality,
                draft.ForwardPlus,
                draft.PerPixelLighting,
                draft.MaterialInstances,
                draft.HdrOutput,
                draft.Exposure,
                draft.WhitePoint,
                draft.LutResource,
                budgets,
                draft.VisualTuning);
            var rendering = SameRenderingUx2EnvD(source.Rendering, rebuiltRendering)
                ? source.Rendering
                : rebuiltRendering;
            var rebuiltSky = new SkyProfile(
                source.Sky.SchemaVersion,
                draft.SkyMode,
                draft.PanoramaResource,
                draft.ZenithColor,
                draft.HorizonColor,
                draft.GroundColor,
                draft.StarIntensity,
                draft.CloudCoverage,
                draft.MoonIntensity,
                draft.EnergyMultiplier,
                draft.TransitionSeconds);
            var sky = source.Sky == rebuiltSky ? source.Sky : rebuiltSky;
            var environment = new AreaEnvironmentProfile(
                source.SchemaVersion,
                source.Classification,
                source.Tags,
                source.Lighting,
                source.Fog,
                source.Audio,
                rendering,
                sky,
                source.Wind,
                source.Weather,
                source.Water,
                source.Reverb,
                source.Authoring,
                source.RequireAuthoringRuntimeMetadata);
            var diff = BuildRenderingDiffUx2EnvD(source, environment);
            if (!diff.HasChanges) environment = source;
            var updatedArea = ReferenceEquals(environment, source) ? sourceArea : sourceArea.WithEnvironment(environment);
            candidateEnvironment = environment; candidateArea = updatedArea;
            var sourceAreaId = sourceArea.Id;
            var candidateProject = new ProjectDefinition(
                project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas.Select(area => area.Id == sourceAreaId ? updatedArea : area).ToArray(),
                project.Dialogues, project.Quests);
            var canonicalDiagnostics = ProjectValidator.Validate(candidateProject).Select(item =>
                new StudioUx2EnvDDiagnostic(item.Code, item.Path,
                    HumanizeRenderingDiagnosticUx2EnvD(item.Code))).ToArray();
            if (canonicalDiagnostics.Length > 0)
            {
                diagnostics = canonicalDiagnostics; message = canonicalDiagnostics[0].Message; return false;
            }
            command = new PatchAreaEnvironmentCommand(
                CommandId.From(Guid.NewGuid()), history.State.Revision, sourceArea.Id, environment, dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException or OverflowException)
        {
            var diagnostic = new StudioUx2EnvDDiagnostic(
                RenderingConstructionCodeUx2EnvD(draft),
                $"areas/{draft.AreaId}/environment/rendering",
                exception is OverflowException
                    ? "Un budget doit utiliser un entier exact dans la plage canonique."
                    : "Le profil de rendu ou de ciel ne peut pas être reconstruit avec ces valeurs.");
            diagnostics = [diagnostic]; message = diagnostic.Message; return false;
        }
    }

    private static IReadOnlyList<StudioUx2EnvDDiagnostic> ValidateRenderingDraftUx2EnvD(StudioUx2EnvDDraft draft)
    {
        var result = new List<StudioUx2EnvDDiagnostic>();
        if (!Enum.IsDefined(draft.MaterialWorkflow) || !Enum.IsDefined(draft.Quality) ||
            !Enum.IsDefined(draft.ToneMapping))
            result.Add(new(Ux2EnvDPbrCode, $"areas/{draft.AreaId}/environment/rendering",
                "PBR : choisissez un workflow, une qualité et un mode de couleur reconnus."));
        if (!double.IsFinite(draft.Exposure) || !double.IsFinite(draft.WhitePoint) ||
            draft.Exposure is < .05 or > 8 || draft.WhitePoint is < 1 or > 32 ||
            !SafeOptionalResourceUx2EnvD(draft.LutResource) || draft.VisualTuning is null)
            result.Add(new(Ux2EnvDColorCode, $"areas/{draft.AreaId}/environment/rendering/color",
                "Couleur : exposition, point blanc et LUT relative doivent respecter le profil canonique."));

        var normalizedPlatforms = draft.LightBudgets.Select(item => NormalizePlatformUx2EnvD(item.Platform)).ToArray();
        if (draft.LightBudgets.Count == 0 || normalizedPlatforms.Any(string.IsNullOrWhiteSpace) ||
            normalizedPlatforms.Distinct(StringComparer.Ordinal).Count() != normalizedPlatforms.Length ||
            draft.LightBudgets.Any(item => !ValidBudgetNumberUx2EnvD(item.MaximumDynamicLights, 0, 4096) ||
                !ValidBudgetNumberUx2EnvD(item.MaximumShadowCastingLights, 0, 4096) ||
                item.MaximumShadowCastingLights > item.MaximumDynamicLights ||
                !ValidBudgetNumberUx2EnvD(item.ShadowAtlasSize, 512, 16384) ||
                !IsPowerOfTwoUx2EnvD(item.ShadowAtlasSize) || !double.IsFinite(item.WarningThreshold) ||
                item.WarningThreshold is < .5 or > 1))
            result.Add(new(Ux2EnvDBudgetCode, $"areas/{draft.AreaId}/environment/rendering/lightBudgets",
                "Budgets : plateformes uniques, comptes entiers, ombres bornées, atlas puissance de deux et seuil de 50 à 100 %."));

        if (!Enum.IsDefined(draft.SkyMode) || !IsHexColorUx2EnvD(draft.ZenithColor) ||
            !IsHexColorUx2EnvD(draft.HorizonColor) || !IsHexColorUx2EnvD(draft.GroundColor) ||
            !FiniteUx2EnvD(draft.StarIntensity, draft.CloudCoverage, draft.MoonIntensity,
                draft.EnergyMultiplier, draft.TransitionSeconds) || draft.StarIntensity is < 0 or > 4 ||
            draft.CloudCoverage is < 0 or > 1 || draft.MoonIntensity is < 0 or > 4 ||
            draft.EnergyMultiplier is < .05 or > 8 || draft.TransitionSeconds is < 0 or > 3600 ||
            !SafeOptionalResourceUx2EnvD(draft.PanoramaResource) ||
            draft.SkyMode == SkyProfileMode.Panorama && string.IsNullOrWhiteSpace(draft.PanoramaResource))
            result.Add(new(Ux2EnvDSkyCode, $"areas/{draft.AreaId}/environment/sky",
                "Ciel : mode, couleurs, intensités, transition et panorama relatif doivent être valides."));

        var simulationPlatform = NormalizePlatformUx2EnvD(draft.SimulationPlatform);
        if (!normalizedPlatforms.Contains(simulationPlatform, StringComparer.Ordinal) ||
            !ValidBudgetNumberUx2EnvD(draft.SimulatedDynamicLights, 0, 1_000_000) ||
            !ValidBudgetNumberUx2EnvD(draft.SimulatedShadowCastingLights, 0, 1_000_000) ||
            draft.SimulatedShadowCastingLights > draft.SimulatedDynamicLights ||
            !double.IsFinite(draft.SimulatedTransitionElapsedSeconds) ||
            draft.SimulatedTransitionElapsedSeconds is < 0 or > 3600)
            result.Add(new(Ux2EnvDBudgetCode, $"areas/{draft.AreaId}/environment/rendering/simulation",
                "Simulation : choisissez une plateforme du profil et des comptes entiers cohérents ; ce ne sont pas des mesures runtime."));
        return result;
    }

    private static StudioUx2EnvDSemanticDiff BuildRenderingDiffUx2EnvD(
        AreaEnvironmentProfile source,
        AreaEnvironmentProfile candidate)
    {
        var pbr = new List<string>();
        if (source.Rendering.MaterialWorkflow != candidate.Rendering.MaterialWorkflow ||
            source.Rendering.MaterialInstances != candidate.Rendering.MaterialInstances)
            pbr.Add("Workflow ou instances de matériaux modifiés.");
        if (source.Rendering.Quality != candidate.Rendering.Quality ||
            source.Rendering.ForwardPlus != candidate.Rendering.ForwardPlus ||
            source.Rendering.PerPixelLighting != candidate.Rendering.PerPixelLighting)
            pbr.Add("Qualité, Forward+ demandé ou éclairage par pixel modifié.");
        var color = new List<string>();
        if (source.Rendering.ToneMapping != candidate.Rendering.ToneMapping ||
            source.Rendering.HdrOutput != candidate.Rendering.HdrOutput ||
            !source.Rendering.Exposure.Equals(candidate.Rendering.Exposure) ||
            !source.Rendering.WhitePoint.Equals(candidate.Rendering.WhitePoint) ||
            !string.Equals(source.Rendering.LutResource, candidate.Rendering.LutResource, StringComparison.Ordinal) ||
            source.Rendering.VisualTuning != candidate.Rendering.VisualTuning)
            color.Add("Tone mapper, HDR, exposition, point blanc ou LUT modifié.");
        var budgets = new List<string>();
        if (!source.Rendering.LightBudgets.SequenceEqual(candidate.Rendering.LightBudgets))
        {
            var sourceBudgets = source.Rendering.LightBudgets.ToDictionary(item => item.Platform,
                StringComparer.Ordinal);
            var candidateBudgets = candidate.Rendering.LightBudgets.ToDictionary(item => item.Platform,
                StringComparer.Ordinal);
            foreach (var platform in sourceBudgets.Keys.Union(candidateBudgets.Keys, StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                if (!sourceBudgets.TryGetValue(platform, out var before))
                {
                    budgets.Add($"Budget {HumanTokenUx2EnvD(platform)} ajouté.");
                    continue;
                }
                if (!candidateBudgets.TryGetValue(platform, out var after))
                {
                    budgets.Add($"Budget {HumanTokenUx2EnvD(platform)} retiré.");
                    continue;
                }
                if (before != after)
                {
                    budgets.Add(
                        $"Budget {HumanTokenUx2EnvD(platform)} modifié : " +
                        $"{before.MaximumDynamicLights}/{before.MaximumShadowCastingLights} → " +
                        $"{after.MaximumDynamicLights}/{after.MaximumShadowCastingLights} lumières/ombres.");
                }
            }
        }
        var sky = new List<string>();
        if (source.Sky != candidate.Sky)
        {
            if (source.Sky.Mode != candidate.Sky.Mode ||
                !string.Equals(source.Sky.PanoramaResource, candidate.Sky.PanoramaResource, StringComparison.Ordinal))
                sky.Add($"Mode de ciel : {HumanSkyModeUx2EnvD(source.Sky.Mode)} → " +
                        $"{HumanSkyModeUx2EnvD(candidate.Sky.Mode)}.");
            if (!string.Equals(source.Sky.ZenithColor, candidate.Sky.ZenithColor, StringComparison.Ordinal) ||
                !string.Equals(source.Sky.HorizonColor, candidate.Sky.HorizonColor, StringComparison.Ordinal) ||
                !string.Equals(source.Sky.GroundColor, candidate.Sky.GroundColor, StringComparison.Ordinal))
                sky.Add("Couleurs du zénith, de l’horizon ou du sol modifiées.");
            if (!source.Sky.StarIntensity.Equals(candidate.Sky.StarIntensity) ||
                !source.Sky.CloudCoverage.Equals(candidate.Sky.CloudCoverage) ||
                !source.Sky.MoonIntensity.Equals(candidate.Sky.MoonIntensity) ||
                !source.Sky.EnergyMultiplier.Equals(candidate.Sky.EnergyMultiplier) ||
                !source.Sky.TransitionSeconds.Equals(candidate.Sky.TransitionSeconds))
                sky.Add("Étoiles, nuages, lune, énergie ou transition du ciel modifiés.");
        }
        return new StudioUx2EnvDSemanticDiff(pbr, color, budgets, sky);
    }

    private static StudioUx2EnvDSemanticDiff EmptyRenderingDiffUx2EnvD() => new([], [], [], []);

    private static StudioUx2EnvDPreview FailedRenderingPreviewUx2EnvD(string message) => new(
        false, message, string.Empty, false, EmptyRenderingDiffUx2EnvD(), GroupRenderingDiagnosticsUx2EnvD([]),
        false, false, false, false, false, true, true);

    private static IReadOnlyList<StudioUx2EnvDDiagnosticGroup> GroupRenderingDiagnosticsUx2EnvD(
        IReadOnlyList<StudioUx2EnvDDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            ("pbr", "PBR, qualité et éclairage", Ux2EnvDPbrCode),
            ("color", "HDR et gestion de couleur", Ux2EnvDColorCode),
            ("budgets", "Budgets plateforme simulés", Ux2EnvDBudgetCode),
            ("sky", "Ciel et transition", Ux2EnvDSkyCode),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item => RenderingDiagnosticGroupUx2EnvD(item) == definition.Item1).ToArray();
            return new StudioUx2EnvDDiagnosticGroup(definition.Item1, definition.Item2,
                items.Any(item => item.Blocking) ? "à corriger" : items.Length > 0 ? "avertissement" : "conforme",
                items);
        }).ToArray();
    }

    private static string RenderingDiagnosticGroupUx2EnvD(StudioUx2EnvDDiagnostic diagnostic)
    {
        if (diagnostic.Code == Ux2EnvDColorCode || diagnostic.Path.Contains("lut", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Path.Contains("color", StringComparison.OrdinalIgnoreCase)) return "color";
        if (diagnostic.Code == Ux2EnvDBudgetCode || diagnostic.Code.Contains("lights.", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("lightBudgets", StringComparison.OrdinalIgnoreCase)) return "budgets";
        if (diagnostic.Code == Ux2EnvDSkyCode || diagnostic.Path.Contains("/sky", StringComparison.OrdinalIgnoreCase))
            return "sky";
        return "pbr";
    }

    private static bool IsBudgetPlanDiagnosticUx2EnvD(string code) => code is
        AreaRenderPlanBuilder.DynamicLightWarningCode or AreaRenderPlanBuilder.DynamicLightBudgetCode or
        AreaRenderPlanBuilder.ShadowLightWarningCode or AreaRenderPlanBuilder.ShadowLightBudgetCode;

    private static string HumanizeRenderingDiagnosticUx2EnvD(string? code) => code switch
    {
        Ux2EnvDPbrCode => "Le workflow PBR ou la qualité de rendu n’est pas valide.",
        Ux2EnvDColorCode => "Le profil HDR, l’exposition ou la LUT n’est pas valide.",
        Ux2EnvDBudgetCode => "Un budget de lumières ou la simulation auteur n’est pas valide.",
        Ux2EnvDSkyCode => "Le ciel ou sa ressource panorama n’est pas valide.",
        AreaRenderPlanBuilder.ForwardPlusRequiredCode =>
            "Forward+ est désactivé dans le profil demandé ; le rendu de référence le recommande.",
        AreaRenderPlanBuilder.PerPixelRequiredCode =>
            "L’éclairage par pixel est désactivé dans le profil demandé.",
        AreaRenderPlanBuilder.DynamicLightWarningCode =>
            "La simulation approche le budget de lumières dynamiques de cette plateforme.",
        AreaRenderPlanBuilder.DynamicLightBudgetCode =>
            "La simulation dépasse le budget de lumières dynamiques de cette plateforme.",
        AreaRenderPlanBuilder.ShadowLightWarningCode =>
            "La simulation approche le budget de lumières avec ombres de cette plateforme.",
        AreaRenderPlanBuilder.ShadowLightBudgetCode =>
            "La simulation dépasse le budget de lumières avec ombres de cette plateforme.",
        "command.revision_conflict" => "Le projet a changé. Rechargez la zone et relancez l’aperçu.",
        _ => "La validation canonique du projet a refusé le rendu ; consultez le groupe concerné.",
    };

    private static string RenderingConstructionCodeUx2EnvD(StudioUx2EnvDDraft draft)
    {
        if (draft.LightBudgets.Count == 0) return Ux2EnvDBudgetCode;
        if (draft.SkyMode == SkyProfileMode.Panorama && string.IsNullOrWhiteSpace(draft.PanoramaResource))
            return Ux2EnvDSkyCode;
        return Ux2EnvDPbrCode;
    }

    private static string RenderingSourceFingerprintUx2EnvD(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string RenderingDraftFingerprintUx2EnvD(StudioUx2EnvDDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString()); writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint ?? string.Empty); writer.Write((int)draft.MaterialWorkflow);
            writer.Write((int)draft.ToneMapping); writer.Write((int)draft.Quality);
            writer.Write(draft.ForwardPlus); writer.Write(draft.PerPixelLighting);
            writer.Write(draft.MaterialInstances); writer.Write(draft.HdrOutput);
            writer.Write(draft.Exposure); writer.Write(draft.WhitePoint); writer.Write(draft.LutResource ?? string.Empty);
            writer.Write(draft.VisualTuning.SchemaVersion);
            writer.Write(draft.VisualTuning.Brightness);
            writer.Write(draft.VisualTuning.ContrastMultiplier);
            writer.Write(draft.VisualTuning.SaturationMultiplier);
            writer.Write(draft.VisualTuning.AmbientOcclusionIntensityMultiplier);
            writer.Write(draft.VisualTuning.AmbientOcclusionPower);
            writer.Write(draft.VisualTuning.DirectionalLightPitchDegrees);
            writer.Write(draft.VisualTuning.DirectionalLightYawDegrees);
            writer.Write(draft.VisualTuning.DirectionalLightAngularDistance);
            writer.Write(draft.VisualTuning.ShadowBlur);
            writer.Write(draft.LightBudgets.Count);
            foreach (var item in draft.LightBudgets)
            {
                writer.Write(item.Platform ?? string.Empty); writer.Write(item.MaximumDynamicLights);
                writer.Write(item.MaximumShadowCastingLights); writer.Write(item.ShadowAtlasSize);
                writer.Write(item.WarningThreshold);
            }
            writer.Write((int)draft.SkyMode); writer.Write(draft.PanoramaResource ?? string.Empty);
            writer.Write(draft.ZenithColor ?? string.Empty); writer.Write(draft.HorizonColor ?? string.Empty);
            writer.Write(draft.GroundColor ?? string.Empty); writer.Write(draft.StarIntensity);
            writer.Write(draft.CloudCoverage); writer.Write(draft.MoonIntensity);
            writer.Write(draft.EnergyMultiplier); writer.Write(draft.TransitionSeconds);
            writer.Write(draft.SimulationPlatform ?? string.Empty); writer.Write(draft.SimulatedDynamicLights);
            writer.Write(draft.SimulatedShadowCastingLights); writer.Write(draft.SimulatedTransitionElapsedSeconds);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool SameBudgetUx2EnvD(
        StudioUx2EnvDLightBudgetDraft draft,
        DynamicLightBudgetProfile source) =>
        string.Equals(NormalizePlatformUx2EnvD(draft.Platform), source.Platform, StringComparison.Ordinal) &&
        draft.MaximumDynamicLights == source.MaximumDynamicLights &&
        draft.MaximumShadowCastingLights == source.MaximumShadowCastingLights &&
        draft.ShadowAtlasSize == source.ShadowAtlasSize &&
        draft.WarningThreshold.Equals(source.WarningThreshold);

    private static bool SameRenderingUx2EnvD(AreaRenderingProfile source, AreaRenderingProfile candidate) =>
        source.MaterialWorkflow == candidate.MaterialWorkflow && source.ToneMapping == candidate.ToneMapping &&
        source.Quality == candidate.Quality && source.ForwardPlus == candidate.ForwardPlus &&
        source.PerPixelLighting == candidate.PerPixelLighting &&
        source.MaterialInstances == candidate.MaterialInstances && source.HdrOutput == candidate.HdrOutput &&
        source.Exposure.Equals(candidate.Exposure) && source.WhitePoint.Equals(candidate.WhitePoint) &&
        string.Equals(source.LutResource, candidate.LutResource, StringComparison.Ordinal) &&
        source.VisualTuning == candidate.VisualTuning &&
        source.LightBudgets.SequenceEqual(candidate.LightBudgets);

    private static bool ValidBudgetNumberUx2EnvD(double value, int minimum, int maximum) =>
        double.IsFinite(value) && value == Math.Truncate(value) && value >= minimum && value <= maximum;

    private static int ExactIntUx2EnvD(double value)
    {
        if (!double.IsFinite(value) || value != Math.Truncate(value) || value < int.MinValue || value > int.MaxValue)
            throw new OverflowException("The value is not an exact 32-bit integer.");
        return checked((int)value);
    }

    private static bool IsPowerOfTwoUx2EnvD(double value)
    {
        if (!ValidBudgetNumberUx2EnvD(value, 1, 16384)) return false;
        var integer = (int)value;
        return (integer & (integer - 1)) == 0;
    }

    private static bool SafeOptionalResourceUx2EnvD(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
        return string.IsNullOrEmpty(normalized) || !Path.IsPathRooted(normalized) &&
               !normalized.Split('/').Any(segment => segment is "" or "." or "..");
    }

    private static bool IsHexColorUx2EnvD(string? value) => value is { Length: 7 } && value[0] == '#' &&
                                                             value.Skip(1).All(Uri.IsHexDigit);

    private static bool FiniteUx2EnvD(params double[] values) => values.All(double.IsFinite);

    private static string NormalizePlatformUx2EnvD(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string HumanToneMappingUx2EnvD(ToneMappingProfile value) => value switch
    {
        ToneMappingProfile.Agx => "AgX",
        ToneMappingProfile.Aces => "ACES",
        ToneMappingProfile.Filmic => "Filmic",
        _ => "Linéaire",
    };

    private static string HumanQualityUx2EnvD(RenderQualityTier value) => value switch
    {
        RenderQualityTier.Low => "qualité basse",
        RenderQualityTier.Medium => "qualité moyenne",
        RenderQualityTier.High => "qualité élevée",
        RenderQualityTier.Ultra => "qualité ultra",
        _ => "qualité inconnue",
    };

    private static string HumanSkyModeUx2EnvD(SkyProfileMode value) => value switch
    {
        SkyProfileMode.Physical => "physique",
        SkyProfileMode.Procedural => "procédural",
        SkyProfileMode.Panorama => "panorama",
        _ => "inconnu",
    };

    private static string HumanSkyPlanUx2EnvD(string value) => value switch
    {
        "area.sky.panorama_missing" => "panorama requis",
        _ => "plan temporel prêt, ressource non chargée",
    };

    private static string HumanTokenUx2EnvD(string? value)
    {
        var normalized = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(normalized) || ContainsTechnicalIdentifierUx2EnvD(normalized)
            ? "profil sans nom lisible"
            : normalized;
    }

    private static bool ContainsTechnicalIdentifierUx2EnvD(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) || text.Contains(".json", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    internal static ProjectDefinition CreateUx2EnvDContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var area = source.Areas.FirstOrDefault() ??
                   throw new InvalidOperationException("The contract project requires an area.");
        var environment = area.Environment;
        var rendering = new AreaRenderingProfile(
            AreaRenderingProfile.CurrentSchemaVersion,
            PbrMaterialWorkflow.MetallicRoughness,
            ToneMappingProfile.Aces,
            RenderQualityTier.Ultra,
            true,
            true,
            true,
            true,
            1.125,
            5.5,
            "grading/neutral-lut.tres",
            [
                new("desktop", 48, 12, 8192, .75),
                new("mobile", 12, 3, 2048, .7),
            ]);
        var sky = new SkyProfile(
            SkyProfile.CurrentSchemaVersion,
            SkyProfileMode.Panorama,
            "skies/ux2-env-d-dawn.exr",
            "#284a78",
            "#d8a870",
            "#202820",
            .35,
            .45,
            .2,
            1.25,
            12);
        var candidate = new AreaEnvironmentProfile(
            environment.SchemaVersion, environment.Classification, environment.Tags,
            environment.Lighting, environment.Fog, environment.Audio, rendering, sky,
            environment.Wind, environment.Weather, environment.Water, environment.Reverb,
            environment.Authoring, environment.RequireAuthoringRuntimeMetadata);
        var updated = area.WithEnvironment(candidate);
        return new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? updated : item).ToArray(),
            source.Dialogues, source.Quests);
    }

    internal StudioUx2EnvDSmokeResult RunUx2EnvDContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0) return FailedUx2EnvDSmoke();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-d-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, CreateUx2EnvDContractProject(sourceProject));
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded) return FailedUx2EnvDSmoke();
            var target = editing.Project!.Areas[0];
            var explicitSelection = editing.CreateRenderingDraftUx2EnvD() is null &&
                                    editing.CreateRenderingDraftUx2EnvD(target.Id) is not null;
            var draft = editing.CreateRenderingDraftUx2EnvD(target.Id)!;
            var completeBudgets = draft.LightBudgets.Count >= 2 &&
                                  draft.LightBudgets.Any(item => item.Platform == "desktop") &&
                                  draft.LightBudgets.Any(item => item.Platform == "mobile");
            var invalidRendering = editing.PreviewRenderingUx2EnvD(draft with { Exposure = 0 });
            var invalidBudget = editing.PreviewRenderingUx2EnvD(draft with
            {
                LightBudgets = draft.LightBudgets.Select((item, index) => index == 0
                    ? item with { ShadowAtlasSize = 3000 }
                    : item).ToArray(),
            });
            var invalidSky = editing.PreviewRenderingUx2EnvD(draft with
            {
                SkyMode = SkyProfileMode.Panorama,
                PanoramaResource = string.Empty,
            });
            var invalidRenderingBlocked = !invalidRendering.Succeeded;
            var invalidBudgetBlocked = !invalidBudget.Succeeded;
            var invalidSkyBlocked = !invalidSky.Succeeded;

            var configuredBudgets = draft.LightBudgets.Select(item => item.Platform == "desktop"
                ? item with { MaximumDynamicLights = 56 }
                : item).ToArray();
            var configured = draft with
            {
                ToneMapping = ToneMappingProfile.Filmic,
                Exposure = 1.375,
                LightBudgets = configuredBudgets,
                SkyMode = SkyProfileMode.Procedural,
                PanoramaResource = string.Empty,
                CloudCoverage = .62,
                EnergyMultiplier = 1.4,
                SimulationPlatform = "desktop",
                SimulatedDynamicLights = 44,
                SimulatedShadowCastingLights = 10,
                SimulatedTransitionElapsedSeconds = 6,
            };
            var history = editing.History!;
            var projectBefore = editing.Project;
            var revisionBefore = history.State.Revision;
            var journalBefore = history.Journal.ToArray();
            var dirtyBefore = editing.IsDirty;
            var undoBefore = history.CanUndo;
            var redoBefore = history.CanRedo;
            var dryPreview = editing.PreviewRenderingUx2EnvD(configured);
            var dryRun = dryPreview.Succeeded && dryPreview.CanApply && dryPreview.CanonicalDryRun &&
                         ReferenceEquals(projectBefore, editing.Project) && history.State.Revision == revisionBefore &&
                         history.Journal.SequenceEqual(journalBefore) && editing.IsDirty == dirtyBefore &&
                         history.CanUndo == undoBefore && history.CanRedo == redoBefore;
            var renderPlans = dryPreview.RenderPlanBuilt && dryPreview.VisualQualityPlanBuilt &&
                              dryPreview.SkyTransitionPlanBuilt && dryPreview.AdapterInstantiationSkipped &&
                              dryPreview.RuntimeClaimsBounded && dryPreview.Summary.Contains("Aucun environnement Godot",
                                  StringComparison.Ordinal) && dryPreview.Summary.Contains("pas des mesures runtime",
                                  StringComparison.Ordinal);
            var planWarnings = dryPreview.BudgetDiagnosticsBuilt && dryPreview.Groups.SelectMany(group => group.Diagnostics)
                .Any(item => !item.Blocking && item.Code == AreaRenderPlanBuilder.DynamicLightWarningCode) &&
                               dryPreview.Groups.SelectMany(group => group.Diagnostics)
                                   .Any(item => !item.Blocking && item.Code == AreaRenderPlanBuilder.ShadowLightWarningCode);

            var staleDraft = editing.CreateRenderingDraftUx2EnvD(target.Id)!;
            var staleMutation = ApplyUx2EnvDRevisionMutation(editing, target.Id, "stale-draft");
            var staleDraftPreview = editing.PreviewRenderingUx2EnvD(staleDraft);
            var staleDraftBlocked = staleMutation && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé",
                                        StringComparison.OrdinalIgnoreCase) && editing.Undo().Succeeded;

            var fingerprintDraft = editing.CreateRenderingDraftUx2EnvD(target.Id)!;
            var sourceFingerprintPreview = editing.PreviewRenderingUx2EnvD(fingerprintDraft with
            {
                SourceFingerprint = new string('0', 64),
            });
            var mutableBudgets = editing.CreateRenderingDraftUx2EnvD(target.Id)!.LightBudgets.ToArray();
            var mutableDraft = editing.CreateRenderingDraftUx2EnvD(target.Id)! with
            {
                Exposure = 1.25,
                LightBudgets = mutableBudgets,
            };
            var mutablePreview = editing.PreviewRenderingUx2EnvD(mutableDraft);
            mutableBudgets[0] = mutableBudgets[0] with { WarningThreshold = .91 };
            var staleFingerprintApply = editing.ApplyApprovedRenderingUx2EnvD();
            var staleFingerprintBlocked = !sourceFingerprintPreview.Succeeded &&
                                          sourceFingerprintPreview.Message.Contains("environnement",
                                              StringComparison.OrdinalIgnoreCase) &&
                                          mutablePreview.Succeeded && !staleFingerprintApply.Succeeded;

            var staleApprovalDraft = editing.CreateRenderingDraftUx2EnvD(target.Id)!;
            var staleApprovalPreview = editing.PreviewRenderingUx2EnvD(staleApprovalDraft with
            {
                Exposure = staleApprovalDraft.Exposure + .125,
            });
            var staleApprovalMutation = ApplyUx2EnvDRevisionMutation(editing, target.Id, "stale-approval");
            var staleApprovalApply = editing.ApplyApprovedRenderingUx2EnvD();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var freshBudgets = editing.CreateRenderingDraftUx2EnvD(target.Id)!.LightBudgets.Select(item =>
                item.Platform == "desktop" ? item with { MaximumDynamicLights = 60 } : item).ToArray();
            var freshDraft = editing.CreateRenderingDraftUx2EnvD(target.Id)! with
            {
                ToneMapping = ToneMappingProfile.Filmic,
                Exposure = 1.5,
                LightBudgets = freshBudgets,
                SkyMode = SkyProfileMode.Procedural,
                PanoramaResource = string.Empty,
                CloudCoverage = .66,
                SimulationPlatform = "desktop",
                SimulatedDynamicLights = 46,
                SimulatedShadowCastingLights = 10,
                SimulatedTransitionElapsedSeconds = 5,
            };
            var freshPreview = editing.PreviewRenderingUx2EnvD(freshDraft);
            var applied = editing.ApplyApprovedRenderingUx2EnvD();
            var appliedProject = editing.Project!;
            var appliedTarget = appliedProject.Areas.Single(area => area.Id == target.Id);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName == "patch_area_environment" &&
                                   appliedTarget.Environment.Rendering.ToneMapping == ToneMappingProfile.Filmic &&
                                   appliedTarget.Environment.Rendering.Exposure.Equals(1.5) &&
                                   appliedTarget.Environment.Rendering.LightBudgets.Single(item =>
                                       item.Platform == "desktop").MaximumDynamicLights == 60 &&
                                   appliedTarget.Environment.Sky.Mode == SkyProfileMode.Procedural;
            var lotBPreserved = sourceTarget.Environment.Classification == appliedTarget.Environment.Classification &&
                                sourceTarget.Environment.Tags.SequenceEqual(appliedTarget.Environment.Tags) &&
                                ReferenceEquals(sourceTarget.Environment.Lighting, appliedTarget.Environment.Lighting) &&
                                ReferenceEquals(sourceTarget.Environment.Fog, appliedTarget.Environment.Fog) &&
                                ReferenceEquals(sourceTarget.Environment.Authoring, appliedTarget.Environment.Authoring) &&
                                sourceTarget.Environment.RequireAuthoringRuntimeMetadata ==
                                appliedTarget.Environment.RequireAuthoringRuntimeMetadata;
            var lotCPreserved = ReferenceEquals(sourceTarget.Environment.Wind, appliedTarget.Environment.Wind) &&
                                ReferenceEquals(sourceTarget.Environment.Weather, appliedTarget.Environment.Weather) &&
                                ReferenceEquals(sourceTarget.Environment.Water, appliedTarget.Environment.Water) &&
                                ReferenceEquals(sourceTarget.Environment.Reverb, appliedTarget.Environment.Reverb);
            var environmentTailsPreserved =
                ReferenceEquals(sourceTarget.Environment.Audio, appliedTarget.Environment.Audio) &&
                sourceTarget.Environment.SchemaVersion == appliedTarget.Environment.SchemaVersion;
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
            var structuredDiagnostics = invalidRendering.Groups.Count == 4 && invalidBudget.Groups.Count == 4 &&
                                        invalidSky.Groups.Count == 4 && dryPreview.Groups.Count == 4;
            var humanText = !ContainsTechnicalIdentifierUx2EnvD(
                string.Join(' ', editing.RenderingChoicesUx2EnvD.Select(choice => choice.Label)) + " " +
                dryPreview.Message + " " + dryPreview.Summary + " " +
                string.Join(' ', dryPreview.Diff.Pbr) + " " + string.Join(' ', dryPreview.Diff.Color) + " " +
                string.Join(' ', dryPreview.Diff.Budgets) + " " + string.Join(' ', dryPreview.Diff.Sky));
            editing.Close(); reopened.Close();
            cleanup = DeleteUx2EnvDTemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && completeBudgets && invalidRenderingBlocked &&
                            invalidBudgetBlocked && invalidSkyBlocked && dryRun && renderPlans && planWarnings &&
                            staleDraftBlocked && staleFingerprintBlocked && staleApprovalBlocked && canonicalCommand &&
                            undoRedo && persistence && lotBPreserved && lotCPreserved &&
                            environmentTailsPreserved && areaNeighborsPreserved && projectNeighborsPreserved &&
                            structuredDiagnostics && humanText;
            return new StudioUx2EnvDSmokeResult(succeeded, explicitSelection, completeBudgets,
                invalidRenderingBlocked, invalidBudgetBlocked, invalidSkyBlocked, dryRun, renderPlans, planWarnings,
                staleDraftBlocked, staleFingerprintBlocked, staleApprovalBlocked, canonicalCommand, undoRedo,
                persistence, lotBPreserved, lotCPreserved, environmentTailsPreserved, areaNeighborsPreserved,
                projectNeighborsPreserved, structuredDiagnostics, humanText, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_D_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2EnvDSmoke();
        }
        finally
        {
            editing.Close(); reopened.Close();
            cleanup = DeleteUx2EnvDTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_D_CLEANUP_FAILED");
        }
    }

    private static bool ApplyUx2EnvDRevisionMutation(
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
        var result = controller._setAreaEnvironmentUx2EnvD.Handle(history.State,
            new PatchAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteUx2EnvDTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-d-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvDSmokeResult FailedUx2EnvDSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false);
}
