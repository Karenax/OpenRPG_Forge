using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Domain.Dialogues;

namespace Forge.Application.Tests.Assistant;

public sealed class AssistedDialogueCreationWorkflowTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 5, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GeneratorProducesDeterministicCanonicalGraphWithCitationsStyleLintAndLocalization()
    {
        var context = Context();

        var first = AssistedDialogueCreationService.Generate(context.Brief, context.Project);
        var second = AssistedDialogueCreationService.Generate(context.Brief, context.Project);

        Assert.True(first.Succeeded);
        Assert.True(first.CanApply);
        Assert.Empty(first.Lint);
        Assert.Empty(first.StyleViolations);
        Assert.Equal(context.Brief.NodeCount, first.Dialogue.Nodes.Count);
        Assert.Equal(context.Brief.NodeCount, first.LocalizationPlaceholders.Count);
        Assert.Equal(first.Dialogue.Nodes.Select(item => item.Id), second.Dialogue.Nodes.Select(item => item.Id));
        Assert.Equal(first.Dialogue.Nodes.Select(item => item.Text), second.Dialogue.Nodes.Select(item => item.Text));
        Assert.Contains(first.Dialogue.Nodes.SelectMany(item => item.Choices), item => item.Conditions.Count > 0);
        Assert.Contains(first.Dialogue.Nodes.SelectMany(item => item.Choices), item => item.Actions.Count > 0);
        Assert.Contains("citations:1", first.SemanticDiff, StringComparison.Ordinal);
        var participant = first.Graph.Participants[0];
        var simulation = DialogueGraphService.Render(first.Graph, first.Graph.EntryNodeId,
            new DialogueContext(
                new Dictionary<string, object>
                {
                    ["player_name"] = "Voyageur",
                    ["assistant.dialogue.ready"] = true,
                },
                new Dictionary<string, string> { [participant.Role] = participant.Value },
                context.Brief.Seed));
        Assert.Empty(simulation.Diagnostics);
        Assert.Single(simulation.AvailableEdges);
    }

    [Fact]
    public void ApprovedProposalRequiresExactPreviewAndAppliesThroughAtomicProjectHistory()
    {
        var context = Context();
        var proposal = AssistedDialogueCreationService.Generate(context.Brief, context.Project)
            .WithApproval(true, "author-approved:test:g12-e");
        var state = new ProjectState(context.Project, new ProjectRevision(0));
        var history = new ProjectCommandHistory(state);
        var handler = new ApplyAssistedDialogueHandler(new FixedClock(Timestamp));
        var transactionId = Guid.NewGuid();
        var executionPlanId = Guid.NewGuid();
        ApplyAssistedDialogueCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transactionId, executionPlanId, history.State.Revision,
            proposal, AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        var withoutPreview = handler.Handle(history.State, Command(false));
        var preview = handler.Handle(history.State, Command(true));
        var applied = handler.Handle(history.State, Command(false));

        Assert.Equal(ApplyAssistedDialogueHandler.PlanNotPreviewedCode, withoutPreview.Diagnostics[0].Code);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Command.Audit.Applied);
        Assert.Empty(preview.Command.State.Project.Dialogues);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Command.Audit.Applied);
        Assert.Single(applied.Command.State.Project.Dialogues);
        history.Record(applied.Command);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Dialogues);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Dialogues);
    }

    [Fact]
    public void DraftOrInsufficientPermissionCannotApply()
    {
        var context = Context();
        var proposal = AssistedDialogueCreationService.Generate(context.Brief, context.Project);
        var state = new ProjectState(context.Project, new ProjectRevision(0));
        var handler = new ApplyAssistedDialogueHandler(new FixedClock(Timestamp));
        ApplyAssistedDialogueCommand Command(AssistedDialogueProposal value, AssistantPermissionLevel permission) =>
            new(CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), state.Revision,
                value, permission, true);

        var draft = handler.Handle(state, Command(proposal, AssistantPermissionLevel.ApplyApprovedPlan));
        var insufficient = handler.Handle(state, Command(
            proposal.WithApproval(true, "author-approved:test:g12-e"), AssistantPermissionLevel.Configure));

        Assert.Equal(ApplyAssistedDialogueHandler.ApprovalRequiredCode, draft.Diagnostics[0].Code);
        Assert.Equal(ApplyAssistedDialogueHandler.PermissionDeniedCode, insufficient.Diagnostics[0].Code);
    }

    private static TestContext Context()
    {
        var areaId = AreaId.From(Guid.Parse("b1260000-0000-4000-8000-000000000001"));
        var spawnId = BlueprintId.From(Guid.Parse("b1260000-0000-4000-8000-000000000002"));
        var speakerId = BlueprintId.From(Guid.Parse("b1260000-0000-4000-8000-000000000003"));
        var spawn = new EntityInstance(
            EntityId.From(Guid.Parse("b1260000-0000-4000-8000-000000000004")),
            spawnId,
            TransformValue.Identity,
            ["player"]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("b1260000-0000-4000-8000-000000000005")),
            "Assisted dialogue",
            [
                new BlueprintDefinition(spawnId, "spawn", "Spawn", BlueprintKind.PlayerSpawn),
                new BlueprintDefinition(speakerId, "elyra", "Elyra", BlueprintKind.Creature),
            ],
            [new AreaDefinition(areaId, "dialogue-test", "Dialogue test", [spawn], AreaKind.Interior,
                new AreaDimensions(32, 32))]);
        var brief = new AssistedDialogueBrief(
            Guid.Parse("b1260000-0000-4000-8000-000000000006"),
            "serment-assiste",
            "Le serment assisté",
            speakerId.Value,
            "Elyra révèle le serment et demande un choix au joueur.",
            ["Présenter le serment", "Révéler le risque", "Demander un choix", "Conclure"],
            ["style:crypt"],
            ["lore:serment-braise"],
            "fr",
            "dialogue.serment-assiste",
            "@author",
            20260805,
            4);
        return new(project, brief);
    }

    private sealed record TestContext(ProjectDefinition Project, AssistedDialogueBrief Brief);
}
