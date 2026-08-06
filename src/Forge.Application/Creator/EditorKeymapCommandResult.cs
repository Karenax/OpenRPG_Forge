using Forge.Application.Commands;

namespace Forge.Application.Creator;

public sealed record EditorKeymapCommandResult(
    bool Succeeded,
    EditorKeymapState State,
    EditorKeymapRevision BaseRevision,
    EditorKeymapRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
