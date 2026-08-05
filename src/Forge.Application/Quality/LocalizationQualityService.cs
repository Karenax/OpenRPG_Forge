using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Localization;
using Forge.Domain.Localization;

namespace Forge.Application.Quality;

public enum LocalizationQualitySeverity { Information, Warning, Error }

public sealed record LocalizationQualityIssue(
    LocalizationQualitySeverity Severity,
    string Code,
    string MessageKey,
    string Locale,
    string Detail);

public sealed record LocalizationLocaleCoverage(
    string Locale,
    int Covered,
    int Total,
    double Ratio);

public sealed record PseudoLocalizationSnapshot(
    string MessageKey,
    string Text,
    int EstimatedCharacters,
    bool OverflowRisk);

public sealed record LocalizationQualityReport(
    bool Passed,
    IReadOnlyList<LocalizationLocaleCoverage> Coverage,
    IReadOnlyList<LocalizationQualityIssue> Issues,
    IReadOnlyList<PseudoLocalizationSnapshot> PseudoSnapshots);

public static partial class LocalizationQualityService
{
    public static LocalizationQualityReport Analyze(
        LocalizationCatalog catalog,
        int maximumCharacters = 96,
        double expansionFactor = 1.35)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (maximumCharacters is < 16 or > 4_096 || !double.IsFinite(expansionFactor) || expansionFactor is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));

        var issues = new List<LocalizationQualityIssue>();
        var coverage = new List<LocalizationLocaleCoverage>();
        foreach (var locale in catalog.Locales.Order(StringComparer.OrdinalIgnoreCase))
        {
            var covered = 0;
            foreach (var message in catalog.Messages)
            {
                var usesFallback = !message.Translations.TryGetValue(locale, out var translated);
                var text = usesFallback ? message.Fallback : translated!;
                if (!usesFallback || string.Equals(locale, catalog.DefaultLocale, StringComparison.OrdinalIgnoreCase)) covered++;
                else issues.Add(new(LocalizationQualitySeverity.Warning, "QA_LOCALIZATION_MISSING", message.Key,
                    locale, "Declared locale has no translation."));

                var expectedPlaceholders = Placeholders(message.Fallback);
                var actualPlaceholders = Placeholders(text);
                if (!expectedPlaceholders.SetEquals(actualPlaceholders))
                    issues.Add(new(LocalizationQualitySeverity.Error, "QA_LOCALIZATION_PLACEHOLDER_MISMATCH",
                        message.Key, locale,
                        $"Expected [{string.Join(',', expectedPlaceholders.Order())}], actual [{string.Join(',', actualPlaceholders.Order())}]."));

                if (UsesPlural(message.Fallback))
                {
                    var required = RequiredPluralCategories(locale);
                    var actual = PluralCategories(text);
                    foreach (var category in required.Where(category => !actual.Contains(category)))
                        issues.Add(new(LocalizationQualitySeverity.Error, "QA_LOCALIZATION_PLURAL_MISSING",
                            message.Key, locale, $"Plural category '{category}' is missing."));
                }

                var estimated = (int)Math.Ceiling(text.Length * expansionFactor);
                if (estimated > maximumCharacters)
                    issues.Add(new(LocalizationQualitySeverity.Warning, "QA_LOCALIZATION_OVERFLOW_RISK",
                        message.Key, locale, $"Estimated {estimated} characters exceeds the {maximumCharacters} character budget."));
            }

            var total = catalog.Messages.Count;
            coverage.Add(new(locale, covered, total, total == 0 ? 1 : (double)covered / total));
        }

        var snapshots = catalog.Messages.OrderBy(message => message.Key, StringComparer.Ordinal)
            .Select(message =>
            {
                var text = PseudoLocalization.Transform(message.Fallback);
                var estimated = (int)Math.Ceiling(text.Length * expansionFactor);
                return new PseudoLocalizationSnapshot(message.Key, text, estimated, estimated > maximumCharacters);
            }).ToArray();
        return new LocalizationQualityReport(
            issues.All(issue => issue.Severity != LocalizationQualitySeverity.Error),
            coverage,
            issues.OrderBy(issue => issue.MessageKey, StringComparer.Ordinal).ThenBy(issue => issue.Locale, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal).ToArray(),
            snapshots);
    }

    public static string RenderPseudoSnapshotSvg(LocalizationQualityReport report, int maximumRows = 12)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (maximumRows is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumRows));
        var rows = report.PseudoSnapshots.Take(maximumRows).ToArray();
        var height = 88 + rows.Length * 38;
        var builder = new StringBuilder()
            .AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1280\" height=\"{height}\" viewBox=\"0 0 1280 {height}\">")
            .AppendLine("<rect width=\"1280\" height=\"100%\" fill=\"#0d0918\"/>")
            .AppendLine("<text x=\"32\" y=\"44\" fill=\"#ff9b61\" font-family=\"sans-serif\" font-size=\"24\" font-weight=\"700\">OPENRPG FORGE · PSEUDO-LOCALISATION</text>");
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var y = 82 + index * 38;
            var color = row.OverflowRisk ? "#ff756d" : "#cfc3e6";
            builder.AppendLine($"<text x=\"32\" y=\"{y}\" fill=\"#9d8bbd\" font-family=\"monospace\" font-size=\"14\">{Escape(row.MessageKey)}</text>");
            builder.AppendLine($"<text x=\"360\" y=\"{y}\" fill=\"{color}\" font-family=\"sans-serif\" font-size=\"16\">{Escape(row.Text)}</text>");
        }
        return builder.AppendLine("</svg>").ToString();
    }

    private static HashSet<string> Placeholders(string value) => PlaceholderRegex().Matches(value)
        .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static bool UsesPlural(string value) => value.Contains(", plural,", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> PluralCategories(string value) => PluralCategoryRegex().Matches(value)
        .Select(match => match.Groups[1].Value.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> RequiredPluralCategories(string locale) => locale.Split('-')[0] switch
    {
        "ar" => new HashSet<string>(["zero", "one", "two", "few", "many", "other"], StringComparer.Ordinal),
        "cs" or "pl" or "ru" or "uk" => new HashSet<string>(["one", "few", "many", "other"], StringComparer.Ordinal),
        _ => new HashSet<string>(["one", "other"], StringComparer.Ordinal),
    };

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;

    [GeneratedRegex(@"\{\s*([A-Za-z_][A-Za-z0-9_.-]*)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"\b(zero|one|two|few|many|other)\s*\{", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex PluralCategoryRegex();
}
