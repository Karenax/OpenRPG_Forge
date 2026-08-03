using Forge.Application.Areas;
using Forge.Domain.Areas;
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
}
