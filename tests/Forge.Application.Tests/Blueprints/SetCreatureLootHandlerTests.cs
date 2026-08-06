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

public sealed class SetCreatureLootHandlerTests
{
    private static readonly BlueprintId CreatureId = BlueprintId.From(Guid.Parse("a1380000-0000-4000-8000-000000000001"));
    private readonly SetCreatureLootHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoAndRedoPreserveCanonicalLootPolicy()
    {
        var history = new ProjectCommandHistory(CreateState());
        var profile = Profile();
        var command = new SetCreatureLootCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        Assert.True(preview.Succeeded);
        Assert.False(preview.Audit.Applied);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureLoot);

        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureLoot!.Id);
        Assert.Contains("creatureLoot", applied.Patch!.Path, StringComparison.Ordinal);

        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureLoot);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureLoot!.Id);
    }

    [Fact]
    public void MissingItemAndSkillReferencesAreRejectedWithoutRevisionChange()
    {
        var state = CreateState(includeReferences: false);

        var result = _handler.Handle(state, new SetCreatureLootCommand(
            CommandId.From(Guid.NewGuid()), state.Revision, CreatureId, Profile(), false));

        Assert.False(result.Succeeded);
        Assert.Equal(state.Revision, result.State.Revision);
        Assert.Contains(result.Diagnostics, item => item.Code == "creature.loot.item_missing");
        Assert.Contains(result.Diagnostics, item => item.Code == "creature.loot.theft_skill_missing");
    }

    private static ProjectState CreateState(bool includeReferences = true)
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a1380000-0000-4000-8000-000000000002")),
            "loot-lab", "Loot lab", []);
        var combatLoot = includeReferences
            ? new[] { new LootDrop("warden-ember", 1), new LootDrop("ash-coin", 3) }
            : [];
        var combat = new CombatProfile(new CombatStats(30, 0, 5, 4, "hostiles", "hostiles"), loot: combatLoot);
        var progression = includeReferences ? Progression() : null;
        var blueprint = new BlueprintDefinition(CreatureId, "ember-warden", "Gardien des braises",
            BlueprintKind.Creature, combat, creatureProgression: progression);
        return new ProjectState(new ProjectDefinition(
            ProjectId.From(Guid.Parse("a1380000-0000-4000-8000-000000000003")),
            "Loot project", [blueprint], [area]), new ProjectRevision(0));
    }

    private static CreatureProgressionProfile Progression() => new(
        Guid.Parse("a1380000-0000-4000-8000-000000000004"), 1, "author:test", "forge.generic", false, false,
        [new("warden", "Gardien", 1, 20, 0, [])], [],
        [
            new("sleight", "Larcin", "agility", 2, 10, 2, true, false, 12),
            new("awareness", "Vigilance", "insight", 2, 10, 2, true, false, 10),
        ], [], new("standard", AbilityLoadoutMode.Learned, 0, [], []), new Dictionary<string, string>());

    private static CreatureLootProfile Profile() => new(
        Guid.Parse("a1380000-0000-4000-8000-000000000005"), 1, "author:test", true,
        [
            new(Guid.Parse("a1380000-0000-4000-8000-000000000006"), "warden-ember", "main-hand", 1, 1, 1,
                true, false, LootOwnershipMode.Party, LootInstancingMode.PerParty, null),
            new(Guid.Parse("a1380000-0000-4000-8000-000000000007"), "ash-coin", "pouch", 1, 3, 0.5,
                true, true, LootOwnershipMode.Killer, LootInstancingMode.PerPlayer, null),
        ],
        new(true, "sleight", "awareness", 12, 10, true, "crime.theft", 30, "loot.pickpocket"),
        new(CreatureCorpseMode.RagdollContainer, 45, "corpse.standard", "ragdoll.humanoid", true,
            "party", 8, CorpseCleanupMode.Timed), new Dictionary<string, string>());
}
