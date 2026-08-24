using System.Text.Json.Nodes;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class QuestJournalPersistenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Aggregate_and_workspace_round_trip_rich_quest_journal_exactly()
    {
        var project = RichProject();
        var aggregate = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(aggregate);
        var created = ProjectSession.Create(_temporaryRoot, project);
        var reopened = ProjectSession.Open(_temporaryRoot);

        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(restored));
        Assert.True(created.Succeeded);
        Assert.True(reopened.Succeeded);
        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(reopened.Session!.Project));
        var quest = Assert.Single(restored.Quests);
        Assert.Equal("main", quest.Category);
        Assert.Equal(QuestProgressScope.Party, quest.InstancePolicy.DefaultScope);
        Assert.Equal(QuestTerminalKind.Completed, quest.States[^1].TerminalKind);
        Assert.Single(quest.States[0].Journal.ContextLinks);
        Assert.Equal([1], quest.Transitions.Select(item => item.DisplayOrder));
        Assert.Equal("dialogue.accept", Assert.Single(quest.Transitions).EventBinding.EventName);
        Assert.Equal(QuestTransitionIdempotence.PerEvent, Assert.Single(quest.Transitions).Idempotence);
        Assert.Single(quest.Simulation.Events);
    }

    [Fact]
    public void Previous_quest_shape_migrates_metadata_order_terminal_and_localization_deterministically()
    {
        var node = JsonNode.Parse(ProjectJsonSerializer.Serialize(RichProject()))!.AsObject();
        node["formatVersion"] = ProjectJsonSerializer.PrePostLaunchCapabilitiesFormatVersion;
        var quest = node["project"]!["quests"]!.AsArray()[0]!.AsObject();
        foreach (var property in new[]
                 {
                     "schemaVersion", "provenance", "title", "description", "category", "priority", "owner",
                     "requiredAtRuntime", "instancePolicy",
                     "simulation",
                 })
            quest.Remove(property);
        foreach (var stateNode in quest["states"]!.AsArray())
        {
            var state = stateNode!.AsObject();
            foreach (var property in new[]
                     {
                         "schemaVersion", "provenance", "slug", "displayOrder", "legacyJournalIndex",
                         "terminalKind", "customTerminalCode", "reactivation",
                     })
                state.Remove(property);
            var journal = state["journal"]!.AsObject();
            foreach (var property in new[]
                     {
                         "id", "schemaVersion", "provenance", "summary", "details", "objectives", "contextLinks",
                     })
                journal.Remove(property);
        }
        foreach (var transitionNode in quest["transitions"]!.AsArray())
        {
            var transition = transitionNode!.AsObject();
            foreach (var property in new[]
                     {
                         "schemaVersion", "provenance", "displayOrder", "eventBinding", "priority",
                         "idempotence", "idempotenceKey", "forgeScriptApiVersion",
                     })
                transition.Remove(property);
        }

        var restored = ProjectJsonSerializer.Deserialize(node.ToJsonString());
        var migrated = Assert.Single(restored.Quests);

        Assert.Equal("journal", migrated.Category);
        Assert.Equal([1, 2], migrated.States.Select(item => item.DisplayOrder));
        Assert.Equal([0, 1], migrated.States.Select(item => item.LegacyJournalIndex));
        Assert.Equal(QuestTerminalKind.Completed, migrated.States[^1].TerminalKind);
        Assert.Equal("quest.ashes.initial", migrated.States[0].Journal.Summary.Key);
        Assert.Equal(1, Assert.Single(migrated.Transitions).DisplayOrder);
        Assert.Equal("manual", Assert.Single(migrated.Transitions).EventBinding.EventName);
        Assert.Empty(migrated.Simulation.Events);
    }

    [Fact]
    public void Quest_authoring_profile_round_trips_objectives_rewards_notes_cast_and_wizard_exactly()
    {
        var source = RichProject();
        var quest = source.Quests.Single();
        var objective = new QuestAuthoringObjective(Guid.Parse("c7110000-0000-4000-8000-000000000401"),
            1, "test:r071", 1, QuestObjectiveKind.Boolean,
            new LocalizedText("quest.r071.objective", "Allumer le brasier"), 1, [], null, 0, [], null, false);
        var creature = source.Blueprints.First(item => item.Kind == Forge.Domain.Blueprints.BlueprintKind.Creature);
        var participant = new QuestAuthoringParticipant(
            Guid.Parse("c7110000-0000-4000-8000-000000000402"), 1, "test:r071", 1, "guide",
            QuestParticipantBindingKind.Blueprint, creature.Slug, ["quest:active"], true);
        var profile = new QuestAuthoringProfile(Guid.Parse("c7110000-0000-4000-8000-000000000403"), 1,
            "test:r071", true, true, [objective],
            new QuestAuthoringRewardBundle(Guid.Parse("c7110000-0000-4000-8000-000000000404"), 1,
                "test:r071", 10, new Dictionary<string, int>(),
                new Dictionary<string, decimal> { ["ember"] = 5 }, new Dictionary<string, double>(),
                ["crypt-open"], ["choice:dawn"]),
            new QuestAuthorNotes("Design", "Canon", "author:test", QuestEditorialStatus.Approved,
                ["risk"], ["todo"], ["08_QUEST.md"], [new QuestAuthorReview(
                    Guid.Parse("c7110000-0000-4000-8000-000000000405"), 1, "test:r071", "reviewer:test",
                    QuestEditorialStatus.Approved, "Approved", DateTimeOffset.Parse("2026-08-11T00:00:00Z"))]),
            [participant], new QuestPlotWizardPlan(
                Guid.Parse("c7110000-0000-4000-8000-000000000406"), 1, "test:r071",
                [source.Areas[0].Slug], [participant.Id], [source.Blueprints[0].Slug],
                [source.Dialogues[0].Id], [objective.Id], quest.Transitions.Select(item => item.Id).ToArray(),
                ["nominal", "failure", "repeat", "multiplayer", "save-resume"], false));
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints, source.Areas,
            source.Dialogues, [quest.WithAuthoring(profile)]);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        var authoring = Assert.Single(restored.Quests).Authoring;
        Assert.NotNull(authoring);
        Assert.Equal(QuestObjectiveKind.Boolean, Assert.Single(authoring.Objectives).Kind);
        Assert.Equal("guide", Assert.Single(authoring.Participants).Role);
        Assert.Equal(5, authoring.Wizard.GeneratedTestScenarios.Count);
        Assert.False(authoring.Wizard.GeneratesOpaqueCode);
    }

    [Fact]
    public void Quest_experience_round_trips_resources_steps_visibility_history_and_markers_exactly()
    {
        var source = RichProject();
        var quest = source.Quests.Single();
        var objective = new QuestAuthoringObjective(Guid.Parse("c7120000-0000-4000-8000-000000000601"),
            1, "test:r072", 1, QuestObjectiveKind.Destination,
            new LocalizedText("quest.r072.objective", "Atteindre la crypte"), 0, [], source.Areas[0].Slug,
            0, [], null, false);
        var participant = new QuestAuthoringParticipant(Guid.Parse("c7120000-0000-4000-8000-000000000602"),
            1, "test:r072", 1, "guide", QuestParticipantBindingKind.Query, "query:guide", [], true);
        var authoring = new QuestAuthoringProfile(Guid.Parse("c7120000-0000-4000-8000-000000000603"),
            1, "test:r072", true, false, [objective],
            new QuestAuthoringRewardBundle(Guid.Parse("c7120000-0000-4000-8000-000000000604"), 1,
                "test:r072", 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(),
                new Dictionary<string, double>(), [], []),
            new QuestAuthorNotes("Design", "Canon", "author:test", QuestEditorialStatus.Review, [], [],
                ["source"], [new QuestAuthorReview(Guid.Parse("c7120000-0000-4000-8000-000000000605"),
                    1, "test:r072", "reviewer:test", QuestEditorialStatus.Review, "Reviewed",
                    DateTimeOffset.UnixEpoch)]), [participant],
            new QuestPlotWizardPlan(Guid.Parse("c7120000-0000-4000-8000-000000000606"), 1,
                "test:r072", [source.Areas[0].Slug], [participant.Id], [], [source.Dialogues[0].Id],
                [objective.Id], quest.Transitions.Select(item => item.Id).ToArray(),
                ["nominal", "failure", "repeat", "multiplayer", "save-resume"], false));
        quest = quest.WithAuthoring(authoring);
        var experience = new QuestExperienceProfile(Guid.Parse("c7120000-0000-4000-8000-000000000607"),
            1, "test:r072", true, true,
            [new QuestExperienceResource(Guid.Parse("c7120000-0000-4000-8000-000000000608"), 1,
                "test:r072", 1, QuestExperienceResourceKind.Asset, "asset:ember-prism", "owner:module", true,
                ["quest:active"])],
            [new QuestExperienceStep(Guid.Parse("c7120000-0000-4000-8000-000000000609"), 1,
                "template:r072:v2", 1, new LocalizedText("quest.r072.step", "Inspecter le prisme"),
                [objective.Id], quest.Transitions.Select(item => item.Id).ToArray(), "investigation", 2, true)],
            new QuestExperienceVisibilityPolicy(Guid.Parse("c7120000-0000-4000-8000-000000000610"), 1,
                "test:r072", QuestExperienceVisibility.Hidden,
                new Dictionary<QuestExperienceVisibility, IReadOnlyList<string>>
                {
                    [QuestExperienceVisibility.Rumor] = ["heard"],
                    [QuestExperienceVisibility.Discovered] = ["heard", "seen"],
                }, true),
            [new QuestExperienceHistoryEvent(Guid.Parse("c7120000-0000-4000-8000-000000000611"), 1,
                "test:r072", quest.Id, "hidden", "discovered", "dialogue", "player:test",
                DateTimeOffset.Parse("2026-08-11T08:00:00Z"), new Dictionary<string, string> { ["seed"] = "72" },
                new LocalizedText("quest.r072.history", "Le prisme est révélé."))],
            [new QuestExperienceMarker(Guid.Parse("c7120000-0000-4000-8000-000000000612"), 1,
                "test:r072", 1, objective.Id, QuestExperienceMarkerBindingKind.Query, "query:ember-prism",
                QuestExperienceVisibility.Discovered, 4, true,
                new LocalizedText("quest.r072.marker", "Prisme des braises"), new Vector3Value(1, 0, 2))]);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints, source.Areas,
            source.Dialogues, [quest.WithExperience(experience)]);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var reopened = ProjectSession.Create(_temporaryRoot, project);

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.True(reopened.Succeeded);
        var profile = Assert.Single(restored.Quests).Experience;
        Assert.NotNull(profile);
        Assert.Equal(QuestExperienceResourceKind.Asset, Assert.Single(profile.Resources).Kind);
        Assert.Equal(2, Assert.Single(profile.Steps).TemplateVersion);
        Assert.Equal(QuestExperienceVisibility.Hidden, profile.Visibility.Initial);
        Assert.Equal("dialogue", Assert.Single(profile.History).Cause);
        Assert.Equal(new Vector3Value(1, 0, 2), Assert.Single(profile.Markers).FallbackPosition);
    }

    [Fact]
    public void Quest_quality_round_trips_generated_graph_and_governed_ai_plan_exactly()
    {
        var source = RichProject();
        var quest = source.Quests.Single();
        var objective = new QuestAuthoringObjective(Guid.Parse("c7130000-0000-4000-8000-000000000601"),
            1, "test:r073", 1, QuestObjectiveKind.Boolean,
            new LocalizedText("quest.r073.objective", "Rétablir le signal"), 1, [], null, 0, [], null, false);
        var participant = new QuestAuthoringParticipant(Guid.Parse("c7130000-0000-4000-8000-000000000602"),
            1, "test:r073", 1, "guide", QuestParticipantBindingKind.Query, "query:guide", [], true);
        var authoring = new QuestAuthoringProfile(Guid.Parse("c7130000-0000-4000-8000-000000000603"),
            1, "test:r073", true, false, [objective],
            new QuestAuthoringRewardBundle(Guid.Parse("c7130000-0000-4000-8000-000000000604"), 1,
                "test:r073", 0, new Dictionary<string, int>(), new Dictionary<string, decimal>(),
                new Dictionary<string, double>(), [], []),
            new QuestAuthorNotes("Design", "Canon", "author:test", QuestEditorialStatus.Review, [], [], ["source"],
                [new QuestAuthorReview(Guid.Parse("c7130000-0000-4000-8000-000000000610"), 1,
                    "test:r073", "reviewer:test", QuestEditorialStatus.Review, "Reviewed",
                    DateTimeOffset.UnixEpoch)]),
            [participant], new QuestPlotWizardPlan(Guid.Parse("c7130000-0000-4000-8000-000000000605"),
                1, "test:r073", [source.Areas[0].Slug], [participant.Id], [], [source.Dialogues[0].Id],
                [objective.Id], quest.Transitions.Select(item => item.Id).ToArray(), ["a", "b", "c", "d", "e"], false));
        quest = quest.WithAuthoring(authoring);
        var dependencies = new[]
        {
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000606"), 1, "test:r073", 1,
                QuestAiDependencyKind.State, quest.InitialStateId.ToString()),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000607"), 1, "test:r073", 2,
                QuestAiDependencyKind.Zone, source.Areas[0].Slug),
        };
        var plan = QuestQualityRuntimeService.CreateAiPlan(quest,
            new LocalizedText("quest.r073.ai", "Proposer une quête cohérente avec le canon."), dependencies,
            ["08_QUEST.md#orf-qst-025"], ["style:crypt"], "agent:test",
            Guid.Parse("c7130000-0000-4000-8000-000000000608"), "preview:quest:r073");
        plan = QuestQualityRuntimeService.Approve(plan, "author:test", DateTimeOffset.UnixEpoch);
        var quality = new QuestQualityProfile(Guid.Parse("c7130000-0000-4000-8000-000000000609"), 1,
            "test:r073", true, true, QuestQualityRuntimeService.GenerateTests(quest, 7300), plan);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints, source.Areas,
            source.Dialogues, [quest.WithQuality(quality)]);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var created = ProjectSession.Create(_temporaryRoot, project);
        var profileJson = QuestQualityProfileJsonSerializer.Serialize(quality);
        var restoredProfile = QuestQualityProfileJsonSerializer.Deserialize(profileJson);

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.True(created.Succeeded);
        var profile = Assert.Single(restored.Quests).Quality;
        Assert.NotNull(profile);
        Assert.Equal(5, profile.TestScenarios.Count);
        Assert.Equal(QuestQualityTestKind.SaveResume, profile.TestScenarios[^1].Kind);
        Assert.Equal(plan.TransactionId, profile.AiPlan.TransactionId);
        Assert.Equal("author:test", profile.AiPlan.ApprovedBy);
        Assert.Equal(2, profile.AiPlan.Dependencies.Count);
        Assert.Equal(profileJson, QuestQualityProfileJsonSerializer.Serialize(restoredProfile));
        Assert.Equal(5, restoredProfile.TestScenarios.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }

    private static ProjectDefinition RichProject()
    {
        var source = TestProjectFactory.CreateValid();
        var legacy = Assert.Single(source.Quests);
        var states = legacy.States.Select((state, index) => new QuestStateDefinition(
            state.Id, 1, "test:r007", index == 0 ? "initial" : "completed", state.DisplayName,
            new QuestJournalEntry(
                new LocalizedText(index == 0 ? "quest.ashes.initial" : "quest.ashes.completed",
                    state.Journal.Fallback, provenance: "test:r007"),
                new LocalizedText(index == 0 ? "quest.ashes.initial.details" : "quest.ashes.completed.details",
                    $"{state.Journal.Fallback} — détails", provenance: "test:r007"),
                objectives: index == 0 ? [new("quest.ashes.objective", "Trouver la clé")] : [],
                contextLinks: index == 0
                    ? [new QuestJournalLink(QuestJournalLinkKind.Custom, "lore:ember-key")]
                    : [], provenance: "test:r007"),
            state.IsTerminal, index + 1, 10 + index,
            state.IsTerminal ? QuestTerminalKind.Completed : QuestTerminalKind.None, string.Empty,
            state.IsTerminal ? QuestReactivationRule.Manual : QuestReactivationRule.Never)).ToArray();
        var transitions = legacy.Transitions.Select((transition, index) => new QuestTransition(
            transition.Id, 1, "test:r008", transition.FromStateId, transition.ToStateId, index + 1,
            new QuestEventBinding(QuestEventKind.Dialogue, "dialogue.accept", "dialogue:elyra", "test:r008"),
            80, QuestTransitionIdempotence.PerEvent, "accept", 1, transition.Conditions,
            transition.Actions)).ToArray();
        var simulation = new QuestSimulationProfile(Guid.Parse("a8080000-0000-4000-8000-000000000031"), 10,
            events: [new QuestSimulationEvent(Guid.Parse("a8080000-0000-4000-8000-000000000032"),
                "dialogue.accept", new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero), "player:test")],
            provenance: "test:r008");
        var quest = new QuestDefinition(legacy.Id, 1, "test:r007", legacy.Slug, legacy.DisplayName,
            new("quest.ashes.title", legacy.DisplayName),
            new("quest.ashes.description", "Libérez la Crypte des Cendres."),
            "main", 90, "author:karen", true,
            new(QuestProgressScope.Party, [QuestProgressScope.Party], true), legacy.InitialStateId,
            states, transitions, legacy.Operations, simulation);
        return new(source.Id, source.Title, source.Module, source.Blueprints, source.Areas, source.Dialogues, [quest]);
    }
}
