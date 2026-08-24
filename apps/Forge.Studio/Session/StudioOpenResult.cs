using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed record StudioOpenResult(
    bool Succeeded,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics);
