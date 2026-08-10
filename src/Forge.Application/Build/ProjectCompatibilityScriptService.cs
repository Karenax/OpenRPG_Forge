using Forge.Domain.Build;

namespace Forge.Application.Build;

public sealed class ProjectCompatibilityScriptService
{
    public ProjectCompatibilityScriptHandle Read(WorkspaceBuildProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var compatibility = profile.Compatibility;
        return new(
            compatibility.Id,
            compatibility.SchemaVersion,
            compatibility.Provenance,
            compatibility.MinimumRuntimeVersion,
            compatibility.ProjectFormatVersion,
            compatibility.AutomaticMigrations,
            compatibility.RequiredAtRuntime);
    }
}

public sealed record ProjectCompatibilityScriptHandle(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string MinimumRuntimeVersion,
    string ProjectFormatVersion,
    bool AutomaticMigrations,
    bool RequiredAtRuntime);
