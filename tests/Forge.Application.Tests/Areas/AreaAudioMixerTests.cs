using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
namespace Forge.Application.Tests.Areas;

public sealed class AreaAudioMixerTests
{
    [Fact]
    public void Evaluates_global_and_spatial_attenuation_script_policy_and_adaptive_layers()
    {
        var layer = new AdaptiveAmbienceLayer("night-rain.ogg", 20, 6, "rain", "forest", .2f, false, .8f);
        var profile = new AreaAudioProfile("", "", "", "", "forest", 1, [], spatial: new(AreaSoundMode.Spatial3D, 2, 12, .1f, "linear"),
            runtimeControl: new(false, AudioInstancePolicy.Overlap, AudioAuthority.Server, 2, "audio.started", "audio.stopped"), adaptiveLayers: [layer]);
        var near = AreaAudioMixer.Evaluate(profile, new(1, 22, "rain", new HashSet<string> { "forest" }, .5f, false));
        var far = AreaAudioMixer.Evaluate(profile, new(12, 22, "rain", new HashSet<string> { "forest" }, .5f, false));
        Assert.Equal(.9f, near.Attenuation); Assert.Equal(0, far.Attenuation); Assert.Equal(["night-rain.ogg"], near.ActiveLayers);
        Assert.Equal(AudioAuthority.Server, near.Authority); Assert.Equal(AudioInstancePolicy.Overlap, near.InstancePolicy);
    }

    [Fact]
    public void Normalizes_loudness_routes_bus_caps_gain_and_personalizes_global_listener()
    {
        var authoring = new AudioAuthoringProfile(Guid.Parse("a0612000-0000-4000-8000-000000000001"),
            1, "test:r061", "assets/audio.json", "CC0-1.0", new string('a', 64), false,
            new(1, 1, new Dictionary<string, float>()),
            new(0, 0, 0, 0, 1, 1, AudioModulationDistribution.Uniform,
                [new(0, 0), new(1, 1)], 1, false, 120, 1),
            new(-18, "Ambient-2D", -8, 1, .75f, AudioMeterMode.Loudness),
            new("area-a", "Ambient-2D", AudioListenerPolicy.Personalized,
                new Dictionary<string, float> { ["player-a"] = .5f }));
        var profile = new AreaAudioProfile("", "", "", "", "forest", 1, [],
            relativeVolume: 2, authoring: authoring);

        var active = AreaAudioMixer.Evaluate(profile, new(0, 12, "", new HashSet<string>(), 0,
            false, "area-a", "player-a", SourceLoudnessLufs: -30));
        var outside = AreaAudioMixer.Evaluate(profile, new(0, 12, "", new HashSet<string>(), 0,
            false, "area-b", "player-a", SourceLoudnessLufs: -30));
        var unknown = AreaAudioMixer.Evaluate(profile, new(0, 12, "", new HashSet<string>(), 0,
            false, "area-a", "player-b", SourceLoudnessLufs: -30));

        Assert.Equal("Ambient-2D", active.Bus);
        Assert.Equal(-8, active.DuckingDb);
        Assert.True(active.Personalized);
        Assert.True(active.AccessibilityCapped);
        Assert.Equal(.75f, active.NormalizedGain);
        Assert.True(active.ListenerAllowed);
        Assert.Equal(0, outside.Attenuation);
        Assert.False(unknown.ListenerAllowed);
    }
}
