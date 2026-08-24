using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed record StudioUx6KCapabilityChoice(
    string Id,
    string Label,
    string Authority,
    string Quota,
    IReadOnlyList<string> ApiNames);

internal sealed record StudioUx6KPolicyDraft(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ApiVersion,
    Guid ModuleId,
    bool DenyByDefault,
    bool ServerEnforced,
    bool RequireSignedDistribution,
    bool AllowUnsignedDevelopment,
    bool RequiredAtRuntime,
    IReadOnlySet<string> AllowedCapabilities,
    IReadOnlyList<ForgeScriptKitCapabilityGrant> KitGrants,
    IReadOnlyList<ForgeScriptPermissionEventBinding> EventBindings,
    long BaseRevision,
    string SourceFingerprint);

internal sealed record StudioUx6KPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    string SemanticDiff,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6KSimulation(
    bool Succeeded,
    string Message,
    bool TrustedServerExecuted,
    bool CapabilityIntersection,
    bool UndeclaredCapabilityDenied,
    bool UntrustedSignatureDenied,
    bool DevelopmentPolicyHonored,
    bool ClientAuthorityDenied,
    bool ModuleMismatchDenied,
    bool MissingOptionalIgnored,
    bool MissingRequiredDiagnosed,
    bool QuotaAbuseBlocked,
    bool RuntimeRecovered,
    bool CanonicalSourceCompiled,
    IReadOnlyList<string> Events,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6KActionResult(
    bool Succeeded,
    string Message,
    bool CanUndo,
    bool CanRedo);

internal sealed record StudioUx6KApprovalSnapshot(
    StudioUx6KPolicyDraft? Draft,
    long? Revision,
    string? CandidateFingerprint);

internal sealed record StudioUx6KSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6FApprovalSnapshot ScriptApproval,
    StudioUx6JApprovalSnapshot ExecutionPolicyApproval,
    StudioUx6KApprovalSnapshot PermissionApproval,
    bool PermissionFileExisted,
    byte[]? PermissionFileBytes);

internal sealed record StudioUx6KSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanCapabilities,
    bool CanonicalManifest,
    bool DenyByDefault,
    bool ServerEnforced,
    bool SignaturePolicy,
    bool KitBindingIntersection,
    bool AuthoritySandbox,
    bool InvalidManifestBlocked,
    bool CanonicalDryRun,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool SourcePreserved,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
