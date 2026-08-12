using Forge.Application.Areas;
using Forge.Domain.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class AudioOrchestrationRuntimeServiceTests
{
    [Fact]
    public void Previews_debug_ranges_and_controls_safe_handle_with_authority_events()
    {
        var spatial = new AudioSpatialProfile(AreaSoundMode.Spatial3D, 2, 20, .1f,
            "inverse-square", true);
        var preview = AudioOrchestrationRuntimeService.PreviewAttenuation(spatial, 9);
        var control = new AudioRuntimeControl(false, AudioInstancePolicy.Restart, AudioAuthority.Server,
            1, "audio.started", "audio.stopped");
        var state = AudioOrchestrationRuntimeService.Create(
            Guid.Parse("a0621000-0000-4000-8000-000000000001"));
        var denied = AudioOrchestrationRuntimeService.Start(state, control, AudioAuthority.Local);
        var started = AudioOrchestrationRuntimeService.Start(state, control, AudioAuthority.Server);
        var paused = AudioOrchestrationRuntimeService.Pause(started, control, AudioAuthority.Server);
        var faded = AudioOrchestrationRuntimeService.Fade(paused, control, AudioAuthority.Server, .2f, 2);
        var stopped = AudioOrchestrationRuntimeService.Stop(faded, control, AudioAuthority.Server);

        Assert.Equal(["inner", "outer"], preview.DebugSpheres.Select(item => item.Kind));
        Assert.Equal(1, preview.Samples[0].Gain);
        Assert.Equal(0, preview.Samples[^1].Gain);
        Assert.Equal(AudioOrchestrationRuntimeService.AuthorityDeniedCode,
            Assert.Single(denied.Diagnostics).Code);
        Assert.Equal(AudioPlaybackStatus.Paused, paused.Status);
        Assert.Contains("audio.faded:0.2:2", stopped.Events);
        Assert.Equal(AudioPlaybackStatus.Stopped, stopped.Status);
    }

    [Fact]
    public void Plans_context_crossfade_and_monitors_hierarchical_platform_mix()
    {
        var masterId = Guid.Parse("a0622000-0000-4000-8000-000000000001");
        var ambienceId = Guid.Parse("a0622000-0000-4000-8000-000000000002");
        var layers = new[]
        {
            new AdaptiveAmbienceLayer("day.ogg", 6, 18, "", "forest", 0, false, .8f, 2),
            new AdaptiveAmbienceLayer("night-rain.ogg", 18, 6, "rain", "forest", .2f, false, 1, 3,
                "forest-volume"),
        };
        var orchestration = new AreaAudioOrchestration([], [
            new(masterId, "Master", null, .8f, "default", "all", .8f, true, true),
            new(ambienceId, "Ambience", masterId, .5f, "rain", "windows", .6f, true, true),
        ], []);
        var profile = new AreaAudioProfile("", "", "", "", "forest", 1, [],
            adaptiveLayers: layers, orchestration: orchestration);
        var day = new AudioListenerContext(0, 12, "", new HashSet<string> { "forest" }, 0, false);
        var night = new AudioListenerContext(0, 22, "rain",
            new HashSet<string> { "forest", "forest-volume" }, .5f, false);

        var fade = AudioOrchestrationRuntimeService.PlanAdaptiveCrossfade(profile, day, night);
        var meters = AudioOrchestrationRuntimeService.MonitorMix(orchestration,
            new Dictionary<string, (float, float, float)>
            {
                ["Master"] = (-3, -9, -14),
                ["Ambience"] = (-6, -12, -20),
            }, "windows", "rain");

        Assert.Equal(2, fade.Entries.Count);
        Assert.Contains(fade.Entries, item => item.Resource == "day.ogg" && item.ToGain == 0);
        Assert.Contains(fade.Entries, item => item.Resource == "night-rain.ogg" && item.FromGain == 0 &&
                                            item.ToGain == 1 && item.CrossfadeSeconds == 3);
        Assert.Equal(2, meters.Count);
        Assert.Equal(.4f, meters.Single(item => item.Bus == "Ambience").EffectiveGain, 3);
        Assert.All(meters, item => Assert.True(item.AccessibilityEnhanced));
    }
}
