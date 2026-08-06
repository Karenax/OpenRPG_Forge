using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Domain.Tests.Quests;

public sealed class QuestOperationsTests
{
    [Fact]
    public void Resolver_covers_repeat_share_exclusive_notification_and_journal_order()
    {
        var quest = CreateQuest();
        var first = quest.Transitions[0].Id;
        var second = quest.Transitions[1].Id;
        var now = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var context = new QuestOperationsRuntimeContext(now, now.AddMinutes(-20), 1, 3, 3, true, true,
            new HashSet<QuestTransitionId>());

        var repeat = QuestOperationsRuntimeResolver.ResolveRepeat(quest, context);
        var share = QuestOperationsRuntimeResolver.ResolveShare(quest, context);
        var exclusive = QuestOperationsRuntimeResolver.ResolveExclusive(quest, first, context);
        var blocked = QuestOperationsRuntimeResolver.ResolveExclusive(quest, second,
            context with { ChosenTransitions = new HashSet<QuestTransitionId> { first } });
        var oneWayPolicy = quest.WithOperations(quest.Operations! with
        {
            ExclusiveChoices = [quest.Operations!.ExclusiveChoices[0]]
        });
        var blockedByInboundConflict = QuestOperationsRuntimeResolver.ResolveExclusive(oneWayPolicy, second,
            context with { ChosenTransitions = new HashSet<QuestTransitionId> { first } });
        var notification = QuestOperationsRuntimeResolver.ResolveNotification(quest);
        var sorted = QuestOperationsRuntimeResolver.SortJournal([quest.WithOperations(quest.Operations! with
        {
            Journal = quest.Operations!.Journal with { Priority = 10, DefaultPinned = false }
        }), quest]);

        Assert.True(repeat.Allowed);
        Assert.Equal(0.75, repeat.RewardMultiplier, 3);
        Assert.True(share.Accepted);
        Assert.True(exclusive.Allowed);
        Assert.True(exclusive.RequiresConfirmation);
        Assert.Contains(second, exclusive.LockedTransitions);
        Assert.False(blocked.Allowed);
        Assert.False(blockedByInboundConflict.Allowed);
        Assert.True(notification.Visible);
        Assert.Equal("quest.update", notification.GroupKey);
        Assert.True(sorted[0].Operations!.Journal.DefaultPinned);
    }

    [Fact]
    public void Resolver_refuses_cooldown_consent_and_client_authority()
    {
        var quest = CreateQuest();
        var now = DateTimeOffset.UtcNow;
        var context = new QuestOperationsRuntimeContext(now, now.AddSeconds(-10), 1, 4, 2, false, true,
            new HashSet<QuestTransitionId>());
        Assert.Equal("quest.repeat.cooldown", QuestOperationsRuntimeResolver.ResolveRepeat(quest, context).Code);
        Assert.Equal("quest.share.consent_missing", QuestOperationsRuntimeResolver.ResolveShare(quest, context).Code);
        Assert.Equal("quest.share.server_authority_required",
            QuestOperationsRuntimeResolver.ResolveShare(quest, context with { ServerAuthorized = false }).Code);
    }

    [Fact]
    public void Validator_reports_invalid_references_and_accessibility()
    {
        var quest = CreateQuest();
        var invalid = quest.WithOperations(quest.Operations! with
        {
            Repeat = quest.Operations!.Repeat with { ResetStateIds = [QuestStateId.From(Guid.NewGuid())] },
            Notification = quest.Operations.Notification with { AccessibleText = "" }
        });
        var codes = QuestOperationsValidator.Validate(invalid).Select(item => item.Code).ToHashSet();
        Assert.Contains("quest.operations.repeat_invalid", codes);
        Assert.Contains("quest.operations.notification_invalid", codes);
    }

    public static QuestDefinition CreateQuest()
    {
        var start = QuestStateId.From(Guid.Parse("10000000-0000-4000-8000-000000000001"));
        var pathA = QuestStateId.From(Guid.Parse("10000000-0000-4000-8000-000000000002"));
        var pathB = QuestStateId.From(Guid.Parse("10000000-0000-4000-8000-000000000003"));
        var transitionA = QuestTransitionId.From(Guid.Parse("20000000-0000-4000-8000-000000000001"));
        var transitionB = QuestTransitionId.From(Guid.Parse("20000000-0000-4000-8000-000000000002"));
        var operations = new QuestOperationsProfile(Guid.Parse("30000000-0000-4000-8000-000000000001"), 1,
            "test:g13-j", true,
            new(QuestJournalSortingPolicy.PriorityDescending, 80, true, true, 90, true),
            new(true, 300, 3, QuestRepeatResetMode.SelectedStates, [start], 0.75, 2026),
            new(QuestShareMode.Unanimous, true, QuestSeparationPolicy.SnapshotIndividual, true),
            [
                new(Guid.Parse("40000000-0000-4000-8000-000000000001"), transitionA, "ending", true,
                    new LocalizedText("quest.confirm.a", "Confirmer la voie A ?"),
                    new LocalizedText("quest.consequence.a", "La voie B sera verrouillée."), [transitionB], "test:g13-j"),
                new(Guid.Parse("40000000-0000-4000-8000-000000000002"), transitionB, "ending", false,
                    new LocalizedText("quest.confirm.b", "Choisir la voie B ?"),
                    new LocalizedText("quest.consequence.b", "La voie A sera verrouillée."), [transitionA], "test:g13-j")
            ],
            new(QuestNotificationMode.Grouped,
                new LocalizedText("quest.update.title", "Journal"),
                new LocalizedText("quest.update.body", "Objectif mis à jour."),
                "Objectif de quête mis à jour", "quest.update", 2));
        return new QuestDefinition(QuestId.From(Guid.Parse("50000000-0000-4000-8000-000000000001")),
            "branched-quest", "Branched quest", start,
            [
                new(start, "Start", new QuestJournalEntry("quest.start", "Start"), false),
                new(pathA, "Path A", new QuestJournalEntry("quest.a", "A"), true),
                new(pathB, "Path B", new QuestJournalEntry("quest.b", "B"), true)
            ],
            [new(transitionA, start, pathA), new(transitionB, start, pathB)], operations);
    }
}
