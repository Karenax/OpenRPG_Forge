using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Sequences;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialoguePresentationTests
{
    [Fact]
    public void CompleteProfileValidatesAndResolvesLocalizedAccessiblePresentation()
    {
        var dialogue = Complete();
        var profile = dialogue.Presentation!;
        var companion = profile.InterjectionRules[0].CompanionBlueprintId;
        var context = new DialoguePresentationRuntimeContext("fr-FR", false, false, false, true,
            DateTimeOffset.Parse("2026-08-06T12:00:00Z"),
            [new(companion, true, 75, new HashSet<string> { "loyal", "wry" })],
            new Dictionary<Guid, DateTimeOffset>());

        var result = DialoguePresentationRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, context);

        Assert.Empty(DialoguePresentationValidator.Validate(dialogue));
        Assert.Single(result.AnimationCues);
        Assert.Equal("fr-FR", result.VoiceCue!.Locale);
        Assert.True(result.UsesSubtitleFallback);
        Assert.NotNull(result.CameraShot);
        Assert.NotNull(result.Interjection);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void CameraRespectsPlayerControlAndReducedMotion()
    {
        var dialogue = Complete();
        var baseContext = new DialoguePresentationRuntimeContext("fr-FR", false, true, false, true,
            DateTimeOffset.UnixEpoch, [], new Dictionary<Guid, DateTimeOffset>());

        var player = DialoguePresentationRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, baseContext);
        var reduced = DialoguePresentationRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId,
            baseContext with { PlayerControlsCamera = false, ReduceMotion = true });

        Assert.Null(player.CameraShot);
        Assert.Null(reduced.CameraShot);
    }

    [Fact]
    public void InterjectionsAreServerAuthoritativeAndCooldownAware()
    {
        var dialogue = Complete();
        var rule = dialogue.Presentation!.InterjectionRules[0];
        var companion = new DialogueCompanionState(rule.CompanionBlueprintId, true, 100,
            new HashSet<string> { "loyal", "wry" });
        var now = DateTimeOffset.Parse("2026-08-06T12:00:00Z");
        var client = new DialoguePresentationRuntimeContext("fr", false, false, false, false, now,
            [companion], new Dictionary<Guid, DateTimeOffset>());
        var cooldown = client with
        {
            IsServer = true,
            LastInterjections = new Dictionary<Guid, DateTimeOffset> { [rule.Id] = now.AddSeconds(-5) },
        };

        var denied = DialoguePresentationRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, client);
        var suppressed = DialoguePresentationRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, cooldown);

        Assert.Equal("dialogue.interjection.authority_denied", Assert.Single(denied.Diagnostics).Code);
        Assert.Null(denied.Interjection);
        Assert.Null(suppressed.Interjection);
    }

    [Fact]
    public void InvalidResourcesReferencesAndDuplicateUuidsAreRejected()
    {
        var dialogue = Complete();
        var profile = dialogue.Presentation!;
        var invalid = dialogue.WithPresentation(profile with
        {
            VoiceCues = [profile.VoiceCues[0] with
            {
                Id = profile.AnimationCues[0].Id,
                Resource = "../voice.wav",
                LicenseSpdx = null,
                ContentSha256 = "bad",
            }],
            CameraShots = [profile.CameraShots[0] with { FieldOfViewDegrees = 200 }],
        });

        var diagnostics = DialoguePresentationValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == "dialogue.presentation.uuid_duplicate");
        Assert.Contains(diagnostics, item => item.Code == "dialogue.voice.invalid");
        Assert.Contains(diagnostics, item => item.Code == "dialogue.camera.invalid");
    }

    [Fact]
    public void MissingProfileHasOptionalFallbackOrRequiredDiagnostic()
    {
        var dialogue = Complete().WithPresentation(null);
        var optional = new DialoguePresentationRuntimeContext("fr", false, false, false, true,
            DateTimeOffset.UnixEpoch, [], new Dictionary<Guid, DateTimeOffset>());

        var fallback = DialoguePresentationRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, optional);
        var required = DialoguePresentationRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId,
            optional with { RequirePresentation = true });

        Assert.True(fallback.OptionalFallback);
        Assert.Equal(DialoguePresentationValidator.RequiredMissingCode, Assert.Single(required.Diagnostics).Code);
        Assert.False(required.OptionalFallback);
    }

    internal static DialogueDefinition Complete()
    {
        var dialogueId = DialogueId.Parse("d1300000-0000-4000-8000-000000000001");
        var entry = DialogueNodeId.Parse("d1300000-0000-4000-8000-000000000002");
        var interjection = DialogueNodeId.Parse("d1300000-0000-4000-8000-000000000003");
        var speaker = BlueprintId.Parse("d1300000-0000-4000-8000-000000000004");
        var companion = BlueprintId.Parse("d1300000-0000-4000-8000-000000000005");
        var profile = new DialoguePresentationProfile(
            Guid.Parse("d1300000-0000-4000-8000-000000000010"), 1, "author:test", false, true,
            [new(Guid.Parse("d1300000-0000-4000-8000-000000000011"), entry, speaker, "talk.explain", 0,
                1200, 150, DialogueGesture.Explain, companion, DialogueAnimationInterruptionPolicy.BlendOut,
                "idle", "author:test")],
            [new(Guid.Parse("d1300000-0000-4000-8000-000000000012"), entry, speaker, "fr-FR", null,
                "take-01", new("dialogue.demo.entry", "La crypte nous écoute."), "viseme-neutral", "concerned",
                1800, null, null, true, "author:test")],
            [new(Guid.Parse("d1300000-0000-4000-8000-000000000013"), entry,
                DialogueCameraShotMode.Automatic, null, speaker, CameraFraming.Medium, 52, 1800, 250,
                true, true, true, .08f, 10, "author:test")],
            [new(Guid.Parse("d1300000-0000-4000-8000-000000000014"), entry, "Entrée Elyra",
                DialogueBookmarkScope.Shared, "Valider le cadrage et le ton.", "qa-lead",
                $"dialogue://{dialogueId}/{entry}", "author:test")],
            [new(Guid.Parse("d1300000-0000-4000-8000-000000000015"), entry, interjection, companion,
                ["loyal", "wry"], true, 50, 100, 30, "companion-main",
                DialogueInterjectionConflictPolicy.HighestPriority, "author:test")]);
        return new(dialogueId, "elyra-crypt", "Elyra et la crypte", entry,
            [new(entry, speaker, new("dialogue.demo.entry", "La crypte nous écoute."), choices:
                [new(DialogueChoiceId.Parse("d1300000-0000-4000-8000-000000000006"),
                    new("dialogue.demo.choice", "Que veux-tu dire ?"), targetNodeId: interjection)]),
             new(interjection, companion, new("dialogue.demo.interjection", "Et elle n'aime pas les intrus."))],
            profile);
    }
}
