using System.Text.RegularExpressions;
using Forge.Domain.Validation;

namespace Forge.Domain.Assistant;

public static partial class AssistedAreaPlanValidator
{
    public const string SchemaUnsupportedCode = "assistant.area_plan.schema_unsupported";
    public const string IdentityInvalidCode = "assistant.area_plan.identity_invalid";
    public const string ScopeInvalidCode = "assistant.area_plan.scope_invalid";
    public const string TopologyInvalidCode = "assistant.area_plan.topology_invalid";
    public const string ContentInvalidCode = "assistant.area_plan.content_invalid";
    public const string GovernanceMissingCode = "assistant.area_plan.governance_missing";

    private static readonly Regex SafeToken = SafeTokenRegex();
    private static readonly Regex SafeSource = SafeSourceRegex();

    public static IReadOnlyList<DomainDiagnostic> Validate(AssistedAreaPlanCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = new List<DomainDiagnostic>();
        if (catalog.SchemaVersion != AssistedAreaPlanCatalog.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Area-plan catalog schema is unsupported.", "assistant/area-plans/schemaVersion"));
        if (catalog.Id == Guid.Empty || catalog.Plans.Count > 256 ||
            catalog.Plans.GroupBy(item => item.Id).Any(group => group.Count() > 1))
            diagnostics.Add(new(IdentityInvalidCode, "Area-plan catalog requires a stable UUID and unique bounded plans.", "assistant/area-plans"));
        if (string.IsNullOrWhiteSpace(catalog.Provenance))
            diagnostics.Add(new(GovernanceMissingCode, "Area-plan catalog provenance is required.", "assistant/area-plans/provenance"));
        foreach (var plan in catalog.Plans) ValidatePlan(plan, diagnostics);
        return diagnostics;
    }

    private static void ValidatePlan(AssistedAreaPlanDefinition plan, List<DomainDiagnostic> diagnostics)
    {
        var path = $"assistant/area-plans/{plan.Id:D}";
        if (plan.Id == Guid.Empty || plan.AreaId == Guid.Empty ||
            plan.SchemaVersion != AssistedAreaPlanDefinition.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(plan.Title) || plan.Title.Length > 160)
            diagnostics.Add(new(IdentityInvalidCode, "Area plan identity, area reference, title or schema is invalid.", path));
        if (!float.IsFinite(plan.WidthMeters) || !float.IsFinite(plan.DepthMeters) ||
            plan.WidthMeters is < 8 or > 2048 || plan.DepthMeters is < 8 or > 2048 ||
            plan.Nodes.Count is < 2 or > 64 || plan.Links.Count > 192)
            diagnostics.Add(new(ScopeInvalidCode, "Area plan dimensions or topology scope exceed supported bounds.", path));
        if (plan.KitBlueprintIds.Count is < 1 or > 64 || plan.KitBlueprintIds.Contains(Guid.Empty) ||
            plan.Constraints.Count is < 1 or > 32 || plan.Objectives.Count is < 1 or > 32 ||
            plan.Constraints.Any(InvalidText) || plan.Objectives.Any(InvalidText))
            diagnostics.Add(new(ContentInvalidCode, "Area plan requires bounded kits, constraints and objectives.", path));
        if (plan.EstimatedCostUsd < 0 || plan.EstimatedCostUsd > 10_000 ||
            string.IsNullOrWhiteSpace(plan.ProviderKey) || !SafeToken.IsMatch(plan.ProviderKey) ||
            string.IsNullOrWhiteSpace(plan.Provenance))
            diagnostics.Add(new(GovernanceMissingCode, "Area plan provider, cost and provenance are invalid.", path));
        if (plan.LoreSourceIds.Count > 64 || plan.LoreSourceIds.Any(item => !SafeSource.IsMatch(item)))
            diagnostics.Add(new(ContentInvalidCode, "Area plan lore sources are invalid.", $"{path}/loreSourceIds"));

        var nodeIds = plan.Nodes.Select(item => item.Id).ToHashSet();
        if (nodeIds.Count != plan.Nodes.Count || plan.Nodes.Any(item =>
                item.Id == Guid.Empty || item.SchemaVersion != 1 || string.IsNullOrWhiteSpace(item.Label) ||
                item.Label.Length > 120 || !float.IsFinite(item.X) || !float.IsFinite(item.Z) ||
                !float.IsFinite(item.Width) || !float.IsFinite(item.Depth) ||
                !Enum.IsDefined(item.PlacementMode) || !float.IsFinite(item.TerrainOffset) ||
                item.TerrainOffset is < -512 or > 512 ||
                item.Width is < 2 or > 512 || item.Depth is < 2 or > 512 ||
                item.Tags.Count > 24 || item.Tags.Any(tag => !SafeToken.IsMatch(tag))))
            diagnostics.Add(new(TopologyInvalidCode, "Area-plan nodes require unique UUIDs and finite bounded geometry hints.", $"{path}/nodes"));
        if (plan.Nodes.Any(item =>
                MathF.Abs(item.X) + (item.Width / 2) > plan.WidthMeters / 2 ||
                MathF.Abs(item.Z) + (item.Depth / 2) > plan.DepthMeters / 2))
            diagnostics.Add(new(TopologyInvalidCode, "Area-plan node bounds must stay inside planned dimensions.", $"{path}/nodes"));
        if (plan.Links.Select(item => item.Id).Distinct().Count() != plan.Links.Count ||
            plan.Links.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                item.FromNodeId == item.ToNodeId || !nodeIds.Contains(item.FromNodeId) ||
                !nodeIds.Contains(item.ToNodeId) || !SafeToken.IsMatch(item.Traversal)))
            diagnostics.Add(new(TopologyInvalidCode, "Area-plan links must be unique and resolve two distinct nodes.", $"{path}/links"));
        if (nodeIds.Count > 0 && Reachable(plan, plan.Nodes[0].Id).Count != nodeIds.Count)
            diagnostics.Add(new(TopologyInvalidCode, "Area-plan topology must be connected.", $"{path}/links"));
    }

    private static HashSet<Guid> Reachable(AssistedAreaPlanDefinition plan, Guid start)
    {
        var seen = new HashSet<Guid> { start };
        var queue = new Queue<Guid>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
        {
            foreach (var link in plan.Links.Where(item => item.FromNodeId == current || item.ToNodeId == current))
            {
                var next = link.FromNodeId == current ? link.ToNodeId : link.FromNodeId;
                if (seen.Add(next)) queue.Enqueue(next);
            }
        }
        return seen;
    }

    private static bool InvalidText(string value) => string.IsNullOrWhiteSpace(value) || value.Length > 500;

    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]{1,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeTokenRegex();

    [GeneratedRegex("^(project|module|blueprint|dialogue|dialogue-node|quest|quest-state|manual):[a-z0-9._:-]{2,160}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSourceRegex();
}
