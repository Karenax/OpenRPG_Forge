using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldGeneration;

namespace Forge.Application.WorldGeneration;

public sealed record WorldGenerationProductionVisualEvidence(
    int SchemaVersion,
    Guid SourceAreaId,
    int TexturedTerrainLayerCount,
    int PaintedTerrainCellCount,
    int PaintedTerrainLayerCount,
    float TerrainSampleSpacingMeters,
    int ScatterResourceCount,
    IReadOnlyList<string> VegetationStrata,
    int ProductionFeatureCount,
    int TexturedRoadCount,
    bool EnvironmentInherited,
    WorldGenerationProductionSpatialQualityEvidence SpatialQuality,
    string Fingerprint)
{
    public const int CurrentSchemaVersion = 4;
}

public sealed record WorldGenerationProductionSpatialQualityEvidence(
    int CameraSpawnCount,
    float MaximumCameraReliefMeters,
    int GroundedFoundationCount,
    float MaximumFoundationOffsetMeters);

public sealed record WorldGenerationProductionArtDirectionResult(
    TerrainDefinition? Terrain,
    IReadOnlyList<BiomeLayerDefinition> Biomes,
    IReadOnlyList<TerrainFeatureDefinition> Features,
    AreaEnvironmentProfile? Environment,
    WorldGenerationProductionVisualEvidence? Evidence,
    IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Terrain is not null && Evidence is not null && Diagnostics.Count == 0;
}

public static class WorldGenerationProductionArtDirection
{
    public const string ProductionTierRequiredCode = "world_generation.production_visuals.tier_required";
    public const string SourceRequiredCode = "world_generation.production_visuals.source_required";
    public const string SourceMissingCode = "world_generation.production_visuals.source_missing";
    public const string SourceTerrainInvalidCode = "world_generation.production_visuals.terrain_invalid";
    public const string SourceVegetationInvalidCode = "world_generation.production_visuals.vegetation_invalid";
    public const string SourceRoadInvalidCode = "world_generation.production_visuals.road_invalid";
    public const string TerrainResolutionInvalidCode = "world_generation.production_visuals.terrain_resolution_invalid";
    public const string CameraSpawnUnsafeCode = "world_generation.production_visuals.camera_spawn_unsafe";
    public const string FoundationFloatingCode = "world_generation.production_visuals.foundation_floating";
    public const string PatrolLocomotionMismatchCode = "world_generation.production_visuals.patrol_locomotion_mismatch";
    public const string VegetationLocalityInvalidCode = "world_generation.production_visuals.vegetation_locality_invalid";

    public const float CameraInspectionDiameterMeters = 64f;
    public const float MaximumCameraReliefMeters = 12f;
    public const float MinimumPlayerGroundOffsetMeters = -.25f;
    public const float MaximumPlayerGroundOffsetMeters = 3.5f;
    public const float MaximumFoundationOffsetMeters = 1.25f;
    public const float MaximumAutomaticStructureAdjustmentMeters = 2f;
    public const float BuildingVegetationEnvelopePaddingMeters = 1.25f;
    public const float BuildingTallVegetationClearanceMeters = 4f;

    public static float MaximumTerrainSampleSpacingMeters(int width, int depth) =>
        Math.Max(4f, Math.Max(width, depth) / 256f);

    private static readonly string[][] StratumTags =
    [
        ["tree", "canopy", "woodland", "forest"],
        ["shrub", "bush", "understory"],
        ["ground-cover", "grass", "flower", "meadow", "fern"],
    ];

    private static readonly string[] StratumNames = ["canopy", "shrub", "ground-cover"];

    public static WorldGenerationProductionArtDirectionResult Materialize(
        ProjectDefinition project,
        AreaDefinition target,
        WorldGenerationRecipe recipe,
        WorldGenerationTerrainSurfaceEvidence surface,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        var diagnostics = ValidateSource(project, target, recipe).ToList();
        diagnostics.AddRange(ValidatePatrolLocomotion(target));
        var structureAdaptation = recipe.Terrain.AdaptToStructures
            ? AdaptSurfaceToGeneratedStructures(target, surface)
            : new StructureSurfaceAdaptation(surface, 0, 0, 0);
        var productionSurface = structureAdaptation.Surface;
        var spatialQuality = InspectSpatialQuality(target, productionSurface);
        diagnostics.AddRange(spatialQuality.Diagnostics.Select(diagnostic =>
            diagnostic.StartsWith(FoundationFloatingCode, StringComparison.Ordinal)
                ? $"{diagnostic} Structure adaptation: requested={recipe.Terrain.AdaptToStructures}, groups={structureAdaptation.GroupCount}, eligible={structureAdaptation.EligibleGroupCount}, changedSamples={structureAdaptation.ChangedSampleCount}."
                : diagnostic));
        if (diagnostics.Count > 0)
            return new(null, [], [], null, null, diagnostics);

        var source = project.Areas.Single(area =>
            area.Id.Value == recipe.ArtDirection.SourceAreaId!.Value);
        var materialMap = source.World.Terrain.MaterialLayers
            .Select((layer, index) => new
            {
                layer.Id,
                NewId = StableId(target.Id.Value, $"production-material:{index}"),
            })
            .ToDictionary(item => item.Id, item => item.NewId);
        var materials = source.World.Terrain.MaterialLayers
            .Select((layer, index) => new TerrainMaterialLayer(
                materialMap[layer.Id],
                layer.Name,
                layer.AlbedoColor,
                layer.MaterialResource,
                layer.UvScale))
            .ToArray();
        var maximumChunkSize = Math.Min(productionSurface.SamplesX, productionSurface.SamplesZ) - 1;
        var chunkSize = Math.Clamp(source.World.Terrain.ChunkSize, 2, Math.Max(2, maximumChunkSize));
        var materialPaint = MaterializeTerrainPaint(source.World, productionSurface, materialMap, recipe.Seed);
        var terrain = new TerrainDefinition(
            StableId(target.Id.Value, "production-terrain"),
            TerrainDefinition.CurrentSchemaVersion,
            TerrainProviderKind.Heightmap,
            productionSurface.Width,
            productionSurface.Depth,
            productionSurface.SamplesX,
            productionSurface.SamplesZ,
            chunkSize,
            recipe.Seed,
            productionSurface.HeightScale,
            productionSurface.Heights,
            materials,
            [],
            string.Empty,
            string.Empty,
            string.Empty,
            provenance,
            materialPaint);
        var foundations = target.Entities.Where(IsGeneratedFoundation).ToArray();
        var vegetationAnchors = SettlementAnchors(target, foundations, includeFallback: false);
        var vegetationFocus = vegetationAnchors.Count > 0
            ? TargetVegetationFocus.Create(vegetationAnchors, productionSurface)
            : null;
        var biomes = source.World.Biomes
            .Where(biome => !string.IsNullOrWhiteSpace(biome.ScatterResource) &&
                            materialMap.ContainsKey(biome.MaterialLayerId))
            .Select((biome, index) => new BiomeLayerDefinition(
                StableId(target.Id.Value, $"production-biome:{index}"),
                biome.Name,
                materialMap[biome.MaterialLayerId],
                biome.MinimumAltitude,
                biome.MaximumAltitude,
                biome.MinimumSlopeDegrees,
                biome.MaximumSlopeDegrees,
                biome.MinimumMoisture,
                biome.MaximumMoisture,
                biome.ScatterDensity,
                biome.ScatterResource,
                MaterializeBiomeTags(biome, vegetationFocus),
                biome.ExclusionTags,
                biome.Priority,
                provenance))
            .ToArray();
        var strata = ClassifyStrata(biomes);
        if (foundations.Length > 0 && biomes.Any(IsGroundCover) &&
            !biomes.Where(IsGroundCover).All(HasFocusRegion))
        {
            return new(null, [], [], null, null,
                [$"{VegetationLocalityInvalidCode}: production settlements require every ground-cover layer to be localized around the authored play area."]);
        }
        var features = MaterializeFeatures(source, target, recipe, productionSurface, provenance);
        var texturedRoadCount = features.Count(feature =>
            feature.Kind == TerrainFeatureKind.Road &&
            !string.IsNullOrWhiteSpace(feature.MaterialResource) &&
            !feature.MaterialResource.StartsWith("builtin/", StringComparison.OrdinalIgnoreCase));
        if (recipe.Roads.Count > 0 && texturedRoadCount < recipe.Roads.Count)
        {
            return new(null, [], [], null, null,
                [$"{SourceRoadInvalidCode}: production output requires one terrain-snapped textured road per requested road; requested {recipe.Roads.Count}, materialized {texturedRoadCount}."]);
        }
        var fingerprint = Fingerprint(source, productionSurface, materials, materialPaint, biomes, features, strata,
            recipe.ArtDirection.InheritEnvironment, spatialQuality.Evidence);
        var evidence = new WorldGenerationProductionVisualEvidence(
            WorldGenerationProductionVisualEvidence.CurrentSchemaVersion,
            source.Id.Value,
            materials.Count(layer => !string.IsNullOrWhiteSpace(layer.MaterialResource)),
            materialPaint.Count,
            materialPaint.Select(cell => cell.MaterialLayerId).Distinct().Count(),
            Math.Max(
                productionSurface.Width / (float)(productionSurface.SamplesX - 1),
                productionSurface.Depth / (float)(productionSurface.SamplesZ - 1)),
            biomes.Length,
            strata,
            features.Count,
            texturedRoadCount,
            recipe.ArtDirection.InheritEnvironment,
            spatialQuality.Evidence,
            fingerprint);
        var environment = recipe.ArtDirection.InheritEnvironment
            ? source.Environment.CloneForArea(target.Id)
            : target.Environment;
        return new(terrain, biomes, features, environment, evidence, []);
    }

    public static IReadOnlyList<string> ValidateSource(
        ProjectDefinition project,
        AreaDefinition target,
        WorldGenerationRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(recipe);
        var diagnostics = new List<string>();
        if (recipe.ArtDirection.VisualTier != WorldGenerationVisualTier.Production)
            diagnostics.Add($"{ProductionTierRequiredCode}: canonical promotion only accepts production output; technical proxies are preview-only.");
        if (recipe.ArtDirection.SourceAreaId is not { } sourceAreaId)
        {
            diagnostics.Add($"{SourceRequiredCode}: production output requires an explicit art-direction source area.");
            return diagnostics;
        }
        var source = project.Areas.SingleOrDefault(area => area.Id.Value == sourceAreaId);
        if (source is null || source.Id == target.Id || source.Kind != AreaKind.Exterior)
        {
            diagnostics.Add($"{SourceMissingCode}: art-direction source '{sourceAreaId:D}' must resolve to another exterior area.");
            return diagnostics;
        }

        var terrain = source.World.Terrain;
        if (!terrain.Enabled ||
            terrain.MaterialLayers.Count(layer => !string.IsNullOrWhiteSpace(layer.MaterialResource)) == 0)
            diagnostics.Add($"{SourceTerrainInvalidCode}: production art direction requires enabled terrain with at least one real material resource.");
        var realBiomes = source.World.Biomes
            .Where(biome => !string.IsNullOrWhiteSpace(biome.ScatterResource))
            .ToArray();
        var strata = ClassifyStrata(realBiomes);
        if (realBiomes.Length == 0 || strata.Count < recipe.ArtDirection.MinimumVegetationStrata)
            diagnostics.Add($"{SourceVegetationInvalidCode}: production art direction requires real scatter resources across at least {recipe.ArtDirection.MinimumVegetationStrata} vegetation strata; found {strata.Count}.");
        if (recipe.Roads.Count > 0 && !source.World.Features.Any(feature =>
                feature.Kind == TerrainFeatureKind.Road &&
                !string.IsNullOrWhiteSpace(feature.MaterialResource) &&
                !feature.MaterialResource.StartsWith("builtin/", StringComparison.OrdinalIgnoreCase)))
            diagnostics.Add($"{SourceRoadInvalidCode}: a production recipe requesting roads requires a textured road in its art-direction source.");
        return diagnostics;
    }

    public static IReadOnlyList<string> ValidateSpatialQuality(
        AreaDefinition target,
        WorldGenerationTerrainSurfaceEvidence surface,
        bool adaptToStructures = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(surface);
        var inspectedSurface = adaptToStructures
            ? AdaptSurfaceToGeneratedStructures(target, surface).Surface
            : surface;
        return InspectSpatialQuality(target, inspectedSurface).Diagnostics;
    }

    public static IReadOnlyList<string> ValidatePatrolLocomotion(AreaDefinition target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var diagnostics = new List<string>();
        foreach (var entity in target.Entities)
        {
            if (!Guid.TryParse(GetOverride(entity, "runtime.patrolRoute"), out var routeId)) continue;
            var route = target.Navigation.Waypoints.PatrolRoutes.FirstOrDefault(item => item.Id == routeId);
            if (route is null) continue;

            var animationProfile = GetOverride(entity, "runtime.animationProfile");
            var runClip = GetOverride(entity, "runtime.animationRun");
            var hasAnimatedRun = !string.IsNullOrWhiteSpace(runClip) || animationProfile is
                "humanoid-basic" or "kaykit-adventurer" or "authored-clips";
            if (!hasAnimatedRun || !string.IsNullOrWhiteSpace(GetOverride(entity, "runtime.animationWalk")))
                continue;

            var walkingPoint = route.Points
                .Where(point => PatrolLocomotionPolicy.IsWalkingPace(point.Speed))
                .OrderBy(point => point.Speed)
                .FirstOrDefault();
            if (walkingPoint is null) continue;
            diagnostics.Add(
                $"{PatrolLocomotionMismatchCode}: entity '{entity.Id}' uses a run-only animation profile on route '{route.Id:D}' at {walkingPoint.Speed.ToString("0.##", CultureInfo.InvariantCulture)} m/s; author a walk clip or use at least {PatrolLocomotionPolicy.MinimumRunSpeedMetersPerSecond.ToString("0.##", CultureInfo.InvariantCulture)} m/s.");
        }
        return diagnostics;
    }

    private static IReadOnlyList<string> ClassifyStrata(IEnumerable<BiomeLayerDefinition> biomes)
    {
        var result = new List<string>();
        var values = biomes.ToArray();
        for (var index = 0; index < StratumTags.Length; index++)
        {
            if (values.Any(biome => StratumTags[index].Any(tag =>
                    biome.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase) ||
                    biome.Name.Contains(tag, StringComparison.OrdinalIgnoreCase))))
                result.Add(StratumNames[index]);
        }
        return result;
    }

    private static IReadOnlyList<string> MaterializeBiomeTags(
        BiomeLayerDefinition biome,
        TargetVegetationFocus? focus)
    {
        var tags = biome.Tags
            .Where(tag => !tag.StartsWith("focus-center:", StringComparison.OrdinalIgnoreCase) &&
                          !tag.StartsWith("focus-radius:", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (focus is null || (!IsGroundCover(biome) && !IsShrubLayer(biome)))
            return tags.ToArray();

        var radius = IsGroundCover(biome)
            ? focus.Radius
            : Math.Min(focus.Radius * 1.35f, 240f);
        tags.Add($"focus-center:{focus.CenterX.ToString("R", CultureInfo.InvariantCulture)},{focus.CenterZ.ToString("R", CultureInfo.InvariantCulture)}");
        tags.Add($"focus-radius:{radius.ToString("R", CultureInfo.InvariantCulture)}");
        return tags.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsGroundCover(BiomeLayerDefinition biome) => StratumTags[2].Any(tag =>
        biome.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase) ||
        biome.Name.Contains(tag, StringComparison.OrdinalIgnoreCase));

    private static bool IsShrubLayer(BiomeLayerDefinition biome) => StratumTags[1].Any(tag =>
        biome.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase) ||
        biome.Name.Contains(tag, StringComparison.OrdinalIgnoreCase));

    private static bool HasFocusRegion(BiomeLayerDefinition biome) =>
        biome.Tags.Any(tag => tag.StartsWith("focus-center:", StringComparison.OrdinalIgnoreCase)) &&
        biome.Tags.Any(tag => tag.StartsWith("focus-radius:", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<TerrainMaterialPaintCell> MaterializeTerrainPaint(
        AreaWorldProfile sourceWorld,
        WorldGenerationTerrainSurfaceEvidence surface,
        IReadOnlyDictionary<Guid, Guid> materialMap,
        int seed)
    {
        var source = sourceWorld.Terrain;
        var sourcePaint = source.MaterialPaint.ToDictionary(
            cell => (cell.X, cell.Z),
            cell => cell.MaterialLayerId);
        var sourceCellsX = source.SamplesX - 1;
        var sourceCellsZ = source.SamplesZ - 1;
        var targetCellsX = surface.SamplesX - 1;
        var targetCellsZ = surface.SamplesZ - 1;
        var groundMaterial = sourceWorld.Biomes.FirstOrDefault(IsGroundCover)?.MaterialLayerId ??
                             source.MaterialLayers.FirstOrDefault(layer =>
                                 !string.IsNullOrWhiteSpace(layer.MaterialResource))?.Id ??
                             source.MaterialLayers[0].Id;
        var rockMaterial = sourceWorld.Biomes.FirstOrDefault(biome =>
            biome.Tags.Contains("rock", StringComparer.OrdinalIgnoreCase) ||
            biome.Tags.Contains("cliff", StringComparer.OrdinalIgnoreCase))?.MaterialLayerId;
        var soilMaterial = source.MaterialLayers.FirstOrDefault(layer =>
            new[] { "soil", "dirt", "earth", "terre", "path", "cobble" }.Any(token =>
                layer.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                layer.MaterialResource.Contains(token, StringComparison.OrdinalIgnoreCase)))?.Id;
        var result = new List<TerrainMaterialPaintCell>(targetCellsX * targetCellsZ);
        for (var z = 0; z < targetCellsZ; z++)
        {
            for (var x = 0; x < targetCellsX; x++)
            {
                var sourceX = Math.Clamp(
                    (int)MathF.Floor((x + .5f) / targetCellsX * sourceCellsX),
                    0,
                    sourceCellsX - 1);
                var sourceZ = Math.Clamp(
                    (int)MathF.Floor((z + .5f) / targetCellsZ * sourceCellsZ),
                    0,
                    sourceCellsZ - 1);
                var sourceMaterial = sourcePaint.GetValueOrDefault((sourceX, sourceZ));
                if (sourceMaterial == Guid.Empty || !materialMap.ContainsKey(sourceMaterial))
                {
                    var worldX = -surface.Width * .5f + (x + .5f) * surface.Width / targetCellsX;
                    var worldZ = -surface.Depth * .5f + (z + .5f) * surface.Depth / targetCellsZ;
                    var step = Math.Max(1f, Math.Min(
                        surface.Width / (float)targetCellsX,
                        surface.Depth / (float)targetCellsZ));
                    var deltaX = HeightAt(surface, worldX + step, worldZ) -
                                 HeightAt(surface, worldX - step, worldZ);
                    var deltaZ = HeightAt(surface, worldX, worldZ + step) -
                                 HeightAt(surface, worldX, worldZ - step);
                    var slope = MathF.Atan(MathF.Sqrt(deltaX * deltaX + deltaZ * deltaZ) /
                                           (step * 2f)) * 180f / MathF.PI;
                    sourceMaterial = slope >= 18f && rockMaterial is { } rock
                        ? rock
                        : soilMaterial is { } soil && StableCellNoise(seed, x / 5, z / 5) < .055f
                            ? soil
                            : groundMaterial;
                }
                result.Add(new TerrainMaterialPaintCell(x, z, materialMap[sourceMaterial]));
            }
        }
        return result;
    }

    private static float StableCellNoise(int seed, int x, int z)
    {
        var value = unchecked((uint)seed ^ ((uint)x * 0x9e3779b9u) ^ ((uint)z * 0x85ebca6bu));
        value ^= value >> 16;
        value *= 0x7feb352du;
        value ^= value >> 15;
        value *= 0x846ca68bu;
        value ^= value >> 16;
        return (value & 0x00ffffff) / 16777216f;
    }

    private static StructureSurfaceAdaptation AdaptSurfaceToGeneratedStructures(
        AreaDefinition target,
        WorldGenerationTerrainSurfaceEvidence surface)
    {
        var foundations = target.Entities.Where(IsGeneratedFoundation).ToArray();
        var groups = GeneratedBuildingGroups(target, foundations);
        if (surface.SamplesX < 2 || surface.SamplesZ < 2 ||
            surface.Heights.Count != surface.SamplesX * surface.SamplesZ ||
            !float.IsFinite(surface.HeightScale) || Math.Abs(surface.HeightScale) <= .000001f)
        {
            return new(surface, groups.Count, 0, 0);
        }

        var pads = groups
            .Where(group => group.Foundations.All(foundation =>
                PointIsInBounds(surface, foundation.Transform.Position.X, foundation.Transform.Position.Z) &&
                Math.Abs(foundation.Transform.Position.Y - HeightAt(
                    surface,
                    foundation.Transform.Position.X,
                    foundation.Transform.Position.Z)) <= MaximumAutomaticStructureAdjustmentMeters))
            .Select(group =>
            {
                var positions = group.Members.Select(entity => entity.Transform.Position).ToArray();
                return new TerrainStructurePad(
                    group.Key,
                    positions.Min(position => position.X),
                    positions.Max(position => position.X),
                    positions.Min(position => position.Z),
                    positions.Max(position => position.Z),
                    group.Foundations.Average(entity => entity.Transform.Position.Y));
            })
            .ToArray();
        if (pads.Length == 0) return new(surface, groups.Count, 0, 0);

        var spacingX = surface.Width / (float)(surface.SamplesX - 1);
        var spacingZ = surface.Depth / (float)(surface.SamplesZ - 1);
        var innerMargin = MathF.Sqrt((spacingX * spacingX) + (spacingZ * spacingZ)) + .001f;
        var blendDistance = Math.Max(8f, Math.Max(spacingX, spacingZ) * 2f);
        var adjusted = surface.Heights.ToArray();
        var changedSampleCount = 0;
        for (var zIndex = 0; zIndex < surface.SamplesZ; zIndex++)
        {
            var worldZ = (-surface.Depth * .5f) + (zIndex * spacingZ);
            for (var xIndex = 0; xIndex < surface.SamplesX; xIndex++)
            {
                var worldX = (-surface.Width * .5f) + (xIndex * spacingX);
                TerrainStructurePad? nearest = null;
                var nearestDistance = float.MaxValue;
                foreach (var pad in pads)
                {
                    var distance = DistanceToEnvelope(worldX, worldZ, pad);
                    if (distance < nearestDistance - .000001f ||
                        (Math.Abs(distance - nearestDistance) <= .000001f &&
                         (nearest is null || string.CompareOrdinal(pad.Key, nearest.Key) < 0)))
                    {
                        nearest = pad;
                        nearestDistance = distance;
                    }
                }
                if (nearest is null || nearestDistance > innerMargin + blendDistance) continue;

                var influence = nearestDistance <= innerMargin
                    ? 1f
                    : 1f - ((nearestDistance - innerMargin) / blendDistance);
                influence = influence * influence * (3f - (2f * influence));
                var index = (zIndex * surface.SamplesX) + xIndex;
                var targetHeight = nearest.Height / surface.HeightScale;
                var next = adjusted[index] + ((targetHeight - adjusted[index]) * influence);
                if (Math.Abs(next - adjusted[index]) > .000001f) changedSampleCount++;
                adjusted[index] = next;
            }
        }
        if (changedSampleCount == 0) return new(surface, groups.Count, pads.Length, 0);

        return new(
            surface with
            {
                Heights = adjusted,
                Fingerprint = WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
                    surface.Width,
                    surface.Depth,
                    surface.SamplesX,
                    surface.SamplesZ,
                    surface.HeightScale,
                    adjusted,
                    surface.CellDiagonal),
            },
            groups.Count,
            pads.Length,
            changedSampleCount);
    }

    private static IReadOnlyList<GeneratedBuildingGroup> GeneratedBuildingGroups(
        AreaDefinition target,
        IReadOnlyList<EntityInstance> foundations) => foundations
        .GroupBy(GeneratedBuildingGroupKey)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group =>
        {
            var members = group.Key.StartsWith("assistant-node:", StringComparison.OrdinalIgnoreCase)
                ? target.Entities.Where(entity => entity.Tags.Contains(
                    group.Key, StringComparer.OrdinalIgnoreCase)).ToArray()
                : group.ToArray();
            return new GeneratedBuildingGroup(group.Key, group.ToArray(), members);
        })
        .ToArray();

    private static string GeneratedBuildingGroupKey(EntityInstance entity) =>
        entity.Tags.FirstOrDefault(tag =>
            tag.StartsWith("assistant-node:", StringComparison.OrdinalIgnoreCase)) ??
        entity.Id.ToString();

    private static float DistanceToEnvelope(float x, float z, TerrainStructurePad pad)
    {
        var deltaX = x < pad.MinimumX
            ? pad.MinimumX - x
            : x > pad.MaximumX
                ? x - pad.MaximumX
                : 0f;
        var deltaZ = z < pad.MinimumZ
            ? pad.MinimumZ - z
            : z > pad.MaximumZ
                ? z - pad.MaximumZ
                : 0f;
        return MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }

    private sealed record GeneratedBuildingGroup(
        string Key,
        IReadOnlyList<EntityInstance> Foundations,
        IReadOnlyList<EntityInstance> Members);

    private sealed record TerrainStructurePad(
        string Key,
        float MinimumX,
        float MaximumX,
        float MinimumZ,
        float MaximumZ,
        float Height);

    private sealed record StructureSurfaceAdaptation(
        WorldGenerationTerrainSurfaceEvidence Surface,
        int GroupCount,
        int EligibleGroupCount,
        int ChangedSampleCount);

    private static string Fingerprint(
        AreaDefinition source,
        WorldGenerationTerrainSurfaceEvidence surface,
        IReadOnlyList<TerrainMaterialLayer> materials,
        IReadOnlyList<TerrainMaterialPaintCell> materialPaint,
        IReadOnlyList<BiomeLayerDefinition> biomes,
        IReadOnlyList<TerrainFeatureDefinition> features,
        IReadOnlyList<string> strata,
        bool environmentInherited,
        WorldGenerationProductionSpatialQualityEvidence spatialQuality)
    {
        var value = new StringBuilder();
        Append(value, "openrpg-production-art-direction-v6");
        Append(value, source.Id.Value.ToString("D"));
        Append(value, surface.Fingerprint);
        Append(value, environmentInherited.ToString(CultureInfo.InvariantCulture));
        foreach (var material in materials.OrderBy(item => item.Id))
        {
            Append(value, material.Name);
            Append(value, material.AlbedoColor);
            Append(value, material.MaterialResource);
            Append(value, material.UvScale.ToString("R", CultureInfo.InvariantCulture));
        }
        foreach (var cell in materialPaint.OrderBy(item => item.Z).ThenBy(item => item.X))
        {
            Append(value, cell.X.ToString(CultureInfo.InvariantCulture));
            Append(value, cell.Z.ToString(CultureInfo.InvariantCulture));
            Append(value, cell.MaterialLayerId.ToString("D"));
        }
        foreach (var biome in biomes.OrderBy(item => item.Id))
        {
            Append(value, biome.Name);
            Append(value, biome.ScatterResource);
            Append(value, biome.ScatterDensity.ToString("R", CultureInfo.InvariantCulture));
            foreach (var tag in biome.Tags) Append(value, tag);
        }
        foreach (var feature in features.OrderBy(item => item.Id))
        {
            Append(value, feature.Name);
            Append(value, feature.Kind.ToString());
            Append(value, feature.Width.ToString("R", CultureInfo.InvariantCulture));
            Append(value, feature.Height.ToString("R", CultureInfo.InvariantCulture));
            Append(value, feature.MaterialResource);
            foreach (var point in feature.ControlPoints)
            {
                Append(value, point.X.ToString("R", CultureInfo.InvariantCulture));
                Append(value, point.Y.ToString("R", CultureInfo.InvariantCulture));
                Append(value, point.Z.ToString("R", CultureInfo.InvariantCulture));
            }
        }
        foreach (var stratum in strata) Append(value, stratum);
        Append(value, spatialQuality.CameraSpawnCount.ToString(CultureInfo.InvariantCulture));
        Append(value, spatialQuality.MaximumCameraReliefMeters.ToString("R", CultureInfo.InvariantCulture));
        Append(value, spatialQuality.GroundedFoundationCount.ToString(CultureInfo.InvariantCulture));
        Append(value, spatialQuality.MaximumFoundationOffsetMeters.ToString("R", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())))
            .ToLowerInvariant();
    }

    private static IReadOnlyList<TerrainFeatureDefinition> MaterializeFeatures(
        AreaDefinition source,
        AreaDefinition target,
        WorldGenerationRecipe recipe,
        WorldGenerationTerrainSurfaceEvidence surface,
        string provenance)
    {
        var preserved = target.World.Features
            .Where(feature => !feature.Provenance.StartsWith(
                "world-generation-promotion:", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var foundations = target.Entities.Where(IsGeneratedFoundation).ToArray();
        var tallVegetationTags = source.World.Biomes
            .SelectMany(biome => biome.Tags)
            .Where(tag => StratumTags[0].Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        preserved.AddRange(MaterializeBuildingScatterExclusions(
            target,
            foundations,
            tallVegetationTags,
            surface,
            provenance));
        if (recipe.Roads.Count == 0) return preserved;

        var sourceRoads = source.World.Features
            .Where(feature => feature.Kind == TerrainFeatureKind.Road &&
                              !string.IsNullOrWhiteSpace(feature.MaterialResource) &&
                              !feature.MaterialResource.StartsWith("builtin/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(feature => feature.Id)
            .ToArray();
        if (sourceRoads.Length == 0) return preserved;

        var anchors = SettlementAnchors(target, foundations);
        var frame = TargetFeatureFrame.Create(anchors, surface);
        for (var index = 0; index < recipe.Roads.Count; index++)
        {
            var roadRecipe = recipe.Roads[index];
            var sourceRoad = sourceRoads[index % sourceRoads.Length];
            var points = RelocateRoad(sourceRoad, roadRecipe, frame, foundations, surface, index);
            preserved.Add(new TerrainFeatureDefinition(
                StableId(target.Id.Value, $"production-road:{roadRecipe.Id:D}"),
                TerrainFeatureDefinition.CurrentSchemaVersion,
                $"{sourceRoad.Name} — {roadRecipe.Kind}",
                TerrainFeatureKind.Road,
                points,
                (float)roadRecipe.WidthMeters,
                Math.Max(.035f, sourceRoad.Height),
                sourceRoad.BankDegrees,
                sourceRoad.CrossingPolicy,
                sourceRoad.CompatibleBiomeTags,
                sourceRoad.MaterialResource,
                provenance));
        }
        return preserved;
    }

    private static IReadOnlyList<TerrainFeatureDefinition> MaterializeBuildingScatterExclusions(
        AreaDefinition target,
        IReadOnlyList<EntityInstance> foundations,
        IReadOnlyList<string> tallVegetationTags,
        WorldGenerationTerrainSurfaceEvidence surface,
        string provenance)
    {
        var result = new List<TerrainFeatureDefinition>();
        var groups = GeneratedBuildingGroups(target, foundations);
        foreach (var group in groups)
        {
            var positions = group.Members.Select(entity => entity.Transform.Position).ToArray();
            var minimumX = positions.Min(position => position.X);
            var maximumX = positions.Max(position => position.X);
            var minimumZ = positions.Min(position => position.Z);
            var maximumZ = positions.Max(position => position.Z);
            AddGuide(
                result,
                target.Id.Value,
                group.Key,
                "envelope",
                "Emprise végétale du bâtiment",
                minimumX,
                maximumX,
                minimumZ,
                maximumZ,
                BuildingVegetationEnvelopePaddingMeters,
                [],
                surface,
                provenance);
            if (tallVegetationTags.Count > 0)
            {
                AddGuide(
                    result,
                    target.Id.Value,
                    group.Key,
                    "tall-clearance",
                    "Dégagement des arbres du bâtiment",
                    minimumX,
                    maximumX,
                    minimumZ,
                    maximumZ,
                    BuildingTallVegetationClearanceMeters,
                    tallVegetationTags,
                    surface,
                    provenance);
            }
        }
        return result;
    }

    private static void AddGuide(
        ICollection<TerrainFeatureDefinition> destination,
        Guid areaId,
        string groupKey,
        string discriminator,
        string name,
        float minimumX,
        float maximumX,
        float minimumZ,
        float maximumZ,
        float clearance,
        IReadOnlyList<string> compatibleBiomeTags,
        WorldGenerationTerrainSurfaceEvidence surface,
        string provenance)
    {
        var spanX = maximumX - minimumX;
        var spanZ = maximumZ - minimumZ;
        var alongX = spanX >= spanZ;
        var centerX = (minimumX + maximumX) * .5f;
        var centerZ = (minimumZ + maximumZ) * .5f;
        var halfWidth = (alongX ? spanZ : spanX) * .5f + clearance;
        var startX = alongX ? minimumX : centerX;
        var startZ = alongX ? centerZ : minimumZ;
        var endX = alongX ? maximumX : centerX;
        var endZ = alongX ? centerZ : maximumZ;
        var edgeMargin = .01f;
        startX = Math.Clamp(startX, -surface.Width * .5f + edgeMargin, surface.Width * .5f - edgeMargin);
        endX = Math.Clamp(endX, -surface.Width * .5f + edgeMargin, surface.Width * .5f - edgeMargin);
        startZ = Math.Clamp(startZ, -surface.Depth * .5f + edgeMargin, surface.Depth * .5f - edgeMargin);
        endZ = Math.Clamp(endZ, -surface.Depth * .5f + edgeMargin, surface.Depth * .5f - edgeMargin);
        destination.Add(new TerrainFeatureDefinition(
            StableId(areaId, $"production-building-scatter:{groupKey}:{discriminator}"),
            TerrainFeatureDefinition.CurrentSchemaVersion,
            $"{name} — {groupKey}",
            TerrainFeatureKind.Building,
            [
                new Vector3Value(startX, HeightAt(surface, startX, startZ), startZ),
                new Vector3Value(endX, HeightAt(surface, endX, endZ), endZ),
            ],
            Math.Clamp(halfWidth * 2f, .1f, 512f),
            0,
            0,
            TerrainFeatureCrossingPolicy.Allow,
            compatibleBiomeTags,
            "builtin/invisible-scatter-exclusion",
            provenance));
    }

    private static IReadOnlyList<(float X, float Z)> SettlementAnchors(
        AreaDefinition target,
        IReadOnlyList<EntityInstance> foundations,
        bool includeFallback = true)
    {
        var groups = foundations
            .GroupBy(entity => entity.Tags.FirstOrDefault(tag =>
                    tag.StartsWith("assistant-node:", StringComparison.OrdinalIgnoreCase)) ??
                entity.Id.ToString())
            .Select(group => (
                X: group.Average(entity => entity.Transform.Position.X),
                Z: group.Average(entity => entity.Transform.Position.Z)))
            .ToList();
        groups.AddRange(target.Entities.Where(IsPlayerSpawn)
            .Select(entity => (entity.Transform.Position.X, entity.Transform.Position.Z)));
        if (includeFallback && groups.Count == 0)
        {
            groups.AddRange(target.Entities.Take(16)
                .Select(entity => (entity.Transform.Position.X, entity.Transform.Position.Z)));
        }
        return groups.Count > 0 ? groups : includeFallback ? [(0f, 0f)] : [];
    }

    private sealed record TargetVegetationFocus(float CenterX, float CenterZ, float Radius)
    {
        public static TargetVegetationFocus Create(
            IReadOnlyList<(float X, float Z)> anchors,
            WorldGenerationTerrainSurfaceEvidence surface)
        {
            var centerX = anchors.Average(point => point.X);
            var centerZ = anchors.Average(point => point.Z);
            var furthestAnchor = anchors.Max(point => MathF.Sqrt(
                MathF.Pow(point.X - centerX, 2) + MathF.Pow(point.Z - centerZ, 2)));
            var edgeRadius = MathF.Min(
                surface.Width * .5f - MathF.Abs(centerX),
                surface.Depth * .5f - MathF.Abs(centerZ));
            var maximumRadius = MathF.Max(8f, MathF.Min(192f, edgeRadius));
            var minimumRadius = MathF.Min(72f, maximumRadius);
            return new(centerX, centerZ,
                Math.Clamp(furthestAnchor + 40f, minimumRadius, maximumRadius));
        }
    }

    private static IReadOnlyList<Vector3Value> RelocateRoad(
        TerrainFeatureDefinition sourceRoad,
        RoadGenerationRecipe recipe,
        TargetFeatureFrame frame,
        IReadOnlyList<Forge.Domain.Entities.EntityInstance> foundations,
        WorldGenerationTerrainSurfaceEvidence surface,
        int roadIndex)
    {
        var points = sourceRoad.ControlPoints;
        var sourceRangeX = points.Max(point => point.X) - points.Min(point => point.X);
        var sourceRangeZ = points.Max(point => point.Z) - points.Min(point => point.Z);
        var sourceRunsAlongX = sourceRangeX >= sourceRangeZ;
        var primaryValues = points.Select(point => sourceRunsAlongX ? point.X : point.Z).ToArray();
        var minorValues = points.Select(point => sourceRunsAlongX ? point.Z : point.X).ToArray();
        var primaryMin = primaryValues.Min();
        var primaryRange = Math.Max(primaryValues.Max() - primaryMin, .001f);
        var minorCenter = (minorValues.Min() + minorValues.Max()) * .5f;
        var minorHalfRange = Math.Max((minorValues.Max() - minorValues.Min()) * .5f, .001f);
        var minorAmplitude = Math.Min(frame.HalfWidth * .25f, 10f);
        var candidates = new[] { -.375f, -.25f, -.125f, 0f, .125f, .25f, .375f }
            .Select(value => value * frame.HalfWidth)
            .ToArray();
        var bestOffset = candidates
            .Select(offset => new
            {
                Offset = offset,
                Score = RoadClearanceScore(Build(offset), foundations) - Math.Abs(offset) * .35f,
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => Math.Abs(candidate.Offset))
            .ThenBy(candidate => candidate.Offset)
            .First().Offset;
        return Build(bestOffset);

        IReadOnlyList<Vector3Value> Build(float lateralOffset)
        {
            var result = new Vector3Value[points.Count];
            var edgeMargin = (float)recipe.WidthMeters * .5f + 1f;
            for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                var primaryT = ((primaryValues[pointIndex] - primaryMin) / primaryRange * 2f) - 1f;
                var minorT = (minorValues[pointIndex] - minorCenter) / minorHalfRange;
                var stagger = roadIndex * Math.Max((float)recipe.WidthMeters + 2f, 7f);
                var primary = primaryT * frame.HalfLength;
                var minor = minorT * minorAmplitude + lateralOffset + stagger;
                var x = frame.CenterX + frame.PrimaryX * primary + frame.MinorX * minor;
                var z = frame.CenterZ + frame.PrimaryZ * primary + frame.MinorZ * minor;
                x = Math.Clamp(x, (-surface.Width * .5f) + edgeMargin,
                    (surface.Width * .5f) - edgeMargin);
                z = Math.Clamp(z, (-surface.Depth * .5f) + edgeMargin,
                    (surface.Depth * .5f) - edgeMargin);
                var y = HeightAt(surface, x, z) + Math.Max(.035f, sourceRoad.Height);
                result[pointIndex] = new Vector3Value(x, y, z);
            }
            return result;
        }
    }

    private static float RoadClearanceScore(
        IReadOnlyList<Vector3Value> road,
        IReadOnlyList<EntityInstance> foundations)
    {
        if (foundations.Count == 0) return 0;
        var minimum = float.MaxValue;
        foreach (var foundation in foundations)
        {
            var point = foundation.Transform.Position;
            for (var index = 0; index < road.Count - 1; index++)
            {
                minimum = Math.Min(minimum, DistanceToSegment(
                    point.X, point.Z,
                    road[index].X, road[index].Z,
                    road[index + 1].X, road[index + 1].Z));
            }
        }
        return minimum;
    }

    private static float DistanceToSegment(
        float pointX,
        float pointZ,
        float startX,
        float startZ,
        float endX,
        float endZ)
    {
        var deltaX = endX - startX;
        var deltaZ = endZ - startZ;
        var lengthSquared = deltaX * deltaX + deltaZ * deltaZ;
        if (lengthSquared <= .000001f)
            return MathF.Sqrt(MathF.Pow(pointX - startX, 2) + MathF.Pow(pointZ - startZ, 2));
        var t = Math.Clamp(((pointX - startX) * deltaX + (pointZ - startZ) * deltaZ) /
                           lengthSquared, 0, 1);
        var nearestX = startX + deltaX * t;
        var nearestZ = startZ + deltaZ * t;
        return MathF.Sqrt(MathF.Pow(pointX - nearestX, 2) + MathF.Pow(pointZ - nearestZ, 2));
    }

    private sealed record TargetFeatureFrame(
        float CenterX,
        float CenterZ,
        float PrimaryX,
        float PrimaryZ,
        float MinorX,
        float MinorZ,
        float HalfLength,
        float HalfWidth)
    {
        public static TargetFeatureFrame Create(
            IReadOnlyList<(float X, float Z)> anchors,
            WorldGenerationTerrainSurfaceEvidence surface)
        {
            var centerX = anchors.Average(point => point.X);
            var centerZ = anchors.Average(point => point.Z);
            var rangeX = anchors.Max(point => point.X) - anchors.Min(point => point.X);
            var rangeZ = anchors.Max(point => point.Z) - anchors.Min(point => point.Z);
            var alongX = rangeX >= rangeZ;
            var primaryRange = alongX ? rangeX : rangeZ;
            var minorRange = alongX ? rangeZ : rangeX;
            var halfLength = Math.Min(Math.Max(primaryRange * .5f + 18f, 24f),
                Math.Min(surface.Width, surface.Depth) * .42f);
            var halfWidth = Math.Min(Math.Max(minorRange * .5f + 12f, 16f),
                Math.Min(surface.Width, surface.Depth) * .22f);
            return alongX
                ? new(centerX, centerZ, 1, 0, 0, 1, halfLength, halfWidth)
                : new(centerX, centerZ, 0, 1, 1, 0, halfLength, halfWidth);
        }
    }

    private static SpatialQualityInspection InspectSpatialQuality(
        AreaDefinition target,
        WorldGenerationTerrainSurfaceEvidence surface)
    {
        var diagnostics = new List<string>();
        var terrainSampleSpacing = Math.Max(
            surface.Width / (float)(surface.SamplesX - 1),
            surface.Depth / (float)(surface.SamplesZ - 1));
        var maximumTerrainSampleSpacing = MaximumTerrainSampleSpacingMeters(surface.Width, surface.Depth);
        if (terrainSampleSpacing > maximumTerrainSampleSpacing + .0001f)
        {
            diagnostics.Add(
                $"{TerrainResolutionInvalidCode}: canonical terrain uses {terrainSampleSpacing:F2} m between samples; production output for {surface.Width} x {surface.Depth} m permits at most {maximumTerrainSampleSpacing:F2} m.");
        }
        var cameraSpawns = target.Entities.Where(IsPlayerSpawn).ToArray();
        var maximumCameraRelief = 0f;
        foreach (var spawn in cameraSpawns)
        {
            var position = spawn.Transform.Position;
            if (!FootprintIsInBounds(surface, position.X, position.Z, CameraInspectionDiameterMeters))
            {
                diagnostics.Add($"{CameraSpawnUnsafeCode}: player spawn '{spawn.Id}' does not leave a complete {CameraInspectionDiameterMeters:R} m camera inspection corridor inside terrain bounds.");
                continue;
            }

            var heights = SampleFootprint(surface, position.X, position.Z, CameraInspectionDiameterMeters, 5);
            var relief = heights.Max() - heights.Min();
            maximumCameraRelief = Math.Max(maximumCameraRelief, relief);
            var groundOffset = position.Y - HeightAt(surface, position.X, position.Z);
            if (relief > MaximumCameraReliefMeters ||
                groundOffset < MinimumPlayerGroundOffsetMeters ||
                groundOffset > MaximumPlayerGroundOffsetMeters)
            {
                diagnostics.Add(
                    $"{CameraSpawnUnsafeCode}: player spawn '{spawn.Id}' has {relief:F2} m of relief across the camera corridor and a {groundOffset:F2} m ground offset; limits are {MaximumCameraReliefMeters:R} m relief and {MinimumPlayerGroundOffsetMeters:R}..{MaximumPlayerGroundOffsetMeters:R} m offset.");
            }
        }

        var foundations = target.Entities.Where(IsGeneratedFoundation).ToArray();
        var maximumFoundationOffset = 0f;
        foreach (var foundation in foundations)
        {
            var position = foundation.Transform.Position;
            if (!PointIsInBounds(surface, position.X, position.Z))
            {
                diagnostics.Add($"{FoundationFloatingCode}: generated foundation '{foundation.Id}' lies outside terrain bounds.");
                continue;
            }
            var offset = Math.Abs(position.Y - HeightAt(surface, position.X, position.Z));
            maximumFoundationOffset = Math.Max(maximumFoundationOffset, offset);
            if (offset > MaximumFoundationOffsetMeters)
            {
                var surfaceFingerprint = surface.Fingerprint.Length > 12
                    ? surface.Fingerprint[..12]
                    : surface.Fingerprint;
                diagnostics.Add(
                    $"{FoundationFloatingCode}: generated foundation '{foundation.Id}' is {offset:F2} m from the canonical terrain surface; maximum is {MaximumFoundationOffsetMeters:R} m (surface {surfaceFingerprint}).");
            }
        }

        diagnostics.AddRange(WorldGenerationBuildingEntranceInspector.Inspect(target).Diagnostics.Select(item =>
            $"{item.Code}: {item.Message} ({item.Group})."));

        return new SpatialQualityInspection(
            new WorldGenerationProductionSpatialQualityEvidence(
                cameraSpawns.Length,
                maximumCameraRelief,
                foundations.Length,
                maximumFoundationOffset),
            diagnostics);
    }

    private static bool IsPlayerSpawn(EntityInstance entity) =>
        entity.Tags.Any(tag => tag.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                               tag.Equals("player-start", StringComparison.OrdinalIgnoreCase) ||
                               tag.Equals("player-spawn", StringComparison.OrdinalIgnoreCase));

    private static bool IsGeneratedFoundation(EntityInstance entity) =>
        entity.Tags.Contains("foundation", StringComparer.OrdinalIgnoreCase) &&
        (entity.Tags.Contains("assistant-generated", StringComparer.OrdinalIgnoreCase) ||
         entity.Tags.Contains("world-generation", StringComparer.OrdinalIgnoreCase));

    private static string? GetOverride(EntityInstance entity, string key) => entity.Overrides.Properties
        .FirstOrDefault(property => string.Equals(property.Key, key, StringComparison.Ordinal))?.Value;

    private static bool PointIsInBounds(
        WorldGenerationTerrainSurfaceEvidence surface,
        float x,
        float z) =>
        x >= -surface.Width / 2f && x <= surface.Width / 2f &&
        z >= -surface.Depth / 2f && z <= surface.Depth / 2f;

    private static bool FootprintIsInBounds(
        WorldGenerationTerrainSurfaceEvidence surface,
        float centerX,
        float centerZ,
        float diameter)
    {
        var radius = diameter / 2f;
        return PointIsInBounds(surface, centerX - radius, centerZ - radius) &&
               PointIsInBounds(surface, centerX + radius, centerZ + radius);
    }

    private static IReadOnlyList<float> SampleFootprint(
        WorldGenerationTerrainSurfaceEvidence surface,
        float centerX,
        float centerZ,
        float diameter,
        int samplesPerAxis)
    {
        var result = new List<float>(samplesPerAxis * samplesPerAxis);
        var radius = diameter / 2f;
        for (var zIndex = 0; zIndex < samplesPerAxis; zIndex++)
        {
            var z = centerZ - radius + (diameter * zIndex / (samplesPerAxis - 1f));
            for (var xIndex = 0; xIndex < samplesPerAxis; xIndex++)
            {
                var x = centerX - radius + (diameter * xIndex / (samplesPerAxis - 1f));
                result.Add(HeightAt(surface, x, z));
            }
        }
        return result;
    }

    private static float HeightAt(
        WorldGenerationTerrainSurfaceEvidence surface,
        float worldX,
        float worldZ)
    {
        var sampleX = Math.Clamp((worldX + (surface.Width / 2f)) / surface.Width, 0, 1) *
                      (surface.SamplesX - 1);
        var sampleZ = Math.Clamp((worldZ + (surface.Depth / 2f)) / surface.Depth, 0, 1) *
                      (surface.SamplesZ - 1);
        var x0 = (int)MathF.Floor(sampleX);
        var z0 = (int)MathF.Floor(sampleZ);
        var x1 = Math.Min(x0 + 1, surface.SamplesX - 1);
        var z1 = Math.Min(z0 + 1, surface.SamplesZ - 1);
        var tx = sampleX - x0;
        var tz = sampleZ - z0;
        float Sample(int x, int z) => surface.Heights[(z * surface.SamplesX) + x] * surface.HeightScale;
        var h00 = Sample(x0, z0);
        var h10 = Sample(x1, z0);
        var h01 = Sample(x0, z1);
        var h11 = Sample(x1, z1);
        if (surface.CellDiagonal == TerrainCellDiagonal.Main)
        {
            return tx >= tz
                ? (h00 * (1 - tx)) + (h10 * (tx - tz)) + (h11 * tz)
                : (h00 * (1 - tz)) + (h11 * tx) + (h01 * (tz - tx));
        }
        return tx + tz <= 1
            ? (h00 * (1 - tx - tz)) + (h10 * tx) + (h01 * tz)
            : (h10 * (1 - tz)) + (h01 * (1 - tx)) + (h11 * (tx + tz - 1));
    }

    private sealed record SpatialQualityInspection(
        WorldGenerationProductionSpatialQualityEvidence Evidence,
        IReadOnlyList<string> Diagnostics);

    private static void Append(StringBuilder target, string value) =>
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');

    private static Guid StableId(Guid areaId, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId:D}:{discriminator}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}
