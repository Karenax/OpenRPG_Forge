using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioDoorReactiveComplexChoice(
    BlueprintId BlueprintId,
    string Label,
    bool Configured);

internal sealed record StudioDoorDialogueChoice(Guid Id, string Label);

internal sealed record StudioDoorFactionChoice(string Slug, string Label);

internal sealed record StudioDoorCustomEventChoice(
    string EventName,
    string Label,
    string Namespace,
    string Name,
    string ScriptId,
    bool Before,
    bool Authoritative);

internal sealed record StudioDoorCustomEventDraft(
    bool Touched,
    string? OriginalEvent,
    bool Enabled,
    string Namespace,
    string Name,
    string ScriptId,
    bool Before,
    bool Authoritative)
{
    internal string EventName => $"custom:{Namespace.Trim()}:{Name.Trim()}";
}

internal sealed record StudioDoorAssemblyPanelDraft(
    Guid Id,
    string Role,
    double OpenOffset,
    double DelaySeconds,
    bool Reversed);

internal sealed record StudioDoorReactiveComplexDraft(
    BlueprintId BlueprintId,
    bool AbilityTargetedEnabled,
    string AbilityTargetedScriptId,
    bool AbilityImpactedEnabled,
    string AbilityImpactedScriptId,
    bool HeartbeatEnabled,
    string HeartbeatScriptId,
    StudioDoorCustomEventDraft CustomEvent,
    Guid? DialogueId,
    string? FactionId,
    bool AssemblyEnabled,
    DoorWorldProfile.AssemblyKind AssemblyKind,
    IReadOnlyList<StudioDoorAssemblyPanelDraft> Panels,
    string Sequence,
    bool Interlocked,
    string FailureRecovery);

internal sealed record StudioDoorReactiveComplexPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    bool EventSimulationAccepted = false,
    bool NarrativeReferencesAccepted = false,
    bool AssemblyAccepted = false);

internal sealed record StudioDoorReactiveComplexActionResult(
    bool Succeeded,
    string Message,
    bool StaleRevision = false);

internal sealed record StudioUx1ADSmokeResult(
    bool Succeeded,
    bool ExplicitDoorSelection,
    bool DuplicateHumanLabels,
    bool UnsafeAssemblyBlocked,
    bool AbilityEvents,
    bool HeartbeatAndCustomEvents,
    bool NarrativeReferences,
    bool ComplexAssembly,
    bool NeighborEventMerge,
    bool DryRun,
    bool ApprovalRevisionGuard,
    bool CommandRevisionGuard,
    bool CanonicalCommand,
    bool Preservation,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);
