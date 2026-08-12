using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record AreaStreamingBenchmarkDiagnostic(
    string Code,
    string Message,
    string Path,
    Guid ProfileId);

public sealed record AreaStreamingBenchmarkEvaluation(
    bool Passed,
    int TargetMilliseconds,
    double ObservedMilliseconds,
    IReadOnlyList<AreaStreamingBenchmarkDiagnostic> Diagnostics);

public static class AreaStreamingBenchmarkGate
{
    public const string TargetExceededCode = "area.loading.target_exceeded";

    public static AreaStreamingBenchmarkEvaluation Evaluate(
        AreaLoadingProfile profile,
        AreaStreamingResult result)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(result);

        var observed = result.Elapsed.TotalMilliseconds;
        if (result.TargetMet && observed <= profile.TargetLoadMilliseconds)
        {
            return new(true, profile.TargetLoadMilliseconds, observed, []);
        }

        return new(false, profile.TargetLoadMilliseconds, observed,
        [
            new AreaStreamingBenchmarkDiagnostic(
                TargetExceededCode,
                $"Area loading took {observed:0.###} ms and exceeded the {profile.TargetLoadMilliseconds} ms target.",
                "area.world.loading.targetLoadMilliseconds",
                profile.Id),
        ]);
    }
}
