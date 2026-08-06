using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Domain.Quests;

namespace Forge.Application.Tests.Assistant;

public sealed class AssistedQuestCreationWorkflowTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GeneratorProducesDeterministicCanonicalQuestWithFailureRewardAndFiveScenarios()
    {
        var context = Context();

        var first = AssistedQuestCreationService.Generate(context.Brief, context.Project);
        var second = AssistedQuestCreationService.Generate(context.Brief, context.Project);

        Assert.True(first.Succeeded);
        Assert.True(first.CanApply);
        Assert.Empty(first.Lint);
        Assert.Empty(first.DependencyWarnings);
        Assert.Empty(first.StyleViolations);
        Assert.Equal(4, first.Quest.States.Count);
        Assert.Equal(3, first.Quest.Transitions.Count);
        Assert.Equal(2, first.AdvancedQuest.Objectives.Count);
        Assert.Single(first.AdvancedQuest.Participants);
        Assert.Equal(context.Brief.ProgressionReward, first.AdvancedQuest.Rewards.Progression);
        Assert.Contains(first.Quest.States, state => state.IsTerminal && state.DisplayName.Contains("chou", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Enum.GetValues<QuestTestKind>(), first.TestScenarios.Select(item => item.Kind));
        Assert.Equal(first.Quest.States.Select(item => item.Id), second.Quest.States.Select(item => item.Id));
        Assert.Equal(first.Quest.Transitions.Select(item => item.Id), second.Quest.Transitions.Select(item => item.Id));
        Assert.Contains("failure-path:1", first.SemanticDiff, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedProposalRequiresExactPreviewAndAppliesThroughAtomicProjectHistory()
    {
        var context = Context();
        var proposal = AssistedQuestCreationService.Generate(context.Brief, context.Project)
            .WithApproval(true, "author-approved:test:g12-f");
        var history = new ProjectCommandHistory(new ProjectState(context.Project, new ProjectRevision(0)));
        var handler = new ApplyAssistedQuestHandler(new FixedClock(Timestamp));
        var transactionId = Guid.NewGuid();
        var executionPlanId = Guid.NewGuid();
        ApplyAssistedQuestCommand Command(bool dryRun) => new(
            CommandId.From(Guid.NewGuid()), transactionId, executionPlanId, history.State.Revision,
            proposal, AssistantPermissionLevel.ApplyApprovedPlan, dryRun);

        var withoutPreview = handler.Handle(history.State, Command(false));
        var preview = handler.Handle(history.State, Command(true));
        var applied = handler.Handle(history.State, Command(false));

        Assert.Equal(ApplyAssistedQuestHandler.PlanNotPreviewedCode, withoutPreview.Diagnostics[0].Code);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Command.Audit.Applied);
        Assert.Empty(preview.Command.State.Project.Quests);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Command.Audit.Applied);
        Assert.Single(applied.Command.State.Project.Quests);
        history.Record(applied.Command);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Quests);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Quests);
    }

    [Fact]
    public void DraftOrInsufficientPermissionCannotApply()
    {
        var context = Context();
        var proposal = AssistedQuestCreationService.Generate(context.Brief, context.Project);
        var state = new ProjectState(context.Project, new ProjectRevision(0));
        var handler = new ApplyAssistedQuestHandler(new FixedClock(Timestamp));
        ApplyAssistedQuestCommand Command(AssistedQuestProposal value, AssistantPermissionLevel permission) =>
            new(CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), state.Revision,
                value, permission, true);

        var draft = handler.Handle(state, Command(proposal, AssistantPermissionLevel.ApplyApprovedPlan));
        var insufficient = handler.Handle(state, Command(
            proposal.WithApproval(true, "author-approved:test:g12-f"), AssistantPermissionLevel.Configure));

        Assert.Equal(ApplyAssistedQuestHandler.ApprovalRequiredCode, draft.Diagnostics[0].Code);
        Assert.Equal(ApplyAssistedQuestHandler.PermissionDeniedCode, insufficient.Diagnostics[0].Code);
    }

    private static TestContext Context()
    {
        var areaId = AreaId.From(Guid.Parse("b1270000-0000-4000-8000-000000000001"));
        var spawnId = BlueprintId.From(Guid.Parse("b1270000-0000-4000-8000-000000000002"));
        var guideId = BlueprintId.From(Guid.Parse("b1270000-0000-4000-8000-000000000003"));
        var spawn = new EntityInstance(
            EntityId.From(Guid.Parse("b1270000-0000-4000-8000-000000000004")),
            spawnId,
            TransformValue.Identity,
            ["player"]);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("b1270000-0000-4000-8000-000000000005")),
            "Assisted quest",
            [
                new BlueprintDefinition(spawnId, "spawn", "Spawn", BlueprintKind.PlayerSpawn),
                new BlueprintDefinition(guideId, "elyra", "Elyra", BlueprintKind.Creature),
            ],
            [new AreaDefinition(areaId, "quest-test", "Quest test", [spawn], AreaKind.Interior,
                new AreaDimensions(32, 32))]);
        var brief = new AssistedQuestBrief(
            Guid.Parse("b1270000-0000-4000-8000-000000000006"),
            "braises-perdues",
            "Les braises perdues",
            areaId.Value,
            guideId.Value,
            "Elyra demande de retrouver les braises avant l'effondrement de la crypte.",
            ["Trouver la première braise", "Rapporter les braises à Elyra"],
            ["lore:crypt-of-ashes"],
            ["style:crypt"],
            "quest.braises-perdues",
            "@author",
            20260806,
            250);
        return new(project, brief);
    }

    private sealed record TestContext(ProjectDefinition Project, AssistedQuestBrief Brief);
}
