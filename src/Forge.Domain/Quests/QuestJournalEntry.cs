using System.Text.Json.Serialization;
using Forge.Domain.Dialogues;

namespace Forge.Domain.Quests;

public sealed record QuestJournalEntry
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public LocalizedText Summary { get; }

    public LocalizedText Details { get; }

    public IReadOnlyList<LocalizedText> Objectives { get; }

    public IReadOnlyList<QuestJournalLink> ContextLinks { get; }

    public string LocalizationKey => Summary.Key;

    public string Fallback => Summary.Fallback;

    [JsonConstructor]
    public QuestJournalEntry(
        Guid id,
        int schemaVersion,
        string? provenance,
        LocalizedText? summary,
        LocalizedText? details,
        IReadOnlyList<LocalizedText>? objectives,
        IReadOnlyList<QuestJournalLink>? contextLinks,
        string? localizationKey,
        string? fallback)
    {
        var legacyKey = NormalizeLegacyKey(
            string.IsNullOrWhiteSpace(localizationKey) ? "quest.journal.migrated" : localizationKey.Trim());
        var legacyFallback = string.IsNullOrWhiteSpace(fallback) ? "Entrée de journal" : fallback.Trim();
        Summary = summary ?? new LocalizedText(legacyKey, legacyFallback);
        Details = details ?? new LocalizedText($"{legacyKey}.details", legacyFallback);
        var materializedObjectives = (objectives ?? []).ToArray();
        var materializedLinks = (contextLinks ?? []).OrderBy(item => item.Kind).ThenBy(item => item.ReferenceId,
            StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        if (materializedLinks.Select(item => item.Id).Distinct().Count() != materializedLinks.Length)
            throw new ArgumentException("Quest journal links must have unique IDs.", nameof(contextLinks));
        Id = id == Guid.Empty
            ? DialogueParticipant.StableId($"quest-journal-entry:{Summary.Key}")
            : id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Objectives = materializedObjectives;
        ContextLinks = materializedLinks;
    }

    public QuestJournalEntry(string localizationKey, string fallback)
        : this(Guid.Empty, CurrentSchemaVersion, "migration-default", null, null, null, null,
            localizationKey, fallback)
    {
    }

    public QuestJournalEntry(
        LocalizedText summary,
        LocalizedText details,
        IReadOnlyList<LocalizedText>? objectives = null,
        IReadOnlyList<QuestJournalLink>? contextLinks = null,
        string provenance = "author")
        : this(Guid.Empty, CurrentSchemaVersion, provenance, summary, details, objectives, contextLinks, null, null)
    {
    }

    private static string NormalizeLegacyKey(string value) => new(value
        .Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
            ? character
            : '-')
        .ToArray());
}
