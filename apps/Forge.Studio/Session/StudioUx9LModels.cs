namespace Forge.Studio.Session;

internal sealed record StudioUx9LAccessDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    bool RequiredAtRuntime,
    int SessionLifetimeMinutes,
    int InspectionHistoryLimit,
    int PlayerRefreshSeconds,
    int AuditRetentionDays,
    int ChatMessageLimit);

internal sealed record StudioUx9LAccessProbe(
    string Status,
    string AuthenticationCode,
    bool SessionActive,
    string InspectionCode,
    int InspectionSections,
    int Players,
    int LatencyMs,
    int AuditEntries,
    bool AuditVerified,
    int ChatMessages,
    string LastChannel,
    string PermissionCode,
    bool ScriptHandles,
    bool ResourcesReleased);

internal sealed record StudioUx9LAccessPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool RuntimeReady,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9LAccessProbe Probe);

internal sealed record StudioUx9LActionResult(bool Succeeded, string Message);
internal sealed record StudioUx9LApprovalSnapshot(StudioUx9LAccessDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9LSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
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
    bool Authentication,
    bool Inspection,
    bool Players,
    bool Audit,
    bool Chat,
    bool ScriptHandles,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
