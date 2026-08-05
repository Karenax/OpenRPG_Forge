using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Tests.Serialization;

namespace Forge.ModuleFormat.Tests.Quality;

public sealed class WorkspacePerformanceLabServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Workspace_performance_lab_is_deterministic_and_writes_confined_review_artifacts()
    {
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        File.WriteAllText(Path.Combine(_root, "scripts", "profile.forge"), "void main() {}\n");
        var project = TestProjectFactory.CreateValid();
        var service = new WorkspacePerformanceLabService();

        var written = service.Run(_root, project);
        var readOnly = service.Run(_root, project, new WorkspacePerformanceLabOptions(false));

        Assert.True(written.Passed);
        Assert.Equal(written.Fingerprint, readOnly.Fingerprint);
        Assert.Equal(9, written.Network.Scenarios.Count);
        Assert.Equal(9, written.Benchmarks.Measurements.Count);
        Assert.Equal(8, written.VisualReviews.Cases.Count);
        Assert.Equal(Enum.GetValues<QualityTraceSurface>(),
            written.TraceSpans.Select(item => item.Surface).Distinct().Order());
        Assert.Contains(written.TraceSpans, item => item.Category == "database");
        Assert.Contains(written.TraceSpans, item => item.Category == "script");
        Assert.Contains(written.TraceSpans, item => item.Category == "network");
        Assert.True(File.Exists(Path.Combine(_root, written.PerformanceReportPath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(_root, written.VisualManifestPath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(_root, written.TracePath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Null(readOnly.PerformanceReportPath);
        Assert.Null(readOnly.VisualManifestPath);
        Assert.Null(readOnly.TracePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
