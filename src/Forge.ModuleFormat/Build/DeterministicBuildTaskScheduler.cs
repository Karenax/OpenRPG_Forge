using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Forge.ModuleFormat.Build;

public static class DeterministicBuildTaskScheduler
{
    public const int DefaultMaximumParallelism = 4;
    public const int MaximumSupportedParallelism = 32;

    public static DeterministicBuildBatch<T> Execute<T>(
        IEnumerable<string> taskIds,
        int maximumParallelism,
        Func<string, T> execute)
    {
        ArgumentNullException.ThrowIfNull(taskIds);
        ArgumentNullException.ThrowIfNull(execute);
        if (maximumParallelism is < 1 or > MaximumSupportedParallelism)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumParallelism),
                $"Build parallelism must be between 1 and {MaximumSupportedParallelism}.");
        }

        var ordered = taskIds
            .Select(taskId => taskId?.Trim() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(string.IsNullOrWhiteSpace) || ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException("Build task IDs must be non-empty and unique.", nameof(taskIds));
        }

        var results = new T[ordered.Length];
        var active = 0;
        var peak = 0;
        var started = Stopwatch.GetTimestamp();
        Parallel.For(0, ordered.Length, new ParallelOptions { MaxDegreeOfParallelism = maximumParallelism }, index =>
        {
            var current = Interlocked.Increment(ref active);
            ObservePeak(ref peak, current);
            try
            {
                results[index] = execute(ordered[index]);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });

        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', ordered))));
        return new(results, new BuildTaskExecutionMetrics(
            ordered.Length,
            maximumParallelism,
            peak,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            fingerprint));
    }

    private static void ObservePeak(ref int peak, int current)
    {
        while (true)
        {
            var observed = Volatile.Read(ref peak);
            if (observed >= current || Interlocked.CompareExchange(ref peak, current, observed) == observed)
            {
                return;
            }
        }
    }
}
