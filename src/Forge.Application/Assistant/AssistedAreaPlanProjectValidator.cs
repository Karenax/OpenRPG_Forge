using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Assistant;

public static class AssistedAreaPlanProjectValidator
{
    public const string AreaMissingCode = "assistant.area_plan.area_missing";
    public const string BlueprintMissingCode = "assistant.area_plan.blueprint_missing";
    public const string LoreSourceMissingCode = "assistant.area_plan.lore_source_missing";
    public const string ExplicitExteriorPlacementCode = "assistant.area_plan.explicit_exterior_placement";
    public const string SemanticRoleMismatchCode = "assistant.area_plan.semantic_role_mismatch";

    public static IReadOnlyList<DomainDiagnostic> Validate(AssistedAreaPlanCatalog catalog, ProjectDefinition project)
    {
        var diagnostics = AssistedAreaPlanValidator.Validate(catalog).ToList();
        var areas = project.Areas.ToDictionary(item => item.Id.Value);
        var blueprints = project.Blueprints.ToDictionary(item => item.Id.Value);
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
            else if (area.Kind == Forge.Domain.Areas.AreaKind.Exterior &&
                     plan.Nodes.Any(item => item.PlacementMode == AssistedAreaPlacementMode.ExplicitTransform))
                diagnostics.Add(new(ExplicitExteriorPlacementCode,
                    "Assisted exterior plans cannot author vertical transforms; canonical terrain placement is required.",
                    $"{path}/nodes"));
            var referenced = plan.KitBlueprintIds.Concat(plan.Nodes.SelectMany(item => item.ContentBlueprintIds)).Distinct();
            foreach (var id in referenced.Where(id => !blueprints.ContainsKey(id)))
                diagnostics.Add(new(BlueprintMissingCode, $"Blueprint '{id:D}' does not exist.", $"{path}/kitBlueprintIds"));
            foreach (var node in plan.Nodes)
            {
                foreach (var id in node.ContentBlueprintIds.Where(blueprints.ContainsKey))
                {
                    var blueprint = blueprints[id];
                    if (node.Tags.Any(tag => tag is "npc" or "villager") && blueprint.Kind != BlueprintKind.Creature)
                        diagnostics.Add(new(SemanticRoleMismatchCode,
                            $"Node '{node.Label}' declares an NPC role, but blueprint '{blueprint.Slug}' is {blueprint.Kind} instead of Creature.",
                            $"{path}/nodes/{node.Id:D}/contentBlueprintIds"));
                    else if (node.Tags.Contains("character", StringComparer.Ordinal) &&
                             blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
                        diagnostics.Add(new(SemanticRoleMismatchCode,
                            $"Node '{node.Label}' declares a character role, but blueprint '{blueprint.Slug}' is not an actor blueprint.",
                            $"{path}/nodes/{node.Id:D}/contentBlueprintIds"));

                    if (!node.Tags.Contains("village-house", StringComparer.Ordinal))
                        continue;
                    var composition = blueprint.Authoring.PrefabComposition;
                    var compositionRoles = composition?.Nodes.SelectMany(item => item.Tags)
                        .ToHashSet(StringComparer.Ordinal) ?? [];
                    if (composition is null || !compositionRoles.Contains("foundation") ||
                        !compositionRoles.Contains("wall") || !compositionRoles.Contains("roof"))
                    {
                        diagnostics.Add(new(SemanticRoleMismatchCode,
                            $"Village-house node '{node.Label}' requires a prefab composition with foundation, wall and roof roles.",
                            $"{path}/nodes/{node.Id:D}/contentBlueprintIds"));
                    }
                }
            }
            foreach (var source in plan.LoreSourceIds.Where(source =>
                         !source.StartsWith("manual:", StringComparison.Ordinal) && !lore.TryGet(source, out _)))
                diagnostics.Add(new(LoreSourceMissingCode, $"Lore source '{source}' does not resolve.", $"{path}/loreSourceIds"));
        }
        return diagnostics;
    }
}
