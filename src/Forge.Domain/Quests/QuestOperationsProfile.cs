using Forge.Domain.Dialogues;

namespace Forge.Domain.Quests;

public enum QuestJournalSortingPolicy
{
    ProjectOrder,
    PriorityDescending,
    ImportanceThenUpdated
}

public sealed record QuestJournalPolicy(
    QuestJournalSortingPolicy Sorting,
    int Priority,
    bool AllowManualPin,
    bool DefaultPinned,
    int NarrativeImportance,
    bool NotifyOnPriorityChange);

public enum QuestRepeatResetMode
{
    Full,
    SelectedStates,
    RewardsOnly
}

public sealed record QuestRepeatPolicy(
    bool Enabled,
    long CooldownSeconds,
    int MaximumCompletions,
    QuestRepeatResetMode ResetMode,
    IReadOnlyList<QuestStateId> ResetStateIds,
    double RewardDecay,
    int SeedOffset);

public enum QuestShareMode
{
    Individual,
    PartyShared,
    OptIn,
    Leader,
    Unanimous
}

public enum QuestSeparationPolicy
{
    KeepShared,
    SnapshotIndividual,
    BlockSeparation
}

public sealed record QuestSharePolicy(
    QuestShareMode Mode,
    bool SynchronizeProgress,
    QuestSeparationPolicy Separation,
    bool RequireServerAuthority);

public sealed record QuestExclusiveChoicePolicy(
    Guid Id,
    QuestTransitionId TransitionId,
    string MutexGroup,
    bool Irreversible,
    LocalizedText Confirmation,
    LocalizedText Consequence,
    IReadOnlyList<QuestTransitionId> IncompatibleTransitionIds,
    string Provenance);

public enum QuestNotificationMode
{
    Grouped,
    Immediate,
    Silent,
    Cinematic
}

public sealed record QuestNotificationPolicy(
    QuestNotificationMode Mode,
    LocalizedText Title,
    LocalizedText Body,
    string AccessibleText,
    string GroupKey,
    long CooldownSeconds);

public sealed record QuestOperationsProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    QuestJournalPolicy Journal,
    QuestRepeatPolicy Repeat,
    QuestSharePolicy Share,
    IReadOnlyList<QuestExclusiveChoicePolicy> ExclusiveChoices,
    QuestNotificationPolicy Notification)
{
    public const int CurrentSchemaVersion = 1;

    public static QuestOperationsProfile CreateDefault(Guid id, string provenance) => new(
        id,
        CurrentSchemaVersion,
        provenance,
        false,
        new(QuestJournalSortingPolicy.PriorityDescending, 50, true, false, 50, true),
        new(false, 0, 0, QuestRepeatResetMode.Full, [], 1, 0),
        new(QuestShareMode.Individual, false, QuestSeparationPolicy.SnapshotIndividual, true),
        [],
        new(QuestNotificationMode.Grouped,
            new("quest.notification.title", "Journal mis à jour"),
            new("quest.notification.body", "Votre journal de quête a été mis à jour."),
            "Journal de quête mis à jour",
            "quest",
            2));
}
