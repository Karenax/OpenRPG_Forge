using Forge.Domain.Creatures;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureAuthoringCompositionProfileTests
{
    [Fact]
    public void CompleteProfileNormalizesAndValidatesWizardSpeciesAppearanceAndAnimationGraph()
    {
        var profile = CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r077");

        var diagnostics = CreatureAuthoringCompositionValidator.Validate(profile,
            "blueprints/warden/creatureFoundation/authoringComposition");

        Assert.Empty(diagnostics);
        Assert.Equal(CreatureAuthoringEncounterRole.Defender, profile.EncounterRole);
        Assert.Equal("species.human", profile.Species.SpeciesId);
        Assert.Contains(profile.AnimationGraph.States, item => item.Category == "additive");
        Assert.Equal(["guardian", "melee"], profile.CombatTags);
    }

    [Fact]
    public void InvalidProfileReportsStableLocalizedComponentPaths()
    {
        var source = CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r077");
        var invalid = new CreatureAuthoringCompositionProfile(Guid.Empty, 99, string.Empty, "bad asset",
            source.PreviewTransform, string.Empty, source.RulesetId, "rig.wrong", source.FactionId,
            source.EncounterRole, -1, source.VisualKitId, [],
            source.Species with { ParentSpeciesIds = [source.Species.SpeciesId] },
            source.Appearance,
            source.AnimationGraph with
            {
                States = [new(Guid.Empty, "idle", "locomotion", string.Empty)],
                Transitions = [new(Guid.Empty, "missing", "idle", string.Empty)],
            },
            new Dictionary<string, string> { ["bad key"] = string.Empty });

        var diagnostics = CreatureAuthoringCompositionValidator.Validate(invalid, "profile");

        Assert.Contains(diagnostics, item => item.Code == "creature.authoring.uuid_required");
        Assert.Contains(diagnostics, item => item.Code == "creature.authoring.wizard_invalid" && item.Path == "profile/wizard");
        Assert.Contains(diagnostics, item => item.Code == "creature.authoring.species_invalid" && item.Path == "profile/species");
        Assert.Contains(diagnostics, item => item.Code == "creature.authoring.appearance_invalid" && item.Path == "profile/appearance");
        Assert.Contains(diagnostics, item => item.Code == "creature.authoring.animation_graph_invalid" && item.Path == "profile/animationGraph");
    }

    [Fact]
    public void OptionalAbsenceIsSafeAndRequiredAbsenceIsDiagnosed()
    {
        Assert.True(CreatureAuthoringCompositionRuntimeResolver.Resolve(null, false).Succeeded);
        Assert.True(CreatureAuthoringCompositionRuntimeResolver.Resolve(null, false).UsedFallback);
        Assert.Equal(CreatureAuthoringCompositionValidator.MissingRequiredCode,
            Assert.Single(CreatureAuthoringCompositionRuntimeResolver.Resolve(null, true).Diagnostics).Code);
    }
}
