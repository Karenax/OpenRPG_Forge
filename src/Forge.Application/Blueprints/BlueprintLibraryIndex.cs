using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed record BlueprintLibraryEntry(BlueprintId BlueprintId, string CategoryPath, string VariantFamily,
    IReadOnlyList<string> Collections, int InstanceCount, IReadOnlyList<EntityId> InstanceIds);

public sealed class BlueprintLibraryIndex
{
    public IReadOnlyList<BlueprintLibraryEntry> Entries { get; }
    public IReadOnlyList<string> CategoryPaths { get; }
    public IReadOnlyList<string> Collections { get; }

    private BlueprintLibraryIndex(IReadOnlyList<BlueprintLibraryEntry> entries)
    {
        Entries = entries;
        CategoryPaths = entries.Select(item => item.CategoryPath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Collections = entries.SelectMany(item => item.Collections).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public static BlueprintLibraryIndex Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var instances = project.Areas.SelectMany(area => area.Entities).GroupBy(entity => entity.BlueprintId)
            .ToDictionary(group => group.Key, group => group.Select(entity => entity.Id).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray());
        var entries = project.Blueprints.Select(blueprint =>
        {
            var ids = instances.GetValueOrDefault(blueprint.Id) ?? [];
            var family = VariantFamily(blueprint.Slug);
            var collections = new List<string> { $"Origine/{blueprint.PaletteOrigin}", $"Auteur/{blueprint.Provenance.Author}" };
            if (blueprint.IsFavorite) collections.Add("Favoris");
            if (!string.Equals(family, blueprint.Slug, StringComparison.Ordinal)) collections.Add($"Variantes/{family}");
            return new BlueprintLibraryEntry(blueprint.Id, $"{blueprint.PaletteOrigin}/{blueprint.Kind}", family,
                collections.Order(StringComparer.Ordinal).ToArray(), ids.Length, ids);
        }).OrderBy(entry => entry.CategoryPath, StringComparer.Ordinal).ThenBy(entry => entry.BlueprintId.ToString(), StringComparer.Ordinal).ToArray();
        return new BlueprintLibraryIndex(entries);
    }

    private static string VariantFamily(string slug)
    {
        var marker = slug.IndexOf("--", StringComparison.Ordinal);
        return marker > 0 ? slug[..marker] : slug;
    }
}
