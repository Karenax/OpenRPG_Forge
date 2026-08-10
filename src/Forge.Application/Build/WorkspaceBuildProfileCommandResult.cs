using Forge.Application.Commands;

namespace Forge.Application.Build;

public sealed record WorkspaceBuildProfileCommandResult(
    bool Succeeded,
    WorkspaceBuildProfileState State,
    WorkspaceBuildProfileRevision BaseRevision,
    WorkspaceBuildProfileRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
