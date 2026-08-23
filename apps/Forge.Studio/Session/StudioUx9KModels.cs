namespace Forge.Studio.Session;

internal sealed record StudioUx9KNarrativeDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    bool RequiredAtRuntime);

internal sealed record StudioUx9KNarrativeProbeOptions(
    string QuestScope,
    string QuestTargetState,
    string EncounterAction,
    string EncounterValue)
{
    internal static StudioUx9KNarrativeProbeOptions CreateDefault() => new(
        "selected-players", "completed", "scale", "1.5");
}

internal sealed record StudioUx9KNarrativeProbe(
    string Status,
    string QuestCode,
    int QuestConsequences,
    int AffectedPlayers,
    bool RpgAdapter,
    bool QuestRolledBack,
    string EncounterInspectCode,
    string EncounterActionCode,
    int EncounterActions,
    bool ScalingBounded,
    bool SpawnAdapter,
    bool AiAdapter,
    bool EncounterRolledBack,
    bool Compensated,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9KNarrativePreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool QuestRuntime,
    bool EncounterRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9KNarrativeProbe Probe);

internal sealed record StudioUx9KActionResult(bool Succeeded, string Message);
internal sealed record StudioUx9KApprovalSnapshot(StudioUx9KNarrativeDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9KSmokeResult(
    bool Succeeded,
    bool TwoRoutes,
    bool ExplicitSelection,
    bool InvalidBlocked,
    bool PermissionBlocked,
    bool ConfirmationRequired,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool QuestRuntime,
    bool EncounterRuntime,
    bool Compensation,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
