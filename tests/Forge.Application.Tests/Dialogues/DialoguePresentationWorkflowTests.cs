using Forge.Application.Commands;
using Forge.Application.Dialogues;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Sequences;

namespace Forge.Application.Tests.Dialogues;

public sealed class DialoguePresentationWorkflowTests
{
    [Fact]
    public void DryRunApplyUndoAndRedoPreserveCanonicalPresentation()
    {
        var (state, dialogue, profile) = CreateState();
        var handler = new SetDialoguePresentationHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var history = new ProjectCommandHistory(state);
        var command = new SetDialoguePresentationCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            dialogue.Id, profile, true);

        var preview = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Contains("1 animation", preview.Patch!.Summary, StringComparison.Ordinal);
        Assert.Contains("1 timed choice", preview.Patch.Summary, StringComparison.Ordinal);
        Assert.Contains("1 variant set", preview.Patch.Summary, StringComparison.Ordinal);
        Assert.Equal(profile.Id, history.State.Project.Dialogues[0].Presentation!.Id);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Dialogues[0].Presentation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.Id, history.State.Project.Dialogues[0].Presentation!.Id);
    }

    [Fact]
    public void RevisionMissingDialogueAndInvalidProfileAreRejected()
    {
        var (state, dialogue, profile) = CreateState();
        var handler = new SetDialoguePresentationHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var conflict = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision.Next(),
            dialogue.Id, profile, false));
        var missing = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            DialogueId.From(Guid.NewGuid()), profile, false));
        var invalid = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            dialogue.Id, profile with { ServerAuthoritativeInterjections = false }, false));

        Assert.Equal(SetDialoguePresentationHandler.RevisionConflictCode, Assert.Single(conflict.Diagnostics).Code);
        Assert.Equal(SetDialoguePresentationHandler.MissingDialogueCode, Assert.Single(missing.Diagnostics).Code);
        Assert.Contains(invalid.Diagnostics, item => item.Code == "dialogue.interjection.authority_required");
        Assert.Equal(state.Revision, invalid.State.Revision);
    }

    [Fact]
    public void ForgeScriptUsesTypedHandleAndRejectsStaleIdentity()
    {
        var (_, dialogue, profile) = CreateState();
        var scripted = dialogue.WithPresentation(profile);
        var service = new DialoguePresentationScriptService(scripted);

        var snapshot = service.Read(service.Handle);
        var adaptive = service.ResolveAdaptive(service.Handle, dialogue.EntryNodeId, 3_001,
            new DialogueAdaptiveRuntimeContext("fr", true, 2, false, false, 42,
                Guid.Parse("d1310000-0000-4000-8000-000000000030"),
                new Dictionary<Guid, IReadOnlyList<Guid>>()));

        Assert.Equal(1, snapshot.AnimationCueCount);
        Assert.Equal(1, snapshot.BookmarkCount);
        Assert.Equal(1, snapshot.TimedChoiceCount);
        Assert.Equal(1, snapshot.VariantSetCount);
        Assert.Equal(2, snapshot.VariantCount);
        Assert.True(adaptive.TimedChoice!.TimedOut);
        Assert.NotNull(adaptive.Variant);
        Assert.Throws<InvalidOperationException>(() => service.Read(service.Handle with { Id = Guid.NewGuid() }));
    }

    private static (ProjectState State, DialogueDefinition Dialogue, DialoguePresentationProfile Profile) CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("d1310000-0000-4000-8000-000000000001")),
            "dialogue-lab", "Dialogue lab", []);
        var speaker = BlueprintId.From(Guid.Parse("d1310000-0000-4000-8000-000000000002"));
        var blueprint = new BlueprintDefinition(speaker, "elyra", "Elyra", BlueprintKind.Creature);
        var node = DialogueNodeId.From(Guid.Parse("d1310000-0000-4000-8000-000000000003"));
        var choice = DialogueChoiceId.From(Guid.Parse("d1310000-0000-4000-8000-000000000005"));
        var dialogue = new DialogueDefinition(DialogueId.From(Guid.Parse("d1310000-0000-4000-8000-000000000004")),
            "elyra", "Elyra", node,
            [new(node, speaker, new("dialogue.elyra", "Bonjour."), choices:
                [new(choice, new("dialogue.elyra.answer", "Continuer."))])]);
        var profile = new DialoguePresentationProfile(Guid.Parse("d1310000-0000-4000-8000-000000000010"),
            1, "author:test", false, true,
            [new(Guid.Parse("d1310000-0000-4000-8000-000000000011"), node, speaker, "talk", 0, 1000, 100,
                DialogueGesture.Greet, null, DialogueAnimationInterruptionPolicy.BlendOut, "idle", "author:test")],
            [new(Guid.Parse("d1310000-0000-4000-8000-000000000012"), node, speaker, "fr", null, "take-01",
                new("dialogue.elyra", "Bonjour."), "neutral", "warm", 1000, null, null, true, "author:test")],
            [new(Guid.Parse("d1310000-0000-4000-8000-000000000013"), node,
                DialogueCameraShotMode.Automatic, null, speaker, CameraFraming.Medium, 55, 1000, 100,
                false, true, true, .05f, 1, "author:test")],
            [new(Guid.Parse("d1310000-0000-4000-8000-000000000014"), node, "Accueil",
                DialogueBookmarkScope.Personal, null, "tester", $"dialogue://{dialogue.Id}/{node}", "author:test")],
            [],
            [new(Guid.Parse("d1310000-0000-4000-8000-000000000015"), node, 3_000, choice,
                DialogueTimedChoicePausePolicy.AccessibilityOnly, DialogueRuntimeAuthorityScope.Server, 2,
                "dialogue.timeout", "tester", "author:test")],
            [new(Guid.Parse("d1310000-0000-4000-8000-000000000016"), node, "greeting",
                DialogueVariantSeedScope.Session, 1, 1, DialogueRuntimeAuthorityScope.Server, true,
                [new(Guid.Parse("d1310000-0000-4000-8000-000000000017"),
                    new("dialogue.elyra.warm", "Bienvenue."), 2, [], "author:test"),
                 new(Guid.Parse("d1310000-0000-4000-8000-000000000018"),
                    new("dialogue.elyra.calm", "Bonjour."), 1, [], "author:test")], "author:test")]);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("d1310000-0000-4000-8000-000000000020")),
            "Dialogue presentation", [blueprint], [area], [dialogue]);
        return (new(project, new ProjectRevision(2)), dialogue, profile);
    }
}
