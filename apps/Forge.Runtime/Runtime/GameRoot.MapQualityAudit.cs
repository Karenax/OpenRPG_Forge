using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.WorldGeneration;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.World;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private const double MapQualityPatrolObservationSeconds = 4;
    private const double MapQualityMaximumObservationWallSeconds = 8;
    private const int MapQualityMaximumObservationFrames = 240;

    private async Task RunMapQualityAuditAsync(
        string destinationDirectory,
        int minimumPatrolActors,
        int minimumBuildingGroups,
        int? requiredVegetationStrata,
        double? minimumGroundCoverDensity)
    {
        if (!Path.IsPathFullyQualified(destinationDirectory))
            throw new ArgumentException("The map-quality audit destination must be absolute.",
                nameof(destinationDirectory));
        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        var area = _activeArea ?? throw new InvalidOperationException(
            "Map-quality audit requires a fully loaded area.");
        var camera = _camera ?? throw new InvalidOperationException(
            "Map-quality audit requires the runtime gameplay camera.");
        var player = _player ?? throw new InvalidOperationException(
            "Map-quality audit requires the runtime player.");
        if (_areaWorldRoot is null || !GodotObject.IsInstanceValid(_areaWorldRoot))
            throw new InvalidOperationException("Map-quality audit requires a materialized runtime world.");

        var exterior = area.Kind == AreaKind.Exterior;
        var requiredTextures = exterior ? 1 : 0;
        var requiredStrata = requiredVegetationStrata ?? (exterior ? 3 : 0);
        var requiredGroundCoverDensity = minimumGroundCoverDensity ?? (requiredStrata > 0 ? .55 : 0);
        GD.Print($"OPENRPG_FORGE_RUNTIME_MAP_QUALITY_MACHINE_START area={area.Slug} " +
                 $"patrol_min={minimumPatrolActors} buildings_min={minimumBuildingGroups} " +
                 $"strata_min={requiredStrata} ground_cover_min={requiredGroundCoverDensity:0.###}");

        var window = GetWindow();
        window.Mode = Window.ModeEnum.Windowed;
        window.Borderless = true;
        window.Size = new Vector2I(1600, 900);
        window.Position = Vector2I.Zero;
        for (var frame = 0; frame < 8; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var renderMethod = RenderingServer.GetCurrentRenderingMethod();
        var renderDriver = RenderingServer.GetCurrentRenderingDriverName();
        var resolvedTerrainTextures = _areaWorldRoot
            .GetMeta("forge_terrain_resolved_textures", 0).AsInt32();
        var terrainSampleSpacing = Math.Max(
            area.World.Terrain.Width / (double)(area.World.Terrain.SamplesX - 1),
            area.World.Terrain.Depth / (double)(area.World.Terrain.SamplesZ - 1));
        var maximumTerrainSampleSpacing = exterior
            ? WorldGenerationProductionArtDirection.MaximumTerrainSampleSpacingMeters(
                area.World.Terrain.Width,
                area.World.Terrain.Depth)
            : 0;
        var terrainCellCount = Math.Max(1,
            (area.World.Terrain.SamplesX - 1) * (area.World.Terrain.SamplesZ - 1));
        var terrainPaintCoverage = Math.Clamp(
            area.World.Terrain.MaterialPaint.Count / (double)terrainCellCount,
            0,
            1);
        var materializedTerrainPaintLayers = area.World.Terrain.MaterialPaint
            .Select(item => item.MaterialLayerId)
            .Distinct()
            .Count();
        var requiredTerrainPaintLayers = exterior && area.World.Terrain.MaterialLayers.Count > 1
            ? Math.Min(2, area.World.Terrain.MaterialLayers.Count)
            : 0;
        var minimumTerrainPaintCoverage = requiredTerrainPaintLayers > 0 ? .9 : 0;
        var materializedBiomeInstances = CountMaterializedBiomeInstances(area.World);
        var materializedStrata = CountMaterializedVegetationStrata(area.World, materializedBiomeInstances);
        var groundCover = MeasureGroundCover(area.World, materializedBiomeInstances, requiredStrata);
        GD.Print($"OPENRPG_FORGE_RUNTIME_MAP_QUALITY_MACHINE_PROGRESS phase=environment " +
                 $"textures={resolvedTerrainTextures} terrain_spacing={terrainSampleSpacing:0.###} " +
                 $"terrain_paint={terrainPaintCoverage:P1}/{materializedTerrainPaintLayers} " +
                 $"strata={materializedStrata} " +
                 $"ground_cover={groundCover.InstanceCount}");
        var patrol = await MeasurePatrolQualityAsync(area);
        GD.Print($"OPENRPG_FORGE_RUNTIME_MAP_QUALITY_MACHINE_PROGRESS phase=patrols " +
                 $"audited={patrol.AuditedActorCount} moving={patrol.MovingActorCount} " +
                 $"samples={patrol.MovingSampleCount}");
        var buildings = MeasureBuildingQuality(area);
        var entrances = await MeasureBuildingEntranceQualityAsync(area);
        var buildingVegetation = MeasureBuildingVegetationClearance(area, buildings.Envelopes);
        GD.Print($"OPENRPG_FORGE_RUNTIME_MAP_QUALITY_MACHINE_PROGRESS phase=buildings " +
                 $"groups={buildings.AuditedGroupCount} foundations={buildings.AuditedFoundationCount} " +
                 $"entrances={entrances.AuditedEntranceCount}/{entrances.ClickableDoorCount}/{entrances.ToggleVerifiedCount} " +
                 $"vegetation_overlap={buildingVegetation.EnvelopeOverlapCount} " +
                 $"tall_clearance={buildingVegetation.TallClearanceViolationCount}");

        var yawBefore = _cameraRig.YawDegrees;
        var pitchBefore = _cameraRig.PitchDegrees;
        var distanceBefore = _cameraRig.Distance;
        var cameraBefore = camera.GlobalPosition;
        _cameraRig.HandleInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true,
        });
        _cameraRig.HandleInput(new InputEventMouseMotion { Relative = new Vector2(42, -28) });
        _cameraRig.HandleInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = false,
        });
        _cameraRig.Snap(camera, player.GlobalPosition,
            _moduleExperience.ConstrainCamera ? ConstrainCameraPosition : null);
        var cameraOrbitResponded =
            Math.Abs(_cameraRig.YawDegrees - yawBefore) > .1f ||
            Math.Abs(_cameraRig.PitchDegrees - pitchBefore) > .1f;
        cameraOrbitResponded &= camera.GlobalPosition.DistanceTo(cameraBefore) > .05f;

        var cameraBeforeZoom = camera.GlobalPosition;
        _cameraRig.HandleInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.WheelUp,
            Pressed = true,
        });
        _cameraRig.Snap(camera, player.GlobalPosition,
            _moduleExperience.ConstrainCamera ? ConstrainCameraPosition : null);
        var cameraZoomResponded = _cameraRig.Distance < distanceBefore - .01f &&
                                  camera.GlobalPosition.DistanceTo(cameraBeforeZoom) > .05f;

        var captures = await CaptureMapQualityViewsAsync(
            root,
            camera,
            player,
            buildings,
            entrances,
            minimumPatrolActors,
            minimumBuildingGroups);
        var requiredCaptureCount = WorldGenerationMapQualityAudit.MinimumCaptureCount +
                                   (minimumPatrolActors > 0 ? 1 : 0) +
                                   (minimumBuildingGroups > 0 ? 3 : 0);
        var input = new WorldGenerationMapQualityAuditInput(
            SchemaVersion: WorldGenerationMapQualityAuditInput.CurrentSchemaVersion,
            RenderMethod: renderMethod,
            RenderDriver: renderDriver,
            RequiredTexturedTerrainLayerCount: requiredTextures,
            ResolvedTerrainTextureCount: resolvedTerrainTextures,
            TerrainSampleSpacingMeters: terrainSampleSpacing,
            MaximumTerrainSampleSpacingMeters: maximumTerrainSampleSpacing,
            RequiredTerrainPaintLayerCount: requiredTerrainPaintLayers,
            MaterializedTerrainPaintLayerCount: materializedTerrainPaintLayers,
            TerrainMaterialPaintCoverageRatio: terrainPaintCoverage,
            MinimumTerrainMaterialPaintCoverageRatio: minimumTerrainPaintCoverage,
            RequiredVegetationStrataCount: requiredStrata,
            MaterializedVegetationStrataCount: materializedStrata,
            GroundCoverLocalized: groundCover.Localized,
            LocalGroundCoverInstanceCount: groundCover.InstanceCount,
            LocalGroundCoverAreaSquareMeters: groundCover.AreaSquareMeters,
            MinimumGroundCoverDensityPerSquareMeter: requiredGroundCoverDensity,
            RequiredPatrolActorCount: minimumPatrolActors,
            AuthoredPatrolActorCount: patrol.AuthoredActorCount,
            AuditedPatrolActorCount: patrol.AuditedActorCount,
            VisualPatrolActorCount: patrol.VisualActorCount,
            AnimatedPatrolActorCount: patrol.AnimatedActorCount,
            MovingPatrolActorCount: patrol.MovingActorCount,
            MovingPatrolSampleCount: patrol.MovingSampleCount,
            MaximumPatrolRootGroundErrorMeters: patrol.MaximumRootGroundErrorMeters,
            MaximumPatrolVisualGroundErrorMeters: patrol.MaximumVisualGroundErrorMeters,
            MaximumPatrolSpeedErrorRatio: patrol.MaximumSpeedErrorRatio,
            LocomotionAnimationMismatchCount: patrol.AnimationMismatchCount,
            RequiredBuildingGroupCount: minimumBuildingGroups,
            AuthoredBuildingGroupCount: buildings.AuthoredGroupCount,
            AuditedBuildingGroupCount: buildings.AuditedGroupCount,
            AuthoredFoundationCount: buildings.AuthoredFoundationCount,
            AuditedFoundationCount: buildings.AuditedFoundationCount,
            GovernedFoundationVisualCount: buildings.GovernedVisualCount,
            MaximumFoundationVisualGapMeters: buildings.MaximumVisualGapMeters,
            RequiredBuildingEntranceCount: minimumBuildingGroups,
            AuthoredBuildingEntranceCount: entrances.AuthoredEntranceCount,
            AuditedBuildingEntranceCount: entrances.AuditedEntranceCount,
            ClickableBuildingDoorCount: entrances.ClickableDoorCount,
            DoorToggleVerifiedCount: entrances.ToggleVerifiedCount,
            MaximumDoorVisualLateralErrorMeters: entrances.MaximumDoorVisualLateralErrorMeters,
            MaximumDoorVisualPlaneErrorMeters: entrances.MaximumDoorVisualPlaneErrorMeters,
            MaximumDoorFrameYawErrorDegrees: entrances.MaximumDoorFrameYawErrorDegrees,
            MaximumStairVisualLateralErrorMeters: entrances.MaximumStairVisualLateralErrorMeters,
            MaximumStairThresholdGapMeters: entrances.MaximumStairThresholdGapMeters,
            VegetationBuildingOverlapCount: buildingVegetation.EnvelopeOverlapCount,
            TallVegetationBuildingClearanceViolationCount: buildingVegetation.TallClearanceViolationCount,
            CaptureCorridorObstructionCount: captures.Sum(item => item.CorridorObstructionCount),
            CameraOrbitResponded: cameraOrbitResponded,
            CameraZoomResponded: cameraZoomResponded,
            RequiredCaptureCount: requiredCaptureCount,
            CaptureCount: captures.Count);
        var result = WorldGenerationMapQualityAudit.Evaluate(input);
        var report = new MapQualityRuntimeReport(
            SchemaVersion: 1,
            AreaId: area.Id.Value,
            AreaSlug: area.Slug,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            MachineResult: result,
            Captures: captures,
            VisualReviewRequired: true);
        var reportPath = Path.Combine(root, "map-quality-machine-audit.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }) + System.Environment.NewLine, new UTF8Encoding(false));

        if (!result.Passed)
        {
            var failures = string.Join(" | ", result.Diagnostics.Select(item =>
                $"{item.Code}:{item.Message}"));
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_MAP_QUALITY_MACHINE_FAIL area={area.Slug} " +
                        $"diagnostics={result.Diagnostics.Count} report={reportPath}");
            throw new InvalidOperationException($"Runtime map-quality audit failed: {failures}");
        }

        GD.Print($"OPENRPG_FORGE_RUNTIME_MAP_QUALITY_MACHINE_PASS area={area.Slug} " +
                 $"textures={resolvedTerrainTextures} terrain_spacing={terrainSampleSpacing:0.###} " +
                 $"terrain_paint={terrainPaintCoverage:P1}/{materializedTerrainPaintLayers} " +
                 $"strata={materializedStrata} " +
                  $"ground_cover={groundCover.InstanceCount}/{groundCover.DensityPerSquareMeter:0.###}m2 " +
                  $"patrols={patrol.AuditedActorCount}/{patrol.MovingActorCount} " +
                  $"buildings={buildings.AuditedGroupCount} entrances={entrances.ToggleVerifiedCount} " +
                  $"door_center={entrances.MaximumDoorVisualLateralErrorMeters:0.###}m " +
                  $"stair_center={entrances.MaximumStairVisualLateralErrorMeters:0.###}m captures={captures.Count} " +
                  $"visual_review_required=true report={reportPath}");
        GetTree().Quit(0);
    }

    private async Task<MapQualityPatrolMeasurement> MeasurePatrolQualityAsync(AreaDefinition area)
    {
        var authoredActorCount = area.Entities.Count(entity =>
            Guid.TryParse(GetOverride(entity, "runtime.patrolRoute"), out _));
        var auditedActors = _entityWaypointPatrols.Keys
            .Where(GodotObject.IsInstanceValid)
            .ToArray();
        var visualActorCount = auditedActors.Count(actor => FindAuthoredVisual(actor) is not null);
        var animatedActorCount = auditedActors.Count(actor => _characterAnimations.ContainsKey(actor));
        var movingActors = new HashSet<Node3D>();
        var movingSamples = 0;
        var animationMismatches = 0;
        var maximumRootGroundError = 0d;
        var maximumVisualGroundError = 0d;
        var maximumSpeedErrorRatio = 0d;
        var observedSeconds = 0d;
        var observedFrames = 0;
        var observationStarted = Stopwatch.GetTimestamp();
        var hasGround = TryGetExteriorGroundHeight(out var groundHeightAt);

        while (observedSeconds < MapQualityPatrolObservationSeconds &&
               observedFrames < MapQualityMaximumObservationFrames &&
               Stopwatch.GetElapsedTime(observationStarted).TotalSeconds <
               MapQualityMaximumObservationWallSeconds)
        {
            var before = _entityWaypointPatrols.ToDictionary(
                item => item.Key,
                item => new PatrolFrameSample(
                    item.Value.Position,
                    item.Value.TargetIndex,
                    item.Value.WaitRemainingSeconds,
                    item.Value.Route.Points[item.Value.TargetIndex].Speed));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var delta = Math.Max(0, GetProcessDeltaTime());
            observedSeconds += delta;
            observedFrames++;
            if (observedFrames % 60 == 0)
                GD.Print($"OPENRPG_FORGE_RUNTIME_MAP_QUALITY_MACHINE_PROGRESS phase=patrol-observation " +
                         $"frames={observedFrames} simulation_seconds={observedSeconds:0.###} " +
                         $"wall_seconds={Stopwatch.GetElapsedTime(observationStarted).TotalSeconds:0.###}");

            foreach (var actor in auditedActors)
            {
                if (!GodotObject.IsInstanceValid(actor) ||
                    !_entityWaypointPatrols.TryGetValue(actor, out var state) ||
                    !before.TryGetValue(actor, out var previous))
                {
                    continue;
                }

                if (hasGround)
                {
                    var ground = groundHeightAt(actor.GlobalPosition.X, actor.GlobalPosition.Z);
                    maximumRootGroundError = Math.Max(maximumRootGroundError,
                        Math.Abs(actor.GlobalPosition.Y -
                                 (ground + GodotTerrainGroundedPatrolMotion.GroundClearanceMeters)));
                    if (FindAuthoredVisual(actor) is { } visual &&
                        TryWorldVisualBottom(visual, out var visualBottom))
                    {
                        var visualGround = groundHeightAt(visualBottom.X, visualBottom.Z);
                        maximumVisualGroundError = Math.Max(maximumVisualGroundError,
                            Math.Abs(visualBottom.Y - visualGround));
                    }
                }

                var horizontal = new Vector2(
                    state.Position.X - previous.Position.X,
                    state.Position.Z - previous.Position.Z).Length();
                if (horizontal <= .0001f) continue;
                movingActors.Add(actor);
                if (delta <= .000001 || previous.WaitRemainingSeconds > 0 ||
                    previous.TargetIndex != state.TargetIndex)
                {
                    continue;
                }

                movingSamples++;
                var actualSpeed = horizontal / delta;
                var expectedSpeed = Math.Max(.1, previous.TargetSpeed);
                maximumSpeedErrorRatio = Math.Max(maximumSpeedErrorRatio,
                    Math.Abs(actualSpeed - expectedSpeed) / expectedSpeed);
                if (!_characterAnimations.TryGetValue(actor, out var animation)) continue;
                var walking = PatrolLocomotionPolicy.IsWalkingPace(previous.TargetSpeed);
                if (walking && animation.WalkClip is null)
                {
                    animationMismatches++;
                    continue;
                }
                var expectedClip = walking ? animation.WalkClip : animation.RunClip;
                if (!string.Equals(animation.CurrentClip, expectedClip, StringComparison.Ordinal))
                    animationMismatches++;
            }
        }

        return new(
            authoredActorCount,
            auditedActors.Length,
            visualActorCount,
            animatedActorCount,
            movingActors.Count,
            movingSamples,
            maximumRootGroundError,
            maximumVisualGroundError,
            maximumSpeedErrorRatio,
            animationMismatches);
    }

    private MapQualityBuildingMeasurement MeasureBuildingQuality(AreaDefinition area)
    {
        var foundations = area.Entities
            .Where(entity => entity.Tags.Contains("foundation", StringComparer.OrdinalIgnoreCase) &&
                             entity.Tags.Any(tag => tag.StartsWith("assistant-node:",
                                 StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var groups = foundations
            .Select(AssistantNodeTag)
            .Where(tag => tag is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var auditedFoundations = 0;
        var governedVisuals = 0;
        var auditedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groupPositions = new Dictionary<string, List<Vector3>>(StringComparer.OrdinalIgnoreCase);
        var focusPositions = new List<Vector3>();
        var maximumVisualGap = 0d;
        var hasGround = TryGetExteriorGroundHeight(out var groundHeightAt);

        foreach (var foundation in foundations)
        {
            if (!_entityNodes.TryGetValue(foundation.Id.Value, out var node) ||
                !GodotObject.IsInstanceValid(node))
            {
                continue;
            }
            auditedFoundations++;
            focusPositions.Add(node.GlobalPosition);
            if (AssistantNodeTag(foundation) is { } group)
            {
                auditedGroups.Add(group);
                if (!groupPositions.TryGetValue(group, out var positions))
                {
                    positions = [];
                    groupPositions.Add(group, positions);
                }
                positions.Add(node.GlobalPosition);
            }
            if (FindAuthoredVisual(node) is not { } visual) continue;
            governedVisuals++;
            if (hasGround && TryWorldVisualBottom(visual, out var bottom))
            {
                var ground = groundHeightAt(bottom.X, bottom.Z);
                maximumVisualGap = Math.Max(maximumVisualGap, Math.Max(0, bottom.Y - ground));
            }
        }

        var groupCenters = groupPositions
            .Select(item => new Vector3(
                item.Value.Average(position => position.X),
                item.Value.Average(position => position.Y),
                item.Value.Average(position => position.Z)))
            .ToArray();
        var envelopes = groupPositions
            .Select(item => new MapQualityBuildingEnvelope(
                item.Value.Min(position => position.X),
                item.Value.Max(position => position.X),
                item.Value.Min(position => position.Z),
                item.Value.Max(position => position.Z)))
            .ToArray();
        var focus = focusPositions.Count == 0
            ? _player?.GlobalPosition ?? Vector3.Zero
            : new Vector3(
                focusPositions.Average(item => item.X),
                focusPositions.Average(item => item.Y),
                focusPositions.Average(item => item.Z));
        return new(
            groups.Length,
            auditedGroups.Count,
            foundations.Length,
            auditedFoundations,
            governedVisuals,
            maximumVisualGap,
            focus,
            groupCenters,
            envelopes);
    }

    private async Task<MapQualityBuildingEntranceMeasurement> MeasureBuildingEntranceQualityAsync(
        AreaDefinition area)
    {
        var authored = WorldGenerationBuildingEntranceInspector.Inspect(area);
        var audited = 0;
        var clickable = 0;
        var maximumDoorVisualLateralError = 0d;
        var maximumDoorVisualPlaneError = 0d;
        var maximumStairVisualLateralError = 0d;
        var maximumThresholdGap = 0d;
        var toggles = new Dictionary<Node3D, Vector3>();
        var groups = area.Entities
            .Where(entity => entity.Tags.Any(tag => tag.StartsWith("assistant-node:",
                StringComparison.OrdinalIgnoreCase)))
            .GroupBy(AssistantNodeTag)
            .Where(group => group.Key is not null && group.Count() >= 6 &&
                            group.Any(entity => entity.Tags.Contains("village-building",
                                StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        foreach (var group in groups)
        {
            var frame = group.SingleOrDefault(entity =>
                entity.Tags.Contains("doorway", StringComparer.OrdinalIgnoreCase) &&
                entity.Tags.Contains("entrance", StringComparer.OrdinalIgnoreCase));
            var door = group.SingleOrDefault(entity =>
                entity.Tags.Contains("interactive-door", StringComparer.OrdinalIgnoreCase) &&
                entity.Tags.Contains("entrance", StringComparer.OrdinalIgnoreCase));
            var stairs = group.SingleOrDefault(entity =>
                entity.Tags.Contains("entrance-stairs", StringComparer.OrdinalIgnoreCase) &&
                entity.Tags.Contains("entrance", StringComparer.OrdinalIgnoreCase));
            if (frame is null || door is null || stairs is null ||
                !_entityNodes.TryGetValue(frame.Id.Value, out var frameNode) ||
                !_entityNodes.TryGetValue(door.Id.Value, out var doorNode) ||
                !_entityNodes.TryGetValue(stairs.Id.Value, out var stairNode) ||
                !GodotObject.IsInstanceValid(frameNode) || !GodotObject.IsInstanceValid(doorNode) ||
                !GodotObject.IsInstanceValid(stairNode) ||
                FindAuthoredVisual(frameNode) is null ||
                FindAuthoredVisual(doorNode) is not { } doorVisual ||
                FindAuthoredVisual(stairNode) is not { } stairVisual)
            {
                continue;
            }

            if (!TryWorldVisualBottom(doorVisual, out var doorBottom) ||
                !TryWorldVisualTop(stairVisual, out var stairTop))
                continue;

            audited++;
            var doorBottomInFrame = frameNode.ToLocal(doorBottom);
            var stairTopInFrame = frameNode.ToLocal(stairTop);
            maximumDoorVisualLateralError = Math.Max(maximumDoorVisualLateralError,
                Math.Abs(doorBottomInFrame.X));
            maximumDoorVisualPlaneError = Math.Max(maximumDoorVisualPlaneError,
                Math.Abs(doorBottomInFrame.Z));
            maximumStairVisualLateralError = Math.Max(maximumStairVisualLateralError,
                Math.Abs(stairTopInFrame.X));
            maximumThresholdGap = Math.Max(maximumThresholdGap, Math.Abs(doorBottom.Y - stairTop.Y));

            if (!_clickInteractions.TryGetValue(doorNode, out var interaction) ||
                interaction.Action != EntityClickTriggerAction.ToggleDoor ||
                !_environmentDoors.ContainsKey(doorNode) ||
                doorNode.FindChildren("*", "CollisionShape3D", recursive: true, owned: false)
                    .OfType<CollisionShape3D>().All(shape => shape.Disabled))
            {
                continue;
            }

            clickable++;
            toggles[doorNode] = doorNode.GlobalTransform.Basis.X.Normalized();
            ExecuteClickInteraction(doorNode, interaction);
        }

        for (var frame = 0; frame < 32 && toggles.Count > 0; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var toggleVerified = toggles.Count(item =>
            Mathf.RadToDeg(Mathf.Acos(Math.Clamp(item.Value.Dot(
                item.Key.GlobalTransform.Basis.X.Normalized()), -1f, 1f))) >= 30);
        foreach (var node in toggles.Keys.Where(node =>
                     _environmentDoors.TryGetValue(node, out var state) && state.IsOpen))
            ToggleEnvironmentDoor(node);
        for (var frame = 0; frame < 32 && toggles.Count > 0; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        return new(
            authored.ValidEntranceCount,
            audited,
            clickable,
            toggleVerified,
            maximumDoorVisualLateralError,
            maximumDoorVisualPlaneError,
            authored.MaximumDoorFrameYawErrorDegrees,
            maximumStairVisualLateralError,
            maximumThresholdGap,
            toggles.Keys.FirstOrDefault());
    }

    private static MapQualityBuildingVegetationMeasurement MeasureBuildingVegetationClearance(
        AreaDefinition area,
        IReadOnlyList<MapQualityBuildingEnvelope> envelopes)
    {
        if (envelopes.Count == 0) return new(0, 0);
        var prepared = BiomeScatterPlanBuilder.Prepare(area.World);
        var envelopeOverlapCount = 0;
        var tallClearanceViolationCount = 0;
        foreach (var biome in area.World.Biomes.Where(IsVegetationBiome))
        {
            var placements = prepared.Build(biome).Placements;
            var tall = IsTallVegetationBiome(biome);
            foreach (var placement in placements)
            {
                if (envelopes.Any(envelope => envelope.Contains(
                        placement.Position.X,
                        placement.Position.Z,
                        WorldGenerationProductionArtDirection.BuildingVegetationEnvelopePaddingMeters)))
                {
                    envelopeOverlapCount++;
                }
                if (tall && envelopes.Any(envelope => envelope.Contains(
                        placement.Position.X,
                        placement.Position.Z,
                        WorldGenerationProductionArtDirection.BuildingTallVegetationClearanceMeters)))
                {
                    tallClearanceViolationCount++;
                }
            }
        }
        return new(envelopeOverlapCount, tallClearanceViolationCount);
    }

    private IReadOnlyDictionary<Guid, int> CountMaterializedBiomeInstances(AreaWorldProfile world)
    {
        var counts = world.Biomes.ToDictionary(item => item.Id, _ => 0);
        if (_areaWorldRoot is null) return counts;
        var prefixes = world.Biomes.ToDictionary(
            item => $"Scatter_{item.Id:N}_",
            item => item.Id,
            StringComparer.OrdinalIgnoreCase);
        foreach (var node in Descendants(_areaWorldRoot).OfType<MultiMeshInstance3D>())
        {
            var match = prefixes.FirstOrDefault(item =>
                node.Name.ToString().StartsWith(item.Key, StringComparison.OrdinalIgnoreCase));
            if (match.Key is null || node.Multimesh is null) continue;
            counts[match.Value] += node.Multimesh.InstanceCount;
        }
        return counts;
    }

    private static int CountMaterializedVegetationStrata(
        AreaWorldProfile world,
        IReadOnlyDictionary<Guid, int> materializedBiomeInstances)
    {
        var biomes = world.Biomes
            .Where(item => materializedBiomeInstances.GetValueOrDefault(item.Id) > 0)
            .ToArray();
        return new[]
        {
            new[] { "tree", "canopy", "woodland", "forest" },
            new[] { "shrub", "bush", "understory" },
            new[] { "ground-cover", "grass", "flower", "meadow", "fern" },
        }.Count(tags => biomes.Any(biome => tags.Any(tag => HasBiomeClassification(biome, tag))));
    }

    private static MapQualityGroundCoverMeasurement MeasureGroundCover(
        AreaWorldProfile world,
        IReadOnlyDictionary<Guid, int> materializedBiomeInstances,
        int requiredVegetationStrata)
    {
        var groundCoverBiomes = world.Biomes
            .Where(IsGroundCoverBiome)
            .Where(item => !string.IsNullOrWhiteSpace(item.ScatterResource))
            .ToArray();
        var focusRegions = groundCoverBiomes
            .Select(TryReadFocusRegion)
            .Where(item => item is not null)
            .Select(item => item!.Value)
            .ToArray();
        var localized = requiredVegetationStrata == 0 ||
                        groundCoverBiomes.Length > 0 && focusRegions.Length == groundCoverBiomes.Length;
        var zones = focusRegions
            .GroupBy(item =>
                $"{item.X.ToString("R", CultureInfo.InvariantCulture)}|" +
                item.Z.ToString("R", CultureInfo.InvariantCulture),
                StringComparer.Ordinal)
            .Select(group => group.MaxBy(item => item.Radius))
            .ToArray();
        var area = zones.Length > 0
            ? zones.Sum(item => Math.PI * item.Radius * item.Radius)
            : world.Terrain.Enabled ? world.Terrain.Width * world.Terrain.Depth : 0;
        var instances = groundCoverBiomes.Sum(item => materializedBiomeInstances.GetValueOrDefault(item.Id));
        return new(localized, instances, area);
    }

    private async Task<IReadOnlyList<MapQualityCaptureEvidence>> CaptureMapQualityViewsAsync(
        string root,
        Camera3D camera,
        PlayerController player,
        MapQualityBuildingMeasurement buildings,
        MapQualityBuildingEntranceMeasurement entrances,
        int minimumPatrolActors,
        int minimumBuildingGroups)
    {
        _sequenceOwnsCamera = true;
        var buildingFocus = buildings.Focus;
        var horizontalPositions = MeasureBuildingSpread(buildingFocus);
        var radius = horizontalPositions.Count == 0
            ? 28f
            : Math.Clamp(horizontalPositions.Max(item => new Vector2(
                item.X - buildingFocus.X,
                item.Z - buildingFocus.Z).Length()) + 14f, 28f, 120f);
        var terrainFocus = new Vector3(
            buildingFocus.X,
            _activeArea?.World.GroundHeightAt(buildingFocus.X, buildingFocus.Z) ?? buildingFocus.Y,
            buildingFocus.Z);
        var occluders = BuildMapQualityCaptureOccluders(_activeArea!.World);
        var views = new List<MapQualityView>
        {
            new MapQualityView("overview", "01-overview.png",
                terrainFocus + new Vector3(radius * .85f, radius * .62f, radius),
                terrainFocus + Vector3.Up * 2f,
                56f,
                0),
            new MapQualityView("overhead", "02-overhead.png",
                terrainFocus + new Vector3(0, radius * 1.65f, .5f),
                terrainFocus,
                52f,
                0),
            SelectClearMapQualityView(
                "ground-level",
                "03-ground-level.png",
                terrainFocus,
                radius * .95f,
                3.6f,
                1.4f,
                58f,
                49f,
                occluders),
        };
        if (minimumPatrolActors > 0 && _entityWaypointPatrols.Keys
                .Where(GodotObject.IsInstanceValid)
                .OrderBy(actor => actor.GlobalPosition.DistanceSquaredTo(terrainFocus))
                .FirstOrDefault() is { } patrolSubject)
        {
            views.Add(SelectClearMapQualityView(
                "patrol-character",
                "04-patrol-character.png",
                patrolSubject.GlobalPosition,
                8.5f,
                2.6f,
                1f,
                48f,
                50f,
                occluders));
        }
        if (minimumBuildingGroups > 0 && buildings.GroupCenters.Count > 0 && buildings.GroupCenters
                .OrderBy(position => position.DistanceSquaredTo(terrainFocus))
                .FirstOrDefault() is { } buildingSubject)
        {
            views.Add(SelectClearMapQualityView(
                "building-detail",
                "05-building-detail.png",
                buildingSubject,
                18.5f,
                5.2f,
                2.2f,
                52f,
                49f,
                occluders));
        }
        if (minimumBuildingGroups > 0 &&
            entrances.RepresentativeDoor is { } entranceDoor &&
            GodotObject.IsInstanceValid(entranceDoor))
        {
            var doorPosition = entranceDoor.GlobalPosition;
            var front = entranceDoor.GlobalTransform.Basis.Z.Normalized();
            var cameraHorizontal = doorPosition + (front * 8f);
            var cameraGround = _activeArea?.World.GroundHeightAt(cameraHorizontal.X, cameraHorizontal.Z) ??
                               doorPosition.Y;
            var cameraPosition = new Vector3(cameraHorizontal.X, cameraGround + 2.8f, cameraHorizontal.Z);
            var target = doorPosition + Vector3.Up * 1.2f;
            var corridor = MeasureCaptureCorridor(cameraPosition, target, occluders);
            views.Add(new MapQualityView(
                "door-closed",
                "06-door-closed.png",
                cameraPosition,
                target,
                48f,
                corridor.ObstructionCount));
            views.Add(new MapQualityView(
                "door-open",
                "07-door-open.png",
                cameraPosition,
                target,
                48f,
                corridor.ObstructionCount));
        }
        var captures = new List<MapQualityCaptureEvidence>();
        foreach (var view in views)
        {
            var captureDoor = view.Role == "door-open" ? entrances.RepresentativeDoor : null;
            if (captureDoor is not null &&
                GodotObject.IsInstanceValid(captureDoor) &&
                _environmentDoors.TryGetValue(captureDoor, out var doorState) &&
                !doorState.IsOpen)
            {
                ToggleEnvironmentDoor(captureDoor);
                for (var frame = 0; frame < 32; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            camera.GlobalPosition = view.Position;
            camera.Fov = view.FieldOfView;
            camera.LookAt(view.Target, Vector3.Up);
            player.Visible = true;
            for (var frame = 0; frame < 8; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            RenderingServer.ForceSync();
            var image = GetViewport().GetTexture().GetImage();
            if (GetViewport().UseHdr2D && image.GetFormat() is Image.Format.Rgb8 or Image.Format.Rgba8)
                image.LinearToSrgb();
            var path = Path.Combine(root, view.FileName);
            if (image.SavePng(path) != Error.Ok)
                throw new IOException($"Map-quality capture could not be written: {path}.");
            captures.Add(new(
                view.Role,
                view.FileName,
                image.GetWidth(),
                image.GetHeight(),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                view.CorridorObstructionCount));
            if (captureDoor is not null &&
                GodotObject.IsInstanceValid(captureDoor) &&
                _environmentDoors.TryGetValue(captureDoor, out doorState) &&
                doorState.IsOpen)
            {
                ToggleEnvironmentDoor(captureDoor);
                for (var frame = 0; frame < 32; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        return captures;
    }

    private MapQualityView SelectClearMapQualityView(
        string role,
        string fileName,
        Vector3 subject,
        float horizontalDistance,
        float cameraHeight,
        float targetHeight,
        float fieldOfView,
        float preferredAngleDegrees,
        IReadOnlyList<MapQualityVegetationOccluder> occluders)
    {
        var angleOffsets = new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f, 22.5f, -22.5f };
        MapQualityView? best = null;
        var bestClearance = float.MinValue;
        foreach (var angleOffset in angleOffsets)
        {
            var angle = Mathf.DegToRad(preferredAngleDegrees + angleOffset);
            var x = subject.X + (Mathf.Cos(angle) * horizontalDistance);
            var z = subject.Z + (Mathf.Sin(angle) * horizontalDistance);
            var ground = _activeArea?.World.GroundHeightAt(x, z) ?? subject.Y;
            var position = new Vector3(x, ground + cameraHeight, z);
            var target = subject + Vector3.Up * targetHeight;
            var corridor = MeasureCaptureCorridor(position, target, occluders);
            var candidate = new MapQualityView(
                role,
                fileName,
                position,
                target,
                fieldOfView,
                corridor.ObstructionCount);
            if (best is null || corridor.ObstructionCount < best.CorridorObstructionCount ||
                (corridor.ObstructionCount == best.CorridorObstructionCount &&
                 corridor.MinimumClearanceMeters > bestClearance))
            {
                best = candidate;
                bestClearance = corridor.MinimumClearanceMeters;
            }
        }
        return best!;
    }

    private static MapQualityCaptureCorridorMeasurement MeasureCaptureCorridor(
        Vector3 position,
        Vector3 target,
        IReadOnlyList<MapQualityVegetationOccluder> occluders)
    {
        var start = new Vector2(position.X, position.Z);
        var end = new Vector2(target.X, target.Z);
        var delta = end - start;
        var lengthSquared = delta.LengthSquared();
        if (lengthSquared <= .0001f) return new(0, float.MaxValue);

        var obstructionCount = 0;
        var minimumClearance = float.MaxValue;
        foreach (var occluder in occluders)
        {
            var point = new Vector2(occluder.Position.X, occluder.Position.Z);
            var t = Math.Clamp((point - start).Dot(delta) / lengthSquared, 0f, 1f);
            if (t is < .02f or > .9f) continue;
            var distance = point.DistanceTo(start + (delta * t));
            var clearance = distance - occluder.RadiusMeters;
            minimumClearance = Math.Min(minimumClearance, clearance);
            if (clearance < 0) obstructionCount++;
        }
        return new(obstructionCount, minimumClearance);
    }

    private static IReadOnlyList<MapQualityVegetationOccluder> BuildMapQualityCaptureOccluders(
        AreaWorldProfile world)
    {
        var prepared = BiomeScatterPlanBuilder.Prepare(world);
        var result = new List<MapQualityVegetationOccluder>();
        foreach (var biome in world.Biomes.Where(biome =>
                     IsVegetationBiome(biome) && !IsGroundCoverBiome(biome)))
        {
            var radius = IsTallVegetationBiome(biome) ? 2.25f : 1.5f;
            result.AddRange(prepared.Build(biome).Placements.Select(placement =>
                new MapQualityVegetationOccluder(placement.Position, radius)));
        }
        return result;
    }

    private IReadOnlyList<Vector3> MeasureBuildingSpread(Vector3 fallback)
    {
        if (_activeArea is null) return [fallback];
        return _activeArea.Entities
            .Where(entity => entity.Tags.Contains("foundation", StringComparer.OrdinalIgnoreCase))
            .Select(entity => _entityNodes.GetValueOrDefault(entity.Id.Value))
            .Where(node => node is not null && GodotObject.IsInstanceValid(node))
            .Select(node => node!.GlobalPosition)
            .ToArray();
    }

    private static Node3D? FindAuthoredVisual(Node root) =>
        root.FindChild("AuthoredVisual", recursive: true, owned: false) as Node3D;

    private static bool TryWorldVisualBottom(Node3D visual, out Vector3 bottom)
    {
        if (!TryMeasureNodeBounds(visual, out var minimum, out var maximum))
        {
            bottom = Vector3.Zero;
            return false;
        }
        var localBottom = new Vector3(
            (minimum.X + maximum.X) * .5f,
            minimum.Y,
            (minimum.Z + maximum.Z) * .5f);
        bottom = visual.GetParent() is Node3D parent ? parent.ToGlobal(localBottom) : localBottom;
        return true;
    }

    private static bool TryWorldVisualTop(Node3D visual, out Vector3 top)
    {
        if (!TryMeasureNodeBounds(visual, out var minimum, out var maximum))
        {
            top = Vector3.Zero;
            return false;
        }
        var localTop = new Vector3(
            (minimum.X + maximum.X) * .5f,
            maximum.Y,
            (minimum.Z + maximum.Z) * .5f);
        top = visual.GetParent() is Node3D parent ? parent.ToGlobal(localTop) : localTop;
        return true;
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static bool HasBiomeClassification(BiomeLayerDefinition biome, string value) =>
        biome.Tags.Contains(value, StringComparer.OrdinalIgnoreCase) ||
        biome.Name.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static bool IsGroundCoverBiome(BiomeLayerDefinition biome) =>
        new[] { "ground-cover", "grass", "flower", "meadow", "fern" }
            .Any(tag => HasBiomeClassification(biome, tag));

    private static bool IsVegetationBiome(BiomeLayerDefinition biome) =>
        new[]
        {
            "tree", "canopy", "woodland", "forest", "shrub", "bush", "understory",
            "ground-cover", "grass", "flower", "meadow", "fern",
        }.Any(tag => HasBiomeClassification(biome, tag));

    private static bool IsTallVegetationBiome(BiomeLayerDefinition biome) =>
        new[] { "tree", "canopy", "woodland", "forest" }
            .Any(tag => HasBiomeClassification(biome, tag));

    private static ScatterFocusRegion? TryReadFocusRegion(BiomeLayerDefinition biome)
    {
        const string radiusPrefix = "focus-radius:";
        const string centerPrefix = "focus-center:";
        var radiusTag = biome.Tags.FirstOrDefault(item =>
            item.StartsWith(radiusPrefix, StringComparison.OrdinalIgnoreCase));
        var centerTag = biome.Tags.FirstOrDefault(item =>
            item.StartsWith(centerPrefix, StringComparison.OrdinalIgnoreCase));
        if (radiusTag is null || centerTag is null ||
            !float.TryParse(radiusTag[radiusPrefix.Length..], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var radius) || !float.IsFinite(radius) || radius <= 0)
        {
            return null;
        }
        var components = centerTag[centerPrefix.Length..].Split(',', StringSplitOptions.TrimEntries);
        if (components.Length != 2 ||
            !float.TryParse(components[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(components[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) ||
            !float.IsFinite(x) || !float.IsFinite(z))
        {
            return null;
        }
        return new(x, z, radius);
    }

    private static string? AssistantNodeTag(EntityInstance entity) => entity.Tags.FirstOrDefault(tag =>
        tag.StartsWith("assistant-node:", StringComparison.OrdinalIgnoreCase));

    private static int ParseMapQualityNonNegativeInteger(
        IReadOnlyList<string> arguments,
        string name,
        int defaultValue) =>
        ParseMapQualityOptionalNonNegativeInteger(arguments, name) ?? defaultValue;

    private static int? ParseMapQualityOptionalNonNegativeInteger(
        IReadOnlyList<string> arguments,
        string name)
    {
        var value = GetArgumentValue(arguments, name);
        if (value is null) return null;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            throw new InvalidOperationException($"{name} must be a non-negative integer.");
        return parsed;
    }

    private static double? ParseMapQualityOptionalNonNegativeDouble(
        IReadOnlyList<string> arguments,
        string name)
    {
        var value = GetArgumentValue(arguments, name);
        if (value is null) return null;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed) || parsed < 0)
            throw new InvalidOperationException($"{name} must be a finite, non-negative number.");
        return parsed;
    }

    private sealed record MapQualityRuntimeReport(
        int SchemaVersion,
        Guid AreaId,
        string AreaSlug,
        DateTimeOffset GeneratedAtUtc,
        WorldGenerationMapQualityAuditResult MachineResult,
        IReadOnlyList<MapQualityCaptureEvidence> Captures,
        bool VisualReviewRequired);

    private sealed record MapQualityCaptureEvidence(
        string Role,
        string FileName,
        int Width,
        int Height,
        string Sha256,
        int CorridorObstructionCount);

    private sealed record MapQualityView(
        string Role,
        string FileName,
        Vector3 Position,
        Vector3 Target,
        float FieldOfView,
        int CorridorObstructionCount);

    private sealed record MapQualityPatrolMeasurement(
        int AuthoredActorCount,
        int AuditedActorCount,
        int VisualActorCount,
        int AnimatedActorCount,
        int MovingActorCount,
        int MovingSampleCount,
        double MaximumRootGroundErrorMeters,
        double MaximumVisualGroundErrorMeters,
        double MaximumSpeedErrorRatio,
        int AnimationMismatchCount);

    private sealed record MapQualityBuildingMeasurement(
        int AuthoredGroupCount,
        int AuditedGroupCount,
        int AuthoredFoundationCount,
        int AuditedFoundationCount,
        int GovernedVisualCount,
        double MaximumVisualGapMeters,
        Vector3 Focus,
        IReadOnlyList<Vector3> GroupCenters,
        IReadOnlyList<MapQualityBuildingEnvelope> Envelopes);

    private sealed record MapQualityBuildingEntranceMeasurement(
        int AuthoredEntranceCount,
        int AuditedEntranceCount,
        int ClickableDoorCount,
        int ToggleVerifiedCount,
        double MaximumDoorVisualLateralErrorMeters,
        double MaximumDoorVisualPlaneErrorMeters,
        double MaximumDoorFrameYawErrorDegrees,
        double MaximumStairVisualLateralErrorMeters,
        double MaximumStairThresholdGapMeters,
        Node3D? RepresentativeDoor);

    private readonly record struct MapQualityBuildingVegetationMeasurement(
        int EnvelopeOverlapCount,
        int TallClearanceViolationCount);

    private readonly record struct MapQualityBuildingEnvelope(
        float MinimumX,
        float MaximumX,
        float MinimumZ,
        float MaximumZ)
    {
        public bool Contains(float x, float z, float padding) =>
            x >= MinimumX - padding && x <= MaximumX + padding &&
            z >= MinimumZ - padding && z <= MaximumZ + padding;
    }

    private readonly record struct MapQualityGroundCoverMeasurement(
        bool Localized,
        int InstanceCount,
        double AreaSquareMeters)
    {
        public double DensityPerSquareMeter => AreaSquareMeters > 0 ? InstanceCount / AreaSquareMeters : 0;
    }

    private readonly record struct PatrolFrameSample(
        Vector3 Position,
        int TargetIndex,
        double WaitRemainingSeconds,
        double TargetSpeed);

    private readonly record struct ScatterFocusRegion(float X, float Z, float Radius);

    private readonly record struct MapQualityVegetationOccluder(
        Vector3Value Position,
        float RadiusMeters);

    private readonly record struct MapQualityCaptureCorridorMeasurement(
        int ObstructionCount,
        float MinimumClearanceMeters);
}
