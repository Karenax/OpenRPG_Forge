using Forge.Domain.Projects;

namespace Forge.ModuleFormat.Packages;

public sealed record ModulePackageValidationResult(
    ModuleManifest? Manifest,
    ProjectDefinition? Project,
    IReadOnlyList<ModulePackageDiagnostic> Diagnostics)
{
    public bool IsValid => Manifest is not null && Project is not null && Diagnostics.Count == 0;
}
