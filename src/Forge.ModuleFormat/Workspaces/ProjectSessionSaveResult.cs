namespace Forge.ModuleFormat.Workspaces;

public sealed record ProjectSessionSaveResult(
    bool Succeeded,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics);
