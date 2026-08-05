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
                blueprint.Provenance))
            .OrderBy(item => item.Origin)
            .ThenBy(item => item.DisplayName, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<BlueprintPaletteItem> Filter(
        BlueprintPaletteView view,
        string? query = null)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var filtered = _items
            .Where(item => MatchesView(item, view))
            .Where(item => MatchesQuery(item, normalizedQuery));
        return (view == BlueprintPaletteView.Recent
                ? filtered.OrderBy(item => item.RecentRank)
                : filtered)
            .ToArray();
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
        item.Provenance.LicenseSpdx.Contains(query, StringComparison.OrdinalIgnoreCase);
}
