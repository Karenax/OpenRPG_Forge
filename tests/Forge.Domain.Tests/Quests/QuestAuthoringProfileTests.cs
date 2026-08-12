using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Domain.Tests.Quests;

public sealed class QuestAuthoringProfileTests
{
    [Fact]
    public void Profile_models_all_objective_kinds_rewards_notes_cast_and_transparent_wizard()
    {
        var quest = Quest();
        var profile = Profile(quest, Enum.GetValues<QuestObjectiveKind>());
        quest = quest.WithAuthoring(profile);

        Assert.Empty(QuestAuthoringProfileValidator.Validate(quest));
        Assert.Equal(8, profile.Objectives.Count);
        Assert.All(Enum.GetValues<QuestObjectiveKind>(), kind =>
            Assert.Contains(profile.Objectives, item => item.Kind == kind));
        Assert.False(profile.Wizard.GeneratesOpaqueCode);
        Assert.Equal(5, profile.Wizard.GeneratedTestScenarios.Count);
    }

    [Fact]
    public void Rewards_commit_progression_items_currency_reputation_unlocks_and_choices_atomically()
    {
        var quest = Quest();
        quest = quest.WithAuthoring(Profile(quest, [QuestObjectiveKind.Collection]));
        var original = new QuestAuthoringRewardState(2, new Dictionary<string, int>(),
            new Dictionary<string, decimal>(), new Dictionary<string, double>(),
            new HashSet<string>(), new HashSet<string>());

        var applied = QuestAuthoringRuntimeService.GrantRewards(quest, original, "player:smoke");
        Assert.True(applied.Committed);
        Assert.Equal(12, applied.State.Progression);
        Assert.Equal(1, applied.State.Items["ash-key"]);
        Assert.Equal(5, applied.State.Currencies["ember"]);
        Assert.Equal(2, applied.State.Reputation["ash-watch"]);
        Assert.Contains("observatory", applied.State.Unlocks);
        Assert.Contains("choice:dawn", applied.State.Choices);
        Assert.Contains("choices:1", applied.SemanticDiff);

        var rejected = QuestAuthoringRuntimeService.GrantRewards(quest, original, string.Empty);
        Assert.False(rejected.Committed);
        Assert.Same(original, rejected.State);
        Assert.Equal("no-change", rejected.SemanticDiff);
    }

    [Fact]
    public void Validation_reports_localized_paths_for_objective_reward_notes_cast_and_wizard_failures()
    {
        var quest = Quest();
        var profile = Profile(quest, [QuestObjectiveKind.Collection]);
        profile = profile with
        {
            Objectives = [profile.Objectives[0] with { TargetIds = [] }],
            Rewards = profile.Rewards with { Items = new Dictionary<string, int> { ["ash-key"] = 0 } },
            Notes = profile.Notes with { OwnerId = string.Empty, Sources = [], ReviewHistory = [] },
            Participants = [profile.Participants[0] with { BindingValue = string.Empty }],
            Wizard = profile.Wizard with { GeneratesOpaqueCode = true, GeneratedTestScenarios = [] },
        };

        var diagnostics = QuestAuthoringProfileValidator.Validate(quest.WithAuthoring(profile));
        Assert.Contains(diagnostics, item => item.Code == QuestAuthoringProfileValidator.ObjectiveInvalidCode &&
                                             item.Path.Contains("/objectives/", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == QuestAuthoringProfileValidator.RewardInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == QuestAuthoringProfileValidator.NotesInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == QuestAuthoringProfileValidator.ParticipantInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == QuestAuthoringProfileValidator.WizardInvalidCode);
    }

    [Fact]
    public void Optional_absence_is_safe_and_required_disabled_profile_is_diagnostic()
    {
        var quest = Quest();
        Assert.Empty(QuestAuthoringProfileValidator.Validate(quest));
        var profile = Profile(quest, [QuestObjectiveKind.Boolean]) with
        {
            Enabled = false,
            RequiredAtRuntime = true,
        };
        Assert.Contains(QuestAuthoringProfileValidator.Validate(quest.WithAuthoring(profile)),
            item => item.Code == QuestAuthoringProfileValidator.RequiredMissingCode);
    }

    internal static QuestDefinition Quest()
    {
        var questId = QuestId.From(Guid.Parse("c7110000-0000-4000-8000-000000000001"));
        var initial = QuestStateId.From(Guid.Parse("c7110000-0000-4000-8000-000000000002"));
        var completed = QuestStateId.From(Guid.Parse("c7110000-0000-4000-8000-000000000003"));
        var transition = QuestTransitionId.From(Guid.Parse("c7110000-0000-4000-8000-000000000004"));
        return new QuestDefinition(questId, "r071-probe", "R071 probe", initial,
            [
                new(initial, "Initial", new QuestJournalEntry("quest.r071.initial", "Initial")),
                new(completed, "Completed", new QuestJournalEntry("quest.r071.completed", "Completed"), true),
            ], [new QuestTransition(transition, initial, completed)]);
    }

    internal static QuestAuthoringProfile Profile(QuestDefinition quest, IReadOnlyList<QuestObjectiveKind> kinds)
    {
        var objectives = kinds.Select((kind, index) => new QuestAuthoringObjective(
            Guid.Parse($"c7110000-0000-4000-8000-{(100 + index):D12}"), 1, "test:r071", index + 1, kind,
            new LocalizedText($"quest.r071.objective.{index}", kind.ToString()),
            kind is QuestObjectiveKind.Counter or QuestObjectiveKind.Boolean or QuestObjectiveKind.Collection ? 1 : 0,
            kind is QuestObjectiveKind.Collection or QuestObjectiveKind.Escort ? ["ash-key"] : [],
            kind is QuestObjectiveKind.Destination or QuestObjectiveKind.Escort ? "crypt-of-ashes" : null,
            kind == QuestObjectiveKind.Survival ? 60 : 0,
            kind == QuestObjectiveKind.Choice ? ["choice:dawn", "choice:dusk"] : [],
            kind == QuestObjectiveKind.Custom ? "rule:ember" : null, false)).ToArray();
        var participant = new QuestAuthoringParticipant(
            Guid.Parse("c7110000-0000-4000-8000-000000000201"), 1, "test:r071", 1, "guide",
            QuestParticipantBindingKind.Blueprint, "elyra-npc", ["quest:active"], true);
        return new QuestAuthoringProfile(
            Guid.Parse("c7110000-0000-4000-8000-000000000202"), 1, "test:r071", true, true,
            objectives,
            new QuestAuthoringRewardBundle(Guid.Parse("c7110000-0000-4000-8000-000000000203"), 1,
                "test:r071", 10, new Dictionary<string, int> { ["ash-key"] = 1 },
                new Dictionary<string, decimal> { ["ember"] = 5 },
                new Dictionary<string, double> { ["ash-watch"] = 2 }, ["observatory"], ["choice:dawn"]),
            new QuestAuthorNotes("Restore the vigil", "The ember covenant is canonical", "author:karen",
                QuestEditorialStatus.Review, ["branch complexity"], ["record voices"], ["08_QUEST.md"],
                [new QuestAuthorReview(Guid.Parse("c7110000-0000-4000-8000-000000000204"), 1,
                    "test:r071", "reviewer:lyra", QuestEditorialStatus.Review, "Ready for validation",
                    DateTimeOffset.Parse("2026-08-11T00:00:00Z"))]),
            [participant],
            new QuestPlotWizardPlan(Guid.Parse("c7110000-0000-4000-8000-000000000205"), 1, "test:r071",
                ["crypt-of-ashes"], [participant.Id], ["crypt-pillar"],
                [DialogueId.From(Guid.Parse("40000000-0000-4000-8000-000000000001"))],
                objectives.Select(item => item.Id).ToArray(), quest.Transitions.Select(item => item.Id).ToArray(),
                ["nominal-path", "objective-failure", "repeated-transition", "multiplayer-authority", "save-resume"],
                false));
    }
}
