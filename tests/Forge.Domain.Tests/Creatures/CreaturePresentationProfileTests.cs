using Forge.Domain.Creatures;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreaturePresentationProfileTests
{
    [Fact]
    public void CompleteProfile_ValidatesModularPortraitMaterialAttachmentAndVoiceData()
    {
        var profile = Complete();

        var diagnostics = CreaturePresentationValidator.Validate(profile);

        Assert.Empty(diagnostics);
        Assert.Equal("nonbinary", profile.GenderId);
        Assert.Equal(2, profile.Portraits.Count);
        Assert.Contains(profile.Portraits, item => item.Expression == "angry" && item.Width == 512);
        Assert.Equal("#7A5CFF", profile.MaterialColors.Single().HexColor);
        Assert.Equal("fr-FR", profile.Voice!.Language);
        Assert.False(CreaturePresentationRuntimeResolver.Resolve(profile, required: true).UseModelFallback);
    }

    [Fact]
    public void InvalidProfile_ReportsStableLocalizedDiagnostics()
    {
        var invalid = new CreaturePresentationProfile(
            Guid.Empty, 99, "", "?", "", new("", "", "", ""), [], false, "",
            [new("head", "", "wrong-rig", -1, [], "")],
            [new("primary", "orange")],
            [new("wing", "", "", "wrong-rig")],
            new("", "", [], [], null, [], "", ""));

        var diagnostics = CreaturePresentationValidator.Validate(invalid, "blueprints/test/creaturePresentation");

        Assert.Contains(diagnostics, item => item.Code == "creature.presentation.uuid_required");
        Assert.Contains(diagnostics, item => item.Code == "creature.presentation.portrait_or_fallback_required");
        Assert.Contains(diagnostics, item => item.Code == "creature.presentation.body_slot_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.presentation.material_color_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.presentation.attachment_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.presentation.voice_invalid");
        Assert.All(diagnostics, item => Assert.StartsWith("blueprints/test/creaturePresentation", item.Path));
    }

    [Fact]
    public void RuntimeResolver_IgnoresOptionalAbsenceAndDiagnosesRequiredAbsence()
    {
        var optional = CreaturePresentationRuntimeResolver.Resolve(null, required: false);
        var required = CreaturePresentationRuntimeResolver.Resolve(null, required: true);

        Assert.True(optional.Succeeded);
        Assert.True(optional.UseModelFallback);
        Assert.False(required.Succeeded);
        Assert.Equal("creature.presentation.required_missing", Assert.Single(required.Diagnostics).Code);
    }

    private static CreaturePresentationProfile Complete() => new(
        Guid.Parse("a1300000-0000-4000-8000-000000000001"),
        1,
        "author:test",
        "NonBinary",
        "Humanoid-Medium",
        new("iel", "iel", "son", "soi"),
        [
            new("neutral", "assets/portraits/warden-256.png", 256, 256, "CC0-1.0", new string('a', 64), "Portrait du gardien"),
            new("angry", "assets/portraits/warden-angry-512.png", 512, 512, "CC0-1.0", new string('b', 64), "Gardien en colère"),
        ],
        false,
        "rig.humanoid",
        [new("head", "asset.helmet", "rig.humanoid", 4, ["hair"], "hide-covered")],
        [new("primary", "#7A5CFF")],
        [new("back", "wings", "asset.wings", "rig.humanoid")],
        new("voice.warden", "fr-FR", ["audio.warden.core"], ["calm", "angry"], "lipsync.fr-v1", ["a", "b"], "CC0-1.0", "author-recorded"));
}
