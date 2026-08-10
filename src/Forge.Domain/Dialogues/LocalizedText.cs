using System.Text.Json.Serialization;
using Forge.Domain.Localization;

namespace Forge.Domain.Dialogues;

public enum DialoguePluralCategory
{
    Unspecified,
    Zero,
    One,
    Two,
    Few,
    Many,
    Other,
}

public sealed record LocalizedTextVariant
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public string Locale { get; }

    public string Gender { get; }

    public DialoguePluralCategory PluralCategory { get; }

    public string Text { get; }

    [JsonConstructor]
    public LocalizedTextVariant(
        Guid id,
        int schemaVersion,
        string provenance,
        string locale,
        string gender,
        DialoguePluralCategory pluralCategory,
        string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Locale = LocalizationCatalog.NormalizeLocale(locale);
        Gender = NormalizeDimension(gender, "any");
        if (!Enum.IsDefined(pluralCategory))
            throw new ArgumentOutOfRangeException(nameof(pluralCategory));
        Id = id == Guid.Empty
            ? DialogueParticipant.StableId($"localized-variant:{Locale}:{Gender}:{pluralCategory}")
            : id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        PluralCategory = pluralCategory;
        Text = text.Trim();
    }

    public static LocalizedTextVariant Create(
        string textKey,
        string locale,
        string text,
        string gender = "any",
        DialoguePluralCategory pluralCategory = DialoguePluralCategory.Other,
        string provenance = "author")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textKey);
        var normalizedLocale = LocalizationCatalog.NormalizeLocale(locale);
        var normalizedGender = NormalizeDimension(gender, "any");
        return new(
            DialogueParticipant.StableId(
                $"localized-variant:{textKey.Trim()}:{normalizedLocale}:{normalizedGender}:{pluralCategory}"),
            CurrentSchemaVersion,
            provenance,
            normalizedLocale,
            normalizedGender,
            pluralCategory,
            text);
    }

    private static string NormalizeDimension(string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
        if (normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            throw new ArgumentException("Localization dimensions may only contain letters, digits, '-' and '_'.");
        return normalized;
    }
}

public sealed record LocalizedText
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public string Key { get; }

    public string Fallback { get; }

    public IReadOnlyList<LocalizedTextVariant> Variants { get; }

    [JsonConstructor]
    public LocalizedText(
        Guid id,
        int schemaVersion,
        string provenance,
        string key,
        string fallback,
        IReadOnlyList<LocalizedTextVariant>? variants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        var normalizedKey = key.Trim();
        if (normalizedKey.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '_' or '-')))
            throw new ArgumentException("Localization keys may only contain letters, digits, '.', '_' and '-'.", nameof(key));
        var materializedVariants = (variants ?? [])
            .OrderBy(item => item.Locale, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Gender, StringComparer.Ordinal)
            .ThenBy(item => item.PluralCategory)
            .ToArray();
        if (materializedVariants.Select(item => (item.Locale.ToLowerInvariant(), item.Gender, item.PluralCategory))
            .Distinct().Count() != materializedVariants.Length)
            throw new ArgumentException("Localized text variants must have unique locale, gender and plural dimensions.", nameof(variants));

        Id = id == Guid.Empty
            ? DialogueParticipant.StableId($"localized-text:{normalizedKey}")
            : id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Key = normalizedKey;
        Fallback = fallback.Trim();
        Variants = materializedVariants;
    }

    public LocalizedText(
        string key,
        string fallback,
        IReadOnlyList<LocalizedTextVariant>? variants = null,
        string provenance = "migration-default")
        : this(Guid.Empty, CurrentSchemaVersion, provenance, key, fallback, variants)
    {
    }

    public string Resolve(string locale, string gender = "any", DialoguePluralCategory pluralCategory = DialoguePluralCategory.Other)
    {
        var requestedLocale = LocalizationCatalog.NormalizeLocale(locale);
        var requestedGender = string.IsNullOrWhiteSpace(gender) ? "any" : gender.Trim().ToLowerInvariant();
        var language = requestedLocale.Split('-')[0];
        var candidates = Variants
            .Where(item => string.Equals(item.Locale, requestedLocale, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(item.Locale, language, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => string.Equals(item.Locale, requestedLocale, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => string.Equals(item.Gender, requestedGender, StringComparison.Ordinal))
            .ThenByDescending(item => item.Gender == "any")
            .ThenByDescending(item => item.PluralCategory == pluralCategory)
            .ThenByDescending(item => item.PluralCategory is DialoguePluralCategory.Other or DialoguePluralCategory.Unspecified)
            .ToArray();
        return candidates.FirstOrDefault(item =>
                   (item.Gender == requestedGender || item.Gender == "any") &&
                   (item.PluralCategory == pluralCategory ||
                    item.PluralCategory is DialoguePluralCategory.Other or DialoguePluralCategory.Unspecified))?.Text
               ?? Fallback;
    }
}
