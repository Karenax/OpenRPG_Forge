using Forge.Domain.Quests;

namespace Forge.Application.Quests;

public readonly record struct QuestOperationsHandle(Guid Value);

public sealed record QuestOperationsSnapshot(
    QuestOperationsHandle Handle,
    QuestId QuestId,
    int Priority,
    bool Repeatable,
    QuestShareMode ShareMode,
    int ExclusiveChoiceCount,
    QuestNotificationMode NotificationMode);

public sealed class QuestOperationsScriptService
{
    private readonly QuestDefinition _quest;

    public QuestOperationsScriptService(QuestDefinition quest)
    {
        _quest = quest ?? throw new ArgumentNullException(nameof(quest));
        if (quest.Operations is null)
            throw new ArgumentException("Quest operations are not configured.", nameof(quest));
    }

    public QuestOperationsHandle Handle => new(_quest.Operations!.Id);

    public QuestOperationsSnapshot Read(QuestOperationsHandle handle)
    {
        Ensure(handle);
        var profile = _quest.Operations!;
        return new(handle, _quest.Id, profile.Journal.Priority, profile.Repeat.Enabled, profile.Share.Mode,
            profile.ExclusiveChoices.Count, profile.Notification.Mode);
    }

    public QuestRepeatDecision ResolveRepeat(QuestOperationsHandle handle, QuestOperationsRuntimeContext context)
    {
        Ensure(handle);
        return QuestOperationsRuntimeResolver.ResolveRepeat(_quest, context);
    }

    public QuestExclusiveDecision ResolveExclusive(QuestOperationsHandle handle, QuestTransitionId transitionId,
        QuestOperationsRuntimeContext context)
    {
        Ensure(handle);
        return QuestOperationsRuntimeResolver.ResolveExclusive(_quest, transitionId, context);
    }

    private void Ensure(QuestOperationsHandle handle)
    {
        if (handle != Handle)
            throw new InvalidOperationException("quest.operations.handle_invalid");
    }
}
