using Forge.Application.Commands;

namespace Forge.Application.Assets;

public sealed record ExternalWorkerSecurityPolicyCommandResult(
    bool Succeeded,
    ExternalWorkerSecurityPolicyState State,
    ExternalWorkerSecurityPolicyRevision BaseRevision,
    ExternalWorkerSecurityPolicyRevision? NewRevision,
    Guid PlanId,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
