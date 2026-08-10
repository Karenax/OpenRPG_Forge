using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Application.Quality;

public enum NarrativeFixtureScope { Player, Party, World }

public sealed record NarrativeStateFixture(
    string Id,
    NarrativeFixtureScope Scope,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<string> InjectedEvents);

public sealed record DialogueTestPath(
    string FixtureId,
    IReadOnlyList<DialogueNodeId> Nodes,
    IReadOnlyList<DialogueAction> Actions,
    bool Completed,
    string StopReason);

public sealed record DialogueQualityReport(
    DialogueId DialogueId,
    bool Passed,
    int CoveredNodes,
    int TotalNodes,
    int CoveredChoices,
    int TotalChoices,
    IReadOnlyList<DialogueTestPath> Paths);

public sealed record QuestTestPath(
    string FixtureId,
    NarrativeFixtureScope Scope,
    IReadOnlyList<QuestStateId> States,
    IReadOnlyList<QuestTransitionId> Transitions,
    IReadOnlyList<QuestAction> Actions,
    bool SaveResumeEqual,
    bool Completed,
    string StopReason);

public sealed record QuestQualityReport(
    QuestId QuestId,
    bool Passed,
    int CoveredStates,
    int TotalStates,
    int CoveredTransitions,
    int TotalTransitions,
    IReadOnlySet<NarrativeFixtureScope> CoveredScopes,
    IReadOnlyList<QuestTestPath> Paths);

public static class NarrativeQualityService
{
    public static DialogueQualityReport TestDialogue(
        DialogueDefinition dialogue,
        IReadOnlyList<NarrativeStateFixture>? fixtures = null,
        int maximumDepth = 64)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        if (maximumDepth is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        var selectedFixtures = fixtures ?? DialogueFixtures(dialogue);
        var paths = new List<DialogueTestPath>();
        var coveredNodes = new HashSet<DialogueNodeId>();
        var coveredChoices = new HashSet<DialogueChoiceId>();
        foreach (var fixture in selectedFixtures.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            Walk(dialogue.EntryNodeId, fixture, fixture.Values.ToDictionary(), [], [], new HashSet<DialogueNodeId>(), 0);
        }

        var totalChoices = dialogue.Nodes.Sum(node => node.Choices.Count);
        var passed = coveredNodes.Count == dialogue.Nodes.Count && coveredChoices.Count == totalChoices &&
            paths.All(path => path.StopReason is "completed" or "no-available-choice");
        return new DialogueQualityReport(dialogue.Id, passed, coveredNodes.Count, dialogue.Nodes.Count,
            coveredChoices.Count, totalChoices, paths.OrderBy(path => path.FixtureId, StringComparer.Ordinal)
                .ThenBy(path => string.Join('/', path.Nodes)).ToArray());

        void Walk(
            DialogueNodeId nodeId,
            NarrativeStateFixture fixture,
            Dictionary<string, string> state,
            IReadOnlyList<DialogueNodeId> prefix,
            IReadOnlyList<DialogueAction> actions,
            HashSet<DialogueNodeId> seen,
            int depth)
        {
            var nodes = prefix.Append(nodeId).ToArray();
            if (depth > maximumDepth) { paths.Add(new(fixture.Id, nodes, actions, false, "maximum-depth")); return; }
            var node = dialogue.Nodes.SingleOrDefault(item => item.Id == nodeId);
            if (node is null) { paths.Add(new(fixture.Id, nodes, actions, false, "missing-node")); return; }
            coveredNodes.Add(nodeId);
            if (!seen.Add(nodeId)) { paths.Add(new(fixture.Id, nodes, actions, false, "cycle")); return; }
            var enteredActions = actions.Concat(node.OnEnterActions).ToArray();
            Apply(node.OnEnterActions, state);
            var evaluator = new DialogueEvaluator(state);
            var available = node.Choices.Where(choice => DialogueConditionLogic.Evaluate(choice, evaluator).Available).ToArray();
            if (available.Length == 0) { paths.Add(new(fixture.Id, nodes, enteredActions, false, "no-available-choice")); return; }
            foreach (var choice in available)
            {
                coveredChoices.Add(choice.Id);
                var branchState = state.ToDictionary();
                Apply(choice.Actions, branchState);
                var branchActions = enteredActions.Concat(choice.Actions).ToArray();
                if (choice.TargetNodeId is null)
                    paths.Add(new(fixture.Id, nodes, branchActions, true, "completed"));
                else
                    Walk(choice.TargetNodeId, fixture, branchState, nodes, branchActions,
                        new HashSet<DialogueNodeId>(seen), depth + 1);
            }
        }
    }

    public static QuestQualityReport TestQuest(
        QuestDefinition quest,
        IReadOnlyList<NarrativeStateFixture>? fixtures = null,
        int maximumDepth = 64)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (maximumDepth is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        var selectedFixtures = fixtures ?? QuestFixtures(quest);
        var paths = new List<QuestTestPath>();
        var coveredStates = new HashSet<QuestStateId>();
        var coveredTransitions = new HashSet<QuestTransitionId>();
        foreach (var fixture in selectedFixtures.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var initialState = fixture.Values.ToDictionary();
            foreach (var injectedEvent in fixture.InjectedEvents)
                initialState[$"event:{injectedEvent}"] = "true";
            Walk(quest.InitialStateId, fixture, initialState, [], [], [], new HashSet<QuestStateId>(), true, 0);
        }

        var scopeCoverage = selectedFixtures.Select(fixture => fixture.Scope).ToHashSet();
        var passed = coveredStates.Count == quest.States.Count && coveredTransitions.Count == quest.Transitions.Count &&
            scopeCoverage.Contains(NarrativeFixtureScope.Player) && scopeCoverage.Contains(NarrativeFixtureScope.Party) &&
            paths.All(path => path.SaveResumeEqual && path.StopReason is "completed" or "no-available-transition");
        return new QuestQualityReport(quest.Id, passed, coveredStates.Count, quest.States.Count,
            coveredTransitions.Count, quest.Transitions.Count, scopeCoverage,
            paths.OrderBy(path => path.FixtureId, StringComparer.Ordinal).ThenBy(path => string.Join('/', path.States)).ToArray());

        void Walk(
            QuestStateId stateId,
            NarrativeStateFixture fixture,
            Dictionary<string, string> state,
            IReadOnlyList<QuestStateId> statePrefix,
            IReadOnlyList<QuestTransitionId> transitionPrefix,
            IReadOnlyList<QuestAction> actions,
            HashSet<QuestStateId> seen,
            bool saveResumeEqual,
            int depth)
        {
            var states = statePrefix.Append(stateId).ToArray();
            if (depth > maximumDepth) { paths.Add(new(fixture.Id, fixture.Scope, states, transitionPrefix, actions, saveResumeEqual, false, "maximum-depth")); return; }
            var definition = quest.States.SingleOrDefault(item => item.Id == stateId);
            if (definition is null) { paths.Add(new(fixture.Id, fixture.Scope, states, transitionPrefix, actions, false, false, "missing-state")); return; }
            coveredStates.Add(stateId);
            if (definition.IsTerminal) { paths.Add(new(fixture.Id, fixture.Scope, states, transitionPrefix, actions, saveResumeEqual, true, "completed")); return; }
            if (!seen.Add(stateId)) { paths.Add(new(fixture.Id, fixture.Scope, states, transitionPrefix, actions, saveResumeEqual, false, "cycle")); return; }
            var available = quest.Transitions.Where(transition => transition.FromStateId == stateId &&
                transition.Conditions.All(condition => Satisfied(condition, state))).ToArray();
            if (available.Length == 0) { paths.Add(new(fixture.Id, fixture.Scope, states, transitionPrefix, actions, saveResumeEqual, false, "no-available-transition")); return; }
            foreach (var transition in available)
            {
                coveredTransitions.Add(transition.Id);
                var machine = new QuestStateMachine(quest);
                machine.Restore(stateId);
                var result = machine.Advance(transition.Id, new QuestEvaluator(state));
                var resumed = new QuestStateMachine(quest);
                resumed.Restore(result.State.Id);
                var equal = saveResumeEqual && resumed.CurrentState.Id == result.State.Id;
                var branchState = state.ToDictionary();
                Apply(transition.Actions, branchState);
                Walk(result.State.Id, fixture, branchState, states, transitionPrefix.Append(transition.Id).ToArray(),
                    actions.Concat(transition.Actions).ToArray(), new HashSet<QuestStateId>(seen), equal, depth + 1);
            }
        }
    }

    private static IReadOnlyList<NarrativeStateFixture> DialogueFixtures(DialogueDefinition dialogue)
    {
        var fixtures = new List<NarrativeStateFixture> { new("dialogue-baseline", NarrativeFixtureScope.Player,
            new Dictionary<string, string>(), []) };
        var index = 0;
        foreach (var choice in dialogue.Nodes.SelectMany(node => node.Choices).Where(choice => choice.Conditions.Count > 0))
        {
            var values = choice.Conditions.ToDictionary(ConditionKey, condition => condition.ExpectedValue, StringComparer.Ordinal);
            fixtures.Add(new($"dialogue-branch-{index++:D3}", NarrativeFixtureScope.Player, values, []));
        }
        return fixtures;
    }

    private static IReadOnlyList<NarrativeStateFixture> QuestFixtures(QuestDefinition quest)
    {
        var all = quest.Transitions.SelectMany(transition => transition.Conditions)
            .GroupBy(ConditionKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().ExpectedValue, StringComparer.Ordinal);
        return
        [
            new("quest-player-baseline", NarrativeFixtureScope.Player, new Dictionary<string, string>(), []),
            new("quest-player-events", NarrativeFixtureScope.Player, all, all.Keys.Select(key => $"set:{key}").ToArray()),
            new("quest-party-events", NarrativeFixtureScope.Party, all, all.Keys.Select(key => $"set:{key}").ToArray()),
            new("quest-world-events", NarrativeFixtureScope.World, all, all.Keys.Select(key => $"set:{key}").ToArray()),
        ];
    }

    private static string ConditionKey(DialogueCondition condition) => condition.Kind switch
    {
        DialogueConditionKind.HasItem => $"item:{condition.Key}",
        DialogueConditionKind.QuestStateEquals => $"quest:{condition.Key}",
        _ => $"variable:{condition.Key}",
    };

    private static string ConditionKey(QuestCondition condition) => condition.Kind switch
    {
        QuestConditionKind.HasItem => $"item:{condition.Key}",
        _ => $"variable:{condition.Key}",
    };

    private static bool Satisfied(DialogueCondition condition, IReadOnlyDictionary<string, string> values) =>
        values.GetValueOrDefault(ConditionKey(condition)) == condition.ExpectedValue;

    private static bool Satisfied(QuestCondition condition, IReadOnlyDictionary<string, string> values) =>
        values.GetValueOrDefault(ConditionKey(condition)) == condition.ExpectedValue;

    private static void Apply(IEnumerable<DialogueAction> actions, IDictionary<string, string> state)
    {
        foreach (var action in actions) state[DialogueActionTransaction.StateKey(action)] = action.Value;
    }

    private static void Apply(IEnumerable<QuestAction> actions, IDictionary<string, string> state)
    {
        foreach (var action in actions) state[$"action:{action.Kind}:{action.Key}"] = action.Value;
    }

    private sealed class QuestEvaluator(IReadOnlyDictionary<string, string> values) : IQuestConditionEvaluator
    {
        public bool IsSatisfied(QuestCondition condition) => Satisfied(condition, values);
    }

    private sealed class DialogueEvaluator(IReadOnlyDictionary<string, string> values) : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => Satisfied(condition, values);
    }
}
