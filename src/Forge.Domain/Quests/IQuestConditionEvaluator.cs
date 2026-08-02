namespace Forge.Domain.Quests;

public interface IQuestConditionEvaluator
{
    bool IsSatisfied(QuestCondition condition);
}
