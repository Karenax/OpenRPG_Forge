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

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }
}
