namespace Forge.Studio.Session;

internal enum StudioUx6LEventFieldKind
{
    Boolean,
    Integer,
    Long,
    Number,
    Text,
    Guid,
}

internal sealed record StudioUx6LRuntimeDraft(
    string AudioCueId,
    string AudioBus,
    double AudioVolume,
    double FadeInSeconds,
    double FadeOutSeconds,
    bool Loop,
    string Snapshot,
    string Listener,
    string ListenerPolicy,
    double TimeScale,
    double AdvanceSeconds,
    double TimerDelaySeconds,
    double TimerRepeatSeconds,
    string TimerHandler,
    string TimerSchedule,
    string EventName,
    int EventVersion,
    string EventField,
    StudioUx6LEventFieldKind EventFieldKind,
    string EventValue,
    IReadOnlyList<string> Subscribers);

internal sealed record StudioUx6LSourcePlan(
    bool Succeeded,
    string Message,
    string ReviewedSource,
    string CandidateSource,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6LRuntimeResult(
    bool Succeeded,
    string Message,
    bool AudioLifecycle,
    bool AudioListenerAndSnapshot,
    bool AudioHandleControl,
    bool DeterministicClock,
    bool TimerDueAndRepeat,
    bool CalendarDerived,
    bool EventSchema,
    bool EventDeliveryOrder,
    bool EventAuthorityBlocked,
    bool InvalidPayloadBlocked,
    bool ApiCatalogComplete,
    bool NativeCompilation,
    bool NativeExecution,
    bool NativeAudioIntent,
    bool NativeTimeIntent,
    bool NativeEventIntent,
    IReadOnlyList<string> Events,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6LSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool SharedSourceAuthority,
    bool ReviewedSource,
    bool AudioRuntime,
    bool TimeRuntime,
    bool CustomEvents,
    bool ApiCatalog,
    bool NativeExecution,
    bool InvalidAudioBlocked,
    bool InvalidEventBlocked,
    bool CanonicalDryRun,
    bool Compilation,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
