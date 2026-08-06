using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.Application.Tests.Quests;

public sealed class QuestOperationsWorkflowTests
{
    [Fact]
    public void Command_supports_dry_run_apply_and_typed_script_resolution()
    {
        var target = CreateQuest();
        var state = CreateState(target.WithOperations(null));
        var profile = target.Operations!;
        var handler = new SetQuestOperationsHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new SetQuestOperationsCommand(CommandId.From(Guid.NewGuid()), state.Revision, target.Id,
            profile, true);

        var preview = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        var updated = applied.State.Project.Quests.Single(item => item.Id == target.Id);
        var script = new QuestOperationsScriptService(updated);
        var snapshot = script.Read(script.Handle);

        Assert.True(preview.Succeeded);
        Assert.Equal(state, preview.State);
        Assert.Contains("advanced quest policies", preview.Patch!.Summary, StringComparison.Ordinal);
        Assert.True(applied.Succeeded);
        Assert.Equal(state.Revision.Next(), applied.NewRevision);
        Assert.Equal(1, snapshot.ExclusiveChoiceCount);
        Assert.True(snapshot.Repeatable);
        Assert.Equal(QuestShareMode.Unanimous, snapshot.ShareMode);
    }

    [Fact]
    public void Command_rejects_stale_revision_and_invalid_profile()
    {
        var target = CreateQuest();
        var state = CreateState(target.WithOperations(null));
        var profile = target.Operations!;
        var handler = new SetQuestOperationsHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var stale = handler.Handle(state, new SetQuestOperationsCommand(CommandId.From(Guid.NewGuid()),
            state.Revision.Next(), target.Id, profile, false));
        var invalid = handler.Handle(state, new SetQuestOperationsCommand(CommandId.From(Guid.NewGuid()),
            state.Revision, target.Id, profile with { SchemaVersion = 99 }, false));
        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");
        Assert.Contains(invalid.Diagnostics, item => item.Code == "quest.operations.identity_invalid");
    }

    private static ProjectState CreateState(QuestDefinition quest)
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("91000000-0000-4000-8000-000000000001")),
            "quest-area", "Quest area", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("92000000-0000-4000-8000-000000000001")),
            "Quest project", [], [area], quests: [quest]);
        return new ProjectState(project, new ProjectRevision(6));
    }

    private static QuestDefinition CreateQuest()
    {
        var start = QuestStateId.From(Guid.Parse("94000000-0000-4000-8000-000000000001"));
        var completed = QuestStateId.From(Guid.Parse("94000000-0000-4000-8000-000000000002"));
        var transition = QuestTransitionId.From(Guid.Parse("96000000-0000-4000-8000-000000000001"));
        var profile = new QuestOperationsProfile(Guid.Parse("97000000-0000-4000-8000-000000000001"), 1,
            "test:g13-j", false,
            new(QuestJournalSortingPolicy.PriorityDescending, 80, true, false, 90, true),
            new(true, 60, 0, QuestRepeatResetMode.Full, [], 0.9, 12),
            new(QuestShareMode.Unanimous, true, QuestSeparationPolicy.SnapshotIndividual, true),
            [new(Guid.Parse("98000000-0000-4000-8000-000000000001"), transition, "ending", true,
                new LocalizedText("quest.confirm", "Confirmer ?"),
                new LocalizedText("quest.consequence", "La quête prendra fin."), [], "test:g13-j")],
            new(QuestNotificationMode.Grouped, new LocalizedText("quest.title", "Journal"),
                new LocalizedText("quest.body", "Objectif mis à jour."), "Objectif mis à jour", "quest", 2));
        return new QuestDefinition(QuestId.From(Guid.Parse("95000000-0000-4000-8000-000000000001")),
            "quest-test", "Quest test", start,
            [new(start, "Initial", new QuestJournalEntry("quest.initial", "Start")),
                new(completed, "Completed", new QuestJournalEntry("quest.completed", "Done"), true)],
            [new(transition, start, completed)], profile);
    }
}
