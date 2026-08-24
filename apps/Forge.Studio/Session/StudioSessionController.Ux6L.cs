using System.Globalization;
using System.Text;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6LStartMarker = "// <studio:ux6-l-runtime-tools>";
    private const string Ux6LEndMarker = "// </studio:ux6-l-runtime-tools>";

    internal StudioUx6LRuntimeDraft DefaultRuntimeToolsDraftUx6L() => new(
        "music:dawn",
        "music",
        .8,
        1,
        2,
        true,
        "exploration",
        "hero",
        "player",
        2,
        3,
        1,
        5,
        "OnStudioTick",
        "daily",
        "studio.damage",
        1,
        "amount",
        StudioUx6LEventFieldKind.Integer,
        "4",
        ["audio", "quest"]);

    internal StudioUx6LSourcePlan BuildRuntimeToolsSourceUx6L(
        StudioUx6FDraft source,
        StudioUx6LRuntimeDraft runtime)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(runtime);
        var diagnostics = ValidateRuntimeToolsUx6L(runtime);
        if (diagnostics.Count > 0)
            return new(false, "Le scénario runtime est incomplet · corrigez les champs signalés.",
                string.Empty, source.Source, diagnostics);

        var block = RuntimeToolsBlockUx6L(runtime);
        var start = source.Source.IndexOf(Ux6LStartMarker, StringComparison.Ordinal);
        var end = source.Source.IndexOf(Ux6LEndMarker, StringComparison.Ordinal);
        string candidate;
        if (start >= 0 || end >= 0)
        {
            if (start < 0 || end < start)
                return new(false,
                    "Le bloc UX6-L existant est incomplet et reste protégé.", string.Empty, source.Source,
                    [new("script.runtime_tools.block_invalid", "script/source", "error",
                        "Les deux marqueurs UX6-L sont requis.")]);
            candidate = source.Source[..start] + block +
                        source.Source[(end + Ux6LEndMarker.Length)..];
        }
        else
        {
            candidate = source.Source.TrimEnd() + Environment.NewLine + Environment.NewLine +
                        block + Environment.NewLine;
        }

        return new(true,
            "Bloc R099 versionné généré · relisez-le avant l’aperçu sec et l’enregistrement.",
            block, candidate, []);
    }

    internal StudioUx6LRuntimeResult PreviewRuntimeToolsUx6L(
        StudioUx6FDraft source,
        StudioUx6LRuntimeDraft runtime)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(runtime);
        var diagnostics = ValidateRuntimeToolsUx6L(runtime);
        if (diagnostics.Count > 0)
            return FailedRuntimeToolsUx6L("Simulation refusée · paramètres invalides.", diagnostics);

        try
        {
            var audio = new AudioState(
                new Dictionary<string, AudioCue>(StringComparer.Ordinal), "camera", "default", []);
            var cue = new AudioCue(runtime.AudioCueId, runtime.AudioBus, runtime.AudioVolume,
                TimeSpan.FromSeconds(runtime.FadeInSeconds), TimeSpan.FromSeconds(runtime.FadeOutSeconds),
                runtime.Loop, runtime.Snapshot);
            var playing = ForgeScriptRuntimeTools.Play(audio, cue);
            var listening = ForgeScriptRuntimeTools.SetListener(playing, runtime.Listener,
                runtime.Snapshot, runtime.ListenerPolicy);
            var emitter = new ScriptAudioEmitterState(
                new(Guid.Parse("6e000000-0000-4000-8000-000000000121"), 1), "stopped", 1, []);
            var started = ForgeScriptRuntimeTools.Control(emitter, "start");
            var paused = ForgeScriptRuntimeTools.Control(started, "pause");
            var resumed = ForgeScriptRuntimeTools.Control(paused, "resume");
            var faded = ForgeScriptRuntimeTools.Control(resumed, "fade", runtime.AudioVolume,
                runtime.FadeOutSeconds);
            var stopped = ForgeScriptRuntimeTools.Control(faded, "stop");
            var completed = ForgeScriptRuntimeTools.Complete(listening, cue.Id);
            var audioLifecycle = playing.Playing.ContainsKey(cue.Id) && completed.Playing.Count == 0 &&
                                 completed.Revision == 3 &&
                                 completed.Events.Contains($"audio-completed:{cue.Id}", StringComparer.Ordinal);
            var audioListener = listening.ListenerId == runtime.Listener &&
                                listening.ListenerPolicy == runtime.ListenerPolicy &&
                                listening.Snapshot == runtime.Snapshot;
            var audioHandle = paused.Status == "paused" && resumed.Status == "playing" &&
                              faded.Gain == runtime.AudioVolume && stopped.Status == "stopped" &&
                              stopped.Revision == 5;

            var clock = new WorldClock(new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero),
                TimeSpan.Zero, runtime.TimeScale, 0);
            var advanced = ForgeScriptRuntimeTools.Advance(clock,
                TimeSpan.FromSeconds(runtime.AdvanceSeconds));
            var timer = ForgeScriptRuntimeTools.Schedule(advanced,
                Guid.Parse("6e000000-0000-4000-8000-000000000122"),
                TimeSpan.FromSeconds(runtime.TimerDelaySeconds),
                runtime.TimerRepeatSeconds > 0 ? TimeSpan.FromSeconds(runtime.TimerRepeatSeconds) : null,
                runtime.TimerHandler, runtime.TimerSchedule);
            var dueClock = ForgeScriptRuntimeTools.Advance(advanced,
                TimeSpan.FromSeconds(runtime.TimerDelaySeconds / Math.Max(.0001, runtime.TimeScale) + 1));
            var due = ForgeScriptRuntimeTools.Due(dueClock, [timer]);
            var rescheduled = ForgeScriptRuntimeTools.Reschedule(timer, dueClock);
            var cancelled = ForgeScriptRuntimeTools.Cancel(rescheduled);
            var calendar = ForgeScriptRuntimeTools.CalendarTime(dueClock);
            var deterministicClock = advanced.SimulationTime ==
                                     TimeSpan.FromSeconds(runtime.AdvanceSeconds * runtime.TimeScale) &&
                                     advanced.Revision == 1;
            var timerReady = due.Count == 1 && due[0].Id == timer.Id &&
                             rescheduled.Revision == 1 && cancelled.Cancelled;
            var calendarDerived = calendar == dueClock.Epoch + dueClock.SimulationTime;

            if (!TryEventFieldUx6L(runtime.EventFieldKind, runtime.EventValue,
                    out var fieldType, out var fieldValue))
                return FailedRuntimeToolsUx6L("La valeur d’événement ne correspond pas au type choisi.",
                    [new("script.event.value_invalid", "script/runtime/event/value", "error",
                        "Saisissez une valeur compatible avec le type de champ.")]);
            var events = new ForgeScriptRuntimeTools();
            events.Register(new CustomEventSchema(runtime.EventName, runtime.EventVersion,
                new Dictionary<string, Type>(StringComparer.Ordinal) { [runtime.EventField] = fieldType }));
            foreach (var subscriber in runtime.Subscribers)
                events.Subscribe(runtime.EventName, subscriber, runtime.EventVersion);
            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [runtime.EventField] = fieldValue,
            };
            var custom = new CustomEvent(runtime.EventName, runtime.EventVersion, payload, "server",
                Guid.Parse("6e000000-0000-4000-8000-000000000123"));
            var deliveries = events.Publish(custom);
            var eventSchema = deliveries.All(item => item.Event == custom);
            var deliveryOrder = deliveries.Select(item => item.Subscriber)
                .SequenceEqual(runtime.Subscribers.Order(StringComparer.Ordinal));
            var authorityBlocked = ThrowsInvalidUx6L(() => events.Publish(custom with
            {
                Authority = "client",
                CorrelationId = Guid.Parse("6e000000-0000-4000-8000-000000000124"),
            }));
            var invalidPayload = ThrowsInvalidUx6L(() => events.Publish(custom with
            {
                Payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [runtime.EventField] = new object(),
                },
                CorrelationId = Guid.Parse("6e000000-0000-4000-8000-000000000125"),
            }));

            string[] expectedApis =
            [
                "audio_emitter", "audio_start", "audio_pause", "audio_resume", "audio_fade",
                "audio_snapshot", "audio_listener", "audio_complete", "world_clock",
                "world_calendar_ms", "timer_schedule", "timer_cancel", "deterministic_delay",
                "event_subscribe", "custom_event", "event_publish",
            ];
            var names = ForgeScriptApiCatalog.Registrations.Select(item => item.Name)
                .ToHashSet(StringComparer.Ordinal);
            var apiCatalog = expectedApis.All(names.Contains);

            var native = new NativeForgeScriptRuntime();
            var compilation = native.Compile(NativeRuntimeToolsSourceUx6L(runtime));
            var nativeDiagnostics = compilation.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray();
            if (!compilation.Success || compilation.Artifact is not ForgeScriptArtifact artifact)
                return FailedRuntimeToolsUx6L("Le scénario natif R099 ne compile pas.", nativeDiagnostics);
            var execution = native.Execute(artifact,
                new ForgeScriptQuotas(40_000, 1000, 32, 8, 8 * 1024 * 1024, 2026));
            var executionDiagnostics = execution.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray();
            var nativeExecution = execution.Status == ForgeScriptExecutionStatus.Completed &&
                                  executionDiagnostics.Length == 0;
            var nativeAudio = execution.Events.Any(item => item.Kind == "audio.started") &&
                              execution.Events.Any(item => item.Kind == "audio.faded") &&
                              execution.Events.Any(item => item.Kind == "audio.completed");
            var nativeTime = execution.Events.Any(item => item.Kind == "time.clock") &&
                             execution.Events.Any(item => item.Kind == "time.timer.scheduled") &&
                             execution.Events.Any(item => item.Kind == "time.delay");
            var nativeEvent = execution.Events.Any(item => item.Kind == "event.published");
            var allEvents = completed.Events
                .Concat(stopped.Events)
                .Concat(deliveries.Select(item => $"livraison:{item.Subscriber}:{item.Trace}"))
                .Concat(execution.Events.Select(item => $"natif:{item.Kind}:{item.Value}"))
                .ToArray();
            var succeeded = audioLifecycle && audioListener && audioHandle && deterministicClock &&
                            timerReady && calendarDerived && eventSchema && deliveryOrder &&
                            authorityBlocked && invalidPayload && apiCatalog && compilation.Success &&
                            nativeExecution && nativeAudio && nativeTime && nativeEvent;
            return new(succeeded,
                succeeded
                    ? "Simulation R099 terminée · états C# et intentions natives cohérents, sans lecture audio ni horloge système."
                    : "Simulation R099 incomplète · consultez les sous-invariants.",
                audioLifecycle, audioListener, audioHandle, deterministicClock, timerReady,
                calendarDerived, eventSchema, deliveryOrder, authorityBlocked, invalidPayload,
                apiCatalog, compilation.Success, nativeExecution, nativeAudio, nativeTime, nativeEvent,
                allEvents, executionDiagnostics);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
                                           OverflowException or DllNotFoundException or BadImageFormatException)
        {
            return FailedRuntimeToolsUx6L($"Simulation R099 refusée : {exception.Message}",
                [new("script.runtime_tools.failed", "script/runtime-tools", "error", exception.Message)]);
        }
    }

    internal StudioUx6LSmokeResult RunUx6LContractSmoke()
    {
        var sourceSession = Session;
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceSession is null || sourceProject is null || sourceHistory is null)
            return FailedSmokeUx6L();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-l",
            Guid.NewGuid().ToString("N"));
        var released = false;
        StudioSessionController? editing = null;
        StudioUx6LSmokeResult result;
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded)
                return FailedSmokeUx6L();
            var document = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(document);
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(document.RelativePath, out var initial, out _) ||
                initial is null)
                return FailedSmokeUx6L();
            var runtime = editing.DefaultRuntimeToolsDraftUx6L();
            var invalidAudio = !editing.BuildRuntimeToolsSourceUx6L(initial,
                runtime with { AudioVolume = 2 }).Succeeded;
            var invalidEvent = !editing.BuildRuntimeToolsSourceUx6L(initial,
                runtime with { EventName = "invalid" }).Succeeded;
            var plan = editing.BuildRuntimeToolsSourceUx6L(initial, runtime);
            var candidate = initial with { Source = plan.CandidateSource };
            var simulation = editing.PreviewRuntimeToolsUx6L(candidate, runtime);
            var preview = editing.PreviewScriptSourceUx6F(candidate);
            var compilation = editing.CompileScriptSourceUx6F(candidate);
            var stale = !editing.ApplyApprovedScriptSourceUx6F(candidate with
            {
                BaseRevision = candidate.BaseRevision + 1,
            }).Succeeded;
            editing.PreviewScriptSourceUx6F(candidate);
            var applied = editing.ApplyApprovedScriptSourceUx6F(candidate);
            var undone = editing.UndoScriptSourceUx6F();
            var redone = editing.RedoScriptSourceUx6F();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(root).Succeeded &&
                            reopened.TryOpenCanonicalSourceUx6F(candidate.RelativePath,
                                out var reopenedDraft, out _) &&
                            reopenedDraft?.Source == candidate.Source;
            reopened.Close();
            var sourcePreserved = ReferenceEquals(sourceSession, Session) &&
                                  ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History.State.Revision == sourceRevision &&
                                  History.Journal.SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty;
            result = new(
                plan.Succeeded && simulation.Succeeded && preview.Succeeded && preview.CanApply &&
                compilation.Succeeded && invalidAudio && invalidEvent && stale && applied.Succeeded &&
                undone.Succeeded && redone.Succeeded && persisted && sourcePreserved,
                true,
                candidate.Id == initial.Id && candidate.RelativePath == initial.RelativePath,
                plan.ReviewedSource.Contains("<studio:ux6-l-runtime-tools>", StringComparison.Ordinal),
                simulation.AudioLifecycle && simulation.AudioListenerAndSnapshot &&
                simulation.AudioHandleControl,
                simulation.DeterministicClock && simulation.TimerDueAndRepeat &&
                simulation.CalendarDerived,
                simulation.EventSchema && simulation.EventDeliveryOrder &&
                simulation.EventAuthorityBlocked && simulation.InvalidPayloadBlocked,
                simulation.ApiCatalogComplete,
                simulation.NativeCompilation && simulation.NativeExecution &&
                simulation.NativeAudioIntent && simulation.NativeTimeIntent &&
                simulation.NativeEventIntent,
                invalidAudio,
                invalidEvent,
                preview.CanonicalDryRun,
                compilation.Succeeded,
                stale,
                applied.Succeeded && undone.Succeeded && redone.Succeeded,
                persisted,
                sourcePreserved,
                false);
        }
        finally
        {
            editing?.Close();
            released = DeleteUx6FTemporaryWorkspace(root);
        }

        return result with
        {
            Succeeded = result.Succeeded && released,
            TemporaryWorkspaceReleased = released,
        };
    }

    private static IReadOnlyList<StudioUx6FDiagnostic> ValidateRuntimeToolsUx6L(
        StudioUx6LRuntimeDraft runtime)
    {
        var diagnostics = new List<StudioUx6FDiagnostic>();
        Add(string.IsNullOrWhiteSpace(runtime.AudioCueId) || runtime.AudioCueId.Length > 128,
            "script.audio.cue_invalid", "script/runtime/audio/cue",
            "L’identifiant audio doit rester lisible et borné.");
        Add(string.IsNullOrWhiteSpace(runtime.AudioBus) || string.IsNullOrWhiteSpace(runtime.Snapshot) ||
            string.IsNullOrWhiteSpace(runtime.Listener), "script.audio.reference_invalid",
            "script/runtime/audio/references", "Bus, snapshot et auditeur sont requis.");
        Add(!double.IsFinite(runtime.AudioVolume) || runtime.AudioVolume is < 0 or > 1 ||
            !double.IsFinite(runtime.FadeInSeconds) || runtime.FadeInSeconds is < 0 or > 120 ||
            !double.IsFinite(runtime.FadeOutSeconds) || runtime.FadeOutSeconds is < 0 or > 120,
            "script.audio.values_invalid", "script/runtime/audio",
            "Volume et fondus doivent respecter les bornes audio.");
        Add(runtime.ListenerPolicy is not ("camera" or "entity" or "player" or "every-listener"),
            "script.audio.listener_policy_invalid", "script/runtime/audio/listener",
            "Choisissez une politique d’écoute canonique.");
        Add(!double.IsFinite(runtime.TimeScale) || runtime.TimeScale is <= 0 or > 100 ||
            !double.IsFinite(runtime.AdvanceSeconds) || runtime.AdvanceSeconds is < 0 or > 86_400 ||
            !double.IsFinite(runtime.TimerDelaySeconds) || runtime.TimerDelaySeconds is < 0 or > 86_400 ||
            !double.IsFinite(runtime.TimerRepeatSeconds) || runtime.TimerRepeatSeconds is < 0 or > 86_400,
            "script.time.values_invalid", "script/runtime/time",
            "Échelle, avance, délai et répétition doivent être finis et bornés.");
        Add(string.IsNullOrWhiteSpace(runtime.TimerHandler) ||
            string.IsNullOrWhiteSpace(runtime.TimerSchedule), "script.timer.binding_invalid",
            "script/runtime/time/timer", "Handler et schedule sont requis.");
        Add(!ValidEventNameUx6L(runtime.EventName) || runtime.EventVersion <= 0 ||
            string.IsNullOrWhiteSpace(runtime.EventField), "script.event.schema_invalid",
            "script/runtime/event/schema", "Nom namespacé, version et champ sont requis.");
        Add(runtime.Subscribers.Count != 2 || runtime.Subscribers.Any(string.IsNullOrWhiteSpace) ||
            runtime.Subscribers.Distinct(StringComparer.Ordinal).Count() != runtime.Subscribers.Count,
            "script.event.subscribers_invalid", "script/runtime/event/subscribers",
            "Deux abonnés distincts sont requis pour prouver l’ordre de livraison.");
        Add(!TryEventFieldUx6L(runtime.EventFieldKind, runtime.EventValue, out _, out _),
            "script.event.value_invalid", "script/runtime/event/value",
            "La valeur doit correspondre au type de champ choisi.");
        return diagnostics;

        void Add(bool condition, string code, string path, string message)
        {
            if (condition) diagnostics.Add(new(code, path, "error", message));
        }
    }

    private static string RuntimeToolsBlockUx6L(StudioUx6LRuntimeDraft runtime)
    {
        var repeatMilliseconds = (long)Math.Round(runtime.TimerRepeatSeconds * 1000,
            MidpointRounding.AwayFromZero);
        var delayMilliseconds = (long)Math.Round(runtime.TimerDelaySeconds * 1000,
            MidpointRounding.AwayFromZero);
        var subscriber = runtime.Subscribers.Order(StringComparer.Ordinal).First();
        var builder = new StringBuilder();
        builder.AppendLine(Ux6LStartMarker);
        builder.AppendLine("// API ForgeScript 1.0.0 · R099 · bloc auteur revu.");
        builder.AppendLine("void StudioRuntimeToolsScenario()");
        builder.AppendLine("{");
        builder.AppendLine("    EntityRef listener = spawn(\"studio-listener\");");
        builder.AppendLine($"    AudioEmitterRef audio = audio_emitter(\"{QuoteUx6L(runtime.AudioCueId)}\");");
        builder.AppendLine("    audio_start(audio);");
        builder.AppendLine("    audio_pause(audio);");
        builder.AppendLine("    audio_resume(audio);");
        builder.AppendLine($"    audio_fade(audio, {FloatUx6L(runtime.AudioVolume)}, {FloatUx6L(runtime.FadeOutSeconds)});");
        builder.AppendLine($"    audio_snapshot(\"{QuoteUx6L(runtime.Snapshot)}\");");
        builder.AppendLine($"    audio_listener(listener, \"{QuoteUx6L(runtime.ListenerPolicy)}\");");
        builder.AppendLine("    audio_complete(audio, \"finished\");");
        builder.AppendLine("    WorldClockRef clock = world_clock();");
        builder.AppendLine("    int64 calendar = world_calendar_ms(clock);");
        builder.AppendLine($"    TimerRef timer = timer_schedule({delayMilliseconds}, {repeatMilliseconds}, \"{QuoteUx6L(runtime.TimerHandler)}\", \"{QuoteUx6L(runtime.TimerSchedule)}\");");
        builder.AppendLine("    timer_cancel(timer);");
        builder.AppendLine($"    deterministic_delay({delayMilliseconds}, \"{QuoteUx6L(runtime.TimerHandler)}\");");
        builder.AppendLine($"    event_subscribe(\"{QuoteUx6L(runtime.EventName)}\", {runtime.EventVersion}, \"{QuoteUx6L(subscriber)}\");");
        builder.AppendLine($"    CustomEventRef evt = custom_event(\"{QuoteUx6L(runtime.EventName)}\", {runtime.EventVersion}, \"{QuoteUx6L(runtime.EventField)}={QuoteUx6L(runtime.EventValue)}\");");
        builder.AppendLine("    event_publish(evt);");
        builder.AppendLine("}");
        builder.Append(Ux6LEndMarker);
        return builder.ToString();
    }

    private static string NativeRuntimeToolsSourceUx6L(StudioUx6LRuntimeDraft runtime)
    {
        var block = RuntimeToolsBlockUx6L(runtime);
        var start = block.IndexOf("void StudioRuntimeToolsScenario()", StringComparison.Ordinal);
        var end = block.LastIndexOf(Ux6LEndMarker, StringComparison.Ordinal);
        return block[start..end]
            .Replace("void StudioRuntimeToolsScenario()", "void main()", StringComparison.Ordinal)
            .TrimEnd();
    }

    private static bool TryEventFieldUx6L(
        StudioUx6LEventFieldKind kind,
        string text,
        out Type type,
        out object? value)
    {
        type = typeof(string);
        value = null;
        switch (kind)
        {
            case StudioUx6LEventFieldKind.Boolean:
                type = typeof(bool);
                if (bool.TryParse(text, out var boolean)) { value = boolean; return true; }
                return false;
            case StudioUx6LEventFieldKind.Integer:
                type = typeof(int);
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                { value = integer; return true; }
                return false;
            case StudioUx6LEventFieldKind.Long:
                type = typeof(long);
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                { value = longValue; return true; }
                return false;
            case StudioUx6LEventFieldKind.Number:
                type = typeof(double);
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                    double.IsFinite(number)) { value = number; return true; }
                return false;
            case StudioUx6LEventFieldKind.Text:
                type = typeof(string);
                if (!string.IsNullOrWhiteSpace(text) && text.Length <= 4096) { value = text; return true; }
                return false;
            case StudioUx6LEventFieldKind.Guid:
                type = typeof(Guid);
                if (Guid.TryParse(text, out var guid) && guid != Guid.Empty) { value = guid; return true; }
                return false;
            default:
                return false;
        }
    }

    private static bool ValidEventNameUx6L(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 128 && name.Contains('.') &&
        name.Split('.').All(part => part.Length > 0 && char.IsAsciiLetter(part[0]) &&
            part.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'));

    private static bool ThrowsInvalidUx6L(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static string QuoteUx6L(string value) => (value ?? string.Empty)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    private static string FloatUx6L(double value)
    {
        var literal = value.ToString("0.######", CultureInfo.InvariantCulture);
        return literal.Contains('.', StringComparison.Ordinal) ? literal + "f" : literal + ".0f";
    }

    private static StudioUx6LRuntimeResult FailedRuntimeToolsUx6L(
        string message,
        IReadOnlyList<StudioUx6FDiagnostic> diagnostics) =>
        new(false, message, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, [], diagnostics);

    private static StudioUx6LSmokeResult FailedSmokeUx6L() =>
        new(false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false);
}
