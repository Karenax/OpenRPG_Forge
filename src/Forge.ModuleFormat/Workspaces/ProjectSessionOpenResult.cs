namespace Forge.ModuleFormat.Workspaces;

public sealed record ProjectSessionOpenResult(
    ProjectSession? Session,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Session is not null && Diagnostics.Count == 0;
}
