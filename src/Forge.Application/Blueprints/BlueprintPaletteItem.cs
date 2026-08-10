using Forge.Domain.Blueprints;

namespace Forge.Application.Blueprints;

public sealed record BlueprintPaletteItem(
    BlueprintId BlueprintId,
    string Slug,
    string DisplayName,
    BlueprintKind Kind,
    BlueprintPaletteOrigin Origin,
    bool IsFavorite,
    bool IsRecent,
    int RecentRank,
    bool IsReadOnly,
    BlueprintProvenance Provenance,
    string Kit,
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Compatibility,
    IReadOnlyList<string> Tags);
