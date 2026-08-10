using System.Text.Json.Serialization;

namespace Forge.Domain.Quests;

public enum QuestEventKind
{
    Manual,
    Dialogue,
    Script,
    AreaEntered,
    EntityInteracted,
    ItemAcquired,
    VariableChanged,
    Custom,
}

public sealed record QuestEventBinding
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }
    public string Provenance { get; }
    public QuestEventKind Kind { get; }
    public string EventName { get; }
    public string? SourceReference { get; }

    [JsonConstructor]
    public QuestEventBinding(
        int schemaVersion,
        string? provenance,
        QuestEventKind kind,
        string eventName,
        string? sourceReference = null)
    {
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Kind = kind;
        EventName = string.IsNullOrWhiteSpace(eventName) ? "manual" : eventName.Trim();
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();
    }

    public QuestEventBinding(QuestEventKind kind, string eventName, string? sourceReference = null,
        string provenance = "author")
        : this(CurrentSchemaVersion, provenance, kind, eventName, sourceReference)
    {
    }

    public static QuestEventBinding Manual { get; } = new(QuestEventKind.Manual, "manual",
        provenance: "migration-default");
}

public enum QuestTransitionIdempotence
{
    None,
    PerInstance,
    PerActor,
    PerEvent,
}

public sealed record QuestTransitionAuditEntry(
    Guid Id,
    QuestTransitionId TransitionId,
    Guid EventId,
    QuestStateId FromStateId,
    QuestStateId ToStateId,
    DateTimeOffset OccurredAt,
    string ActorId,
    string IdempotenceKey,
    IReadOnlyList<QuestAction> Effects,
    string Provenance);
