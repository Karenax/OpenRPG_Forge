namespace Forge.Studio.Session;

internal sealed record StudioUx9CWorldAdministrationDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    string TemplateIds,
    int MaximumInstances,
    int MaximumPlayersPerInstance,
    int GarbageCollectionMinutes,
    string AllowedOverrideKeys,
    int MaximumEntities,
    int MaximumResourceMegabytes,
    int SessionLifetimeMinutes,
    bool OAuthEnabled,
    bool AnonymousEnabled,
    int RequestsPerMinute,
    string SafeCommandIds,
    int ModerationRetentionDays);

internal sealed record StudioUx9CProbeSnapshot(
    string Status,
    string InstanceCode,
    int InstancePlayers,
    string DynamicAreaCode,
    string AuthenticationCode,
    bool NonEnumeratingFailure,
    string AdministrationCode,
    bool UnsafeCommandBlocked,
    string ModerationCode,
    int ModerationAuditEntries,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9CWorldAdministrationPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool InstancesRuntime,
    bool DynamicAreasRuntime,
    bool AuthenticationRuntime,
    bool AdministrationRuntime,
    bool ModerationRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9CProbeSnapshot Probe);

internal sealed record StudioUx9CActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9CApprovalSnapshot(
    StudioUx9CWorldAdministrationDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9CSmokeResult(
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
    bool InstancesRuntime,
    bool DynamicAreasRuntime,
    bool AuthenticationRuntime,
    bool AdministrationRuntime,
    bool ModerationRuntime,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
