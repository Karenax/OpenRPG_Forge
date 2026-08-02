using Forge.Domain.Build;

namespace Forge.ModuleFormat.Build;

public sealed record IncrementalBuildPlan(
    IReadOnlyList<string> ChangedPaths,
    IReadOnlyList<string> RemovedPaths,
    IReadOnlyList<BuildDependencyNode> RebuiltResources,
    IReadOnlyList<BuildDependencyNode> ReusedResources);
