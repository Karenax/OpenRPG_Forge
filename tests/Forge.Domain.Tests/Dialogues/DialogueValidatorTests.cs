using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialogueValidatorTests
{
    [Fact]
    public void Validate_AcceptsConnectedDialogue()
    {
        Assert.Empty(DialogueValidator.Validate(DialogueTestFactory.Create()));
    }

    [Fact]
    public void Validate_ReportsMissingEntryNode()
    {
        var valid = DialogueTestFactory.Create();
        var invalid = new DialogueDefinition(
            valid.Id,
            valid.Slug,
            valid.DisplayName,
            DialogueNodeId.From(Guid.Parse("4c968070-80e3-43a5-8fce-5667f0bb6226")),
            valid.Nodes);

        var diagnostic = Assert.Single(DialogueValidator.Validate(invalid));

        Assert.Equal(DialogueValidator.MissingEntryNodeCode, diagnostic.Code);
    }

    [Fact]
    public void Validate_ReportsMissingChoiceTarget()
    {
        var missingTarget = DialogueNodeId.From(Guid.Parse("0190330c-8bcb-4804-949b-84d39b078f48"));
        var entry = new DialogueNode(
            DialogueTestFactory.EntryNodeId,
            null,
            new LocalizedText("dialogue.entry", "Entrée"),
            choices: [new DialogueChoice(
                DialogueTestFactory.AcceptChoiceId,
                new LocalizedText("dialogue.choice", "Continuer"),
                targetNodeId: missingTarget)]);
        var invalid = new DialogueDefinition(
            DialogueId.From(Guid.Parse("b3c925a7-dc3a-4ce1-8edc-d45e60d7742b")),
            "invalid-target",
            "Invalid target",
            entry.Id,
            [entry]);

        var diagnostic = Assert.Single(DialogueValidator.Validate(invalid));

        Assert.Equal(DialogueValidator.MissingTargetNodeCode, diagnostic.Code);
    }

    [Fact]
    public void Validate_ReportsUnreachableNode()
    {
        var valid = DialogueTestFactory.Create();
        var unreachable = new DialogueNode(
            DialogueNodeId.From(Guid.Parse("5508b7ae-0961-479f-8978-656836872693")),
            null,
            new LocalizedText("dialogue.unreachable", "Jamais affiché"));
        var invalid = new DialogueDefinition(
            valid.Id,
            valid.Slug,
            valid.DisplayName,
            valid.EntryNodeId,
            [.. valid.Nodes, unreachable]);

        var diagnostic = Assert.Single(DialogueValidator.Validate(invalid));

        Assert.Equal(DialogueValidator.UnreachableNodeCode, diagnostic.Code);
    }
}
