using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Application.Tests.Creatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Blueprints;

public sealed class SetCreatureRoleBehaviorHandlerTests
{
    private static readonly BlueprintId CreatureId = BlueprintId.From(Guid.Parse("a1410000-0000-4000-8000-000000000001"));
    private readonly SetCreatureRoleBehaviorHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoAndRedoPreserveBehaviorAndApplyDeterministicTemplate()
    {
        var history = new ProjectCommandHistory(CreateState());
        var profile = CreatureRoleBehaviorTestData.Complete();
        var command = new SetCreatureRoleBehaviorCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        Assert.True(preview.Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureRoleBehavior);
        Assert.Contains("change", preview.Patch!.Summary, StringComparison.OrdinalIgnoreCase);

        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        var updated = history.State.Project.Blueprints.Single();
        Assert.Equal(profile.Id, updated.CreatureRoleBehavior!.Id);
        Assert.Equal(6, updated.CreatureProgression!.Tracks.Sum(item => item.Level));

        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureRoleBehavior);
        Assert.Equal(2, history.State.Project.Blueprints.Single().CreatureProgression!.Tracks.Sum(item => item.Level));
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureRoleBehavior!.Id);
    }

    [Fact]
    public void MissingProgressionReferencesAreRejectedWithoutRevisionChange()
    {
        var state = CreateState(includeProgression: false);

        var result = _handler.Handle(state, new SetCreatureRoleBehaviorCommand(CommandId.From(Guid.NewGuid()),
            state.Revision, CreatureId, CreatureRoleBehaviorTestData.Complete(), false));

        Assert.False(result.Succeeded);
        Assert.Equal(state.Revision, result.State.Revision);
        Assert.Contains(result.Diagnostics, item => item.Code == "creature.autolevel.progression_missing");
    }

    private static ProjectState CreateState(bool includeProgression = true)
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a1410000-0000-4000-8000-000000000002")),
            "behavior-lab", "Behavior lab", []);
        var blueprint = new BlueprintDefinition(CreatureId, "ember-warden", "Gardien des braises",
            BlueprintKind.Creature,
            creatureProgression: includeProgression
                ? CreatureRoleBehaviorTestData.Progression()
                : null);
        return new ProjectState(new ProjectDefinition(
            ProjectId.From(Guid.Parse("a1410000-0000-4000-8000-000000000003")),
            "Behavior project", [blueprint], [area]), new ProjectRevision(0));
    }
}
