using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed record BlueprintPaletteScriptHandle(
    BlueprintId BlueprintId,
    string Slug,
    string Version,
    IReadOnlyList<string> CategoryPaths,
    IReadOnlyList<string> Tags,
    BlueprintId? VariantBaseBlueprintId,
    int CompositionNodeCount,
    IReadOnlyList<string> AuthorCollections,
    int AssetBudgetPlatformCount);

public sealed record BlueprintInstanceRuntimeSnapshot(
    EntityId EntityId,
    BlueprintPaletteScriptHandle? Blueprint,
    IReadOnlyList<EntityPropertyOverride> EffectiveProperties,
    IReadOnlyList<string> Diagnostics);

public sealed class BlueprintPaletteScriptService
{
    public const string RequiredBlueprintMissingCode = "blueprint.runtime.required_missing";
    private readonly ProjectDefinition _project;

    public BlueprintPaletteScriptService(ProjectDefinition project) =>
        _project = project ?? throw new ArgumentNullException(nameof(project));

    public IReadOnlyList<BlueprintPaletteScriptHandle> Query(string? search = null, int limit = 100)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        var value = search?.Trim() ?? string.Empty;
        return _project.Blueprints.Where(item => string.IsNullOrEmpty(value) ||
                item.Id.ToString().Contains(value, StringComparison.OrdinalIgnoreCase) ||
                item.Slug.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                item.DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                item.Authoring.Tags.Any(tag => tag.Contains(value, StringComparison.OrdinalIgnoreCase)) ||
                item.Authoring.CategoryPaths.Any(path => path.Contains(value, StringComparison.OrdinalIgnoreCase)) ||
                item.Authoring.AuthorCollections.Any(path =>
                    path.Contains(value, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Slug, StringComparer.Ordinal)
            .Take(limit)
            .Select(Handle)
            .ToArray();
    }

    public BlueprintInstanceRuntimeSnapshot Resolve(EntityId entityId, bool blueprintRequired)
    {
        var entity = _project.Areas.SelectMany(area => area.Entities).FirstOrDefault(item => item.Id == entityId);
        if (entity is null)
            return new(entityId, null, [], blueprintRequired
                ? [$"{RequiredBlueprintMissingCode}: entities/{entityId}"] : []);
        var blueprint = _project.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
        if (blueprint is null)
            return new(entityId, null, entity.Overrides.Properties, blueprintRequired
                ? [$"{RequiredBlueprintMissingCode}: entities/{entityId}/blueprintId"] : []);
        var effective = blueprint.Authoring.PromotedDefaults
            .Where(item => entity.Overrides.Properties.All(value => value.Key != item.Key))
            .Concat(entity.Overrides.Properties)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        return new(entityId, Handle(blueprint), effective, []);
    }

    private static BlueprintPaletteScriptHandle Handle(BlueprintDefinition blueprint) =>
        new(blueprint.Id, blueprint.Slug, blueprint.Authoring.Version, blueprint.Authoring.CategoryPaths,
            blueprint.Authoring.Tags, blueprint.Authoring.PrefabVariant?.BaseBlueprintId,
            blueprint.Authoring.PrefabComposition?.Nodes.Count ?? 0, blueprint.Authoring.AuthorCollections,
            blueprint.Authoring.AssetBudget?.Platforms.Count ?? 0);
}
