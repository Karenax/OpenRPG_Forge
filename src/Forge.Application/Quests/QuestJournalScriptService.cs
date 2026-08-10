using Forge.Domain.Quests;

namespace Forge.Application.Quests;

public sealed record QuestJournalHandle(QuestId Id, int SchemaVersion);

public sealed record QuestJournalScriptSnapshot(
    QuestJournalHandle Handle,
    string Slug,
    string Category,
    int Priority,
    string Owner,
    QuestProgressScope DefaultScope,
    IReadOnlyList<QuestProgressScope> AllowedScopes,
    int StateCount,
    int TerminalStateCount,
    int LocalizedTextCount,
    int ContextLinkCount);

public sealed record QuestTransitionScriptSnapshot(
    QuestTransitionId Id,
    int SchemaVersion,
    string EventName,
    QuestEventKind EventKind,
    QuestStateId FromStateId,
    QuestStateId ToStateId,
    int DisplayOrder,
    int Priority,
    QuestTransitionIdempotence Idempotence,
    int ConditionCount,
    int ActionCount,
    int ForgeScriptApiVersion);

public sealed record QuestSimulationScriptSnapshot(
    Guid ScenarioId,
    QuestSimulationStatus Status,
    QuestStateId FinalStateId,
    int StepCount,
    int AppliedTransitionCount,
    int DiagnosticCount,
    IReadOnlyDictionary<string, string> Variables);

/// <summary>Read-only typed boundary used by ForgeScript adapters.</summary>
public sealed class QuestJournalScriptService
{
    private readonly QuestDefinition _quest;

    public QuestJournalScriptService(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var diagnostics = QuestValidator.Validate(quest);
        if (diagnostics.Count > 0)
            throw new ArgumentException(string.Join(';', diagnostics.Select(item => item.Code)), nameof(quest));
        _quest = quest;
    }

    public QuestJournalHandle Handle => new(_quest.Id, _quest.SchemaVersion);

    public QuestJournalScriptSnapshot Read(QuestJournalHandle handle)
    {
        Ensure(handle);
        return new(handle, _quest.Slug, _quest.Category, _quest.Priority, _quest.Owner,
            _quest.InstancePolicy.DefaultScope, _quest.InstancePolicy.AllowedScopes,
            _quest.States.Count, _quest.States.Count(item => item.IsTerminal),
            _quest.States.Sum(item => 2 + item.Journal.Objectives.Count +
                                      item.Journal.ContextLinks.Count(link => link.Label is not null)),
            _quest.States.Sum(item => item.Journal.ContextLinks.Count));
    }

    public QuestStateDefinition ReadState(QuestJournalHandle handle, QuestStateId stateId)
    {
        Ensure(handle);
        return _quest.States.SingleOrDefault(item => item.Id == stateId)
            ?? throw new KeyNotFoundException("QUEST_STATE_MISSING");
    }

    public IReadOnlyList<QuestTransitionScriptSnapshot> ReadTransitions(QuestJournalHandle handle)
    {
        Ensure(handle);
        return _quest.Transitions.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id.Value)
            .Select(item => new QuestTransitionScriptSnapshot(item.Id, item.SchemaVersion,
                item.EventBinding.EventName, item.EventBinding.Kind, item.FromStateId, item.ToStateId,
                item.DisplayOrder, item.Priority, item.Idempotence, item.Conditions.Count, item.Actions.Count,
                item.ForgeScriptApiVersion)).ToArray();
    }

    public QuestSimulationScriptSnapshot Simulate(
        QuestJournalHandle handle,
        QuestProgressScope scope,
        string scopeOwnerId,
        DateTimeOffset startedAt,
        bool authorityGranted = true)
    {
        Ensure(handle);
        var result = QuestSimulationEngine.Run(_quest, scope, scopeOwnerId, startedAt, authorityGranted);
        return new(result.ScenarioId, result.Status, result.Instance.CurrentStateId, result.Steps.Count,
            result.Audit.Count, result.Diagnostics.Count, result.Instance.Variables);
    }

    private void Ensure(QuestJournalHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.Id != _quest.Id || handle.SchemaVersion != _quest.SchemaVersion)
            throw new InvalidOperationException("QUEST_JOURNAL_HANDLE_STALE");
    }
}
