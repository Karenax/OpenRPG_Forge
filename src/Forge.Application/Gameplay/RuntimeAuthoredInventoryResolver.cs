using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Gameplay;

public static class RuntimeAuthoredInventoryResolver
{
    public static IReadOnlyList<RuntimeInventoryStack> Resolve(
        ProjectDefinition project,
        AreaId areaId,
        EntityId? playerEntityId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(areaId);

        var area = project.Areas.SingleOrDefault(candidate => candidate.Id == areaId)
            ?? throw new KeyNotFoundException($"Area '{areaId}' is not part of the project.");
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var playerEntity = playerEntityId is null
            ? area.Entities.FirstOrDefault(entity =>
                blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
                blueprint.Kind == BlueprintKind.PlayerSpawn)
            : area.Entities.SingleOrDefault(entity => entity.Id == playerEntityId)
              ?? throw new KeyNotFoundException(
                  $"Player entity '{playerEntityId}' is not part of area '{area.Id}'.");
        if (playerEntity is null || !blueprints.TryGetValue(playerEntity.BlueprintId, out var playerBlueprint))
        {
            return [];
        }

        var containers = playerBlueprint.CreatureFoundation?.RulesComposition?.Inventory?.Containers;
        if (containers is null) return [];

        return containers
            .SelectMany(container => container.Items)
            .Select(entry =>
            {
                if (!blueprints.TryGetValue(entry.ItemBlueprintId, out var itemBlueprint))
                {
                    throw new KeyNotFoundException(
                        $"Inventory item blueprint '{entry.ItemBlueprintId}' is not part of project '{project.Id}'.");
                }

                return new RuntimeInventoryStack(itemBlueprint.Slug, entry.Quantity);
            })
            .GroupBy(stack => stack.Slug, StringComparer.Ordinal)
            .Select(group => new RuntimeInventoryStack(
                group.Key,
                group.Aggregate(0, (quantity, stack) => checked(quantity + stack.Quantity))))
            .OrderBy(stack => stack.Slug, StringComparer.Ordinal)
            .ToArray();
    }
}
