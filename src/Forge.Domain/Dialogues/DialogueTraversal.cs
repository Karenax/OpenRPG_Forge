namespace Forge.Domain.Dialogues;

public sealed class DialogueTraversal
{
    private readonly DialogueDefinition _dialogue;

    public DialogueNode? CurrentNode { get; private set; }

    public bool Completed => CurrentNode is null;

    public DialogueTraversal(DialogueDefinition dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        var diagnostics = DialogueValidator.Validate(dialogue);
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Dialogue is invalid: {string.Join("; ", diagnostics.Select(diagnostic => diagnostic.Message))}",
                nameof(dialogue));
        }

        _dialogue = dialogue;
        CurrentNode = dialogue.Nodes.Single(node => node.Id == dialogue.EntryNodeId);
    }

    public DialogueStep Start() => new(CurrentNode, CurrentNode!.OnEnterActions, Completed: false);

    public IReadOnlyList<DialogueChoice> GetAvailableChoices(IDialogueConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return CurrentNode?.Choices
            .Where(choice => choice.Conditions.All(evaluator.IsSatisfied))
            .ToArray() ?? [];
    }

    public DialogueStep Choose(DialogueChoiceId choiceId, IDialogueConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(choiceId);
        ArgumentNullException.ThrowIfNull(evaluator);
        if (CurrentNode is null)
        {
            throw new InvalidOperationException("The dialogue traversal is already complete.");
        }

        var choice = CurrentNode.Choices.SingleOrDefault(candidate => candidate.Id == choiceId)
            ?? throw new ArgumentException($"Choice '{choiceId}' is not available on the current node.", nameof(choiceId));
        if (!choice.Conditions.All(evaluator.IsSatisfied))
        {
            throw new InvalidOperationException($"Choice '{choiceId}' does not satisfy its conditions.");
        }

        if (choice.TargetNodeId is null)
        {
            CurrentNode = null;
            return new DialogueStep(null, choice.Actions, Completed: true);
        }

        CurrentNode = _dialogue.Nodes.Single(node => node.Id == choice.TargetNodeId);
        return new DialogueStep(
            CurrentNode,
            [.. choice.Actions, .. CurrentNode.OnEnterActions],
            Completed: false);
    }
}
