using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class TerrainProductionBenchmarkTests
{
    [Fact]
    public void RepresentativeExteriorPassesAllBudgets()
    {
        var report = TerrainProductionBenchmark.Evaluate(RepresentativeSample());

        Assert.True(report.Passed);
        Assert.Equal(TerrainProductionBenchmark.CurrentSchemaVersion, report.SchemaVersion);
        Assert.Equal(16, report.Checks.Count);
        Assert.All(report.Checks, check => Assert.True(check.Passed, check.Key));
    }

    [Fact]
    public void OverBudgetReconstructionAndMissingLodAreActionable()
    {
        var sample = RepresentativeSample() with
        {
            ReconstructionMilliseconds = 5_001,
            LodAssetCount = 21,
        };

        var report = TerrainProductionBenchmark.Evaluate(sample);

        Assert.False(report.Passed);
        Assert.Equal(
            ["lod-assets", "reconstruction"],
            report.Checks.Where(check => !check.Passed).Select(check => check.Key).ToArray());
    }

    private static TerrainProductionBenchmarkSample RepresentativeSample() => new(
        "representative-exterior",
        128,
        128,
        1_089,
        1_089,
        2_048,
        97,
        3,
        357,
        1,
        1_990,
        58,
        1,
        72,
        22,
        66,
        3,
        1_073_258,
        32 * 1024 * 1024,
        16 * 1024 * 1024,
        64 * 1024 * 1024,
        400,
        80,
        35,
        12,
        120,
        true);
}
