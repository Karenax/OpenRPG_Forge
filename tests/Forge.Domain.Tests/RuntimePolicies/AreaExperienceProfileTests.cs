using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.RuntimePolicies;

public sealed class AreaExperienceProfileTests
{
    [Fact]
    public void Profile_validates_assets_scatter_voice_patrol_and_loading()
    {
        var area = AreaId.From(Guid.NewGuid());
        var destination = AreaId.From(Guid.NewGuid());
        var portal = PortalId.From(Guid.NewGuid());
        var cue = Cue();
        var profile = new AreaExperienceProfile(area.Value, 1, [cue],
            [new(Guid.NewGuid(), cue.CueId, Vector3Value.Zero, new(20, 4, 20), true, 2026, 2, 6, .8f,
                true, ["forest"], ["indoors"], "test")],
            [Voice()],
            [new CrossAreaPatrolRoute(Guid.NewGuid(), "guard.elyra",
                [new CrossAreaPatrolSegment(Guid.NewGuid(), area, destination, portal, Guid.NewGuid(), true,
                    PatrolOwnershipAuthority.Server)], false, "test")], Loading(area), "test");

        Assert.Equal(area.Value, profile.Id);
        Assert.Single(profile.ScatterEmitters);
        Assert.True(profile.Loading.ShowProgress);
    }

    [Fact]
    public void Unlicensed_resource_and_unconsented_tts_are_rejected()
    {
        var area = AreaId.From(Guid.NewGuid());
        var invalidCue = Cue() with { Resource = "audio/owl.ogg", LicenseSpdx = "", ContentSha256 = "" };
        Assert.Throws<ArgumentOutOfRangeException>(() => new AreaExperienceProfile(area.Value, 1, [invalidCue],
            [], [], [], Loading(area), "test"));
        var voice = Voice() with
        {
            Consent = new VoiceConsentMetadata(true, false, string.Empty, "actor"),
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => new AreaExperienceProfile(area.Value, 1, [], [], [voice],
            [], Loading(area), "test"));
    }

    [Fact]
    public void Looping_patrol_must_return_to_its_first_area()
    {
        var area = AreaId.From(Guid.NewGuid());
        var middle = AreaId.From(Guid.NewGuid());
        var destination = AreaId.From(Guid.NewGuid());
        var route = new CrossAreaPatrolRoute(Guid.NewGuid(), "guard",
            [
                new CrossAreaPatrolSegment(Guid.NewGuid(), area, middle, PortalId.From(Guid.NewGuid()),
                    Guid.NewGuid(), true, PatrolOwnershipAuthority.Server),
                new CrossAreaPatrolSegment(Guid.NewGuid(), middle, destination, PortalId.From(Guid.NewGuid()),
                    Guid.NewGuid(), true, PatrolOwnershipAuthority.Server),
            ], true, "test");

        Assert.Throws<ArgumentOutOfRangeException>(() => new AreaExperienceProfile(area.Value, 1, [], [], [],
            [route], Loading(area), "test"));
    }

    internal static AudioCueDefinition Cue() => new(Guid.NewGuid(), "ambient.owl", string.Empty, "Ambience", .8f,
        false, string.Empty, string.Empty, "1.0", "test");

    internal static CreatureVoiceProfile Voice() => new(Guid.NewGuid(), BlueprintId.From(Guid.NewGuid()), "fr",
        [new LocalizedVoiceTake("fr", string.Empty, "La route est sûre.", string.Empty, string.Empty, string.Empty)],
        "tts.local", new VoiceConsentMetadata(true, true, "consent:test", "voix de test"), "test");

    internal static AreaLoadingPresentation Loading(AreaId area) => new(Guid.NewGuid(), "Chargement",
        "loading.png", "loading.ogv", new Dictionary<string, IReadOnlyList<string>>
        {
            ["fr"] = ["Explorez chaque détour."],
            ["en"] = ["Explore every path."],
        }, true, "Chargement en cours.", true, 300, "test");
}
