namespace Forge.Scripting;

public sealed record AudioCue(string Id, string Bus, double Volume, TimeSpan FadeIn, TimeSpan FadeOut, bool Loop, string Snapshot);
public sealed record AudioState(IReadOnlyDictionary<string, AudioCue> Playing, string ListenerId, string Snapshot, IReadOnlyList<string> Events);
public sealed record WorldClock(DateTimeOffset Epoch, TimeSpan SimulationTime, double Scale, long Revision);
public sealed record ScriptTimer(Guid Id, TimeSpan DueAt, TimeSpan? Repeat, string Handler, string Schedule, bool Cancelled);
public sealed record CustomEventSchema(string Name, int Version, IReadOnlyDictionary<string, Type> Fields);
public sealed record CustomEvent(string Name, int Version, IReadOnlyDictionary<string, object?> Payload, string Authority, Guid CorrelationId);
public sealed record EventDelivery(string Subscriber, CustomEvent Event, string Trace);
public sealed record HotReloadModule(string Id, int Version, IReadOnlyDictionary<string, string> StateSchema, IReadOnlySet<string> Dependencies, string SourceHash);
public sealed record HotReloadResult(bool Applied, HotReloadModule Active, IReadOnlyList<string> Invalidated, string Strategy, string Diagnostic);
public sealed record DebugFrame(string Function, string Source, int Line, IReadOnlyDictionary<string, object?> Locals);
public sealed record DebugSnapshot(string SessionId, string Authority, string State, IReadOnlyList<DebugFrame> Stack, IReadOnlyDictionary<string, object?> Watches, IReadOnlyDictionary<string, string> Entities, IReadOnlyList<string> Events);

public sealed class ForgeScriptRuntimeTools
{
    private readonly Dictionary<string, CustomEventSchema> _schemas = [];
    private readonly Dictionary<string, List<string>> _subscriptions = [];

    public static AudioState Play(AudioState state, AudioCue cue) { if (string.IsNullOrWhiteSpace(cue.Id) || cue.Volume is < 0 or > 1 || cue.FadeIn < TimeSpan.Zero || cue.FadeOut < TimeSpan.Zero) throw new InvalidOperationException("SCRIPT_AUDIO_CUE_INVALID"); var playing = state.Playing.ToDictionary(); playing[cue.Id] = cue; return state with { Playing = playing, Snapshot = cue.Snapshot, Events = [.. state.Events, $"audio-started:{cue.Id}", $"fade-in:{cue.FadeIn.TotalMilliseconds:0}"] }; }
    public static AudioState Complete(AudioState state, string cueId) { var playing = state.Playing.ToDictionary(); if (!playing.Remove(cueId, out var cue)) return state; return state with { Playing = playing, Events = [.. state.Events, $"fade-out:{cue.FadeOut.TotalMilliseconds:0}", $"audio-completed:{cueId}"] }; }
    public static AudioState SetListener(AudioState state, string listenerId, string snapshot) => string.IsNullOrWhiteSpace(listenerId) ? throw new InvalidOperationException("SCRIPT_AUDIO_LISTENER_INVALID") : state with { ListenerId = listenerId, Snapshot = snapshot, Events = [.. state.Events, $"listener:{listenerId}", $"snapshot:{snapshot}"] };

    public static WorldClock Advance(WorldClock clock, TimeSpan realElapsed) => realElapsed < TimeSpan.Zero || clock.Scale < 0 || !double.IsFinite(clock.Scale) ? throw new InvalidOperationException("SCRIPT_TIME_INVALID") : clock with { SimulationTime = clock.SimulationTime + realElapsed * clock.Scale, Revision = checked(clock.Revision + 1) };
    public static IReadOnlyList<ScriptTimer> Due(WorldClock clock, IReadOnlyList<ScriptTimer> timers) => timers.Where(timer => !timer.Cancelled && timer.DueAt <= clock.SimulationTime).OrderBy(timer => timer.DueAt).ThenBy(timer => timer.Id).ToArray();
    public static ScriptTimer Reschedule(ScriptTimer timer, WorldClock clock) => timer.Repeat is { } repeat && repeat > TimeSpan.Zero ? timer with { DueAt = clock.SimulationTime + repeat } : timer with { Cancelled = true };
    public static DateTimeOffset CalendarTime(WorldClock clock) => clock.Epoch + clock.SimulationTime;

    public void Register(CustomEventSchema schema) { if (string.IsNullOrWhiteSpace(schema.Name) || schema.Version <= 0) throw new InvalidOperationException("SCRIPT_EVENT_SCHEMA_INVALID"); _schemas[$"{schema.Name}@{schema.Version}"] = schema; }
    public void Subscribe(string eventName, string subscriber) { if (!_subscriptions.TryGetValue(eventName, out var listeners)) _subscriptions[eventName] = listeners = []; if (!listeners.Contains(subscriber, StringComparer.Ordinal)) listeners.Add(subscriber); }
    public IReadOnlyList<EventDelivery> Publish(CustomEvent @event)
    {
        if (@event.Authority != "server" || @event.CorrelationId == Guid.Empty || !_schemas.TryGetValue($"{@event.Name}@{@event.Version}", out var schema)) throw new InvalidOperationException("SCRIPT_EVENT_UNREGISTERED");
        foreach (var field in schema.Fields) if (!@event.Payload.TryGetValue(field.Key, out var value) || value is null || !field.Value.IsInstanceOfType(value)) throw new InvalidOperationException($"SCRIPT_EVENT_PAYLOAD_INVALID:{field.Key}");
        return (_subscriptions.GetValueOrDefault(@event.Name) ?? []).Order(StringComparer.Ordinal).Select(listener => new EventDelivery(listener, @event, $"{@event.CorrelationId}:{@event.Name}@{@event.Version}->{listener}")).ToArray();
    }

    public static HotReloadResult Reload(HotReloadModule active, HotReloadModule candidate, bool AtHandlerBoundary, Func<IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, string>, bool> migrate)
    {
        if (!AtHandlerBoundary) return new(false, active, [], "deferred", "SCRIPT_HOT_RELOAD_HANDLER_ACTIVE"); if (candidate.Version <= active.Version || string.IsNullOrWhiteSpace(candidate.SourceHash)) return new(false, active, [], "restart", "SCRIPT_HOT_RELOAD_VERSION_INVALID"); if (!migrate(active.StateSchema, candidate.StateSchema)) return new(false, active, [], "restart", "SCRIPT_HOT_RELOAD_MIGRATION_FAILED"); return new(true, candidate, active.Dependencies.Union(candidate.Dependencies).Order().ToArray(), "migrated", "ok");
    }

    public static DebugSnapshot Break(string sessionId, string authority, IReadOnlyList<DebugFrame> stack, IReadOnlyDictionary<string, object?> watches, IReadOnlyDictionary<string, string> entities, IReadOnlyList<string> events)
    {
        if (authority is not ("local-author" or "server-admin")) throw new UnauthorizedAccessException("SCRIPT_DEBUG_PERMISSION_DENIED"); return new(sessionId, authority, "paused", stack, watches, entities, events);
    }
    public static DebugSnapshot Step(DebugSnapshot snapshot, string mode) => snapshot.State != "paused" || mode is not ("into" or "over" or "out" or "continue") ? throw new InvalidOperationException("SCRIPT_DEBUG_STEP_INVALID") : snapshot with { State = mode == "continue" ? "running" : "paused", Events = [.. snapshot.Events, $"debug-step:{mode}"] };
}
