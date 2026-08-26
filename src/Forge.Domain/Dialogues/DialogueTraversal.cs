namespace Forge.Domain.Dialogues;

public sealed class DialogueTraversal
{
    private readonly DialogueDefinition _dialogue;

    public DialogueNode? CurrentNode { get; private set; }

    public bool Completed => CurrentNode is null;

    public DialogueTraversal(DialogueDefinition dialogue)
        : this(dialogue, dialogue?.EntryNodeId ?? throw new ArgumentNullException(nameof(dialogue)))
    {
    }

    public DialogueTraversal(DialogueDefinition dialogue, DialogueNodeId entryNodeId)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(entryNodeId);
        var diagnostics = DialogueValidator.Validate(dialogue);
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Dialogue is invalid: {string.Join("; ", diagnostics.Select(diagnostic => diagnostic.Message))}",
                nameof(dialogue));
        }

        _dialogue = dialogue;
        CurrentNode = dialogue.Nodes.SingleOrDefault(node => node.Id == entryNodeId)
            ?? throw new ArgumentException(
                $"Dialogue entry node '{entryNodeId}' is not part of dialogue '{dialogue.Id}'.",
                nameof(entryNodeId));
    }

    public DialogueStep Start() => new(CurrentNode, CurrentNode!.OnEnterActions, Completed: false);

    public IReadOnlyList<DialogueChoice> GetAvailableChoices(IDialogueConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return CurrentNode?.Choices
            .Select((choice, index) => (choice, index))
            .Where(item => DialogueConditionLogic.Evaluate(item.choice, evaluator).Available)
            .OrderBy(item => item.choice.Priority)
            .ThenBy(item => item.index)
            .Select(item => item.choice)
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
        if (!DialogueConditionLogic.Evaluate(choice, evaluator).Available)
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
