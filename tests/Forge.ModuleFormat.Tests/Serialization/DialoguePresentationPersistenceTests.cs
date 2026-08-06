using System.Text.Json.Nodes;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Sequences;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class DialoguePresentationPersistenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AggregateAndWorkspaceRoundTripsPreservePresentationExactly()
    {
        var project = CompleteProject();
        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var workspace = Path.Combine(_temporaryRoot, "workspace");

        var created = ProjectSession.Create(workspace, project);
        var reopened = ProjectSession.Open(workspace);

        Assert.Contains("\"formatVersion\": \"0.6.30\"", json, StringComparison.Ordinal);
        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.True(created.Succeeded);
        Assert.True(reopened.Succeeded);
        Assert.Equal(json, ProjectJsonSerializer.Serialize(reopened.Session!.Project));
        Assert.Contains("\"formatVersion\": \"1.24.0\"",
            File.ReadAllText(reopened.Session.ProjectFilePath), StringComparison.Ordinal);
        Assert.Equal("dialogue://6393019c-06c3-466c-ab93-9b4093368f36/3f92330b-c513-437e-9444-6e0dbbe8e555",
            restored.Dialogues[0].Presentation!.Bookmarks[0].DeepLink);
        Assert.Single(restored.Dialogues[0].Presentation!.TimedChoices);
        Assert.Equal(2, Assert.Single(restored.Dialogues[0].Presentation!.VariantSets).Variants.Count);
    }

    [Fact]
    public void PreviousFormatWithoutPresentationMigratesToOptionalFallback()
    {
        var source = TestProjectFactory.CreateValid();
        var node = JsonNode.Parse(ProjectJsonSerializer.Serialize(source))!.AsObject();
        node["formatVersion"] = ProjectJsonSerializer.PreDialoguePresentationFormatVersion;
        foreach (var dialogue in node["project"]!.AsObject()["dialogues"]!.AsArray())
            dialogue!.AsObject().Remove("presentation");

        var restored = ProjectJsonSerializer.Deserialize(node.ToJsonString());

        Assert.Null(Assert.Single(restored.Dialogues).Presentation);
    }

    [Fact]
    public void PreviousPresentationFormatMigratesAdaptiveCollectionsToEmpty()
    {
        var source = CompleteProject();
        var node = JsonNode.Parse(ProjectJsonSerializer.Serialize(source))!.AsObject();
        node["formatVersion"] = ProjectJsonSerializer.PreAdaptiveDialogueFormatVersion;
        var presentation = node["project"]!.AsObject()["dialogues"]!.AsArray()[0]!.AsObject()["presentation"]!.AsObject();
        presentation.Remove("timedChoices");
        presentation.Remove("variantSets");

        var restored = ProjectJsonSerializer.Deserialize(node.ToJsonString());

        Assert.Empty(restored.Dialogues[0].Presentation!.TimedChoices);
        Assert.Empty(restored.Dialogues[0].Presentation!.VariantSets);
        Assert.Single(restored.Dialogues[0].Presentation!.AnimationCues);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }

    private static ProjectDefinition CompleteProject()
    {
        var source = TestProjectFactory.CreateValid();
        var dialogue = source.Dialogues[0];
        var node = dialogue.EntryNodeId;
        var speaker = dialogue.Nodes[0].SpeakerBlueprintId!;
        var choice = dialogue.Nodes[0].Choices[0].Id;
        var profile = new DialoguePresentationProfile(
            Guid.Parse("d1320000-0000-4000-8000-000000000010"), 1, "author:test", false, true,
            [new(Guid.Parse("d1320000-0000-4000-8000-000000000011"), node, speaker, "talk.warning", 0,
                1400, 180, DialogueGesture.Threaten, null, DialogueAnimationInterruptionPolicy.BlendOut,
                "idle", "author:test")],
            [new(Guid.Parse("d1320000-0000-4000-8000-000000000012"), node, speaker, "en", null,
                "warden-01", dialogue.Nodes[0].Text, "viseme-neutral", "angry", 1400,
                null, null, true, "author:test")],
            [new(Guid.Parse("d1320000-0000-4000-8000-000000000013"), node,
                DialogueCameraShotMode.Automatic, null, speaker, CameraFraming.CloseUp, 48, 1400, 200,
                true, true, true, .08f, 20, "author:test")],
            [new(Guid.Parse("d1320000-0000-4000-8000-000000000014"), node, "Warden warning",
                DialogueBookmarkScope.Shared, "Approve timing and framing.", "reviewer",
                $"dialogue://{dialogue.Id}/{node}", "author:test")], [],
            [new(Guid.Parse("d1320000-0000-4000-8000-000000000015"), node, 4_000, choice,
                DialogueTimedChoicePausePolicy.AccessibilityOnly, DialogueRuntimeAuthorityScope.Server, 3,
                "dialogue.timeout.warning", "server:test", "author:test")],
            [new(Guid.Parse("d1320000-0000-4000-8000-000000000016"), node, "warden-warning",
                DialogueVariantSeedScope.ServerRevision, 9, 1, DialogueRuntimeAuthorityScope.Server, true,
                [new(Guid.Parse("d1320000-0000-4000-8000-000000000017"),
                    new("dialogue.warden.warning.a", "Do not enter."), 2, [], "author:test"),
                 new(Guid.Parse("d1320000-0000-4000-8000-000000000018"),
                    new("dialogue.warden.warning.b", "Turn back."), 1, [], "author:test")], "author:test")]);
        return new(source.Id, source.Title, source.Module, source.Blueprints, source.Areas,
            [dialogue.WithPresentation(profile)], source.Quests);
    }
}
