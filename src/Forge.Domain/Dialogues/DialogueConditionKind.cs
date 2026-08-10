namespace Forge.Domain.Dialogues;

public enum DialogueConditionKind
{
    VariableEquals,
    HasItem,
    QuestStateEquals,
    RelationEquals,
    EventRaised,
}

public enum DialogueConditionCombination
{
    All,
    Any,
}
