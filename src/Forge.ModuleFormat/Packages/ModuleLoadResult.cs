using Forge.Domain.Projects;

namespace Forge.ModuleFormat.Packages;

public sealed record ModuleLoadResult(
    ModuleManifest Manifest,
    ProjectDefinition Project,
    string CachePath,
    bool CacheHit);
