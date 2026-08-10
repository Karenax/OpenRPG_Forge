using System.Text.Json.Serialization;

namespace Forge.Domain.Quests;

public sealed record QuestTransition
{
    public const int CurrentSchemaVersion = 1;

    public QuestTransitionId Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public QuestStateId FromStateId { get; }

    public QuestStateId ToStateId { get; }

    public int DisplayOrder { get; }

    public QuestEventBinding EventBinding { get; }

    public int Priority { get; }

    public QuestTransitionIdempotence Idempotence { get; }

    public string IdempotenceKey { get; }

    public int ForgeScriptApiVersion { get; }

    public IReadOnlyList<QuestCondition> Conditions { get; }

    public IReadOnlyList<QuestAction> Actions { get; }

    [JsonConstructor]
    public QuestTransition(
        QuestTransitionId id,
        int schemaVersion,
        string? provenance,
        QuestStateId fromStateId,
        QuestStateId toStateId,
        int displayOrder,
        QuestEventBinding? eventBinding,
        int priority,
        QuestTransitionIdempotence idempotence,
        string? idempotenceKey,
        int forgeScriptApiVersion,
        IReadOnlyList<QuestCondition>? conditions = null,
        IReadOnlyList<QuestAction>? actions = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(fromStateId);
        ArgumentNullException.ThrowIfNull(toStateId);
        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        FromStateId = fromStateId;
        ToStateId = toStateId;
        DisplayOrder = displayOrder;
        EventBinding = eventBinding ?? QuestEventBinding.Manual;
        Priority = priority == 0 ? 50 : priority;
        Idempotence = idempotence;
        IdempotenceKey = string.IsNullOrWhiteSpace(idempotenceKey) ? string.Empty : idempotenceKey.Trim();
        ForgeScriptApiVersion = forgeScriptApiVersion == 0 ? 1 : forgeScriptApiVersion;
        Conditions = (conditions ?? []).ToArray();
        Actions = (actions ?? []).ToArray();
    }

    public QuestTransition(
        QuestTransitionId id,
        QuestStateId fromStateId,
        QuestStateId toStateId,
        IReadOnlyList<QuestCondition>? conditions = null,
        IReadOnlyList<QuestAction>? actions = null)
        : this(id, CurrentSchemaVersion, "migration-default", fromStateId, toStateId, 0,
            QuestEventBinding.Manual, 50, QuestTransitionIdempotence.None, string.Empty, 1, conditions, actions)
    {
    }

    public QuestTransition WithOrdering(int displayOrder) => new(
        Id, SchemaVersion, Provenance, FromStateId, ToStateId, displayOrder, EventBinding, Priority,
        Idempotence, IdempotenceKey, ForgeScriptApiVersion, Conditions, Actions);
}
