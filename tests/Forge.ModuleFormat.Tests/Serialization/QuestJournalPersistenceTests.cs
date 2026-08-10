using System.Text.Json.Nodes;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class QuestJournalPersistenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Aggregate_and_workspace_round_trip_rich_quest_journal_exactly()
    {
        var project = RichProject();
        var aggregate = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(aggregate);
        var created = ProjectSession.Create(_temporaryRoot, project);
        var reopened = ProjectSession.Open(_temporaryRoot);

        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(restored));
        Assert.True(created.Succeeded);
        Assert.True(reopened.Succeeded);
        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(reopened.Session!.Project));
        var quest = Assert.Single(restored.Quests);
        Assert.Equal("main", quest.Category);
        Assert.Equal(QuestProgressScope.Party, quest.InstancePolicy.DefaultScope);
        Assert.Equal(QuestTerminalKind.Completed, quest.States[^1].TerminalKind);
        Assert.Single(quest.States[0].Journal.ContextLinks);
        Assert.Equal([1], quest.Transitions.Select(item => item.DisplayOrder));
        Assert.Equal("dialogue.accept", Assert.Single(quest.Transitions).EventBinding.EventName);
        Assert.Equal(QuestTransitionIdempotence.PerEvent, Assert.Single(quest.Transitions).Idempotence);
        Assert.Single(quest.Simulation.Events);
    }

    [Fact]
    public void Previous_quest_shape_migrates_metadata_order_terminal_and_localization_deterministically()
    {
        var node = JsonNode.Parse(ProjectJsonSerializer.Serialize(RichProject()))!.AsObject();
        node["formatVersion"] = ProjectJsonSerializer.PrePostLaunchCapabilitiesFormatVersion;
        var quest = node["project"]!["quests"]!.AsArray()[0]!.AsObject();
        foreach (var property in new[]
                 {
                     "schemaVersion", "provenance", "title", "description", "category", "priority", "owner",
                     "requiredAtRuntime", "instancePolicy",
                     "simulation",
                 })
            quest.Remove(property);
        foreach (var stateNode in quest["states"]!.AsArray())
        {
            var state = stateNode!.AsObject();
            foreach (var property in new[]
                     {
                         "schemaVersion", "provenance", "slug", "displayOrder", "legacyJournalIndex",
                         "terminalKind", "customTerminalCode", "reactivation",
                     })
                state.Remove(property);
            var journal = state["journal"]!.AsObject();
            foreach (var property in new[]
                     {
                         "id", "schemaVersion", "provenance", "summary", "details", "objectives", "contextLinks",
                     })
                journal.Remove(property);
        }
        foreach (var transitionNode in quest["transitions"]!.AsArray())
        {
            var transition = transitionNode!.AsObject();
            foreach (var property in new[]
                     {
                         "schemaVersion", "provenance", "displayOrder", "eventBinding", "priority",
                         "idempotence", "idempotenceKey", "forgeScriptApiVersion",
                     })
                transition.Remove(property);
        }

        var restored = ProjectJsonSerializer.Deserialize(node.ToJsonString());
        var migrated = Assert.Single(restored.Quests);

        Assert.Equal("journal", migrated.Category);
        Assert.Equal([1, 2], migrated.States.Select(item => item.DisplayOrder));
        Assert.Equal([0, 1], migrated.States.Select(item => item.LegacyJournalIndex));
        Assert.Equal(QuestTerminalKind.Completed, migrated.States[^1].TerminalKind);
        Assert.Equal("quest.ashes.initial", migrated.States[0].Journal.Summary.Key);
        Assert.Equal(1, Assert.Single(migrated.Transitions).DisplayOrder);
        Assert.Equal("manual", Assert.Single(migrated.Transitions).EventBinding.EventName);
        Assert.Empty(migrated.Simulation.Events);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }

    private static ProjectDefinition RichProject()
    {
        var source = TestProjectFactory.CreateValid();
        var legacy = Assert.Single(source.Quests);
        var states = legacy.States.Select((state, index) => new QuestStateDefinition(
            state.Id, 1, "test:r007", index == 0 ? "initial" : "completed", state.DisplayName,
            new QuestJournalEntry(
                new LocalizedText(index == 0 ? "quest.ashes.initial" : "quest.ashes.completed",
                    state.Journal.Fallback, provenance: "test:r007"),
                new LocalizedText(index == 0 ? "quest.ashes.initial.details" : "quest.ashes.completed.details",
                    $"{state.Journal.Fallback} — détails", provenance: "test:r007"),
                objectives: index == 0 ? [new("quest.ashes.objective", "Trouver la clé")] : [],
                contextLinks: index == 0
                    ? [new QuestJournalLink(QuestJournalLinkKind.Custom, "lore:ember-key")]
                    : [], provenance: "test:r007"),
            state.IsTerminal, index + 1, 10 + index,
            state.IsTerminal ? QuestTerminalKind.Completed : QuestTerminalKind.None, string.Empty,
            state.IsTerminal ? QuestReactivationRule.Manual : QuestReactivationRule.Never)).ToArray();
        var transitions = legacy.Transitions.Select((transition, index) => new QuestTransition(
            transition.Id, 1, "test:r008", transition.FromStateId, transition.ToStateId, index + 1,
            new QuestEventBinding(QuestEventKind.Dialogue, "dialogue.accept", "dialogue:elyra", "test:r008"),
            80, QuestTransitionIdempotence.PerEvent, "accept", 1, transition.Conditions,
            transition.Actions)).ToArray();
        var simulation = new QuestSimulationProfile(Guid.Parse("a8080000-0000-4000-8000-000000000031"), 10,
            events: [new QuestSimulationEvent(Guid.Parse("a8080000-0000-4000-8000-000000000032"),
                "dialogue.accept", new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero), "player:test")],
            provenance: "test:r008");
        var quest = new QuestDefinition(legacy.Id, 1, "test:r007", legacy.Slug, legacy.DisplayName,
            new("quest.ashes.title", legacy.DisplayName),
            new("quest.ashes.description", "Libérez la Crypte des Cendres."),
            "main", 90, "author:karen", true,
            new(QuestProgressScope.Party, [QuestProgressScope.Party], true), legacy.InitialStateId,
            states, transitions, legacy.Operations, simulation);
        return new(source.Id, source.Title, source.Module, source.Blueprints, source.Areas, source.Dialogues, [quest]);
    }
}
