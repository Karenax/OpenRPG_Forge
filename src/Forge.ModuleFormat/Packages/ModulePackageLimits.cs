namespace Forge.ModuleFormat.Packages;

public sealed record ModulePackageLimits(
    int MaximumFileCount = 2_048,
    long MaximumFileBytes = 128 * 1024 * 1024,
    long MaximumTotalBytes = 512 * 1024 * 1024,
    double MaximumCompressionRatio = 100);
