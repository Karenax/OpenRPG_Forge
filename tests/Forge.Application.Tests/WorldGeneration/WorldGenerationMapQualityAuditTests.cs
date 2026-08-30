using Forge.Application.WorldGeneration;

namespace Forge.Application.Tests.WorldGeneration;

public sealed class WorldGenerationMapQualityAuditTests
{
    [Fact]
    public void AcceptsCompleteForwardPlusRuntimeEvidence()
    {
        var result = WorldGenerationMapQualityAudit.Evaluate(ValidInput());

        Assert.True(result.Passed, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void RejectsMissingOrSparseMaterializedVegetation()
    {
        var missing = WorldGenerationMapQualityAudit.Evaluate(ValidInput() with
        {
            MaterializedVegetationStrataCount = 0,
            LocalGroundCoverInstanceCount = 0,
        });
        var sparse = WorldGenerationMapQualityAudit.Evaluate(ValidInput() with
        {
            LocalGroundCoverInstanceCount = 100,
            LocalGroundCoverAreaSquareMeters = 1_000,
        });

        Assert.Contains(missing.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.VegetationStrataMissingCode);
        Assert.Contains(missing.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.GroundCoverDensityLowCode);
        Assert.Contains(sparse.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.GroundCoverDensityLowCode);
    }

    [Fact]
    public void RejectsCoarseUnpaintedTerrainEvenWhenTextureResourcesResolve()
    {
        var result = WorldGenerationMapQualityAudit.Evaluate(ValidInput() with
        {
            ResolvedTerrainTextureCount = 2,
            TerrainSampleSpacingMeters = 32,
            MaterializedTerrainPaintLayerCount = 0,
            TerrainMaterialPaintCoverageRatio = 0,
        });

        Assert.DoesNotContain(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.TerrainTextureMissingCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.TerrainResolutionInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.TerrainPaintLayerMissingCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.TerrainPaintCoverageInvalidCode);
    }

    [Fact]
    public void RejectsMissingBuriedOrLocomotionMismatchedPatrolActors()
    {
        var result = WorldGenerationMapQualityAudit.Evaluate(ValidInput() with
        {
            AuthoredPatrolActorCount = 2,
            AuditedPatrolActorCount = 1,
            VisualPatrolActorCount = 0,
            AnimatedPatrolActorCount = 0,
            MovingPatrolActorCount = 0,
            MovingPatrolSampleCount = 0,
            MaximumPatrolRootGroundErrorMeters = 1.4,
            MaximumPatrolVisualGroundErrorMeters = 1.8,
            MaximumPatrolSpeedErrorRatio = .7,
            LocomotionAnimationMismatchCount = 3,
        });

        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.PatrolCountMissingCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.PatrolAuditIncompleteCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.PatrolVisualGroundingInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.PatrolSpeedInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.PatrolAnimationMismatchCode);
    }

    [Fact]
    public void RejectsMissingOrFloatingBuildingGroups()
    {
        var result = WorldGenerationMapQualityAudit.Evaluate(ValidInput() with
        {
            AuthoredBuildingGroupCount = 4,
            AuditedBuildingGroupCount = 3,
            AuditedFoundationCount = 9,
            GovernedFoundationVisualCount = 8,
            MaximumFoundationVisualGapMeters = 2.2,
        });

        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.BuildingCountMissingCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.BuildingAuditIncompleteCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.FoundationVisualMissingCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.FoundationFloatingCode);
    }

    [Fact]
    public void RejectsIncoherentUnclickableOrUntestedBuildingEntrances()
    {
        var result = WorldGenerationMapQualityAudit.Evaluate(ValidInput() with
        {
            AuthoredBuildingEntranceCount = 4,
            AuditedBuildingEntranceCount = 3,
            ClickableBuildingDoorCount = 2,
            DoorToggleVerifiedCount = 1,
            MaximumDoorVisualLateralErrorMeters = .8,
            MaximumDoorVisualPlaneErrorMeters = .5,
            MaximumDoorFrameYawErrorDegrees = 45,
            MaximumStairVisualLateralErrorMeters = .5,
            MaximumStairThresholdGapMeters = .6,
        });

        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.BuildingEntranceMissingCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.BuildingEntranceAuditIncompleteCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.BuildingDoorNotClickableCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.BuildingDoorToggleInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.BuildingDoorAlignmentInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.BuildingStairThresholdInvalidCode);
    }

    [Fact]
    public void RejectsVegetationIntersectingOrObscuringBuildings()
    {
        var result = WorldGenerationMapQualityAudit.Evaluate(ValidInput() with
        {
            VegetationBuildingOverlapCount = 12,
            TallVegetationBuildingClearanceViolationCount = 3,
        });

        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.VegetationBuildingOverlapCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.TallVegetationBuildingClearanceCode);
    }

    [Fact]
    public void RejectsRegressedCameraAndInsufficientVisualCoverage()
    {
        var result = WorldGenerationMapQualityAudit.Evaluate(ValidInput() with
        {
            RenderMethod = "gl_compatibility",
            RenderDriver = "opengl3",
            ResolvedTerrainTextureCount = 0,
            CameraOrbitResponded = false,
            CameraZoomResponded = false,
            CaptureCorridorObstructionCount = 2,
            CaptureCount = 1,
        });

        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.RenderBackendInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.TerrainTextureMissingCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.CameraOrbitInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.CameraZoomInvalidCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.CaptureCorridorObstructedCode);
        Assert.Contains(result.Diagnostics, item =>
            item.Code == WorldGenerationMapQualityAudit.CaptureCountMissingCode);
    }

    private static WorldGenerationMapQualityAuditInput ValidInput() => new(
        WorldGenerationMapQualityAuditInput.CurrentSchemaVersion,
        RenderMethod: "forward_plus",
        RenderDriver: "vulkan",
        RequiredTexturedTerrainLayerCount: 1,
        ResolvedTerrainTextureCount: 3,
        TerrainSampleSpacingMeters: 4,
        MaximumTerrainSampleSpacingMeters: 8,
        RequiredTerrainPaintLayerCount: 2,
        MaterializedTerrainPaintLayerCount: 3,
        TerrainMaterialPaintCoverageRatio: 1,
        MinimumTerrainMaterialPaintCoverageRatio: .9,
        RequiredVegetationStrataCount: 3,
        MaterializedVegetationStrataCount: 3,
        GroundCoverLocalized: true,
        LocalGroundCoverInstanceCount: 8_000,
        LocalGroundCoverAreaSquareMeters: 10_000,
        MinimumGroundCoverDensityPerSquareMeter: .55,
        RequiredPatrolActorCount: 4,
        AuthoredPatrolActorCount: 4,
        AuditedPatrolActorCount: 4,
        VisualPatrolActorCount: 4,
        AnimatedPatrolActorCount: 4,
        MovingPatrolActorCount: 4,
        MovingPatrolSampleCount: 120,
        MaximumPatrolRootGroundErrorMeters: .01,
        MaximumPatrolVisualGroundErrorMeters: .03,
        MaximumPatrolSpeedErrorRatio: .08,
        LocomotionAnimationMismatchCount: 0,
        RequiredBuildingGroupCount: 6,
        AuthoredBuildingGroupCount: 6,
        AuditedBuildingGroupCount: 6,
        AuthoredFoundationCount: 12,
        AuditedFoundationCount: 12,
        GovernedFoundationVisualCount: 12,
        MaximumFoundationVisualGapMeters: .02,
        RequiredBuildingEntranceCount: 6,
        AuthoredBuildingEntranceCount: 6,
        AuditedBuildingEntranceCount: 6,
        ClickableBuildingDoorCount: 6,
        DoorToggleVerifiedCount: 6,
        MaximumDoorVisualLateralErrorMeters: .01,
        MaximumDoorVisualPlaneErrorMeters: .01,
        MaximumDoorFrameYawErrorDegrees: .1,
        MaximumStairVisualLateralErrorMeters: .01,
        MaximumStairThresholdGapMeters: .03,
        VegetationBuildingOverlapCount: 0,
        TallVegetationBuildingClearanceViolationCount: 0,
        CaptureCorridorObstructionCount: 0,
        CameraOrbitResponded: true,
        CameraZoomResponded: true,
        RequiredCaptureCount: 5,
        CaptureCount: 5);
}
