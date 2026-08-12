using Forge.RpgKit.Application.Scripting;
using Forge.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ForgeScriptRuntimeToolAdapterTests
{
    [Fact]
    public void Optional_missing_is_safe_required_missing_is_diagnostic_and_stale_handles_fail()
    {
        var adapter = new ForgeScriptRuntimeToolAdapter();
        var document = Document();

        Assert.True(adapter.Resolve(null, false).OptionalFallback);
        Assert.Equal(ForgeScriptRuntimeToolAdapter.RequiredMissingCode,
            adapter.Resolve(null, true).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(new(Guid.NewGuid()), document,
            Audio(), Clock(), Module()));
    }

    [Fact]
    public void Audio_time_and_timers_share_one_immutable_runtime_projection()
    {
        var (adapter, handle, state) = Runtime();
        state = adapter.Play(handle, state, new("music:dawn", "music", .8,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), true, "exploration"));
        state = adapter.SetListener(handle, state, "hero", "combat", "player");
        state = adapter.Schedule(handle, state, Guid.Parse("c7990000-0000-4000-8000-000000000020"),
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3), "OnTick", "daily");
        state = adapter.Advance(handle, state, TimeSpan.FromSeconds(5));

        Assert.Contains("music:dawn", state.Audio.Playing);
        Assert.Equal("player", state.Audio.ListenerPolicy);
        Assert.Single(ForgeScriptRuntimeTools.Due(state.Clock, state.Timers));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(10), ForgeScriptRuntimeTools.CalendarTime(state.Clock));
    }

    [Fact]
    public void Custom_events_and_hot_reload_are_schema_bound_and_rollback_safe()
    {
        var (adapter, handle, state) = Runtime();
        var target = Guid.Parse("c7990000-0000-4000-8000-000000000021");
        var deliveries = adapter.Publish(handle, state,
            new("forge.damage", 1,
                new Dictionary<string, Type> { ["amount"] = typeof(int), ["target"] = typeof(Guid) }),
            ["quest", "audio"], new("forge.damage", 1,
                new Dictionary<string, object?> { ["amount"] = 4, ["target"] = target },
                "server", Guid.Parse("c7990000-0000-4000-8000-000000000022")));
        var candidate = state.ActiveModule with
        {
            Version = 2,
            SourceHash = "new",
            Dependencies = new HashSet<string> { "quest" },
        };
        var rejected = adapter.Reload(handle, state, candidate, true, (_, _) => false);
        var applied = adapter.Reload(handle, state, candidate, true, (_, _) => true);

        Assert.Equal(["audio", "quest"], deliveries.Select(item => item.Subscriber));
        Assert.Same(state, rejected.State);
        Assert.Equal("restart", rejected.Result.Strategy);
        Assert.True(applied.Result.Applied);
        Assert.Equal(2, applied.State.ActiveModule.Version);
    }

    [Fact]
    public void Remote_debugger_requires_admin_and_exposes_breakpoints_stack_watches_and_entities()
    {
        var (adapter, handle, state) = Runtime();
        var frame = new DebugFrame("OnUse", "scripts/use.as", 12,
            new Dictionary<string, object?> { ["count"] = 2 });
        state = adapter.Break(handle, state, "debug:r099", "server-admin", [frame],
            new Dictionary<string, object?> { ["hero.hp"] = 10 },
            new Dictionary<string, string> { ["hero"] = "entity:1" }, ["OnUse"],
            new HashSet<string> { "scripts/use.as:12" }, true);
        state = adapter.Step(handle, state, "over");

        Assert.NotNull(state.Debugger);
        Assert.True(state.Debugger.Remote);
        Assert.Contains("scripts/use.as:12", state.Debugger.Breakpoints);
        Assert.Equal("paused", state.Debugger.State);
        Assert.Equal("entity:1", ForgeScriptRuntimeTools.InspectEntity(state.Debugger, "hero"));
    }

    [Fact]
    public void Versioned_native_runtime_tool_apis_execute_in_the_same_bounded_sandbox()
    {
        var (adapter, handle, state) = Runtime();

        var result = adapter.Execute(handle, state, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));

        Assert.Equal(ForgeScriptExecutionStatus.Completed, result.Status);
        Assert.Contains(result.Events, item => item.Kind == "audio.started");
        Assert.Contains(result.Events, item => item.Kind == "time.timer.scheduled");
        Assert.Contains(result.Events, item => item.Kind == "event.published");
        Assert.Contains(result.Events, item => item.Kind == "hot-reload.requested");
        Assert.Contains(result.Events, item => item.Kind == "debug.step");
    }

    private static (ForgeScriptRuntimeToolAdapter Adapter, ForgeScriptRuntimeToolHandle Handle,
        ForgeScriptRuntimeToolState State) Runtime()
    {
        var adapter = new ForgeScriptRuntimeToolAdapter();
        var document = Document();
        var handle = adapter.Resolve(document, true).Handle!;
        return (adapter, handle, adapter.Build(handle, document, Audio(), Clock(), Module()));
    }

    private static ForgeScriptSourceDocument Document() =>
        new(Guid.Parse("c7990000-0000-4000-8000-000000000001"), 1, "test:r099",
            "scripts/r099.as", """
            void main()
            {
                EntityRef hero = spawn("hero");
                AudioEmitterRef music = audio_emitter("music:dawn");
                audio_start(music);
                audio_snapshot("combat");
                audio_listener(hero, "player");
                WorldClockRef clock = world_clock();
                TimerRef timer = timer_schedule(1000, 5000, "OnTick", "daily");
                event_subscribe("forge.damage", 1, "OnDamage");
                CustomEventRef evt = custom_event("forge.damage", 1, "amount=4");
                event_publish(evt);
                HotReloadRef reload = hot_reload_request("module", 2, "sha256:new");
                DebugSessionRef debug = debug_break("session:1", "local-author");
                debug_step(debug, "over");
            }
            """, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);

    private static AudioState Audio() =>
        new(new Dictionary<string, AudioCue>(), "camera", "default", []);

    private static WorldClock Clock() =>
        new(DateTimeOffset.UnixEpoch, TimeSpan.Zero, 2, 0);

    private static HotReloadModule Module() =>
        new("module", 1, new Dictionary<string, string> { ["score"] = "int" },
            new HashSet<string> { "dialogue" }, "old");
}
