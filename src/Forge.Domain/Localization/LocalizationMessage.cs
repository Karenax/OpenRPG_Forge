namespace Forge.Domain.Localization;

public sealed record LocalizationMessage
{
    public string Key { get; }

    public string Fallback { get; }

    public IReadOnlyDictionary<string, string> Translations { get; }

    public string Provenance { get; }

    public LocalizationMessage(
        string key,
        string fallback,
        IReadOnlyDictionary<string, string>? translations = null,
        string provenance = "author")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var normalizedKey = key.Trim();
        if (normalizedKey.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '_' or '-')))
        {
            throw new ArgumentException("Localization keys may only contain letters, digits, '.', '_' and '-'.", nameof(key));
        }

        var normalizedTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in translations ?? new Dictionary<string, string>())
        {
            var locale = LocalizationCatalog.NormalizeLocale(pair.Key);
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Value);
            if (!normalizedTranslations.TryAdd(locale, pair.Value.Trim()))
            {
                throw new ArgumentException($"Duplicate translation locale '{locale}'.", nameof(translations));
            }
        }

        Key = normalizedKey;
        Fallback = fallback.Trim();
        Translations = normalizedTranslations
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        Provenance = provenance.Trim();
    }
}
