using Forge.Application.Commands;

namespace Forge.Application.Assets;

public sealed record AssetProcessingProfileCommandResult(
    bool Succeeded,
    AssetProcessingProfileState State,
    AssetProcessingProfileRevision BaseRevision,
    AssetProcessingProfileRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
