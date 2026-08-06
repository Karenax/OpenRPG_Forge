using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Tests.Creatures;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Blueprints;

public sealed class SetCreatureEventsHandlerTests
{
    private static readonly BlueprintId CreatureId =
        BlueprintId.From(Guid.Parse("a1510000-0000-4000-8000-000000000001"));
    private readonly SetCreatureEventsHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoAndRedoPreserveCanonicalBindings()
    {
        var history = new ProjectCommandHistory(CreateState());
        var profile = CreatureEventTestData.Complete();
        var command = new SetCreatureEventsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        Assert.True(preview.Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureEvents);
        Assert.Contains("6", preview.Patch!.Summary, StringComparison.Ordinal);

        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureEvents!.Id);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureEvents);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureEvents!.Id);
    }

    [Fact]
    public void RevisionKindAndInvalidBindingsAreRejectedWithoutMutation()
    {
        var state = CreateState();
        var invalid = CreatureEventTestData.Complete() with { };
        var conflict = _handler.Handle(state, new SetCreatureEventsCommand(CommandId.From(Guid.NewGuid()),
            state.Revision.Next(), CreatureId, invalid, false));
        var invalidProfile = new Forge.Domain.Creatures.CreatureEventProfile(invalid.Id, invalid.SchemaVersion,
            invalid.Provenance, invalid.RequiredAtRuntime, invalid.CombatBoundary, invalid.Inventory, invalid.Rest,
            invalid.Heartbeat! with { IntervalMilliseconds = 100 }, invalid.CustomEvents, invalid.Overrides);
        var rejected = _handler.Handle(state, new SetCreatureEventsCommand(CommandId.From(Guid.NewGuid()),
            state.Revision, CreatureId, invalidProfile, false));

        Assert.Equal(SetCreatureEventsHandler.RevisionConflictCode, Assert.Single(conflict.Diagnostics).Code);
        Assert.Contains(rejected.Diagnostics,
            item => item.Code == "creature.events.heartbeat_interval_invalid");
        Assert.Equal(state.Revision, rejected.State.Revision);
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a1510000-0000-4000-8000-000000000002")),
            "event-lab", "Event lab", []);
        var blueprint = new BlueprintDefinition(CreatureId, "ember-warden", "Gardien des braises",
            BlueprintKind.Creature);
        return new ProjectState(new ProjectDefinition(
            ProjectId.From(Guid.Parse("a1510000-0000-4000-8000-000000000003")),
            "Creature-event project", [blueprint], [area]), new ProjectRevision(0));
    }
}
