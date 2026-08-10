using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Domain.Tests.Quests;

public sealed class QuestTransitionSimulationTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Simulation_injects_ordered_events_applies_effects_and_emits_audit()
    {
        var quest = ScenarioQuest();

        var result = QuestSimulationEngine.Run(quest, QuestProgressScope.Player, "player:test", Start,
            instanceId: Guid.Parse("a8080000-0000-4000-8000-000000000080"));

        Assert.Equal(QuestSimulationStatus.Completed, result.Status);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal(2, result.Audit.Count);
        Assert.Equal(QuestTerminalKind.Completed,
            quest.States.Single(item => item.Id == result.Instance.CurrentStateId).TerminalKind);
        Assert.Equal("true", result.Instance.Variables["quest.accepted"]);
        Assert.Equal("1", result.Instance.Variables["item:ember-key"]);
        Assert.All(result.Audit, item => Assert.NotEqual(Guid.Empty, item.Id));
    }

    [Fact]
    public void Simulation_reports_unhandled_event_without_mutating_state()
    {
        var source = ScenarioQuest();
        var simulation = new QuestSimulationProfile(source.Simulation.Id, 10, events:
            [new(Guid.Parse("a8080000-0000-4000-8000-000000000099"), "unknown", Start, "player:test")],
            provenance: "test:r008");
        var quest = Rebuild(source, simulation: simulation);

        var result = QuestSimulationEngine.Run(quest, QuestProgressScope.Player, "player:test", Start);

        Assert.Equal(QuestSimulationStatus.AwaitingEvent, result.Status);
        Assert.Equal(source.InitialStateId, result.Instance.CurrentStateId);
        Assert.Contains(result.Diagnostics, item => item.Code == QuestSimulationEngine.EventUnhandledCode);
    }

    [Fact]
    public void Simulation_stops_at_configured_step_limit()
    {
        var source = ScenarioQuest();
        var simulation = new QuestSimulationProfile(source.Simulation.Id, 1, events: source.Simulation.Events,
            provenance: "test:r008");
        var quest = Rebuild(source, simulation: simulation);

        var result = QuestSimulationEngine.Run(quest, QuestProgressScope.Player, "player:test", Start);

        Assert.Equal(QuestSimulationStatus.StepLimitReached, result.Status);
        Assert.Single(result.Steps);
        Assert.Contains(result.Diagnostics, item => item.Code == QuestSimulationEngine.StepLimitCode);
    }

    [Fact]
    public void Validator_reports_ambiguous_transitions_with_stable_paths()
    {
        var source = ScenarioQuest();
        var competing = new QuestTransition(
            TransitionId(3), 1, "test:r008", source.InitialStateId, source.States[^1].Id, 3,
            new QuestEventBinding(QuestEventKind.Dialogue, "dialogue.accept", provenance: "test:r008"),
            80, QuestTransitionIdempotence.PerEvent, "accept", 1);
        var quest = Rebuild(source, transitions: [.. source.Transitions, competing]);

        var conflicts = QuestValidator.Validate(quest)
            .Where(item => item.Code == QuestNarrativeValidator.TransitionConflictCode).ToArray();

        Assert.Equal(2, conflicts.Length);
        Assert.All(conflicts, item => Assert.Contains("/transitions/", item.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void Narrative_linter_reports_cycle_impossible_objective_and_repeated_reward()
    {
        var stateId = StateId(1);
        var state = State(stateId, "loop", false, objectives: [new("quest.loop.objective", "Sortir")]);
        var transition = new QuestTransition(TransitionId(1), 1, "test:r008", stateId, stateId, 1,
            new QuestEventBinding(QuestEventKind.Custom, "loop", provenance: "test:r008"), 50,
            QuestTransitionIdempotence.None, string.Empty, 1,
            actions: [new QuestAction(QuestActionKind.GiveItem, "ember", "1")]);
        var quest = new QuestDefinition(QuestId.From(Guid.Parse("a8080000-0000-4000-8000-000000000001")),
            1, "test:r008", "loop", "Loop", new("quest.loop.title", "Loop"),
            new("quest.loop.description", "Loop"), "main", 50, "author:test", false,
            new(QuestProgressScope.Player, [QuestProgressScope.Player], false), stateId, [state], [transition]);

        var codes = QuestNarrativeValidator.Validate(quest).Select(item => item.Code).ToHashSet();

        Assert.Contains(QuestNarrativeValidator.CycleWithoutExitCode, codes);
        Assert.Contains(QuestNarrativeValidator.ImpossibleObjectiveCode, codes);
        Assert.Contains(QuestNarrativeValidator.RepeatedRewardCode, codes);
    }

    [Fact]
    public void Validator_rejects_invalid_transition_policy_and_simulation_event_identity()
    {
        var source = ScenarioQuest();
        var first = source.Transitions[0];
        var invalidTransition = new QuestTransition(first.Id, 2, string.Empty, first.FromStateId, first.ToStateId,
            1, new QuestEventBinding(2, string.Empty, QuestEventKind.Custom, "bad"), 101,
            QuestTransitionIdempotence.PerActor, string.Empty, 2, first.Conditions, first.Actions);
        var duplicateEvent = source.Simulation.Events[0];
        var invalidSimulation = new QuestSimulationProfile(source.Simulation.Id, 1, "test:r008", 10, null,
            [duplicateEvent, duplicateEvent]);
        var quest = Rebuild(source, transitions: [invalidTransition, source.Transitions[1]],
            simulation: invalidSimulation);

        var diagnostics = QuestValidator.Validate(quest);

        Assert.Contains(diagnostics, item => item.Code == QuestValidator.TransitionSchemaUnsupportedCode);
        Assert.Contains(diagnostics, item => item.Code == QuestValidator.TransitionBindingInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == QuestValidator.TransitionPolicyInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == QuestValidator.SimulationEventIdentityDuplicateCode);
    }

    internal static QuestDefinition ScenarioQuest()
    {
        var initial = StateId(1);
        var active = StateId(2);
        var completed = StateId(3);
        var transitions = new[]
        {
            new QuestTransition(TransitionId(1), 1, "test:r008", initial, active, 1,
                new QuestEventBinding(QuestEventKind.Dialogue, "dialogue.accept", "dialogue:elyra",
                    "test:r008"), 80, QuestTransitionIdempotence.PerEvent, "accept", 1,
                actions: [new QuestAction(QuestActionKind.SetVariable, "quest.accepted", "true")]),
            new QuestTransition(TransitionId(2), 1, "test:r008", active, completed, 2,
                new QuestEventBinding(QuestEventKind.Custom, "warden.defeated", provenance: "test:r008"),
                90, QuestTransitionIdempotence.PerInstance, "reward", 1,
                [new QuestCondition(QuestConditionKind.VariableEquals, "warden.defeated", "true")],
                [new QuestAction(QuestActionKind.GiveItem, "ember-key", "1")]),
        };
        var events = new[]
        {
            new QuestSimulationEvent(Guid.Parse("a8080000-0000-4000-8000-000000000011"),
                "dialogue.accept", Start.AddSeconds(1), "player:test"),
            new QuestSimulationEvent(Guid.Parse("a8080000-0000-4000-8000-000000000012"),
                "warden.defeated", Start.AddSeconds(2), "player:test",
                new Dictionary<string, string> { ["warden.defeated"] = "true" }),
        };
        return new QuestDefinition(QuestId.From(Guid.Parse("a8080000-0000-4000-8000-000000000002")),
            1, "test:r008", "ashes", "Ashes", new("quest.ashes.title", "Ashes"),
            new("quest.ashes.description", "Ashes"), "main", 80, "author:test", false,
            new(QuestProgressScope.Player, [QuestProgressScope.Player], false), initial,
            [State(initial, "initial", false), State(active, "active", false),
             State(completed, "completed", true)], transitions, null,
            new QuestSimulationProfile(Guid.Parse("a8080000-0000-4000-8000-000000000010"), 10,
                events: events, provenance: "test:r008"));
    }

    private static QuestDefinition Rebuild(QuestDefinition source,
        IReadOnlyList<QuestTransition>? transitions = null,
        QuestSimulationProfile? simulation = null) => new(source.Id, source.SchemaVersion, source.Provenance,
        source.Slug, source.DisplayName, source.Title, source.Description, source.Category, source.Priority,
        source.Owner, source.RequiredAtRuntime, source.InstancePolicy, source.InitialStateId, source.States,
        transitions ?? source.Transitions, source.Operations, simulation ?? source.Simulation);

    private static QuestStateDefinition State(QuestStateId id, string slug, bool terminal,
        IReadOnlyList<LocalizedText>? objectives = null) => new(id, 1, "test:r008", slug, slug,
        new QuestJournalEntry(new LocalizedText($"quest.{slug}.summary", slug),
            new LocalizedText($"quest.{slug}.details", slug), objectives: objectives, provenance: "test:r008"),
        terminal, int.Parse(id.Value.ToString("N")[^1..]), null,
        terminal ? QuestTerminalKind.Completed : QuestTerminalKind.None, string.Empty,
        QuestReactivationRule.Never);

    private static QuestStateId StateId(int value) => QuestStateId.From(Guid.Parse(
        $"a8080000-0000-4000-8000-{value.ToString().PadLeft(12, '0')}"));

    private static QuestTransitionId TransitionId(int value) => QuestTransitionId.From(Guid.Parse(
        $"a8081000-0000-4000-8000-{value.ToString().PadLeft(12, '0')}"));
}
