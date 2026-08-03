namespace Forge.Domain.Localization;

public sealed record LocalizationCatalog
{
    public const int CurrentSchemaVersion = 1;

    public LocalizationCatalogId Id { get; }

    public int SchemaVersion { get; }

    public string DefaultLocale { get; }

    public IReadOnlyList<string> Locales { get; }

    public IReadOnlyList<LocalizationMessage> Messages { get; }

    public LocalizationCatalog(
        LocalizationCatalogId id,
        string defaultLocale,
        IReadOnlyList<string> locales,
        IReadOnlyList<LocalizationMessage> messages,
        int schemaVersion = CurrentSchemaVersion)
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
    }

    public string Resolve(string key, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var message = Messages.SingleOrDefault(candidate => string.Equals(candidate.Key, key.Trim(), StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Localization key '{key}' does not exist.");
        var requested = NormalizeLocale(locale);
        if (message.Translations.TryGetValue(requested, out var exact))
        {
            return exact;
        }

        var language = requested.Split('-')[0];
        var languageMatch = message.Translations.FirstOrDefault(pair =>
            string.Equals(pair.Key, language, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(languageMatch.Value))
        {
            return languageMatch.Value;
        }

        return message.Translations.TryGetValue(DefaultLocale, out var defaultText)
            ? defaultText
            : message.Fallback;
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
}
