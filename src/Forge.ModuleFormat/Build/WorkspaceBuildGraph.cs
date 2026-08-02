using Forge.Domain.Build;

namespace Forge.ModuleFormat.Build;

public sealed record WorkspaceBuildGraph(
    BuildDependencyGraph Graph,
    IReadOnlyList<string> AutomaticDependencies);
