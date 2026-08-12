using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Lifecycle;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureInteractionCompositionProfileTests
{
    [Fact]
    public void ValidProfileSelectsContextStatePriorityAndReportsProtectedInterruption()
    {
        var profile = Complete();

        Assert.Empty(CreatureInteractionCompositionValidator.Validate(profile));
        var selected = CreatureInteractionCompositionValidator.RequestDialogue(
            profile, "greeting", "player.hero", "state.hostile");
        var protectedDecision = CreatureInteractionCompositionValidator.RequestDialogue(
            profile, "interrupt", "player.hero", null);

        Assert.True(selected.Accepted);
        Assert.Equal(20, selected.Binding!.Priority);
        Assert.False(protectedDecision.Accepted);
        Assert.Equal("creature.interaction.dialogue_protected", protectedDecision.RefusalCode);
    }

    [Fact]
    public void InvalidFiveSectionsProduceLocalizedStableDiagnostics()
    {
        var source = Complete();
        var invalid = new CreatureInteractionCompositionProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.AssetId, source.PreviewTransform,
            new(source.Factions.Id, Guid.NewGuid(),
                [new(Guid.NewGuid(), Guid.NewGuid(), 2, "bad rank", 101, false)]),
            new(source.Perception.Id,
                [new(Guid.NewGuid(), CreatureInteractionSenseKind.Vision, 0, 0, true, [])], -1),
            [new(Guid.NewGuid(), "bad context", 2001, DialogueId.From(Guid.NewGuid()), "bad actor", null, true)],
            new(source.Protection.Id, true, true, false, ["bad permission"]),
            new(Guid.NewGuid(), 1, "test:r079", "1.0", "wrong.event", "bad-name", EventAuthorityScope.Local),
            source.Overrides);

        var diagnostics = CreatureInteractionCompositionValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == "creature.interaction.factions_invalid" &&
                                             item.Path.Contains("/factions/", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "creature.interaction.perception_invalid" &&
                                             item.Path.Contains("/perception/", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "creature.interaction.dialogue_bindings_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.interaction.protection_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.interaction.on_conversation_invalid");
    }

    [Fact]
    public void DuplicateComponentUuidIsRejected()
    {
        var source = Complete();
        var duplicate = new CreatureInteractionCompositionProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.AssetId, source.PreviewTransform, source.Factions, source.Perception, source.DialogueBindings,
            source.Protection with { Id = source.Perception.Id }, source.OnConversation, source.Overrides);

        Assert.Contains(CreatureInteractionCompositionValidator.Validate(duplicate),
            item => item.Code == "creature.interaction.component_uuid_invalid");
    }

    [Fact]
    public void MissingOptionalProfileIsIgnoredAndRequiredProfileIsDiagnosed()
    {
        var optional = CreatureInteractionCompositionRuntimeResolver.Resolve(null, false);
        var required = CreatureInteractionCompositionRuntimeResolver.Resolve(null, true);

        Assert.True(optional.Succeeded);
        Assert.True(optional.UsedFallback);
        Assert.False(required.Succeeded);
        Assert.Equal(CreatureInteractionCompositionValidator.MissingRequiredCode,
            Assert.Single(required.Diagnostics).Code);
    }

    internal static CreatureInteractionCompositionProfile Complete(
        Guid? primaryFactionId = null,
        DialogueId? dialogueId = null)
    {
        var factionId = primaryFactionId ?? Guid.Parse("a1791000-0000-4000-8000-000000000030");
        var dialogue = dialogueId ?? DialogueId.Parse("a1791000-0000-4000-8000-000000000040");
        return new(Guid.Parse("a1791000-0000-4000-8000-000000000010"), 1, "test:r079",
            "asset.interaction.warden", TransformValue.Identity,
            new(Guid.Parse("a1791000-0000-4000-8000-000000000011"), factionId,
                [new(Guid.Parse("a1791000-0000-4000-8000-000000000012"), factionId, 1, "rank.captain", 35, true)]),
            new(Guid.Parse("a1791000-0000-4000-8000-000000000013"),
                [new(Guid.Parse("a1791000-0000-4000-8000-000000000014"),
                    CreatureInteractionSenseKind.Vision, 30, 120, true, ["stimulus.visual"]),
                 new(Guid.Parse("a1791000-0000-4000-8000-000000000015"),
                    CreatureInteractionSenseKind.Hearing, 20, 360, true, ["stimulus.sound"]),
                 new(Guid.Parse("a1791000-0000-4000-8000-000000000016"),
                    CreatureInteractionSenseKind.Smell, 8, 360, false, ["stimulus.scent"]),
                 new(Guid.Parse("a1791000-0000-4000-8000-000000000017"),
                    CreatureInteractionSenseKind.Magic, 15, 360, false, ["stimulus.magic"])], 12),
            [new(Guid.Parse("a1791000-0000-4000-8000-000000000018"), "greeting", 20, dialogue,
                "player.hero", "state.hostile", false),
             new(Guid.Parse("a1791000-0000-4000-8000-000000000019"), "greeting", 1, dialogue,
                null, null, true),
             new(Guid.Parse("a1791000-0000-4000-8000-000000000020"), "interrupt", 1, dialogue,
                null, null, true)],
            new(Guid.Parse("a1791000-0000-4000-8000-000000000021"), true, true, false,
                ["creature.plot.override", "creature.dialogue.interrupt"]),
            new(Guid.Parse("a1791000-0000-4000-8000-000000000022"), 1, "test:r079", "1.0",
                "creature.interaction_requested", "OnConversation", EventAuthorityScope.Server),
            new Dictionary<string, string> { ["dialogue.selection"] = "context-priority-state-fallback" });
    }
}
