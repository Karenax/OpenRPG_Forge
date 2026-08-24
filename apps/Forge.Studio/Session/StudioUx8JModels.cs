using Forge.Application.Assets;
using Forge.Application.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;

namespace Forge.Studio.Session;

internal sealed record StudioUx8JWorkerDraft(
    Guid ProjectId,
    string ProjectLabel,
    ExternalWorkerSecurityPolicyRevision BaseRevision,
    string SourceFingerprint,
    int MaximumJobSeconds,
    long MaximumOutputBytes,
    IReadOnlyList<AssetExternalOperation> AllowedOperations,
    AssistantPermissionLevel Permission,
    bool Consent);

internal sealed record StudioUx8JInjectionDraft(
    Guid ProjectId,
    string ProjectLabel,
    AssistantWorkspaceProfileRevision BaseRevision,
    string SourceFingerprint,
    int MaximumUntrustedCharacters,
    IReadOnlyList<string> SuspiciousPatterns,
    bool Consent);

internal sealed record StudioUx8JSecurityPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool ScopeBounded,
    bool DenialsVerified,
    bool AuditReady,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx8JActionResult(bool Succeeded, string Message);

internal sealed record StudioUx8JApprovalSnapshot(
    StudioUx8JWorkerDraft? WorkerDraft,
    ExternalWorkerSecurityPolicyRevision? WorkerRevision,
    string? WorkerSourceFingerprint,
    string? WorkerDraftFingerprint,
    Guid? WorkerPlanId,
    StudioUx8JInjectionDraft? InjectionDraft,
    AssistantWorkspaceProfileRevision? InjectionRevision,
    string? InjectionSourceFingerprint,
    string? InjectionDraftFingerprint,
    Guid? InjectionPlanId);

internal sealed record StudioUx8JSmokeResult(
    bool Succeeded,
    bool TwoRoutes,
    bool ScopeBounded,
    bool ConsentRequired,
    bool PythonShellEvalNetworkDenied,
    bool PromptInjectionDetected,
    bool ToolAndEscalationDenied,
    bool CanonicalDryRun,
    bool CanonicalCommands,
    bool AuditRecorded,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
