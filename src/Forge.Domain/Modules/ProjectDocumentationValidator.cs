using Forge.Domain.Validation;

namespace Forge.Domain.Modules;

public static class ProjectDocumentationValidator
{
    public const string PageTooLargeCode = "project.documentation.page_too_large";
    public const string LocaleInvalidCode = "project.documentation.locale_invalid";
    public const int MaximumPageCharacters = 65_536;

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var localization in metadata.Localizations)
        {
            var root = $"module/metadata/localizations/{localization.Locale}";
            if (string.IsNullOrWhiteSpace(localization.Locale) || string.IsNullOrWhiteSpace(localization.Name))
                diagnostics.Add(new(LocaleInvalidCode,
                    "Localized player documentation requires a locale and public module name.", root));
            foreach (var page in Pages(localization).Where(page => page.Value.Length > MaximumPageCharacters))
                diagnostics.Add(new(PageTooLargeCode,
                    $"Localized player documentation page '{page.Key}' exceeds {MaximumPageCharacters} characters.",
                    $"{root}/{page.Key}"));
        }

        return diagnostics;
    }

    public static IReadOnlyDictionary<string, string> Pages(ProjectLocalizedMetadata localization) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["synopsis"] = localization.Description,
            ["installation"] = localization.Installation,
            ["rules"] = localization.Rules,
            ["controls"] = localization.Controls,
            ["credits"] = localization.Credits,
            ["licenses"] = localization.Licenses,
            ["warnings"] = localization.Warnings,
            ["compatibility"] = localization.Compatibility,
        };
}
