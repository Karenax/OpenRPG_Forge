namespace Forge.Scripting;

public sealed record AudioCue(
    string Id,
    string Bus,
    double Volume,
    TimeSpan FadeIn,
    TimeSpan FadeOut,
    bool Loop,
    string Snapshot);

public sealed record AudioState(
    IReadOnlyDictionary<string, AudioCue> Playing,
    string ListenerId,
    string Snapshot,
    IReadOnlyList<string> Events)
{
    public string ListenerPolicy { get; init; } = "camera";
    public long Revision { get; init; }
}

public sealed record ScriptAudioEmitterRef(Guid Id, int Generation);

public sealed record ScriptAudioEmitterState(
    ScriptAudioEmitterRef Handle,
    string Status,
    double Gain,
    IReadOnlyList<string> Events)
{
    public long Revision { get; init; }
}

public sealed record WorldClock(
    DateTimeOffset Epoch,
    TimeSpan SimulationTime,
    double Scale,
    long Revision);

public sealed record ScriptTimer(
    Guid Id,
    TimeSpan DueAt,
    TimeSpan? Repeat,
    string Handler,
    string Schedule,
    bool Cancelled)
{
    public long Revision { get; init; }
}

public sealed record CustomEventSchema(
    string Name,
    int Version,
    IReadOnlyDictionary<string, Type> Fields);

public sealed record CustomEvent(
    string Name,
    int Version,
    IReadOnlyDictionary<string, object?> Payload,
    string Authority,
    Guid CorrelationId);

public sealed record EventDelivery(string Subscriber, CustomEvent Event, string Trace);

public sealed record HotReloadModule(
    string Id,
    int Version,
    IReadOnlyDictionary<string, string> StateSchema,
    IReadOnlySet<string> Dependencies,
    string SourceHash)
{
    public int SchemaVersion { get; init; } = 1;
    public string Provenance { get; init; } = "forge:script-hot-reload";
    public string ApiVersion { get; init; } = "1.0.0";
}

public sealed record HotReloadResult(
    bool Applied,
    HotReloadModule Active,
    IReadOnlyList<string> Invalidated,
    string Strategy,
    string Diagnostic);

public sealed record DebugFrame(
    string Function,
    string Source,
    int Line,
    IReadOnlyDictionary<string, object?> Locals);

public sealed record DebugSnapshot(
    string SessionId,
    string Authority,
    string State,
    IReadOnlyList<DebugFrame> Stack,
    IReadOnlyDictionary<string, object?> Watches,
    IReadOnlyDictionary<string, string> Entities,
    IReadOnlyList<string> Events)
{
    public IReadOnlySet<string> Breakpoints { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public bool Remote { get; init; }
    public long Revision { get; init; }
}

public sealed class ForgeScriptRuntimeTools
{
    private static readonly IReadOnlySet<string> ListenerPolicies =
        new HashSet<string>(["camera", "entity", "player", "every-listener"], StringComparer.Ordinal);

    private static readonly IReadOnlySet<Type> EventFieldTypes =
        new HashSet<Type>([typeof(bool), typeof(int), typeof(long), typeof(double), typeof(string), typeof(Guid)]);

    private readonly Dictionary<string, CustomEventSchema> _schemas = [];
    private readonly Dictionary<string, List<string>> _subscriptions = [];

    public static AudioState Play(AudioState state, AudioCue cue)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(cue);
        ValidateAudioCue(cue);
        var playing = state.Playing.ToDictionary(StringComparer.Ordinal);
        playing[cue.Id] = cue;
        return state with
        {
            Playing = playing,
            Snapshot = cue.Snapshot,
            Events =
            [
                .. state.Events,
                $"audio-started:{cue.Id}:bus={cue.Bus}:loop={cue.Loop}",
                $"fade-in:{cue.FadeIn.TotalMilliseconds:0}",
                $"snapshot:{cue.Snapshot}",
            ],
            Revision = checked(state.Revision + 1),
        };
    }

    public static AudioState Complete(AudioState state, string cueId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(cueId);
        var playing = state.Playing.ToDictionary(StringComparer.Ordinal);
        if (!playing.Remove(cueId, out var cue)) return state;
        return state with
        {
            Playing = playing,
            Events =
            [
                .. state.Events,
                $"fade-out:{cue.FadeOut.TotalMilliseconds:0}",
                $"audio-completed:{cueId}",
            ],
            Revision = checked(state.Revision + 1),
        };
    }

    public static AudioState SetListener(
        AudioState state,
        string listenerId,
        string snapshot,
        string policy = "entity")
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(listenerId) || string.IsNullOrWhiteSpace(snapshot) ||
            !ListenerPolicies.Contains(policy))
            throw new InvalidOperationException("SCRIPT_AUDIO_LISTENER_INVALID");
        return state with
        {
            ListenerId = listenerId,
            ListenerPolicy = policy,
            Snapshot = snapshot,
            Events = [.. state.Events, $"listener:{listenerId}:policy={policy}", $"snapshot:{snapshot}"],
            Revision = checked(state.Revision + 1),
        };
    }

    public static ScriptAudioEmitterState Control(
        ScriptAudioEmitterState state,
        string operation,
        double targetGain = 1,
        double fadeSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Handle.Id == Guid.Empty || state.Handle.Generation <= 0)
            throw new InvalidOperationException("SCRIPT_AUDIO_HANDLE_INVALID");
        if (!double.IsFinite(targetGain) || targetGain is < 0 or > 4 ||
            !double.IsFinite(fadeSeconds) || fadeSeconds is < 0 or > 120)
            throw new InvalidOperationException("SCRIPT_AUDIO_FADE_INVALID");
        var next = operation switch
        {
            "start" => state with { Status = "playing", Events = [.. state.Events, "audio.started"] },
            "stop" => state with
            {
                Status = "stopped",
                Gain = 0,
                Events = [.. state.Events, "audio.stopped"],
            },
            "pause" when state.Status == "playing" => state with
            {
                Status = "paused",
                Events = [.. state.Events, "audio.paused"],
            },
            "resume" when state.Status == "paused" => state with
            {
                Status = "playing",
                Events = [.. state.Events, "audio.started:resume"],
            },
            "fade" => state with
            {
                Status = targetGain == 0 ? "stopped" : "fading",
                Gain = targetGain,
                Events = [.. state.Events, FormattableString.Invariant(
                    $"audio.faded:{targetGain:0.###}:{fadeSeconds:0.###}")],
            },
            "pause" or "resume" => state,
            _ => throw new InvalidOperationException("SCRIPT_AUDIO_OPERATION_INVALID"),
        };
        return ReferenceEquals(next, state) ? next : next with { Revision = checked(state.Revision + 1) };
    }

    public static WorldClock Advance(WorldClock clock, TimeSpan realElapsed)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (clock.Epoch == default || realElapsed < TimeSpan.Zero || clock.SimulationTime < TimeSpan.Zero ||
            clock.Scale is < 0 or > 100 || !double.IsFinite(clock.Scale))
            throw new InvalidOperationException("SCRIPT_TIME_INVALID");
        return clock with
        {
            SimulationTime = clock.SimulationTime + realElapsed * clock.Scale,
            Revision = checked(clock.Revision + 1),
        };
    }

    public static ScriptTimer Schedule(
        WorldClock clock,
        Guid id,
        TimeSpan delay,
        TimeSpan? repeat,
        string handler,
        string schedule)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (id == Guid.Empty || delay < TimeSpan.Zero || repeat is { } interval && interval <= TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(handler) || string.IsNullOrWhiteSpace(schedule))
            throw new InvalidOperationException("SCRIPT_TIMER_INVALID");
        return new(id, clock.SimulationTime + delay, repeat, handler, schedule, false);
    }

    public static IReadOnlyList<ScriptTimer> Due(WorldClock clock, IReadOnlyList<ScriptTimer> timers)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timers);
        if (timers.Any(timer => timer.Id == Guid.Empty) || timers.Select(timer => timer.Id).Distinct().Count() != timers.Count)
            throw new InvalidOperationException("SCRIPT_TIMER_IDENTITY_INVALID");
        return timers.Where(timer => !timer.Cancelled && timer.DueAt <= clock.SimulationTime)
            .OrderBy(timer => timer.DueAt)
            .ThenBy(timer => timer.Id)
            .ToArray();
    }

    public static ScriptTimer Reschedule(ScriptTimer timer, WorldClock clock)
    {
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(clock);
        return timer.Repeat is { } repeat && repeat > TimeSpan.Zero
            ? timer with { DueAt = clock.SimulationTime + repeat, Revision = checked(timer.Revision + 1) }
            : timer with { Cancelled = true, Revision = checked(timer.Revision + 1) };
    }

    public static ScriptTimer Cancel(ScriptTimer timer) => timer with
    {
        Cancelled = true,
        Revision = checked(timer.Revision + 1),
    };

    public static DateTimeOffset CalendarTime(WorldClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return clock.Epoch + clock.SimulationTime;
    }

    public void Register(CustomEventSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (!ValidEventName(schema.Name) || schema.Version <= 0 || schema.Fields.Count == 0 ||
            schema.Fields.Any(field => string.IsNullOrWhiteSpace(field.Key) || !EventFieldTypes.Contains(field.Value)))
            throw new InvalidOperationException("SCRIPT_EVENT_SCHEMA_INVALID");
        var key = EventKey(schema.Name, schema.Version);
        if (_schemas.TryGetValue(key, out var registered) && !registered.Equals(schema))
            throw new InvalidOperationException("SCRIPT_EVENT_SCHEMA_CONFLICT");
        _schemas[key] = schema;
    }

    public void Subscribe(string eventName, string subscriber, int version = 1)
    {
        if (!ValidEventName(eventName) || string.IsNullOrWhiteSpace(subscriber) || version <= 0)
            throw new InvalidOperationException("SCRIPT_EVENT_SUBSCRIPTION_INVALID");
        var key = EventKey(eventName, version);
        if (!_subscriptions.TryGetValue(key, out var listeners)) _subscriptions[key] = listeners = [];
        if (!listeners.Contains(subscriber, StringComparer.Ordinal)) listeners.Add(subscriber);
    }

    public void Unsubscribe(string eventName, string subscriber, int version = 1)
    {
        var key = EventKey(eventName, version);
        if (_subscriptions.TryGetValue(key, out var listeners)) listeners.Remove(subscriber);
    }

    public IReadOnlyList<EventDelivery> Publish(CustomEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (@event.Authority != "server" || @event.CorrelationId == Guid.Empty ||
            !_schemas.TryGetValue(EventKey(@event.Name, @event.Version), out var schema))
            throw new InvalidOperationException("SCRIPT_EVENT_UNREGISTERED");
        if (@event.Payload.Count != schema.Fields.Count)
            throw new InvalidOperationException("SCRIPT_EVENT_PAYLOAD_FIELD_COUNT_INVALID");
        foreach (var field in schema.Fields)
        {
            if (!@event.Payload.TryGetValue(field.Key, out var value) || value is null ||
                !field.Value.IsInstanceOfType(value))
                throw new InvalidOperationException($"SCRIPT_EVENT_PAYLOAD_INVALID:{field.Key}");
        }

        return (_subscriptions.GetValueOrDefault(EventKey(@event.Name, @event.Version)) ?? [])
            .Order(StringComparer.Ordinal)
            .Select(listener => new EventDelivery(listener, @event,
                $"{@event.CorrelationId}:{@event.Name}@{@event.Version}->{listener}"))
            .ToArray();
    }

    public static HotReloadResult Reload(
        HotReloadModule active,
        HotReloadModule candidate,
        bool atHandlerBoundary,
        Func<IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, string>, bool> migrate)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(migrate);
        if (!atHandlerBoundary)
            return new(false, active, [], "deferred", "SCRIPT_HOT_RELOAD_HANDLER_ACTIVE");
        if (string.IsNullOrWhiteSpace(active.Id) || candidate.Id != active.Id || candidate.Version <= active.Version ||
            candidate.SchemaVersion != 1 || string.IsNullOrWhiteSpace(candidate.Provenance) ||
            candidate.ApiVersion != "1.0.0" || string.IsNullOrWhiteSpace(candidate.SourceHash))
            return new(false, active, [], "restart", "SCRIPT_HOT_RELOAD_VERSION_INVALID");
        if (!migrate(active.StateSchema, candidate.StateSchema))
            return new(false, active, [], "restart", "SCRIPT_HOT_RELOAD_MIGRATION_FAILED");
        return new(true, candidate,
            active.Dependencies.Union(candidate.Dependencies).Order(StringComparer.Ordinal).ToArray(),
            "migrated", "ok");
    }

    public static DebugSnapshot Break(
        string sessionId,
        string authority,
        IReadOnlyList<DebugFrame> stack,
        IReadOnlyDictionary<string, object?> watches,
        IReadOnlyDictionary<string, string> entities,
        IReadOnlyList<string> events,
        IReadOnlySet<string>? breakpoints = null,
        bool remote = false)
    {
        if (authority is not ("local-author" or "server-admin"))
            throw new UnauthorizedAccessException("SCRIPT_DEBUG_PERMISSION_DENIED");
        if (string.IsNullOrWhiteSpace(sessionId) || stack.Any(frame => string.IsNullOrWhiteSpace(frame.Function) ||
                string.IsNullOrWhiteSpace(frame.Source) || frame.Line <= 0) ||
            remote && authority != "server-admin")
            throw new InvalidOperationException("SCRIPT_DEBUG_SESSION_INVALID");
        return new(sessionId, authority, "paused", stack, watches, entities, events)
        {
            Breakpoints = breakpoints ?? new HashSet<string>(StringComparer.Ordinal),
            Remote = remote,
        };
    }

    public static DebugSnapshot Step(DebugSnapshot snapshot, string mode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State != "paused" || mode is not ("into" or "over" or "out" or "continue"))
            throw new InvalidOperationException("SCRIPT_DEBUG_STEP_INVALID");
        return snapshot with
        {
            State = mode == "continue" ? "running" : "paused",
            Events = [.. snapshot.Events, $"debug-step:{mode}"],
            Revision = checked(snapshot.Revision + 1),
        };
    }

    public static DebugSnapshot SetWatch(DebugSnapshot snapshot, string expression, object? value)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        var watches = snapshot.Watches.ToDictionary(StringComparer.Ordinal);
        watches[expression] = value;
        return snapshot with { Watches = watches, Revision = checked(snapshot.Revision + 1) };
    }

    public static string InspectEntity(DebugSnapshot snapshot, string alias)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        return snapshot.Entities.TryGetValue(alias, out var entity)
            ? entity
            : throw new InvalidOperationException("SCRIPT_DEBUG_ENTITY_UNKNOWN");
    }

    private static void ValidateAudioCue(AudioCue cue)
    {
        if (string.IsNullOrWhiteSpace(cue.Id) || string.IsNullOrWhiteSpace(cue.Bus) ||
            string.IsNullOrWhiteSpace(cue.Snapshot) || !double.IsFinite(cue.Volume) || cue.Volume is < 0 or > 1 ||
            cue.FadeIn < TimeSpan.Zero || cue.FadeOut < TimeSpan.Zero ||
            cue.FadeIn > TimeSpan.FromMinutes(2) || cue.FadeOut > TimeSpan.FromMinutes(2))
            throw new InvalidOperationException("SCRIPT_AUDIO_CUE_INVALID");
    }

    private static string EventKey(string name, int version) => $"{name}@{version}";

    private static bool ValidEventName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || !name.Contains('.')) return false;
        return name.Split('.').All(part => part.Length > 0 &&
            char.IsAsciiLetter(part[0]) &&
            part.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'));
    }
}
