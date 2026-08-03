using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Companions;

namespace Forge.RpgKit.Domain.Tests.Companions;

public sealed class CompanionServiceTests
{
    [Fact]
    public void Dialogue_context_distinguishes_offer_owner_other_owner_and_unavailable()
    {
        var companion = Fixture();
        Assert.Equal(RecruitDialogueContext.NotRecruited, CompanionService.DialogueContext(companion, "player", true));
        Assert.Equal(RecruitDialogueContext.RecruitedBySpeaker, CompanionService.DialogueContext(companion with { OwnerId = "player" }, "player", true));
        Assert.Equal(RecruitDialogueContext.RecruitedByOther, CompanionService.DialogueContext(companion with { OwnerId = "other" }, "player", true));
        Assert.Equal(RecruitDialogueContext.Unavailable, CompanionService.DialogueContext(companion, "player", false));
    }

    [Fact]
    public void Recruit_and_dismiss_are_atomic_for_capacity_ownership_funds_inventory_position_and_persistence()
    {
        var original = State(); var recruited = CompanionService.Recruit(original, "player");
        Assert.True(recruited.Committed); Assert.Equal("player", recruited.State.Companion.OwnerId); Assert.Contains(original.Companion.Id, recruited.State.Party.CompanionIds); Assert.Equal(8, recruited.State.Party.Funds["shard"]); Assert.Equal("player", recruited.State.InventoryOwners[original.Companion.InventoryId]);
        var position = new TransformValue(new(8, 0, 2), QuaternionValue.Identity, Vector3Value.One); var dismissed = CompanionService.Dismiss(recruited.State, "player", position);
        Assert.True(dismissed.Committed); Assert.Empty(dismissed.State.Companion.OwnerId); Assert.DoesNotContain(original.Companion.Id, dismissed.State.Party.CompanionIds); Assert.Equal(position, dismissed.State.DismissPosition); Assert.Equal(CompanionPersistence.Campaign, dismissed.State.Companion.Persistence);
    }

    [Fact]
    public void Failed_recruit_or_dismiss_rolls_back_exact_state()
    {
        var original = State() with { Party = State().Party with { Capacity = 0, Funds = new Dictionary<string, decimal> { ["shard"] = 0 } } };
        var recruit = CompanionService.Recruit(original, "player"); Assert.False(recruit.Committed); Assert.Same(original, recruit.State); Assert.Equal("no-change", recruit.SemanticDiff);
        var dismissOriginal = State(); var dismiss = CompanionService.Dismiss(dismissOriginal, "intruder", TransformValue.Identity); Assert.False(dismiss.Committed); Assert.Same(dismissOriginal, dismiss.State); Assert.Contains(dismiss.Diagnostics, item => item.Code == "RPG_COMPANION_DISMISS_FORBIDDEN");
    }

    [Fact]
    public void Validation_covers_recruit_paths_ownership_commands_equipment_death_dialogue_and_references()
    {
        var companion = Fixture(); Assert.Empty(CompanionService.Validate(companion, Context(companion)));
        var invalid = companion with { CommandIds = new HashSet<string> { "missing" }, InventoryId = "missing", DeathPolicyId = "missing", DialogueContextIds = new HashSet<string>(), RecruitPolicy = companion.RecruitPolicy with { CurrencyId = "missing", AllowDialogue = false, AllowScript = false } };
        var diagnostics = CompanionService.Validate(invalid, Context(companion));
        Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_COMMAND_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_EQUIPMENT_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_DEATH_POLICY_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_DIALOGUE_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_RECRUIT_POLICY_INVALID");
    }

    private static CompanionComponent Fixture() => new(Guid.Parse("11111111-2222-3333-4444-555555555555"), 1, "author:test", Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), new(new HashSet<string> { "quest:vigil" }, 2, "shard", 0.25, ["relation:lyra+1"], true, true), "", CompanionPersistence.Campaign, "inventory:lyra", TransformValue.Identity, new HashSet<string> { "follow", "hold" }, new HashSet<string> { "not-recruited", "recruited-by-speaker" }, "equipment:companion", "death:recoverable");
    private static CompanionWorldState State() { var companion = Fixture(); return new(companion, new("party", 2, [], new Dictionary<string, decimal> { ["shard"] = 10 }, new HashSet<string> { "quest:vigil" }, 0.5), new Dictionary<string, string> { [companion.InventoryId] = companion.Id.ToString() }, TransformValue.Identity); }
    private static CompanionValidationContext Context(CompanionComponent companion) => new(new HashSet<Guid> { companion.CreatureDefinitionId }, new HashSet<string> { "follow", "hold" }, new HashSet<string> { "inventory:lyra" }, new HashSet<string> { "equipment:companion" }, new HashSet<string> { "death:recoverable" }, new HashSet<string> { "not-recruited", "recruited-by-speaker" }, new HashSet<string> { "shard" }, 4);
}
