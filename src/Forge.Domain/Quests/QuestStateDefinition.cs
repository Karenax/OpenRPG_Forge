using System.Text.Json.Serialization;

namespace Forge.Domain.Quests;

public sealed record QuestStateDefinition
{
    public const int CurrentSchemaVersion = 1;

    public QuestStateId Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public QuestJournalEntry Journal { get; }

    public bool IsTerminal { get; }

    public int DisplayOrder { get; }

    public int? LegacyJournalIndex { get; }

    public QuestTerminalKind TerminalKind { get; }

    public string CustomTerminalCode { get; }

    public QuestReactivationRule Reactivation { get; }

    [JsonConstructor]
    public QuestStateDefinition(
        QuestStateId id,
        int schemaVersion,
        string? provenance,
        string? slug,
        string displayName,
        QuestJournalEntry journal,
        bool isTerminal,
        int displayOrder,
        int? legacyJournalIndex,
        QuestTerminalKind terminalKind,
        string? customTerminalCode,
        QuestReactivationRule reactivation)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(journal);
        var effectiveTerminal = terminalKind == QuestTerminalKind.None && isTerminal
            ? QuestTerminalKind.Completed
            : terminalKind;
        if (!Enum.IsDefined(effectiveTerminal))
            throw new ArgumentOutOfRangeException(nameof(terminalKind));
        if (!Enum.IsDefined(reactivation))
            throw new ArgumentOutOfRangeException(nameof(reactivation));
        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Slug = string.IsNullOrWhiteSpace(slug) ? Slugify(displayName) : slug.Trim();
        DisplayName = displayName.Trim();
        Journal = journal;
        DisplayOrder = displayOrder;
        LegacyJournalIndex = legacyJournalIndex;
        TerminalKind = effectiveTerminal;
        IsTerminal = effectiveTerminal != QuestTerminalKind.None;
        CustomTerminalCode = customTerminalCode?.Trim() ?? string.Empty;
        Reactivation = reactivation;
    }

    public QuestStateDefinition(
        QuestStateId id,
        string displayName,
        QuestJournalEntry journal,
        bool isTerminal = false)
        : this(id, CurrentSchemaVersion, "migration-default", null, displayName, journal, isTerminal, 0, null,
            isTerminal ? QuestTerminalKind.Completed : QuestTerminalKind.None, string.Empty,
            QuestReactivationRule.Never)
    {
    }

    public QuestStateDefinition WithOrdering(int displayOrder, int? legacyJournalIndex = null) => new(
        Id, SchemaVersion, Provenance, Slug, DisplayName, Journal, IsTerminal, displayOrder,
        legacyJournalIndex ?? LegacyJournalIndex, TerminalKind, CustomTerminalCode, Reactivation);

    private static string Slugify(string value)
    {
        var normalized = new string(value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        return normalized.Trim('-');
    }
}
