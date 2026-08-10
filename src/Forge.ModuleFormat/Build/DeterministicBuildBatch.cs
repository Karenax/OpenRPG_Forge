namespace Forge.ModuleFormat.Build;

public sealed record DeterministicBuildBatch<T>(
    IReadOnlyList<T> Results,
    BuildTaskExecutionMetrics Metrics);
