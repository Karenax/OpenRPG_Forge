using Forge.Application.Quality;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Localization;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;
using Forge.Domain.Assistant;

namespace Forge.Application.Tests.Quality;

public sealed class ProjectQualityServicesTests
{
    [Fact]
    public void Localization_reports_coverage_placeholders_plurals_overflow_and_renders_pseudo_snapshot()
    {
        var catalog = new LocalizationCatalog(LocalizationCatalogId.From(Guid.NewGuid()), "fr", ["fr", "en"],
        [
            new LocalizationMessage("loot.count",
                "{count, plural, one {Un objet} other {# objets}}",
                new Dictionary<string, string> { ["en"] = "Items" }, "test"),
        ]);

        var report = LocalizationQualityService.Analyze(catalog, maximumCharacters: 24);
        var svg = LocalizationQualityService.RenderPseudoSnapshotSvg(report);

        Assert.False(report.Passed);
        Assert.Contains(report.Issues, issue => issue.Code == "QA_LOCALIZATION_PLACEHOLDER_MISMATCH");
        Assert.Contains(report.Issues, issue => issue.Code == "QA_LOCALIZATION_PLURAL_MISSING");
        Assert.Contains(report.Issues, issue => issue.Code == "QA_LOCALIZATION_OVERFLOW_RISK");
        Assert.Contains("PSEUDO-LOCALISATION", svg);
        Assert.Contains("loot.count", svg);
    }

    [Fact]
    public void Narrative_and_encounter_tests_cover_real_project_definitions_deterministically()
    {
        var project = Project();

        var dialogue = NarrativeQualityService.TestDialogue(Assert.Single(project.Dialogues));
        var quest = NarrativeQualityService.TestQuest(Assert.Single(project.Quests));
        var encountersA = EncounterQualityService.Simulate(project);
        var encountersB = EncounterQualityService.Simulate(project);

        Assert.True(dialogue.Passed);
        Assert.Equal(dialogue.TotalNodes, dialogue.CoveredNodes);
        Assert.Equal(dialogue.TotalChoices, dialogue.CoveredChoices);
        Assert.True(quest.Passed);
        Assert.Equal(quest.TotalTransitions, quest.CoveredTransitions);
        Assert.Contains(NarrativeFixtureScope.Player, quest.CoveredScopes);
        Assert.Contains(NarrativeFixtureScope.Party, quest.CoveredScopes);
        Assert.True(encountersA.Passed);
        Assert.Equal(9, encountersA.TotalRuns);
        Assert.Equal(encountersA.Areas[0].Runs.Select(run => run.SelectedEntities),
            encountersB.Areas[0].Runs.Select(run => run.SelectedEntities));
        Assert.All(encountersA.Areas[0].Runs, run => Assert.True(run.NavigationPassed && run.CleanupPassed));
    }

    [Fact]
    public void R120_AssistantScenarioAgentUsesOnlyGameplayActionsAndReportsCoverageDeterministically()
    {
        var project = Project();
        var profile = AssistantTestAutomationProfile.CreateDefault();

        var first = AssistantScenarioTestService.Run(project, profile);
        var second = AssistantScenarioTestService.Run(project, profile);

        Assert.True(first.Succeeded);
        Assert.Equal(project.Dialogues.Count, first.Coverage.Dialogues);
        Assert.Equal(first.Coverage.DialogueNodesTotal, first.Coverage.DialogueNodesCovered);
        Assert.Equal(project.Quests.Count, first.Coverage.Quests);
        Assert.Equal(first.Coverage.QuestTransitionsTotal, first.Coverage.QuestTransitionsCovered);
        Assert.All(first.GameplayActions,
            action => Assert.Contains(action, AssistantTestAutomationProfile.SupportedGameplayActions));
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.DoesNotContain(first.GameplayActions, action => action.Contains("shell", StringComparison.Ordinal));
    }

    [Fact]
    public void R120_AssistantScenarioAgentReportsContradictionsRewardCyclesAndCitedNarrativeConflicts()
    {
        var project = ProjectWithNarrativeFailures();

        var report = AssistantScenarioTestService.Run(project, AssistantTestAutomationProfile.CreateDefault());

        Assert.False(report.Succeeded);
        Assert.Contains(report.Findings, item =>
            item.Code == AssistantScenarioTestService.DialogueConditionContradictionCode &&
            item.SuggestedTransaction is not null && item.ReproductionSteps.Count > 0);
        Assert.Contains(report.Findings, item =>
            item.Code == AssistantScenarioTestService.QuestRewardExploitCode &&
            item.Severity == AssistantScenarioFindingSeverity.Blocking);
        Assert.Contains(report.Findings, item =>
            item.Code == AssistantScenarioTestService.NarrativeConflictCode && item.Citations.Count == 2 &&
            item.SuggestedTransaction!.StartsWith("localization.resolve_conflict", StringComparison.Ordinal));
    }

    [Fact]
    public void Performance_lab_plans_visual_fixtures_and_evaluates_all_budgets_deterministically()
    {
        var project = Project();
        var profile = project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance;

        var first = PerformanceQualityService.Benchmark(project, profile, 1_024_000, 3);
        var second = PerformanceQualityService.Benchmark(project, profile, 1_024_000, 3);
        var visual = PerformanceQualityService.PlanVisualReviews(profile.Visual);

        Assert.True(first.Passed);
        Assert.Equal(9, first.Measurements.Count);
        Assert.Equal(Enum.GetValues<Forge.Domain.RuntimeExtensions.AreaBenchmarkKind>(),
            first.Measurements.Select(item => item.Kind).Distinct().Order());
        Assert.All(first.Measurements, item =>
        {
            Assert.Equal(1_024_000, item.PackageBytes);
            Assert.True(item.NavigationRebuildMilliseconds >= 0);
        });
        Assert.Equal(9, first.Budgets.Count);
        Assert.DoesNotContain(first.Budgets, item => item.Status == PerformanceBudgetStatus.Failed);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(8, visual.Cases.Count);
        Assert.All(visual.Cases, item => Assert.EndsWith(".review.json", item.ReviewPath));
    }

    private static ProjectDefinition Project()
    {
        var spawnBlueprint = BlueprintId.From(Guid.Parse("e1000000-0000-4000-8000-000000000001"));
        var creatureBlueprint = BlueprintId.From(Guid.Parse("e1000000-0000-4000-8000-000000000002"));
        var area = new AreaDefinition(AreaId.From(Guid.Parse("e1000000-0000-4000-8000-000000000003")),
            "quality-arena", "Quality arena",
            [
                new EntityInstance(EntityId.From(Guid.Parse("e1000000-0000-4000-8000-000000000004")),
                    spawnBlueprint, Transform(0, 0)),
                new EntityInstance(EntityId.From(Guid.Parse("e1000000-0000-4000-8000-000000000005")),
                    creatureBlueprint, Transform(2, 2)),
            ]);
        var entry = DialogueNodeId.From(Guid.Parse("e1000000-0000-4000-8000-000000000006"));
        var leaf = DialogueNodeId.From(Guid.Parse("e1000000-0000-4000-8000-000000000007"));
        var dialogue = new DialogueDefinition(DialogueId.From(Guid.Parse("e1000000-0000-4000-8000-000000000008")),
            "quality-dialogue", "Quality dialogue", entry,
            [
                new DialogueNode(entry, null, new LocalizedText("dialogue.quality.entry", "Ready?"), choices:
                [
                    new DialogueChoice(DialogueChoiceId.From(Guid.Parse("e1000000-0000-4000-8000-000000000009")),
                        new LocalizedText("dialogue.quality.yes", "Yes"),
                        [new DialogueCondition(DialogueConditionKind.VariableEquals, "ready", "true")], targetNodeId: leaf),
                    new DialogueChoice(DialogueChoiceId.From(Guid.Parse("e1000000-0000-4000-8000-000000000010")),
                        new LocalizedText("dialogue.quality.no", "No")),
                ]),
                new DialogueNode(leaf, null, new LocalizedText("dialogue.quality.leaf", "Done")),
            ]);
        var initial = QuestStateId.From(Guid.Parse("e1000000-0000-4000-8000-000000000011"));
        var completed = QuestStateId.From(Guid.Parse("e1000000-0000-4000-8000-000000000012"));
        var quest = new QuestDefinition(QuestId.From(Guid.Parse("e1000000-0000-4000-8000-000000000013")),
            "quality-quest", "Quality quest", initial,
            [
                new QuestStateDefinition(initial, "Active", new QuestJournalEntry("quest.quality.active", "Active")),
                new QuestStateDefinition(completed, "Complete", new QuestJournalEntry("quest.quality.complete", "Complete"), true),
            ],
            [new QuestTransition(QuestTransitionId.From(Guid.Parse("e1000000-0000-4000-8000-000000000014")), initial, completed)]);
        return new ProjectDefinition(ProjectId.From(Guid.Parse("e1000000-0000-4000-8000-000000000015")),
            "Quality", [
                new BlueprintDefinition(spawnBlueprint, "spawn", "Spawn", BlueprintKind.PlayerSpawn),
                new BlueprintDefinition(creatureBlueprint, "creature", "Creature", BlueprintKind.Creature),
            ], [area], [dialogue], [quest]);
    }

    private static ProjectDefinition ProjectWithNarrativeFailures()
    {
        var baseline = Project();
        var dialogue = Assert.Single(baseline.Dialogues);
        var entry = dialogue.Nodes[0];
        var contradictory = new DialogueChoice(
            DialogueChoiceId.From(Guid.Parse("e1530000-0000-4000-8000-000000000001")),
            new LocalizedText("narrative.shared", "First truth"),
            [
                new DialogueCondition(DialogueConditionKind.VariableEquals, "ready", "true"),
                new DialogueCondition(DialogueConditionKind.VariableEquals, "ready", "false"),
            ]);
        var brokenDialogue = new DialogueDefinition(dialogue.Id, dialogue.Slug, dialogue.DisplayName,
            dialogue.EntryNodeId,
            [new DialogueNode(entry.Id, entry.SpeakerBlueprintId, entry.Text, entry.OnEnterActions, [contradictory])],
            metadata: dialogue.Metadata);

        var stateA = QuestStateId.From(Guid.Parse("e1530000-0000-4000-8000-000000000010"));
        var stateB = QuestStateId.From(Guid.Parse("e1530000-0000-4000-8000-000000000011"));
        var rewardQuest = new QuestDefinition(
            QuestId.From(Guid.Parse("e1530000-0000-4000-8000-000000000012")),
            QuestDefinition.CurrentSchemaVersion, "test:r120", "reward-cycle", "Reward cycle",
            new LocalizedText("narrative.shared", "Second truth"),
            new LocalizedText("quest.reward.description", "Cycle"), "test", 10, "test", false,
            new QuestInstancePolicy(QuestProgressScope.Player, [QuestProgressScope.Player], false), stateA,
            [
                new QuestStateDefinition(stateA, "A", new QuestJournalEntry("quest.reward.a", "A")),
                new QuestStateDefinition(stateB, "B", new QuestJournalEntry("quest.reward.b", "B")),
            ],
            [
                new QuestTransition(QuestTransitionId.From(Guid.Parse("e1530000-0000-4000-8000-000000000013")),
                    stateA, stateB, actions: [new QuestAction(QuestActionKind.GiveItem, "gem", "1")]),
                new QuestTransition(QuestTransitionId.From(Guid.Parse("e1530000-0000-4000-8000-000000000014")),
                    stateB, stateA),
            ]);
        return new ProjectDefinition(baseline.Id, baseline.Title, baseline.Module, baseline.Blueprints,
            baseline.Areas, [brokenDialogue], [rewardQuest]);
    }

    private static TransformValue Transform(float x, float z) => new(new Vector3Value(x, 0, z),
        QuaternionValue.Identity, Vector3Value.One);
}
