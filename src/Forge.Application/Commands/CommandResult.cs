using Forge.Application.Projects;

namespace Forge.Application.Commands;

public sealed record CommandResult(
    bool Succeeded,
    ProjectState State,
    ProjectRevision BaseRevision,
    ProjectRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
