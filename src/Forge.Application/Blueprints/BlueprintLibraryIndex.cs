using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed record BlueprintLibraryEntry(BlueprintId BlueprintId, string CategoryPath, string VariantFamily,
    IReadOnlyList<string> Collections, int InstanceCount, IReadOnlyList<EntityId> InstanceIds,
    IReadOnlyList<string>? CustomCategoryPaths = null);

public sealed class BlueprintLibraryIndex
{
    public IReadOnlyList<BlueprintLibraryEntry> Entries { get; }
    public IReadOnlyList<string> CategoryPaths { get; }
    public IReadOnlyList<string> Collections { get; }

    private BlueprintLibraryIndex(IReadOnlyList<BlueprintLibraryEntry> entries)
    {
        Entries = entries;
        CategoryPaths = entries.SelectMany(item => new[] { item.CategoryPath }
                .Concat((item.CustomCategoryPaths ?? []).SelectMany(ExpandCategoryPath)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Collections = entries.SelectMany(item => item.Collections).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public static BlueprintLibraryIndex Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);
        var instances = project.Areas.SelectMany(area => area.Entities).GroupBy(entity => entity.BlueprintId)
            .ToDictionary(group => group.Key, group => group.Select(entity => entity.Id).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray());
        var entries = project.Blueprints.Select(blueprint =>
        {
            var ids = instances.GetValueOrDefault(blueprint.Id) ?? [];
            var family = blueprint.Authoring.PrefabVariant is { } variant &&
                         blueprints.TryGetValue(variant.BaseBlueprintId, out var variantBase)
                ? variantBase.Slug
                : VariantFamily(blueprint.Slug);
            var collections = new List<string> { $"Origine/{blueprint.PaletteOrigin}", $"Auteur/{blueprint.Provenance.Author}" };
            if (blueprint.IsFavorite) collections.Add("Favoris");
            if (!string.Equals(family, blueprint.Slug, StringComparison.Ordinal)) collections.Add($"Variantes/{family}");
            collections.AddRange(blueprint.Authoring.Tags.Select(tag => $"Tags/{tag}"));
            collections.AddRange(blueprint.Authoring.Styles.Select(style => $"Styles/{style}"));
            collections.AddRange(blueprint.Authoring.Compatibility.Select(value => $"Compatibilité/{value}"));
            collections.AddRange(blueprint.Authoring.AuthorCollections.Select(value => $"Collections/{value}"));
            return new BlueprintLibraryEntry(blueprint.Id, $"{blueprint.PaletteOrigin}/{blueprint.Kind}", family,
                collections.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), ids.Length, ids,
                blueprint.Authoring.CategoryPaths);
        }).OrderBy(entry => entry.CategoryPath, StringComparer.Ordinal).ThenBy(entry => entry.BlueprintId.ToString(), StringComparer.Ordinal).ToArray();
        return new BlueprintLibraryIndex(entries);
    }

    private static IEnumerable<string> ExpandCategoryPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var length = 1; length <= segments.Length; length++)
            yield return string.Join('/', segments.Take(length));
    }

    private static string VariantFamily(string slug)
    {
        var marker = slug.IndexOf("--", StringComparison.Ordinal);
        return marker > 0 ? slug[..marker] : slug;
    }
}
