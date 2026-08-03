using Forge.Domain.Localization;

namespace Forge.Application.Localization;

public sealed record LocalizationExtractionResult(
    LocalizationCatalog Catalog,
    IReadOnlyList<string> Diagnostics);
