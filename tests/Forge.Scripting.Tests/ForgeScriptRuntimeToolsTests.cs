using Forge.Scripting;

namespace Forge.Scripting.Tests;

public sealed class ForgeScriptRuntimeToolsTests
{
    [Fact]
    public void Audio_supports_cues_fades_snapshots_listener_and_completion()
    {
        var empty = new AudioState(new Dictionary<string, AudioCue>(), "camera", "default", []); var cue = new AudioCue("music:dawn", "music", .8, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), true, "exploration"); var playing = ForgeScriptRuntimeTools.Play(empty, cue); Assert.Equal("exploration", playing.Snapshot); Assert.Contains(cue.Id, playing.Playing); var listening = ForgeScriptRuntimeTools.SetListener(playing, "hero", "combat"); Assert.Equal("hero", listening.ListenerId); var completed = ForgeScriptRuntimeTools.Complete(listening, cue.Id); Assert.Empty(completed.Playing); Assert.Contains("audio-completed:music:dawn", completed.Events);
    }

    [Fact]
    public void World_time_drives_deterministic_calendar_timers_and_schedules()
    {
        var clock = ForgeScriptRuntimeTools.Advance(new WorldClock(DateTimeOffset.UnixEpoch, TimeSpan.Zero, 2, 0), TimeSpan.FromSeconds(5)); Assert.Equal(TimeSpan.FromSeconds(10), clock.SimulationTime); Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(10), ForgeScriptRuntimeTools.CalendarTime(clock)); var due = new ScriptTimer(Guid.Parse("00000000-0000-0000-0000-000000000001"), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3), "OnTick", "daily", false); Assert.Single(ForgeScriptRuntimeTools.Due(clock, [due])); Assert.Equal(TimeSpan.FromSeconds(13), ForgeScriptRuntimeTools.Reschedule(due, clock).DueAt);
    }

    [Fact]
    public void Event_bus_validates_versioned_payload_and_orders_subscribers()
    {
        var tools = new ForgeScriptRuntimeTools(); tools.Register(new("forge.damage", 1, new Dictionary<string, Type> { ["amount"] = typeof(int) })); tools.Subscribe("forge.damage", "z"); tools.Subscribe("forge.damage", "a"); var deliveries = tools.Publish(new("forge.damage", 1, new Dictionary<string, object?> { ["amount"] = 3 }, "server", Guid.NewGuid())); Assert.Equal(["a", "z"], deliveries.Select(item => item.Subscriber)); Assert.Throws<InvalidOperationException>(() => tools.Publish(new("forge.damage", 1, new Dictionary<string, object?>(), "server", Guid.NewGuid())));
    }

    [Fact]
    public void Hot_reload_is_safe_migrated_invalidating_and_restartable()
    {
        var active = new HotReloadModule("module", 1, new Dictionary<string, string> { ["score"] = "int" }, new HashSet<string> { "dialogue" }, "old"); var candidate = active with { Version = 2, SourceHash = "new", Dependencies = new HashSet<string> { "quest" } }; var applied = ForgeScriptRuntimeTools.Reload(active, candidate, true, (_, _) => true); Assert.True(applied.Applied); Assert.Equal(["dialogue", "quest"], applied.Invalidated); var failed = ForgeScriptRuntimeTools.Reload(active, candidate, true, (_, _) => false); Assert.Equal("restart", failed.Strategy);
    }

    [Fact]
    public void Debugger_exposes_stack_locals_watches_events_entities_and_guarded_stepping()
    {
        var frame = new DebugFrame("OnUse", "scripts/use.as", 12, new Dictionary<string, object?> { ["count"] = 2 }); var snapshot = ForgeScriptRuntimeTools.Break("debug:1", "local-author", [frame], new Dictionary<string, object?> { ["hero.hp"] = 10 }, new Dictionary<string, string> { ["hero"] = "entity:1" }, ["OnUse"]); Assert.Equal("paused", snapshot.State); Assert.Equal(2, snapshot.Stack[0].Locals["count"]); Assert.Equal("running", ForgeScriptRuntimeTools.Step(snapshot, "continue").State); Assert.Throws<UnauthorizedAccessException>(() => ForgeScriptRuntimeTools.Break("x", "player", [], new Dictionary<string, object?>(), new Dictionary<string, string>(), []));
    }
}
