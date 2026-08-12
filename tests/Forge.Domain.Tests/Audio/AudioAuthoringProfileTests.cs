using Forge.Domain.Audio;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Audio;

public sealed class AudioAuthoringProfileTests
{
    [Fact]
    public void Validates_governed_profile_and_optional_required_runtime_contract()
    {
        var resource = "audio/forest-rain.ogg";
        var catalog = Catalog(resource);
        var profile = Profile(resource);

        var diagnostics = AudioAuthoringProfileValidator.Validate(profile, [resource], catalog);
        var optional = AudioAuthoringRuntimeResolver.Resolve(null, false);
        var required = AudioAuthoringRuntimeResolver.Resolve(null, true);

        Assert.Empty(diagnostics);
        Assert.Empty(optional.Diagnostics);
        Assert.False(optional.Available);
        Assert.Equal(AudioAuthoringRuntimeResolver.RequiredMissingCode, Assert.Single(required.Diagnostics).Code);
        Assert.True(AudioAuthoringRuntimeResolver.Resolve(profile, true).Available);
    }

    [Fact]
    public void Rejects_invalid_ranges_unknown_weight_and_unlicensed_playlist_resource()
    {
        var profile = Profile("audio/forest-rain.ogg") with
        {
            Selection = new(2026, -1,
                new Dictionary<string, float> { ["audio/missing.ogg"] = 0 }),
            Modulation = new(10, 2, 2, -2, 1, 1, AudioModulationDistribution.Uniform,
                [new(1, 1), new(0, 0)], 1, true, 500, 0),
            Mix = new(4, string.Empty, 2, 0, 9, AudioMeterMode.Loudness),
            Ambient = new("area", string.Empty, AudioListenerPolicy.Personalized,
                new Dictionary<string, float> { ["player"] = 9 }),
        };

        var diagnostics = AudioAuthoringProfileValidator.Validate(profile,
            ["audio/forest-rain.ogg", "audio/unlicensed.ogg"], Catalog("audio/forest-rain.ogg"));

        Assert.Contains(diagnostics, item => item.Code == AudioAuthoringProfileValidator.SelectionInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AudioAuthoringProfileValidator.SelectionReferenceMissingCode);
        Assert.Contains(diagnostics, item => item.Code == AudioAuthoringProfileValidator.ModulationInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AudioAuthoringProfileValidator.MixInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AudioAuthoringProfileValidator.AmbientInvalidCode);
        Assert.Contains(diagnostics, item => item.Path.EndsWith("playlist/1", StringComparison.Ordinal));
    }

    internal static AudioAuthoringProfile Profile(string resource) => new(
        Guid.Parse("a0610000-0000-4000-8000-000000000001"), 1, "test:r061",
        "assets/audio-manifest.json", "CC0-1.0", new string('a', 64), true,
        new(2026, 2, new Dictionary<string, float> { [resource] = 3 }),
        new(4, 12, -1, 1, .8f, 1.2f, AudioModulationDistribution.Triangular,
            [new(0, 0), new(.5f, .35f), new(1, 1)], 2026, true, 96, 2),
        new(-18, "Ambience", -6, 1, .9f, AudioMeterMode.Loudness),
        new("area-r061", "Ambience", AudioListenerPolicy.Personalized,
            new Dictionary<string, float> { ["player"] = .8f }));

    internal static AudioEmitterCatalog Catalog(string resource)
    {
        var definitionId = Guid.Parse("a0610000-0000-4000-8000-000000000002");
        return new(1,
        [
            new(definitionId, 1, "Forest rain",
                new AudioClipSet(Guid.Parse("a0610000-0000-4000-8000-000000000003"), 1,
                    "Forest rain clips", AudioClipSelectionMode.Weighted,
                    [new(Guid.Parse("a0610000-0000-4000-8000-000000000004"), resource, 3,
                        ["forest", "rain"], "soft", "fr", -21, "audio/forest-rain.preview.ogg",
                        "audio/ogg", 4096, "CC0-1.0", "asset-vault:r061", new string('b', 64),
                        "pluie douce dans les feuilles", [.1f, .4f, .8f, .3f], 48)], "test:r061"),
                new(AudioLoopMode.Loop, 0, 0, 0, .1f, string.Empty),
                new(1, 30, "inverse-square", 360, 0, 0, 0, true), null, 0,
                string.Empty, string.Empty, false, "test:r061", "assets/audio-manifest.json",
                "CC0-1.0", new string('c', 64)),
        ],
        [new(Guid.Parse("a0610000-0000-4000-8000-000000000005"), definitionId,
            TransformValue.Identity, true, "test:r061")]);
    }
}
