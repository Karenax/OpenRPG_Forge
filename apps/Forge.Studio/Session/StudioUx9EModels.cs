namespace Forge.Studio.Session;

internal sealed record StudioUx9EResilienceCapacityDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    int SnapshotIntervalMinutes,
    int BackupRetentionDays,
    int MaximumPlayers,
    int ReservedAdministratorSlots,
    int ReservationSeconds);

internal sealed record StudioUx9EResilienceCapacityProbe(
    string Status,
    long SnapshotRevision,
    string RollbackPreviewCode,
    string RollbackApplyCode,
    long RestoredRevision,
    int PublicCapacity,
    int ReservedAdministratorSlots,
    int FirstQueuedPosition,
    int SecondQueuedPosition,
    string PromotedCode,
    int QueueDepth,
    bool LoadSheddingEnabled,
    bool MetricsEnabled,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9EResilienceCapacityPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool RollbackRuntime,
    bool CapacityRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9EResilienceCapacityProbe Probe);

internal sealed record StudioUx9EActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9EApprovalSnapshot(
    StudioUx9EResilienceCapacityDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9ESmokeResult(
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
    bool RollbackRuntime,
    bool CapacityRuntime,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
