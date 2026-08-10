using Forge.Application.Quests;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Application.Tests.Quests;

public sealed class QuestTransitionScriptServiceTests
{
    [Fact]
    public void Typed_handle_reads_ordered_transition_contract_and_runs_bounded_scenario()
    {
        var quest = CreateQuest();
        var service = new QuestJournalScriptService(quest);

        var transitions = service.ReadTransitions(service.Handle);
        var simulation = service.Simulate(service.Handle, QuestProgressScope.Player, "player:script",
            new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero));

        var transition = Assert.Single(transitions);
        Assert.Equal("dialogue.accept", transition.EventName);
        Assert.Equal(QuestTransitionIdempotence.PerEvent, transition.Idempotence);
        Assert.Equal(1, transition.ForgeScriptApiVersion);
        Assert.Equal(QuestSimulationStatus.Completed, simulation.Status);
        Assert.Equal(1, simulation.AppliedTransitionCount);
    }

    [Fact]
    public void Typed_handle_rejects_stale_version_before_simulation()
    {
        var quest = CreateQuest();
        var service = new QuestJournalScriptService(quest);
        var stale = new QuestJournalHandle(quest.Id, 99);

        var error = Assert.Throws<InvalidOperationException>(() => service.ReadTransitions(stale));

        Assert.Equal("QUEST_JOURNAL_HANDLE_STALE", error.Message);
    }

    private static QuestDefinition CreateQuest()
    {
        var initial = QuestStateId.From(Guid.Parse("a8082000-0000-4000-8000-000000000001"));
        var completed = QuestStateId.From(Guid.Parse("a8082000-0000-4000-8000-000000000002"));
        var transition = new QuestTransition(
            QuestTransitionId.From(Guid.Parse("a8083000-0000-4000-8000-000000000001")), 1, "test:r008",
            initial, completed, 1,
            new QuestEventBinding(QuestEventKind.Dialogue, "dialogue.accept", provenance: "test:r008"),
            80, QuestTransitionIdempotence.PerEvent, "accept", 1);
        var simulation = new QuestSimulationProfile(Guid.Parse("a8084000-0000-4000-8000-000000000001"), 10,
            events: [new QuestSimulationEvent(Guid.Parse("a8084000-0000-4000-8000-000000000002"),
                "dialogue.accept", new DateTimeOffset(2026, 8, 7, 12, 0, 1, TimeSpan.Zero), "player:script")],
            provenance: "test:r008");
        QuestStateDefinition State(QuestStateId id, string slug, int order, bool terminal) => new(
            id, 1, "test:r008", slug, slug,
            new QuestJournalEntry(new LocalizedText($"quest.{slug}.summary", slug),
                new LocalizedText($"quest.{slug}.details", slug), provenance: "test:r008"),
            terminal, order, order - 1, terminal ? QuestTerminalKind.Completed : QuestTerminalKind.None,
            string.Empty, QuestReactivationRule.Never);
        return new QuestDefinition(QuestId.From(Guid.Parse("a8085000-0000-4000-8000-000000000001")),
            1, "test:r008", "script", "Script", new("quest.script.title", "Script"),
            new("quest.script.description", "Script"), "main", 80, "author:test", false,
            new(QuestProgressScope.Player, [QuestProgressScope.Player], false), initial,
            [State(initial, "initial", 1, false), State(completed, "completed", 2, true)],
            [transition], null, simulation);
    }
}
