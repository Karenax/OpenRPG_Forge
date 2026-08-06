using Forge.Application.Commands;

namespace Forge.Application.Assistant;

public sealed record AssistantWorkspaceProfileCommandResult(
    bool Succeeded,
    AssistantWorkspaceProfileState State,
    AssistantWorkspaceProfileRevision BaseRevision,
    AssistantWorkspaceProfileRevision? NewRevision,
    Guid PlanId,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
