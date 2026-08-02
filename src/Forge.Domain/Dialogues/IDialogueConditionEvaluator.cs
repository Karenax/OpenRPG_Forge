namespace Forge.Domain.Dialogues;

public interface IDialogueConditionEvaluator
{
    bool IsSatisfied(DialogueCondition condition);
}
