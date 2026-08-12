using Forge.Scripting;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptRuntimeToolsTests
{
    [Fact]
    public void Audio_supports_cues_fades_snapshots_listener_policies_completion_and_revision()
    {
        var empty = new AudioState(new Dictionary<string, AudioCue>(), "camera", "default", []);
        var cue = new AudioCue("music:dawn", "music", .8, TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2), true, "exploration");

        var playing = ForgeScriptRuntimeTools.Play(empty, cue);
        var listening = ForgeScriptRuntimeTools.SetListener(playing, "hero", "combat", "player");
        var completed = ForgeScriptRuntimeTools.Complete(listening, cue.Id);

        Assert.Equal("exploration", playing.Snapshot);
        Assert.Contains(cue.Id, playing.Playing);
        Assert.Equal("hero", listening.ListenerId);
        Assert.Equal("player", listening.ListenerPolicy);
        Assert.Empty(completed.Playing);
        Assert.Equal(3, completed.Revision);
        Assert.Contains("audio-completed:music:dawn", completed.Events);
    }

    [Fact]
    public void Audio_rejects_invalid_cues_listener_policies_handles_and_fades()
    {
        var empty = new AudioState(new Dictionary<string, AudioCue>(), "camera", "default", []);
        Assert.Throws<InvalidOperationException>(() => ForgeScriptRuntimeTools.Play(empty,
            new("", "music", 2, TimeSpan.Zero, TimeSpan.Zero, false, "default")));
        Assert.Throws<InvalidOperationException>(() => ForgeScriptRuntimeTools.SetListener(
            empty, "hero", "default", "unknown"));
        Assert.Throws<InvalidOperationException>(() => ForgeScriptRuntimeTools.Control(
            new(new(Guid.Empty, 0), "stopped", 1, []), "start"));
    }

    [Fact]
    public void Audio_emitter_handle_supports_start_pause_resume_fade_and_stop()
    {
        var source = new ScriptAudioEmitterState(
            new(Guid.Parse("a0620000-0000-4000-8000-000000000001"), 1), "stopped", 1, []);
        var started = ForgeScriptRuntimeTools.Control(source, "start");
        var paused = ForgeScriptRuntimeTools.Control(started, "pause");
        var resumed = ForgeScriptRuntimeTools.Control(paused, "resume");
        var faded = ForgeScriptRuntimeTools.Control(resumed, "fade", .25, 2);
        var stopped = ForgeScriptRuntimeTools.Control(faded, "stop");

        Assert.Equal("paused", paused.Status);
        Assert.Equal("playing", resumed.Status);
        Assert.Equal(.25, faded.Gain);
        Assert.Equal("stopped", stopped.Status);
        Assert.Equal(5, stopped.Revision);
        Assert.Contains("audio.paused", stopped.Events);
        Assert.Contains("audio.faded:0.25:2", stopped.Events);
    }

    [Fact]
    public void World_time_drives_deterministic_calendar_timers_schedules_and_cancellation()
    {
        var start = new WorldClock(DateTimeOffset.UnixEpoch, TimeSpan.Zero, 2, 0);
        var timer = ForgeScriptRuntimeTools.Schedule(start,
            Guid.Parse("00000000-0000-0000-0000-000000000001"), TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3), "OnTick", "daily");
        var clock = ForgeScriptRuntimeTools.Advance(start, TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(10), clock.SimulationTime);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(10), ForgeScriptRuntimeTools.CalendarTime(clock));
        Assert.Single(ForgeScriptRuntimeTools.Due(clock, [timer]));
        Assert.Equal(TimeSpan.FromSeconds(13), ForgeScriptRuntimeTools.Reschedule(timer, clock).DueAt);
        Assert.True(ForgeScriptRuntimeTools.Cancel(timer).Cancelled);
    }

    [Fact]
    public void World_time_rejects_invalid_scale_timer_identity_and_duplicate_ids()
    {
        var clock = new WorldClock(DateTimeOffset.UnixEpoch, TimeSpan.Zero, 1, 0);
        Assert.Throws<InvalidOperationException>(() => ForgeScriptRuntimeTools.Advance(
            clock with { Scale = double.PositiveInfinity }, TimeSpan.Zero));
        Assert.Throws<InvalidOperationException>(() => ForgeScriptRuntimeTools.Schedule(
            clock, Guid.Empty, TimeSpan.Zero, null, "OnTick", "daily"));
        var timer = ForgeScriptRuntimeTools.Schedule(clock, Guid.NewGuid(), TimeSpan.Zero, null,
            "OnTick", "once");
        Assert.Throws<InvalidOperationException>(() => ForgeScriptRuntimeTools.Due(clock, [timer, timer]));
    }

    [Fact]
    public void Event_bus_validates_namespaced_versioned_payload_and_orders_subscribers()
    {
        var tools = new ForgeScriptRuntimeTools();
        tools.Register(new("forge.damage", 1,
            new Dictionary<string, Type> { ["amount"] = typeof(int), ["target"] = typeof(Guid) }));
        tools.Subscribe("forge.damage", "z");
        tools.Subscribe("forge.damage", "a");
        var deliveries = tools.Publish(new("forge.damage", 1,
            new Dictionary<string, object?> { ["amount"] = 3, ["target"] = Guid.NewGuid() },
            "server", Guid.NewGuid()));

        Assert.Equal(["a", "z"], deliveries.Select(item => item.Subscriber));
        Assert.All(deliveries, item => Assert.Contains("forge.damage@1", item.Trace));
        Assert.Throws<InvalidOperationException>(() => tools.Publish(new("forge.damage", 1,
            new Dictionary<string, object?> { ["amount"] = 3 }, "server", Guid.NewGuid())));
    }

    [Fact]
    public void Event_bus_rejects_unscoped_unknown_conflicting_and_unauthorized_events()
    {
        var tools = new ForgeScriptRuntimeTools();
        Assert.Throws<InvalidOperationException>(() => tools.Register(new("damage", 1,
            new Dictionary<string, Type> { ["amount"] = typeof(int) })));
        tools.Register(new("forge.damage", 1,
            new Dictionary<string, Type> { ["amount"] = typeof(int) }));
        Assert.Throws<InvalidOperationException>(() => tools.Publish(new("forge.damage", 1,
            new Dictionary<string, object?> { ["amount"] = 1 }, "client", Guid.NewGuid())));
    }

    [Fact]
    public void Hot_reload_is_boundary_safe_migrated_invalidating_and_restartable()
    {
        var active = new HotReloadModule("module", 1,
            new Dictionary<string, string> { ["score"] = "int" },
            new HashSet<string> { "dialogue" }, "old");
        var candidate = active with
        {
            Version = 2,
            SourceHash = "new",
            Dependencies = new HashSet<string> { "quest" },
        };

        var deferred = ForgeScriptRuntimeTools.Reload(active, candidate, false, (_, _) => true);
        var applied = ForgeScriptRuntimeTools.Reload(active, candidate, true, (_, _) => true);
        var failed = ForgeScriptRuntimeTools.Reload(active, candidate, true, (_, _) => false);

        Assert.Equal("deferred", deferred.Strategy);
        Assert.True(applied.Applied);
        Assert.Equal(["dialogue", "quest"], applied.Invalidated);
        Assert.Equal("restart", failed.Strategy);
        Assert.Same(active, failed.Active);
    }

    [Fact]
    public void Debugger_exposes_breakpoints_stack_locals_watches_events_entities_and_guarded_stepping()
    {
        var frame = new DebugFrame("OnUse", "scripts/use.as", 12,
            new Dictionary<string, object?> { ["count"] = 2 });
        var snapshot = ForgeScriptRuntimeTools.Break("debug:1", "server-admin", [frame],
            new Dictionary<string, object?> { ["hero.hp"] = 10 },
            new Dictionary<string, string> { ["hero"] = "entity:1" }, ["OnUse"],
            new HashSet<string> { "scripts/use.as:12" }, remote: true);
        var watched = ForgeScriptRuntimeTools.SetWatch(snapshot, "hero.mana", 5);

        Assert.Equal("paused", snapshot.State);
        Assert.Equal(2, snapshot.Stack[0].Locals["count"]);
        Assert.Contains("scripts/use.as:12", snapshot.Breakpoints);
        Assert.True(snapshot.Remote);
        Assert.Equal("entity:1", ForgeScriptRuntimeTools.InspectEntity(snapshot, "hero"));
        Assert.Equal(5, watched.Watches["hero.mana"]);
        Assert.Equal("running", ForgeScriptRuntimeTools.Step(snapshot, "continue").State);
        Assert.Throws<UnauthorizedAccessException>(() => ForgeScriptRuntimeTools.Break(
            "x", "player", [], new Dictionary<string, object?>(), new Dictionary<string, string>(), []));
    }
}
