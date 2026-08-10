using Forge.Domain.Build;
using Forge.ModuleFormat.Build;

namespace Forge.ModuleFormat.Tests.Build;

public sealed class BuildGovernanceEvidenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-governance-evidence", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RepositoryEvidenceCoversWorktreesCiFixturesVersionsAndAdrs()
    {
        var diagnostics = WorkspaceBuildGovernanceEvidenceValidator.Validate(
            FindRepositoryRoot(), WorkspaceBuildGovernanceProfile.CreateDefault());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void MissingRepositoryEvidenceReturnsStableLocalizedDiagnostics()
    {
        Directory.CreateDirectory(_temporaryRoot);

        var diagnostics = WorkspaceBuildGovernanceEvidenceValidator.Validate(
            _temporaryRoot, WorkspaceBuildGovernanceProfile.CreateDefault());

        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceEvidenceValidator.WorktreeEvidenceMissingCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceEvidenceValidator.CiEvidenceMissingCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceEvidenceValidator.FixtureEvidenceMissingCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceEvidenceValidator.VersioningEvidenceMissingCode);
        Assert.Contains(diagnostics, item => item.Code == WorkspaceBuildGovernanceEvidenceValidator.AdrEvidenceMissingCode);
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
