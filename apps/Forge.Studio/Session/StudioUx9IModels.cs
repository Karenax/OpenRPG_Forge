namespace Forge.Studio.Session;

internal sealed record StudioUx9IGroupDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    int SelectionCap,
    bool RequiredAtRuntime);

internal sealed record StudioUx9IGroupProbeOptions(
    string TriggerEvent,
    string TriggerPayload,
    string MovementFormation,
    bool FactionWarningAccepted,
    string PlayerTravelMode,
    bool PlayerTravelConfirmed,
    bool PartyConsentConfirmed)
{
    internal static StudioUx9IGroupProbeOptions CreateDefault() => new(
        "enter", "quête:cendres", "wedge", true, "summon-party", true, true);
}

internal sealed record StudioUx9IGroupProbe(
    string Status,
    string TriggerCode,
    int TriggerTraceEntries,
    int SelectionCount,
    bool StableSelectionId,
    string MovementCode,
    int MovedCount,
    int PartialFailures,
    bool PathAdapter,
    string AttackCode,
    int AssignedCount,
    bool ObjectiveReady,
    bool CombatAdapter,
    string TravelCode,
    int TravelledObjects,
    string CameraTransition,
    bool PartyConsent,
    bool Compensated,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9IGroupPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool TriggerRuntime,
    bool SelectionRuntime,
    bool MovementRuntime,
    bool AttackRuntime,
    bool TravelRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9IGroupProbe Probe);

internal sealed record StudioUx9IActionResult(bool Succeeded, string Message);
internal sealed record StudioUx9IApprovalSnapshot(StudioUx9IGroupDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9ISmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool ExplicitSelection,
    bool InvalidBlocked,
    bool PermissionBlocked,
    bool ConfirmationRequired,
    bool PartyConsentRequired,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool TriggerRuntime,
    bool SelectionRuntime,
    bool MovementRuntime,
    bool AttackRuntime,
    bool TravelRuntime,
    bool Compensation,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
