using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssetPackageMigrationStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "migration-store", Guid.NewGuid().ToString("N"));
    private static readonly AssetPackageManifestId PackageId = AssetPackageManifestId.From(Guid.Parse("c1100000-0000-4000-8000-000000000110"));
    private static readonly Guid StepId = Guid.Parse("c1100000-0000-4000-8000-000000000111");

    [Fact]
    public void TrustedMigrationWritesBackupChangelogAndCanRestore()
    {
        var relative = "assets/package/data.txt";
        var path = Path.Combine(_root, "assets", "package", "data.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "v1");
        var installation = new AssetPackageInstallation(PackageId, "1.0.0", [], [relative]);
        var step = new AssetPackageMigrationStep(StepId, PackageId, "1.0.0", "2.0.0", true, true, "Upgrade fixture.");
        var plan = AssetPackageMigrationPlanner.Plan(installation, "2.0.0", [step], dryRun: false);

        var result = AssetPackageMigrationStore.Execute(_root, installation, plan, new ReplaceHostStep("v2"));

        Assert.True(result.Succeeded);
        Assert.Equal("2.0.0", result.Installation.Version);
        Assert.Equal("v2", File.ReadAllText(path));
        Assert.True(File.Exists(Path.Combine(_root, result.ChangelogRelativePath!)));
        AssetPackageMigrationStore.RestoreBackup(_root, result.BackupRelativePath!, [relative]);
        Assert.Equal("v1", File.ReadAllText(path));
    }

    [Fact]
    public void FailedTrustedMigrationRollsBackAutomatically()
    {
        var relative = "assets/package/data.txt";
        var path = Path.Combine(_root, "assets", "package", "data.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "v1");
        var installation = new AssetPackageInstallation(PackageId, "1.0.0", [], [relative]);
        var step = new AssetPackageMigrationStep(StepId, PackageId, "1.0.0", "2.0.0", true, true, "Fail fixture.");
        var plan = AssetPackageMigrationPlanner.Plan(installation, "2.0.0", [step], dryRun: false);

        var result = AssetPackageMigrationStore.Execute(_root, installation, plan, new FailingHostStep());

        Assert.False(result.Succeeded);
        Assert.Equal(AssetPackageMigrationStore.ApplyFailedCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal("v1", File.ReadAllText(path));
        Assert.Equal("1.0.0", result.Installation.Version);
    }

    [Fact]
    public void DryRunAndUnsafePathsNeverWriteMigrationArtifacts()
    {
        var relative = "assets/package/data.txt";
        var path = Path.Combine(_root, "assets", "package", "data.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "v1");
        var installation = new AssetPackageInstallation(PackageId, "1.0.0", [], [relative]);
        var step = new AssetPackageMigrationStep(StepId, PackageId, "1.0.0", "2.0.0", true, true, "Dry-run fixture.");
        var dryRun = AssetPackageMigrationPlanner.Plan(installation, "2.0.0", [step], dryRun: true);

        var preview = AssetPackageMigrationStore.Execute(_root, installation, dryRun, new ReplaceHostStep("v2"));
        var unsafeInstallation = installation with { InstalledFiles = ["../outside.txt"] };
        var unsafePlan = AssetPackageMigrationPlanner.Plan(unsafeInstallation, "2.0.0", [step], dryRun: false);
        var refused = AssetPackageMigrationStore.Execute(_root, unsafeInstallation, unsafePlan, new ReplaceHostStep("v2"));

        Assert.True(preview.Succeeded);
        Assert.True(preview.DryRun);
        Assert.Equal("v1", File.ReadAllText(path));
        Assert.False(Directory.Exists(Path.Combine(_root, ".forge")));
        Assert.False(refused.Succeeded);
        Assert.Equal(AssetPackageMigrationStore.PlanInvalidCode, Assert.Single(refused.Diagnostics).Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class ReplaceHostStep(string value) : IAssetPackageMigrationHostStep
    {
        public Guid StepId => AssetPackageMigrationStoreTests.StepId;
        public void Apply(string workspaceRoot, IReadOnlyList<string> installedRelativePaths) =>
            File.WriteAllText(Path.Combine(workspaceRoot, installedRelativePaths.Single()), value);
    }

    private sealed class FailingHostStep : IAssetPackageMigrationHostStep
    {
        public Guid StepId => AssetPackageMigrationStoreTests.StepId;
        public void Apply(string workspaceRoot, IReadOnlyList<string> installedRelativePaths)
        {
            File.WriteAllText(Path.Combine(workspaceRoot, installedRelativePaths.Single()), "broken");
            throw new InvalidOperationException("fixture failure");
        }
    }
}
