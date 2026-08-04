using Forge.Application.Commands;

namespace Forge.Application.Assets;

public sealed record AssetExternalEditCommandResult(
    bool Succeeded,
    AssetExternalEditState State,
    AssetExternalEditRevision BaseRevision,
    AssetExternalEditRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
