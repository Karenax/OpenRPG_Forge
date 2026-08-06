using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Blueprints;

public sealed class SetCreatureProgressionHandlerTests
{
    private static readonly BlueprintId CreatureId = BlueprintId.From(Guid.Parse("a1330000-0000-4000-8000-000000000001"));
    private readonly SetCreatureProgressionHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoAndRedoPreserveCanonicalProgression()
    {
        var history = new ProjectCommandHistory(CreateState());
        var profile = Profile();
        var command = new SetCreatureProgressionCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureProgression);

        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureProgression!.Id);
        Assert.Contains("creatureProgression", applied.Patch!.Path, StringComparison.Ordinal);

        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureProgression);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureProgression!.Id);
    }

    [Fact]
    public void MissingCombatAbilityIsRejectedWithoutRevisionChange()
    {
        var state = CreateState(includeAbility: false);

        var result = _handler.Handle(state, new SetCreatureProgressionCommand(
            CommandId.From(Guid.NewGuid()), state.Revision, CreatureId,
            Profile(), false));

        Assert.False(result.Succeeded);
        Assert.Equal(state.Revision, result.State.Revision);
        Assert.Contains(result.Diagnostics, item => item.Code == "creature.progression.ability_missing");
    }

    private static ProjectState CreateState(bool includeAbility = true)
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a1330000-0000-4000-8000-000000000002")),
            "progression-lab", "Progression lab", []);
        var abilities = includeAbility
            ? new[] { Ability("ember-bolt"), Ability("warding-step") }
            : [];
        var combat = new CombatProfile(new CombatStats(30, 12, 5, 4, "heroes", "heroes"), abilities);
        var blueprint = new BlueprintDefinition(CreatureId, "ember-warden", "Gardien des braises",
            BlueprintKind.Creature, combat);
        return new ProjectState(new ProjectDefinition(
            ProjectId.From(Guid.Parse("a1330000-0000-4000-8000-000000000003")),
            "Progression project", [blueprint], [area]), new ProjectRevision(0));
    }

    private static AbilityDefinition Ability(string slug) => new(
        AbilityId.From(slug == "ember-bolt"
            ? Guid.Parse("a1330000-0000-4000-8000-000000000010")
            : Guid.Parse("a1330000-0000-4000-8000-000000000011")), slug, slug, 1, 1,
        AbilityTargeting.SingleEnemy, [new AbilityEffect(AbilityEffectKind.Damage, 1)],
        new AbilityVisualCue($"cue.{slug}"));

    private static CreatureProgressionProfile Profile() => new(
        Guid.Parse("a1330000-0000-4000-8000-000000000004"), 1, "author:test", "forge.generic",
        true, true,
        [new("warden", "Gardien", 3, 20, 2_400, ["ember-mastery"])],
        [new("mercy", "Impitoyable", "Miséricordieux", 0.65)],
        [new("lore", "Savoir ancien", "insight", 3, 10, 4, true, true, 12)],
        [new("ember-mastery", "Maîtrise des braises", 1, 3, true,
            [new("track", "warden", 2), new("skill", "lore", 2)], ["ember-bolt"], ["combat.on-hit"])],
        new("prepared", AbilityLoadoutMode.Prepared, 3, ["ember-bolt", "warding-step"],
            [new("ember-bolt", 2, 3, 4.5, "focus", 2, ["empowered"])]),
        new Dictionary<string, string> { ["xp-rate"] = "1.0" });
}
