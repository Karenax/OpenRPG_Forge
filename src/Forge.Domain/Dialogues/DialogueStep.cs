namespace Forge.Domain.Dialogues;

public sealed record DialogueStep(
    DialogueNode? Node,
    IReadOnlyList<DialogueAction> Actions,
    bool Completed);
