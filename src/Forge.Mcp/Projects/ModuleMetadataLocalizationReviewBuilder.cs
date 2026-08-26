using Forge.Application.Localization;
using Forge.Application.Quality;
using Forge.Domain.Projects;

namespace Forge.Mcp.Projects;

internal static class ModuleMetadataLocalizationReviewBuilder
{
    internal const int CharacterBudget = 96;
    private const double ExpansionFactor = 1.35;

    internal static IReadOnlyList<ModuleMetadataLocalizationReview> Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var catalog = LocalizationCatalogExtractor.Extract(project).Catalog;
        var quality = LocalizationQualityService.Analyze(catalog, CharacterBudget, ExpansionFactor);
        var pseudo = quality.PseudoSnapshots.ToDictionary(item => item.MessageKey, StringComparer.Ordinal);
        return catalog.Messages.Where(item => item.Key.StartsWith("module.", StringComparison.Ordinal))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .SelectMany(message => catalog.Locales.Order(StringComparer.OrdinalIgnoreCase).Select(locale =>
            {
                var text = message.Translations.TryGetValue(locale, out var translated)
                    ? translated
                    : message.Fallback;
                var estimated = (int)Math.Ceiling(text.Length * ExpansionFactor);
                var snapshot = pseudo[message.Key];
                var codes = quality.Issues.Where(item => item.MessageKey == message.Key &&
                                                         string.Equals(item.Locale, locale,
                                                             StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                    .ToArray();
                return new ModuleMetadataLocalizationReview(message.Key, locale, text, estimated,
                    CharacterBudget, estimated > CharacterBudget, snapshot.Text, snapshot.EstimatedCharacters,
                    snapshot.OverflowRisk, codes);
            })).ToArray();
    }
}
