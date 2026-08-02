using Forge.Domain.Build;
using Forge.ModuleFormat.Build;

namespace Forge.ModuleFormat.Packages;

public sealed record ModulePackageExportResult(
    string PackagePath,
    ModuleManifest Manifest,
    int FileCount,
    long ArchiveBytes,
    BuildDependencyGraph DependencyGraph,
    IncrementalBuildPlan BuildPlan);
