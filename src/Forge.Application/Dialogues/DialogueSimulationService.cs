using Forge.Domain.Dialogues;
using Forge.Domain.Validation;

namespace Forge.Application.Dialogues;

public sealed record DialogueSimulationJournalEntry(
    int Sequence,
    string Kind,
    string Message,
    DialogueNodeId? NodeId,
    DialogueChoiceId? ChoiceId);

public sealed record DialogueSimulationResult(
    bool Succeeded,
    bool Completed,
    string StopReason,
    DialogueSandboxState FinalState,
    IReadOnlyList<DialogueNodeId> VisitedNodes,
    IReadOnlyList<DialogueAction> AppliedActions,
    IReadOnlyList<DialogueSimulationJournalEntry> Journal,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class DialogueSimulationService
{
    public const string ChoiceUnavailableCode = "dialogue.simulation.choice_unavailable";
    public const string StepLimitCode = "dialogue.simulation.step_limit";

    public static DialogueSimulationResult Run(
        DialogueDefinition dialogue,
        IReadOnlyDictionary<string, string>? initialState = null,
        IReadOnlyList<DialogueChoiceId>? scriptedChoices = null,
        int maximumSteps = 64)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        if (maximumSteps is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(maximumSteps));
        var structural = DialogueValidator.Validate(dialogue);
        var state = new DialogueSandboxState((initialState ?? new Dictionary<string, string>())
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
        if (structural.Count > 0)
            return new(false, false, "invalid-dialogue", state, [], [], [], structural);

        var nodes = dialogue.Nodes.ToDictionary(item => item.Id);
        var current = nodes[dialogue.EntryNodeId];
        var visited = new List<DialogueNodeId>();
        var applied = new List<DialogueAction>();
        var journal = new List<DialogueSimulationJournalEntry>();
        var diagnostics = new List<DomainDiagnostic>();
        var scriptedIndex = 0;
        for (var step = 0; step < maximumSteps; step++)
        {
            visited.Add(current.Id);
            journal.Add(new(journal.Count, "node", $"Enter {current.Text.Key}: {current.Text.Fallback}", current.Id, null));
            var enter = DialogueActionTransaction.Apply(state, current.OnEnterActions,
                $"dialogues/{dialogue.Id}/nodes/{current.Id}/onEnterActions");
            diagnostics.AddRange(enter.Diagnostics);
            if (!enter.Succeeded)
                return new(false, false, "action-rollback", state, visited, applied, journal, diagnostics);
            state = enter.State;
            applied.AddRange(enter.AppliedActions);

            if (current.Choices.Count == 0 || current.Role == DialogueNodeRole.End)
            {
                journal.Add(new(journal.Count, "complete", "Reached a terminal node.", current.Id, null));
                return new(true, true, "completed", state, visited, applied, journal, diagnostics);
            }

            var evaluator = new SandboxEvaluator(state.Values);
            var decisions = current.Choices.Select((choice, index) =>
                    (choice, index, decision: DialogueConditionLogic.Evaluate(choice, evaluator)))
                .OrderBy(item => item.choice.Priority)
                .ThenBy(item => item.index)
                .ToArray();
            foreach (var item in decisions)
            {
                var explanation = item.decision.Conditions.Count == 0
                    ? "unconditional"
                    : string.Join("; ", item.decision.Conditions.Select(condition => condition.Explanation));
                journal.Add(new(journal.Count, "decision",
                    $"{item.decision.Combination}: {explanation} => {(item.decision.Available ? "available" : "hidden")}",
                    current.Id, item.choice.Id));
            }

            DialogueChoice? selected;
            if (scriptedChoices is not null && scriptedIndex < scriptedChoices.Count)
            {
                var requested = scriptedChoices[scriptedIndex++];
                selected = decisions.FirstOrDefault(item => item.choice.Id == requested && item.decision.Available).choice;
                if (selected is null)
                {
                    diagnostics.Add(new DomainDiagnostic(ChoiceUnavailableCode,
                        $"Scripted choice '{requested}' is absent or hidden.",
                        $"dialogues/{dialogue.Id}/nodes/{current.Id}/choices/{requested}"));
                    return new(false, false, "choice-unavailable", state, visited, applied, journal, diagnostics);
                }
            }
            else
            {
                selected = decisions.FirstOrDefault(item => item.decision.Available).choice;
                if (selected is null)
                    return new(true, false, "no-available-choice", state, visited, applied, journal, diagnostics);
            }

            journal.Add(new(journal.Count, "choice", $"Select {selected.Text.Key}: {selected.Text.Fallback}",
                current.Id, selected.Id));
            var transaction = DialogueActionTransaction.Apply(state, selected.Actions,
                $"dialogues/{dialogue.Id}/nodes/{current.Id}/choices/{selected.Id}/actions");
            diagnostics.AddRange(transaction.Diagnostics);
            if (!transaction.Succeeded)
                return new(false, false, "action-rollback", state, visited, applied, journal, diagnostics);
            state = transaction.State;
            applied.AddRange(transaction.AppliedActions);
            if (selected.TargetNodeId is null)
            {
                journal.Add(new(journal.Count, "complete", "Choice ends the dialogue.", current.Id, selected.Id));
                return new(true, true, "completed", state, visited, applied, journal, diagnostics);
            }
            current = nodes[selected.TargetNodeId];
        }

        diagnostics.Add(new DomainDiagnostic(StepLimitCode,
            $"Dialogue simulation exceeded {maximumSteps} steps.", $"dialogues/{dialogue.Id}"));
        return new(false, false, "step-limit", state, visited, applied, journal, diagnostics);
    }

    private sealed class SandboxEvaluator(IReadOnlyDictionary<string, string> values) : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) =>
            values.GetValueOrDefault(StateKey(condition)) == condition.ExpectedValue;

        private static string StateKey(DialogueCondition condition) => condition.Kind switch
        {
            DialogueConditionKind.VariableEquals => $"variable:{condition.Key}",
            DialogueConditionKind.HasItem => $"item:{condition.Key}",
            DialogueConditionKind.QuestStateEquals => $"quest:{condition.Key}",
            DialogueConditionKind.RelationEquals => $"relation:{condition.Key}",
            DialogueConditionKind.EventRaised => $"event:{condition.Key}",
            _ => $"unsupported:{condition.Key}",
        };
    }
}
