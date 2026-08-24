using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Domain.Tests.Quests;

public sealed class QuestQualityProfileTests
{
    [Fact]
    public void Generated_scenarios_cover_five_required_paths_with_stable_graph_identity()
    {
        var quest = Quest();
        var first = QuestQualityRuntimeService.GenerateTests(quest);
        var repeated = QuestQualityRuntimeService.GenerateTests(quest);

        Assert.Equal(Enum.GetValues<QuestQualityTestKind>(), first.Select(item => item.Kind));
        Assert.Equal(first.Select(item => item.Id), repeated.Select(item => item.Id));
        Assert.All(first, scenario =>
        {
            Assert.NotEmpty(scenario.Fixtures);
            Assert.NotEmpty(scenario.Steps);
            Assert.Equal(Math.Max(0, scenario.Steps.Count - 1), scenario.Edges.Count);
            Assert.NotEmpty(scenario.Assertions);
        });
        Assert.Empty(QuestQualityProfileValidator.Validate(quest.WithQuality(Profile(quest))));
    }

    [Fact]
    public void Scenario_runner_reports_nominal_success_and_localized_failure_path()
    {
        var scenario = QuestQualityRuntimeService.GenerateTests(Quest())
            .Single(item => item.Kind == QuestQualityTestKind.Nominal);
        var passed = QuestQualityRuntimeService.Run(scenario, (step, state) =>
            step.Action == QuestQualityTestActionKind.CompleteObjectives
                ? new Dictionary<string, string> { ["quest/state"] = "completed", ["quest/rewards"] = "true" }
                : state);
        var failed = QuestQualityRuntimeService.Run(scenario, (_, state) => state);

        Assert.True(passed.Passed);
        Assert.False(failed.Passed);
        Assert.Contains(failed.Failures, item => item.Contains("quest/state", StringComparison.Ordinal));
    }

    [Fact]
    public void Ai_plan_requires_named_human_approval_before_one_transaction()
    {
        var plan = Profile(Quest()).AiPlan;
        Assert.Throws<InvalidOperationException>(() => QuestQualityRuntimeService.MarkApplied(plan));
        var approved = QuestQualityRuntimeService.Approve(plan, "author:karen",
            DateTimeOffset.Parse("2026-08-11T12:00:00Z"));
        var applied = QuestQualityRuntimeService.MarkApplied(approved);

        Assert.Equal(QuestAiAuthorizationLevel.ApplyApprovedPlan, applied.Authorization);
        Assert.Equal("author:karen", applied.ApprovedBy);
        Assert.True(applied.Applied);
        Assert.Contains("apply:", applied.SemanticDiff, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => QuestQualityRuntimeService.MarkApplied(applied));
    }

    [Fact]
    public void Invalid_graph_ai_contract_and_required_disable_have_stable_document_paths()
    {
        var quest = Quest();
        Assert.Empty(QuestQualityProfileValidator.Validate(quest));
        var profile = Profile(quest);
        var invalidScenario = profile.TestScenarios[0] with
        {
            Edges = [],
            Assertions = [profile.TestScenarios[0].Assertions[0] with { Path = string.Empty }],
        };
        var invalid = profile with
        {
            TestScenarios = [invalidScenario, .. profile.TestScenarios.Skip(1)],
            AiPlan = profile.AiPlan with { TransactionId = Guid.Empty },
        };
        var diagnostics = QuestQualityProfileValidator.Validate(quest.WithQuality(invalid));

        Assert.Contains(diagnostics, item => item.Code == QuestQualityProfileValidator.GraphInvalidCode &&
                                             item.Path.Contains("/graph", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == QuestQualityProfileValidator.AssertionInvalidCode &&
                                             item.Path.Contains("/assertions", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == QuestQualityProfileValidator.AiPlanInvalidCode &&
                                             item.Path.EndsWith("/aiPlan", StringComparison.Ordinal));
        Assert.Contains(QuestQualityProfileValidator.Validate(quest.WithQuality(profile with
        {
            Enabled = false,
            RequiredAtRuntime = true,
        })), item => item.Code == QuestQualityProfileValidator.RequiredMissingCode);
    }

    internal static QuestDefinition Quest()
    {
        var quest = QuestAuthoringProfileTests.Quest();
        return quest.WithAuthoring(QuestAuthoringProfileTests.Profile(quest, [QuestObjectiveKind.Destination]));
    }

    internal static QuestQualityProfile Profile(QuestDefinition quest)
    {
        var dependencies = new[]
        {
            Dependency(0, QuestAiDependencyKind.State, quest.InitialStateId.ToString()),
            Dependency(1, QuestAiDependencyKind.Transition, quest.Transitions[0].Id.ToString()),
            Dependency(2, QuestAiDependencyKind.Participant, quest.Authoring!.Participants[0].Id.ToString()),
            Dependency(3, QuestAiDependencyKind.Zone, quest.Authoring.Wizard.ZoneIds[0]),
            Dependency(4, QuestAiDependencyKind.Asset, "asset:quest-generation-model"),
        };
        var plan = QuestQualityRuntimeService.CreateAiPlan(quest,
            new LocalizedText("quest.r073.ai.synopsis", "Rétablir le signal des Cendres."),
            dependencies, ["08_QUEST.md#orf-qst-025"], ["style:crypt"], "agent:forge",
            Guid.Parse("c7130000-0000-4000-8000-000000000001"),
            $"preview:quest:{quest.Id}|states:{quest.States.Count}|participants:{quest.Authoring.Participants.Count}");
        return new QuestQualityProfile(Guid.Parse("c7130000-0000-4000-8000-000000000002"), 1,
            "test:r073", true, true, QuestQualityRuntimeService.GenerateTests(quest), plan);

        static QuestAiDependency Dependency(int index, QuestAiDependencyKind kind, string reference) => new(
            Guid.Parse($"c7130000-0000-4000-8000-{(100 + index):D12}"), 1, "test:r073", index + 1,
            kind, reference);
    }
}
