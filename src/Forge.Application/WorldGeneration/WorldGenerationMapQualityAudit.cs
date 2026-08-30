namespace Forge.Application.WorldGeneration;

public sealed record WorldGenerationMapQualityAuditInput(
    int SchemaVersion,
    string RenderMethod,
    string RenderDriver,
    int RequiredTexturedTerrainLayerCount,
    int ResolvedTerrainTextureCount,
    double TerrainSampleSpacingMeters,
    double MaximumTerrainSampleSpacingMeters,
    int RequiredTerrainPaintLayerCount,
    int MaterializedTerrainPaintLayerCount,
    double TerrainMaterialPaintCoverageRatio,
    double MinimumTerrainMaterialPaintCoverageRatio,
    int RequiredVegetationStrataCount,
    int MaterializedVegetationStrataCount,
    bool GroundCoverLocalized,
    int LocalGroundCoverInstanceCount,
    double LocalGroundCoverAreaSquareMeters,
    double MinimumGroundCoverDensityPerSquareMeter,
    int RequiredPatrolActorCount,
    int AuthoredPatrolActorCount,
    int AuditedPatrolActorCount,
    int VisualPatrolActorCount,
    int AnimatedPatrolActorCount,
    int MovingPatrolActorCount,
    int MovingPatrolSampleCount,
    double MaximumPatrolRootGroundErrorMeters,
    double MaximumPatrolVisualGroundErrorMeters,
    double MaximumPatrolSpeedErrorRatio,
    int LocomotionAnimationMismatchCount,
    int RequiredBuildingGroupCount,
    int AuthoredBuildingGroupCount,
    int AuditedBuildingGroupCount,
    int AuthoredFoundationCount,
    int AuditedFoundationCount,
    int GovernedFoundationVisualCount,
    double MaximumFoundationVisualGapMeters,
    int RequiredBuildingEntranceCount,
    int AuthoredBuildingEntranceCount,
    int AuditedBuildingEntranceCount,
    int ClickableBuildingDoorCount,
    int DoorToggleVerifiedCount,
    double MaximumDoorVisualLateralErrorMeters,
    double MaximumDoorVisualPlaneErrorMeters,
    double MaximumDoorFrameYawErrorDegrees,
    double MaximumStairVisualLateralErrorMeters,
    double MaximumStairThresholdGapMeters,
    int VegetationBuildingOverlapCount,
    int TallVegetationBuildingClearanceViolationCount,
    int CaptureCorridorObstructionCount,
    bool CameraOrbitResponded,
    bool CameraZoomResponded,
    int RequiredCaptureCount,
    int CaptureCount)
{
    public const int CurrentSchemaVersion = 1;

    public double LocalGroundCoverDensityPerSquareMeter => LocalGroundCoverAreaSquareMeters > 0
        ? LocalGroundCoverInstanceCount / LocalGroundCoverAreaSquareMeters
        : 0;
}

public sealed record WorldGenerationMapQualityAuditDiagnostic(string Code, string Message);

public sealed record WorldGenerationMapQualityAuditResult(
    WorldGenerationMapQualityAuditInput Input,
    IReadOnlyList<WorldGenerationMapQualityAuditDiagnostic> Diagnostics)
{
    public bool Passed => Diagnostics.Count == 0;
}

public static class WorldGenerationMapQualityAudit
{
    public const string InputInvalidCode = "map_quality.input_invalid";
    public const string RenderBackendInvalidCode = "map_quality.render_backend_invalid";
    public const string TerrainTextureMissingCode = "map_quality.terrain_texture_missing";
    public const string TerrainResolutionInvalidCode = "map_quality.terrain_resolution_invalid";
    public const string TerrainPaintLayerMissingCode = "map_quality.terrain_paint_layer_missing";
    public const string TerrainPaintCoverageInvalidCode = "map_quality.terrain_paint_coverage_invalid";
    public const string VegetationStrataMissingCode = "map_quality.vegetation_strata_missing";
    public const string GroundCoverNotLocalizedCode = "map_quality.ground_cover_not_localized";
    public const string GroundCoverDensityLowCode = "map_quality.ground_cover_density_low";
    public const string PatrolCountMissingCode = "map_quality.patrol_count_missing";
    public const string PatrolAuditIncompleteCode = "map_quality.patrol_audit_incomplete";
    public const string PatrolVisualMissingCode = "map_quality.patrol_visual_missing";
    public const string PatrolAnimationMissingCode = "map_quality.patrol_animation_missing";
    public const string PatrolNotMovingCode = "map_quality.patrol_not_moving";
    public const string PatrolGroundingInvalidCode = "map_quality.patrol_grounding_invalid";
    public const string PatrolVisualGroundingInvalidCode = "map_quality.patrol_visual_grounding_invalid";
    public const string PatrolSpeedInvalidCode = "map_quality.patrol_speed_invalid";
    public const string PatrolAnimationMismatchCode = "map_quality.patrol_animation_mismatch";
    public const string BuildingCountMissingCode = "map_quality.building_count_missing";
    public const string BuildingAuditIncompleteCode = "map_quality.building_audit_incomplete";
    public const string FoundationVisualMissingCode = "map_quality.foundation_visual_missing";
    public const string FoundationFloatingCode = "map_quality.foundation_floating";
    public const string BuildingEntranceMissingCode = "map_quality.building_entrance_missing";
    public const string BuildingEntranceAuditIncompleteCode = "map_quality.building_entrance_audit_incomplete";
    public const string BuildingDoorNotClickableCode = "map_quality.building_door_not_clickable";
    public const string BuildingDoorToggleInvalidCode = "map_quality.building_door_toggle_invalid";
    public const string BuildingDoorAlignmentInvalidCode = "map_quality.building_door_alignment_invalid";
    public const string BuildingStairThresholdInvalidCode = "map_quality.building_stair_threshold_invalid";
    public const string VegetationBuildingOverlapCode = "map_quality.vegetation_building_overlap";
    public const string TallVegetationBuildingClearanceCode = "map_quality.tall_vegetation_building_clearance";
    public const string CaptureCorridorObstructedCode = "map_quality.capture_corridor_obstructed";
    public const string CameraOrbitInvalidCode = "map_quality.camera_orbit_invalid";
    public const string CameraZoomInvalidCode = "map_quality.camera_zoom_invalid";
    public const string CaptureCountMissingCode = "map_quality.capture_count_missing";

    public const double MaximumPatrolGroundErrorMeters = .08;
    public const double MaximumPatrolSpeedErrorRatio = .25;
    public const double MaximumFoundationVisualGapMeters = .12;
    public const double MaximumDoorVisualLateralErrorMeters = .1;
    public const double MaximumDoorVisualPlaneErrorMeters = .2;
    public const double MaximumDoorFrameYawErrorDegrees = 3;
    public const double MaximumStairVisualLateralErrorMeters = .1;
    public const double MaximumStairThresholdGapMeters = .12;
    public const int MinimumCaptureCount = 3;

    public static WorldGenerationMapQualityAuditResult Evaluate(WorldGenerationMapQualityAuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var diagnostics = new List<WorldGenerationMapQualityAuditDiagnostic>();
        if (!HasValidShape(input))
        {
            diagnostics.Add(new(InputInvalidCode,
                "Map-quality audit values must use schema 1 and finite, non-negative counts, areas, densities and errors."));
            return new(input, diagnostics);
        }

        if (!string.Equals(input.RenderMethod, "gl_compatibility", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(input.RenderMethod, "forward_plus", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new(RenderBackendInvalidCode,
                $"Runtime renderer '{input.RenderMethod}/{input.RenderDriver}' is unknown; Forward+ Vulkan evidence is required."));
        }
        else if (!string.Equals(input.RenderMethod, "forward_plus", StringComparison.OrdinalIgnoreCase) ||
                 !input.RenderDriver.Contains("vulkan", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new(RenderBackendInvalidCode,
                $"Runtime renderer must be Forward+ Vulkan, found '{input.RenderMethod}/{input.RenderDriver}'."));
        }

        if (input.ResolvedTerrainTextureCount < input.RequiredTexturedTerrainLayerCount)
            diagnostics.Add(new(TerrainTextureMissingCode,
                $"Resolved terrain textures {input.ResolvedTerrainTextureCount} are below the required {input.RequiredTexturedTerrainLayerCount}."));
        if (input.MaximumTerrainSampleSpacingMeters > 0 &&
            input.TerrainSampleSpacingMeters > input.MaximumTerrainSampleSpacingMeters + .000001)
        {
            diagnostics.Add(new(TerrainResolutionInvalidCode,
                $"Terrain sample spacing {input.TerrainSampleSpacingMeters:0.###} m exceeds the production limit {input.MaximumTerrainSampleSpacingMeters:0.###} m."));
        }
        if (input.MaterializedTerrainPaintLayerCount < input.RequiredTerrainPaintLayerCount)
            diagnostics.Add(new(TerrainPaintLayerMissingCode,
                $"Materialized terrain paint layers {input.MaterializedTerrainPaintLayerCount} are below the required {input.RequiredTerrainPaintLayerCount}."));
        if (input.MinimumTerrainMaterialPaintCoverageRatio > 0 &&
            input.TerrainMaterialPaintCoverageRatio + .000001 < input.MinimumTerrainMaterialPaintCoverageRatio)
        {
            diagnostics.Add(new(TerrainPaintCoverageInvalidCode,
                $"Terrain material-paint coverage {input.TerrainMaterialPaintCoverageRatio:P1} is below the required {input.MinimumTerrainMaterialPaintCoverageRatio:P1}."));
        }

        if (input.MaterializedVegetationStrataCount < input.RequiredVegetationStrataCount)
            diagnostics.Add(new(VegetationStrataMissingCode,
                $"Materialized vegetation strata {input.MaterializedVegetationStrataCount} are below the required {input.RequiredVegetationStrataCount}."));
        if (input.RequiredVegetationStrataCount > 0 && !input.GroundCoverLocalized)
            diagnostics.Add(new(GroundCoverNotLocalizedCode,
                "Exterior production ground cover must be localized around the authored play area."));
        if (input.MinimumGroundCoverDensityPerSquareMeter > 0 &&
            input.LocalGroundCoverDensityPerSquareMeter + .000001 < input.MinimumGroundCoverDensityPerSquareMeter)
        {
            diagnostics.Add(new(GroundCoverDensityLowCode,
                $"Local ground-cover density {input.LocalGroundCoverDensityPerSquareMeter:0.###}/m2 is below the required {input.MinimumGroundCoverDensityPerSquareMeter:0.###}/m2."));
        }

        if (input.AuthoredPatrolActorCount < input.RequiredPatrolActorCount)
            diagnostics.Add(new(PatrolCountMissingCode,
                $"Authored patrol actors {input.AuthoredPatrolActorCount} are below the requested {input.RequiredPatrolActorCount}."));
        if (input.AuditedPatrolActorCount < input.AuthoredPatrolActorCount)
            diagnostics.Add(new(PatrolAuditIncompleteCode,
                $"Only {input.AuditedPatrolActorCount} of {input.AuthoredPatrolActorCount} authored patrol actors loaded into the runtime audit."));
        if (input.VisualPatrolActorCount < input.AuditedPatrolActorCount)
            diagnostics.Add(new(PatrolVisualMissingCode,
                $"Only {input.VisualPatrolActorCount} of {input.AuditedPatrolActorCount} audited patrol actors have a governed authored visual."));
        if (input.AnimatedPatrolActorCount < input.AuditedPatrolActorCount)
            diagnostics.Add(new(PatrolAnimationMissingCode,
                $"Only {input.AnimatedPatrolActorCount} of {input.AuditedPatrolActorCount} audited patrol actors have a character animation controller."));
        if (input.AuditedPatrolActorCount > 0 && input.MovingPatrolActorCount < input.AuditedPatrolActorCount)
            diagnostics.Add(new(PatrolNotMovingCode,
                $"Only {input.MovingPatrolActorCount} of {input.AuditedPatrolActorCount} patrol actors moved during the runtime observation window."));
        if (input.AuditedPatrolActorCount > 0 && input.MovingPatrolSampleCount == 0)
            diagnostics.Add(new(PatrolNotMovingCode,
                "No moving patrol sample was recorded in the runtime observation window."));
        if (input.MaximumPatrolRootGroundErrorMeters > MaximumPatrolGroundErrorMeters)
            diagnostics.Add(new(PatrolGroundingInvalidCode,
                $"Maximum patrol-root ground error {input.MaximumPatrolRootGroundErrorMeters:0.###} m exceeds {MaximumPatrolGroundErrorMeters:0.###} m."));
        if (input.MaximumPatrolVisualGroundErrorMeters > MaximumPatrolGroundErrorMeters)
            diagnostics.Add(new(PatrolVisualGroundingInvalidCode,
                $"Maximum patrol visual-foot ground error {input.MaximumPatrolVisualGroundErrorMeters:0.###} m exceeds {MaximumPatrolGroundErrorMeters:0.###} m."));
        if (input.MaximumPatrolSpeedErrorRatio > MaximumPatrolSpeedErrorRatio)
            diagnostics.Add(new(PatrolSpeedInvalidCode,
                $"Maximum patrol speed error ratio {input.MaximumPatrolSpeedErrorRatio:0.###} exceeds {MaximumPatrolSpeedErrorRatio:0.###}."));
        if (input.LocomotionAnimationMismatchCount > 0)
            diagnostics.Add(new(PatrolAnimationMismatchCode,
                $"Observed {input.LocomotionAnimationMismatchCount} patrol samples with a speed/animation mismatch."));

        if (input.AuthoredBuildingGroupCount < input.RequiredBuildingGroupCount)
            diagnostics.Add(new(BuildingCountMissingCode,
                $"Authored building groups {input.AuthoredBuildingGroupCount} are below the requested {input.RequiredBuildingGroupCount}."));
        if (input.AuditedBuildingGroupCount < input.AuthoredBuildingGroupCount ||
            input.AuditedFoundationCount < input.AuthoredFoundationCount)
        {
            diagnostics.Add(new(BuildingAuditIncompleteCode,
                $"Runtime audited {input.AuditedBuildingGroupCount}/{input.AuthoredBuildingGroupCount} building groups and {input.AuditedFoundationCount}/{input.AuthoredFoundationCount} foundations."));
        }
        if (input.GovernedFoundationVisualCount < input.AuditedFoundationCount)
            diagnostics.Add(new(FoundationVisualMissingCode,
                $"Only {input.GovernedFoundationVisualCount} of {input.AuditedFoundationCount} audited foundations use governed authored visuals."));
        if (input.MaximumFoundationVisualGapMeters > MaximumFoundationVisualGapMeters)
            diagnostics.Add(new(FoundationFloatingCode,
                $"Maximum visible foundation gap {input.MaximumFoundationVisualGapMeters:0.###} m exceeds {MaximumFoundationVisualGapMeters:0.###} m."));
        if (input.AuthoredBuildingEntranceCount < input.RequiredBuildingEntranceCount)
            diagnostics.Add(new(BuildingEntranceMissingCode,
                $"Authored coherent building entrances {input.AuthoredBuildingEntranceCount} are below the required {input.RequiredBuildingEntranceCount}."));
        if (input.AuditedBuildingEntranceCount < input.AuthoredBuildingEntranceCount)
            diagnostics.Add(new(BuildingEntranceAuditIncompleteCode,
                $"Runtime audited {input.AuditedBuildingEntranceCount}/{input.AuthoredBuildingEntranceCount} authored building entrances."));
        if (input.ClickableBuildingDoorCount < input.AuditedBuildingEntranceCount)
            diagnostics.Add(new(BuildingDoorNotClickableCode,
                $"Only {input.ClickableBuildingDoorCount}/{input.AuditedBuildingEntranceCount} audited entrance doors expose a ray-pickable toggle interaction."));
        if (input.DoorToggleVerifiedCount < input.ClickableBuildingDoorCount)
            diagnostics.Add(new(BuildingDoorToggleInvalidCode,
                $"Only {input.DoorToggleVerifiedCount}/{input.ClickableBuildingDoorCount} clickable entrance doors completed a measured opening transition."));
        if (input.MaximumDoorVisualLateralErrorMeters > MaximumDoorVisualLateralErrorMeters ||
            input.MaximumDoorVisualPlaneErrorMeters > MaximumDoorVisualPlaneErrorMeters ||
            input.MaximumDoorFrameYawErrorDegrees > MaximumDoorFrameYawErrorDegrees)
        {
            diagnostics.Add(new(BuildingDoorAlignmentInvalidCode,
                $"Closed door visual/frame error reached lateral={input.MaximumDoorVisualLateralErrorMeters:0.###} m, plane={input.MaximumDoorVisualPlaneErrorMeters:0.###} m and yaw={input.MaximumDoorFrameYawErrorDegrees:0.###} degrees."));
        }
        if (input.MaximumStairVisualLateralErrorMeters > MaximumStairVisualLateralErrorMeters ||
            input.MaximumStairThresholdGapMeters > MaximumStairThresholdGapMeters)
        {
            diagnostics.Add(new(BuildingStairThresholdInvalidCode,
                $"Entrance stair visual error reached lateral={input.MaximumStairVisualLateralErrorMeters:0.###} m and stair-top/door-threshold={input.MaximumStairThresholdGapMeters:0.###} m."));
        }
        if (input.VegetationBuildingOverlapCount > 0)
            diagnostics.Add(new(VegetationBuildingOverlapCode,
                $"Detected {input.VegetationBuildingOverlapCount} vegetation instances inside building envelopes."));
        if (input.TallVegetationBuildingClearanceViolationCount > 0)
            diagnostics.Add(new(TallVegetationBuildingClearanceCode,
                $"Detected {input.TallVegetationBuildingClearanceViolationCount} tall vegetation instances inside building sightline clearances."));
        if (input.CaptureCorridorObstructionCount > 0)
            diagnostics.Add(new(CaptureCorridorObstructedCode,
                $"Detected {input.CaptureCorridorObstructionCount} vegetation obstructions inside mandatory ground-level subject corridors."));

        if (!input.CameraOrbitResponded)
            diagnostics.Add(new(CameraOrbitInvalidCode, "Runtime camera did not respond to an orbit input."));
        if (!input.CameraZoomResponded)
            diagnostics.Add(new(CameraZoomInvalidCode, "Runtime camera did not respond to a zoom input."));
        var requiredCaptureCount = Math.Max(MinimumCaptureCount, input.RequiredCaptureCount);
        if (input.CaptureCount < requiredCaptureCount)
            diagnostics.Add(new(CaptureCountMissingCode,
                $"Runtime audit produced {input.CaptureCount} captures; at least {requiredCaptureCount} distinct viewpoints are required."));

        return new(input, diagnostics);
    }

    private static bool HasValidShape(WorldGenerationMapQualityAuditInput input) =>
        input.SchemaVersion == WorldGenerationMapQualityAuditInput.CurrentSchemaVersion &&
        input.RequiredTexturedTerrainLayerCount >= 0 && input.ResolvedTerrainTextureCount >= 0 &&
        IsFiniteNonNegative(input.TerrainSampleSpacingMeters) &&
        IsFiniteNonNegative(input.MaximumTerrainSampleSpacingMeters) &&
        input.RequiredTerrainPaintLayerCount >= 0 && input.MaterializedTerrainPaintLayerCount >= 0 &&
        IsRatio(input.TerrainMaterialPaintCoverageRatio) &&
        IsRatio(input.MinimumTerrainMaterialPaintCoverageRatio) &&
        input.RequiredVegetationStrataCount >= 0 && input.MaterializedVegetationStrataCount >= 0 &&
        input.LocalGroundCoverInstanceCount >= 0 &&
        IsFiniteNonNegative(input.LocalGroundCoverAreaSquareMeters) &&
        IsFiniteNonNegative(input.MinimumGroundCoverDensityPerSquareMeter) &&
        input.RequiredPatrolActorCount >= 0 && input.AuthoredPatrolActorCount >= 0 &&
        input.AuditedPatrolActorCount >= 0 && input.VisualPatrolActorCount >= 0 &&
        input.AnimatedPatrolActorCount >= 0 && input.MovingPatrolActorCount >= 0 &&
        input.MovingPatrolSampleCount >= 0 && IsFiniteNonNegative(input.MaximumPatrolRootGroundErrorMeters) &&
        IsFiniteNonNegative(input.MaximumPatrolVisualGroundErrorMeters) &&
        IsFiniteNonNegative(input.MaximumPatrolSpeedErrorRatio) && input.LocomotionAnimationMismatchCount >= 0 &&
        input.RequiredBuildingGroupCount >= 0 && input.AuthoredBuildingGroupCount >= 0 &&
        input.AuditedBuildingGroupCount >= 0 && input.AuthoredFoundationCount >= 0 &&
        input.AuditedFoundationCount >= 0 && input.GovernedFoundationVisualCount >= 0 &&
        IsFiniteNonNegative(input.MaximumFoundationVisualGapMeters) &&
        input.RequiredBuildingEntranceCount >= 0 && input.AuthoredBuildingEntranceCount >= 0 &&
        input.AuditedBuildingEntranceCount >= 0 && input.ClickableBuildingDoorCount >= 0 &&
        input.DoorToggleVerifiedCount >= 0 &&
        IsFiniteNonNegative(input.MaximumDoorVisualLateralErrorMeters) &&
        IsFiniteNonNegative(input.MaximumDoorVisualPlaneErrorMeters) &&
        IsFiniteNonNegative(input.MaximumDoorFrameYawErrorDegrees) &&
        IsFiniteNonNegative(input.MaximumStairVisualLateralErrorMeters) &&
        IsFiniteNonNegative(input.MaximumStairThresholdGapMeters) &&
        input.VegetationBuildingOverlapCount >= 0 &&
        input.TallVegetationBuildingClearanceViolationCount >= 0 &&
        input.CaptureCorridorObstructionCount >= 0 &&
        input.RequiredCaptureCount >= 0 && input.CaptureCount >= 0;

    private static bool IsFiniteNonNegative(double value) => double.IsFinite(value) && value >= 0;

    private static bool IsRatio(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}
