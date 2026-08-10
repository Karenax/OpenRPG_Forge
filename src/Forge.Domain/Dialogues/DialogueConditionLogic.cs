namespace Forge.Domain.Dialogues;

public sealed record DialogueConditionDecision(
    Guid ConditionId,
    DialogueConditionKind Kind,
    string Key,
    string ExpectedValue,
    bool RawResult,
    bool Negated,
    bool Result,
    string Explanation);

public sealed record DialogueChoiceDecision(
    DialogueChoiceId ChoiceId,
    DialogueConditionCombination Combination,
    bool Available,
    IReadOnlyList<DialogueConditionDecision> Conditions);

public static class DialogueConditionLogic
{
    public static DialogueChoiceDecision Evaluate(DialogueChoice choice, IDialogueConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(evaluator);
        var decisions = choice.Conditions.Select(condition =>
        {
            var raw = evaluator.IsSatisfied(condition);
            var result = condition.Negated ? !raw : raw;
            return new DialogueConditionDecision(
                condition.Id,
                condition.Kind,
                condition.Key,
                condition.ExpectedValue,
                raw,
                condition.Negated,
                result,
                $"{(condition.Negated ? "NOT " : string.Empty)}{condition.Kind}({condition.Key}) " +
                $"expected '{condition.ExpectedValue}' => {(result ? "true" : "false")}");
        }).ToArray();
        var available = decisions.Length == 0 || choice.ConditionCombination switch
        {
            DialogueConditionCombination.All => decisions.All(item => item.Result),
            DialogueConditionCombination.Any => decisions.Any(item => item.Result),
            _ => false,
        };
        return new DialogueChoiceDecision(choice.Id, choice.ConditionCombination, available, decisions);
    }
}
