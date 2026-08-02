using Forge.Application.Commands;
using Forge.Application.Dialogues;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Tests.Dialogues;

public sealed class SetDialogueHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Handle_AddsDialogueAndAdvancesRevision()
    {
        var context = CreateContext();

        var result = context.Handler.Handle(context.State, CreateCommand(context, CreateDialogue(context.BlueprintId)));

        Assert.True(result.Succeeded);
        Assert.Equal(new ProjectRevision(5), result.State.Revision);
        Assert.Equal("add", result.Patch?.Operation);
        Assert.Equal("remove", result.Inverse?.Operation);
        Assert.Single(result.State.Project.Dialogues);
        Assert.Equal(Timestamp, result.Audit.Timestamp);
    }

    [Fact]
    public void Handle_ReplacesExistingDialogueAndSupportsHistory()
    {
        var context = CreateContext();
        var initial = context.Handler.Handle(context.State, CreateCommand(context, CreateDialogue(context.BlueprintId)));
        var history = new ProjectCommandHistory(context.State);
        history.Record(initial);
        var replacement = CreateDialogue(context.BlueprintId, "Dialogue révisé");

        var result = context.Handler.Handle(
            history.State,
            new SetDialogueCommand(
                CommandId.From(Guid.Parse("141f86a9-c3e1-4672-9a7e-96984ac7b7a7")),
                history.State.Revision,
                replacement,
                DryRun: false));
        history.Record(result);

        Assert.Equal("replace", result.Patch?.Operation);
        Assert.Equal("Dialogue révisé", Assert.Single(history.State.Project.Dialogues).DisplayName);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal("Dialogue test", Assert.Single(history.State.Project.Dialogues).DisplayName);
    }

    [Fact]
    public void Handle_DryRunValidatesWithoutMutation()
    {
        var context = CreateContext();
        var command = CreateCommand(context, CreateDialogue(context.BlueprintId)) with { DryRun = true };

        var result = context.Handler.Handle(context.State, command);

        Assert.True(result.Succeeded);
        Assert.Same(context.State, result.State);
        Assert.Null(result.NewRevision);
        Assert.False(result.Audit.Applied);
    }

    [Fact]
    public void Handle_RejectsRevisionConflict()
    {
        var context = CreateContext();
        var command = CreateCommand(context, CreateDialogue(context.BlueprintId)) with
        {
            BaseRevision = new ProjectRevision(3),
        };

        var result = context.Handler.Handle(context.State, command);

        Assert.False(result.Succeeded);
        Assert.Equal(SetDialogueHandler.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Handle_RejectsMissingSpeakerBlueprint()
    {
        var context = CreateContext();
        var missing = BlueprintId.From(Guid.Parse("52705cb3-078a-43b4-87f2-f0a2ac0e92a0"));

        var result = context.Handler.Handle(context.State, CreateCommand(context, CreateDialogue(missing)));

        Assert.False(result.Succeeded);
        Assert.Equal(ProjectValidator.MissingDialogueSpeakerBlueprintCode, Assert.Single(result.Diagnostics).Code);
    }

    private static TestContext CreateContext()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("37ebc020-109f-40f6-9a57-b06d38fdde66")),
            "dialogue-test",
            "Dialogue test",
            []);
        var blueprintId = BlueprintId.From(Guid.Parse("22dbcd19-3096-45bb-928d-d79b5c64491e"));
        var blueprint = new BlueprintDefinition(blueprintId, "speaker", "Speaker", BlueprintKind.Creature);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("3bc73450-e68b-44c2-ab27-b05b6c60f08f")),
            "Dialogue project",
            [blueprint],
            [area]);
        return new TestContext(
            new SetDialogueHandler(new FixedClock(Timestamp)),
            new ProjectState(project, new ProjectRevision(4)),
            blueprintId);
    }

    private static SetDialogueCommand CreateCommand(TestContext context, DialogueDefinition dialogue) =>
        new(
            CommandId.From(Guid.Parse("46539214-c3e9-4729-b5a6-827d1590a729")),
            context.State.Revision,
            dialogue,
            DryRun: false);

    private static DialogueDefinition CreateDialogue(BlueprintId speakerId, string displayName = "Dialogue test")
    {
        var nodeId = DialogueNodeId.From(Guid.Parse("d2a410da-d3c7-4dfc-a114-87a405640191"));
        return new DialogueDefinition(
            DialogueId.From(Guid.Parse("60aff217-c3aa-457b-9b86-1b57d193fa38")),
            "dialogue-test",
            displayName,
            nodeId,
            [new DialogueNode(
                nodeId,
                speakerId,
                new LocalizedText("dialogue.test", "Test"))]);
    }

    private sealed record TestContext(
        SetDialogueHandler Handler,
        ProjectState State,
        BlueprintId BlueprintId);
}
