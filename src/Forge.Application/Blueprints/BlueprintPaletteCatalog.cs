using Forge.Domain.Blueprints;
using Forge.Domain.Authoring;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class BlueprintPaletteCatalog
{
    private readonly IReadOnlyList<BlueprintPaletteItem> _items;

    private BlueprintPaletteCatalog(IReadOnlyList<BlueprintPaletteItem> items)
    {
        _items = items;
    }

    public IReadOnlyList<BlueprintPaletteItem> Items => _items;

    public static BlueprintPaletteCatalog Build(ProjectDefinition project, EditorWorkspaceProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var personalFavorites = (profile?.PersonalFavoriteBlueprintIds ?? []).ToHashSet();
        var recentRanks = (profile?.RecentBlueprintIds ?? [])
            .Select((id, rank) => (id, rank))
            .ToDictionary(item => item.id, item => item.rank);
        return new BlueprintPaletteCatalog(project.Blueprints
            .Select(blueprint => new BlueprintPaletteItem(
                blueprint.Id,
                blueprint.Slug,
                blueprint.DisplayName,
                blueprint.Kind,
                blueprint.PaletteOrigin,
                blueprint.IsFavorite || personalFavorites.Contains(blueprint.Id),
                recentRanks.ContainsKey(blueprint.Id),
                recentRanks.GetValueOrDefault(blueprint.Id, int.MaxValue),
                blueprint.IsReadOnly(),
                blueprint.Provenance,
                blueprint.Authoring.Kit,
                blueprint.Authoring.Styles,
                blueprint.Authoring.Compatibility,
                blueprint.Authoring.Tags))
            .OrderBy(item => item.Origin)
            .ThenBy(item => item.DisplayName, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<BlueprintPaletteItem> Filter(
        BlueprintPaletteView view,
        string? query = null,
        EditorPaletteFilter? contextFilter = null)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var filter = contextFilter ?? EditorPaletteFilter.ShowAll;
        var filtered = _items
            .Where(item => MatchesView(item, view))
            .Where(item => MatchesQuery(item, normalizedQuery))
            .Where(item => MatchesContext(item, filter));
        return (view == BlueprintPaletteView.Recent
                ? filtered.OrderBy(item => item.RecentRank)
                : filtered)
            .ToArray();
    }

    public IReadOnlyList<BlueprintPaletteItem> FilterNamedCollection(NamedPaletteCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var ranks = collection.BlueprintIds.Select((id, rank) => (id, rank))
            .ToDictionary(item => item.id, item => item.rank);
        return _items.Where(item => ranks.ContainsKey(item.BlueprintId))
            .OrderBy(item => ranks[item.BlueprintId]).ToArray();
    }

    private static bool MatchesView(BlueprintPaletteItem item, BlueprintPaletteView view) => view switch
    {
        BlueprintPaletteView.All => true,
        BlueprintPaletteView.InstalledPacks => item.Origin == BlueprintPaletteOrigin.InstalledPack,
        BlueprintPaletteView.ProjectContent => item.Origin == BlueprintPaletteOrigin.ProjectContent,
        BlueprintPaletteView.Generated => item.Origin == BlueprintPaletteOrigin.Generated,
        BlueprintPaletteView.Favorites => item.IsFavorite,
        BlueprintPaletteView.Recent => item.IsRecent,
        BlueprintPaletteView.Temporary => item.Origin == BlueprintPaletteOrigin.Temporary,
        _ => false,
    };

    private static bool MatchesQuery(BlueprintPaletteItem item, string query) =>
        string.IsNullOrWhiteSpace(query) ||
        item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Slug.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Kind.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Provenance.Source.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Provenance.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Provenance.LicenseSpdx.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Kit.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Styles.Concat(item.Compatibility).Concat(item.Tags)
            .Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesContext(BlueprintPaletteItem item, EditorPaletteFilter filter) =>
        filter.Kinds.Contains(item.Kind) &&
        (!filter.FavoritesOnly || item.IsFavorite) &&
        MatchesAny(item.Kit, filter.Kits) &&
        MatchesAll(item.Styles, filter.Styles) &&
        MatchesAny(item.Provenance.LicenseSpdx, filter.Licenses) &&
        MatchesAll(item.Compatibility, filter.Compatibility) &&
        MatchesAll(item.Tags, filter.Tags);

    private static bool MatchesAny(string value, IReadOnlyList<string> required) =>
        required.Count == 0 || required.Any(item => value.Contains(item, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesAll(IReadOnlyList<string> values, IReadOnlyList<string> required) =>
        required.Count == 0 || required.All(item => values.Any(value =>
            value.Contains(item, StringComparison.OrdinalIgnoreCase)));
}
