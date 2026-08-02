using Forge.Domain.Quests;

namespace Forge.Domain.Tests.Quests;

public sealed class QuestStateMachineTests
{
    private static readonly QuestStateId NotStartedId = QuestStateId.From(
        Guid.Parse("81000000-0000-4000-8000-000000000001"));

    private static readonly QuestStateId ActiveId = QuestStateId.From(
        Guid.Parse("81000000-0000-4000-8000-000000000002"));

    private static readonly QuestStateId CompletedId = QuestStateId.From(
        Guid.Parse("81000000-0000-4000-8000-000000000003"));

    private static readonly QuestTransitionId StartTransitionId = QuestTransitionId.From(
        Guid.Parse("82000000-0000-4000-8000-000000000001"));

    private static readonly QuestTransitionId CompleteTransitionId = QuestTransitionId.From(
        Guid.Parse("82000000-0000-4000-8000-000000000002"));

    [Fact]
    public void StateMachine_AdvancesJournalAndActionsToCompletion()
    {
        var machine = new QuestStateMachine(CreateQuest());
        var evaluator = new TestEvaluator();

        var started = machine.Advance(StartTransitionId, evaluator);

        Assert.Equal("Retrouvez la clé de braise.", started.State.Journal.Fallback);
        Assert.Equal(QuestActionKind.SetVariable, Assert.Single(started.Actions).Kind);
        Assert.False(started.Completed);
        Assert.Empty(machine.GetAvailableTransitions(evaluator));

        evaluator.SatisfiedKeys.Add("warden.defeated");
        var completed = machine.Advance(CompleteTransitionId, evaluator);

        Assert.True(completed.Completed);
        Assert.True(machine.Completed);
        Assert.Equal("La Crypte des Cendres est libérée.", completed.State.Journal.Fallback);
    }

    [Fact]
    public void StateMachine_RejectsInvalidTransitionFromCurrentState()
    {
        var machine = new QuestStateMachine(CreateQuest());

        Assert.Throws<InvalidOperationException>(() =>
            machine.Advance(CompleteTransitionId, new TestEvaluator()));
    }

    [Fact]
    public void StateMachine_RejectsUnsatisfiedTransition()
    {
        var machine = new QuestStateMachine(CreateQuest());
        machine.Advance(StartTransitionId, new TestEvaluator());

        Assert.Throws<InvalidOperationException>(() =>
            machine.Advance(CompleteTransitionId, new TestEvaluator()));
    }

    [Fact]
    public void StateMachine_RestoresAValidatedSaveState()
    {
        var machine = new QuestStateMachine(CreateQuest());

        machine.Restore(ActiveId);

        Assert.Equal(ActiveId, machine.CurrentState.Id);
        Assert.Throws<InvalidOperationException>(() => machine.Restore(
            QuestStateId.From(Guid.Parse("81000000-0000-4000-8000-000000000099"))));
    }

    [Fact]
    public void Validator_AcceptsConnectedQuest()
    {
        Assert.Empty(QuestValidator.Validate(CreateQuest()));
    }

    [Fact]
    public void Validator_ReportsMissingInitialState()
    {
        var valid = CreateQuest();
        var invalid = new QuestDefinition(
            valid.Id,
            valid.Slug,
            valid.DisplayName,
            QuestStateId.From(Guid.Parse("81000000-0000-4000-8000-000000000099")),
            valid.States,
            valid.Transitions);

        Assert.Equal(
            QuestValidator.MissingInitialStateCode,
            Assert.Single(QuestValidator.Validate(invalid)).Code);
    }

    [Fact]
    public void Validator_ReportsMissingTransitionState()
    {
        var missing = QuestStateId.From(Guid.Parse("81000000-0000-4000-8000-000000000098"));
        var initial = CreateState(NotStartedId, "Initial", "Initial");
        var invalid = new QuestDefinition(
            QuestId.From(Guid.Parse("80000000-0000-4000-8000-000000000001")),
            "invalid",
            "Invalid",
            initial.Id,
            [initial],
            [new QuestTransition(StartTransitionId, initial.Id, missing)]);

        Assert.Equal(
            QuestValidator.MissingTransitionStateCode,
            Assert.Single(QuestValidator.Validate(invalid)).Code);
    }

    [Fact]
    public void Validator_ReportsUnreachableState()
    {
        var valid = CreateQuest();
        var unreachable = CreateState(
            QuestStateId.From(Guid.Parse("81000000-0000-4000-8000-000000000097")),
            "Caché",
            "Caché");
        var invalid = new QuestDefinition(
            valid.Id,
            valid.Slug,
            valid.DisplayName,
            valid.InitialStateId,
            [.. valid.States, unreachable],
            valid.Transitions);

        Assert.Equal(
            QuestValidator.UnreachableStateCode,
            Assert.Single(QuestValidator.Validate(invalid)).Code);
    }

    [Fact]
    public void Validator_RejectsOutgoingTransitionFromTerminalState()
    {
        var valid = CreateQuest();
        var invalidTransition = new QuestTransition(
            QuestTransitionId.From(Guid.Parse("82000000-0000-4000-8000-000000000003")),
            CompletedId,
            NotStartedId);
        var invalid = new QuestDefinition(
            valid.Id,
            valid.Slug,
            valid.DisplayName,
            valid.InitialStateId,
            valid.States,
            [.. valid.Transitions, invalidTransition]);

        Assert.Equal(
            QuestValidator.TerminalStateTransitionCode,
            Assert.Single(QuestValidator.Validate(invalid)).Code);
    }

    internal static QuestDefinition CreateQuest() => new(
        QuestId.From(Guid.Parse("80000000-0000-4000-8000-000000000001")),
        "crypt-of-ashes",
        "Crypte des Cendres",
        NotStartedId,
        [
            CreateState(NotStartedId, "Non commencée", "Parlez à Elyra."),
            CreateState(ActiveId, "Active", "Retrouvez la clé de braise."),
            CreateState(CompletedId, "Terminée", "La Crypte des Cendres est libérée.", true),
        ],
        [
            new QuestTransition(
                StartTransitionId,
                NotStartedId,
                ActiveId,
                actions: [new QuestAction(QuestActionKind.SetVariable, "quest.accepted", "true")]),
            new QuestTransition(
                CompleteTransitionId,
                ActiveId,
                CompletedId,
                [new QuestCondition(QuestConditionKind.VariableEquals, "warden.defeated", "true")]),
        ]);

    private static QuestStateDefinition CreateState(
        QuestStateId id,
        string name,
        string journal,
        bool terminal = false) =>
        new(id, name, new QuestJournalEntry($"quest.crypt.{name.ToLowerInvariant()}", journal), terminal);

    private sealed class TestEvaluator : IQuestConditionEvaluator
    {
        internal HashSet<string> SatisfiedKeys { get; } = [];

        public bool IsSatisfied(QuestCondition condition) => SatisfiedKeys.Contains(condition.Key);
    }
}
