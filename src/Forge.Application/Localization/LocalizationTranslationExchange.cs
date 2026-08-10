using Forge.Domain.Localization;

namespace Forge.Application.Localization;

public sealed record LocalizationTranslationUnit(
    string Key,
    string Source,
    string Translation,
    string Provenance);

public sealed record LocalizationTranslationBundle(
    int SchemaVersion,
    Guid CatalogId,
    string CatalogContentSha256,
    string SourceLocale,
    string TargetLocale,
    string PackageNamespace,
    string LicenseSpdx,
    IReadOnlyList<LocalizationTranslationUnit> Units)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record LocalizationTranslationImportResult(
    LocalizationCatalog? Catalog,
    IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Catalog is not null && Diagnostics.Count == 0;
}

public static class LocalizationTranslationExchange
{
    public const string SchemaCode = "localization.exchange.schema_unsupported";
    public const string IdentityCode = "localization.exchange.catalog_mismatch";
    public const string HashCode = "localization.exchange.catalog_stale";
    public const string LocaleCode = "localization.exchange.locale_invalid";
    public const string KeyCode = "localization.exchange.key_unknown";
    public const string DuplicateCode = "localization.exchange.key_duplicate";
    public const string MessageCode = "localization.exchange.message_invalid";

    public static LocalizationTranslationBundle Export(LocalizationCatalog catalog, string targetLocale)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var normalizedLocale = LocalizationCatalog.NormalizeLocale(targetLocale);
        return new LocalizationTranslationBundle(
            LocalizationTranslationBundle.CurrentSchemaVersion,
            catalog.Id.Value,
            catalog.ContentSha256,
            catalog.DefaultLocale,
            normalizedLocale,
            catalog.PackageNamespace,
            catalog.LicenseSpdx,
            catalog.Messages.Select(message => new LocalizationTranslationUnit(
                message.Key,
                message.Translations.GetValueOrDefault(catalog.DefaultLocale, message.Fallback),
                message.Translations.GetValueOrDefault(normalizedLocale, string.Empty),
                message.Provenance)).ToArray());
    }

    public static LocalizationTranslationImportResult Import(
        LocalizationCatalog catalog,
        LocalizationTranslationBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(bundle);
        var diagnostics = new List<string>();
        if (bundle.SchemaVersion != LocalizationTranslationBundle.CurrentSchemaVersion)
            diagnostics.Add(SchemaCode);
        if (bundle.CatalogId != catalog.Id.Value ||
            !string.Equals(bundle.PackageNamespace, catalog.PackageNamespace, StringComparison.Ordinal) ||
            !string.Equals(bundle.LicenseSpdx, catalog.LicenseSpdx, StringComparison.Ordinal))
            diagnostics.Add(IdentityCode);
        if (!string.Equals(bundle.CatalogContentSha256, catalog.ContentSha256, StringComparison.Ordinal))
            diagnostics.Add(HashCode);

        string targetLocale;
        try
        {
            targetLocale = LocalizationCatalog.NormalizeLocale(bundle.TargetLocale);
        }
        catch (ArgumentException)
        {
            diagnostics.Add(LocaleCode);
            targetLocale = catalog.DefaultLocale;
        }

        var units = new Dictionary<string, LocalizationTranslationUnit>(StringComparer.Ordinal);
        foreach (var unit in bundle.Units ?? [])
        {
            if (!units.TryAdd(unit.Key, unit)) diagnostics.Add($"{DuplicateCode}:{unit.Key}");
            if (!catalog.Messages.Any(message => message.Key == unit.Key)) diagnostics.Add($"{KeyCode}:{unit.Key}");
            if (string.IsNullOrWhiteSpace(unit.Translation) || !LocalizationMessageFormatter.IsValid(unit.Translation))
                diagnostics.Add($"{MessageCode}:{unit.Key}");
        }

        if (diagnostics.Count > 0) return new(null, diagnostics.Order(StringComparer.Ordinal).ToArray());
        var messages = catalog.Messages.Select(message =>
        {
            if (!units.TryGetValue(message.Key, out var unit)) return message;
            var translations = message.Translations.ToDictionary(item => item.Key, item => item.Value,
                StringComparer.OrdinalIgnoreCase);
            translations[targetLocale] = unit.Translation.Trim();
            return new LocalizationMessage(message.Key, message.Fallback, translations, message.Provenance);
        }).ToArray();
        return new LocalizationTranslationImportResult(
            new LocalizationCatalog(catalog.Id, catalog.DefaultLocale, [.. catalog.Locales, targetLocale], messages,
                catalog.SchemaVersion, catalog.PackageNamespace, catalog.LicenseSpdx, catalog.Provenance,
                catalog.PlatformByteBudget),
            []);
    }
}
