using Forge.Application.Quests;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Quests;

public sealed class QuestJournalScriptServiceTests
{
    [Fact]
    public void Typed_handle_reads_bounded_canonical_snapshot_and_state()
    {
        var quest = CreateQuest();
        var service = new QuestJournalScriptService(quest);

        var snapshot = service.Read(service.Handle);
        var state = service.ReadState(service.Handle, quest.InitialStateId);

        Assert.Equal("main", snapshot.Category);
        Assert.Equal(2, snapshot.StateCount);
        Assert.Equal(1, snapshot.TerminalStateCount);
        Assert.Equal(2, snapshot.ContextLinkCount);
        Assert.Equal("search", state.Slug);
    }

    [Fact]
    public void Typed_handle_rejects_stale_schema_and_missing_state()
    {
        var quest = CreateQuest();
        var service = new QuestJournalScriptService(quest);

        Assert.Throws<InvalidOperationException>(() => service.Read(new(quest.Id, 99)));
        Assert.Throws<KeyNotFoundException>(() => service.ReadState(service.Handle,
            QuestStateId.From(Guid.Parse("c7500000-0000-4000-8000-000000000099"))));
    }

    [Fact]
    public void Typed_handle_reads_quest_authoring_objective_cast_rewards_reviews_and_wizard()
    {
        var source = CreateQuest();
        var objective = new QuestAuthoringObjective(Guid.Parse("c7710000-0000-4000-8000-000000000010"),
            1, "test:r071", 1, QuestObjectiveKind.Boolean,
            new LocalizedText("quest.r071.objective", "Allumer le brasier"), 1, [], null, 0, [], null, false);
        var participant = new QuestAuthoringParticipant(
            Guid.Parse("c7710000-0000-4000-8000-000000000011"), 1, "test:r071", 1, "guide",
            QuestParticipantBindingKind.Query, "query:tag=guide", ["quest:active"], true);
        var profile = new QuestAuthoringProfile(Guid.Parse("c7710000-0000-4000-8000-000000000012"),
            1, "test:r071", true, false, [objective],
            new QuestAuthoringRewardBundle(Guid.Parse("c7710000-0000-4000-8000-000000000013"), 1,
                "test:r071", 10, new Dictionary<string, int>(),
                new Dictionary<string, decimal> { ["ember"] = 2 }, new Dictionary<string, double>(),
                ["vigil"], ["choice:dawn"]),
            new QuestAuthorNotes("Design", "Canon", "author:test", QuestEditorialStatus.Review, [], [],
                ["08_QUEST.md"], [new QuestAuthorReview(
                    Guid.Parse("c7710000-0000-4000-8000-000000000014"), 1, "test:r071", "reviewer:test",
                    QuestEditorialStatus.Review, "Reviewed", DateTimeOffset.Parse("2026-08-11T00:00:00Z"))]),
            [participant], new QuestPlotWizardPlan(
                Guid.Parse("c7710000-0000-4000-8000-000000000015"), 1, "test:r071", ["crypt"],
                [participant.Id], [], [DialogueId.From(Guid.Parse("40000000-0000-4000-8000-000000000001"))],
                [objective.Id], source.Transitions.Select(item => item.Id).ToArray(),
                ["nominal", "failure", "repeat", "multiplayer", "save-resume"], false));
        var quest = source.WithAuthoring(profile);
        var service = new QuestJournalScriptService(quest);

        var snapshot = service.ReadAuthoring(service.Handle);

        Assert.Equal(1, snapshot.ObjectiveCount);
        Assert.Equal(1, snapshot.ParticipantCount);
        Assert.Equal(3, snapshot.RewardChannelCount);
        Assert.Equal(5, snapshot.GeneratedTestCount);
        Assert.Equal(objective.Id, service.ReadObjective(service.Handle, objective.Id).Id);
        Assert.Equal(participant.Id, service.ReadParticipant(service.Handle, "guide").Id);
    }

    [Fact]
    public void Typed_handle_reads_quest_resources_steps_visibility_timeline_and_map_markers()
    {
        var source = CreateQuest();
        var objective = new QuestAuthoringObjective(Guid.Parse("c7720000-0000-4000-8000-000000000001"),
            1, "test:r072", 1, QuestObjectiveKind.Destination,
            new LocalizedText("quest.r072.objective", "Atteindre la crypte"), 0, [], "crypt", 0, [], null, false);
        var participant = new QuestAuthoringParticipant(Guid.Parse("c7720000-0000-4000-8000-000000000002"),
            1, "test:r072", 1, "guide", QuestParticipantBindingKind.Query, "query:guide", [], true);
        var authoring = new QuestAuthoringProfile(Guid.Parse("c7720000-0000-4000-8000-000000000003"),
            1, "test:r072", true, false, [objective],
            new QuestAuthoringRewardBundle(Guid.Parse("c7720000-0000-4000-8000-000000000004"), 1,
                "test:r072", 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(),
                new Dictionary<string, double>(), [], []),
            new QuestAuthorNotes("Design", "Canon", "author:test", QuestEditorialStatus.Review, [], [], ["source"],
                [new QuestAuthorReview(Guid.Parse("c7720000-0000-4000-8000-000000000005"), 1, "test:r072",
                    "reviewer:test", QuestEditorialStatus.Review, "Reviewed", DateTimeOffset.UnixEpoch)]),
            [participant], new QuestPlotWizardPlan(Guid.Parse("c7720000-0000-4000-8000-000000000006"), 1,
                "test:r072", ["crypt"], [participant.Id], [],
                [DialogueId.From(Guid.Parse("40000000-0000-4000-8000-000000000001"))], [objective.Id],
                source.Transitions.Select(item => item.Id).ToArray(), ["a", "b", "c", "d", "e"], false));
        var history = new QuestExperienceHistoryEvent(Guid.Parse("c7720000-0000-4000-8000-000000000010"),
            1, "test:r072", source.Id, "hidden", "discovered", "dialogue", "player:test",
            DateTimeOffset.UnixEpoch, new Dictionary<string, string> { ["seed"] = "72" },
            new LocalizedText("quest.r072.history", "La crypte est révélée."));
        var marker = new QuestExperienceMarker(Guid.Parse("c7720000-0000-4000-8000-000000000011"), 1,
            "test:r072", 1, objective.Id, QuestExperienceMarkerBindingKind.Waypoint, "waypoint:crypt",
            QuestExperienceVisibility.Discovered, 4, true,
            new LocalizedText("quest.r072.marker", "Entrée de la crypte"), new Vector3Value(1, 0, 2));
        var experience = new QuestExperienceProfile(Guid.Parse("c7720000-0000-4000-8000-000000000012"),
            1, "test:r072", true, false,
            [new QuestExperienceResource(Guid.Parse("c7720000-0000-4000-8000-000000000013"), 1,
                "test:r072", 1, QuestExperienceResourceKind.Asset, "asset:ember", "owner:module", false, [])],
            [new QuestExperienceStep(Guid.Parse("c7720000-0000-4000-8000-000000000014"), 1, "test:r072", 1,
                new LocalizedText("quest.r072.step", "Inspecter le brasier"), [objective.Id],
                source.Transitions.Select(item => item.Id).ToArray(), "investigation", 1, true)],
            new QuestExperienceVisibilityPolicy(Guid.Parse("c7720000-0000-4000-8000-000000000015"), 1,
                "test:r072", QuestExperienceVisibility.Hidden,
                new Dictionary<QuestExperienceVisibility, IReadOnlyList<string>>
                { [QuestExperienceVisibility.Rumor] = ["heard"] }, true), [history], [marker]);
        var quest = source.WithAuthoring(authoring).WithExperience(experience);
        var service = new QuestJournalScriptService(quest);

        var snapshot = service.ReadExperience(service.Handle);
        Assert.Equal(1, snapshot.ResourceCount);
        Assert.Equal(1, snapshot.StepCount);
        Assert.Equal(1, snapshot.HistoryEventCount);
        Assert.Equal(marker.Id, service.ReadMarker(service.Handle, marker.Id).Id);
        Assert.Equal("dialogue", service.ReadHistory(service.Handle, QuestExperienceHistoryView.Debug).Single().Cause);
        Assert.Null(service.ReadHistory(service.Handle, QuestExperienceHistoryView.Player).Single().Cause);
    }

    [Fact]
    public void Typed_handle_reads_generated_test_graph_and_governed_ai_plan()
    {
        var source = CreateQuest();
        var objective = new QuestAuthoringObjective(Guid.Parse("c7730000-0000-4000-8000-000000000001"),
            1, "test:r073", 1, QuestObjectiveKind.Boolean,
            new LocalizedText("quest.r073.objective", "Rétablir le signal"), 1, [], null, 0, [], null, false);
        var participant = new QuestAuthoringParticipant(Guid.Parse("c7730000-0000-4000-8000-000000000002"),
            1, "test:r073", 1, "guide", QuestParticipantBindingKind.Query, "query:guide", [], true);
        var authoring = new QuestAuthoringProfile(Guid.Parse("c7730000-0000-4000-8000-000000000003"),
            1, "test:r073", true, false, [objective],
            new QuestAuthoringRewardBundle(Guid.Parse("c7730000-0000-4000-8000-000000000004"), 1,
                "test:r073", 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(),
                new Dictionary<string, double>(), [], []),
            new QuestAuthorNotes("Design", "Canon", "author:test", QuestEditorialStatus.Review, [], [], ["source"],
                [new QuestAuthorReview(Guid.Parse("c7730000-0000-4000-8000-000000000010"), 1,
                    "test:r073", "reviewer:test", QuestEditorialStatus.Review, "Reviewed",
                    DateTimeOffset.UnixEpoch)]),
            [participant], new QuestPlotWizardPlan(Guid.Parse("c7730000-0000-4000-8000-000000000005"),
                1, "test:r073", ["crypt"], [participant.Id], [],
                [DialogueId.From(Guid.Parse("40000000-0000-4000-8000-000000000001"))], [objective.Id],
                source.Transitions.Select(item => item.Id).ToArray(), ["a", "b", "c", "d", "e"], false));
        source = source.WithAuthoring(authoring);
        var dependencies = new[]
        {
            new QuestAiDependency(Guid.Parse("c7730000-0000-4000-8000-000000000006"), 1, "test:r073", 1,
                QuestAiDependencyKind.State, source.InitialStateId.ToString()),
            new QuestAiDependency(Guid.Parse("c7730000-0000-4000-8000-000000000007"), 1, "test:r073", 2,
                QuestAiDependencyKind.Participant, participant.Id.ToString("D")),
        };
        var plan = QuestQualityRuntimeService.CreateAiPlan(source,
            new LocalizedText("quest.r073.ai", "Proposer la suite de la mission."), dependencies,
            ["08_QUEST.md#orf-qst-025"], ["style:crypt"], "agent:test",
            Guid.Parse("c7730000-0000-4000-8000-000000000008"), "preview:quest:r073");
        plan = QuestQualityRuntimeService.Approve(plan, "author:test", DateTimeOffset.UnixEpoch);
        var quality = new QuestQualityProfile(Guid.Parse("c7730000-0000-4000-8000-000000000009"), 1,
            "test:r073", true, true, QuestQualityRuntimeService.GenerateTests(source, 7300), plan);
        var service = new QuestJournalScriptService(source.WithQuality(quality));

        var snapshot = service.ReadQuality(service.Handle);

        Assert.Equal(5, snapshot.ScenarioCount);
        Assert.Equal(11, snapshot.StepCount);
        Assert.Equal(6, snapshot.AssertionCount);
        Assert.True(snapshot.HumanApproved);
        Assert.Equal(QuestQualityTestKind.Nominal,
            service.ReadQualityScenario(service.Handle, quality.TestScenarios[0].Id).Kind);
        Assert.Equal(plan.TransactionId, service.ReadAiPlan(service.Handle).TransactionId);
    }

    private static QuestDefinition CreateQuest()
    {
        var initial = QuestStateId.From(Guid.Parse("c7600000-0000-4000-8000-000000000001"));
        var completed = QuestStateId.From(Guid.Parse("c7600000-0000-4000-8000-000000000002"));
        QuestStateDefinition State(QuestStateId id, string slug, int order, QuestTerminalKind terminal) => new(
            id, 1, "test:r007", slug, slug,
            new QuestJournalEntry(new LocalizedText($"quest.{slug}.summary", slug),
                new LocalizedText($"quest.{slug}.details", $"{slug} details"),
                contextLinks: slug == "search"
                    ? [
                        new QuestJournalLink(QuestJournalLinkKind.Custom, "one"),
                        new QuestJournalLink(QuestJournalLinkKind.Custom, "two"),
                    ]
                    : [], provenance: "test:r007"),
            terminal != QuestTerminalKind.None, order, order - 1, terminal, string.Empty,
            QuestReactivationRule.Never);
        return new(
            QuestId.From(Guid.Parse("c7700000-0000-4000-8000-000000000001")), 1, "test:r007",
            "script-quest", "Script quest", new("quest.script.title", "Script quest"),
            new("quest.script.description", "Script quest description"), "main", 80, "author:test", false,
            new(QuestProgressScope.Player, [QuestProgressScope.Player], false), initial,
            [State(initial, "search", 1, QuestTerminalKind.None),
             State(completed, "completed", 2, QuestTerminalKind.Completed)],
            [new QuestTransition(QuestTransitionId.From(Guid.Parse("c7800000-0000-4000-8000-000000000001")),
                initial, completed)]);
    }
}
