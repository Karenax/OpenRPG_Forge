using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.ModuleFormat.Packages;

public sealed record ModuleManifest(
    string FormatVersion,
    ModuleId ModuleId,
    string Slug,
    string ModuleVersion,
    string RuntimeMinimumVersion,
    string ForgeScriptApiVersion,
    AreaId EntryAreaId,
    IReadOnlyList<string> Dependencies,
    string ContentSha256)
{
    public const string CurrentFormatVersion = "1.0.0";

    public static ModuleManifest Create(
        ProjectDefinition project,
        string moduleVersion,
        string runtimeMinimumVersion,
        string forgeScriptApiVersion,
        string contentSha256,
        IReadOnlyList<string>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ModuleManifest(
            CurrentFormatVersion,
            project.Module.Id,
            project.Module.Slug,
            moduleVersion,
            runtimeMinimumVersion,
            forgeScriptApiVersion,
            project.Module.EntryAreaId,
            dependencies ?? [],
            contentSha256);
    }
}
