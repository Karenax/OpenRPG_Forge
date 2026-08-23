namespace Forge.Studio.Session;

internal sealed record StudioUx9DPersistenceObservabilityDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    int BackupRetentionDays,
    string AllowedOperationIds,
    string Metrics,
    string HealthEndpoint,
    string MetricsEndpoint,
    int MaximumCapturedSpans,
    int ReconnectionGraceSeconds);

internal sealed record StudioUx9DProbeSnapshot(
    string Status,
    long SnapshotRevision,
    string DataCode,
    int DataRows,
    bool UnlistedOperationBlocked,
    int MetricSignals,
    string HealthStatus,
    bool UnlistedMetricBlocked,
    string ProfileOperation,
    int ProfileCaptures,
    string ReconnectionCode,
    bool ReconnectionReplayBlocked,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9DPersistenceObservabilityPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool PersistenceRuntime,
    bool SqlRuntime,
    bool ObservabilityRuntime,
    bool ProfilerRuntime,
    bool ReconnectionRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9DProbeSnapshot Probe);

internal sealed record StudioUx9DActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9DApprovalSnapshot(
    StudioUx9DPersistenceObservabilityDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9DSmokeResult(
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
    bool PersistenceRuntime,
    bool SqlRuntime,
    bool ObservabilityRuntime,
    bool ProfilerRuntime,
    bool ReconnectionRuntime,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
