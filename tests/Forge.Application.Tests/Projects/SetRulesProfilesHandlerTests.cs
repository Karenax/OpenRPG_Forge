using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Rules;

namespace Forge.Application.Tests.Projects;

public sealed class SetRulesProfilesHandlerTests
{
    [Fact]
    public void Rules_command_previews_applies_and_round_trips_history_atomically()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas[0];
        var current = context.State.Project.Module.RuntimePolicies.Rules;
        var difficulty = current.ActiveDifficulty;
        var changedDifficulty = new DifficultyProfile(difficulty.Id, 1, difficulty.Key, difficulty.DisplayName,
            1.25, 1.4, 1.1, .9, .8, SaveRestrictionPolicy.CheckpointsOnly, FriendlyFireMode.PartyOnly,
            difficulty.Accessibility, "test");
        var module = new ModuleRulesProfile(current.Id, 1, current.Progression, [changedDifficulty],
            changedDifficulty.Key, current.Mutators, "test");
        var combat = area.RuntimePolicies.Rules.Combat;
        var areaRules = new AreaRulesProfile(area.RuntimePolicies.Rules.Id, 1,
            area.RuntimePolicies.Rules.Perception,
            new AreaCombatPolicy(combat.Id, 1, AreaPvpMode.DuelsOnly, FriendlyFireMode.Disabled,
                true, true, true, true, "test"), "test");
        var handler = new SetRulesProfilesHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new SetRulesProfilesCommand(CommandId.From(Guid.NewGuid()), context.State.Revision, module,
            area.Id, areaRules, true);
        var preview = handler.Handle(context.State, command);
        Assert.True(preview.Succeeded);
        Assert.Equal("rules", preview.Patch!.Path);
        var applied = handler.Handle(context.State, command with { DryRun = false });
        Assert.True(applied.Succeeded);
        Assert.Equal(1.4, applied.State.Project.Module.RuntimePolicies.Rules.ActiveDifficulty.IncomingDamageMultiplier);
        Assert.Equal(AreaPvpMode.DuelsOnly,
            applied.State.Project.Areas[0].RuntimePolicies.Rules.Combat.PvpMode);

        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(AreaPvpMode.Disabled,
            history.State.Project.Areas[0].RuntimePolicies.Rules.Combat.PvpMode);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(AreaPvpMode.DuelsOnly,
            history.State.Project.Areas[0].RuntimePolicies.Rules.Combat.PvpMode);
    }
}
