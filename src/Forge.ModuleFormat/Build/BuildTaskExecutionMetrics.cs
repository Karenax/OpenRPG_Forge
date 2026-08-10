namespace Forge.ModuleFormat.Build;

public sealed record BuildTaskExecutionMetrics(
    int TaskCount,
    int MaximumParallelism,
    int PeakParallelism,
    double DurationMilliseconds,
    string OrderFingerprint);
