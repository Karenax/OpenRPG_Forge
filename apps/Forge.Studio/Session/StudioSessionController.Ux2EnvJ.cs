using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2EnvJMapCode = "studio.map.profile_invalid";
    private const string Ux2EnvJPinCode = "studio.map.pin_invalid";
    private const string Ux2EnvJLodCode = "studio.lod.profile_invalid";
    private const string Ux2EnvJOverrideCode = "studio.lod.override_invalid";

    private readonly SetAreaAuthoringHandler _setAreaAuthoringUx2EnvJ = new(new SystemClock());
    private StudioUx2EnvJDraft? _approvedMapLodUx2EnvJ;
    private ProjectRevision? _approvedMapLodRevisionUx2EnvJ;
    private string? _approvedMapLodFingerprintUx2EnvJ;

    internal IReadOnlyList<StudioUx2EnvJAreaChoice> MapLodChoicesUx2EnvJ
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildMapLodChoicesUx2EnvJ(project);
        }
    }

    private static IReadOnlyList<StudioUx2EnvJAreaChoice> BuildMapLodChoicesUx2EnvJ(
        ProjectDefinition project)
    {
        var duplicateNames = project.Areas.GroupBy(area => area.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var map = area.Authoring.Map;
                var lod = area.Authoring.Experience.Lod;
                var display = HumanTokenUx2EnvJ(area.DisplayName);
                if (duplicateNames[area.DisplayName] > 1)
                    display = $"{display} · {HumanTokenUx2EnvJ(area.Slug)}";
                var mapSummary = map.Enabled
                    ? $"{map.FloorCount} étage(s) · {map.Pins.Count} repère(s) · " +
                      (map.FogOfWar ? "brouillard actif" : "brouillard désactivé")
                    : "carte désactivée";
                return new StudioUx2EnvJAreaChoice(
                    area.Id,
                    $"{display} · {mapSummary} · LOD {HumanLodModeUx2EnvJ(lod.DefaultSourceMode)}",
                    map.FloorCount,
                    map.Pins.Count,
                    lod.AssetOverrides.Count,
                    lod.DefaultSourceMode);
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2EnvJDraft? CreateMapLodDraftUx2EnvJ(AreaId? requestedAreaId = null)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || requestedAreaId is null) return null;
        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1
            ? CreateMapLodDraftUx2EnvJ(history.State.Revision, matches[0])
            : null;
    }

    private static StudioUx2EnvJDraft CreateMapLodDraftUx2EnvJ(
        ProjectRevision revision,
        AreaDefinition area)
    {
        var map = area.Authoring.Map;
        var lod = area.Authoring.Experience.Lod;
        return new StudioUx2EnvJDraft(
            area.Id,
            revision,
            MapLodSourceFingerprintUx2EnvJ(area),
            area.DisplayName,
            area.Slug,
            map.Enabled,
            map.FogOfWar,
            map.Projection,
            map.FloorCount.ToString(CultureInfo.InvariantCulture),
            map.Generate2D,
            map.Generate3D,
            map.ExplorationScope,
            InvariantUx2EnvJ(map.ExplorationCellSize),
            map.RevealApi,
            map.Pins.Select(pin => new StudioUx2EnvJPinDraft(
                pin.Id,
                pin.Label,
                InvariantUx2EnvJ(pin.X),
                InvariantUx2EnvJ(pin.Z),
                pin.Floor.ToString(CultureInfo.InvariantCulture),
                pin.InitiallyVisible)).ToArray(),
            lod.DefaultSourceMode,
            JoinFloatsUx2EnvJ(lod.DistanceThresholds),
            lod.MaximumVisibleTriangles.ToString(CultureInfo.InvariantCulture),
            lod.MaximumHlodClusters.ToString(CultureInfo.InvariantCulture),
            InvariantUx2EnvJ(lod.HlodClusterSize),
            InvariantUx2EnvJ(lod.PoppingTolerance),
            lod.CrossFade,
            lod.MeasurePopping,
            lod.AssetOverrides.Select(item => new StudioUx2EnvJLodOverrideDraft(
                item.Id,
                item.AssetId,
                item.SourceMode,
                JoinFloatsUx2EnvJ(item.ScreenRatios),
                string.Join("; ", item.TriangleBudgets.Select(value =>
                    value.ToString(CultureInfo.InvariantCulture))),
                InvariantUx2EnvJ(item.CrossFadeSeconds),
                item.Provenance)).ToArray());
    }

    internal StudioUx2EnvJPreview PreviewMapLodUx2EnvJ(StudioUx2EnvJDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelMapLodApprovalUx2EnvJ();
        var history = History;
        var project = Project;
        if (history is null || project is null)
            return FailedMapLodPreviewUx2EnvJ("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedMapLodPreviewUx2EnvJ(
                "Le projet a changé depuis l’ouverture. Rechargez la zone avant l’aperçu.");
        if (!TryBuildMapLodUx2EnvJ(draft, true, out var command, out var sourceArea,
                out var candidateAuthoring, out var diagnostics, out var message))
            return new(false, message, string.Empty, false, new([]),
                GroupMapLodDiagnosticsUx2EnvJ(diagnostics), [], null, [], null,
                false, false, false, false, false, true, true, true);

        var sourceProject = project;
        var sourceRevision = history.State.Revision;
        var sourceJournal = history.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceCanUndo = history.CanUndo;
        var sourceCanRedo = history.CanRedo;
        var dryRun = _setAreaAuthoringUx2EnvJ.Handle(history.State, command!);
        var dryRunPreserved = ReferenceEquals(sourceProject, Project) &&
                              history.State.Revision == sourceRevision &&
                              history.Journal.SequenceEqual(sourceJournal) &&
                              IsDirty == sourceDirty && history.CanUndo == sourceCanUndo &&
                              history.CanRedo == sourceCanRedo;
        if (!dryRun.Succeeded || !dryRunPreserved)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => CommandDiagnosticUx2EnvJ(item,
                $"areas/{draft.AreaId}/authoring")).ToArray();
            return new(false,
                commandDiagnostics.FirstOrDefault()?.Message ??
                "La commande canonique a refusé cet aperçu.",
                string.Empty,
                false,
                BuildMapLodDiffUx2EnvJ(sourceArea!.Authoring, candidateAuthoring!),
                GroupMapLodDiagnosticsUx2EnvJ(commandDiagnostics), [], null, [], null,
                false, false, false, false, false, true, true, true);
        }

        var candidateArea = sourceArea!.WithAuthoring(candidateAuthoring!);
        var candidateProject = ReplaceAreaUx2EnvJ(project, candidateArea.Id, candidateArea);
        var render = AreaExperienceRenderPlanBuilder.Build(candidateArea);
        var hlod = AreaHlodGenerationPlanner.Plan(candidateArea, dryRun: true);
        var performance = AreaPerformanceAnalyzer.Analyze(candidateProject, candidateArea.Id);
        var floors = render.MapFloors.Select(item => new StudioUx2EnvJMapFloorPlan(
            item.Floor,
            item.PinCount,
            item.ExplorationCellCount,
            item.Generate2D,
            item.Generate3D,
            item.FogOfWar)).ToArray();
        var map = candidateAuthoring.Map;
        var initialReveal = new AreaMapExplorationState(
            map.ExplorationScope,
            "aperçu-studio",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<Guid>());
        var firstPin = map.Pins.FirstOrDefault();
        var revealed = AreaExperienceAuthoringScriptService.Reveal(
            candidateArea,
            initialReveal,
            firstPin?.X ?? 0,
            firstPin?.Z ?? 0,
            firstPin?.Id);
        var revealPlan = new StudioUx2EnvJRevealPlan(
            HumanExplorationScopeUx2EnvJ(revealed.Scope),
            revealed.RevealedCells.Count,
            revealed.RevealedPins.Count,
            map.Enabled
                ? $"État copié : {revealed.RevealedCells.Count} cellule(s) et " +
                  $"{revealed.RevealedPins.Count} repère(s) révélés."
                : "Carte désactivée : l’état copié reste inchangé.");
        var hooks = hlod.Clusters.Select(item => new StudioUx2EnvJHlodHookPlan(
            item.CellX,
            item.CellZ,
            item.SourceEntityIds.Count,
            item.MaximumTriangles,
            item.Operation)).ToArray();
        var performancePlan = new StudioUx2EnvJPerformancePlan(
            performance.EntityCount,
            performance.UniqueAssetCount,
            performance.EstimatedDrawUnits,
            performance.OccupiedOcclusionSectors,
            performance.NearLodCount,
            performance.MidLodCount,
            performance.FarLodCount,
            performance.EstimatedPopping.ToString("0.###", CultureInfo.CurrentCulture),
            performance.IsWithinBudget,
            performance.Diagnostics.ToArray());
        var planDiagnostics = hlod.Diagnostics.Select(item => new StudioUx2EnvJDiagnostic(
                item.Code, item.Path, item.Message))
            .Concat(performance.Diagnostics.Select(item => new StudioUx2EnvJDiagnostic(
                "area.performance.budget_warning",
                $"areas/{candidateArea.Id}/authoring/experience/lod",
                HumanizePerformanceDiagnosticUx2EnvJ(item),
                Blocking: false)))
            .ToArray();
        var diff = BuildMapLodDiffUx2EnvJ(sourceArea.Authoring, candidateAuthoring);
        if (!hlod.CanGenerate)
        {
            return new(false,
                "Le plan HLOD dépasse le budget de clusters; corrigez-le avant application.",
                BuildMapLodSummaryUx2EnvJ(candidateArea, floors, revealPlan, hlod, performancePlan),
                false,
                diff,
                GroupMapLodDiagnosticsUx2EnvJ(planDiagnostics),
                floors,
                revealPlan,
                hooks,
                performancePlan,
                true, true, true, true, true, true, true, true);
        }

        if (diff.HasChanges)
        {
            _approvedMapLodUx2EnvJ = draft;
            _approvedMapLodRevisionUx2EnvJ = history.State.Revision;
            _approvedMapLodFingerprintUx2EnvJ = MapLodDraftFingerprintUx2EnvJ(draft);
        }
        return new(true,
            diff.HasChanges
                ? "Aperçu sec validé · la carte et la politique LOD ne sont pas encore appliquées."
                : "Aperçu sec validé · aucune différence à appliquer.",
            BuildMapLodSummaryUx2EnvJ(candidateArea, floors, revealPlan, hlod, performancePlan),
            diff.HasChanges,
            diff,
            GroupMapLodDiagnosticsUx2EnvJ(planDiagnostics),
            floors,
            revealPlan,
            hooks,
            performancePlan,
            true, true, true, true, true, true, true, true);
    }

    internal StudioUx2EnvJActionResult ApplyApprovedMapLodUx2EnvJ()
    {
        var draft = _approvedMapLodUx2EnvJ;
        var history = History;
        if (draft is null || history is null || _approvedMapLodRevisionUx2EnvJ != history.State.Revision ||
            !string.Equals(_approvedMapLodFingerprintUx2EnvJ,
                MapLodDraftFingerprintUx2EnvJ(draft), StringComparison.Ordinal))
        {
            CancelMapLodApprovalUx2EnvJ();
            return new(false, "Relancez l’aperçu sec avant d’appliquer la carte et le LOD.");
        }
        if (!TryBuildMapLodUx2EnvJ(draft, false, out var command, out var sourceArea,
                out var candidateAuthoring, out _, out var message))
        {
            CancelMapLodApprovalUx2EnvJ();
            return new(false, message);
        }
        var candidateArea = sourceArea!.WithAuthoring(candidateAuthoring!);
        var dryPlan = AreaHlodGenerationPlanner.Plan(candidateArea, dryRun: true);
        if (!dryPlan.CanGenerate)
        {
            CancelMapLodApprovalUx2EnvJ();
            return new(false, "Le budget HLOD refuse cette application.");
        }
        var dryRun = _setAreaAuthoringUx2EnvJ.Handle(history.State, command! with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            CancelMapLodApprovalUx2EnvJ();
            return new(false, HumanizeMapLodDiagnosticUx2EnvJ(dryRun.Diagnostics.FirstOrDefault()?.Code));
        }
        var applied = _setAreaAuthoringUx2EnvJ.Handle(history.State, command!);
        CancelMapLodApprovalUx2EnvJ();
        if (!applied.Succeeded)
            return new(false, HumanizeMapLodDiagnosticUx2EnvJ(applied.Diagnostics.FirstOrDefault()?.Code));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true,
            "Carte, brouillard et politique LOD appliqués par set_area_authoring · une commande annulable.");
    }

    internal void CancelMapLodApprovalUx2EnvJ()
    {
        _approvedMapLodUx2EnvJ = null;
        _approvedMapLodRevisionUx2EnvJ = null;
        _approvedMapLodFingerprintUx2EnvJ = null;
    }

    internal StudioUx2EnvJApprovalSnapshot CaptureMapLodApprovalUx2EnvJ() => new(
        _approvedMapLodUx2EnvJ,
        _approvedMapLodRevisionUx2EnvJ,
        _approvedMapLodFingerprintUx2EnvJ);

    internal void RestoreMapLodApprovalUx2EnvJ(StudioUx2EnvJApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedMapLodUx2EnvJ = snapshot.Draft;
        _approvedMapLodRevisionUx2EnvJ = snapshot.Revision;
        _approvedMapLodFingerprintUx2EnvJ = snapshot.Fingerprint;
    }

    private bool TryBuildMapLodUx2EnvJ(
        StudioUx2EnvJDraft draft,
        bool dryRun,
        out SetAreaAuthoringCommand? command,
        out AreaDefinition? sourceArea,
        out AreaAuthoringProfile? candidateAuthoring,
        out IReadOnlyList<StudioUx2EnvJDiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateAuthoring = null;
        var found = new List<StudioUx2EnvJDiagnostic>();
        diagnostics = found;
        message = "La fiche carte et LOD est invalide.";
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
                MapLodSourceFingerprintUx2EnvJ(sourceArea), StringComparison.Ordinal))
        {
            message = "La zone a changé depuis l’ouverture; ses données voisines ne seront pas réinjectées.";
            return false;
        }

        if (!TryIntUx2EnvJ(draft.FloorCount, out var floorCount) || floorCount is < 1 or > 256)
            found.Add(new(Ux2EnvJMapCode, "map/floorCount",
                "Le nombre d’étages doit être un entier compris entre 1 et 256; la valeur brute n’est jamais plafonnée silencieusement."));
        if (string.IsNullOrWhiteSpace(draft.Projection))
            found.Add(new(Ux2EnvJMapCode, "map/projection",
                "La projection de carte est obligatoire."));
        if (!TryFloatUx2EnvJ(draft.ExplorationCellSize, out var cellSize) || cellSize is < 1 or > 256)
            found.Add(new(Ux2EnvJMapCode, "map/explorationCellSize",
                "La cellule d’exploration doit être finie et rester entre 1 et 256 mètres."));
        if (!draft.Generate2D && !draft.Generate3D)
            found.Add(new(Ux2EnvJMapCode, "map/generation",
                "Activez au moins une génération de carte, en 2D ou en 3D."));
        if (string.IsNullOrWhiteSpace(draft.RevealApi))
            found.Add(new(Ux2EnvJMapCode, "map/revealApi",
                "L’API de révélation ne peut pas être vide."));

        var pins = new List<AreaMapPin>();
        foreach (var pin in draft.Pins)
        {
            var validX = TryFloatUx2EnvJ(pin.X, out var x);
            var validZ = TryFloatUx2EnvJ(pin.Z, out var z);
            var validFloor = TryIntUx2EnvJ(pin.Floor, out var floor);
            if (pin.Id == Guid.Empty || string.IsNullOrWhiteSpace(pin.Label) || !validX || !validZ ||
                !validFloor || floor < 0 || floor >= floorCount)
            {
                found.Add(new(Ux2EnvJPinCode, "map/pins",
                    "Chaque repère exige un nom, une position finie, un étage existant et une identité stable."));
                continue;
            }
            pins.Add(new AreaMapPin(pin.Id, pin.Label.Trim(), x, z, floor, pin.InitiallyVisible));
        }
        if (pins.Select(pin => pin.Id).Distinct().Count() != pins.Count)
            found.Add(new(Ux2EnvJPinCode, "map/pins",
                "Deux repères ne peuvent pas partager la même identité."));

        var thresholdsValid = TryFloatListUx2EnvJ(draft.DistanceThresholds, out var thresholds);
        if (!thresholdsValid || thresholds.Count is < 1 or > 6 ||
            thresholds.Any(value => value <= 0) ||
            thresholds.Zip(thresholds.Skip(1), (left, right) => right > left).Any(valid => !valid))
            found.Add(new(Ux2EnvJLodCode, "experience/lod/distanceThresholds",
                "Saisissez 1 à 6 distances finies, positives et strictement croissantes."));
        if (!TryIntUx2EnvJ(draft.MaximumVisibleTriangles, out var maximumTriangles) || maximumTriangles <= 0)
            found.Add(new(Ux2EnvJLodCode, "experience/lod/maximumVisibleTriangles",
                "Le budget de triangles visibles doit être un entier positif."));
        if (!TryIntUx2EnvJ(draft.MaximumHlodClusters, out var maximumClusters) || maximumClusters <= 0)
            found.Add(new(Ux2EnvJLodCode, "experience/lod/maximumHlodClusters",
                "Le budget de clusters HLOD doit être un entier positif."));
        if (!TryFloatUx2EnvJ(draft.HlodClusterSize, out var clusterSize) || clusterSize <= 0)
            found.Add(new(Ux2EnvJLodCode, "experience/lod/hlodClusterSize",
                "La taille de cellule HLOD doit être finie et positive."));
        if (!TryFloatUx2EnvJ(draft.PoppingTolerance, out var popping) || popping is < 0 or > 1)
            found.Add(new(Ux2EnvJLodCode, "experience/lod/poppingTolerance",
                "La tolérance de popping doit rester entre 0 et 1."));

        var overrides = new List<AreaLodAssetOverride>();
        foreach (var item in draft.AssetOverrides)
        {
            var ratiosValid = TryFloatListUx2EnvJ(item.ScreenRatios, out var ratios);
            var budgetsValid = TryIntListUx2EnvJ(item.TriangleBudgets, out var budgets);
            var crossFadeValid = TryFloatUx2EnvJ(item.CrossFadeSeconds, out var crossFadeSeconds);
            if (item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.AssetId) ||
                string.IsNullOrWhiteSpace(item.Provenance) || !ratiosValid || !budgetsValid ||
                ratios.Count == 0 || ratios.Count != budgets.Count ||
                ratios.Any(value => value is <= 0 or > 1) || budgets.Any(value => value <= 0) ||
                !crossFadeValid || crossFadeSeconds is < 0 or > 10)
            {
                found.Add(new(Ux2EnvJOverrideCode, "experience/lod/assetOverrides",
                    "Chaque asset exige des ratios 0–1, autant de budgets positifs, un fondu 0–10 s et une provenance."));
                continue;
            }
            overrides.Add(new AreaLodAssetOverride(
                item.Id,
                item.AssetId.Trim(),
                item.SourceMode,
                ratios,
                budgets,
                crossFadeSeconds,
                item.Provenance.Trim()));
        }
        if (overrides.Select(item => item.Id).Distinct().Count() != overrides.Count ||
            overrides.Select(item => item.AssetId).Distinct(StringComparer.Ordinal).Count() != overrides.Count)
            found.Add(new(Ux2EnvJOverrideCode, "experience/lod/assetOverrides",
                "Les identités et les assets des overrides LOD doivent être uniques."));
        if (found.Count > 0)
        {
            message = found[0].Message;
            return false;
        }

        try
        {
            var source = sourceArea.Authoring;
            var sourceMap = source.Map;
            var map = new AreaMapProfile(
                draft.MapEnabled,
                draft.FogOfWar,
                draft.Projection.Trim(),
                floorCount,
                pins,
                sourceMap.Id,
                sourceMap.SchemaVersion,
                sourceMap.Provenance,
                draft.Generate2D,
                draft.Generate3D,
                draft.ExplorationScope,
                cellSize,
                draft.RevealApi.Trim());
            var sourceExperience = source.Experience;
            var lod = new AreaLodAuthoringProfile(
                draft.DefaultSourceMode,
                thresholds,
                maximumTriangles,
                maximumClusters,
                clusterSize,
                popping,
                draft.CrossFade,
                draft.MeasurePopping,
                overrides);
            var experience = new AreaExperienceAuthoringProfile(
                sourceExperience.Id,
                sourceExperience.SchemaVersion,
                sourceExperience.Provenance,
                sourceExperience.AudioSnapshots,
                sourceExperience.Visibility,
                lod,
                sourceExperience.RequireRuntimeMetadata);
            candidateAuthoring = new AreaAuthoringProfile(
                source.SchemaVersion,
                source.UserEvents,
                map,
                source.Layers,
                experience,
                source.RequireLayerRuntimeMetadata);
            command = new SetAreaAuthoringCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                sourceArea.Id,
                candidateAuthoring,
                dryRun);
            message = "Carte et politique LOD prêtes pour la commande canonique.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            found.Add(new(Ux2EnvJMapCode, "authoring",
                $"Le profil ne peut pas être reconstruit · {exception.Message}"));
            message = found[0].Message;
            return false;
        }
    }

    private static StudioUx2EnvJDiff BuildMapLodDiffUx2EnvJ(
        AreaAuthoringProfile source,
        AreaAuthoringProfile candidate)
    {
        var entries = new List<string>();
        var sourceMap = source.Map;
        var map = candidate.Map;
        if (sourceMap.Enabled != map.Enabled || sourceMap.FogOfWar != map.FogOfWar)
            entries.Add($"Carte : {(map.Enabled ? "active" : "désactivée")} · " +
                        $"brouillard {(map.FogOfWar ? "actif" : "désactivé")}.");
        if (!sourceMap.Projection.Equals(map.Projection, StringComparison.Ordinal) ||
            sourceMap.FloorCount != map.FloorCount || sourceMap.Generate2D != map.Generate2D ||
            sourceMap.Generate3D != map.Generate3D)
            entries.Add($"Génération : projection {HumanTokenUx2EnvJ(map.Projection)} · " +
                        $"{map.FloorCount} étage(s) · {(map.Generate2D ? "2D" : "sans 2D")} · " +
                        $"{(map.Generate3D ? "3D" : "sans 3D")}.");
        if (sourceMap.ExplorationScope != map.ExplorationScope ||
            !sourceMap.ExplorationCellSize.Equals(map.ExplorationCellSize) ||
            !sourceMap.RevealApi.Equals(map.RevealApi, StringComparison.Ordinal))
            entries.Add($"Exploration : {HumanExplorationScopeUx2EnvJ(map.ExplorationScope)} · " +
                        $"cellules {map.ExplorationCellSize:0.##} m · API de révélation conservée.");
        if (!SamePinsUx2EnvJ(sourceMap.Pins, map.Pins))
            entries.Add($"Repères : {sourceMap.Pins.Count} → {map.Pins.Count}, avec étages et visibilité initiale.");
        var sourceLod = source.Experience.Lod;
        var lod = candidate.Experience.Lod;
        if (sourceLod.DefaultSourceMode != lod.DefaultSourceMode ||
            !sourceLod.DistanceThresholds.SequenceEqual(lod.DistanceThresholds))
            entries.Add($"LOD : mode {HumanLodModeUx2EnvJ(lod.DefaultSourceMode)} · seuils " +
                        $"{string.Join(" / ", lod.DistanceThresholds.Select(value => $"{value:0.##} m"))}.");
        if (sourceLod.MaximumVisibleTriangles != lod.MaximumVisibleTriangles ||
            sourceLod.MaximumHlodClusters != lod.MaximumHlodClusters ||
            !sourceLod.HlodClusterSize.Equals(lod.HlodClusterSize) ||
            !sourceLod.PoppingTolerance.Equals(lod.PoppingTolerance))
            entries.Add($"Budgets : {lod.MaximumVisibleTriangles:N0} triangles · " +
                        $"{lod.MaximumHlodClusters} clusters de {lod.HlodClusterSize:0.##} m · " +
                        $"tolérance {lod.PoppingTolerance:P0}.");
        if (sourceLod.CrossFade != lod.CrossFade || sourceLod.MeasurePopping != lod.MeasurePopping)
            entries.Add($"Transitions : fondu {(lod.CrossFade ? "actif" : "désactivé")} · " +
                        $"mesure du popping {(lod.MeasurePopping ? "active" : "désactivée")}.");
        if (!SameLodOverridesUx2EnvJ(sourceLod.AssetOverrides, lod.AssetOverrides))
            entries.Add($"Overrides asset : {sourceLod.AssetOverrides.Count} → {lod.AssetOverrides.Count}.");
        return new(entries);
    }

    private static bool SamePinsUx2EnvJ(
        IReadOnlyList<AreaMapPin> left,
        IReadOnlyList<AreaMapPin> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Id == pair.Second.Id &&
            pair.First.Label.Equals(pair.Second.Label, StringComparison.Ordinal) &&
            pair.First.X.Equals(pair.Second.X) && pair.First.Z.Equals(pair.Second.Z) &&
            pair.First.Floor == pair.Second.Floor &&
            pair.First.InitiallyVisible == pair.Second.InitiallyVisible);

    private static bool SameLodOverridesUx2EnvJ(
        IReadOnlyList<AreaLodAssetOverride> left,
        IReadOnlyList<AreaLodAssetOverride> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Id == pair.Second.Id &&
            pair.First.AssetId.Equals(pair.Second.AssetId, StringComparison.Ordinal) &&
            pair.First.SourceMode == pair.Second.SourceMode &&
            pair.First.ScreenRatios.SequenceEqual(pair.Second.ScreenRatios) &&
            pair.First.TriangleBudgets.SequenceEqual(pair.Second.TriangleBudgets) &&
            pair.First.CrossFadeSeconds.Equals(pair.Second.CrossFadeSeconds) &&
            pair.First.Provenance.Equals(pair.Second.Provenance, StringComparison.Ordinal));

    private static string BuildMapLodSummaryUx2EnvJ(
        AreaDefinition area,
        IReadOnlyList<StudioUx2EnvJMapFloorPlan> floors,
        StudioUx2EnvJRevealPlan reveal,
        AreaHlodGenerationPlan hlod,
        StudioUx2EnvJPerformancePlan performance)
    {
        var map = area.Authoring.Map;
        var lod = area.Authoring.Experience.Lod;
        var generation = hlod.SourceMode == AreaLodSourceMode.Imported
            ? "Mode importé : aucun cluster à générer."
            : $"Plan HLOD : {hlod.Clusters.Count} cluster(s) déterministe(s) confiné(s).";
        return $"Carte {floors.Count} étage(s), {map.Pins.Count} repère(s), " +
               $"{(map.FogOfWar ? "brouillard actif" : "brouillard désactivé")}, " +
               $"exploration {HumanExplorationScopeUx2EnvJ(map.ExplorationScope)}. " +
               $"{reveal.Summary} {generation} " +
               $"Budget : {lod.MaximumVisibleTriangles:N0} triangles, " +
               $"{performance.EstimatedDrawUnits} unité(s) de dessin estimée(s), " +
               $"popping {performance.EstimatedPopping}. " +
               "Studio construit des plans et un état de révélation copié : aucun GLB n’est généré, " +
               "aucune carte n’est rendue et aucun état joueur/parti/monde n’est persisté par cet aperçu.";
    }

    private static IReadOnlyList<StudioUx2EnvJDiagnosticGroup> GroupMapLodDiagnosticsUx2EnvJ(
        IReadOnlyList<StudioUx2EnvJDiagnostic> diagnostics)
    {
        var definitions = new[]
        {
            (Key: "map", Title: "Carte, brouillard et repères"),
            (Key: "lod", Title: "LOD et HLOD"),
            (Key: "performance", Title: "Budget et popping"),
            (Key: "command", Title: "Commande canonique"),
        };
        return definitions.Select(definition =>
        {
            var items = diagnostics.Where(item =>
                MapLodDiagnosticGroupUx2EnvJ(item) == definition.Key).ToArray();
            var state = items.Any(item => item.Blocking)
                ? "À corriger"
                : items.Length > 0 ? "Avertissement" : "Valide";
            return new StudioUx2EnvJDiagnosticGroup(definition.Key, definition.Title, state, items);
        }).ToArray();
    }

    private static string MapLodDiagnosticGroupUx2EnvJ(StudioUx2EnvJDiagnostic diagnostic)
    {
        if (diagnostic.Code is Ux2EnvJMapCode or Ux2EnvJPinCode ||
            diagnostic.Code.Contains(".map_", StringComparison.Ordinal) ||
            diagnostic.Path.Contains("/map", StringComparison.Ordinal)) return "map";
        if (diagnostic.Code is Ux2EnvJLodCode or Ux2EnvJOverrideCode ||
            diagnostic.Code.Contains("hlod", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Path.Contains("/lod", StringComparison.Ordinal)) return "lod";
        if (diagnostic.Code.StartsWith("area.performance.", StringComparison.Ordinal)) return "performance";
        return "command";
    }

    private static StudioUx2EnvJDiagnostic CommandDiagnosticUx2EnvJ(
        CommandDiagnostic diagnostic,
        string fallbackPath)
    {
        var message = diagnostic.Message ?? string.Empty;
        var separator = message.IndexOf(" · ", StringComparison.Ordinal);
        return new StudioUx2EnvJDiagnostic(
            diagnostic.Code,
            separator > 0 ? message[..separator] : fallbackPath,
            separator > 0 ? message[(separator + 3)..] : HumanizeMapLodDiagnosticUx2EnvJ(diagnostic.Code));
    }

    private static string HumanizeMapLodDiagnosticUx2EnvJ(string? code) => code switch
    {
        null or "" => "Aucun diagnostic.",
        "command.revision_conflict" => "Le projet a changé; rechargez la zone et relancez l’aperçu.",
        "area.authoring.area_missing" => "La zone sélectionnée n’existe plus.",
        AreaExperienceAuthoringValidator.MapCode => "La carte, ses repères ou son API de révélation sont invalides.",
        AreaExperienceAuthoringValidator.LodCode => "La politique LOD/HLOD est invalide.",
        AreaHlodGenerationPlanner.ClusterBudgetCode => "Le nombre de clusters dépasse le budget HLOD.",
        _ => "La validation canonique a refusé cette carte ou cette politique LOD.",
    };

    private static string HumanizePerformanceDiagnosticUx2EnvJ(string value)
    {
        if (value.StartsWith("entities:", StringComparison.Ordinal)) return "Le nombre d’entités dépasse le budget d’édition.";
        if (value.StartsWith("assets:", StringComparison.Ordinal)) return "Le nombre d’assets distincts dépasse le budget d’édition.";
        if (value.StartsWith("draw-units:", StringComparison.Ordinal)) return "Les unités de dessin estimées dépassent le budget d’édition.";
        if (value.StartsWith("lod-popping:", StringComparison.Ordinal)) return "Le popping estimé dépasse la tolérance choisie.";
        if (value.StartsWith("visible-triangles:", StringComparison.Ordinal)) return "L’estimation de triangles visibles dépasse le budget LOD.";
        return "Le rapport de performance contient un avertissement.";
    }

    private static StudioUx2EnvJPreview FailedMapLodPreviewUx2EnvJ(string message) => new(
        false, message, string.Empty, false, new([]), GroupMapLodDiagnosticsUx2EnvJ([]),
        [], null, [], null, false, false, false, false, false, true, true, true);

    private static string MapLodSourceFingerprintUx2EnvJ(AreaDefinition area)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string MapLodDraftFingerprintUx2EnvJ(StudioUx2EnvJDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.ToString());
            writer.Write(draft.BaseRevision.Value);
            writer.Write(draft.SourceFingerprint ?? string.Empty);
            writer.Write(draft.AreaDisplayName ?? string.Empty);
            writer.Write(draft.AreaSlug ?? string.Empty);
            writer.Write(draft.MapEnabled);
            writer.Write(draft.FogOfWar);
            writer.Write(draft.Projection ?? string.Empty);
            writer.Write(draft.FloorCount ?? string.Empty);
            writer.Write(draft.Generate2D);
            writer.Write(draft.Generate3D);
            writer.Write((int)draft.ExplorationScope);
            writer.Write(draft.ExplorationCellSize ?? string.Empty);
            writer.Write(draft.RevealApi ?? string.Empty);
            writer.Write(draft.Pins.Count);
            foreach (var pin in draft.Pins)
            {
                writer.Write(pin.Id.ToByteArray());
                writer.Write(pin.Label ?? string.Empty);
                writer.Write(pin.X ?? string.Empty);
                writer.Write(pin.Z ?? string.Empty);
                writer.Write(pin.Floor ?? string.Empty);
                writer.Write(pin.InitiallyVisible);
            }
            writer.Write((int)draft.DefaultSourceMode);
            writer.Write(draft.DistanceThresholds ?? string.Empty);
            writer.Write(draft.MaximumVisibleTriangles ?? string.Empty);
            writer.Write(draft.MaximumHlodClusters ?? string.Empty);
            writer.Write(draft.HlodClusterSize ?? string.Empty);
            writer.Write(draft.PoppingTolerance ?? string.Empty);
            writer.Write(draft.CrossFade);
            writer.Write(draft.MeasurePopping);
            writer.Write(draft.AssetOverrides.Count);
            foreach (var item in draft.AssetOverrides)
            {
                writer.Write(item.Id.ToByteArray());
                writer.Write(item.AssetId ?? string.Empty);
                writer.Write((int)item.SourceMode);
                writer.Write(item.ScreenRatios ?? string.Empty);
                writer.Write(item.TriangleBudgets ?? string.Empty);
                writer.Write(item.CrossFadeSeconds ?? string.Empty);
                writer.Write(item.Provenance ?? string.Empty);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool TryFloatUx2EnvJ(string? text, out float value)
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

    private static bool TryIntUx2EnvJ(string? text, out int value) =>
        int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value) ||
        int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer,
            CultureInfo.CurrentCulture, out value);

    private static bool TryFloatListUx2EnvJ(string? text, out IReadOnlyList<float> values)
    {
        var result = new List<float>();
        foreach (var token in SplitListUx2EnvJ(text))
        {
            if (!TryFloatUx2EnvJ(token, out var value))
            {
                values = [];
                return false;
            }
            result.Add(value);
        }
        values = result;
        return result.Count > 0;
    }

    private static bool TryIntListUx2EnvJ(string? text, out IReadOnlyList<int> values)
    {
        var result = new List<int>();
        foreach (var token in SplitListUx2EnvJ(text))
        {
            if (!TryIntUx2EnvJ(token, out var value))
            {
                values = [];
                return false;
            }
            result.Add(value);
        }
        values = result;
        return result.Count > 0;
    }

    private static IReadOnlyList<string> SplitListUx2EnvJ(string? text) =>
        (text ?? string.Empty)
        .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string InvariantUx2EnvJ(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string JoinFloatsUx2EnvJ(IEnumerable<float> values) =>
        string.Join("; ", values.Select(InvariantUx2EnvJ));

    private static string HumanTokenUx2EnvJ(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsTechnicalIdentifierUx2EnvJ(text)
            ? "Valeur conservée"
            : text;
    }

    private static bool ContainsTechnicalIdentifierUx2EnvJ(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            "(?i)(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})") ||
        text.Contains("areas/", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("forge://", StringComparison.OrdinalIgnoreCase);

    private static string HumanExplorationScopeUx2EnvJ(AreaMapExplorationScope scope) => scope switch
    {
        AreaMapExplorationScope.Player => "par joueur",
        AreaMapExplorationScope.Party => "par groupe",
        AreaMapExplorationScope.World => "partagée au monde",
        _ => "inconnue",
    };

    private static string HumanLodModeUx2EnvJ(AreaLodSourceMode mode) => mode switch
    {
        AreaLodSourceMode.Imported => "importé",
        AreaLodSourceMode.Generated => "généré",
        AreaLodSourceMode.Hybrid => "hybride",
        _ => "inconnu",
    };

    private static ProjectDefinition ReplaceAreaUx2EnvJ(
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

    internal static ProjectDefinition CreateUx2EnvJContractProject(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Areas.Count == 0) return source;
        var area = source.Areas.First();
        var entities = area.Entities.ToList();
        if (entities.Count < 4 && source.Blueprints.Count > 0)
        {
            var blueprint = entities.FirstOrDefault()?.BlueprintId ?? source.Blueprints.First().Id;
            while (entities.Count < 4)
                entities.Add(new EntityInstance(EntityId.From(Guid.NewGuid()), blueprint,
                    TransformValue.Identity, ["studio-hlod-fixture"]));
        }
        if (entities.Count >= 4)
        {
            var near = TransformValue.Identity;
            var far = new TransformValue(new Vector3Value(96, 0, 96),
                QuaternionValue.Identity, Vector3Value.One);
            for (var index = 0; index < 4; index++)
            {
                var item = entities[index];
                entities[index] = new EntityInstance(item.Id, item.BlueprintId,
                    index < 2 ? near : far, item.Tags, item.Overrides, item.BlueprintVersionRequirement);
            }
        }
        area = area.WithEntities(entities);
        var sourceAuthoring = area.Authoring;
        var sourceMap = sourceAuthoring.Map;
        var map = new AreaMapProfile(
            true, true, "orthographic", 2,
            [
                new AreaMapPin(Guid.NewGuid(), "Entrée principale", 0, 0, 0, true),
                new AreaMapPin(Guid.NewGuid(), "Balcon supérieur", 8, 12, 1, false),
            ],
            sourceMap.Id,
            sourceMap.SchemaVersion,
            sourceMap.Provenance,
            true,
            true,
            AreaMapExplorationScope.Party,
            8,
            "area.map.reveal");
        var sourceExperience = sourceAuthoring.Experience;
        var lod = new AreaLodAuthoringProfile(
            AreaLodSourceMode.Hybrid,
            [18, 48, 96],
            1_500_000,
            64,
            32,
            .12f,
            true,
            true,
            [new AreaLodAssetOverride(Guid.NewGuid(), "architecture/pilier",
                AreaLodSourceMode.Generated, [1, .5f, .2f], [12000, 6000, 2400], .25f,
                "studio-contract")]);
        var experience = new AreaExperienceAuthoringProfile(
            sourceExperience.Id,
            sourceExperience.SchemaVersion,
            sourceExperience.Provenance,
            sourceExperience.AudioSnapshots,
            sourceExperience.Visibility,
            lod,
            sourceExperience.RequireRuntimeMetadata);
        var authoring = new AreaAuthoringProfile(
            sourceAuthoring.SchemaVersion,
            sourceAuthoring.UserEvents,
            map,
            sourceAuthoring.Layers,
            experience,
            sourceAuthoring.RequireLayerRuntimeMetadata);
        return ReplaceAreaUx2EnvJ(source, area.Id, area.WithAuthoring(authoring));
    }

    internal StudioUx2EnvJSmokeResult RunUx2EnvJContractSmoke()
    {
        var sourceProject = Project;
        if (sourceProject is null || sourceProject.Areas.Count == 0) return FailedUx2EnvJSmoke();
        var realMapSource = sourceProject.Areas.Any(area =>
            area.Authoring.Map.Enabled && area.Authoring.Map.FogOfWar &&
            area.Authoring.Experience.Lod.DefaultSourceMode != AreaLodSourceMode.Imported);
        var contractProject = CreateUx2EnvJContractProject(sourceProject);
        if (contractProject.Areas.Count == 0) return FailedUx2EnvJSmoke();
        var target = contractProject.Areas.First();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux2-env-j-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(tempRoot, contractProject);
            if (!created.Succeeded || !editing.Open(tempRoot, target.Id.ToString()).Succeeded)
                return FailedUx2EnvJSmoke();
            var history = editing.History!;
            var explicitSelection = editing.CreateMapLodDraftUx2EnvJ() is null &&
                                    editing.CreateMapLodDraftUx2EnvJ(target.Id) is not null;
            var baseDraft = editing.CreateMapLodDraftUx2EnvJ(target.Id)!;

            var invalidMap = editing.PreviewMapLodUx2EnvJ(baseDraft with
            {
                ExplorationCellSize = "0",
            });
            var invalidPin = editing.PreviewMapLodUx2EnvJ(baseDraft with
            {
                Pins = [baseDraft.Pins[0] with { Floor = "99" }],
            });
            var invalidLod = editing.PreviewMapLodUx2EnvJ(baseDraft with
            {
                DistanceThresholds = "50; 20",
            });
            var invalidOverride = editing.PreviewMapLodUx2EnvJ(baseDraft with
            {
                AssetOverrides = [baseDraft.AssetOverrides[0] with { TriangleBudgets = "12000" }],
            });
            var invalidBudget = editing.PreviewMapLodUx2EnvJ(baseDraft with
            {
                MaximumHlodClusters = "1",
            });

            var projectBeforeDryRun = editing.Project;
            var revisionBeforeDryRun = history.State.Revision;
            var journalBeforeDryRun = history.Journal.ToArray();
            var dirtyBeforeDryRun = editing.IsDirty;
            var canUndoBeforeDryRun = history.CanUndo;
            var canRedoBeforeDryRun = history.CanRedo;
            var dryDraft = baseDraft with
            {
                FloorCount = "3",
                ExplorationScope = AreaMapExplorationScope.World,
                ExplorationCellSize = "6.5",
                Pins = [.. baseDraft.Pins, new StudioUx2EnvJPinDraft(
                    Guid.NewGuid(), "Sanctuaire", "12.5", "-4.25", "2", true)],
                DistanceThresholds = "16; 44; 104",
                MaximumVisibleTriangles = "1750000",
                MaximumHlodClusters = "64",
                HlodClusterSize = "32",
                PoppingTolerance = "0.15",
            };
            var dryPreview = editing.PreviewMapLodUx2EnvJ(dryDraft);
            var dryStatePreserved = ReferenceEquals(projectBeforeDryRun, editing.Project) &&
                                    history.State.Revision == revisionBeforeDryRun &&
                                    history.Journal.SequenceEqual(journalBeforeDryRun) &&
                                    editing.IsDirty == dirtyBeforeDryRun &&
                                    history.CanUndo == canUndoBeforeDryRun && history.CanRedo == canRedoBeforeDryRun;
            var canonicalDryRun = dryPreview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  dryStatePreserved;
            var mapRenderPlan = dryPreview.RenderPlanBuilt && dryPreview.Floors.Count == 3 &&
                                dryPreview.Floors.All(floor => floor.Generate2D && floor.Generate3D);
            var revealPlan = dryPreview.RevealPlanBuilt && dryPreview.Reveal is
            {
                RevealedCellCount: 1,
                RevealedPinCount: 1,
            };
            var candidateArea = editing.Project!.Areas.Single(area => area.Id == target.Id)
                .WithAuthoring(BuildAuthoringFromDraftForSmokeUx2EnvJ(editing, dryDraft));
            var firstHlod = AreaHlodGenerationPlanner.Plan(candidateArea, dryRun: true);
            var secondHlod = AreaHlodGenerationPlanner.Plan(candidateArea, dryRun: true);
            var deterministicHlod = dryPreview.HlodPlanBuilt && firstHlod.CanGenerate &&
                                    firstHlod.Clusters.Count >= 2 &&
                                    SameHlodPlansUx2EnvJ(firstHlod, secondHlod);
            var performancePlan = dryPreview.PerformancePlanBuilt && dryPreview.Performance is not null &&
                                  dryPreview.Performance.EntityCount == target.Entities.Count;
            var noRuntimeClaim = dryPreview is
            {
                GenerationSkipped: true,
                PlayerStateMutationSkipped: true,
                RuntimeClaimsBounded: true,
            } && dryPreview.Summary.Contains("aucun GLB", StringComparison.Ordinal) &&
                 dryPreview.Summary.Contains("aucune carte n’est rendue", StringComparison.Ordinal) &&
                 dryPreview.Summary.Contains("aucun état", StringComparison.Ordinal);

            var staleDraft = editing.CreateMapLodDraftUx2EnvJ(target.Id)!;
            var staleMutation = ApplyUx2EnvJRevisionMutation(editing, target.Id, "draft");
            var stalePreview = editing.PreviewMapLodUx2EnvJ(staleDraft);
            var stalePreviewBlocked = staleMutation && !stalePreview.Succeeded && editing.Undo().Succeeded;
            var staleApprovalDraft = editing.CreateMapLodDraftUx2EnvJ(target.Id)! with
            {
                ExplorationCellSize = "7.5",
            };
            var staleApprovalPreview = editing.PreviewMapLodUx2EnvJ(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2EnvJRevisionMutation(editing, target.Id, "approval");
            var staleApprovalApply = editing.ApplyApprovedMapLodUx2EnvJ();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;
            var staleRevisionBlocked = stalePreviewBlocked && staleApprovalBlocked;

            var fingerprintDraft = editing.CreateMapLodDraftUx2EnvJ(target.Id)! with
            {
                ExplorationCellSize = "7.25",
            };
            var fingerprintPreview = editing.PreviewMapLodUx2EnvJ(fingerprintDraft);
            var approval = editing.CaptureMapLodApprovalUx2EnvJ();
            editing.RestoreMapLodApprovalUx2EnvJ(approval with
            {
                Draft = approval.Draft! with { ExplorationCellSize = "7.75" },
            });
            var fingerprintApply = editing.ApplyApprovedMapLodUx2EnvJ();
            var approvalFingerprintBlocked = fingerprintPreview.Succeeded && !fingerprintApply.Succeeded;

            var sourceState = editing.Project!;
            var sourceTarget = sourceState.Areas.Single(area => area.Id == target.Id);
            var sourceAuthoring = sourceTarget.Authoring;
            var sourceExperience = sourceAuthoring.Experience;
            var fresh = editing.CreateMapLodDraftUx2EnvJ(target.Id)! with
            {
                FogOfWar = true,
                FloorCount = "3",
                ExplorationScope = AreaMapExplorationScope.Player,
                ExplorationCellSize = "5.75",
                Pins = [.. baseDraft.Pins, new StudioUx2EnvJPinDraft(
                    Guid.NewGuid(), "Salle du conseil", "18.125", "9.5", "2", true)],
                DefaultSourceMode = AreaLodSourceMode.Hybrid,
                DistanceThresholds = "14; 38; 92",
                MaximumVisibleTriangles = "1650000",
                MaximumHlodClusters = "64",
                HlodClusterSize = "32",
                PoppingTolerance = "0.2",
                CrossFade = true,
                MeasurePopping = true,
            };
            var preview = editing.PreviewMapLodUx2EnvJ(fresh);
            var apply = editing.ApplyApprovedMapLodUx2EnvJ();
            var finalProject = editing.Project!;
            var finalTarget = finalProject.Areas.Single(area => area.Id == target.Id);
            var finalAuthoring = finalTarget.Authoring;
            var journalTail = editing.History!.Journal.LastOrDefault();
            var applied = preview.Succeeded && preview.CanApply && apply.Succeeded &&
                          journalTail?.CommandName == "set_area_authoring" &&
                          finalAuthoring.Map is
                          {
                              FogOfWar: true,
                              FloorCount: 3,
                              ExplorationScope: AreaMapExplorationScope.Player,
                              ExplorationCellSize: 5.75f,
                          } && finalAuthoring.Map.Pins.Count == baseDraft.Pins.Count + 1 &&
                          finalAuthoring.Experience.Lod is
                          {
                              DefaultSourceMode: AreaLodSourceMode.Hybrid,
                              MaximumVisibleTriangles: 1650000,
                              MaximumHlodClusters: 64,
                              HlodClusterSize: 32,
                              PoppingTolerance: .2f,
                              CrossFade: true,
                              MeasurePopping: true,
                          };
            var mapIdentityPreserved = finalAuthoring.Map.Id == sourceAuthoring.Map.Id &&
                                       finalAuthoring.Map.SchemaVersion == sourceAuthoring.Map.SchemaVersion &&
                                       finalAuthoring.Map.Provenance.Equals(sourceAuthoring.Map.Provenance,
                                           StringComparison.Ordinal);
            var authoringTailsPreserved = finalAuthoring.SchemaVersion == sourceAuthoring.SchemaVersion &&
                                          finalAuthoring.UserEvents.SequenceEqual(sourceAuthoring.UserEvents) &&
                                          finalAuthoring.Layers.SequenceEqual(sourceAuthoring.Layers) &&
                                          finalAuthoring.RequireLayerRuntimeMetadata ==
                                          sourceAuthoring.RequireLayerRuntimeMetadata;
            var experienceTailsPreserved = finalAuthoring.Experience.Id == sourceExperience.Id &&
                                           finalAuthoring.Experience.SchemaVersion == sourceExperience.SchemaVersion &&
                                           finalAuthoring.Experience.Provenance.Equals(sourceExperience.Provenance,
                                               StringComparison.Ordinal) &&
                                           finalAuthoring.Experience.AudioSnapshots.SequenceEqual(
                                               sourceExperience.AudioSnapshots) &&
                                           ReferenceEquals(finalAuthoring.Experience.Visibility,
                                               sourceExperience.Visibility) &&
                                           finalAuthoring.Experience.RequireRuntimeMetadata ==
                                               sourceExperience.RequireRuntimeMetadata;
            var expectedExperience = new AreaExperienceAuthoringProfile(
                sourceExperience.Id, sourceExperience.SchemaVersion, sourceExperience.Provenance,
                sourceExperience.AudioSnapshots, sourceExperience.Visibility,
                finalAuthoring.Experience.Lod, sourceExperience.RequireRuntimeMetadata);
            var expectedAuthoring = new AreaAuthoringProfile(
                sourceAuthoring.SchemaVersion, sourceAuthoring.UserEvents, finalAuthoring.Map,
                sourceAuthoring.Layers, expectedExperience, sourceAuthoring.RequireLayerRuntimeMetadata);
            var expectedArea = sourceTarget.WithAuthoring(expectedAuthoring);
            var areaNeighborsPreserved = string.Equals(AreaJsonSerializer.Serialize(expectedArea),
                AreaJsonSerializer.Serialize(finalTarget), StringComparison.Ordinal);
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
            var structuredDiagnostics = invalidMap.Groups.Count == 4 && invalidPin.Groups.Count == 4 &&
                                        invalidLod.Groups.Count == 4 && invalidOverride.Groups.Count == 4 &&
                                        invalidBudget.Groups.Count == 4 && preview.Groups.Count == 4;
            var humanText = !ContainsTechnicalIdentifierUx2EnvJ(
                string.Join(' ', editing.MapLodChoicesUx2EnvJ.Select(choice => choice.Label)) + " " +
                preview.Message + " " + preview.Summary + " " +
                string.Join(' ', preview.Diff.Entries));

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvJTemporaryWorkspace(tempRoot);
            var succeeded = cleanup && explicitSelection && !invalidMap.Succeeded && !invalidPin.Succeeded &&
                            !invalidLod.Succeeded && !invalidOverride.Succeeded && !invalidBudget.Succeeded &&
                            canonicalDryRun && mapRenderPlan && revealPlan && deterministicHlod && performancePlan &&
                            noRuntimeClaim && staleRevisionBlocked && approvalFingerprintBlocked && applyUndoRedo &&
                            persistence && mapIdentityPreserved && authoringTailsPreserved &&
                            experienceTailsPreserved && areaNeighborsPreserved && projectNeighborsPreserved &&
                            structuredDiagnostics && humanText;
            return new StudioUx2EnvJSmokeResult(
                succeeded, explicitSelection, realMapSource, !invalidMap.Succeeded, !invalidPin.Succeeded,
                !invalidLod.Succeeded, !invalidOverride.Succeeded, !invalidBudget.Succeeded,
                canonicalDryRun, mapRenderPlan, revealPlan, deterministicHlod, performancePlan, noRuntimeClaim,
                staleRevisionBlocked, approvalFingerprintBlocked, applyUndoRedo, persistence,
                mapIdentityPreserved, authoringTailsPreserved, experienceTailsPreserved,
                areaNeighborsPreserved, projectNeighborsPreserved, structuredDiagnostics, humanText, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_ENV_J_CONTRACT_FAILED type={exception.GetType().Name} " +
                $"message={exception.Message}");
            return FailedUx2EnvJSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx2EnvJTemporaryWorkspace(tempRoot);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_ENV_J_CLEANUP_FAILED");
        }
    }

    private static AreaAuthoringProfile BuildAuthoringFromDraftForSmokeUx2EnvJ(
        StudioSessionController controller,
        StudioUx2EnvJDraft draft)
    {
        if (!controller.TryBuildMapLodUx2EnvJ(draft, true, out _, out _, out var authoring,
                out _, out _))
            throw new InvalidOperationException("The contract draft is invalid.");
        return authoring!;
    }

    private static bool ApplyUx2EnvJRevisionMutation(
        StudioSessionController controller,
        AreaId areaId,
        string marker)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null) return false;
        var source = area.Authoring;
        var sourceMap = source.Map;
        var map = new AreaMapProfile(
            sourceMap.Enabled,
            !sourceMap.FogOfWar,
            sourceMap.Projection,
            sourceMap.FloorCount,
            sourceMap.Pins,
            sourceMap.Id,
            sourceMap.SchemaVersion,
            sourceMap.Provenance,
            sourceMap.Generate2D,
            sourceMap.Generate3D,
            sourceMap.ExplorationScope,
            sourceMap.ExplorationCellSize,
            sourceMap.RevealApi + $".{marker}");
        var candidate = new AreaAuthoringProfile(
            source.SchemaVersion, source.UserEvents, map, source.Layers, source.Experience,
            source.RequireLayerRuntimeMetadata);
        var result = controller._setAreaAuthoringUx2EnvJ.Handle(history.State,
            new SetAreaAuthoringCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                area.Id, candidate, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    private static bool SameHlodPlansUx2EnvJ(
        AreaHlodGenerationPlan left,
        AreaHlodGenerationPlan right) =>
        left.CanGenerate == right.CanGenerate && left.DryRun == right.DryRun &&
        left.SourceMode == right.SourceMode && left.ClusterSize.Equals(right.ClusterSize) &&
        left.CrossFade == right.CrossFade && left.MeasurePopping == right.MeasurePopping &&
        left.PoppingTolerance.Equals(right.PoppingTolerance) &&
        left.Diagnostics.SequenceEqual(right.Diagnostics) && left.Clusters.Count == right.Clusters.Count &&
        left.Clusters.Zip(right.Clusters).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.CellX == pair.Second.CellX &&
            pair.First.CellZ == pair.Second.CellZ &&
            pair.First.SourceEntityIds.SequenceEqual(pair.Second.SourceEntityIds) &&
            pair.First.Operation.Equals(pair.Second.Operation, StringComparison.Ordinal) &&
            pair.First.OutputRelativePath.Equals(pair.Second.OutputRelativePath, StringComparison.Ordinal) &&
            pair.First.MaximumTriangles == pair.Second.MaximumTriangles);

    internal static bool DeleteUx2EnvJTemporaryWorkspace(string? directory)
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
                !leaf.StartsWith("openrpg-forge-studio-ux2-env-j-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx2EnvJSmokeResult FailedUx2EnvJSmoke() => new(
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false);
}
