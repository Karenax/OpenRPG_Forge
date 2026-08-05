using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Tests.Serialization;
using Forge.Scripting;

namespace Forge.ModuleFormat.Tests.Quality;

public sealed class WorkspaceGovernanceLabServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Governance_lab_audits_accessibility_all_untrusted_boundaries_and_dependencies_deterministically()
    {
        Directory.CreateDirectory(_root);
        var service = new WorkspaceGovernanceLabService(new NativeForgeScriptRuntime());

        var first = service.Run(_root, TestProjectFactory.CreateValid());
        var second = service.Run(_root, TestProjectFactory.CreateValid());

        Assert.True(first.Passed);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.True(first.Accessibility.Passed);
        Assert.True(first.Dependencies.Passed);
        Assert.True(first.SecurityFuzz.Passed);
        Assert.Equal(7, first.SecurityFuzz.Boundaries.Count);
        Assert.Equal(112, first.SecurityFuzz.TotalCases);
        Assert.All(first.SecurityFuzz.Boundaries, boundary =>
        {
            Assert.Equal(16, boundary.Cases);
            Assert.Equal(0, boundary.UnexpectedFailures);
            Assert.Equal(0, boundary.PathEscapes);
        });
        Assert.Equal(WorkspaceGovernanceLabService.ReportRelativePath, first.ReportPath);
        Assert.True(File.Exists(Path.Combine(_root, ".forge", "quality", "governance-report.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
