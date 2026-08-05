using Forge.Application.Validation;
using Forge.Application.Tests.Entities;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Application.Tests.Validation;

public sealed class ProjectHealthReportServiceTests
{
    [Fact]
    public void Project_report_covers_domains_and_accepts_attached_test_evidence()
    {
        var project = new PlaceEntityTestContext().State.Project;
        var evidence = new ProjectHealthSignal("tests", ProjectHealthCategory.Tests,
            ProjectHealthSeverity.Information, "health.tests.pass", "659 tests passed.",
            "health/tests/gate", true);

        var report = ProjectHealthReportService.Evaluate(project,
            new DateTimeOffset(2026, 8, 5, 18, 0, 0, TimeSpan.Zero), [evidence]);

        Assert.Equal(ProjectHealthStatus.Healthy, report.Status);
        Assert.True(report.BuildAllowed);
        Assert.Equal(64, report.Fingerprint.Length);
        Assert.Equal(project.Module.RuntimePolicies.Extensions.OperationalReadiness.ProjectHealth.Domains,
            report.Domains.Select(item => item.Domain));
    }

    [Fact]
    public void Failed_error_is_unhealthy_stable_and_blocks_configured_build()
    {
        var policy = OperationalReadinessProfile.CreateDefault(Guid.NewGuid()).ProjectHealth;
        var failed = new ProjectHealthSignal("build", ProjectHealthCategory.Coverage,
            ProjectHealthSeverity.Error, "health.coverage.failed", "Coverage gate failed.",
            "tracking/coverage", false);
        var passed = new ProjectHealthSignal("tests", ProjectHealthCategory.Tests,
            ProjectHealthSeverity.Information, "health.tests.pass", "Tests passed.", "health/tests", true);

        var first = ProjectHealthReportService.Build(policy, [failed, passed], DateTimeOffset.UnixEpoch);
        var second = ProjectHealthReportService.Build(policy, [passed, failed], DateTimeOffset.UnixEpoch);

        Assert.Equal(ProjectHealthStatus.Unhealthy, first.Status);
        Assert.False(first.BuildAllowed);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }
}
