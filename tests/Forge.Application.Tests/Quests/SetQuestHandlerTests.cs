using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.Application.Tests.Quests;

public sealed class SetQuestHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Handle_AddsQuestAndSupportsUndo()
    {
        var state = CreateState();
        var handler = new SetQuestHandler(new FixedClock(Timestamp));
        var command = CreateCommand(state, CreateQuest());

        var result = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(result);

        Assert.True(result.Succeeded);
        Assert.Equal("add", result.Patch?.Operation);
        Assert.Single(history.State.Project.Quests);
        Assert.Equal(Timestamp, result.Audit.Timestamp);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Quests);
    }

    [Fact]
    public void Handle_ReplacesExistingQuest()
    {
        var initialQuest = CreateQuest();
        var state = CreateState([initialQuest]);
        var handler = new SetQuestHandler(new FixedClock(Timestamp));
        var replacement = new QuestDefinition(
            initialQuest.Id,
            initialQuest.Slug,
            "Quête révisée",
            initialQuest.InitialStateId,
            initialQuest.States,
            initialQuest.Transitions);

        var result = handler.Handle(state, CreateCommand(state, replacement));

        Assert.True(result.Succeeded);
        Assert.Equal("replace", result.Patch?.Operation);
        Assert.Equal("Quête révisée", Assert.Single(result.State.Project.Quests).DisplayName);
    }

    [Fact]
    public void Handle_DryRunDoesNotMutateState()
    {
        var state = CreateState();
        var handler = new SetQuestHandler(new FixedClock(Timestamp));
        var command = CreateCommand(state, CreateQuest()) with { DryRun = true };

        var result = handler.Handle(state, command);

        Assert.True(result.Succeeded);
        Assert.Same(state, result.State);
        Assert.Null(result.NewRevision);
    }

    [Fact]
    public void Handle_RejectsRevisionConflict()
    {
        var state = CreateState();
        var handler = new SetQuestHandler(new FixedClock(Timestamp));
        var command = CreateCommand(state, CreateQuest()) with { BaseRevision = new ProjectRevision(1) };

        var result = handler.Handle(state, command);

        Assert.False(result.Succeeded);
        Assert.Equal(SetQuestHandler.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Handle_RejectsInvalidQuestGraph()
    {
        var state = CreateState();
        var handler = new SetQuestHandler(new FixedClock(Timestamp));
        var valid = CreateQuest();
        var invalid = new QuestDefinition(
            valid.Id,
            valid.Slug,
            valid.DisplayName,
            QuestStateId.From(Guid.Parse("90000000-0000-4000-8000-000000000099")),
            valid.States,
            valid.Transitions);

        var result = handler.Handle(state, CreateCommand(state, invalid));

        Assert.False(result.Succeeded);
        Assert.Equal(QuestValidator.MissingInitialStateCode, Assert.Single(result.Diagnostics).Code);
    }

    private static ProjectState CreateState(IReadOnlyList<QuestDefinition>? quests = null)
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("91000000-0000-4000-8000-000000000001")),
            "quest-area",
            "Quest area",
            []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("92000000-0000-4000-8000-000000000001")),
            "Quest project",
            [],
            [area],
            quests: quests);
        return new ProjectState(project, new ProjectRevision(6));
    }

    private static SetQuestCommand CreateCommand(ProjectState state, QuestDefinition quest) =>
        new(
            CommandId.From(Guid.Parse("93000000-0000-4000-8000-000000000001")),
            state.Revision,
            quest,
            DryRun: false);

    private static QuestDefinition CreateQuest()
    {
        var initialId = QuestStateId.From(Guid.Parse("94000000-0000-4000-8000-000000000001"));
        var completedId = QuestStateId.From(Guid.Parse("94000000-0000-4000-8000-000000000002"));
        return new QuestDefinition(
            QuestId.From(Guid.Parse("95000000-0000-4000-8000-000000000001")),
            "quest-test",
            "Quête test",
            initialId,
            [
                new QuestStateDefinition(
                    initialId,
                    "Initiale",
                    new QuestJournalEntry("quest.test.initial", "Commencez la quête.")),
                new QuestStateDefinition(
                    completedId,
                    "Terminée",
                    new QuestJournalEntry("quest.test.completed", "Quête terminée."),
                    isTerminal: true),
            ],
            [new QuestTransition(
                QuestTransitionId.From(Guid.Parse("96000000-0000-4000-8000-000000000001")),
                initialId,
                completedId)]);
    }
}
