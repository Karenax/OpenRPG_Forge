using Forge.Application.Modules;
using Forge.Application.Projects;
using Forge.Domain.Modules;

namespace Forge.Studio.Session;

internal sealed record StudioUx6UVariableChoice(ProjectVariableId Id, string Label);

internal sealed record StudioUx6UDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    ProjectVariableId VariableId,
    string DefaultValue,
    ProjectVariableReplicationMode Replication,
    bool DebugVisible,
    bool RequiredAtRuntime,
    string RuntimeValue,
    BlackboardReplicationAudience Audience,
    ProjectVariableScope SnapshotScope);

internal sealed record StudioUx6UDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx6UPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6UDiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool TypedMutation,
    bool DebugSnapshot,
    bool ReplicationDelta,
    bool SaveScopeRoundTrip,
    bool StaleHandleRejected,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6UActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6UApprovalSnapshot(
    StudioUx6UDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6USmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleReadable,
    bool TypedValidation,
    bool SchemaValidation,
    bool CanonicalDryRun,
    bool TypedMutation,
    bool DebugSnapshot,
    bool ReplicationDelta,
    bool SaveScopeRoundTrip,
    bool StaleHandleRejected,
    bool LegacyBoundary,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool ModuleNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
