using System.Text.Json.Serialization;

namespace Forge.Domain.Quests;

public sealed record QuestInstanceHistoryEntry(
    Guid Id,
    QuestStateId? FromStateId,
    QuestStateId ToStateId,
    QuestTransitionId? TransitionId,
    DateTimeOffset OccurredAt,
    string ActorId,
    string Reason);

public sealed record QuestInstance
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public QuestId QuestId { get; }

    public QuestProgressScope Scope { get; }

    public string ScopeOwnerId { get; }

    public QuestStateId CurrentStateId { get; }

    public IReadOnlyList<QuestInstanceHistoryEntry> History { get; }

    public IReadOnlyDictionary<string, string> Variables { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public DateTimeOffset? LastSimulatedAt { get; }

    public long Revision { get; }

    [JsonConstructor]
    public QuestInstance(
        Guid id,
        int schemaVersion,
        string? provenance,
        QuestId questId,
        QuestProgressScope scope,
        string scopeOwnerId,
        QuestStateId currentStateId,
        IReadOnlyList<QuestInstanceHistoryEntry>? history,
        IReadOnlyDictionary<string, string>? variables,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? lastSimulatedAt,
        long revision)
    {
        if (id == Guid.Empty) throw new ArgumentException("A quest instance ID cannot be empty.", nameof(id));
        ArgumentNullException.ThrowIfNull(questId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeOwnerId);
        ArgumentNullException.ThrowIfNull(currentStateId);
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "runtime" : provenance.Trim();
        QuestId = questId;
        Scope = scope;
        ScopeOwnerId = scopeOwnerId.Trim();
        CurrentStateId = currentStateId;
        History = (history ?? []).OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToArray();
        Variables = new SortedDictionary<string, string>(
            (variables ?? new Dictionary<string, string>()).ToDictionary(item => item.Key, item => item.Value,
                StringComparer.Ordinal), StringComparer.Ordinal);
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        LastSimulatedAt = lastSimulatedAt;
        Revision = revision;
    }
}

public static class QuestInstanceService
{
    public static QuestInstance Create(
        QuestDefinition quest,
        QuestProgressScope scope,
        string scopeOwnerId,
        DateTimeOffset now,
        bool authorityGranted = true,
        Guid? id = null)
    {
        EnsureQuestAndScope(quest, scope, authorityGranted);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeOwnerId);
        var instanceId = id ?? Guid.NewGuid();
        var history = new QuestInstanceHistoryEntry(
            StableId($"quest-instance-history:{instanceId}:{quest.Id}:created"), null, quest.InitialStateId, null, now,
            scopeOwnerId.Trim(), "created");
        return new(instanceId, QuestInstance.CurrentSchemaVersion, "runtime", quest.Id, scope,
            scopeOwnerId, quest.InitialStateId, [history], new Dictionary<string, string>(), now, now, now, 0);
    }

    public static QuestInstance Advance(
        QuestDefinition quest,
        QuestInstance instance,
        QuestTransitionId transitionId,
        IQuestConditionEvaluator evaluator,
        DateTimeOffset now,
        string actorId,
        IReadOnlyDictionary<string, string>? variableUpdates = null,
        bool authorityGranted = true)
    {
        Ensure(quest, instance, authorityGranted);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        var machine = new QuestStateMachine(quest);
        machine.Restore(instance.CurrentStateId);
        var from = machine.CurrentState.Id;
        var result = machine.Advance(transitionId, evaluator);
        var variables = new SortedDictionary<string, string>(
            instance.Variables.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var pair in variableUpdates ?? new Dictionary<string, string>()) variables[pair.Key] = pair.Value;
        var history = new QuestInstanceHistoryEntry(
            StableId($"quest-instance-history:{instance.Id}:{instance.Revision + 1}:{transitionId}"), from,
            result.State.Id, transitionId, now, actorId.Trim(), "transition");
        return new(instance.Id, instance.SchemaVersion, instance.Provenance, instance.QuestId, instance.Scope,
            instance.ScopeOwnerId, result.State.Id, [.. instance.History, history], variables, instance.CreatedAt,
            now, now, instance.Revision + 1);
    }

    public static QuestInstance Reactivate(
        QuestDefinition quest,
        QuestInstance instance,
        DateTimeOffset now,
        string actorId,
        bool authorityGranted = true)
    {
        Ensure(quest, instance, authorityGranted);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        var current = quest.States.Single(item => item.Id == instance.CurrentStateId);
        if (!current.IsTerminal || current.Reactivation == QuestReactivationRule.Never)
            throw new InvalidOperationException("quest.instance.reactivation_forbidden");
        if (current.Reactivation == QuestReactivationRule.RepeatPolicy && quest.Operations?.Repeat.Enabled != true)
            throw new InvalidOperationException("quest.instance.repeat_policy_disabled");
        var history = new QuestInstanceHistoryEntry(
            StableId($"quest-instance-history:{instance.Id}:{instance.Revision + 1}:reactivate"), current.Id,
            quest.InitialStateId, null, now, actorId.Trim(), "reactivated");
        return new(instance.Id, instance.SchemaVersion, instance.Provenance, instance.QuestId, instance.Scope,
            instance.ScopeOwnerId, quest.InitialStateId, [.. instance.History, history], instance.Variables,
            instance.CreatedAt, now, now, instance.Revision + 1);
    }

    private static void Ensure(QuestDefinition quest, QuestInstance instance, bool authorityGranted)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureQuestAndScope(quest, instance.Scope, authorityGranted);
        if (instance.QuestId != quest.Id) throw new InvalidOperationException("quest.instance.quest_mismatch");
        if (!quest.States.Any(item => item.Id == instance.CurrentStateId))
            throw new InvalidOperationException("quest.instance.state_missing");
    }

    private static void EnsureQuestAndScope(QuestDefinition quest, QuestProgressScope scope, bool authorityGranted)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var diagnostics = QuestValidator.Validate(quest);
        if (diagnostics.Count > 0) throw new ArgumentException(diagnostics[0].Code, nameof(quest));
        if (!quest.InstancePolicy.AllowedScopes.Contains(scope))
            throw new InvalidOperationException("quest.instance.scope_forbidden");
        if (quest.InstancePolicy.RequireAuthority && !authorityGranted)
            throw new InvalidOperationException("quest.instance.authority_required");
    }

    private static Guid StableId(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }
}
