using Forge.Domain.Assets;

namespace Forge.Domain.Tests.Assets;

public sealed class AssetPackageLifecycleTests
{
    [Fact]
    public void MigrationDryRunRequiresTrustedStepAndBackup()
    {
        var installation = Installation(PackageA, "1.0.0", ["assets/a.glb"]);
        var step = new AssetPackageMigrationStep(
            Guid.Parse("c1100000-0000-4000-8000-000000000001"),
            PackageA,
            "1.0.0",
            "2.0.0",
            true,
            true,
            "Rebuild platform variants.");

        var plan = AssetPackageMigrationPlanner.Plan(installation, "2.0.0", [step], dryRun: true);

        Assert.True(plan.CanApply);
        Assert.True(plan.DryRun);
        Assert.Equal(["assets/a.glb"], plan.BackupFiles);
        Assert.Empty(plan.Diagnostics);
    }

    [Fact]
    public void MigrationRejectsUntrustedHostCode()
    {
        var installation = Installation(PackageA, "1.0.0", ["assets/a.glb"]);
        var step = new AssetPackageMigrationStep(
            Guid.NewGuid(), PackageA, "1.0.0", "2.0.0", false, true, "Unsafe script.");

        var plan = AssetPackageMigrationPlanner.Plan(installation, "2.0.0", [step], dryRun: true);

        Assert.False(plan.CanApply);
        Assert.Contains(plan.Diagnostics, item => item.Code == AssetPackageMigrationPlanner.StepUntrustedCode);
    }

    [Fact]
    public void UninstallRefusesDependentsUntilInstalledReplacementExists()
    {
        var packageA = Installation(PackageA, "1.0.0", ["assets/a.glb"]);
        var dependent = new AssetPackageInstallation(PackageB, "1.0.0", [PackageA], ["assets/b.glb"]);

        var blocked = AssetPackageUninstallAnalyzer.Analyze(PackageA, [packageA, dependent], []);
        var allowed = AssetPackageUninstallAnalyzer.Analyze(
            PackageA,
            [packageA, dependent, Installation(PackageC, "1.0.0", ["assets/c.glb"])],
            [new AssetPackageReplacement(PackageA, PackageC)]);

        Assert.False(blocked.CanUninstall);
        Assert.Equal([PackageB], blocked.BlockingDependents);
        Assert.True(allowed.CanUninstall);
        Assert.Empty(allowed.BlockingDependents);
        Assert.Equal(["assets/a.glb"], allowed.BackupFiles);
    }

    private static AssetPackageInstallation Installation(
        AssetPackageManifestId id,
        string version,
        IReadOnlyList<string> files) => new(id, version, [], files);

    private static readonly AssetPackageManifestId PackageA = AssetPackageManifestId.From(
        Guid.Parse("c1100000-0000-4000-8000-000000000010"));
    private static readonly AssetPackageManifestId PackageB = AssetPackageManifestId.From(
        Guid.Parse("c1100000-0000-4000-8000-000000000020"));
    private static readonly AssetPackageManifestId PackageC = AssetPackageManifestId.From(
        Guid.Parse("c1100000-0000-4000-8000-000000000030"));
}
