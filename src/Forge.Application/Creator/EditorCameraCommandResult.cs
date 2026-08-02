using Forge.Application.Commands;

namespace Forge.Application.Creator;

public sealed record EditorCameraCommandResult(
    bool Succeeded,
    EditorCameraState State,
    EditorCameraRevision BaseRevision,
    EditorCameraRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
