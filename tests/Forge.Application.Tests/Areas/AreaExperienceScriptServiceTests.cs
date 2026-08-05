using Forge.Application.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class AreaExperienceScriptServiceTests
{
    [Fact]
    public void Read_model_is_copied_and_runtime_mutations_emit_typed_events()
    {
        var service = new AreaExperienceScriptService(AreaExperienceRuntimeServiceTests.Profile());
        var snapshot = service.Resolve(service.Handle);
        var ids = Enumerable.Range(1, 6).Select(value => new Guid(value, 0, 0, new byte[8])).GetEnumerator();
        Guid NextId() { ids.MoveNext(); return ids.Current; }

        var playback = service.PlayCue("ambient.owl", DateTimeOffset.UnixEpoch, NextId);
        service.SetMusicState("combat", DateTimeOffset.UnixEpoch, NextId);
        service.PushSnapshot("underwater", DateTimeOffset.UnixEpoch, NextId);
        service.InterruptCue(playback.PlaybackId, "area-transition", DateTimeOffset.UnixEpoch, NextId);

        Assert.Equal(new[] { "ambient.owl" }, snapshot.Values["audioCueIds"]);
        Assert.Contains(service.State.Events, item => item is AudioCueStarted);
        Assert.Contains(service.State.Events, item => item is MusicStateChanged);
        Assert.Contains(service.State.Events, item => item is AudioSnapshotPushed);
        Assert.Contains(service.State.Events, item => item is AudioCueInterrupted);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, object?>)snapshot.Values).Add("forbidden", true));
    }

    [Fact]
    public void Unknown_handle_is_rejected()
    {
        var service = new AreaExperienceScriptService(AreaExperienceRuntimeServiceTests.Profile());
        Assert.Throws<KeyNotFoundException>(() =>
            service.Resolve(new AreaExperienceScriptHandle(Guid.NewGuid(), 1)));
    }
}
