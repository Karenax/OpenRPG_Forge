using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private static readonly Regex Ux2CTechnicalId = new(
        "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly EditTerrainBrushHandler _editTerrainBrushUx2C = new(new SystemClock());
    private readonly SetAreaWorldProfileHandler _setAreaWorldUx2C = new(new SystemClock());
    private readonly PatchAreaBiomesHandler _patchAreaBiomesUx2C = new(new SystemClock());
    private readonly EditAreaGeometryHandler _editAreaGeometryUx2C = new(new SystemClock());
    private StudioUx2CTerrainDraft? _approvedTerrainSurfaceDraftUx2C;
    private ProjectRevision? _approvedTerrainSurfaceRevisionUx2C;
    private string? _approvedTerrainSurfaceFingerprintUx2C;

    private sealed record Ux2CTerrainPlan(
        Func<bool, CommandResult> Execute,
        AreaDefinition SourceArea,
        IReadOnlyList<string> Changes);

    internal IReadOnlyList<StudioUx2CAreaChoice> TerrainSurfaceAreaChoicesUx2C
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            var duplicates = project.Areas.GroupBy(item => item.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(item => item.Key, item => item.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(area =>
                {
                    var name = HumanTerrainSurfaceTextUx2C(area.DisplayName);
                    var label = duplicates[area.DisplayName] > 1
                        ? $"{name} · {HumanTerrainSurfaceTextUx2C(area.Slug)}"
                        : name;
                    return new StudioUx2CAreaChoice(
                        area.Id,
                        $"{label} · {area.World.Features.Count} feature(s) · " +
                        $"{area.Foundation.TileKit.Placements.Count} tuile(s)",
                        area.World.Terrain.Enabled && area.World.Terrain.Provider != TerrainProviderKind.Flat,
                        area.World.Features.Count,
                        area.Foundation.TileKit.Placements.Count);
                })
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2CNamedChoice> TerrainMaterialChoicesUx2C(AreaId areaId) =>
        Project?.Areas.SingleOrDefault(item => item.Id == areaId)?.World.Terrain.MaterialLayers
            .Select(item => new StudioUx2CNamedChoice(item.Id, HumanTerrainSurfaceTextUx2C(item.Name)))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];

    internal IReadOnlyList<StudioUx2CNamedChoice> TerrainFeatureChoicesUx2C(AreaId areaId) =>
        Project?.Areas.SingleOrDefault(item => item.Id == areaId)?.World.Features
            .Select(item => new StudioUx2CNamedChoice(item.Id,
                $"{HumanTerrainSurfaceTextUx2C(item.Name)} · {HumanFeatureKindUx2C(item.Kind)}"))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];

    internal IReadOnlyList<StudioUx2CNamedChoice> TerrainBiomeChoicesUx2C(AreaId areaId) =>
        Project?.Areas.SingleOrDefault(item => item.Id == areaId)?.World.Biomes
            .Select(item => new StudioUx2CNamedChoice(item.Id, HumanTerrainSurfaceTextUx2C(item.Name)))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray() ?? [];

    internal IReadOnlyList<StudioUx2CPlacementChoice> TerrainPropertyPlacementChoicesUx2C(AreaId areaId)
    {
        var area = Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return [];
        var definitions = area.Foundation.TileKit.Definitions.ToDictionary(item => item.Id);
        return area.Foundation.TileKit.Placements.Select(item => new StudioUx2CPlacementChoice(
                item.Id,
                $"{(definitions.TryGetValue(item.DefinitionId, out var definition)
                    ? HumanTerrainSurfaceTextUx2C(definition.Key)
                    : "tuile héritée protégée")} · case {item.GridX}, {item.GridZ}"))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2CTerrainDraft? CreateTerrainSurfaceDraftUx2C(AreaId? requestedAreaId)
    {
        var history = History;
        var area = requestedAreaId is null
            ? null
            : Project?.Areas.SingleOrDefault(item => item.Id == requestedAreaId);
        if (history is null || area is null) return null;
        var material = area.World.Terrain.MaterialLayers.FirstOrDefault()?.Id ?? Guid.Empty;
        var biomeTag = area.World.Biomes.SelectMany(item => item.Tags).FirstOrDefault() ?? string.Empty;
        var biome = area.World.Biomes.FirstOrDefault();
        var scatter = biome is null ? new BiomeScatterSettings(null, null, null, null) : BiomeScatterAuthoring.Read(biome);
        return new StudioUx2CTerrainDraft(
            area.Id,
            history.State.Revision,
            TerrainSurfaceSourceFingerprintUx2C(area),
            StudioUx2CTerrainOperation.Sculpt,
            TerrainBrushOperation.Raise,
            0,
            0,
            MathF.Max(1, MathF.Min(area.Dimensions.Width, area.Dimensions.Depth) / 8f),
            .15f,
            0,
            material,
            Guid.Empty,
            false,
            string.Empty,
            TerrainFeatureKind.Road,
            [],
            3,
            .1f,
            0,
            TerrainFeatureCrossingPolicy.Allow,
            string.IsNullOrWhiteSpace(biomeTag) ? [] : [biomeTag],
            string.Empty,
            Guid.Empty,
            Guid.Empty,
            "normal",
            string.Empty,
            false,
            new Dictionary<string, string>(StringComparer.Ordinal),
            false,
            "#ffffff",
            1,
            6,
            true,
            true,
            [],
            new Dictionary<string, string>(StringComparer.Ordinal),
            biome?.Id ?? Guid.Empty,
            biome?.ScatterDensity ?? 0,
            biome?.ScatterResource ?? string.Empty,
            biome?.MinimumAltitude ?? 0,
            biome?.MaximumAltitude ?? 0,
            biome?.MinimumSlopeDegrees ?? 0,
            biome?.MaximumSlopeDegrees ?? 90,
            biome?.MinimumMoisture ?? 0,
            biome?.MaximumMoisture ?? 1,
            scatter.Scale is not null,
            scatter.Scale ?? 1,
            scatter.Tint is not null,
            scatter.Tint?.Color ?? "#ffffff",
            scatter.Tint?.Strength ?? 0,
            scatter.Focus is not null,
            scatter.Focus?.X ?? 0,
            scatter.Focus?.Z ?? 0,
            scatter.Focus?.Radius ?? MathF.Max(1, MathF.Min(area.Dimensions.Width, area.Dimensions.Depth) / 2),
            biome?.ExclusionTags ?? [],
            scatter.Seed is not null,
            scatter.Seed ?? 0);
    }

    internal StudioUx2CTerrainDraft? SelectTerrainFeatureUx2C(
        StudioUx2CTerrainDraft draft,
        Guid? featureId,
        bool createNew)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var area = Project?.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        var history = History;
        if (area is null || history is null) return null;
        if (createNew)
        {
            var halfWidth = MathF.Max(2, area.Dimensions.Width / 5f);
            var halfDepth = MathF.Max(2, area.Dimensions.Depth / 5f);
            var biomeTag = area.World.Biomes.SelectMany(item => item.Tags).FirstOrDefault() ?? string.Empty;
            return draft with
            {
                Operation = StudioUx2CTerrainOperation.Feature,
                FeatureId = StableTerrainSurfaceGuidUx2C(area.Id.Value, history.State.Revision.Value,
                    "feature:new"),
                FeatureIsNew = true,
                FeatureName = "Nouveau chemin",
                FeatureKind = TerrainFeatureKind.Road,
                FeatureControlPoints = [new(-halfWidth, 0, -halfDepth), new(halfWidth, 0, halfDepth)],
                FeatureWidth = 3,
                FeatureHeight = .1f,
                FeatureBankDegrees = 0,
                FeatureCrossingPolicy = TerrainFeatureCrossingPolicy.Allow,
                FeatureBiomeTags = string.IsNullOrWhiteSpace(biomeTag) ? [] : [biomeTag],
                FeatureMaterialResource = string.Empty,
            };
        }

        var feature = featureId is null
            ? null
            : area.World.Features.SingleOrDefault(item => item.Id == featureId.Value);
        return feature is null
            ? null
            : draft with
            {
                Operation = StudioUx2CTerrainOperation.Feature,
                FeatureId = feature.Id,
                FeatureIsNew = false,
                FeatureName = feature.Name,
                FeatureKind = feature.Kind,
                FeatureControlPoints = feature.ControlPoints,
                FeatureWidth = feature.Width,
                FeatureHeight = feature.Height,
                FeatureBankDegrees = feature.BankDegrees,
                FeatureCrossingPolicy = feature.CrossingPolicy,
                FeatureBiomeTags = feature.CompatibleBiomeTags,
                FeatureMaterialResource = feature.MaterialResource,
            };
    }

    internal StudioUx2CTerrainDraft? SelectTerrainPropertyUx2C(
        StudioUx2CTerrainDraft draft,
        Guid placementId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var area = Project?.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        var history = History;
        var placement = area?.Foundation.TileKit.Placements.SingleOrDefault(item => item.Id == placementId);
        if (area is null || history is null || placement is null) return null;
        var property = area.Foundation.TileKit.Editing?.Properties
            .SingleOrDefault(item => item.PlacementId == placement.Id);
        return draft with
        {
            Operation = StudioUx2CTerrainOperation.TileProperty,
            PlacementId = placement.Id,
            PropertyId = property?.Id ?? StableTerrainSurfaceGuidUx2C(area.Id.Value,
                history.State.Revision.Value, $"property:{placement.Id:D}"),
            SemanticState = property?.SemanticState ?? "normal",
            Animation = property?.Animation ?? string.Empty,
            LoopAnimation = property?.LoopAnimation ?? false,
            MaterialOverrides = property?.MaterialOverrides ??
                                  new Dictionary<string, string>(StringComparer.Ordinal),
            LightEnabled = property?.Light.Enabled ?? false,
            LightColor = property?.Light.Color ?? "#ffffff",
            LightIntensity = property?.Light.Intensity ?? 1,
            LightRangeMeters = property?.Light.RangeMeters ?? 6,
            CollisionEnabled = property?.CollisionEnabled ?? true,
            NavigationEnabled = property?.NavigationEnabled ?? true,
            SocketOverrides = property?.SocketOverrides ?? [],
            PropertyOverrides = property?.Overrides ??
                                new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    internal StudioUx2CTerrainDraft? SelectTerrainBiomeUx2C(
        StudioUx2CTerrainDraft draft,
        Guid biomeId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var biome = Project?.Areas.SingleOrDefault(item => item.Id == draft.AreaId)?.World.Biomes
            .SingleOrDefault(item => item.Id == biomeId);
        if (biome is null) return null;
        var scatter = BiomeScatterAuthoring.Read(biome);
        return draft with
        {
            Operation = StudioUx2CTerrainOperation.Biome,
            BiomeId = biome.Id,
            BiomeDensity = biome.ScatterDensity,
            BiomeResource = biome.ScatterResource,
            BiomeMinimumAltitude = biome.MinimumAltitude,
            BiomeMaximumAltitude = biome.MaximumAltitude,
            BiomeMinimumSlope = biome.MinimumSlopeDegrees,
            BiomeMaximumSlope = biome.MaximumSlopeDegrees,
            BiomeMinimumMoisture = biome.MinimumMoisture,
            BiomeMaximumMoisture = biome.MaximumMoisture,
            BiomeScaleEnabled = scatter.Scale is not null,
            BiomeScale = scatter.Scale ?? 1,
            BiomeTintEnabled = scatter.Tint is not null,
            BiomeTintColor = scatter.Tint?.Color ?? "#ffffff",
            BiomeTintStrength = scatter.Tint?.Strength ?? 0,
            BiomeFocusEnabled = scatter.Focus is not null,
            BiomeFocusX = scatter.Focus?.X ?? 0,
            BiomeFocusZ = scatter.Focus?.Z ?? 0,
            BiomeFocusRadius = scatter.Focus?.Radius ?? 1,
            BiomeExclusionTags = biome.ExclusionTags,
            BiomeSeedEnabled = scatter.Seed is not null,
            BiomeSeed = scatter.Seed ?? 0,
        };
    }

    internal StudioUx2CTerrainPreview PreviewTerrainSurfaceUx2C(StudioUx2CTerrainDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelTerrainSurfaceApprovalUx2C();
        if (!TryPlanTerrainSurfaceUx2C(draft, out var plan, out var message))
            return FailedTerrainSurfacePreviewUx2C(message);

        var dryRun = plan!.Execute(true);
        if (!dryRun.Succeeded)
            return FailedTerrainSurfacePreviewUx2C(HumanTerrainSurfaceDiagnosticUx2C(
                dryRun.Diagnostics.FirstOrDefault()));
        var projected = plan.Execute(false);
        if (!projected.Succeeded)
            return FailedTerrainSurfacePreviewUx2C(HumanTerrainSurfaceDiagnosticUx2C(
                projected.Diagnostics.FirstOrDefault()));

        var target = projected.State.Project.Areas.Single(item => item.Id == draft.AreaId);
        var changedSamples = plan.SourceArea.World.Terrain.Heights.Zip(target.World.Terrain.Heights)
            .Count(pair => Math.Abs(pair.First - pair.Second) > .000001f);
        var changedCells = Math.Abs(plan.SourceArea.World.Terrain.Holes.Count - target.World.Terrain.Holes.Count) +
                           Math.Abs(plan.SourceArea.World.Terrain.MaterialPaint.Count -
                                    target.World.Terrain.MaterialPaint.Count);
        _approvedTerrainSurfaceDraftUx2C = draft;
        _approvedTerrainSurfaceRevisionUx2C = History!.State.Revision;
        _approvedTerrainSurfaceFingerprintUx2C = TerrainSurfaceDraftFingerprintUx2C(draft);
        return new(
            true,
            "Aperçu sec validé · aucun document, nœud moteur, collision ou navigation n’a été modifié.",
            HumanTerrainSurfaceSummaryUx2C(draft, target),
            true,
            plan.Changes,
            [],
            changedSamples,
            changedCells,
            target.World.Features.Count,
            target.Foundation.TileKit.Editing?.Properties.Count ?? 0);
    }

    internal StudioUx2CActionResult ApplyApprovedTerrainSurfaceUx2C()
    {
        var draft = _approvedTerrainSurfaceDraftUx2C;
        var history = History;
        if (draft is null || history is null || _approvedTerrainSurfaceRevisionUx2C != history.State.Revision ||
            !string.Equals(_approvedTerrainSurfaceFingerprintUx2C,
                TerrainSurfaceDraftFingerprintUx2C(draft), StringComparison.Ordinal))
        {
            CancelTerrainSurfaceApprovalUx2C();
            return new(false, "Prévisualisez de nouveau cette opération avant de l’appliquer.");
        }
        if (!TryPlanTerrainSurfaceUx2C(draft, out var plan, out var message))
        {
            CancelTerrainSurfaceApprovalUx2C();
            return new(false, message);
        }
        var dryRun = plan!.Execute(true);
        if (!dryRun.Succeeded)
        {
            CancelTerrainSurfaceApprovalUx2C();
            return new(false, HumanTerrainSurfaceDiagnosticUx2C(dryRun.Diagnostics.FirstOrDefault()));
        }
        var applied = plan.Execute(false);
        CancelTerrainSurfaceApprovalUx2C();
        if (!applied.Succeeded)
            return new(false, HumanTerrainSurfaceDiagnosticUx2C(applied.Diagnostics.FirstOrDefault()));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Modification appliquée · Ctrl+Z annule la commande, Ctrl+Y la rétablit.");
    }

    internal void CancelTerrainSurfaceApprovalUx2C()
    {
        _approvedTerrainSurfaceDraftUx2C = null;
        _approvedTerrainSurfaceRevisionUx2C = null;
        _approvedTerrainSurfaceFingerprintUx2C = null;
    }

    internal void CloseUx2C() => CancelTerrainSurfaceApprovalUx2C();

    internal StudioUx2CApprovalSnapshot CaptureTerrainSurfaceApprovalUx2C() => new(
        _approvedTerrainSurfaceDraftUx2C,
        _approvedTerrainSurfaceRevisionUx2C,
        _approvedTerrainSurfaceFingerprintUx2C);

    internal void RestoreTerrainSurfaceApprovalUx2C(StudioUx2CApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedTerrainSurfaceDraftUx2C = snapshot.Draft;
        _approvedTerrainSurfaceRevisionUx2C = snapshot.Revision;
        _approvedTerrainSurfaceFingerprintUx2C = snapshot.Fingerprint;
    }

    private bool TryPlanTerrainSurfaceUx2C(
        StudioUx2CTerrainDraft draft,
        out Ux2CTerrainPlan? plan,
        out string message)
    {
        plan = null;
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
        var area = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (area is null)
        {
            message = "La zone choisie n’existe plus.";
            return false;
        }
        if (!string.Equals(draft.SourceFingerprint, TerrainSurfaceSourceFingerprintUx2C(area),
                StringComparison.Ordinal))
        {
            message = "Le terrain a changé. Rechargez cette fiche avant de poursuivre.";
            return false;
        }

        var state = history.State;
        var commandId = CommandId.From(StableTerrainSurfaceGuidUx2C(
            area.Id.Value,
            history.State.Revision.Value,
            TerrainSurfaceDraftFingerprintUx2C(draft)));
        try
        {
            switch (draft.Operation)
            {
                case StudioUx2CTerrainOperation.Sculpt:
                    {
                        var stroke = new TerrainBrushStroke(
                            draft.BrushOperation,
                            draft.WorldX,
                            draft.WorldZ,
                            draft.Radius,
                            draft.Strength,
                            draft.TargetHeight,
                            draft.BrushOperation == TerrainBrushOperation.PaintMaterial
                                ? draft.MaterialLayerId
                                : Guid.Empty);
                        var command = new EditTerrainBrushCommand(commandId, state.Revision, area.Id, stroke, true);
                        plan = new(
                            dry => _editTerrainBrushUx2C.Handle(state, command with { DryRun = dry }),
                            area,
                            [$"{HumanBrushOperationUx2C(draft.BrushOperation)} au point " +
                         $"{draft.WorldX:0.###}, {draft.WorldZ:0.###} sur un rayon de {draft.Radius:0.###} m."]);
                        return true;
                    }
                case StudioUx2CTerrainOperation.Feature:
                    {
                        if (draft.FeatureId == Guid.Empty || string.IsNullOrWhiteSpace(draft.FeatureName) ||
                            draft.FeatureControlPoints.Count < 2)
                        {
                            message = "Choisissez ou créez une feature avec un nom et au moins deux points.";
                            return false;
                        }
                        var feature = new TerrainFeatureDefinition(
                            draft.FeatureId,
                            TerrainFeatureDefinition.CurrentSchemaVersion,
                            draft.FeatureName,
                            draft.FeatureKind,
                            draft.FeatureControlPoints,
                            draft.FeatureWidth,
                            draft.FeatureHeight,
                            draft.FeatureBankDegrees,
                            draft.FeatureCrossingPolicy,
                            draft.FeatureBiomeTags,
                            draft.FeatureMaterialResource,
                            "studio:ux2-c:feature");
                        var features = area.World.Features.Where(item => item.Id != feature.Id)
                            .Append(feature).ToArray();
                        var world = new AreaWorldProfile(
                            area.World.Id,
                            area.World.SchemaVersion,
                            area.World.Terrain,
                            area.World.Biomes,
                            features,
                            area.World.VolumetricNavigation,
                            area.World.FlightRoutes,
                            area.World.Loading,
                            "studio:ux2-c:feature");
                        var command = new SetAreaWorldProfileCommand(commandId, state.Revision, area.Id, world, true);
                        plan = new(
                            dry => _setAreaWorldUx2C.Handle(state, command with { DryRun = dry }),
                            area,
                            [$"{(draft.FeatureIsNew ? "Créer" : "Modifier")} la feature " +
                         $"« {HumanTerrainSurfaceTextUx2C(draft.FeatureName)} » avec " +
                         $"{draft.FeatureControlPoints.Count} point(s)."]);
                        return true;
                    }
                case StudioUx2CTerrainOperation.TileProperty:
                    {
                        if (draft.PlacementId == Guid.Empty || draft.PropertyId == Guid.Empty ||
                            string.IsNullOrWhiteSpace(draft.SemanticState))
                        {
                            message = "Choisissez une tuile et renseignez son état sémantique.";
                            return false;
                        }
                        var property = new TileInstancePropertyProfile(
                            draft.PropertyId,
                            draft.PlacementId,
                            draft.SemanticState,
                            draft.Animation,
                            draft.LoopAnimation,
                            draft.MaterialOverrides,
                            new(draft.LightEnabled, draft.LightColor, draft.LightIntensity, draft.LightRangeMeters),
                            draft.CollisionEnabled,
                            draft.NavigationEnabled,
                            draft.SocketOverrides,
                            draft.PropertyOverrides);
                        var authored = AreaTileAuthoringPlanner.UpsertProperty(area, property);
                        if (!authored.CanApply)
                        {
                            message = HumanTerrainSurfaceDomainDiagnosticUx2C(authored.Diagnostics.FirstOrDefault());
                            return false;
                        }
                        var command = new EditAreaGeometryCommand(
                            commandId,
                            state.Revision,
                            area.Id,
                            area.Dimensions,
                            authored.Entities,
                            "Studio terrain · propriétés de tuile",
                            true,
                            authored.Foundation);
                        plan = new(
                            dry => _editAreaGeometryUx2C.Handle(state, command with { DryRun = dry }),
                            area,
                            [$"Mettre à jour l’état « {HumanTerrainSurfaceTextUx2C(draft.SemanticState)} », " +
                         "l’animation, les matériaux, la lumière, la collision, la navigation et les sockets."]);
                        return true;
                    }
                case StudioUx2CTerrainOperation.Biome:
                    {
                        if (draft.BiomeId == Guid.Empty)
                        {
                            message = "Choisissez un biome a modifier.";
                            return false;
                        }
                        var patch = new BiomeScatterPatch(
                            draft.BiomeId,
                            [
                                BiomeScatterPatchField.Density,
                                BiomeScatterPatchField.Resource,
                                BiomeScatterPatchField.Altitude,
                                BiomeScatterPatchField.Slope,
                                BiomeScatterPatchField.Moisture,
                                BiomeScatterPatchField.Scale,
                                BiomeScatterPatchField.Tint,
                                BiomeScatterPatchField.Focus,
                                BiomeScatterPatchField.ExclusionTags,
                                BiomeScatterPatchField.Seed,
                            ],
                            draft.BiomeDensity,
                            draft.BiomeResource,
                            new(draft.BiomeMinimumAltitude, draft.BiomeMaximumAltitude),
                            new(draft.BiomeMinimumSlope, draft.BiomeMaximumSlope),
                            new(draft.BiomeMinimumMoisture, draft.BiomeMaximumMoisture),
                            draft.BiomeScaleEnabled ? draft.BiomeScale : null,
                            draft.BiomeTintEnabled ? new(draft.BiomeTintColor, draft.BiomeTintStrength) : null,
                            draft.BiomeFocusEnabled
                                ? new(draft.BiomeFocusX, draft.BiomeFocusZ, draft.BiomeFocusRadius)
                                : null,
                            draft.BiomeExclusionTags,
                            draft.BiomeSeedEnabled ? draft.BiomeSeed : null);
                        var command = new PatchAreaBiomesCommand(commandId, state.Revision, area.Id, [patch], true);
                        plan = new(
                            dry => _patchAreaBiomesUx2C.Handle(state, command with { DryRun = dry }),
                            area,
                            ["Modifier densite, ressource, plages, echelle, teinte, focus, exclusions et graine " +
                             "du biome selectionne sans toucher au terrain, a l'eau ni aux features."]);
                        return true;
                    }
                default:
                    message = "Opération de terrain inconnue.";
                    return false;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            message = HumanTerrainSurfaceExceptionUx2C(exception.Message);
            return false;
        }
    }

    private static StudioUx2CTerrainPreview FailedTerrainSurfacePreviewUx2C(string message) => new(
        false,
        message,
        string.Empty,
        false,
        [],
        [message],
        0,
        0,
        0,
        0);

    private static string HumanTerrainSurfaceSummaryUx2C(
        StudioUx2CTerrainDraft draft,
        AreaDefinition target) => draft.Operation switch
        {
            StudioUx2CTerrainOperation.Sculpt =>
                $"Relief {HumanBrushOperationUx2C(draft.BrushOperation).ToLowerInvariant()} · " +
                $"{target.World.Terrain.SamplesX}×{target.World.Terrain.SamplesZ} échantillons · moteur non muté",
            StudioUx2CTerrainOperation.Feature =>
                $"{target.World.Features.Count} feature(s) · collection complète · rendu Godot non reconstruit avant application",
            StudioUx2CTerrainOperation.Biome =>
                $"{target.World.Biomes.Count} biome(s) - dispersion bornee - terrain, eau et features preserves",
            StudioUx2CTerrainOperation.TileProperty =>
                $"{target.Foundation.TileKit.Editing?.Properties.Count ?? 0} fiche(s) de tuile · " +
                "collision/navigation authorées, sans recalcul moteur",
            _ => "Aperçu terrain",
        };

    private static string HumanBrushOperationUx2C(TerrainBrushOperation operation) => operation switch
    {
        TerrainBrushOperation.Raise => "Élever le relief",
        TerrainBrushOperation.Lower => "Abaisser le relief",
        TerrainBrushOperation.Flatten => "Aplanir",
        TerrainBrushOperation.Smooth => "Lisser",
        TerrainBrushOperation.PaintMaterial => "Peindre le matériau",
        TerrainBrushOperation.OpenHole => "Ouvrir un trou",
        TerrainBrushOperation.CloseHole => "Refermer un trou",
        _ => "Modifier le relief",
    };

    private static string HumanFeatureKindUx2C(TerrainFeatureKind kind) => kind switch
    {
        TerrainFeatureKind.Road => "route",
        TerrainFeatureKind.River => "rivière",
        TerrainFeatureKind.Wall => "mur",
        TerrainFeatureKind.Bridge => "pont",
        TerrainFeatureKind.Cliff => "falaise",
        TerrainFeatureKind.Building => "bâtiment",
        _ => "feature",
    };

    private static string HumanTerrainSurfaceDiagnosticUx2C(CommandDiagnostic? diagnostic) => diagnostic?.Code switch
    {
        "command.revision_conflict" => "Le projet a changé. Prévisualisez de nouveau.",
        "area.terrain.area_missing" or "area.world.area_missing" or "area.biomes.area_missing" =>
            "La zone choisie n’existe plus.",
        "area.terrain.brush_invalid" => "Les paramètres de la brosse sont invalides.",
        "area.terrain.brush_no_change" => "La brosse ne touche aucun échantillon éditable.",
        "area.biomes.patch_invalid" => "Les valeurs du biome ou de sa dispersion sont invalides.",
        _ => diagnostic is null
            ? "La commande canonique a refusé cette opération."
            : "Le validateur canonique refuse cette opération de terrain.",
    };

    private static string HumanTerrainSurfaceDomainDiagnosticUx2C(
        Forge.Domain.Validation.DomainDiagnostic? diagnostic) => diagnostic?.Code switch
        {
            AreaTileEditingValidator.PropertyCode => "Les propriétés de la tuile sont incomplètes ou invalides.",
            AreaTileEditingValidator.ReferenceCode => "La tuile choisie n’existe plus.",
            _ => "Le validateur canonique refuse les propriétés de cette tuile.",
        };

    private static string HumanTerrainSurfaceExceptionUx2C(string message) =>
        message.Contains("feature", StringComparison.OrdinalIgnoreCase)
            ? "La géométrie de la feature est invalide ou sort de la zone."
            : message.Contains("brush", StringComparison.OrdinalIgnoreCase) ||
              message.Contains("terrain", StringComparison.OrdinalIgnoreCase)
                ? "Les paramètres du terrain sont invalides."
                : "Cette valeur ne respecte pas le contrat canonique.";

    private static string TerrainSurfaceSourceFingerprintUx2C(AreaDefinition area) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AreaJsonSerializer.Serialize(area))));

    private static string TerrainSurfaceDraftFingerprintUx2C(StudioUx2CTerrainDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(draft.AreaId.Value.ToByteArray());
            writer.Write(draft.BaseRevision.Value);
            WriteUx2CString(writer, draft.SourceFingerprint);
            writer.Write((int)draft.Operation);
            writer.Write((int)draft.BrushOperation);
            writer.Write(draft.WorldX);
            writer.Write(draft.WorldZ);
            writer.Write(draft.Radius);
            writer.Write(draft.Strength);
            writer.Write(draft.TargetHeight);
            writer.Write(draft.MaterialLayerId.ToByteArray());
            writer.Write(draft.FeatureId.ToByteArray());
            writer.Write(draft.FeatureIsNew);
            WriteUx2CString(writer, draft.FeatureName);
            writer.Write((int)draft.FeatureKind);
            writer.Write(draft.FeatureControlPoints.Count);
            foreach (var point in draft.FeatureControlPoints)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
                writer.Write(point.Z);
            }
            writer.Write(draft.FeatureWidth);
            writer.Write(draft.FeatureHeight);
            writer.Write(draft.FeatureBankDegrees);
            writer.Write((int)draft.FeatureCrossingPolicy);
            WriteUx2CStrings(writer, draft.FeatureBiomeTags);
            WriteUx2CString(writer, draft.FeatureMaterialResource);
            writer.Write(draft.PlacementId.ToByteArray());
            writer.Write(draft.PropertyId.ToByteArray());
            WriteUx2CString(writer, draft.SemanticState);
            WriteUx2CString(writer, draft.Animation);
            writer.Write(draft.LoopAnimation);
            WriteUx2CMap(writer, draft.MaterialOverrides);
            writer.Write(draft.LightEnabled);
            WriteUx2CString(writer, draft.LightColor);
            writer.Write(draft.LightIntensity);
            writer.Write(draft.LightRangeMeters);
            writer.Write(draft.CollisionEnabled);
            writer.Write(draft.NavigationEnabled);
            WriteUx2CStrings(writer, draft.SocketOverrides);
            WriteUx2CMap(writer, draft.PropertyOverrides);
            writer.Write(draft.BiomeId.ToByteArray());
            writer.Write(draft.BiomeDensity);
            WriteUx2CString(writer, draft.BiomeResource);
            writer.Write(draft.BiomeMinimumAltitude);
            writer.Write(draft.BiomeMaximumAltitude);
            writer.Write(draft.BiomeMinimumSlope);
            writer.Write(draft.BiomeMaximumSlope);
            writer.Write(draft.BiomeMinimumMoisture);
            writer.Write(draft.BiomeMaximumMoisture);
            writer.Write(draft.BiomeScaleEnabled);
            writer.Write(draft.BiomeScale);
            writer.Write(draft.BiomeTintEnabled);
            WriteUx2CString(writer, draft.BiomeTintColor);
            writer.Write(draft.BiomeTintStrength);
            writer.Write(draft.BiomeFocusEnabled);
            writer.Write(draft.BiomeFocusX);
            writer.Write(draft.BiomeFocusZ);
            writer.Write(draft.BiomeFocusRadius);
            WriteUx2CStrings(writer, draft.BiomeExclusionTags);
            writer.Write(draft.BiomeSeedEnabled);
            writer.Write(draft.BiomeSeed);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx2CString(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteUx2CStrings(BinaryWriter writer, IEnumerable<string> values)
    {
        var items = values.ToArray();
        writer.Write(items.Length);
        foreach (var item in items) WriteUx2CString(writer, item);
    }

    private static void WriteUx2CMap(BinaryWriter writer, IReadOnlyDictionary<string, string> values)
    {
        writer.Write(values.Count);
        foreach (var item in values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            WriteUx2CString(writer, item.Key);
            WriteUx2CString(writer, item.Value);
        }
    }

    private static Guid StableTerrainSurfaceGuidUx2C(Guid areaId, long revision, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{areaId:D}:{revision}:{discriminator}"));
        var value = bytes[..16];
        value[7] = (byte)((value[7] & 0x0f) | 0x50);
        value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }

    private static string HumanTerrainSurfaceTextUx2C(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "sans nom" : value.Trim();
        return Ux2CTechnicalId.IsMatch(text) ? "élément hérité protégé" : text;
    }

    internal ProjectDefinition? CreateTerrainSurfaceEvidenceProjectUx2C()
    {
        var source = CreateTerrainEvidenceProjectUx2B();
        if (source is null) return null;
        var tileArea = source.Areas.Single(item => item.Slug == "atelier-tuiles");
        var world = AreaWorldProfile.CreateDefault(tileArea.Id, AreaKind.Exterior, tileArea.Dimensions);
        var tag = world.Biomes.SelectMany(item => item.Tags).FirstOrDefault() ?? "meadow";
        var features = new[]
        {
            new TerrainFeatureDefinition(
                StableTerrainSurfaceGuidUx2C(tileArea.Id.Value, 0, "feature:road"),
                1,
                "Chemin de mousse",
                TerrainFeatureKind.Road,
                [new(-6, 0, -4), new(6, 0, -4)],
                2.5f,
                .1f,
                0,
                TerrainFeatureCrossingPolicy.Allow,
                [tag],
                string.Empty,
                "studio:ux2-c:evidence"),
            new TerrainFeatureDefinition(
                StableTerrainSurfaceGuidUx2C(tileArea.Id.Value, 0, "feature:river"),
                1,
                "Ruisseau calme",
                TerrainFeatureKind.River,
                [new(-6, 0, 4), new(6, 0, 4)],
                2,
                .2f,
                0,
                TerrainFeatureCrossingPolicy.Allow,
                [tag],
                string.Empty,
                "studio:ux2-c:evidence"),
        };
        var evidenceWorld = new AreaWorldProfile(
            world.Id,
            world.SchemaVersion,
            world.Terrain,
            world.Biomes,
            features,
            world.VolumetricNavigation,
            world.FlightRoutes,
            world.Loading,
            "studio:ux2-c:evidence");
        var evidenceArea = new AreaDefinition(
            tileArea.Id,
            "atelier-surface",
            "Atelier relief et features UX2",
            tileArea.Entities,
            AreaKind.Exterior,
            tileArea.Dimensions,
            tileArea.Portals,
            tileArea.SchemaVersion,
            tileArea.Navigation,
            tileArea.Environment,
            tileArea.Authoring,
            evidenceWorld,
            tileArea.RuntimePolicies,
            tileArea.Foundation,
            tileArea.Playtest);
        return new ProjectDefinition(
            source.Id,
            source.Title,
            source.Module,
            source.Blueprints,
            source.Areas.Select(item => item.Id == tileArea.Id ? evidenceArea : item).ToArray(),
            source.Dialogues,
            source.Quests);
    }

    internal StudioUx2CSmokeResult RunTerrainSurfaceContractSmokeUx2C()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux2c-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateTerrainSurfaceEvidenceProjectUx2C();
            if (fixture is null || !ProjectSession.Create(tempRoot, fixture).Succeeded ||
                !editing.Open(tempRoot).Succeeded)
                return FailedTerrainSurfaceSmokeUx2C();
            var area = editing.Project!.Areas.Single(item => item.Slug == "atelier-surface");
            var neighbour = editing.Project.Areas.First(item => item.Id != area.Id);
            var neighbourJson = AreaJsonSerializer.Serialize(neighbour);
            var explicitSelection = editing.CreateTerrainSurfaceDraftUx2C(null) is null;
            var baseDraft = editing.CreateTerrainSurfaceDraftUx2C(area.Id)!;
            var initialRevision = editing.History!.State.Revision;

            var brushDraft = baseDraft with
            {
                Operation = StudioUx2CTerrainOperation.Sculpt,
                BrushOperation = TerrainBrushOperation.Raise,
                Radius = 5.125f,
                Strength = .1375f,
            };
            var brushPreview = editing.PreviewTerrainSurfaceUx2C(brushDraft);
            var dryRun = brushPreview.Succeeded && editing.History.State.Revision == initialRevision;
            var brushApply = editing.ApplyApprovedTerrainSurfaceUx2C();
            var sculpt = brushApply.Succeeded && editing.Project.Areas.Single(item => item.Id == area.Id)
                .World.Terrain.HeightAt(0, 0) > area.World.Terrain.HeightAt(0, 0);

            var afterBrush = editing.CreateTerrainSurfaceDraftUx2C(area.Id)!;
            var newFeature = editing.SelectTerrainFeatureUx2C(afterBrush, null, createNew: true)! with
            {
                FeatureName = "Sentier de preuve",
                FeatureControlPoints = [new(-5.25f, 0, 0), new(0, .125f, 3.5f), new(5.25f, 0, 1)],
                FeatureWidth = 2.125f,
            };
            var featurePreview = editing.PreviewTerrainSurfaceUx2C(newFeature);
            var featureApply = editing.ApplyApprovedTerrainSurfaceUx2C();
            var feature = featurePreview.Succeeded && featureApply.Succeeded &&
                          editing.Project.Areas.Single(item => item.Id == area.Id).World.Features
                              .Any(item => item.Id == newFeature.FeatureId && item.ControlPoints.Count == 3);

            var afterFeature = editing.CreateTerrainSurfaceDraftUx2C(area.Id)!;
            var placementId = editing.Project.Areas.Single(item => item.Id == area.Id)
                .Foundation.TileKit.Placements[0].Id;
            var propertyDraft = editing.SelectTerrainPropertyUx2C(afterFeature, placementId)! with
            {
                SemanticState = "mousse-humide",
                Animation = "idle_glow",
                LoopAnimation = true,
                MaterialOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["floor"] = "wet-moss",
                },
                LightEnabled = true,
                LightColor = "#9fd8a0",
                LightIntensity = .8125f,
                LightRangeMeters = 6.125f,
                CollisionEnabled = true,
                NavigationEnabled = true,
                SocketOverrides = ["passage-ouvert"],
                PropertyOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["audio.step"] = "moss",
                },
            };
            var propertyPreview = editing.PreviewTerrainSurfaceUx2C(propertyDraft);
            var propertyApply = editing.ApplyApprovedTerrainSurfaceUx2C();
            var tileProperty = propertyPreview.Succeeded && propertyApply.Succeeded &&
                               editing.Project.Areas.Single(item => item.Id == area.Id).Foundation.TileKit.Editing!
                                   .Properties.Any(item => item.PlacementId == placementId &&
                                                           item.SemanticState == "mousse-humide");

            var beforeBiomeArea = editing.Project.Areas.Single(item => item.Id == area.Id);
            var biomeId = beforeBiomeArea.World.Biomes[0].Id;
            var biomeDraft = editing.SelectTerrainBiomeUx2C(
                editing.CreateTerrainSurfaceDraftUx2C(area.Id)!, biomeId)! with
            {
                BiomeDensity = .73125f,
                BiomeResource = "assets/nature/studio-proof.glb",
                BiomeScaleEnabled = true,
                BiomeScale = 1.125f,
                BiomeTintEnabled = true,
                BiomeTintColor = "#79bd39",
                BiomeTintStrength = .25f,
                BiomeFocusEnabled = true,
                BiomeFocusX = 1.25f,
                BiomeFocusZ = -2.5f,
                BiomeFocusRadius = 12.5f,
                BiomeExclusionTags = ["road", "water"],
                BiomeSeedEnabled = true,
                BiomeSeed = 9401,
            };
            var biomePreview = editing.PreviewTerrainSurfaceUx2C(biomeDraft);
            var biomeApply = editing.ApplyApprovedTerrainSurfaceUx2C();
            var afterBiomeArea = editing.Project.Areas.Single(item => item.Id == area.Id);
            var afterBiome = afterBiomeArea.World.Biomes.Single(item => item.Id == biomeId);
            var expectedBiomeWorld = new AreaWorldProfile(beforeBiomeArea.World.Id,
                beforeBiomeArea.World.SchemaVersion, beforeBiomeArea.World.Terrain, afterBiomeArea.World.Biomes,
                beforeBiomeArea.World.Features, beforeBiomeArea.World.VolumetricNavigation,
                beforeBiomeArea.World.FlightRoutes, beforeBiomeArea.World.Loading, beforeBiomeArea.World.Provenance);
            var biome = biomePreview.Succeeded && biomeApply.Succeeded &&
                        Math.Abs(afterBiome.ScatterDensity - .73125f) < .000001f &&
                        BiomeScatterAuthoring.Read(afterBiome).Seed == 9401 &&
                        string.Equals(AreaJsonSerializer.Serialize(beforeBiomeArea.WithWorld(expectedBiomeWorld)),
                            AreaJsonSerializer.Serialize(afterBiomeArea), StringComparison.Ordinal);

            var invalidBrush = editing.PreviewTerrainSurfaceUx2C(
                editing.CreateTerrainSurfaceDraftUx2C(area.Id)! with { Radius = 0 });
            var invalidFeature = editing.SelectTerrainFeatureUx2C(
                editing.CreateTerrainSurfaceDraftUx2C(area.Id)!, null, createNew: true)! with
            {
                FeatureControlPoints = [new(0, 0, 0)],
            };
            var invalidProperty = editing.SelectTerrainPropertyUx2C(
                editing.CreateTerrainSurfaceDraftUx2C(area.Id)!, placementId)! with
            {
                SemanticState = string.Empty,
            };
            var invalidBiome = editing.SelectTerrainBiomeUx2C(
                editing.CreateTerrainSurfaceDraftUx2C(area.Id)!, biomeId)! with
            {
                BiomeMinimumMoisture = .9f,
                BiomeMaximumMoisture = .1f,
            };
            var invalidValuesBlocked = !invalidBrush.Succeeded &&
                                       !editing.PreviewTerrainSurfaceUx2C(invalidFeature).Succeeded &&
                                       !editing.PreviewTerrainSurfaceUx2C(invalidProperty).Succeeded &&
                                       !editing.PreviewTerrainSurfaceUx2C(invalidBiome).Succeeded;

            var staleDraft = editing.CreateTerrainSurfaceDraftUx2C(area.Id)!;
            var staleMutation = ApplyTerrainSurfaceRevisionMutationUx2C(editing, area.Id, "draft");
            var staleDraftBlocked = staleMutation &&
                                    !editing.PreviewTerrainSurfaceUx2C(staleDraft).Succeeded &&
                                    editing.Undo().Succeeded;

            var approvalDraft = editing.SelectTerrainFeatureUx2C(
                editing.CreateTerrainSurfaceDraftUx2C(area.Id)!,
                editing.Project.Areas.Single(item => item.Id == area.Id).World.Features[0].Id,
                createNew: false)! with
            { FeatureWidth = 3.375f };
            var approvalPreview = editing.PreviewTerrainSurfaceUx2C(approvalDraft);
            var approvalMutation = ApplyTerrainSurfaceRevisionMutationUx2C(editing, area.Id, "approval");
            var staleApprovalBlocked = approvalPreview.Succeeded && approvalMutation &&
                                       !editing.ApplyApprovedTerrainSurfaceUx2C().Succeeded &&
                                       editing.Undo().Succeeded;

            var finalDraft = editing.SelectTerrainPropertyUx2C(
                editing.CreateTerrainSurfaceDraftUx2C(area.Id)!, placementId)! with
            {
                SemanticState = "mousse-approuvée",
                CollisionEnabled = false,
                NavigationEnabled = true,
            };
            var finalPreview = editing.PreviewTerrainSurfaceUx2C(finalDraft);
            var finalApply = editing.ApplyApprovedTerrainSurfaceUx2C();
            var canonicalCommands = finalPreview.Succeeded && finalApply.Succeeded &&
                                    editing.History.Journal.LastOrDefault()?.CommandName == "edit_area_geometry";
            var appliedProject = editing.Project!;
            var appliedJson = ProjectJsonSerializer.Serialize(appliedProject);
            var undo = editing.Undo();
            var redo = editing.Redo();
            var undoRedo = undo.Succeeded && redo.Succeeded &&
                           string.Equals(appliedJson, ProjectJsonSerializer.Serialize(editing.Project!),
                               StringComparison.Ordinal);
            var saved = editing.Save();
            editing.Close();
            var opened = reopened.Open(tempRoot, area.Id.ToString());
            var persistence = saved.Succeeded && opened.Succeeded && reopened.Project is not null &&
                              string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopened.Project),
                                  StringComparison.Ordinal);
            var neighboursPreserved = opened.Succeeded && reopened.Project is not null &&
                                      string.Equals(neighbourJson,
                                          AreaJsonSerializer.Serialize(reopened.Project.Areas.Single(item =>
                                              item.Id == neighbour.Id)), StringComparison.Ordinal);
            var restoredTarget = reopened.Project?.Areas.Single(item => item.Id == area.Id);
            var fullCollections = restoredTarget is not null && restoredTarget.World.Features.Count >= 3 &&
                                  restoredTarget.Foundation.TileKit.Editing?.Properties.Count >= 1 &&
                                  restoredTarget.World.Biomes.Count == area.World.Biomes.Count;
            var humanText = !Ux2CTechnicalId.IsMatch(
                brushPreview.Message + featurePreview.Summary + propertyPreview.Summary + biomePreview.Summary +
                string.Join(' ', editing.TerrainSurfaceAreaChoicesUx2C.Select(item => item.Label)));
            var succeeded = explicitSelection && sculpt && feature && tileProperty && biome && fullCollections && dryRun &&
                            invalidValuesBlocked && staleDraftBlocked && staleApprovalBlocked && canonicalCommands &&
                            undoRedo && persistence && neighboursPreserved && humanText;
            return new(
                succeeded,
                explicitSelection,
                true,
                sculpt,
                feature,
                tileProperty,
                fullCollections,
                dryRun,
                invalidValuesBlocked,
                staleDraftBlocked,
                staleApprovalBlocked,
                canonicalCommands,
                undoRedo,
                persistence,
                neighboursPreserved,
                humanText)
            {
                Biome = biome,
            };
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2C_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedTerrainSurfaceSmokeUx2C();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (!DeleteTerrainSurfaceTemporaryWorkspaceUx2C(tempRoot))
                Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2C_CLEANUP_FAILED");
        }
    }

    private static bool ApplyTerrainSurfaceRevisionMutationUx2C(
        StudioSessionController controller,
        AreaId areaId,
        string marker)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null) return false;
        var world = new AreaWorldProfile(
            area.World.Id,
            area.World.SchemaVersion,
            area.World.Terrain,
            area.World.Biomes,
            area.World.Features,
            area.World.VolumetricNavigation,
            area.World.FlightRoutes,
            area.World.Loading,
            $"studio:ux2-c:stale:{marker}");
        var handler = new SetAreaWorldProfileHandler(new SystemClock());
        var result = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, area.Id, world, false));
        if (!result.Succeeded) return false;
        history.Record(result);
        return true;
    }

    internal static bool DeleteTerrainSurfaceTemporaryWorkspaceUx2C(string? directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory)) return true;
            var full = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2c-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private static StudioUx2CSmokeResult FailedTerrainSurfaceSmokeUx2C() => new(
        false, false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, false);
}
