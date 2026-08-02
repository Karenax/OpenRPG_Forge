using Forge.Domain.Blueprints;

namespace Forge.Application.Blueprints;

public sealed record BlueprintPaletteItem(
    BlueprintId BlueprintId,
    string Slug,
    string DisplayName,
    BlueprintKind Kind,
    BlueprintPaletteOrigin Origin,
    bool IsFavorite,
    bool IsReadOnly,
    BlueprintProvenance Provenance);
