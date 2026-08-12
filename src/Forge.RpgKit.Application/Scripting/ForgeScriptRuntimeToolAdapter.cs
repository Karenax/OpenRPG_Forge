using Forge.Scripting;

namespace Forge.RpgKit.Application.Scripting;

public sealed record ForgeScriptRuntimeToolHandle(Guid SourceId);

public sealed record ForgeScriptRuntimeToolResolution(
    bool Available,
    bool OptionalFallback,
    ForgeScriptRuntimeToolHandle? Handle,
    string? DiagnosticCode);

public sealed record ForgeScriptRuntimeToolState(
    ForgeScriptSourceDocument Document,
    AudioState Audio,
    WorldClock Clock,
    IReadOnlyList<ScriptTimer> Timers,
    HotReloadModule ActiveModule,
    DebugSnapshot? Debugger);

public sealed class ForgeScriptRuntimeToolAdapter
{
    public const string RequiredMissingCode = "FORGESCRIPT_RUNTIME_TOOL_REQUIRED_MISSING";

    public ForgeScriptRuntimeToolResolution Resolve(
        ForgeScriptSourceDocument? document,
        bool requiredAtRuntime)
    {
        if (document is null)
            return requiredAtRuntime
                ? new(false, false, null, RequiredMissingCode)
                : new(false, true, null, null);
        return new(true, false, new(document.Id), null);
    }

    public ForgeScriptRuntimeToolState Build(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptSourceDocument document,
        AudioState audio,
        WorldClock clock,
        HotReloadModule activeModule)
    {
        Require(handle, document);
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(activeModule);
        return new(document, audio, clock, [], activeModule, null);
    }

    public ForgeScriptRuntimeToolState Play(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        AudioCue cue)
    {
        Require(handle, state.Document);
        return state with { Audio = ForgeScriptRuntimeTools.Play(state.Audio, cue) };
    }

    public ForgeScriptRuntimeToolState CompleteAudio(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        string cueId)
    {
        Require(handle, state.Document);
        return state with { Audio = ForgeScriptRuntimeTools.Complete(state.Audio, cueId) };
    }

    public ForgeScriptRuntimeToolState SetListener(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        string listenerId,
        string snapshot,
        string policy)
    {
        Require(handle, state.Document);
        return state with
        {
            Audio = ForgeScriptRuntimeTools.SetListener(state.Audio, listenerId, snapshot, policy),
        };
    }

    public ForgeScriptRuntimeToolState Advance(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        TimeSpan elapsed)
    {
        Require(handle, state.Document);
        return state with { Clock = ForgeScriptRuntimeTools.Advance(state.Clock, elapsed) };
    }

    public ForgeScriptRuntimeToolState Schedule(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        Guid timerId,
        TimeSpan delay,
        TimeSpan? repeat,
        string handler,
        string schedule)
    {
        Require(handle, state.Document);
        var timer = ForgeScriptRuntimeTools.Schedule(state.Clock, timerId, delay, repeat, handler, schedule);
        if (state.Timers.Any(item => item.Id == timer.Id))
            throw new InvalidOperationException("FORGESCRIPT_TIMER_DUPLICATE");
        return state with { Timers = [.. state.Timers, timer] };
    }

    public IReadOnlyList<EventDelivery> Publish(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        CustomEventSchema schema,
        IReadOnlyList<string> subscribers,
        CustomEvent @event)
    {
        Require(handle, state.Document);
        ArgumentNullException.ThrowIfNull(subscribers);
        var tools = new ForgeScriptRuntimeTools();
        tools.Register(schema);
        foreach (var subscriber in subscribers) tools.Subscribe(schema.Name, subscriber, schema.Version);
        return tools.Publish(@event);
    }

    public (ForgeScriptRuntimeToolState State, HotReloadResult Result) Reload(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        HotReloadModule candidate,
        bool atHandlerBoundary,
        Func<IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, string>, bool> migrate)
    {
        Require(handle, state.Document);
        var result = ForgeScriptRuntimeTools.Reload(state.ActiveModule, candidate, atHandlerBoundary, migrate);
        return result.Applied
            ? (state with { ActiveModule = result.Active }, result)
            : (state, result);
    }

    public ForgeScriptRuntimeToolState Break(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        string sessionId,
        string authority,
        IReadOnlyList<DebugFrame> stack,
        IReadOnlyDictionary<string, object?> watches,
        IReadOnlyDictionary<string, string> entities,
        IReadOnlyList<string> events,
        IReadOnlySet<string> breakpoints,
        bool remote)
    {
        Require(handle, state.Document);
        return state with
        {
            Debugger = ForgeScriptRuntimeTools.Break(sessionId, authority, stack, watches, entities,
                events, breakpoints, remote),
        };
    }

    public ForgeScriptRuntimeToolState Step(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        string mode)
    {
        Require(handle, state.Document);
        if (state.Debugger is null) throw new InvalidOperationException("FORGESCRIPT_DEBUG_SESSION_MISSING");
        return state with { Debugger = ForgeScriptRuntimeTools.Step(state.Debugger, mode) };
    }

    public ForgeScriptExecutionResult Execute(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptRuntimeToolState state,
        IForgeScriptRuntime runtime,
        ForgeScriptQuotas? quotas = null)
    {
        Require(handle, state.Document);
        ArgumentNullException.ThrowIfNull(runtime);
        var compiled = runtime.Compile(state.Document.Source);
        if (!compiled.Success || compiled.Artifact is null)
            return new(ForgeScriptExecutionStatus.Failed, [], compiled.Diagnostics, null);
        return runtime.Execute(compiled.Artifact, quotas);
    }

    private static void Require(
        ForgeScriptRuntimeToolHandle handle,
        ForgeScriptSourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(document);
        if (handle.SourceId == Guid.Empty || handle.SourceId != document.Id)
            throw new InvalidOperationException("FORGESCRIPT_RUNTIME_TOOL_HANDLE_STALE");
    }
}
