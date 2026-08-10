using Forge.Application.Quests;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Application.Tests.Quests;

public sealed class QuestJournalScriptServiceTests
{
    [Fact]
    public void Typed_handle_reads_bounded_canonical_snapshot_and_state()
    {
        var quest = CreateQuest();
        var service = new QuestJournalScriptService(quest);

        var snapshot = service.Read(service.Handle);
        var state = service.ReadState(service.Handle, quest.InitialStateId);

        Assert.Equal("main", snapshot.Category);
        Assert.Equal(2, snapshot.StateCount);
        Assert.Equal(1, snapshot.TerminalStateCount);
        Assert.Equal(2, snapshot.ContextLinkCount);
        Assert.Equal("search", state.Slug);
    }

    [Fact]
    public void Typed_handle_rejects_stale_schema_and_missing_state()
    {
        var quest = CreateQuest();
        var service = new QuestJournalScriptService(quest);

        Assert.Throws<InvalidOperationException>(() => service.Read(new(quest.Id, 99)));
        Assert.Throws<KeyNotFoundException>(() => service.ReadState(service.Handle,
            QuestStateId.From(Guid.Parse("c7500000-0000-4000-8000-000000000099"))));
    }

    private static QuestDefinition CreateQuest()
    {
        var initial = QuestStateId.From(Guid.Parse("c7600000-0000-4000-8000-000000000001"));
        var completed = QuestStateId.From(Guid.Parse("c7600000-0000-4000-8000-000000000002"));
        QuestStateDefinition State(QuestStateId id, string slug, int order, QuestTerminalKind terminal) => new(
            id, 1, "test:r007", slug, slug,
            new QuestJournalEntry(new LocalizedText($"quest.{slug}.summary", slug),
                new LocalizedText($"quest.{slug}.details", $"{slug} details"),
                contextLinks: slug == "search"
                    ? [
                        new QuestJournalLink(QuestJournalLinkKind.Custom, "one"),
                        new QuestJournalLink(QuestJournalLinkKind.Custom, "two"),
                    ]
                    : [], provenance: "test:r007"),
            terminal != QuestTerminalKind.None, order, order - 1, terminal, string.Empty,
            QuestReactivationRule.Never);
        return new(
            QuestId.From(Guid.Parse("c7700000-0000-4000-8000-000000000001")), 1, "test:r007",
            "script-quest", "Script quest", new("quest.script.title", "Script quest"),
            new("quest.script.description", "Script quest description"), "main", 80, "author:test", false,
            new(QuestProgressScope.Player, [QuestProgressScope.Player], false), initial,
            [State(initial, "search", 1, QuestTerminalKind.None),
             State(completed, "completed", 2, QuestTerminalKind.Completed)],
            [new QuestTransition(QuestTransitionId.From(Guid.Parse("c7800000-0000-4000-8000-000000000001")),
                initial, completed)]);
    }
}
