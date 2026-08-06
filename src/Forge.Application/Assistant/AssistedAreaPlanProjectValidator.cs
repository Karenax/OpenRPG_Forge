using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Assistant;

public static class AssistedAreaPlanProjectValidator
{
    public const string AreaMissingCode = "assistant.area_plan.area_missing";
    public const string BlueprintMissingCode = "assistant.area_plan.blueprint_missing";
    public const string LoreSourceMissingCode = "assistant.area_plan.lore_source_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(AssistedAreaPlanCatalog catalog, ProjectDefinition project)
    {
        var diagnostics = AssistedAreaPlanValidator.Validate(catalog).ToList();
        var areas = project.Areas.ToDictionary(item => item.Id.Value);
        var blueprints = project.Blueprints.Select(item => item.Id.Value).ToHashSet();
        var lore = LoreProjectSourceCatalog.Build(project);
        foreach (var plan in catalog.Plans)
        {
            var path = $"assistant/area-plans/{plan.Id:D}";
            if (!areas.TryGetValue(plan.AreaId, out var area))
                diagnostics.Add(new(AreaMissingCode, $"Area '{plan.AreaId:D}' does not exist.", $"{path}/areaId"));
            else if (plan.WidthMeters > area.Dimensions.Width || plan.DepthMeters > area.Dimensions.Depth)
                diagnostics.Add(new(AssistedAreaPlanValidator.ScopeInvalidCode,
                    $"Plan dimensions {plan.WidthMeters:0.##} × {plan.DepthMeters:0.##} exceed area dimensions {area.Dimensions.Width} × {area.Dimensions.Depth}.",
                    $"{path}/dimensions"));
            var referenced = plan.KitBlueprintIds.Concat(plan.Nodes.SelectMany(item => item.ContentBlueprintIds)).Distinct();
            foreach (var id in referenced.Where(id => !blueprints.Contains(id)))
                diagnostics.Add(new(BlueprintMissingCode, $"Blueprint '{id:D}' does not exist.", $"{path}/kitBlueprintIds"));
            foreach (var source in plan.LoreSourceIds.Where(source =>
                         !source.StartsWith("manual:", StringComparison.Ordinal) && !lore.TryGet(source, out _)))
                diagnostics.Add(new(LoreSourceMissingCode, $"Lore source '{source}' does not resolve.", $"{path}/loreSourceIds"));
        }
        return diagnostics;
    }
}
