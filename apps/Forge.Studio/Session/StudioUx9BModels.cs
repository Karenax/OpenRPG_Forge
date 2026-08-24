using Forge.Network.Operations;

namespace Forge.Studio.Session;

internal sealed record StudioUx9BOperationsDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    int ChunkKilobytes,
    int MaximumCacheMegabytes,
    bool DeltaEnabled,
    string AllowedLicenses,
    string ModuleId,
    string ModuleVersion,
    string ProtocolVersion,
    string ContentFingerprint,
    NetworkRole EditedRole,
    string Capabilities,
    string CustomCapabilities,
    bool ZoneScoped,
    int LocalRadius,
    int MaximumCharacters,
    string AllowedLanguages,
    string FilterToken,
    string FilterReplacement,
    bool AllowGlobal,
    bool AllowSystem,
    int MaximumPendingEvents,
    string EventKind,
    string SubscriberId,
    int EventPriority);

internal sealed record StudioUx9BProbeSnapshot(
    string Status,
    string ContentCode,
    int ContentChunks,
    bool CacheReused,
    string VersionCode,
    string PermissionCode,
    string ChatCode,
    int ChatMessages,
    int EventHandlers,
    int EventTraces,
    bool ReplayBlocked,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9BOperationsPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool ContentRuntime,
    bool VersionRuntime,
    bool RolesRuntime,
    bool ChatRuntime,
    bool EventsRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9BProbeSnapshot Probe);

internal sealed record StudioUx9BActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9BApprovalSnapshot(
    StudioUx9BOperationsDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9BSmokeResult(
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
    bool ContentRuntime,
    bool VersionRuntime,
    bool RolesRuntime,
    bool ChatRuntime,
    bool EventsRuntime,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
