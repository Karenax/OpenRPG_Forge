using Forge.Domain.Lifecycle;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;

namespace Forge.Studio.Session;

internal sealed record StudioEventScriptChoice(
    string RelativePath,
    string DisplayName);

internal sealed record StudioLifecycleEventBindingDraft(
    RuntimeLifecycleEventKind Event,
    RuntimeAuthority Authority,
    string Handler,
    string AutomaticHandler,
    string? ScriptPath,
    bool RequiredAtRuntime);

internal sealed record StudioGameplayEventBindingDraft(
    string EventName,
    string Handler,
    string AutomaticHandler,
    string? ScriptPath,
    bool RequiredAtRuntime);

internal sealed record StudioHeartbeatScheduleDraft(
    Guid Id,
    string Name,
    bool Enabled,
    int IntervalMilliseconds,
    int BudgetMilliseconds,
    bool SuspendWhenInactive,
    string Handler,
    string AutomaticHandler,
    string? ScriptPath,
    bool RequiredAtRuntime);

internal sealed record StudioCustomWorldEventDraft(
    Guid Id,
    string Namespace,
    string Name,
    int PayloadVersion,
    IReadOnlyDictionary<string, string> Fields,
    bool RequiredExtension,
    string Handler,
    string AutomaticHandler,
    string? ScriptPath,
    bool RequiredAtRuntime);

internal sealed record StudioSequenceAbortDraft(
    bool Enabled,
    SequenceAbortRestoreFlags Restore,
    int MaximumNarrativeRollbackSteps,
    string Handler,
    string AutomaticHandler,
    string? ScriptPath,
    bool RequiredAtRuntime);

internal sealed record StudioWorldTimeZoneDraft(
    string AreaKey,
    string DisplayName,
    int OffsetMinutes,
    bool Configured);

internal sealed record StudioWorldClockDraft(
    int DaysPerMonth,
    int MonthsPerYear,
    int HoursPerDay,
    int MinutesPerHour,
    double Speed,
    bool Paused,
    long InitialMinute,
    IReadOnlyList<StudioWorldTimeZoneDraft> AreaTimeZones,
    IReadOnlyDictionary<WorldAstronomicalEventKind, int> AstronomicalMinutes);

internal sealed record StudioProgressionSourceDraft(
    string Key,
    string DisplayName,
    double Multiplier);

internal sealed record StudioProgressionDraft(
    string RulesetId,
    string RulesetDisplayName,
    IReadOnlyList<ProgressionCurvePoint> Curve,
    double GroupMultiplier,
    double DifficultyMultiplier,
    IReadOnlyList<StudioProgressionSourceDraft> Sources,
    bool ShareWithParty);

internal sealed record StudioProjectEventConfigurationDraft(
    bool LifecycleEnabled,
    bool LifecycleRequiredAtRuntime,
    IReadOnlyList<StudioLifecycleEventBindingDraft> LifecycleBindings,
    bool GameplayEnabled,
    bool GameplayRequiredAtRuntime,
    bool UseDownedState,
    bool AutoRespawn,
    bool ReplicateEquipmentAndProgression,
    IReadOnlyList<StudioGameplayEventBindingDraft> GameplayBindings,
    bool RestEnabled,
    PartyRestPolicy RestPartyPolicy,
    int RestDurationSeconds,
    IReadOnlyList<StudioGameplayEventBindingDraft> RestBindings,
    IReadOnlyList<StudioHeartbeatScheduleDraft> Heartbeats,
    bool WorldEventsEnabled,
    bool WorldEventsRequiredAtRuntime,
    IReadOnlyList<StudioCustomWorldEventDraft> CustomWorldEvents,
    StudioSequenceAbortDraft SequenceAbort,
    StudioWorldClockDraft WorldClock,
    StudioProgressionDraft Progression);

internal sealed record StudioProjectEventPreview(
    bool Succeeded,
    string Message,
    string Summary);

internal sealed record StudioUx0FSmokeResult(
    bool Succeeded,
    bool ScriptDiscovery,
    bool MissingScriptBlocked,
    bool LifecycleContexts,
    bool GameplayGroups,
    bool ServerAuthority,
    bool DryRun,
    bool AtomicUndoRedo,
    bool PersistenceRoundTrip,
    bool NeighborPoliciesPreserved,
    bool HumanDiagnostics);

internal sealed record StudioUx0GSmokeResult(
    bool Succeeded,
    bool EquipmentAndProgression,
    bool RestLifecycle,
    bool GlobalHeartbeat,
    bool UserEvent,
    bool RuntimeDispatch,
    bool MissingScriptBlocked,
    bool DryRun,
    bool AtomicUndoRedo,
    bool PersistenceRoundTrip,
    bool NeighborPoliciesPreserved,
    bool HumanDiagnostics);

internal sealed record StudioUx0HSmokeResult(
    bool Succeeded,
    bool SequenceAbort,
    bool WorldClock,
    bool Progression,
    bool RuntimeExecution,
    bool MissingScriptBlocked,
    bool InvalidValuesBlocked,
    bool DryRun,
    bool AtomicUndoRedo,
    bool PersistenceRoundTrip,
    bool NeighborPoliciesPreserved,
    bool HumanDiagnostics);
