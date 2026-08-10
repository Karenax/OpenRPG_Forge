using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Quality;

namespace Forge.ModuleFormat.Tests.Quality;

public sealed class WorkspaceQualityAcceptanceEvidenceValidatorTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-quality-acceptance", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Repository_evidence_covers_fixtures_tracker_and_acceptance_gates()
    {
        var quality = QualityAssuranceProfile.CreateDefault(Guid.Parse("c0030000-0000-4000-8000-000000000100"));

        var report = WorkspaceQualityAcceptanceEvidenceValidator.Validate(FindRepositoryRoot(), quality);

        Assert.True(report.Passed, string.Join(Environment.NewLine,
            report.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(7, report.FixtureCount);
        Assert.Equal(717, report.FeatureCount);
        Assert.Equal(717, report.GateAssignmentCount);
    }

    [Fact]
    public void Hostile_payload_probes_reject_huge_arrays_injection_and_invalid_assets()
    {
        var policy = MaliciousModuleFixturePolicy.CreateDefault(
            Guid.Parse("c0030000-0000-4000-8000-000000000100"));

        Assert.Equal(MaliciousModuleFixtureProbe.HugeArrayCode,
            MaliciousModuleFixtureProbe.InspectHugeArray(65_537, policy)?.Code);
        Assert.Equal(MaliciousModuleFixtureProbe.InjectionTextCode,
            MaliciousModuleFixtureProbe.InspectInjectionText("<script>forge.inject()</script>", policy)?.Code);
        Assert.Equal(MaliciousModuleFixtureProbe.InvalidAssetCode,
            MaliciousModuleFixtureProbe.InspectAssetHeader([0xde, 0xad, 0xbe, 0xef], policy)?.Code);
        Assert.Null(MaliciousModuleFixtureProbe.InspectHugeArray(64, policy));
    }

    [Fact]
    public void Missing_repository_returns_stable_localized_diagnostic()
    {
        Directory.CreateDirectory(_temporaryRoot);
        var quality = QualityAssuranceProfile.CreateDefault(Guid.NewGuid());

        var report = WorkspaceQualityAcceptanceEvidenceValidator.Validate(_temporaryRoot, quality);

        Assert.False(report.Passed);
        Assert.Equal(WorkspaceQualityAcceptanceEvidenceValidator.RepositoryRootInvalidCode,
            Assert.Single(report.Diagnostics).Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenRPGForge.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
