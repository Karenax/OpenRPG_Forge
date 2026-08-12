using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class DialogueGraphPersistenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AggregateAndWorkspaceRoundTripsPreserveCanonicalDialogueGraph()
    {
        var source = TestProjectFactory.CreateValid();
        var previous = source.Dialogues[0];
        var entry = previous.EntryNodeId;
        var end = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000020"));
        var flow = new DialogueChoice(
            DialogueChoiceId.From(Guid.Parse("d0050000-0000-4000-8000-000000000021")),
            new LocalizedText("dialogue.graph.continue", "Continue",
                [LocalizedTextVariant.Create("dialogue.graph.continue", "fr", "Continuer")], "author:test"),
            [new(DialogueConditionKind.HasItem, "ash-key", "true"),
             new(DialogueConditionKind.QuestStateEquals, "crypt", "completed", negated: true)],
            [new(DialogueActionKind.SetRelation, "elyra", "trusted"),
             new(DialogueActionKind.PlayAudio, "dialogue.continue", "play")],
            end, priority: 20, conditionCombination: DialogueConditionCombination.All);
        var reference = new DialogueChoice(
            DialogueChoiceId.From(Guid.Parse("d0050000-0000-4000-8000-000000000022")),
            new("dialogue.graph.inspect", "Inspect"), targetNodeId: end,
            edgeKind: DialogueEdgeKind.Reference, priority: 10);
        var speakerParticipant = Guid.Parse("d0050000-0000-4000-8000-000000000023");
        var narratorParticipant = Guid.Parse("d0050000-0000-4000-8000-000000000024");
        var graph = new DialogueDefinition(previous.Id, previous.Slug, previous.DisplayName, entry,
        [
            new(entry, previous.Nodes[0].SpeakerBlueprintId,
                new("dialogue.graph.entry", "Choose"), [], [flow, reference],
                DialogueNodeRole.PlayerChoice, speakerParticipant, null),
            new(end, null, new("dialogue.graph.end", "End"), [], [], DialogueNodeRole.End,
                narratorParticipant, null),
        ], previous.Presentation, provenance: "author:test",
        metadata: new Dictionary<string, string> { ["kind"] = "conversation", ["chapter"] = "crypt" },
        participants:
        [
            new(speakerParticipant, 1, "warden", previous.Nodes[0].SpeakerBlueprintId, "author:test"),
            new(narratorParticipant, 1, "narrator", null, "author:test"),
        ],
        entryPoints: [new(Guid.Parse("d0050000-0000-4000-8000-000000000025"), 1, "default", entry, 0, "author:test")],
        edges:
        [
            new(flow.Id, entry, end, DialogueEdgeKind.Flow, 0, 20),
            new(reference.Id, entry, end, DialogueEdgeKind.Reference, 1, 10),
        ]);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas, [graph], source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var workspace = ProjectSession.Create(Path.Combine(_temporaryRoot, "workspace"), project);

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.True(workspace.Succeeded);
        var reopened = ProjectSession.Open(workspace.Session!.RootDirectory);
        Assert.True(reopened.Succeeded);
        Assert.Equal(json, ProjectJsonSerializer.Serialize(reopened.Session!.Project));
        var persisted = Assert.Single(restored.Dialogues);
        Assert.Equal(DialogueNodeRole.PlayerChoice, persisted.Nodes[0].Role);
        Assert.Equal(DialogueEdgeKind.Reference, persisted.Edges[1].Kind);
        Assert.Equal([20, 10], persisted.Nodes[0].Choices.Select(choice => choice.Priority));
        Assert.Equal("crypt", persisted.Metadata["chapter"]);
        Assert.Equal("Continuer", persisted.Nodes[0].Choices[0].Text.Resolve("fr"));
        Assert.Equal(DialogueConditionCombination.All, persisted.Nodes[0].Choices[0].ConditionCombination);
        Assert.True(persisted.Nodes[0].Choices[0].Conditions[1].Negated);
        Assert.Equal([DialogueActionKind.SetRelation, DialogueActionKind.PlayAudio],
            persisted.Nodes[0].Choices[0].Actions.Select(item => item.Kind));
    }

    [Fact]
    public void R068AuthoringProfileRoundTripsTokensParticipantsEvaluationWizardAndNotes()
    {
        var source = TestProjectFactory.CreateValid();
        var previous = source.Dialogues[0];
        var entry = previous.Nodes.Single(item => item.Id == previous.EntryNodeId);
        var text = new LocalizedText(entry.Text.Id, entry.Text.SchemaVersion, "author:r068", entry.Text.Key,
            entry.Text.Fallback + " {{hero}}", entry.Text.Variants);
        var nodes = previous.Nodes.Select(item => item.Id == entry.Id
            ? new DialogueNode(item.Id, item.SpeakerBlueprintId, text, item.OnEnterActions, item.Choices,
                item.Role, item.ParticipantId, item.SubgraphKey)
            : item).ToArray();
        var authoredBase = new DialogueDefinition(previous.Id, previous.Slug, previous.DisplayName,
            previous.EntryNodeId, nodes, previous.Presentation, previous.SchemaVersion, previous.Provenance,
            previous.Metadata, previous.Participants, previous.EntryPoints, edges: null);
        var profile = DialogueAuthoringProfile.CreateDefault(authoredBase, "author:r068");
        var token = new DialogueTextToken(Guid.Parse("d0682000-0000-4000-8000-000000000001"), 1,
            "author:r068", "hero", DialogueTextTokenType.EntityName, true, null);
        profile = profile with
        {
            RequiredAtRuntime = true,
            Nodes = profile.Nodes.Select(item => item.NodeId == entry.Id
                ? item with
                {
                    Tokens = [token],
                    EvaluationMode = DialogueChoiceEvaluationMode.RandomDeterministic,
                    Notes = item.Notes with
                    {
                        Intent = "Présenter le gardien.",
                        Emotion = "solemn",
                        Source = "brief:r068",
                        Status = DialogueNodeEditorialStatus.Review,
                        Todos = ["Ajouter la voix"],
                        AuthorMention = "@karen",
                    },
                }
                : item).ToArray(),
        };
        var authored = authoredBase.WithAuthoring(profile);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas, source.Dialogues.Select(item => item.Id == authored.Id ? authored : item).ToArray(),
            source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var workspace = ProjectSession.Create(Path.Combine(_temporaryRoot, "r068"), project);

        Assert.True(workspace.Succeeded);
        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal(json, ProjectJsonSerializer.Serialize(ProjectSession.Open(
            workspace.Session!.RootDirectory).Session!.Project));
        var persisted = restored.Dialogues.Single(item => item.Id == authored.Id).Authoring!;
        Assert.True(persisted.RequiredAtRuntime);
        Assert.Equal(DialogueTextTokenType.EntityName,
            persisted.Nodes.Single(item => item.NodeId == entry.Id).Tokens.Single().Type);
        Assert.Equal(DialogueChoiceEvaluationMode.RandomDeterministic,
            persisted.Nodes.Single(item => item.NodeId == entry.Id).EvaluationMode);
        Assert.Equal("@karen", persisted.Nodes.Single(item => item.NodeId == entry.Id).Notes.AuthorMention);
    }

    [Fact]
    public void R069ExperienceProfileRoundTripsLifecycleSearchReviewMemoryAndSkillChecks()
    {
        var source = TestProjectFactory.CreateValid();
        var dialogue = source.Dialogues[0];
        var success = dialogue.EntryNodeId;
        var profile = DialogueExperienceProfile.CreateDefault(dialogue, "author:r069");
        profile = profile with
        {
            RequiredAtRuntime = true,
            Lifecycle = profile.Lifecycle with
            {
                CompletedActions = [new(DialogueActionKind.SetVariable, "dialogue.completed", "true")],
                AbortedActions = [new(DialogueActionKind.RunScript, "dialogue-aborted.forge", "reason")],
                RollbackOnAbort = true,
            },
            Search = profile.Search with
            {
                Pattern = "gardien|warden",
                UseRegex = true,
                ReplacementPreview = "guide",
                SpeakerRoles = [],
            },
            MemorySlots =
            [new(Guid.Parse("d0692000-0000-4000-8000-000000000001"), 1, "author:r069",
                DialogueMemoryScope.Player, "player:current", "trust", DialogueTextTokenType.Integer,
                DialogueMemoryDuration.Campaign, "0")],
            SkillChecks =
            [new(Guid.Parse("d0692000-0000-4000-8000-000000000002"), 1, "author:r069",
                dialogue.EntryNodeId, "persuasion", 12, "warden", true,
                DialogueSkillCheckVisibility.FullExplanation, success, success)],
        };
        var authored = dialogue.WithExperience(profile);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas, source.Dialogues.Select(item => item.Id == authored.Id ? authored : item).ToArray(),
            source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var workspace = ProjectSession.Create(Path.Combine(_temporaryRoot, "r069"), project);

        Assert.True(workspace.Succeeded);
        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal(json, ProjectJsonSerializer.Serialize(ProjectSession.Open(
            workspace.Session!.RootDirectory).Session!.Project));
        var persisted = restored.Dialogues.Single(item => item.Id == authored.Id).Experience!;
        Assert.True(persisted.RequiredAtRuntime);
        Assert.True(persisted.Lifecycle.RollbackOnAbort);
        Assert.True(persisted.Search.UseRegex);
        Assert.Equal(6, persisted.Review.Badges.Count);
        Assert.Equal(DialogueMemoryDuration.Campaign, Assert.Single(persisted.MemorySlots).Duration);
        Assert.Equal("persuasion", Assert.Single(persisted.SkillChecks).SkillId);
    }

    [Fact]
    public void R070ProductionProfileRoundTripsSubgraphsImpactCoverageCollaborationAndAi()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Dialogues[0];
        var nodes = original.Nodes.Select(item => item.Id == original.EntryNodeId
            ? new DialogueNode(item.Id, item.SpeakerBlueprintId, item.Text, item.OnEnterActions, item.Choices,
                DialogueNodeRole.Subgraph, item.ParticipantId, "embers")
            : item).ToArray();
        var dialogue = new DialogueDefinition(original.Id, original.Slug, original.DisplayName,
            original.EntryNodeId, nodes, original.Presentation, original.SchemaVersion, "author:r070",
            original.Metadata, original.Participants, original.EntryPoints);
        var p = "author:r070";
        var contractId = Guid.Parse("d0702000-0000-4000-8000-000000000001");
        var parameter = new DialogueSubgraphParameter(Guid.Parse("d0702000-0000-4000-8000-000000000002"),
            1, p, "trust", DialogueTextTokenType.Integer, DialogueSubgraphParameterDirection.Input, true);
        var test = new DialogueSubgraphTestCase(Guid.Parse("d0702000-0000-4000-8000-000000000003"), 1, p,
            "trust", new Dictionary<string, string> { ["trust"] = "4" },
            new Dictionary<string, string>());
        var profile = new DialogueProductionProfile(Guid.Parse("d0702000-0000-4000-8000-000000000004"),
            1, p, true, true,
            [new(contractId, 1, p, "embers", 2, [parameter], ["party:trust"], [test])],
            [new(Guid.Parse("d0702000-0000-4000-8000-000000000005"), 1, p, dialogue.EntryNodeId,
                contractId, new Dictionary<string, string> { ["trust"] = "party:trust" },
                new Dictionary<string, string>())],
            [new(Guid.Parse("d0702000-0000-4000-8000-000000000006"), 1, p, dialogue.EntryNodeId,
                DialogueConsequenceTargetKind.Quest, "quest:crypt", "advance", "2", "Advance quest")],
            new DialogueCoveragePolicy(Guid.Parse("d0702000-0000-4000-8000-000000000007"), 1, p, 64,
                new Dictionary<string, string> { ["quest:crypt"] = "active" },
                [new(Guid.Parse("d0702000-0000-4000-8000-000000000008"), 1, p, "session:crypt",
                    DialogueCoverageEvidenceKind.Session, [dialogue.EntryNodeId],
                    new Dictionary<string, string> { ["quest:crypt"] = "active" })]),
            new DialogueCollaborationProfile(Guid.Parse("d0702000-0000-4000-8000-000000000009"), 1, p,
                [new(Guid.Parse("d0702000-0000-4000-8000-000000000010"), 1, p, dialogue.EntryNodeId, 1,
                    "author", "{\"text\":\"entry\"}", DateTimeOffset.UnixEpoch)], [],
                [new(Guid.Parse("d0702000-0000-4000-8000-000000000011"), 1, p, dialogue.EntryNodeId,
                    "reviewer", "Review impact", false, DateTimeOffset.UnixEpoch)], []),
            [new(Guid.Parse("d0702000-0000-4000-8000-000000000012"), 1, p,
                Guid.Parse("d0702000-0000-4000-8000-000000000013"),
                Guid.Parse("d0702000-0000-4000-8000-000000000014"), DialogueAiAuthorizationLevel.AuthorApply,
                "Crypt promise", ["offer"], ["style:embers"], ["context#dialogue"], "add:embers", [], true,
                false)]);
        var authored = dialogue.WithProduction(profile);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas, source.Dialogues.Select(item => item.Id == authored.Id ? authored : item).ToArray(),
            source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var workspace = ProjectSession.Create(Path.Combine(_temporaryRoot, "r070"), project);

        Assert.True(workspace.Succeeded);
        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal(json, ProjectJsonSerializer.Serialize(ProjectSession.Open(
            workspace.Session!.RootDirectory).Session!.Project));
        var persisted = restored.Dialogues.Single(item => item.Id == authored.Id).Production!;
        Assert.True(persisted.RequiredAtRuntime);
        Assert.Equal(2, Assert.Single(persisted.Subgraphs).Version);
        Assert.Equal(DialogueConsequenceTargetKind.Quest, Assert.Single(persisted.Consequences).Kind);
        Assert.Equal(DialogueCoverageEvidenceKind.Session, Assert.Single(persisted.Coverage.Evidence).Kind);
        Assert.Single(persisted.Collaboration.Comments);
        Assert.True(Assert.Single(persisted.AiPlans).PreviewOnly);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }
}
