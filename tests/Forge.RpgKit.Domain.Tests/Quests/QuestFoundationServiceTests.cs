using Forge.RpgKit.Domain.Quests;

namespace Forge.RpgKit.Domain.Tests.Quests;

public sealed class QuestFoundationServiceTests
{
    [Fact]
    public void Wizard_previews_canonical_quest_zones_cast_props_dialogue_objectives_transitions_and_tests()
    {
        var request = Request(); var context = Context(request); var preview = QuestFoundationService.Preview(request, context); Assert.True(preview.CanApply); Assert.Equal(5, preview.GeneratedTestScenarios.Count); Assert.Contains("save-resume", preview.GeneratedTestScenarios); Assert.Contains("generated-tests:5", preview.SemanticDiff);
        var applied = QuestFoundationService.Apply(request, context); Assert.Equal(preview.Quest.Id, applied.Id); Assert.Equal("guide", applied.Participants.Single().Role);
    }

    [Fact]
    public void Objectives_cover_counter_boolean_collection_destination_escort_survival_choice_and_custom()
    {
        Assert.Equal(8, Enum.GetValues<QuestObjectiveKind>().Length); var request = Request() with { Objectives = Enum.GetValues<QuestObjectiveKind>().Select((kind, index) => new QuestObjective(Guid.NewGuid(), kind, kind.ToString(), index + 1, new HashSet<string> { "target:shard" }, "zone:observatory", TimeSpan.FromMinutes(2), new HashSet<string> { "choice:dawn" }, "rule:vigil", false)).ToArray() };
        Assert.Empty(QuestFoundationService.Validate(QuestFoundationService.Preview(request, Context(request)).Quest, Context(request) with { MaximumObjectives = 10 }));
    }

    [Fact]
    public void Reward_bundle_grants_progression_items_currency_reputation_unlocks_and_choices_atomically()
    {
        var request = Request(); var original = new QuestRewardState(1, new Dictionary<Guid, int>(), new Dictionary<string, decimal>(), new Dictionary<string, double>(), new HashSet<string>()); var result = QuestFoundationService.GrantRewards(original, request.Rewards, "player");
        Assert.True(result.Committed); Assert.Equal(11, result.State.Progression); Assert.Equal(1, result.State.Items.Values.Single()); Assert.Equal(5, result.State.Currencies["shard"]); Assert.Equal(2, result.State.Reputation["faction:vigil"]); Assert.Contains("unlock:observatory", result.State.Unlocks); Assert.Contains("choices:1", result.SemanticDiff);
        var rejected = QuestFoundationService.GrantRewards(original, request.Rewards with { Progression = -1 }, "player"); Assert.False(rejected.Committed); Assert.Same(original, rejected.State); Assert.Equal("no-change", rejected.SemanticDiff);
    }

    [Fact]
    public void Validation_localizes_cast_objective_reward_reference_and_author_notes_failures()
    {
        var request = Request(); var invalid = request with { Participants = [new("guide", QuestParticipantBindingKind.Query, "missing", new HashSet<string>(), true)], Objectives = [], Rewards = request.Rewards with { Currencies = new Dictionary<string, decimal> { ["missing"] = 1 } }, Notes = request.Notes with { OwnerId = "", Sources = [], ReviewHistory = [] } };
        var preview = QuestFoundationService.Preview(invalid, Context(request)); Assert.False(preview.CanApply); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_QUEST_PARTICIPANT_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_QUEST_OBJECTIVE_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_QUEST_REWARD_REFERENCE_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_QUEST_NOTES_INVALID");
    }

    private static QuestWizardRequest Request()
    {
        var item = Guid.Parse("11111111-1111-1111-1111-111111111111"); var dialogue = Guid.Parse("22222222-2222-2222-2222-222222222222");
        return new("sample-quest", "Quête de démonstration", ["zone:observatory"], [new("guide", QuestParticipantBindingKind.Blueprint, "creature:lyra", new HashSet<string> { "night" }, true)], ["placeable:prism"], [dialogue], [new(Guid.NewGuid(), QuestObjectiveKind.Collection, "Réunir les éclats", 3, new HashSet<string> { "target:shard" }, null, TimeSpan.Zero, new HashSet<string>(), null, false)], ["transition:offer", "transition:complete"], new(10, new Dictionary<Guid, int> { [item] = 1 }, new Dictionary<string, decimal> { ["shard"] = 5 }, new Dictionary<string, double> { ["faction:vigil"] = 2 }, new HashSet<string> { "unlock:observatory" }, ["choice:dawn"]), new("Restore the sample quest", "The glass moon is fractured", "author:karen", QuestEditorialStatus.Review, ["branch complexity"], ["voice"], ["PROJECT_CONTEXT.md"], ["review:initial"]), "author:test");
    }
    private static QuestValidationContext Context(QuestWizardRequest request) => new(new HashSet<string> { "zone:observatory" }, new HashSet<string> { "creature:lyra" }, new HashSet<string> { "placeable:prism" }, request.DialogueIds.ToHashSet(), new HashSet<string> { "transition:offer", "transition:complete" }, request.Rewards.Items.Keys.ToHashSet(), new HashSet<string> { "shard" }, new HashSet<string> { "faction:vigil" }, new HashSet<string> { "unlock:observatory" }, new HashSet<string> { "target:shard" }, 20);
}
