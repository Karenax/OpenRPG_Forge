using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

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
    public const string NavigationRegressionCode = "assistant.area_build.navigation_regression";
    public const string LightingInvalidCode = "assistant.area_build.lighting_invalid";
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
                var entityId = EntityId.From(StableId(plan.Id, node.Id, blueprintValue));
                var place = new PlaceEntityCommand(
                    CommandId.From(StableId(command.ExecutionPlanId, node.Id, blueprintValue)),
                    candidate.Revision,
                    areaBefore.Id,
                    entityId,
                    BlueprintId.From(blueprintValue),
                    ResolvePlacement(areaBefore, node),
                    dryRun: false,
                    [marker, $"assistant-node:{node.Id:D}", $"plan-seed:{plan.Seed}",
                        "assistant-generated", node.Kind.ToString().ToLowerInvariant()]);
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

    private static TransformValue ResolvePlacement(
        Forge.Domain.Areas.AreaDefinition area,
        AssistedAreaPlanNode node)
    {
        if (node.PlacementMode == AssistedAreaPlacementMode.ExplicitTransform)
        {
            return node.PlacementTransform;
        }

        var authored = node.PlacementTransform;
        var surface = area.World.Terrain.Enabled
            ? area.World.Terrain.HeightAt(authored.Position.X, authored.Position.Z)
            : 0;
        return new TransformValue(
            new Vector3Value(authored.Position.X, surface + node.TerrainOffset, authored.Position.Z),
            authored.Rotation,
            authored.Scale);
    }

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
