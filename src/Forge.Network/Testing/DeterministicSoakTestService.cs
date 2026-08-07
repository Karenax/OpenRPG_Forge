using Forge.Domain.RuntimeExtensions;

namespace Forge.Network.Testing;

public sealed record SoakTestSample(int ElapsedMinutes, int MemoryMegabytes, int OpenHandles,
    double P95LatencyMilliseconds, bool DatabaseHealthy, int ConnectedPlayers);

public sealed record SoakTestReport(bool Passed, string Code, int DurationMinutes, int Samples,
    int MemoryGrowthMegabytes, int HandleGrowth, double MaximumP95LatencyMilliseconds,
    int MinimumConnectedPlayers, IReadOnlyList<string> Diagnostics);

public static class DeterministicSoakTestService
{
    public static SoakTestReport Evaluate(ProductionAutomationProfile.SoakTestSettings policy,
        IReadOnlyList<SoakTestSample> samples)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(samples);
        if (!policy.Enabled)
            return new(false, "SOAK_DISABLED", 0, 0, 0, 0, 0, 0, ["Soak testing is disabled by policy."]);
        var ordered = samples.OrderBy(item => item.ElapsedMinutes).ToArray();
        if (ordered.Length < 2 || ordered[0].ElapsedMinutes != 0 ||
            ordered.Select(item => item.ElapsedMinutes).Distinct().Count() != ordered.Length)
            return new(false, "SOAK_SAMPLES_INVALID", 0, ordered.Length, 0, 0, 0, 0,
                ["Samples require a unique zero-minute baseline and a final sample."]);
        var diagnostics = new List<string>();
        var duration = ordered[^1].ElapsedMinutes;
        var memoryGrowth = ordered.Max(item => item.MemoryMegabytes) - ordered[0].MemoryMegabytes;
        var handleGrowth = ordered.Max(item => item.OpenHandles) - ordered[0].OpenHandles;
        var latency = ordered.Max(item => item.P95LatencyMilliseconds);
        var players = ordered.Min(item => item.ConnectedPlayers);
        if (duration < policy.DurationMinutes) diagnostics.Add($"SOAK_DURATION_SHORT:{duration}<{policy.DurationMinutes}");
        if (memoryGrowth > policy.MaximumMemoryGrowthMegabytes)
            diagnostics.Add($"SOAK_MEMORY_GROWTH:{memoryGrowth}>{policy.MaximumMemoryGrowthMegabytes}");
        if (handleGrowth > policy.MaximumHandleGrowth)
            diagnostics.Add($"SOAK_HANDLE_GROWTH:{handleGrowth}>{policy.MaximumHandleGrowth}");
        if (latency > policy.MaximumP95LatencyMilliseconds)
            diagnostics.Add(FormattableString.Invariant(
                $"SOAK_LATENCY:{latency:0.###}>{policy.MaximumP95LatencyMilliseconds:0.###}"));
        if (players < policy.VirtualPlayers) diagnostics.Add($"SOAK_PLAYERS:{players}<{policy.VirtualPlayers}");
        if (policy.AssertDatabaseHealth && ordered.Any(item => !item.DatabaseHealthy))
            diagnostics.Add("SOAK_DATABASE_UNHEALTHY");
        return new(diagnostics.Count == 0, diagnostics.Count == 0 ? "SOAK_PASS" : "SOAK_FAILED", duration,
            ordered.Length, memoryGrowth, handleGrowth, latency, players, diagnostics);
    }
}
