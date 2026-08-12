using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureInteractionCompositionFoundationServiceTests
{
    [Fact]
    public void CanonicalInteractionAdaptsToRpgKitDetectionDialogueAndProtection()
    {
        var creatureId = BlueprintId.From(Guid.Parse("a1792000-0000-4000-8000-000000000001"));
        var itemId = BlueprintId.From(Guid.Parse("a1792000-0000-4000-8000-000000000002"));
        var interaction = CompleteInteraction();
        var project = Project(creatureId, itemId, CompleteFoundation(itemId, interaction));

        var result = CreatureInteractionCompositionFoundationService.Resolve(project, creatureId);
        Assert.True(result.Succeeded);
        var creature = Assert.IsType<CreatureDefinition>(result.Creature);
        var perception = Assert.IsType<CreaturePerceptionProfile>(creature.Perception);
        var protection = Assert.IsType<CreatureProtectionPolicy>(result.Protection);
        var detected = CreatureRulesService.Detect(perception, Vector3Value.Zero,
            [new("torch", "stimulus.visual", new Vector3Value(3, 0, 0), 1)], 5);
        var decision = CreatureInteractionService.RequestDialogue(creatureId.Value, "player.hero", "greeting",
            "state.hostile", result.DialogueBindings, protection, Guid.NewGuid);

        Assert.Equal(interaction.Factions.PrimaryFactionId.ToString("D"),
            creature.Factions!.PrimaryFactionId);
        Assert.Equal(4, perception.Senses.Count);
        Assert.Equal("torch", Assert.Single(detected).StimulusId);
        Assert.True(decision.Accepted);
        Assert.Equal("OnConversation", result.OnConversation!.Handler);
        Assert.True(protection.Invulnerable);
    }

    [Fact]
    public void MissingOptionalInteractionIsIgnoredAndRequiredInteractionIsDiagnosed()
    {
        var creatureId = BlueprintId.From(Guid.NewGuid());
        var itemId = BlueprintId.From(Guid.NewGuid());
        var source = CompleteFoundation(itemId, null);
        var optional = CreatureInteractionCompositionFoundationService.Resolve(Project(creatureId, itemId, source),
            creatureId);
        Assert.True(optional.Succeeded);
        Assert.True(optional.UsedFallback);

        var required = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides, source.AuthoringComposition,
            source.AuthoringCompositionRequiredAtRuntime, source.RulesComposition,
            source.RulesCompositionRequiredAtRuntime, null, true);
        var result = CreatureInteractionCompositionFoundationService.Resolve(Project(creatureId, itemId, required),
            creatureId);
        Assert.False(result.Succeeded);
        Assert.Equal(CreatureInteractionCompositionValidator.MissingRequiredCode,
            Assert.Single(result.Diagnostics).Code);
    }

    private static CreatureFoundationProfile CompleteFoundation(
        BlueprintId itemId,
        CreatureInteractionCompositionProfile? interaction)
    {
        var source = CreatureFoundationProfile.CreateDefault(Guid.NewGuid(), "warden", "Gardien", "test:r079");
        return new(source.Id, source.SchemaVersion, source.Provenance, true, source.Identity, source.DefaultTransform,
            source.Resources, source.Movement, source.References, source.Spawned, source.Overrides,
            CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "test:r079"), true,
            CreatureRulesCompositionFoundationServiceTests.CompleteRules(itemId), true, interaction,
            interaction is not null);
    }

    private static CreatureInteractionCompositionProfile CompleteInteraction() => new(
        Guid.Parse("a1792000-0000-4000-8000-000000000010"), 1, "test:r079", "asset.interaction.warden",
        TransformValue.Identity,
        new(Guid.Parse("a1792000-0000-4000-8000-000000000011"),
            Guid.Parse("a1792000-0000-4000-8000-000000000030"),
            [new(Guid.Parse("a1792000-0000-4000-8000-000000000012"),
                Guid.Parse("a1792000-0000-4000-8000-000000000030"), 1, "rank.captain", 35, true)]),
        new(Guid.Parse("a1792000-0000-4000-8000-000000000013"),
            [new(Guid.Parse("a1792000-0000-4000-8000-000000000014"), CreatureInteractionSenseKind.Vision,
                30, 120, true, ["stimulus.visual"]),
             new(Guid.Parse("a1792000-0000-4000-8000-000000000015"), CreatureInteractionSenseKind.Hearing,
                20, 360, true, ["stimulus.sound"]),
             new(Guid.Parse("a1792000-0000-4000-8000-000000000016"), CreatureInteractionSenseKind.Smell,
                8, 360, false, ["stimulus.scent"]),
             new(Guid.Parse("a1792000-0000-4000-8000-000000000017"), CreatureInteractionSenseKind.Magic,
                15, 360, false, ["stimulus.magic"])], 12),
        [new(Guid.Parse("a1792000-0000-4000-8000-000000000018"), "greeting", 20,
            DialogueId.Parse("a1792000-0000-4000-8000-000000000040"), "player.hero", "state.hostile", false)],
        new(Guid.Parse("a1792000-0000-4000-8000-000000000021"), true, true, false,
            ["creature.plot.override"]),
        new(Guid.Parse("a1792000-0000-4000-8000-000000000022"), 1, "test:r079", "1.0",
            "creature.interaction_requested", "OnConversation", Forge.Domain.Lifecycle.EventAuthorityScope.Server),
        new Dictionary<string, string>());

    private static ProjectDefinition Project(BlueprintId creatureId, BlueprintId itemId,
        CreatureFoundationProfile foundation) => new(ProjectId.From(Guid.NewGuid()), "R079",
        [new BlueprintDefinition(creatureId, "warden", "Gardien", BlueprintKind.Creature,
             creatureFoundation: foundation),
         new BlueprintDefinition(itemId, "ember-blade", "Lame", BlueprintKind.Item)],
        [new AreaDefinition(AreaId.From(Guid.NewGuid()), "lab", "Lab", [])]);
}
