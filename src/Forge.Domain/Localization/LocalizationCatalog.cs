using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Localization;

public sealed record LocalizationCatalog
{
    public const int CurrentSchemaVersion = 1;

    public LocalizationCatalogId Id { get; }

    public int SchemaVersion { get; }

    public string DefaultLocale { get; }

    public IReadOnlyList<string> Locales { get; }

    public IReadOnlyList<LocalizationMessage> Messages { get; }

    public string PackageNamespace { get; }

    public string LicenseSpdx { get; }

    public string Provenance { get; }

    public long PlatformByteBudget { get; }

    public string ContentSha256 { get; }

    public LocalizationCatalog(
        LocalizationCatalogId id,
        string defaultLocale,
        IReadOnlyList<string> locales,
        IReadOnlyList<LocalizationMessage> messages,
        int schemaVersion = CurrentSchemaVersion,
        string packageNamespace = "project.localization",
        string licenseSpdx = "CC0-1.0",
        string provenance = "project-author",
        long platformByteBudget = 4 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(locales);
        ArgumentNullException.ThrowIfNull(messages);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), $"Localization schema '{schemaVersion}' is not supported.");
        }

        var normalizedDefault = NormalizeLocale(defaultLocale);
        var normalizedLocales = locales.Select(NormalizeLocale)
            .Append(normalizedDefault)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var materializedMessages = messages.OrderBy(message => message.Key, StringComparer.Ordinal).ToArray();
        if (materializedMessages.Select(message => message.Key).Distinct(StringComparer.Ordinal).Count() != materializedMessages.Length)
        {
            throw new ArgumentException("Localization message keys must be unique.", nameof(messages));
        }

        var unsupportedLocale = materializedMessages.SelectMany(message => message.Translations.Keys)
            .FirstOrDefault(locale => !normalizedLocales.Contains(locale, StringComparer.OrdinalIgnoreCase));
        if (unsupportedLocale is not null)
        {
            throw new ArgumentException($"Translation locale '{unsupportedLocale}' is not declared by the catalog.", nameof(messages));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        DefaultLocale = normalizedDefault;
        Locales = normalizedLocales;
        Messages = materializedMessages;
        PackageNamespace = (packageNamespace ?? string.Empty).Trim().ToLowerInvariant();
        LicenseSpdx = (licenseSpdx ?? string.Empty).Trim();
        Provenance = (provenance ?? string.Empty).Trim();
        PlatformByteBudget = platformByteBudget;
        ContentSha256 = ComputeContentSha256(
            Id,
            SchemaVersion,
            DefaultLocale,
            Locales,
            Messages,
            PackageNamespace,
            LicenseSpdx,
            Provenance,
            PlatformByteBudget);
    }

    public string Resolve(string key, string locale)
        => Resolve(key, locale, new Dictionary<string, object?>());

    public string Resolve(string key, string locale, IReadOnlyDictionary<string, object?> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(arguments);
        var message = Messages.SingleOrDefault(candidate => string.Equals(candidate.Key, key.Trim(), StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Localization key '{key}' does not exist.");
        var requested = NormalizeLocale(locale);
        string resolved;
        if (message.Translations.TryGetValue(requested, out var exact))
        {
            resolved = exact;
            return LocalizationMessageFormatter.Format(resolved, arguments, requested);
        }

        var language = requested.Split('-')[0];
        var languageMatch = message.Translations.FirstOrDefault(pair =>
            string.Equals(pair.Key, language, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(languageMatch.Value))
        {
            resolved = languageMatch.Value;
            return LocalizationMessageFormatter.Format(resolved, arguments, requested);
        }

        resolved = message.Translations.TryGetValue(DefaultLocale, out var defaultText)
            ? defaultText
            : message.Fallback;
        return LocalizationMessageFormatter.Format(resolved, arguments, requested);
    }

    public static LocalizationCatalog CreateEmpty(Guid moduleId, string defaultLocale) => new(
        LocalizationCatalogId.From(moduleId),
        defaultLocale,
        [defaultLocale],
        []);

    public static string NormalizeLocale(string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        var segments = locale.Trim().Split('-');
        if (segments.Length is < 1 or > 2 || segments[0].Length is < 2 or > 3 || !segments[0].All(char.IsLetter) ||
            segments.Length == 2 && (segments[1].Length is not (2 or 4) || !segments[1].All(char.IsLetter)))
        {
            throw new ArgumentException($"Locale '{locale}' must use a language tag such as 'fr' or 'fr-FR'.", nameof(locale));
        }

        return segments.Length == 1
            ? segments[0].ToLowerInvariant()
            : $"{segments[0].ToLowerInvariant()}-{(segments[1].Length == 2 ? segments[1].ToUpperInvariant() : char.ToUpperInvariant(segments[1][0]) + segments[1][1..].ToLowerInvariant())}";
    }

    private static string ComputeContentSha256(
        LocalizationCatalogId id,
        int schemaVersion,
        string defaultLocale,
        IReadOnlyList<string> locales,
        IReadOnlyList<LocalizationMessage> messages,
        string packageNamespace,
        string licenseSpdx,
        string provenance,
        long platformByteBudget)
    {
        var canonical = new StringBuilder()
            .Append(id).Append('|').Append(schemaVersion).Append('|').Append(defaultLocale).Append('|')
            .Append(string.Join(',', locales)).Append('|').Append(packageNamespace).Append('|')
            .Append(licenseSpdx).Append('|').Append(provenance).Append('|').Append(platformByteBudget);
        foreach (var message in messages)
        {
            canonical.Append('\n').Append(message.Key).Append('|').Append(message.Fallback).Append('|')
                .Append(message.Provenance);
            foreach (var translation in message.Translations)
            {
                canonical.Append('|').Append(translation.Key).Append('=').Append(translation.Value);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }
}
