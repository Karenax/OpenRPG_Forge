using Forge.Application.Commands;

namespace Forge.Application.Projects;

public sealed record NewModuleResult(
    bool Succeeded,
    ProjectState? State,
    ProjectRevision BaseRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
