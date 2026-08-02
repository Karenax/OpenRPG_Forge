using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialogueTraversalTests
{
    [Fact]
    public void Traversal_FiltersChoicesAndReturnsActionsAcrossACompletePath()
    {
        var dialogue = DialogueTestFactory.Create();
        var evaluator = new TestEvaluator();
        var traversal = new DialogueTraversal(dialogue);

        var start = traversal.Start();
        Assert.Equal(DialogueTestFactory.EntryNodeId, start.Node?.Id);
        Assert.Single(traversal.GetAvailableChoices(evaluator));

        evaluator.SatisfiedKeys.Add("ash-key");
        Assert.Equal(2, traversal.GetAvailableChoices(evaluator).Count);

        var reward = traversal.Choose(DialogueTestFactory.AcceptChoiceId, evaluator);
        Assert.Equal(DialogueTestFactory.RewardNodeId, reward.Node?.Id);
        Assert.Equal(
            [DialogueActionKind.StartQuest, DialogueActionKind.SetVariable],
            reward.Actions.Select(action => action.Kind));

        var completed = traversal.Choose(DialogueTestFactory.FinishChoiceId, evaluator);
        Assert.True(completed.Completed);
        Assert.True(traversal.Completed);
        Assert.Equal(DialogueActionKind.AdvanceQuest, Assert.Single(completed.Actions).Kind);
    }

    [Fact]
    public void Traversal_RejectsChoiceWithUnsatisfiedCondition()
    {
        var traversal = new DialogueTraversal(DialogueTestFactory.Create());

        Assert.Throws<InvalidOperationException>(() =>
            traversal.Choose(DialogueTestFactory.AcceptChoiceId, new TestEvaluator()));
    }

    [Fact]
    public void Definition_RejectsDuplicateChoiceIdsAcrossNodes()
    {
        var valid = DialogueTestFactory.Create();
        var duplicate = new DialogueNode(
            DialogueNodeId.From(Guid.Parse("a5d72cc0-d3b2-4de9-8ba2-5d074691c0a4")),
            null,
            new LocalizedText("dialogue.duplicate", "Duplicate"),
            choices: [new DialogueChoice(
                DialogueTestFactory.AcceptChoiceId,
                new LocalizedText("dialogue.duplicate.choice", "Duplicate"))]);

        Assert.Throws<ArgumentException>(() => new DialogueDefinition(
            valid.Id,
            valid.Slug,
            valid.DisplayName,
            valid.EntryNodeId,
            [.. valid.Nodes, duplicate]));
    }

    private sealed class TestEvaluator : IDialogueConditionEvaluator
    {
        internal HashSet<string> SatisfiedKeys { get; } = [];

        public bool IsSatisfied(DialogueCondition condition) => SatisfiedKeys.Contains(condition.Key);
    }
}
