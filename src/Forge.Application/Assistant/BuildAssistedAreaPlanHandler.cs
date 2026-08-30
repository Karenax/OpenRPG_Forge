using System.Security.Cryptography;
using System.Text;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Application.WorldGeneration;

namespace Forge.Application.Assistant;

public sealed class BuildAssistedAreaPlanHandler
{
    public const string RevisionConflictCode = "assistant.area_build.revision_conflict";
    public const string TransactionRequiredCode = "assistant.area_build.transaction_required";
    public const string PlanRequiredCode = "assistant.area_build.plan_required";
    public const string PlanMissingCode = "assistant.area_build.plan_missing";
    public const string ApprovalRequiredCode = "assistant.area_build.approval_required";
    public const string PermissionDeniedCode = "assistant.area_build.permission_denied";
    public const string AlreadyBuiltCode = "assistant.area_build.already_built";
    public const string PlanNotPreviewedCode = "assistant.area_build.plan_not_previewed";
    public const string PrefabInvalidCode = "assistant.area_build.prefab_invalid";
    public const string TerrainSurfaceUnavailableCode = "assistant.area_build.terrain_surface_unavailable";
    public const string TerrainFootprintUnfitCode = "assistant.area_build.terrain_footprint_unfit";
    public const string EnvironmentEvidenceMissingCode = "assistant.area_build.environment_evidence_missing";
    public const string VegetationIntentUnsatisfiedCode = "assistant.area_build.vegetation_intent_unsatisfied";
    public const string VegetationFootprintConflictCode = "assistant.area_build.vegetation_footprint_conflict";
    public const string NavigationRegressionCode = "assistant.area_build.navigation_regression";
    public const string LightingInvalidCode = "assistant.area_build.lighting_invalid";
    public const string EntranceInvalidCode = "assistant.area_build.entrance_invalid";
    private const string CommandName = "build_assisted_area_plan";
    private readonly IClock _clock;
    private readonly PlaceEntityHandler _placeEntity;
    private readonly Dictionary<Guid, (ProjectRevision Revision, Guid TransactionId, string Fingerprint)> _previews = [];

    public BuildAssistedAreaPlanHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _placeEntity = new PlaceEntityHandler(clock);
    }

    public AssistedAreaBuildResult Handle(
        ProjectState state,
        BuildAssistedAreaPlanCommand command,
        AssistedAreaPlanCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(catalog);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "Project changed before assisted construction.");
        if (command.TransactionId == Guid.Empty)
            return Failure(state, command, TransactionRequiredCode, "Assisted construction requires a transaction ID.");
        if (command.ExecutionPlanId == Guid.Empty)
            return Failure(state, command, PlanRequiredCode, "Assisted construction requires a preview plan ID.");
        var plan = catalog.Plans.SingleOrDefault(item => item.Id == command.AssistedAreaPlanId);
        if (plan is null)
            return Failure(state, command, PlanMissingCode, $"Assisted area plan '{command.AssistedAreaPlanId:D}' does not exist.");
        if (!plan.ApprovedForConstruction)
            return Failure(state, command, ApprovalRequiredCode, "Area plan requires explicit human approval before construction.");
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan)
            return Failure(state, command, PermissionDeniedCode, "Assisted construction requires ApplyApprovedPlan permission.");
        var validation = AssistedAreaPlanProjectValidator.Validate(catalog, state.Project);
        if (validation.Count > 0)
            return Failure(state, command, validation[0].Code, validation[0].Message);
        var marker = $"assistant-plan:{plan.Id:D}";
        if (state.Project.Areas.SelectMany(item => item.Entities).Any(item => item.Tags.Contains(marker, StringComparer.Ordinal)))
            return Failure(state, command, AlreadyBuiltCode, "This assisted area plan is already present in the project.");

        var areaBefore = state.Project.Areas.Single(item => item.Id.Value == plan.AreaId);
        var environmentFailure = ValidateEnvironmentIntent(areaBefore, plan, state.Project);
        if (environmentFailure is { } environmental)
            return Failure(state, command, environmental.Code, environmental.Message);
        var geometryBefore = AreaGeometryValidator.Validate(state.Project, areaBefore)
            .Select(Key).ToHashSet(StringComparer.Ordinal);
        var navBefore = AreaNavigationValidator.AnalyzeAll(state.Project, areaBefore)
            .SelectMany(item => item.Diagnostics).Select(Key).ToHashSet(StringComparer.Ordinal);
        var candidate = state;
        var placed = new List<EntityId>();
        foreach (var node in plan.Nodes.OrderBy(item => item.Id))
        {
            foreach (var blueprintValue in node.ContentBlueprintIds.Order())
            {
                var blueprintId = BlueprintId.From(blueprintValue);
                var blueprint = candidate.Project.Blueprints.Single(item => item.Id == blueprintId);
                var placementResult = ResolvePlacement(areaBefore, node, blueprint, plan.EnvironmentIntent);
                if (!placementResult.Succeeded)
                    return Failure(state, command, placementResult.Code!, placementResult.Message!);
                var placement = placementResult.Transform!;
                var assistedTags = node.Tags.Concat([
                    marker,
                    $"assistant-node:{node.Id:D}",
                    $"plan-seed:{plan.Seed}",
                    "assistant-generated",
                    node.Kind.ToString().ToLowerInvariant(),
                ]).ToArray();
                if (blueprint.Authoring.PrefabComposition is { })
                {
                    PrefabCompositionPlacementPlan prefab;
                    try
                    {
                        prefab = PrefabCompositionPlacementPlanner.Build(candidate.Project, blueprintId,
                            areaBefore.Id, placement, StableId(plan.Id, node.Id, blueprintValue));
                    }
                    catch (ArgumentException exception)
                    {
                        return Failure(state, command, PrefabInvalidCode, exception.Message);
                    }

                    foreach (var component in prefab.Entities.OrderBy(item => item.Id.Value))
                    {
                        var placeComponent = new PlaceEntityCommand(
                            CommandId.From(StableId(command.ExecutionPlanId, node.Id, component.Id.Value)),
                            candidate.Revision,
                            areaBefore.Id,
                            component.Id,
                            component.BlueprintId,
                            component.Transform,
                            dryRun: false,
                            component.Tags.Concat(assistedTags).ToArray(),
                            component.Overrides);
                        var componentResult = _placeEntity.Handle(candidate, placeComponent);
                        if (!componentResult.Succeeded)
                            return Failure(state, command, componentResult.Diagnostics[0].Code,
                                componentResult.Diagnostics[0].Message);
                        candidate = componentResult.State;
                        placed.Add(component.Id);
                    }
                    continue;
                }

                var entityId = EntityId.From(StableId(plan.Id, node.Id, blueprintValue));
                var place = new PlaceEntityCommand(
                    CommandId.From(StableId(command.ExecutionPlanId, node.Id, blueprintValue)),
                    candidate.Revision,
                    areaBefore.Id,
                    entityId,
                    blueprintId,
                    placement,
                    dryRun: false,
                    assistedTags);
                var placedResult = _placeEntity.Handle(candidate, place);
                if (!placedResult.Succeeded)
                    return Failure(state, command, placedResult.Diagnostics[0].Code, placedResult.Diagnostics[0].Message);
                candidate = placedResult.State;
                placed.Add(entityId);
            }
        }
        if (placed.Count == 0)
            return Failure(state, command, PlanMissingCode, "Approved area plan contains no placeable content.");

        var areaAfter = candidate.Project.Areas.Single(item => item.Id.Value == plan.AreaId);
        var entranceInspection = WorldGenerationBuildingEntranceInspector.Inspect(areaAfter);
        if (!entranceInspection.Passed)
        {
            var diagnostic = entranceInspection.Diagnostics[0];
            return Failure(state, command, EntranceInvalidCode,
                $"{diagnostic.Code}: {diagnostic.Message} ({diagnostic.Group}).");
        }
        var geometry = AreaGeometryValidator.Validate(candidate.Project, areaAfter)
            .Where(item => !geometryBefore.Contains(Key(item)))
            .ToArray();
        if (geometry.Length > 0)
            return Failure(state, command, geometry[0].Code, geometry[0].Message);
        var navAfter = AreaNavigationValidator.AnalyzeAll(candidate.Project, areaAfter)
            .SelectMany(item => item.Diagnostics).ToArray();
        var regressions = navAfter.Where(item => !navBefore.Contains(Key(item)) &&
            placed.Any(id => item.Path.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase))).ToArray();
        if (regressions.Length > 0)
            return Failure(state, command, NavigationRegressionCode, regressions[0].Message);
        var lightingPass = areaAfter.Environment.Lighting.AmbientIntensity +
            areaAfter.Environment.Lighting.DirectionalIntensity > 0;
        if (!lightingPass)
            return Failure(state, command, LightingInvalidCode, "Area lighting pass requires non-zero ambient or directional intensity.");

        var patch = new ProjectPatch("add", $"areas/{areaBefore.Id}/entities",
            $"Build approved assisted plan '{plan.Title}' with {placed.Count} placement commands.");
        var inverse = new ProjectPatch("remove", $"areas/{areaBefore.Id}/entities",
            $"Remove {placed.Count} entities built from assisted plan '{plan.Title}'.");
        var fingerprint = Fingerprint(plan, placed);
        if (command.DryRun)
        {
            lock (_previews)
            {
                if (_previews.Count >= 128) _previews.Clear();
                _previews[command.ExecutionPlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            return Success(state, state, command, placed, patch, inverse, navBefore.Count, navAfter.Length, lightingPass, false);
        }
        lock (_previews)
        {
            if (!_previews.TryGetValue(command.ExecutionPlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                !string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal))
                return Failure(state, command, PlanNotPreviewedCode,
                    "Assisted construction must match its successful dry-run transaction and current revision.");
            _previews.Remove(command.ExecutionPlanId);
        }
        var final = new ProjectState(candidate.Project, state.Revision.Next());
        return Success(state, final, command, placed, patch, inverse, navBefore.Count, navAfter.Length, lightingPass, true);
    }

    private AssistedAreaBuildResult Success(
        ProjectState before,
        ProjectState after,
        BuildAssistedAreaPlanCommand command,
        IReadOnlyList<EntityId> placed,
        ProjectPatch patch,
        ProjectPatch inverse,
        int navBefore,
        int navAfter,
        bool lightingPass,
        bool applied)
    {
        var core = new CommandResult(true, after, command.BaseRevision,
            applied ? after.Revision : null, [], patch, inverse,
            new AuditEntry(command.CommandId, CommandName, _clock.UtcNow, applied));
        return new(true, core, command.TransactionId, command.ExecutionPlanId, command.AssistedAreaPlanId,
            placed, $"plan:{command.AssistedAreaPlanId:D}|placements:{placed.Count}|seed:preserved|navigation:validated|lighting:validated|atomic:true",
            navBefore, navAfter, lightingPass, []);
    }

    private AssistedAreaBuildResult Failure(
        ProjectState state,
        BuildAssistedAreaPlanCommand command,
        string code,
        string message)
    {
        var diagnostic = new CommandDiagnostic(code, message);
        var core = new CommandResult(false, state, command.BaseRevision, null, [diagnostic], null, null,
            new AuditEntry(command.CommandId, CommandName, _clock.UtcNow, false));
        return new(false, core, command.TransactionId, command.ExecutionPlanId, command.AssistedAreaPlanId,
            [], "no-change", 0, 0, false, [diagnostic]);
    }

    private static string Key(DomainDiagnostic diagnostic) => $"{diagnostic.Code}|{diagnostic.Path}";

    private static PlacementResolution ResolvePlacement(
        Forge.Domain.Areas.AreaDefinition area,
        AssistedAreaPlanNode node,
        BlueprintDefinition blueprint,
        AssistedAreaEnvironmentIntent environmentIntent)
    {
        if (node.PlacementMode == AssistedAreaPlacementMode.ExplicitTransform)
        {
            return new(true, node.PlacementTransform, null, null);
        }

        var authored = node.PlacementTransform;
        if (area.Kind == Forge.Domain.Areas.AreaKind.Exterior &&
            area.World.PlacementSurface is null && !area.World.Terrain.Enabled)
            return new(false, null, TerrainSurfaceUnavailableCode,
                "Exterior assisted placement requires a canonical terrain-placement surface.");

        var surface = area.World.GroundHeightAt(node.X, node.Z);
        if (area.Kind == Forge.Domain.Areas.AreaKind.Exterior && RequiresFootprint(node, blueprint))
        {
            var inspection = TerrainPlacementFootprintInspector.Inspect(
                area, node.X, node.Z, node.Width, node.Depth);
            if (!inspection.SurfaceAvailable || !inspection.InBounds)
                return new(false, null, TerrainFootprintUnfitCode,
                    $"Structure footprint '{node.Label}' leaves the canonical terrain-placement surface.");
            if (inspection.SurfaceVariationMeters >
                environmentIntent.MaximumStructureSurfaceVariationMeters + .0001f)
                return new(false, null, TerrainFootprintUnfitCode,
                    $"Structure footprint '{node.Label}' varies by {inspection.SurfaceVariationMeters:0.###} m; " +
                    $"the approved limit is {environmentIntent.MaximumStructureSurfaceVariationMeters:0.###} m.");
            if (node.TerrainOffset > .05f)
                return new(false, null, TerrainFootprintUnfitCode,
                    $"Structure footprint '{node.Label}' cannot use a positive terrain offset in an assisted exterior plan.");
            surface = inspection.MinimumHeight;
        }

        var anchorHeight = TerrainPlacementGrounding.AnchorHeight(blueprint.Kind, surface);
        return new(true, new TransformValue(
            new Vector3Value(node.X, anchorHeight + node.TerrainOffset, node.Z), authored.Rotation, authored.Scale),
            null, null);
    }

    private static bool RequiresFootprint(AssistedAreaPlanNode node, BlueprintDefinition blueprint) =>
        blueprint.Authoring.PrefabComposition is not null || node.Tags.Any(tag => tag is
            "building" or "structure" or "village-house" or "grounding:footprint");

    private static (string Code, string Message)? ValidateEnvironmentIntent(
        Forge.Domain.Areas.AreaDefinition area,
        AssistedAreaPlanDefinition plan,
        Forge.Domain.Projects.ProjectDefinition project)
    {
        var intent = plan.EnvironmentIntent;
        var occupancy = area.World.EnvironmentOccupancy;
        if (occupancy is not null)
        {
            var blueprints = project.Blueprints.ToDictionary(item => item.Id.Value);
            foreach (var node in plan.Nodes.Where(item => RequiresFootprint(item, blueprints)))
            {
                var intersecting = occupancy.CountIntersectingFootprint(
                    "vegetation", node.X, node.Z, node.Width, node.Depth);
                if (intersecting > 0)
                    return (VegetationFootprintConflictCode,
                        $"Structure footprint '{node.Label}' intersects {intersecting} canonical vegetation instances.");
            }
        }
        if (intent.Vegetation != AssistedAreaVegetationIntent.RequirePresence)
            return null;
        if (occupancy is null)
            return (EnvironmentEvidenceMissingCode,
                "Required vegetation cannot be validated because the area has no canonical environment occupancy.");
        var contentNodes = plan.Nodes.Where(item => item.ContentBlueprintIds.Count > 0).ToArray();
        var evaluated = contentNodes.Length > 0 ? contentNodes : plan.Nodes.ToArray();
        foreach (var node in evaluated)
        {
            var count = occupancy.CountNear(
                "vegetation", node.X, node.Z, intent.EvaluationRadiusMeters);
            var density = occupancy.DensityPerHectare(
                "vegetation", node.X, node.Z, intent.EvaluationRadiusMeters);
            if (count < intent.MinimumNearbyInstances ||
                density + .0001f < intent.MinimumVegetationDensityPerHectare)
                return (VegetationIntentUnsatisfiedCode,
                    $"Approved vegetation intent for '{node.Label}' requires at least " +
                    $"{intent.MinimumNearbyInstances} nearby instances and " +
                    $"{intent.MinimumVegetationDensityPerHectare:0.##}/ha, but canonical evidence contains " +
                    $"{count} and {density:0.##}/ha.");
        }
        return null;
    }

    private static bool RequiresFootprint(
        AssistedAreaPlanNode node,
        IReadOnlyDictionary<Guid, BlueprintDefinition> blueprints) =>
        node.Tags.Any(tag => tag is "building" or "structure" or "village-house" or "grounding:footprint") ||
        node.ContentBlueprintIds.Any(id =>
            blueprints.TryGetValue(id, out var blueprint) && blueprint.Authoring.PrefabComposition is not null);

    private sealed record PlacementResolution(
        bool Succeeded,
        TransformValue? Transform,
        string? Code,
        string? Message);

    private static Guid StableId(Guid first, Guid second, Guid third)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{first:D}:{second:D}:{third:D}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static string Fingerprint(AssistedAreaPlanDefinition plan, IReadOnlyList<EntityId> placed)
    {
        var value = $"{plan.Id:D}|{plan.Seed}|{string.Join(',', placed.Select(item => item.Value).Order())}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
