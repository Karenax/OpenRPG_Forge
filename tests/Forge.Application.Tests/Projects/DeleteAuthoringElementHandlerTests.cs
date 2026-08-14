using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.Application.Tests.Projects;

public sealed class DeleteAuthoringElementHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AuthoringElementKind.Area)]
    [InlineData(AuthoringElementKind.Blueprint)]
    [InlineData(AuthoringElementKind.Dialogue)]
    [InlineData(AuthoringElementKind.Quest)]
    public void Handle_DryRunsThenDeletesEachIndependentAggregate(AuthoringElementKind kind)
    {
        var state = CreateState();
        var id = ResolveRemovableId(state.Project, kind);
        var handler = new DeleteAuthoringElementHandler(new FixedClock(Timestamp));
        var preview = new DeleteAuthoringElementCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            kind, id, DryRun: true);

        var dryRun = handler.Handle(state, preview);
        var applied = handler.Handle(state, preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });

        Assert.True(dryRun.Succeeded, string.Join(" | ", dryRun.Diagnostics.Select(item =>
            $"{item.Code}: {item.Message}")));
        Assert.False(dryRun.Audit.Applied);
        Assert.Same(state, dryRun.State);
        Assert.Equal("remove", dryRun.Patch?.Operation);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Audit.Applied);
        Assert.Equal(state.Revision.Next(), applied.State.Revision);
        Assert.DoesNotContain(id, AggregateIds(applied.State.Project, kind));
        Assert.Equal(AggregateIds(state.Project, kind).Count - 1, AggregateIds(applied.State.Project, kind).Count);
    }

    [Fact]
    public void Handle_RecordsAnUndoableDeletion()
    {
        var state = CreateState();
        var id = ResolveRemovableId(state.Project, AuthoringElementKind.Dialogue);
        var command = new DeleteAuthoringElementCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            AuthoringElementKind.Dialogue, id, DryRun: false);
        var result = new DeleteAuthoringElementHandler(new FixedClock(Timestamp)).Handle(state, command);
        var history = new ProjectCommandHistory(state);

        history.Record(result);

        Assert.Empty(history.State.Project.Dialogues);
        Assert.True(history.Undo().Succeeded);
        Assert.Single(history.State.Project.Dialogues);
        Assert.True(history.Redo().Succeeded);
        Assert.Empty(history.State.Project.Dialogues);
    }

    [Fact]
    public void Handle_ProtectsEntryAreaLastAreaAndInstalledBlueprint()
    {
        var state = CreateState();
        var handler = new DeleteAuthoringElementHandler(new FixedClock(Timestamp));
        CommandResult Delete(AuthoringElementKind kind, Guid id) => handler.Handle(state,
            new DeleteAuthoringElementCommand(CommandId.From(Guid.NewGuid()), state.Revision, kind, id, false));

        var entry = Delete(AuthoringElementKind.Area, state.Project.Module.EntryAreaId.Value);
        var installed = state.Project.Blueprints.Single(item => item.IsReadOnly());
        var readOnly = Delete(AuthoringElementKind.Blueprint, installed.Id.Value);
        var oneAreaProject = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, [state.Project.Areas[0]], state.Project.Dialogues, state.Project.Quests);
        var oneAreaState = new ProjectState(oneAreaProject, state.Revision);
        var lastArea = handler.Handle(oneAreaState, new DeleteAuthoringElementCommand(CommandId.From(Guid.NewGuid()),
            oneAreaState.Revision, AuthoringElementKind.Area, oneAreaProject.Areas[0].Id.Value, false));

        Assert.Equal(DeleteAuthoringElementHandler.EntryAreaCode, Assert.Single(entry.Diagnostics).Code);
        Assert.Equal(DeleteAuthoringElementHandler.ReadOnlyCode, Assert.Single(readOnly.Diagnostics).Code);
        Assert.Equal(DeleteAuthoringElementHandler.LastAreaCode, Assert.Single(lastArea.Diagnostics).Code);
    }

    [Fact]
    public void Handle_RejectsUnknownElementAndStaleRevision()
    {
        var state = CreateState();
        var handler = new DeleteAuthoringElementHandler(new FixedClock(Timestamp));
        var unknown = handler.Handle(state, new DeleteAuthoringElementCommand(CommandId.From(Guid.NewGuid()),
            state.Revision, AuthoringElementKind.Quest, Guid.NewGuid(), false));
        var stale = handler.Handle(state, new DeleteAuthoringElementCommand(CommandId.From(Guid.NewGuid()),
            new ProjectRevision(state.Revision.Value - 1), AuthoringElementKind.Quest,
            state.Project.Quests[0].Id.Value, false));

        Assert.Equal(DeleteAuthoringElementHandler.NotFoundCode, Assert.Single(unknown.Diagnostics).Code);
        Assert.Equal(DeleteAuthoringElementHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
    }

    private static ProjectState CreateState()
    {
        var entry = new AreaDefinition(AreaId.From(Guid.Parse("a4010000-0000-4000-8000-000000000001")),
            "entry", "Entry", []);
        var removableArea = new AreaDefinition(AreaId.From(Guid.Parse("a4010000-0000-4000-8000-000000000002")),
            "removable", "Removable", []);
        var speaker = new BlueprintDefinition(BlueprintId.From(Guid.Parse("b4010000-0000-4000-8000-000000000001")),
            "speaker", "Speaker", BlueprintKind.Creature);
        var removableBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("b4010000-0000-4000-8000-000000000002")),
            "removable", "Removable", BlueprintKind.Placeable);
        var installedId = BlueprintId.From(Guid.Parse("b4010000-0000-4000-8000-000000000003"));
        var installedProvenance = new BlueprintProvenance(BlueprintProvenance.CurrentSchemaVersion,
            "pack://forge-core/1.0.0/installed", "OpenRPG Forge", "CC0-1.0", new string('a', 64));
        var installedBlueprint = new BlueprintDefinition(
            installedId,
            "installed", "Installed", BlueprintKind.Placeable,
            paletteOrigin: BlueprintPaletteOrigin.InstalledPack,
            provenance: installedProvenance);
        var nodeId = DialogueNodeId.From(Guid.Parse("d4010000-0000-4000-8000-000000000001"));
        var dialogue = new DialogueDefinition(DialogueId.From(Guid.Parse("d4010000-0000-4000-8000-000000000002")),
            "dialogue", "Dialogue", nodeId,
            [new DialogueNode(nodeId, speaker.Id, new LocalizedText("dialogue.line", "Hello"))]);
        var questStateId = QuestStateId.From(Guid.Parse("e4010000-0000-4000-8000-000000000001"));
        var quest = new QuestDefinition(QuestId.From(Guid.Parse("e4010000-0000-4000-8000-000000000002")),
            "quest", "Quest", questStateId,
            [new QuestStateDefinition(questStateId, "Done", new QuestJournalEntry("quest.done", "Done"),
                isTerminal: true)]);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("f4010000-0000-4000-8000-000000000001")),
            "Manual authoring", [speaker, removableBlueprint, installedBlueprint], [entry, removableArea],
            [dialogue], [quest]);
        return new ProjectState(project, new ProjectRevision(8));
    }

    private static Guid ResolveRemovableId(ProjectDefinition project, AuthoringElementKind kind) => kind switch
    {
        AuthoringElementKind.Area => project.Areas.Single(item => item.Id != project.Module.EntryAreaId).Id.Value,
        AuthoringElementKind.Blueprint => project.Blueprints.Single(item => item.Slug == "removable").Id.Value,
        AuthoringElementKind.Dialogue => project.Dialogues.Single().Id.Value,
        AuthoringElementKind.Quest => project.Quests.Single().Id.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static IReadOnlyList<Guid> AggregateIds(ProjectDefinition project, AuthoringElementKind kind) => kind switch
    {
        AuthoringElementKind.Area => project.Areas.Select(item => item.Id.Value).ToArray(),
        AuthoringElementKind.Blueprint => project.Blueprints.Select(item => item.Id.Value).ToArray(),
        AuthoringElementKind.Dialogue => project.Dialogues.Select(item => item.Id.Value).ToArray(),
        AuthoringElementKind.Quest => project.Quests.Select(item => item.Id.Value).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
