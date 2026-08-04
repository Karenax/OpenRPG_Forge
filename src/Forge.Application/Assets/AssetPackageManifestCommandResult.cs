using Forge.Application.Commands;

namespace Forge.Application.Assets;

public sealed record AssetPackageManifestCommandResult(
    bool Succeeded,
    AssetPackageManifestState State,
    AssetPackageManifestRevision BaseRevision,
    AssetPackageManifestRevision? NewRevision,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
